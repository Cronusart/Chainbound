using System;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Root controller for the gameplay/player menu. Designers wire screens,
    /// buttons, and layout in the prefab; this component owns open/close,
    /// tab selection, input locks, and feature gates.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMenuController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private GameObject hubRoot;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private MenuScreenStack screenStack;
        [SerializeField] private MenuInputContextController inputContext;
        [SerializeField] private PlayerMenuOverviewPanel overviewPanel;
        [SerializeField] private PlayerMenuBackgroundController backgroundController;
        [SerializeField] private PlayerMenuBootSequenceController bootSequenceController;
        [SerializeField] private PlayerMenuPanelTransitionController panelTransitionController;

        [Header("Tabs")]
        [SerializeField] private MenuTabBar tabBar;
        [SerializeField] private MenuTabDefinitionSO[] tabDefinitions;
        [Tooltip("Primary tab body wiring. Each entry should reference an embedded screen under ScreenStackHost.")]
        [SerializeField] private MenuScreen[] tabScreens;
        [SerializeField] private string defaultTabId = "chainlings";
        [SerializeField] private int defaultTabIndex = 0;

        [Header("Chainlings Tab")]
        [Tooltip("Catalog used to wire Chainlings detail/slot/column prefab references at runtime.")]
        [SerializeField] private ChainlingsMenuViewsSO chainlingsMenuViews;

        [Header("Services")]
        [SerializeField] private GameplayMenuAvailabilityService availabilityService;
        [Tooltip("Optional MonoBehaviour implementing IFeatureUnlockService.")]
        [SerializeField] private MonoBehaviour featureUnlockServiceBehaviour;

        [Header("Pause / Input")]
        [SerializeField] private bool acquireInputLock = true;
        [SerializeField] private InputLockChannel inputLockChannel = InputLockChannel.All;
        [SerializeField] private bool pauseTimeScale = false;
        [SerializeField] private string inputLockReason = "PlayerMenu";

        [Header("Audio")]
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        [Header("Panel Transitions")]
        [SerializeField] private bool glitchPanelsOnMenuOpen = true;
        [SerializeField] private bool glitchPanelsOnMenuClose = true;

        [Header("Debug")]
        [SerializeField] private bool bypassAvailabilityGate = false;
        [SerializeField] private bool enableDebugLogs = false;

        private InputLockHandle inputLockHandle;
        private bool hasInputLock;
        private float previousTimeScale = 1f;
        private int currentTabIndex = -1;
        private int focusedTabIndex = -1;
        private bool isContentMode;
        private IFeatureUnlockService featureUnlockService;
        private bool tabScreensInstalled;
        private bool openingSequencePlaying;
        private bool closingSequencePlaying;
        private bool panelTransitionPlaying;
        private bool openingPanelTransitionStarted;
        private bool openingPanelTransitionCompleted;
        private float panelTransitionStartedAt = -1f;
        private MonoBehaviour cachedSfxPlayerBehaviour;

        private const float PanelTransitionWatchdogSeconds = 2f;

        public bool IsOpen { get; private set; }
        public int CurrentTabIndex => currentTabIndex;
        public int FocusedTabIndex => focusedTabIndex;
        public bool IsContentMode => isContentMode;

        public MenuScreenStack GetScreenStack() => screenStack;

        public ChainlingsMenuViewsSO ChainlingsMenuViews => chainlingsMenuViews;

        public PlayerMenuBackgroundController EnsureRootBackgroundForEditor()
        {
            ResolveReferences();
            RemoveLegacyRootDimmers();
            RemoveContentBackdrop();
            RemoveMenuContainerBackdrop();
            RemoveTabRootBackdrops();
            PlayerMenuBackgroundController background = EnsureRootBackground();
            EnsureRootBootSequenceForEditor();
            EnsureRootPanelTransitionForEditor();
            return background;
        }

        public PlayerMenuBootSequenceController EnsureRootBootSequenceForEditor()
        {
            ResolveReferences();
            PlayerMenuBackgroundController background = EnsureRootBackground();
            Transform container = ResolveMenuContainer();
            return EnsureRootBootSequence(background, container as RectTransform);
        }

        public PlayerMenuPanelTransitionController EnsureRootPanelTransitionForEditor()
        {
            ResolveReferences();
            return EnsureRootPanelTransition();
        }

        public ChainlingsPartyScreen GetChainlingsPartyScreen()
        {
            int tabIndex = ResolveTabIndex("chainlings");
            return tabIndex >= 0 ? GetTabScreen(tabIndex) as ChainlingsPartyScreen : null;
        }

        public InventoryScreen GetInventoryScreen()
        {
            int tabIndex = ResolveTabIndex("inventory");
            return tabIndex >= 0 ? GetTabScreen(tabIndex) as InventoryScreen : null;
        }

        public void WarmChainlingPresentation()
        {
            ChainlingsPartyScreen partyScreen = GetChainlingsPartyScreen();
            partyScreen?.WarmPresentation();

            InventoryScreen inventoryScreen = GetInventoryScreen();
            inventoryScreen?.WarmPresentation();

            ProgressionConfigSO progression = BalanceConfigRuntime.Progression;
            if (progression != null && progression.starXpThresholds != null)
                _ = progression.starXpThresholds.Length;
        }

        public bool TryGetTabDefinition(int tabIndex, out MenuTabDefinitionSO definition)
        {
            definition = null;
            if (tabDefinitions == null || tabIndex < 0 || tabIndex >= tabDefinitions.Length)
                return false;

            definition = tabDefinitions[tabIndex];
            return definition != null;
        }

        public void EnsureTabScreensInstalled()
        {
            if (tabScreensInstalled)
                return;

            PlayerMenuTabScreenInstaller.InstallAll(this, enableDebugLogs);
            tabScreensInstalled = true;
        }

        public MenuTabDefinitionSO[] GetTabDefinitions() => tabDefinitions;

        public int GetTabScreenCount() => tabScreens != null ? tabScreens.Length : 0;

        public MenuScreen GetTabScreen(int index)
        {
            if (tabScreens == null || index < 0 || index >= tabScreens.Length)
                return null;

            return tabScreens[index];
        }

        public void SetTabScreen(int index, MenuScreen screen)
        {
            if (tabScreens == null || index < 0 || index >= tabScreens.Length)
                return;

            tabScreens[index] = screen;
        }

        public void SetBypassAvailabilityGate(bool bypass)
        {
            bypassAvailabilityGate = bypass;
        }

        public void ResolveShareCaptureChrome(
            out GameObject closeButton,
            out GameObject footerHints,
            out GameObject hub,
            out GameObject background)
        {
            ResolveReferences();
            Transform container = ResolveMenuContainer();
            closeButton = FindChildObject(container, "CloseButton");
            footerHints = FindChildObject(container, "FooterHints");
            hub = hubRoot;
            background = backgroundController != null ? backgroundController.gameObject : null;
        }

        public void SetShareCaptureChromeVisible(bool visible)
        {
            ResolveShareCaptureChrome(
                out GameObject closeButton,
                out GameObject footerHints,
                out GameObject hub,
                out GameObject background);

            if (closeButton != null)
                closeButton.SetActive(visible);
            if (footerHints != null)
                footerHints.SetActive(visible);
            if (hub != null)
                hub.SetActive(visible);
            if (background != null)
                background.SetActive(visible);
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRootBackground();
            RemoveLegacyRootDimmers();
            RemoveContentBackdrop();
            RemoveMenuContainerBackdrop();
            RemoveTabRootBackdrops();
            EnsureRuntimeHubLayout();
            EnsureTabScreensInstalled();
            EnsureRootBootSequence();
            EnsureRootPanelTransition();
            ApplyRootVisible(false);
        }

        private void OnEnable()
        {
            if (tabBar != null)
            {
                tabBar.TabFocused += PreviewTab;
                tabBar.TabSubmitted += SelectTab;
            }

            if (inputContext != null)
            {
                inputContext.OnCancel.AddListener(HandleCancel);
                inputContext.OnCloseMenu.AddListener(Close);
            }

            OnScreenKeyboardService.SessionOpened += HandleOnScreenKeyboardOpened;
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (panelTransitionPlaying)
            {
                bool controllerIdle = panelTransitionController == null || !panelTransitionController.IsPlaying;
                float elapsed = Time.unscaledTime - panelTransitionStartedAt;
                if ((controllerIdle && elapsed > 0.05f) || elapsed >= PanelTransitionWatchdogSeconds)
                    RecoverPanelTransitionHold();
            }

            if (closingSequencePlaying && panelTransitionStartedAt > 0f &&
                Time.unscaledTime - panelTransitionStartedAt >= PanelTransitionWatchdogSeconds)
            {
                CloseImmediate();
            }
        }

        private void OnDisable()
        {
            OnScreenKeyboardService.SessionOpened -= HandleOnScreenKeyboardOpened;

            if (tabBar != null)
            {
                tabBar.TabFocused -= PreviewTab;
                tabBar.TabSubmitted -= SelectTab;
            }

            if (inputContext != null)
            {
                inputContext.OnCancel.RemoveListener(HandleCancel);
                inputContext.OnCloseMenu.RemoveListener(Close);
            }

            if (IsOpen || openingSequencePlaying || closingSequencePlaying)
                CloseImmediate();
        }

        private void HandleOnScreenKeyboardOpened()
        {
            if (!IsOpen && !openingSequencePlaying && !closingSequencePlaying)
                return;

            if (enableDebugLogs)
                Debug.Log("[PlayerMenuController] Closing menu because on-screen keyboard opened.", this);

            CloseImmediate();
        }

        public void Open()
        {
            OpenAtTab(ResolveDefaultTabIndex());
        }

        public void OpenToTab(string tabId)
        {
            if (!ColosseumMenuVisibility.IsMenuTabAllowed(tabId))
            {
                Open();
                return;
            }

            int index = ResolveTabIndex(tabId);
            OpenAtTab(index >= 0 ? index : ResolveDefaultTabIndex());
        }

        public void OpenToTab(int tabIndex)
        {
            OpenAtTab(IsValidDefinition(tabIndex) ? tabIndex : ResolveDefaultTabIndex());
        }

        public int ResolveTabIndex(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId) || tabDefinitions == null)
                return -1;

            string normalized = tabId.Trim();
            for (int i = 0; i < tabDefinitions.Length; i++)
            {
                MenuTabDefinitionSO definition = tabDefinitions[i];
                if (definition != null && string.Equals(definition.TabId, normalized, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private void OpenAtTab(int tabIndex)
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            ResolveReferences();
            EnsureRuntimeHubLayout();

            if (closingSequencePlaying)
                return;

            int targetTab = IsValidDefinition(tabIndex) ? tabIndex : ResolveDefaultTabIndex();

            if (IsOpen)
            {
                EnterHubMode(targetTab);
                return;
            }

            OnScreenKeyboardService keyboard = OnScreenKeyboardService.Instance;
            if (keyboard != null && keyboard.IsOpen)
            {
                if (enableDebugLogs)
                    Debug.Log("[PlayerMenuController] Open ignored: on-screen keyboard is open.", this);
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            if (!bypassAvailabilityGate)
            {
                GameplayMenuAvailabilityService gate = availabilityService != null
                    ? availabilityService
                    : GameplayMenuAvailabilityService.Instance;

                if (gate != null && !gate.CanOpen)
                {
                    if (enableDebugLogs)
                        Debug.Log("[PlayerMenuController] Open ignored: gameplay menu is unavailable.", this);
                    PlaySfxCue(PlayerMenuSfxCue.Invalid);
                    return;
                }
            }

            IsOpen = true;
            PlaySfxCue(PlayerMenuSfxCue.MenuOpen);
            PlayerMenuSfxResolver.SuppressNavigationCues(0.35f);
            ApplyRootVisible(true);
            SetRootInteraction(false);
            AcquirePauseAndInput();
            ConfigureTabs();
            featureUnlockService = ResolveFeatureUnlockService();
            ApplyFeatureLocks(featureUnlockService != null && featureUnlockService.IsHandbookUnlocked);
            _ = RefreshFeatureLocksAsync();
            RunWithoutNavigationSfx(() => EnterHubMode(targetTab));
            PlayOpenBootSequence();
        }

        public int ResolveDefaultTabIndex()
        {
            int tabIdIndex = ResolveTabIndex(defaultTabId);
            if (IsValidDefinition(tabIdIndex))
                return tabIdIndex;

            return IsValidDefinition(defaultTabIndex) ? defaultTabIndex : FirstDefinedTabIndex();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (closingSequencePlaying) return;

            if (openingSequencePlaying)
            {
                CloseImmediate();
                return;
            }

            SetRootInteraction(false);
            closingSequencePlaying = true;
            inputContext?.SetAnimationHold(true);
            panelTransitionStartedAt = Time.unscaledTime;
            panelTransitionController?.Cancel();
            PlayerMenuSfxResolver.SuppressWholeMenuCloseCues(0.35f);
            PlaySfxCue(PlayerMenuSfxCue.MenuClose);

            Transform activePanel = ResolveActivePanelRoot();
            if (glitchPanelsOnMenuClose && ShouldUsePanelTransition(activePanel, null))
            {
                BeginPanelTransition();
                panelTransitionController.PlayOut(activePanel, PlayCloseBootSequence);
                return;
            }

            PlayCloseBootSequence();
        }

        private void CloseImmediate()
        {
            if (!IsOpen && !openingSequencePlaying && !closingSequencePlaying) return;

            IsOpen = false;
            openingSequencePlaying = false;
            closingSequencePlaying = false;
            panelTransitionPlaying = false;
            openingPanelTransitionStarted = false;
            openingPanelTransitionCompleted = false;
            panelTransitionStartedAt = -1f;
            inputContext?.SetAnimationHold(false);
            MenuInputIntentGate.Clear();
            bootSequenceController?.Cancel();
            panelTransitionController?.Cancel();
            currentTabIndex = -1;
            focusedTabIndex = -1;
            isContentMode = false;

            if (screenStack != null)
                screenStack.Clear();

            ReleasePauseAndInput();
            ApplyRootVisible(false);
            bootSequenceController?.RestoreRestState();
        }

        public void Toggle()
        {
            if (closingSequencePlaying) return;
            if (IsOpen) Close();
            else Open();
        }

        public void SelectTab(int index)
        {
            if (openingSequencePlaying || closingSequencePlaying) return;
            if (panelTransitionPlaying) return;
            OpenTabContent(index);
        }

        public void PreviewTab(int index)
        {
            if (closingSequencePlaying) return;
            if (panelTransitionPlaying) return;
            if (!IsOpen) return;
            if (!IsValidDefinition(index)) return;

            ApplyHubFocus(index, playSfx: true);
        }

        public void HandleCancel()
        {
            if (!IsOpen) return;
            if (closingSequencePlaying) return;

            if (panelTransitionPlaying)
                RecoverPanelTransitionHold();

            if (openingSequencePlaying)
            {
                Close();
                return;
            }

            if (isContentMode)
            {
                MenuScreen current = screenStack != null ? screenStack.Current : null;
                if (current != null && current.TryHandleCancel())
                {
                    PlaySfxCue(PlayerMenuSfxCue.Cancel);
                    return;
                }

                if (current != null && current.CloseWithBack && screenStack.Count > 1)
                {
                    MenuScreen next = screenStack.PeekBelow();
                    screenStack.Pop();
                    PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
                    PlaySfxCue(PlayerMenuSfxCue.Cancel);
                    ApplyContentNavigation(next != null ? next : screenStack.Current);
                    return;
                }

                PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
                PlaySfxCue(PlayerMenuSfxCue.Cancel);
                ReturnToHubMode(currentTabIndex >= 0 ? currentTabIndex : focusedTabIndex);
                return;
            }

            Close();
        }

        private async System.Threading.Tasks.Task RefreshFeatureLocksAsync()
        {
            bool handbookUnlocked = false;

            featureUnlockService = ResolveFeatureUnlockService();
            if (featureUnlockService != null)
            {
                try
                {
                    handbookUnlocked = await featureUnlockService.RefreshHandbookUnlockedAsync(
                        ResolvePlayerProfileId(),
                        ResolveWalletAddress());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerMenuController] Failed to refresh feature locks: {ex.Message}", this);
                    handbookUnlocked = featureUnlockService.IsHandbookUnlocked;
                }
            }

            ApplyFeatureLocks(handbookUnlocked);
        }

        private void OpenTabContent(int index)
        {
            if (openingSequencePlaying || closingSequencePlaying) return;
            if (panelTransitionPlaying) return;
            if (!IsOpen) return;
            if (!IsValidTab(index)) return;

            if (tabBar != null && tabBar.IsLocked(index))
            {
                PreviewTab(index);
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            PrepareTabContentState(index);
            MenuScreen screen = tabScreens[index];

            if (ShouldUsePanelTransition(hubRoot != null ? hubRoot.transform : null, screen != null ? screen.transform : null))
            {
                BeginPanelTransition();
                panelTransitionController.PlaySwap(
                    hubRoot.transform,
                    () => ShowTabContentImmediate(index, applyNavigation: false),
                    screen.transform,
                    () =>
                    {
                        CompletePanelTransition();
                    });
                PlaySfxCue(PlayerMenuSfxCue.PanelSwitch);
                return;
            }

            ShowTabContentImmediate(index, applyNavigation: true);
            PlaySfxCue(PlayerMenuSfxCue.PanelSwitch);
        }

        private void PrepareTabContentState(int index)
        {
            isContentMode = true;
            currentTabIndex = index;
            focusedTabIndex = index;
            tabBar?.SetSelectedIndex(index);
            overviewPanel?.SetFocusedTab(tabDefinitions[index], false);
        }

        private void ShowTabContentImmediate(int index, bool applyNavigation)
        {
            if (!IsValidTab(index))
                return;

            SetHubVisible(false);

            MenuScreen screen = tabScreens[index];
            if (screen is ChainlingsPartyScreen partyScreen)
                partyScreen.NotifyTabOpened();
            else if (screen is InventoryScreen inventoryScreen)
                inventoryScreen.NotifyTabOpened();

            screenStack?.ShowRoot(screen, animate: false);
            screen?.Refresh();

            if (applyNavigation)
                ApplyContentNavigation(screen);
        }

        private void ApplyContentNavigation(MenuScreen screen)
        {
            if (inputContext == null)
                return;

            if (screen != null)
                screen.ConfigureInputContext(inputContext);
            else
                inputContext.RestoreInspectorNavigation();
        }

        private void EnterHubMode(
            int focusIndex,
            bool cancelScreenTransitions = true,
            bool resetPanelCanvasState = true)
        {
            if (!IsOpen) return;

            isContentMode = false;
            currentTabIndex = -1;

            if (screenStack != null)
            {
                if (cancelScreenTransitions)
                    screenStack.Clear();
                else
                    screenStack.ClearWithoutCancellingTransition();
            }

            SetHubVisible(true, resetPanelCanvasState);

            int resolvedIndex = ResolveFocusIndex(focusIndex);
            if (resolvedIndex >= 0)
                ApplyHubFocus(resolvedIndex, playSfx: false);

            overviewPanel?.RefreshOverview();

            inputContext?.RestoreInspectorNavigation();
            SelectFocusedButton();
        }

        private void ReturnToHubMode(int focusIndex)
        {
            Transform outgoing = screenStack != null && screenStack.Current != null
                ? screenStack.Current.transform
                : contentRoot != null
                    ? contentRoot.transform
                    : null;

            if (ShouldUsePanelTransition(outgoing, hubRoot != null ? hubRoot.transform : null))
            {
                BeginPanelTransition();
                RunWithoutNavigationSfx(() =>
                {
                    panelTransitionController.PlaySwap(
                        outgoing,
                        () => EnterHubMode(
                            focusIndex,
                            cancelScreenTransitions: false,
                            resetPanelCanvasState: false),
                        hubRoot.transform,
                        CompletePanelTransition);
                });
                return;
            }

            RunWithoutNavigationSfx(() => EnterHubMode(focusIndex));
        }

        private void ConfigureTabs()
        {
            if (tabBar != null)
            {
                tabBar.SetTabs(ColosseumMenuVisibility.ForMenuBar(tabDefinitions));
                tabBar.SetSelectedIndex(-1);
            }
        }

        private void ApplyFeatureLocks(bool handbookUnlocked)
        {
            if (tabDefinitions == null || tabBar == null) return;

            for (int i = 0; i < tabDefinitions.Length; i++)
            {
                MenuTabDefinitionSO definition = tabDefinitions[i];
                bool locked = definition != null &&
                    (!ColosseumMenuVisibility.IsMenuTabAllowed(definition.TabId) ||
                     (definition.LockRule == PlayerMenuTabLock.HandbookUnlock && !handbookUnlocked));
                tabBar.SetLocked(i, locked);
            }

            if (!IsOpen)
                return;

            if (isContentMode && IsValidTab(currentTabIndex) && tabBar.IsLocked(currentTabIndex))
            {
                EnterHubMode(FirstAvailableTabIndex());
                return;
            }

            if (IsValidDefinition(focusedTabIndex))
                RunWithoutNavigationSfx(() => PreviewTab(focusedTabIndex));
        }

        private int FirstAvailableTabIndex()
        {
            if (tabScreens == null) return -1;

            for (int i = 0; i < tabScreens.Length; i++)
            {
                if (IsValidTab(i) && (tabBar == null || !tabBar.IsLocked(i)))
                    return i;
            }

            return -1;
        }

        private int FirstDefinedTabIndex()
        {
            if (tabDefinitions == null) return -1;

            for (int i = 0; i < tabDefinitions.Length; i++)
            {
                if (IsValidDefinition(i))
                    return i;
            }

            return -1;
        }

        private int ResolveFocusIndex(int requestedIndex)
        {
            if (IsValidDefinition(requestedIndex))
                return requestedIndex;

            int tabIdIndex = ResolveTabIndex(defaultTabId);
            if (IsValidDefinition(tabIdIndex))
                return tabIdIndex;

            if (IsValidDefinition(defaultTabIndex))
                return defaultTabIndex;

            return FirstDefinedTabIndex();
        }

        private bool IsValidTab(int index)
        {
            return tabScreens != null &&
                index >= 0 &&
                index < tabScreens.Length &&
                tabScreens[index] != null;
        }

        private bool IsValidDefinition(int index)
        {
            return tabDefinitions != null &&
                index >= 0 &&
                index < tabDefinitions.Length &&
                tabDefinitions[index] != null;
        }

        private void SelectFocusedButton()
        {
            if (tabBar == null || inputContext == null || focusedTabIndex < 0)
                return;

            Button button = tabBar.GetButton(focusedTabIndex);
            if (button != null)
                inputContext.Select(button);
        }

        private void SetHubVisible(bool visible, bool resetPanelCanvasState = true)
        {
            if (hubRoot != null)
            {
                hubRoot.SetActive(visible);
                if (resetPanelCanvasState)
                    SetPanelCanvasState(hubRoot, visible);
            }

            if (contentRoot != null)
            {
                contentRoot.SetActive(!visible);
                if (resetPanelCanvasState)
                    SetPanelCanvasState(contentRoot, !visible);
            }
        }

        private void ApplyHubFocus(int index, bool playSfx)
        {
            if (!IsValidDefinition(index))
                return;

            focusedTabIndex = index;
            tabBar?.SetSelectedIndex(index);
            overviewPanel?.SetFocusedTab(tabDefinitions[index], tabBar != null && tabBar.IsLocked(index));

            if (playSfx)
                PlaySfxCue(PlayerMenuSfxCue.PanelSwitch);
        }

        private static void SetPanelCanvasState(GameObject root, bool visible)
        {
            if (root == null)
                return;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void ApplyRootVisible(bool visible)
        {
            if (menuRoot == null)
                menuRoot = gameObject;

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = visible ? 1f : 0f;
                rootCanvasGroup.interactable = visible;
                rootCanvasGroup.blocksRaycasts = visible;
            }

            menuRoot.SetActive(visible);
        }

        private void SetRootInteraction(bool interactive)
        {
            if (rootCanvasGroup == null)
                return;

            rootCanvasGroup.interactable = interactive;
            rootCanvasGroup.blocksRaycasts = interactive;
        }

        private bool ShouldUsePanelTransition(Transform outgoingRoot, Transform incomingRoot)
        {
            return Application.isPlaying &&
                panelTransitionController != null &&
                panelTransitionController.isActiveAndEnabled &&
                !panelTransitionController.IsPlaying &&
                (outgoingRoot != null || incomingRoot != null);
        }

        private Transform ResolveActivePanelRoot()
        {
            if (isContentMode && screenStack != null && screenStack.Current != null)
                return screenStack.Current.transform;

            if (!isContentMode && hubRoot != null && hubRoot.activeInHierarchy)
                return hubRoot.transform;

            if (contentRoot != null && contentRoot.activeInHierarchy)
                return contentRoot.transform;

            return hubRoot != null ? hubRoot.transform : null;
        }

        private void BeginPanelTransition()
        {
            panelTransitionPlaying = true;
            panelTransitionStartedAt = Time.unscaledTime;
            PlayerMenuSfxResolver.SuppressNavigationCues(0.35f);
            SetRootInteraction(false);
            inputContext?.SetAnimationHold(true);
        }

        private void CompletePanelTransition()
        {
            panelTransitionPlaying = false;
            panelTransitionStartedAt = -1f;
            PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
            inputContext?.SetAnimationHold(openingSequencePlaying || closingSequencePlaying);
            MenuInputIntentGate.Clear();
            if (IsOpen && !openingSequencePlaying && !closingSequencePlaying)
            {
                SetRootInteraction(true);
                if (isContentMode)
                    ApplyContentNavigation(screenStack != null ? screenStack.Current : null);
                else
                    SelectFocusedButton();
            }
        }

        private void RecoverPanelTransitionHold()
        {
            panelTransitionController?.Cancel();
            CompletePanelTransition();
        }

        private void AcquirePauseAndInput()
        {
            if (pauseTimeScale)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (!acquireInputLock || hasInputLock) return;

            InputLockService service = InputLockService.Instance;
            if (service == null) return;

            inputLockHandle = service.AcquireLock(inputLockReason, inputLockChannel, this);
            hasInputLock = inputLockHandle.IsValid;
        }

        private void ReleasePauseAndInput()
        {
            if (hasInputLock)
            {
                inputLockHandle.Release();
                hasInputLock = false;
            }

            if (pauseTimeScale)
                Time.timeScale = previousTimeScale;
        }

        private IFeatureUnlockService ResolveFeatureUnlockService()
        {
            if (featureUnlockService != null)
                return featureUnlockService;

            if (featureUnlockServiceBehaviour != null)
                return featureUnlockServiceBehaviour as IFeatureUnlockService;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IFeatureUnlockService service)
                    return service;
            }

            return null;
        }

        private static string ResolvePlayerProfileId()
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null ? session.PlayerProfileId : "";
        }

        private static string ResolveWalletAddress()
        {
            GameSessionContext session = GameSessionContext.Instance;
            return session != null ? session.WalletAddress : "";
        }

        private void ResolveReferences()
        {
            if (menuRoot == null)
                menuRoot = gameObject;
            if (rootCanvasGroup == null)
                rootCanvasGroup = GetComponent<CanvasGroup>();
            if (backgroundController == null && menuRoot != null)
                backgroundController = menuRoot.GetComponentInChildren<PlayerMenuBackgroundController>(true);
            if (bootSequenceController == null && menuRoot != null)
                bootSequenceController = menuRoot.GetComponentInChildren<PlayerMenuBootSequenceController>(true);
            if (panelTransitionController == null && menuRoot != null)
                panelTransitionController = menuRoot.GetComponentInChildren<PlayerMenuPanelTransitionController>(true);
            if (screenStack == null)
                screenStack = GetComponentInChildren<MenuScreenStack>(true);
            if (inputContext == null)
                inputContext = GetComponent<MenuInputContextController>();
            if (overviewPanel == null)
                overviewPanel = GetComponentInChildren<PlayerMenuOverviewPanel>(true);
            if (tabBar == null)
                tabBar = GetComponentInChildren<MenuTabBar>(true);
            if (contentRoot == null && screenStack != null)
                contentRoot = screenStack.gameObject;
            if (availabilityService == null)
                availabilityService = FindObjectOfType<GameplayMenuAvailabilityService>();
        }

        private void PlaySfxCue(PlayerMenuSfxCue cue)
        {
            IPlayerMenuSfxPlayer sfxPlayer = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            sfxPlayer?.PlayCue(cue);
        }

        private static void RunWithoutNavigationSfx(Action action)
        {
            PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
            action?.Invoke();
            PlayerMenuSfxResolver.SuppressNavigationCues(0.05f);
        }

        private void EnsureRuntimeHubLayout()
        {
            EnsureRootBackground();
            RemoveLegacyRootDimmers();
            RemoveContentBackdrop();
            RemoveMenuContainerBackdrop();
            RemoveTabRootBackdrops();

            if (hubRoot != null && overviewPanel != null && contentRoot != null)
            {
                EnsureRootBootSequence();
                return;
            }

            if (tabBar == null || screenStack == null)
                return;

            Transform container = ResolveMenuContainer();
            if (container == null)
                return;

            DisableLegacyChild(container, "HeaderBar");
            DisableLegacyChild(container, "FooterHints");
            RemoveMenuContainerBackdrop(container);
            RemoveTabRootBackdrops();

            if (hubRoot == null)
            {
                hubRoot = CreateRect("HubRoot", container);
                Stretch((RectTransform)hubRoot.transform);
            }

            Image leftPanel = CreateImage("LeftTabPanel", hubRoot.transform, new Color(0.12f, 0.16f, 0.2f, 0.98f));
            FixedTopLeft(leftPanel.rectTransform, 560f, 520f, 34f, 34f);

            TMP_Text title = CreateText("Title", leftPanel.transform, "PLAYER MENU", 32f, TextAlignmentOptions.MidlineLeft);
            Top(title.rectTransform, 58f, 26f, 26f, 18f);

            Transform tabTransform = tabBar.transform;
            tabTransform.SetParent(leftPanel.transform, false);
            Stretch((RectTransform)tabTransform, 24f, 24f, 24f, 92f);

            HorizontalLayoutGroup oldHorizontal = tabBar.GetComponent<HorizontalLayoutGroup>();
            if (oldHorizontal != null)
                Destroy(oldHorizontal);

            VerticalLayoutGroup vertical = tabBar.GetComponent<VerticalLayoutGroup>();
            if (vertical == null)
                vertical = tabBar.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 12f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            for (int i = 0; tabDefinitions != null && i < tabDefinitions.Length; i++)
            {
                Button button = tabBar.GetButton(i);
                if (button == null) continue;
                LayoutElement element = button.GetComponent<LayoutElement>();
                if (element == null)
                    element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 70f;
                element.flexibleWidth = 1f;
            }

            Image rightPanel = CreateImage("RightInfoPanel", hubRoot.transform, new Color(0.11f, 0.15f, 0.18f, 0.98f));
            FixedTopRight(rightPanel.rectTransform, 760f, 420f, 34f, 76f);
            TMP_Text areaLabel = CreateText("AreaLabel", rightPanel.transform, "UNKNOWN AREA", 42f, TextAlignmentOptions.Top);
            Top(areaLabel.rectTransform, 92f, 34f, 34f, 46f);
            TMP_Text dateTimeLabel = CreateText("DateTimeLabel", rightPanel.transform, "00/00/00\n--:--", 34f, TextAlignmentOptions.Top);
            Top(dateTimeLabel.rectTransform, 128f, 34f, 34f, 146f);
            TMP_Text segmentLabel = CreateText("SegmentLabel", rightPanel.transform, "UNKNOWN", 28f, TextAlignmentOptions.Top);
            Top(segmentLabel.rectTransform, 52f, 34f, 34f, 284f);
            segmentLabel.color = new Color(0.7f, 0.82f, 0.9f, 1f);
            TMP_Text ramLabel = CreateText("RamLabel", rightPanel.transform, "RAM ----", 34f, TextAlignmentOptions.Bottom);
            Bottom(ramLabel.rectTransform, 62f, 34f, 34f, 30f);

            Image bottomPanel = CreateImage("BottomDescriptionPanel", hubRoot.transform, new Color(0.11f, 0.15f, 0.18f, 0.98f));
            FixedBottomCenter(bottomPanel.rectTransform, 1500f, 300f, 40f);
            TMP_Text tabTitleLabel = CreateText("TabTitleLabel", bottomPanel.transform, "Chainlings", 36f, TextAlignmentOptions.TopLeft);
            Top(tabTitleLabel.rectTransform, 58f, 34f, 34f, 30f);
            TMP_Text tabDescriptionLabel = CreateText("TabDescriptionLabel", bottomPanel.transform, "", 30f, TextAlignmentOptions.TopLeft);
            Stretch(tabDescriptionLabel.rectTransform, 34f, 34f, 34f, 96f);
            tabDescriptionLabel.color = new Color(0.86f, 0.91f, 0.98f, 1f);

            Image centerPanel = CreateImage("CenterFillerPanel", hubRoot.transform, new Color(0.075f, 0.105f, 0.14f, 0.6f));
            Stretch(centerPanel.rectTransform, 650f, 390f, 850f, 108f);
            TMP_Text centerLabel = CreateText("CenterStatus", centerPanel.transform, "SIGNAL JAM\nMODULE SELECT", 32f, TextAlignmentOptions.Center);
            Stretch(centerLabel.rectTransform, 24f, 24f, 24f, 24f);
            centerLabel.color = new Color(0.62f, 0.75f, 0.84f, 1f);

            overviewPanel = hubRoot.GetComponent<PlayerMenuOverviewPanel>();
            if (overviewPanel == null)
                overviewPanel = hubRoot.AddComponent<PlayerMenuOverviewPanel>();
            overviewPanel.Configure(null);
            WireOverviewRuntime(overviewPanel, areaLabel, dateTimeLabel, segmentLabel, ramLabel, tabTitleLabel, tabDescriptionLabel);

            contentRoot = screenStack.gameObject;
            contentRoot.transform.SetParent(container, false);
            Stretch((RectTransform)contentRoot.transform, 36f, 36f, 36f, 36f);
            RemoveContentBackdrop();
            contentRoot.SetActive(false);

            Button closeButton = CreateButton("CloseButton", container, new Color(0.18f, 0.23f, 0.31f, 1f));
            FixedTopRight((RectTransform)closeButton.transform, 76f, 76f, 24f, 24f);
            TMP_Text closeLabel = CreateText("Label", closeButton.transform, "X", 34f, TextAlignmentOptions.Center);
            Stretch(closeLabel.rectTransform);
            closeButton.onClick.AddListener(Close);

            TMP_Text footer = CreateText("FooterHints", container, "UP/DOWN Tabs   A Open   B Back", 24f, TextAlignmentOptions.BottomLeft);
            Bottom(footer.rectTransform, 42f, 38f, 120f, 2f);
            footer.color = new Color(0.68f, 0.76f, 0.86f, 1f);

            EnsureRootBootSequence();
        }

        private Transform ResolveMenuContainer()
        {
            if (hubRoot != null)
                return hubRoot.transform.parent;

            Transform current = tabBar != null ? tabBar.transform : transform;
            while (current != null)
            {
                if (current.name == "MenuContainer")
                    return current;
                current = current.parent;
            }

            return transform;
        }

        private static GameObject FindChildObject(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            Transform child = parent.Find(childName);
            return child != null ? child.gameObject : null;
        }

        private static void DisableLegacyChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        private PlayerMenuBackgroundController EnsureRootBackground()
        {
            if (menuRoot == null)
                menuRoot = gameObject;

            if (backgroundController == null && menuRoot != null)
                backgroundController = menuRoot.GetComponentInChildren<PlayerMenuBackgroundController>(true);

            if (backgroundController == null && menuRoot != null)
            {
                GameObject backgroundRoot = CreateRect("PlayerMenuBackground", menuRoot.transform);
                backgroundController = backgroundRoot.AddComponent<PlayerMenuBackgroundController>();
            }

            if (backgroundController != null)
            {
                backgroundController.BuildDefaultLayoutForEditor();
                backgroundController.transform.SetAsFirstSibling();
            }

            return backgroundController;
        }

        private PlayerMenuBootSequenceController EnsureRootBootSequence()
        {
            PlayerMenuBackgroundController background = EnsureRootBackground();
            Transform container = ResolveMenuContainer();
            return EnsureRootBootSequence(background, container as RectTransform);
        }

        private PlayerMenuPanelTransitionController EnsureRootPanelTransition()
        {
            if (menuRoot == null)
                menuRoot = gameObject;

            if (panelTransitionController == null && menuRoot != null)
                panelTransitionController = menuRoot.GetComponentInChildren<PlayerMenuPanelTransitionController>(true);

            if (panelTransitionController == null && menuRoot != null)
            {
                GameObject transitionRoot = CreateRect("PlayerMenuPanelTransitions", menuRoot.transform);
                panelTransitionController = transitionRoot.AddComponent<PlayerMenuPanelTransitionController>();
            }

            if (screenStack != null)
                screenStack.ConfigureTransitionController(panelTransitionController);

            return panelTransitionController;
        }

        private PlayerMenuBootSequenceController EnsureRootBootSequence(
            PlayerMenuBackgroundController background,
            RectTransform menuPanel)
        {
            if (menuRoot == null)
                menuRoot = gameObject;

            if (bootSequenceController == null && menuRoot != null)
                bootSequenceController = menuRoot.GetComponentInChildren<PlayerMenuBootSequenceController>(true);

            if (bootSequenceController == null && menuRoot != null)
            {
                GameObject bootRoot = CreateRect("PlayerMenuBootSequence", menuRoot.transform);
                bootSequenceController = bootRoot.AddComponent<PlayerMenuBootSequenceController>();
            }

            if (bootSequenceController == null)
                return null;

            if (menuPanel != null && menuPanel.GetComponent<CanvasGroup>() == null)
                menuPanel.gameObject.AddComponent<CanvasGroup>();

            bootSequenceController.ConfigureTargets(
                background != null ? background.transform as RectTransform : null,
                menuPanel);
            bootSequenceController.BuildDefaultLayoutForEditor();
            bootSequenceController.transform.SetAsLastSibling();
            return bootSequenceController;
        }

        private void PlayOpenBootSequence()
        {
            PlayerMenuBootSequenceController boot = EnsureRootBootSequence();
            if (boot == null)
            {
                CompleteOpenBootSequence();
                return;
            }

            openingSequencePlaying = true;
            openingPanelTransitionStarted = false;
            openingPanelTransitionCompleted = false;
            inputContext?.SetAnimationHold(true);
            boot.PlayOpenSequence(CompleteOpenBootSequence, BeginOpenPanelTransition);

            if (!boot.IsPlaying && !panelTransitionPlaying)
            {
                openingSequencePlaying = false;
                openingPanelTransitionStarted = false;
                openingPanelTransitionCompleted = false;
                inputContext?.SetAnimationHold(false);
            }
        }

        private void CompleteOpenBootSequence()
        {
            if (!IsOpen)
                return;

            openingSequencePlaying = false;

            if (!openingPanelTransitionStarted && !openingPanelTransitionCompleted)
            {
                BeginOpenPanelTransition();
                if (openingPanelTransitionStarted)
                    return;
            }

            if (!panelTransitionPlaying)
            {
                inputContext?.SetAnimationHold(false);
                SetRootInteraction(true);
                SelectFocusedButton();
            }
        }

        private void BeginOpenPanelTransition()
        {
            if (!IsOpen || openingPanelTransitionStarted || openingPanelTransitionCompleted)
                return;

            Transform activePanel = ResolveActivePanelRoot();
            if (glitchPanelsOnMenuOpen && ShouldUsePanelTransition(null, activePanel))
            {
                openingPanelTransitionStarted = true;
                BeginPanelTransition();
                panelTransitionController.PlayIn(
                    activePanel,
                    () =>
                    {
                        openingPanelTransitionStarted = false;
                        openingPanelTransitionCompleted = true;
                        CompletePanelTransition();
                    });
            }
        }

        private void PlayCloseBootSequence()
        {
            PlayerMenuBootSequenceController boot = EnsureRootBootSequence();
            if (boot == null)
            {
                CompleteCloseBootSequence();
                return;
            }

            boot.PlayCloseSequence(CompleteCloseBootSequence);

            if (!boot.IsPlaying)
                closingSequencePlaying = false;
        }

        private void CompleteCloseBootSequence()
        {
            if (!IsOpen && !closingSequencePlaying)
                return;

            IsOpen = false;
            openingSequencePlaying = false;
            closingSequencePlaying = false;
            panelTransitionPlaying = false;
            openingPanelTransitionStarted = false;
            openingPanelTransitionCompleted = false;
            panelTransitionStartedAt = -1f;
            inputContext?.SetAnimationHold(false);
            MenuInputIntentGate.Clear();
            currentTabIndex = -1;
            focusedTabIndex = -1;
            isContentMode = false;

            if (screenStack != null)
                screenStack.Clear();

            ReleasePauseAndInput();
            ApplyRootVisible(false);
            bootSequenceController?.RestoreRestState();
        }

        private void RemoveLegacyRootDimmers()
        {
            Transform root = menuRoot != null ? menuRoot.transform : transform;
            Transform dimOverlay = root.Find("SafeAreaPanel/DimOverlay") ?? root.Find("DimOverlay");
            if (dimOverlay != null)
                DestroyUnityObject(dimOverlay.gameObject);
        }

        private void RemoveContentBackdrop()
        {
            if (contentRoot == null && screenStack != null)
                contentRoot = screenStack.gameObject;

            if (contentRoot == null)
                return;

            Image contentBackdrop = contentRoot.GetComponent<Image>();
            if (contentBackdrop != null)
                DestroyUnityObject(contentBackdrop);

            CanvasRenderer renderer = contentRoot.GetComponent<CanvasRenderer>();
            if (renderer != null)
                DestroyUnityObject(renderer);
        }

        private void RemoveMenuContainerBackdrop()
        {
            Transform container = ResolveMenuContainer();
            RemoveMenuContainerBackdrop(container);
        }

        private void RemoveMenuContainerBackdrop(Transform container)
        {
            if (container == null || container.name != "MenuContainer")
                return;

            Image containerBackdrop = container.GetComponent<Image>();
            if (containerBackdrop != null)
                DestroyUnityObject(containerBackdrop);

            CanvasRenderer renderer = container.GetComponent<CanvasRenderer>();
            if (renderer != null)
                DestroyUnityObject(renderer);
        }

        private void RemoveTabRootBackdrops()
        {
            Transform host = screenStack != null
                ? screenStack.transform
                : contentRoot != null
                    ? contentRoot.transform
                    : null;

            if (host == null)
                return;

            for (int i = 0; i < host.childCount; i++)
            {
                Transform child = host.GetChild(i);
                if (!child.name.StartsWith("Tab_", StringComparison.Ordinal))
                    continue;

                Image tabBackdrop = child.GetComponent<Image>();
                if (tabBackdrop != null)
                    DestroyUnityObject(tabBackdrop);

                CanvasRenderer renderer = child.GetComponent<CanvasRenderer>();
                if (renderer != null)
                    DestroyUnityObject(renderer);
            }
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

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = new Color(0.92f, 0.96f, 1f, 1f);
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void WireOverviewRuntime(
            PlayerMenuOverviewPanel overview,
            TMP_Text areaLabel,
            TMP_Text dateTimeLabel,
            TMP_Text segmentLabel,
            TMP_Text ramLabel,
            TMP_Text tabTitleLabel,
            TMP_Text tabDescriptionLabel)
        {
            if (overview == null) return;

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            SetPrivateField(overview, flags, "areaLabel", areaLabel);
            SetPrivateField(overview, flags, "dateTimeLabel", dateTimeLabel);
            SetPrivateField(overview, flags, "segmentLabel", segmentLabel);
            SetPrivateField(overview, flags, "ramLabel", ramLabel);
            SetPrivateField(overview, flags, "tabTitleLabel", tabTitleLabel);
            SetPrivateField(overview, flags, "tabDescriptionLabel", tabDescriptionLabel);
        }

        private static void SetPrivateField(object target, System.Reflection.BindingFlags flags, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, flags);
            if (field != null)
                field.SetValue(target, value);
        }

        private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void Top(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height - y);
            rt.offsetMax = new Vector2(-right, -y);
        }

        private static void Bottom(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(-right, y + height);
        }

        private static void FixedTopLeft(RectTransform rt, float width, float height, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void FixedTopRight(RectTransform rt, float width, float height, float x, float y)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-x, -y);
        }

        private static void FixedBottomCenter(RectTransform rt, float width, float height, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static bool IsGameplayMenuBlockedInCurrentScene()
        {
            return GameplayMenuScenePolicy.BlocksPlayerMenuInActiveScene();
        }
    }
}
