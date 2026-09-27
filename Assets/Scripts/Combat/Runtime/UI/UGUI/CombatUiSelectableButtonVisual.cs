using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Combat-safe gray/orange sprite-swap + shimmer, matching player-menu tab chrome
    /// without referencing MenuNavigation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class CombatUiSelectableButtonVisual : MonoBehaviour
    {
        [SerializeField] private Image buttonImage;
        [SerializeField] private Sprite unselectedSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Image shimmerImage;
        [SerializeField] private Sprite[] shimmerFrames;
        [SerializeField, Min(1f)] private float shimmerFps = 12f;
        [SerializeField, Min(0f)] private float shimmerPauseDuration = 0.6f;

        private Button _button;
        private Coroutine _shimmerRoutine;
        private bool _selected;

        public bool IsSelected => _selected;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.transition = Selectable.Transition.None;
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            EnsureShimmerImage();
            ApplyVisuals(false);
        }

        private void OnDisable()
        {
            StopShimmer();
        }

        public void Configure(Sprite unselected, Sprite selected, Sprite[] frames, float fps = 12f)
        {
            unselectedSprite = unselected;
            selectedSprite = selected;
            shimmerFrames = frames;
            shimmerFps = Mathf.Max(1f, fps);
            EnsureShimmerImage();
            ApplyVisuals(_selected);
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected && isActiveAndEnabled)
            {
                ApplyVisuals(selected);
                return;
            }

            _selected = selected;
            ApplyVisuals(selected);
            if (selected)
                StartShimmer();
            else
                StopShimmer();
        }

        private void ApplyVisuals(bool selected)
        {
            if (buttonImage == null)
                return;

            Sprite sprite = selected && selectedSprite != null ? selectedSprite : unselectedSprite;
            if (sprite != null)
                buttonImage.sprite = sprite;

            if (buttonImage.sprite != null)
            {
                buttonImage.color = Color.white;
                buttonImage.type = Image.Type.Sliced;
                if (shimmerImage != null)
                {
                    shimmerImage.type = Image.Type.Sliced;
                    shimmerImage.pixelsPerUnitMultiplier = buttonImage.pixelsPerUnitMultiplier;
                }
            }
        }

        private void EnsureShimmerImage()
        {
            if (shimmerImage != null)
                return;

            Transform existing = transform.Find("Shimmer");
            if (existing != null)
                shimmerImage = existing.GetComponent<Image>();

            if (shimmerImage != null)
                return;

            var go = new GameObject("Shimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            shimmerImage = go.GetComponent<Image>();
            shimmerImage.raycastTarget = false;
            shimmerImage.color = Color.white;
            shimmerImage.type = Image.Type.Sliced;
            go.SetActive(false);
        }

        private bool HasShimmerFrames()
        {
            return shimmerFrames != null && shimmerFrames.Length > 0 && shimmerFrames[0] != null;
        }

        private void StartShimmer()
        {
            EnsureShimmerImage();
            StopShimmer();
            if (!HasShimmerFrames() || shimmerImage == null)
                return;

            shimmerImage.gameObject.SetActive(true);
            shimmerImage.sprite = shimmerFrames[0];
            if (isActiveAndEnabled)
                _shimmerRoutine = StartCoroutine(ShimmerLoop());
        }

        private void StopShimmer()
        {
            if (_shimmerRoutine != null)
            {
                StopCoroutine(_shimmerRoutine);
                _shimmerRoutine = null;
            }

            if (shimmerImage != null)
                shimmerImage.gameObject.SetActive(false);
        }

        private IEnumerator ShimmerLoop()
        {
            float frameSeconds = 1f / Mathf.Max(1f, shimmerFps);
            WaitForSecondsRealtime frameWait = new WaitForSecondsRealtime(frameSeconds);
            WaitForSecondsRealtime pauseWait = new WaitForSecondsRealtime(Mathf.Max(0f, shimmerPauseDuration));

            while (_selected)
            {
                for (int i = 0; i < shimmerFrames.Length && _selected; i++)
                {
                    if (shimmerFrames[i] != null)
                        shimmerImage.sprite = shimmerFrames[i];
                    yield return frameWait;
                }

                if (shimmerPauseDuration > 0f && _selected)
                    yield return pauseWait;
            }
        }
    }
}
