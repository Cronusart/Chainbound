#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PlayerMenuRootBackgroundPrefabUtility
    {
        private static readonly string[] RootPrefabPaths =
        {
            "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab",
            "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab",
        };

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Player Menu Root Background", priority = 41)]
        public static void EnsurePlayerMenuRootBackground()
        {
            int updatedCount = 0;
            for (int i = 0; i < RootPrefabPaths.Length; i++)
            {
                if (EnsureRootPrefabBackground(RootPrefabPaths[i]))
                    updatedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerMenuRootBackgroundPrefabUtility] Ensured editable root PlayerMenuBackground and PlayerMenuBootSequence on {updatedCount} prefab(s).");
        }

        private static bool EnsureRootPrefabBackground(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PlayerMenuRootBackgroundPrefabUtility] Prefab not found: {prefabPath}");
                return false;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            PlayerMenuController controller = instance.GetComponent<PlayerMenuController>();
            if (controller == null)
            {
                Debug.LogWarning($"[PlayerMenuRootBackgroundPrefabUtility] PlayerMenuController was not found on {prefabPath}.");
                PrefabUtility.UnloadPrefabContents(instance);
                return false;
            }

            PlayerMenuBackgroundController background = controller.EnsureRootBackgroundForEditor();
            if (background != null)
                EditorUtility.SetDirty(background);
            PlayerMenuBootSequenceController bootSequence = controller.EnsureRootBootSequenceForEditor();
            if (bootSequence != null)
                EditorUtility.SetDirty(bootSequence);
            EditorUtility.SetDirty(controller);

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            PrefabUtility.UnloadPrefabContents(instance);
            return true;
        }
    }
}
#endif
