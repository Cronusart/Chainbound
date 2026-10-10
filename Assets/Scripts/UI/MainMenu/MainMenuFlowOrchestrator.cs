using System;
using Chainbound.Combat;
using UnityEngine;

public enum MainMenuEditorWalletMode
{
    EditorMock,
    EditorBrowserBridge,
    NativeAdapterBuildOnly
}

/// <summary>
/// Account-flow adapter for main menu UI. Buttons call this class; it delegates
/// account, wallet, and profile decisions to services so UI remains passive.
/// </summary>
public class MainMenuFlowOrchestrator : MonoBehaviour
{
    private const string AccountServicesHostName = "[ChainboundAccountServices]";

    [Header("Account Services")]
    [SerializeField] private AccountSessionService accountSessionService;
    [SerializeField] private SupabasePlayerProfileGateway profileGateway;
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Providers")]
    [SerializeField] private MonoBehaviour guestIdentityProvider;
    [SerializeField] private MonoBehaviour recoverableSignInProvider;
    [SerializeField] private MonoBehaviour walletProvider;
    [SerializeField] private MonoBehaviour authSessionProvider;

    [Header("Legacy Compatibility")]
    [SerializeField] private MainMenuWalletServiceBase legacyWalletService;
    [SerializeField] private MainMenuProfileServiceBase legacyProfileService;

    [Header("Editor Defaults")]
    [SerializeField] private bool autoCreateEditorMocks = true;
    [SerializeField] private MainMenuEditorWalletMode editorWalletMode = MainMenuEditorWalletMode.EditorMock;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    public AccountSessionService AccountSessionService => ResolveAccountSessionService();
    public PlayerAccountSession CurrentSession => AccountSessionService != null ? AccountSessionService.CurrentSession : PlayerAccountSession.Empty();

    /// <summary>
    /// True when any recovery mechanism is available. False means there is nothing to
    /// restore from at all, which is a player-facing state rather than a request failure,
    /// so callers should say so instead of issuing a round trip that can only fail.
    /// </summary>
    public bool CanRestoreAccount => ResolveRestoreRoute() != AccountRestoreRoute.None;

    /// <summary>
    /// Picks the recovery mechanism this build can actually attempt. The platform
    /// policy decides the mechanism (Seeker is always wallet proof); provider
    /// availability only answers whether that attempt can proceed.
    /// </summary>
    public AccountRestoreRoute ResolveRestoreRoute()
    {
        bool identityCanRestore = ResolveRecoverableProvider() is IRecoverableSignInProvider identity
            && identity.IsAvailable
            && identity.CanRestore;
        bool walletAvailable = ResolveWalletProvider() is IWalletProvider wallet && wallet.IsAvailable;

        return AccountRestorePolicy.Resolve(
            PlatformContext.ResolveActiveBuildPlatform(),
            PlatformContext.ResolveCapabilities(),
            identityCanRestore,
            walletAvailable);
    }

    /// <summary>
    /// Restores an existing profile using whichever mechanism this build supports.
    /// Callers that need to know whether the wallet app will be opened should read
    /// <see cref="ResolveRestoreRoute"/> first.
    /// </summary>
    public void RestoreProfile(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        switch (ResolveRestoreRoute())
        {
            case AccountRestoreRoute.PlatformIdentity:
                RestoreAccount(onComplete, onError);
                return;
            case AccountRestoreRoute.Wallet:
                RestoreWalletProfile(onComplete, onError);
                return;
            default:
                onError?.Invoke("No profile recovery mechanism is available on this build.");
                return;
        }
    }

    public void ConfigureEditorWalletMode(MainMenuEditorWalletMode mode)
    {
        if (editorWalletMode == mode)
            return;

        editorWalletMode = mode;

#if UNITY_EDITOR
        if ((walletProvider is EditorMockWalletProvider && mode != MainMenuEditorWalletMode.EditorMock) ||
            (walletProvider is SolanaWalletAdapterProvider && mode == MainMenuEditorWalletMode.EditorMock))
        {
            walletProvider = null;
        }
#endif
    }

    public async void ContinueAsGuest(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession session = await service.StartGuestAsync(Application.version);
        CompleteSession(session, onComplete, onError, "ContinueAsGuest");
    }

    /// <summary>Silent menu boot: restore/create a JWT-owned anonymous profile.</summary>
    public async void BootstrapProfile(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession session = await service.BootstrapSessionAsync(Application.version);
        CompleteSession(session, onComplete, onError, "BootstrapProfile");
    }

    /// <summary>Idempotently records the player accepting New Game before onboarding begins.</summary>
    public async void BeginNewGame(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession session = await service.BeginNewGameAsync(Application.version);
        CompleteSession(session, onComplete, onError, "BeginNewGame");
    }

    public async void MarkIntroCinematicComplete(Action<PlayerAccountSession> onComplete = null, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession session = await service.SetIntroCinematicCompleteAsync();
        CompleteSession(session, onComplete, onError, "MarkIntroCinematicComplete");
    }

    public async void RestoreAccount(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession session = await service.RestoreAccountAsync(Application.version);
        CompleteSession(session, onComplete, onError, "RestoreAccount");
    }

    public async void ConnectWallet(Action<WalletConnectResult> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service != null && walletProvider is IWalletProvider)
        {
            WalletConnectResult result = await service.ConnectWalletAsync();
            if (!result.success)
            {
                onError?.Invoke(result.errorMessage);
                return;
            }

            onComplete?.Invoke(result);
            return;
        }

        if (legacyWalletService == null)
        {
            onError?.Invoke("Wallet service is not available.");
            return;
        }

        legacyWalletService.Connect(result =>
        {
            if (!result.success)
            {
                onError?.Invoke(result.errorMessage);
                return;
            }

            onComplete?.Invoke(new WalletConnectResult
            {
                success = true,
                providerType = WalletProviderType.Other,
            walletPublicKey = result.walletAddress,
            });
        });
    }

    public async void ConnectWalletAndResolveSession(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        if (!(ResolveWalletProvider() is IWalletProvider))
        {
            onError?.Invoke("Wallet provider is not available.");
            return;
        }

        PlayerAccountSession session = await service.ConnectWalletAndResolveSessionAsync(
            Application.version,
            WalletConnectMode.Resolve);
        if (session != null && !session.fetchError &&
            string.Equals(session.walletResolutionStatus, "profile_found", StringComparison.Ordinal))
        {
            session = await service.RestoreWalletProfileAsync(Application.version);
        }
        CompleteSession(session, onComplete, onError, "ConnectWalletAndResolveSession");
    }

    /// <summary>
    /// Awaitable form used by the reusable wallet prompt. A failed wallet
    /// operation leaves the anonymous profile session intact for retry/skip.
    /// </summary>
    public async System.Threading.Tasks.Task<PlayerAccountSession> ConnectWalletToCurrentProfileAsync()
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
            return PlayerAccountSession.Empty();

        return await service.ConnectWalletAndResolveSessionAsync(Application.version, WalletConnectMode.Link);
    }

    public async void CreateWalletProfileAndBeginNewGame(
        Action<PlayerAccountSession> onComplete,
        Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        PlayerAccountSession created = await service.CreateWalletProfileAsync(Application.version);
        if (created == null || !created.hasSession || created.fetchError)
        {
            onError?.Invoke(created != null ? created.errorMessage : "Wallet profile creation failed.");
            return;
        }

        PlayerAccountSession started = await service.BeginNewGameAsync(Application.version);
        CompleteSession(started, onComplete, onError, "CreateWalletProfileAndBeginNewGame");
    }

    public async System.Threading.Tasks.Task<PlayerAccountSession> RestoreWalletProfileAsync()
    {
        AccountSessionService service = ResolveAccountSessionService();
        return service != null
            ? await service.RestoreWalletProfileAsync(Application.version)
            : PlayerAccountSession.Empty();
    }

    /// <summary>
    /// Callback form of wallet recovery for menu buttons. Connects the wallet if needed,
    /// signs a challenge, and moves this install's auth identity onto the profile already
    /// bound to that wallet. Never merges guest progress into the recovered profile.
    /// </summary>
    public async void RestoreWalletProfile(Action<PlayerAccountSession> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        if (!(ResolveWalletProvider() is IWalletProvider))
        {
            onError?.Invoke("Wallet provider is not available.");
            return;
        }

        PlayerAccountSession session = await service.RestoreWalletProfileAsync(Application.version);
        CompleteSession(session, onComplete, onError, "RestoreWalletProfile");
    }

    public async void LinkWalletToCurrentProfile(Action<AccountLinkResult> onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        AccountLinkResult result = await service.LinkConnectedWalletAsync(explicitConsent: true);
        if (!result.success)
            onError?.Invoke(result.message);

        onComplete?.Invoke(result);
    }

    public async void DisconnectWallet(Action onComplete, Action<string> onError = null)
    {
        AccountSessionService service = ResolveAccountSessionService();
        if (service == null)
        {
            onError?.Invoke("Account session service is not available.");
            return;
        }

        await service.DisconnectWalletAsync();
        onComplete?.Invoke();
    }

    public void LoadLegacyWalletProfile(string walletAddress, Action<MainMenuProfileSnapshot> onComplete)
    {
        if (legacyProfileService == null)
        {
            onComplete?.Invoke(new MainMenuProfileSnapshot
            {
                hasProfile = false,
                fetchError = true,
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                walletPublicKey = walletAddress ?? "",
            });
            return;
        }

        legacyProfileService.FetchProfile(walletAddress, onComplete);
    }

    private void CompleteSession(
        PlayerAccountSession session,
        Action<PlayerAccountSession> onComplete,
        Action<string> onError,
        string flowName)
    {
        // A previously authenticated profile may continue through the limited
        // offline path. Fresh creation/import is still rejected by the service.
        if (session == null || !session.hasSession)
        {
            string message = session != null ? session.errorMessage : "Session was empty.";
            if (enableDebugLogs)
                Debug.LogWarning($"[MainMenuFlowOrchestrator] {flowName} failed: {message}");
            onError?.Invoke(message);
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[MainMenuFlowOrchestrator] {flowName} profile={session.PlayerProfileId} platform={session.activePlatformBuild}");

        onComplete?.Invoke(session);
    }

    private AccountSessionService ResolveAccountSessionService()
    {
        AccountSessionService authority = AccountSessionService.Instance;
        if (authority != null)
            accountSessionService = authority;

        if (accountSessionService == null)
            accountSessionService = GetComponent<AccountSessionService>();
        if (accountSessionService == null)
            accountSessionService = FindObjectOfType<AccountSessionService>();
        if (accountSessionService == null)
        {
            GameObject host = ResolveAccountServicesHost();
            accountSessionService = host.GetComponent<AccountSessionService>();
            if (accountSessionService == null)
                accountSessionService = host.AddComponent<AccountSessionService>();
        }

        EnsureWalletOperationGuard(accountSessionService.gameObject);

        accountSessionService.Configure(
            ResolveGuestProvider(),
            ResolveRecoverableProvider(),
            ResolveWalletProvider(),
            ResolveGateway(),
            ResolveAuthSessionProvider(),
            supabaseConfig);

        return accountSessionService;
    }

    private MonoBehaviour ResolveGateway()
    {
        SupabasePlayerProfileGateway discovered = null;
        if (profileGateway == null)
            discovered = GetComponent<SupabasePlayerProfileGateway>();
        if (profileGateway == null && discovered == null)
            discovered = FindObjectOfType<SupabasePlayerProfileGateway>();

        if (supabaseConfig == null && discovered != null)
            supabaseConfig = discovered.Config;

        if (profileGateway == null)
        {
            GameObject host = accountSessionService != null ? accountSessionService.gameObject : ResolveAccountServicesHost();
            profileGateway = host.GetComponent<SupabasePlayerProfileGateway>();
            if (profileGateway == null)
                profileGateway = host.AddComponent<SupabasePlayerProfileGateway>();
        }

        profileGateway.Configure(supabaseConfig);
        if (supabaseConfig == null)
            supabaseConfig = profileGateway.Config;
        return profileGateway;
    }

    private MonoBehaviour ResolveGuestProvider()
    {
        if (guestIdentityProvider != null)
            return guestIdentityProvider;

        GameObject host = accountSessionService != null ? accountSessionService.gameObject : gameObject;
        guestIdentityProvider = host.GetComponent<GuestIdentityProvider>();
        if (guestIdentityProvider == null && autoCreateEditorMocks)
            guestIdentityProvider = host.AddComponent<GuestIdentityProvider>();

        return guestIdentityProvider;
    }

    private MonoBehaviour ResolveRecoverableProvider()
    {
        DiscardEditorOnlyProvider(ref recoverableSignInProvider, nameof(recoverableSignInProvider));
        if (recoverableSignInProvider != null)
            return recoverableSignInProvider;

#if UNITY_EDITOR
        if (autoCreateEditorMocks)
        {
            GameObject host = accountSessionService != null ? accountSessionService.gameObject : gameObject;
            recoverableSignInProvider = host.GetComponent<EditorMockIdentityProvider>();
            if (recoverableSignInProvider == null)
                recoverableSignInProvider = host.AddComponent<EditorMockIdentityProvider>();
        }
#endif

        return recoverableSignInProvider;
    }

    private MonoBehaviour ResolveAuthSessionProvider()
    {
        if (authSessionProvider != null)
            return authSessionProvider;

        GameObject host = accountSessionService != null ? accountSessionService.gameObject : ResolveAccountServicesHost();
        authSessionProvider = host.GetComponent<SupabaseAnonymousAuthSessionProvider>();
        if (authSessionProvider == null)
            authSessionProvider = host.AddComponent<SupabaseAnonymousAuthSessionProvider>();
        return authSessionProvider;
    }

    private MonoBehaviour ResolveWalletProvider()
    {
        DiscardEditorOnlyProvider(ref walletProvider, nameof(walletProvider));
        if (walletProvider != null)
            return walletProvider;

#if !UNITY_EDITOR
        return ResolveSolanaWalletProvider();
#endif

#if UNITY_EDITOR
        switch (editorWalletMode)
        {
            case MainMenuEditorWalletMode.EditorBrowserBridge:
                return ResolveSolanaWalletProvider();
            case MainMenuEditorWalletMode.NativeAdapterBuildOnly:
                if (enableDebugLogs)
                    Debug.LogWarning("[MainMenuFlowOrchestrator] Native mobile wallet adapter requires an Android/iOS player build. Use EditorBrowserBridge for real-wallet Play Mode testing.", this);
                return null;
            default:
                if (autoCreateEditorMocks)
                {
                    GameObject host = accountSessionService != null ? accountSessionService.gameObject : gameObject;
                    walletProvider = host.GetComponent<EditorMockWalletProvider>();
                    if (walletProvider == null)
                        walletProvider = host.AddComponent<EditorMockWalletProvider>();
                }
                break;
        }
#endif

        return walletProvider;
    }

    /// <summary>
    /// Drops a serialized Editor-only mock when running in a player build. Scenes wire
    /// mocks for Play Mode convenience; honouring that reference on device would
    /// silently connect a fabricated wallet/identity instead of reaching the Mobile
    /// Wallet Adapter. Clearing the field lets the caller's existing null check fall
    /// through to the real platform provider.
    /// </summary>
    private void DiscardEditorOnlyProvider(ref MonoBehaviour provider, string fieldName)
    {
        if (provider == null || Application.isEditor || !(provider is IEditorOnlyAccountProvider))
            return;

        Debug.LogWarning(
            $"[MainMenuFlowOrchestrator] '{fieldName}' is wired to Editor-only mock " +
            $"'{provider.GetType().Name}', which cannot be used in a player build. " +
            "Falling back to the real platform provider. Clear this reference in the scene.",
            this);

        provider = null;
    }

    private MonoBehaviour ResolveSolanaWalletProvider()
    {
        GameObject host = accountSessionService != null ? accountSessionService.gameObject : ResolveAccountServicesHost();
        walletProvider = host.GetComponent<SolanaWalletAdapterProvider>();
        if (walletProvider == null)
            walletProvider = host.AddComponent<SolanaWalletAdapterProvider>();
        return walletProvider;
    }

    private static GameObject ResolveAccountServicesHost()
    {
        AccountSessionService existing = AccountSessionService.Instance;
        if (existing == null)
            existing = FindObjectOfType<AccountSessionService>();
        if (existing != null)
            return existing.gameObject;

        GameObject host = GameObject.Find(AccountServicesHostName);
        if (host == null)
            host = new GameObject(AccountServicesHostName);

        DontDestroyOnLoad(host);
        return host;
    }

    private static void EnsureWalletOperationGuard(GameObject host)
    {
        if (host == null)
            return;

        if (host.GetComponent<WalletOperationGuard>() == null)
            host.AddComponent<WalletOperationGuard>();
    }
}
