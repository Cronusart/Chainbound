using System.Collections;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Starts a casual match after the scene bootstrap. CombatAutoStarter does not own this path.
    /// </summary>
    public sealed class CasualPvpBattleBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            CombatBattleController controller = GetComponent<CombatBattleController>();
            if (controller == null || !controller.IsCasualPvpSession || controller.BattleStarted)
                yield break;

            controller.StartBattle();
        }
    }
}
