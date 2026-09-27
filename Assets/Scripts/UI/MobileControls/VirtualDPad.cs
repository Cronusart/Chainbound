using System;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// DEPRECATED: Legacy 4-button child-raycast D-pad replaced by <see cref="HybridMovementPad"/>.
    /// Kept as a thin shim so existing scenes don't hard-error on a missing script; new work
    /// should migrate movement references to <see cref="HybridMovementPad"/>.
    /// </summary>
    [Obsolete("Use HybridMovementPad instead. This shim exists only to preserve scene compatibility.")]
    [AddComponentMenu("")] // Hide from Add Component menu.
    public class VirtualDPad : MonoBehaviour
    {
        private readonly Vector2 zero = Vector2.zero;

        public Vector2 InputVector => zero;
        public bool IsActive => false;

        public Vector2 GetInput() => zero;

        public void ResetDPad()
        {
            // No-op.
        }

        private void Awake()
        {
            Debug.LogWarning(
                "[VirtualDPad] This component is deprecated. Replace it with HybridMovementPad on " + gameObject.name,
                this);
        }
    }
}
