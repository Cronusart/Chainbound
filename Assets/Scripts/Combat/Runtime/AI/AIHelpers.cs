using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared helper methods for AI implementations.
    /// Builds legal action lists, scores targets, and picks Stamped moves by criteria.
    /// </summary>
    public static class AIHelpers
    {
        /// <summary>
        /// Get all legal actions for a Chainling (Stamped moves it can afford + swap targets).
        /// </summary>
        public static List<ScoredAction> GetLegalActions(
            ChainlingInstance actor,
            TeamState myTeam,
            TeamState enemyTeam,
            int teamIndex,
            CombatState state,
            CombatActionEligibilityService eligibilityService = null,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot = null,
            int actorSlot = -1)
        {
            var actions = new List<ScoredAction>();
            var eligibility = eligibilityService ?? new CombatActionEligibilityService();

            if (actor == null || !actor.isAlive)
            {
                actions.Add(new ScoredAction(CombatAction.Pass(teamIndex), 0f, "Dead/null"));
                return actions;
            }

            // Move actions
            for (int i = 0; i < actor.moves.Count; i++)
            {
                var insc = actor.moves[i];
                MoveSelectionEvaluation evaluation = eligibility.EvaluateMoveSelection(
                    actor, insc, state, queuedBySlot: queuedBySlot, actorSlot: actorSlot);
                if (!evaluation.canSelect)
                    continue;

                if (CombatWallStateUtility.MovePlacesBlock(insc))
                {
                    for (int targetSlot = 0; myTeam?.activeIndices != null && targetSlot < myTeam.activeIndices.Count; targetSlot++)
                    {
                        ChainlingInstance target = myTeam.GetActiveInSlot(targetSlot);
                        if (!eligibility.EvaluateBlockTarget(
                                actor, insc, target, state, queuedBySlot, actorSlot).canTarget)
                            continue;
                        actions.Add(new ScoredAction(
                            CombatAction.UseMove(teamIndex, i, teamIndex, targetSlot),
                            ScoreBlockValue(insc, target, enemyTeam),
                            $"{insc.displayName} -> {target.displayName}"));
                    }
                }
                else
                {
                    actions.Add(new ScoredAction(
                        CombatAction.UseMove(teamIndex, i),
                        0f,
                        insc.displayName));
                }
            }

            // Swap actions (swap with bench)
            var reserved = new HashSet<int>();
            if (state != null)
            {
                if (teamIndex == 0 && state.swapReservedBenchIndexTeam0 >= 0)
                    reserved.Add(state.swapReservedBenchIndexTeam0);
                if (teamIndex == 1 && state.swapReservedBenchIndexTeam1 >= 0)
                    reserved.Add(state.swapReservedBenchIndexTeam1);
            }

            List<int> benchIndices = eligibility.GetAvailableSwapTargets(myTeam, actor, reserved);
            for (int i = 0; i < benchIndices.Count; i++)
            {
                int benchIdx = benchIndices[i];
                actions.Add(new ScoredAction(
                    CombatAction.Swap(teamIndex, benchIdx),
                    0f,
                    $"Swap to {myTeam.chainlings[benchIdx].displayName}"));
            }

            // Always have pass as fallback
            if (actions.Count == 0)
            {
                actions.Add(new ScoredAction(CombatAction.Pass(teamIndex), 0f, "No options"));
            }

            return actions;
        }

        /// <summary>Check if a move is a damaging move.</summary>
        public static bool IsDamaging(MoveInstance insc)
        {
            return insc.category == MoveCategory.DamagingMelee ||
                   insc.category == MoveCategory.DamagingRanged;
        }

        /// <summary>Check if a move is a support/heal move.</summary>
        public static bool IsSupport(MoveInstance insc)
        {
            return insc.category == MoveCategory.Support;
        }

        /// <summary>Check if a move is a status/utility move.</summary>
        public static bool IsUtility(MoveInstance insc)
        {
            return insc.category == MoveCategory.StatusUtility;
        }

        /// <summary>
        /// Pick the best enemy target from active opponents.
        /// Returns null if no targets available.
        /// </summary>
        public static ChainlingInstance PickTarget(
            TeamState enemyTeam, TargetPriority priority)
        {
            var targets = enemyTeam.GetActiveChainlings();
            if (targets.Count == 0) return null;
            if (targets.Count == 1) return targets[0];

            switch (priority)
            {
                case TargetPriority.LowestHP:
                    return targets[0].currentHP <= targets[1].currentHP ? targets[0] : targets[1];

                case TargetPriority.HighestThreat:
                    return CombatStateEvaluator.ThreatScore(targets[0]) >=
                           CombatStateEvaluator.ThreatScore(targets[1])
                        ? targets[0] : targets[1];

                case TargetPriority.MostVulnerable:
                    return CombatStateEvaluator.VulnerabilityScore(targets[0]) >=
                           CombatStateEvaluator.VulnerabilityScore(targets[1])
                        ? targets[0] : targets[1];

                default:
                    return targets[0];
            }
        }

        /// <summary>
        /// Estimate raw damage a move would deal (rough, no chemistry).
        /// Used for AI scoring, not resolution.
        /// </summary>
        public static float EstimateDamage(ChainlingInstance attacker, MoveInstance insc)
        {
            if (!IsDamaging(insc) || insc.basePower <= 0) return 0f;

            bool isMelee = insc.category == MoveCategory.DamagingMelee;
            CoreStat atkStat = isMelee ? CoreStat.MeleeAtk : CoreStat.RangedAtk;
            float atk = attacker.GetEffectiveStat(atkStat);
            float avgHits = (insc.hitCount + insc.hitCountMax) / 2f;
            if (avgHits < 1) avgHits = 1;

            // Simplified estimate: Power * (Atk/100) * avgHits * accuracy
            float accFactor = insc.baseAccuracy > 0 ? insc.baseAccuracy / 100f : 1f;
            return insc.basePower * (atk / 100f) * avgHits * accFactor;
        }

        /// <summary>Damage-per-AP efficiency estimate.</summary>
        public static float DamagePerAP(ChainlingInstance attacker, MoveInstance insc)
        {
            if (insc.apCost <= 0) return 0f;
            return EstimateDamage(attacker, insc) / insc.apCost;
        }

        public static float ScoreBlockValue(
            MoveInstance blockMove,
            ChainlingInstance target,
            TeamState enemyTeam)
        {
            if (!TryGetBlockEffect(blockMove, out UtilityEffectPayload block) || target == null || enemyTeam == null)
                return -1f;

            bool protectsMelee = block.contactFilter == UtilityContactFilter.Contact;
            float largestThreat = 0f;
            List<ChainlingInstance> enemies = enemyTeam.GetActiveChainlings();
            for (int i = 0; i < enemies.Count; i++)
            {
                ChainlingInstance enemy = enemies[i];
                if (enemy?.moves == null)
                    continue;
                for (int m = 0; m < enemy.moves.Count; m++)
                {
                    MoveInstance attack = enemy.moves[m];
                    if (attack == null || !IsDamaging(attack))
                        continue;
                    bool matching = protectsMelee
                        ? attack.category == MoveCategory.DamagingMelee
                        : attack.category == MoveCategory.DamagingRanged;
                    if (matching)
                        largestThreat = Math.Max(largestThreat, EstimateDamage(enemy, attack));
                }
            }

            float mitigation = block.magnitude > 1f ? block.magnitude / 100f : block.magnitude;
            float prevented = largestThreat * Math.Max(0f, mitigation);
            float worthwhileThreshold = Math.Max(1f, target.currentHP * 0.15f);
            return prevented >= worthwhileThreshold ? prevented : -1f;
        }

        public static bool ShouldBankForGreaterBlock(
            ChainlingInstance actor,
            TeamState myTeam,
            TeamState enemyTeam,
            CombatState state,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot,
            int actorSlot)
        {
            if (actor?.moves == null || actor.currentAP >= CombatConstants.AP_CAP)
                return false;

            var eligibility = new CombatActionEligibilityService();
            for (int i = 0; i < actor.moves.Count; i++)
            {
                MoveInstance move = actor.moves[i];
                if (!TryGetBlockEffect(move, out UtilityEffectPayload block))
                    continue;
                float mitigation = block.magnitude > 1f ? block.magnitude / 100f : block.magnitude;
                if (mitigation <= 0.5f || move.apCost <= CombatConstants.AP_PER_TURN || actor.currentAP >= move.apCost)
                    continue;

                for (int slot = 0; myTeam?.activeIndices != null && slot < myTeam.activeIndices.Count; slot++)
                {
                    ChainlingInstance target = myTeam.GetActiveInSlot(slot);
                    if (!eligibility.EvaluateBlockTarget(actor, move, target, state, queuedBySlot, actorSlot).canTarget)
                        continue;
                    if (ScoreBlockValue(move, target, enemyTeam) > 0f)
                        return true;
                }
            }
            return false;
        }

        private static bool TryGetBlockEffect(MoveInstance move, out UtilityEffectPayload block)
        {
            block = null;
            if (move?.utilityEffects == null)
                return false;
            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload candidate = move.utilityEffects[i];
                if (candidate != null && candidate.effectType == UtilityEffectType.BlockWindow)
                {
                    block = candidate;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>An action paired with a score and description for AI decision-making.</summary>
    public struct ScoredAction : IComparable<ScoredAction>
    {
        public CombatAction action;
        public float score;
        public string description;

        public ScoredAction(CombatAction action, float score, string description)
        {
            this.action = action;
            this.score = score;
            this.description = description;
        }

        public int CompareTo(ScoredAction other) => other.score.CompareTo(score); // Descending
    }

    /// <summary>Target selection priority for AI.</summary>
    public enum TargetPriority
    {
        LowestHP,
        HighestThreat,
        MostVulnerable,
        Random,
    }
}
