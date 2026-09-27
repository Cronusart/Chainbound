using System.Collections.Generic;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>One-shot, idempotent authoring for repeatable progression pools and the combat folio overlay.</summary>
    public static class ProgressionFolioAuthoringUtility
    {
        public const string PrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        public const string ProfilePath = "Assets/Data/Combat/UI/ProgressionFolioPresentation.asset";
        public const string GlitchMaterialPath = "Assets/Sprites/Materials/Signal_Glitch_UI_PlayerMenu.mat";

        [MenuItem("Chainbound/Combat/Author Progression Folio Reveal")]
        public static string AuthorAll()
        {
            int migrated = MigrateSpeciesPools();
            ProgressionFolioPresentationSO profile = EnsureProfile();
            AuthorPrefab(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[]
            {
                "Assets/Data/Combat/ChainlingProgressionRewardTables.asset",
            });
            AssetDatabase.Refresh();
            string result = $"Progression folio authoring complete. Migrated {migrated} move-pool entries; profile and prefab are ready.";
            Debug.Log($"[ProgressionFolioAuthoring] {result}");
            return result;
        }

        private static int MigrateSpeciesPools()
        {
            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:SpeciesDefinitionSO", new[] { "Assets/Data/Combat/ChainlingSpecies" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SpeciesDefinitionSO species = AssetDatabase.LoadAssetAtPath<SpeciesDefinitionSO>(path);
                if (species == null)
                    continue;

                SerializedObject serialized = new SerializedObject(species);
                SerializedProperty pool = serialized.FindProperty("progressionMovePool");
                if (pool == null || !pool.isArray)
                    continue;

                var exclusiveMoves = new HashSet<MoveDefinitionSO>();
                if (species.exclusiveMoves != null)
                {
                    for (int exclusiveIndex = 0; exclusiveIndex < species.exclusiveMoves.Count; exclusiveIndex++)
                    {
                        if (species.exclusiveMoves[exclusiveIndex] != null)
                            exclusiveMoves.Add(species.exclusiveMoves[exclusiveIndex]);
                    }
                }

                for (int entryIndex = 0; entryIndex < pool.arraySize; entryIndex++)
                {
                    SerializedProperty entry = pool.GetArrayElementAtIndex(entryIndex);
                    SerializedProperty exclude = entry.FindPropertyRelative("excludeIfOwned");
                    if (exclude != null && exclude.boolValue)
                    {
                        exclude.boolValue = false;
                        changed++;
                    }

                    SerializedProperty move = entry.FindPropertyRelative("move");
                    SerializedProperty poolTag = entry.FindPropertyRelative("poolTag");
                    if (move != null && poolTag != null &&
                        move.objectReferenceValue is MoveDefinitionSO moveDefinition &&
                        exclusiveMoves.Contains(moveDefinition) &&
                        poolTag.enumValueIndex != (int)ProgressionPoolTag.Exclusive)
                    {
                        poolTag.enumValueIndex = (int)ProgressionPoolTag.Exclusive;
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(species);
            }
            return changed;
        }

        private static ProgressionFolioPresentationSO EnsureProfile()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Combat/UI"))
                AssetDatabase.CreateFolder("Assets/Data/Combat", "UI");

            ProgressionFolioPresentationSO profile = AssetDatabase.LoadAssetAtPath<ProgressionFolioPresentationSO>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ProgressionFolioPresentationSO>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.uiGlitchMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlitchMaterialPath);
            AudioClip neutralSettle = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Data/Audio/UI/ui_playermenu_open.mp3");
            profile.folioIssuedClip = neutralSettle;
            profile.noFolioClip = neutralSettle;
            profile.cardSlideSound = AssetDatabase.LoadAssetAtPath<SoundEventSO>("Assets/Data/Audio/SFX/LIBRARY_CARD_SLIDE.asset");
            profile.cardRiserSound = AssetDatabase.LoadAssetAtPath<SoundEventSO>("Assets/Data/Audio/SFX/REVEAL_RISER.asset");
            profile.libraryCardFront = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_frontUI.png");
            profile.libraryCardOutline = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_outline.png");
            profile.rarityColors = new List<FolioRarityColor>
            {
                new FolioRarityColor { rarity = RarityTier.Common, color = new Color(0.42f, 0.36f, 0.27f, 1f) },
                new FolioRarityColor { rarity = RarityTier.Uncommon, color = new Color(0.22f, 0.45f, 0.29f, 1f) },
                new FolioRarityColor { rarity = RarityTier.Rare, color = new Color(0.18f, 0.36f, 0.58f, 1f) },
                new FolioRarityColor { rarity = RarityTier.Epic, color = new Color(0.48f, 0.25f, 0.58f, 1f) },
                new FolioRarityColor { rarity = RarityTier.Legendary, color = new Color(0.68f, 0.39f, 0.12f, 1f) },
                new FolioRarityColor { rarity = RarityTier.Mythic, color = new Color(0.61f, 0.17f, 0.20f, 1f) },
            };
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void AuthorPrefab(ProgressionFolioPresentationSO profile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                CombatPostBattleReportController report = root.GetComponentInChildren<CombatPostBattleReportController>(true);
                if (report == null)
                    throw new System.InvalidOperationException("CombatPostBattleReportController was not found in CombatBattleCanvas.prefab.");

                SerializedObject reportSerialized = new SerializedObject(report);
                SerializedProperty overlayProperty = reportSerialized.FindProperty("overlayRoot");
                GameObject overlay = overlayProperty != null ? overlayProperty.objectReferenceValue as GameObject : null;
                if (overlay == null)
                    throw new System.InvalidOperationException("The authored post-battle overlay root is not assigned.");

                CombatProgressionFolioRevealView reveal = null;
                Transform existing = overlay.transform.Find("ProgressionFolioOverlay");
                if (existing != null)
                    reveal = existing.GetComponent<CombatProgressionFolioRevealView>();

                if (reveal == null)
                    reveal = CombatProgressionFolioRevealView.Create(overlay.transform, null);
                reveal.EnsureAuthoredLayout(null);
                reveal.transform.SetAsLastSibling();
                reveal.gameObject.SetActive(false);

                reportSerialized.FindProperty("progressionFolioReveal").objectReferenceValue = reveal;
                reportSerialized.FindProperty("progressionFolioPresentation").objectReferenceValue = profile;
                reportSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(report);
                EditorUtility.SetDirty(reveal);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
