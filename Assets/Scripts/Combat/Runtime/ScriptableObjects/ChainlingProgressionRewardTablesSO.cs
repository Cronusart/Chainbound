using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingProgressionRewardTables", menuName = "Chainbound/Combat/Config/Chainling Progression Reward Tables")]
    public class ChainlingProgressionRewardTablesSO : ScriptableObject
    {
        private static readonly int[] DefaultStandard = { 10, 20, 30, 40, 50 };
        private static readonly int[] DefaultEntangled = { 15, 25, 35, 45, 55 };

        [Header("Star Known Chances")]
        [Tooltip("Percent chance to grant any Known on star-up. Index 0 is star 1.")]
        public int[] starKnownMoveChanceStandard = { 10, 20, 30, 40, 50 };

        [Tooltip("Percent chance to grant any Known on star-up for entangled Chainlings. Index 0 is star 1.")]
        public int[] starKnownMoveChanceEntangled = { 15, 25, 35, 45, 55 };

        [Header("Mastery")]
        [Range(0, 100)] public int masteryKnownMoveChanceStandard = 50;
        [Range(0, 100)] public int masteryKnownMoveChanceEntangled = 55;

        public int GetStarGatePercent(int starIndex, bool isEntangled, int bonusPercent = 0)
        {
            int index = Mathf.Clamp(starIndex - 1, 0, CombatConstants.STAR_MAX - 1);
            int[] source = isEntangled ? starKnownMoveChanceEntangled : starKnownMoveChanceStandard;
            int baseChance = source != null && source.Length > index ? source[index] : 0;
            return Mathf.Clamp(baseChance + bonusPercent, 0, 100);
        }

        public int GetMasteryGatePercent(bool isEntangled, int bonusPercent = 0)
        {
            int baseChance = isEntangled ? masteryKnownMoveChanceEntangled : masteryKnownMoveChanceStandard;
            return Mathf.Clamp(baseChance + bonusPercent, 0, 100);
        }

        public static ChainlingProgressionRewardTablesSO CreateRuntimeDefault()
        {
            var tables = CreateInstance<ChainlingProgressionRewardTablesSO>();
            tables.hideFlags = HideFlags.HideAndDontSave;
            tables.ResetToEconomyDefaults();
            return tables;
        }

        public void ResetToEconomyDefaults()
        {
            starKnownMoveChanceStandard = (int[])DefaultStandard.Clone();
            starKnownMoveChanceEntangled = (int[])DefaultEntangled.Clone();
            masteryKnownMoveChanceStandard = 50;
            masteryKnownMoveChanceEntangled = 55;
        }

        private void OnValidate()
        {
            starKnownMoveChanceStandard = NormalizeArray(starKnownMoveChanceStandard, DefaultStandard);
            starKnownMoveChanceEntangled = NormalizeArray(starKnownMoveChanceEntangled, DefaultEntangled);
            masteryKnownMoveChanceStandard = Mathf.Clamp(masteryKnownMoveChanceStandard, 0, 100);
            masteryKnownMoveChanceEntangled = Mathf.Clamp(masteryKnownMoveChanceEntangled, 0, 100);
        }

        private static int[] NormalizeArray(int[] values, int[] defaults)
        {
            if (values == null || values.Length != CombatConstants.STAR_MAX)
                values = (int[])defaults.Clone();

            for (int i = 0; i < values.Length; i++)
                values[i] = Mathf.Clamp(values[i], 0, 100);

            return values;
        }
    }
}
