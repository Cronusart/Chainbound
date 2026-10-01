using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lever board for what is granted after battle (XP amounts, RAM, distribution shares).
    /// Does NOT own progression thresholds — those live on <see cref="ProgressionConfigSO"/>.
    /// Known gate odds stay on <see cref="ChainlingProgressionRewardTablesSO"/> (referenced, not copied).
    /// </summary>
    [CreateAssetMenu(fileName = "PostBattleRewardsConfig", menuName = "Chainbound/Combat/Config/Post Battle Rewards Config")]
    public class PostBattleRewardsConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int configVersion = 3;

        [Header("KO XP by Difficulty")]
        public int koXpStandard = 20;
        public int koXpElite = 45;
        public int koXpBoss = 0;

        [Header("Completion Bonus by Difficulty")]
        public int completionBonusStandard = 20;
        public int completionBonusElite = 40;
        public int completionBonusBoss = 100;

        [Header("RAM by Difficulty")]
        public int ramStandard = 5;
        public int ramElite = 15;
        public int ramBoss = 25;

        [Header("XP Distribution")]
        [Range(0f, 1f)] public float activeXpShare = 1f;
        [Range(0f, 1f)] public float benchXpShare = 0.5f;
        public bool kodReceivesXp = true;
        public bool capturedReceivesXp = false;

        [Header("Defeat")]
        public bool defeatGrantsRewards = false;
        [Tooltip("RAM removed after a PvE loss. Fleeing and draws are never charged.")]
        [Min(0)] public int defeatRamPenaltyFlat = 5;
        [Tooltip("Reserved for player progression. Keep at zero until player levels are implemented.")]
        [Min(0)] public int defeatRamPenaltyPerPlayerLevel = 0;

        [Tooltip("When enabled, Chainlings that were KO'd in a lost PvE battle are revived before returning to the recovery point.")]
        public bool defeatRevivesKoChainlings = true;
        [Tooltip("Fraction of battle max HP restored to each KO'd Chainling after a lost PvE battle.")]
        [Range(0f, 1f)] public float defeatKoRespawnHpFraction = 0.5f;

        [Header("Known Gate Tables (reference only)")]
        [Tooltip("Referenced for post-battle Known rolls; not owned by this board.")]
        public ChainlingProgressionRewardTablesSO progressionRewardTables;

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

        public int GetDefeatRamPenalty(int playerLevel)
        {
            long penalty = (long)Mathf.Max(0, defeatRamPenaltyFlat)
                + ((long)Mathf.Max(0, playerLevel) * Mathf.Max(0, defeatRamPenaltyPerPlayerLevel));
            return penalty > int.MaxValue ? int.MaxValue : (int)penalty;
        }

        /// <summary>
        /// Resolves a living respawn HP value from the Chainling's battle max HP.
        /// A positive configured fraction always produces at least 1 HP; zero disables recovery.
        /// </summary>
        public int ResolveDefeatKoRespawnHp(int maxHp)
        {
            if (!defeatRevivesKoChainlings || maxHp <= 0)
                return 0;

            float fraction = Mathf.Clamp01(defeatKoRespawnHpFraction);
            if (fraction <= 0f)
                return 0;

            int recoveredHp = Mathf.CeilToInt(maxHp * fraction);
            return Mathf.Clamp(recoveredHp, 1, maxHp);
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashPostBattleRewards(this);

        public static PostBattleRewardsConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<PostBattleRewardsConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 3;
            koXpStandard = 20;
            koXpElite = 45;
            koXpBoss = 0;
            completionBonusStandard = 20;
            completionBonusElite = 40;
            completionBonusBoss = 100;
            ramStandard = 5;
            ramElite = 15;
            ramBoss = 25;
            activeXpShare = 1f;
            benchXpShare = 0.5f;
            kodReceivesXp = true;
            capturedReceivesXp = false;
            defeatGrantsRewards = false;
            defeatRamPenaltyFlat = 5;
            defeatRamPenaltyPerPlayerLevel = 0;
            defeatRevivesKoChainlings = true;
            defeatKoRespawnHpFraction = 0.5f;
            progressionRewardTables = null;
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            koXpStandard = Mathf.Max(0, koXpStandard);
            koXpElite = Mathf.Max(0, koXpElite);
            koXpBoss = Mathf.Max(0, koXpBoss);
            completionBonusStandard = Mathf.Max(0, completionBonusStandard);
            completionBonusElite = Mathf.Max(0, completionBonusElite);
            completionBonusBoss = Mathf.Max(0, completionBonusBoss);
            ramStandard = Mathf.Max(0, ramStandard);
            ramElite = Mathf.Max(0, ramElite);
            ramBoss = Mathf.Max(0, ramBoss);
            defeatRamPenaltyFlat = Mathf.Max(0, defeatRamPenaltyFlat);
            defeatRamPenaltyPerPlayerLevel = Mathf.Max(0, defeatRamPenaltyPerPlayerLevel);
            defeatKoRespawnHpFraction = Mathf.Clamp01(defeatKoRespawnHpFraction);
            activeXpShare = Mathf.Clamp01(activeXpShare);
            benchXpShare = Mathf.Clamp01(benchXpShare);
        }
    }
}
