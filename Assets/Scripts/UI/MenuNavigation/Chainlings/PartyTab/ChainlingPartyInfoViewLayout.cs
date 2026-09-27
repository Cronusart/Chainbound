using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyInfoViewLayout : MonoBehaviour
    {
        [SerializeField] private ChainlingPartyViewMode viewMode = ChainlingPartyViewMode.General;
        [SerializeField] private RectTransform linesRoot;
        [SerializeField] private TMP_Text lineTemplate;
        [SerializeField] private List<TMP_Text> authoredLineSlots = new List<TMP_Text>();
        [SerializeField] private Color normalLineColor = PlayerMenuUiFactory.Text;
        [SerializeField] private Color mutedLineColor = PlayerMenuUiFactory.Muted;
        [SerializeField] private string labelValueSeparator = ": ";

        [Header("Optional View Pieces")]
        [SerializeField] private PartyTabStarRatingDisplay starRatingDisplay;
        [SerializeField] private bool showStarRating;
        [SerializeField] private PartyTabCycleTypographySO cycleTypography;
        [SerializeField] private PartyTabCycleProgressDisplay cycleProgressDisplay;
        [SerializeField] private bool showCycleProgress;
        [SerializeField] private Image xpBarBackground;
        [SerializeField] private Image xpBarFill;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private bool showXpBar;
        [SerializeField] private RectTransform badgesRoot;
        [SerializeField] private Image badgeTemplate;
        [SerializeField] private bool showBadges;
        [SerializeField] private int maxBadgeIcons = 3;

        [Header("XP Bar Animation")]
        [SerializeField] private bool animateXpBarOnBind = true;
        [SerializeField] private bool animateXpLabelOnBind = true;
        [SerializeField, Min(0f)] private float xpFillAnimationDuration = 0.46f;
        [SerializeField] private Ease xpFillAnimationEase = Ease.OutCubic;
        [SerializeField, HideInInspector] private bool migratedCycleProgressVisibility;

        private readonly List<TMP_Text> runtimeLinePool = new List<TMP_Text>();
        private readonly List<Image> runtimeBadgePool = new List<Image>();
        private Tween xpFillTween;
        private Tween xpLabelTween;
        private float pendingXpFill01;
        private int pendingXpEarned;
        private int pendingXpSize;
        private string pendingXpLabel = "";
        private bool deferProgressReveal;

        public ChainlingPartyViewMode ViewMode => viewMode;

        public void SetDeferProgressReveal(bool defer)
        {
            deferProgressReveal = defer;
        }

        public bool CanBindRuntime()
        {
            EnsureBuilt(createIfMissing: false);

            if (viewMode == ChainlingPartyViewMode.Moves)
                return GetComponent<ChainlingPartyMovesViewLayout>() != null;

            if (viewMode == ChainlingPartyViewMode.Attributes)
                return GetComponent<ChainlingPartyAttributesViewLayout>() != null
                    || lineTemplate != null
                    || authoredLineSlots.Count > 0;

            if (viewMode == ChainlingPartyViewMode.Detail)
                return GetComponent<ChainlingPartyEntryViewLayout>() != null
                    || lineTemplate != null
                    || authoredLineSlots.Count > 0;

            // Stats is prefab-authored only — never falls back to Instantiated line/badge pools.
            if (viewMode == ChainlingPartyViewMode.Stats)
                return GetComponent<ChainlingPartyStatsViewLayout>() != null;

            // Loadout look card is prefab-authored only.
            if (viewMode == ChainlingPartyViewMode.Loadout)
                return GetComponent<ChainlingPartyLoadoutViewLayout>() != null;

            if (lineTemplate != null || authoredLineSlots.Count > 0)
                return true;

            if (showStarRating && starRatingDisplay != null)
                return true;

            if (showCycleProgress && cycleProgressDisplay != null)
                return true;

            if (showXpBar && xpBarBackground != null)
                return true;

            return showBadges && badgesRoot != null;
        }

        public void ConfigureMode(ChainlingPartyViewMode mode)
        {
            viewMode = mode;
        }

        public void ApplyDefaultPresentationForMode()
        {
            showStarRating = viewMode == ChainlingPartyViewMode.General;
            showCycleProgress = viewMode == ChainlingPartyViewMode.General;
            showXpBar = viewMode == ChainlingPartyViewMode.General;
            // Badges live on Archive / Full Stat Sheet — not party Stats summary.
            showBadges = false;
            maxBadgeIcons = Mathf.Max(1, maxBadgeIcons);
        }

        public void ConfigureStarRatingArt(PartyTabStarRatingArtSO art)
        {
            if (starRatingDisplay != null)
                starRatingDisplay.Configure(art);
        }

        public void ConfigureCycleTypography(PartyTabCycleTypographySO typography)
        {
            if (typography != null)
                cycleTypography = typography;

            if (cycleProgressDisplay != null)
                cycleProgressDisplay.Configure(cycleTypography);
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt(createIfMissing: true);
        }

        public void Bind(
            ChainlingPartyColumnSummary summary,
            PartyTabStarRatingArtSO starRatingArt,
            PartyTabCycleTypographySO typography)
        {
            EnsureBuilt(createIfMissing: false);
            ConfigureStarRatingArt(starRatingArt);
            ConfigureCycleTypography(typography);

            if (viewMode == ChainlingPartyViewMode.Moves &&
                TryBindMovesViewLayout(summary))
            {
                return;
            }

            if (viewMode == ChainlingPartyViewMode.Attributes &&
                TryBindAttributesViewLayout(summary))
            {
                return;
            }

            if (viewMode == ChainlingPartyViewMode.Detail &&
                TryBindEntryViewLayout(summary))
            {
                return;
            }

            if (viewMode == ChainlingPartyViewMode.Stats)
            {
                if (TryBindStatsViewLayout(summary))
                    return;

                // Missing authored StatsViewLayout: fail closed without Instantiating lines/badges.
                BindStatsFallbackWithoutInstantiation();
                return;
            }

            if (viewMode == ChainlingPartyViewMode.Loadout)
            {
                if (TryBindLoadoutViewLayout(summary))
                    return;

                BindLoadoutFallbackWithoutInstantiation();
                return;
            }

            if (summary == null || summary.isEmpty)
            {
                SetLineSurfaceVisible(true);
                ClearLines();
                AddLine("", summary != null ? summary.emptyMessage : "Assign at a PC", true);
                BindStarRating(0, false, false);
                BindCycleProgress("", 0, false);
                SetXpBarVisible(false);
                SetXpLabel("", false);
                SetBadgesVisible(false);
                ClearBadges();
                return;
            }

            SetLineSurfaceVisible(true);
            BindStarRating(summary.starCount, showStarRating, showStarRating);
            BindCycleProgress(summary.cycleLabel, summary.cycleTier, showCycleProgress);

            ClearLines();
            if (summary.lines != null)
            {
                for (int i = 0; i < summary.lines.Count; i++)
                {
                    ChainlingPartyDisplayLine line = summary.lines[i];
                    AddLine(line.Label, line.Value, line.IsMuted);
                }
            }

            SetXpBarVisible(showXpBar);
            if (showXpBar)
                ApplyXpProgress(summary);
            else
                SetXpLabel("", false);

            bool badgesVisible = showBadges && summary.topBadges != null && summary.topBadges.Count > 0;
            SetBadgesVisible(badgesVisible);
            ClearBadges();
            if (badgesVisible)
            {
                int count = Mathf.Min(Mathf.Max(0, maxBadgeIcons), summary.topBadges.Count);
                for (int i = 0; i < count; i++)
                {
                    ChainlingPartyBadgeDisplay badge = summary.topBadges[i];
                    Image icon = AcquireBadgeIcon();
                    if (icon == null)
                        continue;

                    icon.sprite = badge.Icon;
                    icon.enabled = badge.Icon != null;
                    icon.color = badge.Unlocked ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                }
            }
        }

        private void BindStatsFallbackWithoutInstantiation()
        {
            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();
        }

        private void BindLoadoutFallbackWithoutInstantiation()
        {
            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();
        }

        private void EnsureBuilt(bool createIfMissing)
        {
            // Stats / Loadout career/look cards are entirely prefab-authored.
            // Never Instantiates Lines / Badges for these modes.
            if (viewMode == ChainlingPartyViewMode.Stats ||
                viewMode == ChainlingPartyViewMode.Loadout)
            {
                showBadges = false;
                return;
            }

            ApplyMigrationDefaults();

            // Attributes radar layout owns the surface — never (re)create Lines here.
            bool usesAttributesRadar = GetComponent<ChainlingPartyAttributesViewLayout>() != null;
            if (usesAttributesRadar)
            {
                linesRoot = null;
                lineTemplate = null;
            }
            else
            {
                if (linesRoot == null)
                    linesRoot = transform.Find("Lines") as RectTransform;

                if (linesRoot == null && createIfMissing)
                {
                    linesRoot = PlayerMenuUiFactory.CreateRect("Lines", transform);
                    PlayerMenuUiFactory.Stretch(linesRoot, 8f, 8f, 8f, 8f);
                    VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(linesRoot.gameObject, 4f);
                    layout.childControlWidth = true;
                    layout.childControlHeight = false;
                    layout.childForceExpandHeight = false;
                }

                if (lineTemplate == null && linesRoot != null)
                {
                    Transform existingTemplate = linesRoot.Find("LineTemplate");
                    if (existingTemplate != null)
                        lineTemplate = existingTemplate.GetComponent<TMP_Text>();
                }

                if (lineTemplate == null && linesRoot != null && createIfMissing)
                {
                    lineTemplate = PlayerMenuUiFactory.CreateText("LineTemplate", linesRoot, "Label: Value", 18f, TextAlignmentOptions.TopLeft, true);
                    lineTemplate.gameObject.SetActive(false);
                }

                BindAuthoredLineSlots();
                if (createIfMissing)
                    EnsureDefaultLineSlots();
            }

            if (starRatingDisplay == null)
                starRatingDisplay = GetComponentInChildren<PartyTabStarRatingDisplay>(true);

            if (showStarRating && starRatingDisplay == null && createIfMissing)
            {
                RectTransform starRoot = PlayerMenuUiFactory.CreateRect("StarRating", transform);
                starRoot.anchorMin = new Vector2(0.5f, 1f);
                starRoot.anchorMax = new Vector2(0.5f, 1f);
                starRoot.pivot = new Vector2(0.5f, 1f);
                starRoot.anchoredPosition = new Vector2(0f, -12f);
                starRoot.sizeDelta = new Vector2(340f, 48f);
                starRatingDisplay = starRoot.gameObject.AddComponent<PartyTabStarRatingDisplay>();
                starRatingDisplay.BuildDefaultLayoutForEditor();
            }

            if (cycleProgressDisplay == null)
            {
                Transform cycleRoot = transform.Find("CycleProgress");
                cycleProgressDisplay = cycleRoot != null
                    ? cycleRoot.GetComponent<PartyTabCycleProgressDisplay>()
                    : null;
            }

            if (viewMode != ChainlingPartyViewMode.General)
                showCycleProgress = false;

            if (showCycleProgress &&
                viewMode == ChainlingPartyViewMode.General &&
                cycleProgressDisplay == null &&
                createIfMissing)
            {
                RectTransform cycleRoot = PlayerMenuUiFactory.CreateRect("CycleProgress", transform);
                cycleRoot.anchorMin = new Vector2(0.08f, 0.67f);
                cycleRoot.anchorMax = new Vector2(0.92f, 0.78f);
                cycleRoot.offsetMin = Vector2.zero;
                cycleRoot.offsetMax = Vector2.zero;
                cycleProgressDisplay = cycleRoot.gameObject.AddComponent<PartyTabCycleProgressDisplay>();
                cycleProgressDisplay.Configure(cycleTypography);
                cycleProgressDisplay.BuildDefaultLayoutForEditor();
            }

            if (xpBarBackground == null)
                xpBarBackground = transform.Find("XpBarBackground")?.GetComponent<Image>();

            if (showXpBar && xpBarBackground == null && createIfMissing)
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

            if (showXpBar && xpBarFill == null && xpBarBackground != null && createIfMissing)
            {
                xpBarFill = PlayerMenuUiFactory.CreateImage("XpBarFill", xpBarBackground.transform, new Color(0.95f, 0.78f, 0.28f, 1f));
                PlayerMenuUiFactory.Stretch(xpBarFill.rectTransform, 0f, 0f, 0f, 0f);
                PartyTabProgressBarUtility.ConfigureRectFill(xpBarFill);
            }

            if (xpLabel == null)
                xpLabel = transform.Find("XpLabel")?.GetComponent<TMP_Text>();

            if (xpLabel == null && xpBarBackground != null)
                xpLabel = xpBarBackground.transform.Find("XpLabel")?.GetComponent<TMP_Text>();

            if (showXpBar && xpLabel == null && xpBarBackground != null && createIfMissing)
            {
                xpLabel = PlayerMenuUiFactory.CreateText(
                    "XpLabel",
                    xpBarBackground.transform,
                    "0 / 0 XP",
                    13f,
                    TextAlignmentOptions.Midline,
                    false);
                xpLabel.raycastTarget = false;
                xpLabel.color = new Color(1f, 0.96f, 0.78f, 1f);
                PlayerMenuUiFactory.Stretch(xpLabel.rectTransform, 4f, 0f, 4f, 0f);
            }

            if (badgesRoot == null)
                badgesRoot = transform.Find("Badges") as RectTransform;

            if (showBadges && badgesRoot == null && createIfMissing)
            {
                badgesRoot = PlayerMenuUiFactory.CreateRect("Badges", transform);
                badgesRoot.anchorMin = new Vector2(0.08f, 0.12f);
                badgesRoot.anchorMax = new Vector2(0.92f, 0.2f);
                badgesRoot.offsetMin = Vector2.zero;
                badgesRoot.offsetMax = Vector2.zero;
                PlayerMenuUiFactory.HorizontalLayout(badgesRoot.gameObject, 6f);
            }

            if (badgeTemplate == null && badgesRoot != null)
                badgeTemplate = badgesRoot.Find("BadgeTemplate")?.GetComponent<Image>();

            if (showBadges && badgeTemplate == null && badgesRoot != null && createIfMissing)
            {
                badgeTemplate = PlayerMenuUiFactory.CreateImage("BadgeTemplate", badgesRoot, Color.white);
                badgeTemplate.gameObject.SetActive(false);
                PlayerMenuUiFactory.LayoutElement(badgeTemplate.gameObject, preferredWidth: 28f, preferredHeight: 28f);
            }
        }

        private void BindAuthoredLineSlots()
        {
            authoredLineSlots.RemoveAll(item => item == null);
            if (linesRoot == null)
                return;

            TMP_Text[] authored = linesRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < authored.Length; i++)
            {
                TMP_Text text = authored[i];
                if (text == null || text == lineTemplate || authoredLineSlots.Contains(text))
                    continue;
                authoredLineSlots.Add(text);
            }
        }

        private void ApplyMigrationDefaults()
        {
            if (migratedCycleProgressVisibility)
                return;

            showCycleProgress = viewMode == ChainlingPartyViewMode.General;
            migratedCycleProgressVisibility = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void EnsureDefaultLineSlots()
        {
            if (linesRoot == null || lineTemplate == null)
                return;

            int desiredCount = ResolveDefaultLineSlotCount(viewMode);
            while (authoredLineSlots.Count < desiredCount)
            {
                TMP_Text clone = Instantiate(lineTemplate, linesRoot);
                clone.name = $"Line_{authoredLineSlots.Count + 1:00}";
                clone.gameObject.SetActive(false);
                authoredLineSlots.Add(clone);
            }
        }

        private void AddLine(string label, string value, bool muted)
        {
            TMP_Text row = AcquireLine();
            if (row == null)
                return;

            row.gameObject.SetActive(true);
            row.color = muted ? mutedLineColor : normalLineColor;
            row.text = string.IsNullOrWhiteSpace(label)
                ? value
                : $"{label}{labelValueSeparator}{value}";
        }

        private TMP_Text AcquireLine()
        {
            for (int i = 0; i < authoredLineSlots.Count; i++)
            {
                TMP_Text row = authoredLineSlots[i];
                if (row != null && !row.gameObject.activeSelf)
                    return row;
            }

            for (int i = 0; i < runtimeLinePool.Count; i++)
            {
                TMP_Text row = runtimeLinePool[i];
                if (row != null && !row.gameObject.activeSelf)
                    return row;
            }

            if (lineTemplate == null || linesRoot == null)
                return null;

            TMP_Text clone = Instantiate(lineTemplate, linesRoot);
            clone.gameObject.SetActive(false);
            runtimeLinePool.Add(clone);
            return clone;
        }

        private Image AcquireBadgeIcon()
        {
            for (int i = 0; i < runtimeBadgePool.Count; i++)
            {
                Image icon = runtimeBadgePool[i];
                if (icon != null && !icon.gameObject.activeSelf)
                {
                    icon.gameObject.SetActive(true);
                    return icon;
                }
            }

            if (badgeTemplate == null || badgesRoot == null)
                return null;

            Image clone = Instantiate(badgeTemplate, badgesRoot);
            clone.gameObject.SetActive(true);
            runtimeBadgePool.Add(clone);
            return clone;
        }

        private void ClearLines()
        {
            for (int i = 0; i < authoredLineSlots.Count; i++)
            {
                if (authoredLineSlots[i] != null)
                    authoredLineSlots[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < runtimeLinePool.Count; i++)
            {
                if (runtimeLinePool[i] != null)
                    runtimeLinePool[i].gameObject.SetActive(false);
            }
        }

        private void ClearBadges()
        {
            for (int i = 0; i < runtimeBadgePool.Count; i++)
            {
                if (runtimeBadgePool[i] != null)
                    runtimeBadgePool[i].gameObject.SetActive(false);
            }
        }

        private void BindStarRating(int filledCount, bool visible, bool playShimmer)
        {
            if (starRatingDisplay != null)
                starRatingDisplay.SetRating(filledCount, visible, playShimmer);
        }

        private void BindCycleProgress(string label, int tier, bool visible)
        {
            if (cycleProgressDisplay == null && visible)
                EnsureBuilt(createIfMissing: true);

            if (cycleProgressDisplay == null)
                return;

            cycleProgressDisplay.Configure(cycleTypography);
            cycleProgressDisplay.SetCycle(label, tier, visible);
        }

        private void SetXpBarVisible(bool visible)
        {
            if (!visible)
                StopXpTweens(complete: false);

            if (xpBarBackground != null)
            {
                xpBarBackground.gameObject.SetActive(visible);
                xpBarBackground.enabled = visible;
            }

            if (xpBarFill != null)
            {
                xpBarFill.gameObject.SetActive(visible);
                if (!visible)
                    xpBarFill.enabled = false;
            }
        }

        private void SetXpLabel(string value, bool visible)
        {
            if (xpLabel == null)
                return;

            string label = value ?? "";
            xpLabel.text = label;
            xpLabel.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(label));
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

        private void ApplyXpBarFill(float fill01)
        {
            if (xpBarFill == null)
                return;

            float targetFill = Mathf.Clamp01(fill01);
            StopXpFillTween(complete: false);

            // Tab enter owns the 0→target fill via ReplayEnterFill. Bind only stores the target.
            if (deferProgressReveal)
            {
                PartyTabProgressBarUtility.SetFill01(xpBarFill, 0f);
                return;
            }

            PartyTabProgressBarUtility.SetFill01(xpBarFill, targetFill);
        }

        private void ApplyXpProgress(ChainlingPartyColumnSummary summary)
        {
            pendingXpFill01 = summary != null ? summary.xpBarFill01 : 0f;
            pendingXpEarned = summary != null ? summary.xpSegmentEarned : 0;
            pendingXpSize = summary != null ? summary.xpSegmentSize : 0;
            pendingXpLabel = summary != null ? summary.xpLabel ?? "" : "";
            ApplyXpBarFill(pendingXpFill01);
            ApplyXpLabel(summary);
        }

        public void ReplayEnterFill(float delaySeconds)
        {
            if (!showXpBar || xpBarFill == null)
                return;

            StopXpTweens(complete: false);
            PartyTabProgressBarUtility.SetFill01(xpBarFill, 0f);
            if (pendingXpSize > 0)
                SetXpSegmentLabel(0, pendingXpSize);
            else
                SetXpLabel("", !string.IsNullOrWhiteSpace(pendingXpLabel));

            float duration = Mathf.Max(0.05f, xpFillAnimationDuration);
            xpFillTween = PartyTabProgressBarUtility
                .DOFill01(xpBarFill, pendingXpFill01, duration)
                .SetDelay(Mathf.Max(0f, delaySeconds))
                .SetEase(xpFillAnimationEase)
                .SetUpdate(true)
                .SetTarget(this);

            if (pendingXpSize > 0 && animateXpLabelOnBind)
            {
                xpLabelTween = DOVirtual
                    .Float(0f, pendingXpEarned, duration, value =>
                    {
                        SetXpSegmentLabel(Mathf.RoundToInt(value), pendingXpSize);
                    })
                    .SetDelay(Mathf.Max(0f, delaySeconds))
                    .SetEase(xpFillAnimationEase)
                    .SetUpdate(true)
                    .SetTarget(this)
                    .OnComplete(() => SetXpLabel(pendingXpLabel, true));
            }
        }

        private void ApplyXpLabel(ChainlingPartyColumnSummary summary)
        {
            StopXpLabelTween(complete: false);

            if (xpLabel == null)
                return;

            string finalLabel = summary != null ? summary.xpLabel ?? "" : "";
            bool visible = !string.IsNullOrWhiteSpace(finalLabel);
            xpLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                xpLabel.text = "";
                return;
            }

            bool canAnimateSegment =
                summary != null &&
                summary.hasXpSegment &&
                summary.xpSegmentSize > 0;

            if (deferProgressReveal && canAnimateSegment)
            {
                SetXpSegmentLabel(0, Mathf.Max(1, summary.xpSegmentSize));
                return;
            }

            if (!canAnimateSegment ||
                !Application.isPlaying ||
                deferProgressReveal ||
                !animateXpBarOnBind ||
                !animateXpLabelOnBind ||
                xpFillAnimationDuration <= 0f)
            {
                xpLabel.text = finalLabel;
                return;
            }

            int targetEarned = Mathf.Clamp(summary.xpSegmentEarned, 0, summary.xpSegmentSize);
            int segmentSize = Mathf.Max(1, summary.xpSegmentSize);
            SetXpSegmentLabel(0, segmentSize);

            xpLabelTween = DOVirtual
                .Float(0f, targetEarned, xpFillAnimationDuration, value =>
                {
                    SetXpSegmentLabel(Mathf.RoundToInt(value), segmentSize);
                })
                .SetEase(xpFillAnimationEase)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    xpLabel.text = finalLabel;
                });
        }

        private void SetXpSegmentLabel(int earned, int segmentSize)
        {
            if (xpLabel == null)
                return;

            int clampedSize = Mathf.Max(1, segmentSize);
            int clampedEarned = Mathf.Clamp(earned, 0, clampedSize);
            xpLabel.text = $"{clampedEarned} / {clampedSize} XP";
        }

        private void StopXpFillTween(bool complete)
        {
            if (xpFillTween == null)
                return;

            xpFillTween.Kill(complete);
            xpFillTween = null;
        }

        private void StopXpLabelTween(bool complete)
        {
            if (xpLabelTween == null)
                return;

            xpLabelTween.Kill(complete);
            xpLabelTween = null;
        }

        private void StopXpTweens(bool complete)
        {
            StopXpFillTween(complete);
            StopXpLabelTween(complete);
        }

        private void OnDisable()
        {
            StopXpTweens(complete: false);
        }

        private void OnDestroy()
        {
            StopXpTweens(complete: false);
        }

        private bool TryBindMovesViewLayout(ChainlingPartyColumnSummary summary)
        {
            ChainlingPartyMovesViewLayout movesLayout = GetComponent<ChainlingPartyMovesViewLayout>();
            if (movesLayout == null)
                return false;

            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();

            if (summary == null || summary.isEmpty)
                movesLayout.BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
            else
                movesLayout.Bind(summary);

            return true;
        }

        private bool TryBindAttributesViewLayout(ChainlingPartyColumnSummary summary)
        {
            ChainlingPartyAttributesViewLayout attributesLayout =
                GetComponent<ChainlingPartyAttributesViewLayout>();
            if (attributesLayout == null)
                return false;

            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();

            if (summary == null || summary.isEmpty)
                attributesLayout.BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
            else
                attributesLayout.Bind(summary);

            return true;
        }

        private bool TryBindEntryViewLayout(ChainlingPartyColumnSummary summary)
        {
            ChainlingPartyEntryViewLayout entryLayout = GetComponent<ChainlingPartyEntryViewLayout>();
            if (entryLayout == null)
                return false;

            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();

            if (summary == null || summary.isEmpty)
                entryLayout.BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
            else
                entryLayout.Bind(summary);

            return true;
        }

        private bool TryBindStatsViewLayout(ChainlingPartyColumnSummary summary)
        {
            ChainlingPartyStatsViewLayout statsLayout = GetComponent<ChainlingPartyStatsViewLayout>();
            if (statsLayout == null)
                return false;

            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();

            if (summary == null || summary.isEmpty)
                statsLayout.BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
            else
                statsLayout.Bind(summary);

            return true;
        }

        private bool TryBindLoadoutViewLayout(ChainlingPartyColumnSummary summary)
        {
            ChainlingPartyLoadoutViewLayout loadoutLayout = GetComponent<ChainlingPartyLoadoutViewLayout>();
            if (loadoutLayout == null)
                return false;

            if (linesRoot != null)
                linesRoot.gameObject.SetActive(false);

            BindStarRating(0, false, false);
            BindCycleProgress("", 0, false);
            SetXpBarVisible(false);
            SetXpLabel("", false);
            SetBadgesVisible(false);
            ClearBadges();

            if (summary == null || summary.isEmpty)
                loadoutLayout.BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
            else
                loadoutLayout.Bind(summary);

            return true;
        }

        private static int ResolveDefaultLineSlotCount(ChainlingPartyViewMode mode)
        {
            switch (mode)
            {
                case ChainlingPartyViewMode.General:
                    return 2;
                case ChainlingPartyViewMode.Attributes:
                    return 4;
                case ChainlingPartyViewMode.Moves:
                    return 4;
                case ChainlingPartyViewMode.Detail:
                    return 2;
                case ChainlingPartyViewMode.Loadout:
                    return 5;
                case ChainlingPartyViewMode.Stats:
                    return 6;
                default:
                    return 4;
            }
        }
    }
}
