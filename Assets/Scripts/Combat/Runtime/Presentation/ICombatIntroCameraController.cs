using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>
    /// Temporary intro framing for battle intros. Implemented by the scene camera
    /// (Assembly-CSharp) so Combat.Runtime never references camera concrete types.
    /// </summary>
    public interface ICombatIntroCameraController
    {
        /// <summary>
        /// Capture authored battle framing for later restore. Opaque token.
        /// Call before applying intro framing.
        /// </summary>
        object CaptureIntroFraming();

        /// <summary>
        /// Blend to temporary framing derived from the authored battle profile.
        /// <paramref name="followOffsetYDelta"/> is added to the source profile's followOffset.y.
        /// <paramref name="followOffsetZ"/> replaces followOffset.z.
        /// Pitch must go through profile apply. Use blendSeconds = 0 to apply instantly.
        /// </summary>
        IEnumerator BlendToIntroFraming(
            float followOffsetYDelta,
            float followOffsetZ,
            float pitchAngle,
            float blendSeconds);

        /// <summary>Restore framing captured by <see cref="CaptureIntroFraming"/>.</summary>
        IEnumerator RestoreIntroFraming(object snapshotToken, float blendSeconds);
    }
}
