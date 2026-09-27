#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PartyTabStarRatingPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string ColumnShellPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPartyColumnShell.prefab";
        private const string StarRatingArtPath = "Assets/Data/UI/PartyTabStarRatingArt.asset";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Info Panel Layouts", priority = 42)]
        public static void EnsureInfoPanelLayoutsInMenuPrefabs()
        {
            EnsureStarRatingLayoutInMenuPrefabs();
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Star Rating Layout", priority = 43)]
        public static void EnsureStarRatingLayoutInMenuPrefabs()
        {
            PartyTabStarRatingArtSO art = AssetDatabase.LoadAssetAtPath<PartyTabStarRatingArtSO>(StarRatingArtPath);
            int updated = 0;
            updated += EnsureInPrefab(PartyPrefabPath, art);
            updated += EnsureInPrefab(ColumnShellPrefabPath, art);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PartyTabStarRatingPrefabUtility] Ensured star rating and per-view info layouts on {updated} info panel binder(s). " +
                "Edit Views/*View, StarRating, and StarRowLayout in the prefab to tune each mode.");
        }

        private static int EnsureInPrefab(string prefabPath, PartyTabStarRatingArtSO art)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PartyTabStarRatingPrefabUtility] Prefab not found: {prefabPath}");
                return 0;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            ChainlingPartyInfoPanelBinder[] binders = instance.GetComponentsInChildren<ChainlingPartyInfoPanelBinder>(true);
            int count = 0;

            for (int i = 0; i < binders.Length; i++)
            {
                ChainlingPartyInfoPanelBinder binder = binders[i];
                if (art != null)
                    binder.ConfigureStarRatingArt(art);
                binder.BuildAuthoredViewLayoutsForEditor();
                EditorUtility.SetDirty(binder);
                count++;
            }

            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[PartyTabStarRatingPrefabUtility] Updated {count} binder(s) in {prefabPath}.");
            }

            PrefabUtility.UnloadPrefabContents(instance);
            return count;
        }
    }

    [CustomEditor(typeof(ChainlingPartyInfoPanelBinder))]
    public sealed class ChainlingPartyInfoPanelBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Info Panel Layouts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates or repairs the authored per-view roots under Views: General, Attributes, Moves, Detail, Loadout, and Stats. " +
                "When authored layouts are enabled, root-level legacy fallback pieces such as StarRating and XpBarBackground are hidden.",
                MessageType.Info);
            if (GUILayout.Button("Ensure Info Panel Layouts"))
            {
                var binder = (ChainlingPartyInfoPanelBinder)target;
                Undo.RegisterFullObjectHierarchyUndo(binder.gameObject, "Ensure Info Panel Layouts");
                binder.BuildAuthoredViewLayoutsForEditor();
                EditorUtility.SetDirty(binder);
            }
        }
    }
}
#endif
