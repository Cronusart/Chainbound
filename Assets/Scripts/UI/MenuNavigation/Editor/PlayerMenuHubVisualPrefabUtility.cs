#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class PlayerMenuHubVisualPrefabUtility
    {
        private const string PlayerMenuRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        private const string SandboxPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";
        private const string TransitionMaterialPath = "Assets/Sprites/Materials/Signal_Glitch_UI_PlayerMenu.mat";
        private const string TransitionProfilePath = "Assets/Data/UI/PlayerMenuPanelTransitionProfile.asset";
        private const string TabVisualProfilePath = "Assets/Data/UI/PlayerMenuTabButtonVisualProfile.asset";
        private const string GlitchUiShaderName = "SignalJam/UI/SignalGlitchOverlay";
        private const string GreyButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button.png";
        private const string OrangeButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button.png";
        private const string ShimmerFrameFolder = "Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Hub Styling + Panel Transitions", priority = 42)]
        public static void EnsureHubStylingAndPanelTransitions()
        {
            Material transitionMaterial = EnsureTransitionMaterial();
            PlayerMenuPanelTransitionProfileSO transitionProfile = EnsureTransitionProfile(transitionMaterial);
            PlayerMenuTabButtonVisualProfileSO tabVisualProfile = EnsureTabVisualProfile();

            int updated = 0;
            updated += EnsurePrefab(PlayerMenuRootPrefabPath, transitionProfile, tabVisualProfile) ? 1 : 0;
            updated += EnsurePrefab(SandboxPrefabPath, transitionProfile, tabVisualProfile) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerMenuHubVisualPrefabUtility] Ensured hub visuals and panel transitions on {updated} prefab(s).");
        }

        private static bool EnsurePrefab(
            string prefabPath,
            PlayerMenuPanelTransitionProfileSO transitionProfile,
            PlayerMenuTabButtonVisualProfileSO tabVisualProfile)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null)
            {
                Debug.LogWarning($"[PlayerMenuHubVisualPrefabUtility] Prefab not found: {prefabPath}");
                return false;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PlayerMenuController controller = instance.GetComponent<PlayerMenuController>();
                if (controller == null)
                {
                    Debug.LogWarning($"[PlayerMenuHubVisualPrefabUtility] PlayerMenuController missing on {prefabPath}.");
                    return false;
                }

                PlayerMenuRootTabScreenSyncEditor.SyncController(controller, logEachTab: false);
                PlayerMenuPanelTransitionController transitionController = controller.EnsureRootPanelTransitionForEditor();
                AssignTransitionProfile(transitionController, transitionProfile);
                SetDefaultTab(controller);
                EnsureTabButtonVisuals(controller, tabVisualProfile);

                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static Material EnsureTransitionMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TransitionMaterialPath);
            Shader shader = Shader.Find(GlitchUiShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[PlayerMenuHubVisualPrefabUtility] Shader not found: {GlitchUiShaderName}");
                return material;
            }

            bool created = material == null;
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Signal_Glitch_UI_PlayerMenu"
                };
                AssetDatabase.CreateAsset(material, TransitionMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            if (created)
            {
                material.SetFloat("_GlitchIntensity", 1f);
                material.SetFloat("_SliceDisplacement", 0.1f);
                material.SetFloat("_SliceBandCount", 64f);
                material.SetFloat("_ScanlineCount", 32f);
                material.SetFloat("_ScanlineSpeed", 4f);
                material.SetFloat("_ScanlineAlpha", 0.555f);
                material.SetFloat("_StaticAmount", 0.8f);
                material.SetFloat("_DissolveThreshold", 0f);
                material.SetColor("_Color", new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f));
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static PlayerMenuPanelTransitionProfileSO EnsureTransitionProfile(Material material)
        {
            PlayerMenuPanelTransitionProfileSO profile =
                AssetDatabase.LoadAssetAtPath<PlayerMenuPanelTransitionProfileSO>(TransitionProfilePath);
            bool created = profile == null;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlayerMenuPanelTransitionProfileSO>();
                AssetDatabase.CreateAsset(profile, TransitionProfilePath);
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("glitchMaterial").objectReferenceValue = material;
            if (created)
            {
                serialized.FindProperty("fadeOutDuration").floatValue = 0.14f;
                serialized.FindProperty("swapHoldDuration").floatValue = 0.02f;
                serialized.FindProperty("fadeInDuration").floatValue = 0.16f;
                serialized.FindProperty("peakGlitchIntensity").floatValue = 1f;
                serialized.FindProperty("sliceDisplacement").floatValue = GetMaterialFloat(material, "_SliceDisplacement", 0.1f);
                serialized.FindProperty("sliceBandCount").floatValue = GetMaterialFloat(material, "_SliceBandCount", 64f);
                serialized.FindProperty("scanlineCount").floatValue = GetMaterialFloat(material, "_ScanlineCount", 32f);
                serialized.FindProperty("scanlineSpeed").floatValue = GetMaterialFloat(material, "_ScanlineSpeed", 4f);
                serialized.FindProperty("scanlineAlpha").floatValue = GetMaterialFloat(material, "_ScanlineAlpha", 0.555f);
                serialized.FindProperty("staticAmount").floatValue = GetMaterialFloat(material, "_StaticAmount", 0.8f);
                serialized.FindProperty("dissolveThreshold").floatValue = GetMaterialFloat(material, "_DissolveThreshold", 0f);
                serialized.FindProperty("tint").colorValue = material != null && material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static float GetMaterialFloat(Material material, string propertyName, float fallback)
        {
            return material != null && material.HasProperty(propertyName)
                ? material.GetFloat(propertyName)
                : fallback;
        }

        private static PlayerMenuTabButtonVisualProfileSO EnsureTabVisualProfile()
        {
            PlayerMenuTabButtonVisualProfileSO profile =
                AssetDatabase.LoadAssetAtPath<PlayerMenuTabButtonVisualProfileSO>(TabVisualProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PlayerMenuTabButtonVisualProfileSO>();
                AssetDatabase.CreateAsset(profile, TabVisualProfilePath);
            }

            Sprite grey = AssetDatabase.LoadAssetAtPath<Sprite>(GreyButtonPath);
            Sprite orange = AssetDatabase.LoadAssetAtPath<Sprite>(OrangeButtonPath);
            List<Sprite> shimmerFrames = LoadShimmerFrames();

            SerializedObject serialized = new SerializedObject(profile);
            serialized.FindProperty("normalSprite").objectReferenceValue = grey;
            serialized.FindProperty("selectedSprite").objectReferenceValue = orange;
            serialized.FindProperty("lockedSprite").objectReferenceValue = grey;
            serialized.FindProperty("imageType").enumValueIndex = (int)Image.Type.Sliced;
            serialized.FindProperty("preserveAspect").boolValue = false;
            serialized.FindProperty("normalColor").colorValue = Color.white;
            serialized.FindProperty("selectedColor").colorValue = Color.white;
            serialized.FindProperty("lockedColor").colorValue = new Color(0.55f, 0.55f, 0.55f, 1f);
            serialized.FindProperty("selectedScale").floatValue = 1.05f;
            serialized.FindProperty("focusDuration").floatValue = 0.14f;
            serialized.FindProperty("unfocusDuration").floatValue = 0.14f;
            serialized.FindProperty("pressScale").floatValue = 0.94f;
            serialized.FindProperty("pressDuration").floatValue = 0.08f;
            serialized.FindProperty("useUnscaledTime").boolValue = true;

            SerializedProperty frames = serialized.FindProperty("shimmerFrames");
            if (!HasAssignedFrames(frames))
            {
                frames.arraySize = shimmerFrames.Count;
                for (int i = 0; i < shimmerFrames.Count; i++)
                    frames.GetArrayElementAtIndex(i).objectReferenceValue = shimmerFrames[i];
            }

            serialized.FindProperty("shimmerFps").floatValue = 12f;
            serialized.FindProperty("shimmerPauseDuration").floatValue = 0.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void AssignTransitionProfile(
            PlayerMenuPanelTransitionController transitionController,
            PlayerMenuPanelTransitionProfileSO profile)
        {
            if (transitionController == null || profile == null)
                return;

            SerializedObject serialized = new SerializedObject(transitionController);
            serialized.FindProperty("profile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(transitionController);
        }

        private static void SetDefaultTab(PlayerMenuController controller)
        {
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("defaultTabId").stringValue = "chainlings";
            int chainlingsIndex = controller.ResolveTabIndex("chainlings");
            if (chainlingsIndex >= 0)
                serialized.FindProperty("defaultTabIndex").intValue = chainlingsIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureTabButtonVisuals(
            PlayerMenuController controller,
            PlayerMenuTabButtonVisualProfileSO profile)
        {
            if (controller == null)
                return;

            MenuTabBar tabBar = controller.GetComponentInChildren<MenuTabBar>(true);
            if (tabBar == null)
                return;

            SerializedObject tabBarSerialized = new SerializedObject(tabBar);
            SerializedProperty preferVisuals = tabBarSerialized.FindProperty("preferButtonVisualComponents");
            if (preferVisuals != null)
                preferVisuals.boolValue = true;
            tabBarSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tabBar);

            MenuTabDefinitionSO[] definitions = controller.GetTabDefinitions();
            int count = definitions != null ? definitions.Length : 0;
            for (int i = 0; i < count; i++)
            {
                Button button = tabBar.GetButton(i);
                if (button == null)
                    continue;

                PlayerMenuTabButtonVisual visual = button.GetComponent<PlayerMenuTabButtonVisual>();
                if (visual == null)
                    visual = button.gameObject.AddComponent<PlayerMenuTabButtonVisual>();

                visual.EnsureShimmerChildForEditor();
                SerializedObject serialized = new SerializedObject(visual);
                serialized.FindProperty("profile").objectReferenceValue = profile;
                serialized.FindProperty("buttonImage").objectReferenceValue = button.GetComponent<Image>();
                serialized.FindProperty("label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
                serialized.FindProperty("motionTarget").objectReferenceValue = button.transform as RectTransform;
                SerializedProperty preserveLabel = serialized.FindProperty("preserveAuthoredLabelAppearance");
                if (preserveLabel != null)
                    preserveLabel.boolValue = true;
                Transform shimmer = button.transform.Find("Shimmer");
                serialized.FindProperty("shimmerImage").objectReferenceValue =
                    shimmer != null ? shimmer.GetComponent<Image>() : null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(visual);
            }
        }

        private static List<Sprite> LoadShimmerFrames()
        {
            List<Sprite> frames = new List<Sprite>();
            for (int i = 1; i <= 11; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShimmerFrameFolder}/orange_button_anim{i}.png");
                if (sprite != null)
                    frames.Add(sprite);
            }

            return frames;
        }

        private static bool HasAssignedFrames(SerializedProperty frames)
        {
            if (frames == null || !frames.isArray)
                return false;

            for (int i = 0; i < frames.arraySize; i++)
            {
                if (frames.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    return true;
            }

            return false;
        }
    }
}
#endif
