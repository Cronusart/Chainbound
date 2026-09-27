using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Editor utility to create the Combat test harness scene.
    /// Menu: Tools/Chainbound/Combat/Create Test Harness Scene
    /// </summary>
    public static class Combat2SceneSetup
    {
        [MenuItem("Tools/Chainbound/Combat/Create Test Harness Scene")]
        public static void CreateTestHarnessScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create test harness GameObject
            GameObject harnessGO = new GameObject("Combat2TestHarness");
            harnessGO.AddComponent<Combat2TestHarness>();

            // Create a simple directional light if not present
            if (GameObject.FindObjectOfType<Light>() == null)
            {
                GameObject lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Save scene
            string scenePath = "Assets/Game/Combat/Scenes/Combat2_TestHarness.unity";

            // Ensure Scenes directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Game/Combat/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/Game/Combat", "Scenes");
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Combat] Test harness scene created at: {scenePath}");

            EditorUtility.DisplayDialog("Combat Scene Setup",
                $"Test harness scene created at:\n{scenePath}\n\n" +
                "A Combat2TestHarness component has been added.\n" +
                "Assign database SOs in the Inspector if available,\n" +
                "or use the built-in test data by leaving fields empty.",
                "OK");
        }
    }
}
