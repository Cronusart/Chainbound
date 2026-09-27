#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    public static class CombatPopupVisualProfileGenerator
    {
        private const string ProfilesFolder = "Assets/Data/Combat/PopUps/Profiles";
        private const string CatalogPath = "Assets/Data/Combat/PopUps/VisualCatalog/CombatPopupVisualCatalog.asset";

        [MenuItem("Tools/Chainbound/Combat/Popup/Generate Default Visual Profiles")]
        public static void GenerateDefaultProfilesMenu()
        {
            GenerateDefaultProfiles();
            Debug.Log("[CombatPopupVisualProfileGenerator] Default popup profiles and catalog updated.");
        }

        public static void GenerateDefaultProfiles()
        {
            EnsureFolder(ProfilesFolder);

            var profilesByCategory = new Dictionary<CombatPopupCategory, CombatPopupVisualProfileSO>();
            foreach (CombatPopupCategory category in Enum.GetValues(typeof(CombatPopupCategory)))
            {
                string assetName = $"CombatPopupVisual_{category}.asset";
                string assetPath = $"{ProfilesFolder}/{assetName}";

                var profile = AssetDatabase.LoadAssetAtPath<CombatPopupVisualProfileSO>(assetPath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<CombatPopupVisualProfileSO>();
                    AssetDatabase.CreateAsset(profile, assetPath);
                }

                CombatPopupVisualProfileDefaults.ApplyDefaults(profile, category);
                EditorUtility.SetDirty(profile);
                profilesByCategory[category] = profile;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CombatPopupVisualCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<CombatPopupVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new List<CombatPopupVisualCatalogEntry>();
            foreach (var pair in profilesByCategory)
            {
                entries.Add(new CombatPopupVisualCatalogEntry
                {
                    category = pair.Key,
                    profile = pair.Value,
                });
            }

            SetCatalogEntries(catalog, entries, profilesByCategory[CombatPopupCategory.GenericImpact]);
            EditorUtility.SetDirty(catalog);
            catalog.BuildLookup();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetCatalogEntries(
            CombatPopupVisualCatalogSO catalog,
            List<CombatPopupVisualCatalogEntry> entries,
            CombatPopupVisualProfileSO fallback)
        {
            var entriesField = typeof(CombatPopupVisualCatalogSO).GetField(
                "entries",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var fallbackField = typeof(CombatPopupVisualCatalogSO).GetField(
                "defaultProfile",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            entriesField?.SetValue(catalog, entries);
            fallbackField?.SetValue(catalog, fallback);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
