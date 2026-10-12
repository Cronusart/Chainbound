#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(OnScreenKeyboardUI))]
    public class OnScreenKeyboardUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "If Row1–Row4 under KeyboardShell are empty, keys are rebuilt at runtime only and you cannot edit sprites here. " +
                "Click below to persist 46 VirtualKeyButton instances, then save the prefab (Ctrl+S). " +
                "You can also use Tools → Chainbound → UI → Repair On-Screen Keyboard In GameServices Prefab.\n\n" +
                "Label font/layout is not automatic at runtime. After authoring TMP on the key named in Label Style Source Key Name (default Q), " +
                "right‑click the OnScreenKeyboardUI component header → Apply Label Style To All Keys to copy that label to every key (text is preserved). " +
                "For Image + Button (sprite swap, colors), use Apply Key Visual Style To Character Keys (excludes utilities) to match Q on letter keys while keeping Shift, Space, Back, Clear, Cancel, and Confirm distinct. " +
                "Rebuild deletes and recreates all keys from defaults, then reapplies the source key's label style to every key.",
                MessageType.Info);

            var ui = (OnScreenKeyboardUI)target;
            if (GUILayout.Button("Rebuild authored keys"))
            {
                if (!EditorUtility.DisplayDialog(
                        "Rebuild on-screen keyboard keys?",
                        "This deletes every key under RowSymbols and Row1–Row4 and recreates 46 default keys. " +
                        "Custom per-key Button sprites are reset unless you reassign them. " +
                        "TMP on the style source key (Label Style Source Key Name) is cached and reapplied to all keys after rebuild.\n\nContinue?",
                        "Rebuild",
                        "Cancel"))
                {
                    return;
                }

                Undo.RecordObject(ui, "Rebuild On-Screen Keyboard Keys");
                ui.EditorRebuildAuthoredKeys();
                EditorUtility.SetDirty(ui);

                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                    EditorSceneManager.MarkSceneDirty(stage.scene);
                else if (ui.gameObject.scene.IsValid())
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
            }
        }
    }
}
#endif
