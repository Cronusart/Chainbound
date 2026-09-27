using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rolls a visual variant id from a weighted pool.
    /// </summary>
    public static class WildVariantRoller
    {
        public static ChainlingVisualIdentity Roll(
            IReadOnlyList<WeightedVariantEntry> pool,
            System.Random rng,
            bool isEntangled,
            IReadOnlyDictionary<string, float> variantWeightMultipliers = null)
        {
            var identity = ChainlingVisualIdentity.CreateDefault();
            if (rng == null)
                rng = new System.Random();

            string variantId = ChainlingVisualIdentity.BaseVariantId;
            var working = BuildWorkingPool(pool, variantWeightMultipliers);
            if (working.Count > 0)
            {
                var picked = WeightedPick(working, rng);
                if (picked != null && !string.IsNullOrWhiteSpace(picked.visualVariantId))
                    variantId = picked.visualVariantId.Trim();
            }

            identity.visualVariantId = variantId;
            // Entangled is a separate trait flag/type; do not rewrite skin id.
            identity.Normalize();
            return identity;
        }

        private static List<WeightedVariantEntry> BuildWorkingPool(
            IReadOnlyList<WeightedVariantEntry> pool,
            IReadOnlyDictionary<string, float> multipliers)
        {
            var working = new List<WeightedVariantEntry>();
            if (pool == null)
                return working;

            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.visualVariantId) || entry.weight <= 0)
                    continue;

                float mult = 1f;
                if (multipliers != null &&
                    multipliers.TryGetValue(entry.visualVariantId.Trim(), out float m))
                {
                    mult = Mathf.Max(0f, m);
                }

                int scaled = Mathf.Max(0, Mathf.RoundToInt(entry.weight * mult));
                if (scaled <= 0)
                    continue;

                working.Add(new WeightedVariantEntry
                {
                    visualVariantId = entry.visualVariantId.Trim(),
                    weight = scaled,
                });
            }

            return working;
        }

        private static WeightedVariantEntry WeightedPick(List<WeightedVariantEntry> entries, System.Random rng)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
                total += Mathf.Max(0, entries[i].weight);
            if (total <= 0)
                return entries.Count > 0 ? entries[0] : null;

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
