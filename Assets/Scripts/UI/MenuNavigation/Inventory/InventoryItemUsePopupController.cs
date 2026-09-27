using System;
using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public enum InventoryItemUsePopupMode
    {
        Hidden = 0,
        TargetBrowse = 1,
        Result = 2,
        AoeConfirm = 3,
        SelfConfirm = 4,
        Inspect = 5,
    }

    /// <summary>
    /// Shared Inventory Use/Inspect shell: party_target, party_aoe_confirm, self_confirm, item_inspect.
    /// Invalid Use keeps consumable ceremonies open in their confirm/browse mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryItemUsePopupController : MonoBehaviour
    {
        public event Action UseRequested;
        public event Action CancelRequested;
        public event Action ContinueRequested;
        public event Action<int> TargetIndexChanged;

        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text effectLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Image dialoguePanel;

        [Header("Targets")]
        [SerializeField] private RectTransform targetRowRoot;
        [SerializeField] private InventoryItemUseTargetSlot slotTemplate;
        [Tooltip("Authored selection chrome under UsePopupPanel; moves with Up/Down nav.")]
        [SerializeField] private RectTransform selectionFrame;
        [Tooltip("Full frame around all party slots for party_aoe_confirm.")]
        [SerializeField] private RectTransform partyGroupFrame;

        [Header("Self Confirm")]
        [SerializeField] private RectTransform selfConfirmRoot;
        [SerializeField] private TMP_Text favorStatusLabel;
        [SerializeField] private TMP_Text favorApplyLabel;

        [Header("Inspect")]
        [SerializeField] private RectTransform inspectRoot;
        [SerializeField] private Image inspectPortrait;
        [SerializeField] private TMP_Text inspectBodyLabel;

        [Header("Presentation")]
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private CompactStarRatingArtSO compactStarRatingArt;
        [SerializeField] private CycleIconPaletteSO cycleIconPalette;
        [SerializeField] private PartyTabMotionProfileSO motionProfile;
        [SerializeField] private float healTweenDuration = 0.55f;

        private readonly List<InventoryItemUseTargetSlot> slots = new List<InventoryItemUseTargetSlot>(3);
        private readonly List<PlayerRosterEntry> targets = new List<PlayerRosterEntry>(3);
        private readonly List<ChainlingVitalsDisplay> vitalsBySlot = new List<ChainlingVitalsDisplay>(3);
        private InventoryItemUsePopupMode mode = InventoryItemUsePopupMode.Hidden;
        private InventoryItemCeremonyId activeCeremony = InventoryItemCeremonyId.None;
        private int selectedIndex;
        private Tween healTween;
        private Sequence multiHealSequence;
        private Tween selectionFrameMoveTween;
        private Tween selectionFrameScaleTween;
        private Vector2 authoredSelectionFrameSize;
        private bool capturedSelectionFrameSize;
        private string itemDisplayName = "";

        public InventoryItemUsePopupMode Mode => mode;
        public InventoryItemCeremonyId ActiveCeremony => activeCeremony;
        public bool IsOpen => mode != InventoryItemUsePopupMode.Hidden;
        public int SelectedIndex => selectedIndex;
        public int TargetCount => targets.Count;

        public PlayerRosterEntry SelectedTarget =>
            targets.Count == 0 ? null : targets[Mathf.Clamp(selectedIndex, 0, targets.Count - 1)];

        public void ConfigureMotion(PartyTabMotionProfileSO profile)
        {
            if (profile != null)
                motionProfile = profile;
        }

        public void ConfigurePresentation(
            TypeIconPaletteSO typeIcons,
            CompactStarRatingArtSO starArt,
            CycleIconPaletteSO cyclePalette)
        {
            if (typeIcons != null)
                typeIconPalette = typeIcons;
            if (starArt != null)
                compactStarRatingArt = starArt;
            if (cyclePalette != null)
                cycleIconPalette = cyclePalette;
        }

        public void WarmPresentation()
        {
            typeIconPalette?.WarmPresentation();
            compactStarRatingArt?.WarmPresentation();
            cycleIconPalette?.WarmPresentation();
            if (slotTemplate != null)
                slotTemplate.ConfigurePresentation(typeIconPalette, compactStarRatingArt, cycleIconPalette);
        }

        /// <summary>
        /// Editor authoring entry: scaffold missing nodes, wire serialized refs, leave root
        /// active so layout/message/slot template can be edited on InventoryView.prefab.
        /// </summary>
        public void EnsureAuthoringLayoutForEditor(RectTransform parent)
        {
            EnsureDefaultLayout(parent, hideWhenDone: Application.isPlaying);
            if (!Application.isPlaying && root != null)
            {
                root.gameObject.SetActive(true);
                if (rootGroup != null)
                {
                    rootGroup.alpha = 1f;
                    rootGroup.interactable = false;
                    rootGroup.blocksRaycasts = false;
                }
            }
        }

        public void EnsureDefaultLayout(RectTransform parent)
        {
            EnsureDefaultLayout(parent, hideWhenDone: true);
        }

        public void EnsureDefaultLayout(RectTransform parent, bool hideWhenDone)
        {
            ResolveAuthoredReferencesFromHierarchy(parent);

            // Prefer authored prefab hierarchy. Only create missing nodes; never rewrite
            // TargetRow / DialoguePanel / slot rects or layout-group tuning at runtime.
            bool createdRoot = false;
            if (root == null)
            {
                root = PlayerMenuUiFactory.CreateRect("UsePopupRoot", parent != null ? parent : transform as RectTransform);
                PlayerMenuUiFactory.Stretch(root);
                Image dim = PlayerMenuUiFactory.GetOrAdd<Image>(root.gameObject);
                dim.color = new Color(0.02f, 0.04f, 0.07f, 0.72f);
                dim.raycastTarget = true;
                rootGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(root.gameObject);
                createdRoot = true;
            }
            else if (rootGroup == null)
            {
                rootGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(root.gameObject);
            }

            // Full-stretch only when we just scaffolded the root (runtime fallback).
            if (createdRoot)
                PlayerMenuUiFactory.Stretch(root);

            RectTransform panel = root.Find("UsePopupPanel") as RectTransform;
            if (panel == null)
            {
                panel = PlayerMenuUiFactory.CreateRect("UsePopupPanel", root);
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(920f, 720f);
                Image bg = PlayerMenuUiFactory.GetOrAdd<Image>(panel.gameObject);
                bg.color = PlayerMenuUiFactory.Panel;
            }

            EnsurePopupThemeBackground(panel);

            if (titleLabel == null)
            {
                titleLabel = FindText(panel, "Title");
                if (titleLabel == null)
                {
                    titleLabel = PlayerMenuUiFactory.CreateText("Title", panel, "Use Item", 34f, TextAlignmentOptions.Center, false);
                    PlayerMenuUiFactory.Top(titleLabel.rectTransform, 48f, 24f, 24f, 18f);
                }
            }

            if (effectLabel == null)
                effectLabel = FindText(panel, "EffectLabel");
            if (effectLabel == null)
            {
                effectLabel = PlayerMenuUiFactory.CreateText(
                    "EffectLabel",
                    panel,
                    "",
                    22f,
                    TextAlignmentOptions.Center,
                    true);
                RectTransform effectRect = effectLabel.rectTransform;
                effectRect.anchorMin = new Vector2(0f, 1f);
                effectRect.anchorMax = new Vector2(1f, 1f);
                effectRect.pivot = new Vector2(0.5f, 1f);
                effectRect.anchoredPosition = new Vector2(0f, -78f);
                effectRect.sizeDelta = new Vector2(-48f, 48f);
                effectLabel.color = PlayerMenuUiFactory.Muted;
            }

            EnsureSelfConfirmBody(panel);
            EnsureInspectBody(panel);

            // Legacy hint node — hide; dialogue lives in Message.
            TMP_Text hint = FindText(panel, "Hint");
            if (hint != null)
                hint.gameObject.SetActive(false);

            if (targetRowRoot == null)
                targetRowRoot = panel.Find("TargetRow") as RectTransform;

            bool createdTargetRow = false;
            if (targetRowRoot == null)
            {
                targetRowRoot = PlayerMenuUiFactory.CreateRect("TargetRow", panel);
                PlayerMenuUiFactory.Stretch(targetRowRoot, 28f, 140f, 28f, 150f);
                createdTargetRow = true;
            }

            HorizontalLayoutGroup legacyRow = targetRowRoot.GetComponent<HorizontalLayoutGroup>();
            if (legacyRow != null)
            {
                legacyRow.enabled = false;
                DestroyUnityObject(legacyRow);
            }

            VerticalLayoutGroup column = targetRowRoot.GetComponent<VerticalLayoutGroup>();
            if (column == null)
            {
                column = targetRowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                column.spacing = 14f;
                column.childAlignment = TextAnchor.UpperCenter;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
                column.padding = new RectOffset(0, 0, 8, 8);
            }
            else if (createdTargetRow)
            {
                // Defaults only for freshly scaffolded TargetRow.
                column.spacing = 14f;
                column.childAlignment = TextAnchor.UpperCenter;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
                column.padding = new RectOffset(0, 0, 8, 8);
            }

            if (slotTemplate == null)
            {
                Transform existingTemplate = targetRowRoot.Find("TargetSlotTemplate");
                if (existingTemplate != null)
                    slotTemplate = existingTemplate.GetComponent<InventoryItemUseTargetSlot>()
                        ?? existingTemplate.gameObject.AddComponent<InventoryItemUseTargetSlot>();
                if (slotTemplate == null)
                {
                    RectTransform slotRoot = PlayerMenuUiFactory.CreateRect("TargetSlotTemplate", targetRowRoot);
                    slotTemplate = slotRoot.gameObject.AddComponent<InventoryItemUseTargetSlot>();
                }
            }

            slotTemplate.ConfigurePresentation(typeIconPalette, compactStarRatingArt, cycleIconPalette);
            // Scaffold missing children only; do not rewrite authored sizes/positions.
            slotTemplate.EnsureMissingLayoutForEditor();
            if (Application.isPlaying || hideWhenDone)
                slotTemplate.gameObject.SetActive(false);

            if (selectionFrame == null)
            {
                Transform frameTransform = panel.Find("TargetSlotFrame");
                if (frameTransform != null)
                    selectionFrame = frameTransform as RectTransform;
            }

            if (selectionFrame != null)
            {
                // Snapshot authored size before any play-mode alignment can mutate it.
                if (!capturedSelectionFrameSize)
                {
                    authoredSelectionFrameSize = selectionFrame.sizeDelta;
                    capturedSelectionFrameSize = true;
                }
                else
                {
                    selectionFrame.sizeDelta = authoredSelectionFrameSize;
                }

                selectionFrame.gameObject.SetActive(false);
                selectionFrame.localScale = Vector3.one;
                selectionFrame.SetAsLastSibling();
            }

            if (partyGroupFrame == null)
            {
                Transform groupTransform = panel.Find("PartyGroupFrame");
                if (groupTransform != null)
                    partyGroupFrame = groupTransform as RectTransform;
            }

            if (partyGroupFrame == null)
            {
                partyGroupFrame = PlayerMenuUiFactory.CreateRect("PartyGroupFrame", panel);
                Image groupImage = PlayerMenuUiFactory.GetOrAdd<Image>(partyGroupFrame.gameObject);
                groupImage.color = new Color(0.69803923f, 0.7294118f, 0.5647059f, 0.85f);
                groupImage.raycastTarget = false;
                partyGroupFrame.sizeDelta = new Vector2(860f, 420f);
            }

            partyGroupFrame.gameObject.SetActive(false);

            RectTransform dialogueRoot = panel.Find("DialoguePanel") as RectTransform;
            if (dialogueRoot == null && messageLabel != null)
                dialogueRoot = messageLabel.transform.parent as RectTransform;

            if (dialogueRoot == null || dialogueRoot == panel)
            {
                dialogueRoot = panel.Find("DialoguePanel") as RectTransform;
                if (dialogueRoot == null)
                {
                    dialogueRoot = PlayerMenuUiFactory.CreateRect("DialoguePanel", panel);
                    PlayerMenuUiFactory.Bottom(dialogueRoot, 120f, 28f, 28f, 28f);
                    dialoguePanel = PlayerMenuUiFactory.GetOrAdd<Image>(dialogueRoot.gameObject);
                    dialoguePanel.color = new Color(0.1f, 0.12f, 0.14f, 1f);
                }
            }

            if (dialoguePanel == null && dialogueRoot != null)
                dialoguePanel = dialogueRoot.GetComponent<Image>();

            if (messageLabel == null)
                messageLabel = FindText(dialogueRoot, "Message") ?? FindText(panel, "Message");

            if (messageLabel == null)
            {
                messageLabel = PlayerMenuUiFactory.CreateText(
                    "Message",
                    dialogueRoot != null ? dialogueRoot : panel,
                    InventoryItemUseMessageUtility.FormatChooseTarget("Item"),
                    26f,
                    TextAlignmentOptions.Center,
                    true);
                PlayerMenuUiFactory.Stretch(messageLabel.rectTransform, 16f, 16f, 16f, 16f);
            }

            // Remove legacy on-screen action buttons (input-only confirm/cancel).
            Transform buttons = panel.Find("Buttons");
            if (buttons != null)
                buttons.gameObject.SetActive(false);

            if (hideWhenDone)
                HideImmediate();
        }

        /// <summary>
        /// PlayerMenu CRT scanlines on UsePopupPanel only (behind rows/dialogue).
        /// Reuses <see cref="PlayerMenuBackgroundController"/> + SCANLINE_FRAME_PMROOT settings.
        /// </summary>
        public void ConfigurePopupScanlines(Texture scanlineTexture, bool enabled = true)
        {
            RectTransform panel = root != null
                ? root.Find("UsePopupPanel") as RectTransform
                : transform.Find("UsePopupRoot/UsePopupPanel") as RectTransform;
            if (panel == null)
                return;

            PlayerMenuBackgroundController theme = EnsurePopupThemeBackground(panel);
            if (theme == null)
                return;

            // Match main_menu PlayerMenuBackground overrides (tiling Y=1, stronger alpha).
            theme.ConfigureScanlines(
                enabled,
                scanlineTexture,
                color: new Color(1f, 1f, 1f, 0.647f),
                tiling: new Vector2(1f, 1f),
                scrollDirection: Vector2.down,
                uvSpeed: 0.05f);
            theme.SetBaseTintBlocksRaycasts(true);
        }

        public PlayerMenuBackgroundController EnsurePopupThemeBackground(RectTransform panel)
        {
            if (panel == null)
                return null;

            Transform themeTransform = panel.Find("PopupThemeBackground");
            PlayerMenuBackgroundController theme = themeTransform != null
                ? themeTransform.GetComponent<PlayerMenuBackgroundController>()
                : null;

            if (theme == null)
            {
                GameObject themeRoot = new GameObject(
                    "PopupThemeBackground",
                    typeof(RectTransform),
                    typeof(PlayerMenuBackgroundController));
                themeRoot.transform.SetParent(panel, false);
                theme = themeRoot.GetComponent<PlayerMenuBackgroundController>();
            }

            theme.BuildDefaultLayoutForEditor();
            theme.transform.SetAsFirstSibling();

            // Absorb legacy solid PopupBackground into BaseTint, then hide the duplicate.
            Transform legacy = panel.Find("PopupBackground");
            if (legacy != null && legacy != theme.transform)
            {
                Image legacyImage = legacy.GetComponent<Image>();
                if (legacyImage != null && theme.BaseTintImage != null)
                {
                    // Prefer authored popup tint when theme still has the menu-root default.
                    Color defaultMenuTint = new Color(0.02f, 0.03f, 0.06f, 0.5f);
                    if (ApproximatelyColor(theme.BaseTintImage.color, defaultMenuTint))
                        theme.ConfigureBaseTint(legacyImage.color, blocksRaycasts: true);
                }

                legacy.gameObject.SetActive(false);
            }

            // Panel should still catch clicks (legacy PopupBackground was a raycast blocker).
            theme.SetBaseTintBlocksRaycasts(true);

            // Clip BaseTint + ScanlineOverlay to UsePopupPanel's 9-slice silhouette.
            EnsureThemeMaskedToPanelShape(panel, theme);

            return theme;
        }

        /// <summary>
        /// Stencil-mask PopupThemeBackground children to the panel Image sprite
        /// (e.g. 9sliceborder_blank_0 / Sliced / PPU multiplier).
        /// </summary>
        private static void EnsureThemeMaskedToPanelShape(
            RectTransform panel,
            PlayerMenuBackgroundController theme)
        {
            if (panel == null || theme == null)
                return;

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage == null || panelImage.sprite == null)
                return;

            Image maskImage = theme.GetComponent<Image>();
            if (maskImage == null)
                maskImage = theme.gameObject.AddComponent<Image>();

            maskImage.sprite = panelImage.sprite;
            maskImage.type = panelImage.type;
            maskImage.pixelsPerUnitMultiplier = panelImage.pixelsPerUnitMultiplier;
            maskImage.fillCenter = panelImage.fillCenter;
            maskImage.preserveAspect = panelImage.preserveAspect;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            maskImage.maskable = true;

            Mask mask = theme.GetComponent<Mask>();
            if (mask == null)
                mask = theme.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            if (theme.BaseTintImage != null)
                theme.BaseTintImage.maskable = true;
            if (theme.ScanlineOverlay != null)
                theme.ScanlineOverlay.maskable = true;
        }

        private static bool ApproximatelyColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f
                && Mathf.Abs(a.a - b.a) < 0.002f;
        }

        public void ResolveAuthoredReferencesFromHierarchy(RectTransform preferredParent = null)
        {
            if (root == null)
            {
                Transform found = transform.Find("UsePopupRoot");
                if (found == null && preferredParent != null)
                    found = preferredParent.Find("UsePopupRoot");
                if (found == null)
                    found = transform.parent != null ? transform.parent.Find("UsePopupRoot") : null;
                root = found as RectTransform;
            }

            if (root != null && rootGroup == null)
                rootGroup = root.GetComponent<CanvasGroup>();

            RectTransform panel = root != null ? root.Find("UsePopupPanel") as RectTransform : null;
            if (panel == null)
                return;

            if (titleLabel == null)
                titleLabel = FindText(panel, "Title");
            if (effectLabel == null)
                effectLabel = FindText(panel, "EffectLabel");
            if (targetRowRoot == null)
                targetRowRoot = panel.Find("TargetRow") as RectTransform;
            if (slotTemplate == null && targetRowRoot != null)
            {
                Transform template = targetRowRoot.Find("TargetSlotTemplate");
                if (template != null)
                    slotTemplate = template.GetComponent<InventoryItemUseTargetSlot>();
            }

            if (selectionFrame == null && panel != null)
            {
                Transform frameTransform = panel.Find("TargetSlotFrame");
                if (frameTransform != null)
                    selectionFrame = frameTransform as RectTransform;
            }

            if (partyGroupFrame == null && panel != null)
            {
                Transform groupTransform = panel.Find("PartyGroupFrame");
                if (groupTransform != null)
                    partyGroupFrame = groupTransform as RectTransform;
            }

            if (selfConfirmRoot == null && panel != null)
                selfConfirmRoot = panel.Find("SelfConfirmBody") as RectTransform;
            if (favorStatusLabel == null && selfConfirmRoot != null)
                favorStatusLabel = FindText(selfConfirmRoot, "FavorStatus");
            if (favorApplyLabel == null && selfConfirmRoot != null)
                favorApplyLabel = FindText(selfConfirmRoot, "FavorApply");

            if (inspectRoot == null && panel != null)
                inspectRoot = panel.Find("InspectBody") as RectTransform;
            if (inspectPortrait == null && inspectRoot != null)
            {
                Transform portrait = inspectRoot.Find("InspectPortrait");
                if (portrait != null)
                    inspectPortrait = portrait.GetComponent<Image>();
            }

            if (inspectBodyLabel == null && inspectRoot != null)
                inspectBodyLabel = FindText(inspectRoot, "InspectBodyText");

            RectTransform dialogueRoot = panel.Find("DialoguePanel") as RectTransform;
            if (dialoguePanel == null && dialogueRoot != null)
                dialoguePanel = dialogueRoot.GetComponent<Image>();
            if (messageLabel == null)
            {
                if (dialogueRoot != null)
                    messageLabel = FindText(dialogueRoot, "Message");
                if (messageLabel == null)
                    messageLabel = FindText(panel, "Message");
            }
        }

        private static TMP_Text FindText(Transform parent, string childName)
        {
            if (parent == null)
                return null;
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        public void Open(
            string itemName,
            IReadOnlyList<PlayerRosterEntry> partyTargets,
            SpeciesDatabaseSO speciesDatabase,
            int initialIndex = 0)
        {
            OpenCeremony(new InventoryItemCeremonyContext
            {
                Ceremony = InventoryItemCeremonyId.PartyTarget,
                ItemDisplayName = itemName,
                EffectLabel = "",
                PartyTargets = partyTargets,
                SpeciesDatabase = speciesDatabase,
            });
        }

        public void OpenCeremony(InventoryItemCeremonyContext context)
        {
            if (context == null)
                return;

            KillHealTween();
            KillSelectionFrameTweens();

            gameObject.SetActive(true);
            EnsureDefaultLayout(transform as RectTransform, hideWhenDone: false);

            activeCeremony = context.Ceremony;
            itemDisplayName = string.IsNullOrWhiteSpace(context.ItemDisplayName) ? "Item" : context.ItemDisplayName.Trim();
            targets.Clear();
            vitalsBySlot.Clear();

            if (InventoryItemCeremonyResolver.NeedsPartySlots(activeCeremony) && context.PartyTargets != null)
            {
                for (int i = 0; i < context.PartyTargets.Count; i++)
                {
                    PlayerRosterEntry entry = context.PartyTargets[i];
                    if (entry?.snapshot == null || string.IsNullOrWhiteSpace(entry.snapshot.backendChainlingId))
                        continue;
                    targets.Add(entry);
                }
            }

            if (titleLabel != null)
            {
                titleLabel.text = activeCeremony == InventoryItemCeremonyId.ItemInspect
                    ? $"Inspect {itemDisplayName}"
                    : $"Use {itemDisplayName}";
            }

            SetEffectLabel(context.EffectLabel);
            SetBodyVisibility(activeCeremony);

            switch (activeCeremony)
            {
                case InventoryItemCeremonyId.PartyTarget:
                    SetDialogue(InventoryItemUseMessageUtility.FormatChooseTarget(itemDisplayName));
                    RebuildSlots(context.SpeciesDatabase);
                    selectedIndex = 0;
                    mode = InventoryItemUsePopupMode.TargetBrowse;
                    break;
                case InventoryItemCeremonyId.PartyAoeConfirm:
                    SetDialogue(InventoryItemUseMessageUtility.FormatConfirmPartyAoe(itemDisplayName));
                    RebuildSlots(context.SpeciesDatabase);
                    selectedIndex = 0;
                    mode = InventoryItemUsePopupMode.AoeConfirm;
                    break;
                case InventoryItemCeremonyId.SelfConfirm:
                    ClearPartySlots();
                    BindSelfConfirm(context);
                    SetDialogue(InventoryItemUseMessageUtility.FormatConfirmFavor(itemDisplayName));
                    mode = InventoryItemUsePopupMode.SelfConfirm;
                    break;
                case InventoryItemCeremonyId.ItemInspect:
                    ClearPartySlots();
                    BindInspect(context);
                    SetDialogue(InventoryItemUseMessageUtility.FormatInspectHint());
                    mode = InventoryItemUsePopupMode.Inspect;
                    break;
                default:
                    mode = InventoryItemUsePopupMode.Hidden;
                    SetRootVisible(false);
                    return;
            }

            SetRootVisible(true);
            ForcePopupLayout();
            PlaySlotProgressReveal();
            RefreshSelectionVisuals(animate: false);
            if (activeCeremony == InventoryItemCeremonyId.PartyAoeConfirm)
                AlignPartyGroupFrame();
        }

        private void EnsureSelfConfirmBody(RectTransform panel)
        {
            if (panel == null)
                return;

            if (selfConfirmRoot == null)
                selfConfirmRoot = panel.Find("SelfConfirmBody") as RectTransform;
            if (selfConfirmRoot == null)
            {
                selfConfirmRoot = PlayerMenuUiFactory.CreateRect("SelfConfirmBody", panel);
                PlayerMenuUiFactory.Stretch(selfConfirmRoot, 36f, 150f, 36f, 150f);
            }

            if (favorStatusLabel == null)
                favorStatusLabel = FindText(selfConfirmRoot, "FavorStatus");
            if (favorStatusLabel == null)
            {
                favorStatusLabel = PlayerMenuUiFactory.CreateText(
                    "FavorStatus",
                    selfConfirmRoot,
                    "",
                    24f,
                    TextAlignmentOptions.TopLeft,
                    true);
                PlayerMenuUiFactory.Top(favorStatusLabel.rectTransform, 220f, 8f, 8f, 8f);
            }

            if (favorApplyLabel == null)
                favorApplyLabel = FindText(selfConfirmRoot, "FavorApply");
            if (favorApplyLabel == null)
            {
                favorApplyLabel = PlayerMenuUiFactory.CreateText(
                    "FavorApply",
                    selfConfirmRoot,
                    "",
                    24f,
                    TextAlignmentOptions.TopLeft,
                    true);
                PlayerMenuUiFactory.Bottom(favorApplyLabel.rectTransform, 220f, 8f, 8f, 8f);
            }

            selfConfirmRoot.gameObject.SetActive(false);
        }

        private void EnsureInspectBody(RectTransform panel)
        {
            if (panel == null)
                return;

            if (inspectRoot == null)
                inspectRoot = panel.Find("InspectBody") as RectTransform;
            if (inspectRoot == null)
            {
                inspectRoot = PlayerMenuUiFactory.CreateRect("InspectBody", panel);
                PlayerMenuUiFactory.Stretch(inspectRoot, 36f, 150f, 36f, 150f);
            }

            if (inspectPortrait == null)
            {
                Transform portrait = inspectRoot.Find("InspectPortrait");
                if (portrait != null)
                    inspectPortrait = portrait.GetComponent<Image>();
            }

            if (inspectPortrait == null)
            {
                RectTransform portraitRect = PlayerMenuUiFactory.CreateRect("InspectPortrait", inspectRoot);
                portraitRect.anchorMin = new Vector2(0.5f, 1f);
                portraitRect.anchorMax = new Vector2(0.5f, 1f);
                portraitRect.pivot = new Vector2(0.5f, 1f);
                portraitRect.anchoredPosition = new Vector2(0f, -8f);
                portraitRect.sizeDelta = new Vector2(128f, 128f);
                inspectPortrait = PlayerMenuUiFactory.GetOrAdd<Image>(portraitRect.gameObject);
                inspectPortrait.preserveAspect = true;
                inspectPortrait.raycastTarget = false;
            }

            if (inspectBodyLabel == null)
                inspectBodyLabel = FindText(inspectRoot, "InspectBodyText");
            if (inspectBodyLabel == null)
            {
                inspectBodyLabel = PlayerMenuUiFactory.CreateText(
                    "InspectBodyText",
                    inspectRoot,
                    "",
                    22f,
                    TextAlignmentOptions.TopLeft,
                    true);
                PlayerMenuUiFactory.Stretch(inspectBodyLabel.rectTransform, 8f, 8f, 148f, 8f);
            }

            inspectRoot.gameObject.SetActive(false);
        }

        private void SetBodyVisibility(InventoryItemCeremonyId ceremony)
        {
            bool party = InventoryItemCeremonyResolver.NeedsPartySlots(ceremony);
            if (targetRowRoot != null)
                targetRowRoot.gameObject.SetActive(party);
            if (selfConfirmRoot != null)
                selfConfirmRoot.gameObject.SetActive(ceremony == InventoryItemCeremonyId.SelfConfirm);
            if (inspectRoot != null)
                inspectRoot.gameObject.SetActive(ceremony == InventoryItemCeremonyId.ItemInspect);
            if (selectionFrame != null && ceremony != InventoryItemCeremonyId.PartyTarget)
                selectionFrame.gameObject.SetActive(false);
            if (partyGroupFrame != null)
                partyGroupFrame.gameObject.SetActive(ceremony == InventoryItemCeremonyId.PartyAoeConfirm);
            if (effectLabel != null)
                effectLabel.gameObject.SetActive(ceremony != InventoryItemCeremonyId.None);
        }

        private void BindSelfConfirm(InventoryItemCeremonyContext context)
        {
            if (favorStatusLabel != null)
            {
                favorStatusLabel.text = InventoryItemEffectCopy.FormatFavorStatusBlock(
                    context.CurrentFavorScore,
                    context.LuckStrength01,
                    context.AmpouleFavorPoints,
                    context.AmpouleBattlesRemaining,
                    context.PartyFavorPoints);
            }

            if (favorApplyLabel != null)
            {
                favorApplyLabel.text = InventoryItemEffectCopy.FormatFavorApplyBlock(
                    context.ApplyFavorPoints,
                    context.ApplyFavorBattles,
                    context.ProjectedFavorScore,
                    context.ProjectedLuckStrength01);
            }
        }

        private void BindInspect(InventoryItemCeremonyContext context)
        {
            if (inspectPortrait != null)
            {
                Sprite sprite = context.Presentation != null
                    ? context.Presentation.detailPortrait != null
                        ? context.Presentation.detailPortrait
                        : context.Presentation.rowIcon
                    : null;
                inspectPortrait.sprite = sprite;
                inspectPortrait.enabled = sprite != null;
                if (context.Presentation != null)
                    inspectPortrait.color = context.Presentation.fallbackTint;
                else
                    inspectPortrait.color = Color.white;
            }

            if (inspectBodyLabel != null)
                inspectBodyLabel.text = context.InspectBodyText ?? "";
        }

        private void ClearPartySlots()
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] != null && slots[i] != slotTemplate)
                {
                    slots[i].gameObject.SetActive(false);
                    DestroyUnityObjectImmediate(slots[i].gameObject);
                }
            }

            slots.Clear();
            vitalsBySlot.Clear();
            targets.Clear();
            if (selectionFrame != null)
                selectionFrame.gameObject.SetActive(false);
            if (partyGroupFrame != null)
                partyGroupFrame.gameObject.SetActive(false);
        }

        private void SetEffectLabel(string text)
        {
            if (effectLabel != null)
                effectLabel.text = text ?? "";
        }

        private void AlignPartyGroupFrame()
        {
            if (partyGroupFrame == null || targetRowRoot == null)
                return;

            if (partyGroupFrame.parent != targetRowRoot.parent)
                partyGroupFrame.SetParent(targetRowRoot.parent, false);

            LayoutRebuilder.ForceRebuildLayoutImmediate(targetRowRoot);
            Canvas.ForceUpdateCanvases();

            RectTransform panel = targetRowRoot.parent as RectTransform;
            if (panel == null)
                return;

            Vector3[] corners = new Vector3[4];
            targetRowRoot.GetWorldCorners(corners);
            Vector2 localMin = panel.InverseTransformPoint(corners[0]);
            Vector2 localMax = panel.InverseTransformPoint(corners[2]);
            Vector2 center = (localMin + localMax) * 0.5f;
            Vector2 size = localMax - localMin;
            size.x = Mathf.Max(size.x + 24f, 200f);
            size.y = Mathf.Max(size.y + 24f, 120f);

            partyGroupFrame.anchorMin = new Vector2(0.5f, 0.5f);
            partyGroupFrame.anchorMax = new Vector2(0.5f, 0.5f);
            partyGroupFrame.pivot = new Vector2(0.5f, 0.5f);
            partyGroupFrame.anchoredPosition = center;
            partyGroupFrame.sizeDelta = size;
            partyGroupFrame.gameObject.SetActive(true);
            partyGroupFrame.SetAsLastSibling();
            if (selectionFrame != null)
                selectionFrame.gameObject.SetActive(false);
        }

        private void ForcePopupLayout()
        {
            if (targetRowRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetRowRoot);

            RectTransform panel = root != null ? root.Find("UsePopupPanel") as RectTransform : null;
            if (panel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            Canvas.ForceUpdateCanvases();
        }

        public void ShowInvalidMessage(string message)
        {
            if (activeCeremony == InventoryItemCeremonyId.PartyAoeConfirm)
                mode = InventoryItemUsePopupMode.AoeConfirm;
            else if (activeCeremony == InventoryItemCeremonyId.SelfConfirm)
                mode = InventoryItemUsePopupMode.SelfConfirm;
            else if (activeCeremony == InventoryItemCeremonyId.ItemInspect)
                mode = InventoryItemUsePopupMode.Inspect;
            else
                mode = InventoryItemUsePopupMode.TargetBrowse;

            SetDialogue(message ?? "");
            RefreshSelectionVisuals(false);
        }

        public void PlayHealSuccess(
            int slotIndex,
            float fromFill01,
            float toFill01,
            string hpLabelText,
            string successMessage,
            Action onTweenComplete)
        {
            mode = InventoryItemUsePopupMode.Result;
            SetDialogue(successMessage ?? "");
            KillHealTween();

            InventoryItemUseTargetSlot slot = GetSlot(slotIndex);
            if (slot == null)
            {
                onTweenComplete?.Invoke();
                return;
            }

            slot.ApplyHpVisual(fromFill01, hpLabelText);
            float duration = motionProfile != null ? Mathf.Max(0.2f, motionProfile.HpFillDuration * 2.2f) : healTweenDuration;
            healTween = DOVirtual
                .Float(fromFill01, toFill01, duration, value =>
                {
                    slot.ApplyHpVisual(value, hpLabelText);
                })
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    slot.ApplyHpVisual(toFill01, hpLabelText);
                    onTweenComplete?.Invoke();
                });

            if (slot.HpLabel != null)
            {
                slot.HpLabel.transform.DOKill();
                slot.HpLabel.transform.DOPunchScale(Vector3.one * 0.08f, 0.22f, 1, 0.5f).SetUpdate(true);
            }

            RefreshSelectionVisuals(false);
        }

        public void PlayPartyHealSuccess(
            IReadOnlyList<(int slotIndex, float fromFill01, float toFill01, string hpLabel)> fills,
            string successMessage,
            Action onTweenComplete)
        {
            mode = InventoryItemUsePopupMode.Result;
            SetDialogue(successMessage ?? "");
            KillHealTween();

            if (fills == null || fills.Count == 0)
            {
                onTweenComplete?.Invoke();
                return;
            }

            float duration = motionProfile != null ? Mathf.Max(0.2f, motionProfile.HpFillDuration * 2.2f) : healTweenDuration;
            multiHealSequence = DOTween.Sequence().SetUpdate(true);

            for (int i = 0; i < fills.Count; i++)
            {
                int slotIndex = fills[i].slotIndex;
                float from = fills[i].fromFill01;
                float to = fills[i].toFill01;
                string label = fills[i].hpLabel;
                InventoryItemUseTargetSlot slot = GetSlot(slotIndex);
                if (slot == null)
                    continue;

                slot.ApplyHpVisual(from, label);
                int capturedIndex = slotIndex;
                string capturedLabel = label;
                float capturedTo = to;
                multiHealSequence.Join(
                    DOVirtual
                        .Float(from, to, duration, value =>
                        {
                            InventoryItemUseTargetSlot live = GetSlot(capturedIndex);
                            live?.ApplyHpVisual(value, capturedLabel);
                        })
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            InventoryItemUseTargetSlot live = GetSlot(capturedIndex);
                            live?.ApplyHpVisual(capturedTo, capturedLabel);
                        }));

                if (slot.HpLabel != null)
                {
                    slot.HpLabel.transform.DOKill();
                    slot.HpLabel.transform.DOPunchScale(Vector3.one * 0.08f, 0.22f, 1, 0.5f).SetUpdate(true);
                }
            }

            multiHealSequence.OnComplete(() => onTweenComplete?.Invoke());
            if (partyGroupFrame != null)
                partyGroupFrame.gameObject.SetActive(true);
            RefreshSelectionVisuals(false);
        }

        public void PlayFavorSuccess(string successMessage, Action onComplete = null)
        {
            mode = InventoryItemUsePopupMode.Result;
            SetDialogue(successMessage ?? "");
            onComplete?.Invoke();
            RefreshSelectionVisuals(false);
        }

        public void Close()
        {
            KillHealTween();
            KillSelectionFrameTweens();
            if (selectionFrame != null)
            {
                selectionFrame.DOKill();
                selectionFrame.gameObject.SetActive(false);
                selectionFrame.localScale = Vector3.one;
            }

            if (partyGroupFrame != null)
                partyGroupFrame.gameObject.SetActive(false);

            activeCeremony = InventoryItemCeremonyId.None;
            mode = InventoryItemUsePopupMode.Hidden;
            SetRootVisible(false);
        }

        public bool TryMoveSelection(int delta)
        {
            if (mode != InventoryItemUsePopupMode.TargetBrowse || targets.Count <= 1)
                return false;

            selectedIndex = (selectedIndex + delta + targets.Count) % targets.Count;
            RefreshSelectionVisuals(true);
            TargetIndexChanged?.Invoke(selectedIndex);
            return true;
        }

        public bool TryHandleSubmit()
        {
            if (mode == InventoryItemUsePopupMode.TargetBrowse
                || mode == InventoryItemUsePopupMode.AoeConfirm
                || mode == InventoryItemUsePopupMode.SelfConfirm)
            {
                UseRequested?.Invoke();
                return true;
            }

            if (mode == InventoryItemUsePopupMode.Result || mode == InventoryItemUsePopupMode.Inspect)
            {
                ContinueRequested?.Invoke();
                return true;
            }

            return false;
        }

        public bool TryHandleCancel()
        {
            if (mode == InventoryItemUsePopupMode.Result || mode == InventoryItemUsePopupMode.Inspect)
            {
                ContinueRequested?.Invoke();
                return true;
            }

            if (mode == InventoryItemUsePopupMode.TargetBrowse
                || mode == InventoryItemUsePopupMode.AoeConfirm
                || mode == InventoryItemUsePopupMode.SelfConfirm)
            {
                CancelRequested?.Invoke();
                return true;
            }

            return false;
        }

        public Selectable ResolvePreferredSelectable()
        {
            InventoryItemUseTargetSlot slot = GetSlot(selectedIndex);
            if (slot != null && slot.Button != null)
                return slot.Button;
            return slots.Count > 0 && slots[0] != null ? slots[0].Button : null;
        }

        public ChainlingVitalsDisplay GetVitals(int index)
        {
            if (index < 0 || index >= vitalsBySlot.Count)
                return default;
            return vitalsBySlot[index];
        }

        private void RebuildSlots(SpeciesDatabaseSO speciesDatabase)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] != null && slots[i] != slotTemplate)
                {
                    // Destroy() is deferred — on reopen the layout group still sees old rows
                    // for a frame and TargetSlotFrame aligns to the wrong Y.
                    slots[i].gameObject.SetActive(false);
                    DestroyUnityObjectImmediate(slots[i].gameObject);
                }
            }

            slots.Clear();
            vitalsBySlot.Clear();
            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);

            // Drop any stray runtime clones left under TargetRow (e.g. interrupted open).
            if (targetRowRoot != null)
            {
                for (int i = targetRowRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = targetRowRoot.GetChild(i);
                    if (child == null || child == slotTemplate?.transform || child == selectionFrame)
                        continue;
                    if (child.name.StartsWith("TargetSlot_", StringComparison.Ordinal))
                        DestroyUnityObjectImmediate(child.gameObject);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                PlayerRosterEntry entry = targets[i];
                InventoryItemUseTargetSlot slot = Instantiate(slotTemplate, targetRowRoot);
                slot.name = $"TargetSlot_{i:00}";
                slot.gameObject.SetActive(true);
                slot.ConfigurePresentation(typeIconPalette, compactStarRatingArt, cycleIconPalette);
                // Clones keep authored template layout; never rebuild rects at runtime.

                SpeciesDefinitionSO species = speciesDatabase != null && entry.snapshot != null
                    ? speciesDatabase.GetById(entry.snapshot.speciesId)
                    : null;
                ChainlingVitalsDisplay vitals = ChainlingVitalsUtility.BuildDisplay(entry.snapshot, species);
                vitalsBySlot.Add(vitals);

                slot.Bind(entry, species, typeIconPalette, compactStarRatingArt, cycleIconPalette, motionProfile);
                int captured = i;
                if (slot.Button != null)
                {
                    slot.Button.onClick.RemoveAllListeners();
                    slot.Button.onClick.AddListener(() => HandleSlotClicked(captured));
                }

                slots.Add(slot);
            }

            EnsureSelectionFrameInTargetRow();
        }

        private void PlaySlotProgressReveal()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryItemUseTargetSlot slot = slots[i];
                if (slot != null)
                    slot.PlayProgressReveal();
            }
        }

        /// <summary>
        /// Frame must live under TargetRow (ignoreLayout) so it shares the same layout space
        /// as the slots. Panel-center anchoring cannot line up with an offset TargetRow.
        /// </summary>
        private void EnsureSelectionFrameInTargetRow()
        {
            if (selectionFrame == null || targetRowRoot == null)
                return;

            if (selectionFrame.parent != targetRowRoot)
                selectionFrame.SetParent(targetRowRoot, false);

            LayoutElement layoutElement = selectionFrame.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = selectionFrame.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            CaptureAuthoredSelectionFrameSize();
            selectionFrame.sizeDelta = authoredSelectionFrameSize;
        }

        private void HandleSlotClicked(int index)
        {
            if (mode == InventoryItemUsePopupMode.AoeConfirm)
            {
                UseRequested?.Invoke();
                return;
            }

            if (mode != InventoryItemUsePopupMode.TargetBrowse)
                return;

            if (index == selectedIndex)
            {
                UseRequested?.Invoke();
                return;
            }

            selectedIndex = index;
            RefreshSelectionVisuals(true);
            TargetIndexChanged?.Invoke(selectedIndex);
        }

        private void RefreshSelectionVisuals(bool animate)
        {
            bool showRowSelection = mode == InventoryItemUsePopupMode.TargetBrowse
                || (mode == InventoryItemUsePopupMode.Result && activeCeremony == InventoryItemCeremonyId.PartyTarget);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                    slots[i].SetSelected(showRowSelection && i == selectedIndex, animate);
            }

            if (activeCeremony == InventoryItemCeremonyId.PartyAoeConfirm
                || mode == InventoryItemUsePopupMode.AoeConfirm
                || (mode == InventoryItemUsePopupMode.Result && activeCeremony == InventoryItemCeremonyId.PartyAoeConfirm))
            {
                if (selectionFrame != null)
                    selectionFrame.gameObject.SetActive(false);
                AlignPartyGroupFrame();
                return;
            }

            if (partyGroupFrame != null)
                partyGroupFrame.gameObject.SetActive(false);

            AlignSelectionFrameToSelectedSlot(snap: !(animate && showRowSelection));
        }

        /// <summary>
        /// Open/reopen must snap; Up/Down may tween. Failed measure keeps the frame hidden
        /// so we never flash a previous session's Y.
        /// </summary>
        private void AlignSelectionFrameToSelectedSlot(bool snap)
        {
            AlignSelectionFrame(selectedIndex, animate: !snap);
        }

        private void AlignSelectionFrame(int index, bool animate)
        {
            KillSelectionFrameTweens();
            InventoryItemUseTargetSlot slot = GetSlot(index);
            if (selectionFrame == null)
                return;

            if (slot == null || !IsOpen)
            {
                selectionFrame.gameObject.SetActive(false);
                return;
            }

            EnsureSelectionFrameInTargetRow();

            RectTransform slotRect = slot.RectTransform;
            if (slotRect == null)
            {
                selectionFrame.gameObject.SetActive(false);
                return;
            }

            CaptureAuthoredSelectionFrameSize();
            selectionFrame.sizeDelta = authoredSelectionFrameSize;

            // Same anchors/pivot as the layout-driven slot so positions share TargetRow space.
            selectionFrame.anchorMin = slotRect.anchorMin;
            selectionFrame.anchorMax = slotRect.anchorMax;
            selectionFrame.pivot = slotRect.pivot;

            Vector3 slotScale = slotRect.localScale;
            slotRect.localScale = Vector3.one;
            if (targetRowRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(targetRowRoot);
            Canvas.ForceUpdateCanvases();
            Vector2 targetPos = slotRect.anchoredPosition;
            slotRect.localScale = slotScale;

            float targetScale = slot.SelectedScale;
            float duration = Mathf.Max(0.01f, slot.SelectionTweenDuration);

            selectionFrame.gameObject.SetActive(true);
            selectionFrame.SetAsLastSibling();

            if (!animate || !Application.isPlaying)
            {
                selectionFrame.anchoredPosition = targetPos;
                selectionFrame.localScale = Vector3.one * targetScale;
                return;
            }

            selectionFrameMoveTween = selectionFrame
                .DOAnchorPos(targetPos, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
            selectionFrameScaleTween = selectionFrame
                .DOScale(targetScale, duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void CaptureAuthoredSelectionFrameSize()
        {
            if (capturedSelectionFrameSize || selectionFrame == null)
                return;
            authoredSelectionFrameSize = selectionFrame.sizeDelta;
            capturedSelectionFrameSize = true;
        }

        private void KillSelectionFrameTweens()
        {
            if (selectionFrameMoveTween != null && selectionFrameMoveTween.IsActive())
                selectionFrameMoveTween.Kill();
            if (selectionFrameScaleTween != null && selectionFrameScaleTween.IsActive())
                selectionFrameScaleTween.Kill();
            selectionFrameMoveTween = null;
            selectionFrameScaleTween = null;
        }

        private void SetDialogue(string text)
        {
            if (messageLabel != null)
                messageLabel.text = text ?? "";
        }

        private void SetRootVisible(bool visible)
        {
            // Prefab may leave the UsePopup host inactive; children cannot appear unless the host is on.
            if (visible)
                gameObject.SetActive(true);

            if (root != null)
                root.gameObject.SetActive(visible);
            if (rootGroup != null)
            {
                rootGroup.alpha = visible ? 1f : 0f;
                rootGroup.interactable = visible;
                rootGroup.blocksRaycasts = visible;
            }
        }

        private void HideImmediate()
        {
            mode = InventoryItemUsePopupMode.Hidden;
            SetRootVisible(false);
        }

        private InventoryItemUseTargetSlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count)
                return null;
            return slots[index];
        }

        private void KillHealTween()
        {
            if (healTween != null && healTween.IsActive())
                healTween.Kill();
            healTween = null;

            if (multiHealSequence != null && multiHealSequence.IsActive())
                multiHealSequence.Kill();
            multiHealSequence = null;
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

        private static void DestroyUnityObjectImmediate(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            DestroyImmediate(obj);
        }

        private void OnDestroy()
        {
            KillHealTween();
            KillSelectionFrameTweens();
        }
    }
}
