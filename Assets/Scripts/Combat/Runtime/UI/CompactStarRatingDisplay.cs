using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Dense star row for Inventory Use / combat HUD. Static sprites, optional RTL fill,
    /// scale/stagger on filled stars — no shimmer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompactStarRatingDisplay : MonoBehaviour
    {
        private sealed class StarSlot
        {
            public RectTransform cell;
            public Image emptyImage;
            public Image filledImage;
            public RectTransform filledRect;
            public Tween presentationTween;
            public Vector3 baseScale = Vector3.one;
            public bool capturedBase;
        }

        [SerializeField] private CompactStarRatingArtSO art;
        [SerializeField] private RectTransform layoutRoot;
        [SerializeField, Tooltip("Copies spacing and cell size from the art asset on SetRating. Leave off when the star row is laid out on the prefab.")]
        private bool applyArtLayoutOverrides = true;

        private readonly StarSlot[] slots = new StarSlot[CombatConstants.STAR_MAX];
        private bool slotsBound;
        private Tween masteryTween;

        public void Configure(CompactStarRatingArtSO ratingArt)
        {
            if (ratingArt != null)
                art = ratingArt;
        }

        public void BuildDefaultLayoutForEditor()
        {
            BuildOrBindHierarchy(createIfMissing: true);
        }

        public void SetRating(int filledCount, bool visible, bool playPresentation)
        {
            BuildOrBindHierarchy(createIfMissing: false);
            StopPresentationTweens(reset: true);

            if (visible && !gameObject.activeSelf)
                gameObject.SetActive(true);
            gameObject.SetActive(visible);
            if (!visible || !slotsBound)
                return;

            if (applyArtLayoutOverrides)
                ApplyArtLayoutOverrides();

            int clamped = Mathf.Clamp(filledCount, 0, CombatConstants.STAR_MAX);
            bool rtl = art == null || art.FillRightToLeft;

            for (int i = 0; i < slots.Length; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.emptyImage == null || slot.filledImage == null)
                    continue;

                // RTL: hierarchy index 0 is rightmost when reverseArrangement is on.
                bool filled = rtl ? i < clamped : i < clamped;
                ApplyEmpty(slot);
                if (filled)
                    ApplyFilled(slot);
                else
                    HideFilled(slot);
            }

            if (playPresentation &&
                Application.isPlaying &&
                (art == null || art.AnimateFilledPresentation))
            {
                PlayFilledPresentation(clamped);
            }
        }

        public void ResolveScaleUpMotion(out float startScale, out float overshootScale, out float scaleInDuration, out float settleDuration, float totalDuration)
        {
            startScale = art != null ? art.FilledStartScale : 0.72f;
            overshootScale = art != null ? art.FilledOvershootScale : 1.12f;
            float authoredIn = art != null ? Mathf.Max(0.01f, art.FilledScaleInDuration) : 0.12f;
            float authoredSettle = art != null ? Mathf.Max(0.01f, art.FilledSettleDuration) : 0.11f;
            float authoredTotal = authoredIn + authoredSettle;
            float duration = Mathf.Max(0.05f, totalDuration);
            scaleInDuration = duration * (authoredIn / authoredTotal);
            settleDuration = Mathf.Max(0.01f, duration - scaleInDuration);
        }

        public Tween PlayStarGain(int filledCount, float duration)
        {
            BuildOrBindHierarchy(createIfMissing: false);
            if (!slotsBound)
                return null;

            int clamped = Mathf.Clamp(filledCount, 0, CombatConstants.STAR_MAX);
            if (clamped <= 0)
                return null;

            int slotIndex = clamped - 1;
            StarSlot slot = slots[slotIndex];
            if (slot?.filledRect == null || slot.filledImage == null)
                return null;

            ApplyFilled(slot);
            ResolveScaleUpMotion(out float startScale, out float overshootScale, out float scaleIn, out float settle, duration);
            CaptureBase(slot);
            Vector3 from = slot.baseScale * Mathf.Max(0f, startScale);
            Vector3 mid = slot.baseScale * Mathf.Max(0f, overshootScale);
            slot.filledRect.localScale = from;

            slot.presentationTween?.Kill(false);
            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(slot.filledRect);
            sequence.Append(slot.filledRect.DOScale(mid, scaleIn).SetEase(Ease.OutSine));
            sequence.Append(slot.filledRect.DOScale(slot.baseScale, settle).SetEase(Ease.InOutSine));
            slot.presentationTween = sequence;
            sequence.Pause();
            return sequence;
        }

        /// <summary>
        /// Plays one restrained five-star mastery sweep without changing the displayed rating.
        /// The returned tween is paused so callers can compose it into an authoritative reveal queue.
        /// </summary>
        public Tween PlayMasteryCycle(float duration = 0.8f)
        {
            BuildOrBindHierarchy(createIfMissing: false);
            if (!slotsBound)
                return null;

            SetRating(CombatConstants.STAR_MAX, true, false);
            masteryTween?.Kill(false);
            DOTween.Kill(this, false);

            const float authoredStagger = 0.06f;
            const float authoredDim = 0.07f;
            const float authoredRise = 0.12f;
            const float authoredSettle = 0.11f;
            const float authoredPulse = 0.16f;
            float authoredTotal = ((CombatConstants.STAR_MAX - 1) * authoredStagger) + authoredDim + authoredRise + authoredSettle + authoredPulse;
            float scale = Mathf.Max(0.2f, duration / authoredTotal);
            float stagger = authoredStagger * scale;
            float dimDuration = authoredDim * scale;
            float riseDuration = authoredRise * scale;
            float settleDuration = authoredSettle * scale;
            float pulseDuration = authoredPulse * scale;

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < CombatConstants.STAR_MAX; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.filledRect == null || slot.filledImage == null)
                    continue;

                ApplyFilled(slot);
                CaptureBase(slot);
                Color fullColor = slot.filledImage.color;
                Color dimColor = new Color(fullColor.r, fullColor.g, fullColor.b, fullColor.a * 0.25f);
                float at = i * stagger;
                sequence.Insert(at, slot.filledImage.DOColor(dimColor, dimDuration).SetEase(Ease.OutSine));
                sequence.Insert(at, slot.filledRect.DOScale(slot.baseScale * 0.86f, dimDuration).SetEase(Ease.InSine));
                sequence.Insert(at + dimDuration, slot.filledImage.DOColor(fullColor, riseDuration).SetEase(Ease.OutSine));
                sequence.Insert(at + dimDuration, slot.filledRect.DOScale(slot.baseScale * 1.15f, riseDuration).SetEase(Ease.OutBack));
                sequence.Insert(at + dimDuration + riseDuration, slot.filledRect.DOScale(slot.baseScale, settleDuration).SetEase(Ease.InOutSine));
            }

            if (layoutRoot != null)
            {
                Vector3 rowRest = layoutRoot.localScale.sqrMagnitude > 0.0001f ? layoutRoot.localScale : Vector3.one;
                float pulseAt = ((CombatConstants.STAR_MAX - 1) * stagger) + dimDuration + riseDuration + settleDuration;
                sequence.Insert(pulseAt, layoutRoot.DOScale(rowRest * 1.04f, pulseDuration * 0.45f).SetEase(Ease.OutSine));
                sequence.Insert(pulseAt + (pulseDuration * 0.45f), layoutRoot.DOScale(rowRest, pulseDuration * 0.55f).SetEase(Ease.InOutSine));
            }

            masteryTween = sequence;
            sequence.OnKill(() => masteryTween = null);
            sequence.Pause();
            return sequence;
        }

        private void Awake()
        {
            BuildOrBindHierarchy(createIfMissing: false);
        }

        private void OnDisable()
        {
            StopPresentationTweens(reset: true);
        }

        private void BuildOrBindHierarchy(bool createIfMissing)
        {
            if (slotsBound)
                return;

            if (TryBindAuthored())
                return;

            if (!createIfMissing)
                return;

            EnsureLayoutRoot();
            EnsureCells();
            slotsBound = TryBindAuthored();
        }

        private bool TryBindAuthored()
        {
            if (layoutRoot == null)
            {
                layoutRoot = transform.Find("StarRowLayout") as RectTransform;
                if (layoutRoot == null)
                    layoutRoot = GetComponentInChildren<HorizontalLayoutGroup>(true)?.transform as RectTransform;
            }

            if (layoutRoot == null || layoutRoot.childCount < CombatConstants.STAR_MAX)
                return false;

            for (int i = 0; i < CombatConstants.STAR_MAX; i++)
            {
                RectTransform cell = layoutRoot.GetChild(i) as RectTransform;
                if (cell == null)
                    return false;

                Image empty = FindImage(cell, "EmptyStar") ?? FindImage(cell, "Empty") ?? cell.GetComponent<Image>();
                Image filled = FindImage(cell, "FilledStar") ?? FindImage(cell, "Filled");
                if (empty == null)
                    return false;

                if (filled == null)
                {
                    filled = CreateLayer("FilledStar", cell);
                    CopyRect(empty.rectTransform, filled.rectTransform);
                }

                empty.raycastTarget = false;
                filled.raycastTarget = false;
                filled.enabled = false;

                slots[i] = new StarSlot
                {
                    cell = cell,
                    emptyImage = empty,
                    filledImage = filled,
                    filledRect = filled.rectTransform,
                };
            }

            slotsBound = true;
            return true;
        }

        private void EnsureLayoutRoot()
        {
            if (layoutRoot != null)
                return;

            layoutRoot = transform.Find("StarRowLayout") as RectTransform;
            bool created = false;
            if (layoutRoot == null)
            {
                created = true;
                GameObject go = new GameObject("StarRowLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(transform, false);
                layoutRoot = go.GetComponent<RectTransform>();
                layoutRoot.anchorMin = Vector2.zero;
                layoutRoot.anchorMax = Vector2.one;
                layoutRoot.offsetMin = Vector2.zero;
                layoutRoot.offsetMax = Vector2.zero;
            }

            HorizontalLayoutGroup row = layoutRoot.GetComponent<HorizontalLayoutGroup>();
            if (row == null)
            {
                created = true;
                row = layoutRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            if (!created)
                return;

            bool rtl = art == null || art.FillRightToLeft;
            row.spacing = art != null ? art.Spacing : 2f;
            row.childAlignment = TextAnchor.MiddleRight;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.reverseArrangement = rtl;
        }

        private void EnsureCells()
        {
            float cell = art != null ? art.CellSize : 18f;
            while (layoutRoot.childCount < CombatConstants.STAR_MAX)
            {
                int index = layoutRoot.childCount;
                GameObject cellGo = new GameObject($"Star_{index:00}", typeof(RectTransform), typeof(LayoutElement));
                cellGo.transform.SetParent(layoutRoot, false);
                LayoutElement le = cellGo.GetComponent<LayoutElement>();
                le.preferredWidth = cell;
                le.preferredHeight = cell;
                le.minWidth = cell;
                le.minHeight = cell;

                Image empty = CreateLayer("EmptyStar", cellGo.transform as RectTransform);
                Stretch(empty.rectTransform);
                Image filled = CreateLayer("FilledStar", cellGo.transform as RectTransform);
                Stretch(filled.rectTransform);
                filled.enabled = false;
            }
        }

        private void ApplyArtLayoutOverrides()
        {
            if (art == null || layoutRoot == null)
                return;

            HorizontalLayoutGroup row = layoutRoot.GetComponent<HorizontalLayoutGroup>();
            if (row != null)
            {
                row.spacing = art.Spacing;
                row.reverseArrangement = art.FillRightToLeft;
                row.childAlignment = art.FillRightToLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            }

            float cell = art.CellSize;
            for (int i = 0; i < layoutRoot.childCount; i++)
            {
                LayoutElement le = layoutRoot.GetChild(i).GetComponent<LayoutElement>();
                if (le == null)
                    continue;
                le.preferredWidth = cell;
                le.preferredHeight = cell;
                le.minWidth = cell;
                le.minHeight = cell;
            }
        }

        private void ApplyEmpty(StarSlot slot)
        {
            if (art != null && art.EmptyStar != null)
                slot.emptyImage.sprite = art.EmptyStar;
            if (art != null)
                slot.emptyImage.color = art.EmptyTint;
            slot.emptyImage.enabled = slot.emptyImage.sprite != null;
            slot.emptyImage.preserveAspect = true;
        }

        private void ApplyFilled(StarSlot slot)
        {
            if (art != null && art.FilledStar != null)
                slot.filledImage.sprite = art.FilledStar;
            if (art != null)
                slot.filledImage.color = art.FilledTint;
            slot.filledImage.enabled = slot.filledImage.sprite != null;
            slot.filledImage.preserveAspect = true;
            CaptureBase(slot);
            slot.filledRect.localScale = slot.baseScale;
        }

        private static void HideFilled(StarSlot slot)
        {
            if (slot?.filledImage == null)
                return;
            slot.filledImage.enabled = false;
        }

        private void PlayFilledPresentation(int filledCount)
        {
            int count = Mathf.Clamp(filledCount, 0, slots.Length);
            float stagger = art != null ? art.FilledStaggerSeconds : 0.045f;
            float startScale = art != null ? art.FilledStartScale : 0.72f;
            float overshoot = art != null ? art.FilledOvershootScale : 1.12f;
            float scaleIn = art != null ? Mathf.Max(0.01f, art.FilledScaleInDuration) : 0.12f;
            float settle = art != null ? Mathf.Max(0.01f, art.FilledSettleDuration) : 0.11f;

            // With reverseArrangement, index 0 is rightmost — animate 0..count-1 for RTL fill order.
            for (int i = 0; i < count; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.filledRect == null || slot.filledImage == null || !slot.filledImage.enabled)
                    continue;

                CaptureBase(slot);
                Vector3 from = slot.baseScale * Mathf.Max(0f, startScale);
                Vector3 mid = slot.baseScale * Mathf.Max(0f, overshoot);
                slot.filledRect.localScale = from;

                Sequence sequence = DOTween.Sequence()
                    .SetDelay(i * Mathf.Max(0f, stagger))
                    .SetUpdate(true)
                    .SetTarget(slot.filledRect);
                sequence.Append(slot.filledRect.DOScale(mid, scaleIn).SetEase(Ease.OutSine));
                sequence.Append(slot.filledRect.DOScale(slot.baseScale, settle).SetEase(Ease.InOutSine));
                slot.presentationTween = sequence;
            }
        }

        private void StopPresentationTweens(bool reset)
        {
            masteryTween?.Kill(false);
            masteryTween = null;
            DOTween.Kill(this, false);
            for (int i = 0; i < slots.Length; i++)
            {
                StarSlot slot = slots[i];
                if (slot == null)
                    continue;
                slot.presentationTween?.Kill(false);
                slot.presentationTween = null;
                if (reset && slot.filledRect != null)
                {
                    CaptureBase(slot);
                    slot.filledRect.localScale = slot.baseScale;
                    if (slot.filledImage != null && slot.filledImage.enabled && art != null)
                        slot.filledImage.color = art.FilledTint;
                }
            }

            if (reset && layoutRoot != null)
                layoutRoot.localScale = Vector3.one;
        }

        private static void CaptureBase(StarSlot slot)
        {
            if (slot == null || slot.capturedBase || slot.filledRect == null)
                return;
            slot.baseScale = slot.filledRect.localScale;
            if (slot.baseScale.sqrMagnitude < 0.0001f)
                slot.baseScale = Vector3.one;
            slot.capturedBase = true;
        }

        private static Image FindImage(Transform cell, string name)
        {
            Transform child = cell.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Image CreateLayer(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
                return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
        }
    }
}
