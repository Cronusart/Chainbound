using System.Collections.Generic;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public class ChainlingPartyInfoPanelBinder : MonoBehaviour
    {
        private static readonly ChainlingPartyViewMode[] Modes =
        {
            ChainlingPartyViewMode.General,
            ChainlingPartyViewMode.Attributes,
            ChainlingPartyViewMode.Moves,
            ChainlingPartyViewMode.Detail,
            ChainlingPartyViewMode.Loadout,
            ChainlingPartyViewMode.Stats,
        };

        [Header("Authored View Layouts")]
        [SerializeField] private bool useAuthoredViewLayouts = true;
        [SerializeField] private RectTransform viewsRoot;
        [SerializeField] private CanvasGroup viewContentGroup;
        [SerializeField] private List<ChainlingPartyInfoViewLayout> viewLayouts = new List<ChainlingPartyInfoViewLayout>();

        [Header("Legacy/Fallback Layout")]
        [SerializeField] private PartyTabStarRatingArtSO starRatingArt;
        [SerializeField] private PartyTabCycleTypographySO cycleTypography;
        [SerializeField] private ChainlingPartyTypeChromePaletteSO typeChromePalette;
        [SerializeField] private PartyTabStarRatingDisplay starRatingDisplay;
        [SerializeField] private PartyTabCycleProgressDisplay cycleProgressDisplay;
        [SerializeField] private RectTransform linesRoot;
        [SerializeField] private TMP_Text lineTemplate;
        [SerializeField] private Image xpBarBackground;
        [SerializeField] private Image xpBarFill;
        [SerializeField] private RectTransform badgesRoot;
        [SerializeField] private Image badgeTemplate;

        private readonly List<TMP_Text> linePool = new List<TMP_Text>();
        private readonly List<Image> badgePool = new List<Image>();
        private ChainlingPartyViewMode lastBoundMode = ChainlingPartyViewMode.General;
        private bool deferProgressReveal;

        public PartyTabStarRatingDisplay StarRatingDisplay => starRatingDisplay;
        public PartyTabCycleProgressDisplay CycleProgressDisplay => cycleProgressDisplay;
        public RectTransform ViewsRoot => viewsRoot;
        public CanvasGroup ViewContentGroup => ResolveViewContentGroup(createIfMissing: true);
        public IReadOnlyList<ChainlingPartyInfoViewLayout> ViewLayouts => viewLayouts;

        public void ConfigureStarRatingArt(PartyTabStarRatingArtSO art)
        {
            if (art != null)
                starRatingArt = art;

            EnsureStarRatingDisplay(createIfMissing: false);
            if (starRatingDisplay != null)
                starRatingDisplay.Configure(starRatingArt);

            EnsureViewLayouts(createIfMissing: false);
            for (int i = 0; i < viewLayouts.Count; i++)
                viewLayouts[i]?.ConfigureStarRatingArt(starRatingArt);
        }

        public void ConfigureCycleTypography(PartyTabCycleTypographySO typography)
        {
            if (typography != null)
                cycleTypography = typography;

            if (useAuthoredViewLayouts)
            {
                ClearLegacyCycleProgressReference();
                EnsureViewLayouts(createIfMissing: false);
                for (int i = 0; i < viewLayouts.Count; i++)
                    viewLayouts[i]?.ConfigureCycleTypography(cycleTypography);
                return;
            }

            EnsureCycleProgressDisplay(createIfMissing: false);
            if (cycleProgressDisplay != null)
                cycleProgressDisplay.Configure(cycleTypography);
        }

        public void ConfigureTypeChromePalette(ChainlingPartyTypeChromePaletteSO palette)
        {
            if (palette != null)
                typeChromePalette = palette;

            EnsureViewLayouts(createIfMissing: false);
            for (int i = 0; i < viewLayouts.Count; i++)
            {
                ChainlingPartyInfoViewLayout layout = viewLayouts[i];
                if (layout == null)
                    continue;

                ChainlingPartyAttributesViewLayout attributesLayout =
                    layout.GetComponent<ChainlingPartyAttributesViewLayout>();
                attributesLayout?.ConfigureTypeChromePalette(typeChromePalette);

                ChainlingPartyEntryViewLayout entryLayout =
                    layout.GetComponent<ChainlingPartyEntryViewLayout>();
                entryLayout?.ConfigurePalettes(null, null, typeChromePalette);
            }
        }

        public void ConfigureEntryMovePalettes(
            TypeIconPaletteSO typeIcons,
            PartyTabMoveRarityPaletteSO rarityPalette)
        {
            EnsureViewLayouts(createIfMissing: false);
            for (int i = 0; i < viewLayouts.Count; i++)
            {
                ChainlingPartyInfoViewLayout layout = viewLayouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.Detail)
                    continue;

                ChainlingPartyEntryViewLayout entryLayout =
                    layout.GetComponent<ChainlingPartyEntryViewLayout>();
                entryLayout?.ConfigurePalettes(typeIcons, rarityPalette, typeChromePalette);
            }
        }

        public void BuildDefaultLayoutForEditor()
        {
            if (useAuthoredViewLayouts)
            {
                BuildAuthoredViewLayoutsForEditor();
                return;
            }

            EnsureBuilt(createIfMissing: true);
            EnsureStarRatingDisplay(createIfMissing: true);
            EnsureCycleProgressDisplay(createIfMissing: true);
        }

        public void BuildAuthoredViewLayoutsForEditor()
        {
            EnsureViewLayouts(createIfMissing: true);
            ResolveViewContentGroup(createIfMissing: true);
            SuppressLegacyFallbackSurfaceForEditor();
        }

        public void SuppressLegacyFallbackSurfaceForEditor()
        {
            if (!useAuthoredViewLayouts)
                return;

            EnsureBuilt(createIfMissing: false);
            EnsureStarRatingDisplay(createIfMissing: false);
            EnsureCycleProgressDisplay(createIfMissing: false);
            SetLegacySurfaceVisible(false);
        }

        /// <summary>
        /// Hides stacked authored views and binds the empty General surface before roster data arrives.
        /// The prefab keeps every view mode active; without this, first open flashes all of them.
        /// </summary>
        public void PrepareUnboundPresentation(ChainlingPartyViewMode mode = ChainlingPartyViewMode.General)
        {
            Bind(ChainlingPartyColumnSummary.Empty("Assign at a PC."), mode);
        }

        private void Awake()
        {
            PrepareUnboundPresentation();
        }

        public void SetDeferProgressReveal(bool defer)
        {
            deferProgressReveal = defer;
            EnsureViewLayouts(createIfMissing: false);
            for (int i = 0; i < viewLayouts.Count; i++)
                viewLayouts[i]?.SetDeferProgressReveal(defer);
        }

        public void Bind(ChainlingPartyColumnSummary summary, ChainlingPartyViewMode mode)
        {
            lastBoundMode = mode;
            EnsureBuilt(createIfMissing: false);
            EnsureStarRatingDisplay(createIfMissing: false);
            EnsureCycleProgressDisplay(createIfMissing: false);
            EnsureViewLayouts(createIfMissing: false);

            if (useAuthoredViewLayouts && TryGetViewLayout(mode, out ChainlingPartyInfoViewLayout viewLayout))
            {
                SetLegacySurfaceVisible(false);
                SetActiveViewLayout(viewLayout);
                viewLayout.SetDeferProgressReveal(deferProgressReveal);
                viewLayout.Bind(summary, starRatingArt, cycleTypography);
                return;
            }

            SetActiveViewLayout(null);
            SetLegacySurfaceVisible(true);
            BindLegacy(summary, mode);
        }

        public void ReplayEnterProgress(float delaySeconds)
        {
            if (TryGetViewLayout(lastBoundMode, out ChainlingPartyInfoViewLayout layout))
                layout.ReplayEnterFill(delaySeconds);
        }

        private void BindLegacy(ChainlingPartyColumnSummary summary, ChainlingPartyViewMode mode)
        {
            if (summary == null || summary.isEmpty)
            {
                SetLineSurfaceVisible(true);
                ClearLines();
                AddLine("-", summary != null ? summary.emptyMessage : "Assign at a PC", true);
                SetXpBarVisible(false);
                SetBadgesVisible(false);
                ClearBadges();
                BindStarRating(0, false, false);
                BindCycleProgress("", 0, false);
                return;
            }

            SetLineSurfaceVisible(true);
            BindStarRating(summary.starCount, mode == ChainlingPartyViewMode.General, mode == ChainlingPartyViewMode.General);
            BindCycleProgress(summary.cycleLabel, summary.cycleTier, mode == ChainlingPartyViewMode.General);

            ClearLines();
            if (summary.lines != null)
            {
                for (int i = 0; i < summary.lines.Count; i++)
                {
                    ChainlingPartyDisplayLine line = summary.lines[i];
                    AddLine(line.Label, line.Value, line.IsMuted);
                }
            }

            bool showXp = mode == ChainlingPartyViewMode.General;
            SetXpBarVisible(showXp);
            if (showXp && xpBarFill != null)
            {
                float fill01 = deferProgressReveal ? 0f : summary.xpBarFill01;
                PartyTabProgressBarUtility.SetFill01(xpBarFill, fill01);
            }

            // Party Stats summary no longer shows a badge strip (Archive / drill-down only).
            SetBadgesVisible(false);
            ClearBadges();
        }

        private void EnsureBuilt(bool createIfMissing)
        {
            SanitizeLegacyReferences();

            if (linesRoot == null)
                linesRoot = transform.Find("Lines") as RectTransform;

            if (linesRoot == null && createIfMissing)
            {
                linesRoot = PlayerMenuUiFactory.CreateRect("Lines", transform);
                PlayerMenuUiFactory.Stretch(linesRoot, 8f, 8f, 8f, 8f);
                PlayerMenuUiFactory.VerticalLayout(linesRoot.gameObject, 4f);
            }

            if (lineTemplate == null && linesRoot != null)
                lineTemplate = linesRoot.Find("LineTemplate")?.GetComponent<TMP_Text>();

            if (lineTemplate == null && linesRoot != null && createIfMissing)
            {
                lineTemplate = PlayerMenuUiFactory.CreateText("LineTemplate", linesRoot, "Label: Value", 18f, TextAlignmentOptions.TopLeft, true);
                lineTemplate.gameObject.SetActive(false);
            }

            if (xpBarBackground == null)
                xpBarBackground = transform.Find("XpBarBackground")?.GetComponent<Image>();

            if (xpBarBackground == null && createIfMissing)
            {
                xpBarBackground = PlayerMenuUiFactory.CreateImage("XpBarBackground", transform, new Color(0.08f, 0.1f, 0.14f, 1f));
                RectTransform xpRect = xpBarBackground.rectTransform;
                xpRect.anchorMin = new Vector2(0.08f, 0.04f);
                xpRect.anchorMax = new Vector2(0.92f, 0.1f);
                xpRect.offsetMin = Vector2.zero;
                xpRect.offsetMax = Vector2.zero;
            }

            if (xpBarFill == null && xpBarBackground != null)
                xpBarFill = xpBarBackground.transform.Find("XpBarFill")?.GetComponent<Image>();

            if (xpBarFill == null && xpBarBackground != null && createIfMissing)
            {
                xpBarFill = PlayerMenuUiFactory.CreateImage("XpBarFill", xpBarBackground.transform, new Color(0.95f, 0.78f, 0.28f, 1f));
                PlayerMenuUiFactory.Stretch(xpBarFill.rectTransform, 0f, 0f, 0f, 0f);
                PartyTabProgressBarUtility.ConfigureRectFill(xpBarFill);
            }

            if (badgesRoot == null)
                badgesRoot = transform.Find("Badges") as RectTransform;

            if (badgesRoot == null && createIfMissing)
            {
                badgesRoot = PlayerMenuUiFactory.CreateRect("Badges", transform);
                RectTransform badgeRect = badgesRoot;
                badgeRect.anchorMin = new Vector2(0.08f, 0.12f);
                badgeRect.anchorMax = new Vector2(0.92f, 0.2f);
                badgeRect.offsetMin = Vector2.zero;
                badgeRect.offsetMax = Vector2.zero;
                PlayerMenuUiFactory.HorizontalLayout(badgesRoot.gameObject, 6f);
            }

            if (badgeTemplate == null && badgesRoot != null)
                badgeTemplate = badgesRoot.Find("BadgeTemplate")?.GetComponent<Image>();

            if (badgeTemplate == null && badgesRoot != null && createIfMissing)
            {
                badgeTemplate = PlayerMenuUiFactory.CreateImage("BadgeTemplate", badgesRoot, Color.white);
                badgeTemplate.gameObject.SetActive(false);
                PlayerMenuUiFactory.LayoutElement(badgeTemplate.gameObject, preferredWidth: 28f, preferredHeight: 28f);
            }
        }

        private void EnsureViewLayouts(bool createIfMissing)
        {
            ResolveViewsRoot();

            if (viewsRoot == null && createIfMissing)
            {
                viewsRoot = PlayerMenuUiFactory.CreateRect("Views", transform);
                PlayerMenuUiFactory.Stretch(viewsRoot, 0f, 0f, 0f, 0f);
            }

            ResolveViewContentGroup(createIfMissing);

            viewLayouts.RemoveAll(item => item == null);
            if (viewsRoot != null)
            {
                ChainlingPartyInfoViewLayout[] authored = viewsRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
                for (int i = 0; i < authored.Length; i++)
                {
                    if (authored[i] != null && !viewLayouts.Contains(authored[i]))
                        viewLayouts.Add(authored[i]);
                }
            }

            if (!createIfMissing || viewsRoot == null)
                return;

            for (int i = 0; i < Modes.Length; i++)
            {
                ChainlingPartyViewMode mode = Modes[i];
                if (!TryFindViewLayout(mode, requireRuntimeBindable: false, out ChainlingPartyInfoViewLayout layout))
                    layout = CreateViewLayout(mode);

                layout.ConfigureMode(mode);
                layout.ConfigureStarRatingArt(starRatingArt);
                layout.ConfigureCycleTypography(cycleTypography);
                layout.BuildDefaultLayoutForEditor();
            }
        }

        private ChainlingPartyInfoViewLayout CreateViewLayout(ChainlingPartyViewMode mode)
        {
            RectTransform root = PlayerMenuUiFactory.CreateRect($"{mode}View", viewsRoot);
            PlayerMenuUiFactory.Stretch(root, 0f, 0f, 0f, 0f);
            ChainlingPartyInfoViewLayout layout = root.gameObject.AddComponent<ChainlingPartyInfoViewLayout>();
            layout.ConfigureMode(mode);
            layout.ApplyDefaultPresentationForMode();
            viewLayouts.Add(layout);
            return layout;
        }

        private bool TryGetViewLayout(ChainlingPartyViewMode mode, out ChainlingPartyInfoViewLayout layout)
        {
            return TryFindViewLayout(mode, requireRuntimeBindable: true, out layout);
        }

        private bool TryFindViewLayout(
            ChainlingPartyViewMode mode,
            bool requireRuntimeBindable,
            out ChainlingPartyInfoViewLayout layout)
        {
            for (int i = 0; i < viewLayouts.Count; i++)
            {
                ChainlingPartyInfoViewLayout item = viewLayouts[i];
                if (item == null || item.ViewMode != mode)
                    continue;

                if (requireRuntimeBindable && !item.CanBindRuntime())
                    continue;

                layout = item;
                return true;
            }

            if (viewsRoot != null)
            {
                ChainlingPartyInfoViewLayout[] authored = viewsRoot.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
                for (int i = 0; i < authored.Length; i++)
                {
                    ChainlingPartyInfoViewLayout item = authored[i];
                    if (item == null || item.ViewMode != mode)
                        continue;

                    if (requireRuntimeBindable && !item.CanBindRuntime())
                        continue;

                    if (!viewLayouts.Contains(item))
                        viewLayouts.Add(item);

                    layout = item;
                    return true;
                }
            }

            layout = null;
            return false;
        }

        private void SetActiveViewLayout(ChainlingPartyInfoViewLayout activeLayout)
        {
            if (viewsRoot != null)
                viewsRoot.gameObject.SetActive(activeLayout != null);

            for (int i = 0; i < viewLayouts.Count; i++)
            {
                ChainlingPartyInfoViewLayout layout = viewLayouts[i];
                if (layout != null)
                    layout.gameObject.SetActive(layout == activeLayout);
            }
        }

        private void SetLegacySurfaceVisible(bool visible)
        {
            if (IsLegacySurface(linesRoot))
                linesRoot.gameObject.SetActive(visible);
            if (IsLegacySurface(xpBarBackground))
                xpBarBackground.gameObject.SetActive(visible);
            if (IsLegacySurface(badgesRoot))
                badgesRoot.gameObject.SetActive(visible);
            if (IsLegacySurface(starRatingDisplay))
                starRatingDisplay.gameObject.SetActive(visible);
            if (IsLegacySurface(cycleProgressDisplay))
                cycleProgressDisplay.gameObject.SetActive(visible);
        }

        private void EnsureStarRatingDisplay(bool createIfMissing)
        {
            if (!IsLegacySurface(starRatingDisplay))
                starRatingDisplay = null;

            if (starRatingDisplay == null)
                starRatingDisplay = transform.Find("StarRating")?.GetComponent<PartyTabStarRatingDisplay>();

            if (!IsLegacySurface(starRatingDisplay))
                starRatingDisplay = null;

            if (starRatingDisplay == null && createIfMissing)
                starRatingDisplay = CreateStarRatingRoot();

            if (starRatingDisplay == null)
                return;

            starRatingDisplay.Configure(starRatingArt);
            if (createIfMissing)
                starRatingDisplay.BuildDefaultLayoutForEditor();
        }

        private PartyTabStarRatingDisplay CreateStarRatingRoot()
        {
            RectTransform starRoot = PlayerMenuUiFactory.CreateRect("StarRating", transform);
            RectTransform starRect = starRoot;
            starRect.anchorMin = new Vector2(0.5f, 1f);
            starRect.anchorMax = new Vector2(0.5f, 1f);
            starRect.pivot = new Vector2(0.5f, 1f);
            starRect.anchoredPosition = new Vector2(0f, -12f);
            starRect.sizeDelta = new Vector2(340f, 48f);

            Transform background = transform.Find("Background");
            starRoot.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

            PartyTabStarRatingDisplay display = starRoot.gameObject.AddComponent<PartyTabStarRatingDisplay>();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(starRoot.gameObject, "Create Star Rating");
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
            return display;
        }

        private void EnsureCycleProgressDisplay(bool createIfMissing)
        {
            if (useAuthoredViewLayouts)
            {
                ClearLegacyCycleProgressReference();
                return;
            }

            if (!IsLegacySurface(cycleProgressDisplay))
                cycleProgressDisplay = null;

            if (cycleProgressDisplay == null)
                cycleProgressDisplay = transform.Find("CycleProgress")?.GetComponent<PartyTabCycleProgressDisplay>();

            if (!IsLegacySurface(cycleProgressDisplay))
                cycleProgressDisplay = null;

            if (cycleProgressDisplay == null && createIfMissing)
                cycleProgressDisplay = CreateCycleProgressRoot();

            if (cycleProgressDisplay == null)
                return;

            cycleProgressDisplay.Configure(cycleTypography);
            if (createIfMissing)
                cycleProgressDisplay.BuildDefaultLayoutForEditor();
        }

        private void ClearLegacyCycleProgressReference()
        {
            cycleProgressDisplay = null;
        }

        private PartyTabCycleProgressDisplay CreateCycleProgressRoot()
        {
            RectTransform cycleRoot = PlayerMenuUiFactory.CreateRect("CycleProgress", transform);
            cycleRoot.anchorMin = new Vector2(0.08f, 0.67f);
            cycleRoot.anchorMax = new Vector2(0.92f, 0.78f);
            cycleRoot.offsetMin = Vector2.zero;
            cycleRoot.offsetMax = Vector2.zero;

            Transform background = transform.Find("Background");
            cycleRoot.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

            PartyTabCycleProgressDisplay display = cycleRoot.gameObject.AddComponent<PartyTabCycleProgressDisplay>();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(cycleRoot.gameObject, "Create Cycle Progress");
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
            return display;
        }

        private RectTransform ResolveViewsRoot()
        {
            if (viewsRoot == null)
                viewsRoot = transform.Find("Views") as RectTransform;

            return viewsRoot;
        }

        private CanvasGroup ResolveViewContentGroup(bool createIfMissing)
        {
            if (viewContentGroup != null && viewsRoot != null && viewContentGroup.transform == viewsRoot)
                return viewContentGroup;

            ResolveViewsRoot();
            if (viewsRoot == null)
            {
                viewContentGroup = null;
                return null;
            }

            viewContentGroup = viewsRoot.GetComponent<CanvasGroup>();
            bool createdGroup = false;
            if (viewContentGroup == null && createIfMissing)
            {
                viewContentGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(viewsRoot.gameObject);
                createdGroup = true;
            }

            if (viewContentGroup != null)
            {
                if (createdGroup && viewContentGroup.alpha <= 0f)
                    viewContentGroup.alpha = 1f;
                viewContentGroup.interactable = false;
                viewContentGroup.blocksRaycasts = false;
            }

            return viewContentGroup;
        }

        private bool IsLegacySurface(Component component)
        {
            if (component == null)
                return false;

            ResolveViewsRoot();
            Transform target = component.transform;
            if (!target.IsChildOf(transform))
                return false;

            return viewsRoot == null || !target.IsChildOf(viewsRoot);
        }

        private void SanitizeLegacyReferences()
        {
            if (!IsLegacySurface(linesRoot))
                linesRoot = null;
            if (!IsLegacySurface(starRatingDisplay))
                starRatingDisplay = null;
            if (!IsLegacySurface(cycleProgressDisplay))
                cycleProgressDisplay = null;
            if (!IsLegacySurface(xpBarBackground))
                xpBarBackground = null;
            if (!IsLegacySurface(xpBarFill))
                xpBarFill = null;
            if (!IsLegacySurface(badgesRoot))
                badgesRoot = null;
            if (!IsLegacySurface(badgeTemplate))
                badgeTemplate = null;
            if (!IsLegacySurface(lineTemplate))
                lineTemplate = null;
        }

        private void BindStarRating(int filledCount, bool visible, bool playShimmer)
        {
            if (starRatingDisplay == null)
                return;

            starRatingDisplay.SetRating(filledCount, visible, playShimmer);
        }

        private void BindCycleProgress(string label, int tier, bool visible)
        {
            if (cycleProgressDisplay == null && visible)
                EnsureCycleProgressDisplay(createIfMissing: true);

            if (cycleProgressDisplay == null)
                return;

            cycleProgressDisplay.Configure(cycleTypography);
            cycleProgressDisplay.SetCycle(label, tier, visible);
        }

        private void AddLine(string label, string value, bool muted)
        {
            TMP_Text row = AcquireLine();
            if (row == null)
                return;

            row.gameObject.SetActive(true);
            row.color = muted ? PlayerMenuUiFactory.Muted : PlayerMenuUiFactory.Text;
            row.text = string.IsNullOrWhiteSpace(label) ? value : $"{label}: {value}";
        }

        private TMP_Text AcquireLine()
        {
            for (int i = 0; i < linePool.Count; i++)
            {
                if (linePool[i] != null && !linePool[i].gameObject.activeSelf)
                    return linePool[i];
            }

            if (lineTemplate == null || linesRoot == null)
                return null;

            TMP_Text clone = Instantiate(lineTemplate, linesRoot);
            linePool.Add(clone);
            return clone;
        }

        private Image AcquireBadgeIcon()
        {
            for (int i = 0; i < badgePool.Count; i++)
            {
                if (badgePool[i] != null && !badgePool[i].gameObject.activeSelf)
                {
                    badgePool[i].gameObject.SetActive(true);
                    return badgePool[i];
                }
            }

            if (badgeTemplate == null || badgesRoot == null)
                return null;

            Image clone = Instantiate(badgeTemplate, badgesRoot);
            badgePool.Add(clone);
            clone.gameObject.SetActive(true);
            return clone;
        }

        private void ClearLines()
        {
            for (int i = 0; i < linePool.Count; i++)
            {
                if (linePool[i] != null)
                    linePool[i].gameObject.SetActive(false);
            }
        }

        private void ClearBadges()
        {
            for (int i = 0; i < badgePool.Count; i++)
            {
                if (badgePool[i] != null)
                    badgePool[i].gameObject.SetActive(false);
            }
        }

        private void SetXpBarVisible(bool visible)
        {
            if (xpBarBackground != null)
            {
                xpBarBackground.gameObject.SetActive(visible);
                xpBarBackground.enabled = visible;
            }

            if (xpBarFill != null)
            {
                xpBarFill.gameObject.SetActive(visible);
                xpBarFill.enabled = visible;
            }
        }

        private void SetLineSurfaceVisible(bool visible)
        {
            if (linesRoot != null)
                linesRoot.gameObject.SetActive(visible);
        }

        private void SetBadgesVisible(bool visible)
        {
            if (badgesRoot != null)
                badgesRoot.gameObject.SetActive(visible);

            if (badgeTemplate != null)
                badgeTemplate.gameObject.SetActive(false);
        }
    }
}
