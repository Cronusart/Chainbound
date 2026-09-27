using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared IV / Cycle-pick copy+clamp helpers for manual and production snapshot mappers.
    /// </summary>
    public static class ChainlingSnapshotStatUtility
    {
        public static int[] CopyIvs(int[] source)
        {
            var result = new int[CombatConstants.STAT_COUNT];
            if (source == null)
                return result;

            int copy = Mathf.Min(source.Length, result.Length);
            for (int i = 0; i < copy; i++)
                result[i] = Mathf.Clamp(source[i], CombatConstants.IV_MIN, CombatConstants.IV_MAX);

            return result;
        }

        public static int[] CopyRebirthIvBonuses(int[] source)
        {
            return ChainlingProgressionUtility.NormalizeRebirthIvBonuses(source);
        }

        /// <summary>
        /// Soft warning when pick allocations do not match Cycle count. Does not invent picks.
        /// </summary>
        public static void WarnIfPickSumMismatch(string context, int rebirthCount, int[] rebirthIvBonuses)
        {
            if (rebirthIvBonuses == null)
                return;

            int sum = 0;
            for (int i = 0; i < rebirthIvBonuses.Length; i++)
                sum += Mathf.Max(0, rebirthIvBonuses[i]);

            int normalizedRebirth = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount);
            if (sum == normalizedRebirth)
                return;

            Debug.LogWarning(
                $"[ChainlingSnapshotStatUtility] {context}: rebirth_iv_bonuses sum ({sum}) " +
                $"!= rebirth_count ({normalizedRebirth}). Using authored picks as-is.");
        }
    }
}
