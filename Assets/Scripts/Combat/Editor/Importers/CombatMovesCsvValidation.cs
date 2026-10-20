using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    public enum CombatMovesCsvIssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    public sealed class CombatMovesCsvIssue
    {
        public CombatMovesCsvIssueSeverity Severity;
        public int RowNumber;
        public string MoveId;
        public string Code;
        public string Message;

        public override string ToString()
        {
            string loc = RowNumber > 0 ? $"row {RowNumber}" : "file";
            string id = string.IsNullOrWhiteSpace(MoveId) ? "-" : MoveId;
            return $"[{Severity}] {loc} ({id}) {Code}: {Message}";
        }
    }

    /// <summary>
    /// Shared Moves.csv validation used by import and stand-alone audit menus.
    /// </summary>
    public static class CombatMovesCsvValidation
    {
        public const string DefaultReportPath = "Docs/Combat/Audits/moves_import_validation.md";

        [MenuItem("Tools/Chainbound/Combat/Validate Moves CSV")]
        public static void ValidateMovesCsvMenu()
        {
            string report = ValidateAndWriteReport(
                Path.Combine(CombatCsvRoundTrip.DefaultDataFolder, "Moves.csv").Replace('\\', '/'),
                DefaultReportPath);
            Debug.Log($"[CombatMovesCsvValidation]\n{report}");
            EditorUtility.DisplayDialog("Validate Moves CSV", report, "OK");
        }

        public static string ValidateAndWriteReport(string csvPath, string reportPath)
        {
            var issues = new List<CombatMovesCsvIssue>();
            ValidateFile(csvPath, issues);
            string body = FormatMarkdownReport(csvPath, issues);
            WriteReport(reportPath, body);

            int errors = CountSeverity(issues, CombatMovesCsvIssueSeverity.Error);
            int warnings = CountSeverity(issues, CombatMovesCsvIssueSeverity.Warning);
            return $"Validated Moves.csv → {errors} error(s), {warnings} warning(s). Report: {reportPath}";
        }

        public static void ValidateFile(string csvPath, List<CombatMovesCsvIssue> issues)
        {
            if (issues == null)
                return;

            if (!File.Exists(csvPath))
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    Code = "MissingFile",
                    Message = $"Moves.csv not found at {csvPath}",
                });
                return;
            }

            string[] lines = File.ReadAllLines(csvPath);
            int headerIndex = CombatMovesCsvSchema.FindDataHeaderLineIndex(lines);
            if (headerIndex < 0)
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    Code = "MissingHeader",
                    Message = "No Moves.csv header row starting with MoveId was found.",
                });
                return;
            }

            string[] headerFields = Combat2DataImporter.ParseCsvLinePublic(lines[headerIndex]);
            if (!CombatMovesCsvSchema.TryBuildColumnMap(
                    headerFields,
                    out CombatCsvColumnMap columns,
                    out List<string> missingRequired,
                    out List<string> unknownColumns))
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    RowNumber = headerIndex + 1,
                    Code = "MissingRequiredColumns",
                    Message = "Missing required columns: " + string.Join(", ", missingRequired),
                });
                return;
            }

            for (int i = 0; i < unknownColumns.Count; i++)
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Warning,
                    RowNumber = headerIndex + 1,
                    Code = "UnknownColumn",
                    Message = $"Header column '{unknownColumns[i]}' is not in the Moves schema (ignored).",
                });
            }

            StatusCatalogSO statusCatalog = LoadStatusCatalog();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int row = headerIndex + 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] fields = Combat2DataImporter.ParseCsvLinePublic(line);
                string moveId = columns.Get(fields, "MoveId");
                int rowNumber = row + 1;

                if (string.IsNullOrWhiteSpace(moveId))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        Code = "EmptyMoveId",
                        Message = "MoveId is empty.",
                    });
                    continue;
                }

                if (!seenIds.Add(moveId))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "DuplicateMoveId",
                        Message = "Duplicate MoveId in Moves.csv.",
                    });
                }

                ValidateEnumField(columns, fields, "Category", typeof(MoveCategory), rowNumber, moveId, issues);
                ValidateEnumField(columns, fields, "BaseType", typeof(ChainlingType), rowNumber, moveId, issues);
                ValidateEnumField(columns, fields, "Rarity", typeof(RarityTier), rowNumber, moveId, issues);
                ValidateEnumField(columns, fields, "Targeting", typeof(MoveTargeting), rowNumber, moveId, issues);
                ValidateEnumField(columns, fields, "ContactType", typeof(ContactType), rowNumber, moveId, issues);
                ValidateEnumField(columns, fields, "TriggerCondition", typeof(PassiveTriggerCondition), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "SelectionPolicy", typeof(MoveSelectionPolicy), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "ActiveConditionType", typeof(ActiveConditionType), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "ResolvePrimaryTiming", typeof(MoveEffectTimingBucket), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "ResolveUtilityTiming", typeof(MoveUtilityExecutionTiming), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "ResolveConditionalPolicy", typeof(MoveConditionalEffectPolicy), rowNumber, moveId, issues);
                ValidateOptionalEnumField(columns, fields, "DamageAttackStat", typeof(CoreStat), rowNumber, moveId, issues);

                ValidateStatusEffects(columns.Get(fields, "StatusEffects"), statusCatalog, rowNumber, moveId, issues);
                ValidateUtilityEffects(columns.Get(fields, "UtilityEffects"), rowNumber, moveId, issues);
                ValidateResolveConditions(columns.Get(fields, "ResolveConditions"), rowNumber, moveId, "ResolveConditions", issues);
                ValidateResolveConditions(columns.Get(fields, "ConditionalDamageConditions"), rowNumber, moveId, "ConditionalDamageConditions", issues);

                string scaling = columns.Get(fields, "ConditionalDamageScalingCondition");
                if (!string.IsNullOrWhiteSpace(scaling) &&
                    !Combat2DataImporter.TryParseResolveConditionPublic(scaling, out _))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "BadScalingCondition",
                        Message = $"Could not parse ConditionalDamageScalingCondition '{scaling}'.",
                    });
                }
            }
        }

        public static void ValidateUtilityEffects(
            string raw,
            int rowNumber,
            string moveId,
            List<CombatMovesCsvIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(raw) || issues == null)
                return;

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                string opcode = entry.Split(':')[0].Trim();
                if (!Enum.TryParse(opcode, true, out UtilityEffectType _))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "UnknownUtilityOpcode",
                        Message = $"Unknown UtilityEffects opcode '{opcode}' in '{entry}'.",
                    });
                }
            }
        }

        public static void ValidateStatusEffects(
            string raw,
            StatusCatalogSO catalog,
            int rowNumber,
            string moveId,
            List<CombatMovesCsvIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(raw) || issues == null)
                return;

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                string[] parts = entry.Split(':');
                if (parts.Length < 3)
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "BadStatusEffectToken",
                        Message = $"StatusEffects entry needs StatusId:Duration:Chance[:Target]. Got '{entry}'.",
                    });
                    continue;
                }

                if (!Enum.TryParse(parts[0].Trim(), true, out StatusId statusId))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "UnknownStatusId",
                        Message = $"Unknown StatusId '{parts[0].Trim()}' in StatusEffects.",
                    });
                    continue;
                }

                if (catalog != null && catalog.IsChemistryOnly(statusId))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "ChemistryOnlyStatus",
                        Message = $"StatusEffects may not apply ChemistryOnly status '{statusId}'.",
                    });
                }
            }
        }

        public static void ValidateResolveConditions(
            string raw,
            int rowNumber,
            string moveId,
            string fieldName,
            List<CombatMovesCsvIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(raw) || issues == null)
                return;

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                if (!Combat2DataImporter.TryParseResolveConditionPublic(entry, out _))
                {
                    issues.Add(new CombatMovesCsvIssue
                    {
                        Severity = CombatMovesCsvIssueSeverity.Error,
                        RowNumber = rowNumber,
                        MoveId = moveId,
                        Code = "BadResolveCondition",
                        Message = $"Could not parse {fieldName} token '{entry}'.",
                    });
                }
            }
        }

        public static string FormatMarkdownReport(string csvPath, List<CombatMovesCsvIssue> issues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Moves.csv Import Validation");
            sb.AppendLine();
            sb.AppendLine($"- Source: `{csvPath}`");
            sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Errors: {CountSeverity(issues, CombatMovesCsvIssueSeverity.Error)}");
            sb.AppendLine($"- Warnings: {CountSeverity(issues, CombatMovesCsvIssueSeverity.Warning)}");
            sb.AppendLine($"- Infos: {CountSeverity(issues, CombatMovesCsvIssueSeverity.Info)}");
            sb.AppendLine();

            if (issues == null || issues.Count == 0)
            {
                sb.AppendLine("No issues found.");
                return sb.ToString();
            }

            sb.AppendLine("| Severity | Row | MoveId | Code | Message |");
            sb.AppendLine("|---|---:|---|---|---|");
            for (int i = 0; i < issues.Count; i++)
            {
                CombatMovesCsvIssue issue = issues[i];
                sb.Append("| ")
                    .Append(issue.Severity).Append(" | ")
                    .Append(issue.RowNumber > 0 ? issue.RowNumber.ToString() : "-").Append(" | ")
                    .Append(EscapeMd(issue.MoveId)).Append(" | ")
                    .Append(EscapeMd(issue.Code)).Append(" | ")
                    .Append(EscapeMd(issue.Message)).AppendLine(" |");
            }

            return sb.ToString();
        }

        public static void WriteReport(string reportPath, string body)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                return;

            string dir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(reportPath, body ?? string.Empty, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static void ValidateEnumField(
            CombatCsvColumnMap columns,
            string[] fields,
            string column,
            Type enumType,
            int rowNumber,
            string moveId,
            List<CombatMovesCsvIssue> issues)
        {
            string raw = columns.Get(fields, column);
            if (string.IsNullOrWhiteSpace(raw))
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    RowNumber = rowNumber,
                    MoveId = moveId,
                    Code = "EmptyEnum",
                    Message = $"{column} is empty.",
                });
                return;
            }

            if (!Enum.TryParse(enumType, raw, true, out _))
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    RowNumber = rowNumber,
                    MoveId = moveId,
                    Code = "BadEnum",
                    Message = $"{column} value '{raw}' is not a valid {enumType.Name}.",
                });
            }
        }

        private static void ValidateOptionalEnumField(
            CombatCsvColumnMap columns,
            string[] fields,
            string column,
            Type enumType,
            int rowNumber,
            string moveId,
            List<CombatMovesCsvIssue> issues)
        {
            if (!columns.Has(column))
                return;

            string raw = columns.Get(fields, column);
            if (string.IsNullOrWhiteSpace(raw))
                return;

            if (!Enum.TryParse(enumType, raw, true, out _))
            {
                issues.Add(new CombatMovesCsvIssue
                {
                    Severity = CombatMovesCsvIssueSeverity.Error,
                    RowNumber = rowNumber,
                    MoveId = moveId,
                    Code = "BadEnum",
                    Message = $"{column} value '{raw}' is not a valid {enumType.Name}.",
                });
            }
        }

        private static StatusCatalogSO LoadStatusCatalog()
        {
            const string canonicalPath = "Assets/Data/Combat/StatusDef/Catalog/StatusCatalog.asset";
            var canonical = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>(canonicalPath);
            if (canonical != null)
                return canonical;

            string[] guids = AssetDatabase.FindAssets("t:StatusCatalogSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var catalog = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>(path);
                if (catalog != null)
                    return catalog;
            }

            return null;
        }

        private static int CountSeverity(List<CombatMovesCsvIssue> issues, CombatMovesCsvIssueSeverity severity)
        {
            if (issues == null)
                return 0;

            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null && issues[i].Severity == severity)
                    count++;
            }

            return count;
        }

        private static string EscapeMd(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";
            return value.Replace("|", "\\|").Replace("\n", " ");
        }
    }
}
