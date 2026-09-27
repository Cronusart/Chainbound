#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SignalJam.VisualFx.Editor
{
    public static class VisualTierCatalogBootstrap
    {
        private const string RootFolder = "Assets/Data/UI/VisualFx";

        [MenuItem("Chainbound/VFX/Create Starter Visual Tier Assets")]
        public static void CreateStarterAssets()
        {
            EnsureFolder("Assets/Data/UI");
            EnsureFolder(RootFolder);

            VisualTierPaletteSO palette = CreateAsset<VisualTierPaletteSO>(
                $"{RootFolder}/DefaultVisualTierPalette.asset");

            VisualTierTypographyPresetSO typography = CreateAsset<VisualTierTypographyPresetSO>(
                $"{RootFolder}/DefaultTypographyPreset.asset");
            SetPalette(typography, palette);

            VisualTierMeshPresetSO mesh = CreateAsset<VisualTierMeshPresetSO>(
                $"{RootFolder}/DefaultMeshPreset.asset");
            SetPalette(mesh, palette);

            VisualTierAuraPresetSO aura = CreateAsset<VisualTierAuraPresetSO>(
                $"{RootFolder}/DefaultAuraPreset.asset");
            SetPalette(aura, palette);

            VisualTierProfileSO profile = CreateAsset<VisualTierProfileSO>(
                $"{RootFolder}/DefaultVisualTierProfile.asset");
            SerializedObject profileObject = new SerializedObject(profile);
            profileObject.FindProperty("sharedPalette").objectReferenceValue = palette;
            profileObject.FindProperty("typographyPreset").objectReferenceValue = typography;
            profileObject.FindProperty("meshPreset").objectReferenceValue = mesh;
            profileObject.FindProperty("auraPreset").objectReferenceValue = aura;
            profileObject.ApplyModifiedPropertiesWithoutUndo();

            VisualVariantCatalogSO catalog = CreateAsset<VisualVariantCatalogSO>(
                $"{RootFolder}/DefaultVisualVariantCatalog.asset");
            SerializedObject catalogObject = new SerializedObject(catalog);
            SerializedProperty entries = catalogObject.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("tierId").stringValue = "default";
            entry.FindPropertyRelative("tierIndex").intValue = 0;
            entry.FindPropertyRelative("profile").objectReferenceValue = profile;
            catalogObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = catalog;

            Debug.Log(
                "[VisualTier] Starter assets created under Assets/Data/UI/VisualFx. " +
                "Duplicate presets per tier and assign a shared palette for consistent tokens.");
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetPalette(Object preset, VisualTierPaletteSO palette)
        {
            SerializedObject serialized = new SerializedObject(preset);
            serialized.FindProperty("palette").objectReferenceValue = palette;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
