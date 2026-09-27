using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SignalJam.UI.MobileControls;
using SignalJam.UI.MenuNavigation;
using Chainbound.Combat;

/// <summary>
/// Persistent process-lifecycle coordinator. OS focus loss is treated as a
/// recoverable interrupt; it never resets a battle, session, or wallet state.
/// </summary>
[DisallowMultipleComponent]
public sealed class AppLifecycleService : MonoBehaviour
{
    private static AppLifecycleService instance;
    public static AppLifecycleService Instance => instance != null ? instance : FindObjectOfType<AppLifecycleService>(true);

    [SerializeField] private AppInterruptPolicySO policy;
    [SerializeField] private AppInterruptOverlayController overlay;
    [SerializeField] private bool simulateInEditor;
    [SerializeField] private KeyCode simulateInterruptKey = KeyCode.F9;
    [SerializeField] private bool logTransitions;

    public AppInterruptContext CurrentContext { get; private set; } = AppInterruptContext.MainMenu;
    public bool IsInterrupted { get; private set; }
    public bool IsWalletSigning { get; private set; }
    public event Action<AppInterruptContext> Interrupted;
    public event Action Resumed;
    public event Action WalletSigningTimedOut;

    private InputLockHandle inputLock;
    private bool hasInputLock;
    private bool mutedByLifecycle;
    private float previousTimeScale = 1f;
    private bool previousTimeScaleStored;
    private Coroutine walletTimeoutRoutine;
    private AppInterruptContext contextBeforeWalletSigning = AppInterruptContext.Overworld;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        if (overlay != null)
        {
            overlay.ContinueRequested += RequestContinue;
            overlay.FleeRequested += RequestFlee;
            overlay.CancelRequested += RequestWalletCancel;
            overlay.Hide();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (overlay != null)
        {
            overlay.ContinueRequested -= RequestContinue;
            overlay.FleeRequested -= RequestFlee;
            overlay.CancelRequested -= RequestWalletCancel;
        }
        if (instance == this) instance = null;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (simulateInEditor && Input.GetKeyDown(simulateInterruptKey))
            HandleInterruptionSignal("editor_simulation");
#endif
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string name = (scene.name ?? string.Empty).ToLowerInvariant();
        if (name.Contains("combat") || name.Contains("battle")) CurrentContext = AppInterruptContext.CombatPvE;
        else if (name.Contains("menu")) CurrentContext = AppInterruptContext.MainMenu;
        else if (name.Contains("load")) CurrentContext = AppInterruptContext.Loading;
        else CurrentContext = AppInterruptContext.Overworld;
    }

    public void SetContext(AppInterruptContext context) => CurrentContext = context;

    public void BeginWalletSigning()
    {
        if (!IsWalletSigning)
            contextBeforeWalletSigning = CurrentContext;
        IsWalletSigning = true;
        CurrentContext = AppInterruptContext.WalletSigning;
        if (!hasInputLock)
        {
            inputLock = InputLockService.Instance.AcquireLock("AppLifecycle.WalletSigning", InputLockChannel.All, this);
            hasInputLock = true;
        }
        if (walletTimeoutRoutine != null) StopCoroutine(walletTimeoutRoutine);
        walletTimeoutRoutine = StartCoroutine(WalletTimeoutRoutine());
    }

    public void EndWalletSigning()
    {
        IsWalletSigning = false;
        if (CurrentContext == AppInterruptContext.WalletSigning)
            CurrentContext = contextBeforeWalletSigning;
        if (walletTimeoutRoutine != null) { StopCoroutine(walletTimeoutRoutine); walletTimeoutRoutine = null; }
        if (IsInterrupted) CompleteInterrupt();
        else if (hasInputLock) { inputLock.Release(); hasInputLock = false; }
    }

    private IEnumerator WalletTimeoutRoutine()
    {
        float timeout = policy != null ? policy.WalletTimeoutSeconds : 120f;
        yield return new WaitForSecondsRealtime(timeout);
        if (IsWalletSigning)
        {
            Debug.LogWarning("[AppLifecycleService] Wallet signing timed out; waiting operation remains recoverable.");
            WalletSigningTimedOut?.Invoke();
            RequestWalletCancel();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) HandleInterruptionSignal("application_pause");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) HandleInterruptionSignal("application_focus_lost");
        else { /* resume is explicit through the overlay, except wallet completion */ }
    }

    private void HandleInterruptionSignal(string reason)
    {
#if UNITY_EDITOR
        if (policy != null && policy.IgnoreOsInterruptsInEditor && !simulateInEditor && reason != "editor_simulation") return;
#endif
        if (IsInterrupted) return;
        // Launching Seed Vault intentionally backgrounds Unity. Wallet authorization
        // already owns the input lock and timeout; showing a second interruption modal
        // creates a false "waiting" step after the wallet has completed.
        if (IsWalletSigning || IsWalletOperationActive()) return;
        AppInterruptContext context = IsWalletSigning || IsWalletOperationActive() ? AppInterruptContext.WalletSigning : CurrentContext;
        PersistentPlayerMenuHost menuHost = FindObjectOfType<PersistentPlayerMenuHost>(true);
        if (context != AppInterruptContext.WalletSigning && menuHost != null && menuHost.IsMenuOpen)
            context = AppInterruptContext.PlayerMenu;
        CurrentContext = context;
        AppInterruptPolicy resolved = AppInterruptPolicyResolver.Resolve(policy, context);
        if (!resolved.PauseTimeScale && !resolved.MuteAudio && !resolved.LockInput && resolved.Overlay == AppInterruptOverlayMode.None)
            return;
        IsInterrupted = true;

        if (resolved.PauseTimeScale)
        {
            previousTimeScale = Time.timeScale;
            previousTimeScaleStored = true;
            Time.timeScale = 0f;
        }
        if (resolved.MuteAudio && AudioService.Instance != null)
        {
            AudioService.Instance.SetLifecycleMute(true);
            mutedByLifecycle = true;
        }
        if (resolved.LockInput)
        {
            if (!hasInputLock)
            {
                inputLock = InputLockService.Instance.AcquireLock("AppLifecycle", InputLockChannel.All, this);
                hasInputLock = true;
            }
        }
        if (overlay != null && resolved.Overlay != AppInterruptOverlayMode.None)
        {
            string title = context == AppInterruptContext.WalletSigning ? "Waiting for Seed Vault…" : "Game Paused";
            string body = context == AppInterruptContext.WalletSigning ? "Approve the wallet request, or cancel to return." : "Your progress is safe. Continue when ready.";
            AppInterruptOverlayMode overlayMode = resolved.Overlay;
            if (overlayMode == AppInterruptOverlayMode.ContinueAndFlee)
            {
                CombatBattleController battle = FindObjectOfType<CombatBattleController>(true);
                if (battle != null && !battle.CanPlayerFlee)
                {
                    overlayMode = AppInterruptOverlayMode.ContinueOnly;
                    body = "This encounter cannot be fled. Continue when ready.";
                }
            }
            overlay.Show(overlayMode, title, body);
        }
        if (logTransitions) Debug.Log($"[AppLifecycleService] Interrupted ({reason}) context={context}");
        Interrupted?.Invoke(context);
    }

    public void RequestContinue()
    {
        if (!IsInterrupted || IsWalletSigning) return;
        CompleteInterrupt();
    }

    public void RequestFlee()
    {
        if (!IsInterrupted) return;
        CombatBattleController battle = FindObjectOfType<CombatBattleController>(true);
        if (battle == null)
        {
            CompleteInterrupt();
            return;
        }

        FleeRequestResult result = battle.RequestFlee(FleeRequestSource.AppLifecycle);
        if (result.Accepted)
            CompleteInterrupt();
        else
            Debug.Log($"[AppLifecycleService] Flee request denied: {result.Message}");
    }

    public void RequestWalletCancel()
    {
        WalletConnectionPromptService prompt = FindObjectOfType<WalletConnectionPromptService>(true);
        if (prompt != null) prompt.TryCancel();
        if (IsWalletSigning) EndWalletSigning();
        if (IsInterrupted) CompleteInterrupt();
    }

    private void CompleteInterrupt()
    {
        IsInterrupted = false;
        if (overlay != null) overlay.Hide();
        if (hasInputLock) { inputLock.Release(); hasInputLock = false; }
        if (mutedByLifecycle && AudioService.Instance != null) { AudioService.Instance.SetLifecycleMute(false); mutedByLifecycle = false; }
        if (previousTimeScaleStored)
        {
            Time.timeScale = previousTimeScale;
            previousTimeScaleStored = false;
        }
        if (SolanaServiceBridge.Instance != null) SolanaServiceBridge.Instance.ResumePendingOperations();
        Resumed?.Invoke();
    }

    private bool IsWalletOperationActive()
    {
        SolanaServiceBridge bridge = SolanaServiceBridge.Instance;
        return bridge != null && bridge.WalletServiceInstance != null && bridge.WalletServiceInstance.IsWalletUiActive;
    }
}
