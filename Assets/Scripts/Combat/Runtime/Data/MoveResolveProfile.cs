using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Move-authored resolve-time knobs. Defaults preserve the legacy combat formula.
    /// </summary>
    [Serializable]
    public sealed class MoveResolveProfile
    {
        [Tooltip("Primary timing bucket for the move's authored effect contract.")]
        public MoveEffectTimingBucket primaryTiming = MoveEffectTimingBucket.ResolveTimePreHit;

        [Tooltip("When utility payloads on damaging moves execute. Support/utility moves always use the utility resolver.")]
        public MoveUtilityExecutionTiming utilityExecutionTiming = MoveUtilityExecutionTiming.UtilityOnly;

        [Tooltip("How legacy active conditions and resolve-time predicates combine for conditional effects.")]
        public MoveConditionalEffectPolicy conditionalEffectPolicy = MoveConditionalEffectPolicy.LegacyAndResolveConditions;

        [Tooltip("Resolve-time predicates evaluated against live combat state when the action resolves.")]
        public List<MoveResolveCondition> resolveConditions = new List<MoveResolveCondition>();

        [Tooltip("If true, every resolve condition must pass. If false, any passing condition is enough.")]
        public bool requireAllResolveConditions = true;

        [Tooltip("If true, move statuses may apply when a damaging hit misses.")]
        public bool applyStatusEffectsOnMiss;

        [Tooltip("If true, move stage changes may apply when a damaging hit misses.")]
        public bool applyStageChangesOnMiss;

        [Tooltip("If true, heal tags/percent healing may resolve from damaging moves after confirmed damage.")]
        public bool applyHealingOnDamagingMoves;

        public MoveHitCountTuning hitCount = new MoveHitCountTuning();
        public MoveAccuracyTuning accuracy = new MoveAccuracyTuning();
        public MoveCritTuning crit = new MoveCritTuning();
        public MoveDamageFormulaTuning damage = new MoveDamageFormulaTuning();
        public MoveConditionalDamageTuning conditionalDamage = new MoveConditionalDamageTuning();

        public MoveResolveProfile Clone()
        {
            var clone = new MoveResolveProfile
            {
                primaryTiming = primaryTiming,
                utilityExecutionTiming = utilityExecutionTiming,
                conditionalEffectPolicy = conditionalEffectPolicy,
                requireAllResolveConditions = requireAllResolveConditions,
                applyStatusEffectsOnMiss = applyStatusEffectsOnMiss,
                applyStageChangesOnMiss = applyStageChangesOnMiss,
                applyHealingOnDamagingMoves = applyHealingOnDamagingMoves,
                hitCount = hitCount != null ? hitCount.Clone() : new MoveHitCountTuning(),
                accuracy = accuracy != null ? accuracy.Clone() : new MoveAccuracyTuning(),
                crit = crit != null ? crit.Clone() : new MoveCritTuning(),
                damage = damage != null ? damage.Clone() : new MoveDamageFormulaTuning(),
                conditionalDamage = conditionalDamage != null ? conditionalDamage.Clone() : new MoveConditionalDamageTuning(),
                resolveConditions = new List<MoveResolveCondition>(),
            };

            if (resolveConditions != null)
            {
                for (int i = 0; i < resolveConditions.Count; i++)
                    clone.resolveConditions.Add(resolveConditions[i]);
            }

            return clone;
        }
    }

    /// <summary>
    /// Optional resolve-time damage branch. Fixed predicates, count scaling, and seeded
    /// discrete random multipliers share one data contract so content never needs move-id code.
    /// </summary>
    [Serializable]
    public sealed class MoveConditionalDamageTuning
    {
        [Tooltip("Multiplier applied when authored conditions pass. 1 preserves base damage.")]
        public float multiplier = 1f;

        public List<MoveResolveCondition> conditions = new List<MoveResolveCondition>();
        public bool requireAllConditions = true;

        [Tooltip("Optional count-producing predicate, such as buffed partners or target statuses.")]
        public bool useScalingCondition;
        public MoveResolveCondition scalingCondition;
        public float bonusMultiplierPerCount;
        public float maxMultiplier;

        [Tooltip("Optional seeded discrete damage multipliers. One entry is selected per hit.")]
        public List<float> randomMultipliers = new List<float>();

        public MoveConditionalDamageTuning Clone()
        {
            return new MoveConditionalDamageTuning
            {
                multiplier = multiplier,
                conditions = conditions != null
                    ? new List<MoveResolveCondition>(conditions)
                    : new List<MoveResolveCondition>(),
                requireAllConditions = requireAllConditions,
                useScalingCondition = useScalingCondition,
                scalingCondition = scalingCondition,
                bonusMultiplierPerCount = bonusMultiplierPerCount,
                maxMultiplier = maxMultiplier,
                randomMultipliers = randomMultipliers != null
                    ? new List<float>(randomMultipliers)
                    : new List<float>(),
            };
        }
    }

    [Serializable]
    public sealed class MoveHitCountTuning
    {
        [Tooltip("Force the minimum authored hit count, bypassing variable hit rolls.")]
        public bool forceMinimumHits;

        [Tooltip("Force the maximum authored hit count, bypassing variable hit rolls.")]
        public bool forceMaximumHits;

        [Tooltip("Flat hits added after the base hit roll and before cap. Can be negative.")]
        public int flatHitBonus;

        [Tooltip("Maximum total hits after all move/modifier bonuses. 0 means uncapped.")]
        public int maxTotalHitsCap;

        public MoveHitCountTuning Clone()
        {
            return new MoveHitCountTuning
            {
                forceMinimumHits = forceMinimumHits,
                forceMaximumHits = forceMaximumHits,
                flatHitBonus = flatHitBonus,
                maxTotalHitsCap = maxTotalHitsCap,
            };
        }
    }

    [Serializable]
    public sealed class MoveAccuracyTuning
    {
        [Tooltip("Force this move to hit unless forceMiss is also set.")]
        public bool forceHit;

        [Tooltip("Force this move to miss. Overrides forceHit.")]
        public bool forceMiss;

        [Tooltip("Multiplies the final attacker accuracy multiplier.")]
        public float accuracyMultiplier = 1f;

        [Tooltip("Additional accuracy bonus. 20 means +20%; 0.2 is also accepted.")]
        public float accuracyBonusPercent;

        [Tooltip("Additional positive defender evasion stages ignored by this move.")]
        public int ignoreEvasionStages;

        [Tooltip("Lower clamp for final hit chance. 20 means 20%; 0.2 is also accepted.")]
        public float minHitChance;

        [Tooltip("Upper clamp for final hit chance. 100 means 100%; 1 is also accepted.")]
        public float maxHitChance = 1f;

        public MoveAccuracyTuning Clone()
        {
            return new MoveAccuracyTuning
            {
                forceHit = forceHit,
                forceMiss = forceMiss,
                accuracyMultiplier = accuracyMultiplier,
                accuracyBonusPercent = accuracyBonusPercent,
                ignoreEvasionStages = ignoreEvasionStages,
                minHitChance = minHitChance,
                maxHitChance = maxHitChance,
            };
        }
    }

    [Serializable]
    public sealed class MoveCritTuning
    {
        [Tooltip("If true, this move cannot crit.")]
        public bool suppressCrits;

        [Tooltip("If true, this move always crits unless suppressCrits is also set.")]
        public bool forceCrit;

        [Tooltip("Override base crit chance. 0 disables override; 6.25 means 6.25%; 0.0625 is also accepted.")]
        public float baseCritChanceOverride;

        [Tooltip("Crit chance added per crit-rate stage. 2 means +2%; 0.02 is also accepted.")]
        public float critChancePerStagePercent = 0.02f;

        [Tooltip("Additional crit chance. 10 means +10%; 0.1 is also accepted.")]
        public float critChanceBonusPercent;

        [Tooltip("Multiplies final crit chance before clamping.")]
        public float critChanceMultiplier = 1f;

        [Tooltip("Override crit damage multiplier. 0 keeps CombatConstants default.")]
        public float critDamageMultiplierOverride;

        [Tooltip("Flat bonus added to crit damage multiplier after override/default and modifiers.")]
        public float critDamageMultiplierBonus;

        [Tooltip("Lower clamp for final crit chance. 20 means 20%; 0.2 is also accepted.")]
        public float minCritChance;

        [Tooltip("Upper clamp for final crit chance. 100 means 100%; 1 is also accepted.")]
        public float maxCritChance = 1f;

        public MoveCritTuning Clone()
        {
            return new MoveCritTuning
            {
                suppressCrits = suppressCrits,
                forceCrit = forceCrit,
                baseCritChanceOverride = baseCritChanceOverride,
                critChancePerStagePercent = critChancePerStagePercent,
                critChanceBonusPercent = critChanceBonusPercent,
                critChanceMultiplier = critChanceMultiplier,
                critDamageMultiplierOverride = critDamageMultiplierOverride,
                critDamageMultiplierBonus = critDamageMultiplierBonus,
                minCritChance = minCritChance,
                maxCritChance = maxCritChance,
            };
        }
    }

    [Serializable]
    public sealed class MoveDamageFormulaTuning
    {
        [Tooltip("Flat power adjustment before power multipliers. Can be negative.")]
        public int flatPowerBonus;

        [Tooltip("Move-level power multiplier before modifier power multipliers.")]
        public float powerMultiplier = 1f;

        [Tooltip("Multiplier applied before mitigation.")]
        public float preMitigationDamageMultiplier = 1f;

        [Tooltip("Flat damage added before mitigation. Can be negative.")]
        public int preMitigationFlatBonus;

        [Tooltip("Multiplier applied after mitigation floor.")]
        public float finalDamageMultiplier = 1f;

        [Tooltip("Flat damage added after mitigation floor. Can be negative.")]
        public int finalDamageFlatBonus;

        [Tooltip("Minimum final damage after all tuning. Default 1 preserves legacy chip damage.")]
        public int minimumFinalDamage = 1;

        [Tooltip("Use custom random roll bounds instead of CombatConstants RANDOM_ROLL_MIN/MAX.")]
        public bool useCustomRandomRollRange;

        public float randomRollMin = 0.85f;
        public float randomRollMax = 1f;

        [Tooltip("When true, uses customProgressionDamageFactor instead of CombatConstants.PROGRESSION_DAMAGE_FACTOR.")]
        public bool useCustomProgressionDamageFactor;

        public float customProgressionDamageFactor = 1f;

        [Tooltip("Flat value added to the base damage formula before multipliers. Legacy is +1.")]
        public float additiveDamageConstant = 1f;

        [Tooltip("If true, defender mitigation sources and defensive mitigation modifiers are ignored.")]
        public bool ignoreMitigation;

        [Tooltip("Override mitigation floor. 10 means 10%; 0.1 is also accepted.")]
        public bool overrideMitigationFloor;
        public float mitigationFloorOverride = 0.10f;

        [Tooltip("If true, this move consumes/apply next-move statuses such as Staggered, Surging, and Amplified.")]
        public bool consumeNextMoveDamageStatuses = true;

        public float staggeredDamageMultiplier = 0.90f;
        public float surgingDamageMultiplier = 1.25f;
        public float amplifiedDamageMultiplier = 2f;
        public float exploitMarkDamageMultiplier = 1.15f;

        [Tooltip("Override the attack stat used by this move before modifier substitution.")]
        public bool overrideAttackStat;
        public CoreStat attackStatOverride = CoreStat.MeleeAtk;

        [Tooltip("Override the defense stat used by this move.")]
        public bool overrideDefenseStat;
        public CoreStat defenseStatOverride = CoreStat.MeleeDef;

        public MoveDamageFormulaTuning Clone()
        {
            return new MoveDamageFormulaTuning
            {
                flatPowerBonus = flatPowerBonus,
                powerMultiplier = powerMultiplier,
                preMitigationDamageMultiplier = preMitigationDamageMultiplier,
                preMitigationFlatBonus = preMitigationFlatBonus,
                finalDamageMultiplier = finalDamageMultiplier,
                finalDamageFlatBonus = finalDamageFlatBonus,
                minimumFinalDamage = minimumFinalDamage,
                useCustomRandomRollRange = useCustomRandomRollRange,
                randomRollMin = randomRollMin,
                randomRollMax = randomRollMax,
                useCustomProgressionDamageFactor = useCustomProgressionDamageFactor,
                customProgressionDamageFactor = customProgressionDamageFactor,
                additiveDamageConstant = additiveDamageConstant,
                ignoreMitigation = ignoreMitigation,
                overrideMitigationFloor = overrideMitigationFloor,
                mitigationFloorOverride = mitigationFloorOverride,
                consumeNextMoveDamageStatuses = consumeNextMoveDamageStatuses,
                staggeredDamageMultiplier = staggeredDamageMultiplier,
                surgingDamageMultiplier = surgingDamageMultiplier,
                amplifiedDamageMultiplier = amplifiedDamageMultiplier,
                exploitMarkDamageMultiplier = exploitMarkDamageMultiplier,
                overrideAttackStat = overrideAttackStat,
                attackStatOverride = attackStatOverride,
                overrideDefenseStat = overrideDefenseStat,
                defenseStatOverride = defenseStatOverride,
            };
        }
    }

    [Serializable]
    public struct MoveResolveCondition
    {
        public MoveResolveConditionSubject subject;
        public MoveResolveConditionType conditionType;

        [Tooltip("Threshold for HP/AP/stage comparisons. Percent fields accept 20 or 0.2.")]
        public float threshold;

        public StatusId statusId;
        public CoreStat stat;
        public CoreStat comparisonStat;
    }

    public sealed class MoveResolveEvaluationContext
    {
        public TeamState sourceTeam;
        public TeamState targetTeam;
        public TurnActionLedger ledger;
        public StatusCatalogSO statusCatalog;
    }

    public readonly struct MoveResolveConditionEvaluation
    {
        public readonly bool passed;
        public readonly string reason;

        public MoveResolveConditionEvaluation(bool passed, string reason)
        {
            this.passed = passed;
            this.reason = reason ?? string.Empty;
        }
    }

    public static class MoveResolveProfileUtility
    {
        public static MoveResolveProfile Resolve(MoveInstance move)
        {
            if (move == null)
                return new MoveResolveProfile();
            if (move.resolveProfile == null)
                move.resolveProfile = new MoveResolveProfile();
            EnsureNestedDefaults(move.resolveProfile);
            return move.resolveProfile;
        }

        public static MoveResolveProfile CloneOrDefault(MoveResolveProfile profile)
        {
            return profile != null ? profile.Clone() : new MoveResolveProfile();
        }

        public static void EnsureNestedDefaults(MoveResolveProfile profile)
        {
            if (profile == null)
                return;
            if (profile.resolveConditions == null)
                profile.resolveConditions = new List<MoveResolveCondition>();
            if (profile.hitCount == null)
                profile.hitCount = new MoveHitCountTuning();
            if (profile.accuracy == null)
                profile.accuracy = new MoveAccuracyTuning();
            if (profile.crit == null)
                profile.crit = new MoveCritTuning();
            if (profile.damage == null)
                profile.damage = new MoveDamageFormulaTuning();
            if (profile.conditionalDamage == null)
                profile.conditionalDamage = new MoveConditionalDamageTuning();
            if (profile.conditionalDamage.conditions == null)
                profile.conditionalDamage.conditions = new List<MoveResolveCondition>();
            if (profile.conditionalDamage.randomMultipliers == null)
                profile.conditionalDamage.randomMultipliers = new List<float>();
        }

        public static float PercentOrFraction(float value)
        {
            return Math.Abs(value) > 1f ? value / 100f : value;
        }

        public static float Clamp01Flexible(float value)
        {
            float normalized = PercentOrFraction(value);
            return Math.Max(0f, Math.Min(1f, normalized));
        }

        public static MoveResolveConditionEvaluation EvaluateConditions(
            MoveResolveProfile profile,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveResolveEvaluationContext context = null)
        {
            if (profile == null)
                return new MoveResolveConditionEvaluation(true, string.Empty);

            return EvaluateConditions(profile.resolveConditions, profile.requireAllResolveConditions, source, target, context);
        }

        public static MoveResolveConditionEvaluation EvaluateConditions(
            List<MoveResolveCondition> conditions,
            bool requireAll,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveResolveEvaluationContext context = null)
        {
            if (conditions == null || conditions.Count == 0)
                return new MoveResolveConditionEvaluation(true, string.Empty);

            bool anyPassed = false;
            string firstFailure = string.Empty;

            for (int i = 0; i < conditions.Count; i++)
            {
                MoveResolveCondition condition = conditions[i];
                bool passed = EvaluateCondition(condition, source, target, context, out string reason);
                if (passed)
                {
                    anyPassed = true;
                    if (!requireAll)
                        return new MoveResolveConditionEvaluation(true, string.Empty);
                    continue;
                }

                if (string.IsNullOrEmpty(firstFailure))
                    firstFailure = reason;
                if (requireAll)
                    return new MoveResolveConditionEvaluation(false, firstFailure);
            }

            if (requireAll)
                return new MoveResolveConditionEvaluation(true, string.Empty);
            return new MoveResolveConditionEvaluation(anyPassed, anyPassed ? string.Empty : firstFailure);
        }

        public static int ResolveConditionCount(
            MoveResolveCondition condition,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveResolveEvaluationContext context = null)
        {
            ChainlingInstance subject = condition.subject == MoveResolveConditionSubject.Target ? target : source;
            if (subject == null)
                return 0;

            switch (condition.conditionType)
            {
                case MoveResolveConditionType.DebuffCountAtLeast:
                    return CountStatuses(subject, context, requireDebuff: true, requireBuff: false);
                case MoveResolveConditionType.StatusCountAtLeast:
                    return CountActiveStatuses(subject);
                case MoveResolveConditionType.BuffedPartnerCountAtLeast:
                    return CountBuffedPartners(source, context);
                default:
                    return EvaluateCondition(condition, source, target, context, out _) ? 1 : 0;
            }
        }

        private static bool EvaluateCondition(
            MoveResolveCondition condition,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveResolveEvaluationContext context,
            out string reason)
        {
            reason = string.Empty;
            if (condition.conditionType == MoveResolveConditionType.None)
                return true;

            ChainlingInstance subject = condition.subject == MoveResolveConditionSubject.Target ? target : source;
            ChainlingInstance other = condition.subject == MoveResolveConditionSubject.Target ? source : target;
            if (subject == null)
            {
                reason = $"{condition.subject}Missing";
                return false;
            }

            switch (condition.conditionType)
            {
                case MoveResolveConditionType.HPBelowPercent:
                {
                    if (subject.EffectiveMaxHP <= 0)
                    {
                        reason = "MaxHPMissing";
                        return false;
                    }
                    float hp = subject.currentHP / (float)subject.EffectiveMaxHP;
                    float threshold = Clamp01Flexible(condition.threshold);
                    bool passed = hp < threshold;
                    reason = passed ? string.Empty : $"{condition.subject}HPNotBelow{threshold:0.##}";
                    return passed;
                }

                case MoveResolveConditionType.HPAtOrBelowPercent:
                {
                    if (subject.EffectiveMaxHP <= 0)
                    {
                        reason = "MaxHPMissing";
                        return false;
                    }
                    float hp = subject.currentHP / (float)subject.EffectiveMaxHP;
                    float threshold = Clamp01Flexible(condition.threshold);
                    bool passed = hp <= threshold;
                    reason = passed ? string.Empty : $"{condition.subject}HPNotAtOrBelow{threshold:0.##}";
                    return passed;
                }

                case MoveResolveConditionType.HPAbovePercent:
                {
                    if (subject.EffectiveMaxHP <= 0)
                    {
                        reason = "MaxHPMissing";
                        return false;
                    }
                    float hp = subject.currentHP / (float)subject.EffectiveMaxHP;
                    float threshold = Clamp01Flexible(condition.threshold);
                    bool passed = hp > threshold;
                    reason = passed ? string.Empty : $"{condition.subject}HPNotAbove{threshold:0.##}";
                    return passed;
                }

                case MoveResolveConditionType.APAtLeast:
                {
                    bool passed = subject.currentAP >= condition.threshold;
                    reason = passed ? string.Empty : $"{condition.subject}APBelow{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.APAtMost:
                {
                    bool passed = subject.currentAP <= condition.threshold;
                    reason = passed ? string.Empty : $"{condition.subject}APAbove{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.HasStatus:
                {
                    bool passed = HasActiveStatus(subject, condition.statusId);
                    reason = passed ? string.Empty : $"{condition.subject}MissingStatus{condition.statusId}";
                    return passed;
                }

                case MoveResolveConditionType.MissingStatus:
                {
                    bool passed = !HasActiveStatus(subject, condition.statusId);
                    reason = passed ? string.Empty : $"{condition.subject}HasStatus{condition.statusId}";
                    return passed;
                }

                case MoveResolveConditionType.StageAtLeast:
                {
                    int stage = subject.GetStage(condition.stat);
                    bool passed = stage >= condition.threshold;
                    reason = passed ? string.Empty : $"{condition.subject}{condition.stat}StageBelow{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.StageAtMost:
                {
                    int stage = subject.GetStage(condition.stat);
                    bool passed = stage <= condition.threshold;
                    reason = passed ? string.Empty : $"{condition.subject}{condition.stat}StageAbove{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.SpeedHigherThanOther:
                {
                    if (other == null)
                    {
                        reason = "ComparisonTargetMissing";
                        return false;
                    }
                    bool passed = subject.GetEffectiveStat(CoreStat.Speed) > other.GetEffectiveStat(CoreStat.Speed);
                    reason = passed ? string.Empty : $"{condition.subject}SpeedNotHigher";
                    return passed;
                }

                case MoveResolveConditionType.BarrierActive:
                {
                    bool passed = CombatWallStateUtility.HasActiveWallState(subject);
                    reason = passed ? string.Empty : $"{condition.subject}BarrierMissing";
                    return passed;
                }

                case MoveResolveConditionType.NoBarrier:
                {
                    bool passed = !CombatWallStateUtility.HasActiveWallState(subject);
                    reason = passed ? string.Empty : $"{condition.subject}BarrierActive";
                    return passed;
                }

                case MoveResolveConditionType.DebuffCountAtLeast:
                {
                    int count = CountStatuses(subject, context, requireDebuff: true, requireBuff: false);
                    bool passed = count >= Math.Max(0, (int)condition.threshold);
                    reason = passed ? string.Empty : $"{condition.subject}DebuffCount{count}Below{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.StatusCountAtLeast:
                {
                    int count = CountActiveStatuses(subject);
                    bool passed = count >= Math.Max(0, (int)condition.threshold);
                    reason = passed ? string.Empty : $"{condition.subject}StatusCount{count}Below{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.NoBuffs:
                {
                    int count = CountStatuses(subject, context, requireDebuff: false, requireBuff: true);
                    bool passed = count == 0 && !HasPositiveStage(subject);
                    reason = passed ? string.Empty : $"{condition.subject}HasBuffs";
                    return passed;
                }

                case MoveResolveConditionType.BuffedPartnerCountAtLeast:
                {
                    int count = CountBuffedPartners(source, context);
                    bool passed = count >= Math.Max(0, (int)condition.threshold);
                    reason = passed ? string.Empty : $"BuffedPartnerCount{count}Below{condition.threshold}";
                    return passed;
                }

                case MoveResolveConditionType.StageAtLeastOtherStage:
                {
                    int left = subject.GetStage(condition.stat);
                    int right = subject.GetStage(condition.comparisonStat);
                    bool passed = left >= right;
                    reason = passed ? string.Empty : $"{condition.subject}{condition.stat}Below{condition.comparisonStat}";
                    return passed;
                }

                case MoveResolveConditionType.WasHitLastTurn:
                {
                    bool passed = context?.ledger != null && context.ledger.WasTargetHitLastTurn(subject.instanceId);
                    reason = passed ? string.Empty : $"{condition.subject}NotHitLastTurn";
                    return passed;
                }

                case MoveResolveConditionType.WasDamagedLastTurn:
                {
                    bool passed = context?.ledger != null && context.ledger.DidTargetTakeDamageLastTurn(subject.instanceId);
                    reason = passed ? string.Empty : $"{condition.subject}NotDamagedLastTurn";
                    return passed;
                }

                case MoveResolveConditionType.WasNotDamagedLastTurn:
                {
                    bool passed = context?.ledger != null && !context.ledger.DidTargetTakeDamageLastTurn(subject.instanceId);
                    reason = passed ? string.Empty : $"{condition.subject}DamagedLastTurn";
                    return passed;
                }

                case MoveResolveConditionType.DidNotUseDamagingMoveThisTurn:
                {
                    bool passed = context?.ledger != null && !context.ledger.DidActorUseDamagingMoveThisTurn(subject.instanceId);
                    reason = passed ? string.Empty : $"{condition.subject}UsedDamagingMoveThisTurn";
                    return passed;
                }

                case MoveResolveConditionType.AnyFoeAPGreaterThanSelf:
                {
                    TeamState foes = context?.targetTeam;
                    bool passed = false;
                    if (foes != null)
                    {
                        List<ChainlingInstance> activeFoes = foes.GetActiveChainlings();
                        for (int i = 0; i < activeFoes.Count; i++)
                        {
                            if (activeFoes[i] != null && activeFoes[i].currentAP > subject.currentAP)
                            {
                                passed = true;
                                break;
                            }
                        }
                    }
                    reason = passed ? string.Empty : "NoFoeAPGreaterThanSelf";
                    return passed;
                }
            }

            reason = "UnsupportedResolveCondition";
            return false;
        }

        private static bool HasActiveStatus(ChainlingInstance chainling, StatusId statusId)
        {
            if (chainling == null || statusId == StatusId.None || chainling.statuses == null)
                return false;

            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance status = chainling.statuses[i];
                if (status != null && status.statusId == statusId && !status.IsExpired)
                    return true;
            }

            return false;
        }

        private static int CountActiveStatuses(ChainlingInstance chainling)
        {
            if (chainling?.statuses == null)
                return 0;
            int count = 0;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance status = chainling.statuses[i];
                if (status != null && !status.IsExpired)
                    count++;
            }
            return count;
        }

        private static int CountStatuses(
            ChainlingInstance chainling,
            MoveResolveEvaluationContext context,
            bool requireDebuff,
            bool requireBuff)
        {
            if (chainling?.statuses == null)
                return 0;

            int count = 0;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance status = chainling.statuses[i];
                if (status == null || status.IsExpired)
                    continue;

                StatusDefinitionSO definition = context?.statusCatalog != null
                    ? context.statusCatalog.Get(status.statusId)
                    : null;
                if (requireDebuff && (definition == null || !definition.isDebuff))
                    continue;
                if (requireBuff && (definition == null || !definition.isBuff))
                    continue;
                count++;
            }
            return count;
        }

        private static int CountBuffedPartners(ChainlingInstance source, MoveResolveEvaluationContext context)
        {
            if (source == null || context?.sourceTeam == null)
                return 0;

            int count = 0;
            List<ChainlingInstance> allies = context.sourceTeam.GetActiveChainlings();
            for (int i = 0; i < allies.Count; i++)
            {
                ChainlingInstance ally = allies[i];
                if (ally == null || ally == source)
                    continue;
                if (HasPositiveStage(ally) || CountStatuses(ally, context, requireDebuff: false, requireBuff: true) > 0)
                    count++;
            }
            return count;
        }

        private static bool HasPositiveStage(ChainlingInstance chainling)
        {
            return chainling != null &&
                   (chainling.stageMeleeAtk > 0 || chainling.stageMeleeDef > 0 ||
                    chainling.stageRangedAtk > 0 || chainling.stageRangedDef > 0 ||
                    chainling.stageSpeed > 0 || chainling.stageAccuracy > 0 ||
                    chainling.stageEvasion > 0 || chainling.stageCritRate > 0);
        }
    }
}
