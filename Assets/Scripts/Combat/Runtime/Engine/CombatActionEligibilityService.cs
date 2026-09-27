using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public readonly struct MoveSelectionEvaluation
    {
        public readonly bool canSelect;
        public readonly bool conditionMet;
        public readonly string reason;

        public MoveSelectionEvaluation(bool canSelect, bool conditionMet, string reason)
        {
            this.canSelect = canSelect;
            this.conditionMet = conditionMet;
            this.reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Single authority for move/swap legality checks.
    /// Shared by UI, AI, and resolver to prevent legality drift.
    /// </summary>
    public sealed class CombatActionEligibilityService
    {
        public MoveSelectionEvaluation EvaluateMoveSelection(ChainlingInstance actor, MoveInstance move, CombatState state, bool skipHpCostCheck = false)
        {
            if (actor == null)
                return new MoveSelectionEvaluation(false, false, "ActorMissing");
            if (!actor.isAlive)
                return new MoveSelectionEvaluation(false, false, "ActorNotAlive");
            if (move == null)
                return new MoveSelectionEvaluation(false, false, "MoveMissing");
            if (move.category == MoveCategory.Passive)
                return new MoveSelectionEvaluation(false, false, "PassiveMove");
            if (move.triggerOncePerBattle && actor.triggeredPassiveIds != null &&
                actor.triggeredPassiveIds.Contains(move.instanceId))
                return new MoveSelectionEvaluation(false, false, "MoveAlreadyUsedThisBattle");
            if (GetEffectiveApCost(actor, move) > actor.currentAP)
                return new MoveSelectionEvaluation(false, false, "InsufficientAP");
            if (!skipHpCostCheck && IsFullAbsorbBarrierWithHpCost(move) && !HasEnoughHpForBarrierCost(actor, move))
                return new MoveSelectionEvaluation(false, false, "InsufficientHP");
            if (IsMoveRecharging(actor, move))
                return new MoveSelectionEvaluation(false, false, "MoveRecharging");

            bool conditionMet = EvaluateActiveCondition(actor, move);
            if (move.selectionPolicy == MoveSelectionPolicy.Gate &&
                move.activeConditionType != ActiveConditionType.None &&
                !conditionMet)
            {
                return new MoveSelectionEvaluation(false, false, "ActiveConditionNotMet");
            }

            return new MoveSelectionEvaluation(true, conditionMet, conditionMet ? string.Empty : "ConditionNotMet");
        }

        public List<int> GetAvailableSwapTargets(TeamState team, ISet<int> reservedBenchIndices)
        {
            return GetAvailableSwapTargets(team, null, reservedBenchIndices);
        }

        public List<int> GetAvailableSwapTargets(TeamState team, ChainlingInstance actor, ISet<int> reservedBenchIndices)
        {
            var available = new List<int>(2);
            if (team == null || team.chainlings == null || team.activeIndices == null)
                return available;
            if (HasActiveStatus(actor, StatusId.FrozenAssets))
                return available;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null || !chainling.isAlive)
                    continue;
                if (team.activeIndices.Contains(i))
                    continue;
                if (reservedBenchIndices != null && reservedBenchIndices.Contains(i))
                    continue;

                available.Add(i);
            }

            return available;
        }

        public PassiveDisplayState GetPassiveDisplayState(ChainlingInstance actor, MoveInstance move)
        {
            if (actor == null || move == null || move.category != MoveCategory.Passive)
                return PassiveDisplayState.Locked;
            if (move.triggerOncePerBattle && actor.triggeredPassiveIds != null && actor.triggeredPassiveIds.Contains(move.instanceId))
                return PassiveDisplayState.Used;
            if (move.triggerCondition == PassiveTriggerCondition.HPBelowThreshold && actor.EffectiveMaxHP > 0)
            {
                float hpPercent = actor.currentHP * 100f / actor.EffectiveMaxHP;
                return hpPercent <= move.hpThresholdPercent ? PassiveDisplayState.Active : PassiveDisplayState.Armed;
            }
            if (move.triggerCondition == PassiveTriggerCondition.None)
                return PassiveDisplayState.Active;
            return PassiveDisplayState.Armed;
        }

        public int GetEffectiveApCost(ChainlingInstance actor, MoveInstance move)
        {
            if (move == null)
                return 0;

            int surcharge = actor != null ? Math.Max(0, actor.nextMoveApSurcharge) : 0;
            int discount = ResolvePassiveApDiscount(actor);
            return Math.Max(0, move.apCost + surcharge - discount);
        }

        private static int ResolvePassiveApDiscount(ChainlingInstance actor)
        {
            if (actor == null || actor.moves == null || actor.EffectiveMaxHP <= 0)
                return 0;

            int discount = 0;
            float hpPercent = actor.currentHP * 100f / actor.EffectiveMaxHP;
            for (int i = 0; i < actor.moves.Count; i++)
            {
                MoveInstance passive = actor.moves[i];
                if (passive == null || passive.category != MoveCategory.Passive || passive.utilityEffects == null)
                    continue;

                for (int e = 0; e < passive.utilityEffects.Count; e++)
                {
                    UtilityEffectPayload effect = passive.utilityEffects[e];
                    if (effect == null || effect.effectType != UtilityEffectType.MoveAPDiscountWhileHPAbove)
                        continue;
                    float threshold = effect.threshold > 0 ? effect.threshold : effect.magnitude;
                    if (threshold > 0f && threshold <= 1f)
                        threshold *= 100f;
                    if (hpPercent > threshold)
                        discount += effect.apGain > 0 ? effect.apGain : 1;
                }
            }
            return Math.Max(0, discount);
        }

        public bool ShouldApplyConditionalEffects(MoveInstance move, bool conditionMet)
        {
            if (move == null)
                return false;
            if (move.activeConditionType == ActiveConditionType.None)
                return true;
            if (conditionMet)
                return true;
            return false;
        }

        public bool EvaluateActiveConditionAtResolve(ChainlingInstance actor, MoveInstance move)
        {
            return EvaluateActiveCondition(actor, move);
        }

        private static bool IsMoveRecharging(ChainlingInstance actor, MoveInstance move)
        {
            if (actor == null || move == null || actor.statuses == null)
                return false;

            for (int i = 0; i < actor.statuses.Count; i++)
            {
                StatusInstance status = actor.statuses[i];
                if (status == null || status.IsExpired)
                    continue;
                if (status.statusId != StatusId.Recharging)
                    continue;
                if (string.IsNullOrEmpty(status.rechargingMoveId))
                    continue;
                if (string.Equals(status.rechargingMoveId, move.instanceId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool EvaluateActiveCondition(ChainlingInstance actor, MoveInstance move)
        {
            if (move == null || actor == null)
                return false;

            switch (move.activeConditionType)
            {
                case ActiveConditionType.None:
                    return true;

                case ActiveConditionType.SelfHPBelowPercent:
                {
                    if (actor.EffectiveMaxHP <= 0)
                        return false;
                    float hpPercent = actor.currentHP * 100f / actor.EffectiveMaxHP;
                    return hpPercent < move.activeConditionThresholdPercent;
                }
            }

            return false;
        }

        private static bool IsFullAbsorbBarrierWithHpCost(MoveInstance move)
        {
            if (move == null || move.tags == null) return false;
            return move.hpCostPercent > 0 && move.tags.Contains("FULL_ABSORB_WALL");
        }

        private static bool HasEnoughHpForBarrierCost(ChainlingInstance actor, MoveInstance move)
        {
            if (actor == null || move == null || actor.EffectiveMaxHP <= 0) return true;
            int required = (int)(actor.EffectiveMaxHP * move.hpCostPercent / 100f);
            return actor.currentHP > required;
        }

        private static bool HasActiveStatus(ChainlingInstance chainling, StatusId statusId)
        {
            if (chainling == null || chainling.statuses == null)
                return false;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance status = chainling.statuses[i];
                if (status != null && status.statusId == statusId && !status.IsExpired)
                    return true;
            }
            return false;
        }
    }
}
