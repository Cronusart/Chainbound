using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Simplest AI: picks the first affordable damaging move, else first utility, else pass.
    /// Always targets the opponent's lead. Never swaps.
    /// Good baseline for testing and as a "tutorial difficulty" opponent.
    /// </summary>
    public class DumbAI : ICombatAI
    {
        public string PersonalityName => "Dumb";

        public List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng)
        {
            var myTeam = state.GetTeam(teamIndex);
            var enemyTeam = state.GetOpposingTeam(teamIndex);
            var actions = new List<CombatAction>();

            for (int slot = 0; slot < myTeam.activeIndices.Count; slot++)
            {
                var c = myTeam.GetActiveInSlot(slot);
                if (c == null || !c.isAlive)
                {
                    actions.Add(CombatAction.Pass(teamIndex));
                    continue;
                }

                var legalActions = AIHelpers.GetLegalActions(c, myTeam, enemyTeam, teamIndex, state);
                CombatAction chosen = CombatAction.Pass(teamIndex);

                // First affordable damaging move
                for (int i = 0; i < legalActions.Count; i++)
                {
                    if (legalActions[i].action.actionType != CombatActionType.UseMove)
                        continue;
                    var insc = c.moves[legalActions[i].action.moveIndex];
                    if (AIHelpers.IsDamaging(insc))
                    {
                        chosen = legalActions[i].action;
                        break;
                    }
                }

                // Fallback: first affordable non-passive
                if (chosen.actionType == CombatActionType.Pass)
                {
                    for (int i = 0; i < legalActions.Count; i++)
                    {
                        if (legalActions[i].action.actionType != CombatActionType.UseMove)
                            continue;
                        var insc = c.moves[legalActions[i].action.moveIndex];
                        if (insc.category != MoveCategory.Passive)
                        {
                            chosen = legalActions[i].action;
                            break;
                        }
                    }
                }

                actions.Add(chosen);
            }

            return actions;
        }
    }
}
