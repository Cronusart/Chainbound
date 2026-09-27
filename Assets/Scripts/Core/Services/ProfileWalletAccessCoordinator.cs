using System;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Process-wide gate for profile-aware wallet access. It establishes MWA
/// authorization and signed proof only; ownership transactions remain the
/// responsibility of their calling feature.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProfileWalletAccessCoordinator : MonoBehaviour, IProfileWalletAccessCoordinator
{
    [SerializeField] private AccountSessionService accountSessionService;
    [SerializeField] private WalletConnectionPromptService promptPresenter;
    [SerializeField] private MobileControlRouter mobileControlRouter;
    [SerializeField] private bool enableDebugLogs;

    private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
    private WalletConnectionPromptService scenePromptPresenter;
    private WalletConnectionPromptService activePresenter;
    private bool routerBound;

    private void Awake()
    {
        ProfileWalletAccessRegistry.Register(this);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindMobileRouter();
    }

    private void Update()
    {
        if (!routerBound)
            BindMobileRouter();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindMobileRouter();
    }

    private void OnDestroy()
    {
        ProfileWalletAccessRegistry.Unregister(this);
        requestGate.Dispose();
    }

    public async Task<WalletAccessResult> EnsureAccessAsync(WalletAccessRequest request)
    {
        WalletAccessRequest resolved = request ?? new WalletAccessRequest();
        await requestGate.WaitAsync();

        InputLockHandle inputLock = default;
        MobileHudToken hudToken = default;
        try
        {
            inputLock = InputLockService.Instance.AcquireLock(
                $"ProfileWalletAccess.{resolved.reason}",
                InputLockChannel.All,
                this);
            hudToken = MobileHudService.Instance.AcquireContext(
                MobileHudContext.Menu,
                $"ProfileWalletAccess.{resolved.reason}");

            return await EnsureAccessInternalAsync(resolved);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return WalletAccessResult.From(WalletAccessResultKind.Failed, exception.Message);
        }
        finally
        {
            hudToken.Release();
            inputLock.Release();
            requestGate.Release();
        }
    }

    private async Task<WalletAccessResult> EnsureAccessInternalAsync(WalletAccessRequest request)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
            return WalletAccessResult.From(WalletAccessResultKind.Failed, "Account session service is unavailable.");

        PlayerAccountSession current = service.CurrentSession;
        if (current == null || !current.hasSession || string.IsNullOrWhiteSpace(current.PlayerProfileId))
            return WalletAccessResult.From(WalletAccessResultKind.Failed, "Choose an authenticated profile first.", current);

        if (service.HasVerifiedWalletConnection)
        {
            if (request.reason == WalletAccessReason.ManualHud)
                await ShowConnectedStatusAsync(current);
            return WalletAccessResult.From(WalletAccessResultKind.Ready, "Wallet connected and verified.", current);
        }

        if (current.isWalletConnected || !current.HasFreshWalletProof)
            service.ClearRuntimeWalletAuthorization();

        current = service.CurrentSession;
        if (!current.HasLinkedWallet &&
            request.reason == WalletAccessReason.ResumeProfile &&
            request.allowOfflineContinuation)
        {
            return WalletAccessResult.From(WalletAccessResultKind.ContinuedOffline, "Profile has no linked wallet.", current);
        }

        WalletConnectionPromptService presenter = ResolvePromptPresenter();
        if (presenter == null)
            return WalletAccessResult.From(WalletAccessResultKind.Failed, "Wallet access prompt is unavailable.", current);

        WalletAccessPromptChoice choice = await ShowPromptAsync(presenter, BuildInitialPrompt(current, request));
        if (choice == WalletAccessPromptChoice.ContinueOffline && request.allowOfflineContinuation)
            return WalletAccessResult.From(WalletAccessResultKind.ContinuedOffline, "Continuing without wallet authorization.", current);
        if (choice != WalletAccessPromptChoice.Connect && choice != WalletAccessPromptChoice.Retry)
            return WalletAccessResult.From(WalletAccessResultKind.Cancelled, "Wallet access was not authorized.", current);

        while (true)
        {
            PlayerAccountSession verified;
            AppLifecycleService.Instance?.BeginWalletSigning();
            try
            {
                verified = await service.ConnectWalletAndResolveSessionAsync(Application.version);
            }
            finally
            {
                AppLifecycleService.Instance?.EndWalletSigning();
            }

            if (verified != null && verified.hasSession && !verified.fetchError && service.HasVerifiedWalletConnection)
            {
                SynchronizeGameSessionContext(verified);
                return WalletAccessResult.From(WalletAccessResultKind.Ready, "Wallet connected and verified.", verified);
            }

            string error = verified != null && !string.IsNullOrWhiteSpace(verified.errorMessage)
                ? verified.errorMessage
                : "Wallet verification failed.";

            if (IsUserCancellation(error))
                return WalletAccessResult.From(WalletAccessResultKind.Cancelled, error, service.CurrentSession);

            bool wrongWallet = error.Contains("wallet_mismatch_for_profile", StringComparison.OrdinalIgnoreCase) ||
                               error.Contains("profile_wallet_mismatch", StringComparison.OrdinalIgnoreCase);
            choice = await ShowPromptAsync(presenter, BuildRetryPrompt(service.CurrentSession, error, wrongWallet));
            if (choice != WalletAccessPromptChoice.Retry && choice != WalletAccessPromptChoice.Connect)
            {
                return WalletAccessResult.From(
                    wrongWallet ? WalletAccessResultKind.WrongWallet : WalletAccessResultKind.Cancelled,
                    error,
                    service.CurrentSession);
            }
        }
    }

    private static WalletAccessPromptModel BuildInitialPrompt(PlayerAccountSession session, WalletAccessRequest request)
    {
        bool linked = session != null && session.HasLinkedWallet;
        string action = string.IsNullOrWhiteSpace(request.actionName) ? "ownership features" : request.actionName.Trim();

        if (linked)
        {
            string address = ShortAddress(session.WalletPublicKey);
            string body = request.reason == WalletAccessReason.ResumeProfile
                ? $"This profile is linked to {address}. Connect it to enable ownership features?"
                : $"{action} requires the wallet linked to this profile ({address}).";
            return new WalletAccessPromptModel
            {
                title = "Connect Linked Wallet",
                body = body,
                primaryLabel = "Connect Linked Wallet",
                primaryChoice = WalletAccessPromptChoice.Connect,
                secondaryLabel = request.allowOfflineContinuation ? "Continue Offline" : "Not Now",
                secondaryChoice = request.allowOfflineContinuation
                    ? WalletAccessPromptChoice.ContinueOffline
                    : WalletAccessPromptChoice.Cancel,
            };
        }

        return new WalletAccessPromptModel
        {
            title = "Link a Wallet",
            body = $"{action} requires a wallet. Connect and verify a wallet to link it to this profile?",
            primaryLabel = "Connect & Link Wallet",
            primaryChoice = WalletAccessPromptChoice.Connect,
            secondaryLabel = request.allowOfflineContinuation ? "Continue Offline" : "Not Now",
            secondaryChoice = request.allowOfflineContinuation
                ? WalletAccessPromptChoice.ContinueOffline
                : WalletAccessPromptChoice.Cancel,
        };
    }

    private static WalletAccessPromptModel BuildRetryPrompt(PlayerAccountSession session, string error, bool wrongWallet)
    {
        bool betaAccessRequired = !string.IsNullOrWhiteSpace(error) &&
                                  error.Contains("beta_not_allowlisted", StringComparison.OrdinalIgnoreCase);
        string body = wrongWallet && session != null && session.HasLinkedWallet
            ? $"That is not the wallet linked to this profile. Choose {ShortAddress(session.WalletPublicKey)} in Seed Vault and retry."
            : betaAccessRequired
                ? "The wallet proof succeeded, but this wallet is not approved for the closed beta."
                : "The wallet could not be verified. Retry, or return to the game.";
        return new WalletAccessPromptModel
        {
            title = wrongWallet
                ? "Wrong Wallet"
                : betaAccessRequired ? "Closed Beta Access Required" : "Wallet Verification Failed",
            body = body,
            status = MainMenuAccountStatusCopy.Resolve(error),
            primaryLabel = "Retry",
            primaryChoice = WalletAccessPromptChoice.Retry,
            secondaryLabel = "Cancel",
            secondaryChoice = WalletAccessPromptChoice.Cancel,
        };
    }

    private async Task ShowConnectedStatusAsync(PlayerAccountSession session)
    {
        WalletConnectionPromptService presenter = ResolvePromptPresenter();
        if (presenter == null)
            return;

        await ShowPromptAsync(presenter, new WalletAccessPromptModel
        {
            title = "Wallet Connected",
            body = $"This profile is linked to {ShortAddress(session.WalletPublicKey)}.",
            status = "Ownership features are enabled for this session.",
            showPrimary = false,
            secondaryLabel = "Close",
            secondaryChoice = WalletAccessPromptChoice.Close,
        });
    }

    private AccountSessionService ResolveAccountSessionService()
    {
        AccountSessionService authority = AccountSessionService.Instance;
        if (authority != null)
        {
            accountSessionService = authority;
            return accountSessionService;
        }

        if (accountSessionService == null)
            accountSessionService = FindObjectOfType<AccountSessionService>(true);
        return accountSessionService;
    }

    private WalletConnectionPromptService ResolvePromptPresenter()
    {
        if (scenePromptPresenter != null)
            return scenePromptPresenter;
        if (promptPresenter != null)
            return promptPresenter;
        if (WalletConnectionPromptService.Instance != null)
            return WalletConnectionPromptService.Instance;
        return FindObjectOfType<WalletConnectionPromptService>(true);
    }

    /// <summary>
    /// Selects a scene-authored prompt for the active scene. The serialized
    /// GameServices presenter remains the fallback for gameplay scenes that do
    /// not provide their own wallet UI.
    /// </summary>
    public void SetScenePromptPresenter(WalletConnectionPromptService presenter)
    {
        scenePromptPresenter = presenter;
    }

    private async Task<WalletAccessPromptChoice> ShowPromptAsync(
        WalletConnectionPromptService presenter,
        WalletAccessPromptModel model)
    {
        activePresenter = presenter;
        try
        {
            return await presenter.ShowAccessAsync(model);
        }
        finally
        {
            if (activePresenter == presenter)
                activePresenter = null;
        }
    }

    private void SynchronizeGameSessionContext(PlayerAccountSession session)
    {
        GameSessionContext context = GameSessionContext.Instance;
        if (context == null)
            return;

        context.SetFromAccountSession(
            session,
            context.DesiredSpawnTag,
            context.PendingQuestId,
            isOfflineSession: false,
            starterSpeciesId: context.StarterSpeciesId,
            starterBackendChainlingId: context.StarterBackendChainlingId);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scenePromptPresenter != null && scenePromptPresenter.gameObject.scene != scene)
            scenePromptPresenter = null;
        UnbindMobileRouter();
        BindMobileRouter();
    }

    private void BindMobileRouter()
    {
        if (routerBound)
            return;
        if (mobileControlRouter == null)
            mobileControlRouter = FindObjectOfType<MobileControlRouter>(true);
        if (mobileControlRouter == null)
            return;

        mobileControlRouter.BackRouted += HandleManualBackRequest;
        mobileControlRouter.WalletRouted += HandleManualWalletRequest;
        routerBound = true;
    }

    private void UnbindMobileRouter()
    {
        if (routerBound && mobileControlRouter != null)
        {
            mobileControlRouter.BackRouted -= HandleManualBackRequest;
            mobileControlRouter.WalletRouted -= HandleManualWalletRequest;
        }
        routerBound = false;
        mobileControlRouter = null;
    }

    private void HandleManualBackRequest()
    {
        WalletConnectionPromptService presenter = activePresenter != null
            ? activePresenter
            : ResolvePromptPresenter();
        if (presenter != null && presenter.IsOpen)
            presenter.TryCancel();
    }

    private async void HandleManualWalletRequest()
    {
        WalletAccessResult result = await EnsureAccessAsync(new WalletAccessRequest
        {
            reason = WalletAccessReason.ManualHud,
            actionName = "ownership features",
            requireFreshProof = true,
            allowOfflineContinuation = false,
        });

        if (enableDebugLogs)
            Debug.Log($"[ProfileWalletAccessCoordinator] Manual HUD wallet result={result.kind}", this);
    }

    private static string ShortAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "the linked wallet";
        string trimmed = address.Trim();
        return trimmed.Length <= 12 ? trimmed : $"{trimmed.Substring(0, 4)}…{trimmed.Substring(trimmed.Length - 4)}";
    }

    private static bool IsUserCancellation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        string normalized = message.ToUpperInvariant();
        return normalized.Contains("CANCEL") ||
               normalized.Contains("TIMED OUT") ||
               normalized.Contains("TIMEOUT") ||
               normalized.Contains("USER_REJECT");
    }
}
