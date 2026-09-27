using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Shows a navigation focus ring when EventSystem selection lands on a selectable.
    /// Separate from semantic highlights such as lineup selection or active sort mode.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class MenuSelectableFocusVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image focusFrame;
        [SerializeField] private Color focusColor = new Color(0.2f, 0.85f, 0.75f, 0.95f);
        [SerializeField] private float focusOutlineInset = 3f;

        private void Awake()
        {
            EnsureFocusFrame();
            SetFocused(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        public static MenuSelectableFocusVisual EnsureOn(Selectable selectable, Color? color = null)
        {
            if (selectable == null)
                return null;

            MenuSelectableFocusVisual visual = selectable.GetComponent<MenuSelectableFocusVisual>();
            if (visual == null)
                visual = selectable.gameObject.AddComponent<MenuSelectableFocusVisual>();

            if (color.HasValue)
                visual.focusColor = color.Value;

            visual.EnsureFocusFrame();
            return visual;
        }

        private void EnsureFocusFrame()
        {
            if (focusFrame != null)
                return;

            focusFrame = PlayerMenuUiFactory.CreateImage("FocusFrame", transform, focusColor);
            PlayerMenuUiFactory.Stretch(focusFrame.rectTransform, focusOutlineInset, focusOutlineInset, focusOutlineInset, focusOutlineInset);
            focusFrame.raycastTarget = false;
            focusFrame.enabled = false;
        }

        private void SetFocused(bool focused)
        {
            EnsureFocusFrame();
            if (focusFrame != null)
            {
                focusFrame.color = focusColor;
                focusFrame.enabled = focused;
            }
        }
    }
}
