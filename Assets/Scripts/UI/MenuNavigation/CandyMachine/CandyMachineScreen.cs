using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public enum CandyMachineMode
    {
        GetKnown = 0,
        Stamp = 1,
    }

    public enum CandyMachinePresentationState
    {
        Browse,
        Review,
        Committing,
        PendingSettlement,
        Pulling,
        Revealing,
        AwaitingConfirm,
    }

    public enum CandyMachineNavLayer
    {
        Mode,
        Browse,
        Detail,
    }

    public enum CandyMachineRuntimeMode
    {
        LocalPreview = 0,
        DevnetOwnership = 1,
    }

    /// <summary>
    /// Academy candy-machine overlay. PlayerMenu chrome + Inventory safe-area insets.
    /// Editor preview may use local inventory. Android development players always use the
    /// authoritative devnet ownership path; release players remain fail-closed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CandyMachineScreen : MonoBehaviour, IMenuContentInputHandler, IMenuBlockedInputSilence
    {
        public const string InputLockReason = "CandyMachine";
        public const string MachineTitle = "Candy Machine";
        public const int OverlaySortingOrder = 860;

        /// <summary>One candy Stamp fills the next empty slot only; the panel never offers a 1/2 mod picker.</summary>
        public const int StampSlotsPerAction = 1;

        private const int MaxStampSlots = 2;
        private const int RecentChipCount = 4;

        private static readonly Color PanelColor = new Color(0.07f, 0.095f, 0.14f, 0.92f);
        private static readonly Color CardColor = new Color(0.10f, 0.13f, 0.18f, 0.94f);
        private static readonly Color SlotFilledColor = new Color(0.16f, 0.22f, 0.30f, 0.96f);
        private static readonly Color SlotEmptyColor = new Color(0.08f, 0.11f, 0.15f, 0.90f);
        private static readonly Color SlotNextColor = new Color(0.85f, 0.45f, 0.13f, 0.95f);
        private static readonly Color TabActiveColor = new Color(0.85f, 0.45f, 0.13f, 1f);
        private static readonly Color TabIdleColor = new Color(0.85f, 0.45f, 0.13f, 0f);

        // Button plates ship as light 9-slice art, so their labels are ink rather than menu white.
        private static readonly Color ButtonInk = new Color(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color ButtonInkMuted = new Color(0.08f, 0.09f, 0.12f, 0.55f);

        [Header("Spine (production, fail-closed)")]
        [SerializeField] private CandyMachineRuntimeMode runtimeMode = CandyMachineRuntimeMode.LocalPreview;
        [SerializeField] private bool enableDevnetOwnershipSpine;
        [SerializeField] private MonoBehaviour ownershipGateway;
        [SerializeField] private SolanaConfigSO solanaConfig;
        [SerializeField, Min(1)] private int supportedSchemaVersion = 1;

        [Header("Catalogs")]
        [SerializeField] private VendingConfigSO vendingConfig;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [SerializeField] private CandyMachineDevConfigSO devConfig;

        [Header("View")]
        [SerializeField] private CanvasGroup presentationGroup;
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text ramLabel;
        [SerializeField] private Button folioTabButton;
        [SerializeField] private Button stampTabButton;
        [SerializeField] private Image folioTabMarker;
        [SerializeField] private Image stampTabMarker;

        [Header("View / Browse column")]
        [SerializeField] private TMP_Text browseHeader;
        [SerializeField] private TMP_Text browseSubheader;
        [SerializeField] private RectTransform folioBrowse;
        [SerializeField] private RectTransform stampBrowse;
        [SerializeField] private TMP_Text oddsLabel;
        [SerializeField] private TMP_Text payoutLabel;
        [SerializeField] private RectTransform recentContent;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private TMP_Text listFooter;

        [Header("View / Detail column")]
        [SerializeField] private TMP_Text detailHeader;
        [SerializeField] private TMP_Text detailSubheader;
        [SerializeField] private TMP_Text previewCaption;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailMeta;
        [SerializeField] private RectTransform slotRow;
        [SerializeField] private Image slotOneFrame;
        [SerializeField] private TMP_Text slotOneCaption;
        [SerializeField] private TMP_Text slotOneValue;
        [SerializeField] private Image slotTwoFrame;
        [SerializeField] private TMP_Text slotTwoCaption;
        [SerializeField] private TMP_Text slotTwoValue;
        [SerializeField] private RectTransform folioMetaRow;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private TMP_Text tokensLabel;
        [SerializeField] private TMP_Text reviewHint;
        [SerializeField] private Button primaryButton;

        [Header("View / Pull and reveal")]
        [SerializeField] private CanvasGroup bodyRow;
        [SerializeField] private CandyMachineDetailBodyView detailBodyView;
        [SerializeField] private CandyMachineResultView resultView;
        [SerializeField] private UnityEngine.Object revealStinger;
        [SerializeField] private UnityEngine.Object revealImpactStinger;
        [SerializeField] private UnityEngine.Object revealSlideStinger;
        [SerializeField] private UnityEngine.Object revealTypeSticker;
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        [Header("View / Shell")]
        [SerializeField] private PlayerMenuChromePalette chrome = new PlayerMenuChromePalette();
        [SerializeField] private MenuInputContextController inputContext;
        [SerializeField] private CandyMachineDevGrantPanel grantPanel;
        [Header("Editor")]
        [Tooltip("Destructive. When false, BuildDefaultLayoutForEditor only binds existing hierarchy and will not rebuild chrome or restyle Background.")]
        [SerializeField] private bool allowEditorShellRebuild;
        [SerializeField] private Selectable defaultSelection;

        private static CandyMachineScreen instance;

        private readonly List<Button> knownRowButtons = new List<Button>();
        private readonly List<GameObject> recentChips = new List<GameObject>();
        private IKnownMoveService knownMoves;
        private IStampedMoveService stampedMoves;
        private IOwnershipSpineRuntimeService ownershipService;
        private DevCandyMachineInventory devInventory;
        private CandyMachineMode mode = CandyMachineMode.GetKnown;
        private int selectedKnownIndex;
        private StampMoveRequest reviewedRequest;
        private InputLockHandle inputLockHandle;
        private ICandyMachinePullDirector pullDirector;
        private CandyMachineRevealData pendingReveal;
        private Coroutine revealCoroutine;
        private Coroutine authoredShellLayoutRoutine;
        private MonoBehaviour cachedSfxPlayerBehaviour;
        private CandyMachinePresentationState presentationState = CandyMachinePresentationState.Browse;
        private bool blockedInputMuted;
        private readonly List<string> revealLog = new List<string>();
        public bool MuteBlockedInput => blockedInputMuted;
        private CandyMachineNavLayer navLayer = CandyMachineNavLayer.Mode;
        private bool showing;
        private CancellationTokenSource stampReconciliationCancellation;
        private bool stampReconciliationRunning;
        private bool settlementPullStarted;
        private bool pullAnimationReady;

        public static CandyMachineScreen Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                instance = FindFirstObjectByType<CandyMachineScreen>(FindObjectsInactive.Include);
                return instance;
            }
        }

        public bool IsShowing => showing;

        public CandyMachinePresentationState PresentationState => presentationState;

        public CandyMachineNavLayer NavLayer => navLayer;

        public DevCandyMachineInventory DevInventory => devInventory;

        public CandyMachineRuntimeMode EffectiveRuntimeMode =>
            ResolveRuntimeMode(runtimeMode, Application.isEditor, Debug.isDebugBuild);

        public bool UsesDevnetOwnership => EffectiveRuntimeMode == CandyMachineRuntimeMode.DevnetOwnership;

        public bool CanShowDevGrantPanel =>
            DevCandyMachineInventory.IsDevSurfaceAllowed && (devInventory != null || ownershipService != null);

        public long DisplayRamBalance => ownershipService != null
            ? ownershipService.RamBalance
            : devInventory != null ? devInventory.RamBalance : 0L;

        public PlayerMenuChromePalette Chrome => chrome;

        public bool EnableDevnetOwnershipSpine => enableDevnetOwnershipSpine;

        public void Configure(
            VendingConfigSO vending,
            MoveDatabaseSO moves,
            ModifierCatalogSO modifiers,
            CandyMachineDevConfigSO grants)
        {
            vendingConfig = vending;
            moveDatabase = moves;
            modifierCatalog = modifiers;
            devConfig = grants;
        }

        public bool Open() => Open(null);

        public bool Open(ICandyMachinePullDirector director)
        {
            CancelStampReconciliation();
            gameObject.SetActive(true);
            BindFromHierarchy();
            RequestAuthoredShellLayoutProtection();
            if (root == null)
            {
                SetInputContextActive(false);
                return false;
            }

            EnsureServices();
            if (grantPanel != null)
                grantPanel.Bind(this);
            pullDirector = director;
            presentationState = CandyMachinePresentationState.Browse;
            pendingReveal = null;
            settlementPullStarted = false;
            pullAnimationReady = false;
            showing = true;
            SetInputContextActive(true);
            SetOverlayVisible(true);
            SetBrowseRevealVisibility(true);
            AcquireInputLock();
            WireInput();
            mode = CandyMachineMode.GetKnown;
            navLayer = CandyMachineNavLayer.Mode;
            reviewedRequest = null;
            RefreshPresentation();
            if (ownershipService != null)
                RefreshOwnershipStateOnOpen();
            if (grantPanel != null)
                grantPanel.RefreshVisibility();
            PlayUiCue(PlayerMenuSfxCue.MenuOpen);
            FocusModeTab();
            return true;
        }

        public void Close()
        {
            if (IsInteractionLocked())
                return;
            CloseImmediate();
        }

        private void CloseImmediate()
        {
            CancelStampReconciliation();
            showing = false;
            PlayUiCue(PlayerMenuSfxCue.MenuClose);
            SetInputContextActive(false);
            reviewedRequest = null;
            StopRevealCoroutine();
            DOTween.Kill(this);
            pullDirector?.CancelAndRestore();
            pullDirector = null;
            pendingReveal = null;
            settlementPullStarted = false;
            pullAnimationReady = false;
            presentationState = CandyMachinePresentationState.Browse;
            navLayer = CandyMachineNavLayer.Mode;
            SetOverlayVisible(false);
            ReleaseInputLock();
            SetBlockedInputMuted(false);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void RefreshPresentation()
        {
            RefreshRam();
            RefreshModeVisuals();
            RefreshList();
            RefreshDetail();
            RefreshActions();
            RefreshNavigation();
            if (grantPanel != null)
                grantPanel.RefreshVisibility();
        }

        /// <summary>
        /// Mode is the hub. Known never puts Browse in the graph. Stamp Browse is U/D
        /// only; Detail is the isolated confirm CTA. Back pops a layer; Mode Back exits.
        /// </summary>
        private void RefreshNavigation()
        {
            IsolateNavigation(folioTabButton);
            IsolateNavigation(stampTabButton);
            IsolateNavigation(primaryButton);
            for (int i = 0; i < knownRowButtons.Count; i++)
                IsolateNavigation(knownRowButtons[i]);

            if (navLayer == CandyMachineNavLayer.Mode)
            {
                SetNavigation(folioTabButton, null, stampTabButton, null, null);
                SetNavigation(stampTabButton, folioTabButton, null, null, null);
                return;
            }

            if (navLayer != CandyMachineNavLayer.Browse)
                return;

            Selectable lastRow = null;
            for (int i = 0; i < knownRowButtons.Count; i++)
            {
                Button row = knownRowButtons[i];
                if (row == null || !row.interactable)
                    continue;

                SetNavigation(row, null, null, lastRow, null);
                if (lastRow != null)
                {
                    Navigation previous = lastRow.navigation;
                    previous.selectOnDown = row;
                    lastRow.navigation = previous;
                }

                lastRow = row;
            }
        }

        private static void IsolateNavigation(Selectable target)
        {
            SetNavigation(target, null, null, null, null);
        }

        private static void SetNavigation(
            Selectable target,
            Selectable left,
            Selectable right,
            Selectable up,
            Selectable down)
        {
            if (target == null)
                return;

            target.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
                selectOnUp = up,
                selectOnDown = down,
            };
        }

        public void BuildDefaultLayoutForEditor()
        {
            ConfigureOverlayCanvas();
            if (!allowEditorShellRebuild)
            {
                BindFromHierarchy();
                ProtectBackgroundPresentation();
                ProtectAuthoredShellLayout();
                Debug.LogWarning(
                    "CandyMachineScreen.BuildDefaultLayoutForEditor skipped shell rebuild (allowEditorShellRebuild is false). Bind-only.",
                    this);
                return;
            }

            EnsureHierarchy();
            BindFromHierarchy();
            ProtectBackgroundPresentation();

            // Author the prefab in its opening state: FOLIO tab, browse cards, Get Known CTA.
            mode = CandyMachineMode.GetKnown;
            RefreshModeVisuals();
            RefreshDetail();
            RefreshActions();
            SetText(oddsLabel, "COMMON 60% | RARE 30% | LEGEND 10%");
            detailBodyView?.Show(CandyMachineDetailBodyMode.Idle, "FOLIO", "READY | FOLIO TOKEN AVAILABLE", "CONFIRM TO PULL");
        }

        private void Awake()
        {
            instance = this;
            BindFromHierarchy();
            ProtectBackgroundPresentation();
            RequestAuthoredShellLayoutProtection();
            if (!showing)
            {
                SetInputContextActive(false);
                SetOverlayVisible(false);
            }
        }

        private void OnEnable()
        {
            if (instance == null)
                instance = this;
            BindFromHierarchy();
            RequestAuthoredShellLayoutProtection();
            if (!showing)
            {
                SetInputContextActive(false);
                SetOverlayVisible(false);
            }
        }

        private void OnDisable()
        {
            SetInputContextActive(false);
            UnwireInput();
            StopAuthoredShellLayoutProtection();
            StopRevealCoroutine();
            DOTween.Kill(this);
            pullDirector?.CancelAndRestore();
            pullDirector = null;
            pendingReveal = null;
            settlementPullStarted = false;
            pullAnimationReady = false;
            presentationState = CandyMachinePresentationState.Browse;
            navLayer = CandyMachineNavLayer.Mode;
            showing = false;
            ReleaseInputLock();
        }

        /// <summary>
        /// A hidden Candy Machine must not retain UI action subscriptions or a
        /// MobileHudContext.Menu token. The presenter lives under GameServices and
        /// may be authored active, so Canvas visibility alone is not an input gate.
        /// </summary>
        private void SetInputContextActive(bool active)
        {
            if (inputContext == null)
                inputContext = GetComponent<MenuInputContextController>();
            if (inputContext == null)
                return;

            inputContext.SetInteractionEnabled(active);
            inputContext.enabled = active;
        }

        private void OnDestroy()
        {
            StopAuthoredShellLayoutProtection();
            DOTween.Kill(this);
            pullDirector?.CancelAndRestore();
            if (instance == this)
                instance = null;
            ReleaseInputLock();
        }

        private void SetOverlayVisible(bool visible)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = visible;

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = visible;

            Transform background = transform.Find("Background");
            if (background != null)
                background.gameObject.SetActive(visible);

            PlayerMenuBackgroundController backgroundController =
                background != null
                    ? background.GetComponent<PlayerMenuBackgroundController>()
                    : GetComponentInChildren<PlayerMenuBackgroundController>(true);
            if (backgroundController != null)
                backgroundController.SetBaseTintBlocksRaycasts(visible);

            if (root != null)
            {
                root.alpha = visible ? 1f : 0f;
                root.interactable = visible;
                root.blocksRaycasts = visible;
                root.gameObject.SetActive(visible);
            }

            if (presentationGroup != null)
            {
                presentationGroup.alpha = visible ? 1f : 0f;
                presentationGroup.interactable = visible;
                presentationGroup.blocksRaycasts = visible;
            }
        }

        private void EnsureServices()
        {
            ResolveOwnershipDependencies();
            knownMoves = null;
            stampedMoves = null;
            ownershipService = null;

            if (EffectiveRuntimeMode == CandyMachineRuntimeMode.LocalPreview
                && DevCandyMachineInventory.IsDevSurfaceAllowed)
            {
                if (devInventory == null)
                {
                    int seed = devConfig != null ? devConfig.Seed : 1776;
                    devInventory = DevCandyMachineInventory.FromCatalogs(
                        vendingConfig,
                        moveDatabase,
                        modifierCatalog,
                        seed);
                }

                knownMoves = devInventory;
                stampedMoves = devInventory;
                return;
            }

            devInventory = null;
            bool devnetEnabled = enableDevnetOwnershipSpine;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            devnetEnabled = true;
#endif
            if (EffectiveRuntimeMode == CandyMachineRuntimeMode.DevnetOwnership
                && devnetEnabled
                && ownershipGateway is IOwnershipSpineGateway gateway
                && solanaConfig != null
                && !string.IsNullOrWhiteSpace(solanaConfig.coreMovesCollectionId)
                && !string.IsNullOrWhiteSpace(solanaConfig.coreMovesUpdateAuthority))
            {
                var service = new OwnershipSpineService(
                    gateway,
                    solanaConfig.coreMovesCollectionId,
                    solanaConfig.coreMovesUpdateAuthority,
                    supportedSchemaVersion,
                    moveDatabase,
                    modifierCatalog);
                knownMoves = service;
                stampedMoves = service;
                ownershipService = service;
                return;
            }

            knownMoves = null;
            stampedMoves = null;
            SetStatus("Get Known and Stamp need the Ownership Spine on devnet.");
        }

        private void ResolveOwnershipDependencies()
        {
            if (ownershipGateway == null)
            {
                Transform rootTransform = transform.root;
                SupabaseOwnershipSpineGateway localGateway = rootTransform != null
                    ? rootTransform.GetComponentInChildren<SupabaseOwnershipSpineGateway>(true)
                    : null;
                ownershipGateway = localGateway != null
                    ? localGateway
                    : FindFirstObjectByType<SupabaseOwnershipSpineGateway>(FindObjectsInactive.Include);
            }

            if (solanaConfig == null && SolanaServiceBridge.Instance != null)
                solanaConfig = SolanaServiceBridge.Instance.Config;
        }

        private bool HasFeatureServices() =>
            devInventory != null || (ownershipService != null && knownMoves != null && stampedMoves != null);

        public static CandyMachineRuntimeMode ResolveRuntimeMode(
            CandyMachineRuntimeMode configuredMode,
            bool isEditor,
            bool isDevelopmentBuild)
        {
            if (isEditor)
                return configuredMode;

            // LocalPreview must never escape into a device player. Development APKs are
            // ownership smoke-test artifacts; release players remain on the fail-closed path.
            if (isDevelopmentBuild)
                return CandyMachineRuntimeMode.DevnetOwnership;
            return CandyMachineRuntimeMode.DevnetOwnership;
        }

        public async Task RefreshOwnershipStateAsync()
        {
            if (ownershipService == null)
                return;

            await ownershipService.RefreshAsync();
            if (showing)
                RefreshPresentation();
        }

        private async void RefreshOwnershipStateOnOpen()
        {
            try
            {
                await RefreshOwnershipStateAsync();
                if (showing && ownershipService?.PendingStamp != null)
                    await ResumePendingStampAsync(ownershipService.PendingStamp);
            }
            catch (Exception exception)
            {
                if (showing)
                    ShowOperationError($"Ownership inventory refresh failed: {exception.Message}");
                Debug.LogError($"[CandyMachineScreen] Ownership hydration failed: {exception}");
            }
        }

        public Task<DevnetRamGrantReview> ReviewDevnetRamGrantAsync(string idempotencyKey)
        {
            if (ownershipService == null)
                throw new InvalidOperationException("Devnet ownership service is unavailable.");
            return ownershipService.ReviewDevnetRamGrantAsync(idempotencyKey);
        }

        public Task<DevnetRamGrantResult> SubmitDevnetRamGrantAsync(string idempotencyKey)
        {
            if (ownershipService == null)
                throw new InvalidOperationException("Devnet ownership service is unavailable.");
            return ownershipService.GrantDevnetRamAsync(idempotencyKey);
        }

        public async Task<KnownMoveRecord> RollDevnetKnownAsync()
        {
            if (ownershipService == null)
                throw new InvalidOperationException("Devnet ownership service is unavailable.");
            KnownMoveRecord result = await ownershipService.GetKnownAsync(Guid.NewGuid().ToString("N"));
            RefreshPresentation();
            return result;
        }

        private bool CanUseFeature()
        {
            if (HasFeatureServices())
                return true;
            SetStatus("Get Known and Stamp need the Ownership Spine on devnet.");
            return false;
        }

        private async void RollKnown()
        {
            if (!CanUseFeature() || knownMoves == null)
                return;

            presentationState = CandyMachinePresentationState.Committing;
            SetBlockedInputMuted(true);
            SetPending(true, "Rolling Known...");
            try
            {
                KnownMoveRecord result = await knownMoves.GetKnownAsync(Guid.NewGuid().ToString("N"));
                if (!IsVerifiedKnown(result) || !ContainsCachedKnown(result.instanceId))
                {
                    ShowOperationError("Known roll was not verified.");
                    return;
                }

                if (moveDatabase == null || moveDatabase.GetById(result.moveId) == null)
                {
                    ShowOperationError("Verified Known move is missing authored display data.");
                    return;
                }

                selectedKnownIndex = Mathf.Max(0, knownMoves.CachedKnown.Count - 1);
                BeginCommittedPull(BuildKnownReveal(result));
            }
            catch (Exception exception)
            {
                ShowOperationError(exception.Message);
            }
        }

        private async void StampSelected()
        {
            if (!CanUseFeature() || knownMoves == null || stampedMoves == null)
                return;
            if (ownershipService?.PendingStamp != null)
            {
                await ResumePendingStampAsync(ownershipService.PendingStamp);
                return;
            }
            if (knownMoves.CachedKnown.Count == 0)
            {
                ShowOperationError("Grant or roll a Known move before Stamp.");
                return;
            }

            selectedKnownIndex = Mathf.Clamp(selectedKnownIndex, 0, knownMoves.CachedKnown.Count - 1);
            KnownMoveRecord selected = knownMoves.CachedKnown[selectedKnownIndex];
            StampMoveRequest request = reviewedRequest ?? new StampMoveRequest
            {
                knownInstanceId = selected.instanceId,
                requestedModifierCount = StampSlotsPerAction,
                idempotencyKey = Guid.NewGuid().ToString("N"),
            };

            try
            {
                if (reviewedRequest == null)
                {
                    presentationState = CandyMachinePresentationState.Committing;
                    SetBlockedInputMuted(true);
                    SetPending(true, "Reviewing Stamp...");
                    StampTransactionReview review = await stampedMoves.ReviewStampAsync(request);
                    if (review == null || !review.simulationSucceeded)
                    {
                        ShowOperationError(review?.simulationMessage ?? "Simulation failed.");
                        return;
                    }

                    reviewedRequest = request;
                    ShowReview(review);
                    presentationState = CandyMachinePresentationState.Review;
                    SetBlockedInputMuted(false);
                    SetPending(false, "Review the result, then confirm Stamp.");
                    RefreshActions();
                    FocusDetailPrimary();
                    return;
                }

                presentationState = CandyMachinePresentationState.Committing;
                SetBlockedInputMuted(true);
                SetPending(true, "Stamping...");
                StampMoveResult result = await stampedMoves.StampMoveAsync(request);
                reviewedRequest = null;
                if (result != null && result.success && result.pending)
                {
                    BeginSettlementPull();
                    await ReconcileStampUntilSettledAsync(result);
                    return;
                }
                if (result == null || !result.success || !result.confirmed || result.verifiedAsset == null
                    || !ContainsCachedStamp(result.verifiedAsset.assetAddress))
                {
                    ShowOperationError(result?.error ?? "Stamp was not confirmed by verified Core inventory.");
                    return;
                }

                selectedKnownIndex = 0;
                BeginCommittedPull(BuildStampReveal(result.verifiedAsset));
            }
            catch (Exception exception)
            {
                reviewedRequest = null;
                ShowOperationError(exception.Message);
            }
        }

        private void SetMode(CandyMachineMode next)
        {
            if (IsInteractionLocked() || navLayer != CandyMachineNavLayer.Mode)
                return;

            mode = next;
            reviewedRequest = null;
            presentationState = CandyMachinePresentationState.Browse;
            navLayer = next == CandyMachineMode.Stamp
                ? CandyMachineNavLayer.Browse
                : CandyMachineNavLayer.Detail;
            RefreshPresentation();
            PlayUiCue(PlayerMenuSfxCue.PanelSwitch);
            if (mode == CandyMachineMode.Stamp)
                FocusSelectedRow();
            else
                FocusDetailPrimary();
        }

        private void SelectKnown(int index)
        {
            if (IsInteractionLocked() || navLayer != CandyMachineNavLayer.Browse)
                return;
            selectedKnownIndex = index;
            reviewedRequest = null;
            presentationState = CandyMachinePresentationState.Browse;
            navLayer = CandyMachineNavLayer.Detail;
            RefreshPresentation();
            PlayUiCue(PlayerMenuSfxCue.PanelSwitch);
            FocusDetailPrimary();
        }

        private void RefreshRam()
        {
            long ram = DisplayRamBalance;
            if (ramLabel != null)
                ramLabel.text = $"RAM  {ram}";
            if (titleLabel != null)
                titleLabel.text = MachineTitle;
        }

        private void RefreshModeVisuals()
        {
            bool folio = mode == CandyMachineMode.GetKnown;
            bool committed = navLayer != CandyMachineNavLayer.Mode;
            SetTabActive(folioTabButton, folioTabMarker, committed && folio);
            SetTabActive(stampTabButton, stampTabMarker, committed && !folio);
            if (committed)
            {
                ApplyStickyModeChrome(folioTabButton, folio);
                ApplyStickyModeChrome(stampTabButton, !folio);
            }
            else
            {
                SetTabLabelInk(folioTabButton, true);
                SetTabLabelInk(stampTabButton, true);
                ClearStickyModeChrome(folioTabButton);
                ClearStickyModeChrome(stampTabButton);
            }

            if (folioBrowse != null)
                folioBrowse.gameObject.SetActive(folio);
            if (stampBrowse != null)
                stampBrowse.gameObject.SetActive(!folio);
            if (slotRow != null)
                slotRow.gameObject.SetActive(!folio);
            if (folioMetaRow != null)
                folioMetaRow.gameObject.SetActive(true);
            if (resultLabel != null && resultLabel.transform.parent != null)
                resultLabel.transform.parent.gameObject.SetActive(folio);
            if (tokensLabel != null && tokensLabel.transform.parent != null)
                tokensLabel.transform.parent.gameObject.SetActive(folio);
            if (reviewHint != null && reviewHint.transform.parent != null)
                reviewHint.transform.parent.gameObject.SetActive(!folio);

            if (browseHeader != null)
                browseHeader.text = folio ? "FOLIO BROWSE" : "STAMPABLE MOVES";
            if (browseSubheader != null)
            {
                browseSubheader.text = folio
                    ? "ODDS | WHAT YOU GET | RECENT"
                    : "VERTICAL LIST | U/D | LONG LIBRARY";
            }

            if (detailHeader != null)
                detailHeader.text = folio ? "FOLIO / GET KNOWN" : "STAMP A MOVE";
            if (detailSubheader != null)
            {
                detailSubheader.text = folio
                    ? "DRAW ONE MOVE | NO MODIFIERS | NO STAMP"
                    : "ONE STAMP PER ACTION | MAX 2 SLOTS | NO 1/2 MOD PICKER";
            }

            if (previewCaption != null)
                previewCaption.text = folio ? "PREVIEW | KNOWN MOVE" : "SELECTED";
            if (listFooter != null)
                listFooter.text = "SCROLL U/D | LOCKED = 2/2 FULL";

            if (oddsLabel != null && folio)
                oddsLabel.text = devInventory != null ? devInventory.FormatOdds() : "SERVER-VERIFIED POOL";
            if (payoutLabel != null && folio)
                payoutLabel.text = "1 KNOWN MOVE | 0 MODIFIERS";
        }

        private void RefreshList()
        {
            RefreshRecentChips();

            if (listContent == null)
                return;

            knownRowButtons.Clear();
            for (int i = listContent.childCount - 1; i >= 0; i--)
                DestroyView(listContent.GetChild(i).gameObject);

            if (mode != CandyMachineMode.Stamp || knownMoves == null)
                return;

            IReadOnlyList<KnownMoveRecord> records = knownMoves.CachedKnown;
            for (int i = 0; i < records.Count; i++)
            {
                int index = i;
                KnownMoveRecord record = records[i];
                Button row = EnsureRow(
                    $"Known_{record.instanceId}",
                    ResolveMoveName(record.moveId).ToUpperInvariant(),
                    FormatMeter(record.modifierCount));
                SetRowSelected(row, index == selectedKnownIndex);
                row.onClick.AddListener(() => SelectKnown(index));
                knownRowButtons.Add(row);
            }

            IReadOnlyList<StampedMoveRecord> stampedRecords =
                stampedMoves != null ? stampedMoves.CachedStamped : null;
            if (stampedRecords != null)
            {
                for (int i = 0; i < stampedRecords.Count; i++)
                {
                    StampedMoveRecord record = stampedRecords[i];
                    Button row = EnsureRow(
                        $"Stamped_{record.assetAddress}",
                        ResolveMoveName(record.moveId).ToUpperInvariant(),
                        FormatMeter(record.modifierCount));

                    // Already-stamped rows stay readable but never arm the CTA | no double-stamp path.
                    row.interactable = false;
                    SetRowSelected(row, false);
                    knownRowButtons.Add(row);
                }
            }

            if (navLayer != CandyMachineNavLayer.Browse)
            {
                for (int i = 0; i < knownRowButtons.Count; i++)
                {
                    if (knownRowButtons[i] != null)
                        knownRowButtons[i].interactable = false;
                }
            }
        }

        private void RefreshRecentChips()
        {
            if (recentContent == null)
                return;

            recentChips.Clear();
            for (int i = recentContent.childCount - 1; i >= 0; i--)
                DestroyView(recentContent.GetChild(i).gameObject);

            if (knownMoves == null)
                return;

            IReadOnlyList<KnownMoveRecord> records = knownMoves.CachedKnown;
            int shown = 0;
            for (int i = records.Count - 1; i >= 0 && shown < RecentChipCount; i--, shown++)
            {
                TMP_Text chip = EnsureText(
                    $"Chip_{records[i].instanceId}",
                    recentContent,
                    ResolveMoveName(records[i].moveId).ToUpperInvariant(),
                    20f,
                    TextAlignmentOptions.Center,
                    false);
                PlayerMenuUiFactory.LayoutElement(chip.gameObject, 180f, 48f);
                recentChips.Add(chip.gameObject);
            }

            int overflow = records.Count - shown;
            if (overflow <= 0)
                return;

            TMP_Text pill = EnsureText(
                "ChipOverflow",
                recentContent,
                $"+{overflow}",
                20f,
                TextAlignmentOptions.Center,
                false);
            PlayerMenuUiFactory.LayoutElement(pill.gameObject, 72f, 48f);
            recentChips.Add(pill.gameObject);
        }

        private void RefreshDetail()
        {
            if (mode == CandyMachineMode.GetKnown)
            {
                int cost = devInventory != null
                    ? devInventory.RamCostGetKnown
                    : vendingConfig != null ? Mathf.Max(0, vendingConfig.ramCostGetKnown) : 0;
                SetText(detailTitle, "ARC OF THE POOL");
                SetText(detailMeta, "0 MODIFIERS | CLEAN ENTRY");
                SetText(costLabel, cost <= 0 ? "FREE" : $"{cost} RAM");
                SetText(resultLabel, "KNOWN / CLEAN");
                SetText(tokensLabel, ramLabel != null ? ramLabel.text.Replace("RAM", string.Empty).Trim() : "0");
                detailBodyView?.Show(
                    CandyMachineDetailBodyMode.Idle,
                    "GET KNOWN",
                    "A NEW MOVE ENTERS THE FOLIO WITH ZERO STAMPS. USE STAMP MODE LATER TO FILL SLOTS.",
                    cost <= 0 ? "CONFIRM TO PULL" : $"CONFIRM TO SPEND {cost} RAM");
                RefreshSlots(0, null, null);
                return;
            }

            if (knownMoves == null || knownMoves.CachedKnown.Count == 0)
            {
                SetText(detailTitle, "NO STAMPABLE MOVES");
                SetText(detailMeta, $"METER {FormatMeterIcons(0)} | NOTHING SELECTED");
                SetText(costLabel, "COST | 1 STAMP");
                detailBodyView?.Show(
                    CandyMachineDetailBodyMode.Error,
                    "NO STAMPABLE MOVES",
                    "ROLL GET KNOWN OR USE THE DEV GRANT STRIP TO ADD A KNOWN MOVE.",
                    "SELECT FOLIO TO CONTINUE");
                RefreshSlots(0, null, null);
                return;
            }

            selectedKnownIndex = Mathf.Clamp(selectedKnownIndex, 0, knownMoves.CachedKnown.Count - 1);
            KnownMoveRecord selected = knownMoves.CachedKnown[selectedKnownIndex];
            DevCandyMachineInventory.CatalogMove catalog = devInventory != null
                ? devInventory.ResolveMove(selected.moveId)
                : new DevCandyMachineInventory.CatalogMove(selected.moveId, selected.moveId, RarityTier.Common);

            int filled = Mathf.Clamp(selected.modifierCount, 0, MaxStampSlots);
            SetText(detailTitle, catalog.displayName.ToUpperInvariant());
            SetText(
                detailMeta,
                $"METER {FormatMeterIcons(filled)} | NEXT = SLOT {Mathf.Min(filled + 1, MaxStampSlots)}");
            int stampCost = devInventory != null
                ? devInventory.RamCostStamp
                : vendingConfig != null ? Mathf.Max(0, vendingConfig.ramCostStampKnown) : 0;
            SetText(costLabel, stampCost <= 0 ? "1 STAMP" : $"1 STAMP | {stampCost} RAM");
            SetText(reviewHint, "CONFIRM");
            detailBodyView?.Show(
                CandyMachineDetailBodyMode.Idle,
                "STAMP SELECTED MOVE",
                "FILL NEXT EMPTY SLOT ONLY | NO DOUBLE-STAMP, NO MOD PICKER.",
                stampCost <= 0 ? "CONFIRM TO REVIEW" : $"CONFIRM TO REVIEW | {stampCost} RAM");
            RefreshSlots(filled, null, null);
        }

        private void RefreshSlots(int filledSlots, string slotOneModifier, string slotTwoModifier)
        {
            bool stampMode = mode == CandyMachineMode.Stamp;
            ApplySlot(
                slotOneFrame,
                slotOneCaption,
                slotOneValue,
                1,
                filledSlots >= 1,
                stampMode && filledSlots == 0,
                slotOneModifier);
            ApplySlot(
                slotTwoFrame,
                slotTwoCaption,
                slotTwoValue,
                2,
                filledSlots >= 2,
                stampMode && filledSlots == 1,
                slotTwoModifier);
        }

        private void ApplySlot(
            Image frame,
            TMP_Text caption,
            TMP_Text value,
            int slotNumber,
            bool filled,
            bool isNext,
            string modifierId)
        {
            if (caption != null)
            {
                string state = filled ? "FILLED" : isNext ? "NEXT" : "EMPTY";
                caption.text = $"[{slotNumber}] {state}";
            }

            if (value != null)
            {
                if (!filled)
                {
                    value.text = "EMPTY";
                }
                else if (string.IsNullOrWhiteSpace(modifierId))
                {
                    value.text = "STAMPED";
                }
                else
                {
                    ModifierDefinitionSO definition = modifierCatalog != null
                        ? modifierCatalog.GetBySectionId(modifierId)
                        : null;
                    value.text = CandyMachineResultView.FormatModifierLabel(modifierId, definition);
                }
            }

            if (frame != null)
                frame.color = isNext ? SlotNextColor : filled ? SlotFilledColor : SlotEmptyColor;
        }

        private void RefreshActions()
        {
            bool canUse = HasFeatureServices();
            bool stampPending = ownershipService?.PendingStamp != null || stampReconciliationRunning;
            if (primaryButton != null)
            {
                TMP_Text label = primaryButton.GetComponentInChildren<TMP_Text>(true);
                if (mode == CandyMachineMode.GetKnown)
                {
                    if (label != null)
                        label.text = "GET KNOWN";
                    primaryButton.interactable = canUse && !stampPending && navLayer == CandyMachineNavLayer.Detail;
                }
                else
                {
                    if (label != null)
                        label.text = stampPending ? "SETTLING" : reviewedRequest == null ? "STAMP" : "CONFIRM STAMP";
                    primaryButton.interactable = canUse && !stampPending
                        && navLayer == CandyMachineNavLayer.Detail
                        && knownMoves != null
                        && knownMoves.CachedKnown.Count > 0;
                }
            }
        }

        private void ShowReview(StampTransactionReview review)
        {
            if (review == null)
                return;
            detailBodyView?.Show(
                CandyMachineDetailBodyMode.Confirm,
                $"STAMP REVIEW | {review.cluster}",
                $"RAM {review.ramCost} | FEE {review.estimatedNetworkFeeLamports} LAMPORTS\n{review.simulationMessage}",
                "PRESS CONFIRM TO SIGN AND STAMP");
        }

        private async Task ResumePendingStampAsync(PendingStampRecord pending)
        {
            if (pending == null || stampReconciliationRunning)
                return;
            BeginSettlementPull();
            await ReconcileStampUntilSettledAsync(new StampMoveResult
            {
                success = true,
                pending = true,
                operationId = pending.operationId,
                operationStatus = pending.operationStatus,
                knownInstanceId = pending.knownInstanceId,
                idempotencyKey = pending.idempotencyKey,
                assetAddress = pending.assetAddress,
                signature = pending.signature,
                message = pending.message,
            });
        }

        private async Task ReconcileStampUntilSettledAsync(StampMoveResult pending)
        {
            if (ownershipService == null || stampReconciliationRunning)
                return;

            stampReconciliationRunning = true;
            CancelStampReconciliation();
            stampReconciliationCancellation = new CancellationTokenSource();
            CancellationToken token = stampReconciliationCancellation.Token;
            presentationState = CandyMachinePresentationState.Committing;
            SetBlockedInputMuted(true);
            reviewedRequest = null;
            int[] delaysMs = { 0, 750, 1250, 2000, 3000 };
            try
            {
                for (int attempt = 0; attempt < delaysMs.Length; attempt++)
                {
                    string phase = string.Equals(pending?.operationStatus, "chain_confirmed", StringComparison.OrdinalIgnoreCase)
                        ? "Transaction confirmed. Reading Stamp receipt..."
                        : "Stamp submitted. Confirming transaction...";
                    SetPending(true, $"{phase} ({attempt + 1}/{delaysMs.Length})");
                    if (delaysMs[attempt] > 0)
                        await Task.Delay(delaysMs[attempt], token);
                    StampMoveResult result = await ownershipService.ReconcilePendingStampAsync(pending?.idempotencyKey);
                    token.ThrowIfCancellationRequested();
                    if (result == null || !result.success)
                    {
                        ShowOperationError(result?.error ?? "Stamp reconciliation failed.");
                        return;
                    }

                    if (result.confirmed && result.verifiedAsset != null
                        && ContainsCachedStamp(result.verifiedAsset.assetAddress))
                    {
                        selectedKnownIndex = 0;
                        BeginCommittedPull(BuildStampReveal(result.verifiedAsset));
                        return;
                    }

                    pending = result;
                }

                ShowStampPendingState(pending);
            }
            catch (OperationCanceledException)
            {
                // Closing the panel or changing scenes intentionally stops only
                // local polling. The durable server intent remains reconcilable.
            }
            catch (Exception exception)
            {
                ShowOperationError(exception.Message);
            }
            finally
            {
                stampReconciliationRunning = false;
                if (showing)
                    RefreshActions();
            }
        }

        private void ShowStampPendingState(StampMoveResult pending)
        {
            RestoreAfterSettlementPull();
            presentationState = CandyMachinePresentationState.PendingSettlement;
            SetBlockedInputMuted(false);
            detailBodyView?.Show(
                CandyMachineDetailBodyMode.Confirm,
                "STAMP CONFIRMED",
                string.IsNullOrWhiteSpace(pending?.message)
                    ? "TRUSTED ASSET INDEXING IS STILL IN PROGRESS."
                    : pending.message.ToUpperInvariant(),
                "YOU MAY CLOSE; THIS SAME STAMP RESUMES ON RETURN");
            RefreshActions();
        }

        private void CancelStampReconciliation()
        {
            if (stampReconciliationCancellation == null)
                return;
            stampReconciliationCancellation.Cancel();
            stampReconciliationCancellation.Dispose();
            stampReconciliationCancellation = null;
        }

        private static string FormatStampResult(StampedMoveRecord record)
        {
            if (record == null)
                return "Stamped.";
            string second = string.IsNullOrWhiteSpace(record.modifier2Id)
                ? string.Empty
                : $" + {record.modifier2Id}";
            return $"Stamped {record.moveId} with {record.modifier1Id}{second}.";
        }

        private void SetPending(bool pending, string message)
        {
            SetStatus(message);
            if (primaryButton != null)
                primaryButton.interactable = !pending && HasFeatureServices();
        }

        private void SetStatus(string value)
        {
            if (detailBodyView != null && presentationState == CandyMachinePresentationState.Committing)
                detailBodyView.Show(CandyMachineDetailBodyMode.Confirm, "PROCESSING", value, "PLEASE WAIT");
        }

        private void BeginCommittedPull(CandyMachineRevealData reveal)
        {
            pendingReveal = reveal;
            if (settlementPullStarted)
            {
                if (pullAnimationReady)
                    BeginRevealWhenReady();
                return;
            }

            BeginPullPresentation();
        }

        private void BeginSettlementPull()
        {
            if (settlementPullStarted)
                return;
            pendingReveal = null;
            settlementPullStarted = true;
            pullAnimationReady = false;
            BeginPullPresentation();
        }

        private void BeginPullPresentation()
        {
            presentationState = CandyMachinePresentationState.Pulling;
            SetPending(true, "Preparing machine...");
            if (presentationGroup == null)
            {
                StartWorldPull();
                return;
            }

            presentationGroup.interactable = false;
            presentationGroup.blocksRaycasts = false;
            DOTween.Kill(this);
            presentationGroup.DOFade(0f, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(StartWorldPull);
        }

        private void StartWorldPull()
        {
            if (pullDirector == null)
            {
                Debug.LogWarning("[CandyMachineScreen] Verified operation completed without a pull director; revealing immediately.", this);
                OnPullRevealReady();
                return;
            }

            pullDirector.BeginPull(OnPullRevealReady);
        }

        private void OnPullRevealReady()
        {
            pullAnimationReady = true;
            if (!showing || pendingReveal == null)
                return;

            BeginRevealWhenReady();
        }

        private void BeginRevealWhenReady()
        {
            if (!showing || pendingReveal == null)
                return;
            settlementPullStarted = false;
            pullAnimationReady = false;

            SetBrowseRevealVisibility(false);
            resultView?.Prepare(pendingReveal);
            if (presentationGroup != null)
            {
                presentationGroup.alpha = 0f;
                presentationGroup.interactable = true;
                presentationGroup.blocksRaycasts = true;
                presentationGroup.DOFade(1f, 0.2f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetTarget(this);
            }

            StopRevealCoroutine();
            revealCoroutine = StartCoroutine(PlayRevealSequence());
        }

        private void RestoreAfterSettlementPull()
        {
            if (!settlementPullStarted)
                return;
            settlementPullStarted = false;
            pullAnimationReady = false;
            pullDirector?.CancelAndRestore();
            if (presentationGroup != null)
            {
                DOTween.Kill(this);
                presentationGroup.alpha = 1f;
                presentationGroup.interactable = true;
                presentationGroup.blocksRaycasts = true;
            }
        }

        private const float BeatGapSeconds = 1.7f;
        private Coroutine contentStingerRoutine;
        private Coroutine blockedInputMuteRoutine;

        private IEnumerator PlayRevealSequence()
        {
            presentationState = CandyMachinePresentationState.Revealing;
            revealLog.Clear();
            string[] subtitles = pendingReveal.Mode == CandyMachineMode.GetKnown
                ? new[] { "Reading move type", "Locking rarity", "Materializing folio entry" }
                : new[] { "Browsing archives", "Locking mod rarity", "Imprinting the modifier" };

            for (int stage = 1; stage <= 3; stage++)
            {
                detailBodyView?.Show(
                    CandyMachineDetailBodyMode.Reveal,
                    "CANDY MACHINE",
                    RevealLogText(),
                    "REVEAL IN PROGRESS",
                    subtitles[stage - 1]);
                float stageDuration = resultView != null ? resultView.SetStage(stage) : 1.2f;
                float contentDelay = resultView != null ? resultView.ContentDelay : 0f;
                if (stage == 1)
                    PlayLayeredStinger(revealSlideStinger);
                bool stampSlideOnly = stage == 1
                    && pendingReveal != null
                    && pendingReveal.Mode == CandyMachineMode.Stamp;
                if (!stampSlideOnly)
                    ScheduleContentStinger(contentDelay, stage);
                yield return new WaitForSecondsRealtime(stageDuration);
                if (stage < 3)
                    yield return new WaitForSecondsRealtime(BeatGapSeconds);
            }

            revealCoroutine = null;
            SetBlockedInputMuted(false);
            presentationState = CandyMachinePresentationState.AwaitingConfirm;
            detailBodyView?.Show(
                CandyMachineDetailBodyMode.Reveal,
                "RESULT READY",
                RevealLogText(),
                "CONFIRM TO CONTINUE",
                "Entry verified");
            yield return StartCoroutine(FocusRevealContinue());
        }

        public bool TryHandleSubmit()
        {
            if (presentationState == CandyMachinePresentationState.AwaitingConfirm)
            {
                ConfirmReveal();
                return true;
            }

            return IsInteractionLocked();
        }

        public bool TryHandleCancel()
        {
            if (!showing)
                return false;
            if (IsInteractionLocked())
                return true;
            if (navLayer == CandyMachineNavLayer.Mode)
                return false;

            StepBack();
            return true;
        }

        public bool TryHandleMove(Vector2 _) =>
            presentationState == CandyMachinePresentationState.AwaitingConfirm;

        private IEnumerator FocusRevealContinue()
        {
            resultView?.EnableContinue();
            yield return null;
            Button continueButton = resultView != null ? resultView.ContinueButton : null;
            if (continueButton == null || inputContext == null)
                yield break;

            inputContext.Select(continueButton);
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != continueButton.gameObject)
            {
                yield return null;
                inputContext.Select(continueButton);
            }
        }

        private void ConfirmReveal()
        {
            if (presentationState != CandyMachinePresentationState.AwaitingConfirm)
                return;

            StopRevealCoroutine();
            pullDirector?.RestoreAfterReveal();
            pendingReveal = null;
            presentationState = CandyMachinePresentationState.Browse;
            navLayer = mode == CandyMachineMode.Stamp
                ? CandyMachineNavLayer.Browse
                : CandyMachineNavLayer.Detail;
            resultView?.Hide();
            SetBrowseRevealVisibility(true);
            RefreshPresentation();
            if (bodyRow != null)
            {
                bodyRow.alpha = 0f;
                bodyRow.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this);
            }
            if (mode == CandyMachineMode.Stamp)
                FocusSelectedRow();
            else
                FocusDetailPrimary();
        }

        private void SetBrowseRevealVisibility(bool browse)
        {
            if (bodyRow != null)
            {
                bodyRow.gameObject.SetActive(browse);
                bodyRow.alpha = browse ? 1f : 0f;
                bodyRow.interactable = browse;
                bodyRow.blocksRaycasts = browse;
            }
            if (browse)
                resultView?.Hide();
            else if (resultView != null)
                resultView.gameObject.SetActive(true);
        }

        private void ShowOperationError(string message)
        {
            RestoreAfterSettlementPull();
            presentationState = reviewedRequest != null
                ? CandyMachinePresentationState.Review
                : CandyMachinePresentationState.Browse;
            SetBlockedInputMuted(false);
            SetPending(false, message);
            detailBodyView?.Show(
                CandyMachineDetailBodyMode.Error,
                "CANDY MACHINE ERROR",
                string.IsNullOrWhiteSpace(message) ? "THE OPERATION COULD NOT BE COMPLETED." : message,
                reviewedRequest != null ? "CONFIRM TO RETRY" : "CHECK REQUIREMENTS AND TRY AGAIN");
            PlayUiCue(PlayerMenuSfxCue.Invalid);
            RefreshActions();
        }

        private Sprite ResolveTypeIcon(MoveDefinitionSO move)
        {
            if (typeIconPalette == null)
                return null;
            ChainlingType type = move != null ? move.baseType : ChainlingType.None;
            return typeIconPalette.GetIcon(type);
        }

        private void ApplyStickyModeChrome(Button tab, bool active)
        {
            if (tab == null)
                return;
            PlayerMenuChromeButtonVisual visual = tab.GetComponent<PlayerMenuChromeButtonVisual>();
            visual?.SetStickySelected(active);
        }

        private static void ClearStickyModeChrome(Button tab)
        {
            if (tab == null)
                return;
            PlayerMenuChromeButtonVisual visual = tab.GetComponent<PlayerMenuChromeButtonVisual>();
            visual?.ClearStickySelection();
        }

        private void StepBack()
        {
            if (navLayer == CandyMachineNavLayer.Detail && mode == CandyMachineMode.Stamp)
            {
                reviewedRequest = null;
                presentationState = CandyMachinePresentationState.Browse;
                navLayer = CandyMachineNavLayer.Browse;
                RefreshPresentation();
                PlayUiCue(PlayerMenuSfxCue.PanelSwitch);
                FocusSelectedRow();
                return;
            }

            ReturnToModeHub();
        }

        private void ReturnToModeHub()
        {
            navLayer = CandyMachineNavLayer.Mode;
            reviewedRequest = null;
            presentationState = CandyMachinePresentationState.Browse;
            RefreshPresentation();
            PlayUiCue(PlayerMenuSfxCue.PanelSwitch);
            FocusModeTab();
        }

        private void FocusDetailPrimary()
        {
            if (inputContext != null && primaryButton != null)
                inputContext.Select(primaryButton);
        }

        private void FocusModeTab()
        {
            Button tab = mode == CandyMachineMode.Stamp ? stampTabButton : folioTabButton;
            if (tab == null)
                tab = defaultSelection as Button;
            if (inputContext != null && tab != null)
                inputContext.Select(tab);
        }

        private void FocusSelectedRow()
        {
            Button row = null;
            if (selectedKnownIndex >= 0 && selectedKnownIndex < knownRowButtons.Count)
                row = knownRowButtons[selectedKnownIndex];
            if (row == null || !row.interactable)
            {
                for (int i = 0; i < knownRowButtons.Count; i++)
                {
                    if (knownRowButtons[i] != null && knownRowButtons[i].interactable)
                    {
                        row = knownRowButtons[i];
                        break;
                    }
                }
            }

            if (inputContext == null)
                return;
            if (row != null)
                inputContext.Select(row);
        }

        private void PlayUiCue(PlayerMenuSfxCue cue)
        {
            if (PlayerMenuSfxResolver.IsCueSuppressed(cue))
                return;
            IPlayerMenuSfxPlayer player = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            player?.PlayCue(cue);
        }

        private CandyMachineRevealData BuildKnownReveal(KnownMoveRecord record)
        {
            MoveDefinitionSO move = moveDatabase != null ? moveDatabase.GetById(record.moveId) : null;
            string displayName = move != null && !string.IsNullOrWhiteSpace(move.displayName)
                ? move.displayName
                : ResolveMoveName(record.moveId);
            string type = move != null ? move.baseType.ToString() : "UNKNOWN";
            string rarity = move != null
                ? move.rarity.ToString()
                : devInventory != null ? devInventory.ResolveMove(record.moveId).rarity.ToString() : "UNKNOWN";
            string description = move != null ? move.description : "Known move added to the folio.";
            RarityTier rarityTier = move != null ? move.rarity : RarityTier.Common;
            return new CandyMachineRevealData(
                CandyMachineMode.GetKnown,
                "TYPE",
                type,
                "RARITY",
                rarity,
                "KNOWN MOVE",
                displayName,
                description,
                ResolveTypeIcon(move),
                MoveSelectDisplayUtility.FormatPowerOrEffectLabel(move),
                MoveSelectDisplayUtility.FormatAccuracyLine(move),
                move != null ? MoveSelectDisplayUtility.FormatApCostLine(move.apCost) : "AP Cost --",
                displayName,
                LibraryCardLayout.FormatStatsInk(move, description),
                moveRarity: rarityTier);
        }

        private CandyMachineRevealData BuildStampReveal(StampedMoveRecord record)
        {
            MoveDefinitionSO move = moveDatabase != null ? moveDatabase.GetById(record.moveId) : null;
            string firstEffect = ResolveModifierDescription(
                record.modifier1Id,
                record.modifier1EffectValue,
                record.modifier1EffectValueLocked);
            string secondEffect = ResolveModifierDescription(
                record.modifier2Id,
                record.modifier2EffectValue,
                record.modifier2EffectValueLocked);
            string firstLabel = FormatModifierLabel(record.modifier1Id);
            string secondLabel = FormatModifierLabel(record.modifier2Id);
            string modifierIds = string.IsNullOrWhiteSpace(record.modifier2Id)
                ? firstLabel
                : $"{firstLabel} + {secondLabel}";
            string effects = string.IsNullOrWhiteSpace(secondEffect)
                ? firstEffect
                : $"{firstEffect}\n{secondEffect}";
            RarityTier moveRarity = move != null ? move.rarity : RarityTier.Common;
            RarityTier stampRarity = ParseRarity(record.rarity, moveRarity);
            bool hasSecond = !string.IsNullOrWhiteSpace(record.modifier2Id);
            return new CandyMachineRevealData(
                CandyMachineMode.Stamp,
                "HOST MOVE",
                ResolveMoveName(record.moveId),
                "STAMP RARITY",
                string.IsNullOrWhiteSpace(record.rarity) ? moveRarity.ToString() : record.rarity,
                "MODIFIERS",
                modifierIds,
                effects,
                ResolveTypeIcon(move),
                MoveSelectDisplayUtility.FormatPowerOrEffectLabel(move),
                MoveSelectDisplayUtility.FormatAccuracyLine(move),
                move != null ? MoveSelectDisplayUtility.FormatApCostLine(move.apCost) : "AP Cost --",
                ResolveMoveName(record.moveId),
                LibraryCardLayout.FormatStatsInk(move, move != null ? move.description : string.Empty),
                firstLabel,
                hasSecond ? secondLabel : string.Empty,
                firstEffect,
                hasSecond ? secondEffect : string.Empty,
                moveRarity,
                !string.IsNullOrWhiteSpace(firstLabel),
                stampRarity,
                hasSecond,
                stampRarity);
        }

        private string ResolveModifierDescription(string modifierId, float lockedValue, bool hasLockedValue)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
                return string.Empty;
            ModifierDefinitionSO definition = modifierCatalog != null
                ? modifierCatalog.GetBySectionId(modifierId)
                : null;
            return CandyMachineResultView.FormatModifierDescription(
                modifierId, definition, lockedValue, hasLockedValue);
        }

        private static RarityTier ParseRarity(string value, RarityTier fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                System.Enum.TryParse(value.Trim(), true, out RarityTier tier))
                return tier;
            return fallback;
        }

        private string ResolveModifierEffect(string modifierId, float lockedValue, bool hasLockedValue)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
                return string.Empty;
            ModifierDefinitionSO definition = modifierCatalog != null
                ? modifierCatalog.GetBySectionId(modifierId)
                : null;
            return CandyMachineResultView.FormatModifierEffect(
                modifierId, definition, lockedValue, hasLockedValue);
        }

        private string FormatModifierLabel(string modifierId)
        {
            if (string.IsNullOrWhiteSpace(modifierId))
                return string.Empty;
            ModifierDefinitionSO definition = modifierCatalog != null
                ? modifierCatalog.GetBySectionId(modifierId)
                : null;
            return CandyMachineResultView.FormatModifierLabel(modifierId, definition);
        }

        private void ScheduleContentStinger(float delay, int stage)
        {
            if (contentStingerRoutine != null)
                StopCoroutine(contentStingerRoutine);
            contentStingerRoutine = StartCoroutine(PlayContentStingerAfter(delay, stage));
        }

        private IEnumerator PlayContentStingerAfter(float delay, int stage)
        {
            if (delay > 0.01f)
                yield return new WaitForSecondsRealtime(delay);
            contentStingerRoutine = null;
            AppendRevealBeat(stage);
            detailBodyView?.SetBody(RevealLogText());
            bool knownTypeBeat = stage == 1
                && pendingReveal != null
                && pendingReveal.Mode == CandyMachineMode.GetKnown;
            if (knownTypeBeat)
                PlayLayeredStinger(revealTypeSticker);
            else
                PlayStampStinger();
        }

        private void SetBlockedInputMuted(bool muted)
        {
            if (blockedInputMuteRoutine != null)
            {
                StopCoroutine(blockedInputMuteRoutine);
                blockedInputMuteRoutine = null;
            }

            if (!muted)
            {
                ApplyBlockedInputMuted(false);
                return;
            }

            blockedInputMuteRoutine = StartCoroutine(MuteBlockedInputNextFrame());
        }

        private IEnumerator MuteBlockedInputNextFrame()
        {
            yield return null;
            blockedInputMuteRoutine = null;
            if (presentationState == CandyMachinePresentationState.Committing
                || presentationState == CandyMachinePresentationState.Pulling
                || presentationState == CandyMachinePresentationState.Revealing)
                ApplyBlockedInputMuted(true);
        }

        private void ApplyBlockedInputMuted(bool muted)
        {
            blockedInputMuted = muted;
            if (inputContext == null)
                inputContext = GetComponent<MenuInputContextController>();
            inputContext?.SetAnimationHold(muted);
        }

        private void AppendRevealBeat(int stage)
        {
            if (pendingReveal == null)
                return;

            if (pendingReveal.Mode == CandyMachineMode.GetKnown)
            {
                if (stage == 1)
                    revealLog.Add($"TYPE  {pendingReveal.StepOneValue}");
                else if (stage == 2)
                    revealLog.Add($"RARITY  {pendingReveal.StepTwoValue}");
                else if (stage == 3)
                    revealLog.Add($"MOVE  {pendingReveal.MoveName}");
                return;
            }

            if (stage == 2)
            {
                bool both = pendingReveal.HasMod1Rarity && pendingReveal.HasMod2Rarity;
                if (pendingReveal.HasMod1Rarity)
                    revealLog.Add(ModLine("RARITY", 1, both, pendingReveal.Mod1Rarity.ToString()));
                if (pendingReveal.HasMod2Rarity)
                    revealLog.Add(ModLine("RARITY", 2, both, pendingReveal.Mod2Rarity.ToString()));
                return;
            }

            if (stage != 3)
                return;

            bool twoMods = pendingReveal.HasMod1Rarity && pendingReveal.HasMod2Rarity;
            if (pendingReveal.HasMod1Rarity)
            {
                revealLog.Add(ModLine("MOD", 1, twoMods, pendingReveal.Mod1Name));
                if (!string.IsNullOrWhiteSpace(pendingReveal.Mod1Effect))
                    revealLog.Add(ModLine("EFFECT", 1, twoMods, pendingReveal.Mod1Effect));
            }

            if (pendingReveal.HasMod2Rarity)
            {
                revealLog.Add(ModLine("MOD", 2, twoMods, pendingReveal.Mod2Name));
                if (!string.IsNullOrWhiteSpace(pendingReveal.Mod2Effect))
                    revealLog.Add(ModLine("EFFECT", 2, twoMods, pendingReveal.Mod2Effect));
            }
        }

        private static string ModLine(string label, int slot, bool numbered, string value)
        {
            string prefix = numbered ? $"{label} {slot}" : label;
            return $"{prefix}  {value}";
        }

        private string RevealLogText()
        {
            return revealLog.Count == 0 ? string.Empty : string.Join("\n", revealLog);
        }

        private void PlayStampStinger()
        {
            IPlayerMenuSfxPlayer player = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            player?.PlayCue(PlayerMenuSfxCue.Confirm, revealStinger);
        }

        private void PlayLayeredStinger(UnityEngine.Object sound)
        {
            if (sound == null)
                return;
            IPlayerMenuSfxPlayer player = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            player?.PlayLayered(sound, 1f);
        }

        private void StopRevealCoroutine()
        {
            if (contentStingerRoutine != null)
            {
                StopCoroutine(contentStingerRoutine);
                contentStingerRoutine = null;
            }

            if (revealCoroutine == null)
                return;
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        private bool IsInteractionLocked() =>
            presentationState == CandyMachinePresentationState.Committing
            || presentationState == CandyMachinePresentationState.Pulling
            || presentationState == CandyMachinePresentationState.Revealing
            || presentationState == CandyMachinePresentationState.AwaitingConfirm;

        private static bool IsVerifiedKnown(KnownMoveRecord value) =>
            value != null
            && !string.IsNullOrWhiteSpace(value.instanceId)
            && !string.IsNullOrWhiteSpace(value.moveId)
            && value.modifierCount == 0;

        private bool ContainsCachedKnown(string instanceId)
        {
            IReadOnlyList<KnownMoveRecord> cached = knownMoves?.CachedKnown;
            if (cached == null || string.IsNullOrWhiteSpace(instanceId))
                return false;
            for (int i = 0; i < cached.Count; i++)
                if (string.Equals(cached[i]?.instanceId, instanceId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private bool ContainsCachedStamp(string assetAddress)
        {
            IReadOnlyList<StampedMoveRecord> cached = stampedMoves?.CachedStamped;
            if (cached == null || string.IsNullOrWhiteSpace(assetAddress))
                return false;
            for (int i = 0; i < cached.Count; i++)
                if (string.Equals(cached[i]?.assetAddress, assetAddress, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private string ResolveMoveName(string moveId)
        {
            if (devInventory != null)
                return devInventory.ResolveMove(moveId).displayName;
            if (moveDatabase != null)
            {
                MoveDefinitionSO def = moveDatabase.GetById(moveId);
                if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                    return def.displayName;
            }

            return moveId;
        }

        private static void SetTabActive(Button tab, Image marker, bool active)
        {
            if (marker != null)
                marker.color = active ? TabActiveColor : TabIdleColor;
            SetTabLabelInk(tab, active);
        }

        private static void SetTabLabelInk(Button tab, bool active)
        {
            if (tab == null)
                return;

            TMP_Text label = tab.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.color = active ? ButtonInk : ButtonInkMuted;
        }

        private static void SetRowSelected(Button row, bool selected)
        {
            if (row == null)
                return;
            Transform marker = row.transform.Find("SelectMarker");
            if (marker != null)
                marker.gameObject.SetActive(selected);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }

        private static string FormatMeter(int filled)
        {
            int clamped = Mathf.Clamp(filled, 0, MaxStampSlots);
            return $"{FormatMeterIcons(clamped)}  {clamped}/{MaxStampSlots}";
        }

        private static string FormatMeterIcons(int filled)
        {
            int clamped = Mathf.Clamp(filled, 0, MaxStampSlots);
            string icons = string.Empty;
            for (int i = 0; i < MaxStampSlots; i++)
                icons += i < clamped ? "[#]" : "[-]";
            return icons;
        }

        private static void DestroyView(GameObject target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void WireInput()
        {
            if (folioTabButton != null)
            {
                folioTabButton.onClick.RemoveListener(OnGetKnownMode);
                folioTabButton.onClick.AddListener(OnGetKnownMode);
            }

            if (stampTabButton != null)
            {
                stampTabButton.onClick.RemoveListener(OnStampMode);
                stampTabButton.onClick.AddListener(OnStampMode);
            }

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(OnPrimary);
                primaryButton.onClick.AddListener(OnPrimary);
            }

            if (resultView?.ContinueButton != null)
            {
                resultView.ContinueButton.onClick.RemoveListener(ConfirmReveal);
                resultView.ContinueButton.onClick.AddListener(ConfirmReveal);
            }

            if (inputContext != null)
            {
                inputContext.OnCancel.RemoveListener(HandleCancel);
                inputContext.OnCancel.AddListener(HandleCancel);
                inputContext.SetContentInputHandler(this);
            }
        }

        private void UnwireInput()
        {
            if (folioTabButton != null)
                folioTabButton.onClick.RemoveListener(OnGetKnownMode);
            if (stampTabButton != null)
                stampTabButton.onClick.RemoveListener(OnStampMode);
            if (primaryButton != null)
                primaryButton.onClick.RemoveListener(OnPrimary);
            if (resultView?.ContinueButton != null)
                resultView.ContinueButton.onClick.RemoveListener(ConfirmReveal);
            if (inputContext != null)
            {
                inputContext.OnCancel.RemoveListener(HandleCancel);
                inputContext.SetContentInputHandler(null);
            }
        }

        private void OnGetKnownMode() => SetMode(CandyMachineMode.GetKnown);

        private void OnStampMode() => SetMode(CandyMachineMode.Stamp);

        private void OnPrimary()
        {
            if (IsInteractionLocked() || navLayer != CandyMachineNavLayer.Detail)
                return;
            if (mode == CandyMachineMode.GetKnown)
                RollKnown();
            else
                StampSelected();
        }

        private void HandleCancel()
        {
            if (!IsInteractionLocked())
                CloseImmediate();
        }

        private void AcquireInputLock()
        {
            InputLockService lockService = InputLockService.Instance
                ?? FindFirstObjectByType<InputLockService>(FindObjectsInactive.Include);
            if (lockService == null || inputLockHandle.IsValid)
                return;
            inputLockHandle = lockService.AcquireLock(InputLockReason, InputLockChannel.All, this);
        }

        private void ReleaseInputLock()
        {
            if (!inputLockHandle.IsValid)
                return;
            inputLockHandle.Release();
            inputLockHandle = default;
        }

        private void ConfigureOverlayCanvas()
        {
            Canvas canvas = PlayerMenuUiFactory.GetOrAdd<Canvas>(gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
 | AdditionalCanvasShaderChannels.Normal
 | AdditionalCanvasShaderChannels.Tangent;

            if (transform is RectTransform rect)
            {
                PlayerMenuUiFactory.Stretch(rect);
                rect.localScale = Vector3.one;
            }

            CanvasScaler scaler = PlayerMenuUiFactory.GetOrAdd<CanvasScaler>(gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2670f, 1200f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            PlayerMenuUiFactory.GetOrAdd<GraphicRaycaster>(gameObject);
            inputContext = PlayerMenuUiFactory.GetOrAdd<MenuInputContextController>(gameObject);
        }

        private void EnsureHierarchy()
        {
            Transform background = transform.Find("Background");
            if (background == null)
            {
                GameObject backgroundObject = PlayerMenuUiFactory.CreateRect("Background", transform).gameObject;
                PlayerMenuUiFactory.Stretch(backgroundObject.GetComponent<RectTransform>());
                PlayerMenuBackgroundController backgroundController =
                    backgroundObject.AddComponent<PlayerMenuBackgroundController>();
                backgroundController.RespectAuthoredPresentation = true;
                // Scaffold children only | do not push hardcoded tint/scanline values.
                backgroundController.BuildDefaultLayoutForEditor();
            }
            else
            {
                ProtectBackgroundPresentation();
            }

            RectTransform safe = EnsureRect("SafeAreaPanel", transform);
            PlayerMenuUiFactory.Stretch(safe);
            PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(safe.gameObject);
            if (safe.GetComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>() == null)
                safe.gameObject.AddComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();
            root = safe.GetComponent<CanvasGroup>();

            StripLegacyLayout(safe);

            RectTransform content = EnsureRect("SafeContent", safe);
            bool hasAuthoredBody = content.Find("BodyRow") != null || content.Find("HeaderBar") != null;
            if (!hasAuthoredBody)
            {
                // ~160px corners clear the gaming-pad D-pad / YXAB thumbs (landscape spec).
                PlayerMenuUiFactory.Stretch(content, 48f, 168f, 48f, 36f);
                VerticalLayoutGroup contentLayout = PlayerMenuUiFactory.VerticalLayout(content.gameObject, 16f);
                contentLayout.childAlignment = TextAnchor.UpperCenter;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;
                contentLayout.padding = new RectOffset(0, 0, 0, 0);
                BuildHeader(content);
                BuildBody(content);
            }
            else
            {
                // Never leave a leftover SafeContent VLG on an authored shell.
                StripContentLayoutDrivers(content);
            }

            BuildDevStrip(safe);
            ApplyTitleStyles();
            SpawnEditorSamples();
            RefreshModeVisuals();
            ProtectBackgroundPresentation();
            ProtectAuthoredShellLayout();

            if (folioTabButton != null)
                defaultSelection = folioTabButton;
        }

        private void BuildHeader(RectTransform content)
        {
            RectTransform header = EnsureRect("HeaderBar", content);
            header.SetSiblingIndex(0);
            Size(header.gameObject, -1f, 88f, 1f, 0f);

            HorizontalLayoutGroup headerLayout = PlayerMenuUiFactory.HorizontalLayout(header.gameObject, 16f);
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            RectTransform titlePlate = EnsurePanel("TitlePlate", header, PanelColor);
            Size(titlePlate.gameObject, -1f, 88f, 1f, 0f);
            HorizontalLayoutGroup titleLayout = PlayerMenuUiFactory.HorizontalLayout(titlePlate.gameObject, 16f);
            titleLayout.padding = new RectOffset(28, 24, 8, 8);
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = true;

            titleLabel = EnsureText("Title", titlePlate, MachineTitle, 32f, TextAlignmentOptions.MidlineLeft, false);
            Size(titleLabel.gameObject, -1f, -1f, 1f, 0f);
            ramLabel = EnsureText("RamLabel", titlePlate, "RAM  0", 26f, TextAlignmentOptions.MidlineRight, false);
            Size(ramLabel.gameObject, 220f, -1f, 0f, 0f);

            RectTransform tabs = EnsureRect("ModeTabs", header);
            Size(tabs.gameObject, 320f, 88f, 0.25f, 0f);
            HorizontalLayoutGroup tabLayout = PlayerMenuUiFactory.HorizontalLayout(tabs.gameObject, 8f);
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            folioTabButton = EnsureButton("FolioTab", tabs, "FOLIO");
            Size(folioTabButton.gameObject, -1f, -1f, 1f, 0f);
            folioTabMarker = EnsureMarker(folioTabButton, "ActiveBar");
            CandyMachineTextStyle.ApplyTitle(folioTabButton.transform.Find("Label")?.GetComponent<TMP_Text>());

            stampTabButton = EnsureButton("StampTab", tabs, "STAMP");
            Size(stampTabButton.gameObject, -1f, -1f, 1f, 0f);
            stampTabMarker = EnsureMarker(stampTabButton, "ActiveBar");
            CandyMachineTextStyle.ApplyTitle(stampTabButton.transform.Find("Label")?.GetComponent<TMP_Text>());
            OrderChildren(header, "TitlePlate", "ModeTabs");
            OrderChildren(titlePlate, "Title", "RamLabel");
            OrderChildren(tabs, "FolioTab", "StampTab");
        }

        private void BuildBody(RectTransform content)
        {
            RectTransform body = EnsureRect("BodyRow", content);
            body.SetSiblingIndex(1);
            Size(body.gameObject, -1f, -1f, 1f, 1f);
            if (body.GetComponent<CanvasGroup>() == null)
                body.gameObject.AddComponent<CanvasGroup>();
            HorizontalLayoutGroup bodyLayout = PlayerMenuUiFactory.HorizontalLayout(body.gameObject, 20f);
            bodyLayout.childAlignment = TextAnchor.UpperLeft;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;

            // Slots own HLG flex; nested columns keep authored stretch rects.
            RectTransform slotBrowse = EnsureSlot(body, "Slot_Browse", 0.3f);
            RectTransform slotMode = EnsureSlot(body, "Slot_Mode", 0.2f);
            RectTransform slotDetail = EnsureSlot(body, "Slot_Detail", 0.3f);

            ReparentNamed(content, "BrowseColumn", slotBrowse);
            ReparentNamed(content, "ModeColumn", slotMode);
            ReparentNamed(content, "DetailColumn", slotDetail);
            ReparentNamed(body, "BrowseColumn", slotBrowse);
            ReparentNamed(body, "ModeColumn", slotMode);
            ReparentNamed(body, "DetailColumn", slotDetail);

            BuildBrowseColumn(slotBrowse);
            BuildModeColumn(slotMode);
            BuildDetailColumn(slotDetail);
            OrderChildren(body, "Slot_Browse", "Slot_Mode", "Slot_Detail");
            OrderChildren(content, "HeaderBar", "BodyRow");
        }

        private static RectTransform EnsureSlot(RectTransform body, string slotName, float flexibleWidth)
        {
            Transform existing = body.Find(slotName);
            RectTransform slot = existing as RectTransform;
            if (slot == null)
            {
                GameObject go = new GameObject(slotName, typeof(RectTransform));
                slot = go.GetComponent<RectTransform>();
                slot.SetParent(body, false);
                slot.anchorMin = Vector2.zero;
                slot.anchorMax = Vector2.zero;
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = Vector2.zero;
            }

            LayoutElement element = PlayerMenuUiFactory.LayoutElement(slot.gameObject, 0f, -1f, flexibleWidth, 1f);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 1f;
            return slot;
        }

        private void BuildModeColumn(RectTransform slot)
        {
            RectTransform column = EnsurePanel("ModeColumn", slot, PanelColor);
            StretchFill(column);
            Size(column.gameObject, 0f, -1f, 0.25f, 1f);
            ConfigureStack(column.gameObject, 16, 20);

            RectTransform titlePlate = EnsurePanel("TitlePlate", column, PanelColor);
            Size(titlePlate.gameObject, -1f, 72f, 1f, 0f);
            titleLabel = EnsureText("Title", titlePlate, MachineTitle, 28f, TextAlignmentOptions.MidlineLeft, false);
            Size(titleLabel.gameObject, -1f, -1f, 1f, 0f);

            RectTransform ramPlate = EnsurePanel("RamPlate", column, PanelColor);
            Size(ramPlate.gameObject, -1f, 56f, 1f, 0f);
            ramLabel = EnsureText("RamLabel", ramPlate, "RAM  0", 24f, TextAlignmentOptions.MidlineLeft, false);
            Size(ramLabel.gameObject, -1f, -1f, 1f, 0f);

            RectTransform tabs = EnsureRect("ModeTabs", column);
            Size(tabs.gameObject, -1f, 72f, 1f, 0f);
            HorizontalLayoutGroup tabLayout = PlayerMenuUiFactory.HorizontalLayout(tabs.gameObject, 8f);
            tabLayout.childAlignment = TextAnchor.MiddleCenter;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            folioTabButton = EnsureButton("FolioTab", tabs, "FOLIO");
            Size(folioTabButton.gameObject, -1f, -1f, 1f, 0f);
            folioTabMarker = EnsureMarker(folioTabButton, "ActiveBar");
            stampTabButton = EnsureButton("StampTab", tabs, "STAMP");
            Size(stampTabButton.gameObject, -1f, -1f, 1f, 0f);
            stampTabMarker = EnsureMarker(stampTabButton, "ActiveBar");
            OrderChildren(column, "TitlePlate", "RamPlate", "ModeTabs");
            defaultSelection = folioTabButton;
        }

        private static void StretchFill(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildBrowseColumn(RectTransform body)
        {
            RectTransform column = EnsurePanel("BrowseColumn", body, PanelColor);
            StretchFill(column);
            Size(column.gameObject, 0f, -1f, 0.4f, 1f);
            ConfigureStack(column.gameObject, 12, 20);

            browseHeader = EnsureText("BrowseHeader", column, "FOLIO BROWSE", 26f, TextAlignmentOptions.MidlineLeft, false);
            Size(browseHeader.gameObject, -1f, 32f, 1f, 0f);
            browseSubheader = EnsureText(
                "BrowseSubheader",
                column,
                "ODDS | WHAT YOU GET | RECENT",
                18f,
                TextAlignmentOptions.MidlineLeft,
                false);
            Size(browseSubheader.gameObject, -1f, 24f, 1f, 0f);
            browseSubheader.color = PlayerMenuUiFactory.Muted;

            folioBrowse = EnsureRect("FolioBrowse", column);
            Size(folioBrowse.gameObject, -1f, 280f, 1f, 1f);
            ConfigureStack(folioBrowse.gameObject, 12, 0);

            RectTransform oddsCard = EnsureLabeledCard(
                folioBrowse,
                "OddsCard",
                "OddsCaption",
                "POOL ODDS",
                "OddsLabel",
                out oddsLabel,
                1f);
            Size(oddsCard.gameObject, -1f, 140f, 1f, 1f);

            RectTransform payoutCard = EnsureLabeledCard(
                folioBrowse,
                "PayoutCard",
                "PayoutCaption",
                "WHAT YOU GET",
                "PayoutLabel",
                out payoutLabel,
                0f);
            Size(payoutCard.gameObject, -1f, 110f, 1f, 0f);
            payoutLabel.text = "1 KNOWN MOVE | 0 MODIFIERS";

            TMP_Text recentCaption = EnsureText(
                "RecentCaption",
                folioBrowse,
                "RECENT PULLS",
                18f,
                TextAlignmentOptions.MidlineLeft,
                false);
            Size(recentCaption.gameObject, -1f, 24f, 1f, 0f);
            recentCaption.color = PlayerMenuUiFactory.Muted;

            recentContent = EnsureRect("RecentStrip", folioBrowse);
            Size(recentContent.gameObject, -1f, 56f, 1f, 0f);
            HorizontalLayoutGroup recentLayout = PlayerMenuUiFactory.HorizontalLayout(recentContent.gameObject, 12f);
            recentLayout.childAlignment = TextAnchor.MiddleLeft;
            recentLayout.childControlWidth = true;
            recentLayout.childControlHeight = true;
            recentLayout.childForceExpandWidth = false;
            recentLayout.childForceExpandHeight = true;

            stampBrowse = EnsureRect("StampBrowse", column);
            Size(stampBrowse.gameObject, -1f, 280f, 1f, 1f);
            ConfigureStack(stampBrowse.gameObject, 8, 0);

            RectTransform viewport = EnsureRect("ListViewport", stampBrowse);
            Size(viewport.gameObject, -1f, -1f, 1f, 1f);
            Image viewportImage = PlayerMenuUiFactory.GetOrAdd<Image>(viewport.gameObject);
            viewportImage.color = new Color(0.04f, 0.06f, 0.09f, 0.4f);
            viewportImage.raycastTarget = true;
            Mask mask = PlayerMenuUiFactory.GetOrAdd<Mask>(viewport.gameObject);
            mask.showMaskGraphic = false;
            ScrollRect scroll = PlayerMenuUiFactory.GetOrAdd<ScrollRect>(viewport.gameObject);

            listContent = EnsureRect("ListContent", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = PlayerMenuUiFactory.VerticalLayout(listContent.gameObject, 10f);
            listLayout.padding = new RectOffset(10, 10, 10, 10);
            listLayout.childControlHeight = true;
            ContentSizeFitter fitter = PlayerMenuUiFactory.GetOrAdd<ContentSizeFitter>(listContent.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = listContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            listFooter = EnsureText(
                "ListFooter",
                stampBrowse,
                "SCROLL U/D | LOCKED = 2/2 FULL",
                18f,
                TextAlignmentOptions.MidlineLeft,
                false);
            Size(listFooter.gameObject, -1f, 28f, 1f, 0f);
            listFooter.color = PlayerMenuUiFactory.Muted;
            OrderChildren(column, "BrowseHeader", "BrowseSubheader", "FolioBrowse", "StampBrowse");
            OrderChildren(folioBrowse, "OddsCard", "PayoutCard", "RecentCaption", "RecentStrip");
            OrderChildren(stampBrowse, "ListViewport", "ListFooter");
        }

        private void BuildDetailColumn(RectTransform body)
        {
            RectTransform column = EnsurePanel("DetailColumn", body, PanelColor);
            StretchFill(column);
            Size(column.gameObject, 0f, -1f, 0.55f, 1f);
            ConfigureStack(column.gameObject, 10, 20);

            Transform staleCost = column.Find("CostLabel");
            if (staleCost != null)
                DestroyView(staleCost.gameObject);

            detailHeader = EnsureText("DetailHeader", column, "FOLIO / GET KNOWN", 26f, TextAlignmentOptions.MidlineLeft, false);
            Size(detailHeader.gameObject, -1f, 32f, 1f, 0f);
            detailSubheader = EnsureText(
                "DetailSubheader",
                column,
                "DRAW ONE MOVE | NO MODIFIERS | NO STAMP",
                18f,
                TextAlignmentOptions.MidlineLeft,
                false);
            Size(detailSubheader.gameObject, -1f, 24f, 1f, 0f);
            detailSubheader.color = PlayerMenuUiFactory.Muted;

            RectTransform preview = EnsurePanel("PreviewCard", column, CardColor);
            Size(preview.gameObject, -1f, 150f, 1f, 0.4f);
            ConfigureStack(preview.gameObject, 4, 20);
            previewCaption = EnsureText(
                "PreviewCaption",
                preview,
                "PREVIEW | KNOWN MOVE",
                18f,
                TextAlignmentOptions.TopLeft,
                false);
            Size(previewCaption.gameObject, -1f, 22f, 1f, 0f);
            previewCaption.color = PlayerMenuUiFactory.Muted;
            detailTitle = EnsureText("DetailTitle", preview, "ARC OF THE POOL", 34f, TextAlignmentOptions.MidlineLeft, false);
            Size(detailTitle.gameObject, -1f, 44f, 1f, 0f);
            detailMeta = EnsureText(
                "DetailMeta",
                preview,
                "0 MODIFIERS | CLEAN ENTRY",
                20f,
                TextAlignmentOptions.MidlineLeft,
                false);
            Size(detailMeta.gameObject, -1f, 28f, 1f, 0f);
            detailMeta.text = "0 MODIFIERS | CLEAN ENTRY";

            slotRow = EnsureRect("StampSlots", column);
            Size(slotRow.gameObject, -1f, 128f, 1f, 0.3f);
            HorizontalLayoutGroup slotLayout = PlayerMenuUiFactory.HorizontalLayout(slotRow.gameObject, 12f);
            slotLayout.childAlignment = TextAnchor.MiddleCenter;
            slotLayout.childControlWidth = true;
            slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = true;
            slotOneFrame = EnsureSlot(slotRow, "SlotOne", 1, out slotOneCaption, out slotOneValue);
            Size(slotOneFrame.gameObject, -1f, -1f, 1f, 1f);
            slotTwoFrame = EnsureSlot(slotRow, "SlotTwo", 2, out slotTwoCaption, out slotTwoValue);
            Size(slotTwoFrame.gameObject, -1f, -1f, 1f, 1f);

            folioMetaRow = EnsureRect("MetaRow", column);
            Size(folioMetaRow.gameObject, -1f, 72f, 1f, 0f);
            HorizontalLayoutGroup metaLayout = PlayerMenuUiFactory.HorizontalLayout(folioMetaRow.gameObject, 12f);
            metaLayout.childAlignment = TextAnchor.MiddleCenter;
            metaLayout.childControlWidth = true;
            metaLayout.childControlHeight = true;
            metaLayout.childForceExpandWidth = true;
            metaLayout.childForceExpandHeight = true;

            costLabel = EnsureChip(folioMetaRow, "CostChip", "COST", "1 TOKEN", "CostLabel");
            resultLabel = EnsureChip(folioMetaRow, "ResultChip", "RESULT", "KNOWN / CLEAN", "ResultLabel");
            tokensLabel = EnsureChip(folioMetaRow, "TokensChip", "TOKENS", "0", "TokensLabel");
            reviewHint = EnsureChip(folioMetaRow, "ReviewChip", "REVIEW", "CONFIRM", "ReviewHint");

            primaryButton = EnsureButton("PrimaryButton", column, "GET KNOWN");
            Size(primaryButton.gameObject, -1f, 88f, 1f, 0f);
            CandyMachineTextStyle.ApplyTitle(primaryButton.transform.Find("Label")?.GetComponent<TMP_Text>());
            OrderChildren(
                column,
                "DetailHeader",
                "DetailSubheader",
                "PreviewCard",
                "StampSlots",
                "MetaRow",
                "PrimaryButton");
            OrderChildren(preview, "PreviewCaption", "DetailTitle", "DetailMeta");
            OrderChildren(folioMetaRow, "CostChip", "ResultChip", "TokensChip", "ReviewChip");
        }

        private void BuildDevStrip(RectTransform safe)
        {
            RectTransform grants = transform.Find("SafeAreaPanel/SafeContent/DetailBody/DevGrantStrip") as RectTransform;
            Transform stalePanel = safe != null ? safe.Find("DevGrantPanel") : null;

            if (grants != null && stalePanel != null && stalePanel != grants)
            {
                DestroyView(stalePanel.gameObject);
                stalePanel = null;
            }

            if (grants == null)
            {
                grants = stalePanel as RectTransform
                    ?? EnsurePanel("DevGrantPanel", safe, new Color(0.12f, 0.08f, 0.04f, 0.92f));
                PlayerMenuUiFactory.Bottom(grants, 64f, 48f, 48f, 196f);
            }

            CanvasGroup grantGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(grants.gameObject);
            Button grantRam = EnsureButton("GrantRamButton", grants, "GRANT RAM");
            SetAnchors(grantRam.transform as RectTransform, new Vector2(0.01f, 0.12f), new Vector2(0.2f, 0.88f));
            Button grantKnown = EnsureButton("GrantKnownButton", grants, "GRANT KNOWN");
            SetAnchors(grantKnown.transform as RectTransform, new Vector2(0.21f, 0.12f), new Vector2(0.4f, 0.88f));
            Button clear = EnsureButton("ClearButton", grants, "CLEAR");
            SetAnchors(clear.transform as RectTransform, new Vector2(0.41f, 0.12f), new Vector2(0.55f, 0.88f));
            TMP_Text grantStatus = EnsureText("GrantStatus", grants, "DEV GRANTS", 16f, TextAlignmentOptions.MidlineLeft);
            SetAnchors(grantStatus.rectTransform, new Vector2(0.57f, 0.1f), new Vector2(0.99f, 0.9f));
            grantPanel = PlayerMenuUiFactory.GetOrAdd<CandyMachineDevGrantPanel>(grants.gameObject);
            grantPanel.Bind(this, grantGroup, grantRam, grantKnown, clear, grantStatus, devConfig);

            // The prefab should remain inspectable exactly as authored. Runtime visibility
            // is still decided by CandyMachineDevGrantPanel when the screen opens.
            if (!Application.isPlaying)
            {
                grants.gameObject.SetActive(true);
                grantGroup.alpha = 1f;
                grantGroup.interactable = true;
                grantGroup.blocksRaycasts = true;
            }
        }

        private static void StripLegacyLayout(RectTransform safe)
        {
            if (safe == null)
                return;

            RectTransform content = safe.Find("SafeContent") as RectTransform;
            if (content == null)
                return;

            string[] retired =
            {
                "StatusStrip",
                "ModeSectionFrame",
                "ListSectionFrame",
                "ActionsSectionFrame",
            };

            for (int i = 0; i < retired.Length; i++)
            {
                Transform legacy = content.Find(retired[i]);
                if (legacy != null)
                    DestroyView(legacy.gameObject);
            }
        }


        private void ProtectBackgroundPresentation()
        {
            Transform background = transform.Find("Background");
            PlayerMenuBackgroundController backgroundController =
                background != null
                    ? background.GetComponent<PlayerMenuBackgroundController>()
                    : GetComponentInChildren<PlayerMenuBackgroundController>(true);
            if (backgroundController == null)
                return;

            backgroundController.RespectAuthoredPresentation = true;
            backgroundController.CaptureAuthoredPresentation();
        }

        private void ProtectAuthoredShellLayout()
        {
            Transform content =
                transform.Find("SafeAreaPanel/SafeContent")
                ?? transform.Find("SafeAreaPanel/SafeContent");
            if (content == null)
            {
                // Fallback: locate by name under this screen (nested GameServices instances).
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "SafeContent" && child.parent != null && child.parent.name.Contains("SafeArea"))
                    {
                        content = child;
                        break;
                    }
                }
            }

            if (content == null)
                return;

            Transform body = content.Find("BodyRow");
            if (body == null)
                return;

            // Authored BodyRow + DetailBody use freeform rects. Any VerticalLayoutGroup /
            // ContentSizeFitter on SafeContent will zero BodyRow width and shove Mode into
            // empty flex "Filler" space. Disable immediately | deferred Destroy is too late.
            StripContentLayoutDrivers(content);

            RectTransform bodyRect = body as RectTransform;
            if (bodyRect != null)
                RestoreAuthoredBodyRowRect(bodyRect);

            OrderChildren(body, "Slot_Browse", "Slot_Mode", "Slot_Detail");
            RepairModeColumn(body.Find("Slot_Mode") ?? body);

            // Keep flex slots from collapsing to zero-width empty fillers.
            EnsureSlotMinWidth(body.Find("Slot_Mode"), 280f, 0.2f);
            EnsureSlotMinWidth(body.Find("Slot_Browse"), 360f, 0.3f);
            EnsureSlotMinWidth(body.Find("Slot_Detail"), 360f, 0.3f);

            if (defaultSelection == null && folioTabButton != null)
                defaultSelection = folioTabButton;

            LayoutRebuilder.MarkLayoutForRebuild(content as RectTransform);
            if (bodyRect != null)
                LayoutRebuilder.MarkLayoutForRebuild(bodyRect);
        }

        private void RequestAuthoredShellLayoutProtection()
        {
            if (!Application.isPlaying)
            {
                ProtectAuthoredShellLayout();
                return;
            }

            if (authoredShellLayoutRoutine == null)
                authoredShellLayoutRoutine = StartCoroutine(ProtectAuthoredShellLayoutAfterCanvasPass());
        }

        private IEnumerator ProtectAuthoredShellLayoutAfterCanvasPass()
        {
            // Awake and OnEnable execute while UGUI is registering the authored shell. Defer all
            // hierarchy and layout changes until that pass has completed; mutating it in-place can
            // re-enter CanvasUpdateRegistry on Android development players.
            yield return new WaitForEndOfFrame();
            authoredShellLayoutRoutine = null;

            if (isActiveAndEnabled)
                ProtectAuthoredShellLayout();
        }

        private void StopAuthoredShellLayoutProtection()
        {
            if (authoredShellLayoutRoutine == null)
                return;

            StopCoroutine(authoredShellLayoutRoutine);
            authoredShellLayoutRoutine = null;
        }

        private static void StripContentLayoutDrivers(Transform content)
        {
            if (content == null)
                return;

            VerticalLayoutGroup vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                vertical.enabled = false;
                DestroyLayoutDriver(vertical);
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
                DestroyLayoutDriver(fitter);
            }
        }

        private static void DestroyLayoutDriver(Component component)
        {
            if (component == null)
                return;

            // Disabling the driver takes effect immediately. Runtime destruction during a Canvas
            // rebuild is unsafe and was the source of the recurring rebuild-list errors on Seeker.
            if (!Application.isPlaying)
                DestroyImmediate(component);
        }

        private static void RestoreAuthoredBodyRowRect(RectTransform bodyRect)
        {
            // BodyRow must not be layout-driven by SafeContent. Keep HLG on BodyRow for slots,
            // but ignore any parent layout group so width cannot collapse to 0.
            LayoutElement bodyElement = bodyRect.GetComponent<LayoutElement>();
            if (bodyElement == null)
                bodyElement = bodyRect.gameObject.AddComponent<LayoutElement>();
            bodyElement.ignoreLayout = true;
            bodyElement.minWidth = -1f;
            bodyElement.preferredWidth = -1f;
            bodyElement.flexibleWidth = -1f;
            bodyElement.minHeight = 0f;
            bodyElement.preferredHeight = 500f;
            bodyElement.flexibleHeight = 0f;

            // Stretch across SafeContent width; keep authored top band height.
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = new Vector2(0f, -32f);
            bodyRect.sizeDelta = new Vector2(-128f, 500f);
            bodyRect.localScale = Vector3.one;
            bodyRect.localRotation = Quaternion.identity;
        }

        private static void RepairModeColumn(Transform slotMode)
        {
            if (slotMode == null)
                return;

            Transform modeColumn = slotMode.Find("ModeColumn");
            if (modeColumn == null)
                modeColumn = slotMode.parent != null ? slotMode.parent.Find("ModeColumn") : null;

            // Runtime / bad edits sometimes leave a spacer named Filler where ModeColumn belongs.
            Transform filler = slotMode.Find("Filler");
            if (modeColumn == null && filler != null)
            {
                filler.name = "ModeColumn";
                modeColumn = filler;
            }
            else if (filler != null && modeColumn != null && filler != modeColumn)
            {
                if (Application.isPlaying)
                    DestroyImmediate(filler.gameObject);
                else
                    DestroyImmediate(filler.gameObject);
            }

            if (modeColumn == null && slotMode.childCount == 1)
            {
                Transform only = slotMode.GetChild(0);
                if (only.name != "BrowseColumn" && only.name != "DetailColumn")
                {
                    only.name = "ModeColumn";
                    modeColumn = only;
                }
            }

            if (modeColumn != null)
            {
                if (!modeColumn.gameObject.activeSelf)
                    modeColumn.gameObject.SetActive(true);

                RectTransform modeRect = modeColumn as RectTransform;
                if (modeRect != null)
                {
                    // Fill the slot; stagger is authored on nested visual offsets, not ignoreLayout.
                    modeRect.anchorMin = Vector2.zero;
                    modeRect.anchorMax = Vector2.one;
                    modeRect.offsetMin = Vector2.zero;
                    modeRect.offsetMax = new Vector2(0f, -100f);
                    modeRect.pivot = new Vector2(0.5f, 1f);
                }

                LayoutElement modeElement = modeColumn.GetComponent<LayoutElement>();
                if (modeElement != null)
                    modeElement.ignoreLayout = true;
            }
        }

        private static void EnsureSlotMinWidth(Transform slot, float minWidth, float flexibleWidth)
        {
            if (slot == null)
                return;
            LayoutElement element = slot.GetComponent<LayoutElement>();
            if (element == null)
                element = slot.gameObject.AddComponent<LayoutElement>();
            if (element.minWidth < minWidth)
                element.minWidth = minWidth;
            if (element.flexibleWidth <= 0f)
                element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 1f;
            element.ignoreLayout = false;
        }

        private void BindFromHierarchy()
        {
            if (presentationGroup == null)
                presentationGroup = GetComponent<CanvasGroup>();
            if (root == null)
            {
                Transform safe = transform.Find("SafeAreaPanel");
                if (safe != null)
                    root = safe.GetComponent<CanvasGroup>();
            }

            const string Header = "SafeAreaPanel/SafeContent/HeaderBar";
            const string DetailBodyRoot = "SafeAreaPanel/SafeContent/DetailBody";
            const string Result = "SafeAreaPanel/SafeContent/ResultPanel";

            // Prefab uses BodyRow/Slot_*/ColumnName; legacy scaffolds used flat BodyRow/ColumnName.
            string Mode =
                transform.Find("SafeAreaPanel/SafeContent/BodyRow/Slot_Mode/ModeColumn") != null
                    ? "SafeAreaPanel/SafeContent/BodyRow/Slot_Mode/ModeColumn"
                    : "SafeAreaPanel/SafeContent/BodyRow/ModeColumn";
            string Browse =
                transform.Find("SafeAreaPanel/SafeContent/BodyRow/Slot_Browse/BrowseColumn") != null
                    ? "SafeAreaPanel/SafeContent/BodyRow/Slot_Browse/BrowseColumn"
                    : "SafeAreaPanel/SafeContent/BodyRow/BrowseColumn";
            string Detail =
                transform.Find("SafeAreaPanel/SafeContent/BodyRow/Slot_Detail/DetailColumn") != null
                    ? "SafeAreaPanel/SafeContent/BodyRow/Slot_Detail/DetailColumn"
                    : "SafeAreaPanel/SafeContent/BodyRow/DetailColumn";

            if (bodyRow == null)
            {
                RectTransform body = FindRect("SafeAreaPanel/SafeContent/BodyRow");
                if (body != null)
                    bodyRow = body.GetComponent<CanvasGroup>();
            }

            titleLabel = titleLabel != null ? titleLabel : FindText($"{Mode}/TitlePlate/Title") ?? FindText($"{Mode}/Title/Title") ?? FindText($"{Mode}/Title") ?? FindText($"{Header}/TitlePlate/Title");
            ramLabel = ramLabel != null ? ramLabel : FindText($"{Mode}/TitlePlate/RamLabel") ?? FindText($"{Mode}/RamPlate/RamLabel") ?? FindText($"{Mode}/RAM/RamLabel") ?? FindText($"{Mode}/RAM") ?? FindText($"{Header}/TitlePlate/RamLabel");
            folioTabButton = folioTabButton != null ? folioTabButton : FindButton($"{Mode}/ModeTabs/FolioTab") ?? FindButton($"{Header}/ModeTabs/FolioTab");
            stampTabButton = stampTabButton != null ? stampTabButton : FindButton($"{Mode}/ModeTabs/StampTab") ?? FindButton($"{Header}/ModeTabs/StampTab");
            folioTabMarker = folioTabMarker != null ? folioTabMarker : FindImage($"{Mode}/ModeTabs/FolioTab/ActiveBar") ?? FindImage($"{Mode}/ModeTabs/FolioTab/ActiveBar") ?? FindImage($"{Header}/ModeTabs/FolioTab/ActiveBar");
            stampTabMarker = stampTabMarker != null ? stampTabMarker : FindImage($"{Mode}/ModeTabs/StampTab/ActiveBar") ?? FindImage($"{Mode}/ModeTabs/StampTab/ActiveBar") ?? FindImage($"{Header}/ModeTabs/StampTab/ActiveBar");

            browseHeader = browseHeader != null ? browseHeader : FindText($"{Browse}/BrowseHeader");
            browseSubheader = browseSubheader != null ? browseSubheader : FindText($"{Browse}/BrowseSubheader");
            folioBrowse = folioBrowse != null ? folioBrowse : FindRect($"{Browse}/FolioBrowse");
            stampBrowse = stampBrowse != null ? stampBrowse : FindRect($"{Browse}/StampBrowse");
            oddsLabel = oddsLabel != null ? oddsLabel : FindText($"{Browse}/FolioBrowse/OddsCard/OddsLabel");
            payoutLabel = payoutLabel != null ? payoutLabel : FindText($"{Browse}/FolioBrowse/PayoutCard/PayoutLabel");
            recentContent = recentContent != null ? recentContent : FindRect($"{Browse}/FolioBrowse/RecentStrip");
            listContent = listContent != null ? listContent : FindRect($"{Browse}/StampBrowse/ListViewport/ListContent");
            listFooter = listFooter != null ? listFooter : FindText($"{Browse}/StampBrowse/ListFooter");

            detailHeader = detailHeader != null ? detailHeader : FindText($"{Detail}/DetailHeader");
            detailSubheader = detailSubheader != null ? detailSubheader : FindText($"{Detail}/DetailSubheader");
            previewCaption = previewCaption != null ? previewCaption : FindText($"{Detail}/PreviewCard/PreviewCaption");
            detailTitle = detailTitle != null ? detailTitle : FindText($"{Detail}/PreviewCard/DetailTitle");
            detailMeta = detailMeta != null ? detailMeta : FindText($"{Detail}/PreviewCard/DetailMeta");
            slotRow = slotRow != null ? slotRow : FindRect($"{Detail}/StampSlots");
            slotOneFrame = slotOneFrame != null ? slotOneFrame : FindImage($"{Detail}/StampSlots/SlotOne");
            slotOneCaption = slotOneCaption != null ? slotOneCaption : FindText($"{Detail}/StampSlots/SlotOne/Caption");
            slotOneValue = slotOneValue != null ? slotOneValue : FindText($"{Detail}/StampSlots/SlotOne/Value");
            slotTwoFrame = slotTwoFrame != null ? slotTwoFrame : FindImage($"{Detail}/StampSlots/SlotTwo");
            slotTwoCaption = slotTwoCaption != null ? slotTwoCaption : FindText($"{Detail}/StampSlots/SlotTwo/Caption");
            slotTwoValue = slotTwoValue != null ? slotTwoValue : FindText($"{Detail}/StampSlots/SlotTwo/Value");
            costLabel = costLabel != null ? costLabel : FindText($"{Detail}/MetaRow/CostChip/CostLabel");
            resultLabel = resultLabel != null ? resultLabel : FindText($"{Detail}/MetaRow/ResultChip/ResultLabel");
            tokensLabel = tokensLabel != null ? tokensLabel : FindText($"{Detail}/MetaRow/TokensChip/TokensLabel");
            reviewHint = reviewHint != null ? reviewHint : FindText($"{Detail}/MetaRow/ReviewChip/ReviewHint");
            folioMetaRow = folioMetaRow != null ? folioMetaRow : FindRect($"{Detail}/MetaRow");
            primaryButton = primaryButton != null ? primaryButton : FindButton($"{Detail}/ButtonRow/PrimaryButton") ?? FindButton($"{Detail}/PrimaryButton");
            if (primaryButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].name == "PrimaryButton")
                    {
                        primaryButton = buttons[i];
                        break;
                    }
                }
            }

            if (detailBodyView == null)
            {
                RectTransform detailRoot = FindRect(DetailBodyRoot);
                if (detailRoot != null)
                    detailBodyView = detailRoot.GetComponent<CandyMachineDetailBodyView>();
            }
            if (detailBodyView != null)
            {
                detailBodyView.Bind(
                    detailBodyView.GetComponent<CanvasGroup>(),
                    FindText($"{DetailBodyRoot}/Title"),
                    EnsureDetailSubtitle(),
                    FindText($"{DetailBodyRoot}/Body"),
                    FindText($"{DetailBodyRoot}/Hint"));
            }

            if (resultView == null)
            {
                RectTransform resultRoot = FindRect(Result);
                if (resultRoot != null)
                    resultView = resultRoot.GetComponent<CandyMachineResultView>();
            }
            if (resultView != null)
            {
                if (detailBodyView != null && resultView.transform.GetSiblingIndex() > detailBodyView.transform.GetSiblingIndex())
                    resultView.transform.SetSiblingIndex(detailBodyView.transform.GetSiblingIndex());
                RectTransform card = FindRect($"{Result}/LibraryCardRoot");
                LibraryCardResultPresenter presenter = card != null
                    ? card.GetComponent<LibraryCardResultPresenter>()
                    : resultView.GetComponentInChildren<LibraryCardResultPresenter>(true);
                TMP_FontAsset stampFont = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
                if (presenter != null)
                {
                    presenter.Bind(
                        card,
                        FindImage($"{Result}/LibraryCardRoot/CardFace"),
                        FindImage($"{Result}/LibraryCardRoot/TypeWell"),
                        FindImage($"{Result}/LibraryCardRoot/MoveNameStamp"),
                        FindImage($"{Result}/LibraryCardRoot/StatsStamp"),
                        FindImage($"{Result}/LibraryCardRoot/Mod1Stamp"),
                        FindImage($"{Result}/LibraryCardRoot/Mod2Stamp"),
                        FindImage($"{Result}/LibraryCardRoot/EffectStamp"),
                        FindImage($"{Result}/LibraryCardRoot/RarityRow/MoveDot"),
                        FindImage($"{Result}/LibraryCardRoot/RarityRow/Mod1Dot"),
                        FindImage($"{Result}/LibraryCardRoot/RarityRow/Mod2Dot"),
                        FindImage($"{Result}/LibraryCardRoot/OutlineFlash"),
                        stampFont);
                }

                resultView.Bind(
                    resultView.GetComponent<CanvasGroup>(),
                    presenter,
                    FindButton($"{Result}/ContinueButton"));
                if (resultView.ContinueButton != null)
                    resultView.ContinueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            }

            if (grantPanel == null)
            {
                Transform grants = transform.Find($"{DetailBodyRoot}/DevGrantStrip")
                    ?? transform.Find("SafeAreaPanel/SafeContent/DevGrantPanel")
                    ?? transform.Find("SafeAreaPanel/DevGrantPanel");
                if (grants != null)
                    grantPanel = grants.GetComponent<CandyMachineDevGrantPanel>();
            }

            if (inputContext == null)
                inputContext = GetComponent<MenuInputContextController>();
            if (defaultSelection == null)
                defaultSelection = folioTabButton;
        }

        private TMP_Text FindText(string path)
        {
            Transform found = transform.Find(path);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private TMP_Text EnsureDetailSubtitle()
        {
            const string detailBodyRoot = "SafeAreaPanel/SafeContent/DetailBody";
            TMP_Text existing = FindText($"{detailBodyRoot}/Subtitle");
            if (existing != null)
                return existing;

            TMP_Text titleText = FindText($"{detailBodyRoot}/Title");
            if (titleText == null)
                return null;

            GameObject clone = Instantiate(titleText.gameObject, titleText.transform.parent);
            clone.name = "Subtitle";
            TMP_Text subtitleText = clone.GetComponent<TMP_Text>();
            subtitleText.text = string.Empty;
            subtitleText.fontSize = Mathf.Max(20f, titleText.fontSize * 0.68f);
            Color ink = titleText.color;
            subtitleText.color = new Color(ink.r, ink.g, ink.b, 0.78f);
            LayoutElement layout = clone.GetComponent<LayoutElement>();
            if (layout == null)
                layout = clone.AddComponent<LayoutElement>();
            layout.preferredHeight = 36f;
            layout.minHeight = 28f;
            layout.flexibleHeight = 0f;
            clone.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex() + 1);
            return subtitleText;
        }

        private Button FindButton(string path)
        {
            Transform found = transform.Find(path);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private Image FindImage(string path)
        {
            Transform found = transform.Find(path);
            return found != null ? found.GetComponent<Image>() : null;
        }

        private RectTransform FindRect(string path) => transform.Find(path) as RectTransform;

        private static void OrderChildren(Transform parent, params string[] names)
        {
            if (parent == null || names == null)
                return;

            for (int i = 0; i < names.Length; i++)
            {
                Transform child = parent.Find(names[i]);
                if (child != null)
                    child.SetSiblingIndex(i);
            }
        }

        private static void Size(GameObject go, float preferredWidth, float preferredHeight, float flexibleWidth, float flexibleHeight)
        {
            LayoutElement element = PlayerMenuUiFactory.LayoutElement(
                go,
                preferredWidth,
                preferredHeight,
                flexibleWidth,
                flexibleHeight);
            element.minHeight = preferredHeight >= 0f ? preferredHeight : 0f;
            if (preferredWidth >= 0f)
                element.minWidth = preferredWidth;
        }

        private static void ConfigureStack(GameObject go, int spacing, int padding)
        {
            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(go, spacing);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void ReparentNamed(Transform currentParent, string childName, Transform newParent)
        {
            Transform child = currentParent.Find(childName);
            if (child != null && child.parent != newParent)
                child.SetParent(newParent, false);
        }

        private void ApplyTitleStyles()
        {
            CandyMachineTextStyle.ApplyTitle(titleLabel);
            CandyMachineTextStyle.ApplyTitle(browseHeader);
            CandyMachineTextStyle.ApplyTitle(detailHeader);
            CandyMachineTextStyle.ApplyLabel(ramLabel);
            CandyMachineTextStyle.ApplyLabel(browseSubheader);
            CandyMachineTextStyle.ApplyLabel(detailSubheader);
            CandyMachineTextStyle.ApplyTitle(detailTitle);
        }

        private void SpawnEditorSamples()
        {
            if (Application.isPlaying)
                return;

            if (recentContent != null && recentContent.childCount == 0)
            {
                string[] chips = { "ARC LUNGE", "VEIL STEP", "IRON GUARD", "+4" };
                for (int i = 0; i < chips.Length; i++)
                {
                    RectTransform plate = EnsurePanel($"SampleChip_{i}", recentContent, CardColor);
                    Size(plate.gameObject, i == chips.Length - 1 ? 72f : 168f, 48f, 0f, 0f);
                    TMP_Text chip = EnsureText("Label", plate, chips[i], 18f, TextAlignmentOptions.Center, false);
                    PlayerMenuUiFactory.Stretch(chip.rectTransform, 8f, 4f, 8f, 4f);
                    chip.color = PlayerMenuUiFactory.Text;
                    recentChips.Add(plate.gameObject);
                }
            }

            if (listContent != null && listContent.childCount == 0)
            {
                knownRowButtons.Add(EnsureRow("Sample_VeilStep", "VEIL STEP", "[-][-]  0/2"));
                knownRowButtons.Add(EnsureRow("Sample_ArcLunge", "ARC LUNGE", "[#][-]  1/2"));
                knownRowButtons.Add(EnsureRow("Sample_IronGuard", "IRON GUARD", "[-][-]  0/2"));
                Button locked = EnsureRow("Sample_StoneWall", "STONE WALL", "[#][#]  2/2");
                locked.interactable = false;
                knownRowButtons.Add(locked);
            }
        }

        private RectTransform EnsureLabeledCard(
            Transform parent,
            string cardName,
            string captionName,
            string caption,
            string valueName,
            out TMP_Text value,
            float flexibleHeight)
        {
            RectTransform card = EnsurePanel(cardName, parent, CardColor);
            ConfigureStack(card.gameObject, 4, 16);
            TMP_Text captionLabel = EnsureText(captionName, card, caption, 16f, TextAlignmentOptions.TopLeft, false);
            Size(captionLabel.gameObject, -1f, 20f, 1f, 0f);
            captionLabel.color = PlayerMenuUiFactory.Muted;
            value = EnsureText(valueName, card, string.Empty, 22f, TextAlignmentOptions.TopLeft);
            Size(value.gameObject, -1f, -1f, 1f, flexibleHeight > 0f ? 1f : 0f);
            return card;
        }

        private TMP_Text EnsureChip(Transform parent, string chipName, string caption, string value, string valueName)
        {
            RectTransform chip = EnsurePanel(chipName, parent, CardColor);
            Size(chip.gameObject, -1f, -1f, 1f, 1f);
            ConfigureStack(chip.gameObject, 2, 12);
            TMP_Text captionLabel = EnsureText("Caption", chip, caption, 16f, TextAlignmentOptions.Center, false);
            Size(captionLabel.gameObject, -1f, 18f, 1f, 0f);
            captionLabel.color = PlayerMenuUiFactory.Muted;
            TMP_Text valueLabel = EnsureText(valueName, chip, value, 22f, TextAlignmentOptions.Center, false);
            Size(valueLabel.gameObject, -1f, -1f, 1f, 1f);
            valueLabel.color = PlayerMenuUiFactory.Text;
            return valueLabel;
        }

        private static RectTransform EnsureRect(string name, Transform parent)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found as RectTransform;
            return PlayerMenuUiFactory.CreateRect(name, parent);
        }

        private static RectTransform EnsurePanel(string name, Transform parent, Color color)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found as RectTransform;
            Image image = PlayerMenuUiFactory.CreateImage(name, parent, color);
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static TMP_Text EnsureText(
            string name,
            Transform parent,
            string value,
            float size,
            TextAlignmentOptions alignment,
            bool wrap = true)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found.GetComponent<TMP_Text>();
            return PlayerMenuUiFactory.CreateText(name, parent, value, size, alignment, wrap);
        }

        private Button EnsureButton(string name, Transform parent, string label)
        {
            Transform found = parent.Find(name);
            Button button = found != null ? found.GetComponent<Button>() : null;
            if (button == null)
                button = PlayerMenuUiFactory.CreateButton(name, parent, label, Color.white);

            TMP_Text text = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.color = ButtonInk;
            }

            chrome.Apply(button);
            return button;
        }

        private Button EnsureRow(string name, string moveName, string meterText)
        {
            Button row = EnsureButton(name, listContent, moveName);
            PlayerMenuUiFactory.LayoutElement(row.gameObject, -1f, 68f, 1f);

            TMP_Text label = row.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = 22f;
                PlayerMenuUiFactory.Stretch(label.rectTransform, 34f, 6f, 190f, 6f);
            }

            TMP_Text meter = EnsureText("Meter", row.transform, meterText, 20f, TextAlignmentOptions.MidlineRight, false);
            meter.text = meterText;
            PlayerMenuUiFactory.Stretch(meter.rectTransform, 0f, 6f, 24f, 6f);
            meter.rectTransform.anchorMin = new Vector2(0.55f, 0f);

            Transform existingMarker = row.transform.Find("SelectMarker");
            Image marker = existingMarker != null
                ? existingMarker.GetComponent<Image>()
                : PlayerMenuUiFactory.CreateImage("SelectMarker", row.transform, TabActiveColor);
            marker.rectTransform.anchorMin = new Vector2(0f, 0f);
            marker.rectTransform.anchorMax = new Vector2(0f, 1f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.offsetMin = new Vector2(14f, 12f);
            marker.rectTransform.offsetMax = new Vector2(22f, -12f);
            marker.raycastTarget = false;
            return row;
        }

        private static Image EnsureMarker(Button host, string name)
        {
            Transform found = host.transform.Find(name);
            Image marker = found != null ? found.GetComponent<Image>() : null;
            if (marker == null)
                marker = PlayerMenuUiFactory.CreateImage(name, host.transform, TabIdleColor);

            PlayerMenuUiFactory.Bottom(marker.rectTransform, 8f, 18f, 18f, 10f);
            marker.raycastTarget = false;
            marker.transform.SetSiblingIndex(1);
            return marker;
        }

        private static Image EnsureSlot(
            Transform parent,
            string name,
            int slotNumber,
            out TMP_Text caption,
            out TMP_Text value)
        {
            RectTransform slot = EnsurePanel(name, parent, SlotEmptyColor);
            ConfigureStack(slot.gameObject, 4, 16);
            Image frame = slot.GetComponent<Image>();
            caption = EnsureText("Caption", slot, $"[{slotNumber}] EMPTY", 18f, TextAlignmentOptions.TopLeft, false);
            Size(caption.gameObject, -1f, 22f, 1f, 0f);
            value = EnsureText("Value", slot, "EMPTY", 26f, TextAlignmentOptions.MidlineLeft, false);
            Size(value.gameObject, -1f, -1f, 1f, 1f);
            return frame;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
