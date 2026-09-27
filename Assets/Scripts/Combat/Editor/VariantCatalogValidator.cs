using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Validates body-skin variant ids across visual sets, wild tables, and manual providers.
    /// Menu: Tools/Chainbound/Combat/Validate Variant Catalog
    /// </summary>
    public static class VariantCatalogValidator
    {
        [MenuItem("Tools/Chainbound/Combat/Validate Variant Catalog")]
        public static void ValidateAll()
        {
            var report = new StringBuilder();
            int issues = 0;

            CombatVisualDatabaseSO database = LoadFirst<CombatVisualDatabaseSO>();
            if (database == null)
            {
                EditorUtility.DisplayDialog(
                    "Variant Catalog",
                    "No CombatVisualDatabaseSO found.",
                    "OK");
                return;
            }

            database.RebuildLookup();
            issues += ValidateVisualSets(database, report);
            issues += ValidateWildTables(database, report);
            issues += ValidateEncounterZones(database, report);
            issues += ValidateManualProviders(database, report);

            if (issues == 0)
            {
                Debug.Log("[VariantCatalogValidator] Variant catalog passed validation.");
                EditorUtility.DisplayDialog("Variant Catalog", "Variant catalog passed validation.", "OK");
            }
            else
            {
                Debug.LogWarning($"[VariantCatalogValidator] {issues} issue(s):\n{report}");
                EditorUtility.DisplayDialog("Variant Catalog", $"{issues} issue(s) found. See Console.", "OK");
            }
        }

        private static int ValidateVisualSets(CombatVisualDatabaseSO database, StringBuilder report)
        {
            int issues = 0;
            IReadOnlyList<ChainlingSpeciesVisualSetSO> sets = database.SpeciesVisualSets;
            if (sets == null)
                return 0;

            for (int i = 0; i < sets.Count; i++)
            {
                ChainlingSpeciesVisualSetSO set = sets[i];
                if (set == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(set);
                if (string.IsNullOrWhiteSpace(set.speciesId))
                    Issue(report, ref issues, path, "speciesId empty");

                bool hasBase = false;
                if (set.variants == null)
                    continue;

                var seen = new HashSet<string>();
                for (int v = 0; v < set.variants.Count; v++)
                {
                    SpeciesVisualVariantEntry entry = set.variants[v];
                    if (entry == null)
                        continue;

                    string id = ChainlingVisualIdentity.NormalizeBodySkinVariantId(entry.variantId);
                    if (!ChainlingVisualIdentity.IsBodySkinVariantId(entry.variantId))
                    {
                        Issue(report, ref issues, path, $"variant '{entry.variantId}' is not a body-skin id");
                        continue;
                    }

                    if (!seen.Add(id))
                        Issue(report, ref issues, path, $"duplicate variantId '{id}'");

                    if (string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal))
                        hasBase = true;

                    if (entry.baseProfile == null)
                        Issue(report, ref issues, path, $"variant '{id}' has no baseProfile");
                }

                if (!hasBase)
                    Issue(report, ref issues, path, "missing required 'base' variant");
            }

            return issues;
        }

        private static int ValidateWildTables(CombatVisualDatabaseSO database, StringBuilder report)
        {
            int issues = 0;
            issues += ValidateAssets<WildVariantRarityTableSO>(
                "t:WildVariantRarityTableSO",
                report,
                (table, path, sb) =>
                {
                    int count = 0;
                    if (table.entries == null)
                        return 0;

                    for (int i = 0; i < table.entries.Count; i++)
                    {
                        WildVariantRarityEntry entry = table.entries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.visualVariantId))
                            continue;

                        string id = entry.visualVariantId.Trim();
                        if (!ChainlingVisualIdentity.IsBodySkinVariantId(id))
                        {
                            Issue(sb, ref count, path, $"'{id}' is not a body-skin variant id");
                            continue;
                        }

                        if (!CatalogKnowsVariant(database, id))
                            Issue(sb, ref count, path, $"'{id}' is not registered on any species visual set or database entry");
                    }

                    return count;
                });

            issues += ValidateAssets<WildVariantPoolSO>(
                "t:WildVariantPoolSO",
                report,
                (pool, path, sb) =>
                {
                    int count = 0;
                    if (pool.entries == null)
                        return 0;

                    for (int i = 0; i < pool.entries.Count; i++)
                        ValidateWeightedEntry(database, path, pool.entries[i], sb, ref count);

                    return count;
                });

            return issues;
        }

        private static int ValidateEncounterZones(CombatVisualDatabaseSO database, StringBuilder report)
        {
            return ValidateAssets<EncounterZoneSO>(
                "t:EncounterZoneSO",
                report,
                (zone, path, sb) =>
                {
                    int count = 0;
                    if (zone.entries == null)
                        return 0;

                    for (int i = 0; i < zone.entries.Count; i++)
                    {
                        ZoneEncounterEntry entry = zone.entries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                            continue;

                        ValidateZoneEntryPool(database, path, entry, sb, ref count);
                    }

                    return count;
                });
        }

        private static void ValidateZoneEntryPool(
            CombatVisualDatabaseSO database,
            string path,
            ZoneEncounterEntry entry,
            StringBuilder report,
            ref int issues)
        {
            if (entry.variantRarityTable != null && entry.variantRarityTable.entries != null)
            {
                for (int i = 0; i < entry.variantRarityTable.entries.Count; i++)
                {
                    WildVariantRarityEntry row = entry.variantRarityTable.entries[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.visualVariantId))
                        continue;

                    string id = ChainlingVisualIdentity.NormalizeBodySkinVariantId(row.visualVariantId);
                    if (!database.ContainsBodyVariant(entry.speciesId, id))
                    {
                        Issue(
                            report,
                            ref issues,
                            path,
                            $"species '{entry.speciesId}' rarity table id '{id}' is not in the visual catalog");
                    }
                    else if (database.TryGetCatalogRarity(entry.speciesId, id, out RarityTier catalogRarity) &&
                             catalogRarity != row.rarity)
                    {
                        Issue(
                            report,
                            ref issues,
                            path,
                            $"species '{entry.speciesId}' id '{id}' table rarity {row.rarity} != catalog rarity {catalogRarity}");
                    }
                }
            }

            if (entry.variantPool != null && entry.variantPool.entries != null)
            {
                for (int i = 0; i < entry.variantPool.entries.Count; i++)
                    ValidateSpeciesWeighted(database, path, entry.speciesId, entry.variantPool.entries[i], report, ref issues);
            }

            if (entry.inlineVariantPool != null)
            {
                for (int i = 0; i < entry.inlineVariantPool.Count; i++)
                    ValidateSpeciesWeighted(database, path, entry.speciesId, entry.inlineVariantPool[i], report, ref issues);
            }
        }

        private static int ValidateManualProviders(CombatVisualDatabaseSO database, StringBuilder report)
        {
            int issues = 0;
            issues += ValidateRosterAssets("t:ManualEncounterProviderSO", "roster", database, report);
            issues += ValidateRosterAssets("t:ManualPartyProviderSO", "roster", database, report);
            return issues;
        }

        private static int ValidateRosterAssets(
            string filter,
            string rosterProperty,
            CombatVisualDatabaseSO database,
            StringBuilder report)
        {
            int issues = 0;
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null)
                    continue;

                var so = new SerializedObject(asset);
                SerializedProperty roster = so.FindProperty(rosterProperty);
                if (roster == null || !roster.isArray)
                    continue;

                for (int r = 0; r < roster.arraySize; r++)
                {
                    SerializedProperty entry = roster.GetArrayElementAtIndex(r);
                    string speciesId = entry.FindPropertyRelative("speciesId")?.stringValue;
                    string variantId = entry.FindPropertyRelative("visualIdentity")
                        ?.FindPropertyRelative("visualVariantId")
                        ?.stringValue;
                    if (string.IsNullOrWhiteSpace(speciesId) || string.IsNullOrWhiteSpace(variantId))
                        continue;

                    string id = ChainlingVisualIdentity.NormalizeBodySkinVariantId(variantId);
                    if (!database.ContainsBodyVariant(speciesId, id))
                    {
                        Issue(
                            report,
                            ref issues,
                            path,
                            $"manual '{speciesId}' variant '{id}' is not in the visual catalog");
                    }
                }
            }

            return issues;
        }

        private static void ValidateWeightedEntry(
            CombatVisualDatabaseSO database,
            string path,
            WeightedVariantEntry entry,
            StringBuilder report,
            ref int issues)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.visualVariantId))
                return;

            string id = entry.visualVariantId.Trim();
            if (!ChainlingVisualIdentity.IsBodySkinVariantId(id))
                Issue(report, ref issues, path, $"'{id}' is not a body-skin variant id");
            else if (!CatalogKnowsVariant(database, id))
                Issue(report, ref issues, path, $"'{id}' is not registered on any species visual set or database entry");
        }

        private static void ValidateSpeciesWeighted(
            CombatVisualDatabaseSO database,
            string path,
            string speciesId,
            WeightedVariantEntry entry,
            StringBuilder report,
            ref int issues)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.visualVariantId))
                return;

            string id = ChainlingVisualIdentity.NormalizeBodySkinVariantId(entry.visualVariantId);
            if (!database.ContainsBodyVariant(speciesId, id))
            {
                Issue(
                    report,
                    ref issues,
                    path,
                    $"species '{speciesId}' pool id '{id}' is not in the visual catalog");
            }
        }

        private static bool CatalogKnowsVariant(CombatVisualDatabaseSO database, string variantId)
        {
            string id = ChainlingVisualIdentity.NormalizeBodySkinVariantId(variantId);
            IReadOnlyList<ChainlingSpeciesVisualSetSO> sets = database.SpeciesVisualSets;
            if (sets != null)
            {
                for (int i = 0; i < sets.Count; i++)
                {
                    ChainlingSpeciesVisualSetSO set = sets[i];
                    if (set != null && set.FindVariant(id) != null)
                        return true;
                }
            }

            IReadOnlyList<CombatVisualDatabaseSO.Entry> entries = database.Entries;
            if (entries == null)
                return string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                CombatVisualDatabaseSO.Entry entry = entries[i];
                if (entry == null || entry.entangledType != ChainlingType.None)
                    continue;
                if (string.Equals(
                        ChainlingVisualIdentity.NormalizeBodySkinVariantId(entry.visualVariantId),
                        id,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal);
        }

        private delegate int Validator<T>(T asset, string path, StringBuilder report);

        private static int ValidateAssets<T>(string filter, StringBuilder report, Validator<T> validator)
            where T : UnityEngine.Object
        {
            int issues = 0;
            string[] gids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < gids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(gids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    continue;
                issues += validator(asset, path, report);
            }

            return issues;
        }

        private static T LoadFirst<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null)
                    return asset;
            }

            return null;
        }

        private static void Issue(StringBuilder report, ref int issues, string path, string message)
        {
            issues++;
            report.Append("- ").Append(path).Append(": ").Append(message).Append('\n');
        }
    }
}
