using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class DamageModifierContext
    {
        public float powerMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float accuracyBonus = 0f;
        public float critChanceBonus = 0f;
        public float critMultiplierBonus = 0f;
        public float additionalMitigation = 0f;
        public float barrierBypassChance = 0f;
        public float barrierPenetrationPercent = 0f;
        public int ignoreEvasionStages = 0;
        public int extraHits = 0;
        public bool expandSingleTargetToAllEnemies;
        public float expandedTargetPowerMultiplier = 1f;
        public bool bounceToOneAdjacent;
        public float bouncePowerMultiplier = 1f;
        public bool inverseAttackScaling;
        public bool hasAtkStatOverride;
        public CoreStat atkStatOverride;
        public bool hasAttackTypeOverride;
        public ChainlingType attackTypeOverride;
        public List<string> appliedModifierIds = new List<string>();
        public List<string> emittedModifierOutcomeIds = new List<string>();

        public void AddApplied(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return;
            if (!appliedModifierIds.Contains(sectionId))
                appliedModifierIds.Add(sectionId);
        }

        public bool TryMarkOutcomeEmitted(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return false;
            if (emittedModifierOutcomeIds.Contains(sectionId))
                return false;
            emittedModifierOutcomeIds.Add(sectionId);
            return true;
        }

        public DamageModifierContext Clone()
        {
            return new DamageModifierContext
            {
                powerMultiplier = powerMultiplier,
                damageMultiplier = damageMultiplier,
                accuracyBonus = accuracyBonus,
                critChanceBonus = critChanceBonus,
                critMultiplierBonus = critMultiplierBonus,
                additionalMitigation = additionalMitigation,
                barrierBypassChance = barrierBypassChance,
                barrierPenetrationPercent = barrierPenetrationPercent,
                ignoreEvasionStages = ignoreEvasionStages,
                extraHits = extraHits,
                expandSingleTargetToAllEnemies = expandSingleTargetToAllEnemies,
                expandedTargetPowerMultiplier = expandedTargetPowerMultiplier,
                bounceToOneAdjacent = bounceToOneAdjacent,
                bouncePowerMultiplier = bouncePowerMultiplier,
                inverseAttackScaling = inverseAttackScaling,
                hasAtkStatOverride = hasAtkStatOverride,
                atkStatOverride = atkStatOverride,
                hasAttackTypeOverride = hasAttackTypeOverride,
                attackTypeOverride = attackTypeOverride,
                appliedModifierIds = new List<string>(appliedModifierIds ?? new List<string>()),
                emittedModifierOutcomeIds = new List<string>(emittedModifierOutcomeIds ?? new List<string>()),
            };
        }
    }
}
