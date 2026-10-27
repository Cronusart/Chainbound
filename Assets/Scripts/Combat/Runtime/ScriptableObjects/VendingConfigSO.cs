using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class VendingItemClassEntry
    {
        public string classId = "modifiers";
        [Min(0)] public int weight = 100;
        public bool enabled = true;
        [Tooltip("Optional ISO date (yyyy-MM-dd). Empty = always available.")]
        public string availableFromUtc;
        [Tooltip("Optional ISO date (yyyy-MM-dd). Empty = no end.")]
        public string availableUntilUtc;
    }

    /// <summary>
    /// Lever board for vending rarity odds, costs, caps, and item-class tables.
    /// Server override via get_vending_config is authoritative when present.
    /// </summary>
    [CreateAssetMenu(fileName = "VendingConfig", menuName = "Chainbound/Combat/Config/Vending Config")]
    public class VendingConfigSO : ScriptableObject
    {
        public const int RollBasisPoints = 10000;

        // ECONOMY_01 defaults (percent): 45 / 30 / 17.5 / 5 / 2 / 0.5
        private static readonly float[] DefaultTierWeightsPercent =
        {
            45f, 30f, 17.5f, 5f, 2f, 0.5f
        };

        private static readonly string[] DefaultKnownPoolMoveIds =
        {
            "baseline_striker", "baseline_slam", "baseline_jab", "baseline_shot", "baseline_snipe",
            "baseline_volley", "baseline_blast", "baseline_barrage", "baseline_thrust", "baseline_multi_strike",
        };

        [Header("Identity")]
        [Min(1)] public int configVersion = 1;
        [Tooltip("Bump when VRF derivation contract changes (e.g. basis points).")]
        [Min(1)] public int generatorVersion = 2;

        [Header("Rarity Tier Weights (percent, ECONOMY_01)")]
        [Tooltip("Order: Common, Uncommon, Rare, Epic, Legendary, Mythic. Normalized to basis points at roll time.")]
        public float[] rarityTierWeightsPercent = { 45f, 30f, 17.5f, 5f, 2f, 0.5f };

        [Header("Costs / Caps")]
        public int ramCostGetKnown = CombatConstants.RAM_COST_GET_KNOWN;
        public int ramCostStampKnown = CombatConstants.RAM_COST_STAMP_KNOWN;
        public int vendingDailyCap = CombatConstants.VENDING_DAILY_CAP;

        [Header("Item Classes")]
        public List<VendingItemClassEntry> itemClasses = new List<VendingItemClassEntry>
        {
            new VendingItemClassEntry { classId = "modifiers", weight = 100, enabled = true },
        };

        [Header("Known Move Pool")]
        [Tooltip("Authored candy-roll membership. Server generation fills element and power from the move catalog and rejects unavailable moves.")]
        public List<string> knownPoolMoveIds = new List<string>(DefaultKnownPoolMoveIds);

        [Header("Event Favor Boost (server-gated)")]
        [Tooltip("Additive rarity bias toward rarer tiers. Default 0; only non-zero via server override.")]
        [Range(0f, 1f)] public float eventFavorBoost = 0f;

        /// <summary>
        /// Builds cumulative basis-point thresholds (length 6) summing to RollBasisPoints.
        /// </summary>
        public int[] BuildCumulativeThresholds()
        {
            float[] weights = rarityTierWeightsPercent;
            if (weights == null || weights.Length != 6)
                weights = DefaultTierWeightsPercent;

            float sum = 0f;
            for (int i = 0; i < 6; i++)
                sum += Mathf.Max(0f, weights[i]);

            var thresholds = new int[6];
            if (sum <= 0f)
            {
                for (int i = 0; i < 6; i++)
                    thresholds[i] = (i + 1) * (RollBasisPoints / 6);
                thresholds[5] = RollBasisPoints;
                return thresholds;
            }

            int cumulative = 0;
            for (int i = 0; i < 6; i++)
            {
                int slice = Mathf.RoundToInt((Mathf.Max(0f, weights[i]) / sum) * RollBasisPoints);
                cumulative += slice;
                thresholds[i] = cumulative;
            }

            thresholds[5] = RollBasisPoints;
            return thresholds;
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashVendingConfig(this);

        public static VendingConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<VendingConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 1;
            generatorVersion = 2;
            rarityTierWeightsPercent = (float[])DefaultTierWeightsPercent.Clone();
            ramCostGetKnown = CombatConstants.RAM_COST_GET_KNOWN;
            ramCostStampKnown = CombatConstants.RAM_COST_STAMP_KNOWN;
            vendingDailyCap = CombatConstants.VENDING_DAILY_CAP;
            itemClasses = new List<VendingItemClassEntry>
            {
                new VendingItemClassEntry { classId = "modifiers", weight = 100, enabled = true },
            };
            knownPoolMoveIds = new List<string>(DefaultKnownPoolMoveIds);
            eventFavorBoost = 0f;
        }

        /// <summary>Applies server JSON fields onto this instance (mutates).</summary>
        public void ApplyServerOverride(
            int version,
            int genVersion,
            float[] tierWeights,
            int getKnownCost,
            int stampKnownCost,
            int dailyCap,
            float favorBoost)
        {
            configVersion = Mathf.Max(1, version);
            generatorVersion = Mathf.Max(1, genVersion);
            if (tierWeights != null && tierWeights.Length == 6)
                rarityTierWeightsPercent = (float[])tierWeights.Clone();
            ramCostGetKnown = Mathf.Max(0, getKnownCost);
            ramCostStampKnown = Mathf.Max(0, stampKnownCost);
            vendingDailyCap = Mathf.Max(0, dailyCap);
            eventFavorBoost = Mathf.Clamp01(favorBoost);
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            generatorVersion = Mathf.Max(1, generatorVersion);
            if (rarityTierWeightsPercent == null || rarityTierWeightsPercent.Length != 6)
                rarityTierWeightsPercent = (float[])DefaultTierWeightsPercent.Clone();

            for (int i = 0; i < rarityTierWeightsPercent.Length; i++)
                rarityTierWeightsPercent[i] = Mathf.Max(0f, rarityTierWeightsPercent[i]);

            ramCostGetKnown = Mathf.Max(0, ramCostGetKnown);
            ramCostStampKnown = Mathf.Max(0, ramCostStampKnown);
            vendingDailyCap = Mathf.Max(0, vendingDailyCap);
            eventFavorBoost = Mathf.Clamp01(eventFavorBoost);

            if (itemClasses == null)
                itemClasses = new List<VendingItemClassEntry>();
            if (knownPoolMoveIds == null || knownPoolMoveIds.Count == 0)
                knownPoolMoveIds = new List<string>(DefaultKnownPoolMoveIds);
        }
    }
}
