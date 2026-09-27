using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class WildVariantRarityEntry
    {
        [Tooltip("Maps to ChainlingVisualIdentity.visualVariantId / species visual-set variantId.")]
        public string visualVariantId = ChainlingVisualIdentity.BaseVariantId;

        [Tooltip("Odds-tier label for encounter rarity multipliers. Not a numeric weight. Prefer matching the skin's catalog rarity.")]
        public RarityTier rarity = RarityTier.Common;

        [Tooltip("Relative spawn weight for this skin in this table. Scaled by rarity-tier multipliers.")]
        [Min(0)] public int weight = 100;
    }

    /// <summary>
    /// Weighted cosmetic skin rarity table for wild encounters.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WildVariantRarityTable_",
        menuName = "Chainbound/Combat/Encounters/Wild Variant Rarity Table")]
    public class WildVariantRarityTableSO : ScriptableObject
    {
        public List<WildVariantRarityEntry> entries = new List<WildVariantRarityEntry>();

        public List<WeightedVariantEntry> BuildWeightedPool(
            IReadOnlyList<float> tierMultipliers = null,
            IReadOnlyDictionary<string, float> variantWeightMultipliers = null)
        {
            var result = new List<WeightedVariantEntry>();
            if (entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                WildVariantRarityEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.visualVariantId) || entry.weight <= 0)
                    continue;

                string variantId = entry.visualVariantId.Trim();
                float tierMult = EncounterRarityLeverUtility.ResolveTierMultiplier(entry.rarity, tierMultipliers);
                float variantMult = 1f;
                if (variantWeightMultipliers != null &&
                    variantWeightMultipliers.TryGetValue(variantId, out float m))
                {
                    variantMult = Mathf.Max(0f, m);
                }

                int scaled = Mathf.Max(0, Mathf.RoundToInt(entry.weight * tierMult * variantMult));
                if (scaled <= 0)
                    continue;

                result.Add(new WeightedVariantEntry
                {
                    visualVariantId = variantId,
                    weight = scaled,
                });
            }

            return result;
        }
    }
}
