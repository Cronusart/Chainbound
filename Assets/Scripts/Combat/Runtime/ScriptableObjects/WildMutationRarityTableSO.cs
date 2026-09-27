using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class WildMutationRarityEntry
    {
        [Tooltip("Mutation FX id mapped to an aura/overlay. Use 'none' for no mutation.")]
        public string mutationId = ChainlingVisualIdentity.NoneId;

        public RarityTier rarity = RarityTier.Common;

        [Min(0)] public int weight = 100;
    }

    /// <summary>
    /// Weighted mutation FX rarity table for wild encounters.
    /// Include a Common 'none' entry as the default no-mutation outcome.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WildMutationRarityTable_",
        menuName = "Chainbound/Combat/Encounters/Wild Mutation Rarity Table")]
    public class WildMutationRarityTableSO : ScriptableObject
    {
        public List<WildMutationRarityEntry> entries = new List<WildMutationRarityEntry>();

        public List<WeightedMutationEntry> BuildWeightedPool(
            IReadOnlyList<float> tierMultipliers = null,
            float nonNoneScalar = 1f)
        {
            var result = new List<WeightedMutationEntry>();
            if (entries == null)
                return result;

            float scalar = Mathf.Max(0f, nonNoneScalar);
            for (int i = 0; i < entries.Count; i++)
            {
                WildMutationRarityEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.mutationId) || entry.weight <= 0)
                    continue;

                string mutationId = ChainlingVisualIdentity.NormalizeId(
                    entry.mutationId,
                    ChainlingVisualIdentity.NoneId);
                float tierMult = EncounterRarityLeverUtility.ResolveTierMultiplier(entry.rarity, tierMultipliers);
                float entryScalar = string.Equals(mutationId, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal)
                    ? 1f
                    : scalar;

                int scaled = Mathf.Max(0, Mathf.RoundToInt(entry.weight * tierMult * entryScalar));
                if (scaled <= 0)
                    continue;

                result.Add(new WeightedMutationEntry
                {
                    mutationId = mutationId,
                    rarity = entry.rarity,
                    weight = scaled,
                });
            }

            return result;
        }
    }

    [Serializable]
    public class WeightedMutationEntry
    {
        public string mutationId = ChainlingVisualIdentity.NoneId;
        public RarityTier rarity = RarityTier.Common;
        public int weight = 100;
    }
}
