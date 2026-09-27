using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public enum InventoryNavigationPhase
    {
        CategorySection = 0,
        CategoryEditing = 1,
        ListSection = 2,
        ListEditing = 3,
        ActionsSection = 4,
        UsePopup = 5,
    }

    /// <summary>
    /// Prefab-driven Player Menu Inventory tab. Supabase/Core bag reads remain service-owned;
    /// this screen only renders verified/stub snapshots and routes menu input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryScreen : MenuScreen, IMenuContentInputHandler, IMenuAdvancedInputHandler
    {
        [Header("Services")]
        [SerializeField] private MonoBehaviour inventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour itemUseServiceBehaviour;
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private PlayerItemCatalogSO catalogOverride;
        [SerializeField] private InventoryItemPresentationCatalogSO presentationCatalog;

        [Header("Layout")]
        [SerializeField] private RectTransform safeContent;
        [SerializeField] private InventoryCategoryRail categoryRail;
        [SerializeField] private InventorySectionChrome sectionChrome;
        [SerializeField] private InventoryMotionController motionController;
        [SerializeField] private PartyTabMotionProfileSO motionProfile;
        [SerializeField] private CanvasGroup categoryPanelGroup;
        [SerializeField] private CanvasGroup listPanelGroup;
        [SerializeField] private CanvasGroup actionsPanelGroup;
        [SerializeField] private CanvasGroup detailPanelGroup;
        [SerializeField] private CanvasGroup actionButtonsGroup;
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        [Header("Header / Status")]
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text ramLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text categorySummaryLabel;

        [Header("List")]
        [SerializeField] private ScrollRect itemScrollRect;
        [SerializeField] private InventoryRowStripeBackground rowStripeBackground;
        [SerializeField] private RectTransform itemRowsRoot;
        [SerializeField] private InventoryItemRowDisplay itemRowTemplate;
        [SerializeField] private TMP_Text emptyListLabel;

        [Header("Portrait / Details")]
        [SerializeField] private Image itemPortrait;
        [SerializeField] private CanvasGroup itemPortraitGroup;
        [SerializeField] private TMP_Text detailNameLabel;
        [SerializeField] private TMP_Text detailBodyLabel;
        [SerializeField] private TMP_Text detailMetaLabel;

        [Header("Actions")]
        [SerializeField] private TMP_Text actionPromptLabel;
        [SerializeField] private Button useButton;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text useButtonLabel;
        [SerializeField] private TMP_Text dropButtonLabel;
        [SerializeField] private TMP_Text refreshButtonLabel;

        [Header("Use Popup")]
        [SerializeField] private InventoryItemUsePopupController usePopup;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;

        [Header("Category Colors")]
        [SerializeField] private Color fieldColor = PlayerItemCategoryPalette.Field;
        [SerializeField] private Color captureColor = PlayerItemCategoryPalette.Capture;
        [FormerlySerializedAs("clarityColor")]
        [SerializeField] private Color favorColor = PlayerItemCategoryPalette.Favor;
        [SerializeField] private Color binderyColor = PlayerItemCategoryPalette.Bindery;
        [SerializeField] private Color archiveColor = PlayerItemCategoryPalette.Archive;
        [SerializeField] private Color cosmeticColor = PlayerItemCategoryPalette.Cosmetic;
        [SerializeField] private Color questColor = PlayerItemCategoryPalette.Quest;
        [SerializeField] private Color emptyPortraitColor = new Color(0.16f, 0.2f, 0.26f, 0.7f);

        [Header("Navigation")]
        [SerializeField] private InventoryNavigationPhase phase = InventoryNavigationPhase.CategorySection;
        [SerializeField] private int selectedRowIndex;
        [SerializeField] private int selectedActionIndex;

        private readonly List<InventoryItemRowDisplay> rowDisplays = new List<InventoryItemRowDisplay>();
        private readonly List<MenuInventoryItemStack> visibleRows = new List<MenuInventoryItemStack>();
        private readonly List<Selectable> navigationOrder = new List<Selectable>();
        private readonly List<Button> actionButtons = new List<Button>(3);
        private readonly List<CanvasGroup> sectionGroups = new List<CanvasGroup>(3);
        private InventorySnapshot snapshot;
        private string equippedCaptureHeadItemId = string.Empty;
        private string equippedCaptureBodyItemId = string.Empty;
        private string selectedCategory = PlayerItemCategoryKeys.Field;
        private readonly List<PlayerRosterEntry> useTargets = new List<PlayerRosterEntry>();
        private int selectedTargetIndex;
        private MenuInventoryItemStack selectedStack;
        private MenuInputContextController activeInputContext;
        private MonoBehaviour cachedSfxPlayerBehaviour;
        private bool busy;
        private bool buttonsWired;
        private bool sectionChromeWired;
        private bool pendingTabEnterPresentation;
        private Coroutine rowPresentationRoutine;
        /// <summary>
        /// True when ListEditing was entered from confirming the category carousel
        /// (auto-send). False when entered from section-browse Confirm on the list frame.
        /// Drives Back destination from ListEditing.
        /// </summary>
        private bool listEditingEnteredFromCategoryCarousel;

        public bool HasEmbeddedLayout
        {
            get
            {
                ResolveAuthoredReferencesFromHierarchy();
                return HasRenderableLayout();
            }
        }

        public void ConfigureServices(
            MonoBehaviour inventoryService,
            MonoBehaviour itemUseService = null,
            SupabaseConfigSO config = null)
        {
            if (inventoryService != null)
                inventoryServiceBehaviour = inventoryService;
            if (itemUseService != null)
                itemUseServiceBehaviour = itemUseService;
            if (config != null)
                supabaseConfig = config;
        }

        public void NotifyTabOpened()
        {
            pendingTabEnterPresentation = true;
        }

        public void WarmPresentation()
        {
            ResolveSpeciesDatabase()?.WarmLookup();
            usePopup?.WarmPresentation();
        }

        public override void Show()
        {
            EnsureBuilt();
            WireRuntimeHandlers();
            categoryRail?.SetCategory(selectedCategory, false);
            // Enter already confirmed into the category carousel (skip CategorySection arming click).
            EnterCategoryEditing(animate: false);
            base.Show();

            if (pendingTabEnterPresentation)
            {
                PlayScreenEnter();
                pendingTabEnterPresentation = false;
            }
            else
            {
                RestoreSectionPanelVisibility();
            }

            _ = ReloadAsync();
        }

        public override void Hide()
        {
            StopRowPresentationRoutine();
            UnwireRuntimeHandlers();
            if (activeInputContext != null)
                activeInputContext.SetContentInputHandler(null);
            activeInputContext = null;
            motionController?.ResetRowAnimation(rowDisplays);
            base.Hide();
        }

        public override void Refresh()
        {
            EnsureBuilt();
            if (headingLabel != null)
                headingLabel.text = string.IsNullOrWhiteSpace(DisplayName) ? "Inventory" : DisplayName;
            Render();
        }

        public override void ConfigureInputContext(MenuInputContextController context)
        {
            activeInputContext = context;
            if (activeInputContext != null)
                activeInputContext.SetContentInputHandler(this);
            RefreshInputNavigation();
        }

        public override bool TryHandleCancel()
        {
            return TryHandleCancelInput();
        }

        public bool TryHandleCancelInput()
        {
            if (phase == InventoryNavigationPhase.UsePopup && usePopup != null && usePopup.IsOpen)
                return usePopup.TryHandleCancel();

            // Back from confirmed carousel → section browse (Category frame).
            if (categoryRail != null && categoryRail.IsCarouselOpen)
            {
                categoryRail.SetOpen(false, true);
                SetPhase(InventoryNavigationPhase.CategorySection, true);
                return true;
            }

            if (phase == InventoryNavigationPhase.ListEditing)
            {
                // History: carousel→list Back returns to carousel; section-browse→list Back stays on list frame.
                if (listEditingEnteredFromCategoryCarousel)
                    EnterCategoryEditing(animate: true);
                else
                    SetPhase(InventoryNavigationPhase.ListSection, true);
                return true;
            }

            if (phase == InventoryNavigationPhase.ActionsSection)
            {
                // Back → ListEditing on the same item; preserves carousel/section-browse history.
                EnterListEditing(true);
                return true;
            }

            if (phase == InventoryNavigationPhase.CategoryEditing)
            {
                SetPhase(InventoryNavigationPhase.CategorySection, true);
                return true;
            }

            // CategorySection / ListSection: let parent close the tab / menu.
            return false;
        }

        public bool TryHandleMove(Vector2 move)
        {
            if (move.sqrMagnitude < 0.01f)
                return false;

            if (phase == InventoryNavigationPhase.UsePopup && usePopup != null && usePopup.IsOpen)
            {
                // Vertical row picker: prefer Y for browse; ignore X.
                if (Mathf.Abs(move.y) >= Mathf.Abs(move.x) && Mathf.Abs(move.y) > 0.01f)
                {
                    bool moved = usePopup.TryMoveSelection(move.y > 0f ? -1 : 1);
                    if (moved)
                        PlaySfxCue(PlayerMenuSfxCue.CarouselMove);
                    return true;
                }

                return true;
            }

            if (categoryRail != null && categoryRail.IsCarouselOpen)
            {
                if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
                {
                    int delta = move.y > 0f ? -1 : 1;
                    PlayerMenuSfxResolver.SuppressNavigationCues(0.12f);
                    bool moved = categoryRail.MoveCarousel(delta, true);
                    if (moved)
                        PlaySfxCue(PlayerMenuSfxCue.CarouselMove);
                    return moved;
                }

                // Horizontal blocked while carousel is open (nav-locked).
                return true;
            }

            if (phase == InventoryNavigationPhase.ListEditing)
            {
                if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
                {
                    int delta = move.y > 0f ? -1 : 1;
                    MoveRow(delta, true);
                    return true;
                }

                // Nav-locked while confirmed in list. Back returns to section browse.
                return true;
            }

            if (phase == InventoryNavigationPhase.ActionsSection)
            {
                if (Mathf.Abs(move.y) >= Mathf.Abs(move.x))
                {
                    int delta = move.y > 0f ? -1 : 1;
                    MoveAction(delta, true);
                    return true;
                }

                // Nav-locked: Cancel returns to list editing; no free left/right section hop.
                return true;
            }

            // Unconfirmed section browse: Category ↔ List only (Actions stays item-confirm locked).
            if (phase == InventoryNavigationPhase.CategorySection ||
                phase == InventoryNavigationPhase.ListSection)
            {
                if (Mathf.Abs(move.x) < Mathf.Abs(move.y))
                    return true;

                MoveSectionBrowse(move.x > 0f ? 1 : -1);
                return true;
            }

            return true;
        }

        public bool TryHandleSubmit()
        {
            switch (phase)
            {
                case InventoryNavigationPhase.CategorySection:
                    EnterCategoryEditing(animate: true);
                    return true;
                case InventoryNavigationPhase.CategoryEditing:
                    if (categoryRail != null && categoryRail.ConfirmCarousel(true))
                        EnterListEditing(true, fromCategoryCarousel: true);
                    return true;
                case InventoryNavigationPhase.ListSection:
                    EnterListEditing(true, fromCategoryCarousel: false);
                    return true;
                case InventoryNavigationPhase.ListEditing:
                    if (selectedStack != null)
                        SetPhase(InventoryNavigationPhase.ActionsSection, true);
                    else if (visibleRows.Count == 0)
                        SetStatus("No items in this category.");
                    else
                        SetStatus("Select an item first.");
                    return true;
                case InventoryNavigationPhase.ActionsSection:
                    ActivateSelectedAction();
                    return true;
                case InventoryNavigationPhase.UsePopup:
                    return usePopup != null && usePopup.TryHandleSubmit();
                default:
                    return false;
            }
        }

        public bool TryHandleDetailedView()
        {
            if (selectedStack == null || busy)
                return false;
            OpenInspectCeremonyForSelectedItem();
            return true;
        }
        public bool TryHandleCloseMenu() => false;

        public bool TryHandleSpecialInteraction()
        {
            OnRefreshClicked();
            return true;
        }

        public bool TryHandleGuide() => false;

        public void BuildDefaultLayoutForEditor()
        {
            if (!Application.isPlaying)
                ClearGeneratedLayoutForEditor();

            EnsureStructuralLayoutForEditor();
        }

        public void EnsureStructuralLayoutForEditor()
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            SetScreenIdentity("inventory", "Inventory", canvasGroup);

            RectTransform root = transform as RectTransform;
            if (root != null)
                PlayerMenuUiFactory.Stretch(root);

            safeContent = ResolveOrCreateRect("SafeContent", transform);
            PlayerMenuUiFactory.Stretch(safeContent, 48f, 72f, 48f, 48f);

            motionController = GetComponent<InventoryMotionController>();
            if (motionController == null)
                motionController = gameObject.AddComponent<InventoryMotionController>();
            motionController.Configure(motionProfile);

            BuildStatusStrip();
            BuildPanels();
            BuildSectionChrome();
            EnsureUsePopupAuthoringForEditor();
            ResolveAuthoredReferencesFromHierarchy();
        }

        /// <summary>
        /// Creates/wires UsePopup hierarchy on InventoryView so dialogue Message and
        /// TargetSlotTemplate rows can be authored in the prefab (not only at runtime).
        /// </summary>
        public void EnsureUsePopupAuthoringForEditor()
        {
            if (usePopup == null)
                usePopup = GetComponentInChildren<InventoryItemUsePopupController>(true);

            if (usePopup == null)
            {
                GameObject go = new GameObject("UsePopup", typeof(RectTransform), typeof(InventoryItemUsePopupController));
                go.transform.SetParent(transform, false);
                usePopup = go.GetComponent<InventoryItemUsePopupController>();
            }

            RectTransform popupHost = usePopup.transform as RectTransform;
            if (popupHost != null)
                PlayerMenuUiFactory.Stretch(popupHost);

            usePopup.EnsureAuthoringLayoutForEditor(popupHost);
            usePopup.ConfigureMotion(motionProfile);
            ResolveAuthoredReferencesFromHierarchy();
        }

        public void EnsureListPanelAuthoringForEditor()
        {
            if (safeContent == null)
            {
                Transform existingSafeContent = transform.Find("SafeContent");
                safeContent = existingSafeContent as RectTransform;
            }

            if (safeContent == null)
                safeContent = PlayerMenuUiFactory.CreateRect("SafeContent", transform);

            PlayerMenuUiFactory.Stretch(safeContent, 48f, 72f, 48f, 48f);

            RectTransform listPanel = BuildPanel(
                "ListPanel",
                safeContent,
                new Vector2(0.245f, 0.08f),
                new Vector2(0.72f, 0.94f));
            listPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(listPanel.gameObject);
            BuildListPanel(listPanel);
            ResolveAuthoredReferencesFromHierarchy();
        }

        public void EnsureActionsPanelAuthoringForEditor()
        {
            if (safeContent == null)
            {
                Transform existingSafeContent = transform.Find("SafeContent");
                safeContent = existingSafeContent as RectTransform;
            }

            if (safeContent == null)
                safeContent = PlayerMenuUiFactory.CreateRect("SafeContent", transform);

            PlayerMenuUiFactory.Stretch(safeContent, 48f, 72f, 48f, 48f);

            RectTransform actionsPanel = BuildPanel(
                "ActionsPanel",
                safeContent,
                new Vector2(0.745f, 0.08f),
                new Vector2(1f, 0.94f));
            actionsPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(actionsPanel.gameObject);
            BuildActionsPanel(actionsPanel);
            ResolveAuthoredReferencesFromHierarchy();
        }

        public void EnsureSectionFrameAuthoringForEditor()
        {
            if (safeContent == null)
            {
                Transform existingSafeContent = transform.Find("SafeContent");
                safeContent = existingSafeContent as RectTransform;
            }

            if (safeContent == null)
                safeContent = PlayerMenuUiFactory.CreateRect("SafeContent", transform);

            PlayerMenuUiFactory.Stretch(safeContent, 48f, 72f, 48f, 48f);

            if (sectionChrome == null)
                sectionChrome = safeContent.GetComponent<InventorySectionChrome>();
            if (sectionChrome == null)
                sectionChrome = safeContent.gameObject.AddComponent<InventorySectionChrome>();

            RectTransform category = safeContent.Find("CategoryPanel") as RectTransform;
            RectTransform list = safeContent.Find("ListPanel") as RectTransform;
            RectTransform actions = safeContent.Find("ActionsPanel") as RectTransform;
            sectionChrome.Configure(category, list, actions);
            sectionChrome.ApplyDefaultAuthoredFrameLayoutForEditor();
            ResolveAuthoredReferencesFromHierarchy();
        }

        private void ClearGeneratedLayoutForEditor()
        {
            DestroyChildImmediate("StatusStrip");
            DestroyChildImmediate("SafeContent");

            safeContent = null;
            categoryRail = null;
            sectionChrome = null;
            categoryPanelGroup = null;
            listPanelGroup = null;
            actionsPanelGroup = null;
            detailPanelGroup = null;
            actionButtonsGroup = null;
            headingLabel = null;
            ramLabel = null;
            statusLabel = null;
            categorySummaryLabel = null;
            itemScrollRect = null;
            rowStripeBackground = null;
            itemRowsRoot = null;
            itemRowTemplate = null;
            emptyListLabel = null;
            itemPortrait = null;
            itemPortraitGroup = null;
            detailPanelGroup = null;
            detailNameLabel = null;
            detailBodyLabel = null;
            detailMetaLabel = null;
            actionPromptLabel = null;
            useButton = null;
            dropButton = null;
            refreshButton = null;
            useButtonLabel = null;
            dropButtonLabel = null;
            refreshButtonLabel = null;
            buttonsWired = false;
            sectionChromeWired = false;
        }

        private void DestroyChildImmediate(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
                DestroyImmediate(child.gameObject);
        }

        private void EnsureBuilt()
        {
            ResolveAuthoredReferencesFromHierarchy();
            CompleteRuntimeLayoutIfNeeded();
            ResolveAuthoredReferencesFromHierarchy();
            if (motionController != null)
                motionController.Configure(motionProfile);
            if (categoryRail != null)
                categoryRail.Configure(motionProfile, HandleCategoryChanged, HandleCategoryRailOpenRequested, HandleCategoryRailClosed);
            EnsureSectionChrome();
            RefreshInputNavigation();
        }

        private void CompleteRuntimeLayoutIfNeeded()
        {
            if (!Application.isPlaying || HasRenderableLayout())
                return;

            BuildDefaultLayoutForEditor();
        }

        private bool HasRenderableLayout()
        {
            return safeContent != null &&
                categoryRail != null &&
                categoryPanelGroup != null &&
                listPanelGroup != null &&
                actionsPanelGroup != null &&
                itemRowsRoot != null &&
                itemRowTemplate != null &&
                itemScrollRect != null &&
                itemPortrait != null &&
                detailBodyLabel != null &&
                useButton != null &&
                dropButton != null;
        }

        private RectTransform ResolveSafeContentOnly()
        {
            if (safeContent == null)
                safeContent = transform.Find("SafeContent") as RectTransform;
            return safeContent;
        }

        private void ResolveAuthoredReferencesFromHierarchy()
        {
            if (safeContent == null)
                safeContent = transform.Find("SafeContent") as RectTransform;

            if (categoryRail == null)
                categoryRail = GetComponentInChildren<InventoryCategoryRail>(true);
            if (sectionChrome == null && safeContent != null)
                sectionChrome = safeContent.GetComponent<InventorySectionChrome>();
            if (motionController == null)
                motionController = GetComponent<InventoryMotionController>();

            if (headingLabel == null)
                headingLabel = FindText("StatusStrip/Heading");
            if (ramLabel == null)
                ramLabel = FindText("StatusStrip/RamLabel");
            if (statusLabel == null)
                statusLabel = FindText("StatusStrip/StatusLabel");
            if (categorySummaryLabel == null)
                categorySummaryLabel = FindText("SafeContent/ListPanel/Header/CategorySummary");

            if (categoryPanelGroup == null)
                categoryPanelGroup = FindCanvasGroup("SafeContent/CategoryPanel");
            if (listPanelGroup == null)
                listPanelGroup = FindCanvasGroup("SafeContent/ListPanel");
            if (actionsPanelGroup == null)
                actionsPanelGroup = FindCanvasGroup("SafeContent/ActionsPanel");
            if (detailPanelGroup == null)
                detailPanelGroup = FindCanvasGroup("SafeContent/ListPanel/ItemDetailArea");

            if (itemScrollRect == null)
                itemScrollRect = FindComponent<ScrollRect>("SafeContent/ListPanel/ItemListArea/ItemRowsViewport")
                    ?? FindComponent<ScrollRect>("SafeContent/ListPanel/ItemRowsViewport");
            if (itemRowsRoot == null && itemScrollRect != null)
                itemRowsRoot = itemScrollRect.content;
            if (rowStripeBackground == null)
                rowStripeBackground = FindComponent<InventoryRowStripeBackground>("SafeContent/ListPanel/ItemListArea/ItemRowsViewport/Rows/RowStripeBackground")
                    ?? FindComponent<InventoryRowStripeBackground>("SafeContent/ListPanel/ItemListArea/ItemRowsViewport/RowStripeBackground")
                    ?? FindComponent<InventoryRowStripeBackground>("SafeContent/ListPanel/ItemRowsViewport/Rows/RowStripeBackground")
                    ?? FindComponent<InventoryRowStripeBackground>("SafeContent/ListPanel/ItemRowsViewport/RowStripeBackground");
            if (itemRowTemplate == null && itemRowsRoot != null)
                itemRowTemplate = itemRowsRoot.GetComponentInChildren<InventoryItemRowDisplay>(true);
            if (emptyListLabel == null)
                emptyListLabel = FindText("SafeContent/ListPanel/ItemListArea/ItemRowsViewport/EmptyListLabel")
                    ?? FindText("SafeContent/ListPanel/ItemRowsViewport/EmptyListLabel");

            if (itemPortrait == null)
                itemPortrait = FindComponent<Image>("SafeContent/ListPanel/ItemDetailArea/ItemPortraitFrame/ItemPortrait")
                    ?? FindComponent<Image>("SafeContent/ListPanel/ItemPortrait");
            if (itemPortraitGroup == null && itemPortrait != null)
                itemPortraitGroup = itemPortrait.GetComponent<CanvasGroup>();
            if (itemPortraitGroup == null && itemPortrait != null)
                itemPortraitGroup = itemPortrait.gameObject.AddComponent<CanvasGroup>();

            if (detailNameLabel == null)
                detailNameLabel = FindText("SafeContent/ListPanel/ItemDetailArea/ItemDescriptionFrame/DetailName")
                    ?? FindText("SafeContent/ActionsPanel/DetailName");
            if (detailBodyLabel == null)
                detailBodyLabel = FindText("SafeContent/ListPanel/ItemDetailArea/ItemDescriptionFrame/DetailBody")
                    ?? FindText("SafeContent/ActionsPanel/DetailBody");
            if (detailMetaLabel == null)
                detailMetaLabel = FindText("SafeContent/ListPanel/ItemDetailArea/ItemDescriptionFrame/DetailMeta")
                    ?? FindText("SafeContent/ActionsPanel/DetailMeta");

            if (actionPromptLabel == null)
                actionPromptLabel = FindText("SafeContent/ActionsPanel/ActionPrompt");
            if (useButton == null)
                useButton = FindComponent<Button>("SafeContent/ActionsPanel/ActionButtonsRoot/UseButton")
                    ?? FindComponent<Button>("SafeContent/ActionsPanel/UseButton");
            if (dropButton == null)
                dropButton = FindComponent<Button>("SafeContent/ActionsPanel/ActionButtonsRoot/DropButton")
                    ?? FindComponent<Button>("SafeContent/ActionsPanel/DropButton");
            if (refreshButton == null)
                refreshButton = FindComponent<Button>("SafeContent/ActionsPanel/ActionButtonsRoot/RefreshButton")
                    ?? FindComponent<Button>("SafeContent/ActionsPanel/RefreshButton");
            if (useButtonLabel == null && useButton != null)
                useButtonLabel = useButton.GetComponentInChildren<TMP_Text>(true);
            if (dropButtonLabel == null && dropButton != null)
                dropButtonLabel = dropButton.GetComponentInChildren<TMP_Text>(true);
            if (refreshButtonLabel == null && refreshButton != null)
                refreshButtonLabel = refreshButton.GetComponentInChildren<TMP_Text>(true);

            if (actionButtonsGroup == null)
                actionButtonsGroup = FindCanvasGroup("SafeContent/ActionsPanel/ActionButtonsRoot");

            if (usePopup == null)
                usePopup = GetComponentInChildren<InventoryItemUsePopupController>(true);
        }

        private void WireRuntimeHandlers()
        {
            if (!buttonsWired)
            {
                if (useButton != null)
                    useButton.onClick.AddListener(OnUseClicked);
                if (dropButton != null)
                    dropButton.onClick.AddListener(OnDropClicked);
                if (refreshButton != null)
                    refreshButton.onClick.AddListener(OnRefreshClicked);
                buttonsWired = true;
            }

            if (!sectionChromeWired && sectionChrome != null)
            {
                sectionChrome.SectionSelected += HandleSectionFrameSelected;
                sectionChromeWired = true;
            }

            EnsureUsePopup();
        }

        private void UnwireRuntimeHandlers()
        {
            if (buttonsWired)
            {
                if (useButton != null)
                    useButton.onClick.RemoveListener(OnUseClicked);
                if (dropButton != null)
                    dropButton.onClick.RemoveListener(OnDropClicked);
                if (refreshButton != null)
                    refreshButton.onClick.RemoveListener(OnRefreshClicked);
                buttonsWired = false;
            }

            if (sectionChromeWired && sectionChrome != null)
            {
                sectionChrome.SectionSelected -= HandleSectionFrameSelected;
                sectionChromeWired = false;
            }

            if (usePopup != null)
            {
                usePopup.UseRequested -= OnUsePopupUseRequested;
                usePopup.CancelRequested -= OnUsePopupCancelRequested;
                usePopup.ContinueRequested -= OnUsePopupContinueRequested;
                if (usePopup.IsOpen)
                    usePopup.Close();
            }
        }

        private async Task ReloadAsync()
        {
            if (busy)
                return;

            busy = true;
            SetStatus("Loading inventory...");
            EnsureServices();

            IInventoryService service = inventoryServiceBehaviour as IInventoryService;
            if (service == null)
            {
                SetStatus("No inventory service found.");
                busy = false;
                Render();
                return;
            }

            string profileId = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";
            string wallet = GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "";
            snapshot = await service.FetchInventoryAsync(profileId, wallet);
            await RefreshCaptureEquipmentFilterAsync();
            busy = false;

            if (selectedStack != null)
                selectedStack = FindStack(selectedStack.itemId, selectedStack.coreAssetId);

            if (selectedStack == null)
                selectedRowIndex = 0;

            SetStatus(FormatInventoryStatus(snapshot));
            Render();
        }

        private async Task RefreshCaptureEquipmentFilterAsync()
        {
            equippedCaptureHeadItemId = string.Empty;
            equippedCaptureBodyItemId = string.Empty;
            AuthoritativeCombatSessionService authority = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (authority == null || !authority.IsRolloutEnabled)
                return;

            try
            {
                Chainbound.Combat.Core.CaptureLoadoutSnapshot loadout = await authority.FetchAsync();
                if (loadout == null)
                    return;
                equippedCaptureHeadItemId = loadout.HeadItemId ?? string.Empty;
                equippedCaptureBodyItemId = loadout.BodyItemId ?? string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[InventoryScreen] Capture equipment filter unavailable; showing all owned utility gear. {exception.Message}", this);
            }
        }

        private void EnsureServices()
        {
            EnsureInventoryService();
            EnsureItemUseService();
        }

        private void EnsureInventoryService()
        {
            if (inventoryServiceBehaviour is IInventoryService)
                return;

            ChainlingsMenuTestHarness harness = ResolveHarness();
            if (harness != null)
            {
                inventoryServiceBehaviour = harness;
                return;
            }

            if (supabaseConfig != null)
            {
                CoreBagItemInventoryService core = GetComponent<CoreBagItemInventoryService>()
                    ?? gameObject.AddComponent<CoreBagItemInventoryService>();
                core.Configure(supabaseConfig);
                inventoryServiceBehaviour = core;
                return;
            }

            SetStatus("Inventory requires Supabase config or a sandbox harness.");
        }

        private void EnsureItemUseService()
        {
            if (itemUseServiceBehaviour is IPlayerItemUseService)
                return;

            ChainlingsMenuTestHarness harness = ResolveHarness();
            if (harness != null)
            {
                StubPlayerItemUseService stub = GetComponent<StubPlayerItemUseService>()
                    ?? gameObject.AddComponent<StubPlayerItemUseService>();
                FavorService favor = GetComponent<FavorService>()
                    ?? harness.GetComponent<FavorService>()
                    ?? gameObject.AddComponent<FavorService>();
                stub.Configure(
                    harness,
                    favor,
                    null,
                    harness,
                    harness.SpeciesDatabase);
                itemUseServiceBehaviour = stub;
                return;
            }

            if (supabaseConfig != null)
            {
                SupabasePlayerItemUseService production = GetComponent<SupabasePlayerItemUseService>()
                    ?? gameObject.AddComponent<SupabasePlayerItemUseService>();
                CoreBagItemInventoryService inventory = inventoryServiceBehaviour as CoreBagItemInventoryService
                    ?? GetComponent<CoreBagItemInventoryService>()
                    ?? gameObject.AddComponent<CoreBagItemInventoryService>();
                inventory.Configure(supabaseConfig);

                FavorService favor = GetComponent<FavorService>()
                    ?? gameObject.AddComponent<FavorService>();
                production.Configure(supabaseConfig, favor, null, inventory);
                itemUseServiceBehaviour = production;
                if (!(inventoryServiceBehaviour is IInventoryService))
                    inventoryServiceBehaviour = inventory;
                return;
            }

            SetStatus("Item use requires Supabase config or a sandbox harness.");
        }

        private ChainlingsMenuTestHarness ResolveHarness()
        {
            return GetComponentInParent<ChainlingsMenuTestHarness>(true)
                ?? FindObjectOfType<ChainlingsMenuTestHarness>(true);
        }

        private void Render()
        {
            if (ramLabel != null)
                ramLabel.text = snapshot != null ? $"RAM {snapshot.ramBalance}" : "RAM --";

            categoryRail?.SetCategory(selectedCategory, false);
            RebuildRows();
            RenderDetails();
            RefreshActionState();
            RefreshInputNavigation();
        }

        private void RebuildRows()
        {
            StopRowPresentationRoutine();
            motionController?.ResetRowAnimation(rowDisplays);
            visibleRows.Clear();
            visibleRows.AddRange(FilterStacks(selectedCategory));

            if (itemRowsRoot == null || itemRowTemplate == null)
                return;

            for (int i = rowDisplays.Count - 1; i >= 0; i--)
            {
                if (rowDisplays[i] != null && rowDisplays[i] != itemRowTemplate)
                    DestroyUnityObject(rowDisplays[i].gameObject);
            }
            rowDisplays.Clear();

            itemRowTemplate.gameObject.SetActive(false);
            Color categoryColor = ResolveCategoryColor(selectedCategory);
            for (int i = 0; i < visibleRows.Count; i++)
            {
                InventoryItemRowDisplay row = Instantiate(itemRowTemplate, itemRowsRoot);
                row.name = $"ItemRow_{i + 1:00}_{visibleRows[i].itemId}";
                row.gameObject.SetActive(true);
                row.BuildDefaultLayoutForEditor();
                PrepareRowForLayout(row);
                PlayerItemDefinition definition = ResolveDefinition(visibleRows[i]);
                InventoryItemPresentationEntry presentation = ResolvePresentation(visibleRows[i], definition);
                Sprite fallbackIcon = presentationCatalog != null ? presentationCatalog.FallbackRowIcon : null;
                row.Bind(visibleRows[i], definition, categoryColor, presentation, fallbackIcon);
                int captured = i;
                row.Button.onClick.AddListener(() => HandleRowConfirmed(captured));
                rowDisplays.Add(row);
            }

            ResetScrollToTop();
            RebuildItemRowsLayout();
            selectedRowIndex = visibleRows.Count > 0 ? Mathf.Clamp(selectedRowIndex, 0, visibleRows.Count - 1) : -1;
            selectedStack = selectedRowIndex >= 0 && selectedRowIndex < visibleRows.Count ? visibleRows[selectedRowIndex] : null;
            ResetScrollToTop();
            if (emptyListLabel != null)
                emptyListLabel.gameObject.SetActive(visibleRows.Count == 0);
            if (categorySummaryLabel != null)
                categorySummaryLabel.text = $"{InventoryCategoryRail.ToLabel(selectedCategory)} - {visibleRows.Count} stack(s)";

            RefreshRowSelection(false);
            QueueRowsInPresentation();
        }

        private static void PrepareRowForLayout(InventoryItemRowDisplay row)
        {
            if (row == null)
                return;

            LayoutElement layoutElement = PlayerMenuUiFactory.GetOrAdd<LayoutElement>(row.gameObject);
            layoutElement.ignoreLayout = false;
            if (layoutElement.preferredHeight <= 0f)
                layoutElement.preferredHeight = 72f;
            layoutElement.flexibleWidth = 1f;
        }

        private void RebuildItemRowsLayout()
        {
            if (itemRowsRoot == null)
                return;

            EnsureRowStripeBackgroundAttached();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(itemRowsRoot);
            if (itemScrollRect != null)
            {
                if (itemScrollRect.viewport != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(itemScrollRect.viewport);

                RectTransform scrollRectTransform = itemScrollRect.transform as RectTransform;
                if (scrollRectTransform != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
            }

            rowStripeBackground?.Rebuild();
            for (int i = 0; i < rowDisplays.Count; i++)
                rowDisplays[i]?.CaptureRestPosition();
        }

        private void QueueRowsInPresentation()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                motionController?.PlayRowsIn(rowDisplays);
                return;
            }

            rowPresentationRoutine = StartCoroutine(PlayRowsAfterStableLayout());
        }

        private IEnumerator PlayRowsAfterStableLayout()
        {
            yield return null;

            RebuildItemRowsLayout();
            ResetScrollToTop();
            RefreshRowSelection(false);
            motionController?.PlayRowsIn(rowDisplays);
            rowPresentationRoutine = null;
        }

        private void StopRowPresentationRoutine()
        {
            if (rowPresentationRoutine == null)
                return;

            StopCoroutine(rowPresentationRoutine);
            rowPresentationRoutine = null;
        }

        private void RenderDetails()
        {
            Color categoryColor = ResolveCategoryColor(selectedCategory);
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            InventoryItemPresentationEntry presentation = ResolvePresentation(selectedStack, definition);
            if (itemPortrait != null)
            {
                Sprite portrait = selectedStack != null && presentation != null && presentation.detailPortrait != null
                    ? presentation.detailPortrait
                    : selectedStack != null && presentationCatalog != null
                        ? presentationCatalog.FallbackDetailPortrait
                        : null;
                itemPortrait.sprite = portrait;
                itemPortrait.preserveAspect = portrait != null;
                itemPortrait.color = selectedStack != null
                    ? portrait != null
                        ? Color.white
                        : new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.9f)
                    : emptyPortraitColor;
            }

            motionController?.PlayDetailUpdate(itemPortraitGroup, itemPortrait != null ? itemPortrait.transform : null);
            if (detailPanelGroup != null)
            {
                detailPanelGroup.DOKill();
                detailPanelGroup.alpha = 1f;
            }

            if (selectedStack == null)
            {
                if (detailNameLabel != null)
                    detailNameLabel.text = "No Item Selected";
                if (detailBodyLabel != null)
                    detailBodyLabel.text = visibleRows.Count == 0
                        ? "This category is empty."
                        : "Select an item to inspect its use, ownership, and Core provenance.";
                if (detailMetaLabel != null)
                    detailMetaLabel.text = "";
                return;
            }

            if (detailNameLabel != null)
                detailNameLabel.text = ResolveDisplayName(selectedStack, definition);

            if (detailBodyLabel != null)
                detailBodyLabel.text = definition != null && !string.IsNullOrWhiteSpace(definition.description)
                    ? definition.description
                    : "No catalog description available.";

            if (detailMetaLabel != null)
            {
                detailMetaLabel.text =
                    $"id: {selectedStack.itemId}\n" +
                    $"qty: {selectedStack.quantity}\n" +
                    $"category: {selectedStack.categoryKey}\n" +
                    $"ownership: {selectedStack.ownershipKind}" +
                    (string.IsNullOrWhiteSpace(selectedStack.coreAssetId)
                        ? ""
                        : $"\ncore asset:\n{selectedStack.coreAssetId}");
            }
        }

        private void RefreshActionState()
        {
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            InventoryItemCeremonyId ceremony = InventoryItemCeremonyResolver.Resolve(definition);
            bool showActions = Application.isPlaying
                ? phase == InventoryNavigationPhase.ActionsSection && selectedStack != null
                : true;
            bool canUse = selectedStack != null &&
                selectedStack.quantity > 0 &&
                !busy &&
                ceremony != InventoryItemCeremonyId.None;
            bool canSelectLockedDrop = selectedStack != null && selectedStack.quantity > 0 && !busy;

            if (actionButtonsGroup != null)
            {
                actionButtonsGroup.alpha = showActions ? 1f : 0f;
                actionButtonsGroup.interactable = showActions;
                actionButtonsGroup.blocksRaycasts = showActions;
                actionButtonsGroup.gameObject.SetActive(showActions || !Application.isPlaying);
            }

            if (actionPromptLabel != null)
            {
                actionPromptLabel.gameObject.SetActive(!showActions || !Application.isPlaying);
                if (selectedStack == null)
                {
                    actionPromptLabel.text = "Select an item to view actions.";
                }
                else
                {
                    switch (ceremony)
                    {
                        case InventoryItemCeremonyId.PartyTarget:
                            actionPromptLabel.text = "Use opens the party picker.";
                            break;
                        case InventoryItemCeremonyId.PartyAoeConfirm:
                            actionPromptLabel.text = "Use confirms a party-wide patch.";
                            break;
                        case InventoryItemCeremonyId.SelfConfirm:
                            actionPromptLabel.text = "Use confirms Favor Boost.";
                            break;
                        case InventoryItemCeremonyId.ItemInspect:
                            actionPromptLabel.text = "Use or Detailed View inspects this item.";
                            break;
                        case InventoryItemCeremonyId.UtilityGearEquip:
                            actionPromptLabel.text = "Equip assigns this item to its Head or Body utility slot.";
                            break;
                        case InventoryItemCeremonyId.CaptureBoostConfirm:
                            actionPromptLabel.text = "Activate starts its real-time capture boost.";
                            break;
                        default:
                            actionPromptLabel.text = "Confirm the item to view actions.";
                            break;
                    }
                }
            }

            if (useButton != null)
                useButton.interactable = canUse;
            if (dropButton != null)
                dropButton.interactable = canSelectLockedDrop;
            if (refreshButton != null)
            {
                refreshButton.interactable = !busy;
                refreshButton.gameObject.SetActive(false);
            }
            if (useButtonLabel != null)
                useButtonLabel.text = busy
                    ? "Busy"
                    : ceremony == InventoryItemCeremonyId.UtilityGearEquip
                        ? "Equip"
                        : ceremony == InventoryItemCeremonyId.CaptureBoostConfirm
                            ? "Activate"
                            : "Use";
            if (dropButtonLabel != null)
                dropButtonLabel.text = "Drop";
            if (refreshButtonLabel != null)
                refreshButtonLabel.text = "Refresh";

            RefreshActionButtons();
            selectedActionIndex = ClampActionIndex(selectedActionIndex);
            // Selection/shimmer is owned by SetPhase after this group is shown, or by MoveAction.
            // Avoid a second Apply here that can race with inactive→active.
            if (phase != InventoryNavigationPhase.ActionsSection)
                RefreshActionSelection(false);
        }

        private void SelectRow(int index, bool animate)
        {
            if (visibleRows.Count == 0)
            {
                selectedRowIndex = -1;
                selectedStack = null;
                RenderDetails();
                RefreshActionState();
                return;
            }

            selectedRowIndex = Mathf.Clamp(index, 0, visibleRows.Count - 1);
            selectedStack = visibleRows[selectedRowIndex];
            RefreshRowSelection(animate);
            RenderDetails();
            RefreshActionState();
        }

        private void MoveRow(int delta, bool animate)
        {
            if (visibleRows.Count == 0)
                return;

            SelectRow(Mathf.Clamp(selectedRowIndex + delta, 0, visibleRows.Count - 1), animate);
            ScrollSelectedRowIntoView();
        }

        private void HandleRowConfirmed(int index)
        {
            SelectRow(index, true);
            if (selectedStack != null)
                SetPhase(InventoryNavigationPhase.ActionsSection, true);
        }

        private void MoveAction(int delta, bool animate)
        {
            RefreshActionButtons();
            if (actionButtons.Count == 0)
                return;

            selectedActionIndex = ClampActionIndex(selectedActionIndex + delta);
            RefreshActionSelection(animate);
            SelectPhaseDefault();
        }

        private void RefreshActionButtons()
        {
            actionButtons.Clear();
            // Don't gate on IsUsable here — the action root may still be inactive while SetPhase
            // is applying selection chrome for the Actions layer.
            if (useButton != null)
                actionButtons.Add(useButton);
            if (dropButton != null)
                actionButtons.Add(dropButton);

            EnsureActionButtonChrome(useButton);
            EnsureActionButtonChrome(dropButton);
        }

        private int ClampActionIndex(int index)
        {
            RefreshActionButtons();
            if (actionButtons.Count == 0)
                return 0;
            return Mathf.Clamp(index, 0, actionButtons.Count - 1);
        }

        private void RefreshActionSelection(bool animate)
        {
            RefreshActionButtons();
            selectedActionIndex = ClampActionIndex(selectedActionIndex);
            bool actionsFocused = phase == InventoryNavigationPhase.ActionsSection;
            for (int i = 0; i < actionButtons.Count; i++)
            {
                Button button = actionButtons[i];
                bool selected = actionsFocused && i == selectedActionIndex;
                PlayerMenuChromeButtonVisual chrome = button != null
                    ? button.GetComponent<PlayerMenuChromeButtonVisual>()
                    : null;
                chrome?.SetSelected(selected);
                motionController?.SetActionSelected(button, selected, animate && actionsFocused);
            }
        }

        private void EnsureActionButtonChrome(Button button)
        {
            if (button == null)
                return;

            PlayerMenuChromeButtonVisual chrome = button.GetComponent<PlayerMenuChromeButtonVisual>();
            if (chrome == null)
                chrome = button.gameObject.AddComponent<PlayerMenuChromeButtonVisual>();

            if (categoryRail != null &&
                (categoryRail.CarouselButtonSprite != null || categoryRail.CarouselButtonSelectedSprite != null))
            {
                chrome.Configure(
                    categoryRail.CarouselButtonSprite,
                    categoryRail.CarouselButtonSelectedSprite,
                    categoryRail.CarouselButtonTint,
                    categoryRail.CarouselButtonSelectedTint,
                    categoryRail.CarouselShimmerFrames,
                    categoryRail.CarouselShimmerFps,
                    categoryRail.EnableCarouselShimmer);
            }
            else
            {
                chrome.EnsureHierarchy();
            }
        }

        private void ActivateSelectedAction()
        {
            RefreshActionButtons();
            selectedActionIndex = ClampActionIndex(selectedActionIndex);
            Button button = selectedActionIndex >= 0 && selectedActionIndex < actionButtons.Count
                ? actionButtons[selectedActionIndex]
                : null;

            if (button == useButton)
            {
                OnUseClicked();
                return;
            }

            if (button == dropButton)
            {
                OnDropClicked();
                return;
            }

            SetStatus("Select an action first.");
            PlaySfxCue(PlayerMenuSfxCue.Invalid);
        }

        private void RefreshRowSelection(bool animate)
        {
            for (int i = 0; i < rowDisplays.Count; i++)
            {
                InventoryItemRowDisplay row = rowDisplays[i];
                if (row == null)
                    continue;

                bool selected = i == selectedRowIndex;
                row.SetSelected(selected, animate);
                motionController?.SetRowSelected(row, selected, animate);
            }
        }

        private void ScrollSelectedRowIntoView()
        {
            if (itemScrollRect == null || visibleRows.Count <= 1)
                return;

            float normalized = 1f - Mathf.Clamp01(selectedRowIndex / Mathf.Max(1f, visibleRows.Count - 1f));
            itemScrollRect.verticalNormalizedPosition = normalized;
        }

        private void MoveSection(int delta)
        {
            MoveSectionBrowse(delta);
        }

        /// <summary>
        /// Unconfirmed panel hop: Category ↔ List only. Actions stay locked behind item confirm.
        /// </summary>
        private void MoveSectionBrowse(int delta)
        {
            InventorySectionFocusTarget section = ResolveFocusedSection();
            int index = section == InventorySectionFocusTarget.Category ? 0 : 1;
            int next = Mathf.Clamp(index + delta, 0, 1);
            SetSection(
                next == 0 ? InventorySectionFocusTarget.Category : InventorySectionFocusTarget.List,
                true);
        }

        private void SetSection(InventorySectionFocusTarget section, bool animate)
        {
            switch (section)
            {
                case InventorySectionFocusTarget.Category:
                    if (categoryRail != null && categoryRail.IsCarouselOpen)
                        categoryRail.SetOpen(false, animate);
                    SetPhase(InventoryNavigationPhase.CategorySection, animate);
                    break;
                case InventorySectionFocusTarget.Actions:
                    // Never via section hop — only reachable after confirming an item.
                    if (phase == InventoryNavigationPhase.ListEditing && selectedStack != null)
                        SetPhase(InventoryNavigationPhase.ActionsSection, animate);
                    else
                        SetPhase(InventoryNavigationPhase.ListSection, animate);
                    break;
                default:
                    if (categoryRail != null && categoryRail.IsCarouselOpen)
                        categoryRail.SetOpen(false, animate);
                    SetPhase(InventoryNavigationPhase.ListSection, animate);
                    break;
            }
        }

        private void EnterCategoryEditing(bool animate)
        {
            SetPhase(InventoryNavigationPhase.CategoryEditing, animate);
            categoryRail?.OpenCarousel(animate);
        }

        private void EnterListEditing(bool animate, bool? fromCategoryCarousel = null)
        {
            if (fromCategoryCarousel.HasValue)
                listEditingEnteredFromCategoryCarousel = fromCategoryCarousel.Value;

            if (visibleRows.Count == 0)
            {
                selectedRowIndex = -1;
                selectedStack = null;
                SetPhase(InventoryNavigationPhase.ListEditing, animate);
                SetStatus("No items in this category.");
                RenderDetails();
                RefreshActionState();
                return;
            }

            if (selectedRowIndex < 0 || selectedRowIndex >= visibleRows.Count)
                selectedRowIndex = 0;
            selectedStack = visibleRows[selectedRowIndex];
            SetPhase(InventoryNavigationPhase.ListEditing, animate);
            RefreshRowSelection(animate);
            RenderDetails();
            RefreshActionState();
        }

        private void SetPhase(InventoryNavigationPhase nextPhase, bool animate)
        {
            phase = nextPhase;
            bool categoryEditing = phase == InventoryNavigationPhase.CategoryEditing;
            bool listEditing = phase == InventoryNavigationPhase.ListEditing;
            bool actionsLocked = phase == InventoryNavigationPhase.ActionsSection;
            bool sectionBrowsing = phase == InventoryNavigationPhase.CategorySection
                || phase == InventoryNavigationPhase.ListSection;
            bool usePopupOpen = phase == InventoryNavigationPhase.UsePopup;
            categoryRail?.SetArmed(
                categoryEditing || phase == InventoryNavigationPhase.CategorySection,
                animate);

            InventorySectionFocusTarget focusedSection = ResolveFocusedSection();
            if (!usePopupOpen)
            {
                // Confirmed panels: focused frame full opacity, others dimmed, no pulse.
                // Section browse: focused frame pulses, others dimmed, frames receive input.
                sectionChrome?.SetActiveSection(focusedSection, sectionBrowsing);
            }

            if (focusedSection == InventorySectionFocusTarget.Actions && !usePopupOpen)
            {
                RebuildUseTargets();
                selectedActionIndex = ClampActionIndex(selectedActionIndex);
            }

            // Show ActionButtonsRoot before selection chrome so shimmer coroutines can start.
            RefreshActionState();
            if (focusedSection == InventorySectionFocusTarget.Actions && !usePopupOpen)
                RefreshActionSelection(animate);

            RefreshInputNavigation();
            SelectPhaseDefault();
        }

        private InventorySectionFocusTarget ResolveFocusedSection()
        {
            switch (phase)
            {
                case InventoryNavigationPhase.CategorySection:
                case InventoryNavigationPhase.CategoryEditing:
                    return InventorySectionFocusTarget.Category;
                case InventoryNavigationPhase.ActionsSection:
                case InventoryNavigationPhase.UsePopup:
                    return InventorySectionFocusTarget.Actions;
                default:
                    return InventorySectionFocusTarget.List;
            }
        }

        private void SelectPhaseDefault()
        {
            if (activeInputContext == null)
                return;

            Selectable target = ResolvePhaseSelectable();
            if (target != null)
                activeInputContext.Select(target);
        }

        private Selectable ResolvePhaseSelectable()
        {
            if (phase == InventoryNavigationPhase.UsePopup && usePopup != null)
                return usePopup.ResolvePreferredSelectable();

            switch (ResolveFocusedSection())
            {
                case InventorySectionFocusTarget.Category:
                    if (phase == InventoryNavigationPhase.CategoryEditing &&
                        categoryRail != null &&
                        categoryRail.IsCarouselOpen)
                    {
                        return categoryRail.GetCategoryButton(categoryRail.FocusedCategory)
                            ?? categoryRail.CollapsedButton;
                    }

                    return sectionChrome != null && sectionChrome.CategorySectionButton != null
                        ? sectionChrome.CategorySectionButton
                        : categoryRail != null ? categoryRail.CollapsedButton : null;
                case InventorySectionFocusTarget.Actions:
                    RefreshActionButtons();
                    selectedActionIndex = ClampActionIndex(selectedActionIndex);
                    return selectedActionIndex >= 0 && selectedActionIndex < actionButtons.Count
                        ? actionButtons[selectedActionIndex]
                        : null;
                default:
                    if (phase == InventoryNavigationPhase.ListEditing &&
                        selectedRowIndex >= 0 &&
                        selectedRowIndex < rowDisplays.Count &&
                        rowDisplays[selectedRowIndex] != null)
                    {
                        return rowDisplays[selectedRowIndex].Button;
                    }

                    return sectionChrome != null ? sectionChrome.ListSectionButton : null;
            }
        }

        private void RefreshInputNavigation()
        {
            if (activeInputContext == null)
                return;

            navigationOrder.Clear();

            switch (phase)
            {
                case InventoryNavigationPhase.CategoryEditing:
                    if (categoryRail != null && categoryRail.IsCarouselOpen)
                    {
                        IReadOnlyList<Button> categoryButtons = categoryRail.CategoryButtons;
                        for (int i = 0; i < categoryButtons.Count; i++)
                            AddNavigationSelectable(categoryButtons[i]);
                    }
                    else
                    {
                        AddNavigationSelectable(categoryRail != null ? categoryRail.CollapsedButton : null);
                    }
                    break;

                case InventoryNavigationPhase.CategorySection:
                case InventoryNavigationPhase.ListSection:
                    // Section browse: Category ↔ List frames only (no Actions).
                    AddNavigationSelectable(sectionChrome != null ? sectionChrome.CategorySectionButton : null);
                    AddNavigationSelectable(categoryRail != null ? categoryRail.CollapsedButton : null);
                    AddNavigationSelectable(sectionChrome != null ? sectionChrome.ListSectionButton : null);
                    break;

                case InventoryNavigationPhase.ListEditing:
                    for (int i = 0; i < rowDisplays.Count; i++)
                        AddNavigationSelectable(rowDisplays[i] != null ? rowDisplays[i].Button : null);
                    if (navigationOrder.Count == 0)
                        AddNavigationSelectable(sectionChrome != null ? sectionChrome.ListSectionButton : null);
                    break;

                case InventoryNavigationPhase.ActionsSection:
                    AddNavigationSelectable(useButton);
                    AddNavigationSelectable(dropButton);
                    break;

                case InventoryNavigationPhase.UsePopup:
                    if (usePopup != null)
                        AddNavigationSelectable(usePopup.ResolvePreferredSelectable());
                    break;
            }

            activeInputContext.SetContentNavigation(navigationOrder, ResolvePhaseSelectable(), transform);
            activeInputContext.SetContentInputHandler(this);
        }

        private void AddNavigationSelectable(Selectable selectable)
        {
            if (!MenuSelectableNavigation.IsUsable(selectable) || navigationOrder.Contains(selectable))
                return;

            navigationOrder.Add(selectable);
        }

        private void HandleSectionFrameSelected(InventorySectionFocusTarget section)
        {
            SetSection(section, true);
        }

        private void HandleCategoryRailOpenRequested()
        {
            EnterCategoryEditing(animate: true);
        }

        private void HandleCategoryRailClosed()
        {
            // Confirm path only (Submit on carousel) — land confirmed in the item list.
            EnterListEditing(true, fromCategoryCarousel: true);
        }

        private void HandleCategoryChanged(string categoryKey)
        {
            string normalized = NormalizeCategory(categoryKey);
            if (string.Equals(selectedCategory, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            selectedCategory = normalized;
            selectedRowIndex = 0;
            selectedStack = null;
            SetStatus($"{InventoryCategoryRail.ToLabel(selectedCategory)} inventory.");
            Render();
        }

        private void OnRefreshClicked() => _ = ReloadAsync();

        private void OnDropClicked()
        {
            if (selectedStack == null || busy)
            {
                SetStatus("Select an item first.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                motionController?.PlayActionFeedback(dropButton != null ? dropButton.transform : null, statusLabel, false);
                return;
            }

            SetStatus("Drop is locked until server-safe inventory mutation is available.");
            PlaySfxCue(PlayerMenuSfxCue.Invalid);
            motionController?.PlayActionFeedback(dropButton != null ? dropButton.transform : null, statusLabel, false);
        }

        private void OnUseClicked()
        {
            if (selectedStack == null || busy)
            {
                SetStatus("Select an item first.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            InventoryItemCeremonyId ceremony = InventoryItemCeremonyResolver.Resolve(definition);
            switch (ceremony)
            {
                case InventoryItemCeremonyId.PartyTarget:
                    OpenPartyTargetCeremony();
                    return;
                case InventoryItemCeremonyId.PartyAoeConfirm:
                    OpenPartyAoeCeremony();
                    return;
                case InventoryItemCeremonyId.SelfConfirm:
                    OpenSelfConfirmCeremony();
                    return;
                case InventoryItemCeremonyId.ItemInspect:
                    OpenInspectCeremonyForSelectedItem();
                    return;
                case InventoryItemCeremonyId.UtilityGearEquip:
                case InventoryItemCeremonyId.CaptureBoostConfirm:
                    _ = ApplyCaptureUtilityItemAsync(definition, ceremony);
                    return;
                default:
                    SetStatus("This item cannot be used from the menu.");
                    PlaySfxCue(PlayerMenuSfxCue.Invalid);
                    motionController?.PlayActionFeedback(useButton != null ? useButton.transform : null, statusLabel, false);
                    return;
            }
        }

        private async Task ApplyCaptureUtilityItemAsync(PlayerItemDefinition definition, InventoryItemCeremonyId ceremony)
        {
            if (definition == null || selectedStack == null || busy)
                return;

            AuthoritativeCombatSessionService service = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (service == null || !service.IsRolloutEnabled)
            {
                SetStatus("Capture utility changes require online game services.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            busy = true;
            RefreshActionState();
            try
            {
                if (ceremony == InventoryItemCeremonyId.UtilityGearEquip)
                {
                    await service.EquipAsync(definition.utilitySlot, definition.itemId);
                    SetStatus($"Equipped {definition.displayName} to {definition.utilitySlot}.");
                }
                else
                {
                    Chainbound.Combat.Core.CaptureLoadoutSnapshot loadout = await service.ActivateBoostAsync(definition.itemId);
                    long expiry = 0;
                    if (loadout != null && loadout.ActiveBoosts != null)
                    {
                        for (int i = 0; i < loadout.ActiveBoosts.Count; i++)
                        {
                            if (loadout.ActiveBoosts[i].ItemId == definition.itemId)
                                expiry = loadout.ActiveBoosts[i].ExpiresAtUnixMilliseconds;
                        }
                    }
                    SetStatus(expiry > 0
                        ? $"{definition.displayName} active until {DateTimeOffset.FromUnixTimeMilliseconds(expiry).ToLocalTime():t}."
                        : $"Activated {definition.displayName}.");
                }

                PlaySfxCue(PlayerMenuSfxCue.Confirm);
                if (inventoryServiceBehaviour is CoreBagItemInventoryService inventory)
                    inventory.ClearItemInventoryCache();
                busy = false;
                await ReloadAsync();
                return;
            }
            catch (Exception exception)
            {
                SetStatus(exception is AuthoritativeCombatServiceException authorityError && authorityError.Code == "time_authority_stale"
                    ? "Network time is stale. Nothing was consumed; try again shortly."
                    : exception.Message);
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
            }

            busy = false;
            Render();
        }

        private void OpenPartyTargetCeremony()
        {
            EnsureUsePopup();
            RebuildUseTargets(includePartyForAoe: false);
            if (useTargets.Count == 0)
            {
                SetStatus("No party Chainlings available.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            OpenCeremonyPopup(BuildCeremonyContext(InventoryItemCeremonyId.PartyTarget));
        }

        private void OpenPartyAoeCeremony()
        {
            EnsureUsePopup();
            RebuildUseTargets(includePartyForAoe: true);
            if (useTargets.Count == 0)
            {
                SetStatus("No party Chainlings available.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            OpenCeremonyPopup(BuildCeremonyContext(InventoryItemCeremonyId.PartyAoeConfirm));
        }

        private void OpenSelfConfirmCeremony()
        {
            EnsureUsePopup();
            OpenCeremonyPopup(BuildCeremonyContext(InventoryItemCeremonyId.SelfConfirm));
        }

        private void OpenInspectCeremonyForSelectedItem()
        {
            if (selectedStack == null)
                return;

            EnsureUsePopup();
            OpenCeremonyPopup(BuildCeremonyContext(InventoryItemCeremonyId.ItemInspect));
        }

        private void OpenCeremonyPopup(InventoryItemCeremonyContext context)
        {
            if (context == null || usePopup == null)
                return;

            usePopup.ConfigureMotion(motionProfile);
            usePopup.gameObject.SetActive(true);
            usePopup.transform.SetAsLastSibling();
            usePopup.OpenCeremony(context);
            SetPhase(InventoryNavigationPhase.UsePopup, true);
            SetStatus(context.Ceremony == InventoryItemCeremonyId.ItemInspect
                ? InventoryItemUseMessageUtility.FormatInspectHint()
                : context.Ceremony == InventoryItemCeremonyId.SelfConfirm
                    ? InventoryItemUseMessageUtility.FormatConfirmFavor(context.ItemDisplayName)
                    : context.Ceremony == InventoryItemCeremonyId.PartyAoeConfirm
                        ? InventoryItemUseMessageUtility.FormatConfirmPartyAoe(context.ItemDisplayName)
                        : InventoryItemUseMessageUtility.FormatChooseTarget(context.ItemDisplayName));
        }

        private InventoryItemCeremonyContext BuildCeremonyContext(InventoryItemCeremonyId ceremony)
        {
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            string displayName = ResolveDisplayName(selectedStack, definition);
            InventoryItemPresentationEntry presentation = null;
            if (presentationCatalog != null)
                presentationCatalog.TryResolve(selectedStack, definition, out presentation);

            var context = new InventoryItemCeremonyContext
            {
                Ceremony = ceremony,
                ItemDisplayName = displayName,
                EffectLabel = InventoryItemEffectCopy.FormatEffectLabel(definition),
                Definition = definition,
                Stack = selectedStack,
                Presentation = presentation,
                PartyTargets = useTargets,
                SpeciesDatabase = ResolveSpeciesDatabase(),
                InspectBodyText = InventoryItemEffectCopy.FormatInspectBody(definition, selectedStack, presentation),
            };

            if (ceremony == InventoryItemCeremonyId.SelfConfirm && definition != null)
                FillFavorContext(context, definition);

            return context;
        }

        private void FillFavorContext(InventoryItemCeremonyContext context, PlayerItemDefinition definition)
        {
            IFavorService favor = FavorRuntime.Active;
            FavorConfigSO config = FavorRuntime.Config;
            int ampoule = favor != null ? favor.AmpouleFavorPoints : 0;
            int party = favor != null ? favor.PartyFavorPoints : 0;
            int battles = favor != null ? favor.AmpouleBattlesRemaining : 0;
            int current = favor != null ? favor.CurrentFavorScore : ampoule + party;
            int applyPoints = Mathf.Max(0, definition.favorPoints);
            int applyBattles = Mathf.Max(0, definition.favorDurationBattles);
            int projected = applyPoints + party;

            context.CurrentFavorScore = current;
            context.AmpouleFavorPoints = ampoule;
            context.PartyFavorPoints = party;
            context.AmpouleBattlesRemaining = battles;
            context.LuckStrength01 = config != null ? config.EvaluateCurve01(current) : 0f;
            context.ApplyFavorPoints = applyPoints;
            context.ApplyFavorBattles = applyBattles;
            context.ProjectedFavorScore = projected;
            context.ProjectedLuckStrength01 = config != null ? config.EvaluateCurve01(projected) : 0f;
        }

        private void OpenUsePopupForSelectedItem()
        {
            OpenPartyTargetCeremony();
        }

        private async System.Threading.Tasks.Task UseItemWithoutTargetPopupAsync()
        {
            // Legacy path kept for harness; ceremonies now own Favor / HealParty.
            OpenSelfConfirmCeremony();
            await Task.CompletedTask;
        }

        private async void OnUsePopupUseRequested()
        {
            if (busy || usePopup == null || !usePopup.IsOpen || selectedStack == null)
                return;

            InventoryItemCeremonyId ceremony = usePopup.ActiveCeremony;
            if (ceremony == InventoryItemCeremonyId.ItemInspect)
            {
                OnUsePopupContinueRequested();
                return;
            }

            if (ceremony == InventoryItemCeremonyId.SelfConfirm)
            {
                await ApplySelfConfirmCeremonyAsync();
                return;
            }

            if (ceremony == InventoryItemCeremonyId.PartyAoeConfirm)
            {
                await ApplyPartyAoeCeremonyAsync();
                return;
            }

            await ApplyPartyTargetCeremonyAsync();
        }

        private async Task ApplyPartyTargetCeremonyAsync()
        {
            PlayerRosterEntry target = usePopup.SelectedTarget;
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            string targetName = ResolveTargetDisplayName(target);
            if (target?.snapshot == null || string.IsNullOrWhiteSpace(target.snapshot.backendChainlingId))
            {
                usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid("target_required", targetName, definition != null ? definition.effectFamily : PlayerItemEffectFamily.None));
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            SpeciesDatabaseSO speciesDatabase = ResolveSpeciesDatabase();
            SpeciesDefinitionSO species = speciesDatabase != null
                ? speciesDatabase.GetById(target.snapshot.speciesId)
                : null;
            if (species != null && definition != null)
            {
                ChainlingVitalsDisplay before = ChainlingVitalsUtility.BuildDisplay(target.snapshot, species);
                if (definition.effectFamily == PlayerItemEffectFamily.Revive && !before.IsFainted)
                {
                    usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid("target_not_fainted", targetName, definition.effectFamily));
                    PlaySfxCue(PlayerMenuSfxCue.Invalid);
                    return;
                }

                if ((definition.effectFamily == PlayerItemEffectFamily.HealSingle ||
                     definition.effectFamily == PlayerItemEffectFamily.StatusClear) &&
                    before.IsFainted)
                {
                    usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid("target_fainted_use_revive", targetName, definition.effectFamily));
                    PlaySfxCue(PlayerMenuSfxCue.Invalid);
                    return;
                }

                if ((definition.effectFamily == PlayerItemEffectFamily.HealSingle ||
                     definition.effectFamily == PlayerItemEffectFamily.StatusClear) &&
                    before.CurrentHp >= before.MaxHp)
                {
                    usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid("already_full_hp", targetName, definition.effectFamily));
                    PlaySfxCue(PlayerMenuSfxCue.Invalid);
                    return;
                }
            }

            EnsureServices();
            IPlayerItemUseService useService = itemUseServiceBehaviour as IPlayerItemUseService;
            if (useService == null)
            {
                usePopup.ShowInvalidMessage("Item use service unavailable.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            float fromFill = 0f;
            if (species != null)
                fromFill = ChainlingVitalsUtility.BuildDisplay(target.snapshot, species).Fill01;

            busy = true;
            PlayerItemUseResult result = await useService.UseItemAsync(
                GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "",
                GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "",
                new PlayerItemUseRequest
                {
                    itemId = selectedStack.itemId,
                    coreAssetId = selectedStack.coreAssetId,
                    targetChainlingId = target.snapshot.backendChainlingId,
                });
            busy = false;

            if (result == null || !result.success)
            {
                string code = result != null ? result.error : "use_failed";
                usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid(
                    code,
                    targetName,
                    definition != null ? definition.effectFamily : PlayerItemEffectFamily.None));
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            if (result.inventory != null)
                snapshot = result.inventory;

            int healed = 0;
            float toFill = fromFill;
            string hpLabel = null;
            if (result.hpPatches != null && result.hpPatches.Count > 0)
            {
                FieldItemHpPatch patch = result.hpPatches[0];
                healed = patch != null ? patch.healedAmount : 0;
                int maxHp = species != null
                    ? ChainlingVitalsUtility.BuildMaxHp(target.snapshot, species)
                    : 0;
                if (patch != null && maxHp > 0)
                {
                    toFill = Mathf.Clamp01(patch.currentHp / (float)maxHp);
                    hpLabel = $"{patch.currentHp} / {maxHp}";
                }
            }

            string successMessage = InventoryItemUseMessageUtility.FormatRecovered(targetName, healed);
            SetStatus(successMessage);
            PlayerItemEffectFamily family = definition != null ? definition.effectFamily : PlayerItemEffectFamily.HealSingle;
            PlaySfxCue(InventoryItemCeremonyResolver.ResolveApplyTickCue(family));
            int slotIndex = usePopup.SelectedIndex;
            usePopup.PlayHealSuccess(slotIndex, fromFill, toFill, hpLabel, successMessage, () =>
            {
                PlaySfxCue(InventoryItemCeremonyResolver.ResolveApplyCompleteCue(family));
            });

            selectedStack = FindStack(selectedStack.itemId, selectedStack.coreAssetId);
            Render();
        }

        private async Task ApplyPartyAoeCeremonyAsync()
        {
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            string displayName = ResolveDisplayName(selectedStack, definition);
            EnsureServices();
            IPlayerItemUseService useService = itemUseServiceBehaviour as IPlayerItemUseService;
            if (useService == null)
            {
                usePopup.ShowInvalidMessage("Item use service unavailable.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            SpeciesDatabaseSO speciesDatabase = ResolveSpeciesDatabase();
            var fromFills = new List<(int slotIndex, float fromFill01, string chainlingId)>(usePopup.TargetCount);
            for (int i = 0; i < usePopup.TargetCount; i++)
            {
                PlayerRosterEntry entry = useTargets.Count > i ? useTargets[i] : null;
                if (entry?.snapshot == null)
                    continue;
                SpeciesDefinitionSO species = speciesDatabase != null
                    ? speciesDatabase.GetById(entry.snapshot.speciesId)
                    : null;
                float from = species != null
                    ? ChainlingVitalsUtility.BuildDisplay(entry.snapshot, species).Fill01
                    : 0f;
                fromFills.Add((i, from, entry.snapshot.backendChainlingId));
            }

            busy = true;
            PlayerItemUseResult result = await useService.UseItemAsync(
                GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "",
                GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "",
                new PlayerItemUseRequest
                {
                    itemId = selectedStack.itemId,
                    coreAssetId = selectedStack.coreAssetId,
                });
            busy = false;

            if (result == null || !result.success)
            {
                string code = result != null ? result.error : "use_failed";
                usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid(
                    code,
                    displayName,
                    definition != null ? definition.effectFamily : PlayerItemEffectFamily.HealParty));
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            if (result.inventory != null)
                snapshot = result.inventory;

            var fills = new List<(int slotIndex, float fromFill01, float toFill01, string hpLabel)>();
            int healedTotal = 0;
            int membersHealed = 0;
            if (result.hpPatches != null)
            {
                for (int p = 0; p < result.hpPatches.Count; p++)
                {
                    FieldItemHpPatch patch = result.hpPatches[p];
                    if (patch == null)
                        continue;
                    healedTotal += Mathf.Max(0, patch.healedAmount);
                    if (patch.healedAmount > 0)
                        membersHealed++;

                    int slotIndex = -1;
                    float fromFill = 0f;
                    for (int f = 0; f < fromFills.Count; f++)
                    {
                        if (string.Equals(fromFills[f].chainlingId, patch.chainlingId, StringComparison.Ordinal))
                        {
                            slotIndex = fromFills[f].slotIndex;
                            fromFill = fromFills[f].fromFill01;
                            break;
                        }
                    }

                    if (slotIndex < 0)
                        continue;

                    PlayerRosterEntry entry = useTargets.Count > slotIndex ? useTargets[slotIndex] : null;
                    SpeciesDefinitionSO species = entry?.snapshot != null && speciesDatabase != null
                        ? speciesDatabase.GetById(entry.snapshot.speciesId)
                        : null;
                    int maxHp = species != null && entry != null
                        ? ChainlingVitalsUtility.BuildMaxHp(entry.snapshot, species)
                        : 0;
                    float toFill = maxHp > 0 ? Mathf.Clamp01(patch.currentHp / (float)maxHp) : fromFill;
                    string hpLabel = maxHp > 0 ? $"{patch.currentHp} / {maxHp}" : null;
                    fills.Add((slotIndex, fromFill, toFill, hpLabel));
                }
            }

            string successMessage = InventoryItemUseMessageUtility.FormatPartyRecovered(healedTotal, membersHealed);
            SetStatus(successMessage);
            PlaySfxCue(InventoryItemCeremonyResolver.ResolveApplyTickCue(PlayerItemEffectFamily.HealParty));
            usePopup.PlayPartyHealSuccess(fills, successMessage, () =>
            {
                PlaySfxCue(InventoryItemCeremonyResolver.ResolveApplyCompleteCue(PlayerItemEffectFamily.HealParty));
            });

            selectedStack = FindStack(selectedStack.itemId, selectedStack.coreAssetId);
            Render();
        }

        private async Task ApplySelfConfirmCeremonyAsync()
        {
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            string displayName = ResolveDisplayName(selectedStack, definition);
            EnsureServices();
            IPlayerItemUseService useService = itemUseServiceBehaviour as IPlayerItemUseService;
            if (useService == null)
            {
                usePopup.ShowInvalidMessage("Item use service unavailable.");
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            busy = true;
            PlayerItemUseResult result = await useService.UseItemAsync(
                GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "",
                GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "",
                new PlayerItemUseRequest
                {
                    itemId = selectedStack.itemId,
                    coreAssetId = selectedStack.coreAssetId,
                });
            busy = false;

            if (result == null || !result.success)
            {
                string code = result != null ? result.error : "use_failed";
                usePopup.ShowInvalidMessage(InventoryItemUseMessageUtility.FormatInvalid(
                    code,
                    displayName,
                    definition != null ? definition.effectFamily : PlayerItemEffectFamily.FavorBoost));
                PlaySfxCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            if (result.inventory != null)
                snapshot = result.inventory;

            int battles = definition != null ? definition.favorDurationBattles : 0;
            int score = result.favorScore;
            if (FavorRuntime.Active != null)
            {
                score = FavorRuntime.Active.CurrentFavorScore;
                battles = FavorRuntime.Active.AmpouleBattlesRemaining;
            }

            string successMessage = InventoryItemUseMessageUtility.FormatFavorApplied(score, battles);
            SetStatus(successMessage);
            PlaySfxCue(InventoryItemCeremonyResolver.ResolveApplyCompleteCue(PlayerItemEffectFamily.FavorBoost));
            usePopup.PlayFavorSuccess(successMessage);

            selectedStack = FindStack(selectedStack.itemId, selectedStack.coreAssetId);
            Render();
        }

        private void OnUsePopupCancelRequested()
        {
            bool wasInspect = usePopup != null && usePopup.ActiveCeremony == InventoryItemCeremonyId.ItemInspect;
            usePopup?.Close();
            SetPhase(InventoryNavigationPhase.ListEditing, true);
            SetStatus(wasInspect ? "Inspect closed." : "Use cancelled.");
            PlaySfxCue(PlayerMenuSfxCue.Cancel);
            Render();
        }

        private void OnUsePopupContinueRequested()
        {
            usePopup?.Close();
            selectedStack = selectedStack != null
                ? FindStack(selectedStack.itemId, selectedStack.coreAssetId)
                : null;
            SetPhase(InventoryNavigationPhase.ListEditing, true);
            SetStatus(selectedStack != null
                ? $"{ResolveDisplayName(selectedStack, ResolveDefinition(selectedStack))} ready."
                : "Select an item.");
            Render();
        }

        private void EnsureUsePopup()
        {
            if (usePopup == null)
            {
                usePopup = GetComponentInChildren<InventoryItemUsePopupController>(true);
                if (usePopup == null)
                {
                    RectTransform host = transform as RectTransform;
                    GameObject go = new GameObject("UsePopup", typeof(RectTransform), typeof(InventoryItemUsePopupController));
                    go.transform.SetParent(host, false);
                    usePopup = go.GetComponent<InventoryItemUsePopupController>();
                }
            }

            RectTransform popupHost = usePopup.transform as RectTransform;
            // Stretch the popup host only when it has no authored size yet (runtime scaffold).
            if (popupHost != null &&
                Mathf.Approximately(popupHost.anchorMin.x, 0.5f) &&
                Mathf.Approximately(popupHost.anchorMax.x, 0.5f) &&
                popupHost.sizeDelta == Vector2.zero)
            {
                PlayerMenuUiFactory.Stretch(popupHost);
            }

            // Resolve/create missing nodes only — do not rewrite authored TargetRow / template layout.
            usePopup.EnsureDefaultLayout(popupHost);
            usePopup.ConfigureMotion(motionProfile);
            usePopup.UseRequested -= OnUsePopupUseRequested;
            usePopup.CancelRequested -= OnUsePopupCancelRequested;
            usePopup.ContinueRequested -= OnUsePopupContinueRequested;
            usePopup.UseRequested += OnUsePopupUseRequested;
            usePopup.CancelRequested += OnUsePopupCancelRequested;
            usePopup.ContinueRequested += OnUsePopupContinueRequested;
        }

        private bool NeedsTargetForSelectedItem()
        {
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            return definition != null && FieldItemHealUtility.NeedsPartyTarget(definition.effectFamily);
        }

        private void RebuildUseTargets(bool includePartyForAoe = false)
        {
            useTargets.Clear();
            PlayerItemDefinition definition = ResolveDefinition(selectedStack);
            bool needsParty = definition != null &&
                (FieldItemHealUtility.NeedsPartyTarget(definition.effectFamily) ||
                 (includePartyForAoe && definition.effectFamily == PlayerItemEffectFamily.HealParty));
            if (!needsParty)
            {
                selectedTargetIndex = 0;
                return;
            }

            PlayerRosterSnapshot roster = ResolveLiveRoster();
            if (roster?.chainlings == null)
            {
                selectedTargetIndex = 0;
                return;
            }

            // Always include every active party member (full HP, damaged, fainted).
            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry?.snapshot == null || string.IsNullOrWhiteSpace(entry.snapshot.backendChainlingId))
                    continue;
                if (!entry.isPartyMember)
                    continue;
                useTargets.Add(entry);
            }

            if (useTargets.Count == 0)
            {
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    if (entry?.snapshot == null || string.IsNullOrWhiteSpace(entry.snapshot.backendChainlingId))
                        continue;
                    useTargets.Add(entry);
                    if (useTargets.Count >= CombatConstants.ROSTER_SIZE)
                        break;
                }
            }

            useTargets.Sort((a, b) => a.partySlot.CompareTo(b.partySlot));
            selectedTargetIndex = useTargets.Count == 0 ? 0 : Mathf.Clamp(selectedTargetIndex, 0, useTargets.Count - 1);
        }

        private static string ResolveTargetDisplayName(PlayerRosterEntry entry)
        {
            if (entry?.snapshot == null)
                return "Your Chainling";
            if (!string.IsNullOrWhiteSpace(entry.snapshot.displayName))
                return entry.snapshot.displayName;
            return string.IsNullOrWhiteSpace(entry.snapshot.speciesId) ? "Your Chainling" : entry.snapshot.speciesId;
        }

        private PlayerRosterSnapshot ResolveLiveRoster()
        {
            ChainlingsMenuTestHarness harness = GetComponentInParent<ChainlingsMenuTestHarness>(true)
                ?? FindObjectOfType<ChainlingsMenuTestHarness>(true);
            if (harness != null)
                return harness.CachedRoster;

            SupabasePlayerRosterProvider supabase = FindObjectOfType<SupabasePlayerRosterProvider>(true);
            return supabase != null ? supabase.CachedRoster : null;
        }

        private SpeciesDatabaseSO ResolveSpeciesDatabase()
        {
            if (speciesDatabase != null)
                return speciesDatabase;

            ChainlingsPartyScreen partyScreen = GetComponentInParent<PlayerMenuController>(true)?.GetChainlingsPartyScreen()
                ?? FindObjectOfType<ChainlingsPartyScreen>(true);
            if (partyScreen != null && partyScreen.SpeciesDatabase != null)
                return partyScreen.SpeciesDatabase;

            PlayerMenuDevBootstrap bootstrap = GetComponentInParent<PlayerMenuDevBootstrap>(true)
                ?? FindObjectOfType<PlayerMenuDevBootstrap>(true);
            if (bootstrap != null)
            {
                ChainlingsPartyScreen bootParty = bootstrap.GetComponentInChildren<ChainlingsPartyScreen>(true);
                if (bootParty != null && bootParty.SpeciesDatabase != null)
                    return bootParty.SpeciesDatabase;
            }

            ChainlingsMenuTestHarness harness = GetComponentInParent<ChainlingsMenuTestHarness>(true)
                ?? FindObjectOfType<ChainlingsMenuTestHarness>(true);
            return harness != null ? harness.SpeciesDatabase : null;
        }

        public void ConfigureSpeciesDatabase(SpeciesDatabaseSO database)
        {
            if (database != null)
                speciesDatabase = database;
        }

        private List<MenuInventoryItemStack> FilterStacks(string categoryKey)
        {
            var filtered = new List<MenuInventoryItemStack>();
            if (snapshot?.stacks == null)
                return filtered;

            string normalized = NormalizeCategory(categoryKey);
            for (int i = 0; i < snapshot.stacks.Count; i++)
            {
                MenuInventoryItemStack stack = snapshot.stacks[i];
                if (stack == null || stack.quantity <= 0)
                    continue;
                if (!string.Equals(NormalizeCategory(stack.categoryKey), normalized, StringComparison.OrdinalIgnoreCase))
                    continue;
                PlayerItemDefinition definition = ResolveDefinition(stack);
                if (definition != null && definition.effectFamily == PlayerItemEffectFamily.UtilityGear &&
                    (string.Equals(stack.itemId, equippedCaptureHeadItemId, StringComparison.Ordinal) ||
                     string.Equals(stack.itemId, equippedCaptureBodyItemId, StringComparison.Ordinal)))
                {
                    continue;
                }
                filtered.Add(stack);
            }

            return filtered;
        }

        private MenuInventoryItemStack FindStack(string itemId, string coreAssetId)
        {
            if (snapshot?.stacks == null)
                return null;

            for (int i = 0; i < snapshot.stacks.Count; i++)
            {
                MenuInventoryItemStack stack = snapshot.stacks[i];
                if (stack == null || stack.itemId != itemId)
                    continue;
                if (!string.IsNullOrWhiteSpace(coreAssetId) && stack.coreAssetId != coreAssetId)
                    continue;
                return stack.quantity > 0 ? stack : null;
            }

            return null;
        }

        private string FormatInventoryStatus(InventorySnapshot loaded)
        {
            if (loaded == null)
                return "Inventory failed to load.";
            if (!string.IsNullOrWhiteSpace(loaded.lastError))
                return loaded.lastError;
            return $"{CountStacks(selectedCategory)} item(s) - {loaded.connectivity}";
        }

        private int CountStacks(string categoryKey) => FilterStacks(categoryKey).Count;

        private PlayerItemDefinition ResolveDefinition(MenuInventoryItemStack stack)
        {
            if (stack == null || string.IsNullOrWhiteSpace(stack.itemId))
                return null;

            PlayerItemDefinition definition = null;
            if (catalogOverride != null)
                catalogOverride.TryGet(stack.itemId, out definition);
            if (definition == null)
                PlayerItemCatalogSeed.TryGet(stack.itemId, out definition);
            return definition;
        }

        private InventoryItemPresentationEntry ResolvePresentation(
            MenuInventoryItemStack stack,
            PlayerItemDefinition definition)
        {
            if (presentationCatalog != null &&
                presentationCatalog.TryResolve(stack, definition, out InventoryItemPresentationEntry presentation))
            {
                return presentation;
            }

            return null;
        }

        private string ResolveDisplayName(MenuInventoryItemStack stack, PlayerItemDefinition definition = null)
        {
            if (stack == null)
                return "item";

            definition ??= ResolveDefinition(stack);
            InventoryItemPresentationEntry presentation = ResolvePresentation(stack, definition);
            if (presentation != null && !string.IsNullOrWhiteSpace(presentation.displayNameOverride))
                return presentation.displayNameOverride;
            if (!string.IsNullOrWhiteSpace(stack.displayName))
                return stack.displayName;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.displayName))
                return definition.displayName;
            return string.IsNullOrWhiteSpace(stack.itemId) ? "item" : stack.itemId;
        }

        private void ResetScrollToTop()
        {
            if (itemScrollRect == null)
                return;

            itemScrollRect.StopMovement();
            itemScrollRect.velocity = Vector2.zero;
            if (itemScrollRect.content != null)
            {
                Vector2 anchoredPosition = itemScrollRect.content.anchoredPosition;
                anchoredPosition.y = 0f;
                itemScrollRect.content.anchoredPosition = anchoredPosition;
            }

            Canvas.ForceUpdateCanvases();
            itemScrollRect.verticalNormalizedPosition = 1f;
            itemScrollRect.velocity = Vector2.zero;
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
                statusLabel.text = value ?? "";
        }

        private void PlaySfxCue(PlayerMenuSfxCue cue)
        {
            IPlayerMenuSfxPlayer sfxPlayer = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            sfxPlayer?.PlayCue(cue);
        }

        private void PlayScreenEnter()
        {
            sectionGroups.Clear();
            if (categoryPanelGroup != null)
                sectionGroups.Add(categoryPanelGroup);
            if (listPanelGroup != null)
                sectionGroups.Add(listPanelGroup);
            if (actionsPanelGroup != null)
                sectionGroups.Add(actionsPanelGroup);

            motionController?.PlayScreenEnter(sectionGroups);
        }

        private void RestoreSectionPanelVisibility()
        {
            RestoreCanvasGroup(categoryPanelGroup);
            RestoreCanvasGroup(listPanelGroup);
            RestoreCanvasGroup(actionsPanelGroup);
            RestoreCanvasGroup(detailPanelGroup);
        }

        private static void RestoreCanvasGroup(CanvasGroup group)
        {
            if (group == null)
                return;

            group.DOKill();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        /// <summary>
        /// Prefab overrides stay on the serialized category colors. Defaults match
        /// <see cref="PlayerItemCategoryPalette"/> so obtain titles stay aligned.
        /// </summary>
        private Color ResolveCategoryColor(string categoryKey)
        {
            switch (NormalizeCategory(categoryKey))
            {
                case PlayerItemCategoryKeys.Capture: return captureColor;
                case PlayerItemCategoryKeys.Favor: return favorColor;
                case PlayerItemCategoryKeys.Bindery: return binderyColor;
                case PlayerItemCategoryKeys.Archive: return archiveColor;
                case PlayerItemCategoryKeys.Cosmetic: return cosmeticColor;
                case PlayerItemCategoryKeys.Quest: return questColor;
                default: return fieldColor;
            }
        }

        private static string NormalizeCategory(string categoryKey)
        {
            return PlayerItemCategoryKeys.TryParse(categoryKey, out PlayerItemCategory category)
                ? PlayerItemCategoryKeys.ToKey(category)
                : PlayerItemCategoryKeys.Field;
        }

        private TMP_Text FindText(string path) => FindComponent<TMP_Text>(path);

        private CanvasGroup FindCanvasGroup(string path) => FindComponent<CanvasGroup>(path);

        private T FindComponent<T>(string path) where T : Component
        {
            Transform found = transform.Find(path);
            return found != null ? found.GetComponent<T>() : null;
        }

        private Image ResolveOrCreateImage(string name, Transform parent, Color color)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null && existing != null)
            {
                if (existing.GetComponent<CanvasRenderer>() == null)
                    existing.gameObject.AddComponent<CanvasRenderer>();
                image = existing.gameObject.AddComponent<Image>();
            }

            if (image == null)
                image = PlayerMenuUiFactory.CreateImage(name, parent, color);

            image.color = color;
            return image;
        }

        private TMP_Text ResolveOrCreateText(
            string name,
            Transform parent,
            string value,
            float size,
            TextAlignmentOptions alignment,
            bool wrap)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (text == null)
                text = PlayerMenuUiFactory.CreateText(name, parent, value, size, alignment, wrap);

            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = wrap;
            text.raycastTarget = false;
            return text;
        }

        private Button ResolveOrCreateButton(string name, Transform parent, string label, Color color)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            Button button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
                button = PlayerMenuUiFactory.CreateButton(name, parent, label, color);

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                button.targetGraphic = image;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = label;
            return button;
        }

        private GameObject ResolveOrCreateGameObject(string name, Transform parent, params Type[] components)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null)
            {
                for (int i = 0; components != null && i < components.Length; i++)
                {
                    if (components[i] != null && existing.GetComponent(components[i]) == null)
                        existing.gameObject.AddComponent(components[i]);
                }

                return existing.gameObject;
            }

            GameObject created = new GameObject(name, components);
            created.transform.SetParent(parent, false);
            return created;
        }

        private GameObject ResolveOrCreateRowStripeBackground(Transform viewport, RectTransform rowsRoot)
        {
            Transform existing = rowsRoot != null ? rowsRoot.Find("RowStripeBackground") : null;
            if (existing == null && viewport != null)
                existing = viewport.Find("RowStripeBackground");

            GameObject stripeObject;
            if (existing != null)
            {
                stripeObject = existing.gameObject;
                if (rowsRoot != null && existing.parent != rowsRoot)
                    existing.SetParent(rowsRoot, false);
            }
            else
            {
                stripeObject = new GameObject(
                    "RowStripeBackground",
                    typeof(RectTransform),
                    typeof(InventoryRowStripeBackground));
                stripeObject.transform.SetParent(rowsRoot != null ? rowsRoot : viewport, false);
            }

            if (stripeObject.GetComponent<RectTransform>() == null)
                stripeObject.AddComponent<RectTransform>();
            if (stripeObject.GetComponent<InventoryRowStripeBackground>() == null)
                stripeObject.AddComponent<InventoryRowStripeBackground>();

            return stripeObject;
        }

        private void EnsureRowStripeBackgroundAttached()
        {
            if (itemRowsRoot == null || itemScrollRect == null)
                return;

            GameObject stripeObject = ResolveOrCreateRowStripeBackground(itemScrollRect.transform, itemRowsRoot);
            rowStripeBackground = stripeObject.GetComponent<InventoryRowStripeBackground>();
            RectTransform stripeRect = stripeObject.transform as RectTransform;
            if (stripeRect != null)
            {
                stripeRect.anchorMin = new Vector2(0f, 1f);
                stripeRect.anchorMax = new Vector2(1f, 1f);
                stripeRect.pivot = new Vector2(0.5f, 1f);
                stripeRect.anchoredPosition = Vector2.zero;
                stripeRect.sizeDelta = Vector2.zero;
                stripeRect.localScale = Vector3.one;
            }

            LayoutElement stripeLayout = PlayerMenuUiFactory.GetOrAdd<LayoutElement>(stripeObject);
            stripeLayout.ignoreLayout = true;

            LayoutElement rowLayout = itemRowTemplate != null
                ? itemRowTemplate.GetComponent<LayoutElement>()
                : null;
            VerticalLayoutGroup verticalLayout = itemRowsRoot.GetComponent<VerticalLayoutGroup>();
            rowStripeBackground?.ConfigureFromRows(itemRowsRoot, rowLayout, verticalLayout);
            rowStripeBackground?.transform.SetAsFirstSibling();
        }

        private RectTransform EnsureScrollbar(Transform parent)
        {
            GameObject scrollbarObject = ResolveOrCreateGameObject(
                "ItemRowsScrollbar",
                parent,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Scrollbar));
            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(0.02f, 0.03f);
            scrollbarRect.anchorMax = new Vector2(0.075f, 0.97f);
            scrollbarRect.offsetMin = Vector2.zero;
            scrollbarRect.offsetMax = Vector2.zero;

            Image background = scrollbarObject.GetComponent<Image>();
            background.color = new Color(0.02f, 0.025f, 0.04f, 0.6f);
            background.raycastTarget = false;

            RectTransform slideArea = ResolveOrCreateRect("Sliding Area", scrollbarRect);
            PlayerMenuUiFactory.Stretch(slideArea, 5f, 5f, 5f, 5f);

            Image handle = ResolveOrCreateImage("Handle", slideArea, new Color(0.2f, 0.85f, 0.75f, 0.86f));
            PlayerMenuUiFactory.Stretch(handle.rectTransform);
            handle.raycastTarget = true;

            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.transition = Selectable.Transition.ColorTint;
            return scrollbarRect;
        }

        private RectTransform ResolveOrCreateRect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing is RectTransform existingRect)
                return existingRect;

            return PlayerMenuUiFactory.CreateRect(name, parent);
        }

        private void BuildStatusStrip()
        {
            RectTransform strip = ResolveOrCreateRect("StatusStrip", transform);
            PlayerMenuUiFactory.Top(strip, 54f, 48f, 48f, 16f);

            headingLabel = ResolveOrCreateText("Heading", strip, "Inventory", 28f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.Stretch(headingLabel.rectTransform, 0f, 0f, 980f, 0f);

            ramLabel = ResolveOrCreateText("RamLabel", strip, "RAM --", 24f, TextAlignmentOptions.MidlineRight, false);
            PlayerMenuUiFactory.Stretch(ramLabel.rectTransform, 1020f, 0f, 0f, 0f);

            statusLabel = ResolveOrCreateText("StatusLabel", strip, "Ready.", 18f, TextAlignmentOptions.BottomLeft, false);
            PlayerMenuUiFactory.Bottom(statusLabel.rectTransform, 22f, 0f, 0f, -18f);
            statusLabel.color = PlayerMenuUiFactory.Muted;
        }

        private void BuildPanels()
        {
            RectTransform categoryPanel = BuildPanel("CategoryPanel", safeContent, new Vector2(0f, 0.08f), new Vector2(0.22f, 0.94f));
            categoryPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(categoryPanel.gameObject);
            RectTransform listPanel = BuildPanel("ListPanel", safeContent, new Vector2(0.245f, 0.08f), new Vector2(0.72f, 0.94f));
            listPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(listPanel.gameObject);
            RectTransform actionsPanel = BuildPanel("ActionsPanel", safeContent, new Vector2(0.745f, 0.08f), new Vector2(1f, 0.94f));
            actionsPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(actionsPanel.gameObject);

            categoryRail = categoryPanel.GetComponent<InventoryCategoryRail>();
            if (categoryRail == null)
                categoryRail = categoryPanel.gameObject.AddComponent<InventoryCategoryRail>();
            PlayerMenuUiFactory.Stretch(categoryRail.transform as RectTransform, 10f, 10f, 10f, 10f);
            categoryRail.BuildDefaultLayoutForEditor();

            BuildListPanel(listPanel);
            BuildActionsPanel(actionsPanel);
        }

        private RectTransform BuildPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            Image panel = ResolveOrCreateImage(name, parent, PlayerMenuUiFactory.Panel);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.raycastTarget = false;
            return rect;
        }

        private void BuildListPanel(RectTransform listPanel)
        {
            TMP_Text header = ResolveOrCreateText("Header", listPanel, "Item List", 24f, TextAlignmentOptions.MidlineLeft, false);
            PlayerMenuUiFactory.Top(header.rectTransform, 44f, 18f, 18f, 10f);
            categorySummaryLabel = ResolveOrCreateText("CategorySummary", header.transform, "Field - 0 stack(s)", 16f, TextAlignmentOptions.MidlineRight, false);
            PlayerMenuUiFactory.Stretch(categorySummaryLabel.rectTransform, 280f, 0f, 0f, 0f);
            categorySummaryLabel.color = PlayerMenuUiFactory.Muted;

            Image itemListAreaImage = ResolveOrCreateImage("ItemListArea", listPanel, new Color(0.04f, 0.055f, 0.08f, 0.3f));
            RectTransform itemListArea = itemListAreaImage.rectTransform;
            itemListArea.anchorMin = new Vector2(0.025f, 0.06f);
            itemListArea.anchorMax = new Vector2(0.61f, 0.89f);
            itemListArea.offsetMin = Vector2.zero;
            itemListArea.offsetMax = Vector2.zero;
            itemListAreaImage.raycastTarget = false;

            Image detailAreaImage = ResolveOrCreateImage("ItemDetailArea", listPanel, new Color(0.04f, 0.055f, 0.08f, 0.24f));
            RectTransform detailArea = detailAreaImage.rectTransform;
            detailArea.anchorMin = new Vector2(0.635f, 0.06f);
            detailArea.anchorMax = new Vector2(0.975f, 0.89f);
            detailArea.offsetMin = Vector2.zero;
            detailArea.offsetMax = Vector2.zero;
            detailAreaImage.raycastTarget = false;
            detailPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(detailArea.gameObject);

            RectTransform scrollbarRect = EnsureScrollbar(itemListArea);

            GameObject viewport = ResolveOrCreateGameObject(
                "ItemRowsViewport",
                itemListArea,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.095f, 0.03f);
            viewportRect.anchorMax = new Vector2(0.98f, 0.97f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0.04f, 0.055f, 0.08f, 0.45f);
            viewportImage.raycastTarget = true;

            itemRowsRoot = ResolveOrCreateRect("Rows", viewport.transform);
            itemRowsRoot.anchorMin = new Vector2(0f, 1f);
            itemRowsRoot.anchorMax = new Vector2(1f, 1f);
            itemRowsRoot.pivot = new Vector2(0.5f, 1f);
            itemRowsRoot.offsetMin = new Vector2(10f, 0f);
            itemRowsRoot.offsetMax = new Vector2(-10f, 0f);
            VerticalLayoutGroup layout = PlayerMenuUiFactory.GetOrAdd<VerticalLayoutGroup>(itemRowsRoot.gameObject);
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = PlayerMenuUiFactory.GetOrAdd<ContentSizeFitter>(itemRowsRoot.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject stripeObject = ResolveOrCreateRowStripeBackground(viewport.transform, itemRowsRoot);
            rowStripeBackground = stripeObject.GetComponent<InventoryRowStripeBackground>();
            RectTransform stripeRect = stripeObject.transform as RectTransform;
            if (stripeRect != null)
            {
                stripeRect.anchorMin = new Vector2(0f, 1f);
                stripeRect.anchorMax = new Vector2(1f, 1f);
                stripeRect.pivot = new Vector2(0.5f, 1f);
                stripeRect.anchoredPosition = Vector2.zero;
                stripeRect.sizeDelta = Vector2.zero;
                stripeRect.localScale = Vector3.one;
            }

            LayoutElement stripeLayout = PlayerMenuUiFactory.GetOrAdd<LayoutElement>(stripeObject);
            stripeLayout.ignoreLayout = true;

            itemScrollRect = viewport.GetComponent<ScrollRect>();
            itemScrollRect.content = itemRowsRoot;
            itemScrollRect.viewport = viewportRect;
            itemScrollRect.horizontal = false;
            itemScrollRect.vertical = true;
            itemScrollRect.movementType = ScrollRect.MovementType.Clamped;
            itemScrollRect.scrollSensitivity = 24f;
            Scrollbar scrollbar = scrollbarRect != null ? scrollbarRect.GetComponent<Scrollbar>() : null;
            itemScrollRect.verticalScrollbar = scrollbar;
            itemScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            RectTransform rowTemplateRect = ResolveOrCreateRect("ItemRowTemplate", itemRowsRoot);
            itemRowTemplate = rowTemplateRect.GetComponent<InventoryItemRowDisplay>();
            if (itemRowTemplate == null)
                itemRowTemplate = rowTemplateRect.gameObject.AddComponent<InventoryItemRowDisplay>();
            LayoutElement rowLayout = PlayerMenuUiFactory.GetOrAdd<LayoutElement>(itemRowTemplate.gameObject);
            rowLayout.preferredHeight = 72f;
            rowLayout.flexibleWidth = 1f;
            itemRowTemplate.BuildDefaultLayoutForEditor();
            itemRowTemplate.gameObject.SetActive(false);
            rowStripeBackground?.ConfigureFromRows(itemRowsRoot, rowLayout, layout);
            rowStripeBackground?.transform.SetAsFirstSibling();
            itemRowsRoot.SetAsLastSibling();

            Image portraitFrame = ResolveOrCreateImage("ItemPortraitFrame", detailArea, new Color(0.095f, 0.125f, 0.18f, 0.94f));
            RectTransform portraitFrameRect = portraitFrame.rectTransform;
            portraitFrameRect.anchorMin = new Vector2(0.06f, 0.51f);
            portraitFrameRect.anchorMax = new Vector2(0.94f, 0.98f);
            portraitFrameRect.offsetMin = Vector2.zero;
            portraitFrameRect.offsetMax = Vector2.zero;
            portraitFrame.raycastTarget = false;

            itemPortrait = ResolveOrCreateImage("ItemPortrait", portraitFrame.transform, emptyPortraitColor);
            PlayerMenuUiFactory.Stretch(itemPortrait.rectTransform, 10f, 10f, 10f, 10f);
            itemPortrait.raycastTarget = false;
            itemPortraitGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(itemPortrait.gameObject);

            Image descriptionFrame = ResolveOrCreateImage("ItemDescriptionFrame", detailArea, new Color(0.095f, 0.125f, 0.18f, 0.78f));
            RectTransform descriptionRect = descriptionFrame.rectTransform;
            descriptionRect.anchorMin = new Vector2(0.06f, 0.02f);
            descriptionRect.anchorMax = new Vector2(0.94f, 0.47f);
            descriptionRect.offsetMin = Vector2.zero;
            descriptionRect.offsetMax = Vector2.zero;
            descriptionFrame.raycastTarget = false;

            detailNameLabel = ResolveOrCreateText("DetailName", descriptionFrame.transform, "No Item Selected", 22f, TextAlignmentOptions.TopLeft, false);
            PlayerMenuUiFactory.Top(detailNameLabel.rectTransform, 36f, 12f, 12f, 10f);

            detailBodyLabel = ResolveOrCreateText("DetailBody", descriptionFrame.transform, "Select an item to inspect it.", 18f, TextAlignmentOptions.TopLeft, true);
            detailBodyLabel.rectTransform.anchorMin = new Vector2(0f, 0.32f);
            detailBodyLabel.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            detailBodyLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            detailBodyLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

            detailMetaLabel = ResolveOrCreateText("DetailMeta", descriptionFrame.transform, "", 14f, TextAlignmentOptions.TopLeft, true);
            detailMetaLabel.color = PlayerMenuUiFactory.Muted;
            detailMetaLabel.rectTransform.anchorMin = new Vector2(0f, 0.04f);
            detailMetaLabel.rectTransform.anchorMax = new Vector2(1f, 0.29f);
            detailMetaLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            detailMetaLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);

            emptyListLabel = ResolveOrCreateText("EmptyListLabel", viewport.transform, "No items in this category.", 22f, TextAlignmentOptions.Center, false);
            PlayerMenuUiFactory.Stretch(emptyListLabel.rectTransform, 18f, 18f, 18f, 18f);
            emptyListLabel.color = PlayerMenuUiFactory.Muted;
            emptyListLabel.transform.SetAsLastSibling();
        }

        private void BuildActionsPanel(RectTransform actionsPanel)
        {
            TMP_Text actionsHeader = ResolveOrCreateText("ActionsHeading", actionsPanel, "Actions", 24f, TextAlignmentOptions.Center, false);
            PlayerMenuUiFactory.Top(actionsHeader.rectTransform, 52f, 16f, 16f, 16f);

            actionPromptLabel = ResolveOrCreateText("ActionPrompt", actionsPanel, "Select an item to view actions.", 22f, TextAlignmentOptions.Center, true);
            RectTransform promptRect = actionPromptLabel.rectTransform;
            promptRect.anchorMin = new Vector2(0.08f, 0.32f);
            promptRect.anchorMax = new Vector2(0.92f, 0.66f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            actionPromptLabel.color = PlayerMenuUiFactory.Muted;

            RectTransform actionButtonsRoot = ResolveOrCreateRect("ActionButtonsRoot", actionsPanel);
            actionButtonsRoot.anchorMin = new Vector2(0.08f, 0.12f);
            actionButtonsRoot.anchorMax = new Vector2(0.92f, 0.78f);
            actionButtonsRoot.offsetMin = Vector2.zero;
            actionButtonsRoot.offsetMax = Vector2.zero;
            actionButtonsGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(actionButtonsRoot.gameObject);
            VerticalLayoutGroup actionLayout = PlayerMenuUiFactory.GetOrAdd<VerticalLayoutGroup>(actionButtonsRoot.gameObject);
            actionLayout.padding = new RectOffset(0, 0, 8, 8);
            actionLayout.spacing = 14f;
            actionLayout.childAlignment = TextAnchor.UpperCenter;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = false;

            useButton = ResolveOrCreateButton("UseButton", actionButtonsRoot, "Use", PlayerMenuUiFactory.PanelAlt);
            RectTransform useRect = useButton.transform as RectTransform;
            useRect.sizeDelta = new Vector2(0f, 78f);
            PlayerMenuUiFactory.LayoutElement(useButton.gameObject, -1f, 78f, 1f);
            useButtonLabel = useButton.GetComponentInChildren<TMP_Text>(true);

            dropButton = ResolveOrCreateButton("DropButton", actionButtonsRoot, "Drop", PlayerMenuUiFactory.PanelAlt);
            RectTransform dropRect = dropButton.transform as RectTransform;
            dropRect.sizeDelta = new Vector2(0f, 78f);
            PlayerMenuUiFactory.LayoutElement(dropButton.gameObject, -1f, 78f, 1f);
            dropButtonLabel = dropButton.GetComponentInChildren<TMP_Text>(true);

            refreshButton = ResolveOrCreateButton("RefreshButton", actionButtonsRoot, "Refresh", PlayerMenuUiFactory.PanelAlt);
            RectTransform refreshRect = refreshButton.transform as RectTransform;
            refreshRect.sizeDelta = new Vector2(0f, 64f);
            PlayerMenuUiFactory.LayoutElement(refreshButton.gameObject, -1f, 64f, 1f);
            refreshButtonLabel = refreshButton.GetComponentInChildren<TMP_Text>(true);
            refreshButton.gameObject.SetActive(false);
        }

        private void BuildSectionChrome()
        {
            sectionChrome = safeContent.GetComponent<InventorySectionChrome>();
            if (sectionChrome == null)
                sectionChrome = safeContent.gameObject.AddComponent<InventorySectionChrome>();
            RectTransform category = categoryPanelGroup != null ? categoryPanelGroup.transform as RectTransform : null;
            RectTransform list = listPanelGroup != null ? listPanelGroup.transform as RectTransform : null;
            RectTransform actions = actionsPanelGroup != null ? actionsPanelGroup.transform as RectTransform : null;
            sectionChrome.Configure(category, list, actions);
            sectionChrome.BuildDefaultLayoutForEditor();
        }

        private void EnsureSectionChrome()
        {
            if (safeContent == null)
                return;

            if (sectionChrome == null)
                sectionChrome = safeContent.GetComponent<InventorySectionChrome>();
            if (sectionChrome == null)
                return;

            RectTransform category = categoryPanelGroup != null ? categoryPanelGroup.transform as RectTransform : null;
            RectTransform list = listPanelGroup != null ? listPanelGroup.transform as RectTransform : null;
            RectTransform actions = actionsPanelGroup != null ? actionsPanelGroup.transform as RectTransform : null;
            sectionChrome.Configure(category, list, actions);
            if (sectionChrome.UsesAuthoredFrameLayout)
                sectionChrome.PrepareAuthoredRuntime();
            else if (sectionChrome.HasAuthoringFrames)
                sectionChrome.RefreshFrameRects(force: true);
            else
                sectionChrome.BuildDefaultLayoutForEditor();
            bool sectionBrowsing = phase == InventoryNavigationPhase.CategorySection
                || phase == InventoryNavigationPhase.ListSection;
            sectionChrome.SetActiveSection(ResolveFocusedSection(), sectionBrowsing);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
