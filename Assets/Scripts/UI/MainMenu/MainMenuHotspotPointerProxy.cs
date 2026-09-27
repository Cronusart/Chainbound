using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Forwards a selected bubble's small raycast rect to its hotspot.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuHotspotPointerProxy : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private MainMenuObjectHotspot hotspot;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hotspot?.SelectFromPointer();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        hotspot?.ConfirmFromPointer();
    }
}
