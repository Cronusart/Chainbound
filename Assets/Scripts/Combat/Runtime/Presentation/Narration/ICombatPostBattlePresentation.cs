using System.Collections;

namespace Chainbound.Combat
{
    public interface ICombatPostBattlePresentation
    {
        bool IsPresentationComplete { get; }
        IEnumerator WaitForCompletion();
    }
}
