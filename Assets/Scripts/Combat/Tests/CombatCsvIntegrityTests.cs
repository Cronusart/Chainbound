using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatCsvIntegrityTests
    {
        private static readonly string[] CsvFiles =
        {
            "Moves.csv",
            "Statuses.csv",
            "SpeciesMovePools.csv",
            "Modifiers.csv",
        };

        [Test]
        public void CombatCsvRowsHaveHeaderFieldCountAndValidEnums()
        {
            string dataDirectory = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Specs/Combat/Data"));

            foreach (string fileName in CsvFiles)
            {
                string path = Path.Combine(dataDirectory, fileName);
                Assert.That(File.Exists(path), Is.True, $"Missing combat CSV: {path}");
                ValidateFile(path);
            }
        }

        [Test]
        public void StatusIdsAreUniqueAndCsvNamesAreUnique()
        {
            var numericOwners = new Dictionary<int, string>();
            foreach (StatusId value in Enum.GetValues(typeof(StatusId)))
            {
                int numeric = (int)value;
                Assert.That(numericOwners.ContainsKey(numeric), Is.False,
                    $"StatusId value {numeric} is shared by {numericOwners.GetValueOrDefault(numeric)} and {value}.");
                numericOwners[numeric] = value.ToString();
            }

            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Specs/Combat/Data/Statuses.csv"));
            List<CsvRecord> records = ReadRecords(File.ReadAllText(path));
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < records.Count; i++)
            {
                CsvRecord record = records[i];
                if (record.Fields.Count == 0 || record.Fields[0].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;
                string name = record.Fields[0].Trim();
                Assert.That(names.Add(name), Is.True, $"Statuses.csv contains duplicate StatusId '{name}'.");
                Assert.That(Enum.TryParse(name, false, out StatusId _), Is.True,
                    $"Statuses.csv line {record.Line} has unknown StatusId '{name}'.");
            }
        }

        [Test]
        public void SpeciesPoolsContainTheirInitialMovesAndACommonDamagingMove()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data"));
            List<CsvRecord> moveRecords = ReadRecords(File.ReadAllText(Path.Combine(root, "Moves.csv")));
            Dictionary<string, int> moveColumns = Columns(moveRecords[0]);
            var moves = new Dictionary<string, CsvRecord>(StringComparer.Ordinal);
            var bossOnly = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < moveRecords.Count; i++)
            {
                CsvRecord row = moveRecords[i];
                if (IsIgnored(row)) continue;
                string moveId = row.Fields[moveColumns["MoveId"]].Trim();
                moves[moveId] = row;
                if (row.Fields[moveColumns["Tags"]].Split('|').Any(x => string.Equals(x.Trim(), "BOSS_ONLY", StringComparison.Ordinal)))
                    bossOnly.Add(moveId);
            }

            List<CsvRecord> poolRecords = ReadRecords(File.ReadAllText(Path.Combine(root, "SpeciesMovePools.csv")));
            Dictionary<string, int> poolColumns = Columns(poolRecords[0]);
            var speciesRows = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < poolRecords.Count; i++)
            {
                CsvRecord row = poolRecords[i];
                if (IsIgnored(row)) continue;
                string speciesId = row.Fields[poolColumns["SpeciesId"]].Trim();
                Assert.That(speciesRows.Add(speciesId), Is.True, $"Duplicate species pool row '{speciesId}'.");
                var pool = new HashSet<string>(SplitIds(row.Fields[poolColumns["LearnableMoves"]]), StringComparer.Ordinal);
                pool.UnionWith(SplitIds(row.Fields[poolColumns["ExclusiveMoves"]]));
                Assert.That(pool.Count, Is.InRange(4, 5), $"{speciesId}: progression pool must contain 4-5 moves.");
                string[] initial = SplitIds(row.Fields[poolColumns["InitialMoves"]]).ToArray();
                CollectionAssert.IsSubsetOf(initial, pool, $"{speciesId}: every initial move must belong to its progression pool.");
                Assert.That(pool.Overlaps(bossOnly), Is.False, $"{speciesId}: BOSS_ONLY moves cannot enter species pools.");

                foreach (string moveId in pool)
                    Assert.That(moves.ContainsKey(moveId), Is.True, $"{speciesId}: unknown move '{moveId}'.");
                bool hasCommonDamage = pool.Any(moveId =>
                {
                    CsvRecord move = moves[moveId];
                    string rarity = move.Fields[moveColumns["Rarity"]].Trim();
                    string category = move.Fields[moveColumns["Category"]].Trim();
                    return rarity == "Common" && category.StartsWith("Damaging", StringComparison.Ordinal);
                });
                Assert.That(hasCommonDamage, Is.True, $"{speciesId}: pool needs at least one Common damaging move.");
            }

            string[] speciesGuids = AssetDatabase.FindAssets(
                "t:SpeciesDefinitionSO",
                new[] { "Assets/Data/Combat/ChainlingSpecies" });
            foreach (string guid in speciesGuids)
            {
                SpeciesDefinitionSO species = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (species == null || string.IsNullOrWhiteSpace(species.speciesId) ||
                    string.IsNullOrWhiteSpace(species.displayName) || species.baseMaxHP <= 0) continue;
                Assert.That(speciesRows.Contains(species.speciesId), Is.True,
                    $"Imported species '{species.speciesId}' has no SpeciesMovePools.csv row.");
                Assert.That(species.progressionMovePool.Count, Is.InRange(4, 5),
                    $"Imported species '{species.speciesId}' does not have a 4-5 move progression pool.");
                foreach (MoveDefinitionSO initial in species.initialMoves)
                    Assert.That(species.progressionMovePool.Any(x => x?.move == initial), Is.True,
                        $"Imported species '{species.speciesId}' has initial move '{initial?.moveId}' outside its pool.");
            }
        }

        [Test]
        public void PlayableLearnPoolsFollowTheBodyBand()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data"));
            List<CsvRecord> moveRecords = ReadRecords(File.ReadAllText(Path.Combine(root, "Moves.csv")));
            Dictionary<string, int> moveColumns = Columns(moveRecords[0]);
            var moveRarity = new Dictionary<string, RarityTier>(StringComparer.Ordinal);
            for (int i = 1; i < moveRecords.Count; i++)
            {
                CsvRecord row = moveRecords[i];
                if (IsIgnored(row))
                    continue;

                string moveId = row.Fields[moveColumns["MoveId"]].Trim();
                string rarityName = row.Fields[moveColumns["Rarity"]].Trim();
                Assert.That(Enum.TryParse(rarityName, false, out RarityTier rarity), Is.True,
                    $"Moves.csv rarity '{rarityName}' is not a {nameof(RarityTier)}.");
                moveRarity[moveId] = rarity;
            }

            var speciesById = new Dictionary<string, SpeciesDefinitionSO>(StringComparer.Ordinal);
            string[] speciesGuids = AssetDatabase.FindAssets(
                "t:SpeciesDefinitionSO",
                new[] { "Assets/Data/Combat/ChainlingSpecies" });
            foreach (string guid in speciesGuids)
            {
                SpeciesDefinitionSO species = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (species == null || string.IsNullOrWhiteSpace(species.speciesId) || species.baseMaxHP <= 0)
                    continue;
                speciesById[species.speciesId] = species;
            }

            List<CsvRecord> poolRecords = ReadRecords(File.ReadAllText(Path.Combine(root, "SpeciesMovePools.csv")));
            Dictionary<string, int> poolColumns = Columns(poolRecords[0]);
            for (int i = 1; i < poolRecords.Count; i++)
            {
                CsvRecord row = poolRecords[i];
                if (IsIgnored(row))
                    continue;

                string speciesId = row.Fields[poolColumns["SpeciesId"]].Trim();
                if (speciesId == "ghost" || speciesId == "cofi_lootwak")
                    continue;

                Assert.That(speciesById.ContainsKey(speciesId), Is.True, $"{speciesId}: missing playable species asset.");
                SpeciesDefinitionSO body = speciesById[speciesId];
                var pool = new HashSet<string>(SplitIds(row.Fields[poolColumns["LearnableMoves"]]), StringComparer.Ordinal);
                pool.UnionWith(SplitIds(row.Fields[poolColumns["ExclusiveMoves"]]));
                string flagship = row.Fields[poolColumns["FlagshipMove"]].Trim();
                Assert.That(pool.Contains(flagship), Is.True, $"{speciesId}: signature '{flagship}' must sit in the learn pool.");
                Assert.That(moveRarity.ContainsKey(flagship), Is.True, $"{speciesId}: unknown signature '{flagship}'.");

                RarityTier signatureRarity = moveRarity[flagship];
                int aboveBody = 0;
                foreach (string moveId in pool)
                {
                    Assert.That(moveRarity.ContainsKey(moveId), Is.True, $"{speciesId}: unknown move '{moveId}'.");
                    if (string.Equals(moveId, flagship, StringComparison.Ordinal))
                        continue;

                    RarityTier rarity = moveRarity[moveId];
                    Assert.That((int)rarity, Is.LessThan((int)signatureRarity),
                        $"{speciesId}: '{moveId}' ({rarity}) is not below signature '{flagship}' ({signatureRarity}).");
                    if (body.isStarter || rarity <= body.rarity)
                        continue;

                    aboveBody++;
                    Assert.That((int)rarity, Is.EqualTo((int)body.rarity + 1),
                        $"{speciesId}: '{moveId}' ({rarity}) is more than one step above {body.rarity}.");
                }

                if (body.isStarter)
                    continue;

                Assert.That(pool.Count, Is.EqualTo(5), $"{speciesId}: wild learn pool must be five moves.");
                Assert.That(aboveBody, Is.LessThanOrEqualTo(1),
                    $"{speciesId}: only one non-signature move may sit one step above {body.rarity}.");
            }
        }

        [Test]
        public void LearnPool_ValidapeUplinkAndLiveBuiltCards()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Specs/Combat/Data/SpeciesMovePools.csv"));
            List<CsvRecord> records = ReadRecords(File.ReadAllText(path));
            Dictionary<string, int> columns = Columns(records[0]);
            var rows = records
                .Skip(1)
                .Where(row => !IsIgnored(row))
                .ToDictionary(
                    row => row.Fields[columns["SpeciesId"]].Trim(),
                    row => row,
                    StringComparer.Ordinal);

            HashSet<string> Pool(string speciesId)
            {
                CsvRecord row = rows[speciesId];
                var result = new HashSet<string>(SplitIds(row.Fields[columns["LearnableMoves"]]), StringComparer.Ordinal);
                result.UnionWith(SplitIds(row.Fields[columns["ExclusiveMoves"]]));
                return result;
            }

            CollectionAssert.AreEquivalent(
                new[] { "hub_heal", "baseline_striker", "uplink", "hammer_sync", "pack_rally" },
                Pool("validape"));
            Assert.That(Pool("validape"), Does.Not.Contain("ap_router"));
            Assert.That(Pool("keyper"), Does.Contain("system_reset"));
            Assert.That(Pool("cinislug"), Does.Contain("hype_burst"));
            Assert.That(Pool("kodiax"), Does.Contain("crystal_extraction"));

            string[] ungranted = { "accuracy_disrupt", "power_drain", "ore_restoration" };
            foreach (string moveId in ungranted)
            {
                foreach (string speciesId in rows.Keys)
                    Assert.That(Pool(speciesId), Does.Not.Contain(moveId), $"{moveId} must remain ungranted ({speciesId}).");
            }
        }

        private static void ValidateFile(string path)
        {
            List<CsvRecord> records = ReadRecords(File.ReadAllText(path));
            Assert.That(records.Count, Is.GreaterThan(0), $"{path} has no records.");

            CsvRecord headerRecord = records[0];
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < headerRecord.Fields.Count; i++)
                columns[headerRecord.Fields[i]] = i;

            for (int i = 1; i < records.Count; i++)
            {
                CsvRecord record = records[i];
                if (record.Fields.Count == 0 ||
                    (record.Fields.Count == 1 && string.IsNullOrWhiteSpace(record.Fields[0])) ||
                    record.Fields[0].TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;

                Assert.That(
                    record.Fields.Count,
                    Is.EqualTo(headerRecord.Fields.Count),
                    $"{Path.GetFileName(path)} line {record.Line}: expected {headerRecord.Fields.Count} fields, got {record.Fields.Count}.");

                if (Path.GetFileName(path) == "Moves.csv")
                    ValidateMoveEnums(path, record, columns);
                else if (Path.GetFileName(path) == "Statuses.csv")
                    ValidateStatusEnums(path, record, columns);
            }
        }

        private static void ValidateMoveEnums(string path, CsvRecord row, Dictionary<string, int> columns)
        {
            AssertEnum<MoveCategory>(path, row, columns, "Category");
            AssertEnum<ChainlingType>(path, row, columns, "BaseType");
            AssertEnum<RarityTier>(path, row, columns, "Rarity");
            AssertEnum<MoveTargeting>(path, row, columns, "Targeting");
            AssertEnum<ContactType>(path, row, columns, "ContactType");
            AssertEnum<MoveSelectionPolicy>(path, row, columns, "SelectionPolicy", allowEmpty: true);
            AssertEnum<ActiveConditionType>(path, row, columns, "ActiveConditionType", allowEmpty: true);
            AssertEnum<MoveEffectTimingBucket>(path, row, columns, "ResolvePrimaryTiming", allowEmpty: true);
            AssertEnum<MoveUtilityExecutionTiming>(path, row, columns, "ResolveUtilityTiming", allowEmpty: true);
            AssertEnum<MoveConditionalEffectPolicy>(path, row, columns, "ResolveConditionalPolicy", allowEmpty: true);

            AssertTokenEnums<CombatStageStat>(path, row, columns, "StageChanges", 0);
            AssertTokenEnums<StatusId>(path, row, columns, "StatusEffects", 0);
            AssertTokenEnums<UtilityEffectType>(path, row, columns, "UtilityEffects", 0);
            AssertConditionTokens(path, row, columns, "ResolveConditions");
            AssertConditionTokens(path, row, columns, "ConditionalDamageConditions");
            AssertConditionTokens(path, row, columns, "ConditionalDamageScalingCondition");
        }

        private static void ValidateStatusEnums(string path, CsvRecord row, Dictionary<string, int> columns)
        {
            AssertEnum<StatusId>(path, row, columns, "StatusId");
            AssertEnum<StatusOrigin>(path, row, columns, "Origin");
            AssertEnum<StatusClass>(path, row, columns, "Class");
            AssertStatusStageTokens(path, row, columns, "StageEffects");
        }

        private static void AssertStatusStageTokens(
            string path,
            CsvRecord row,
            Dictionary<string, int> columns,
            string column)
        {
            foreach (string token in Tokens(row, columns, column))
            {
                string[] parts = token.Split(':');
                Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2), Error(path, row, column, token));
                bool coreStat = Enum.TryParse(parts[0], false, out CoreStat _);
                bool combatStage = Enum.TryParse(parts[0], false, out CombatStageStat stage) &&
                    (stage == CombatStageStat.Accuracy || stage == CombatStageStat.Evasion);
                Assert.That(coreStat || combatStage, Is.True, Error(path, row, column, token));
                Assert.That(int.TryParse(parts[1], out int _), Is.True, Error(path, row, column, token));
                if (parts.Length >= 3)
                    Assert.That(bool.TryParse(parts[2], out bool _), Is.True, Error(path, row, column, token));
            }
        }

        private static void AssertConditionTokens(
            string path,
            CsvRecord row,
            Dictionary<string, int> columns,
            string column)
        {
            foreach (string token in Tokens(row, columns, column))
            {
                string[] parts = token.Split(':');
                Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2), Error(path, row, column, token));
                Assert.That(Enum.TryParse(parts[0], false, out MoveResolveConditionSubject _), Is.True, Error(path, row, column, token));
                Assert.That(Enum.TryParse(parts[1], false, out MoveResolveConditionType _), Is.True, Error(path, row, column, token));
            }
        }

        private static void AssertTokenEnums<T>(
            string path,
            CsvRecord row,
            Dictionary<string, int> columns,
            string column,
            int tokenIndex) where T : struct
        {
            foreach (string token in Tokens(row, columns, column))
            {
                string[] parts = token.Split(':');
                Assert.That(parts.Length, Is.GreaterThan(tokenIndex), Error(path, row, column, token));
                Assert.That(Enum.TryParse(parts[tokenIndex], false, out T _), Is.True, Error(path, row, column, token));
            }
        }

        private static IEnumerable<string> Tokens(CsvRecord row, Dictionary<string, int> columns, string column)
        {
            if (!columns.TryGetValue(column, out int index) || index >= row.Fields.Count)
                yield break;

            string value = row.Fields[index];
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            string[] tokens = value.Split('|');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[i]))
                    yield return tokens[i].Trim();
            }
        }

        private static Dictionary<string, int> Columns(CsvRecord header)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < header.Fields.Count; i++)
                result[header.Fields[i]] = i;
            return result;
        }

        private static bool IsIgnored(CsvRecord row) =>
            row.Fields.Count == 0 ||
            (row.Fields.Count == 1 && string.IsNullOrWhiteSpace(row.Fields[0])) ||
            row.Fields[0].TrimStart().StartsWith("#", StringComparison.Ordinal);

        private static IEnumerable<string> SplitIds(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;
            string[] values = raw.Split('|');
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i]))
                    yield return values[i].Trim();
        }

        private static void AssertEnum<T>(
            string path,
            CsvRecord row,
            Dictionary<string, int> columns,
            string column,
            bool allowEmpty = false) where T : struct
        {
            Assert.That(columns.TryGetValue(column, out int index), Is.True, $"{Path.GetFileName(path)} missing column {column}.");
            string value = row.Fields[index].Trim();
            if (allowEmpty && value.Length == 0)
                return;
            Assert.That(Enum.TryParse(value, false, out T _), Is.True, Error(path, row, column, value));
        }

        private static string Error(string path, CsvRecord row, string column, string value) =>
            $"{Path.GetFileName(path)} line {row.Line}, {column}: invalid token '{value}'.";

        private static List<CsvRecord> ReadRecords(string csv)
        {
            var result = new List<CsvRecord>();
            var fields = new List<string>();
            var field = new System.Text.StringBuilder();
            bool quoted = false;
            int line = 1;
            int recordLine = 1;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else if ((c == '\r' || c == '\n') && !quoted)
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;
                    fields.Add(field.ToString());
                    field.Clear();
                    result.Add(new CsvRecord(recordLine, fields));
                    fields = new List<string>();
                    line++;
                    recordLine = line;
                }
                else
                {
                    field.Append(c);
                    if (c == '\n')
                        line++;
                }
            }

            Assert.That(quoted, Is.False, "CSV ended inside a quoted field.");
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                result.Add(new CsvRecord(recordLine, fields));
            }
            return result;
        }

        private sealed class CsvRecord
        {
            public readonly int Line;
            public readonly List<string> Fields;

            public CsvRecord(int line, List<string> fields)
            {
                Line = line;
                Fields = fields;
            }
        }
    }
}
