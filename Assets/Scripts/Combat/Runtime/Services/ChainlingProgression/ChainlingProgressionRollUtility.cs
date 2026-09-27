using System;
using UnityEngine;
using Random = System.Random;

namespace Chainbound.Combat
{
    public static class ChainlingProgressionRollUtility
    {
        public static bool TryRollStarGate(
            int starIndex,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            int bonusPercent,
            Random rng)
        {
            tables = ResolveTables(tables);
            return RollPercent(tables.GetStarGatePercent(starIndex, isEntangled, bonusPercent), rng);
        }

        public static bool TryRollMasteryGate(
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            int bonusPercent,
            Random rng)
        {
            tables = ResolveTables(tables);
            return RollPercent(tables.GetMasteryGatePercent(isEntangled, bonusPercent), rng);
        }

        public static ResolvedProgressionMoveEntry RollMoveFromPool(
            ChainlingProgressionResolvedPool pool,
            Random rng)
        {
            if (pool == null || pool.entries == null || pool.entries.Count == 0)
                return null;

            rng = rng ?? new Random();
            int totalWeight = pool.TotalWeight;
            if (totalWeight <= 0)
                return null;

            int roll = rng.Next(0, totalWeight);
            int cursor = 0;
            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry == null || entry.effectiveWeight <= 0)
                    continue;

                cursor += entry.effectiveWeight;
                if (roll < cursor)
                    return entry;
            }

            return null;
        }

        public static Random BuildDeterministicRng(
            int battleSeed,
            string chainlingBackendId,
            int starIndex,
            int rollIndex)
        {
            unchecked
            {
                uint hash = 2166136261;
                hash = FnvStep(hash, battleSeed);
                hash = FnvStep(hash, starIndex);
                hash = FnvStep(hash, rollIndex);

                string id = chainlingBackendId ?? "";
                for (int i = 0; i < id.Length; i++)
                {
                    hash ^= id[i];
                    hash *= 16777619;
                }

                int seed = (int)(hash & 0x7fffffff);
                return new Random(seed == 0 ? 1 : seed);
            }
        }

        public static int GetStarGatePercent(
            int starIndex,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            int bonusPercent = 0)
        {
            return ResolveTables(tables).GetStarGatePercent(starIndex, isEntangled, bonusPercent);
        }

        public static int GetMasteryGatePercent(
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            int bonusPercent = 0)
        {
            return ResolveTables(tables).GetMasteryGatePercent(isEntangled, bonusPercent);
        }

        private static bool RollPercent(int percent, Random rng)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (percent <= 0)
                return false;
            if (percent >= 100)
                return true;

            rng = rng ?? new Random();
            return rng.Next(0, 100) < percent;
        }

        private static ChainlingProgressionRewardTablesSO ResolveTables(ChainlingProgressionRewardTablesSO tables)
        {
            return tables != null ? tables : ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
        }

        private static uint FnvStep(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619;
                return hash;
            }
        }
    }
}
