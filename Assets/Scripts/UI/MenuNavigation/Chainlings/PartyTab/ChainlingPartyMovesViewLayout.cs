using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyMovesViewLayout : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private PartyTabMoveRarityPaletteSO rarityPalette;

        [Header("Header")]
        [SerializeField] private TMP_Text slotCountText;
        [SerializeField] private string slotCountFormat = "{0}/{1} moves equipped";

        [Header("Layout")]
        [SerializeField] private RectTransform viewportRoot;
        [SerializeField] private RectTransform moveRowsRoot;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private LayoutElement viewportLayoutElement;
        [SerializeField] private float rowSpacing = 4f;
        [SerializeField] private int visibleRowsBeforeScroll = MoveLoadoutSlotRules.VisibleRowsBeforeScroll;
        [SerializeField] private int maxAuthoredRows = MoveLoadoutSlotRules.MaxMoveSlots;
        [SerializeField] private bool resetScrollOnBind = true;
        [Tooltip("When enabled, runtime binding keeps the prefab-authored viewport RectTransform instead of pinning it to a generated default.")]
        [SerializeField] private bool preserveAuthoredViewportRect = true;

        [Header("Overflow Scroll")]
        [SerializeField] private bool autoScrollOverflowRows = true;
        [SerializeField] private float autoScrollPixelsPerSecond = 18f;
        [SerializeField] private float autoScrollEdgeHoldSeconds = 0.8f;
        [SerializeField] private float viewportHorizontalPadding = 8f;
        [SerializeField] private float viewportBottomPadding = 8f;

        [Header("Move Row Presentation")]
        [SerializeField] private bool animateRowsOnBind = true;
        [SerializeField, Min(0f)] private float rowPresentationStaggerSeconds = 0.045f;
        [SerializeField] private float rowPresentationStartYOffset = 14f;
        [SerializeField] private float rowPresentationOvershootYOffset = -3f;
        [SerializeField, Min(0f)] private float rowPresentationFallDuration = 0.18f;
        [SerializeField, Min(0f)] private float rowPresentationSettleDuration = 0.12f;
        [SerializeField] private float rowPresentationScaleOvershootY = 1.04f;

        [Header("Typography Defaults")]
        [SerializeField] private TMP_FontAsset rowFont;
        [SerializeField] private float rowFontSize = 18f;
        [SerializeField] private float rowHeight = 30f;

        [Header("Authored Rows")]
        [SerializeField] private List<ChainlingPartyMoveRowDisplay> authoredMoveRows = new List<ChainlingPartyMoveRowDisplay>();

        private int boundRowCount;
        private bool shouldAutoScroll;
        private float autoScrollOffsetPixels;
        private float autoScrollHoldTimer;
        private int autoScrollDirection = 1;
        private readonly List<Tween> rowPresentationTweens = new List<Tween>();
        private readonly Dictionary<RectTransform, Vector2> rowBasePositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> rowBaseScales = new Dictionary<RectTransform, Vector3>();

        public TypeIconPaletteSO TypeIconPalette => typeIconPalette;
        public PartyTabMoveRarityPaletteSO RarityPalette => rarityPalette;
        public IReadOnlyList<ChainlingPartyMoveRowDisplay> AuthoredMoveRows => authoredMoveRows;

        private void Update()
        {
            TickAutoScroll(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            StopRowPresentationTweens(reset: true);
        }

        public void ConfigurePalettes(TypeIconPaletteSO icons, PartyTabMoveRarityPaletteSO rarity)
        {
            if (icons != null)
                typeIconPalette = icons;
            if (rarity != null)
                rarityPalette = rarity;
        }

        public void Bind(ChainlingPartyColumnSummary summary)
        {
            EnsureReferences(false);
            CacheAuthoredRows();

            if (summary == null || summary.isEmpty)
            {
                SetSlotCountText("Moves unavailable");
                BindRows(null, "Assign at a PC.");
                return;
            }

            int maxSlots = summary.moveMaxSlots > 0
                ? summary.moveMaxSlots
                : MoveLoadoutSlotRules.ResolveMaxMoveSlots(0);
            int equippedCount = summary.moveEquippedCount > 0
                ? summary.moveEquippedCount
                : summary.moveRows != null ? summary.moveRows.Count : 0;
            SetSlotCountText(string.Format(slotCountFormat, equippedCount, maxSlots));
            BindRows(summary.moveRows, "No moves equipped.");
        }

        public void BindEmpty(string message)
        {
            EnsureReferences(false);
            CacheAuthoredRows();
            SetSlotCountText("Moves unavailable");
            BindRows(null, string.IsNullOrWhiteSpace(message) ? "Assign at a PC." : message);
        }

        public void CopyFirstRowPresentationToAuthoredRows()
        {
            EnsureReferences(false);
            CacheAuthoredRows();
            ChainlingPartyMoveRowDisplay template = ResolveTemplateRow();
            if (template == null)
                return;

            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                ChainlingPartyMoveRowDisplay row = authoredMoveRows[i];
                if (row == null || row == template)
                    continue;

                row.CopyPresentationFrom(template);
            }
        }

#if UNITY_EDITOR
        public void BuildDefaultLayoutForEditor()
        {
            EnsureReferences(createIfMissing: true);
            CacheAuthoredRows();
            ApplyLayoutDefaults();

            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                ChainlingPartyMoveRowDisplay row = authoredMoveRows[i];
                if (row == null)
                    continue;
                row.ApplyEditorDefaults(rowFont, rowHeight);
            }
        }
#endif

        private void BindRows(IReadOnlyList<ChainlingPartyMoveRowSummary> rows, string emptyMessage)
        {
            StopRowPresentationTweens(reset: true);

            int rowCount = rows != null ? rows.Count : 0;
            int displayCount = rowCount > 0 ? rowCount : 1;
            EnsureRowCapacity(displayCount);
            RestoreOwnedRowsToStableScale();

            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                ChainlingPartyMoveRowDisplay rowDisplay = authoredMoveRows[i];
                if (rowDisplay == null)
                    continue;

                if (i >= displayCount)
                {
                    rowDisplay.SetVisible(false);
                    continue;
                }

                ChainlingPartyMoveRowSummary rowData = rowCount > 0 && i < rowCount
                    ? rows[i]
                    : new ChainlingPartyMoveRowSummary
                    {
                        isEmpty = true,
                        moveName = string.IsNullOrWhiteSpace(emptyMessage) ? "No moves equipped." : emptyMessage,
                        statLabel = "",
                    };
                rowDisplay.Bind(rowData, rarityPalette, typeIconPalette);
            }

            ApplyScrollState(rowCount);
            PlayRowPresentation(rowCount);
        }

        private void SetSlotCountText(string value)
        {
            if (slotCountText != null)
                slotCountText.text = value ?? "";
        }

        private void ApplyScrollState(int equippedCount)
        {
            boundRowCount = Mathf.Max(0, equippedCount);
            int visibleRows = Mathf.Clamp(visibleRowsBeforeScroll, 1, MoveLoadoutSlotRules.MaxMoveSlots);
            float height = ResolveViewportHeight(visibleRows);

            if (viewportLayoutElement != null)
            {
                viewportLayoutElement.preferredHeight = height;
                viewportLayoutElement.minHeight = height;
            }

            if (!preserveAuthoredViewportRect)
                ApplyFixedViewportRect(height);

            bool shouldScroll = MoveLoadoutSlotRules.ShouldScrollMoveList(equippedCount);
            if (scrollRect != null)
            {
                scrollRect.vertical = shouldScroll;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = false;
            }

            shouldAutoScroll = shouldScroll && autoScrollOverflowRows;
            if (resetScrollOnBind || !shouldScroll)
                ResetScrollToTop();
        }

        private void PlayRowPresentation(int rowCount)
        {
            if (!animateRowsOnBind || !Application.isPlaying || rowCount <= 0)
                return;

            if (moveRowsRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(moveRowsRoot);
            Canvas.ForceUpdateCanvases();

            int count = Mathf.Min(rowCount, authoredMoveRows.Count);
            for (int i = 0; i < count; i++)
            {
                ChainlingPartyMoveRowDisplay rowDisplay = authoredMoveRows[i];
                if (rowDisplay == null || !rowDisplay.gameObject.activeInHierarchy)
                    continue;

                RectTransform rowRect = rowDisplay.transform as RectTransform;
                if (rowRect == null)
                    continue;

                CaptureRowBase(rowRect);
                Vector2 basePosition = rowBasePositions[rowRect];
                Vector3 baseScale = rowBaseScales[rowRect];
                if (baseScale.y <= 0.001f)
                    baseScale.y = 1f;
                Vector3 startScale = new Vector3(baseScale.x, 0f, baseScale.z);
                Vector3 overshootScale = new Vector3(
                    baseScale.x,
                    baseScale.y * Mathf.Max(0f, rowPresentationScaleOvershootY),
                    baseScale.z);
                float delay = i * Mathf.Max(0f, rowPresentationStaggerSeconds);
                float fallDuration = Mathf.Max(0.01f, rowPresentationFallDuration);
                float settleDuration = Mathf.Max(0.01f, rowPresentationSettleDuration);
                CanvasGroup rowGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(rowDisplay.gameObject);

                rowRect.anchoredPosition = basePosition + new Vector2(0f, rowPresentationStartYOffset);
                rowRect.localScale = startScale;
                rowGroup.alpha = 0f;
                rowGroup.interactable = false;
                rowGroup.blocksRaycasts = false;

                bool completed = false;
                Sequence sequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetTarget(rowRect);
                if (delay > 0f)
                    sequence.AppendInterval(delay);
                sequence.AppendCallback(() =>
                {
                    if (rowGroup != null)
                        rowGroup.alpha = 1f;
                });
                sequence.Append(rowRect
                    .DOScale(overshootScale, fallDuration)
                    .SetEase(Ease.OutSine));
                sequence.Join(rowRect
                    .DOAnchorPosY(basePosition.y + rowPresentationOvershootYOffset, fallDuration)
                    .SetEase(Ease.OutSine));
                sequence.Append(rowRect
                    .DOScale(baseScale, settleDuration)
                    .SetEase(Ease.InOutSine));
                sequence.Join(rowRect
                    .DOAnchorPosY(basePosition.y, settleDuration)
                    .SetEase(Ease.InOutSine));
                sequence.OnComplete(() =>
                {
                    completed = true;
                    if (rowGroup != null)
                        rowGroup.alpha = 1f;
                    if (rowRect != null)
                    {
                        rowRect.anchoredPosition = basePosition;
                        rowRect.localScale = baseScale;
                    }
                });
                sequence.OnKill(() =>
                {
                    if (completed || rowRect == null)
                        return;

                    if (rowGroup != null)
                        rowGroup.alpha = 1f;
                    rowRect.anchoredPosition = basePosition;
                    rowRect.localScale = baseScale;
                });
                rowPresentationTweens.Add(sequence);
            }
        }

        private void StopRowPresentationTweens(bool reset)
        {
            for (int i = rowPresentationTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = rowPresentationTweens[i];
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
            }
            rowPresentationTweens.Clear();

            if (!reset)
                return;

            foreach (KeyValuePair<RectTransform, Vector2> pair in rowBasePositions)
            {
                RectTransform row = pair.Key;
                if (row == null)
                    continue;

                row.anchoredPosition = pair.Value;
                if (rowBaseScales.TryGetValue(row, out Vector3 scale))
                    row.localScale = EnsurePositiveRowScale(scale);
                ResetRowCanvasGroup(row);
            }
        }

        private void CaptureRowBase(RectTransform row)
        {
            if (row == null)
                return;

            rowBasePositions[row] = row.anchoredPosition;
            rowBaseScales[row] = EnsurePositiveRowScale(row.localScale);
        }

        private void RestoreOwnedRowsToStableScale()
        {
            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                ChainlingPartyMoveRowDisplay rowDisplay = authoredMoveRows[i];
                if (rowDisplay == null || !IsOwnedMoveRow(rowDisplay))
                    continue;

                RectTransform rowRect = rowDisplay.transform as RectTransform;
                if (rowRect == null)
                    continue;

                rowRect.localScale = EnsurePositiveRowScale(rowRect.localScale);
                ResetRowCanvasGroup(rowRect);
            }
        }

        private static void ResetRowCanvasGroup(RectTransform row)
        {
            if (row == null)
                return;

            CanvasGroup group = row.GetComponent<CanvasGroup>();
            if (group == null)
                return;

            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static Vector3 EnsurePositiveRowScale(Vector3 scale)
        {
            if (scale.y <= 0.001f)
                scale.y = 1f;
            if (scale.x <= 0.001f)
                scale.x = 1f;
            if (scale.z <= 0.001f)
                scale.z = 1f;
            return scale;
        }

        private float ResolveViewportHeight(int visibleRows)
        {
            float effectiveRowHeight = ResolveEffectiveRowHeight();
            return (visibleRows * effectiveRowHeight) + (Mathf.Max(0, visibleRows - 1) * rowSpacing);
        }

        private void ApplyFixedViewportRect(float height)
        {
            if (viewportRoot == null)
                return;

            viewportRoot.anchorMin = new Vector2(0f, 1f);
            viewportRoot.anchorMax = new Vector2(1f, 1f);
            viewportRoot.pivot = new Vector2(0.5f, 1f);
            viewportRoot.offsetMin = new Vector2(viewportHorizontalPadding, -viewportBottomPadding - height);
            viewportRoot.offsetMax = new Vector2(-viewportHorizontalPadding, -viewportBottomPadding);
        }

        private void ResetScrollToTop()
        {
            autoScrollOffsetPixels = 0f;
            autoScrollDirection = 1;
            autoScrollHoldTimer = shouldAutoScroll ? autoScrollEdgeHoldSeconds : 0f;

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
            ApplyScrollOffset(0f);
        }

        private void TickAutoScroll(float deltaTime)
        {
            if (!shouldAutoScroll || !isActiveAndEnabled || moveRowsRoot == null || deltaTime <= 0f)
                return;

            float maxOffset = ResolveMaxScrollOffset();
            if (maxOffset <= 0.01f)
                return;

            if (autoScrollHoldTimer > 0f)
            {
                autoScrollHoldTimer = Mathf.Max(0f, autoScrollHoldTimer - deltaTime);
                return;
            }

            autoScrollOffsetPixels += autoScrollDirection * Mathf.Max(0f, autoScrollPixelsPerSecond) * deltaTime;
            if (autoScrollOffsetPixels >= maxOffset)
            {
                autoScrollOffsetPixels = maxOffset;
                autoScrollDirection = -1;
                autoScrollHoldTimer = autoScrollEdgeHoldSeconds;
            }
            else if (autoScrollOffsetPixels <= 0f)
            {
                autoScrollOffsetPixels = 0f;
                autoScrollDirection = 1;
                autoScrollHoldTimer = autoScrollEdgeHoldSeconds;
            }

            ApplyScrollOffset(autoScrollOffsetPixels);
        }

        private float ResolveMaxScrollOffset()
        {
            int visibleRows = Mathf.Clamp(visibleRowsBeforeScroll, 1, MoveLoadoutSlotRules.MaxMoveSlots);
            int rowCount = Mathf.Max(boundRowCount, 0);
            if (rowCount <= visibleRows)
                return 0f;

            float effectiveRowHeight = ResolveEffectiveRowHeight();
            float contentHeight = (rowCount * effectiveRowHeight) + (Mathf.Max(0, rowCount - 1) * rowSpacing);
            float viewportHeight = ResolveViewportHeight(visibleRows);
            return Mathf.Max(0f, contentHeight - viewportHeight);
        }

        private void ApplyScrollOffset(float offsetPixels)
        {
            if (moveRowsRoot == null)
                return;

            Vector2 anchored = moveRowsRoot.anchoredPosition;
            anchored.y = Mathf.Max(0f, offsetPixels);
            moveRowsRoot.anchoredPosition = anchored;

            float maxOffset = ResolveMaxScrollOffset();
            if (scrollRect != null && maxOffset > 0.01f)
                scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(offsetPixels / maxOffset);
        }

        private void EnsureRowCapacity(int desiredCount)
        {
            CacheAuthoredRows();

            int targetCount = Mathf.Clamp(desiredCount, 1, Mathf.Max(1, maxAuthoredRows));
            if (authoredMoveRows.Count >= targetCount || moveRowsRoot == null)
                return;

            ChainlingPartyMoveRowDisplay template = ResolveTemplateRow();
            if (template == null)
                return;

            template.CaptureLayoutFromChildren();
            template.ApplySerializedPresentation();

            while (authoredMoveRows.Count < targetCount)
            {
                ChainlingPartyMoveRowDisplay clone = Instantiate(template, moveRowsRoot);
                clone.name = $"MoveRow_{authoredMoveRows.Count + 1:00}";
                clone.gameObject.SetActive(false);
                authoredMoveRows.Add(clone);
            }
        }

        private ChainlingPartyMoveRowDisplay ResolveTemplateRow()
        {
            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                if (authoredMoveRows[i] != null)
                    return authoredMoveRows[i];
            }

            return moveRowsRoot != null
                ? moveRowsRoot.GetComponentInChildren<ChainlingPartyMoveRowDisplay>(true)
                : null;
        }

        private void EnsureReferences(bool createIfMissing)
        {
            if (slotCountText == null)
                slotCountText = transform.Find("SlotCountText")?.GetComponent<TMP_Text>();

            if (viewportRoot == null)
                viewportRoot = transform.Find("MoveRowsViewport") as RectTransform;

            if (moveRowsRoot == null && viewportRoot != null)
                moveRowsRoot = viewportRoot.Find("MoveRows") as RectTransform;
            if (moveRowsRoot == null)
                moveRowsRoot = transform.Find("MoveRows") as RectTransform;

            if (scrollRect == null && viewportRoot != null)
                scrollRect = viewportRoot.GetComponent<ScrollRect>();
            if (viewportLayoutElement == null && viewportRoot != null)
                viewportLayoutElement = viewportRoot.GetComponent<LayoutElement>();

#if UNITY_EDITOR
            if (slotCountText == null && createIfMissing)
            {
                slotCountText = PlayerMenuUiFactory.CreateText(
                    "SlotCountText",
                    transform,
                    "0/4 moves equipped",
                    rowFontSize,
                    TextAlignmentOptions.MidlineLeft,
                    false);
                RectTransform rect = slotCountText.rectTransform;
                rect.anchorMin = new Vector2(0.04f, 0.82f);
                rect.anchorMax = new Vector2(0.96f, 0.98f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            if (viewportRoot == null && createIfMissing)
            {
                viewportRoot = PlayerMenuUiFactory.CreateRect("MoveRowsViewport", transform);
                viewportRoot.gameObject.AddComponent<RectMask2D>();
                viewportLayoutElement = viewportRoot.gameObject.AddComponent<LayoutElement>();
                scrollRect = viewportRoot.gameObject.AddComponent<ScrollRect>();
            }

            if (moveRowsRoot != null && viewportRoot != null && moveRowsRoot.parent != viewportRoot)
                moveRowsRoot.SetParent(viewportRoot, false);

            if (moveRowsRoot == null && viewportRoot != null && createIfMissing)
            {
                moveRowsRoot = PlayerMenuUiFactory.CreateRect("MoveRows", viewportRoot);
            }

            if (moveRowsRoot != null && createIfMissing)
            {
                PlayerMenuUiFactory.Stretch(moveRowsRoot, 0f, 0f, 0f, 0f);
            }
#endif

            if (scrollRect != null)
            {
                scrollRect.content = moveRowsRoot;
                scrollRect.viewport = viewportRoot;
            }

            if (moveRowsRoot != null)
            {
                VerticalLayoutGroup layout = moveRowsRoot.GetComponent<VerticalLayoutGroup>();
#if UNITY_EDITOR
                if (layout == null && createIfMissing)
                    layout = PlayerMenuUiFactory.VerticalLayout(moveRowsRoot.gameObject, rowSpacing);
#endif
                if (layout != null)
                {
                    layout.spacing = rowSpacing;
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }

#if UNITY_EDITOR
                ContentSizeFitter fitter = moveRowsRoot.GetComponent<ContentSizeFitter>();
                if (fitter == null && createIfMissing)
                    fitter = moveRowsRoot.gameObject.AddComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
#endif
            }

            CacheAuthoredRows();
        }

        private void CacheAuthoredRows()
        {
            authoredMoveRows.RemoveAll(item => item == null || !IsOwnedMoveRow(item));
            if (moveRowsRoot == null)
                return;

            ChainlingPartyMoveRowDisplay[] found = moveRowsRoot.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true);
            for (int i = 0; i < found.Length; i++)
            {
                ChainlingPartyMoveRowDisplay row = found[i];
                if (row != null && !authoredMoveRows.Contains(row))
                    authoredMoveRows.Add(row);
            }

            authoredMoveRows.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private bool IsOwnedMoveRow(ChainlingPartyMoveRowDisplay row)
        {
            if (row == null || moveRowsRoot == null)
                return false;

            return row.transform == moveRowsRoot || row.transform.IsChildOf(moveRowsRoot);
        }

        private float ResolveEffectiveRowHeight()
        {
            float fallback = Mathf.Max(1f, rowHeight);
            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                ChainlingPartyMoveRowDisplay row = authoredMoveRows[i];
                if (row == null || !IsOwnedMoveRow(row))
                    continue;

                LayoutElement element = row.LayoutElement != null
                    ? row.LayoutElement
                    : row.GetComponent<LayoutElement>();
                if (element != null)
                {
                    if (element.preferredHeight > 0f)
                        return element.preferredHeight;
                    if (element.minHeight > 0f)
                        return element.minHeight;
                }

                if (row.transform is RectTransform rect && rect.sizeDelta.y > 0f)
                    return rect.sizeDelta.y;
            }

            return fallback;
        }

        private void ApplyLayoutDefaults()
        {
            if (rowFont == null)
            {
                rowFont = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
                if (rowFont == null)
                    rowFont = Resources.Load<TMP_FontAsset>("Fonts/PixeloidSans SDF");
            }

            if (slotCountText != null)
            {
                if (rowFont != null)
                    slotCountText.font = rowFont;
                slotCountText.fontSize = rowFontSize;
                slotCountText.alignment = TextAlignmentOptions.MidlineLeft;
                slotCountText.enableWordWrapping = false;
                slotCountText.overflowMode = TextOverflowModes.Ellipsis;
            }

            maxAuthoredRows = Mathf.Clamp(maxAuthoredRows, 1, MoveLoadoutSlotRules.MaxMoveSlots);
            visibleRowsBeforeScroll = Mathf.Clamp(visibleRowsBeforeScroll, 1, maxAuthoredRows);
            ApplyFixedViewportRect(ResolveViewportHeight(visibleRowsBeforeScroll));

            for (int i = 0; i < authoredMoveRows.Count; i++)
            {
                if (authoredMoveRows[i] == null)
                    continue;

                authoredMoveRows[i].CaptureLayoutFromChildren();
                authoredMoveRows[i].ApplySerializedPresentation();
            }
        }
    }
}
