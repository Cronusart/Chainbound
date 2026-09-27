using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Binds a scene-authored Orb to the capture-attempt director without hard-coding scene logic.
    /// </summary>
    public static class CaptureAttemptOrbBinder
    {
        private const string GlassPrefabPath = "Assets/Prefabs/Combat/Orb_Glass (1).prefab";

        [MenuItem("Tools/Chainbound/Combat/Bind Capture Attempt Orb")]
        public static void Bind()
        {
            CaptureAttemptOrbAnchor anchor = Object.FindFirstObjectByType<CaptureAttemptOrbAnchor>(FindObjectsInactive.Include);
            if (anchor == null)
            {
                Transform orb = FindNamed("Orb")
                    ?? FindNamed("Orb_Glass")
                    ?? FindNamed("Orb_Glass (1)");
                if (orb == null)
                {
                    EditorUtility.DisplayDialog(
                        "Capture Attempt Orb",
                        "No GameObject named Orb / Orb_Glass was found in loaded scenes. Select your authored orb and add CaptureAttemptOrbAnchor, or rename it to Orb.",
                        "OK");
                    return;
                }

                anchor = orb.GetComponent<CaptureAttemptOrbAnchor>();
                if (anchor == null)
                    anchor = Undo.AddComponent<CaptureAttemptOrbAnchor>(orb.gameObject);
            }

            CaptureCommandCoordinatorUGUI coordinator = Object.FindFirstObjectByType<CaptureCommandCoordinatorUGUI>(FindObjectsInactive.Include);
            CaptureAttemptDirector director = coordinator != null
                ? coordinator.GetComponent<CaptureAttemptDirector>()
                : Object.FindFirstObjectByType<CaptureAttemptDirector>(FindObjectsInactive.Include);
            if (director == null && coordinator != null)
                director = Undo.AddComponent<CaptureAttemptDirector>(coordinator.gameObject);

            if (director != null)
            {
                SerializedObject so = new SerializedObject(director);
                so.FindProperty("orbAnchor").objectReferenceValue = anchor;
                GameObject glass = AssetDatabase.LoadAssetAtPath<GameObject>(GlassPrefabPath);
                if (glass != null)
                    so.FindProperty("orbGlassFallbackPrefab").objectReferenceValue = glass;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(director);
            }

            Selection.activeGameObject = anchor.gameObject;
            EditorGUIUtility.PingObject(anchor);
            Debug.Log("[CaptureAttempt] Bound orb '" + anchor.name + "' to the capture-attempt director.", anchor);
        }

        private static Transform FindNamed(string name)
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindRecursive(roots[i].transform, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name)
                return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindRecursive(current.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
