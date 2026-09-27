#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PartyTabXpLabelPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string ColumnShellPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPartyColumnShell.prefab";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab XP Labels", priority = 46)]
        public static void EnsurePartyTabXpLabels()
        {
            int updated = 0;
            updated += EnsureInPrefab(PartyPrefabPath);
            updated += EnsureInPrefab(ColumnShellPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PartyTabXpLabelPrefabUtility] Ensured XP labels on {updated} General view layout(s).");
        }

        private static int EnsureInPrefab(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PartyTabXpLabelPrefabUtility] Prefab not found: {prefabPath}");
                return 0;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            ChainlingPartyInfoViewLayout[] layouts =
                instance.GetComponentsInChildren<ChainlingPartyInfoViewLayout>(true);
            int count = 0;

            for (int i = 0; i < layouts.Length; i++)
            {
                ChainlingPartyInfoViewLayout layout = layouts[i];
                if (layout == null || layout.ViewMode != ChainlingPartyViewMode.General)
                    continue;

                layout.ApplyDefaultPresentationForMode();
                layout.BuildDefaultLayoutForEditor();
                EditorUtility.SetDirty(layout);
                count++;
            }

            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[PartyTabXpLabelPrefabUtility] Updated {count} General view layout(s) in {prefabPath}.");
            }

            PrefabUtility.UnloadPrefabContents(instance);
            return count;
        }
    }
}
#endif
