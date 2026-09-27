using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lever board for combat-result formulas: AP, damage, stages, mitigation, status procs.
    /// Selected via <see cref="CombatRulesetSO"/>. One board owns these values — do not duplicate.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatResolverConfig", menuName = "Chainbound/Combat/Config/Combat Resolver Config")]
    public class CombatResolverConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int configVersion = 2;

        [Header("AP")]
        [Min(0)] public int apPerTurn = CombatConstants.AP_PER_TURN;
        [Min(1)] public int apCap = CombatConstants.AP_CAP;

        [Header("Damage")]
        [Range(0f, 2f)] public float randomRollMin = CombatConstants.RANDOM_ROLL_MIN;
        [Range(0f, 2f)] public float randomRollMax = CombatConstants.RANDOM_ROLL_MAX;
        [Min(1f)] public float critMultiplierDefault = CombatConstants.CRIT_MULTIPLIER_DEFAULT;
        [Min(0f)] public float progressionDamageFactor = CombatConstants.PROGRESSION_DAMAGE_FACTOR;
        [Min(0f)] public float chemistryAdvantageFactor = CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR;
        [Min(0f)] public float chemistryDisadvantageFactor = CombatConstants.CHEMISTRY_DISADVANTAGE_FACTOR;

        [Header("STAB")]
        [Tooltip("When false, STAB is not applied (current shipped behavior).")]
        public bool enableStab = false;
        [Min(1f)] public float stabMultiplier = CombatConstants.STAB_MULTIPLIER;

        [Header("Stages")]
        public int stageMin = CombatConstants.STAGE_MIN;
        public int stageMax = CombatConstants.STAGE_MAX;
        [Min(0)] public int stageActiveChangeCap = CombatConstants.STAGE_ACTIVE_CHANGE_CAP;

        [Header("Mitigation / Barriers")]
        [Range(0f, 1f)] public float mitigationSingleSourceCap = CombatConstants.MITIGATION_SINGLE_SOURCE_CAP;
        [Range(0f, 1f)] public float mitigationFloor = CombatConstants.MITIGATION_FLOOR;
        public bool exclusiveBarriersAndWalls = CombatConstants.EXCLUSIVE_BARRIERS_AND_WALLS;
        [Range(0f, 2f)] public float maxPassiveMaxHpBonusPercent = CombatConstants.MAX_PASSIVE_MAX_HP_BONUS_PERCENT;

        [Header("Status Proc Chances")]
        [Range(0f, 1f)] public float rektLoseActionChance = CombatConstants.REKT_LOSE_ACTION_CHANCE;
        [Range(0f, 1f)] public float glitchedMindMisactChance = CombatConstants.GLITCHED_MIND_MISACT_CHANCE;
        [Tooltip("Unused. Slowed no longer rolls an action skip.")]
        [Range(0f, 1f)] public float slowedLoseActionChance = CombatConstants.SLOWED_LOSE_ACTION_CHANCE;

        [Header("Chemistry")]
        [Range(0f, 1f)] public float firewallProjectileMitigation = CombatConstants.FIREWALL_PROJECTILE_MITIGATION;
        public int slowedSpeedStages = CombatConstants.SLOWED_SPEED_STAGES;
        public int shatteredDefenseStages = CombatConstants.SHATTERED_DEFENSE_STAGES;
        [Range(0f, 1f)] public float shatteredCurrentHpDamage = CombatConstants.SHATTERED_CURRENT_HP_DAMAGE;
        [Range(0f, 1f)] public float dripHealPercentMaxHp = CombatConstants.DRIP_HEAL_PERCENT_MAX_HP;
        [Range(0f, 1f)] public float cascadeFailureMaxHpDamage = CombatConstants.CASCADE_FAILURE_MAX_HP_DAMAGE;
        [Range(0f, 1f)] public float splinterDamageMultiplier = CombatConstants.SPLINTER_DAMAGE_MULTIPLIER;
        [Min(0f)] public float splinterSingleTargetDamageMultiplier = CombatConstants.SPLINTER_SINGLE_TARGET_DAMAGE_MULTIPLIER;
        public int ruggedTickCap = CombatConstants.RUGGED_TICK_CAP;
        [Range(0f, 1f)] public float fomoSelfDotPercent = CombatConstants.FOMO_SELF_DOT_HP;

        [Header("Turn Timer")]
        [Tooltip("Seconds for player action selection. 0 = disabled (no in-battle timer today).")]
        [Min(0f)] public float turnActionTimeLimitSeconds = 0f;

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashResolverConfig(this);

        public static CombatResolverConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<CombatResolverConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 2;
            apPerTurn = CombatConstants.AP_PER_TURN;
            apCap = CombatConstants.AP_CAP;
            randomRollMin = CombatConstants.RANDOM_ROLL_MIN;
            randomRollMax = CombatConstants.RANDOM_ROLL_MAX;
            critMultiplierDefault = CombatConstants.CRIT_MULTIPLIER_DEFAULT;
            progressionDamageFactor = CombatConstants.PROGRESSION_DAMAGE_FACTOR;
            chemistryAdvantageFactor = CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR;
            chemistryDisadvantageFactor = CombatConstants.CHEMISTRY_DISADVANTAGE_FACTOR;
            enableStab = false;
            stabMultiplier = CombatConstants.STAB_MULTIPLIER;
            stageMin = CombatConstants.STAGE_MIN;
            stageMax = CombatConstants.STAGE_MAX;
            stageActiveChangeCap = CombatConstants.STAGE_ACTIVE_CHANGE_CAP;
            mitigationSingleSourceCap = CombatConstants.MITIGATION_SINGLE_SOURCE_CAP;
            mitigationFloor = CombatConstants.MITIGATION_FLOOR;
            exclusiveBarriersAndWalls = CombatConstants.EXCLUSIVE_BARRIERS_AND_WALLS;
            maxPassiveMaxHpBonusPercent = CombatConstants.MAX_PASSIVE_MAX_HP_BONUS_PERCENT;
            rektLoseActionChance = CombatConstants.REKT_LOSE_ACTION_CHANCE;
            glitchedMindMisactChance = CombatConstants.GLITCHED_MIND_MISACT_CHANCE;
            slowedLoseActionChance = CombatConstants.SLOWED_LOSE_ACTION_CHANCE;
            firewallProjectileMitigation = CombatConstants.FIREWALL_PROJECTILE_MITIGATION;
            slowedSpeedStages = CombatConstants.SLOWED_SPEED_STAGES;
            shatteredDefenseStages = CombatConstants.SHATTERED_DEFENSE_STAGES;
            shatteredCurrentHpDamage = CombatConstants.SHATTERED_CURRENT_HP_DAMAGE;
            dripHealPercentMaxHp = CombatConstants.DRIP_HEAL_PERCENT_MAX_HP;
            cascadeFailureMaxHpDamage = CombatConstants.CASCADE_FAILURE_MAX_HP_DAMAGE;
            splinterDamageMultiplier = CombatConstants.SPLINTER_DAMAGE_MULTIPLIER;
            splinterSingleTargetDamageMultiplier = CombatConstants.SPLINTER_SINGLE_TARGET_DAMAGE_MULTIPLIER;
            ruggedTickCap = CombatConstants.RUGGED_TICK_CAP;
            fomoSelfDotPercent = CombatConstants.FOMO_SELF_DOT_HP;
            turnActionTimeLimitSeconds = 0f;
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            apPerTurn = Mathf.Max(0, apPerTurn);
            apCap = Mathf.Max(1, apCap);
            if (randomRollMax < randomRollMin)
                randomRollMax = randomRollMin;
            critMultiplierDefault = Mathf.Max(1f, critMultiplierDefault);
            progressionDamageFactor = Mathf.Max(0f, progressionDamageFactor);
            chemistryAdvantageFactor = Mathf.Max(0f, chemistryAdvantageFactor);
            chemistryDisadvantageFactor = Mathf.Max(0f, chemistryDisadvantageFactor);
            stabMultiplier = Mathf.Max(1f, stabMultiplier);
            if (stageMax < stageMin)
                stageMax = stageMin;
            stageActiveChangeCap = Mathf.Max(0, stageActiveChangeCap);
            mitigationSingleSourceCap = Mathf.Clamp01(mitigationSingleSourceCap);
            mitigationFloor = Mathf.Clamp01(mitigationFloor);
            maxPassiveMaxHpBonusPercent = Mathf.Max(0f, maxPassiveMaxHpBonusPercent);
            rektLoseActionChance = Mathf.Clamp01(rektLoseActionChance);
            glitchedMindMisactChance = Mathf.Clamp01(glitchedMindMisactChance);
            slowedLoseActionChance = Mathf.Clamp01(slowedLoseActionChance);
            firewallProjectileMitigation = Mathf.Clamp01(firewallProjectileMitigation);
            slowedSpeedStages = Mathf.Clamp(slowedSpeedStages, -stageActiveChangeCap, stageActiveChangeCap);
            shatteredDefenseStages = Mathf.Clamp(shatteredDefenseStages, -stageActiveChangeCap, stageActiveChangeCap);
            shatteredCurrentHpDamage = Mathf.Clamp01(shatteredCurrentHpDamage);
            dripHealPercentMaxHp = Mathf.Clamp01(dripHealPercentMaxHp);
            cascadeFailureMaxHpDamage = Mathf.Clamp01(cascadeFailureMaxHpDamage);
            splinterDamageMultiplier = Mathf.Clamp01(splinterDamageMultiplier);
            splinterSingleTargetDamageMultiplier = Mathf.Max(0f, splinterSingleTargetDamageMultiplier);
            ruggedTickCap = Mathf.Clamp(ruggedTickCap, 1, 15);
            fomoSelfDotPercent = Mathf.Clamp01(fomoSelfDotPercent);
            turnActionTimeLimitSeconds = Mathf.Max(0f, turnActionTimeLimitSeconds);
        }
    }
}
