using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>
    /// Camera-facing presentation hook for settled battlefield framing.
    /// Implementations must only adjust the active profile's follow offset.
    /// </summary>
    public interface ICombatBattlefieldFramingController
    {
        IEnumerator SoftBlendBattlefieldFollowOffset(
            float followOffsetYDelta,
            float followOffsetZ,
            float blendSeconds);
    }
}
