#if UNITY_EDITOR
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PartyTabSectionChromePrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party Tab Section Chrome", priority = 43)]
        public static void EnsureSectionChromeInPartyPrefab()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PartyTabSectionChromePrefabUtility] Prefab not found: {PartyPrefabPath}");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            int updated = EnsureSectionChrome(instance);
            if (updated > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, PartyPrefabPath);
                Debug.Log("[PartyTabSectionChromePrefabUtility] Ensured party tab section chrome on ChainlingsPartyView.prefab.");
            }

            PrefabUtility.UnloadPrefabContents(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static int EnsureSectionChrome(GameObject root)
        {
            if (root == null)
                return 0;

            Transform safeContent = root.transform.Find("SafeContent");
            if (safeContent == null)
            {
                Debug.LogWarning("[PartyTabSectionChromePrefabUtility] SafeContent was not found.");
                return 0;
            }

            RectTransform viewRail = safeContent.Find("ViewRail") as RectTransform;
            RectTransform columnsRoot = safeContent.Find("ColumnsRoot") as RectTransform;
            if (viewRail == null || columnsRoot == null)
            {
                Debug.LogWarning("[PartyTabSectionChromePrefabUtility] ViewRail or ColumnsRoot was not found.");
                return 0;
            }

            ChainlingPartySectionFramePalette palette = columnsRoot.GetComponent<ChainlingPartySectionFramePalette>();
            if (palette == null)
                palette = columnsRoot.gameObject.AddComponent<ChainlingPartySectionFramePalette>();

            ChainlingPartySectionChrome chrome = safeContent.GetComponent<ChainlingPartySectionChrome>();
            if (chrome == null)
                chrome = safeContent.gameObject.AddComponent<ChainlingPartySectionChrome>();

            chrome.Configure(viewRail, columnsRoot);
            if (chrome.HasAuthoringFrames)
                chrome.PrepareAuthoredRuntime();
            else
                chrome.BuildDefaultLayoutForEditor();
            EditorUtility.SetDirty(chrome);
            EditorUtility.SetDirty(palette);
            return 1;
        }
    }

    [CustomEditor(typeof(ChainlingPartySectionChrome))]
    public sealed class ChainlingPartySectionChromeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Rebuild Section Chrome Layout"))
            {
                var chrome = (ChainlingPartySectionChrome)target;
                Undo.RegisterFullObjectHierarchyUndo(chrome.gameObject, "Rebuild Section Chrome Layout");
                chrome.RefreshFrameRects(force: true);
                chrome.SetActivePhase(ChainlingPartyNavigationPhase.ViewSection);
                EditorUtility.SetDirty(chrome);
            }
        }
    }
}
#endif
