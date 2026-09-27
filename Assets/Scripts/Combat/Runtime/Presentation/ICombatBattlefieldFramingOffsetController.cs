using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>Receives the battlefield framing target and transition timing from the presenter.</summary>
    public interface ICombatBattlefieldFramingOffsetController
    {
        void SetBattlefieldFramingActive(bool active);
        IEnumerator BlendBattlefieldFraming(bool active, float blendSeconds);
    }
}
