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

                if (AIHelpers.ShouldBankForGreaterBlock(c, myTeam, enemyTeam, state, queuedBySlot, slot))
                {
                    CombatAction bank = CombatAction.Pass(teamIndex);
                    actions.Add(bank);
                    queuedBySlot[slot] = bank;
                    continue;
                }

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
                queuedBySlot[slot] = bestAction;
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
            if (CombatWallStateUtility.MovePlacesBlock(move))
                return sa.score;
            float score = 0f;
            int effectiveCost = ResolveScoringCost(actor, move);

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

                if (HasEquippedPassive(actor, "uplink") && BuffsAnAlly(move))
                    score += 12f;
            }

            // â”€â”€ Damaging moves with status riders get bonus â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            else if (AIHelpers.IsDamaging(move))
            {
                // Base score from damage efficiency
                score += effectiveCost > 0 ? AIHelpers.EstimateDamage(actor, move) / effectiveCost * 2f : 0f;

                // BIG bonus if the move also applies statuses
                score += move.statusEffects.Count * 20f;

                // Bonus for debuff stage changes on hit
                for (int s = 0; s < move.stageChanges.Count; s++)
                {
                    if (move.stageChanges[s].stages < 0 && !move.stageChanges[s].targetsSelf)
                        score += Math.Abs(move.stageChanges[s].stages) * 8f;
                }

                if (HasEquippedPassive(actor, "kindling"))
                    score += ExpectedKindlingChip(enemyTeam);
            }

            // AP efficiency penalty â€” this AI is willing to spend more AP for control
            if (effectiveCost > 0)
                score -= effectiveCost * 1f; // Mild penalty, not heavy

            if (MoveAvailability.IsBossOnly(move) && actor.currentAP >= effectiveCost)
                score += 60f;

            return score;
        }

        private static int ResolveScoringCost(ChainlingInstance actor, MoveInstance move)
        {
            if (move == null)
                return 0;
            if (move.tags != null)
            {
                for (int i = 0; i < move.tags.Count; i++)
                {
                    if (string.Equals(move.tags[i], "SpendRemainingAp", StringComparison.OrdinalIgnoreCase))
                        return Math.Max(0, actor != null ? actor.currentAP : move.apCost);
                }
            }
            int cost = new CombatActionEligibilityService().GetEffectiveApCost(actor, move);
            if (move.utilityEffects != null)
            {
                for (int i = 0; i < move.utilityEffects.Count; i++)
                {
                    UtilityEffectPayload effect = move.utilityEffects[i];
                    if (effect == null || effect.effectType != UtilityEffectType.NextMoveAPSurcharge ||
                        effect.targetOverride != MoveTargeting.Self)
                        continue;
                    float chance = effect.chance <= 0f ? 1f : Math.Min(1f, effect.chance);
                    cost += Math.Max(0, (int)Math.Ceiling(effect.magnitude * chance));
                }
            }
            return cost;
        }

        private static bool HasEquippedPassive(ChainlingInstance chainling, string definitionId)
        {
            if (chainling?.moves == null)
                return false;
            for (int i = 0; i < chainling.moves.Count; i++)
            {
                MoveInstance move = chainling.moves[i];
                if (move != null &&
                    move.category == MoveCategory.Passive &&
                    string.Equals(move.definitionId, definitionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool BuffsAnAlly(MoveInstance move)
        {
            if (move == null)
                return false;
            if (move.targeting == MoveTargeting.SingleAlly || move.targeting == MoveTargeting.AllAllies)
                return true;
            if (move.utilityEffects != null)
            {
                for (int i = 0; i < move.utilityEffects.Count; i++)
                {
                    UtilityEffectType type = move.utilityEffects[i].effectType;
                    if (type == UtilityEffectType.ExtendBuff || type == UtilityEffectType.RaiseLowestStage)
                        return true;
                }
            }
            return false;
        }

        private static float ExpectedKindlingChip(TeamState enemyTeam)
        {
            if (enemyTeam == null)
                return 0f;
            var foes = enemyTeam.GetActiveChainlings();
            for (int i = 0; i < foes.Count; i++)
            {
                ChainlingInstance foe = foes[i];
                if (foe == null || !foe.isAlive || HasStatus(foe, StatusId.Kindled))
                    continue;
                return Math.Max(1f, foe.EffectiveMaxHP * 0.06f);
            }
            return 0f;
        }

        private static bool HasStatus(ChainlingInstance chainling, StatusId statusId)
        {
            if (chainling?.statuses == null)
                return false;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i] != null &&
                    chainling.statuses[i].statusId == statusId &&
                    !chainling.statuses[i].IsExpired)
                    return true;
            }
            return false;
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
