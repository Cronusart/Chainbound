using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Factory methods for creating combat entities from ScriptableObject definitions.
    /// Bridges the data-authoring layer (SOs) with the runtime layer (instances).
    /// </summary>
    public static class CombatFactory
    {
        /// <summary>
        /// Create a ChainlingInstance from a species definition.
        /// </summary>
        public static ChainlingInstance CreateChainling(
            SpeciesDefinitionSO species,
            int stars,
            int rebirthCount,
            int evolutionStage,
            List<MoveDefinitionSO> equippedMoves,
            int[] ivOverride = null,
            CombatRng rng = null,
            int[] rebirthIvBonuses = null)
        {
            var instance = new ChainlingInstance
            {
                speciesId = species.speciesId,
                displayName = species.displayName,
                primaryType = species.primaryType,
                secondaryType = species.secondaryType,
                stars = ChainlingProgressionUtility.NormalizeStars(stars),
                rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount),
                evolutionStage = evolutionStage,
                rebirthIvBonuses = ChainlingProgressionUtility.NormalizeRebirthIvBonuses(rebirthIvBonuses),

                baseMaxHP = species.baseMaxHP,
                baseMeleeAtk = species.baseMeleeAtk,
                baseMeleeDef = species.baseMeleeDef,
                baseRangedAtk = species.baseRangedAtk,
                baseRangedDef = species.baseRangedDef,
                baseSpeed = species.baseSpeed,

                maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount),
                hasCaptureProfile = true,
                captureProfile = species.defaultCaptureProfile,
            };

            if (ivOverride != null && ivOverride.Length >= 6)
            {
                ApplyIvs(instance, ivOverride);
            }
            else if (rng != null)
            {
                // Debug fallback for legacy/test callers that did not supply encounter IVs.
                instance.ivMaxHP = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                instance.ivMeleeAtk = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                instance.ivMeleeDef = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                instance.ivRangedAtk = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                instance.ivRangedDef = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                instance.ivSpeed = rng.NextInt(CombatConstants.IV_MIN, CombatConstants.IV_MAX);
            }

            // Set HP to max
            instance.currentHP = instance.EffectiveMaxHP;
            instance.displayHP = instance.currentHP;
            instance.displayAP = instance.currentAP;

            // Equip Stamped moves
            if (equippedMoves != null)
            {
                int slotCount = Math.Min(equippedMoves.Count, instance.maxSlots);
                for (int i = 0; i < slotCount; i++)
                {
                    instance.moves.Add(CreateMove(equippedMoves[i]));
                }
            }

            return instance;
        }

        private static void ApplyIvs(ChainlingInstance instance, int[] ivs)
        {
            instance.ivMaxHP = ivs[0];
            instance.ivMeleeAtk = ivs[1];
            instance.ivMeleeDef = ivs[2];
            instance.ivRangedAtk = ivs[3];
            instance.ivRangedDef = ivs[4];
            instance.ivSpeed = ivs[5];
        }

        /// <summary>
        /// Create an MoveInstance from a definition.
        /// </summary>
        public static MoveInstance CreateMove(MoveDefinitionSO def)
        {
            var instance = new MoveInstance
            {
                definitionId = def.moveId,
                displayName = def.displayName,
                category = def.category,
                baseType = def.baseType,
                targeting = def.targeting,
                contactType = def.contactType,
                basePower = def.power,
                apCost = def.apCost,
                baseAccuracy = def.accuracy,
                hitCount = def.hitCountMin,
                hitCountMax = def.hitCountMax,
                rarity = def.rarity,
                rechargeTurns = def.rechargeTurns,
                selectionPolicy = def.selectionPolicy,
                activeConditionType = def.activeConditionType,
                activeConditionThresholdPercent = def.activeConditionThresholdPercent,
                resolveProfile = MoveResolveProfileUtility.CloneOrDefault(def.resolveProfile),
            };

            // Copy status effects
            for (int i = 0; i < def.statusEffects.Count; i++)
            {
                var eff = def.statusEffects[i];
                instance.statusEffects.Add(new MoveStatusEffect
                {
                    statusId = eff.statusId,
                    duration = eff.duration,
                    chance = eff.chance,
                    targetOverride = eff.overrideTargeting ? eff.targetOverride : def.targeting,
                    parameterType = eff.parameterType,
                });
            }

            // Copy stage changes
            for (int i = 0; i < def.stageChanges.Count; i++)
            {
                var sc = def.stageChanges[i];
                instance.stageChanges.Add(new MoveStageChange
                {
                    stat = sc.stat,
                    stages = sc.stages,
                    chance = sc.chance,
                    targetsSelf = sc.targetsSelf,
                });
            }

            // Copy tags
            instance.tags.AddRange(def.tags);

            // Copy typed utility effects
            if (def.utilityEffects != null)
            {
                for (int i = 0; i < def.utilityEffects.Count; i++)
                {
                    UtilityEffectPayload effect = def.utilityEffects[i];
                    if (effect == null)
                        continue;

                    instance.utilityEffects.Add(new UtilityEffectPayload
                    {
                        effectType = effect.effectType,
                        magnitude = effect.magnitude,
                        turnBudget = effect.turnBudget,
                        hitBudget = effect.hitBudget,
                        apGain = effect.apGain,
                        stackKey = effect.stackKey,
                        statusId = effect.statusId,
                        statusDuration = effect.statusDuration,
                        targetOverride = effect.targetOverride,
                        chance = effect.chance,
                        reflectPercent = effect.reflectPercent,
                        stageStat = effect.stageStat,
                        stageAmount = effect.stageAmount,
                        stageAmountMax = effect.stageAmountMax,
                        stageCap = effect.stageCap,
                        threshold = effect.threshold,
                        armedTrigger = effect.armedTrigger,
                        healPercentMaxHP = effect.healPercentMaxHP,
                        counterDamagePercentMaxHP = effect.counterDamagePercentMaxHP,
                        affectAllAllies = effect.affectAllAllies,
                        contactFilter = effect.contactFilter,
                    });
                }
            }

            // Copy passive trigger fields
            instance.triggerCondition = def.triggerCondition;
            instance.hpThresholdPercent = def.hpThresholdPercent;
            instance.triggerOncePerBattle = def.triggerOncePerBattle;
            instance.healPercentMaxHP = def.healPercentMaxHP;
            instance.hpCostPercent = def.hpCostPercent;

            return instance;
        }

        /// <summary>
        /// Create a quick test Chainling without ScriptableObjects.
        /// Useful for the test harness when SO assets haven't been created yet.
        /// </summary>
        public static ChainlingInstance CreateTestChainling(
            string name,
            ChainlingType type,
            int stars = 0,
            int rebirthCount = 0,
            int hp = 100, int mAtk = 60, int mDef = 50,
            int rAtk = 60, int rDef = 50, int spd = 60)
        {
            var instance = new ChainlingInstance
            {
                speciesId = $"test_{name.ToLower()}",
                displayName = name,
                primaryType = type,
                secondaryType = ChainlingType.None,
                stars = ChainlingProgressionUtility.NormalizeStars(stars),
                rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount),
                evolutionStage = 1,
                baseMaxHP = hp,
                baseMeleeAtk = mAtk,
                baseMeleeDef = mDef,
                baseRangedAtk = rAtk,
                baseRangedDef = rDef,
                baseSpeed = spd,
                maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount),
                rebirthIvBonuses = ChainlingProgressionUtility.NormalizeRebirthIvBonuses(null),
            };
            instance.currentHP = instance.EffectiveMaxHP;
            instance.displayHP = instance.currentHP;
            instance.displayAP = instance.currentAP;
            return instance;
        }

        /// <summary>
        /// Create a quick test move without ScriptableObjects.
        /// </summary>
        public static MoveInstance CreateTestMove(
            string name,
            MoveCategory category,
            ChainlingType type,
            int power = 60,
            int apCost = 2,
            int accuracy = 95,
            int hitCount = 1)
        {
            return new MoveInstance
            {
                definitionId = $"test_{name.ToLower().Replace(' ', '_')}",
                displayName = name,
                category = category,
                baseType = type,
                targeting = MoveTargeting.SingleEnemy,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.Projectile,
                basePower = power,
                apCost = apCost,
                baseAccuracy = accuracy,
                hitCount = hitCount,
                hitCountMax = hitCount,
                rarity = RarityTier.Common,
            };
        }
    }
}
