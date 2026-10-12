using System;
using System.Collections;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Main menu scripted flow controller (V2 — polished animations):
/// splash -> reveal -> authenticated profile bootstrap -> profile-based play routing.
///
/// Same wiring as V1 but adds:
///   - UiSelectableMotion sprite-swap + focus/pulse/shimmer (per-button)
///   - UiSfxRouter for navigate/confirm SFX
///   - Screen brightness flick (contrast overlay alpha pulse)
///   - InputSystem UI integration + Cancel navigation
///
/// Inspector headers are ordered to match the on-screen sequence.
/// </summary>
public class MainMenuSequenceController2 : MonoBehaviour
{
    private const string DefaultIntroSequenceStartSceneName = "player_room";
    private const string DefaultPostIntroStartSceneName = "player_room";
    private const string DefaultPostIntroSpawnId = "player_room_start";

    // ── 1. Splash ────────────────────────────────────────────────────────────

    [Header("1 — Splash")]
    [SerializeField] private CanvasGroup splashGroup;
    [Tooltip("Hold duration at start before splash fades in (e.g. black screen).")]
    [SerializeField] private float splashHoldBeforeFadeInSeconds = 0f;
    [SerializeField] private float splashFadeInSeconds = 2f;
    [SerializeField] private float splashHoldSeconds = 2f;
    [SerializeField] private float splashFadeOutSeconds = 3f;

    // ── 2. Background ────────────────────────────────────────────────────────

    [Header("2 — Background")]
    [SerializeField] private CanvasGroup backgroundGroup;
    [Tooltip("Optional: frame animator for the background panel.")]
    [SerializeField] private UIImageFrameAnimator backgroundAnimator;
    [Tooltip("Shared fade duration for the background, object layers, foreground, vinyl, logo, and mobile HUD. All layers use one clock so no region appears empty while the others catch up.")]
    [SerializeField] private float backgroundFadeInSeconds = 0.45f;

    // ── 3. Foreground ────────────────────────────────────────────────────────

    [Header("3 — Foreground")]
    [SerializeField] private CanvasGroup foregroundGroup;
    [SerializeField] private RectTransform foregroundTransform;
    [Tooltip("Shared motion root containing both the static Foreground and animated FGVinyl layers. Falls back to foregroundTransform for legacy scenes.")]
    [SerializeField] private RectTransform foregroundMotionRoot;
    [Tooltip("Optional: frame animator for the foreground panel.")]
    [SerializeField] private UIImageFrameAnimator foregroundAnimator;
    [Tooltip("Y offset below target where the foreground motion root starts.")]
    [SerializeField] private float foregroundStartOffsetY = 360f;
    [Tooltip("Foreground upward movement speed in authored canvas pixels per second.")]
    [SerializeField, Min(1f)] private float foregroundMoveSpeedY = 376f;

    // ── 4. Game Logo ─────────────────────────────────────────────────────────

    [Header("4 — Game Logo")]
    [SerializeField] private RectTransform gameLogoTransform;
    [SerializeField] private CanvasGroup gameLogoGroup;
    [SerializeField] private float logoStartOffsetY = 280f;
    [Tooltip("Game-logo downward movement speed in authored canvas pixels per second.")]
    [SerializeField, Min(1f)] private float logoMoveSpeedY = 120f;
    [Tooltip("How long before the logo finishes moving the mobile HUD should start falling into place.")]
    [SerializeField, Min(0f)] private float hudAppearBeforeLogoSettleSeconds = 0.35f;
    [Tooltip("Seconds used to fade the Chainbound logo away after any button is pressed.")]
    [SerializeField, Min(0.01f)] private float logoDismissFadeOutSeconds = 1.25f;

    [Header("4b — Press Start Prompt")]
    [Tooltip("Optional authored TMP under GameLogo. Created at runtime when empty.")]
    [SerializeField] private TMP_Text pressStartPromptText;
    [SerializeField, Min(0f)] private float pressStartDelayAfterSettleSeconds = 0.5f;
    [SerializeField, Min(0.05f)] private float pressStartFlashOnSeconds = 0.4f;
    [SerializeField, Min(0.05f)] private float pressStartFlashOffSeconds = 0.4f;
    [SerializeField] private string pressStartMessage = "PRESS ANY BUTTON";
    [SerializeField] private Vector2 pressStartAnchoredPosition = new Vector2(0f, -92f);
    [SerializeField] private float pressStartFontSize = 28f;

    // ── 5. Contrast Overlay ──────────────────────────────────────────────────

    [Header("5 — Contrast Overlay")]
    [SerializeField] private CanvasGroup contrastOverlayGroup;
    [SerializeField] private float overlayFadeInSeconds = 0.3f;

    // ── 6. Connect Buttons ───────────────────────────────────────────────────

    [Header("6 — Connect Buttons")]
    [SerializeField] private CanvasGroup connectSettingsGroup;
    [SerializeField, Tooltip("Legacy scene reference; hidden by the profile-first menu.")] private Button continueAsGuestButton;
    [SerializeField] private Button connectWalletButton;
    [SerializeField] private Button restoreAccountButton;
    [SerializeField] private Button settingsButton;
    [Tooltip("Duration of the connect-settings group fade-in.")]
    [SerializeField] private float connectButtonsFadeInSeconds = 0.22f;
    [Tooltip("Duration of the connect-settings group fade-out when swapping to the Play button.")]
    [SerializeField] private float connectButtonsFadeOutSeconds = 0.15f;
    [Tooltip("Duration of each button pop-in animation (runs after the group has fully faded in).")]
    [SerializeField] private float buttonPopSeconds = 0.2f;
    [Tooltip("Delay before the second button (Settings) starts its pop animation.")]
    [SerializeField] private float buttonStaggerSeconds = 0.06f;

    // ── 7. Play Button ───────────────────────────────────────────────────────

    [Header("7 — Play Button")]
    [SerializeField] private CanvasGroup playButtonGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button linkWalletButton;
    [Tooltip("Optional: Disconnect button below Play. Leave null until wired up.")]
    [SerializeField] private Button disconnectButton;
    [Tooltip("Duration of the play button group fade-in.")]
    [SerializeField] private float playButtonFadeInSeconds = 0.22f;

    // ── 8. Motion & SFX ─────────────────────────────────────────────────────

    [Header("8 — Motion & SFX")]
    [SerializeField] private UiSfxRouter sfxRouter;

    [Header("8b — Menu Music")]
    [Tooltip("Vinyl needle scratch SFX that plays right as the splash logo appears, before the music starts.")]
    [SerializeField] private SoundEventSO vinylNeedleSfx;
    [Tooltip("Main menu music SoundEvent. Played immediately after the vinyl needle SFX.")]
    [SerializeField] private SoundEventSO menuMusicEvent;
    [Tooltip("Seconds to fade music out when the player hits Play.")]
    [SerializeField] private float musicFadeOutSeconds = 2f;
    [Tooltip("Short guaranteed fade used before a scene route that skips the intro cinematic. The route waits for this handoff so menu and scene music cannot overlap.")]
    [SerializeField, Min(0f)] private float directRouteMusicFadeOutSeconds = 0.5f;

    // ── 9. Settings Panel ────────────────────────────────────────────────────

    [Header("9 — Settings Panel")]
    [SerializeField] private SettingsPanelController settingsPanel;

    [Header("Object Navigation Refactor")]
    [SerializeField] private CanvasGroup objectLayersGroup;
    [SerializeField] private CanvasGroup foregroundVinylGroup;
    [SerializeField] private CanvasGroup bubbleButtonsGroup;
    [SerializeField] private CanvasGroup mobileHudRootGroup;
    [SerializeField] private SyncedOutlineFrameAnimator[] objectFrameAnimators;
    [SerializeField] private MainMenuObjectNavigator objectNavigator;
    [SerializeField] private MainMenuObjectHotspot pcDeskHotspot;
    [SerializeField] private MainMenuObjectHotspot bonkDogHotspot;
    [SerializeField] private MainMenuObjectHotspot xmasTreeHotspot;
    [SerializeField] private MainMenuObjectHotspot vinylHotspot;

    [Header("Object Activation Reveal")]
    [Tooltip("Time used to scale each object from its authored size to the activation size.")]
    [SerializeField, Min(0.01f)] private float objectRevealScaleUpSeconds = 0.2f;
    [Tooltip("Multiplier applied to each object's authored visual scale during activation.")]
    [SerializeField, Min(1f)] private float objectRevealScale = 1.12f;
    [Tooltip("Peak alpha of each authored object fill sprite.")]
    [SerializeField, Range(0f, 1f)] private float objectRevealFillAlpha = 0.55f;
    [Tooltip("Time used to fade the fill away while the object returns to its authored scale.")]
    [SerializeField, Min(0.01f)] private float objectRevealFadeOutSeconds = 0.65f;
    [Tooltip("Fade duration for object outline frames before New Game / Continue routing starts.")]
    [SerializeField, Min(0f)] private float outlineFadeOutBeforeRouteSeconds = 0.35f;

    [SerializeField] private BonkDogPanelController bonkDogPanel;
    [SerializeField] private PCDeskPanelController pcDeskPanel;
    [SerializeField] private EventsPanelController eventsPanel;

    // ── 10. Overlay Flick ────────────────────────────────────────────────────

    [Header("10 — Overlay Flick")]
    [Tooltip("Peak alpha of the contrast overlay pulse.")]
    [SerializeField] private float overlayFlickPeakAlpha = 0.4f;
    [Tooltip("Total duration of the overlay alpha pulse.")]
    [SerializeField] private float overlayFlickDuration = 0.35f;

    // ── 10. Input ────────────────────────────────────────────────────────────

    [Header("10 — Input")]
    [Tooltip("Assign the project InputActionAsset (InputSystem_Actions). The UI map's Navigate, Submit, and Cancel actions drive UI navigation and back handling.")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Optional shared menu input controller. When assigned, it owns UI module setup, selection recovery, explicit navigation, and B/cancel routing.")]
    [SerializeField] private MenuInputContextController menuInputController;

    // ── Services ─────────────────────────────────────────────────────────────

    [Header("Services")]
    [SerializeField] private MainMenuFlowOrchestrator flowOrchestrator;
    [SerializeField] private MainMenuWalletServiceBase walletService;
    [SerializeField] private MainMenuProfileServiceBase profileService;
    [SerializeField] private WalletConnectionPromptService walletPromptService;
    [SerializeField] private WalletPromptDefinitionSO mainMenuWalletPrompt;
    [Tooltip("EditorMock is the fastest Supabase session smoke test. EditorBrowserBridge uses a real browser extension signature in Play Mode. NativeAdapterBuildOnly intentionally requires an Android/iOS runtime build.")]
    [SerializeField] private MainMenuEditorWalletMode editorWalletMode = MainMenuEditorWalletMode.EditorMock;

    // ── Routing ───────────────────────────────────────────────────────────────

    [Header("Routing")]
    [SerializeField] private string introSequenceStartSceneName = DefaultIntroSequenceStartSceneName;
    [SerializeField] private string postIntroStartSceneName = DefaultPostIntroStartSceneName;
    [Tooltip("SpawnPoint id used for direct post-intro login. Leave as player_room_start to enter the same room position as the intro sequence without starting the intro narrative.")]
    [SerializeField] private string postIntroSpawnId = DefaultPostIntroSpawnId;
    [Tooltip("Intro sequence to play when routed to intro path (e.g. chainbound_intro_sequence). Sprite cinematic, load scene, dialogue, etc. run as steps.")]
    [SerializeField] private SequenceAsset introSequence;
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.FullLoading;
    [SerializeField] private RouteMode routeMode = RouteMode.AutoFromProfile;

    // ── Events ────────────────────────────────────────────────────────────────

    [Header("Events")]
    [SerializeField] private UnityEvent onSettingsRequested;
    [SerializeField] private UnityEvent onWalletConnected;
    [SerializeField] private UnityEvent onPlayRequested;

    // ── Debug ─────────────────────────────────────────────────────────────────

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [Tooltip("When profile bootstrap fails (offline / no Supabase), still allow New Game to start the intro cinematic for local animation testing.")]
    [SerializeField] private bool enableOfflineNewGameTest = true;

    // ─────────────────────────────────────────────────────────────────────────

    public enum RouteMode
    {
        AutoFromProfile,
        ForceNewGame,
        ForceStartedIntroIncomplete,
        ForceStartedIntroComplete
    }

    private enum PlayRoute
    {
        IntroPath,
        PostIntroPath
    }

    private enum MenuState
    {
        Boot,
        LogoGate,
        ConnectGroup,
        Connecting,
        FetchingProfile,
        PlayGroup,
        ObjectNavigation,
        ObjectPanel,
        Settings,
        Routing
    }

    private struct RouteDecision
    {
        public PlayRoute route;
        public string targetSceneName;
    }

    private Vector2 _foregroundTargetPosition;
    private Vector2 _logoTargetPosition;
    private RectTransform _primaryActionSafeArea;
    private Rect _lastPrimaryActionSafeArea;
    private Vector2 _lastPrimaryActionScreenSize;

    private Vector3 _continueAsGuestButtonScale;
    private Vector3 _connectWalletButtonScale;
    private Vector3 _restoreAccountButtonScale;
    private Vector3 _settingsButtonScale;
    private Vector3 _playButtonScale;
    private Vector3 _linkWalletButtonScale;

    private bool _walletConnectInFlight;
    private bool _accountFlowInFlight;
    private bool _playInFlight;
    private bool _routeReady;
    private RouteDecision _routeDecision;

    // Stored after a successful wallet+profile round-trip; consumed by PlayRouteCoroutine
    private MainMenuProfileSnapshot _cachedSnapshot;
    private string _cachedWalletAddress = "";

    private MenuState _menuState = MenuState.Boot;
    private InputAction _cancelAction;
    private bool _backTransitionInFlight;
    private bool _bootstrapComplete;
    private bool _bootstrapFailed;
    private bool _startingNewGame;
    private bool _skipNewGameWalletPrompt;
    private bool _introAbortArmed;
    private string _profileServiceError = "";
    private AudioHandle _menuMusicHandle;
    private float _menuMusicStartVolume = 1f;
    private Coroutine _menuMusicCreepCoroutine;
    private Coroutine _pressStartPromptCoroutine;
    private readonly InputSystemAnyButtonGate _menuStartGate = new InputSystemAnyButtonGate();
    private bool _menuStartRequested;
    private bool _sceneRevealGateOpen;
    private SceneLoader _sceneLoader;
    private MobileHudToken _splashHudToken;
    private bool _hasSplashHudToken;
    private bool _splashCleared;
    private bool _hudButtonAudioSuppressed;

    public bool IsAwaitingMenuStart => _menuState == MenuState.LogoGate && !_menuStartRequested;
    public bool IsObjectMenuInteractive => _menuState == MenuState.ObjectNavigation;
    public float GameLogoAlpha => gameLogoGroup != null ? gameLogoGroup.alpha : 0f;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (_hudButtonAudioSuppressed)
            return;

        HudButtonAudioGate.Suppress();
        _hudButtonAudioSuppressed = true;
    }

    private void OnDisable()
    {
        if (!_hudButtonAudioSuppressed)
            return;

        HudButtonAudioGate.Release();
        _hudButtonAudioSuppressed = false;
    }

    private void Awake()
    {
        if (objectNavigator == null)
        {
            EnsureRestoreProfileButton();
            EnsurePrimaryActionSafeAreaContainer();
        }

        if (mobileHudRootGroup == null)
        {
            GameObject mobileHudCanvas = GameObject.Find("MobileHudCanvas");
            if (mobileHudCanvas != null)
                mobileHudRootGroup = mobileHudCanvas.GetComponent<CanvasGroup>();
        }

        RectTransform foregroundMover = ResolveForegroundMover();
        if (foregroundMover != null)
            _foregroundTargetPosition = foregroundMover.anchoredPosition;

        if (gameLogoTransform != null)
            _logoTargetPosition = gameLogoTransform.anchoredPosition;

        if (continueAsGuestButton != null) _continueAsGuestButtonScale = continueAsGuestButton.transform.localScale;
        if (connectWalletButton != null)   _connectWalletButtonScale   = connectWalletButton.transform.localScale;
        if (restoreAccountButton != null)  _restoreAccountButtonScale  = restoreAccountButton.transform.localScale;
        if (settingsButton != null)        _settingsButtonScale        = settingsButton.transform.localScale;
        if (playButton != null)            _playButtonScale            = playButton.transform.localScale;
        if (linkWalletButton != null)      _linkWalletButtonScale      = linkWalletButton.transform.localScale;

        if (continueAsGuestButton != null) continueAsGuestButton.gameObject.SetActive(false);
        if (connectWalletButton != null) connectWalletButton.onClick.AddListener(OnNewGamePressed);
        if (restoreAccountButton != null) restoreAccountButton.onClick.AddListener(OnRestoreAccountPressed);
        if (settingsButton != null)      settingsButton.onClick.AddListener(OnSettingsPressed);
        if (playButton != null)          playButton.onClick.AddListener(OnPlayPressed);
        // Never hide New Game when the legacy link slot still points at the same button.
        if (linkWalletButton != null && linkWalletButton != connectWalletButton)
            linkWalletButton.gameObject.SetActive(false);
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
        if (menuInputController != null) menuInputController.OnCancel.AddListener(HandleMenuCancelRequested);

        AcquireSplashHudSuppression();
        ApplyInitialVisualState();
        if (menuInputController == null)
        {
            ConfigureExplicitNavigation();
            SetupInputActions();
        }
    }

    private void Start()
    {
        _sceneLoader = SceneLoader.Instance;
        _sceneRevealGateOpen = _sceneLoader == null ||
                               !_sceneLoader.IsTransitioning ||
                               !string.Equals(
                                   _sceneLoader.CurrentStatus.SceneId,
                                   gameObject.scene.name,
                                   System.StringComparison.OrdinalIgnoreCase);
        if (_sceneLoader != null)
            _sceneLoader.BeforeReveal += HandleSceneBeforeReveal;

        AppLifecycleService lifecycle = AppLifecycleService.Instance;
        if (lifecycle != null)
            lifecycle.WalletSigningTimedOut += HandleWalletSigningTimedOut;

        // Awake ordering can let a hotspot restore its authored outline tint
        // after ApplyInitialVisualState. Reassert the logo-gate visual lock
        // once every component has finished Awake, before any reveal fades.
        objectNavigator?.SetHotspotsInteractable(false);
        objectNavigator?.SetOutlineVisualsEnabled(false);
        SetObjectFillsImmediate(0f, false);

        if (menuInputController == null)
            ConfigureUIInputModule();

        StartCoroutine(BootSequenceCoroutine());
    }

    private void Update()
    {
        if (_primaryActionSafeArea == null)
            return;

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize != _lastPrimaryActionScreenSize || Screen.safeArea != _lastPrimaryActionSafeArea)
            ApplyPrimaryActionSafeArea();
    }

    private void OnDestroy()
    {
        // Menu music is played by the persistent AudioService, so unloading the
        // menu scene does not destroy its AudioSource. This is the final safety
        // net for every route, including authored/test sequences that load a
        // scene without passing through the normal Play-button handoff.
        StopMenuMusicImmediate();
        DisarmIntroAbortRecovery();

        if (_sceneLoader != null)
            _sceneLoader.BeforeReveal -= HandleSceneBeforeReveal;

        AppLifecycleService lifecycle = AppLifecycleService.Instance;
        if (lifecycle != null)
            lifecycle.WalletSigningTimedOut -= HandleWalletSigningTimedOut;

        if (connectWalletButton != null) connectWalletButton.onClick.RemoveListener(OnNewGamePressed);
        if (restoreAccountButton != null) restoreAccountButton.onClick.RemoveListener(OnRestoreAccountPressed);
        if (settingsButton != null)      settingsButton.onClick.RemoveListener(OnSettingsPressed);
        if (playButton != null)          playButton.onClick.RemoveListener(OnPlayPressed);
        if (menuInputController != null) menuInputController.OnCancel.RemoveListener(HandleMenuCancelRequested);

        StopPressStartPrompt();
        StopMenuMusicCreepCoroutine();
        _menuStartGate.Dispose();
        TeardownInputActions();
        KillTweensOnTargets();
        ReleaseSplashHudSuppression();
    }

    private void HandleWalletSigningTimedOut()
    {
        if (_accountFlowInFlight)
            OnAccountFlowFailed("Wallet request timed out. You can retry without losing this profile.");
        if (_walletConnectInFlight)
        {
            _walletConnectInFlight = false;
            SetConnectButtonsInteractable(true);
            _menuState = MenuState.ConnectGroup;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        introSequenceStartSceneName = NormalizeSceneName(
            introSequenceStartSceneName,
            DefaultIntroSequenceStartSceneName);
        postIntroStartSceneName = NormalizeSceneName(
            postIntroStartSceneName,
            DefaultPostIntroStartSceneName);
        postIntroSpawnId = NormalizeSceneName(
            postIntroSpawnId,
            DefaultPostIntroSpawnId);
        backgroundFadeInSeconds = Mathf.Max(0.01f, backgroundFadeInSeconds);
        foregroundMoveSpeedY = Mathf.Max(1f, foregroundMoveSpeedY);
        logoMoveSpeedY = Mathf.Max(1f, logoMoveSpeedY);
        logoDismissFadeOutSeconds = Mathf.Max(0.01f, logoDismissFadeOutSeconds);
        objectRevealScaleUpSeconds = Mathf.Max(0.01f, objectRevealScaleUpSeconds);
        objectRevealScale = Mathf.Max(1f, objectRevealScale);
        objectRevealFillAlpha = Mathf.Clamp01(objectRevealFillAlpha);
        objectRevealFadeOutSeconds = Mathf.Max(0.01f, objectRevealFadeOutSeconds);
    }
#endif

    // ── Input System ─────────────────────────────────────────────────────────

    private void SetupInputActions()
    {
        var asset = inputActions ?? Resources.Load<InputActionAsset>("InputSystem_Actions");
        if (asset == null) return;

        var uiMap = asset.FindActionMap("UI");
        if (uiMap == null) return;

        _cancelAction = uiMap.FindAction("Cancel");
        if (_cancelAction != null)
        {
            _cancelAction.performed += OnCancelPerformed;
            _cancelAction.Enable();
        }

        uiMap.Enable();
    }

    private void TeardownInputActions()
    {
        if (_cancelAction != null)
        {
            _cancelAction.performed -= OnCancelPerformed;
            _cancelAction = null;
        }
    }

    /// <summary>
    /// Assigns our InputSystem_Actions asset to the InputSystemUIInputModule.
    /// The asset must have a "UI" map with Move, Submit, Cancel (WASD/arrows, Enter/Space, Escape/Backspace).
    /// If inputActions is not assigned, tries to load from Resources as fallback.
    /// </summary>
    private void ConfigureUIInputModule()
    {
        var asset = inputActions;
        if (asset == null)
        {
            asset = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (asset == null)
            {
                Debug.LogWarning("[MainMenuSequenceController2] Input Actions not assigned. Assign InputSystem_Actions in Inspector (Header 10) or place it in a Resources folder for navigation/back to work.");
                return;
            }
        }

        var uiModule = EventSystem.current != null
            ? EventSystem.current.GetComponent<InputSystemUIInputModule>()
            : FindObjectOfType<InputSystemUIInputModule>();

        if (uiModule == null)
        {
            Debug.LogWarning("[MainMenuSequenceController2] No InputSystemUIInputModule found on EventSystem. Add it for keyboard/gamepad navigation.");
            return;
        }

        uiModule.actionsAsset = asset;
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        HandleMenuCancelRequested();
    }

    /// <summary>
    /// Inspector-callable cancel/back route for MenuInputContextController.
    /// </summary>
    public void HandleMenuCancelRequested()
    {
        if (_menuState == MenuState.LogoGate)
        {
            RequestMenuStart();
            return;
        }

        WalletConnectionPromptService prompt = walletPromptService != null && walletPromptService.IsOpen
            ? walletPromptService
            : WalletConnectionPromptService.Instance;
        if (prompt != null && prompt.TryCancel())
            return;

        if (settingsPanel != null && settingsPanel.IsOpen)
        {
            sfxRouter?.PlayCancel();
            settingsPanel?.ClosePanel();
            return;
        }

        if (bonkDogPanel != null && bonkDogPanel.IsOpen)
        {
            sfxRouter?.PlayCancel();
            bonkDogPanel.ClosePanel();
            return;
        }

        if (pcDeskPanel != null && pcDeskPanel.IsOpen)
        {
            sfxRouter?.PlayCancel();
            pcDeskPanel.ClosePanel();
            return;
        }

        if (eventsPanel != null && eventsPanel.IsOpen)
        {
            sfxRouter?.PlayCancel();
            eventsPanel.ClosePanel();
        }
    }

    // ── Initial State ─────────────────────────────────────────────────────────

    private void ApplyInitialVisualState()
    {
        SetGroupState(splashGroup, true, 0f, false);
        SetGroupState(backgroundGroup, true, 0f, false);
        SetGroupState(objectLayersGroup, true, 0f, false);
        SetGroupState(foregroundGroup, true, 0f, false);
        SetGroupState(foregroundVinylGroup, true, 0f, false);
        SetGroupState(bubbleButtonsGroup, true, 1f, false);
        SetGroupState(mobileHudRootGroup, true, 0f, false);
        SetGroupState(gameLogoGroup, true, 0f, false);
        // Hold the authored full-screen contrast panel over the scene while
        // the main-menu art and Chainbound logo settle. It is dismissed only
        // by the logo-gate input below, so no background-only frame can leak
        // through during startup.
        SetGroupState(contrastOverlayGroup, true, 1f, false);
        SetGroupState(connectSettingsGroup, false, 0f, false);
        SetGroupState(playButtonGroup, false, 0f, false);

        if (objectFrameAnimators != null)
        {
            for (int i = 0; i < objectFrameAnimators.Length; i++)
                objectFrameAnimators[i]?.Stop();
        }

        SetObjectFillsImmediate(0f, false);

        objectNavigator?.SetHotspotsInteractable(false);
        objectNavigator?.SetOutlineVisualsEnabled(false);
        bonkDogPanel?.HideImmediate();
        pcDeskPanel?.HideImmediate();
        eventsPanel?.HideImmediate();

        RectTransform foregroundMover = ResolveForegroundMover();
        if (foregroundMover != null)
            foregroundMover.anchoredPosition = _foregroundTargetPosition + Vector2.down * foregroundStartOffsetY;

        if (gameLogoTransform != null)
            gameLogoTransform.anchoredPosition = _logoTargetPosition + Vector2.up * logoStartOffsetY;

        SetButtonToStartScale(continueAsGuestButton, _continueAsGuestButtonScale);
        SetButtonToStartScale(connectWalletButton, _connectWalletButtonScale);
        SetButtonToStartScale(restoreAccountButton, _restoreAccountButtonScale);
        SetButtonToStartScale(settingsButton, _settingsButtonScale);
        SetButtonToStartScale(playButton, _playButtonScale);
        SetButtonToStartScale(linkWalletButton, _linkWalletButtonScale);

        if (continueAsGuestButton != null) continueAsGuestButton.interactable = false;
        if (connectWalletButton != null) connectWalletButton.interactable = false;
        if (restoreAccountButton != null) restoreAccountButton.interactable = false;
        if (settingsButton != null)      settingsButton.interactable = false;
        if (playButton != null)          playButton.interactable = false;
        if (linkWalletButton != null)    linkWalletButton.interactable = false;

        if (objectNavigator != null && menuInputController != null)
            menuInputController.SetInteractionEnabled(false);
    }

    // ── Boot Sequence ─────────────────────────────────────────────────────────

    private IEnumerator BootSequenceCoroutine()
    {
        // Do not expose a wallet-first decision. The menu silently restores or
        // creates its authenticated anonymous profile before choosing New Game or
        // Continue. Start this behind the splash so the logo gate never exposes
        // bootstrap latency after the player has asked to enter the menu.
        BeginProfileBootstrap();

        // Profile/network work begins as soon as the scene activates, but the
        // authored splash clock starts only when the global loading cover is
        // about to reveal this scene.
        while (!_sceneRevealGateOpen)
            yield return null;

        yield return StartCoroutine(RunSplashLeadInSequence());

        // Cross-fade out of the prior splash while every illustrated scene layer
        // fades and moves on one joined reveal. Keeping the splash alive here
        // prevents even a single uncovered frame while the new Images warm up.
        Coroutine reveal = StartCoroutine(RunMainRevealSequence());
        if (splashGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(splashGroup, splashGroup.alpha, 0f, splashFadeOutSeconds));
            SetGroupState(splashGroup, false, 0f, false);
        }

        _splashCleared = true;
        yield return reveal;

        while (!_bootstrapComplete)
            yield return null;

        ApplyProfileActionLayout();

        if (objectNavigator != null)
        {
            SetGroupState(connectSettingsGroup, false, 0f, false);
            SetGroupState(playButtonGroup, false, 0f, false);
            SetGroupState(objectLayersGroup, true, 1f, false);
            SetGroupState(foregroundVinylGroup, true, 1f, false);
            SetGroupState(bubbleButtonsGroup, true, 1f, false);
            objectNavigator.SetHotspotsInteractable(false);

            _menuStartRequested = false;
            _menuState = MenuState.LogoGate;
            _menuStartGate.Arm(RequestMenuStart);
            StartPressStartPrompt();
            while (!_menuStartRequested)
                yield return null;
            _menuStartGate.Disarm();
            StopPressStartPrompt();

            Coroutine logoDismissFade = StartCoroutine(FadeCanvasGroup(
                gameLogoGroup,
                gameLogoGroup != null ? gameLogoGroup.alpha : 1f,
                0f,
                logoDismissFadeOutSeconds));
            Coroutine overlayDismissFade = contrastOverlayGroup != null
                ? StartCoroutine(FadeCanvasGroup(
                    contrastOverlayGroup,
                    contrastOverlayGroup.alpha,
                    0f,
                    logoDismissFadeOutSeconds))
                : null;

            // Both covers leave on the same clock after the player's first
            // button press. Keep the logo/overlay gate closed until both have
            // completed, then expose the interactive object layers.
            yield return logoDismissFade;
            if (overlayDismissFade != null)
            yield return overlayDismissFade;
            SetGroupState(gameLogoGroup, false, 0f, false);
            SetGroupState(contrastOverlayGroup, false, 0f, false);

            // The logo/contrast gate is now fully gone. Reveal the outline
            // layer and run the one-shot object activation before any input,
            // selection pulse, or bubble can become visible.
            objectNavigator.SetOutlineVisualsEnabled(true);
            SetGroupState(objectLayersGroup, true, 1f, false);
            SetGroupState(foregroundVinylGroup, true, 1f, false);
            SetGroupState(bubbleButtonsGroup, true, 1f, false);
            yield return StartCoroutine(RunObjectActivationRevealSequence());

            SetGroupState(objectLayersGroup, true, 1f, true);
            SetGroupState(foregroundVinylGroup, true, 1f, true);
            SetGroupState(bubbleButtonsGroup, true, 1f, true);
            menuInputController?.RestoreInspectorNavigation();
            objectNavigator.SetHotspotsInteractable(true);
            menuInputController?.SetInteractionEnabled(true);
            MainMenuObjectHotspot defaultHotspot = bonkDogHotspot != null
                ? bonkDogHotspot
                : objectNavigator.DefaultHotspot;
            if (defaultHotspot != null)
            {
                objectNavigator.Select(defaultHotspot);
                menuInputController?.Select(defaultHotspot);
            }
            else
            {
                objectNavigator.SelectDefault();
            }

            _menuState = MenuState.ObjectNavigation;
        }
        else
        {
            SetGroupState(connectSettingsGroup, true, 0f, true);
            yield return StartCoroutine(FadeCanvasGroup(connectSettingsGroup, 0f, 1f, connectButtonsFadeInSeconds));
            ApplyProfileActionLayout();
        }
    }

    private void HandleSceneBeforeReveal(SceneTransitionContext context)
    {
        if (string.Equals(
                context.SceneId,
                gameObject.scene.name,
                System.StringComparison.OrdinalIgnoreCase))
        {
            _sceneRevealGateOpen = true;
        }
    }

    private void BeginProfileBootstrap()
    {
        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator == null)
        {
            _bootstrapFailed = true;
            _bootstrapComplete = true;
            _profileServiceError = "Profile service is unavailable.";
            return;
        }

        orchestrator.BootstrapProfile(session =>
        {
            CacheResolvedSession(session);
            _bootstrapComplete = true;
        }, error =>
        {
            _bootstrapFailed = true;
            _profileServiceError = error ?? "Profile service is unavailable.";
            _bootstrapComplete = true;
        });
    }

    /// <summary>
    /// Releases the logo hold. Called by the reusable Input System any-button
    /// gate and exposed for deterministic play-mode smoke tests.
    /// </summary>
    public void RequestMenuStart()
    {
        if (_menuState != MenuState.LogoGate || _menuStartRequested)
            return;

        _menuStartRequested = true;
        _menuStartGate.Disarm();
        StopPressStartPrompt();
    }

    private IEnumerator RunObjectActivationRevealSequence()
    {
        MainMenuObjectHotspot[] hotspots = GetObjectHotspots();
        float scaleUpSeconds = Mathf.Max(0.01f, objectRevealScaleUpSeconds);
        float fadeOutSeconds = Mathf.Max(0.01f, objectRevealFadeOutSeconds);

        objectNavigator?.SetHotspotsInteractable(false);
        menuInputController?.SetInteractionEnabled(false);
        SetGroupState(bubbleButtonsGroup, true, 1f, false);

        if (objectFrameAnimators != null)
        {
            for (int i = 0; i < objectFrameAnimators.Length; i++)
                objectFrameAnimators[i]?.ResetToFirstFrame();
        }

        var baseScales = new Vector3[hotspots.Length];
        for (int i = 0; i < hotspots.Length; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            RectTransform visualRoot = hotspot != null ? hotspot.VisualRoot : null;
            Image fillImage = hotspot != null ? hotspot.FillImage : null;
            if (visualRoot == null)
                continue;

            visualRoot.DOKill();
            Vector3 baseScale = visualRoot.localScale;
            baseScales[i] = baseScale;
            Vector3 revealScale = baseScale * Mathf.Max(1f, objectRevealScale);

            if (fillImage != null)
            {
                fillImage.DOKill(false);
                hotspot.SetFillAppearance(0f, true);
            }

            Sequence sequence = DOTween.Sequence().SetUpdate(true)
                .Append(visualRoot.DOScale(revealScale, scaleUpSeconds).SetEase(Ease.OutSine));
            if (fillImage != null)
            {
                sequence.Join(fillImage.DOFade(Mathf.Clamp01(objectRevealFillAlpha), scaleUpSeconds)
                    .SetEase(Ease.OutSine));
            }

            sequence.Append(visualRoot.DOScale(baseScale, fadeOutSeconds).SetEase(Ease.InOutSine));
            if (fillImage != null)
            {
                sequence.Join(fillImage.DOFade(0f, fadeOutSeconds).SetEase(Ease.InSine));
            }

        }

        float totalSeconds = scaleUpSeconds + fadeOutSeconds;
        if (totalSeconds > 0f)
            yield return new WaitForSecondsRealtime(totalSeconds);

        for (int i = 0; i < hotspots.Length; i++)
        {
            MainMenuObjectHotspot hotspot = hotspots[i];
            if (hotspot == null)
                continue;

            hotspot.SetFillAppearance(0f, false);
            if (hotspot.VisualRoot != null)
                hotspot.VisualRoot.localScale = baseScales[i];
        }

        if (objectFrameAnimators != null)
        {
            for (int i = 0; i < objectFrameAnimators.Length; i++)
                objectFrameAnimators[i]?.Play();
        }
    }

    private MainMenuObjectHotspot[] GetObjectHotspots()
    {
        return new[] { pcDeskHotspot, bonkDogHotspot, xmasTreeHotspot, vinylHotspot };
    }

    private void SetObjectFillsImmediate(float alpha, bool visible)
    {
        MainMenuObjectHotspot[] hotspots = GetObjectHotspots();
        for (int i = 0; i < hotspots.Length; i++)
            hotspots[i]?.SetFillAppearance(alpha, visible);
    }

    // ── Splash Sequence ───────────────────────────────────────────────────────

    private IEnumerator RunSplashLeadInSequence()
    {
        if (splashGroup == null)
            yield break;

        SetGroupState(splashGroup, true, 0f, false);

        if (splashHoldBeforeFadeInSeconds > 0f)
            yield return new WaitForSecondsRealtime(splashHoldBeforeFadeInSeconds);

        // Fire vinyl needle + music as the splash logo fades in
        StartMenuMusicSequence();

        yield return StartCoroutine(FadeCanvasGroup(splashGroup, 0f, 1f, splashFadeInSeconds));

        if (splashHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(splashHoldSeconds);
    }

    private void AcquireSplashHudSuppression()
    {
        if (_hasSplashHudToken)
            return;

        MobileHudService hudService = MobileHudService.Instance;
        if (hudService == null)
            return;

        _splashHudToken = hudService.AcquireContext(MobileHudContext.Hidden, "MainMenu.Splash");
        _hasSplashHudToken = _splashHudToken.IsValid;
    }

    private void ReleaseSplashHudSuppression()
    {
        if (!_hasSplashHudToken)
            return;

        _splashHudToken.Release();
        _splashHudToken = default;
        _hasSplashHudToken = false;
    }

    // ── Main Reveal Sequence ──────────────────────────────────────────────────

    private IEnumerator RunMainRevealSequence()
    {
        // ── Background ──────────────────────────────────────────────────────
        SetGroupState(backgroundGroup, true, 0f, false);
        SetGroupState(objectLayersGroup, true, 0f, false);
        SetGroupState(foregroundGroup, true, 0f, false);
        SetGroupState(foregroundVinylGroup, true, 0f, false);
        SetGroupState(gameLogoGroup, true, 0f, false);

        if (objectFrameAnimators != null)
        {
            for (int i = 0; i < objectFrameAnimators.Length; i++)
                objectFrameAnimators[i]?.Play();
        }

        if (objectNavigator == null)
            backgroundAnimator?.Play();

        // ── Foreground ──────────────────────────────────────────────────────
        if (objectNavigator == null)
            foregroundAnimator?.Play();
        RectTransform foregroundMover = ResolveForegroundMover();
        if (foregroundMover != null)
            foregroundMover.anchoredPosition = _foregroundTargetPosition + Vector2.down * foregroundStartOffsetY;

        // Start every fade and both Y motions before yielding. This guarantees
        // the reveal has no background-only temporal step.
        var fades = new[]
        {
            StartCoroutine(FadeCanvasGroup(backgroundGroup, 0f, 1f, backgroundFadeInSeconds)),
            StartCoroutine(FadeCanvasGroup(objectLayersGroup, 0f, 1f, backgroundFadeInSeconds)),
            StartCoroutine(FadeCanvasGroup(foregroundGroup, 0f, 1f, backgroundFadeInSeconds)),
            StartCoroutine(FadeCanvasGroup(foregroundVinylGroup, 0f, 1f, backgroundFadeInSeconds)),
            StartCoroutine(FadeCanvasGroup(gameLogoGroup, 0f, 1f, backgroundFadeInSeconds)),
        };

        float foregroundDuration = DurationFromSpeed(foregroundStartOffsetY, foregroundMoveSpeedY);
        Coroutine foregroundMove = StartCoroutine(MoveRect(foregroundMover, _foregroundTargetPosition, foregroundDuration));

        // ── Game Logo ───────────────────────────────────────────────────────
        if (gameLogoTransform != null)
            gameLogoTransform.anchoredPosition = _logoTargetPosition + Vector2.up * logoStartOffsetY;
        float logoDuration = gameLogoTransform != null
            ? DurationFromSpeed(logoStartOffsetY, logoMoveSpeedY)
            : 0f;
        Coroutine logoMove = gameLogoTransform != null
            ? StartCoroutine(MoveRect(gameLogoTransform, _logoTargetPosition, logoDuration))
            : null;

        float hudLead = Mathf.Min(hudAppearBeforeLogoSettleSeconds, logoDuration);
        float waitBeforeHud = Mathf.Max(0f, logoDuration - hudLead);
        if (waitBeforeHud > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeHud);
        while (!_splashCleared)
            yield return null;
        ReleaseSplashHudSuppression();

        if (logoMove != null)
            yield return logoMove;

        // ── Contrast Overlay — brightness flick when logo lands ─────────────
        yield return foregroundMove;
        for (int i = 0; i < fades.Length; i++)
            yield return fades[i];

        if (mobileHudRootGroup != null)
        {
            // HUD controls may dismiss the logo gate, but hotspots remain gated.
            mobileHudRootGroup.interactable = true;
            mobileHudRootGroup.blocksRaycasts = true;
        }
    }

    // ── Wallet Connect ────────────────────────────────────────────────────────

    private void OnContinueAsGuestPressed()
    {
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight)
            return;

        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator == null)
        {
            Debug.LogWarning("[MainMenuSequenceController2] Flow orchestrator is not assigned.");
            return;
        }

        _accountFlowInFlight = true;
        _menuState = MenuState.FetchingProfile;
        SetConnectButtonsInteractable(false);

        orchestrator.ContinueAsGuest(OnAccountSessionResolved, OnAccountFlowFailed);
    }

    private void OnNewGamePressed()
    {
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight)
            return;

        if (_bootstrapFailed)
        {
            if (enableOfflineNewGameTest)
            {
                StartOfflineNewGameTest();
                return;
            }

            _bootstrapFailed = false;
            _bootstrapComplete = false;
            SetConnectButtonsInteractable(false);
            BeginProfileBootstrap();
            StartCoroutine(WaitForBootstrapRetry());
            return;
        }

        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator == null)
        {
            if (enableOfflineNewGameTest)
            {
                StartOfflineNewGameTest();
                return;
            }

            return;
        }

        bool skipWalletPrompt = _skipNewGameWalletPrompt;
        _skipNewGameWalletPrompt = false;
        if (skipWalletPrompt)
        {
            BeginNewGameThenRoute();
            return;
        }

        StartCoroutine(PromptForWalletThenBeginNewGame());
    }

    /// <summary>
    /// Legacy New Game asks about a wallet before the profile is marked started.
    /// Cancelling that prompt must leave has_started_game false so the menu
    /// still offers New Game on this same profile.
    /// </summary>
    private IEnumerator PromptForWalletThenBeginNewGame()
    {
        _accountFlowInFlight = true;
        SetConnectButtonsInteractable(false);

        WalletConnectionPromptService prompt = ResolveWalletPromptService();
        if (prompt == null)
        {
            OnAccountFlowFailed("Wallet access prompt is unavailable.");
            yield break;
        }

        WalletPromptDefinitionSO definition = mainMenuWalletPrompt != null
            ? mainMenuWalletPrompt
            : Resources.Load<WalletPromptDefinitionSO>("WalletPrompts/MainMenuWalletPrompt") ?? CreateRuntimeMainMenuWalletPrompt();
        var task = prompt.ShowAsync(definition);
        while (!task.IsCompleted)
            yield return null;

        WalletPromptResult result = task.Result;
        if (result.Kind == WalletPromptResultKind.Failed || result.Kind == WalletPromptResultKind.Cancelled)
        {
            OnAccountFlowFailed(string.IsNullOrWhiteSpace(result.Message) ? "Wallet prompt cancelled." : result.Message);
            yield break;
        }

        BeginNewGameThenRoute();
    }

    private void BeginNewGameThenRoute()
    {
        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator == null)
        {
            if (enableOfflineNewGameTest)
            {
                StartOfflineNewGameTest();
                return;
            }

            OnAccountFlowFailed("Profile service is unavailable.");
            return;
        }

        // An offline cached profile already exists. Replaying the intro must
        // not wait on a network write that cannot succeed.
        if (_cachedSnapshot.fetchError && _cachedSnapshot.hasProfile)
        {
            _accountFlowInFlight = false;
            _startingNewGame = true;
            _routeDecision = DecideRoute(_cachedSnapshot, routeMode);
            _routeReady = true;
            StartCoroutine(PlayRouteCoroutine());
            return;
        }

        _accountFlowInFlight = true;
        _startingNewGame = true;
        SetConnectButtonsInteractable(false);
        orchestrator.BeginNewGame(session =>
        {
            _accountFlowInFlight = false;
            CacheResolvedSession(session);
            StartCoroutine(PlayRouteCoroutine());
        }, error =>
        {
            if (enableOfflineNewGameTest)
            {
                _accountFlowInFlight = false;
                if (enableDebugLogs)
                    Debug.LogWarning($"[MainMenuSequenceController2] BeginNewGame failed ({error}); using offline New Game test path.");
                StartOfflineNewGameTest();
                return;
            }

            OnAccountFlowFailed(error);
        });
    }

    /// <summary>
    /// Local-only New Game path for animation QA when Supabase/profile services are unavailable.
    /// Skips wallet prompt and routes straight into the intro sequence.
    /// </summary>
    private void StartOfflineNewGameTest()
    {
        _skipNewGameWalletPrompt = false;
        _bootstrapFailed = false;
        _startingNewGame = true;
        _cachedSnapshot = new MainMenuProfileSnapshot
        {
            hasProfile = true,
            hasStartedGame = false,
            introCinematicComplete = false,
            introQuestComplete = false,
            isGuestSession = true,
            fetchError = true,
            displayName = "",
            walletPublicKey = "",
            activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
        };
        _routeDecision = DecideRoute(_cachedSnapshot, RouteMode.ForceNewGame);
        _routeReady = true;
        SetConnectButtonsInteractable(false);
        if (enableDebugLogs)
            Debug.Log("[MainMenuSequenceController2] Offline New Game test → IntroPath");
        StartCoroutine(PlayRouteCoroutine());
    }

    private IEnumerator WaitForBootstrapRetry()
    {
        while (!_bootstrapComplete)
            yield return null;

        ApplyProfileActionLayout();
    }

    private void OnRestoreAccountPressed()
    {
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight)
            return;

        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator == null)
        {
            Debug.LogWarning("[MainMenuSequenceController2] Flow orchestrator is not assigned.");
            return;
        }

        // Restore is one button but a different mechanism per store, so the orchestrator
        // owns that choice. This controller only needs to know whether an attempt is
        // possible at all, and whether it will hand off to the wallet app.
        AccountRestoreRoute route = orchestrator.ResolveRestoreRoute();
        if (route == AccountRestoreRoute.None)
        {
            // No mechanism at all is a build/config state, not a failed request, so
            // report it here instead of spending a round trip that can only fail.
            // On a dApp Store build this usually means the Solana service bridge never
            // came up, which also disables Connect Wallet, so it is worth logging.
            Debug.LogWarning(
                "[MainMenuSequenceController2] Restore requested but no recovery provider " +
                "is available. Confirm the app launched through Bootstrap so the wallet " +
                "adapter is registered.");
            _profileServiceError = MainMenuAccountStatusCopy.RestoreUnavailable;
            ApplyProfileActionLayout();
            return;
        }

        _accountFlowInFlight = true;
        _menuState = MenuState.FetchingProfile;
        SetConnectButtonsInteractable(false);

        // Wallet recovery leaves the app for a signature, so it needs the same lifecycle
        // guard and timeout watchdog that Connect Wallet uses.
        if (route == AccountRestoreRoute.Wallet)
            AppLifecycleService.Instance?.BeginWalletSigning();

        orchestrator.RestoreProfile(OnAccountSessionResolved, OnAccountFlowFailed);
    }

    private void OnAccountSessionResolved(PlayerAccountSession session)
    {
        _accountFlowInFlight = false;
        AppLifecycleService.Instance?.EndWalletSigning();
        _profileServiceError = "";
        CacheResolvedSession(session);
        ApplyProfileActionLayout();
    }

    private void OnAccountFlowFailed(string error)
    {
        _accountFlowInFlight = false;
        AppLifecycleService.Instance?.EndWalletSigning();

        if (enableDebugLogs)
            Debug.LogWarning($"[MainMenuSequenceController2] Account flow failed: {error}");

        FlashOverlay(0.2f, 0.3f);
        ShakeButton(continueAsGuestButton != null ? continueAsGuestButton : connectWalletButton);
        _startingNewGame = false;
        _bootstrapFailed = !_routeReady;
        _profileServiceError = MainMenuAccountStatusCopy.Resolve(error);
        ApplyProfileActionLayout();
    }

    private void CacheResolvedSession(PlayerAccountSession session)
    {
        MainMenuProfileSnapshot snapshot = MainMenuProfileServiceSupabase.MapSession(session);
        _cachedWalletAddress = snapshot.walletPublicKey;
        _cachedSnapshot = snapshot;
        _routeDecision = DecideRoute(snapshot, routeMode);
        _routeReady = true;
        _bootstrapFailed = false;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[MainMenuSequenceController2] Account ready profile={snapshot.playerProfileId} " +
                $"platform={snapshot.activePlatformBuild} route={_routeDecision.route}");
        }
    }

    private WalletConnectionPromptService ResolveWalletPromptService()
    {
        if (walletPromptService == null)
        {
            WalletConnectionPromptService[] presenters = FindObjectsOfType<WalletConnectionPromptService>(true);
            for (int i = 0; i < presenters.Length; i++)
            {
                if (presenters[i] != null && presenters[i].gameObject.scene == gameObject.scene)
                {
                    walletPromptService = presenters[i];
                    break;
                }
            }
        }
        if (walletPromptService == null)
        {
            Debug.LogError(
                "[MainMenuSequenceController2] WalletConnectionPromptService is not authored in the scene. " +
                "Use MainMenuCanvas/WalletConnectionPrompt.");
            return null;
        }
        walletPromptService.Configure(ResolveFlowOrchestrator());
        return walletPromptService;
    }

    private static WalletPromptDefinitionSO CreateRuntimeMainMenuWalletPrompt()
    {
        WalletPromptDefinitionSO definition = ScriptableObject.CreateInstance<WalletPromptDefinitionSO>();
        definition.title = "Connect your wallet now?";
        definition.body = "Connect now to secure and recover this profile. You can keep playing without a wallet.";
        definition.connectLabel = "Connect Wallet";
        definition.skipLabel = "Play Without Wallet";
        definition.reason = WalletPromptReason.MainMenuNewGame;
        definition.suppressWhenLinked = true;
        return definition;
    }

    /// <summary>
    /// Keeps the existing scene's two button containers but exposes one logical
    /// PrimaryActionGroup: New Game or Continue, Options, then Restore Profile.
    /// Legacy wallet/link/disconnect controls are always retired from the surface.
    /// </summary>
    private void ApplyProfileActionLayout()
    {
        bool hasProfile = _routeReady && _cachedSnapshot.hasProfile;
        bool showContinue = IntroResumePolicy.ShowContinue(hasProfile, _cachedSnapshot.hasStartedGame, _cachedSnapshot.displayName);
        bool showRestore = !showContinue && (!hasProfile || !_cachedSnapshot.isRecoverable);

        if (objectNavigator != null)
        {
            SetGroupState(connectSettingsGroup, false, 0f, false);
            SetGroupState(playButtonGroup, false, 0f, false);
            bonkDogPanel?.Configure(
                _cachedSnapshot,
                showContinue,
                _bootstrapFailed,
                _profileServiceError);
            bonkDogPanel?.SetActionsInteractable(!_accountFlowInFlight && !_playInFlight);
            bonkDogHotspot?.SetBubbleLabel(showContinue ? "CONTINUE" : "NEW GAME");

            if (bonkDogPanel != null && bonkDogPanel.IsOpen)
            {
                _menuState = MenuState.ObjectPanel;
                ConfigurePanelNavigation(bonkDogPanel);
            }
            else if (_menuState != MenuState.Routing)
            {
                _menuState = MenuState.ObjectNavigation;
            }

            if (_bootstrapFailed && enableDebugLogs)
                Debug.LogWarning($"[MainMenuSequenceController2] {_profileServiceError}");
            return;
        }

        SetGroupState(connectSettingsGroup, true, 1f, true);
        SetGroupState(playButtonGroup, showContinue, showContinue ? 1f : 0f, showContinue);

        if (continueAsGuestButton != null) continueAsGuestButton.gameObject.SetActive(false);
        if (connectWalletButton != null) connectWalletButton.gameObject.SetActive(!showContinue);
        if (playButton != null) playButton.gameObject.SetActive(showContinue);
        if (settingsButton != null) settingsButton.gameObject.SetActive(true);
        if (restoreAccountButton != null) restoreAccountButton.gameObject.SetActive(showRestore);
        if (linkWalletButton != null && linkWalletButton != connectWalletButton)
            linkWalletButton.gameObject.SetActive(false);
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);

        string newGameLabel = "New Game";
        if (_bootstrapFailed)
            newGameLabel = enableOfflineNewGameTest ? "New Game (Test)" : "Retry Profile";
        SetButtonLabel(connectWalletButton, newGameLabel);
        SetButtonLabel(playButton, "Continue");
        SetButtonLabel(settingsButton, "Options");
        SetButtonLabel(restoreAccountButton, "Restore Profile");

        SetConnectButtonsInteractable(!_accountFlowInFlight && !_playInFlight);
        ConfigurePrimaryActionNavigation(showContinue, showRestore);
        _menuState = MenuState.ConnectGroup;

        Button primary = showContinue ? playButton : connectWalletButton;
        if (primary != null && primary.gameObject.activeInHierarchy)
            SetSelectedButton(primary);

        if (_bootstrapFailed && enableDebugLogs)
            Debug.LogWarning($"[MainMenuSequenceController2] {_profileServiceError}");
    }

    private void ConfigurePrimaryActionNavigation(bool hasStarted, bool showRestore)
    {
        Button primary = hasStarted ? playButton : connectWalletButton;
        SetVerticalChain(primary, settingsButton, showRestore ? restoreAccountButton : null);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    private void OnLinkWalletPressed()
    {
        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight || orchestrator == null)
            return;

        _accountFlowInFlight = true;
        AppLifecycleService.Instance?.BeginWalletSigning();
        if (linkWalletButton != null)
            linkWalletButton.interactable = false;

        orchestrator.LinkWalletToCurrentProfile(result =>
        {
            _accountFlowInFlight = false;
            AppLifecycleService.Instance?.EndWalletSigning();
            if (result != null && result.success)
            {
                _cachedSnapshot.walletLinkedToProfile = true;
                _cachedSnapshot.accountLinkState = result.state;
                FlashOverlay(overlayFlickPeakAlpha, overlayFlickDuration);
            }

            if (linkWalletButton != null)
                linkWalletButton.interactable = ShouldShowLinkWalletAction();
        }, OnLinkWalletFailed);
    }

    private void OnLinkWalletFailed(string error)
    {
        _accountFlowInFlight = false;
        AppLifecycleService.Instance?.EndWalletSigning();

        if (enableDebugLogs)
            Debug.LogWarning($"[MainMenuSequenceController2] Link wallet failed: {error}");

        FlashOverlay(0.2f, 0.3f);
        ShakeButton(linkWalletButton);
        if (linkWalletButton != null)
            linkWalletButton.interactable = ShouldShowLinkWalletAction();
    }

    private void OnConnectWalletPressed()
    {
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight)
            return;

        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator != null)
        {
            _accountFlowInFlight = true;
            AppLifecycleService.Instance?.BeginWalletSigning();
            _menuState = MenuState.Connecting;
            SetConnectButtonsInteractable(false);

            orchestrator.ConnectWalletAndResolveSession(session =>
            {
                onWalletConnected?.Invoke();
                if (session != null &&
                    string.Equals(session.walletResolutionStatus, "no_profile", StringComparison.Ordinal))
                {
                    AppLifecycleService.Instance?.EndWalletSigning();
                    StartCoroutine(PromptToCreateWalletProfile(orchestrator));
                    return;
                }

                OnAccountSessionResolved(session);
            }, OnAccountFlowFailed);
            return;
        }

        if (walletService == null)
        {
            Debug.LogWarning("[MainMenuSequenceController2] Wallet service is not assigned.");
            return;
        }

        _walletConnectInFlight = true;
        _menuState = MenuState.Connecting;
        SetConnectButtonsInteractable(false);

        walletService.Connect(OnWalletConnectResult);
    }

    private IEnumerator PromptToCreateWalletProfile(MainMenuFlowOrchestrator orchestrator)
    {
        WalletConnectionPromptService prompt = ResolveWalletPromptService();
        if (prompt == null)
        {
            OnAccountFlowFailed("Wallet access prompt is unavailable.");
            yield break;
        }

        var task = prompt.ShowAccessAsync(new WalletAccessPromptModel
        {
            title = "No Save Found",
            body = "No save found for this wallet. Start a new game?",
            primaryLabel = "Start New Game",
            primaryChoice = WalletAccessPromptChoice.Connect,
            secondaryLabel = "Back",
            secondaryChoice = WalletAccessPromptChoice.Cancel,
            showTertiary = false,
        });
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            OnAccountFlowFailed(task.Exception?.GetBaseException().Message ?? "Wallet profile prompt failed.");
            yield break;
        }

        if (task.Result != WalletAccessPromptChoice.Connect)
        {
            _accountFlowInFlight = false;
            _profileServiceError = "";
            ApplyProfileActionLayout();
            yield break;
        }

        AppLifecycleService.Instance?.BeginWalletSigning();
        orchestrator.CreateWalletProfileAndBeginNewGame(session =>
        {
            _accountFlowInFlight = false;
            AppLifecycleService.Instance?.EndWalletSigning();
            _startingNewGame = true;
            CacheResolvedSession(session);
            StartCoroutine(PlayRouteCoroutine());
        }, OnAccountFlowFailed);
    }

    private void OnWalletConnectResult(MainMenuWalletConnectResult result)
    {
        StartCoroutine(HandleWalletConnectResult(result));
    }

    private IEnumerator HandleWalletConnectResult(MainMenuWalletConnectResult result)
    {
        _walletConnectInFlight = false;
        AppLifecycleService.Instance?.EndWalletSigning();

        if (!result.success)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[MainMenuSequenceController2] Wallet connect failed: {result.errorMessage}");

            if (connectWalletButton != null)
            {
                connectWalletButton.transform.DOKill();
                connectWalletButton.transform
                    .DOShakePosition(0.4f, new Vector3(12f, 0f, 0f), 20, 90, false, true)
                    .SetUpdate(true);
            }

            FlashOverlay(0.2f, 0.3f);

            SetConnectButtonsInteractable(true);
            _menuState = MenuState.ConnectGroup;

            if (connectWalletButton != null)
                SetSelectedButton(connectWalletButton);
            yield break;
        }

        _menuState = MenuState.FetchingProfile;
        _cachedWalletAddress = result.walletAddress;

        FlashOverlay(overlayFlickPeakAlpha, overlayFlickDuration);
        onWalletConnected?.Invoke();

        if (enableDebugLogs)
            Debug.Log($"[MainMenuSequenceController2] State=FetchingProfile wallet={result.walletAddress}");

        MainMenuProfileSnapshot snapshot = default;

        bool profileDone = false;
        if (profileService != null)
        {
            profileService.FetchProfile(result.walletAddress, (fetched) =>
            {
                snapshot = fetched;
                profileDone = true;
            });
        }
        else
        {
            profileDone = true;
        }

        while (!profileDone)
            yield return null;

        // ── Offline fallback on Supabase error ────────────────────────────────
        if (snapshot.fetchError)
        {
            snapshot = TryOfflineFallback(result.walletAddress, out bool usedCache);

            if (!usedCache)
            {
                // No cached data for this wallet — show failure and allow retry
                if (enableDebugLogs)
                    Debug.LogWarning("[MainMenuSequenceController2] Supabase unavailable + no offline cache. Retry required.");

                if (connectWalletButton != null)
                {
                    connectWalletButton.transform.DOKill();
                    connectWalletButton.transform
                        .DOShakePosition(0.4f, new Vector3(12f, 0f, 0f), 20, 90, false, true)
                        .SetUpdate(true);
                }

                FlashOverlay(0.2f, 0.3f);
                SetConnectButtonsInteractable(true);
                _menuState = MenuState.ConnectGroup;

                if (connectWalletButton != null)
                    SetSelectedButton(connectWalletButton);

                yield break;
            }

            if (enableDebugLogs)
                Debug.LogWarning("[MainMenuSequenceController2] Supabase unavailable. Using offline cached profile (limited mode).");
        }

        // ── Profile resolved (live or offline) ───────────────────────────────
        _cachedSnapshot = snapshot;
        _routeDecision  = DecideRoute(snapshot, routeMode);
        _routeReady     = true;

        if (enableDebugLogs)
            Debug.Log($"[MainMenuSequenceController2] State=ReadyToPlay route={_routeDecision.route} scene={_routeDecision.targetSceneName} offline={snapshot.fetchError}");

        yield return StartCoroutine(SwapActionButtonsToPlay());
    }

    private IEnumerator SwapActionButtonsToPlay()
    {
        SetConnectButtonsInteractable(false);
        yield return StartCoroutine(FadeCanvasGroup(connectSettingsGroup,
            connectSettingsGroup != null ? connectSettingsGroup.alpha : 0f, 0f, connectButtonsFadeOutSeconds));
        SetGroupState(connectSettingsGroup, false, 0f, false);

        SetButtonToStartScale(playButton, _playButtonScale);
        SetButtonToStartScale(linkWalletButton, _linkWalletButtonScale);

        SetGroupState(playButtonGroup, true, 0f, true);
        yield return StartCoroutine(FadeCanvasGroup(playButtonGroup, 0f, 1f, playButtonFadeInSeconds));

        yield return StartCoroutine(AnimateButtonIn(playButton, _playButtonScale, 0f));
        yield return StartCoroutine(AnimateButtonIn(linkWalletButton, _linkWalletButtonScale, buttonStaggerSeconds));

        if (playButton != null)
        {
            playButton.interactable = true;

            if (sfxRouter != null)
                sfxRouter.ResetTracking();

            SetSelectedButton(playButton);
        }

        if (disconnectButton != null)
            disconnectButton.interactable = true;

        if (linkWalletButton != null)
            linkWalletButton.interactable = ShouldShowLinkWalletAction();

        _menuState = MenuState.PlayGroup;
    }

    // ── Back Navigation (Cancel → Play group ➜ Connect group) ────────────────

    private IEnumerator SwapPlayButtonsToConnect()
    {
        _backTransitionInFlight = true;
        _routeReady = false;
        _cachedWalletAddress = "";
        _cachedSnapshot = default;

        if (playButton != null)       playButton.interactable = false;
        if (linkWalletButton != null) linkWalletButton.interactable = false;
        if (disconnectButton != null) disconnectButton.interactable = false;

        yield return StartCoroutine(FadeCanvasGroup(playButtonGroup,
            playButtonGroup != null ? playButtonGroup.alpha : 0f, 0f, connectButtonsFadeOutSeconds));
        SetGroupState(playButtonGroup, false, 0f, false);

        SetButtonToStartScale(continueAsGuestButton, _continueAsGuestButtonScale);
        SetButtonToStartScale(connectWalletButton, _connectWalletButtonScale);
        SetButtonToStartScale(restoreAccountButton, _restoreAccountButtonScale);
        SetButtonToStartScale(settingsButton, _settingsButtonScale);

        SetGroupState(connectSettingsGroup, true, 0f, true);
        yield return StartCoroutine(FadeCanvasGroup(connectSettingsGroup, 0f, 1f, connectButtonsFadeInSeconds));

        yield return StartCoroutine(AnimateButtonIn(continueAsGuestButton, _continueAsGuestButtonScale, 0f));
        yield return StartCoroutine(AnimateButtonIn(connectWalletButton, _connectWalletButtonScale, buttonStaggerSeconds));
        yield return StartCoroutine(AnimateButtonIn(restoreAccountButton, _restoreAccountButtonScale, buttonStaggerSeconds));
        yield return StartCoroutine(AnimateButtonIn(settingsButton, _settingsButtonScale, buttonStaggerSeconds));

        SetConnectButtonsInteractable(true);

        if (sfxRouter != null)
            sfxRouter.ResetTracking();

        if (continueAsGuestButton != null)
            SetSelectedButton(continueAsGuestButton);
        else if (connectWalletButton != null)
            SetSelectedButton(connectWalletButton);

        _menuState = MenuState.ConnectGroup;
        _backTransitionInFlight = false;
    }

    // ── Disconnect ──────────────────────────────────────────────────────────────

    private void OnDisconnectPressed()
    {
        if (_playInFlight || _backTransitionInFlight)
            return;

        MainMenuFlowOrchestrator orchestrator = ResolveFlowOrchestrator();
        if (orchestrator != null)
        {
            if (playButton != null)          playButton.interactable = false;
            if (linkWalletButton != null)    linkWalletButton.interactable = false;
            if (disconnectButton != null)    disconnectButton.interactable = false;

            orchestrator.DisconnectWallet(OnDisconnectComplete, error =>
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MainMenuSequenceController2] Disconnect failed: {error}");
                OnDisconnectComplete();
            });
            return;
        }

        if (walletService == null)
        {
            StartCoroutine(SwapPlayButtonsToConnect());
            return;
        }

        if (playButton != null)          playButton.interactable = false;
        if (linkWalletButton != null)    linkWalletButton.interactable = false;
        if (disconnectButton != null)    disconnectButton.interactable = false;

        walletService.Disconnect(OnDisconnectComplete);
    }

    private void OnDisconnectComplete()
    {
        if (enableDebugLogs)
            Debug.Log("[MainMenuSequenceController2] Wallet disconnected. Returning to connect screen.");

        _routeReady = false;
        _cachedWalletAddress = "";
        _cachedSnapshot = default;

        StartCoroutine(SwapPlayButtonsToConnect());
    }

    // ── Settings / Play ───────────────────────────────────────────────────────

    public void OpenBonkDogPanel()
    {
        if (!CanOpenObjectPanel() || bonkDogPanel == null)
            return;

        bool hasProfile = _routeReady && _cachedSnapshot.hasProfile;
        bool showContinue = IntroResumePolicy.ShowContinue(hasProfile, _cachedSnapshot.hasStartedGame, _cachedSnapshot.displayName);
        // A message from a previous visit describes an action the player already saw
        // the result of. Reopening the panel is a fresh start, so only a still-true
        // bootstrap failure should greet them.
        if (_routeReady)
            _profileServiceError = "";

        bonkDogPanel.Configure(_cachedSnapshot, showContinue, _bootstrapFailed, _profileServiceError);
        bonkDogPanel.SetActionsInteractable(true);
        OpenObjectPanel(bonkDogPanel, bonkDogHotspot);
    }

    public void OpenPCDeskPanel()
    {
        if (!CanOpenObjectPanel() || pcDeskPanel == null)
            return;

        OpenObjectPanel(pcDeskPanel, pcDeskHotspot);
    }

    public void OpenEventsPanel()
    {
        if (!CanOpenObjectPanel() || eventsPanel == null)
            return;

        OpenObjectPanel(eventsPanel, xmasTreeHotspot);
    }

    public void OpenSettingsFromVinyl()
    {
        if (!CanOpenObjectPanel() || settingsPanel == null)
            return;

        sfxRouter?.PlayConfirm();
        objectNavigator?.SetHotspotsInteractable(false);
        _menuState = MenuState.Settings;
        settingsPanel.OpenPanel(() => ReturnToObjectNavigation(vinylHotspot));

        if (menuInputController != null)
        {
            menuInputController.SetContentNavigation(
                settingsPanel.GetNavigationSelectables(),
                settingsPanel.DefaultNavigationSelection,
                settingsPanel.NavigationRoot,
                settingsPanel.DefaultNavigationSelection);
        }
    }

    public void RequestNewGameFromObjectPanel()
    {
        _skipNewGameWalletPrompt = true;
        OnNewGamePressed();
    }

    public void RequestConnectWalletFromObjectPanel()
    {
        OnConnectWalletPressed();
    }

    public void RequestContinueFromObjectPanel()
    {
        OnPlayPressed();
    }

    private bool CanOpenObjectPanel()
    {
        // Guardrail: block start-menu object panels while shared text entry owns focus.
        Chainbound.Combat.OnScreenKeyboardService keyboard = Chainbound.Combat.OnScreenKeyboardService.Instance;
        if (keyboard != null && keyboard.IsOpen)
            return false;

        return objectNavigator != null &&
            _menuState == MenuState.ObjectNavigation &&
            !_accountFlowInFlight &&
            !_walletConnectInFlight &&
            !_playInFlight;
    }

    private void OpenObjectPanel(MainMenuPanelControllerBase panel, MainMenuObjectHotspot returnHotspot)
    {
        sfxRouter?.PlayConfirm();
        objectNavigator?.SetHotspotsInteractable(false);
        // Bubbles sit under a full-screen CanvasGroup; keep them from eating
        // panel clicks while a modal is open.
        if (bubbleButtonsGroup != null)
        {
            bubbleButtonsGroup.blocksRaycasts = false;
            bubbleButtonsGroup.interactable = false;
        }

        _menuState = MenuState.ObjectPanel;
        panel.OpenPanel(() => ReturnToObjectNavigation(returnHotspot));
        if (panel == bonkDogPanel)
            bonkDogPanel.SetActionsInteractable(!_accountFlowInFlight && !_playInFlight);

        // Establish focus immediately. The panel is already active and its
        // CanvasGroup/buttons are interactable at this point, so keyboard and
        // HUD navigation must not depend on the deferred refresh below.
        ConfigurePanelNavigation(panel);

        // Keep the deferred refresh as a safety net for panels whose active
        // view changes one frame after opening (for example profile/continue
        // content). It also repairs explicit links after any late layout state.
        StartCoroutine(ConfigurePanelNavigationNextFrame(panel));
    }

    private IEnumerator ConfigurePanelNavigationNextFrame(MainMenuPanelControllerBase panel)
    {
        // Re-apply focus for a short settling window. The HUD/input module and
        // the panel's view toggle can both run on adjacent frames; selecting
        // only once leaves a race where the EventSystem is cleared immediately
        // after the panel appears.
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            if (panel == null || !panel.IsOpen)
                yield break;
            ConfigurePanelNavigation(panel);
        }
    }

    private void ConfigurePanelNavigation(MainMenuPanelControllerBase panel)
    {
        if (menuInputController == null || panel == null || !panel.IsOpen)
            return;

        if (panel == bonkDogPanel)
            bonkDogPanel.SetActionsInteractable(!_accountFlowInFlight && !_playInFlight);

        panel.RefreshNavigation();

        var order = panel.GetUsableNavigationOrder();
        // Game Menu has two authored views (New Game and Continue). Always
        // prefer the top action of the visible view instead of retaining the
        // serialized default from the hidden sibling view.
        Selectable defaultSelection = panel.ActiveViewDefaultSelection;
        if (defaultSelection == null && order.Count > 0)
            defaultSelection = order[0];

        if (order.Count == 0 || defaultSelection == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[MainMenuSequenceController2] Object panel opened with zero usable selectables.");
            return;
        }

        menuInputController.SetContentNavigation(
            order,
            defaultSelection,
            panel.NavigationRoot,
            panel.FallbackSelection ?? defaultSelection);

        if (defaultSelection != null)
            menuInputController.Select(defaultSelection);
    }

    private void ReturnToObjectNavigation(MainMenuObjectHotspot returnHotspot)
    {
        if (objectNavigator == null)
            return;

        if (bubbleButtonsGroup != null)
        {
            bubbleButtonsGroup.blocksRaycasts = true;
            bubbleButtonsGroup.interactable = true;
        }

        menuInputController?.RestoreInspectorNavigation();
        objectNavigator.SetHotspotsInteractable(true);
        objectNavigator.Select(returnHotspot != null ? returnHotspot : objectNavigator.DefaultHotspot);
        _menuState = MenuState.ObjectNavigation;
    }

    private void OnSettingsPressed()
    {
        if (_accountFlowInFlight || _walletConnectInFlight || _playInFlight)
            return;

        if (objectNavigator != null)
        {
            OpenSettingsFromVinyl();
            return;
        }

        _menuState = MenuState.Settings;
        SetConnectButtonsInteractable(false);

        if (settingsPanel != null)
        {
            settingsPanel.OpenPanel(OnSettingsPanelClosed);
        }
        else
        {
            // Fallback: fire the legacy event if no panel is wired
            onSettingsRequested?.Invoke();
        }
    }

    private void OnSettingsPanelClosed()
    {
        if (objectNavigator != null)
        {
            ReturnToObjectNavigation(vinylHotspot);
            return;
        }

        _menuState = MenuState.ConnectGroup;
        SetConnectButtonsInteractable(true);
        SetSelectedButton(settingsButton);
    }

    private void OnPlayPressed()
    {
        if (_playInFlight || !_routeReady)
            return;

        PlayerAccountSession session = ResolveFlowOrchestrator()?.CurrentSession;
        if (session != null && session.HasLinkedWallet)
        {
            StartCoroutine(ConfirmResumeWalletAccessCoroutine());
            return;
        }

        StartCoroutine(PlayRouteCoroutine());
    }

    private IEnumerator ConfirmResumeWalletAccessCoroutine()
    {
        IProfileWalletAccessCoordinator coordinator = ProfileWalletAccessRegistry.Current;
        if (coordinator == null)
        {
            OnAccountFlowFailed("Wallet access service is unavailable.");
            yield break;
        }

        WalletConnectionPromptService scenePrompt = ResolveWalletPromptService();
        if (scenePrompt == null)
        {
            OnAccountFlowFailed("Wallet access prompt is unavailable.");
            yield break;
        }
        if (coordinator is ProfileWalletAccessCoordinator concreteCoordinator)
            concreteCoordinator.SetScenePromptPresenter(scenePrompt);

        _accountFlowInFlight = true;
        SetConnectButtonsInteractable(false);
        var task = coordinator.EnsureAccessAsync(new WalletAccessRequest
        {
            reason = WalletAccessReason.ResumeProfile,
            actionName = "ownership features",
            requireFreshProof = true,
            allowOfflineContinuation = true,
        });
        while (!task.IsCompleted)
            yield return null;

        _accountFlowInFlight = false;
        if (task.IsFaulted)
        {
            OnAccountFlowFailed(task.Exception?.GetBaseException().Message ?? "Wallet access failed.");
            yield break;
        }

        WalletAccessResult result = task.Result;
        if (result.kind == WalletAccessResultKind.Ready ||
            result.kind == WalletAccessResultKind.ContinuedOffline)
        {
            CacheResolvedSession(ResolveFlowOrchestrator().CurrentSession);
            StartCoroutine(PlayRouteCoroutine());
            yield break;
        }

        ApplyProfileActionLayout();
        SetConnectButtonsInteractable(true);
        SetSelectedButton(playButton);
    }

    private IEnumerator PlayRouteCoroutine()
    {
        _playInFlight = true;
        _menuState = MenuState.Routing;

        objectNavigator?.SetHotspotsInteractable(false);
        bonkDogPanel?.HideImmediate();
        pcDeskPanel?.HideImmediate();
        eventsPanel?.HideImmediate();

        SetConnectButtonsInteractable(false);
        if (playButton != null)
            playButton.interactable = false;

        menuInputController?.SetInteractionEnabled(false);

        onPlayRequested?.Invoke();

        // Stamp the cross-scene context before any scene load / intro sequence.
        PopulateGameSessionContext();
        // Start shared menu profile reads now that the canonical identity is available.
        // This intentionally does not delay narrative hydration or the scene handoff.
        _ = PlayerProfileReadStore.ResolveOrCreate().HydrateCurrentProfileAsync();

        // Narrative state must be hydrated before scenes, guards, routers, or
        // interactables can observe quest state for this profile.
        NarrativeProfileStateCoordinator narrativeCoordinator = NarrativeProfileStateCoordinator.Instance;
        var hydrationTask = narrativeCoordinator.HydrateFromCurrentSessionAsync(
            GameSessionContext.Instance != null && GameSessionContext.Instance.IsOfflineSession);
        while (!hydrationTask.IsCompleted)
            yield return null;

        if (hydrationTask.IsFaulted || !narrativeCoordinator.IsReady)
        {
            Debug.LogError($"[MainMenuSequenceController2] Narrative profile hydration failed: {hydrationTask.Exception}");
            RestorePlayRouteAfterFailure("Narrative profile hydration failed.");
            yield break;
        }

        PendingBattleResumeCoordinator pendingBattleCoordinator = PendingBattleResumeCoordinator.Instance;
        if (pendingBattleCoordinator != null)
        {
            var pendingBattleTask = pendingBattleCoordinator.ResolveCurrentAsync();
            while (!pendingBattleTask.IsCompleted)
                yield return null;

            if (pendingBattleTask.IsFaulted || pendingBattleTask.IsCanceled)
            {
                Debug.LogWarning("[MainMenuSequenceController2] Battle check did not finish. Continuing without a resume.");
            }
            else if (pendingBattleTask.Result.Outcome == PendingBattleResumeOutcome.Rejoin ||
                pendingBattleTask.Result.Outcome == PendingBattleResumeOutcome.Wipe)
            {
                yield return FadeOutMenuMusicBeforeSceneHandoff();
                bool pendingRouteAccepted = pendingBattleTask.Result.Outcome == PendingBattleResumeOutcome.Rejoin
                    ? PendingBattleRouteUtility.RequestRejoin(transitionStyle, "MainMenu.PendingBattleResume")
                    : PendingBattleRouteUtility.RequestWipe(transitionStyle, "MainMenu.PendingBattleWipe");
                if (!pendingRouteAccepted)
                    RestorePlayRouteAfterFailure("The destination scene could not be loaded.");
                yield break;
            }
            else if (!PendingBattleRouteUtility.ShouldContinueAfterProbe(pendingBattleTask.Result))
            {
                string detail = pendingBattleTask.Result.Message;
                RestorePlayRouteAfterFailure(string.IsNullOrWhiteSpace(detail)
                    ? "Signal sync is unavailable."
                    : detail);
                yield break;
            }
            else if (pendingBattleTask.Result.Outcome == PendingBattleResumeOutcome.Failed)
            {
                Debug.LogWarning(
                    "[MainMenuSequenceController2] Battle service did not answer (" +
                    pendingBattleTask.Result.Message +
                    "). Continuing.");
            }
        }

        // The durable quest row is authoritative over a stale compatibility
        // mirror returned by an older profile bootstrap response.
        if (routeMode == RouteMode.AutoFromProfile &&
            QuestService.Instance.IsQuestComplete("chainbound_intro"))
        {
            _cachedSnapshot.hasStartedGame = true;
            _cachedSnapshot.introQuestComplete = true;
            _routeDecision = DecideRoute(_cachedSnapshot, routeMode);
            PopulateGameSessionContext();
        }

        bool replayUnnamedIntro = IntroResumePolicy.ShouldReplayIntroFromBeginning(_cachedSnapshot.displayName);
        bool resumesAfterIntroCinematic =
            _routeDecision.route == PlayRoute.IntroPath &&
            _cachedSnapshot.introCinematicComplete &&
            !replayUnnamedIntro;

        if (resumesAfterIntroCinematic)
        {
            // An interrupted chainbound_intro still uses IntroPath so its
            // SequenceRunner can resume, but the persisted cinematic checkpoint
            // skips the final cinematic shot that normally fades menu music.
            // Perform that handoff here before the resumed sequence can load its
            // checkpoint scene and start the destination soundtrack.
            yield return FadeOutMenuMusicBeforeSceneHandoff();
        }

        if (_routeDecision.route == PlayRoute.IntroPath && introSequence != null)
        {
            SanctuarySpawnDirector.ClearPendingPresentation();
            _ = SequenceDirector.Instance;
            ArmIntroAbortRecovery();
            if (replayUnnamedIntro)
            {
                if (enableDebugLogs)
                    Debug.Log("[MainMenuSequenceController2] State=Routing → intro path from the start (no display name)");
                SequenceRunner.Instance?.DiscardRestoredCheckpoint(introSequence.SequenceId);
                SequenceDirector.Instance.Play(introSequence);
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log("[MainMenuSequenceController2] State=Routing → intro path (resume or play)");
                if (!SequenceDirector.Instance.ResumeHydratedSequence(
                        introSequence.SequenceId,
                        onFailed: RestorePlayRouteAfterFailure))
                {
                    SequenceDirector.Instance.Play(introSequence);
                }
            }
        }

        // Clear selection chrome before the cinematic / scene load begins.
        if (objectNavigator != null)
            yield return objectNavigator.FadeOutOutlines(outlineFadeOutBeforeRouteSeconds);

        // Intro cinematic keeps menu music through name entry; the final post-name
        // shot fades it. Direct post-intro routing still fades immediately.
        if (_routeDecision.route == PlayRoute.IntroPath)
            yield break;

        // The post-intro scene soundtrack may begin as soon as the transition is
        // accepted. Finish the menu-owned SoundEvent first so the persistent
        // AudioService cannot carry it underneath the next scene's conductor cue.
        yield return FadeOutMenuMusicBeforeSceneHandoff();

        // Post-intro path: load scene directly
        string targetSceneName = NormalizeRouteSceneName(_routeDecision);
        string targetSpawnId = ResolveDirectRouteSpawnId(_routeDecision);
        SanctuaryLatchKind continueLatchKind = SanctuaryLatchKind.Hub;
        string preferredOverworldChunkSceneId = "";
        string[] preferredOverworldChunkSceneIds = null;
        if (_routeDecision.route == PlayRoute.PostIntroPath)
        {
            SanctuaryStateService sanctuary = SanctuaryStateService.Instance;
            if (sanctuary != null)
            {
                sanctuary.GetContinueDestination(
                    out string sanctuaryScene,
                    out string sanctuarySpawn,
                    out continueLatchKind,
                    out string[] residentChunkSceneIds);
                if (!string.IsNullOrWhiteSpace(sanctuaryScene))
                    targetSceneName = sanctuaryScene;
                if (!string.IsNullOrWhiteSpace(sanctuarySpawn))
                    targetSpawnId = sanctuarySpawn;
                SanctuarySpawnDirector.ArmPendingPresentation(
                    targetSceneName,
                    targetSpawnId,
                    SanctuaryWakeKind.Continue);
                preferredOverworldChunkSceneIds = residentChunkSceneIds;
                preferredOverworldChunkSceneId = residentChunkSceneIds != null && residentChunkSceneIds.Length > 0
                    ? residentChunkSceneIds[0]
                    : "";
            }
            else
            {
                SanctuarySpawnDirector.ClearPendingPresentation();
            }
        }
        else
        {
            SanctuarySpawnDirector.ClearPendingPresentation();
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError($"[MainMenuSequenceController2] Cannot route play flow. Route={_routeDecision.route} has no target scene.");
            _playInFlight = false;
            _menuState = objectNavigator != null ? MenuState.ObjectNavigation : MenuState.PlayGroup;
            if (playButton != null)
                playButton.interactable = true;
            if (objectNavigator != null)
                ReturnToObjectNavigation(bonkDogHotspot);
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"[MainMenuSequenceController2] State=Routing → loading {targetSceneName} spawn '{targetSpawnId}' latch={continueLatchKind}");
        SceneLoader loader = SceneLoader.Instance;
        bool accepted = loader != null && loader.RequestTransition(new SceneTransitionRequest(
            targetSceneName,
            targetSpawnId,
            transitionStyle,
            sourceId: "MainMenu.PlayRoute",
            completed: result =>
            {
                if (result.Succeeded)
                    return;
                _playInFlight = false;
                if (playButton != null)
                    playButton.interactable = true;
            },
            preferredOverworldChunkSceneId: preferredOverworldChunkSceneId,
            preferredOverworldChunkSceneIds: preferredOverworldChunkSceneIds));
        if (!accepted)
        {
            _playInFlight = false;
            if (playButton != null)
                playButton.interactable = true;
        }

        yield break;
    }

    private void ArmIntroAbortRecovery()
    {
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null || _introAbortArmed)
            return;

        runner.OnSequenceAborted += HandleIntroSequenceAborted;
        _introAbortArmed = true;
    }

    private void DisarmIntroAbortRecovery()
    {
        if (!_introAbortArmed)
            return;

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner != null)
            runner.OnSequenceAborted -= HandleIntroSequenceAborted;
        _introAbortArmed = false;
    }

    private void HandleIntroSequenceAborted(SequenceAsset sequence, StepOutcome outcome)
    {
        if (introSequence == null || sequence == null ||
            !string.Equals(sequence.SequenceId, introSequence.SequenceId, System.StringComparison.Ordinal))
        {
            return;
        }

        if (!isActiveAndEnabled)
            return;

        string message = outcome.Message;
        if (string.IsNullOrWhiteSpace(message))
            message = "Intro sequence stopped.";
        RestorePlayRouteAfterFailure(message);
    }

    private void RestorePlayRouteAfterFailure(string message)
    {
        DisarmIntroAbortRecovery();
        Debug.LogError($"[MainMenuSequenceController2] Intro resume failed: {message}");
        _playInFlight = false;
        _menuState = objectNavigator != null ? MenuState.ObjectNavigation : MenuState.PlayGroup;
        menuInputController?.SetInteractionEnabled(true);
        if (playButton != null)
            playButton.interactable = true;
        SetConnectButtonsInteractable(true);
        objectNavigator?.SetHotspotsInteractable(true);
        if (objectNavigator != null)
            ReturnToObjectNavigation(bonkDogHotspot);
    }

    // ── GameSessionContext ────────────────────────────────────────────────────

    private void PopulateGameSessionContext()
    {
        // Create lazily if Bootstrap hasn't placed one under GameServices
        if (GameSessionContext.Instance == null)
        {
            var go = new GameObject("[GameSessionContext]");
            go.AddComponent<GameSessionContext>();
        }

        string spawnTag = _routeDecision.route == PlayRoute.IntroPath ? "IntroSpawn" : "DefaultSpawn";
        string questId  = _routeDecision.route == PlayRoute.IntroPath ? "chainbound_intro" : "";

        GameSessionContext.Instance.SetFromProfile(
            walletAddress   : !string.IsNullOrWhiteSpace(_cachedSnapshot.walletPublicKey) ? _cachedSnapshot.walletPublicKey : _cachedWalletAddress,
            isNewPlayer     : !_cachedSnapshot.hasStartedGame,
            introComplete   : _cachedSnapshot.introQuestComplete,
            introCinematicComplete: _cachedSnapshot.introCinematicComplete,
            desiredSpawnTag : spawnTag,
            pendingQuestId  : questId,
            isOfflineSession: _cachedSnapshot.fetchError,
            displayName     : _cachedSnapshot.displayName,
            playerProfileId : _cachedSnapshot.playerProfileId,
            authUserId      : _cachedSnapshot.authUserId,
            activePlatformBuild: _cachedSnapshot.activePlatformBuild,
            lastSignInMethod: _cachedSnapshot.lastSignInMethod,
            platformIdentityType: _cachedSnapshot.platformIdentityType,
            platformIdentitySubject: _cachedSnapshot.platformIdentitySubject,
            walletProviderType: _cachedSnapshot.walletProviderType,
            accountLinkState: _cachedSnapshot.accountLinkState,
            isGuestSession  : _cachedSnapshot.isGuestSession
        );

        if (enableDebugLogs)
            Debug.Log($"[MainMenuSequenceController2] {GameSessionContext.Instance}");
    }

    // ── Offline Fallback ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the per-wallet PlayerPrefs intro-complete flag written by
    /// MainMenuProfileServiceSupabase on the last successful fetch.
    /// </summary>
    private static MainMenuProfileSnapshot TryOfflineFallback(string walletAddress, out bool usedCache)
    {
        string key = MainMenuProfileCache.GetWalletIntroKey(walletAddress);
        if (!string.IsNullOrEmpty(walletAddress) && PlayerPrefs.HasKey(key))
        {
            bool introComplete = PlayerPrefs.GetInt(key, 0) == 1;
            usedCache = true;
            return new MainMenuProfileSnapshot
            {
                hasProfile       = true,
                hasStartedGame   = true,
                introQuestComplete = introComplete,
                displayName      = "",
                walletPublicKey  = walletAddress ?? "",
                walletProviderType = WalletProviderType.Other,
                accountLinkState = AccountLinkState.Linked,
                lastSignInMethod = LastSignInMethod.Wallet,
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                walletLinkedToProfile = true,
                fetchError       = true   // still flag offline so context knows
            };
        }

        usedCache = false;
        return default;
    }

    // ── Route Decision ────────────────────────────────────────────────────────

    private RouteDecision DecideRoute(MainMenuProfileSnapshot profile, RouteMode mode)
    {
        if (mode == RouteMode.ForceStartedIntroComplete)
        {
            return new RouteDecision
            {
                route = PlayRoute.PostIntroPath,
                targetSceneName = NormalizeSceneName(postIntroStartSceneName, DefaultPostIntroStartSceneName)
            };
        }

        if (mode == RouteMode.ForceNewGame || mode == RouteMode.ForceStartedIntroIncomplete)
        {
            return new RouteDecision
            {
                route = PlayRoute.IntroPath,
                targetSceneName = NormalizeSceneName(introSequenceStartSceneName, DefaultIntroSequenceStartSceneName)
            };
        }

        // A profile with no committed name is not started: always open the intro
        // from the beginning on this same profile. Named players still replay
        // the intro until the cinematic and the intro quest are both complete.
        bool shouldUseIntroPath = IntroResumePolicy.ShouldRouteToIntro(
            profile.hasStartedGame,
            profile.introCinematicComplete,
            profile.introQuestComplete,
            profile.displayName);
        if (shouldUseIntroPath)
        {
            return new RouteDecision
            {
                route = PlayRoute.IntroPath,
                targetSceneName = NormalizeSceneName(introSequenceStartSceneName, DefaultIntroSequenceStartSceneName)
            };
        }

        return new RouteDecision
        {
            route = PlayRoute.PostIntroPath,
            targetSceneName = NormalizeSceneName(postIntroStartSceneName, DefaultPostIntroStartSceneName)
        };
    }

    private static string NormalizeRouteSceneName(RouteDecision decision)
    {
        return NormalizeSceneName(
            decision.targetSceneName,
            decision.route == PlayRoute.IntroPath
                ? DefaultIntroSequenceStartSceneName
                : DefaultPostIntroStartSceneName);
    }

    private string ResolveDirectRouteSpawnId(RouteDecision decision)
    {
        return decision.route == PlayRoute.PostIntroPath
            ? NormalizeSceneName(postIntroSpawnId, DefaultPostIntroSpawnId)
            : "";
    }

    private static string NormalizeSceneName(string sceneName, string fallback)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? fallback : sceneName.Trim();
    }

    // ── Overlay Flick ─────────────────────────────────────────────────────────

    private void FlashOverlay(float peakAlpha, float duration)
    {
        // The full-screen startup cover is intentionally not repurposed for a
        // brightness flick while the Chainbound logo is holding the menu.
        // After dismissal it may be reactivated for the existing error/success
        // feedback flashes.
        if (contrastOverlayGroup == null || _menuState == MenuState.Boot || _menuState == MenuState.LogoGate)
            return;

        SetGroupState(contrastOverlayGroup, true, 0f, false);
        contrastOverlayGroup.DOKill();

        DOTween.Sequence()
            .Append(contrastOverlayGroup.DOFade(peakAlpha, duration * 0.3f).SetEase(Ease.OutSine))
            .Append(contrastOverlayGroup.DOFade(0f, duration * 0.7f).SetEase(Ease.InSine))
            .SetUpdate(true);
    }

    private static void ShakeButton(Button button)
    {
        if (button == null)
            return;

        button.transform.DOKill();
        button.transform
            .DOShakePosition(0.4f, new Vector3(12f, 0f, 0f), 20, 90, false, true)
            .SetUpdate(true);
    }

    // ── Menu Music ──────────────────────────────────────────────────────────

    /// <summary>
    /// Plays vinyl needle SFX then immediately starts menu music.
    /// Called once during splash logo appearance.
    /// </summary>
    private void StartMenuMusicSequence()
    {
        if (AudioService.Instance == null) return;

        if (vinylNeedleSfx != null)
            AudioService.Instance.Play(vinylNeedleSfx);

        if (menuMusicEvent != null)
        {
            _menuMusicStartVolume = Mathf.Clamp01(0.5f * (menuMusicEvent.VolumeMin + menuMusicEvent.VolumeMax));
            _menuMusicHandle = AudioService.Instance.Play(menuMusicEvent);
        }
    }

    /// <summary>
    /// Gradually fades out the menu music (volume only).
    /// </summary>
    public void FadeOutMenuMusic(float fadeSeconds = -1f)
    {
        StopMenuMusicCreepCoroutine();
        if (!_menuMusicHandle.IsValid)
            return;

        float seconds = fadeSeconds >= 0f ? fadeSeconds : musicFadeOutSeconds;
        _menuMusicHandle.Stop(seconds);
    }

    private IEnumerator FadeOutMenuMusicBeforeSceneHandoff()
    {
        StopMenuMusicCreepCoroutine();
        if (!_menuMusicHandle.IsValid)
            yield break;

        float seconds = Mathf.Max(0f, directRouteMusicFadeOutSeconds);
        _menuMusicHandle.Stop(seconds);
        if (seconds > 0f)
            yield return new WaitForSecondsRealtime(seconds);

        // Stop once more as a defensive terminal handoff. AudioService removes
        // an already-faded handle, so this is harmless when the fade completed.
        _menuMusicHandle.Stop(0f);
        _menuMusicHandle = default;
    }

    private void StopMenuMusicImmediate()
    {
        StopMenuMusicCreepCoroutine();
        if (!_menuMusicHandle.IsValid)
            return;

        _menuMusicHandle.Stop(0f);
        _menuMusicHandle = default;
    }

    /// <summary>
    /// Dying-battery style music decay: volume to 0, pitch down, with light flutter.
    /// Uses unscaled time so it tracks the sprite cinematic sleep beat.
    /// </summary>
    public void FadeOutMenuMusicWithCreep(
        float durationSeconds,
        float pitchEnd = 0.72f,
        float flutterHz = 0.35f,
        float flutterDepth = 0.04f)
    {
        StopMenuMusicCreepCoroutine();
        if (!_menuMusicHandle.IsValid)
            return;

        float duration = Mathf.Max(0.01f, durationSeconds);
        _menuMusicCreepCoroutine = StartCoroutine(FadeOutMenuMusicWithCreepRoutine(
            duration,
            pitchEnd,
            flutterHz,
            flutterDepth));
    }

    private IEnumerator FadeOutMenuMusicWithCreepRoutine(
        float durationSeconds,
        float pitchEnd,
        float flutterHz,
        float flutterDepth)
    {
        float startVolume = Mathf.Max(0.01f, _menuMusicStartVolume);
        float elapsed = 0f;

        while (elapsed < durationSeconds && _menuMusicHandle.IsValid)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            float remaining = 1f - t;
            float flutter = Mathf.Sin(elapsed * flutterHz * Mathf.PI * 2f) * flutterDepth * remaining;
            float pitch = Mathf.Lerp(1f, pitchEnd, t) + flutter;
            float volume = Mathf.Lerp(startVolume, 0f, t);

            _menuMusicHandle.SetPitch(pitch);
            _menuMusicHandle.SetVolume(volume);
            yield return null;
        }

        if (_menuMusicHandle.IsValid)
        {
            _menuMusicHandle.SetVolume(0f);
            _menuMusicHandle.Stop(0f);
        }

        _menuMusicCreepCoroutine = null;
    }

    private void StopMenuMusicCreepCoroutine()
    {
        if (_menuMusicCreepCoroutine == null)
            return;

        StopCoroutine(_menuMusicCreepCoroutine);
        _menuMusicCreepCoroutine = null;
    }

    private void StartPressStartPrompt()
    {
        StopPressStartPrompt();
        _pressStartPromptCoroutine = StartCoroutine(RunPressStartPromptCoroutine());
    }

    private void StopPressStartPrompt()
    {
        if (_pressStartPromptCoroutine != null)
        {
            StopCoroutine(_pressStartPromptCoroutine);
            _pressStartPromptCoroutine = null;
        }

        SetPressStartPromptVisible(false);
    }

    private IEnumerator RunPressStartPromptCoroutine()
    {
        EnsurePressStartPrompt();
        SetPressStartPromptVisible(false);

        float delay = Mathf.Max(0f, pressStartDelayAfterSettleSeconds);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        float onSeconds = Mathf.Max(0.05f, pressStartFlashOnSeconds);
        float offSeconds = Mathf.Max(0.05f, pressStartFlashOffSeconds);

        while (IsAwaitingMenuStart)
        {
            SetPressStartPromptVisible(true);
            yield return new WaitForSecondsRealtime(onSeconds);
            if (!IsAwaitingMenuStart)
                break;

            SetPressStartPromptVisible(false);
            yield return new WaitForSecondsRealtime(offSeconds);
        }

        SetPressStartPromptVisible(false);
        _pressStartPromptCoroutine = null;
    }

    private void EnsurePressStartPrompt()
    {
        if (pressStartPromptText != null)
            return;

        Transform parent = gameLogoTransform != null ? gameLogoTransform : transform;
        GameObject promptObject = new GameObject(
            "PressStartPrompt",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        promptObject.transform.SetParent(parent, false);

        RectTransform rect = promptObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640f, 48f);
        rect.anchoredPosition = pressStartAnchoredPosition;

        TextMeshProUGUI label = promptObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset pixeloid = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
        if (pixeloid != null)
            label.font = pixeloid;
        else
            label.font = TMP_Settings.defaultFontAsset;

        label.text = string.IsNullOrWhiteSpace(pressStartMessage)
            ? "PRESS ANY BUTTON"
            : pressStartMessage.Trim().ToUpperInvariant();
        label.fontSize = pressStartFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        pressStartPromptText = label;
        promptObject.SetActive(false);
    }

    private void SetPressStartPromptVisible(bool visible)
    {
        if (pressStartPromptText == null)
            return;

        pressStartPromptText.gameObject.SetActive(visible);
        if (visible)
        {
            pressStartPromptText.enabled = true;
            pressStartPromptText.text = string.IsNullOrWhiteSpace(pressStartMessage)
                ? "PRESS ANY BUTTON"
                : pressStartMessage.Trim().ToUpperInvariant();
        }
    }

    // ── Animation Helpers ─────────────────────────────────────────────────────

    private RectTransform ResolveForegroundMover()
    {
        return foregroundMotionRoot != null ? foregroundMotionRoot : foregroundTransform;
    }

    private static float DurationFromSpeed(float distanceY, float pixelsPerSecond)
    {
        return Mathf.Max(0.01f, Mathf.Abs(distanceY) / Mathf.Max(1f, pixelsPerSecond));
    }

    private IEnumerator MoveRect(RectTransform rect, Vector2 target, float durationSeconds)
    {
        if (rect == null)
            yield break;

        rect.DOKill(complete: false);
        float duration = Mathf.Max(0.01f, durationSeconds);

        yield return rect.DOAnchorPos(target, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .WaitForCompletion();
    }

    private IEnumerator MoveAndFadeIn(RectTransform rect, CanvasGroup group, Vector2 target, float durationSeconds)
    {
        if (rect == null || group == null)
            yield break;

        rect.DOKill(complete: false);
        group.DOKill(complete: false);
        group.alpha = 0f;

        float duration = Mathf.Max(0.01f, durationSeconds);
        var moveTween = rect.DOAnchorPos(target, duration).SetEase(Ease.OutCubic).SetUpdate(true);
        var fadeTween = group.DOFade(1f, duration).SetEase(Ease.Linear).SetUpdate(true);

        yield return DOTween.Sequence().Join(moveTween).Join(fadeTween).SetUpdate(true).WaitForCompletion();
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float durationSeconds)
    {
        if (group == null)
            yield break;

        group.alpha = from;

        if (durationSeconds <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        group.DOKill(complete: false);
        yield return group.DOFade(to, durationSeconds).SetEase(Ease.Linear).SetUpdate(true).WaitForCompletion();
    }

    private IEnumerator AnimateButtonIn(Button button, Vector3 targetScale, float startDelaySeconds)
    {
        if (button == null)
            yield break;

        if (startDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startDelaySeconds);

        Transform t = button.transform;
        t.DOKill(complete: false);

        yield return t.DOScale(targetScale, Mathf.Max(0.01f, buttonPopSeconds))
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .WaitForCompletion();
    }

    // ── Explicit Navigation ─────────────────────────────────────────────────

    /// <summary>
    /// Sets Explicit navigation on all menu buttons so D-pad/WASD Up/Down
    /// moves between them reliably. No looping — pressing up on the top
    /// button or down on the bottom button does nothing.
    /// </summary>
    private void ConfigureExplicitNavigation()
    {
        // Connect group: ConnectWallet (top) ↔ Settings (bottom)
        SetVerticalChain(continueAsGuestButton, connectWalletButton, restoreAccountButton, settingsButton);

        // Play group: Play (top) ↔ Disconnect (bottom, optional)
        SetVerticalChain(playButton, linkWalletButton, disconnectButton);
    }

    private static void SetVerticalPair(Button top, Button bottom)
    {
        if (top != null)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = bottom
            };
            top.navigation = nav;
        }

        if (bottom != null)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = top
            };
            bottom.navigation = nav;
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void SetButtonToStartScale(Button button, Vector3 targetScale)
    {
        if (button == null) return;
        button.transform.localScale = new Vector3(targetScale.x * 0.82f, targetScale.y * 0.82f, targetScale.z);
    }

    private static void SetVerticalChain(params Button[] buttons)
    {
        Button previous = null;
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button current = buttons[i];
            if (current == null)
                continue;

            Button next = null;
            for (int j = i + 1; j < buttons.Length; j++)
            {
                if (buttons[j] != null)
                {
                    next = buttons[j];
                    break;
                }
            }

            current.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = previous,
                selectOnDown = next,
            };

            previous = current;
        }
    }

    private bool ShouldShowLinkWalletAction()
    {
        return _routeReady
            && !string.IsNullOrWhiteSpace(_cachedSnapshot.playerProfileId)
            && !string.IsNullOrWhiteSpace(_cachedSnapshot.walletPublicKey)
            && !_cachedSnapshot.walletLinkedToProfile;
    }

    private MainMenuFlowOrchestrator ResolveFlowOrchestrator()
    {
        if (flowOrchestrator != null)
        {
            flowOrchestrator.ConfigureEditorWalletMode(editorWalletMode);
            return flowOrchestrator;
        }

        flowOrchestrator = GetComponent<MainMenuFlowOrchestrator>();
        if (flowOrchestrator == null)
            flowOrchestrator = FindObjectOfType<MainMenuFlowOrchestrator>();
        if (flowOrchestrator == null)
            flowOrchestrator = gameObject.AddComponent<MainMenuFlowOrchestrator>();

        flowOrchestrator.ConfigureEditorWalletMode(editorWalletMode);
        return flowOrchestrator;
    }

    /// <summary>
    /// Restore Profile must be scene-authored (same shimmer / sprite-swap template
    /// as Options). Runtime cloning is intentionally not used.
    /// </summary>
    private void EnsureRestoreProfileButton()
    {
        if (restoreAccountButton != null)
            return;

        Debug.LogWarning(
            "[MainMenuSequenceController2] restoreAccountButton is not assigned. " +
            "Author RestoreProfileButton under ConnectSettingsGroup and wire it on this controller.");
    }

    /// <summary>
    /// The authored menu uses separate CanvasGroups for the fresh and started
    /// action states. They share the menu canvas, so wrap both in one runtime
    /// safe-area container without disturbing the existing animation wiring.
    /// </summary>
    private void EnsurePrimaryActionSafeAreaContainer()
    {
        if (connectSettingsGroup == null || playButtonGroup == null)
            return;

        RectTransform parent = connectSettingsGroup.transform.parent as RectTransform;
        if (parent == null || playButtonGroup.transform.parent != parent)
            return;

        _primaryActionSafeArea = parent.Find("PrimaryActionSafeArea") as RectTransform;
        if (_primaryActionSafeArea == null)
        {
            GameObject container = new GameObject("PrimaryActionSafeArea", typeof(RectTransform));
            _primaryActionSafeArea = container.GetComponent<RectTransform>();
            _primaryActionSafeArea.SetParent(parent, false);
            connectSettingsGroup.transform.SetParent(_primaryActionSafeArea, false);
            playButtonGroup.transform.SetParent(_primaryActionSafeArea, false);
        }

        ApplyPrimaryActionSafeArea();
    }

    private void ApplyPrimaryActionSafeArea()
    {
        if (_primaryActionSafeArea == null)
            return;

        Vector2 screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        Rect safe = Screen.safeArea;
        _primaryActionSafeArea.anchorMin = safe.position / screenSize;
        _primaryActionSafeArea.anchorMax = (safe.position + safe.size) / screenSize;
        _primaryActionSafeArea.offsetMin = Vector2.zero;
        _primaryActionSafeArea.offsetMax = Vector2.zero;
        _lastPrimaryActionSafeArea = safe;
        _lastPrimaryActionScreenSize = screenSize;
    }

    private void SetConnectButtonsInteractable(bool interactable)
    {
        bonkDogPanel?.SetActionsInteractable(interactable);
        if (continueAsGuestButton != null && continueAsGuestButton.gameObject.activeInHierarchy) continueAsGuestButton.interactable = interactable;
        if (connectWalletButton != null && connectWalletButton.gameObject.activeInHierarchy) connectWalletButton.interactable = interactable;
        if (playButton != null && playButton.gameObject.activeInHierarchy) playButton.interactable = interactable;
        if (restoreAccountButton != null && restoreAccountButton.gameObject.activeInHierarchy) restoreAccountButton.interactable = interactable;
        if (settingsButton != null && settingsButton.gameObject.activeInHierarchy) settingsButton.interactable = interactable;
    }

    private static void SetGroupState(CanvasGroup group, bool active, float alpha, bool interactable)
    {
        if (group == null) return;
        if (group.gameObject.activeSelf != active)
            group.gameObject.SetActive(active);
        group.alpha = alpha;
        group.blocksRaycasts = interactable;
        group.interactable = interactable;
    }

    private void SetSelectedButton(Button button)
    {
        if (button == null) return;

        if (menuInputController != null)
        {
            menuInputController.Select(button);
            return;
        }

        if (EventSystem.current == null) return;
        // Switch directly so a button's deselect animation is not forced to
        // its rest scale for an intermediate frame.
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void KillTweensOnTargets()
    {
        splashGroup?.DOKill();
        backgroundGroup?.DOKill();
        objectLayersGroup?.DOKill();
        foregroundGroup?.DOKill();
        foregroundVinylGroup?.DOKill();
        bubbleButtonsGroup?.DOKill();
        mobileHudRootGroup?.DOKill();
        ResolveForegroundMover()?.DOKill();
        gameLogoGroup?.DOKill();
        gameLogoTransform?.DOKill();
        contrastOverlayGroup?.DOKill();
        MainMenuObjectHotspot[] hotspots = GetObjectHotspots();
        for (int i = 0; i < hotspots.Length; i++)
        {
            hotspots[i]?.VisualRoot?.DOKill();
            hotspots[i]?.FillImage?.DOKill(false);
        }
        connectSettingsGroup?.DOKill();
        playButtonGroup?.DOKill();
        if (continueAsGuestButton != null) continueAsGuestButton.transform.DOKill();
        if (connectWalletButton != null) connectWalletButton.transform.DOKill();
        if (restoreAccountButton != null) restoreAccountButton.transform.DOKill();
        if (settingsButton != null) settingsButton.transform.DOKill();
        if (playButton != null) playButton.transform.DOKill();
        if (linkWalletButton != null) linkWalletButton.transform.DOKill();
    }
}
