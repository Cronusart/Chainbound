using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(CombatVisualDatabaseSO))]
    public class CombatVisualDatabaseSOEditor : UnityEditor.Editor
    {
        private string newSpeciesId = "";
        private string newVariantId = "";
        private CombatVisualProfileSO sourceProfile;

        private static readonly ChainlingType[] ScaffoldTypes =
        {
            ChainlingType.Node,
            ChainlingType.Gas,
            ChainlingType.Block,
            ChainlingType.Encrypted,
            ChainlingType.Burned,
            ChainlingType.Frozen,
            ChainlingType.Forked,
            ChainlingType.Mined,
            ChainlingType.Staked,
            ChainlingType.Compressed,
            ChainlingType.Bot,
            ChainlingType.Glitched,
            ChainlingType.Shard,
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Variant Tools", EditorStyles.boldLabel);
            newSpeciesId = EditorGUILayout.TextField("Species ID", newSpeciesId);
            newVariantId = EditorGUILayout.TextField("Variant ID", newVariantId);
            sourceProfile = (CombatVisualProfileSO)EditorGUILayout.ObjectField(
                "Source Profile",
                sourceProfile,
                typeof(CombatVisualProfileSO),
                false);

            using (new EditorGUI.DisabledScope(!CanCreateVariant()))
            {
                if (GUILayout.Button("Duplicate Source Profile As Variant"))
                    CreateVariantFromSource();
            }

            using (new EditorGUI.DisabledScope(!CanCreateVariant()))
            {
                if (GUILayout.Button("Ensure Entangled Type Slots"))
                    EnsureEntangledTypeSlots();
            }

            if (GUILayout.Button("Validate Registered Profiles"))
                ValidateProfiles((CombatVisualDatabaseSO)target);

            if (GUILayout.Button("Validate Variant Catalog (all assets)"))
                VariantCatalogValidator.ValidateAll();

            serializedObject.ApplyModifiedProperties();
        }

        private bool CanCreateVariant()
        {
            return sourceProfile != null &&
                   !string.IsNullOrWhiteSpace(newSpeciesId) &&
                   !string.IsNullOrWhiteSpace(newVariantId);
        }

        private void CreateVariantFromSource()
        {
            CombatVisualProfileSO newProfile = DuplicateProfile(
                sourceProfile,
                newSpeciesId.Trim(),
                newVariantId.Trim(),
                ChainlingType.None);
            if (newProfile == null)
                return;

            var db = (CombatVisualDatabaseSO)target;
            Undo.RecordObject(db, "Add Combat Visual Variant Entry");
            db.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
            {
                speciesId = newSpeciesId.Trim(),
                visualVariantId = newVariantId.Trim(),
                entangledType = ChainlingType.None,
                profile = newProfile,
            });
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[CombatVisualDatabase] Added variant {newSpeciesId.Trim()} / {newVariantId.Trim()} -> {AssetDatabase.GetAssetPath(newProfile)}.");
        }

        private void EnsureEntangledTypeSlots()
        {
            string species = newSpeciesId.Trim();
            string variant = newVariantId.Trim();
            var db = (CombatVisualDatabaseSO)target;
            int created = 0;

            for (int i = 0; i < ScaffoldTypes.Length; i++)
            {
                ChainlingType type = ScaffoldTypes[i];
                if (db.GetProfileExact(species, variant, type) != null)
                    continue;

                CombatVisualProfileSO profile = DuplicateProfile(
                    sourceProfile,
                    species,
                    $"{variant}_{type}",
                    type);
                if (profile == null)
                    continue;

                Undo.RecordObject(db, "Ensure Entangled Type Slots");
                db.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
                {
                    speciesId = species,
                    visualVariantId = variant,
                    entangledType = type,
                    profile = profile,
                });
                created++;
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            db.RebuildLookup();
            Debug.Log(
                $"[CombatVisualDatabase] Ensured entangled type slots for {species}/{variant}. Created {created} profile(s).");
        }

        private static CombatVisualProfileSO DuplicateProfile(
            CombatVisualProfileSO source,
            string speciesId,
            string variantLabel,
            ChainlingType entangledType)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[CombatVisualDatabase] Source profile must be a saved asset.");
                return null;
            }

            string folder = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ??
                            "Assets/Data/Combat/ChainlingSpecies/Visual";
            string species = SanitizeAssetPart(speciesId);
            string variant = SanitizeAssetPart(variantLabel);
            if (entangledType != ChainlingType.None)
            {
                folder = $"{folder}/Entangled";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/") ??
                                    "Assets/Data/Combat/ChainlingSpecies/Visual";
                    string leaf = Path.GetFileName(folder);
                    if (!AssetDatabase.IsValidFolder(folder))
                        AssetDatabase.CreateFolder(parent, leaf);
                }
            }

            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Visual_{species}_{variant}.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogError($"[CombatVisualDatabase] Failed to duplicate profile to {targetPath}.");
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var newProfile = AssetDatabase.LoadAssetAtPath<CombatVisualProfileSO>(targetPath);
            if (newProfile == null)
            {
                Debug.LogError($"[CombatVisualDatabase] Duplicated asset was not a {nameof(CombatVisualProfileSO)}.");
                return null;
            }

            Undo.RecordObject(newProfile, "Configure Combat Visual Variant Profile");
            newProfile.speciesId = speciesId;
            string typeSuffix = entangledType == ChainlingType.None ? "" : $" {entangledType}";
            newProfile.displayName = string.IsNullOrWhiteSpace(source.displayName)
                ? $"{speciesId} {variantLabel}{typeSuffix}"
                : $"{source.displayName} {variantLabel}{typeSuffix}";
            EditorUtility.SetDirty(newProfile);
            return newProfile;
        }

        private static string SanitizeAssetPart(string value)
        {
            string trimmed = string.IsNullOrWhiteSpace(value) ? "Variant" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(invalid, '_');

            return trimmed.Replace(' ', '_');
        }

        private static void ValidateProfiles(CombatVisualDatabaseSO database)
        {
            if (database == null)
                return;

            int issueCount = 0;
            var keys = new HashSet<string>();
            IReadOnlyList<CombatVisualDatabaseSO.Entry> entries = database.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                CombatVisualDatabaseSO.Entry entry = entries[i];
                if (entry == null)
                {
                    issueCount++;
                    Debug.LogWarning($"[CombatVisualDatabase] Entry {i} is null.", database);
                    continue;
                }

                string speciesId = ChainlingVisualIdentity.NormalizeId(entry.speciesId, "");
                string variantId = ChainlingVisualIdentity.NormalizeBodySkinVariantId(entry.visualVariantId);
                string key = $"{speciesId}:{variantId}:{entry.entangledType}";
                if (!keys.Add(key))
                {
                    issueCount++;
                    Debug.LogWarning($"[CombatVisualDatabase] Duplicate entry for {key}.", database);
                }

                CombatVisualProfileSO profile = entry.profile;
                if (profile == null)
                {
                    issueCount++;
                    Debug.LogWarning($"[CombatVisualDatabase] Missing profile for {key}.", database);
                    continue;
                }

                issueCount += ValidateProfile(profile, key);
            }

            issueCount += ValidateVisualSets(database);

            if (issueCount == 0)
                Debug.Log("[CombatVisualDatabase] Validation passed with no issues.", database);
            else
                Debug.LogWarning($"[CombatVisualDatabase] Validation finished with {issueCount} issue(s).", database);
        }

        private static int ValidateProfile(CombatVisualProfileSO profile, string key)
        {
            int issues = 0;
            issues += WarnIfMissing(profile, key, "Idle", profile.idleSequence);
            issues += WarnIfMissing(profile, key, "Melee Strike", profile.meleeAttackSequence);
            issues += WarnIfMissing(profile, key, "Ranged Strike", profile.rangedAttackSequence);
            issues += WarnIfMissing(profile, key, "Hit Reaction", profile.hitSequence);
            issues += WarnIfMissing(profile, key, "Swap In", profile.swapInSequence);
            issues += WarnIfMissing(profile, key, "Swap Out", profile.swapOutSequence);

            if ((profile.koSequence == null || profile.koSequence.Length == 0) &&
                (profile.swapOutSequence == null || profile.swapOutSequence.Length == 0))
            {
                issues++;
                Debug.LogWarning($"[CombatVisualDatabase] {key} has no KO sequence and no swap-out fallback.", profile);
            }

            if (profile.animationAlternates != null)
            {
                var alternateKeys = new HashSet<string>();
                for (int i = 0; i < profile.animationAlternates.Count; i++)
                {
                    CombatVisualAnimationAlternate alternate = profile.animationAlternates[i];
                    string alternateKey = $"{alternate.slot}:{alternate.StyleId}";
                    if (!alternateKeys.Add(alternateKey))
                    {
                        issues++;
                        Debug.LogWarning($"[CombatVisualDatabase] {key} has duplicate alternate {alternateKey}.", profile);
                    }

                    if (!alternate.HasSequence)
                    {
                        issues++;
                        Debug.LogWarning(
                            $"[CombatVisualDatabase] {key} alternate {alternateKey} has no sprite sequence.",
                            profile);
                    }
                }
            }

            return issues;
        }

        private static int ValidateVisualSets(CombatVisualDatabaseSO database)
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

                if (string.IsNullOrWhiteSpace(set.speciesId))
                {
                    issues++;
                    Debug.LogWarning($"[CombatVisualDatabase] Visual set {i} has empty speciesId.", set);
                }

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
                        issues++;
                        Debug.LogWarning(
                            $"[CombatVisualDatabase] '{set.speciesId}' variant '{entry.variantId}' is not a body-skin id.",
                            set);
                        continue;
                    }

                    if (!seen.Add(id))
                    {
                        issues++;
                        Debug.LogWarning($"[CombatVisualDatabase] '{set.speciesId}' duplicate variantId '{id}'.", set);
                    }

                    if (string.Equals(id, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal))
                        hasBase = true;

                    if (entry.baseProfile == null)
                    {
                        issues++;
                        Debug.LogWarning($"[CombatVisualDatabase] '{set.speciesId}' variant '{id}' has no baseProfile.", set);
                    }
                }

                if (!hasBase)
                {
                    issues++;
                    Debug.LogWarning($"[CombatVisualDatabase] '{set.speciesId}' is missing required 'base' variant.", set);
                }
            }

            return issues;
        }

        private static int WarnIfMissing(UnityEngine.Object context, string key, string label, SpriteAnimationSequence sequence)
        {
            if (sequence != null && sequence.Length > 0)
                return 0;

            Debug.LogWarning($"[CombatVisualDatabase] {key} is missing {label} sprites.", context);
            return 1;
        }
    }
}
