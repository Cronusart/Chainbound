using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Fixed-width 5-star row for General view. Filled slots play baked composite shimmer frames;
    /// empty slots show a static outline sprite. Layout is authored on the prefab (HorizontalLayoutGroup + LayoutElements).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyTabStarRatingDisplay : MonoBehaviour
    {
        private sealed class StarSlot
        {
            public RectTransform cell;
            public RectTransform filledRect;
            public Image emptyImage;
            public Image filledImage;
            public PartyTabFrameAnimator filledAnimator;
            public Tween presentationTween;
            public Vector3 baseScale = Vector3.one;
            public Vector2 baseAnchoredPosition;
            public bool capturedBase;
        }

        [SerializeField] private PartyTabStarRatingArtSO art;
        [SerializeField] private RectTransform rowRoot;
        [SerializeField] private RectTransform layoutRoot;
        [Tooltip("When enabled, PartyTabStarRatingArtSO cell size/spacing override prefab layout at runtime.")]
        [SerializeField] private bool applyArtLayoutOverrides;

        [Header("Filled Star Presentation")]
        [SerializeField] private bool animateFilledStarPresentation = true;
        [SerializeField, Min(0f)] private float filledStarStaggerSeconds = 0.045f;
        [SerializeField, Min(0f)] private float filledStarScaleInDuration = 0.12f;
        [SerializeField, Min(0f)] private float filledStarSettleDuration = 0.11f;
        [SerializeField] private float filledStarStartScale = 0.72f;
        [SerializeField] private float filledStarOvershootScale = 1.12f;
        [SerializeField, Min(0f)] private float filledStarShakeDuration = 0.08f;
        [SerializeField] private Vector2 filledStarShakeStrength = new Vector2(1.6f, 1.1f);
        [SerializeField, Min(0)] private int filledStarShakeVibrato = 8;

        private readonly StarSlot[] slots = new StarSlot[CombatConstants.STAR_MAX];
        private bool slotsBound;

        public RectTransform RowRoot => rowRoot;
        public RectTransform LayoutRoot => layoutRoot;
        public bool HasAuthoredLayout => layoutRoot != null && slotsBound;

        public void Configure(PartyTabStarRatingArtSO ratingArt)
        {
            if (ratingArt != null)
                art = ratingArt;
        }

        public void BuildDefaultLayoutForEditor()
        {
            BuildOrBindHierarchy(createIfMissing: true);
        }

        public void SetRating(int filledCount, bool visible, bool playShimmer)
        {
            BuildOrBindHierarchy(createIfMissing: false);
            StopPresentationTweens(reset: true);

            if (visible && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (rowRoot != null)
                rowRoot.gameObject.SetActive(visible);
            else
                gameObject.SetActive(visible);

            if (!visible)
            {
                StopAllSlots();
                return;
            }

            if (!slotsBound)
                return;

            ResolveArt(out Sprite emptySprite, out IReadOnlyList<Sprite> shimmerFrames, out float fps, out float stagger);
            if (applyArtLayoutOverrides)
                ApplyArtLayoutOverrides();

            int clampedFilled = Mathf.Clamp(filledCount, 0, CombatConstants.STAR_MAX);
            for (int i = 0; i < slots.Length; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.emptyImage == null || slot.filledImage == null)
                    continue;

                bool filled = i < clampedFilled;
                ResetSlotPresentation(slot);
                ApplyEmptyFrame(slot, emptySprite);

                if (filled)
                    ApplyFilledStar(slot, shimmerFrames, fps, stagger, i, playShimmer);
                else
                    HideFilledStar(slot);
            }

            if (playShimmer && animateFilledStarPresentation && Application.isPlaying)
                PlayFilledStarPresentation(clampedFilled);
        }

        private void Awake()
        {
            BuildOrBindHierarchy(createIfMissing: false);
        }

        private void OnDisable()
        {
            StopPresentationTweens(reset: true);
            StopAllSlots();
        }

        private void StopAllSlots()
        {
            for (int i = 0; i < slots.Length; i++)
                slots[i]?.filledAnimator?.Stop();
        }

        private void StopPresentationTweens(bool reset)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                StarSlot slot = slots[i];
                if (slot == null)
                    continue;

                slot.presentationTween?.Kill(false);
                slot.presentationTween = null;

                if (reset)
                    ResetSlotPresentation(slot);
            }
        }

        private void PlayFilledStarPresentation(int filledCount)
        {
            int count = Mathf.Clamp(filledCount, 0, slots.Length);
            for (int i = 0; i < count; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.filledRect == null || slot.filledImage == null || !slot.filledImage.enabled)
                    continue;

                CaptureSlotBase(slot);
                Vector3 startScale = new Vector3(
                    slot.baseScale.x * Mathf.Max(0f, filledStarStartScale),
                    slot.baseScale.y * Mathf.Max(0f, filledStarStartScale),
                    slot.baseScale.z);
                Vector3 overshootScale = new Vector3(
                    slot.baseScale.x * Mathf.Max(0f, filledStarOvershootScale),
                    slot.baseScale.y * Mathf.Max(0f, filledStarOvershootScale),
                    slot.baseScale.z);
                float delay = i * Mathf.Max(0f, filledStarStaggerSeconds);
                float scaleDuration = Mathf.Max(0.01f, filledStarScaleInDuration);
                float settleDuration = Mathf.Max(0.01f, filledStarSettleDuration);

                slot.filledRect.localScale = startScale;
                slot.filledRect.anchoredPosition = slot.baseAnchoredPosition;

                Sequence sequence = DOTween.Sequence()
                    .SetDelay(delay)
                    .SetUpdate(true)
                    .SetTarget(slot.filledRect);
                sequence.Append(slot.filledRect
                    .DOScale(overshootScale, scaleDuration)
                    .SetEase(Ease.OutSine));
                sequence.Append(slot.filledRect
                    .DOScale(slot.baseScale, settleDuration)
                    .SetEase(Ease.InOutSine));

                if (filledStarShakeDuration > 0f &&
                    filledStarShakeStrength.sqrMagnitude > 0.0001f &&
                    filledStarShakeVibrato > 0)
                {
                    sequence.Append(slot.filledRect
                        .DOShakeAnchorPos(
                            filledStarShakeDuration,
                            filledStarShakeStrength,
                            filledStarShakeVibrato,
                            90f,
                            false,
                            true)
                        .SetEase(Ease.OutSine));
                    sequence.OnComplete(() =>
                    {
                        if (slot.filledRect != null)
                        {
                            slot.filledRect.localScale = slot.baseScale;
                            slot.filledRect.anchoredPosition = slot.baseAnchoredPosition;
                        }
                    });
                }

                slot.presentationTween = sequence;
            }
        }

        private void CaptureSlotBase(StarSlot slot)
        {
            if (slot == null || slot.capturedBase || slot.filledRect == null)
                return;

            slot.baseScale = slot.filledRect.localScale;
            slot.baseAnchoredPosition = slot.filledRect.anchoredPosition;
            slot.capturedBase = true;
        }

        private void ResetSlotPresentation(StarSlot slot)
        {
            if (slot?.filledRect == null)
                return;

            CaptureSlotBase(slot);
            slot.filledRect.localScale = slot.baseScale;
            slot.filledRect.anchoredPosition = slot.baseAnchoredPosition;
        }

        private static void ApplyEmptyFrame(StarSlot slot, Sprite emptySprite)
        {
            if (slot?.emptyImage == null)
                return;

            slot.emptyImage.raycastTarget = false;
            slot.emptyImage.preserveAspect = false;
            slot.emptyImage.color = Color.white;
            slot.emptyImage.gameObject.SetActive(true);

            if (emptySprite == null)
            {
                slot.emptyImage.enabled = false;
                return;
            }

            slot.emptyImage.sprite = emptySprite;
            slot.emptyImage.enabled = true;
        }

        private static void ApplyFilledStar(
            StarSlot slot,
            IReadOnlyList<Sprite> shimmerFrames,
            float fps,
            float stagger,
            int slotIndex,
            bool playShimmer)
        {
            if (slot?.filledImage == null || shimmerFrames == null || shimmerFrames.Count == 0)
            {
                HideFilledStar(slot);
                return;
            }

            slot.filledImage.raycastTarget = false;
            slot.filledImage.preserveAspect = false;
            slot.filledImage.color = Color.white;
            slot.filledImage.gameObject.SetActive(true);
            slot.filledImage.enabled = true;
            slot.filledImage.sprite = shimmerFrames[0];

            if (slot.filledAnimator == null)
                return;

            slot.filledAnimator.SetTargetImage(slot.filledImage);
            slot.filledAnimator.SetFrames(shimmerFrames);
            slot.filledAnimator.SetFPS(fps);
            slot.filledAnimator.SetLoop(true);

            if (playShimmer)
                slot.filledAnimator.Play(slotIndex * stagger);
            else
                slot.filledAnimator.Stop();
        }

        private static void HideFilledStar(StarSlot slot)
        {
            if (slot?.filledAnimator != null)
                slot.filledAnimator.Stop();

            if (slot?.filledImage == null)
                return;

            slot.filledImage.gameObject.SetActive(true);
            slot.filledImage.enabled = false;
            slot.filledImage.sprite = null;
        }

        private void BuildOrBindHierarchy(bool createIfMissing)
        {
            if (slotsBound)
                return;

            if (TryBindAuthoredHierarchy())
                return;

            if (!createIfMissing)
                return;

            EnsureRowRootsCreated();
            EnsureLayoutGroupCreated();
            EnsureStarCellsCreated();
            slotsBound = TryBindAuthoredHierarchy();
            MarkLayoutDirty();
        }

        private bool TryBindAuthoredHierarchy()
        {
            if (layoutRoot == null)
                layoutRoot = transform.Find("StarRatingRow/StarRowLayout") as RectTransform;
            if (layoutRoot == null)
                layoutRoot = GetComponentInChildren<HorizontalLayoutGroup>(true)?.transform as RectTransform;
            if (rowRoot == null && layoutRoot != null)
                rowRoot = layoutRoot.parent as RectTransform;

            if (layoutRoot == null || layoutRoot.childCount < CombatConstants.STAR_MAX)
                return false;

            for (int i = 0; i < CombatConstants.STAR_MAX; i++)
            {
                RectTransform cell = layoutRoot.GetChild(i) as RectTransform;
                if (cell == null)
                    return false;

                if (!TryBindStarSlot(cell, out StarSlot slot))
                    return false;

                slots[i] = slot;
                CaptureSlotBase(slots[i]);
            }

            slotsBound = true;
            return true;
        }

        private static bool TryBindStarSlot(RectTransform cell, out StarSlot slot)
        {
            slot = null;
            if (cell == null)
                return false;

            Image filledImage = FindLayerImage(cell, "FilledStar")
                ?? FindLayerImage(cell, "StarFilled")
                ?? FindLayerImage(cell, "Filled");
            Image emptyImage = FindLayerImage(cell, "EmptyStar")
                ?? FindLayerImage(cell, "StarEmpty")
                ?? FindLayerImage(cell, "Empty")
                ?? FindLayerImage(cell, "Star");

            if (emptyImage == null)
            {
                Image[] images = cell.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i] != filledImage)
                    {
                        emptyImage = images[i];
                        break;
                    }
                }
            }

            if (emptyImage == null)
                return false;

            PartyTabFrameAnimator emptyAnimator = emptyImage.GetComponent<PartyTabFrameAnimator>();
            if (emptyAnimator != null)
                emptyAnimator.Stop();

            emptyImage.raycastTarget = false;
            emptyImage.preserveAspect = false;

            if (filledImage == null)
                filledImage = CreateStarLayer("FilledStar", cell, emptyImage.rectTransform);

            if (filledImage == null)
                return false;

            filledImage.raycastTarget = false;
            filledImage.preserveAspect = false;
            filledImage.gameObject.SetActive(true);
            filledImage.enabled = false;

            emptyImage.transform.SetAsFirstSibling();
            filledImage.transform.SetAsLastSibling();

            PartyTabFrameAnimator filledAnimator = filledImage.GetComponent<PartyTabFrameAnimator>();
            if (filledAnimator == null)
                filledAnimator = filledImage.gameObject.AddComponent<PartyTabFrameAnimator>();
            filledAnimator.SetTargetImage(filledImage);

            slot = new StarSlot
            {
                cell = cell,
                emptyImage = emptyImage,
                filledImage = filledImage,
                filledRect = filledImage.rectTransform,
                filledAnimator = filledAnimator,
            };
            return true;
        }

        private static Image FindLayerImage(RectTransform cell, string layerName)
        {
            Transform directChild = cell.Find(layerName);
            if (directChild != null && directChild.TryGetComponent(out Image directImage))
                return directImage;

            Image[] images = cell.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == layerName)
                    return images[i];
            }

            return null;
        }

        private static Image CreateStarLayer(string layerName, RectTransform parent, RectTransform template)
        {
            Image image = PlayerMenuUiFactory.CreateImage(layerName, parent, Color.white);
            image.enabled = false;
            image.raycastTarget = false;
            image.preserveAspect = false;

            CopyRectTransform(template, image.rectTransform);
            return image;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private void EnsureRowRootsCreated()
        {
            if (rowRoot == null)
            {
                rowRoot = PlayerMenuUiFactory.CreateRect("StarRatingRow", transform);
                PlayerMenuUiFactory.Stretch(rowRoot, 0f, 0f, 0f, 0f);
            }

            if (layoutRoot == null)
            {
                layoutRoot = PlayerMenuUiFactory.CreateRect("StarRowLayout", rowRoot);
                PlayerMenuUiFactory.Stretch(layoutRoot, 0f, 0f, 0f, 0f);
            }
        }

        private void EnsureLayoutGroupCreated()
        {
            if (layoutRoot == null)
                return;

            float spacing = art != null ? art.Spacing : 8f;
            HorizontalLayoutGroup layout = PlayerMenuUiFactory.HorizontalLayout(layoutRoot.gameObject, spacing);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void EnsureStarCellsCreated()
        {
            if (layoutRoot == null)
                return;

            float cellSize = art != null ? art.CellSize : 40f;
            for (int i = layoutRoot.childCount; i < CombatConstants.STAR_MAX; i++)
            {
                RectTransform cell = PlayerMenuUiFactory.CreateRect($"StarCell_{i}", layoutRoot);
                PlayerMenuUiFactory.LayoutElement(cell.gameObject, cellSize, cellSize);

                Image emptyImage = PlayerMenuUiFactory.CreateImage("EmptyStar", cell, Color.white);
                PlayerMenuUiFactory.Stretch(emptyImage.rectTransform, 0f, 0f, 0f, 0f);
                emptyImage.preserveAspect = false;
                emptyImage.raycastTarget = false;

                Image filledImage = PlayerMenuUiFactory.CreateImage("FilledStar", cell, Color.white);
                PlayerMenuUiFactory.Stretch(filledImage.rectTransform, 0f, 0f, 0f, 0f);
                filledImage.preserveAspect = false;
                filledImage.raycastTarget = false;
                filledImage.enabled = false;

                PartyTabFrameAnimator animator = filledImage.gameObject.AddComponent<PartyTabFrameAnimator>();
                animator.SetTargetImage(filledImage);
            }
        }

        private void ApplyArtLayoutOverrides()
        {
            if (layoutRoot == null || art == null)
                return;

            HorizontalLayoutGroup layout = layoutRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                layout.spacing = art.Spacing;

            for (int i = 0; i < slots.Length; i++)
            {
                StarSlot slot = slots[i];
                if (slot?.cell == null)
                    continue;

                LayoutElement element = slot.cell.GetComponent<LayoutElement>();
                if (element == null)
                    element = slot.cell.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = art.CellSize;
                element.preferredHeight = art.CellSize;
            }
        }

        private void ResolveArt(
            out Sprite emptySprite,
            out IReadOnlyList<Sprite> shimmerFrames,
            out float fps,
            out float stagger)
        {
            if (art != null && art.EmptyStar != null && art.HasFilledShimmer)
            {
                emptySprite = art.EmptyStar;
                shimmerFrames = art.FilledShimmerFrames;
                fps = art.Fps;
                stagger = art.StaggerSeconds;
                return;
            }

            emptySprite = PartyTabPlaceholderArtUtility.GetEmptyStarSprite();
            shimmerFrames = PartyTabPlaceholderArtUtility.GetFilledStarShimmerFrames();
            fps = 8f;
            stagger = 0.04f;
        }

        private void MarkLayoutDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
