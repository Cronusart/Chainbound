using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    public struct MoveRowSelectionTuning
    {
        public float nudgeOffsetX;
        public bool reserveSelectionArrowSpace;
        public float nudgeDuration;
        public Ease nudgeEase;
        public Ease returnEase;
        public float nudgeScalePeak;
        public float arrowFadeDuration;
        public float arrowScalePeak;
        public Ease arrowEase;
        public float bounceDuration;
        public Ease bounceEaseOut;
        public Ease bounceEaseIn;
        public float backdropFadeDuration;
        public Ease backdropFadeEase;

        public static MoveRowSelectionTuning CreateDefaults() =>
            new MoveRowSelectionTuning
            {
                nudgeOffsetX = 16f,
                reserveSelectionArrowSpace = false,
                nudgeDuration = 0.18f,
                nudgeEase = Ease.OutBack,
                returnEase = Ease.OutCubic,
                nudgeScalePeak = 1.02f,
                arrowFadeDuration = 0.12f,
                arrowScalePeak = 1.1f,
                arrowEase = Ease.OutBack,
                bounceDuration = 0.16f,
                bounceEaseOut = Ease.OutQuad,
                bounceEaseIn = Ease.OutBack,
                backdropFadeDuration = 0.18f,
                backdropFadeEase = Ease.OutCubic,
            };
    }

    /// <summary>
    /// Move-select row view. Prefers authored prefab hierarchy/colors; selection tints the frame
    /// via editor-exposed colors instead of rebuilding layout at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatMoveRowItemUGUI : MonoBehaviour
    {
        private const float DefaultPreferredHeight = 58f;

        [Header("References")]
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform contentRoot;
        [Tooltip("Primary frame / MoveRowBG Image. Selection colors are applied here.")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private RectTransform priorityPillRoot;
        [SerializeField] private Image priorityPillBackground;
        [SerializeField] private TMP_Text priorityPillText;
        [SerializeField] private Image typeIconImage;
        [SerializeField] private Image selectionArrowImage;
        [Tooltip("Always-visible row frame/backdrop. Tinted by Frame Colors below.")]
        [SerializeField] private Image selectionBackdropImage;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Frame Colors (Editor Controllable)")]
        [Tooltip("Applied to the frame/outline (MoveRowBG) when the row is not selected. Does not tint the backdrop fill.")]
        [SerializeField] private Color normalBackground = new Color(0.55f, 0.62f, 0.58f, 1f);
        [Tooltip("Applied to the frame/outline (MoveRowBG) when the row is selected. Does not tint the backdrop fill.")]
        [SerializeField] private Color selectedBackground = new Color(0.92f, 0.88f, 0.42f, 1f);
        [SerializeField] private Color disabledBackground = new Color(0.35f, 0.35f, 0.35f, 0.55f);

        [Header("Label Colors")]
        [SerializeField] private Color usableLabelColor = Color.white;
        [SerializeField] private Color unusableLabelColor = new Color(0.74f, 0.74f, 0.74f, 0.85f);
        [SerializeField] private Color selectionArrowColor = new Color(0.98f, 0.88f, 0.28f, 1f);

        [Header("Priority Pill")]
        [SerializeField] private Color priorityPillBackgroundColor = new Color(0.86f, 0.62f, 0.18f, 0.92f);
        [SerializeField] private Color priorityPillTextColor = new Color(1f, 0.95f, 0.78f, 1f);
        [SerializeField] private float priorityPillPaddingX = 8f;
        [SerializeField] private float priorityPillMinWidth = 56f;
        [SerializeField] private float priorityPillHeight = 22f;
        [SerializeField] private float priorityPillFontSize = 13f;
        [SerializeField] private float priorityPillSelectedHeight = 26f;
        [SerializeField] private float priorityPillSelectedFontSize = 15f;
        [SerializeField] private float priorityPillSelectedScale = 1.08f;
        [Tooltip("Inset from the row's right edge. Kept on the row root so selection nudge cannot clip the pill.")]
        [SerializeField] private float priorityPillRightInset = 10f;

        [Header("Layout")]
        [SerializeField] private float preferredRowHeight = DefaultPreferredHeight;

        [Header("Selection Feedback Fallbacks")]
        [SerializeField] private float selectScalePeak = 1.045f;
        [SerializeField] private float selectBounceDuration = 0.16f;
        [SerializeField] private Ease selectBounceEaseOut = Ease.OutQuad;
        [SerializeField] private Ease selectBounceEaseIn = Ease.OutBack;

        private Tween _scaleTween;
        private Tween _nudgeTween;
        private Tween _arrowTween;
        private Tween _backdropTween;
        private bool _selectionNudgeActive;
        private Color _authoredTypeIconColor = Color.white;
        private bool _authoredTypeIconColorCached;
        private Color _authoredBackdropColor = Color.white;
        private bool _authoredBackdropColorCached;
        private float _authoredContentAnchoredX;
        private bool _authoredContentAnchoredXCached;
        private Vector3 _authoredArrowScale = Vector3.one;
        private bool _authoredArrowScaleCached;
        private bool _isSelected;
        private bool _isUsable = true;
        private MoveRowSelectionTuning _activeTuning = MoveRowSelectionTuning.CreateDefaults();

        public RectTransform RowRect
        {
            get
            {
                EnsureReferences();
                return root;
            }
        }

        public RectTransform SelectionBoundsRect
        {
            get
            {
                EnsureReferences();
                return contentRoot != null ? contentRoot : root;
            }
        }

        public void ConfigureGeneratedDefaults(TMP_FontAsset fontOverride, float preferredHeight = 30f)
        {
            EnsureReferences();
            EnsureLayoutElement(preferredHeight);

            if (labelText != null)
            {
                if (fontOverride != null && labelText.font != fontOverride)
                    labelText.font = fontOverride;
                if (labelText.fontSize <= 0f)
                    labelText.fontSize = 20f;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.alignment = TextAlignmentOptions.Left;
            }

            if (typeIconImage != null)
                typeIconImage.preserveAspect = true;

            EnsureSelectionArrow();
        }

        public void SetVisible(bool visible)
        {
            EnsureReferences();
            if (!visible)
                ResetSelectionVisuals();
            else
                KillSelectionTweens(false);

            gameObject.SetActive(visible);
        }

        public void SetData(
            string label,
            Sprite typeIcon,
            bool isUsable,
            bool selected,
            bool hideTypeIcon = false,
            bool hasPriority = false)
        {
            EnsureReferences();
            EnsureLayoutElement(preferredRowHeight);

            _isUsable = isUsable;
            _isSelected = selected;

            if (labelText != null)
            {
                labelText.text = string.IsNullOrEmpty(label) ? "--" : label;
                labelText.color = isUsable ? usableLabelColor : unusableLabelColor;
            }

            SetPriorityPillVisible(hasPriority && !hideTypeIcon, isUsable, selected);

            EnsureBackdropVisible();
            RestoreAuthoredBackdropColor();
            ApplyFrameColor();

            if (typeIconImage != null)
            {
                CacheAuthoredTypeIconColor();
                if (hideTypeIcon)
                {
                    typeIconImage.sprite = null;
                    typeIconImage.enabled = false;
                }
                else
                {
                    typeIconImage.sprite = typeIcon;
                    typeIconImage.enabled = typeIcon != null;
                    var iconColor = _authoredTypeIconColor;
                    iconColor.a = isUsable ? _authoredTypeIconColor.a : _authoredTypeIconColor.a * 0.45f;
                    typeIconImage.color = iconColor;
                }
            }
        }

        public void ApplySelectionNudge(bool selected, bool animate, MoveRowSelectionTuning tuning)
        {
            EnsureReferences();
            _activeTuning = tuning;
            _isSelected = selected;
            EnsureBackdropVisible();
            RestoreAuthoredBackdropColor();
            ApplyFrameColor();
            CacheAuthoredContentAnchoredX();

            // Selected row shifts from authored X by nudgeOffsetX. Reserve mode parks every row
            // at authored+offset so the list never jumps (no visible select nudge).
            float targetX = _authoredContentAnchoredX;
            if (selected || tuning.reserveSelectionArrowSpace)
                targetX = _authoredContentAnchoredX + tuning.nudgeOffsetX;

            Ease ease = selected ? tuning.nudgeEase : tuning.returnEase;
            bool shouldAnimateNudge = animate
                                      && !tuning.reserveSelectionArrowSpace
                                      && tuning.nudgeDuration > 0f;
            float duration = shouldAnimateNudge ? tuning.nudgeDuration : 0f;
            _selectionNudgeActive = selected;

            KillNudgeTween();
            if (contentRoot == null)
            {
                UpdateSelectionArrowVisual(selected, animate, tuning);
                return;
            }

            if (duration <= 0f)
            {
                SetContentNudgeX(targetX);
            }
            else
            {
                _nudgeTween = contentRoot
                    .DOAnchorPosX(targetX, duration)
                    .SetEase(ease)
                    .SetUpdate(true)
                    .OnKill(() => _nudgeTween = null)
                    .OnComplete(() =>
                    {
                        _nudgeTween = null;
                        SetContentNudgeX(targetX);
                    });
            }

            // Pill lives on the row root (not RowContent) so the select nudge cannot clip it.
            RefreshPriorityPillLayout(selected);
            UpdateSelectionArrowVisual(selected, animate, tuning);
        }

        public void SetMoveBackdropVisible(bool visible, bool animate, MoveRowSelectionTuning tuning)
        {
            // Backdrop stays visible; selection tint goes to the frame/outline only.
            EnsureReferences();
            _activeTuning = tuning;
            EnsureBackdropVisible();
            RestoreAuthoredBackdropColor();
            ApplyFrameColor();
        }

        public void PlaySelectBounce(MoveRowSelectionTuning tuning)
        {
            EnsureReferences();
            _activeTuning = tuning;

            var bounceTarget = contentRoot != null ? contentRoot : root;
            if (bounceTarget == null)
                return;

            KillScaleTween();
            bounceTarget.localScale = Vector3.one;

            float scalePeak = tuning.nudgeScalePeak > 0f ? tuning.nudgeScalePeak : selectScalePeak;
            float duration = tuning.bounceDuration > 0f ? tuning.bounceDuration : selectBounceDuration;
            var easeOut = tuning.bounceEaseOut;
            var easeIn = tuning.bounceEaseIn;

            _scaleTween = bounceTarget
                .DOScale(scalePeak, duration * 0.45f)
                .SetEase(easeOut)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }

        public void PlaySelectBounce()
        {
            PlaySelectBounce(_activeTuning);
        }

        public void ResetSelectionVisuals()
        {
            EnsureReferences();
            _selectionNudgeActive = false;
            _isSelected = false;
            KillSelectionTweens(true);
            HideSelectionArrowImmediate();
            EnsureBackdropVisible();
            RestoreAuthoredBackdropColor();
            ApplyFrameColor();
            RefreshPriorityPillLayout(selected: false);
        }

        public void KillSelectionTweens(bool resetVisualState)
        {
            KillNudgeTween();
            KillScaleTween();
            KillArrowTween();
            KillBackdropTween();

            if (resetVisualState && contentRoot != null)
            {
                CacheAuthoredContentAnchoredX();
                float parkedX = _authoredContentAnchoredX;
                if (_activeTuning.reserveSelectionArrowSpace)
                    parkedX = _authoredContentAnchoredX + _activeTuning.nudgeOffsetX;
                SetContentNudgeX(parkedX);
                contentRoot.localScale = Vector3.one;
            }

            if (resetVisualState)
                HideSelectionArrowImmediate();
        }

        private void ApplyFrameColor()
        {
            // Selection tint applies only to the frame/outline (MoveRowBG), never the backdrop fill.
            if (backgroundImage == null)
                return;

            if (!_isUsable)
                backgroundImage.color = disabledBackground;
            else if (_isSelected)
                backgroundImage.color = selectedBackground;
            else
                backgroundImage.color = normalBackground;
        }

        private void EnsureBackdropVisible()
        {
            if (selectionBackdropImage == null)
                return;

            selectionBackdropImage.gameObject.SetActive(true);
            selectionBackdropImage.enabled = true;
            CacheAuthoredBackdropColor();
        }

        private void CacheAuthoredBackdropColor()
        {
            if (selectionBackdropImage == null || _authoredBackdropColorCached)
                return;

            _authoredBackdropColor = selectionBackdropImage.color;
            _authoredBackdropColorCached = true;
        }

        private void RestoreAuthoredBackdropColor()
        {
            if (selectionBackdropImage == null)
                return;

            CacheAuthoredBackdropColor();
            selectionBackdropImage.color = _authoredBackdropColor;
        }

        private void CacheAuthoredContentAnchoredX()
        {
            if (contentRoot == null || _authoredContentAnchoredXCached)
                return;

            _authoredContentAnchoredX = contentRoot.anchoredPosition.x;
            _authoredContentAnchoredXCached = true;
        }

        private void UpdateSelectionArrowVisual(bool selected, bool animate, MoveRowSelectionTuning tuning)
        {
            EnsureSelectionArrow();
            if (selectionArrowImage == null)
                return;

            CacheAuthoredArrowScale();
            KillArrowTween();

            Vector3 restScale = _authoredArrowScale;
            Vector3 introScale = _authoredArrowScale * 0.85f;
            float peakMultiplier = tuning.arrowScalePeak > 0f ? tuning.arrowScalePeak : 1.1f;
            Vector3 peakScale = _authoredArrowScale * peakMultiplier;

            if (!selected)
            {
                if (animate && tuning.arrowFadeDuration > 0f)
                {
                    var arrowRect = selectionArrowImage.rectTransform;
                    _arrowTween = DOTween.Sequence()
                        .SetUpdate(true)
                        .Join(selectionArrowImage.DOFade(0f, tuning.arrowFadeDuration))
                        .Join(arrowRect.DOScale(introScale, tuning.arrowFadeDuration).SetEase(tuning.returnEase))
                        .OnComplete(HideSelectionArrowImmediate);
                }
                else
                {
                    HideSelectionArrowImmediate();
                }

                return;
            }

            selectionArrowImage.gameObject.SetActive(true);
            selectionArrowImage.enabled = true;
            var color = selectionArrowImage.color;
            color.a = animate ? 0f : 1f;
            selectionArrowImage.color = color;

            var rect = selectionArrowImage.rectTransform;
            rect.localScale = animate ? introScale : restScale;

            if (animate && tuning.arrowFadeDuration > 0f)
            {
                _arrowTween = DOTween.Sequence()
                    .SetUpdate(true)
                    .Join(selectionArrowImage.DOFade(1f, tuning.arrowFadeDuration).SetEase(tuning.arrowEase))
                    .Join(rect.DOScale(peakScale, tuning.arrowFadeDuration).SetEase(tuning.arrowEase))
                    .Append(rect.DOScale(restScale, tuning.arrowFadeDuration * 0.65f).SetEase(tuning.arrowEase));
            }
            else
            {
                color.a = 1f;
                selectionArrowImage.color = color;
                rect.localScale = restScale;
            }
        }

        private void HideSelectionArrowImmediate()
        {
            if (selectionArrowImage == null)
                return;

            CacheAuthoredArrowScale();
            selectionArrowImage.enabled = false;
            selectionArrowImage.gameObject.SetActive(false);
            var color = selectionArrowImage.color;
            color.a = 1f;
            selectionArrowImage.color = color;
            selectionArrowImage.rectTransform.localScale = _authoredArrowScale;
        }

        private void CacheAuthoredArrowScale()
        {
            if (selectionArrowImage == null || _authoredArrowScaleCached)
                return;

            var scale = selectionArrowImage.rectTransform.localScale;
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                scale = Vector3.one;

            _authoredArrowScale = scale;
            _authoredArrowScaleCached = true;
        }

        private void CacheAuthoredTypeIconColor()
        {
            if (typeIconImage == null || _authoredTypeIconColorCached)
                return;

            _authoredTypeIconColor = typeIconImage.color;
            _authoredTypeIconColorCached = true;
        }

        private void EnsureSelectionArrow()
        {
            if (selectionArrowImage != null)
                return;

            var existing = transform.Find("SelectionArrow");
            if (existing != null)
            {
                selectionArrowImage = existing.GetComponent<Image>();
                if (selectionArrowImage != null)
                    return;
            }

            // Do not invent arrow visuals when the prefab intentionally omitted them.
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureLayoutElement(preferredRowHeight);
            CacheAuthoredTypeIconColor();
            CacheAuthoredContentAnchoredX();
            CacheAuthoredArrowScale();
            EnsureBackdropVisible();
            RestoreAuthoredBackdropColor();
            ApplyFrameColor();
        }

        private void OnDisable()
        {
            KillSelectionTweens(true);
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            EnsureReferences();
            EnsureLayoutElement(preferredRowHeight);
        }

        private void SetContentNudgeX(float targetX)
        {
            if (contentRoot == null)
                return;

            var pos = contentRoot.anchoredPosition;
            pos.x = targetX;
            contentRoot.anchoredPosition = pos;
        }

        private void KillNudgeTween()
        {
            if (_nudgeTween == null)
                return;

            _nudgeTween.Kill();
            _nudgeTween = null;
        }

        private void KillArrowTween()
        {
            if (_arrowTween != null)
            {
                _arrowTween.Kill();
                _arrowTween = null;
            }

            if (selectionArrowImage != null)
            {
                selectionArrowImage.DOKill();
                selectionArrowImage.rectTransform.DOKill();
            }
        }

        private void KillScaleTween()
        {
            if (_scaleTween == null)
                return;

            _scaleTween.Kill();
            _scaleTween = null;
        }

        private void KillBackdropTween()
        {
            if (_backdropTween == null)
                return;

            _backdropTween.Kill();
            _backdropTween = null;
        }

        private void EnsureLayoutElement(float preferredHeight)
        {
            LayoutElement own = GetComponent<LayoutElement>();
            if (own == null)
                own = gameObject.AddComponent<LayoutElement>();

            // The preview row in the battle canvas was wired at the list's layout element.
            // Writes must stay on this row so the list's content height is left alone.
            if (layoutElement == null || layoutElement.gameObject != gameObject)
                layoutElement = own;

            float height = preferredHeight > 0f ? preferredHeight : DefaultPreferredHeight;
            if (layoutElement.preferredHeight <= 0f)
                layoutElement.preferredHeight = height;
            if (layoutElement.minHeight <= 0f)
                layoutElement.minHeight = layoutElement.preferredHeight;

            ApplyAuthoredHeightToRect(height);
        }

        private void ApplyAuthoredHeightToRect(float fallbackHeight)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || layoutElement == null)
                return;

            // A stretched vertical anchor is already driven by the parent width/height.
            if (Mathf.Abs(rect.anchorMax.y - rect.anchorMin.y) > 0.001f)
                return;
            if (rect.sizeDelta.y > 0.5f)
                return;

            float authored = layoutElement.preferredHeight > 0f
                ? layoutElement.preferredHeight
                : layoutElement.minHeight > 0f
                    ? layoutElement.minHeight
                    : fallbackHeight;
            if (authored <= 0f)
                return;

            Vector2 size = rect.sizeDelta;
            size.y = authored;
            rect.sizeDelta = size;
        }

        private void SetPriorityPillVisible(bool visible, bool isUsable, bool selected)
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
                Color textColor = priorityPillTextColor;
                if (!isUsable)
                    textColor.a *= 0.7f;
                priorityPillText.color = textColor;
            }

            if (priorityPillBackground != null)
            {
                Color bg = priorityPillBackgroundColor;
                if (!isUsable)
                    bg.a *= 0.55f;
                priorityPillBackground.color = bg;
            }

            RefreshPriorityPillLayout(selected);
        }

        private void EnsurePriorityPill()
        {
            // Keep the pill on the row root so RowContent's selection nudge cannot push it
            // outside the list clip / yellow frame and squash the label.
            Transform parent = root != null ? root : transform;
            if (parent == null)
                return;

            if (priorityPillRoot == null)
            {
                Transform existing = parent.Find("PriorityPill");
                if (existing == null && contentRoot != null)
                    existing = contentRoot.Find("PriorityPill");
                if (existing != null)
                    priorityPillRoot = existing as RectTransform;
            }

            if (priorityPillRoot == null)
            {
                var go = new GameObject("PriorityPill", typeof(RectTransform), typeof(Image));
                priorityPillRoot = go.GetComponent<RectTransform>();
                priorityPillRoot.SetParent(parent, false);

                priorityPillBackground = go.GetComponent<Image>();
                priorityPillBackground.color = priorityPillBackgroundColor;
                priorityPillBackground.raycastTarget = false;

                var textGo = new GameObject("Label", typeof(RectTransform));
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(priorityPillRoot, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(4f, 1f);
                textRect.offsetMax = new Vector2(-4f, -1f);
                priorityPillText = textGo.AddComponent<TextMeshProUGUI>();
                priorityPillText.text = MoveSelectDisplayUtility.PriorityPillLabel;
                priorityPillText.alignment = TextAlignmentOptions.Center;
                priorityPillText.color = priorityPillTextColor;
                priorityPillText.textWrappingMode = TextWrappingModes.NoWrap;
                priorityPillText.overflowMode = TextOverflowModes.Overflow;
                priorityPillText.raycastTarget = false;
                if (labelText != null && labelText.font != null)
                    priorityPillText.font = labelText.font;
            }
            else if (priorityPillRoot.parent != parent)
            {
                priorityPillRoot.SetParent(parent, false);
            }

            if (priorityPillBackground == null)
                priorityPillBackground = priorityPillRoot.GetComponent<Image>();
            if (priorityPillText == null)
                priorityPillText = priorityPillRoot.GetComponentInChildren<TMP_Text>(true);

            priorityPillRoot.anchorMin = new Vector2(1f, 0.5f);
            priorityPillRoot.anchorMax = new Vector2(1f, 0.5f);
            priorityPillRoot.pivot = new Vector2(1f, 0.5f);
            priorityPillRoot.anchoredPosition = new Vector2(-Mathf.Abs(priorityPillRightInset), 0f);
            priorityPillRoot.SetAsLastSibling();
            priorityPillRoot.gameObject.SetActive(false);
        }

        private void RefreshPriorityPillLayout(bool selected)
        {
            if (priorityPillRoot == null || !priorityPillRoot.gameObject.activeSelf)
                return;

            float height = selected ? priorityPillSelectedHeight : priorityPillHeight;
            float fontSize = selected ? priorityPillSelectedFontSize : priorityPillFontSize;
            float width = priorityPillMinWidth;

            if (priorityPillText != null)
            {
                priorityPillText.textWrappingMode = TextWrappingModes.NoWrap;
                priorityPillText.overflowMode = TextOverflowModes.Overflow;
                priorityPillText.fontSize = fontSize;
                priorityPillText.ForceMeshUpdate(true);
                width = Mathf.Max(
                    priorityPillMinWidth,
                    priorityPillText.preferredWidth + Mathf.Max(0f, priorityPillPaddingX) * 2f);
            }

            priorityPillRoot.anchorMin = new Vector2(1f, 0.5f);
            priorityPillRoot.anchorMax = new Vector2(1f, 0.5f);
            priorityPillRoot.pivot = new Vector2(1f, 0.5f);
            priorityPillRoot.anchoredPosition = new Vector2(-Mathf.Abs(priorityPillRightInset), 0f);
            priorityPillRoot.sizeDelta = new Vector2(width, height);
            float scale = selected ? Mathf.Max(1f, priorityPillSelectedScale) : 1f;
            priorityPillRoot.localScale = new Vector3(scale, scale, 1f);
        }

        private void EnsureReferences()
        {
            if (root == null)
                root = transform as RectTransform;

            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();

            if (contentRoot == null)
            {
                var contentTransform = transform.Find("RowContent");
                if (contentTransform != null)
                    contentRoot = contentTransform as RectTransform;
            }

            if (backgroundImage == null)
            {
                // Prefer authored frame (MoveRowBG) over the empty root Image.
                Transform frame = transform.Find("MoveRowBG");
                if (frame == null && contentRoot != null)
                    frame = contentRoot.Find("MoveRowBG");
                if (frame == null)
                {
                    var nested = GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < nested.Length; i++)
                    {
                        if (nested[i] != null && nested[i].name == "MoveRowBG")
                        {
                            frame = nested[i];
                            break;
                        }
                    }
                }

                if (frame != null)
                    backgroundImage = frame.GetComponent<Image>();
                else
                    backgroundImage = GetComponent<Image>();
            }

            if (labelText == null)
            {
                var labelTransform = transform.Find("Label");
                if (labelTransform == null)
                    labelTransform = transform.Find("Text (TMP)");
                if (labelTransform == null && contentRoot != null)
                {
                    labelTransform = contentRoot.Find("Label");
                    if (labelTransform == null)
                        labelTransform = contentRoot.Find("Text (TMP)");
                }
                if (labelTransform != null)
                    labelText = labelTransform.GetComponent<TMP_Text>();
                if (labelText == null)
                    labelText = GetComponentInChildren<TMP_Text>(true);
            }

            if (typeIconImage == null)
            {
                var iconTransform = transform.Find("TypeIcon");
                if (iconTransform == null)
                    iconTransform = transform.Find("Image");
                if (iconTransform == null && contentRoot != null)
                {
                    var imageBg = contentRoot.Find("ImageBG");
                    if (imageBg != null)
                        iconTransform = imageBg.Find("Image");
                    if (iconTransform == null)
                        iconTransform = contentRoot.Find("TypeIcon");
                    if (iconTransform == null)
                        iconTransform = contentRoot.Find("Image");
                }
                if (iconTransform != null)
                    typeIconImage = iconTransform.GetComponent<Image>();
            }

            if (selectionArrowImage == null)
            {
                var arrowTransform = transform.Find("SelectionArrow");
                if (arrowTransform != null)
                    selectionArrowImage = arrowTransform.GetComponent<Image>();
            }

            if (priorityPillRoot == null)
            {
                Transform pillTransform = transform.Find("PriorityPill");
                if (pillTransform == null && contentRoot != null)
                    pillTransform = contentRoot.Find("PriorityPill");
                if (pillTransform != null)
                {
                    priorityPillRoot = pillTransform as RectTransform;
                    priorityPillBackground = priorityPillRoot.GetComponent<Image>();
                    priorityPillText = priorityPillRoot.GetComponentInChildren<TMP_Text>(true);
                }
            }

            CacheAuthoredArrowScale();

            if (selectionBackdropImage == null)
            {
                Transform backdropTransform = transform.Find("MoveRowSelectionBackdrop");
                if (backdropTransform == null && contentRoot != null)
                    backdropTransform = contentRoot.Find("MoveRowSelectionBackdrop");
                if (backdropTransform != null)
                    selectionBackdropImage = backdropTransform.GetComponent<Image>();
            }

            if (backgroundImage != null)
                backgroundImage.raycastTarget = false;
            if (labelText != null)
                labelText.raycastTarget = false;
            if (typeIconImage != null)
                typeIconImage.raycastTarget = false;
            if (selectionArrowImage != null)
                selectionArrowImage.raycastTarget = false;
            if (selectionBackdropImage != null)
                selectionBackdropImage.raycastTarget = false;
        }
    }
}
