using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared helpers for per-tier encounter rarity levers (variant + mutation tables).
    /// </summary>
    public static class EncounterRarityLeverUtility
    {
        public const int TierCount = 6;

        /// <summary>
        /// Returns a 6-length multiplier array indexed by <see cref="RarityTier"/>.
        /// Negative / unset source values become 1.
        /// </summary>
        public static float[] NormalizeTierMultipliers(IReadOnlyList<float> source)
        {
            var result = new float[TierCount];
            for (int i = 0; i < TierCount; i++)
            {
                float value = source != null && i < source.Count ? source[i] : 1f;
                result[i] = value < 0f ? 1f : value;
            }

            return result;
        }

        public static float ResolveTierMultiplier(RarityTier rarity, IReadOnlyList<float> tierMultipliers)
        {
            if (tierMultipliers == null || tierMultipliers.Count == 0)
                return 1f;

            int index = (int)rarity;
            if (index < 0 || index >= tierMultipliers.Count)
                return 1f;

            float value = tierMultipliers[index];
            return value < 0f ? 1f : Mathf.Max(0f, value);
        }

        public static float[] MergeTierMultipliers(IReadOnlyList<float> parent, IReadOnlyList<float> child)
        {
            float[] merged = NormalizeTierMultipliers(parent);
            if (child == null)
                return merged;

            for (int i = 0; i < TierCount && i < child.Count; i++)
            {
                // Negative means inherit parent/default.
                if (child[i] >= 0f)
                    merged[i] = child[i];
            }

            return merged;
        }
    }
}
