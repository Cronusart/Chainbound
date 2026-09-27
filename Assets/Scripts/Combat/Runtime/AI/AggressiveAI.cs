using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Aggressive AI: maximizes damage output.
    /// - Always picks the highest estimated-damage move it can afford
    /// - Targets the most vulnerable (lowest HP) enemy
    /// - Never swaps, never heals â€” pure glass cannon
    /// Good for fast-attacking species like Kitsauto.
    /// </summary>
    public class AggressiveAI : ICombatAI
    {
        public string PersonalityName => "Aggressive";

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

                // Score each action: prefer highest raw damage
                float bestScore = -1f;
                CombatAction bestAction = CombatAction.Pass(teamIndex);

                for (int i = 0; i < legalActions.Count; i++)
                {
                    var sa = legalActions[i];
                    float score = 0f;

                    if (sa.action.actionType == CombatActionType.UseMove)
                    {
                        var move = c.moves[sa.action.moveIndex];

                        if (AIHelpers.IsDamaging(move))
                        {
                            // Score by damage-per-AP efficiency
                            score = AIHelpers.DamagePerAP(c, move) * 10f;

                            // Bonus for moves that can kill the most vulnerable target
                            var target = AIHelpers.PickTarget(enemyTeam, TargetPriority.MostVulnerable);
                            if (target != null)
                            {
                                float estDmg = AIHelpers.EstimateDamage(c, move);
                                if (estDmg >= target.currentHP)
                                    score += 50f; // Big bonus for lethal blows
                            }
                        }
                        else
                        {
                            // Non-damaging moves get very low priority
                            score = 0.1f;
                        }
                    }
                    else if (sa.action.actionType == CombatActionType.Swap)
                    {
                        score = 0.05f; // Aggressive AI almost never swaps
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAction = sa.action;
                    }
                }

                actions.Add(bestAction);
            }

            return actions;
        }
    }
}
