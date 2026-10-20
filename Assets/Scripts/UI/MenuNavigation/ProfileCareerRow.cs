using UnityEngine;
using UnityEngine.EventSystems;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Reports the focused career row into the profile dialogue line.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProfileCareerRow : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private ProfileMenuScreen screen;
        [SerializeField] private string focusLine = "";
        [SerializeField] private GameObject selectedFrame;

        public void Configure(ProfileMenuScreen owner, string line, GameObject frame)
        {
            screen = owner;
            focusLine = line ?? "";
            selectedFrame = frame;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(true);
            if (screen != null)
                screen.ShowFocusLine(focusLine);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(false);
        }
    }
}
