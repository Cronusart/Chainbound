using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Compares Moves.csv gameplay fields against MoveDefinitionSO assets.
    /// Use after import (or before commit) to catch stale assets / silent parse skips.
    /// </summary>
    public static class CombatMovesCsvParityAuditor
    {
        public const string DefaultReportPath = "Docs/Combat/Audits/moves_csv_asset_parity.md";

        [MenuItem("Tools/Chainbound/Combat/Audit Moves CSV vs Assets")]
        public static void AuditMenu()
        {
            string summary = AuditAndWriteReport(
                Path.Combine(CombatCsvRoundTrip.DefaultDataFolder, "Moves.csv").Replace('\\', '/'),
                CombatCsvRoundTrip.DefaultMovesFolder,
                CombatCsvRoundTrip.DefaultMovesDatabasePath,
                DefaultReportPath);
            Debug.Log($"[CombatMovesCsvParityAuditor]\n{summary}");
            EditorUtility.DisplayDialog("Moves CSV ↔ Asset Parity", summary, "OK");
        }

        public static string AuditAndWriteReport(
            string csvPath,
            string movesFolder,
            string moveDatabasePath,
            string reportPath)
        {
            var diffs = new List<string>();
            var csvOnly = new List<string>();
            var assetOnly = new List<string>();

            if (!File.Exists(csvPath))
            {
                string missing = $"Moves.csv not found at {csvPath}";
                CombatMovesCsvValidation.WriteReport(reportPath, "# Moves CSV ↔ Asset Parity\n\n" + missing + "\n");
                return missing;
            }

            Dictionary<string, ParsedMoveRow> csvRows = LoadCsvRows(csvPath, out List<string> loadErrors);
            Dictionary<string, MoveDefinitionSO> assets = LoadAssets(movesFolder, moveDatabasePath);

            for (int i = 0; i < loadErrors.Count; i++)
                diffs.Add(loadErrors[i]);

            foreach (var pair in csvRows)
            {
                if (!assets.TryGetValue(pair.Key, out MoveDefinitionSO asset) || asset == null)
                {
                    csvOnly.Add(pair.Key);
                    continue;
                }

                CompareRow(pair.Value, asset, diffs);
            }

            foreach (var pair in assets)
            {
                if (!csvRows.ContainsKey(pair.Key))
                    assetOnly.Add(pair.Key);
            }

            csvOnly.Sort(StringComparer.OrdinalIgnoreCase);
            assetOnly.Sort(StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.AppendLine("# Moves CSV ↔ Asset Parity");
            sb.AppendLine();
            sb.AppendLine($"- CSV: `{csvPath}`");
            sb.AppendLine($"- Assets: `{movesFolder}` / `{moveDatabasePath}`");
            sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- CSV rows: {csvRows.Count}");
            sb.AppendLine($"- Assets: {assets.Count}");
            sb.AppendLine($"- Field diffs: {diffs.Count}");
            sb.AppendLine($"- CSV-only MoveIds: {csvOnly.Count}");
            sb.AppendLine($"- Asset-only MoveIds: {assetOnly.Count}");
            sb.AppendLine();

            if (diffs.Count == 0 && csvOnly.Count == 0 && assetOnly.Count == 0)
            {
                sb.AppendLine("Parity OK — gameplay fields match.");
            }
            else
            {
                if (diffs.Count > 0)
                {
                    sb.AppendLine("## Field diffs");
                    sb.AppendLine();
                    for (int i = 0; i < diffs.Count; i++)
                        sb.AppendLine("- " + diffs[i]);
                    sb.AppendLine();
                }

                if (csvOnly.Count > 0)
                {
                    sb.AppendLine("## Present in CSV only (missing/unimported assets)");
                    sb.AppendLine();
                    for (int i = 0; i < csvOnly.Count; i++)
                        sb.AppendLine("- `" + csvOnly[i] + "`");
                    sb.AppendLine();
                }

                if (assetOnly.Count > 0)
                {
                    sb.AppendLine("## Present in assets only (not in CSV)");
                    sb.AppendLine();
                    for (int i = 0; i < assetOnly.Count; i++)
                        sb.AppendLine("- `" + assetOnly[i] + "`");
                    sb.AppendLine();
                }
            }

            CombatMovesCsvValidation.WriteReport(reportPath, sb.ToString());
            return $"Parity audit → {diffs.Count} field diff(s), {csvOnly.Count} CSV-only, {assetOnly.Count} asset-only. Report: {reportPath}";
        }

        private static Dictionary<string, ParsedMoveRow> LoadCsvRows(string csvPath, out List<string> errors)
        {
            errors = new List<string>();
            var rows = new Dictionary<string, ParsedMoveRow>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(csvPath);
            int headerIndex = CombatMovesCsvSchema.FindDataHeaderLineIndex(lines);
            if (headerIndex < 0)
            {
                errors.Add("No Moves.csv header row found.");
                return rows;
            }

            string[] headerFields = Combat2DataImporter.ParseCsvLinePublic(lines[headerIndex]);
            if (!CombatMovesCsvSchema.TryBuildColumnMap(
                    headerFields,
                    out CombatCsvColumnMap columns,
                    out List<string> missingRequired,
                    out _))
            {
                errors.Add("Missing required columns: " + string.Join(", ", missingRequired));
                return rows;
            }

            for (int row = headerIndex + 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] fields = Combat2DataImporter.ParseCsvLinePublic(line);
                string moveId = columns.Get(fields, "MoveId");
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                rows[moveId] = new ParsedMoveRow
                {
                    MoveId = moveId,
                    DisplayName = columns.Get(fields, "DisplayName"),
                    Category = columns.Get(fields, "Category"),
                    BaseType = columns.Get(fields, "BaseType"),
                    Rarity = columns.Get(fields, "Rarity"),
                    Targeting = columns.Get(fields, "Targeting"),
                    ContactType = columns.Get(fields, "ContactType"),
                    Power = columns.Get(fields, "Power", "0"),
                    APCost = columns.Get(fields, "APCost", "0"),
                    Accuracy = columns.Get(fields, "Accuracy", "0"),
                    HitMin = columns.Get(fields, "HitMin", "1"),
                    HitMax = columns.Get(fields, "HitMax", "1"),
                    RechargeTurns = columns.Get(fields, "RechargeTurns", "0"),
                    StatusEffects = NormalizePipe(columns.Get(fields, "StatusEffects")),
                    StageChanges = NormalizePipe(columns.Get(fields, "StageChanges")),
                    Tags = NormalizePipe(columns.Get(fields, "Tags")),
                    TriggerCondition = columns.Get(fields, "TriggerCondition", "None"),
                    HPThreshold = columns.Get(fields, "HPThreshold", "0"),
                    TriggerOnce = NormalizeBool(columns.Get(fields, "TriggerOnce", "false")),
                    HealPercentMaxHP = columns.Get(fields, "HealPercentMaxHP", "0"),
                    Description = columns.Get(fields, "Description"),
                    SelectionPolicy = columns.Get(fields, "SelectionPolicy", MoveSelectionPolicy.EffectOnly.ToString()),
                    ActiveConditionType = columns.Get(fields, "ActiveConditionType", ActiveConditionType.None.ToString()),
                    ActiveConditionThreshold = columns.Get(fields, "ActiveConditionThreshold", "0"),
                    UtilityEffects = NormalizePipe(columns.Get(fields, "UtilityEffects")),
                    HpCostPercent = columns.Get(fields, "HpCostPercent", "0"),
                    ResolvePrimaryTiming = columns.Get(fields, "ResolvePrimaryTiming", MoveEffectTimingBucket.ResolveTimePreHit.ToString()),
                    ResolveUtilityTiming = columns.Get(fields, "ResolveUtilityTiming", MoveUtilityExecutionTiming.UtilityOnly.ToString()),
                    ResolveConditionalPolicy = columns.Get(fields, "ResolveConditionalPolicy", MoveConditionalEffectPolicy.LegacyAndResolveConditions.ToString()),
                    ResolveConditions = NormalizePipe(columns.Get(fields, "ResolveConditions")),
                    ResolveRequireAll = NormalizeBool(columns.Get(fields, "ResolveRequireAll", "true")),
                    DamagePowerMultiplier = columns.Get(fields, "DamagePowerMultiplier", "1"),
                    DamagePreMitigationMultiplier = columns.Get(fields, "DamagePreMitigationMultiplier", "1"),
                    DamageFinalMultiplier = columns.Get(fields, "DamageFinalMultiplier", "1"),
                    DamageOverrideAttackStat = NormalizeBool(columns.Get(fields, "DamageOverrideAttackStat", "false")),
                    DamageAttackStat = columns.Get(fields, "DamageAttackStat", CoreStat.MeleeAtk.ToString()),
                    ConditionalDamageMultiplier = columns.Get(fields, "ConditionalDamageMultiplier", "1"),
                    ConditionalDamageConditions = NormalizePipe(columns.Get(fields, "ConditionalDamageConditions")),
                    ConditionalDamageRequireAll = NormalizeBool(columns.Get(fields, "ConditionalDamageRequireAll", "true")),
                    ConditionalDamageScalingCondition = NormalizePipe(columns.Get(fields, "ConditionalDamageScalingCondition")),
                    ConditionalDamageBonusPerCount = columns.Get(fields, "ConditionalDamageBonusPerCount", "0"),
                    ConditionalDamageMaxMultiplier = columns.Get(fields, "ConditionalDamageMaxMultiplier", "0"),
                    RandomDamageMultipliers = NormalizePipe(columns.Get(fields, "RandomDamageMultipliers")),
                };
            }

            return rows;
        }

        private static Dictionary<string, MoveDefinitionSO> LoadAssets(string movesFolder, string moveDatabasePath)
        {
            var map = new Dictionary<string, MoveDefinitionSO>(StringComparer.OrdinalIgnoreCase);
            var db = AssetDatabase.LoadAssetAtPath<MoveDatabaseSO>(moveDatabasePath);
            if (db != null && db.moves != null)
            {
                for (int i = 0; i < db.moves.Count; i++)
                {
                    MoveDefinitionSO move = db.moves[i];
                    if (move == null || string.IsNullOrWhiteSpace(move.moveId))
                        continue;
                    map[move.moveId] = move;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:MoveDefinitionSO", new[] { movesFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(path);
                if (move == null || string.IsNullOrWhiteSpace(move.moveId))
                    continue;
                if (!map.ContainsKey(move.moveId))
                    map[move.moveId] = move;
            }

            return map;
        }

        private static void CompareRow(ParsedMoveRow csv, MoveDefinitionSO asset, List<string> diffs)
        {
            MoveResolveProfile profile = asset.resolveProfile ?? new MoveResolveProfile();
            MoveResolveProfileUtility.EnsureNestedDefaults(profile);

            Compare(csv.MoveId, "DisplayName", csv.DisplayName, asset.displayName ?? string.Empty, diffs);
            Compare(csv.MoveId, "Category", csv.Category, asset.category.ToString(), diffs);
            Compare(csv.MoveId, "BaseType", csv.BaseType, asset.baseType.ToString(), diffs);
            Compare(csv.MoveId, "Rarity", csv.Rarity, asset.rarity.ToString(), diffs);
            Compare(csv.MoveId, "Targeting", csv.Targeting, asset.targeting.ToString(), diffs);
            Compare(csv.MoveId, "ContactType", csv.ContactType, asset.contactType.ToString(), diffs);
            Compare(csv.MoveId, "Power", NormalizeNumber(csv.Power), asset.power.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "APCost", NormalizeNumber(csv.APCost), asset.apCost.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "Accuracy", NormalizeNumber(csv.Accuracy), asset.accuracy.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "HitMin", NormalizeNumber(csv.HitMin, "1"), asset.hitCountMin.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "HitMax", NormalizeNumber(csv.HitMax, "1"), asset.hitCountMax.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "RechargeTurns", NormalizeNumber(csv.RechargeTurns), asset.rechargeTurns.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "TriggerCondition", csv.TriggerCondition, asset.triggerCondition.ToString(), diffs);
            Compare(csv.MoveId, "HPThreshold", NormalizeNumber(csv.HPThreshold), asset.hpThresholdPercent.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "TriggerOnce", csv.TriggerOnce, asset.triggerOncePerBattle ? "true" : "false", diffs);
            Compare(csv.MoveId, "HealPercentMaxHP", NormalizeNumber(csv.HealPercentMaxHP), asset.healPercentMaxHP.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "Description", csv.Description, asset.description ?? string.Empty, diffs);
            Compare(csv.MoveId, "SelectionPolicy", csv.SelectionPolicy, asset.selectionPolicy.ToString(), diffs);
            Compare(csv.MoveId, "ActiveConditionType", csv.ActiveConditionType, asset.activeConditionType.ToString(), diffs);
            Compare(csv.MoveId, "ActiveConditionThreshold", NormalizeNumber(csv.ActiveConditionThreshold), asset.activeConditionThresholdPercent.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "HpCostPercent", NormalizeNumber(csv.HpCostPercent), asset.hpCostPercent.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "StatusEffects", csv.StatusEffects, NormalizePipe(CombatCsvRoundTrip.FormatStatusEffectsPublic(asset.statusEffects)), diffs);
            Compare(csv.MoveId, "StageChanges", csv.StageChanges, NormalizePipe(CombatCsvRoundTrip.FormatStageChangesPublic(asset.stageChanges)), diffs);
            Compare(csv.MoveId, "Tags", csv.Tags, NormalizePipe(string.Join("|", asset.tags ?? new List<string>())), diffs);
            Compare(csv.MoveId, "UtilityEffects", csv.UtilityEffects, NormalizePipe(CombatCsvRoundTrip.FormatUtilityEffectsPublic(asset.utilityEffects)), diffs);
            Compare(csv.MoveId, "ResolvePrimaryTiming", csv.ResolvePrimaryTiming, profile.primaryTiming.ToString(), diffs);
            Compare(csv.MoveId, "ResolveUtilityTiming", csv.ResolveUtilityTiming, profile.utilityExecutionTiming.ToString(), diffs);
            Compare(csv.MoveId, "ResolveConditionalPolicy", csv.ResolveConditionalPolicy, profile.conditionalEffectPolicy.ToString(), diffs);
            Compare(csv.MoveId, "ResolveConditions", csv.ResolveConditions, NormalizePipe(CombatCsvRoundTrip.FormatResolveConditionsPublic(profile.resolveConditions)), diffs);
            Compare(csv.MoveId, "ResolveRequireAll", csv.ResolveRequireAll, profile.requireAllResolveConditions ? "true" : "false", diffs);
            Compare(csv.MoveId, "DamagePowerMultiplier", NormalizeNumber(csv.DamagePowerMultiplier, "1"), profile.damage.powerMultiplier.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "DamagePreMitigationMultiplier", NormalizeNumber(csv.DamagePreMitigationMultiplier, "1"), profile.damage.preMitigationDamageMultiplier.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "DamageFinalMultiplier", NormalizeNumber(csv.DamageFinalMultiplier, "1"), profile.damage.finalDamageMultiplier.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "DamageOverrideAttackStat", csv.DamageOverrideAttackStat, profile.damage.overrideAttackStat ? "true" : "false", diffs);
            Compare(csv.MoveId, "DamageAttackStat", csv.DamageAttackStat, profile.damage.attackStatOverride.ToString(), diffs);
            Compare(csv.MoveId, "ConditionalDamageMultiplier", NormalizeNumber(csv.ConditionalDamageMultiplier, "1"), profile.conditionalDamage.multiplier.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "ConditionalDamageConditions", csv.ConditionalDamageConditions, NormalizePipe(CombatCsvRoundTrip.FormatResolveConditionsPublic(profile.conditionalDamage.conditions)), diffs);
            Compare(csv.MoveId, "ConditionalDamageRequireAll", csv.ConditionalDamageRequireAll, profile.conditionalDamage.requireAllConditions ? "true" : "false", diffs);
            string scalingAsset = profile.conditionalDamage.useScalingCondition
                ? CombatCsvRoundTrip.FormatResolveConditionPublic(profile.conditionalDamage.scalingCondition)
                : string.Empty;
            Compare(csv.MoveId, "ConditionalDamageScalingCondition", csv.ConditionalDamageScalingCondition, NormalizePipe(scalingAsset), diffs);
            Compare(csv.MoveId, "ConditionalDamageBonusPerCount", NormalizeNumber(csv.ConditionalDamageBonusPerCount), profile.conditionalDamage.bonusMultiplierPerCount.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "ConditionalDamageMaxMultiplier", NormalizeNumber(csv.ConditionalDamageMaxMultiplier), profile.conditionalDamage.maxMultiplier.ToString(CultureInfo.InvariantCulture), diffs);
            Compare(csv.MoveId, "RandomDamageMultipliers", csv.RandomDamageMultipliers, NormalizePipe(CombatCsvRoundTrip.FormatFloatListPublic(profile.conditionalDamage.randomMultipliers)), diffs);
        }

        private static void Compare(string moveId, string field, string csvValue, string assetValue, List<string> diffs)
        {
            string left = csvValue ?? string.Empty;
            string right = assetValue ?? string.Empty;
            if (string.Equals(left, right, StringComparison.Ordinal))
                return;

            // Numeric tolerant compare for floats written with different precision.
            if (float.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out float a) &&
                float.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out float b) &&
                Mathf.Abs(a - b) < 0.0001f)
            {
                return;
            }

            diffs.Add($"`{moveId}`.{field}: CSV=`{left}` Asset=`{right}`");
        }

        private static string NormalizePipe(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] parts = value.Split('|');
            var cleaned = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                    cleaned.Add(part);
            }

            cleaned.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", cleaned);
        }

        private static string NormalizeBool(string value)
        {
            if (bool.TryParse(value, out bool b))
                return b ? "true" : "false";
            return "false";
        }

        private static string NormalizeNumber(string value, string fallback = "0")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return f.ToString(CultureInfo.InvariantCulture);
            return value.Trim();
        }

        private sealed class ParsedMoveRow
        {
            public string MoveId;
            public string DisplayName;
            public string Category;
            public string BaseType;
            public string Rarity;
            public string Targeting;
            public string ContactType;
            public string Power;
            public string APCost;
            public string Accuracy;
            public string HitMin;
            public string HitMax;
            public string RechargeTurns;
            public string StatusEffects;
            public string StageChanges;
            public string Tags;
            public string TriggerCondition;
            public string HPThreshold;
            public string TriggerOnce;
            public string HealPercentMaxHP;
            public string Description;
            public string SelectionPolicy;
            public string ActiveConditionType;
            public string ActiveConditionThreshold;
            public string UtilityEffects;
            public string HpCostPercent;
            public string ResolvePrimaryTiming;
            public string ResolveUtilityTiming;
            public string ResolveConditionalPolicy;
            public string ResolveConditions;
            public string ResolveRequireAll;
            public string DamagePowerMultiplier;
            public string DamagePreMitigationMultiplier;
            public string DamageFinalMultiplier;
            public string DamageOverrideAttackStat;
            public string DamageAttackStat;
            public string ConditionalDamageMultiplier;
            public string ConditionalDamageConditions;
            public string ConditionalDamageRequireAll;
            public string ConditionalDamageScalingCondition;
            public string ConditionalDamageBonusPerCount;
            public string ConditionalDamageMaxMultiplier;
            public string RandomDamageMultipliers;
        }
    }
}
