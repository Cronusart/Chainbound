using Chainbound.Combat;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Editor
{
    /// <summary>
    /// Repairs <see cref="OnScreenKeyboardUI"/> key rows inside <c>GameServices.prefab</c> when
    /// the symbol row or Row1–Row4 were cleared: persists 46 <see cref="VirtualKeyButton"/> children so you can assign
    /// custom sprites and Button sprite-swap states in the Inspector (runtime rebuild is skipped).
    /// </summary>
    public static class OnScreenKeyboardGameServicesAuthoring
    {
        private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";

        [MenuItem("Tools/Chainbound/UI/Repair On-Screen Keyboard In GameServices Prefab", false, 221)]
        public static void RepairGameServicesKeyboard()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
            try
            {
                OnScreenKeyboardUI keyboardUi = root.GetComponentInChildren<OnScreenKeyboardUI>(true);
                if (keyboardUi == null)
                {
                    Debug.LogError(
                        "[OnScreenKeyboardGameServicesAuthoring] No OnScreenKeyboardUI under " + GameServicesPrefabPath);
                    return;
                }

                Transform canvasRoot = keyboardUi.transform.Find("KeyboardCanvasRoot");
                if (canvasRoot != null)
                {
                    Vector3 s = canvasRoot.localScale;
                    if (s.sqrMagnitude < 1e-6f)
                    {
                        canvasRoot.localScale = Vector3.one;
                        Debug.Log(
                            "[OnScreenKeyboardGameServicesAuthoring] KeyboardCanvasRoot had zero scale; set to (1,1,1).");
                    }
                }

                keyboardUi.EditorRebuildAuthoredKeys();
                EditorUtility.SetDirty(keyboardUi);

                PrefabUtility.SaveAsPrefabAsset(root, GameServicesPrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[OnScreenKeyboardGameServicesAuthoring] Rebuilt and saved 46 keyboard keys in " +
                    GameServicesPrefabPath +
                    ". Label TMP on the style source key (Label Style Source Key Name, default Q) was reapplied to all keys; " +
                    "assign Default Key Sprite and per-key Button sprite states as needed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
