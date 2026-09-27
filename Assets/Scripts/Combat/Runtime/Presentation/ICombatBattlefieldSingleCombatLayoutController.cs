using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>
    /// Presentation-only battlefield layout contract. Implementations must not mutate
    /// combat state, active slot indices, or Chainling identity bindings.
    /// </summary>
    public interface ICombatBattlefieldSingleCombatLayoutController
    {
        bool IsEnabled { get; }

        IEnumerator BlendSingleCombatLayout(CombatState state, bool active, float blendSeconds);

        void RestoreAuthoredLayout();
    }
}
