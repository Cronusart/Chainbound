using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Interface for camera controllers used by the combat presenter.
    /// Implement this on your camera controller MonoBehaviour to avoid
    /// reflection-based method calls.
    ///
    /// If the camera controller does not implement this interface,
    /// <see cref="Combat2BattlefieldPresenter"/> falls back to reflection
    /// (looking for SetTarget and ShakeCamera by name).
    /// </summary>
    public interface ICombatCameraController
    {
        /// <summary>Set the camera's follow target.</summary>
        void SetTarget(Transform target);

        /// <summary>Trigger a camera shake effect.</summary>
        void ShakeCamera(float intensity, float duration, float frequency);
    }
}
