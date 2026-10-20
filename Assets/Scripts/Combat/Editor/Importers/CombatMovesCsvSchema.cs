using System;
using System.Collections.Generic;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Named-column schema for Moves.csv. Importers look up columns by header name
    /// so spreadsheet column reordering does not silently corrupt data.
    /// </summary>
    public static class CombatMovesCsvSchema
    {
        public const string HeaderKey = "MoveId";

        public static readonly string[] RequiredColumns =
        {
            "MoveId",
            "DisplayName",
            "Category",
            "BaseType",
            "Rarity",
            "Targeting",
            "ContactType",
            "Power",
            "APCost",
            "Accuracy",
            "HitMin",
            "HitMax",
            "RechargeTurns",
            "StatusEffects",
            "StageChanges",
            "Tags",
            "TriggerCondition",
            "HPThreshold",
            "TriggerOnce",
        };

        public static readonly string[] OptionalColumns =
        {
            "HealPercentMaxHP",
            "Description",
            "SelectionPolicy",
            "ActiveConditionType",
            "ActiveConditionThreshold",
            "UtilityEffects",
            "HpCostPercent",
            "ResolvePrimaryTiming",
            "ResolveUtilityTiming",
            "ResolveConditionalPolicy",
            "ResolveConditions",
            "ResolveRequireAll",
            "DamagePowerMultiplier",
            "DamagePreMitigationMultiplier",
            "DamageFinalMultiplier",
            "DamageOverrideAttackStat",
            "DamageAttackStat",
            "ConditionalDamageMultiplier",
            "ConditionalDamageConditions",
            "ConditionalDamageRequireAll",
            "ConditionalDamageScalingCondition",
            "ConditionalDamageBonusPerCount",
            "ConditionalDamageMaxMultiplier",
            "RandomDamageMultipliers",
            "AccuracyTuning",
            "MoveEffectsOncePerTarget",
            "DesignNotes",
        };

        public static bool TryBuildColumnMap(
            string[] headerFields,
            out CombatCsvColumnMap map,
            out List<string> missingRequired,
            out List<string> unknownColumns)
        {
            map = null;
            missingRequired = new List<string>();
            unknownColumns = new List<string>();

            if (headerFields == null || headerFields.Length == 0)
            {
                missingRequired.AddRange(RequiredColumns);
                return false;
            }

            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < RequiredColumns.Length; i++)
                known.Add(RequiredColumns[i]);
            for (int i = 0; i < OptionalColumns.Length; i++)
                known.Add(OptionalColumns[i]);

            for (int i = 0; i < headerFields.Length; i++)
            {
                string name = (headerFields[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (lookup.ContainsKey(name))
                    continue;

                lookup[name] = i;
                if (!known.Contains(name))
                    unknownColumns.Add(name);
            }

            for (int i = 0; i < RequiredColumns.Length; i++)
            {
                if (!lookup.ContainsKey(RequiredColumns[i]))
                    missingRequired.Add(RequiredColumns[i]);
            }

            if (missingRequired.Count > 0)
                return false;

            map = new CombatCsvColumnMap(lookup);
            return true;
        }

        public static int FindDataHeaderLineIndex(string[] lines)
        {
            if (lines == null)
                return -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = (lines[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (trimmed.StartsWith(HeaderKey, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }

    public sealed class CombatCsvColumnMap
    {
        private readonly Dictionary<string, int> _lookup;

        public CombatCsvColumnMap(Dictionary<string, int> lookup)
        {
            _lookup = lookup ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public bool Has(string columnName)
        {
            return !string.IsNullOrWhiteSpace(columnName) && _lookup.ContainsKey(columnName.Trim());
        }

        public string Get(string[] fields, string columnName, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(columnName))
                return fallback ?? string.Empty;

            if (!_lookup.TryGetValue(columnName.Trim(), out int index))
                return fallback ?? string.Empty;

            if (index < 0 || index >= fields.Length)
                return fallback ?? string.Empty;

            string value = fields[index] != null ? fields[index].Trim() : string.Empty;
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }
    }
}
