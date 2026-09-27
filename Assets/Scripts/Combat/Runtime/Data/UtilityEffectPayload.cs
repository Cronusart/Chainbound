using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Data-driven utility effect payload used by move assets and runtime instances.
    /// </summary>
    [Serializable]
    public class UtilityEffectPayload
    {
        public UtilityEffectType effectType;
        public float magnitude;
        public int turnBudget;
        public int hitBudget;
        public int apGain;
        public string stackKey;
        public StatusId statusId;
        public int statusDuration;
        public MoveTargeting targetOverride;
        public float chance;
        public float reflectPercent;
        public CombatStageStat stageStat;
        public int stageAmount;
        public int stageAmountMax;
        public int stageCap;
        public int threshold;
        public ArmedWindowTrigger armedTrigger;
        public float healPercentMaxHP;
        public float counterDamagePercentMaxHP;
        public bool affectAllAllies;
        public UtilityContactFilter contactFilter;
    }
}
