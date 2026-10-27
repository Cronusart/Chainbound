using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatMoveSelectPanelUGUI : MonoBehaviour
    {
        public enum MoveRowActionKind
        {
            Move,
            Rest,
        }

        public struct MoveRowData
        {
            public MoveRowActionKind actionKind;
            public string label;
            public Sprite typeIcon;
            public bool isUsable;
            public bool isPassive;
            public bool hasPriority;
        }

        public struct MoveDetailData
        {
            public bool isRestAction;
            public string moveName;
            public string apText;
            public string powerText;
            public string accuracyText;
            public string targetingText;
            public string typeRoleLabel;
            public Color roleTextColor;
            public bool hasRoleTextColor;
            public bool isUsable;
            public bool isPassive;
            public bool insufficientAp;
            public bool hasPriority;
            public string modifierSummary;
            public IReadOnlyList<ModifierOverviewRowData> modifierRows;
            public string shortDescription;
            public string readableTypeValue;
            public string readablePowerValue;
            public string readableAccuracyValue;
            public Sprite typeIcon;
        }

        public struct ModifierOverviewRowData
        {
            public string description;
            public RarityTier rarity;
        }

        [Header("References")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform moveListRoot;
        [Tooltip("Fixed clip window for the move list. Assign a Mask/RectMask2D viewport; do not stretch the panel.")]
        [SerializeField] private RectTransform moveListViewport;
        [SerializeField] private ScrollRect moveListScrollRect;
        [SerializeField] private TMP_Text noMovesText;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private RectTransform priorityPillRoot;
        [SerializeField] private Image priorityPillBackground;
        [SerializeField] private TMP_Text priorityPillText;
        [SerializeField] private TMP_Text selectedRoleText;
        [SerializeField] private TMP_Text selectedApText;
        [SerializeField] private TMP_Text selectedPowerText;
        [SerializeField] private TMP_Text selectedAccuracyText;
        [SerializeField] private TMP_Text selectedTargetingText;
        [SerializeField] private TMP_Text selectedModifierText;
        [SerializeField] private RectTransform modifierListRoot;
        [SerializeField] private TMP_Text selectedDescriptionText;
        [SerializeField] private Image selectedTypeIconImage;
        [SerializeField] private TMP_Text footerPromptText;
        [SerializeField] private RectTransform detailsRoot;
        [SerializeField] private CanvasGroup detailsCanvasGroup;
        [SerializeField] private CombatMoveLibraryCardView libraryCardView;

        [Header("Rest Action")]
        [Tooltip("Type-icon sprite shown on the Rest move row and in Rest details.")]
        [SerializeField] private Sprite restTypeIcon;
        [Tooltip("SelectedDescription anchored Y when Rest is selected (bottom-anchored). Raises the copy into empty detail space.")]
        [SerializeField] private float restDescriptionAnchoredPosY = 96f;
        [Tooltip("SelectedDescription height when Rest is selected so the longer copy fits inside the details panel.")]
        [SerializeField] private float restDescriptionHeight = 180f;
        [Header("Reveal Effect")]
        [SerializeField] private ProjectedPanelReveal projectedReveal;
        [SerializeField] private TMPTypewriterReveal[] typewriters;
        [SerializeField] private float typewriterDelay = 0.25f;
        [SerializeField] private bool debugTypewriter = false;

        [Header("Move Rows")]
        [SerializeField] private CombatMoveRowItemUGUI moveRowPrefab;
        [SerializeField] private TMP_FontAsset generatedRowFont;
        [SerializeField] private int visibleMoveRowsBeforeScroll = MoveLoadoutSlotRules.VisibleRowsBeforeScroll;

        [Header("Move Row Selection Arrow / Spacing")]
        [Tooltip("How far row content shifts right when selected to clear space for the selection arrow. Set to 0 to disable displacement.")]
        [SerializeField] private float moveRowSelectionNudgeOffsetX = 16f;
        [Tooltip("When enabled, every row keeps that nudge offset permanently (no visible select nudge). Leave off to nudge only the selected/navigated row.")]
        [SerializeField] private bool moveRowReserveSelectionArrowSpace = false;
        [SerializeField] private float moveRowSelectionNudgeDuration = 0.18f;
        [SerializeField] private Ease moveRowSelectionNudgeEase = Ease.OutBack;
        [SerializeField] private Ease moveRowSelectionReturnEase = Ease.OutCubic;
        [SerializeField] private float moveRowSelectionNudgeScalePeak = 1.02f;
        [SerializeField] private float moveRowSelectionArrowFadeDuration = 0.12f;
        [SerializeField] private float moveRowSelectionArrowScalePeak = 1.1f;
        [SerializeField] private Ease moveRowSelectionArrowEase = Ease.OutBack;
        [SerializeField] private float moveRowSelectionSlideDuration = 0.18f;
        [SerializeField] private Ease moveRowSelectionSlideEase = Ease.OutCubic;

        [Header("Modifier Overview Rows")]
        [SerializeField] private CombatModifierOverviewRowUGUI modifierRowPrefab;
        [SerializeField] private TMP_FontAsset generatedModifierRowFont;

        [Header("Modifier Rarity Colors")]
        [SerializeField] private Color commonRarityColor = new Color(0.72f, 0.76f, 0.80f, 1f);
        [SerializeField] private Color uncommonRarityColor = new Color(0.42f, 0.88f, 0.52f, 1f);
        [SerializeField] private Color rareRarityColor = new Color(0.42f, 0.68f, 0.98f, 1f);
        [SerializeField] private Color epicRarityColor = new Color(0.72f, 0.48f, 0.98f, 1f);
        [SerializeField] private Color legendaryRarityColor = new Color(0.98f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color mythicRarityColor = new Color(0.98f, 0.42f, 0.58f, 1f);

        [Header("Targeting Label Colors")]
        [SerializeField] private Color damagingTargetingColor = new Color(0.95f, 0.38f, 0.38f, 1f);
        [SerializeField] private Color debuffTargetingColor = new Color(0.98f, 0.62f, 0.22f, 1f);
        [SerializeField] private Color neutralTargetingColor = new Color(0.98f, 0.88f, 0.28f, 1f);
        [SerializeField] private Color supportTargetingColor = new Color(0.42f, 0.88f, 0.52f, 1f);

        [Header("Details Unusable State")]
        [SerializeField] private Color detailsMutedTextColor = new Color(0.74f, 0.74f, 0.74f, 0.85f);
        [SerializeField] private Color normalApCostColor = new Color(0.82f, 0.92f, 0.98f, 1f);
        [SerializeField] private Color insufficientApCostColor = new Color(0.98f, 0.76f, 0.22f, 1f);
        [SerializeField] private Color detailsTargetingColor = new Color(0.74f, 0.86f, 0.94f, 1f);

        [Header("Priority Pill")]
        [SerializeField] private Color priorityPillBackgroundColor = new Color(0.86f, 0.62f, 0.18f, 0.92f);
        [SerializeField] private Color priorityPillTextColor = new Color(1f, 0.95f, 0.78f, 1f);
        [SerializeField] private float priorityPillPaddingX = 10f;
        [SerializeField] private float priorityPillHeight = 24f;
        [SerializeField] private float priorityPillGapFromName = 10f;

        [Header("Details Refresh Animation")]
        [SerializeField] private float detailsPopDuration = 0.14f;
        [SerializeField] private float detailsPopScale = 1.025f;
        [SerializeField] private Ease detailsPopEaseOut = Ease.OutQuad;
        [SerializeField] private Ease detailsPopEaseIn = Ease.OutBack;

        [Header("Invalid Move Feedback")]
        [SerializeField] private MonoBehaviour uiSfxBehaviour;
        [SerializeField] private MonoBehaviour audioBridgeBehaviour;
        [SerializeField] private UnityEngine.Object unavailableMoveSoundEvent;
        [SerializeField] private float unavailablePanelNudgeOffsetX = 12f;
        [SerializeField] private float unavailablePanelNudgeDuration = 0.08f;
        [SerializeField] private int unavailablePanelNudgeCycles = 2;

        private const string RestActionLabel = "Rest";
        private const string RestActionDescription =
            "Skip this turn to recover AP for a future move, unless an AP-drain effect interferes.";

        private readonly List<CombatMoveRowItemUGUI> _rows = new List<CombatMoveRowItemUGUI>(12);
        private readonly List<CombatModifierOverviewRowUGUI> _modifierRows = new List<CombatModifierOverviewRowUGUI>(4);
        private Tween _typewriterDelayTween;
        private Tween _detailsPopTween;
        private RectTransform _detailsPopTarget;
        private bool _isRevealing;
        private int _lastSelectedIndex = -1;
        private bool _pendingDetailsPop;
        private bool _authoringColorsCached;
        private Color _authoredNameColor = Color.white;
        private Color _authoredRoleColor = Color.white;
        private Color _authoredApColor = Color.white;
        private Color _authoredPowerColor = Color.white;
        private Color _authoredAccuracyColor = Color.white;
        private Color _authoredTargetingColor = Color.white;
        private Color _authoredModifierColor = Color.white;
        private Color _authoredDescriptionColor = Color.white;
        private Vector2 _authoredPanelAnchoredPosition;
        private bool _panelAnchoredPositionCached;
        private Vector2 _authoredMoveListAnchoredPosition;
        private bool _moveListAnchoredPositionCached;
        private RectTransform _descriptionRect;
        private Vector2 _authoredDescriptionAnchoredPosition;
        private Vector2 _authoredDescriptionSizeDelta;
        private TextAlignmentOptions _authoredDescriptionAlignment;
        private TextOverflowModes _authoredDescriptionOverflow;
        private bool _descriptionLayoutCached;
        private bool _readableDetailsResolved;
        private TMP_Text _readableTypeValue;
        private TMP_Text _readablePowerValue;
        private TMP_Text _readableAccuracyValue;
        private TMP_Text _readableDescription;
        private bool _restDescriptionLayoutActive;
        private Tween _panelNudgeTween;
        private MonoBehaviour _cachedUiSfxBehaviour;
        private MonoBehaviour _cachedAudioBridgeBehaviour;
        private readonly List<GameObject> _hiddenForTargetingPrompt = new List<GameObject>(8);
        // Graphics on the panel root itself (e.g. the device_bottom_panelframe Image) that were disabled
        // while target selection holds the panel open for its prompt. Child objects are covered by
        // _hiddenForTargetingPrompt; the root's own graphics are not, so the frame needs its own list.
        private readonly List<Graphic> _rootGraphicsHiddenForTargetingPrompt = new List<Graphic>(2);

        private void Awake()
        {
            EnsureBuilt();
            EnsureAuthoringColorsCached();
            CacheMoveListAnchoredPosition();
            if (!_isRevealing)
                SetVisible(false);
        }

        private void OnDisable()
        {
            KillTypewriterDelay();
            KillTypewriters();
            KillDetailsPopTween();
            KillPanelNudgeTween(restorePosition: true);
            ResetAllRowSelectionVisuals();
        }

        /// <summary>
        /// Keeps this panel active so the shared targeting label can show, and hides the move-list chrome.
        /// When <paramref name="hidePanelFrame"/> is true (target selection), graphics on the panel root
        /// itself, such as the panel frame image, are hidden too so only the prompt remains.
        /// </summary>
        public void HoldOpenForTargetingPrompt(GameObject promptObject, bool hidePanelFrame = false)
        {
            EnsureBuilt();
            RestoreTargetingPromptHide();
            if (root == null)
                return;

            ProjectedPanelReveal effect = projectedReveal != null ? projectedReveal : GetComponent<ProjectedPanelReveal>();
            if (effect != null)
                effect.CancelHideKeepVisible();
            else
                root.gameObject.SetActive(true);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject child = root.GetChild(i).gameObject;
                bool keep = promptObject != null &&
                    (child == promptObject || promptObject.transform.IsChildOf(child.transform));
                if (keep)
                {
                    child.SetActive(true);
                    CanvasGroup promptGroup = child.GetComponent<CanvasGroup>();
                    if (promptGroup != null)
                        promptGroup.alpha = 1f;
                    continue;
                }

                if (!child.activeSelf)
                    continue;

                child.SetActive(false);
                _hiddenForTargetingPrompt.Add(child);
            }

            if (hidePanelFrame)
                HideRootGraphicsForTargetingPrompt(promptObject);
        }

        private void HideRootGraphicsForTargetingPrompt(GameObject promptObject)
        {
            Graphic[] rootGraphics = root.GetComponents<Graphic>();
            for (int i = 0; i < rootGraphics.Length; i++)
            {
                Graphic graphic = rootGraphics[i];
                if (graphic == null || !graphic.enabled)
                    continue;
                // Never hide the prompt text itself if it happens to live on the panel root.
                if (promptObject == root.gameObject && graphic is TMP_Text)
                    continue;

                graphic.enabled = false;
                _rootGraphicsHiddenForTargetingPrompt.Add(graphic);
            }
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            bool wasHeldForTargetingPrompt = IsHeldForTargetingPrompt;
            if (root == null)
            {
                RestoreTargetingPromptHide();
                return;
            }

            var effect = projectedReveal != null ? projectedReveal : GetComponent<ProjectedPanelReveal>();

            if (!visible && wasHeldForTargetingPrompt)
            {
                // Target selection already hid the panel chrome (frame included). Deactivate the root first,
                // then restore the children/frame underneath it, so leaving target selection does not flash
                // the full move panel back on just to fade it out.
                KillTypewriterDelay();
                KillTypewriters();
                KillDetailsPopTween();
                KillPanelNudgeTween(restorePosition: true);
                ResetAllRowSelectionVisuals();
                _lastSelectedIndex = -1;
                _pendingDetailsPop = false;
                if (effect != null)
                    effect.CancelHideKeepVisible();
                root.gameObject.SetActive(false);
                RestoreTargetingPromptHide();
                return;
            }

            RestoreTargetingPromptHide();

            if (visible)
            {
                _isRevealing = true;
                ApplyDetailsPresentationMode();
                try
                {
                    if (effect != null)
                    {
                        effect.Reveal();
                        ScheduleTypewriters();
                    }
                    else
                    {
                        root.gameObject.SetActive(true);
                        ScheduleTypewriters();
                    }
                }
                finally
                {
                    _isRevealing = false;
                }
            }
            else
            {
                KillTypewriterDelay();
                KillTypewriters();
                KillDetailsPopTween();
                KillPanelNudgeTween(restorePosition: true);
                ResetAllRowSelectionVisuals();
                _lastSelectedIndex = -1;
                _pendingDetailsPop = false;
                if (effect != null)
                    effect.Hide();
                else
                    root.gameObject.SetActive(false);
            }
        }

        private void RestoreTargetingPromptHide()
        {
            for (int i = 0; i < _hiddenForTargetingPrompt.Count; i++)
            {
                if (_hiddenForTargetingPrompt[i] != null)
                    _hiddenForTargetingPrompt[i].SetActive(true);
            }

            _hiddenForTargetingPrompt.Clear();

            for (int i = 0; i < _rootGraphicsHiddenForTargetingPrompt.Count; i++)
            {
                if (_rootGraphicsHiddenForTargetingPrompt[i] != null)
                    _rootGraphicsHiddenForTargetingPrompt[i].enabled = true;
            }

            _rootGraphicsHiddenForTargetingPrompt.Clear();
        }

        private bool IsHeldForTargetingPrompt =>
            _hiddenForTargetingPrompt.Count > 0 || _rootGraphicsHiddenForTargetingPrompt.Count > 0;

        private void ScheduleTypewriters()
        {
            KillTypewriterDelay();
            KillTypewriters();

            var list = ResolveTypewriters();
            if (list == null || list.Count == 0)
            {
                if (debugTypewriter)
                    Debug.Log($"[CombatMoveSelectPanelUGUI] ScheduleTypewriters: no typewriters found (assigned={typewriters?.Length ?? 0}, auto-found=0).");
                return;
            }

            if (debugTypewriter)
                Debug.Log($"[CombatMoveSelectPanelUGUI] ScheduleTypewriters: scheduling {list.Count} typewriter(s) after {typewriterDelay}s delay.");

            foreach (var tw in list)
            {
                if (tw != null && tw.gameObject.activeInHierarchy)
                    tw.ResetToHidden();
            }

            _typewriterDelayTween = DOVirtual.DelayedCall(typewriterDelay, PlayTypewriters, true);
        }

        private System.Collections.Generic.List<TMPTypewriterReveal> ResolveTypewriters()
        {
            var list = new System.Collections.Generic.List<TMPTypewriterReveal>();
            if (typewriters != null)
            {
                foreach (var tw in typewriters)
                    if (IsPanelTypewriter(tw) && !list.Contains(tw)) list.Add(tw);
            }
            if (list.Count == 0 && root != null)
            {
                var found = root.GetComponentsInChildren<TMPTypewriterReveal>(true);
                if (found != null && found.Length > 0)
                {
                    for (int i = 0; i < found.Length; i++)
                    {
                        if (IsPanelTypewriter(found[i]))
                            list.Add(found[i]);
                    }
                    if (debugTypewriter)
                        Debug.Log($"[CombatMoveSelectPanelUGUI] ResolveTypewriters: auto-found {list.Count} TMPTypewriterReveal in children.");
                }
            }
            return list;
        }

        private void PlayTypewriters()
        {
            _typewriterDelayTween = null;
            var list = ResolveTypewriters();
            if (list == null || list.Count == 0) return;

            if (debugTypewriter)
                Debug.Log($"[CombatMoveSelectPanelUGUI] PlayTypewriters: playing {list.Count} typewriter(s).");

            foreach (var tw in list)
            {
                if (tw != null && tw.gameObject.activeInHierarchy)
                    tw.Play();
            }
        }

        private static bool IsPanelTypewriter(TMPTypewriterReveal typewriter)
        {
            return typewriter != null && typewriter.GetComponentInParent<CombatMoveLibraryCardView>(true) == null;
        }

        private void KillTypewriterDelay()
        {
            if (_typewriterDelayTween != null)
            {
                _typewriterDelayTween.Kill();
                _typewriterDelayTween = null;
            }
        }

        private void KillTypewriters()
        {
            var list = ResolveTypewriters();
            if (list == null) return;
            foreach (var tw in list)
                if (tw != null) tw.Kill();
        }

        public void SetTitle(string title)
        {
            EnsureBuilt();
            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(title) ? "Moves" : title;
        }

        public void SetMoveRows(IReadOnlyList<MoveRowData> rows, int selectedIndex)
        {
            EnsureBuilt();

            int count = rows != null ? rows.Count : 0;
            bool selectionChanged = selectedIndex >= 0 && selectedIndex != _lastSelectedIndex;
            _pendingDetailsPop = _lastSelectedIndex >= 0 && selectionChanged && selectedIndex < count;
            _lastSelectedIndex = selectedIndex;

            EnsureRowPool(count);
            var selectionTuning = BuildMoveRowSelectionTuning();
            for (int i = 0; i < _rows.Count; i++)
            {
                bool show = i < count;
                var row = _rows[i];
                if (row == null)
                    continue;

                if (!show)
                {
                    row.ResetSelectionVisuals();
                    row.SetVisible(false);
                    continue;
                }

                row.SetVisible(true);
                var entry = rows[i];
                bool selected = i == selectedIndex;
                row.SetData(
                    entry.label,
                    entry.typeIcon,
                    entry.isUsable,
                    selected,
                    hideTypeIcon: false,
                    entry.hasPriority,
                    entry.isPassive);
                row.ApplySelectionNudge(selected, selectionChanged, selectionTuning);
                if (selected && selectionChanged)
                    row.PlaySelectBounce(selectionTuning);
            }

            if (noMovesText != null)
                noMovesText.gameObject.SetActive(count == 0);

            ConfigureMoveListScroll(count);
            ScrollToRow(selectedIndex, count);
        }

        public void ScrollToRow(int selectedIndex)
        {
            ScrollToRow(selectedIndex, CountVisibleMoveRows());
        }

        public void SetSelectedDetails(MoveDetailData details)
        {
            EnsureBuilt();
            EnsureAuthoringColorsCached();
            ApplyDetailsPresentationMode();

            if (details.isRestAction)
            {
                ApplyRestDetails(details);
                PlayDetailsPopIfLegacyPanelIsShowing();
                return;
            }

            SetMoveStatFieldsVisible(true);
            RestoreDescriptionLayout();
            bool muted = !details.isUsable;
            bool keepReadable = details.isPassive;
            bool muteReadableBody = muted && !keepReadable;
            Color bodyColor = muteReadableBody ? detailsMutedTextColor : _authoredNameColor;
            Color roleColor = muted || details.isPassive
                ? detailsMutedTextColor
                : details.hasRoleTextColor ? details.roleTextColor : _authoredRoleColor;
            Color apColor = details.isPassive
                ? detailsMutedTextColor
                : details.insufficientAp
                ? insufficientApCostColor
                : _authoredApColor;

            if (selectedNameText != null)
            {
                selectedNameText.text = string.IsNullOrEmpty(details.moveName) ? "--" : details.moveName;
                selectedNameText.color = bodyColor;
            }

            SetPriorityPillVisible(details.hasPriority, muted);
            if (selectedRoleText != null)
            {
                selectedRoleText.text = string.IsNullOrEmpty(details.typeRoleLabel) ? "--" : details.typeRoleLabel;
                selectedRoleText.color = roleColor;
            }
            if (selectedApText != null)
            {
                selectedApText.text = string.IsNullOrEmpty(details.apText) ? "AP Cost --" : details.apText;
                selectedApText.color = apColor;
            }
            if (selectedPowerText != null)
            {
                selectedPowerText.text = string.IsNullOrEmpty(details.powerText) ? "Power --" : details.powerText;
                selectedPowerText.color = muted ? detailsMutedTextColor : _authoredPowerColor;
            }
            if (selectedAccuracyText != null)
            {
                selectedAccuracyText.text = string.IsNullOrEmpty(details.accuracyText) ? "Accuracy --" : details.accuracyText;
                selectedAccuracyText.color = muted ? detailsMutedTextColor : _authoredAccuracyColor;
            }
            if (selectedTargetingText != null)
            {
                selectedTargetingText.text = string.IsNullOrEmpty(details.targetingText) ? "Target --" : details.targetingText;
                selectedTargetingText.color = muted ? detailsMutedTextColor : _authoredTargetingColor;
            }
            if (selectedModifierText != null)
            {
                bool hasModifierRows = details.modifierRows != null && details.modifierRows.Count > 0;
                selectedModifierText.enabled = !hasModifierRows;
                if (!hasModifierRows)
                {
                    selectedModifierText.text = string.IsNullOrEmpty(details.modifierSummary)
                        ? "Mods: None"
                        : details.modifierSummary;
                }
                selectedModifierText.color = muteReadableBody ? detailsMutedTextColor : _authoredModifierColor;
            }

            SetModifierOverviewRows(details.modifierRows, muteReadableBody);
            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text = string.IsNullOrEmpty(details.shortDescription) ? "No move description available." : details.shortDescription;
                selectedDescriptionText.color = muteReadableBody ? detailsMutedTextColor : _authoredDescriptionColor;
            }

            if (selectedTypeIconImage != null)
            {
                selectedTypeIconImage.sprite = details.typeIcon;
                selectedTypeIconImage.enabled = details.typeIcon != null;
                var iconColor = selectedTypeIconImage.color;
                iconColor.a = muteReadableBody ? 0.45f : 1f;
                selectedTypeIconImage.color = iconColor;
            }

            ApplyReadableDetails(details);
            PlayDetailsPopIfLegacyPanelIsShowing();
        }

        public void SetLibraryCard(CombatMoveLibraryCardView.Presentation presentation)
        {
            ApplyDetailsPresentationMode();
            ResolveLibraryCard();
            if (libraryCardView != null)
                libraryCardView.Show(presentation);
        }

        public void PlayUnavailableMoveFeedback()
        {
            EnsureBuilt();
            CachePanelAnchoredPosition();

            PlayUnavailableMoveSound();
            PlayPanelNudge();
        }

        public MoveDetailData CreateRestDetailData() => CreateRestDetailData(restTypeIcon);

        public static MoveDetailData CreateRestDetailData(Sprite typeIcon)
        {
            return new MoveDetailData
            {
                isRestAction = true,
                moveName = RestActionLabel,
                shortDescription = RestActionDescription,
                typeIcon = typeIcon,
                isUsable = true,
                insufficientAp = false,
            };
        }

        public MoveRowData CreateRestRowData() => CreateRestRowData(restTypeIcon);

        public static MoveRowData CreateRestRowData(Sprite typeIcon)
        {
            return new MoveRowData
            {
                actionKind = MoveRowActionKind.Rest,
                label = RestActionLabel,
                typeIcon = typeIcon,
                isUsable = true,
            };
        }

        public void SetFooterPrompt(string text)
        {
            EnsureBuilt();
            if (footerPromptText != null)
                footerPromptText.text = string.IsNullOrEmpty(text) ? "Confirm=Select   Cancel=Back   Info=Details" : text;
        }

        public Color ResolveTargetingColor(MoveInstance move) =>
            MoveSelectDisplayUtility.ResolveTargetingColor(
                move,
                damagingTargetingColor,
                debuffTargetingColor,
                neutralTargetingColor,
                supportTargetingColor);

        public Color ResolveRarityColor(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Uncommon:
                    return uncommonRarityColor;
                case RarityTier.Rare:
                    return rareRarityColor;
                case RarityTier.Epic:
                    return epicRarityColor;
                case RarityTier.Legendary:
                    return legendaryRarityColor;
                case RarityTier.Mythic:
                    return mythicRarityColor;
                default:
                    return commonRarityColor;
            }
        }

        public void SetModifierOverviewRows(IReadOnlyList<ModifierOverviewRowData> rows, bool muted = false)
        {
            EnsureBuilt();
            if (modifierListRoot == null)
                return;

            int count = rows != null ? rows.Count : 0;
            EnsureModifierRowPool(count);

            for (int i = 0; i < _modifierRows.Count; i++)
            {
                bool show = i < count;
                var row = _modifierRows[i];
                if (row == null)
                    continue;

                row.SetVisible(show);
                if (!show)
                    continue;

                var entry = rows[i];
                var rarityColor = ResolveRarityColor(entry.rarity);
                if (muted)
                    rarityColor = detailsMutedTextColor;
                row.SetData(
                    entry.description,
                    entry.rarity,
                    rarityColor,
                    muted ? detailsMutedTextColor : (Color?)null);
            }

            modifierListRoot.gameObject.SetActive(count > 0);
            if (selectedModifierText != null && muted)
                selectedModifierText.color = detailsMutedTextColor;
        }

        private void ApplyDetailsPresentationMode()
        {
            EnsureDetailsReferences();
            if (detailsRoot == null)
                return;

            Transform legacy = detailsRoot.Find("Details1");
            Transform library = detailsRoot.Find("Details2");
            if (legacy != null)
                legacy.gameObject.SetActive(false);
            if (library != null)
                library.gameObject.SetActive(true);
        }

        private void PlayDetailsPopIfLegacyPanelIsShowing()
        {
            if (!_pendingDetailsPop)
                return;

            _pendingDetailsPop = false;
            EnsureDetailsReferences();
            Transform legacy = detailsRoot != null ? detailsRoot.Find("Details1") : null;
            if (legacy == null || !legacy.gameObject.activeSelf)
                return;

            PlayDetailsPopAnimation(legacy as RectTransform);
        }

        private void PlayDetailsPopAnimation(RectTransform target)
        {
            if (target == null)
                return;

            KillDetailsPopTween();
            target.localScale = Vector3.one;
            if (detailsCanvasGroup != null)
                detailsCanvasGroup.alpha = 1f;

            _detailsPopTarget = target;
            _detailsPopTween = DOTween.Sequence()
                .SetUpdate(true)
                .Append(target.DOScale(detailsPopScale, detailsPopDuration * 0.55f).SetEase(detailsPopEaseOut))
                .Append(target.DOScale(1f, detailsPopDuration * 0.45f).SetEase(detailsPopEaseIn))
                .OnComplete(ResetDetailsPopTarget)
                .OnKill(ResetDetailsPopTarget);
        }

        private void EnsureDetailsReferences()
        {
            if (detailsRoot == null && root != null)
            {
                var details = root.Find("Details") as RectTransform;
                if (details != null)
                    detailsRoot = details;
            }

            if (detailsCanvasGroup == null && detailsRoot != null)
                detailsCanvasGroup = detailsRoot.GetComponent<CanvasGroup>();
        }

        private void ApplyReadableDetails(MoveDetailData details)
        {
            ResolveReadableDetails();
            SetReadableText(_readableTypeValue, details.readableTypeValue);
            SetReadableText(_readablePowerValue, details.readablePowerValue);
            SetReadableText(_readableAccuracyValue, details.readableAccuracyValue);
            SetReadableText(_readableDescription, details.shortDescription);
        }

        private void ResolveReadableDetails()
        {
            if (_readableDetailsResolved)
                return;

            _readableDetailsResolved = true;
            EnsureDetailsReferences();
            Transform details2 = detailsRoot != null ? detailsRoot.Find("Details2") : null;
            if (details2 == null)
                return;

            _readableTypeValue = FindReadableText(details2, "MoveStats/MoveType/Value");
            _readablePowerValue = FindReadableText(details2, "MoveStats/MovePower/Value");
            _readableAccuracyValue = FindReadableText(details2, "MoveStats/MoveAccuracy/Value");
            _readableDescription = FindReadableText(details2, "MoveDetails/MoveDescription/Description");
        }

        private static TMP_Text FindReadableText(Transform root, string path)
        {
            Transform target = root.Find(path);
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }

        private static void SetReadableText(TMP_Text label, string value)
        {
            if (label == null)
                return;

            label.text = string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        private void ResolveLibraryCard()
        {
            if (libraryCardView != null)
                return;

            EnsureDetailsReferences();
            if (detailsRoot != null)
                libraryCardView = detailsRoot.GetComponentInChildren<CombatMoveLibraryCardView>(true);
        }

        private void KillDetailsPopTween()
        {
            if (_detailsPopTween != null)
            {
                Tween active = _detailsPopTween;
                _detailsPopTween = null;
                active.Kill(false);
            }

            ResetDetailsPopTarget();
        }

        private void ResetDetailsPopTarget()
        {
            if (_detailsPopTarget == null)
                return;

            RectTransform target = _detailsPopTarget;
            _detailsPopTarget = null;
            target.localScale = Vector3.one;
        }

        private void ApplyRestDetails(MoveDetailData details)
        {
            SetMoveStatFieldsVisible(false);
            SetModifierOverviewRows(null);
            SetPriorityPillVisible(false, muted: false);
            ApplyRestDescriptionLayout();

            if (selectedNameText != null)
            {
                selectedNameText.text = string.IsNullOrEmpty(details.moveName) ? RestActionLabel : details.moveName;
                selectedNameText.color = _authoredNameColor;
            }

            Sprite icon = details.typeIcon != null ? details.typeIcon : restTypeIcon;
            if (selectedTypeIconImage != null)
            {
                selectedTypeIconImage.gameObject.SetActive(icon != null);
                selectedTypeIconImage.sprite = icon;
                selectedTypeIconImage.enabled = icon != null;
                var iconColor = selectedTypeIconImage.color;
                iconColor.a = 1f;
                selectedTypeIconImage.color = iconColor;
            }

            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text = string.IsNullOrEmpty(details.shortDescription)
                    ? RestActionDescription
                    : details.shortDescription;
                selectedDescriptionText.color = _authoredDescriptionColor;
            }

            ApplyReadableDetails(details);
        }

        private void CacheDescriptionLayout()
        {
            if (_descriptionLayoutCached || selectedDescriptionText == null)
                return;

            _descriptionRect = selectedDescriptionText.rectTransform;
            _authoredDescriptionAnchoredPosition = _descriptionRect.anchoredPosition;
            _authoredDescriptionSizeDelta = _descriptionRect.sizeDelta;
            _authoredDescriptionAlignment = selectedDescriptionText.alignment;
            _authoredDescriptionOverflow = selectedDescriptionText.overflowMode;
            _descriptionLayoutCached = true;
        }

        private void ApplyRestDescriptionLayout()
        {
            CacheDescriptionLayout();
            if (!_descriptionLayoutCached || _descriptionRect == null || selectedDescriptionText == null)
                return;

            _descriptionRect.anchoredPosition = new Vector2(
                _authoredDescriptionAnchoredPosition.x,
                restDescriptionAnchoredPosY);
            _descriptionRect.sizeDelta = new Vector2(
                _authoredDescriptionSizeDelta.x,
                Mathf.Max(restDescriptionHeight, _authoredDescriptionSizeDelta.y));
            selectedDescriptionText.alignment = TextAlignmentOptions.Center;
            selectedDescriptionText.overflowMode = TextOverflowModes.Truncate;
            _restDescriptionLayoutActive = true;
        }

        private void RestoreDescriptionLayout()
        {
            if (!_restDescriptionLayoutActive)
                return;

            CacheDescriptionLayout();
            if (!_descriptionLayoutCached || _descriptionRect == null || selectedDescriptionText == null)
            {
                _restDescriptionLayoutActive = false;
                return;
            }

            _descriptionRect.anchoredPosition = _authoredDescriptionAnchoredPosition;
            _descriptionRect.sizeDelta = _authoredDescriptionSizeDelta;
            selectedDescriptionText.alignment = _authoredDescriptionAlignment;
            selectedDescriptionText.overflowMode = _authoredDescriptionOverflow;
            _restDescriptionLayoutActive = false;
        }

        private void SetMoveStatFieldsVisible(bool visible)
        {
            if (selectedRoleText != null)
                selectedRoleText.gameObject.SetActive(visible);
            if (selectedApText != null)
                selectedApText.gameObject.SetActive(visible);
            if (selectedPowerText != null)
                selectedPowerText.gameObject.SetActive(visible);
            if (selectedAccuracyText != null)
                selectedAccuracyText.gameObject.SetActive(visible);
            if (selectedTargetingText != null)
                selectedTargetingText.gameObject.SetActive(visible);
            if (selectedModifierText != null)
                selectedModifierText.gameObject.SetActive(visible);
            if (selectedTypeIconImage != null)
                selectedTypeIconImage.gameObject.SetActive(visible);
            if (modifierListRoot != null)
                modifierListRoot.gameObject.SetActive(visible);
            if (!visible)
                SetPriorityPillVisible(false, muted: false);
        }

        private void SetPriorityPillVisible(bool visible, bool muted)
        {
            EnsurePriorityPill();
            if (priorityPillRoot == null)
                return;

            priorityPillRoot.gameObject.SetActive(visible);
            if (!visible)
                return;

            if (priorityPillText != null)
            {
                priorityPillText.text = MoveSelectDisplayUtility.PriorityPillLabel;
                Color textColor = muted ? detailsMutedTextColor : priorityPillTextColor;
                priorityPillText.color = textColor;
            }

            if (priorityPillBackground != null)
            {
                Color bg = priorityPillBackgroundColor;
                if (muted)
                    bg.a *= 0.55f;
                priorityPillBackground.color = bg;
            }

            LayoutPriorityPillBesideName();
        }

        private void EnsurePriorityPill()
        {
            if (selectedNameText == null)
                return;

            if (priorityPillRoot == null)
            {
                Transform existing = selectedNameText.transform.Find("PriorityPill");
                if (existing == null && selectedNameText.transform.parent != null)
                    existing = selectedNameText.transform.parent.Find("PriorityPill");
                if (existing != null)
                    priorityPillRoot = existing as RectTransform;
            }

            if (priorityPillRoot == null)
            {
                var go = new GameObject("PriorityPill", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                priorityPillRoot = go.GetComponent<RectTransform>();
                priorityPillRoot.SetParent(selectedNameText.transform, false);
                priorityPillRoot.localScale = Vector3.one;
                priorityPillRoot.anchorMin = new Vector2(0f, 0.5f);
                priorityPillRoot.anchorMax = new Vector2(0f, 0.5f);
                priorityPillRoot.pivot = new Vector2(0f, 0.5f);
                priorityPillRoot.sizeDelta = new Vector2(64f, priorityPillHeight);

                priorityPillBackground = go.GetComponent<Image>();
                priorityPillBackground.color = priorityPillBackgroundColor;
                priorityPillBackground.raycastTarget = false;

                var textGo = new GameObject("Label", typeof(RectTransform));
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(priorityPillRoot, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(6f, 1f);
                textRect.offsetMax = new Vector2(-6f, -1f);
                priorityPillText = textGo.AddComponent<TextMeshProUGUI>();
                priorityPillText.text = MoveSelectDisplayUtility.PriorityPillLabel;
                priorityPillText.fontSize = 14f;
                priorityPillText.alignment = TextAlignmentOptions.Center;
                priorityPillText.color = priorityPillTextColor;
                priorityPillText.textWrappingMode = TextWrappingModes.NoWrap;
                priorityPillText.raycastTarget = false;
                if (selectedNameText.font != null)
                    priorityPillText.font = selectedNameText.font;
            }

            if (priorityPillBackground == null)
                priorityPillBackground = priorityPillRoot.GetComponent<Image>();
            if (priorityPillText == null)
                priorityPillText = priorityPillRoot.GetComponentInChildren<TMP_Text>(true);

            priorityPillRoot.gameObject.SetActive(false);
        }

        private void LayoutPriorityPillBesideName()
        {
            if (priorityPillRoot == null || selectedNameText == null)
                return;

            if (priorityPillRoot.parent != selectedNameText.transform)
                priorityPillRoot.SetParent(selectedNameText.transform, false);

            selectedNameText.ForceMeshUpdate(true);
            float nameWidth = Mathf.Max(0f, selectedNameText.preferredWidth);
            float pillWidth = 64f;
            if (priorityPillText != null)
            {
                priorityPillText.ForceMeshUpdate(true);
                pillWidth = Mathf.Max(48f, priorityPillText.preferredWidth + priorityPillPaddingX * 2f);
            }

            priorityPillRoot.anchorMin = new Vector2(0f, 0.5f);
            priorityPillRoot.anchorMax = new Vector2(0f, 0.5f);
            priorityPillRoot.pivot = new Vector2(0f, 0.5f);
            priorityPillRoot.sizeDelta = new Vector2(pillWidth, priorityPillHeight);
            priorityPillRoot.anchoredPosition = new Vector2(nameWidth + priorityPillGapFromName, 0f);
        }

        private void CachePanelAnchoredPosition()
        {
            EnsureBuilt();
            if (_panelAnchoredPositionCached || root == null)
                return;

            _authoredPanelAnchoredPosition = root.anchoredPosition;
            _panelAnchoredPositionCached = true;
        }

        private void PlayPanelNudge()
        {
            if (root == null)
                return;

            KillPanelNudgeTween(restorePosition: true);
            root.anchoredPosition = _authoredPanelAnchoredPosition;

            if (unavailablePanelNudgeOffsetX <= 0f || unavailablePanelNudgeDuration <= 0f || unavailablePanelNudgeCycles <= 0)
                return;

            var sequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < unavailablePanelNudgeCycles; i++)
            {
                sequence.Append(root
                    .DOAnchorPosX(_authoredPanelAnchoredPosition.x - unavailablePanelNudgeOffsetX, unavailablePanelNudgeDuration)
                    .SetEase(Ease.OutQuad));
                sequence.Append(root
                    .DOAnchorPosX(_authoredPanelAnchoredPosition.x + unavailablePanelNudgeOffsetX, unavailablePanelNudgeDuration)
                    .SetEase(Ease.OutQuad));
            }

            sequence.Append(root
                .DOAnchorPosX(_authoredPanelAnchoredPosition.x, unavailablePanelNudgeDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => _panelNudgeTween = null));
            _panelNudgeTween = sequence;
        }

        private void KillPanelNudgeTween(bool restorePosition)
        {
            if (_panelNudgeTween != null)
            {
                _panelNudgeTween.Kill();
                _panelNudgeTween = null;
            }

            if (root != null)
                root.DOKill();

            if (restorePosition && _panelAnchoredPositionCached && root != null)
                root.anchoredPosition = _authoredPanelAnchoredPosition;
        }

        private void PlayUnavailableMoveSound()
        {
            ICombatUiSfxPlayer uiSfx = CombatUiSfxResolver.Resolve(uiSfxBehaviour, ref _cachedUiSfxBehaviour);
            if (uiSfx != null)
            {
                uiSfx.PlayCue(CombatUiSfxCue.Invalid);
                return;
            }

            if (unavailableMoveSoundEvent == null)
                return;

            ICombatAudioBridge audioBridge = CombatUiSfxResolver.ResolveAudioBridge(audioBridgeBehaviour, ref _cachedAudioBridgeBehaviour);
            audioBridge?.PlayOneShot(unavailableMoveSoundEvent);
        }

        private bool HasAuthoringReferences() =>
            moveListRoot != null
            && selectedNameText != null
            && selectedRoleText != null
            && selectedApText != null
            && selectedDescriptionText != null;

        private void EnsureBuilt()
        {
            if (root == null)
                root = transform as RectTransform;
            if (root == null)
                return;

            if (HasAuthoringReferences())
            {
                EnsureMoveListScrollReference();
                CacheMoveListAnchoredPosition();
                EnsureDetailsReferences();
                EnsurePriorityPill();
                return;
            }

            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();

            var bg = GetComponent<Image>();
            bool createdBackground = false;
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                createdBackground = true;
            }
            if (createdBackground)
                bg.color = new Color(0.03f, 0.04f, 0.05f, 0.80f);
            bg.raycastTarget = false;

            var top = EnsureRect("Top", root, 0.02f, 0.60f, 0.48f, 0.98f);
            moveListRoot = EnsureRect("MoveList", top, 0f, 0f, 1f, 0.85f);
            EnsureVerticalListLayout(moveListRoot);
            EnsureMoveListScrollReference();
            CacheMoveListAnchoredPosition();
            noMovesText = EnsureText("NoMovesText", top, 0f, 0.32f, 1f, 0.65f, 22, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f, 0.9f), "No moves available");

            titleText = EnsureText("TitleText", top, 0f, 0.86f, 1f, 1f, 26, TextAlignmentOptions.Left, Color.white, "Move Select");

            var details = EnsureRect("Details", root, 0.50f, 0.20f, 0.98f, 0.98f);
            selectedTypeIconImage = EnsureImage("SelectedTypeIcon", details, 0f, 0.72f, 0.12f, 0.88f, new Color(1f, 1f, 1f, 0f));
            selectedTypeIconImage.preserveAspect = true;
            selectedNameText = EnsureText("SelectedName", details, 0.14f, 0.84f, 1f, 1f, 28, TextAlignmentOptions.Left, Color.white, "--");
            EnsurePriorityPill();
            selectedRoleText = EnsureText(
                "SelectedRole",
                selectedNameText != null ? selectedNameText.transform : details,
                0f,
                0f,
                1f,
                0f,
                22,
                TextAlignmentOptions.Left,
                neutralTargetingColor,
                "Frozen | Damaging Melee",
                out bool createdSelectedRole);
            if (createdSelectedRole && selectedRoleText != null)
            {
                var roleRect = selectedRoleText.rectTransform;
                roleRect.anchorMin = new Vector2(0f, 0f);
                roleRect.anchorMax = new Vector2(1f, 0f);
                roleRect.pivot = new Vector2(0f, 1f);
                roleRect.anchoredPosition = Vector2.zero;
                roleRect.sizeDelta = new Vector2(0f, 28f);
            }
            selectedApText = EnsureText("SelectedAP", details, 0f, 0.67f, 0.40f, 0.82f, 22, TextAlignmentOptions.Left, new Color(0.82f, 0.92f, 0.98f, 1f), "AP Cost --");
            selectedPowerText = EnsureText("SelectedPower", details, 0.42f, 0.67f, 0.78f, 0.82f, 22, TextAlignmentOptions.Left, new Color(0.95f, 0.9f, 0.74f, 1f), "Power --");
            selectedAccuracyText = EnsureText("SelectedAccuracy", details, 0.78f, 0.55f, 1f, 0.67f, 22, TextAlignmentOptions.Right, new Color(0.82f, 0.92f, 0.98f, 1f), "Accuracy --");
            selectedTargetingText = EnsureText("SelectedTargeting", details, 0f, 0.52f, 1f, 0.67f, 20, TextAlignmentOptions.Left, new Color(0.74f, 0.86f, 0.94f, 1f), "Target --");
            selectedModifierText = EnsureText("SelectedModifiers", details, 0f, 0.37f, 1f, 0.52f, 20, TextAlignmentOptions.Left, new Color(0.76f, 0.84f, 0.89f, 1f), "Mods: None");
            modifierListRoot = EnsureModifierListRoot(selectedModifierText != null ? selectedModifierText.transform : details);
            selectedDescriptionText = EnsureText("SelectedDescription", details, 0f, 0f, 1f, 0.38f, 18, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.88f, 0.90f, 1f), "No move description available.");
            selectedDescriptionText.textWrappingMode = TextWrappingModes.Normal;

            footerPromptText = EnsureText("FooterPrompt", root, 0.02f, 0.02f, 0.98f, 0.16f, 18, TextAlignmentOptions.Center, new Color(0.82f, 0.9f, 0.95f, 1f), "Confirm=Select   Cancel=Back   Info=Details");
            EnsureAuthoringColorsCached();
        }

        private void CacheMoveListAnchoredPosition()
        {
            if (_moveListAnchoredPositionCached)
                return;

            // Prefer viewport position (fixed window). Fall back to content root.
            if (moveListViewport != null)
            {
                _authoredMoveListAnchoredPosition = moveListViewport.anchoredPosition;
                _moveListAnchoredPositionCached = true;
                return;
            }

            if (moveListRoot == null)
                return;

            _authoredMoveListAnchoredPosition = moveListRoot.anchoredPosition;
            _moveListAnchoredPositionCached = true;
        }

        private void EnsureMoveListScrollReference()
        {
            if (moveListViewport == null && moveListRoot != null)
            {
                // Common authoring: Viewport parent of content, or a sibling named MoveListViewport.
                var parent = moveListRoot.parent as RectTransform;
                if (parent != null && (parent.GetComponent<Mask>() != null || parent.GetComponent<RectMask2D>() != null))
                    moveListViewport = parent;
                else if (root != null)
                {
                    var named = root.Find("MoveListViewport") as RectTransform;
                    if (named == null && moveListRoot.parent != null)
                        named = moveListRoot.parent.Find("MoveListViewport") as RectTransform;
                    if (named != null)
                        moveListViewport = named;
                }
            }

            if (moveListScrollRect != null)
            {
                if (moveListRoot != null && moveListScrollRect.content != moveListRoot)
                    moveListScrollRect.content = moveListRoot;
                if (moveListViewport != null && moveListScrollRect.viewport != moveListViewport)
                    moveListScrollRect.viewport = moveListViewport;
                return;
            }

            if (moveListRoot == null)
                return;

            var parentScroll = moveListRoot.GetComponentInParent<ScrollRect>(true);
            if (parentScroll != null)
            {
                moveListScrollRect = parentScroll;
                if (moveListScrollRect.content != moveListRoot)
                    moveListScrollRect.content = moveListRoot;
                if (moveListViewport != null)
                    moveListScrollRect.viewport = moveListViewport;
                return;
            }

            if (moveListViewport != null)
            {
                moveListScrollRect = moveListViewport.GetComponent<ScrollRect>();
                if (moveListScrollRect == null)
                    moveListScrollRect = moveListViewport.GetComponentInParent<ScrollRect>(true);
                if (moveListScrollRect != null)
                {
                    moveListScrollRect.content = moveListRoot;
                    moveListScrollRect.viewport = moveListViewport;
                }
            }
        }

        private void ConfigureMoveListScroll(int rowCount)
        {
            EnsureMoveListScrollReference();
            CacheMoveListAnchoredPosition();

            if (moveListRoot != null)
            {
                // Content grows with rows; viewport clips. Do not stretch the panel.
                var fitter = moveListRoot.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = moveListRoot.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                HonorAuthoredMoveRowHeight();
            }

            bool shouldScroll = rowCount > Mathf.Max(1, visibleMoveRowsBeforeScroll);
            if (moveListScrollRect == null)
                return;

            moveListScrollRect.horizontal = false;
            moveListScrollRect.vertical = shouldScroll;
            moveListScrollRect.movementType = ScrollRect.MovementType.Clamped;
            moveListScrollRect.inertia = false;
            if (moveListScrollRect.content == null && moveListRoot != null)
                moveListScrollRect.content = moveListRoot;
            if (moveListScrollRect.viewport == null && moveListViewport != null)
                moveListScrollRect.viewport = moveListViewport;

            if (!shouldScroll)
                ResetMoveListScroll();
        }

        private void HonorAuthoredMoveRowHeight()
        {
            if (moveListRoot == null)
                return;

            // childControlHeight off makes the group use sizeDelta. The prefab rect is 0
            // while the row LayoutElement preferred height is the authored 58.
            VerticalLayoutGroup vertical = moveListRoot.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                vertical.childControlHeight = true;

            // A layout element on the content object outranks the vertical group and
            // locks the scroller to a single row. Row heights live on the rows.
            LayoutElement contentLayout = moveListRoot.GetComponent<LayoutElement>();
            if (contentLayout != null)
                contentLayout.ignoreLayout = true;
        }

        private void EnsureAuthoringColorsCached()
        {
            if (_authoringColorsCached)
                return;

            if (selectedNameText != null)
                _authoredNameColor = selectedNameText.color;
            if (selectedRoleText != null)
                _authoredRoleColor = selectedRoleText.color;
            if (selectedApText != null)
                _authoredApColor = selectedApText.color;
            if (selectedPowerText != null)
                _authoredPowerColor = selectedPowerText.color;
            if (selectedAccuracyText != null)
                _authoredAccuracyColor = selectedAccuracyText.color;
            if (selectedTargetingText != null)
                _authoredTargetingColor = selectedTargetingText.color;
            if (selectedModifierText != null)
                _authoredModifierColor = selectedModifierText.color;
            if (selectedDescriptionText != null)
                _authoredDescriptionColor = selectedDescriptionText.color;

            CacheDescriptionLayout();
            _authoringColorsCached = true;
        }

        private void EnsureRowPool(int count)
        {
            if (_rows.Count == 0 && moveListRoot != null)
            {
                // Adopt authored preview/template rows so they are not left orphaned beside the pool.
                for (int i = 0; i < moveListRoot.childCount; i++)
                {
                    var child = moveListRoot.GetChild(i);
                    if (child == null)
                        continue;

                    var existing = child.GetComponent<CombatMoveRowItemUGUI>();
                    if (existing == null || _rows.Contains(existing))
                        continue;

                    _rows.Add(existing);
                }
            }

            while (_rows.Count < count)
            {
                CombatMoveRowItemUGUI row = null;
                if (moveRowPrefab != null)
                {
                    row = Instantiate(moveRowPrefab, moveListRoot, false);
                    row.name = $"MoveRow_{_rows.Count:D2}";
                }
                else
                {
                    row = CreateGeneratedRow(_rows.Count);
                }

                if (row != null)
                    _rows.Add(row);
                else
                    break;
            }
        }

        private void ScrollToRow(int selectedIndex, int rowCount)
        {
            if (moveListRoot == null || rowCount <= 0)
                return;

            CacheMoveListAnchoredPosition();

            int visibleRows = Mathf.Max(1, visibleMoveRowsBeforeScroll);
            bool shouldScroll = rowCount > visibleRows && selectedIndex >= 0;
            EnsureMoveListScrollReference();

            if (!shouldScroll)
            {
                ResetMoveListScroll();
                return;
            }

            int maxFirstVisible = Mathf.Max(0, rowCount - visibleRows);
            int firstVisible = Mathf.Clamp(selectedIndex - visibleRows + 1, 0, maxFirstVisible);

            if (moveListScrollRect != null && moveListScrollRect.content == moveListRoot)
            {
                moveListScrollRect.vertical = true;
                moveListScrollRect.horizontal = false;
                moveListScrollRect.movementType = ScrollRect.MovementType.Clamped;
                moveListScrollRect.inertia = false;
                Canvas.ForceUpdateCanvases();
                moveListScrollRect.verticalNormalizedPosition = maxFirstVisible <= 0
                    ? 1f
                    : 1f - (firstVisible / (float)maxFirstVisible);
                return;
            }

            // Fallback (no ScrollRect): keep content under a fixed viewport when assigned; otherwise
            // offset from the authored content position.
            if (moveListViewport != null)
            {
                Vector2 contentPos = moveListRoot.anchoredPosition;
                contentPos.y = firstVisible * ResolveMoveRowStep();
                moveListRoot.anchoredPosition = contentPos;
                return;
            }

            Vector2 position = _authoredMoveListAnchoredPosition;
            position.y = _authoredMoveListAnchoredPosition.y + firstVisible * ResolveMoveRowStep();
            moveListRoot.anchoredPosition = position;
        }

        private void ResetMoveListScroll()
        {
            CacheMoveListAnchoredPosition();

            if (moveListScrollRect != null && moveListScrollRect.content == moveListRoot)
            {
                moveListScrollRect.verticalNormalizedPosition = 1f;
            }

            if (moveListViewport != null)
            {
                // Viewport stays at authored position; content resets to top.
                if (_moveListAnchoredPositionCached)
                    moveListViewport.anchoredPosition = _authoredMoveListAnchoredPosition;
                if (moveListRoot != null)
                {
                    var contentPos = moveListRoot.anchoredPosition;
                    contentPos.y = 0f;
                    moveListRoot.anchoredPosition = contentPos;
                }
                return;
            }

            if (moveListRoot != null && _moveListAnchoredPositionCached)
                moveListRoot.anchoredPosition = _authoredMoveListAnchoredPosition;
        }

        private int CountVisibleMoveRows()
        {
            int count = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null && _rows[i].gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private float ResolveMoveRowStep()
        {
            float height = 30f;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null)
                    continue;

                LayoutElement layout = _rows[i].GetComponent<LayoutElement>();
                if (layout != null && layout.preferredHeight > 0f)
                {
                    height = layout.preferredHeight;
                    break;
                }
            }

            float spacing = 0f;
            VerticalLayoutGroup layoutGroup = moveListRoot != null ? moveListRoot.GetComponent<VerticalLayoutGroup>() : null;
            if (layoutGroup != null)
                spacing = layoutGroup.spacing;

            return Mathf.Max(1f, height + spacing);
        }

        private MoveRowSelectionTuning BuildMoveRowSelectionTuning() =>
            new MoveRowSelectionTuning
            {
                nudgeOffsetX = moveRowSelectionNudgeOffsetX,
                reserveSelectionArrowSpace = moveRowReserveSelectionArrowSpace,
                nudgeDuration = moveRowSelectionNudgeDuration,
                nudgeEase = moveRowSelectionNudgeEase,
                returnEase = moveRowSelectionReturnEase,
                nudgeScalePeak = moveRowSelectionNudgeScalePeak,
                arrowFadeDuration = moveRowSelectionArrowFadeDuration,
                arrowScalePeak = moveRowSelectionArrowScalePeak,
                arrowEase = moveRowSelectionArrowEase,
                bounceDuration = moveRowSelectionNudgeDuration,
                bounceEaseOut = Ease.OutQuad,
                bounceEaseIn = moveRowSelectionNudgeEase,
                backdropFadeDuration = moveRowSelectionSlideDuration,
                backdropFadeEase = moveRowSelectionSlideEase,
            };

        private void ResetAllRowSelectionVisuals()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    _rows[i].ResetSelectionVisuals();
            }
        }

        private CombatMoveRowItemUGUI CreateGeneratedRow(int index)
        {
            var rowGo = new GameObject(
                $"MoveRow_{index:D2}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(CombatMoveRowItemUGUI));
            var rect = rowGo.GetComponent<RectTransform>();
            rect.SetParent(moveListRoot, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 1f);

            var contentRect = EnsureRect("RowContent", rect, 0f, 0f, 1f, 1f);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var iconRect = EnsureRect("TypeIcon", contentRect, 0.02f, 0.14f, 0.10f, 0.86f);
            var iconImage = iconRect.GetComponent<Image>();
            if (iconImage == null)
                iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.color = new Color(1f, 1f, 1f, 0f);
            iconImage.raycastTarget = false;

            var label = EnsureText("Label", contentRect, 0.12f, 0f, 0.78f, 1f, 20, TextAlignmentOptions.Left, Color.white, "--");
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var row = rowGo.GetComponent<CombatMoveRowItemUGUI>();
            if (row != null)
                row.ConfigureGeneratedDefaults(generatedRowFont, 30f);
            return row;
        }

        private RectTransform EnsureModifierListRoot(Transform parent)
        {
            if (parent == null)
                return null;

            var existing = parent.Find("ModsList") as RectTransform;
            if (existing != null)
            {
                EnsureModifierListLayout(existing);
                return existing;
            }

            var rect = EnsureRect("ModsList", parent, 0f, 0f, 1f, 1f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            EnsureModifierListLayout(rect);
            return rect;
        }

        private static void EnsureModifierListLayout(RectTransform rect)
        {
            if (rect == null)
                return;

            EnsureVerticalListLayout(rect, spacing: 4f, controlHeight: true);

            var fitter = rect.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void EnsureModifierRowPool(int count)
        {
            if (_modifierRows.Count == 0 && modifierListRoot != null)
            {
                var existingRows = modifierListRoot.GetComponentsInChildren<CombatModifierOverviewRowUGUI>(true);
                for (int i = 0; i < existingRows.Length; i++)
                {
                    if (existingRows[i] != null && !_modifierRows.Contains(existingRows[i]))
                        _modifierRows.Add(existingRows[i]);
                }
            }

            while (_modifierRows.Count < count)
            {
                CombatModifierOverviewRowUGUI row = null;
                if (modifierRowPrefab != null)
                {
                    row = Instantiate(modifierRowPrefab, modifierListRoot, false);
                    row.name = $"ModifierRow_{_modifierRows.Count:D2}";
                }
                else
                {
                    row = CreateGeneratedModifierRow(_modifierRows.Count);
                }

                if (row != null)
                    _modifierRows.Add(row);
                else
                    break;
            }
        }

        private CombatModifierOverviewRowUGUI CreateGeneratedModifierRow(int index)
        {
            var rowGo = new GameObject(
                $"ModifierRow_{index:D2}",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(CombatModifierOverviewRowUGUI));
            var rect = rowGo.GetComponent<RectTransform>();
            rect.SetParent(modifierListRoot, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var layoutElement = rowGo.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 28f;
            layoutElement.minHeight = 28f;

            var bulletRect = EnsureRect("Image", rect, 0f, 0f, 0f, 0f);
            bulletRect.anchorMin = new Vector2(0f, 0.5f);
            bulletRect.anchorMax = new Vector2(0f, 0.5f);
            bulletRect.pivot = new Vector2(0.5f, 0.5f);
            bulletRect.anchoredPosition = new Vector2(10f, 0f);
            bulletRect.sizeDelta = new Vector2(12f, 12f);
            var bulletImage = bulletRect.gameObject.AddComponent<Image>();
            bulletImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            bulletImage.preserveAspect = true;
            bulletImage.color = commonRarityColor;
            bulletImage.raycastTarget = false;

            var descriptionRect = EnsureRect("Text (TMP)", rect, 0f, 0f, 1f, 1f);
            var description = descriptionRect.gameObject.AddComponent<TextMeshProUGUI>();
            description.text = "--";
            description.fontSize = 22f;
            description.alignment = TextAlignmentOptions.Left;
            description.color = new Color(0.86f, 0.88f, 0.90f, 1f);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.raycastTarget = false;
            if (generatedModifierRowFont != null)
                description.font = generatedModifierRowFont;
            else if (generatedRowFont != null)
                description.font = generatedRowFont;

            var row = rowGo.GetComponent<CombatModifierOverviewRowUGUI>();
            if (row != null)
                row.ConfigureGeneratedDefaults(generatedModifierRowFont ?? generatedRowFont, 28f);
            return row;
        }

        private static void EnsureVerticalListLayout(RectTransform rect, float spacing = 6f, bool controlHeight = false)
        {
            if (rect == null)
                return;

            var vertical = rect.GetComponent<VerticalLayoutGroup>();
            bool createdVertical = false;
            if (vertical == null)
            {
                vertical = rect.gameObject.AddComponent<VerticalLayoutGroup>();
                createdVertical = true;
            }
            if (createdVertical)
            {
                vertical.childControlWidth = true;
                vertical.childControlHeight = controlHeight;
                vertical.childForceExpandWidth = true;
                vertical.childForceExpandHeight = false;
                vertical.spacing = spacing;
                vertical.padding = new RectOffset(0, 0, 0, 0);
            }

            var fitter = rect.GetComponent<ContentSizeFitter>();
            bool createdFitter = false;
            if (fitter == null)
            {
                fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
                createdFitter = true;
            }
            if (createdFitter)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private static RectTransform EnsureRect(string name, Transform parent, float minX, float minY, float maxX, float maxY)
        {
            return EnsureRect(name, parent, minX, minY, maxX, maxY, out _);
        }

        private static RectTransform EnsureRect(
            string name,
            Transform parent,
            float minX,
            float minY,
            float maxX,
            float maxY,
            out bool created)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                created = false;
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            created = true;
            return rect;
        }

        private static TMP_Text EnsureText(
            string name,
            Transform parent,
            float minX,
            float minY,
            float maxX,
            float maxY,
            int size,
            TextAlignmentOptions align,
            Color color,
            string value)
        {
            return EnsureText(name, parent, minX, minY, maxX, maxY, size, align, color, value, out bool _);
        }

        private static TMP_Text EnsureText(
            string name,
            Transform parent,
            float minX,
            float minY,
            float maxX,
            float maxY,
            int size,
            TextAlignmentOptions align,
            Color color,
            string value,
            out bool created)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY, out bool createdRect);
            var text = rect.GetComponent<TMP_Text>();
            bool createdText = false;
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                createdText = true;
            }
            if (createdText)
            {
                text.fontSize = size;
                text.alignment = align;
                text.color = color;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            text.raycastTarget = false;
            if (string.IsNullOrEmpty(text.text))
                text.text = value;
            created = createdRect || createdText;
            return text;
        }

        private static Image EnsureImage(string name, Transform parent, float minX, float minY, float maxX, float maxY, Color color)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY);
            var image = rect.GetComponent<Image>();
            bool createdImage = false;
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
                createdImage = true;
            }
            image.raycastTarget = false;
            if (createdImage)
                image.color = color;
            return image;
        }
    }
}
