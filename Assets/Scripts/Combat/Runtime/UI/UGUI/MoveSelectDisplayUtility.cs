using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Display helpers for move-select detail UI (targeting labels and intent colors).
    /// </summary>
    public static class MoveSelectDisplayUtility
    {
        public enum TargetingIntent
        {
            Damaging,
            Debuff,
            Neutral,
            Support,
        }

        public static string FormatTargetingLabel(MoveTargeting targeting)
        {
            switch (targeting)
            {
                case MoveTargeting.SingleEnemy:
                    return "Single Enemy";
                case MoveTargeting.AllEnemies:
                    return "All Enemies";
                case MoveTargeting.SingleAlly:
                    return "Single Ally";
                case MoveTargeting.AllAllies:
                    return "All Allies";
                case MoveTargeting.Self:
                    return "Self";
                case MoveTargeting.Field:
                    return "Entire Field";
                case MoveTargeting.RandomEnemy:
                    return "Random Enemy";
                default:
                    return NicifyEnumName(targeting.ToString());
            }
        }

        private static string NicifyEnumName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            var label = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1]))
                    label.Append(' ');
                label.Append(c);
            }

            return label.ToString();
        }

        public static string FormatTargetingLine(MoveTargeting targeting) =>
            $"Target {FormatTargetingLabel(targeting)}";

        public static string FormatRoleLabel(MoveCategory category)
        {
            switch (category)
            {
                case MoveCategory.DamagingMelee:
                    return "Damaging Melee";
                case MoveCategory.DamagingRanged:
                    return "Damaging Ranged";
                case MoveCategory.StatusUtility:
                    return "Utility";
                case MoveCategory.Support:
                    return "Support";
                case MoveCategory.Passive:
                    return "Passive";
                default:
                    return NicifyEnumName(category.ToString());
            }
        }

        public static string FormatTypeLabel(ChainlingType baseType, ChainlingType resolvedType)
        {
            string resolvedName = FormatTypeName(resolvedType);
            if (baseType == ChainlingType.None && resolvedType != ChainlingType.None)
                return $"Typeless ({resolvedName})";
            return resolvedName;
        }

        public static string FormatTypeName(ChainlingType type)
        {
            if (type == ChainlingType.None)
                return "Typeless";
            return NicifyEnumName(type.ToString());
        }

        public static string FormatTypeRoleLine(string typeLabel, string roleLabel)
        {
            bool hasType = !string.IsNullOrEmpty(typeLabel);
            bool hasRole = !string.IsNullOrEmpty(roleLabel);

            if (!hasType && !hasRole)
                return "--";
            if (!hasType)
                return roleLabel;
            if (!hasRole)
                return typeLabel;

            return $"{typeLabel} | {roleLabel}";
        }

        public static string FormatPowerOrEffectLabel(MoveInstance move)
        {
            if (move == null)
                return "Power --";

            switch (move.category)
            {
                case MoveCategory.DamagingMelee:
                case MoveCategory.DamagingRanged:
                    return $"Power {move.basePower}";

                case MoveCategory.Support:
                    if (move.healPercentMaxHP > 0)
                        return "Heal";
                    if (HasPositiveStageChanges(move))
                        return "Buff";
                    return "Support";

                case MoveCategory.StatusUtility:
                    switch (ResolveTargetingIntent(move))
                    {
                        case TargetingIntent.Debuff:
                            return "De-Buff";
                        case TargetingIntent.Support:
                            return "Buff";
                        default:
                            return "Utility";
                    }

                case MoveCategory.Passive:
                    return "Passive";

                default:
                    return "Utility";
            }
        }

        public static string FormatAccuracyLine(MoveInstance move)
        {
            if (move == null)
                return "Accuracy --";

            if (move.baseAccuracy <= 0)
                return "Accuracy Auto";

            return $"Accuracy {move.baseAccuracy}";
        }

        public static string FormatPowerOrEffectLabel(MoveDefinitionSO move)
        {
            if (move == null)
                return "Power --";

            switch (move.category)
            {
                case MoveCategory.DamagingMelee:
                case MoveCategory.DamagingRanged:
                    return $"Power {move.power}";
                case MoveCategory.Support:
                    if (move.healPercentMaxHP > 0)
                        return "Heal";
                    return "Support";
                case MoveCategory.StatusUtility:
                    return "Utility";
                case MoveCategory.Passive:
                    return "Passive";
                default:
                    return move.power > 0 ? $"Power {move.power}" : "Utility";
            }
        }

        public static string FormatAccuracyLine(MoveDefinitionSO move)
        {
            if (move == null)
                return "Accuracy --";

            if (move.accuracy <= 0)
                return "Accuracy Auto";

            return $"Accuracy {move.accuracy}";
        }

        public readonly struct ReadableStatValues
        {
            public readonly string typeValue;
            public readonly string powerValue;
            public readonly string accuracyValue;

            public ReadableStatValues(string typeValue, string powerValue, string accuracyValue)
            {
                this.typeValue = typeValue ?? string.Empty;
                this.powerValue = powerValue ?? string.Empty;
                this.accuracyValue = accuracyValue ?? string.Empty;
            }

            public static ReadableStatValues Empty => new ReadableStatValues(string.Empty, string.Empty, string.Empty);
        }

        public static bool ReadablePowerApplies(MoveCategory category) =>
            category == MoveCategory.DamagingMelee || category == MoveCategory.DamagingRanged;

        public static string FormatReadablePowerValue(MoveCategory category, int power) =>
            ReadablePowerApplies(category) ? power.ToString() : string.Empty;

        public static string FormatReadableAccuracyValue(int accuracy) =>
            accuracy > 0 ? accuracy.ToString() + "%" : string.Empty;

        public static ReadableStatValues FormatReadableStats(MoveInstance move, ChainlingType resolvedType)
        {
            if (move == null)
                return ReadableStatValues.Empty;

            return new ReadableStatValues(
                FormatTypeLabel(move.baseType, resolvedType),
                FormatReadablePowerValue(move.category, move.basePower),
                FormatReadableAccuracyValue(move.baseAccuracy));
        }

        public static string FormatApCostLine(int apCost) => $"AP Cost {apCost}";

        public static string FormatApCostLine(int apCost, int currentAp) =>
            $"AP Cost {apCost} (Current {currentAp})";

        public const string InsufficientApReason = "InsufficientAP";
        public const string PriorityTagPrior = "PRIOR";
        public const string PriorityTagPrio = "PRIO";
        public const string PriorityPillLabel = "PRIOR";
        public const string PassivePillLabel = "PAS";

        public static bool HasPriorityTag(MoveInstance move)
        {
            if (move?.tags == null || move.tags.Count == 0)
                return false;

            for (int i = 0; i < move.tags.Count; i++)
            {
                string tag = move.tags[i];
                if (string.IsNullOrEmpty(tag))
                    continue;
                if (tag.Equals(PriorityTagPrior, System.StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals(PriorityTagPrio, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasPositiveStageChanges(MoveInstance move)
        {
            if (move?.stageChanges == null || move.stageChanges.Count == 0)
                return false;

            for (int i = 0; i < move.stageChanges.Count; i++)
            {
                if (move.stageChanges[i].stages > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Maps move category + targeting to UI intent colors:
        /// damaging (red), enemy debuff (orange), field/neutral (yellow), ally/self support (green).
        /// RandomEnemy is always treated as a damaging target type (red).
        /// </summary>
        public static TargetingIntent ResolveTargetingIntent(MoveInstance move)
        {
            if (move == null)
                return TargetingIntent.Neutral;

            if (move.targeting == MoveTargeting.RandomEnemy)
                return TargetingIntent.Damaging;

            switch (move.category)
            {
                case MoveCategory.DamagingMelee:
                case MoveCategory.DamagingRanged:
                    return TargetingIntent.Damaging;

                case MoveCategory.Support:
                    return TargetingIntent.Support;

                case MoveCategory.StatusUtility:
                    if (move.targeting == MoveTargeting.Field)
                        return TargetingIntent.Neutral;
                    if (TargetsEnemies(move.targeting))
                        return TargetingIntent.Debuff;
                    if (TargetsAlliesOrSelf(move.targeting))
                        return TargetingIntent.Support;
                    return TargetingIntent.Neutral;

                case MoveCategory.Passive:
                default:
                    return TargetingIntent.Neutral;
            }
        }

        public static Color ResolveTargetingColor(
            MoveInstance move,
            Color damagingColor,
            Color debuffColor,
            Color neutralColor,
            Color supportColor)
        {
            switch (ResolveTargetingIntent(move))
            {
                case TargetingIntent.Damaging:
                    return damagingColor;
                case TargetingIntent.Debuff:
                    return debuffColor;
                case TargetingIntent.Support:
                    return supportColor;
                default:
                    return neutralColor;
            }
        }

        private static bool TargetsEnemies(MoveTargeting targeting) =>
            targeting == MoveTargeting.SingleEnemy
            || targeting == MoveTargeting.AllEnemies;

        private static bool TargetsAlliesOrSelf(MoveTargeting targeting) =>
            targeting == MoveTargeting.Self
            || targeting == MoveTargeting.SingleAlly
            || targeting == MoveTargeting.AllAllies;

        /// <summary>
        /// Collects all modifier section IDs attached to a move, matching CombatResolver resolution order.
        /// </summary>
        public static List<string> CollectAttachedModifierSectionIds(MoveInstance move)
        {
            var ids = new List<string>(4);
            if (move == null)
                return ids;

            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    var runtime = move.modifiers[i];
                    AddUniqueSectionId(ids, runtime != null ? runtime.definitionSectionId : null);
                }
            }

            AddUniqueSectionId(ids, move.modifier1SectionId);
            AddUniqueSectionId(ids, move.modifier2SectionId);

            if (move.modifierIds != null)
            {
                for (int i = 0; i < move.modifierIds.Length; i++)
                    AddUniqueSectionId(ids, move.modifierIds[i]);
            }

            return ids;
        }

        public static ModifierInstance FindRuntimeModifier(MoveInstance move, string sectionId)
        {
            if (move?.modifiers == null || string.IsNullOrWhiteSpace(sectionId))
                return null;

            for (int i = 0; i < move.modifiers.Count; i++)
            {
                var runtime = move.modifiers[i];
                if (runtime != null &&
                    string.Equals(runtime.definitionSectionId, sectionId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        public static RarityTier ResolveModifierDisplayRarity(ModifierInstance runtime, ModifierDefinitionSO definition)
        {
            if (definition != null)
                return definition.minimumRarity;
            if (runtime != null)
                return runtime.rarity;
            return RarityTier.Common;
        }

        public static string FormatModifierDescription(ModifierInstance modifier, ModifierDefinitionSO definition)
        {
            string displayName = definition != null ? definition.GetDisplayName() : null;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.effectDescription))
            {
                string description = definition.effectDescription.Trim();
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    !string.Equals(displayName, definition.sectionId, System.StringComparison.Ordinal))
                    return $"{displayName}: {description}";
                return description;
            }

            if (modifier == null)
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;
                return definition != null && !string.IsNullOrWhiteSpace(definition.sectionId)
                    ? definition.sectionId
                    : "--";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(NicifyEnumName(modifier.category.ToString()));
            sb.Append(" · ");
            sb.Append(NicifyEnumName(modifier.scope.ToString()));

            if (modifier.effectValue != 0f || modifier.effectValueMax != 0f)
            {
                if (modifier.effectValueMax > modifier.effectValue)
                    sb.Append($" · {modifier.effectValue:0.#}-{modifier.effectValueMax:0.#}");
                else
                    sb.Append($" · {modifier.effectValue:0.#}");
            }

            if (!string.IsNullOrWhiteSpace(modifier.definitionSectionId))
                sb.Append($" ({modifier.definitionSectionId})");

            return sb.ToString();
        }

        private static void AddUniqueSectionId(List<string> ids, string sectionId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(sectionId))
                return;

            string trimmed = sectionId.Trim();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], trimmed, System.StringComparison.OrdinalIgnoreCase))
                    return;
            }

            ids.Add(trimmed);
        }
    }
}
