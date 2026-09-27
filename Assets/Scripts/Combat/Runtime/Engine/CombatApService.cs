using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Single runtime path for AP mutation. ThreadLock, caps, legacy events, and
    /// forensic APChanged payloads all pass through this service.
    /// </summary>
    public sealed class CombatApService
    {
        private readonly CombatEventLog _log;
        private readonly CombatResolverConfigSO _resolverConfig;

        public CombatApService(CombatEventLog log, CombatResolverConfigSO resolverConfig = null)
        {
            _log = log;
            _resolverConfig = resolverConfig != null
                ? resolverConfig
                : BalanceConfigRuntime.ResolverConfig;
        }

        public bool TrySpendCost(ChainlingInstance actor, int cost, int turn, string source, out int paid)
        {
            paid = 0;
            if (actor == null)
                return false;

            cost = Math.Max(0, cost);
            if (actor.currentAP < cost)
                return false;

            int before = actor.currentAP;
            if (cost > 0)
                actor.currentAP = Math.Max(0, actor.currentAP - cost);
            actor.ClampAP();
            paid = before - actor.currentAP;

            if (cost > 0)
                _log?.LogAP(turn, CombatEventType.APSpent, actor.instanceId, -paid, actor.currentAP, CombatApMutationSource.MoveCost);

            _log?.LogForensic(
                CombatForensicEventType.APCostPaid,
                new APCostPaidPayload
                {
                    apBefore = before,
                    cost = cost,
                    apAfter = actor.currentAP,
                },
                actor.instanceId,
                new List<string> { actor.instanceId },
                turn);

            LogApChanged(actor, before, actor.currentAP, string.IsNullOrWhiteSpace(source) ? "MoveCost" : source, turn);
            return true;
        }

        public int Gain(ChainlingInstance target, int amount, int turn, string source,
            CombatEventType eventType = CombatEventType.APGained, bool ignoreThreadLock = false,
            string apMutationSource = null)
        {
            if (target == null || amount <= 0)
                return 0;

            if (!ignoreThreadLock && HasActiveStatus(target, StatusId.ThreadLock))
            {
                string blockedTag = IsTurnRegenSource(source)
                    ? CombatApMutationSource.TurnRegen
                    : CombatApMutationSource.Blocked;
                _log?.LogAP(turn, CombatEventType.APGainBlocked, target.instanceId, 0, target.currentAP, blockedTag);
                _log?.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{target.displayName} could not gain AP from {source} (Thread Lock).",
                    turn);
                LogApChanged(target, target.currentAP, target.currentAP,
                    $"{NormalizeSource(source)}:BlockedByThreadLock", turn);
                return 0;
            }

            int before = target.currentAP;
            int apCap = _resolverConfig.apCap;
            target.currentAP = Math.Min(apCap, target.currentAP + amount);
            target.ClampAP();
            int delta = target.currentAP - before;

            CombatEventType resolvedEventType = delta < amount && target.currentAP >= apCap
                ? CombatEventType.APCapped
                : eventType;

            string mutationTag = apMutationSource ?? ResolveApMutationSource(source, resolvedEventType);
            _log?.LogAP(turn, resolvedEventType, target.instanceId, delta, target.currentAP, mutationTag);
            LogApChanged(target, before, target.currentAP, source, turn);
            return delta;
        }

        public int Refund(ChainlingInstance target, int amount, int turn, string source)
        {
            return Gain(target, amount, turn, source, CombatEventType.APRefunded, false, CombatApMutationSource.Refund);
        }

        public int Drain(ChainlingInstance target, int amount, int turn, string source)
        {
            if (target == null || amount <= 0)
                return 0;

            int before = target.currentAP;
            target.currentAP = Math.Max(0, target.currentAP - amount);
            target.ClampAP();
            int drained = before - target.currentAP;

            if (drained > 0)
                _log?.LogAP(turn, CombatEventType.APSpent, target.instanceId, -drained, target.currentAP, CombatApMutationSource.Drain);
            LogApChanged(target, before, target.currentAP, source, turn);
            return drained;
        }

        public int Steal(ChainlingInstance source, ChainlingInstance target, int amount, int turn, string mutationSource)
        {
            int drained = Drain(target, amount, turn, mutationSource);
            if (drained <= 0 || source == null)
                return 0;

            return Gain(source, drained, turn, mutationSource, CombatEventType.APStolen, false, CombatApMutationSource.Steal);
        }

        public int TransferPaidCost(ChainlingInstance source, ChainlingInstance target, int paidCost, int turn, string mutationSource)
        {
            if (source == null || target == null || paidCost <= 0)
                return 0;

            int granted = Gain(target, paidCost, turn, mutationSource, CombatEventType.APGained, false, CombatApMutationSource.Transfer);
            if (granted > 0)
            {
                _log?.LogMessage(
                    CombatEventType.ActionSelected,
                    $"{source.displayName} transferred {granted} AP to {target.displayName}",
                    turn);
            }

            return granted;
        }

        private void LogApChanged(ChainlingInstance target, int before, int after, string source, int turn)
        {
            if (target == null)
                return;

            _log?.LogForensic(
                CombatForensicEventType.APChanged,
                new APChangedPayload
                {
                    apBefore = before,
                    delta = after - before,
                    apAfter = after,
                    source = NormalizeSource(source),
                },
                target.instanceId,
                new List<string> { target.instanceId },
                turn);
        }

        private static string NormalizeSource(string source)
        {
            return string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim();
        }

        private static string ResolveApMutationSource(string source, CombatEventType eventType)
        {
            if (IsTurnRegenSource(source))
                return CombatApMutationSource.TurnRegen;

            switch (eventType)
            {
                case CombatEventType.APRefunded:
                    return CombatApMutationSource.Refund;
                case CombatEventType.APStolen:
                    return CombatApMutationSource.Steal;
                case CombatEventType.APGainBlocked:
                    return CombatApMutationSource.Blocked;
                case CombatEventType.APCapped:
                    return CombatApMutationSource.Capped;
                default:
                    return CombatApMutationSource.Gain;
            }
        }

        private static bool IsTurnRegenSource(string source)
        {
            return string.Equals(source, "TurnRegen", StringComparison.Ordinal);
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
