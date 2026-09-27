using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds production-shaped <see cref="ChainlingSnapshot"/> values from manual test entries.
    /// </summary>
    public static class ManualChainlingSnapshotBuilder
    {
        public static ChainlingSnapshot Build(ManualChainlingEntry entry, bool logPickMismatch = true)
        {
            if (entry == null)
                return null;

            int stars = ChainlingProgressionUtility.NormalizeStars(entry.stars);
            int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(entry.rebirthCount);

            int[] picks = entry.BuildCyclePickArray();
            int pickSum = 0;
            for (int i = 0; i < picks.Length; i++)
            {
                picks[i] = Mathf.Max(0, picks[i]);
                pickSum += picks[i];
            }

            if (pickSum > rebirthCount)
            {
                if (logPickMismatch)
                {
                    Debug.LogWarning(
                        $"[ManualChainlingSnapshotBuilder] Cycle picks sum ({pickSum}) exceeds rebirthCount ({rebirthCount}) " +
                        $"for '{entry.speciesId}'. Truncating excess picks.");
                }

                TruncatePicksToBudget(picks, rebirthCount);
            }
            else if (pickSum < rebirthCount && pickSum > 0 && logPickMismatch)
            {
                Debug.LogWarning(
                    $"[ManualChainlingSnapshotBuilder] Cycle picks sum ({pickSum}) < rebirthCount ({rebirthCount}) " +
                    $"for '{entry.speciesId}'. Shortfall left unused (auto Cycle still applies).");
            }

            var snapshot = new ChainlingSnapshot
            {
                speciesId = entry.speciesId,
                displayName = entry.displayName,
                level = 0,
                stars = stars,
                rebirthCount = rebirthCount,
                masteryXp = Mathf.Max(0, entry.masteryXp),
                evolutionStage = entry.evolutionStage > 0 ? entry.evolutionStage : 1,
                visualIdentity = ChainlingVisualIdentity.CloneOrDefault(entry.visualIdentity),
                moveIds = entry.moveIds != null
                    ? new List<string>(entry.moveIds)
                    : new List<string>(),
                modifier1SectionIds = entry.modifier1SectionIds != null
                    ? new List<string>(entry.modifier1SectionIds)
                    : new List<string>(),
                modifier2SectionIds = entry.modifier2SectionIds != null
                    ? new List<string>(entry.modifier2SectionIds)
                    : new List<string>(),
                ivs = ChainlingSnapshotStatUtility.CopyIvs(entry.BuildIvArray()),
                rebirthIvBonuses = ChainlingSnapshotStatUtility.CopyRebirthIvBonuses(picks),
                currentHp = entry.currentHp,
                isMutated = entry.isMutated,
                mutationId = entry.isMutated
                    ? ChainlingVisualIdentity.NormalizeId(entry.mutationId, ChainlingVisualIdentity.NoneId)
                    : ChainlingVisualIdentity.NoneId,
                isEntangled = entry.isEntangled,
                entangledType = entry.isEntangled ? entry.entangledType : ChainlingType.None,
            };
            if (snapshot.isMutated &&
                !string.Equals(snapshot.mutationId, ChainlingVisualIdentity.NoneId, System.StringComparison.Ordinal))
            {
                snapshot.visualIdentity.auraId = snapshot.mutationId;
            }

            return snapshot;
        }

        /// <summary>
        /// Truncates from the last non-zero pick toward the first until sum == budget.
        /// </summary>
        public static void TruncatePicksToBudget(int[] picks, int budget)
        {
            if (picks == null)
                return;

            budget = Mathf.Max(0, budget);
            int sum = 0;
            for (int i = 0; i < picks.Length; i++)
            {
                picks[i] = Mathf.Max(0, picks[i]);
                sum += picks[i];
            }

            for (int i = picks.Length - 1; i >= 0 && sum > budget; i--)
            {
                int excess = sum - budget;
                int reduce = Mathf.Min(picks[i], excess);
                picks[i] -= reduce;
                sum -= reduce;
            }
        }
    }
}
