using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chainbound.Combat
{
    /// <summary>Pointer bridge shared by the HUD X button and the full capture surface.</summary>
    public sealed class CaptureHoldSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action<bool> HeldChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            HeldChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            HeldChanged?.Invoke(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerPress == gameObject)
                HeldChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            HeldChanged?.Invoke(false);
        }
    }
}
