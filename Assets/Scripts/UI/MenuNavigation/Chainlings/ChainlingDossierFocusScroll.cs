using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Follows EventSystem selection inside a dossier page. Up and down move the
    /// selection; this only scrolls that row into the page viewport.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ChainlingDossierFocusScroll : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        private GameObject tracked;

        private void Awake()
        {
            if (scrollRect == null)
                scrollRect = GetComponent<ScrollRect>();
        }

        private void LateUpdate()
        {
            if (scrollRect == null || scrollRect.content == null || !isActiveAndEnabled)
                return;

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == tracked)
                return;

            tracked = selected;
            if (selected == null || !selected.transform.IsChildOf(scrollRect.content))
                return;

            ScrollTo(selected.transform as RectTransform);
        }

        private void ScrollTo(RectTransform target)
        {
            RectTransform content = scrollRect.content;
            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : transform as RectTransform;
            if (target == null || content == null || viewport == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            float hidden = content.rect.height - viewport.rect.height;
            if (hidden <= 1f)
                return;

            Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            float half = viewport.rect.height * 0.5f;
            float topOverflow = itemBounds.max.y - half;
            float bottomOverflow = -half - itemBounds.min.y;
            float deltaPixels = 0f;
            if (topOverflow > 0.5f)
                deltaPixels = topOverflow;
            else if (bottomOverflow > 0.5f)
                deltaPixels = -bottomOverflow;

            if (Mathf.Abs(deltaPixels) < 0.5f)
                return;

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                scrollRect.verticalNormalizedPosition + (deltaPixels / hidden));
        }
    }
}
