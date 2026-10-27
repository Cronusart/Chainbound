using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public class ChainlingsPartyScreen : MenuScreen
    {
        public SpeciesDatabaseSO SpeciesDatabase => speciesDatabase;
        public EncounterCatalogSourceSO EncounterCatalog => encounterCatalog;

        private const int PartySlotCount = CombatConstants.ROSTER_SIZE;
        private const float ViewModeDebounceSeconds = 0.15f;
        private static ChainlingPartyViewMode sessionViewMode = ChainlingPartyViewMode.General;

        [Header("Data Source")]
        [Tooltip("Production resolves only Supabase/core services. Harness resolves only IChainlingsMenuTestHarness services.")]
        [SerializeField] private ChainlingsDataSourceMode dataSourceMode = ChainlingsDataSourceMode.Production;

        [Header("Services")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MonoBehaviour rosterProviderBehaviour;
        [SerializeField] private MonoBehaviour partyProviderBehaviour;
        [SerializeField] private MonoBehaviour partyLineupServiceBehaviour;
        [SerializeField] private MonoBehaviour cosmeticInventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour moveInventoryProviderBehaviour;
        [SerializeField] private MonoBehaviour moveEquipServiceBehaviour;
        [SerializeField] private MonoBehaviour visualIdentityServiceBehaviour;
        [SerializeField] private MonoBehaviour rebirthServiceBehaviour;

        [Header("Data")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private EncounterCatalogSourceSO encounterCatalog;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;
        [SerializeField] private ChainlingProgressionRewardTablesSO progressionRewardTables;
        [SerializeField] private ChainlingProgressionEventCatalogSO progressionEventCatalog;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [SerializeField] private ChainlingBadgeCatalogSO badgeCatalog;

        [Header("Party Read Model")]
        [SerializeField] private bool logPartyReadModelDebug;
        [SerializeField] private ChainlingPartyViewMode debugReadModelViewMode = ChainlingPartyViewMode.General;

        [Header("Navigation")]
        [SerializeField] private MenuScreenStack screenStack;
        [SerializeField] private ChainlingDetailScreen detailScreen;
        [SerializeField] private ChainlingDetailScreen detailViewPrefab;

        [Header("Audio")]
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        [Header("Phase 1 Layout")]
        [SerializeField] private PartyTabMotionProfileSO motionProfile;
        [SerializeField] private PartyTabStarRatingArtSO starRatingArt;
        [SerializeField] private PartyTabCycleTypographySO cycleTypography;
        [SerializeField] private PartyTabArchiveIndicatorArtSO archiveIndicatorArt;
        [SerializeField] private ChainlingPartyTypeChromePaletteSO typeChromePalette;
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [Header("Type Icon Layer")]
        [Tooltip("When disabled, each column's authored TypeIconScrollRoot RectTransform is preserved at runtime.")]
        [SerializeField] private bool typeIconOverrideAuthoredViewport;
        [SerializeField] private Vector2 typeIconViewportAnchorMin = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 typeIconViewportAnchorMax = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 typeIconViewportAnchoredPosition = new Vector2(0f, 150f);
        [SerializeField] private Vector2 typeIconViewportSizeDelta = new Vector2(370f, 340f);
        [Tooltip("When disabled, each column's authored PrimaryTypeIcon RectTransform is preserved at runtime.")]
        [SerializeField] private bool typeIconOverrideAuthoredIconRects;
        [SerializeField] private Vector2 typeIconSize = new Vector2(260f, 260f);
        [SerializeField] private Vector2 typeIconScrollDirection = Vector2.up;
        [SerializeField] private float typeIconScrollDistance = 86f;
        [SerializeField] private float typeIconScrollPixelsPerSecond = 15.64f;
        [SerializeField] private float typeIconAlpha = 0.18f;
        [SerializeField] private ChainlingPartyMotionController motionController;
        [SerializeField] private ChainlingPartyNavigationController navigationController;
        [SerializeField] private ChainlingPartySectionChrome sectionChrome;
        [SerializeField] private RectTransform safeContent;
        [SerializeField] private ChainlingPartyViewRail viewRail;
        [SerializeField] private RectTransform columnsRoot;
        [SerializeField] private ChainlingPartyColumnShell[] columnShells = new ChainlingPartyColumnShell[PartySlotCount];
        [SerializeField] private ChainlingPartyColumnShell columnShellPrefab;
        [SerializeField] private ChainlingPartyFooterHints footerHints;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private RectTransform debugHarnessRoot;
        [SerializeField] private Button regenerateDebugRosterButton;
        [SerializeField] private Button randomizeDebugLineupButton;

        private readonly PlayerRosterEntry[] partySlots = new PlayerRosterEntry[PartySlotCount];
        private PlayerRosterSnapshot rosterCache;
        private int selectedSlotIndex;
        private ChainlingPartyViewMode activeViewMode = ChainlingPartyViewMode.General;
        private bool viewRailOpen;
        private Transform confirmedInputIsolationRoot;
        private bool isLoading;
        private bool buttonListenersBound;
        private bool sectionChromeListenersBound;
        private bool navigationControllerEventsBound;
        private bool pendingTabEnterPresentation;
        private bool pendingHpDeltaAnimation;
        private bool rosterPresentedThisOpen;
        private bool suppressHpDeltaOnNextRefresh;
        private Task rosterFetchTask;
        private MenuInputContextController activeInputContext;
        private Coroutine viewModeDebounceRoutine;
        private MonoBehaviour cachedSfxPlayerBehaviour;
        private long lastRosterRefreshSequence;
        private bool rosterRefreshRequested;

        /// <summary>
        /// True when this instance already contains an authored hierarchy (embedded prefab or scene instance).
        /// Used by PlayerMenuRoot tab wiring; avoids treating nested prefabs as empty stubs.
        /// </summary>
        public bool HasEmbeddedLayout =>
            safeContent != null || transform.Find("SafeContent") != null;

        public bool IsPrefabAuthored =>
            safeContent != null &&
            viewRail != null &&
            columnsRoot != null &&
            footerHints != null &&
            HasAuthoredColumnShells();

        public ChainlingsDataSourceMode DataSourceMode => dataSourceMode;

        private bool IsColumnEditing =>
            navigationController != null &&
            navigationController.Phase == ChainlingPartyNavigationPhase.ColumnEditing;

        public void WireViewReferences(ChainlingDetailScreen detail, ChainlingPartySlotWidget slotPrefab)
        {
            if (detail != null)
                detailScreen = detail;
            ConfigureDetailScreen();
            if (detailScreen != null)
                detailScreen.Hide();
        }

        public void WireColumnShellPrefab(ChainlingPartyColumnShell shellPrefab)
        {
            if (shellPrefab != null)
                columnShellPrefab = shellPrefab;
        }

        public void WireDetailViewPrefab(ChainlingDetailScreen prefab)
        {
            if (prefab != null)
                detailViewPrefab = prefab;
        }

        public ChainlingDetailScreen DetailViewPrefab => detailViewPrefab;

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt(false);
        }

        public MenuScreenStack GetScreenStack()
        {
            return screenStack != null ? screenStack : ResolveScreenStack();
        }

        public void ConfigureScreenIdentity(string id, string name, CanvasGroup group)
        {
            SetScreenIdentity(id, name, group);
        }

        public void SetDataSourceMode(ChainlingsDataSourceMode mode)
        {
            dataSourceMode = mode;
            ClearIncompatibleServiceBindings();
            if (detailScreen != null)
                detailScreen.SetDataSourceMode(mode);
            RefreshDebugHarnessControls();
        }

        public void WarmPresentation()
        {
            EnsureBuilt();
            speciesDatabase?.WarmLookup();
            typeIconPalette?.WarmPresentation();
            if (motionProfile != null)
                _ = motionProfile.HpFillDuration;
            if (columnShells != null)
            {
                for (int i = 0; i < columnShells.Length; i++)
                {
                    ChainlingPartyColumnShell shell = columnShells[i];
                    if (shell == null)
                        continue;
                    shell.ConfigureTypeIconPalette(typeIconPalette);
                    Image typeImage = shell.TypeIconPrimaryImage;
                    if (typeImage != null)
                        typeImage.enabled = typeImage.enabled;
                }
            }

            if (rosterCache == null)
                _ = RefreshRosterAsync(bindWhenComplete: false);
        }

        public void Configure(
            SupabaseConfigSO config,
            SpeciesDatabaseSO speciesDb,
            CombatVisualDatabaseSO combatVisualDb,
            ChainlingVisualIdentityDatabaseSO chainlingIdentityDb,
            MoveDatabaseSO moveDb,
            ProgressionConfigSO progression,
            MonoBehaviour rosterProviderSource,
            MonoBehaviour partyProviderSource,
            MonoBehaviour cosmeticInventoryServiceSource,
            MonoBehaviour moveInventoryProviderSource,
            MonoBehaviour moveEquipServiceSource,
            MonoBehaviour rebirthServiceSource,
            MenuScreenStack stack,
            MonoBehaviour partyLineupServiceSource = null,
            MonoBehaviour visualIdentityServiceSource = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (speciesDb != null)
                speciesDatabase = speciesDb;
            if (combatVisualDb != null)
                visualDatabase = combatVisualDb;
            if (chainlingIdentityDb != null)
                identityDatabase = chainlingIdentityDb;
            if (moveDb != null)
                moveDatabase = moveDb;
            if (progression != null)
                progressionConfig = progression;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, rosterProviderSource))
                rosterProviderBehaviour = rosterProviderSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, partyProviderSource))
                partyProviderBehaviour = partyProviderSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, partyLineupServiceSource))
                partyLineupServiceBehaviour = partyLineupServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, cosmeticInventoryServiceSource))
                cosmeticInventoryServiceBehaviour = cosmeticInventoryServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveInventoryProviderSource))
                moveInventoryProviderBehaviour = moveInventoryProviderSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveEquipServiceSource))
                moveEquipServiceBehaviour = moveEquipServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, visualIdentityServiceSource))
                visualIdentityServiceBehaviour = visualIdentityServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, rebirthServiceSource))
                rebirthServiceBehaviour = rebirthServiceSource;
            if (stack != null)
                screenStack = stack;

            EnsureServiceBehaviours();
            EnsureBuilt();
            EnsureButtonListeners();
            ConfigureDetailScreen();
        }

        public void ConfigurePartyReadModelResources(
            ChainlingBadgeCatalogSO catalog,
            ModifierCatalogSO modifiers,
            bool enableDebugLog = false,
            ChainlingProgressionRewardTablesSO progressionTables = null,
            ChainlingProgressionEventCatalogSO eventCatalog = null)
        {
            if (catalog != null)
                badgeCatalog = catalog;
            if (modifiers != null)
                modifierCatalog = modifiers;
            if (progressionTables != null)
                progressionRewardTables = progressionTables;
            if (eventCatalog != null)
                progressionEventCatalog = eventCatalog;
            logPartyReadModelDebug = enableDebugLog;
        }

        private void OnEnable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed += HandleRosterChanged;
            EnsureButtonListeners();
            if (pendingTabEnterPresentation)
            {
                EnsureBuilt();
                HoldColumnsForTabEnter();
            }
        }

        private void OnDisable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
            UnbindButtonListeners();
            UnbindNavigationControllerEvents();
            if (activeInputContext != null)
                activeInputContext.SetContentInputHandler(null);
            pendingTabEnterPresentation = false;
            if (viewModeDebounceRoutine != null)
            {
                StopCoroutine(viewModeDebounceRoutine);
                viewModeDebounceRoutine = null;
            }
        }

        /// <summary>
        /// Called by <see cref="PlayerMenuController"/> when the Chainlings tab content opens.
        /// Schedules the staggered column + swap-in presentation on the next render pass.
        /// </summary>
        public void NotifyTabOpened()
        {
            pendingTabEnterPresentation = true;
            rosterPresentedThisOpen = false;
            suppressHpDeltaOnNextRefresh = true;
        }

        public override void Show()
        {
            EnsureBuilt();
            EnsureButtonListeners();
            EnsurePartyNavigation();
            activeViewMode = sessionViewMode;
            viewRail?.SetMode(activeViewMode, false);
            if (pendingTabEnterPresentation)
            {
                HoldColumnsForTabEnter();
                navigationController?.ResetToPartySection(true);
            }
            base.Show();
            if (activeInputContext != null)
                RefreshInputNavigation();

            TryAdoptStoredRoster();

            if (HasUnprocessedRosterRefresh())
            {
                rosterRefreshRequested = false;
                Refresh();
                return;
            }

            if (rosterCache != null && !isLoading)
                RenderParty();
            else if (pendingTabEnterPresentation)
                HoldColumnsForTabEnter();
            else
                RenderUnboundPlaceholder();
        }

        public override void ConfigureInputContext(MenuInputContextController context)
        {
            activeInputContext = context;
            if (activeInputContext != null)
                activeInputContext.SetContentInputHandler(navigationController);
            RefreshInputNavigation();
        }

        public override bool TryHandleCancel()
        {
            return navigationController != null && navigationController.TryHandleCancel();
        }

        public override void Refresh()
        {
            EnsureBuilt();
            TryAdoptStoredRoster();
            if (isLoading)
            {
                if (rosterCache != null && IsVisible)
                    RenderParty();
                return;
            }

            bool skipHpDelta = suppressHpDeltaOnNextRefresh;
            if (skipHpDelta)
                suppressHpDeltaOnNextRefresh = false;
            else
                pendingHpDeltaAnimation = true;

            bool alreadyPaintedThisOpen =
                rosterPresentedThisOpen &&
                !pendingTabEnterPresentation &&
                skipHpDelta;

            _ = RefreshRosterAsync(bindWhenComplete: !alreadyPaintedThisOpen);
        }

        private void HandleRosterChanged(PlayerRosterRefreshSignal.Notice notice)
        {
            if (!MatchesActiveProfile(notice.PlayerProfileId) || notice.Sequence <= lastRosterRefreshSequence)
                return;

            lastRosterRefreshSequence = notice.Sequence;
            rosterRefreshRequested = true;
            if (gameObject.activeInHierarchy)
                Refresh();
        }

        private bool HasUnprocessedRosterRefresh()
        {
            GameSessionContext session = GameSessionContext.Instance;
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return rosterRefreshRequested;

            if (PlayerRosterRefreshSignal.TryGetLatest(session.PlayerProfileId, out PlayerRosterRefreshSignal.Notice notice) &&
                notice.Sequence > lastRosterRefreshSequence)
            {
                lastRosterRefreshSequence = notice.Sequence;
                rosterRefreshRequested = true;
            }

            return rosterRefreshRequested;
        }

        private static bool MatchesActiveProfile(string playerProfileId)
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null &&
                   !string.IsNullOrWhiteSpace(session.PlayerProfileId) &&
                   string.Equals(session.PlayerProfileId, playerProfileId, StringComparison.Ordinal);
        }

        private void EnsureButtonListeners()
        {
            if (buttonListenersBound)
            {
                EnsureSectionChromeButtonListeners();
                return;
            }

            if (regenerateDebugRosterButton != null)
                regenerateDebugRosterButton.onClick.AddListener(HandleRegenerateDebugRosterClicked);
            if (randomizeDebugLineupButton != null)
                randomizeDebugLineupButton.onClick.AddListener(HandleRandomizeDebugLineupClicked);

            buttonListenersBound = true;
            EnsureSectionChromeButtonListeners();
        }

        private void UnbindButtonListeners()
        {
            if (!buttonListenersBound)
                return;

            if (regenerateDebugRosterButton != null)
                regenerateDebugRosterButton.onClick.RemoveListener(HandleRegenerateDebugRosterClicked);
            if (randomizeDebugLineupButton != null)
                randomizeDebugLineupButton.onClick.RemoveListener(HandleRandomizeDebugLineupClicked);

            buttonListenersBound = false;
            UnbindSectionChromeButtonListeners();
        }

        private void EnsureSectionChromeButtonListeners()
        {
            if (sectionChromeListenersBound || sectionChrome == null)
                return;

            if (sectionChrome.ViewRailSectionButton != null)
                sectionChrome.ViewRailSectionButton.onClick.AddListener(HandleViewSectionSubmitted);
            if (sectionChrome.PartySectionButton != null)
                sectionChrome.PartySectionButton.onClick.AddListener(HandlePartySectionSubmitted);
            sectionChrome.ViewRailSectionSelected += HandleViewSectionSelected;
            sectionChrome.PartySectionSelected += HandlePartySectionSelected;

            sectionChromeListenersBound = true;
        }

        private void UnbindSectionChromeButtonListeners()
        {
            if (!sectionChromeListenersBound || sectionChrome == null)
                return;

            if (sectionChrome.ViewRailSectionButton != null)
                sectionChrome.ViewRailSectionButton.onClick.RemoveListener(HandleViewSectionSubmitted);
            if (sectionChrome.PartySectionButton != null)
                sectionChrome.PartySectionButton.onClick.RemoveListener(HandlePartySectionSubmitted);
            sectionChrome.ViewRailSectionSelected -= HandleViewSectionSelected;
            sectionChrome.PartySectionSelected -= HandlePartySectionSelected;

            sectionChromeListenersBound = false;
        }

        private void HandleRegenerateDebugRosterClicked()
        {
            if (!isLoading)
                _ = RegenerateDebugRosterAsync();
        }

        private void HandleRandomizeDebugLineupClicked()
        {
            if (!isLoading)
                _ = RandomizeDebugLineupAsync();
        }

        private void HandleViewSectionSubmitted()
        {
            if (navigationController != null &&
                navigationController.Phase == ChainlingPartyNavigationPhase.ViewSection)
            {
                navigationController.EnterViewEditing();
            }
        }

        private void HandleViewSectionSelected()
        {
            if (navigationController != null &&
                navigationController.Phase == ChainlingPartyNavigationPhase.PartySection)
            {
                navigationController.EnterViewSection();
            }
        }

        private void HandlePartySectionSubmitted()
        {
            if (navigationController != null &&
                navigationController.Phase == ChainlingPartyNavigationPhase.PartySection)
            {
                navigationController.EnterColumnEditing();
            }
        }

        private void HandlePartySectionSelected()
        {
            if (navigationController != null &&
                navigationController.Phase == ChainlingPartyNavigationPhase.ViewSection)
            {
                navigationController.EnterPartySection();
            }
        }

        private async Task RegenerateDebugRosterAsync()
        {
            IChainlingsMenuTestHarness harness = ResolveTestHarness();
            if (harness == null)
            {
                SetStatus("No Chainlings test harness is assigned.");
                return;
            }

            var session = GameSessionContext.Instance;
            SetLoading(true, "Regenerating sandbox roster...");
            rosterCache = await harness.RegenerateRosterAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "");
            SetLoading(false, rosterCache == null ? "Sandbox roster failed to generate." : $"{harness.HarnessLabel}: roster regenerated.");
            if (rosterCache != null)
            {
                pendingHpDeltaAnimation = true;
                RenderParty();
            }
        }

        private async Task RandomizeDebugLineupAsync()
        {
            IChainlingsMenuTestHarness harness = ResolveTestHarness();
            if (harness == null)
            {
                SetStatus("No Chainlings test harness is assigned.");
                return;
            }

            var session = GameSessionContext.Instance;
            SetLoading(true, "Randomizing sandbox party...");
            MenuMutationResult result = await harness.RandomizeLineupAsync(
                session != null ? session.PlayerProfileId : "",
                session != null ? session.WalletAddress : "");

            if (result == null || !result.success)
            {
                SetLoading(false, result != null ? result.error : "Sandbox party randomization failed.");
                return;
            }

            pendingHpDeltaAnimation = true;
            await RefreshRosterAsync();
            SetStatus($"{harness.HarnessLabel}: party randomized.");
        }

        private async Task RefreshRosterAsync(bool bindWhenComplete = true)
        {
            EnsureServiceBehaviours();
            IPlayerRosterProvider provider = ResolveRosterProvider();
            if (provider == null)
            {
                SetStatus("No roster provider was found.");
                if (pendingTabEnterPresentation && IsVisible)
                {
                    pendingTabEnterPresentation = false;
                    RenderUnboundPlaceholder();
                }

                return;
            }

            if (rosterFetchTask != null && !rosterFetchTask.IsCompleted)
            {
                await rosterFetchTask;
                TryBindFetchedRoster(bindWhenComplete);
                return;
            }

            rosterFetchTask = FetchRosterCore(provider, bindWhenComplete);
            await rosterFetchTask;
        }

        private async Task FetchRosterCore(IPlayerRosterProvider provider, bool bindWhenComplete)
        {
            bool silent = !bindWhenComplete && !pendingTabEnterPresentation;
            if (silent)
                isLoading = true;
            else
                SetLoading(true, pendingTabEnterPresentation ? "" : "Loading active party...");

            try
            {
                var session = GameSessionContext.Instance;
                rosterCache = await PlayerProfileReadStore.ResolveOrCreate().RefreshRosterAsync(
                    provider,
                    session != null ? session.PlayerProfileId : "",
                    session != null ? session.WalletAddress : "");
            }
            finally
            {
                if (silent)
                {
                    isLoading = false;
                    RefreshDebugHarnessControls();
                }
                else
                    SetLoading(false, rosterCache == null ? "Party failed to load." : "");
            }

            TryBindFetchedRoster(bindWhenComplete);
        }

        private void TryBindFetchedRoster(bool bindWhenComplete)
        {
            if (!IsVisible)
                return;

            bool waitingForFirstPaint = pendingTabEnterPresentation || !rosterPresentedThisOpen;
            if (rosterCache != null && (bindWhenComplete || waitingForFirstPaint))
                RenderParty();
            else if (pendingTabEnterPresentation && rosterCache == null)
            {
                pendingTabEnterPresentation = false;
                RenderUnboundPlaceholder();
            }
        }

        private void TryAdoptStoredRoster()
        {
            GameSessionContext session = GameSessionContext.Instance;
            if (session == null)
                return;

            if (rosterCache != null &&
                !string.IsNullOrWhiteSpace(rosterCache.playerProfileId) &&
                !string.Equals(rosterCache.playerProfileId, session.PlayerProfileId, StringComparison.Ordinal))
            {
                rosterCache = null;
            }

            if (PlayerProfileReadStore.ResolveOrCreate().TryGetRoster(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    out PlayerRosterSnapshot stored))
            {
                rosterCache = stored;
            }
        }

        private void HoldColumnsForTabEnter()
        {
            EnsureBuilt();
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
            {
                ChainlingPartyColumnShell shell = columnShells[i];
                if (shell == null)
                    continue;

                shell.ApplyColumnVisibility(0f, false);
            }
        }

        private void RenderUnboundPlaceholder()
        {
            EnsureBuilt();
            BuildPartySlots();
            RefreshAllColumns(false, false);
            RefreshFooterAndHints();
            RefreshInputNavigation();
        }

        private void RenderParty()
        {
            EnsureBuilt();
            RefreshDebugHarnessControls();
            BuildPartySlots();

            int defaultSlot = ResolveInitialSlotIndex();
            if (selectedSlotIndex < 0 || selectedSlotIndex >= PartySlotCount)
                selectedSlotIndex = defaultSlot;
            if (partySlots[selectedSlotIndex] == null && partySlots[defaultSlot] != null)
                selectedSlotIndex = defaultSlot;

            RefreshAllColumns(false, pendingHpDeltaAnimation && !pendingTabEnterPresentation);
            bool playedTabEnterPresentation = pendingTabEnterPresentation;
            PlayTabEnterPresentationIfPending();
            if (!playedTabEnterPresentation)
                RefreshColumnFocus(false);

            pendingHpDeltaAnimation = false;
            rosterPresentedThisOpen = true;

            if (rosterCache != null && rosterCache.Count == 0)
                SetStatus("No Chainlings found for this profile.");
            else if (!HasAnyPartySlot())
                SetStatus("No active party assigned. Change members at a PC.");

            RefreshFooterAndHints();
            RefreshInputNavigation();
            LogPartyReadModelDebug();
        }

        private void RefreshAllColumns(bool animateViewChange, bool animateHpDelta)
        {
            for (int i = 0; i < PartySlotCount; i++)
                RefreshColumn(i, animateViewChange, animateHpDelta);
        }

        private void RefreshColumn(int slotIndex, bool animateViewChange, bool animateHpDelta)
        {
            ChainlingPartyColumnShell shell = GetColumnShell(slotIndex);
            if (shell == null)
                return;

            shell.ConfigureTypeChromePalette(typeChromePalette);
            shell.ConfigureTypeIconPalette(typeIconPalette);
            shell.ConfigureCycleTypography(cycleTypography);
            shell.ConfigureArchiveIndicatorArt(archiveIndicatorArt);
            ConfigureColumnTypeIconPresentation(shell);

            PlayerRosterEntry entry = partySlots[slotIndex];
            SpeciesDefinitionSO species = ResolveSpecies(entry);
            ResolvedChainlingVisualIdentity resolvedVisualIdentity = ResolveVisualIdentity(entry);
            ChainlingPartyColumnContext ctx = BuildColumnContext(entry, species, slotIndex);
            ChainlingPartyColumnSummary summary = ChainlingPartyColumnViewModelBuilder.BuildSummary(activeViewMode, ctx);
            bool columnEditingFocus = IsColumnEditing && slotIndex == selectedSlotIndex;
            CanvasGroup viewContentGroup = shell.InfoPanelViewContentGroup;
            bool deferViewOnlyBind =
                animateViewChange &&
                motionController != null &&
                viewContentGroup != null;

            bool deferProgressReveal = pendingTabEnterPresentation;
            if (!deferViewOnlyBind)
            {
                shell.InfoPanelBinder?.SetDeferProgressReveal(deferProgressReveal);
                if (animateViewChange)
                    shell.BindView(summary, activeViewMode);
                else
                {
                    BindColumnShell(
                        shell,
                        slotIndex,
                        entry,
                        species,
                        resolvedVisualIdentity,
                        summary,
                        columnEditingFocus);
                }
            }

            ChainlingPartyColumnVitals vitals = summary != null ? summary.vitals : null;
            bool animateColumnChrome = motionController != null && !pendingTabEnterPresentation;
            if (motionController != null)
            {
                motionController.ApplyHpBar(
                    slotIndex,
                    shell.HpBarFill,
                    shell.HpLabel,
                    vitals,
                    entry == null,
                    animateHpDelta && !deferProgressReveal,
                    deferEnterReveal: deferProgressReveal);
                motionController.ApplyColumnOpacity(
                    shell,
                    deferProgressReveal ? 0f : ResolveColumnOpacity(slotIndex),
                    animate: animateColumnChrome);
                motionController.SetColumnFocused(
                    shell,
                    columnEditingFocus,
                    IsColumnEditing,
                    animate: animateColumnChrome);
            }
            else
            {
                shell.ApplyColumnVisibility(
                    deferProgressReveal ? 0f : ResolveColumnOpacity(slotIndex),
                    false);
            }

            if (deferViewOnlyBind)
            {
                ChainlingPartyColumnSummary viewSummary = summary;
                ChainlingPartyViewMode viewMode = activeViewMode;
                motionController.PlayViewModeCrossfade(
                    viewContentGroup,
                    () => shell.BindView(viewSummary, viewMode));
            }

            shell.ApplyVisualStageVisibility(
                ResolveVisualStageOpacity(slotIndex),
                animate: animateColumnChrome);
        }

        private void BindColumnShell(
            ChainlingPartyColumnShell shell,
            int slotIndex,
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            ResolvedChainlingVisualIdentity resolvedVisualIdentity,
            ChainlingPartyColumnSummary summary,
            bool columnEditingFocus)
        {
            if (shell == null)
                return;

            shell.Bind(
                slotIndex,
                entry,
                species,
                resolvedVisualIdentity,
                summary,
                activeViewMode,
                columnEditingFocus,
                HandleSlotFocused,
                HandleSlotSubmitted);
        }

        private void PlayTabEnterPresentationIfPending()
        {
            if (!pendingTabEnterPresentation)
                return;

            if (motionController != null)
                motionController.PlayScreenEnter(columnShells, BuildColumnOpacityTargets());
            else if (columnShells != null)
            {
                float[] targets = BuildColumnOpacityTargets();
                for (int i = 0; i < columnShells.Length; i++)
                {
                    float alpha = targets != null && i < targets.Length ? targets[i] : 1f;
                    columnShells[i]?.ApplyColumnVisibility(alpha, false);
                }
            }

            pendingTabEnterPresentation = false;
        }

        private ChainlingPartySectionFramePalette ResolveColumnPalette()
        {
            if (columnsRoot == null && safeContent != null)
                columnsRoot = safeContent.Find("ColumnsRoot") as RectTransform;

            return columnsRoot != null
                ? columnsRoot.GetComponent<ChainlingPartySectionFramePalette>()
                : null;
        }

        private ChainlingPartyColumnOpacityState ResolveColumnOpacityState(int slotIndex)
        {
            ChainlingPartyNavigationPhase phase = navigationController != null
                ? navigationController.Phase
                : ChainlingPartyNavigationPhase.ViewSection;

            if (phase != ChainlingPartyNavigationPhase.PartySection &&
                phase != ChainlingPartyNavigationPhase.ColumnEditing)
            {
                return ChainlingPartyColumnOpacityState.PartySectionInactive;
            }

            if (phase == ChainlingPartyNavigationPhase.ColumnEditing && slotIndex == selectedSlotIndex)
                return ChainlingPartyColumnOpacityState.ColumnFocused;

            return ChainlingPartyColumnOpacityState.PartySectionActive;
        }

        private float ResolveColumnOpacity(int slotIndex)
        {
            return 1f;
        }

        private float ResolveVisualStageOpacity(int slotIndex)
        {
            return 1f;
        }

        private float[] BuildColumnOpacityTargets()
        {
            float[] targets = new float[PartySlotCount];
            for (int i = 0; i < PartySlotCount; i++)
                targets[i] = ResolveColumnOpacity(i);
            return targets;
        }

        private ChainlingPartyColumnContext BuildColumnContext(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            int slotIndex)
        {
            return ChainlingPartyReadModelDebug.CreateContext(
                entry,
                species,
                slotIndex,
                moveDatabase,
                modifierCatalog,
                identityDatabase,
                visualDatabase,
                progressionConfig,
                badgeCatalog,
                null,
                progressionRewardTables,
                progressionEventCatalog,
                speciesDatabase,
                encounterCatalog);
        }

        private void HandleViewModeSelected(ChainlingPartyViewMode mode)
        {
            ApplyPreviewViewMode(mode);
        }

        private void ApplyPreviewViewMode(ChainlingPartyViewMode mode)
        {
            if (mode == activeViewMode)
                return;

            activeViewMode = mode;
            sessionViewMode = mode;
            if (viewModeDebounceRoutine != null)
            {
                StopCoroutine(viewModeDebounceRoutine);
                viewModeDebounceRoutine = null;
            }

            RefreshAllColumns(true, false);
            RefreshFooterAndHints();
            RefreshInputNavigation();
        }

        private IEnumerator RebuildColumnsAfterViewDebounce()
        {
            yield return new WaitForSecondsRealtime(ViewModeDebounceSeconds);
            viewModeDebounceRoutine = null;
            RefreshAllColumns(true, false);
            RefreshFooterAndHints();
            RefreshInputNavigation();
        }

        private void HandleViewRailOpenRequested()
        {
            if (navigationController != null)
            {
                if (navigationController.Phase == ChainlingPartyNavigationPhase.ViewSection)
                {
                    navigationController.EnterViewEditing();
                    return;
                }

                if (navigationController.Phase == ChainlingPartyNavigationPhase.ViewEditing)
                {
                    OpenViewCarousel();
                    return;
                }

                return;
            }

            ArmViewRail();
        }

        private void HandleViewRailCloseRequested()
        {
            if (navigationController != null)
                navigationController.EnterViewSection();
            else
                DisarmViewRail();
        }

        private void HandleNavigationPhaseChanged(
            ChainlingPartyNavigationPhase previous,
            ChainlingPartyNavigationPhase current)
        {
            if (current == ChainlingPartyNavigationPhase.ViewEditing)
                OpenViewCarousel();
            else
                DisarmViewRail();

            PlaySfxCue(PlayerMenuSfxCue.PanelSwitch);
            sectionChrome?.SetActivePhase(current);
            ApplyOutsideFrameInputLock(current);
            bool exitingColumnEditing = previous == ChainlingPartyNavigationPhase.ColumnEditing &&
                current != ChainlingPartyNavigationPhase.ColumnEditing;
            RefreshColumnFocus(true, exitingColumnEditing);
            RefreshInputNavigation();
        }

        private void ApplyOutsideFrameInputLock(ChainlingPartyNavigationPhase phase)
        {
            bool confirmedInsideView = phase == ChainlingPartyNavigationPhase.ViewEditing;
            bool confirmedInsideParty = phase == ChainlingPartyNavigationPhase.ColumnEditing;
            bool lockOutsideFrames = confirmedInsideView || confirmedInsideParty;

            if (viewRail != null && viewRail.CollapsedButton != null)
            {
                bool carouselOpen = viewRail.IsCarouselOpen;
                viewRail.CollapsedButton.interactable = !carouselOpen;
                ApplySelectableNavigationLock(viewRail.CollapsedButton, true);
            }

            ApplySectionButtonNavigationLock(lockOutsideFrames);
            ApplyColumnButtonNavigationLock(true);
            ApplyViewRailModeButtonNavigationLock(phase != ChainlingPartyNavigationPhase.ViewEditing || viewRail == null || !viewRail.IsCarouselOpen);

            if (confirmedInsideParty && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        private Transform GetConfirmedInputIsolationRoot()
        {
            if (confirmedInputIsolationRoot == null)
            {
                var isolationRoot = new GameObject("ConfirmedInputIsolation");
                isolationRoot.transform.SetParent(transform, false);
                confirmedInputIsolationRoot = isolationRoot.transform;
            }

            return confirmedInputIsolationRoot;
        }

        private void ApplyColumnButtonNavigationLock(bool locked)
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
            {
                ChainlingPartyColumnShell shell = columnShells[i];
                if (shell?.Button == null)
                    continue;

                ApplySelectableNavigationLock(shell.Button, locked);
            }
        }

        private void ApplyViewRailModeButtonNavigationLock(bool locked)
        {
            if (viewRail == null || !locked)
                return;

            IReadOnlyList<Button> modeButtons = viewRail.ModeButtons;
            for (int i = 0; i < modeButtons.Count; i++)
                ApplySelectableNavigationLock(modeButtons[i], locked);
        }

        private void ApplySectionButtonNavigationLock(bool locked)
        {
            if (!locked || sectionChrome == null)
                return;

            ApplySelectableNavigationLock(sectionChrome.ViewRailSectionButton, true);
            ApplySelectableNavigationLock(sectionChrome.PartySectionButton, true);
        }

        private static void ApplySelectableNavigationLock(Selectable selectable, bool locked)
        {
            if (selectable == null)
                return;

            Navigation navigation = selectable.navigation;
            navigation.mode = locked ? Navigation.Mode.None : Navigation.Mode.Automatic;
            if (locked)
            {
                navigation.selectOnUp = null;
                navigation.selectOnDown = null;
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
            }

            selectable.navigation = navigation;
        }

        private void HandleNavigationSlotFocusRequested(int slotIndex, bool animate)
        {
            if (slotIndex < 0 || slotIndex >= PartySlotCount)
                return;

            selectedSlotIndex = slotIndex;
            RefreshColumnFocus(animate);
        }

        private void HandleNavigationDetailRequested()
        {
            OpenFocusedSlot();
        }

        private bool HandleViewEditingMoveRequested(int delta)
        {
            if (viewRail == null)
                return true;

            if (!viewRail.IsCarouselOpen)
                return true;

            PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
            if (viewRail.MoveCarousel(delta, true))
            {
                ApplyPreviewViewMode(viewRail.ActiveMode);
                PlaySfxCue(PlayerMenuSfxCue.CarouselMove);
            }
            RefreshInputNavigation();
            return true;
        }

        private bool HandleViewEditingSubmitRequested()
        {
            if (viewRail == null)
                return false;

            if (viewRail.IsCarouselOpen)
                return viewRail.ConfirmCarousel(true);

            OpenViewCarousel();
            return true;
        }

        private bool HandleViewEditingCancelRequested()
        {
            if (viewRail != null && viewRail.IsCarouselOpen)
                return viewRail.ConfirmCarousel(true);

            return false;
        }

        private void ArmViewRail()
        {
            viewRailOpen = true;
            if (viewRail != null)
            {
                viewRail.SetArmed(true, true);
                viewRail.SetMode(activeViewMode, false);
            }
        }

        private void OpenViewCarousel()
        {
            viewRailOpen = true;
            if (viewRail != null)
            {
                viewRail.SetMode(activeViewMode, false);
                viewRail.OpenCarousel(true);
            }

            RefreshInputNavigation();
        }

        private void DisarmViewRail()
        {
            viewRailOpen = false;
            viewRail?.SetArmed(false, true);
        }

        private void LogPartyReadModelDebug()
        {
            if (!logPartyReadModelDebug)
                return;

            PlayerRosterEntry entry = GetFocusedEntry();
            if (entry == null)
                return;

            ChainlingPartyColumnContext ctx = BuildColumnContext(entry, ResolveSpecies(entry), selectedSlotIndex);
            Debug.Log(
                "[ChainlingsPartyScreen] " +
                ChainlingPartyReadModelDebug.BuildColumnSummaryText(debugReadModelViewMode, ctx),
                this);
        }

        private void BuildPartySlots()
        {
            for (int i = 0; i < partySlots.Length; i++)
                partySlots[i] = null;

            if (rosterCache == null || rosterCache.chainlings == null)
                return;

            for (int i = 0; i < rosterCache.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = rosterCache.chainlings[i];
                if (entry == null || !entry.isPartyMember)
                    continue;

                int slot = Mathf.Clamp(entry.partySlot, 0, PartySlotCount - 1);
                if (partySlots[slot] == null)
                    partySlots[slot] = entry;
            }
        }

        private void HandleSlotFocused(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= PartySlotCount)
                return;

            if (navigationController != null &&
                navigationController.Phase != ChainlingPartyNavigationPhase.ColumnEditing)
            {
                return;
            }

            selectedSlotIndex = slotIndex;
            navigationController?.SetSelectedSlotIndex(slotIndex, false);
            RefreshColumnFocus(true);
        }

        private void HandleSlotSubmitted(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= PartySlotCount || navigationController == null)
                return;

            ChainlingPartyNavigationPhase phase = navigationController.Phase;
            if (phase != ChainlingPartyNavigationPhase.ColumnEditing &&
                phase != ChainlingPartyNavigationPhase.PartySection)
            {
                return;
            }

            selectedSlotIndex = slotIndex;
            navigationController.SetSelectedSlotIndex(slotIndex, false);

            if (phase == ChainlingPartyNavigationPhase.PartySection)
            {
                navigationController.EnterColumnEditing();
                return;
            }

            OpenFocusedSlot();
        }

        private void RefreshColumnFocus(bool animate, bool bounceAllToNormal = false)
        {
            bool columnEditing = IsColumnEditing;
            for (int i = 0; i < PartySlotCount; i++)
            {
                ChainlingPartyColumnShell shell = GetColumnShell(i);
                if (shell == null)
                    continue;

                bool focused = columnEditing && i == selectedSlotIndex;
                shell.SetFocused(focused);
                motionController?.ApplyColumnOpacity(shell, ResolveColumnOpacity(i), animate);
                shell.ApplyVisualStageVisibility(ResolveVisualStageOpacity(i), animate);
                motionController?.SetColumnFocused(shell, focused, columnEditing, animate, bounceAllToNormal);
            }

            RefreshFooterAndHints();
            RefreshInputNavigation();
        }

        private void OpenFocusedSlot()
        {
            PlayerRosterEntry entry = GetFocusedEntry();
            if (entry == null)
            {
                SetStatus("Assign this slot at a PC.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            OpenDetail(entry);
        }

        private void HandleDetailClosed()
        {
            if (activeInputContext != null)
                ConfigureInputContext(activeInputContext);
        }

        public void CopyOwnedSpeciesIds(ICollection<string> destination)
        {
            if (destination == null)
                return;

            AddOwnedSpeciesIds(destination, rosterCache);
            GameSessionContext session = GameSessionContext.Instance;
            if (session == null)
                return;
            if (PlayerProfileReadStore.ResolveOrCreate().TryGetRoster(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    out PlayerRosterSnapshot stored))
                AddOwnedSpeciesIds(destination, stored);
        }

        public bool TryOpenOwnedSpeciesDossier(string speciesId)
        {
            PlayerRosterEntry entry = FindOwnedEntry(speciesId);
            if (entry == null)
                return false;

            OpenDetail(entry);
            return true;
        }

        static void AddOwnedSpeciesIds(ICollection<string> destination, PlayerRosterSnapshot roster)
        {
            if (roster == null || roster.chainlings == null)
                return;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                string speciesId = roster.chainlings[i]?.snapshot?.speciesId;
                if (!string.IsNullOrWhiteSpace(speciesId))
                    destination.Add(speciesId.Trim());
            }
        }

        PlayerRosterEntry FindOwnedEntry(string speciesId)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
                return null;

            PlayerRosterEntry cached = FindOwnedEntry(rosterCache, speciesId);
            if (cached != null)
                return cached;

            GameSessionContext session = GameSessionContext.Instance;
            if (session == null)
                return null;
            if (!PlayerProfileReadStore.ResolveOrCreate().TryGetRoster(
                    session.PlayerProfileId,
                    session.WalletAddress,
                    out PlayerRosterSnapshot stored))
                return null;

            return FindOwnedEntry(stored, speciesId);
        }

        static PlayerRosterEntry FindOwnedEntry(PlayerRosterSnapshot roster, string speciesId)
        {
            if (roster == null || roster.chainlings == null)
                return null;

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                string id = entry?.snapshot?.speciesId;
                if (string.Equals(id, speciesId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private void OpenDetail(PlayerRosterEntry rosterEntry)
        {
            EnsureBuilt();
            EnsureServiceBehaviours();
            EnsureDetailInstance();
            ConfigureDetailScreen();
            if (detailScreen == null)
            {
                SetStatus("No Chainling detail screen is assigned.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            CombatVisualProfileSO profile = ResolveProfile(rosterEntry);
            PlaySfxCue(PlayerMenuSfxCue.ChainlingSelect, profile != null ? profile.spawnSound : null);
            detailScreen.Closed -= HandleDetailClosed;
            detailScreen.Closed += HandleDetailClosed;
            detailScreen.Open(rosterEntry, rosterCache);
            MenuScreenStack stack = ResolveScreenStack();
            if (stack != null)
            {
                stack.Push(detailScreen);
                if (activeInputContext != null)
                    detailScreen.ConfigureInputContext(activeInputContext);
            }
            else
            {
                detailScreen.Show();
                if (activeInputContext != null)
                    detailScreen.ConfigureInputContext(activeInputContext);
            }
        }

        private void RefreshFooterAndHints()
        {
            int filled = CountFilledSlots();
            if (footerHints != null)
            {
                footerHints.SetPartyTitle(filled, PartySlotCount);
                footerHints.SetLayer(ResolveFooterLayer(), GetFocusedEntry() != null);
            }

            RefreshDebugHarnessControls();
        }

        private ChainlingPartyFooterHints.FocusLayer ResolveFooterLayer()
        {
            ChainlingPartyNavigationPhase phase = navigationController != null
                ? navigationController.Phase
                : ChainlingPartyNavigationPhase.ViewSection;

            switch (phase)
            {
                case ChainlingPartyNavigationPhase.ViewEditing:
                    return ChainlingPartyFooterHints.FocusLayer.ViewEditing;
                case ChainlingPartyNavigationPhase.PartySection:
                    return ChainlingPartyFooterHints.FocusLayer.PartySection;
                case ChainlingPartyNavigationPhase.ColumnEditing:
                    return ChainlingPartyFooterHints.FocusLayer.ColumnEditing;
                default:
                    return ChainlingPartyFooterHints.FocusLayer.ViewSection;
            }
        }

        private int CountFilledSlots()
        {
            int count = 0;
            for (int i = 0; i < partySlots.Length; i++)
            {
                if (partySlots[i] != null)
                    count++;
            }

            return count;
        }

        private void RefreshDebugHarnessControls()
        {
            IChainlingsMenuTestHarness harness = ResolveTestHarness();
            bool active = harness != null;
            if (debugHarnessRoot != null)
                debugHarnessRoot.gameObject.SetActive(active);
            if (regenerateDebugRosterButton != null)
            {
                regenerateDebugRosterButton.interactable = active && !isLoading;
                ApplySelectableNavigationLock(regenerateDebugRosterButton, true);
            }

            if (randomizeDebugLineupButton != null)
            {
                randomizeDebugLineupButton.interactable = active && !isLoading && rosterCache != null;
                ApplySelectableNavigationLock(randomizeDebugLineupButton, true);
            }
        }

        private void RefreshInputNavigation()
        {
            if (activeInputContext == null || !IsVisible)
                return;

            EnsurePartyNavigation();
            activeInputContext.SetContentInputHandler(navigationController);

            ChainlingPartyNavigationPhase phase = navigationController != null
                ? navigationController.Phase
                : ChainlingPartyNavigationPhase.ViewSection;

            if (phase == ChainlingPartyNavigationPhase.ViewEditing && viewRail != null)
            {
                if (viewRail.IsCarouselOpen)
                {
                    var modeRow = new List<Selectable>();
                    IReadOnlyList<Button> modeButtons = viewRail.ModeButtons;
                    for (int i = 0; i < modeButtons.Count; i++)
                        AddNavigationSelectable(modeRow, modeButtons[i]);

                    if (modeRow.Count > 0)
                        MenuSelectableNavigation.ConfigureGrid(modeRow, 1);

                    Selectable defaultMode = viewRail.GetModeButton(viewRail.FocusedMode);
                    activeInputContext.SetContentNavigation(modeRow, defaultMode, transform);
                    ApplyOutsideFrameInputLock(phase);
                    return;
                }

                var armedRow = new List<Selectable>();
                AddNavigationSelectable(armedRow, viewRail.CollapsedButton);
                activeInputContext.SetContentNavigation(armedRow, viewRail.CollapsedButton, transform);
                ApplyOutsideFrameInputLock(phase);
                return;
            }

            if (phase == ChainlingPartyNavigationPhase.ColumnEditing)
            {
                activeInputContext.SetContentNavigation(
                    null,
                    null,
                    GetConfirmedInputIsolationRoot(),
                    restoreSelectionWhenLost: false);
                ApplyOutsideFrameInputLock(phase);
                return;
            }

            var sectionSelectables = new List<Selectable>();
            Selectable viewSelectable = sectionChrome != null ? sectionChrome.ViewRailSectionButton : null;
            Selectable partySelectable = sectionChrome != null ? sectionChrome.PartySectionButton : null;
            AddNavigationSelectable(sectionSelectables, viewSelectable);
            AddNavigationSelectable(sectionSelectables, partySelectable);
            ApplySectionNavigation(viewSelectable, partySelectable);

            Selectable defaultSection = phase == ChainlingPartyNavigationPhase.ViewSection && MenuSelectableNavigation.IsUsable(viewSelectable)
                ? viewSelectable
                : partySelectable;
            activeInputContext.SetContentNavigation(sectionSelectables, defaultSection, transform);
            ApplyOutsideFrameInputLock(phase);
        }

        private static void ApplySectionNavigation(Selectable viewSelectable, Selectable partySelectable)
        {
            if (viewSelectable != null)
            {
                Navigation viewNav = viewSelectable.navigation;
                viewNav.mode = Navigation.Mode.Explicit;
                viewNav.selectOnRight = partySelectable;
                viewNav.selectOnLeft = null;
                viewNav.selectOnUp = null;
                viewNav.selectOnDown = null;
                viewSelectable.navigation = viewNav;
            }

            if (partySelectable != null)
            {
                Navigation partyNav = partySelectable.navigation;
                partyNav.mode = Navigation.Mode.Explicit;
                partyNav.selectOnLeft = viewSelectable;
                partyNav.selectOnRight = null;
                partyNav.selectOnUp = null;
                partyNav.selectOnDown = null;
                partySelectable.navigation = partyNav;
            }
        }

        private Selectable ResolveDefaultSelectable(IReadOnlyList<Selectable> columns)
        {
            int defaultSlot = ResolveDefaultSlotIndex();
            if (columns != null && defaultSlot >= 0 && defaultSlot < columns.Count && MenuSelectableNavigation.IsUsable(columns[defaultSlot]))
                return columns[defaultSlot];

            if (columns != null)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    if (MenuSelectableNavigation.IsUsable(columns[i]))
                        return columns[i];
                }
            }

            return viewRail != null && MenuSelectableNavigation.IsUsable(viewRail.CollapsedButton)
                ? viewRail.CollapsedButton
                : null;
        }

        private static void AddNavigationSelectable(List<Selectable> order, Selectable selectable)
        {
            if (!MenuSelectableNavigation.IsUsable(selectable) || order.Contains(selectable))
                return;

            order.Add(selectable);
        }

        private int ResolveDefaultSlotIndex()
        {
            if (selectedSlotIndex >= 0 && selectedSlotIndex < PartySlotCount)
                return selectedSlotIndex;

            return ResolveInitialSlotIndex();
        }

        private int ResolveInitialSlotIndex()
        {
            for (int i = 0; i < partySlots.Length; i++)
            {
                if (partySlots[i] != null)
                    return i;
            }

            return Mathf.Clamp(selectedSlotIndex, 0, PartySlotCount - 1);
        }

        private PlayerRosterEntry GetFocusedEntry()
        {
            if (selectedSlotIndex < 0 || selectedSlotIndex >= partySlots.Length)
                return null;

            return partySlots[selectedSlotIndex];
        }

        private bool HasAnyPartySlot()
        {
            for (int i = 0; i < partySlots.Length; i++)
            {
                if (partySlots[i] != null)
                    return true;
            }

            return false;
        }

        private ChainlingPartyColumnShell GetColumnShell(int index)
        {
            if (columnShells == null || index < 0 || index >= columnShells.Length)
                return null;

            return columnShells[index];
        }

        private void SetLoading(bool loading, string status)
        {
            isLoading = loading;
            SetStatus(status);
            RefreshDebugHarnessControls();
        }

        private void SetStatus(string status)
        {
            if (statusLabel != null)
                statusLabel.text = status ?? "";
        }

        private SpeciesDefinitionSO ResolveSpecies(PlayerRosterEntry rosterEntry)
        {
            if (speciesDatabase == null || rosterEntry == null || rosterEntry.snapshot == null)
                return null;

            return speciesDatabase.GetById(rosterEntry.snapshot.speciesId);
        }

        private CombatVisualProfileSO ResolveProfile(PlayerRosterEntry rosterEntry)
        {
            if (visualDatabase == null || rosterEntry == null || rosterEntry.snapshot == null)
                return null;

            ChainlingVisualIdentity identity = ChainlingVisualIdentity.CloneOrDefault(rosterEntry.snapshot.visualIdentity);
            ChainlingType entangledType = rosterEntry.snapshot.isEntangled
                ? rosterEntry.snapshot.entangledType
                : ChainlingType.None;
            return visualDatabase.GetProfile(
                rosterEntry.snapshot.speciesId,
                identity.visualVariantId,
                entangledType);
        }

        private ResolvedChainlingVisualIdentity ResolveVisualIdentity(PlayerRosterEntry rosterEntry)
        {
            if (rosterEntry == null || rosterEntry.snapshot == null)
                return null;

            ChainlingType entangledType = rosterEntry.snapshot.isEntangled
                ? rosterEntry.snapshot.entangledType
                : ChainlingType.None;
            return ChainlingVisualIdentityResolver.Resolve(
                rosterEntry.snapshot.speciesId,
                rosterEntry.snapshot.visualIdentity,
                visualDatabase,
                identityDatabase,
                entangledType);
        }

        private IPlayerRosterProvider ResolveRosterProvider()
        {
            if (dataSourceMode == ChainlingsDataSourceMode.Production)
            {
                if (rosterProviderBehaviour is SupabasePlayerRosterProvider assignedProductionProvider)
                    return assignedProductionProvider;

                SupabasePlayerRosterProvider productionProvider =
                    FindObjectOfType<SupabasePlayerRosterProvider>(true);
                if (productionProvider != null)
                    rosterProviderBehaviour = productionProvider;
                return productionProvider;
            }

            if (rosterProviderBehaviour is IPlayerRosterProvider assigned &&
                ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, rosterProviderBehaviour))
            {
                return assigned;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerRosterProvider service &&
                    ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, behaviours[i]))
                {
                    rosterProviderBehaviour = behaviours[i];
                    return service;
                }
            }

            return null;
        }

        private void PlaySfxCue(PlayerMenuSfxCue cue, UnityEngine.Object overrideSound = null)
        {
            IPlayerMenuSfxPlayer sfxPlayer = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            sfxPlayer?.PlayCue(cue, overrideSound);
        }

        private IChainlingsMenuTestHarness ResolveTestHarness()
        {
            if (dataSourceMode != ChainlingsDataSourceMode.Harness)
                return null;

            if (rosterProviderBehaviour is IChainlingsMenuTestHarness rosterHarness)
                return rosterHarness;
            if (partyLineupServiceBehaviour is IChainlingsMenuTestHarness lineupHarness)
                return lineupHarness;
            if (moveInventoryProviderBehaviour is IChainlingsMenuTestHarness moveHarness)
                return moveHarness;

            MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is IChainlingsMenuTestHarness harness)
                    return harness;
            }

            return null;
        }

        private MenuScreenStack ResolveScreenStack()
        {
            if (screenStack != null)
                return screenStack;

            screenStack = FindObjectOfType<MenuScreenStack>(true);
            return screenStack;
        }

        private void EnsureDetailInstance()
        {
            if (detailScreen != null || detailViewPrefab == null || !Application.isPlaying)
                return;

            MenuScreenStack stack = ResolveScreenStack();
            Transform parent = stack != null ? stack.transform : transform.parent != null ? transform.parent : transform;
            detailScreen = Instantiate(detailViewPrefab, parent);
            detailScreen.name = "ChainlingDetailView";
            if (detailScreen.transform is RectTransform rect)
                PlayerMenuUiFactory.Stretch(rect);
            detailScreen.Hide();
        }

        private void ConfigureDetailScreen()
        {
            if (detailScreen == null)
                return;

            detailScreen.SetDataSourceMode(dataSourceMode);

            detailScreen.Configure(
                supabaseConfig,
                speciesDatabase,
                visualDatabase,
                identityDatabase,
                moveDatabase,
                progressionConfig,
                cosmeticInventoryServiceBehaviour,
                moveInventoryProviderBehaviour,
                moveEquipServiceBehaviour,
                visualIdentityServiceBehaviour,
                rebirthServiceBehaviour);
        }

        private void EnsureServiceBehaviours()
        {
            rosterProviderBehaviour =
                ResolveOrAddService<IPlayerRosterProvider, SupabasePlayerRosterProvider>(rosterProviderBehaviour);
            partyProviderBehaviour =
                ResolveOrAddService<IPlayerPartyProvider, SupabasePlayerLineupPartyProvider>(partyProviderBehaviour);
            partyLineupServiceBehaviour =
                ResolveOrAddService<IMenuPartyLineupService, SupabaseMenuPartyLineupService>(partyLineupServiceBehaviour);
            moveInventoryProviderBehaviour =
                ResolveOrAddService<IMoveInventoryProvider, SupabaseMoveInventoryProvider>(moveInventoryProviderBehaviour);
            moveEquipServiceBehaviour =
                ResolveOrAddService<IMenuMoveEquipService, SupabaseMenuMoveEquipService>(moveEquipServiceBehaviour);
            visualIdentityServiceBehaviour =
                ResolveOrAddService<IMenuVisualIdentityService, SupabaseMenuVisualIdentityService>(visualIdentityServiceBehaviour);
            rebirthServiceBehaviour =
                ResolveOrAddService<IChainlingRebirthService, SupabaseRebirthService>(rebirthServiceBehaviour);
            cosmeticInventoryServiceBehaviour = ResolveCosmeticInventoryService(cosmeticInventoryServiceBehaviour);

            if (supabaseConfig == null)
                return;

            if (rosterProviderBehaviour is SupabasePlayerRosterProvider rosterProvider)
                rosterProvider.Configure(supabaseConfig);
            if (partyProviderBehaviour is SupabasePlayerLineupPartyProvider partyProvider)
                partyProvider.Configure(supabaseConfig);
            if (moveInventoryProviderBehaviour is SupabaseMoveInventoryProvider moveInventoryProvider)
                moveInventoryProvider.Configure(supabaseConfig);
            if (moveEquipServiceBehaviour is SupabaseMenuMoveEquipService moveEquipService)
                moveEquipService.Configure(supabaseConfig);
            if (partyLineupServiceBehaviour is SupabaseMenuPartyLineupService partyLineupService)
                partyLineupService.Configure(supabaseConfig);
            if (visualIdentityServiceBehaviour is SupabaseMenuVisualIdentityService visualIdentityService)
                visualIdentityService.Configure(supabaseConfig);
            if (rebirthServiceBehaviour is SupabaseRebirthService rebirthService)
                rebirthService.Configure(supabaseConfig);
            if (cosmeticInventoryServiceBehaviour is CoreCosmeticInventoryService coreCosmeticInventoryService)
                coreCosmeticInventoryService.Configure(supabaseConfig);
        }

        private MonoBehaviour ResolveCosmeticInventoryService(MonoBehaviour current)
        {
            if (dataSourceMode == ChainlingsDataSourceMode.Production)
            {
                if (current is CoreCosmeticInventoryService)
                    return current;

                CoreCosmeticInventoryService local = GetComponent<CoreCosmeticInventoryService>();
                if (local != null)
                    return local;

                CoreCosmeticInventoryService existing = FindObjectOfType<CoreCosmeticInventoryService>(true);
                if (existing != null)
                    return existing;

                return gameObject.AddComponent<CoreCosmeticInventoryService>();
            }

            if (current is ICosmeticInventoryService &&
                ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, current))
            {
                return current;
            }

            MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is ICosmeticInventoryService &&
                    ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, localBehaviours[i]))
                {
                    return localBehaviours[i];
                }
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICosmeticInventoryService &&
                    ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, behaviours[i]))
                {
                    return behaviours[i];
                }
            }

            return null;
        }

        private MonoBehaviour ResolveOrAddService<TService, TFallback>(MonoBehaviour current)
            where TFallback : MonoBehaviour, TService
        {
            if (dataSourceMode == ChainlingsDataSourceMode.Production)
            {
                if (current is TFallback)
                    return current;

                TFallback localFallback = GetComponent<TFallback>();
                if (localFallback != null)
                    return localFallback;

                TFallback existingFallback = FindObjectOfType<TFallback>(true);
                if (existingFallback != null)
                    return existingFallback;

                return gameObject.AddComponent<TFallback>();
            }

            if (current is TService &&
                ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, current))
            {
                return current;
            }

            MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < localBehaviours.Length; i++)
            {
                if (localBehaviours[i] is TService &&
                    ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, localBehaviours[i]))
                {
                    return localBehaviours[i];
                }
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is TService &&
                    ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, behaviours[i]))
                {
                    return behaviours[i];
                }
            }

            return null;
        }

        private void ClearIncompatibleServiceBindings()
        {
            rosterProviderBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, rosterProviderBehaviour);
            partyProviderBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, partyProviderBehaviour);
            partyLineupServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, partyLineupServiceBehaviour);
            cosmeticInventoryServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, cosmeticInventoryServiceBehaviour);
            moveInventoryProviderBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, moveInventoryProviderBehaviour);
            moveEquipServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, moveEquipServiceBehaviour);
            visualIdentityServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, visualIdentityServiceBehaviour);
            rebirthServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(dataSourceMode, rebirthServiceBehaviour);
        }

        private void EnsureBuilt(bool createDetailFallback = true)
        {
            ResolveAuthoredReferencesFromHierarchy();
            if (HasEmbeddedLayout)
            {
                EnsureAuthoredComponentsReady();
                return;
            }

            RemoveGeneratedChildren();
            RemoveRootLayoutComponents();
            PlayerMenuUiFactory.Stretch((RectTransform)transform, 0f, 0f, 0f, 0f);
            PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(gameObject);

            safeContent = PlayerMenuUiFactory.CreateRect("SafeContent", transform);
            PlayerMenuUiFactory.Stretch(safeContent, 48f, 72f, 48f, 48f);

            motionController = PlayerMenuUiFactory.GetOrAdd<ChainlingPartyMotionController>(gameObject);
            motionController.Configure(motionProfile);

            BuildStatusLine();
            BuildDebugHarness();
            BuildViewRail();
            BuildColumns();
            BuildSectionChrome();
            BuildFooter();

            if (createDetailFallback)
                EnsureDetailScreen();

            EnsureButtonListeners();
            EnsureAuthoredComponentsReady();
        }

        private void BuildStatusLine()
        {
            statusLabel = PlayerMenuUiFactory.CreateText("StatusLine", safeContent, "", 22f, TextAlignmentOptions.TopLeft, true);
            statusLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.Top(statusLabel.rectTransform, 32f, 200f, 0f, 0f);
        }

        private void BuildDebugHarness()
        {
            debugHarnessRoot = PlayerMenuUiFactory.CreateRect("SandboxHarnessBar", safeContent);
            PlayerMenuUiFactory.Top(debugHarnessRoot, 48f, 200f, 0f, 36f);
            PlayerMenuUiFactory.HorizontalLayout(debugHarnessRoot.gameObject, 8f);
            regenerateDebugRosterButton = PlayerMenuUiFactory.CreateButton("RegenerateSandboxRosterButton", debugHarnessRoot, "Regenerate", PlayerMenuUiFactory.Panel);
            randomizeDebugLineupButton = PlayerMenuUiFactory.CreateButton("RandomizeSandboxLineupButton", debugHarnessRoot, "Randomize Party", PlayerMenuUiFactory.Panel);
            ApplySelectableNavigationLock(regenerateDebugRosterButton, true);
            ApplySelectableNavigationLock(randomizeDebugLineupButton, true);
            PlayerMenuUiFactory.LayoutElement(regenerateDebugRosterButton.gameObject, preferredWidth: 150f, preferredHeight: 44f);
            PlayerMenuUiFactory.LayoutElement(randomizeDebugLineupButton.gameObject, preferredWidth: 190f, preferredHeight: 44f);
            debugHarnessRoot.gameObject.SetActive(false);
        }

        private void BuildViewRail()
        {
            RectTransform railRoot = PlayerMenuUiFactory.CreateRect("ViewRail", safeContent);
            railRoot.anchorMin = new Vector2(0f, 0.5f);
            railRoot.anchorMax = new Vector2(0f, 0.5f);
            railRoot.pivot = new Vector2(0f, 0.5f);
            railRoot.anchoredPosition = new Vector2(8f, 40f);
            railRoot.sizeDelta = new Vector2(190f, 420f);

            viewRail = railRoot.gameObject.AddComponent<ChainlingPartyViewRail>();
            viewRail.BuildDefaultLayoutForEditor();
        }

        private void BuildColumns()
        {
            columnsRoot = PlayerMenuUiFactory.CreateRect("ColumnsRoot", safeContent);
            PlayerMenuUiFactory.Stretch(columnsRoot, 210f, 72f, 0f, 96f);
            HorizontalLayoutGroup layout = PlayerMenuUiFactory.HorizontalLayout(columnsRoot.gameObject, 14f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            if (columnShells == null || columnShells.Length != PartySlotCount)
                columnShells = new ChainlingPartyColumnShell[PartySlotCount];

            for (int i = 0; i < PartySlotCount; i++)
                columnShells[i] = CreateColumnShell(i, columnsRoot);
        }

        private ChainlingPartyColumnShell CreateColumnShell(int index, RectTransform parent)
        {
            ChainlingPartyColumnShell shell;
            if (columnShellPrefab != null)
            {
                shell = Instantiate(columnShellPrefab, parent);
                shell.name = SlotColumnObjectName(index);
            }
            else
            {
                var go = new GameObject(
                    SlotColumnObjectName(index),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button),
                    typeof(CanvasGroup),
                    typeof(ChainlingPartyColumnShell));
                go.transform.SetParent(parent, false);
                shell = go.GetComponent<ChainlingPartyColumnShell>();
            }

            PlayerMenuUiFactory.LayoutElement(shell.gameObject, preferredWidth: 360f, preferredHeight: 620f, flexibleWidth: 1f);
            shell.BuildDefaultLayoutForEditor();
            shell.ConfigureTypeChromePalette(typeChromePalette);
            shell.ConfigureTypeIconPalette(typeIconPalette);
            ConfigureColumnTypeIconPresentation(shell);
            shell.ConfigureStarRatingArt(starRatingArt);
            shell.ConfigureCycleTypography(cycleTypography);
            shell.ConfigureArchiveIndicatorArt(archiveIndicatorArt);
            return shell;
        }

        private void BuildSectionChrome()
        {
            sectionChrome = safeContent.gameObject.AddComponent<ChainlingPartySectionChrome>();
            sectionChrome.Configure(viewRail != null ? viewRail.transform as RectTransform : null, columnsRoot);
            sectionChrome.BuildDefaultLayoutForEditor();
        }

        private void BuildFooter()
        {
            RectTransform footerRoot = PlayerMenuUiFactory.CreateRect("FooterHints", safeContent);
            PlayerMenuUiFactory.Bottom(footerRoot, 56f, 0f, 0f, 8f);
            footerHints = footerRoot.gameObject.AddComponent<ChainlingPartyFooterHints>();
            footerHints.BuildDefaultLayoutForEditor();
        }

        private void EnsureAuthoredComponentsReady()
        {
            if (motionController == null)
                motionController = GetComponent<ChainlingPartyMotionController>();
            if (motionController != null)
                motionController.Configure(motionProfile);

            CacheColumnShellsFromRoot();
            ConfigureColumnStarRatingArt();
            ConfigureColumnCycleTypography();
            ConfigureColumnArchiveIndicatorArt();
            ConfigureColumnTypeChromePalette();
            ConfigureColumnTypeIconPalette();
            ConfigureColumnTypeIconPresentation();
            EnsurePartyNavigation();
            EnsureSectionChrome();

            if (viewRail != null)
            {
                viewRail.Configure(
                    motionProfile,
                    HandleViewModeSelected,
                    HandleViewRailOpenRequested,
                    HandleViewRailCloseRequested);
                if (viewRail.CollapsedButton == null)
                    viewRail.BuildDefaultLayoutForEditor();
                else
                    viewRail.RefreshRuntimeState();
            }

            AttachFocusVisuals();
            RefreshFooterAndHints();
        }

        private void ResolveAuthoredReferencesFromHierarchy()
        {
            if (safeContent == null && transform.Find("SafeContent") is RectTransform safeRect)
                safeContent = safeRect;

            if (viewRail == null)
                viewRail = GetComponentInChildren<ChainlingPartyViewRail>(true);

            if (columnsRoot == null)
            {
                Transform columns = safeContent != null ? safeContent.Find("ColumnsRoot") : transform.Find("SafeContent/ColumnsRoot");
                if (columns is RectTransform columnsRect)
                    columnsRoot = columnsRect;
            }

            if (footerHints == null)
                footerHints = GetComponentInChildren<ChainlingPartyFooterHints>(true);

            if (sectionChrome == null && safeContent != null)
                sectionChrome = safeContent.GetComponent<ChainlingPartySectionChrome>();

            CacheColumnShellsFromRoot();
        }

        private void EnsurePartyNavigation()
        {
            if (navigationController == null)
                navigationController = GetComponent<ChainlingPartyNavigationController>();
            if (navigationController == null)
                navigationController = gameObject.AddComponent<ChainlingPartyNavigationController>();

            navigationController.Configure(PartySlotCount);
            navigationController.SetSelectedSlotIndex(Mathf.Clamp(selectedSlotIndex, 0, PartySlotCount - 1), false);
            BindNavigationControllerEvents();
        }

        private void BindNavigationControllerEvents()
        {
            if (navigationController == null || navigationControllerEventsBound)
                return;

            navigationController.PhaseChanged += HandleNavigationPhaseChanged;
            navigationController.SlotFocusRequested += HandleNavigationSlotFocusRequested;
            navigationController.DetailRequested += HandleNavigationDetailRequested;
            navigationController.ViewEditingMoveRequested = HandleViewEditingMoveRequested;
            navigationController.ViewEditingSubmitRequested = HandleViewEditingSubmitRequested;
            navigationController.ViewEditingCancelRequested = HandleViewEditingCancelRequested;
            navigationControllerEventsBound = true;
        }

        private void UnbindNavigationControllerEvents()
        {
            if (!navigationControllerEventsBound || navigationController == null)
                return;

            navigationController.PhaseChanged -= HandleNavigationPhaseChanged;
            navigationController.SlotFocusRequested -= HandleNavigationSlotFocusRequested;
            navigationController.DetailRequested -= HandleNavigationDetailRequested;
            navigationController.ViewEditingMoveRequested = null;
            navigationController.ViewEditingSubmitRequested = null;
            navigationController.ViewEditingCancelRequested = null;
            navigationControllerEventsBound = false;
        }

        private void EnsureSectionChrome()
        {
            if (safeContent == null)
                return;

            if (sectionChrome == null)
                sectionChrome = safeContent.GetComponent<ChainlingPartySectionChrome>();
            if (sectionChrome == null)
                sectionChrome = safeContent.gameObject.AddComponent<ChainlingPartySectionChrome>();

            sectionChrome.Configure(viewRail != null ? viewRail.transform as RectTransform : null, columnsRoot);
            if (sectionChrome.UsesAuthoredFrameLayout)
                sectionChrome.PrepareAuthoredRuntime();
            else if (sectionChrome.HasAuthoringFrames)
                sectionChrome.RefreshFrameRects(force: true);
            else
                sectionChrome.BuildDefaultLayoutForEditor();
            if (navigationController != null)
                sectionChrome.SetActivePhase(navigationController.Phase);
            EnsureSectionChromeButtonListeners();
        }

        private void ConfigureColumnStarRatingArt()
        {
            if (columnShells == null || starRatingArt == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                columnShells[i]?.ConfigureStarRatingArt(starRatingArt);
        }

        private void ConfigureColumnCycleTypography()
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                columnShells[i]?.ConfigureCycleTypography(cycleTypography);
        }

        private void ConfigureColumnArchiveIndicatorArt()
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                columnShells[i]?.ConfigureArchiveIndicatorArt(archiveIndicatorArt);
        }

        private void ConfigureColumnTypeChromePalette()
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                columnShells[i]?.ConfigureTypeChromePalette(typeChromePalette);
        }

        private void ConfigureColumnTypeIconPalette()
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                columnShells[i]?.ConfigureTypeIconPalette(typeIconPalette);
        }

        private void ConfigureColumnTypeIconPresentation()
        {
            if (columnShells == null)
                return;

            for (int i = 0; i < columnShells.Length; i++)
                ConfigureColumnTypeIconPresentation(columnShells[i]);
        }

        private void ConfigureColumnTypeIconPresentation(ChainlingPartyColumnShell shell)
        {
            if (shell == null)
                return;

            shell.ConfigureTypeIconPresentation(
                typeIconOverrideAuthoredViewport,
                typeIconOverrideAuthoredIconRects,
                typeIconAlpha,
                typeIconViewportAnchorMin,
                typeIconViewportAnchorMax,
                typeIconViewportAnchoredPosition,
                typeIconViewportSizeDelta,
                typeIconSize,
                typeIconScrollDirection,
                typeIconScrollDistance,
                typeIconScrollPixelsPerSecond);
        }

        private void CacheColumnShellsFromRoot()
        {
            if (columnShells == null || columnShells.Length != PartySlotCount)
                columnShells = new ChainlingPartyColumnShell[PartySlotCount];

            int existing = 0;
            for (int i = 0; i < columnShells.Length; i++)
            {
                if (columnShells[i] != null)
                    existing++;
            }

            if (existing >= PartySlotCount)
                return;

            ChainlingPartyColumnShell[] found = GetComponentsInChildren<ChainlingPartyColumnShell>(true);
            for (int i = 0; i < found.Length && i < PartySlotCount; i++)
            {
                if (columnShells[i] == null)
                    columnShells[i] = found[i];
            }
        }

        private bool HasAuthoredColumnShells()
        {
            if (columnShells == null || columnShells.Length < PartySlotCount)
                return false;

            for (int i = 0; i < PartySlotCount; i++)
            {
                if (columnShells[i] == null)
                    return false;
            }

            return true;
        }

        private void AttachFocusVisuals()
        {
            AttachFocus(regenerateDebugRosterButton);
            AttachFocus(randomizeDebugLineupButton);

            DisableColumnSelectableFocusVisuals();
        }

        private void DisableColumnSelectableFocusVisuals()
        {
            for (int i = 0; i < PartySlotCount; i++)
            {
                ChainlingPartyColumnShell shell = GetColumnShell(i);
                MenuSelectableFocusVisual focusVisual = shell != null && shell.Button != null
                    ? shell.Button.GetComponent<MenuSelectableFocusVisual>()
                    : null;
                if (focusVisual != null)
                    focusVisual.enabled = false;
            }
        }

        private static void AttachFocus(Selectable selectable)
        {
            if (selectable != null)
                MenuSelectableFocusVisual.EnsureOn(selectable);
        }

        private void EnsureDetailScreen()
        {
            if (detailScreen != null)
                return;

            Transform parent = transform.parent != null ? transform.parent : transform;
            var go = new GameObject("ChainlingDetailScreen", typeof(RectTransform), typeof(CanvasGroup), typeof(ChainlingDetailScreen));
            go.transform.SetParent(parent, false);
            PlayerMenuUiFactory.Stretch(go.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            detailScreen = go.GetComponent<ChainlingDetailScreen>();
            ConfigureDetailScreen();
            detailScreen.Hide();
        }

        private void RemoveGeneratedChildren()
        {
            buttonListenersBound = false;

            if (detailScreen != null && detailScreen.transform.IsChildOf(transform))
                detailScreen = null;

            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyUnityObject(transform.GetChild(i).gameObject);

            safeContent = null;
            viewRail = null;
            columnsRoot = null;
            sectionChrome = null;
            footerHints = null;
            statusLabel = null;
            debugHarnessRoot = null;
            regenerateDebugRosterButton = null;
            randomizeDebugLineupButton = null;
            motionController = null;
            sectionChromeListenersBound = false;
            columnShells = new ChainlingPartyColumnShell[PartySlotCount];
        }

        private void RemoveRootLayoutComponents()
        {
            RemoveComponent<VerticalLayoutGroup>();
            RemoveComponent<HorizontalLayoutGroup>();
            RemoveComponent<GridLayoutGroup>();
            RemoveComponent<ContentSizeFitter>();
            RemoveComponent<ScrollRect>();
            RemoveComponent<Mask>();
        }

        private void RemoveComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null)
                DestroyUnityObject(component);
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        public static string SlotLabel(int slotIndex)
        {
            return (Mathf.Clamp(slotIndex, 0, PartySlotCount - 1) + 1).ToString();
        }

        public static string SlotColumnObjectName(int slotIndex)
        {
            return $"Column_{SlotLabel(slotIndex)}";
        }

        public static bool IsBenchedPartySlot(int slotIndex)
        {
            return slotIndex == 2;
        }
    }
}
