using System.Collections.Generic;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Carousel-matching chrome for menu buttons: idle/selected sprite swap + optional shimmer.
    /// Used by Inventory action buttons (and reusable elsewhere in MenuNavigation).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMenuChromeButtonVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Color idleTint = Color.white;
        [SerializeField] private Color selectedTint = Color.white;
        [SerializeField] private bool enableShimmer = true;
        [SerializeField] private float shimmerFps = 12f;
        [SerializeField] private List<Sprite> shimmerFrames = new List<Sprite>();
        [SerializeField] private Image shimmerImage;
        [SerializeField] private PartyTabFrameAnimator shimmerAnimator;

        [Tooltip("9-slice border scale for both the idle sprite and the shimmer frames.")]
        [SerializeField] private float pixelsPerUnitMultiplier = 1f;

        [Tooltip("When set, EventSystem focus drives the shimmer instead of semantic selection.")]
        [SerializeField] private bool shimmerFollowsFocus;

        private bool selected;
        private bool focused;
        private bool stickyChrome;
        private bool stickyShimmerSuppressed;

        public bool IsSelected => selected;

        public bool IsShimmerPlaying => shimmerImage != null && shimmerImage.gameObject.activeSelf;

        public bool ShimmerFollowsFocus => shimmerFollowsFocus;

        public float PixelsPerUnitMultiplier => pixelsPerUnitMultiplier;

        private void OnEnable()
        {
            // Root may activate after SetSelected ran while inactive — restart shimmer.
            if (selected)
                ApplySelectedVisual();
        }

        public void Configure(
            Sprite idle,
            Sprite selectedSpriteValue,
            Color idleColor,
            Color selectedColor,
            IReadOnlyList<Sprite> frames,
            float fps,
            bool shimmerEnabled = true,
            float pixelsPerUnit = 1f,
            bool followFocus = false)
        {
            if (idle != null)
                idleSprite = idle;
            if (selectedSpriteValue != null)
                selectedSprite = selectedSpriteValue;
            idleTint = idleColor;
            selectedTint = selectedColor;
            enableShimmer = shimmerEnabled;
            shimmerFps = Mathf.Max(1f, fps);
            pixelsPerUnitMultiplier = Mathf.Max(0.01f, pixelsPerUnit);
            shimmerFollowsFocus = followFocus;
            stickyChrome = false;
            stickyShimmerSuppressed = false;
            focused = false;

            shimmerFrames.Clear();
            if (frames != null)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i] != null)
                        shimmerFrames.Add(frames[i]);
                }
            }

            EnsureHierarchy();
            ApplySelectedVisual();
        }

        public void EnsureHierarchy()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();

            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                if (targetImage != null)
                    button.targetGraphic = targetImage;
            }

            if (shimmerImage == null)
            {
                Transform existing = transform.Find("Shimmer");
                shimmerImage = existing != null ? existing.GetComponent<Image>() : null;
            }

            if (shimmerImage == null)
            {
                shimmerImage = PlayerMenuUiFactory.CreateImage("Shimmer", transform, Color.white);
                PlayerMenuUiFactory.Stretch(shimmerImage.rectTransform);
                shimmerImage.raycastTarget = false;
            }

            shimmerImage.color = Color.white;
            shimmerImage.raycastTarget = false;
            shimmerImage.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;

            if (shimmerAnimator == null)
                shimmerAnimator = shimmerImage.GetComponent<PartyTabFrameAnimator>();
            if (shimmerAnimator == null)
                shimmerAnimator = shimmerImage.gameObject.AddComponent<PartyTabFrameAnimator>();

            shimmerAnimator.SetTargetImage(shimmerImage);
            shimmerAnimator.SetFrames(shimmerFrames);
            shimmerAnimator.SetFPS(shimmerFps);
            shimmerAnimator.SetLoop(true);

            EnsureDrawOrder();
            if (!(stickyChrome ? focused : selected))
                shimmerImage.gameObject.SetActive(false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (!stickyChrome && !shimmerFollowsFocus)
                focused = value;
            EnsureHierarchy();
            ApplySelectedVisual();
        }

        /// <summary>
        /// Orange vs grey chrome stays until the next call. Shimmer still follows EventSystem focus.
        /// </summary>
        public void SetStickySelected(bool isSelected) => SetStickySelected(isSelected, true);

        /// <summary>
        /// Sticky orange/grey. With <paramref name="allowShimmer"/> false the plate stays flat
        /// even while focused (committed mode tabs).
        /// </summary>
        public void SetStickySelected(bool isSelected, bool allowShimmer)
        {
            stickyChrome = true;
            stickyShimmerSuppressed = !allowShimmer;
            selected = isSelected;
            EnsureHierarchy();
            ApplySelectedVisual();
        }

        /// <summary>
        /// Drops sticky orange/grey so EventSystem focus can drive the sprite swap again.
        /// </summary>
        public void ClearStickySelection()
        {
            stickyChrome = false;
            stickyShimmerSuppressed = false;
            selected = focused && shimmerFollowsFocus;
            EnsureHierarchy();
            ApplySelectedVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focused = true;
            if (shimmerFollowsFocus && !stickyChrome)
                selected = true;
            ApplySelectedVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focused = false;
            if (shimmerFollowsFocus && !stickyChrome)
                selected = false;
            ApplySelectedVisual();
        }

        private void ApplySelectedVisual()
        {
            bool isSelected = selected;
            Sprite sprite = isSelected && selectedSprite != null ? selectedSprite : idleSprite;
            Color tint = isSelected ? selectedTint : idleTint;
            if (targetImage != null)
            {
                if (sprite != null)
                    targetImage.sprite = sprite;
                targetImage.color = tint;
                targetImage.type = targetImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                targetImage.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
                targetImage.preserveAspect = false;
            }

            bool playShimmer = enableShimmer
                && shimmerFrames != null
                && shimmerFrames.Count > 0
                && (stickyChrome ? focused && !stickyShimmerSuppressed : isSelected);
            if (shimmerImage != null)
            {
                shimmerImage.type = Image.Type.Sliced;
                shimmerImage.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
                shimmerImage.gameObject.SetActive(playShimmer);
                if (playShimmer)
                    EnsureDrawOrder();
            }

            if (shimmerAnimator == null)
                return;

            if (playShimmer)
            {
                shimmerAnimator.SetFrames(shimmerFrames);
                shimmerAnimator.SetFPS(shimmerFps);
                // Always restart — a prior Play while inactive can leave IsPlaying true with no coroutine.
                if (isActiveAndEnabled && shimmerAnimator.isActiveAndEnabled)
                    shimmerAnimator.Play();
            }
            else
            {
                shimmerAnimator.Stop();
            }
        }

        private void EnsureDrawOrder()
        {
            if (shimmerImage != null)
                shimmerImage.transform.SetAsFirstSibling();

            TMP_Text label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.transform.SetAsLastSibling();
        }
    }
}
