namespace Chainbound.Combat
{
    /// <summary>
    /// Shared guard for effects that would add a buff or a positive stage while Cipher Locked.
    /// Reversion and negative deltas deliberately bypass this gate.
    /// </summary>
    public static class BuffGate
    {
        public static bool IsCipherLocked(ChainlingInstance target)
        {
            if (target?.statuses == null)
                return false;
            for (int i = 0; i < target.statuses.Count; i++)
            {
                StatusInstance status = target.statuses[i];
                if (status != null && status.statusId == StatusId.CipherLocked && !status.IsExpired)
                    return true;
            }
            return false;
        }

        public static bool AllowsBuffStatus(
            ChainlingInstance target,
            StatusDefinitionSO definition,
            string sourceId,
            int turn)
        {
            return definition == null || !definition.isBuff || !IsCipherLocked(target);
        }

        public static int FilterPositiveStage(
            ChainlingInstance target,
            CoreStat stat,
            int requestedDelta,
            string sourceTag,
            int turn)
        {
            return requestedDelta > 0 && IsCipherLocked(target) ? 0 : requestedDelta;
        }

        public static int FilterPositiveStage(
            ChainlingInstance target,
            CombatStageStat stat,
            int requestedDelta,
            string sourceTag,
            int turn)
        {
            return requestedDelta > 0 && IsCipherLocked(target) ? 0 : requestedDelta;
        }
    }
}
