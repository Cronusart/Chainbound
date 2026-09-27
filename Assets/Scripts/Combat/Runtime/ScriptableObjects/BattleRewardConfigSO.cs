using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Obsolete combined board. Prefer <see cref="PostBattleRewardsConfigSO"/> + <see cref="ProgressionConfigSO"/>.
    /// Kept so leftover serialized references do not become missing scripts.
    /// </summary>
    [System.Obsolete("Use PostBattleRewardsConfigSO + ProgressionConfigSO")]
    [CreateAssetMenu(fileName = "BattleRewardConfig", menuName = "Chainbound/Combat/Config/Battle Reward Config (Obsolete)")]
    public class BattleRewardConfigSO : ScriptableObject
    {
        [Header("KO XP by Difficulty")]
        public int koXpStandard = 20;
        public int koXpElite = 45;
        public int koXpBoss = 90;

        [Header("Completion Bonus by Difficulty")]
        public int completionBonusStandard = 20;
        public int completionBonusElite = 40;
        public int completionBonusBoss = 80;

        [Header("RAM by Difficulty")]
        public int ramStandard = 3;
        public int ramElite = 15;
        public int ramBoss = 50;

        [Header("XP Distribution")]
        [Range(0f, 1f)] public float activeXpShare = 1f;
        [Range(0f, 1f)] public float benchXpShare = 0.5f;
        public bool kodReceivesXp = true;
        public bool capturedReceivesXp = false;

        [Header("Star Thresholds (0->1, 1->2, ..., 4->5)")]
        public int[] starXpThresholds = { 450, 700, 1050, 1600, 2400 };

        [Header("Mastery")]
        public int masteryBaseXp = 3000;

        [Header("Rebirth")]
        public float rebirthXpMultiplierBase = 1.18f;
        public int[] rebirthRamCosts = { 1000, 1500, 2000, 2500, 3000 };

        [Header("Defeat")]
        public bool defeatGrantsRewards = false;

        public int GetKoXp(EncounterDifficulty difficulty)
        {
            return difficulty switch
            {
                EncounterDifficulty.Elite => koXpElite,
                EncounterDifficulty.Boss => koXpBoss,
                _ => koXpStandard,
            };
        }

        public int GetCompletionBonus(EncounterDifficulty difficulty)
        {
            return difficulty switch
            {
                EncounterDifficulty.Elite => completionBonusElite,
                EncounterDifficulty.Boss => completionBonusBoss,
                _ => completionBonusStandard,
            };
        }

        public int GetRamReward(EncounterDifficulty difficulty)
        {
            return difficulty switch
            {
                EncounterDifficulty.Elite => ramElite,
                EncounterDifficulty.Boss => ramBoss,
                _ => ramStandard,
            };
        }

        public int GetRebirthRamCost(int currentRebirthCount)
        {
            if (rebirthRamCosts == null || rebirthRamCosts.Length == 0)
                return 0;

            int index = Mathf.Clamp(currentRebirthCount, 0, rebirthRamCosts.Length - 1);
            return Mathf.Max(0, rebirthRamCosts[index]);
        }

#pragma warning disable CS0618 // Obsolete type kept for serialized leftover refs
        public static BattleRewardConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<BattleRewardConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }
#pragma warning restore CS0618

        private void OnValidate()
        {
            if (starXpThresholds == null || starXpThresholds.Length == 0)
                starXpThresholds = new[] { 450, 700, 1050, 1600, 2400 };
            if (rebirthRamCosts == null || rebirthRamCosts.Length == 0)
                rebirthRamCosts = new[] { 1000, 1500, 2000, 2500, 3000 };
        }
    }
}
