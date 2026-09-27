#if UNITY_EDITOR
using System.Collections.Generic;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PartyTabViewRailPrefabUtility
    {
        private const string PartyPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab";
        private const string GreyButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button.png";
        private const string OrangeButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button.png";
        private const string ShimmerFrameFolder = "Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Party View Rail Carousel", priority = 44)]
        public static void EnsureViewRailCarouselAuthoring()
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[PartyTabViewRailPrefabUtility] Prefab not found: {PartyPrefabPath}");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            ChainlingPartyViewRail rail = instance.GetComponentInChildren<ChainlingPartyViewRail>(true);
            if (rail == null)
            {
                Debug.LogWarning("[PartyTabViewRailPrefabUtility] ChainlingPartyViewRail was not found.");
                PrefabUtility.UnloadPrefabContents(instance);
                return;
            }

            ApplyDefaultMainMenuArt(rail);
            rail.BuildDefaultLayoutForEditor();
            EditorUtility.SetDirty(rail);

            PrefabUtility.SaveAsPrefabAsset(instance, PartyPrefabPath);
            PrefabUtility.UnloadPrefabContents(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PartyTabViewRailPrefabUtility] Ensured authored carousel controls on ChainlingsPartyView.prefab.");
        }

        private static void ApplyDefaultMainMenuArt(ChainlingPartyViewRail rail)
        {
            if (rail == null)
                return;

            Sprite grey = AssetDatabase.LoadAssetAtPath<Sprite>(GreyButtonPath);
            Sprite orange = AssetDatabase.LoadAssetAtPath<Sprite>(OrangeButtonPath);
            List<Sprite> shimmerFrames = LoadShimmerFrames();
            rail.ConfigureCarouselVisuals(
                collapsedSprite: orange,
                normalSprite: grey,
                selectedSprite: orange,
                shimmerFrameSprites: shimmerFrames);
        }

        private static List<Sprite> LoadShimmerFrames()
        {
            var frames = new List<Sprite>();
            for (int i = 1; i <= 11; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShimmerFrameFolder}/orange_button_anim{i}.png");
                if (sprite != null)
                    frames.Add(sprite);
            }

            return frames;
        }
    }

    [CustomEditor(typeof(ChainlingPartyViewRail))]
    public sealed class ChainlingPartyViewRailEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Carousel Authoring", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure Carousel Layout"))
            {
                var rail = (ChainlingPartyViewRail)target;
                Undo.RegisterFullObjectHierarchyUndo(rail.gameObject, "Ensure Party View Rail Carousel Layout");
                rail.BuildDefaultLayoutForEditor();
                EditorUtility.SetDirty(rail);
            }

            if (GUILayout.Button("Apply Main Menu Button Art To Prefab"))
                PartyTabViewRailPrefabUtility.EnsureViewRailCarouselAuthoring();
        }
    }
}
#endif
