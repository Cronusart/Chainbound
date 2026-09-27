using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Utility/Status AI: prioritizes disruption and status control.
    /// - Leads with debuff/status moves to cripple opponents
    /// - Prefers moves with status effects attached over raw damage
    /// - Targets enemies with the fewest existing debuffs (spread control)
    /// - Falls back to highest-power attack when no utility options remain
    /// Good for control-oriented species like Glitched, Encrypted, Gas types.
    /// </summary>
    public class UtilityStatusAI : ICombatAI
    {
        public string PersonalityName => "Utility/Status";

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

                // Score every action by disruption value
                float bestScore = -1f;
                CombatAction bestAction = CombatAction.Pass(teamIndex);

                for (int i = 0; i < legalActions.Count; i++)
                {
                    var sa = legalActions[i];
                    float score = ScoreAction(c, sa, myTeam, enemyTeam);

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

        private float ScoreAction(ChainlingInstance actor, ScoredAction sa,
            TeamState myTeam, TeamState enemyTeam)
        {
            if (sa.action.actionType == CombatActionType.Swap)
                return 1f; // Low priority â€” only swap if nothing else is good

            if (sa.action.actionType == CombatActionType.Pass)
                return 0f;

            if (sa.action.actionType != CombatActionType.UseMove)
                return 0f;

            var move = actor.moves[sa.action.moveIndex];
            float score = 0f;

            // â”€â”€ Status/Utility moves get high base priority â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (AIHelpers.IsUtility(move))
            {
                score += 30f;

                // Bonus for status effects that target enemies
                score += move.statusEffects.Count * 15f;

                // Bonus for debuff stage changes
                for (int s = 0; s < move.stageChanges.Count; s++)
                {
                    if (move.stageChanges[s].stages < 0 && !move.stageChanges[s].targetsSelf)
                        score += Math.Abs(move.stageChanges[s].stages) * 10f;
                }

                // Bonus for targeting enemies with fewer debuffs (spread control)
                var target = FindLeastDebuffedEnemy(enemyTeam);
                if (target != null && target.statuses.Count == 0)
                    score += 10f; // Extra incentive to debuff a clean target
            }

            // â”€â”€ Support/heal moves get medium priority â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            else if (AIHelpers.IsSupport(move))
            {
                score += 15f;

                // More valuable if allies are hurt
                var actives = myTeam.GetActiveChainlings();
                for (int a = 0; a < actives.Count; a++)
                {
                    float hpFrac = (float)actives[a].currentHP / Math.Max(1, actives[a].EffectiveMaxHP);
                    if (hpFrac < 0.5f) score += 10f;
                }
            }

            // â”€â”€ Damaging moves with status riders get bonus â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            else if (AIHelpers.IsDamaging(move))
            {
                // Base score from damage efficiency
                score += AIHelpers.DamagePerAP(actor, move) * 2f;

                // BIG bonus if the move also applies statuses
                score += move.statusEffects.Count * 20f;

                // Bonus for debuff stage changes on hit
                for (int s = 0; s < move.stageChanges.Count; s++)
                {
                    if (move.stageChanges[s].stages < 0 && !move.stageChanges[s].targetsSelf)
                        score += Math.Abs(move.stageChanges[s].stages) * 8f;
                }
            }

            // AP efficiency penalty â€” this AI is willing to spend more AP for control
            if (move.apCost > 0)
                score -= move.apCost * 1f; // Mild penalty, not heavy

            return score;
        }

        /// <summary>Find the active enemy with the fewest debuff statuses (to spread control).</summary>
        private ChainlingInstance FindLeastDebuffedEnemy(TeamState enemyTeam)
        {
            var targets = enemyTeam.GetActiveChainlings();
            if (targets.Count == 0) return null;
            if (targets.Count == 1) return targets[0];

            int minDebuffs = int.MaxValue;
            ChainlingInstance best = targets[0];

            for (int i = 0; i < targets.Count; i++)
            {
                int debuffCount = 0;
                for (int s = 0; s < targets[i].statuses.Count; s++)
                {
                    // Count non-buff statuses as debuffs
                    if (targets[i].statuses[s].origin == StatusOrigin.General ||
                        targets[i].statuses[s].origin == StatusOrigin.ChemistryOnly)
                        debuffCount++;
                }

                if (debuffCount < minDebuffs)
                {
                    minDebuffs = debuffCount;
                    best = targets[i];
                }
            }

            return best;
        }
    }
}
