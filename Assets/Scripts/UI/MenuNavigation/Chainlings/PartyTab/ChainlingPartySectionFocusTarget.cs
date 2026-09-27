using UnityEngine;
using UnityEngine.EventSystems;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public sealed class ChainlingPartySectionFocusTarget : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private ChainlingPartySectionChrome owner;
        [SerializeField] private ChainlingPartySectionFocusTargetKind target;

        public void Configure(ChainlingPartySectionChrome sectionChrome, ChainlingPartySectionFocusTargetKind focusTarget)
        {
            owner = sectionChrome;
            target = focusTarget;
        }

        public void OnSelect(BaseEventData eventData)
        {
            owner?.NotifyFocusTargetSelected(target);
        }
    }
}
