using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Tunable Favor (luck) application curves for PvE sinks.
    /// Soft curves keep stacked party grants weaker than one Favor Boost.
    /// </summary>
    [CreateAssetMenu(fileName = "FavorConfig", menuName = "Chainbound/Combat/Config/Favor Config")]
    public class FavorConfigSO : ScriptableObject
    {
        [Header("Soft Curve")]
        [Tooltip("Reference score for a full Favor Boost (~100).")]
        public int referenceScore = 100;

        [Tooltip("Higher = slower approach to max bias (diminishing returns).")]
        public float softCurveK = 100f;

        [Header("Encounter")]
        [Range(0f, 1f)]
        [Tooltip("Max relative weight bias toward rarer (lower-weight) zone rows.")]
        public float encounterRarityBiasMax = 0.35f;

        [Header("Capture")]
        [Range(0f, 0.25f)]
        [Tooltip("Max additive bump on capture probability P.")]
        public float captureProbabilityBonusMax = 0.08f;

        [Header("Wild IVs")]
        [Range(0f, 1f)]
        [Tooltip("Max chance to bump the lowest IV by +1 after the base roll.")]
        public float ivLowestBumpChanceMax = 0.45f;

        [Header("Rare Side-Drop")]
        [Range(0f, 0.1f)]
        public float rareConsumableBaseChance = 0.01f;

        [Range(0f, 0.05f)]
        public float rareConsumableFavorBonusMax = 0.01f;

        public string[] rareConsumableItemIds =
        {
            "favor_boost_s",
            "chainling_update",
        };

        /// <summary>
        /// Maps raw favorScore into [0,1] with diminishing returns.
        /// </summary>
        public float EvaluateCurve01(int favorScore)
        {
            int score = Mathf.Max(0, favorScore);
            float k = Mathf.Max(1f, softCurveK);
            return score / (score + k);
        }

        public float EncounterWeightMultiplier(int favorScore, int entryWeight, int maxWeight)
        {
            float t = EvaluateCurve01(favorScore);
            if (t <= 0f || maxWeight <= 0)
                return 1f;

            float rarity01 = 1f - Mathf.Clamp01(entryWeight / (float)maxWeight);
            float bias = encounterRarityBiasMax * t * rarity01;
            return 1f + bias;
        }

        public float CaptureProbabilityBonus(int favorScore)
        {
            return captureProbabilityBonusMax * EvaluateCurve01(favorScore);
        }

        public float IvLowestBumpChance(int favorScore)
        {
            return ivLowestBumpChanceMax * EvaluateCurve01(favorScore);
        }

        public float RareConsumableChance(int favorScore)
        {
            return Mathf.Clamp01(
                rareConsumableBaseChance + rareConsumableFavorBonusMax * EvaluateCurve01(favorScore));
        }

        public static FavorConfigSO CreateRuntimeDefault()
        {
            FavorConfigSO config = CreateInstance<FavorConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }
    }
}
