using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum MoveVfxRole
    {
        Damage,
        Heal,
        Buff,
        Debuff,
        Wall,
        Barrier,
        Armor,
        ApGain,
        ApDrain,
        Swap,
        Passive
    }

    /// <summary>
    /// Pure presentation classifier used by both runtime and preview VFX lookup.
    /// It deliberately reads authored base move data only; stamped infusion does not affect VFX fallback identity.
    /// </summary>
    public static class MoveVfxRoleClassifier
    {
        public static MoveVfxRole Classify(MoveInstance move)
        {
            if (move == null)
                return MoveVfxRole.Buff;

            return Classify(
                move.definitionId,
                move.category,
                move.targeting,
                move.tags,
                move.utilityEffects,
                move.healPercentMaxHP);
        }

        public static MoveVfxRole Classify(MoveDefinitionSO move)
        {
            if (move == null)
                return MoveVfxRole.Buff;

            return Classify(
                move.moveId,
                move.category,
                move.targeting,
                move.tags,
                move.utilityEffects,
                move.healPercentMaxHP);
        }

        public static MoveVfxRole ParentRole(MoveVfxRole role)
        {
            switch (role)
            {
                case MoveVfxRole.Wall:
                case MoveVfxRole.Barrier:
                case MoveVfxRole.Armor:
                case MoveVfxRole.ApGain:
                case MoveVfxRole.Swap:
                    return MoveVfxRole.Buff;
                case MoveVfxRole.ApDrain:
                    return MoveVfxRole.Debuff;
                default:
                    return role;
            }
        }

        private static MoveVfxRole Classify(
            string definitionId,
            MoveCategory category,
            MoveTargeting targeting,
            IList<string> tags,
            IList<UtilityEffectPayload> utilityEffects,
            int healPercentMaxHP)
        {
            // Damage is the primary visual identity even when the move also applies a rider.
            if (category == MoveCategory.DamagingMelee || category == MoveCategory.DamagingRanged)
                return MoveVfxRole.Damage;
            if (category == MoveCategory.Passive)
                return MoveVfxRole.Passive;
            if (string.Equals(definitionId, "position_swap", StringComparison.OrdinalIgnoreCase))
                return MoveVfxRole.Swap;

            UtilityEffectPayload block = FindEffect(utilityEffects, UtilityEffectType.BlockWindow);
            if (block != null)
            {
                if (block.contactFilter == UtilityContactFilter.Contact)
                    return MoveVfxRole.Wall;
                if (block.contactFilter == UtilityContactFilter.Projectile)
                    return MoveVfxRole.Barrier;
            }

            if (HasTag(tags, "ARMOR") || HasEffect(utilityEffects, UtilityEffectType.MitigationWindow))
                return MoveVfxRole.Armor;

            if (healPercentMaxHP > 0 || HasTag(tags, "HEAL") || HasHealingEffect(utilityEffects))
                return MoveVfxRole.Heal;

            switch (targeting)
            {
                case MoveTargeting.SingleEnemy:
                case MoveTargeting.AllEnemies:
                case MoveTargeting.RandomEnemy:
                case MoveTargeting.Field:
                    return HasAnyEffect(utilityEffects,
                            UtilityEffectType.DrainAP,
                            UtilityEffectType.StealAP,
                            UtilityEffectType.NextMoveAPSurcharge)
                        ? MoveVfxRole.ApDrain
                        : MoveVfxRole.Debuff;
                default:
                    if (HasAnyEffect(utilityEffects,
                            UtilityEffectType.GainAP,
                            UtilityEffectType.TransferAP,
                            UtilityEffectType.RefundAP,
                            UtilityEffectType.BankAPUntilNextTurn,
                            UtilityEffectType.GainApIfDistinctTargetsHitAtLeast,
                            UtilityEffectType.GainApOnDamageIfSpeedHigher,
                            UtilityEffectType.MoveAPDiscountWhileHPAbove))
                        return MoveVfxRole.ApGain;
                    return MoveVfxRole.Buff;
            }
        }

        private static bool HasHealingEffect(IList<UtilityEffectPayload> effects)
        {
            return HasAnyEffect(effects,
                UtilityEffectType.BonusHealPercentIfSpeedHigher,
                UtilityEffectType.LifestealPercent,
                UtilityEffectType.HealPercentIfStageAtLeast,
                UtilityEffectType.HealSelfPerTargetDebuff);
        }

        private static bool HasAnyEffect(IList<UtilityEffectPayload> effects, params UtilityEffectType[] types)
        {
            if (effects == null || types == null)
                return false;

            for (int i = 0; i < effects.Count; i++)
            {
                UtilityEffectPayload effect = effects[i];
                if (effect == null)
                    continue;
                for (int j = 0; j < types.Length; j++)
                {
                    if (effect.effectType == types[j])
                        return true;
                }
            }

            return false;
        }

        private static bool HasEffect(IList<UtilityEffectPayload> effects, UtilityEffectType type)
        {
            return FindEffect(effects, type) != null;
        }

        private static UtilityEffectPayload FindEffect(IList<UtilityEffectPayload> effects, UtilityEffectType type)
        {
            if (effects == null)
                return null;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null && effects[i].effectType == type)
                    return effects[i];
            }
            return null;
        }

        private static bool HasTag(IList<string> tags, string expected)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
