using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rolls a mutation FX id from a weighted rarity pool.
    /// </summary>
    public static class WildMutationRoller
    {
        public static WeightedMutationEntry RollEntry(
            IReadOnlyList<WeightedMutationEntry> pool,
            System.Random rng)
        {
            if (rng == null)
                rng = new System.Random();

            if (pool == null || pool.Count == 0)
                return None();

            var working = new List<WeightedMutationEntry>();
            for (int i = 0; i < pool.Count; i++)
            {
                WeightedMutationEntry entry = pool[i];
                if (entry == null || entry.weight <= 0 || string.IsNullOrWhiteSpace(entry.mutationId))
                    continue;
                working.Add(entry);
            }

            if (working.Count == 0)
                return None();

            WeightedMutationEntry picked = WeightedPick(working, rng);
            if (picked == null)
                return None();

            return new WeightedMutationEntry
            {
                mutationId = ChainlingVisualIdentity.NormalizeId(picked.mutationId, ChainlingVisualIdentity.NoneId),
                rarity = picked.rarity,
                weight = picked.weight,
            };
        }

        public static string Roll(
            IReadOnlyList<WeightedMutationEntry> pool,
            System.Random rng)
        {
            return RollEntry(pool, rng).mutationId;
        }

        private static WeightedMutationEntry None()
        {
            return new WeightedMutationEntry
            {
                mutationId = ChainlingVisualIdentity.NoneId,
                rarity = RarityTier.Common,
                weight = 0,
            };
        }

        private static WeightedMutationEntry WeightedPick(List<WeightedMutationEntry> entries, System.Random rng)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
                total += Mathf.Max(0, entries[i].weight);
            if (total <= 0)
                return entries[0];

            int roll = rng.Next(total);
            int cumulative = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += Mathf.Max(0, entries[i].weight);
                if (roll < cumulative)
                    return entries[i];
            }

            return entries[entries.Count - 1];
        }
    }
}
