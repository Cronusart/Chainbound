using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Random AI: picks a random legal action each turn.
    /// Useful for:
    /// - Balance fuzz testing (Fast Sim with RandomAI reveals dominant strategies)
    /// - Baseline comparison (any real AI should beat RandomAI consistently)
    /// - Generating diverse battle logs for analysis
    /// </summary>
    public class RandomAI : ICombatAI
    {
        public string PersonalityName => "Random";

        public List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng)
        {
            var myTeam = state.GetTeam(teamIndex);
            var enemyTeam = state.GetOpposingTeam(teamIndex);
            var actions = new List<CombatAction>();
            var queuedBySlot = new Dictionary<int, CombatAction>();

            for (int slot = 0; slot < myTeam.activeIndices.Count; slot++)
            {
                var c = myTeam.GetActiveInSlot(slot);
                if (c == null || !c.isAlive)
                {
                    actions.Add(CombatAction.Pass(teamIndex));
                    continue;
                }

                var legalActions = AIHelpers.GetLegalActions(
                    c, myTeam, enemyTeam, teamIndex, state, queuedBySlot: queuedBySlot, actorSlot: slot);

                if (legalActions.Count == 0 || rng == null)
                {
                    actions.Add(CombatAction.Pass(teamIndex));
                    continue;
                }

                // Pick a random legal action
                int pick = rng.NextInt(0, legalActions.Count - 1);
                CombatAction chosen = legalActions[pick].action;
                actions.Add(chosen);
                queuedBySlot[slot] = chosen;
            }

            return actions;
        }
    }
}
