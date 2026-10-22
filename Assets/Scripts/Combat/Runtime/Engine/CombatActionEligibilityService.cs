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

    public readonly struct BlockTargetEvaluation
    {
        public readonly bool canTarget;
        public readonly string reason;

        public BlockTargetEvaluation(bool canTarget, string reason)
        {
            this.canTarget = canTarget;
            this.reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Single authority for move/swap legality checks.
    /// Shared by UI, AI, and resolver to prevent legality drift.
    /// </summary>
    public sealed class CombatActionEligibilityService
    {
        public MoveSelectionEvaluation EvaluateMoveSelection(
            ChainlingInstance actor,
            MoveInstance move,
            CombatState state,
            bool skipHpCostCheck = false,
            int extraAvailableAp = 0,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot = null,
            int actorSlot = -1,
            ISet<string> blockedTargetIds = null,
            IReadOnlyDictionary<string, string> blockReservationOwners = null)
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
            int availableAp = actor.currentAP + Math.Max(0, extraAvailableAp);
            if (GetEffectiveApCost(actor, move) > availableAp)
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

            if (CombatWallStateUtility.MovePlacesBlock(move) &&
                !HasAnyEligibleBlockTarget(
                    actor,
                    move,
                    state,
                    queuedBySlot,
                    actorSlot,
                    blockedTargetIds,
                    blockReservationOwners))
            {
                return new MoveSelectionEvaluation(false, conditionMet, "AllTargetsBlocked");
            }

            return new MoveSelectionEvaluation(true, conditionMet, conditionMet ? string.Empty : "ConditionNotMet");
        }

        public BlockTargetEvaluation EvaluateBlockTarget(
            ChainlingInstance actor,
            MoveInstance move,
            ChainlingInstance target,
            CombatState state,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot = null,
            int actorSlot = -1,
            ISet<string> blockedTargetIds = null,
            IReadOnlyDictionary<string, string> blockReservationOwners = null)
        {
            if (!CombatWallStateUtility.MovePlacesBlock(move))
                return new BlockTargetEvaluation(true, string.Empty);
            if (actor == null || target == null || !target.isAlive)
                return new BlockTargetEvaluation(false, "TargetUnavailable");
            bool blockWasActive = blockedTargetIds != null
                ? blockedTargetIds.Contains(target.instanceId)
                : target.HasIntactBlock();
            if (blockWasActive)
                return new BlockTargetEvaluation(false, "BlockAlreadyActive");

            if (blockReservationOwners != null &&
                blockReservationOwners.TryGetValue(target.instanceId, out string ownerId) &&
                !string.Equals(ownerId, actor.instanceId, StringComparison.Ordinal))
            {
                return new BlockTargetEvaluation(false, "BlockAlreadyQueued");
            }

            TeamState team = FindTeamContaining(state, actor);
            int targetSlot = FindActiveSlot(team, target);
            if (team == null || targetSlot < 0)
                return new BlockTargetEvaluation(false, "TargetUnavailable");
            if (HasQueuedBlockForTarget(team, targetSlot, queuedBySlot, actorSlot))
                return new BlockTargetEvaluation(false, "BlockAlreadyQueued");

            return new BlockTargetEvaluation(true, string.Empty);
        }

        private bool HasAnyEligibleBlockTarget(
            ChainlingInstance actor,
            MoveInstance move,
            CombatState state,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot,
            int actorSlot,
            ISet<string> blockedTargetIds,
            IReadOnlyDictionary<string, string> blockReservationOwners)
        {
            TeamState team = FindTeamContaining(state, actor);
            if (team == null || team.activeIndices == null)
                return true;

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                ChainlingInstance target = team.GetActiveInSlot(slot);
                if (target != null && EvaluateBlockTarget(
                    actor,
                    move,
                    target,
                    state,
                    queuedBySlot,
                    actorSlot,
                    blockedTargetIds,
                    blockReservationOwners).canTarget)
                    return true;
            }
            return false;
        }

        private static bool HasQueuedBlockForTarget(
            TeamState team,
            int targetSlot,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot,
            int actorSlot)
        {
            if (team == null || queuedBySlot == null)
                return false;

            int livingTargets = team.CountAliveActiveSlots();
            foreach (KeyValuePair<int, CombatAction> pair in queuedBySlot)
            {
                if (pair.Key == actorSlot)
                    continue;
                CombatAction action = pair.Value;
                if (action == null || action.actionType != CombatActionType.UseMove)
                    continue;
                ChainlingInstance queuedActor = team.GetActiveInSlot(pair.Key);
                if (queuedActor?.moves == null || action.moveIndex < 0 || action.moveIndex >= queuedActor.moves.Count)
                    continue;
                if (!CombatWallStateUtility.MovePlacesBlock(queuedActor.moves[action.moveIndex]))
                    continue;

                bool explicitTarget = action.targetTeamIndex >= 0 && action.targetSlotIndex >= 0;
                if (explicitTarget)
                {
                    if (action.targetTeamIndex == team.teamIndex && action.targetSlotIndex == targetSlot)
                        return true;
                }
                else if (livingTargets == 1 && team.GetActiveInSlot(targetSlot) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static TeamState FindTeamContaining(CombatState state, ChainlingInstance chainling)
        {
            if (state == null || chainling == null)
                return null;
            if (Contains(state.team0, chainling))
                return state.team0;
            if (Contains(state.team1, chainling))
                return state.team1;
            return null;
        }

        private static bool Contains(TeamState team, ChainlingInstance chainling)
        {
            return team?.chainlings != null && team.chainlings.Contains(chainling);
        }

        private static int FindActiveSlot(TeamState team, ChainlingInstance chainling)
        {
            if (team?.activeIndices == null || team.chainlings == null || chainling == null)
                return -1;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                if (ReferenceEquals(team.GetActiveInSlot(slot), chainling))
                    return slot;
            }
            return -1;
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

        /// <summary>
        /// AP a faster ally has already committed to this fighter via AP Donation or AP Router.
        /// The menu can spend it. A slower ally's gift is not included.
        /// </summary>
        public int ResolveCommittedFasterGiftAp(
            TeamState team,
            ChainlingInstance recipient,
            int recipientSlot,
            IReadOnlyDictionary<int, CombatAction> queuedBySlot)
        {
            if (team == null || recipient == null || queuedBySlot == null || team.activeIndices == null)
                return 0;

            int otherLivingAllies = 0;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                ChainlingInstance ally = team.GetActiveInSlot(slot);
                if (ally != null && ally.isAlive && !ReferenceEquals(ally, recipient))
                    otherLivingAllies++;
            }

            int recipientSpeed = recipient.GetStatBreakdown(CoreStat.Speed).finalInt;
            int gift = 0;
            foreach (KeyValuePair<int, CombatAction> pair in queuedBySlot)
            {
                if (pair.Key == recipientSlot)
                    continue;

                CombatAction action = pair.Value;
                if (action == null || action.actionType != CombatActionType.UseMove)
                    continue;

                ChainlingInstance giver = team.GetActiveInSlot(pair.Key);
                if (giver == null || !giver.isAlive || giver.moves == null)
                    continue;
                if (action.moveIndex < 0 || action.moveIndex >= giver.moves.Count)
                    continue;

                MoveInstance move = giver.moves[action.moveIndex];
                if (!IsCommittedGiftMove(move))
                    continue;
                if (giver.GetStatBreakdown(CoreStat.Speed).finalInt <= recipientSpeed)
                    continue;
                if (!GiftTargetsRecipient(action, team, recipientSlot, otherLivingAllies))
                    continue;

                int cost = GetEffectiveApCost(giver, move);
                if (cost <= 0 || giver.currentAP < cost)
                    continue;
                gift += cost;
            }

            return gift;
        }

        private static bool IsCommittedGiftMove(MoveInstance move)
        {
            if (move == null)
                return false;
            if (IsMoveId(move.definitionId, "ap_donation") || IsMoveId(move.definitionId, "ap_router"))
                return true;
            return IsMoveId(move.instanceId, "ap_donation") || IsMoveId(move.instanceId, "ap_router");
        }

        private static bool IsMoveId(string value, string id)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(id, StringComparison.Ordinal) >= 0;
        }

        private static bool GiftTargetsRecipient(
            CombatAction action,
            TeamState team,
            int recipientSlot,
            int otherLivingAllies)
        {
            bool explicitTarget = action.targetTeamIndex >= 0 && action.targetSlotIndex >= 0;
            if (explicitTarget)
                return action.targetTeamIndex == team.teamIndex && action.targetSlotIndex == recipientSlot;
            return otherLivingAllies == 1;
        }

        public int GetEffectiveApCost(ChainlingInstance actor, MoveInstance move)
        {
            if (move == null)
                return 0;

            int surcharge = actor != null ? Math.Max(0, actor.nextMoveApSurcharge) : 0;
            int discount = ResolvePassiveApDiscount(actor);
            int adjusted = move.apCost + surcharge - discount;
            return move.apCost <= 0 ? Math.Max(0, adjusted) : Math.Max(1, adjusted);
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
