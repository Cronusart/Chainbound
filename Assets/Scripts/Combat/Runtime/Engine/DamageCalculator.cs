using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Damage calculation following COMBAT_02C_ResolutionMath_v1.2.
    /// Pure functions except consumed next-move status markers, matching legacy behavior.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// Perform the hit check per 02C section 2.
        /// HitChance = (AttackerAcc / DefenderEva) * MoveAccuracy / 100
        /// </summary>
        public static bool CheckHit(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            CombatRng rng)
        {
            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveAccuracyTuning tuning = profile.accuracy ?? new MoveAccuracyTuning();
            if (tuning.forceMiss)
                return false;
            if (tuning.forceHit || move.baseAccuracy <= 0)
                return true;

            float accMult;
            float evaMult;
            float hitChance = ComputeHitChance(attacker, defender, move, out accMult, out evaMult);
            return rng.Check(hitChance);
        }

        /// <summary>
        /// Calculate base damage per 02C section 3.
        /// BaseDamage = Power * (Atk / Def) * (Level + 37) / 100 * Random(0.85-1.0) + 1
        /// </summary>
        public static int CalculateDamage(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            ChemistryResult chemistry,
            bool isCrit,
            CombatRng rng)
        {
            return CalculateDamageDetailed(attacker, defender, move, chemistry, isCrit, rng, out _);
        }

        public static int CalculateDamageDetailed(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            ChemistryResult chemistry,
            bool isCrit,
            CombatRng rng,
            out DamageComputationTrace trace)
        {
            return CalculateDamageDetailed(attacker, defender, move, chemistry, isCrit, rng, null, null, out trace);
        }

        public static int CalculateDamageDetailed(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            ChemistryResult chemistry,
            bool isCrit,
            CombatRng rng,
            DamageModifierContext modifierContext,
            out DamageComputationTrace trace)
        {
            return CalculateDamageDetailed(attacker, defender, move, chemistry, isCrit, rng, modifierContext, null, out trace);
        }

        public static int CalculateDamageDetailed(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            ChemistryResult chemistry,
            bool isCrit,
            CombatRng rng,
            DamageModifierContext modifierContext,
            CombatResolverConfigSO config,
            out DamageComputationTrace trace)
        {
            CombatResolverConfigSO resolvedConfig = config != null
                ? config
                : BalanceConfigRuntime.ResolverConfig;

            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveDamageFormulaTuning damageTuning = profile.damage ?? new MoveDamageFormulaTuning();

            int adjustedPower = Math.Max(0, move.basePower + damageTuning.flatPowerBonus);
            float powerMultiplier = Math.Max(0f, damageTuning.powerMultiplier);
            if (modifierContext != null)
                powerMultiplier *= Math.Max(0f, modifierContext.powerMultiplier);

            int power = Math.Max(0, (int)Math.Round(adjustedPower * powerMultiplier));
            if (power <= 0)
            {
                trace = default;
                return 0;
            }

            ResolveDamageStats(move, modifierContext, out CoreStat atkStat, out CoreStat defStat);

            for (int m = 0; move.modifiers != null && m < move.modifiers.Count; m++)
            {
                if (move.modifiers[m].category == ModifierCategory.TRX &&
                    move.modifiers[m].scope == ModifierScope.Active)
                {
                    // TODO: Implement stat substitution per Spec 05 TRX rules.
                }
            }

            float atk = attacker.GetEffectiveStat(atkStat);
            float def = defender.GetEffectiveStat(defStat);
            if (def < 1f) def = 1f;

            float randomMin = resolvedConfig.randomRollMin;
            float randomMax = resolvedConfig.randomRollMax;
            if (damageTuning.useCustomRandomRollRange)
            {
                randomMin = damageTuning.randomRollMin;
                randomMax = damageTuning.randomRollMax;
                if (randomMax < randomMin)
                {
                    float temp = randomMin;
                    randomMin = randomMax;
                    randomMax = temp;
                }
            }

            float randomRoll = rng.NextFloat(randomMin, randomMax);
            float levelFactor = ResolveProgressionDamageFactor(damageTuning, resolvedConfig);
            float baseDamage = power * (atk / def) * levelFactor * randomRoll + damageTuning.additiveDamageConstant;

            float stabMult = ResolveStabMultiplier(attacker, move, modifierContext, resolvedConfig);
            baseDamage *= stabMult;

            float chemMult = chemistry.DamageMultiplier;
            baseDamage *= chemMult;

            float critMult = 1f;
            if (isCrit)
            {
                critMult = ResolveCritDamageMultiplier(profile, modifierContext, resolvedConfig);
                baseDamage *= critMult;
            }

            float tuningMult = 1f;
            if (Math.Abs(damageTuning.preMitigationDamageMultiplier - 1f) > 0.0001f)
            {
                float preMultiplier = Math.Max(0f, damageTuning.preMitigationDamageMultiplier);
                tuningMult *= preMultiplier;
                baseDamage *= preMultiplier;
            }

            if (damageTuning.preMitigationFlatBonus != 0)
                baseDamage += damageTuning.preMitigationFlatBonus;

            if (damageTuning.consumeNextMoveDamageStatuses)
            {
                ApplyConsumedStatusDamageMultiplier(attacker, StatusId.Staggered, damageTuning.staggeredDamageMultiplier, ref baseDamage, ref tuningMult);
                ApplyConsumedStatusDamageMultiplier(attacker, StatusId.Surging, damageTuning.surgingDamageMultiplier, ref baseDamage, ref tuningMult);
                ApplyConsumedStatusDamageMultiplier(attacker, StatusId.Amplified, damageTuning.amplifiedDamageMultiplier, ref baseDamage, ref tuningMult);
            }

            for (int s = 0; defender.statuses != null && s < defender.statuses.Count; s++)
            {
                if (defender.statuses[s].statusId == StatusId.ExploitMark)
                {
                    float exploitMultiplier = Math.Max(0f, damageTuning.exploitMarkDamageMultiplier);
                    tuningMult *= exploitMultiplier;
                    baseDamage *= exploitMultiplier;
                    break;
                }
            }

            if (modifierContext != null && Math.Abs(modifierContext.damageMultiplier - 1f) > 0.0001f)
            {
                float contextMultiplier = Math.Max(0f, modifierContext.damageMultiplier);
                tuningMult *= contextMultiplier;
                baseDamage *= contextMultiplier;
            }

            float mitigation = damageTuning.ignoreMitigation ? 0f : CalculateMitigation(defender, move.contactType, resolvedConfig);
            if (!damageTuning.ignoreMitigation && modifierContext != null && modifierContext.additionalMitigation > 0f)
            {
                float extra = Math.Max(0f, Math.Min(resolvedConfig.mitigationSingleSourceCap, modifierContext.additionalMitigation));
                mitigation = 1f - ((1f - mitigation) * (1f - extra));
            }

            int preMitigationDamage = Math.Max(0, (int)Math.Floor(baseDamage));
            baseDamage *= 1f - mitigation;

            float mitigationFloor = damageTuning.overrideMitigationFloor
                ? MoveResolveProfileUtility.Clamp01Flexible(damageTuning.mitigationFloorOverride)
                : resolvedConfig.mitigationFloor;
            if (mitigationFloor > 0f)
            {
                float minDamage = power * (atk / def) * levelFactor * mitigationFloor;
                baseDamage = Math.Max(baseDamage, minDamage);
            }

            if (Math.Abs(damageTuning.finalDamageMultiplier - 1f) > 0.0001f)
            {
                float finalMultiplier = Math.Max(0f, damageTuning.finalDamageMultiplier);
                tuningMult *= finalMultiplier;
                baseDamage *= finalMultiplier;
            }

            if (damageTuning.finalDamageFlatBonus != 0)
                baseDamage += damageTuning.finalDamageFlatBonus;

            int finalDamage = Math.Max(Math.Max(0, damageTuning.minimumFinalDamage), (int)Math.Floor(baseDamage));
            trace = new DamageComputationTrace
            {
                atkStatUsed = atkStat,
                defStatUsed = defStat,
                attackerStatFinal = atk,
                defenderStatFinal = def,
                atkDefRatio = atk / def,
                basePower = power,
                levelFactor = levelFactor,
                randomFactor = randomRoll,
                stab = stabMult,
                chemistry = chemMult,
                crit = critMult,
                mitigation = mitigation,
                mitigationFloor = mitigationFloor,
                tuningMultiplier = tuningMult,
                preMitigationDamage = preMitigationDamage,
                finalDamage = finalDamage,
            };
            return finalDamage;
        }

        public static void ResolveDamageStats(MoveInstance move, out CoreStat atkStat, out CoreStat defStat)
        {
            bool isMelee = move.category == MoveCategory.DamagingMelee;
            atkStat = isMelee ? CoreStat.MeleeAtk : CoreStat.RangedAtk;
            defStat = isMelee ? CoreStat.MeleeDef : CoreStat.RangedDef;

            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveDamageFormulaTuning tuning = profile.damage ?? new MoveDamageFormulaTuning();
            if (tuning.overrideAttackStat)
                atkStat = tuning.attackStatOverride;
            if (tuning.overrideDefenseStat)
                defStat = tuning.defenseStatOverride;
        }

        public static void ResolveDamageStats(
            MoveInstance move,
            DamageModifierContext modifierContext,
            out CoreStat atkStat,
            out CoreStat defStat)
        {
            ResolveDamageStats(move, out atkStat, out defStat);
            if (modifierContext != null && modifierContext.hasAtkStatOverride)
                atkStat = modifierContext.atkStatOverride;
        }

        /// <summary>
        /// Check if this hit crits per 02C section 4.
        /// </summary>
        public static bool CheckCrit(ChainlingInstance attacker, MoveInstance move, CombatRng rng)
        {
            float critRate = ComputeCritRate(attacker, move);
            return rng.Check(critRate);
        }

        public static float ComputeHitChance(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            out float attackerAccuracyMultiplier,
            out float defenderEvasionMultiplier)
        {
            return ComputeHitChance(attacker, defender, move, null, out attackerAccuracyMultiplier, out defenderEvasionMultiplier);
        }

        public static float ComputeHitChance(
            ChainlingInstance attacker,
            ChainlingInstance defender,
            MoveInstance move,
            DamageModifierContext modifierContext,
            out float attackerAccuracyMultiplier,
            out float defenderEvasionMultiplier)
        {
            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveAccuracyTuning tuning = profile.accuracy ?? new MoveAccuracyTuning();

            float stageAccuracyMultiplier = attacker != null
                ? ChainlingInstance.StageMultiplier(attacker.stageAccuracy)
                : 1f;
            float temporaryAccuracyMultiplier = attacker != null ? attacker.GetAccuracySourceMultiplier() : 1f;
            attackerAccuracyMultiplier = stageAccuracyMultiplier * temporaryAccuracyMultiplier;

            if (modifierContext != null && Math.Abs(modifierContext.accuracyBonus) > 0.0001f)
                attackerAccuracyMultiplier *= Math.Max(0f, 1f + modifierContext.accuracyBonus);

            if (Math.Abs(tuning.accuracyBonusPercent) > 0.0001f)
                attackerAccuracyMultiplier *= Math.Max(0f, 1f + MoveResolveProfileUtility.PercentOrFraction(tuning.accuracyBonusPercent));
            attackerAccuracyMultiplier *= Math.Max(0f, tuning.accuracyMultiplier);

            int evasionStage = defender != null ? defender.stageEvasion : 0;
            int ignoredStages = tuning.ignoreEvasionStages;
            if (modifierContext != null)
                ignoredStages = Math.Max(ignoredStages, modifierContext.ignoreEvasionStages);
            if (ignoredStages > 0 && evasionStage > 0)
                evasionStage = Math.Max(0, evasionStage - ignoredStages);
            defenderEvasionMultiplier = defender != null ? ChainlingInstance.StageMultiplier(evasionStage) : 1f;

            if (tuning.forceMiss)
                return 0f;
            if (tuning.forceHit)
                return 1f;
            if (move.baseAccuracy <= 0)
                return 1f;

            float hitChance = (attackerAccuracyMultiplier / defenderEvasionMultiplier) * (move.baseAccuracy / 100f);
            return ClampChance(hitChance, tuning.minHitChance, tuning.maxHitChance);
        }

        public static float ComputeCritRate(ChainlingInstance attacker, MoveInstance move)
        {
            return ComputeCritRate(attacker, move, null);
        }

        public static float ComputeCritRate(ChainlingInstance attacker, MoveInstance move, DamageModifierContext modifierContext)
        {
            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveCritTuning tuning = profile.crit ?? new MoveCritTuning();

            if (tuning.suppressCrits)
                return 0f;
            if (tuning.forceCrit)
                return 1f;

            float critRate = tuning.baseCritChanceOverride > 0f
                ? MoveResolveProfileUtility.Clamp01Flexible(tuning.baseCritChanceOverride)
                : 0.0625f;

            float stageBonus = MoveResolveProfileUtility.PercentOrFraction(tuning.critChancePerStagePercent);
            critRate += attacker.stageCritRate * stageBonus;

            for (int m = 0; move.modifiers != null && m < move.modifiers.Count; m++)
            {
                if (move.modifiers[m].category == ModifierCategory.POT)
                {
                    // TODO: Check specific crit modifier section IDs.
                }
            }

            if (modifierContext != null)
                critRate += Math.Max(0f, modifierContext.critChanceBonus);
            if (Math.Abs(tuning.critChanceBonusPercent) > 0.0001f)
                critRate += MoveResolveProfileUtility.PercentOrFraction(tuning.critChanceBonusPercent);
            critRate *= Math.Max(0f, tuning.critChanceMultiplier);

            return ClampChance(critRate, tuning.minCritChance, tuning.maxCritChance);
        }

        private static float ResolveCritDamageMultiplier(
            MoveResolveProfile profile,
            DamageModifierContext modifierContext,
            CombatResolverConfigSO config)
        {
            MoveCritTuning tuning = profile != null && profile.crit != null
                ? profile.crit
                : new MoveCritTuning();

            float defaultCrit = config != null
                ? config.critMultiplierDefault
                : CombatConstants.CRIT_MULTIPLIER_DEFAULT;
            float multiplier = tuning.critDamageMultiplierOverride > 0f
                ? tuning.critDamageMultiplierOverride
                : defaultCrit;
            multiplier += Math.Max(0f, tuning.critDamageMultiplierBonus);
            if (modifierContext != null)
                multiplier += Math.Max(0f, modifierContext.critMultiplierBonus);
            return Math.Max(0f, multiplier);
        }

        private static float ResolveStabMultiplier(
            ChainlingInstance attacker,
            MoveInstance move,
            DamageModifierContext modifierContext,
            CombatResolverConfigSO config)
        {
            if (config == null || !config.enableStab || attacker == null || move == null)
                return 1f;

            ChainlingType attackType = ResolveAttackType(attacker, move, modifierContext);
            if (attackType == ChainlingType.None)
                return 1f;

            if (attackType == attacker.primaryType ||
                (attacker.secondaryType != ChainlingType.None && attackType == attacker.secondaryType))
            {
                return Math.Max(1f, config.stabMultiplier);
            }

            return 1f;
        }

        private static void ApplyConsumedStatusDamageMultiplier(
            ChainlingInstance attacker,
            StatusId statusId,
            float multiplier,
            ref float baseDamage,
            ref float tuningMultiplier)
        {
            if (attacker == null || attacker.statuses == null)
                return;

            for (int s = 0; s < attacker.statuses.Count; s++)
            {
                StatusInstance status = attacker.statuses[s];
                if (status == null || status.statusId != statusId || status.isConsumed)
                    continue;

                float safeMultiplier = Math.Max(0f, multiplier);
                tuningMultiplier *= safeMultiplier;
                baseDamage *= safeMultiplier;
                status.Consume();
                break;
            }
        }

        private static float ClampChance(float value, float minValue, float maxValue)
        {
            float min = MoveResolveProfileUtility.Clamp01Flexible(minValue);
            float max = MoveResolveProfileUtility.Clamp01Flexible(maxValue);
            if (max < min)
            {
                float temp = min;
                min = max;
                max = temp;
            }

            return Math.Min(max, Math.Max(min, value));
        }

        /// <summary>
        /// Calculate total mitigation from defender's passive resistances.
        /// Multiplicative stacking per 02C section 8.1.
        /// </summary>
        private static float CalculateMitigation(ChainlingInstance defender, ContactType contactType, CombatResolverConfigSO config)
        {
            if (defender == null)
                return 0f;

            float sourceCap = config != null
                ? config.mitigationSingleSourceCap
                : CombatConstants.MITIGATION_SINGLE_SOURCE_CAP;
            float surviving = 1f;

            if (defender.mitigationSources != null)
            {
                for (int i = 0; i < defender.mitigationSources.Count; i++)
                {
                    MitigationSource source = defender.mitigationSources[i];
                    if (source == null || source.turnsRemaining <= 0 || source.hitsRemaining == 0 ||
                        !AppliesToContact(source, contactType))
                        continue;

                    float capped = Math.Max(0f, Math.Min(sourceCap, source.mitigation));
                    surviving *= 1f - capped;
                }
            }

            return 1f - surviving;
        }

        private static bool AppliesToContact(MitigationSource source, ContactType contactType)
        {
            if (source == null || source.contactFilter == UtilityContactFilter.Any)
                return true;
            if (source.contactFilter == UtilityContactFilter.Contact)
                return contactType == ContactType.Contact;
            if (source.contactFilter == UtilityContactFilter.Projectile)
                return contactType == ContactType.Projectile;
            return contactType == ContactType.None;
        }

        public static float ResolveProgressionDamageFactor(MoveDamageFormulaTuning tuning)
        {
            return ResolveProgressionDamageFactor(tuning, null);
        }

        public static float ResolveProgressionDamageFactor(
            MoveDamageFormulaTuning tuning,
            CombatResolverConfigSO config)
        {
            if (tuning != null && tuning.useCustomProgressionDamageFactor)
                return Math.Max(0f, tuning.customProgressionDamageFactor);

            if (config != null)
                return Math.Max(0f, config.progressionDamageFactor);

            return Math.Max(0f, BalanceConfigRuntime.ResolverConfig.progressionDamageFactor);
        }

        /// <summary>
        /// Resolve the attack type for a move.
        /// Typed moves use their base type.
        /// Typeless damaging moves inherit user's type(s) at runtime (03A section 4.2).
        /// </summary>
        public static ChainlingType ResolveAttackType(ChainlingInstance attacker, MoveInstance move)
        {
            return ResolveAttackType(attacker, move, null);
        }

        public static ChainlingType ResolveAttackType(
            ChainlingInstance attacker,
            MoveInstance move,
            DamageModifierContext modifierContext)
        {
            if (modifierContext != null && modifierContext.hasAttackTypeOverride)
                return modifierContext.attackTypeOverride;

            if (move.baseType != ChainlingType.None)
                return move.baseType;

            return attacker.primaryType;
        }
    }
}
