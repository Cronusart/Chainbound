using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lever board for how/when a Chainling advances: XP thresholds, mastery, rebirth costs,
    /// Cycle flat+percent grants, and move-learn rarity weighting.
    /// Does NOT own post-battle grant amounts — those live on <see cref="PostBattleRewardsConfigSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionConfig", menuName = "Chainbound/Combat/Config/Progression Config")]
    public class ProgressionConfigSO : ScriptableObject
    {
        private static readonly int[] DefaultStarXpThresholds = { 450, 700, 1050, 1600, 2400 };
        private static readonly int[] DefaultRebirthRamCosts = { 1000, 1500, 2000, 2500, 3000 };
        private static readonly float[] DefaultMoveLearnRarityWeights =
        {
            1.00f, // Common
            0.72f, // Uncommon
            0.48f, // Rare
            0.28f, // Epic
            0.14f, // Legendary
            0.06f, // Mythic
        };

        [Header("Identity")]
        [Min(1)] public int configVersion = 1;

        [Header("Star Thresholds (0->1, 1->2, ..., 4->5)")]
        public int[] starXpThresholds = { 450, 700, 1050, 1600, 2400 };

        [Header("Mastery")]
        public int masteryBaseXp = 3000;

        [Header("Rebirth")]
        public float rebirthXpMultiplierBase = 1.18f;
        public int[] rebirthRamCosts = { 1000, 1500, 2000, 2500, 3000 };

        [Header("Cycle Grants — Combat Stats (flat + percent of species base)")]
        public int cycleAutoStatFlat = CombatConstants.CYCLE_AUTO_STAT_FLAT;
        [Range(0f, 1f)] public float cycleAutoStatPercent = 0f;
        public int cyclePickStatFlat = CombatConstants.CYCLE_PICK_STAT_FLAT;
        [Range(0f, 1f)] public float cyclePickStatPercent = 0f;

        [Header("Cycle Grants — MaxHP (flat + percent of species base)")]
        public int cycleAutoHpFlat = 0;
        [Range(0f, 1f)] public float cycleAutoHpPercent = CombatConstants.CYCLE_AUTO_HP_PERCENT;
        public int cyclePickHpFlat = 0;
        [Range(0f, 1f)] public float cyclePickHpPercent = CombatConstants.CYCLE_PICK_HP_PERCENT;

        [Header("Move-Learn Rarity Weights (per RarityTier index)")]
        [Tooltip("Multiplies SpeciesProgressionMoveEntry.weight by this factor for the move's rarity. Common 1.00, Uncommon 0.72, Rare 0.48, Epic 0.28, Legendary 0.14, Mythic 0.06.")]
        public float[] moveLearnRarityWeights = { 1.00f, 0.72f, 0.48f, 0.28f, 0.14f, 0.06f };

        public int GetRebirthRamCost(int currentRebirthCount)
        {
            if (rebirthRamCosts == null || rebirthRamCosts.Length == 0)
                return 0;

            int index = Mathf.Clamp(currentRebirthCount, 0, rebirthRamCosts.Length - 1);
            return Mathf.Max(0, rebirthRamCosts[index]);
        }

        public float GetMoveLearnRarityWeight(RarityTier rarity)
        {
            int index = (int)rarity;
            if (moveLearnRarityWeights == null || index < 0 || index >= moveLearnRarityWeights.Length)
                return 1f;
            return Mathf.Max(0f, moveLearnRarityWeights[index]);
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashProgressionConfig(this);

        public static ProgressionConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<ProgressionConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 1;
            starXpThresholds = (int[])DefaultStarXpThresholds.Clone();
            masteryBaseXp = 3000;
            rebirthXpMultiplierBase = 1.18f;
            rebirthRamCosts = (int[])DefaultRebirthRamCosts.Clone();
            cycleAutoStatFlat = CombatConstants.CYCLE_AUTO_STAT_FLAT;
            cycleAutoStatPercent = 0f;
            cyclePickStatFlat = CombatConstants.CYCLE_PICK_STAT_FLAT;
            cyclePickStatPercent = 0f;
            cycleAutoHpFlat = 0;
            cycleAutoHpPercent = CombatConstants.CYCLE_AUTO_HP_PERCENT;
            cyclePickHpFlat = 0;
            cyclePickHpPercent = CombatConstants.CYCLE_PICK_HP_PERCENT;
            moveLearnRarityWeights = (float[])DefaultMoveLearnRarityWeights.Clone();
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            starXpThresholds = NormalizeIntArray(starXpThresholds, DefaultStarXpThresholds, minValue: 1);
            rebirthRamCosts = NormalizeIntArray(rebirthRamCosts, DefaultRebirthRamCosts, minValue: 0);
            masteryBaseXp = Mathf.Max(1, masteryBaseXp);
            rebirthXpMultiplierBase = Mathf.Max(1f, rebirthXpMultiplierBase);
            cycleAutoStatFlat = Mathf.Max(0, cycleAutoStatFlat);
            cyclePickStatFlat = Mathf.Max(0, cyclePickStatFlat);
            cycleAutoHpFlat = Mathf.Max(0, cycleAutoHpFlat);
            cyclePickHpFlat = Mathf.Max(0, cyclePickHpFlat);
            cycleAutoStatPercent = Mathf.Clamp01(cycleAutoStatPercent);
            cyclePickStatPercent = Mathf.Clamp01(cyclePickStatPercent);
            cycleAutoHpPercent = Mathf.Clamp01(cycleAutoHpPercent);
            cyclePickHpPercent = Mathf.Clamp01(cyclePickHpPercent);
            moveLearnRarityWeights = NormalizeFloatArray(moveLearnRarityWeights, DefaultMoveLearnRarityWeights);
        }

        private static int[] NormalizeIntArray(int[] values, int[] defaults, int minValue)
        {
            if (values == null || values.Length != defaults.Length)
                values = (int[])defaults.Clone();

            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Max(minValue, values[i]);

            return values;
        }

        private static float[] NormalizeFloatArray(float[] values, float[] defaults)
        {
            if (values == null || values.Length != defaults.Length)
                values = (float[])defaults.Clone();

            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Max(0f, values[i]);

            return values;
        }
    }
}
