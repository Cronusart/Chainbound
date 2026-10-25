using TMPro;
using Chainbound.Combat;
using SignalJam.Narrative.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Editor
{
    public static class CombatBattleIntroSetupEditor
    {
        private const string CanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        private const string IntroRootName = "BattleIntroRoot";
        private const string BackdropName = "Backdrop";
        private const string CardName = "IntroCard";
        private const string EyebrowName = "Eyebrow";
        private const string MessageName = "MessageText";
        private const string SkipHintName = "SkipHint";
        private const string BridgeObjectName = "BattleIntroBossDialogueBridge";

        private static readonly Color BackdropColor = new Color(0.02f, 0.04f, 0.08f, 0.72f);
        private static readonly Color CardColor = new Color(0.07f, 0.10f, 0.15f, 0.96f);
        private static readonly Color EyebrowColor = new Color(1.00f, 0.80f, 0.37f, 1f);
        private static readonly Color BodyColor = new Color(0.94f, 0.97f, 1.00f, 1f);
        private static readonly Color HintColor = new Color(0.67f, 0.75f, 0.84f, 1f);

        [MenuItem("GameObject/Chainbound/Combat/Create Battle Intro Panel", false, 24)]
        public static void CreateBattleIntroPanelInScene()
        {
            var introHost = ResolveSceneCanvasRoot(createIfMissing: true);
            if (introHost == null)
                return;

            var hudController = Object.FindObjectOfType<CombatBattleUGUIController>(true);
            var install = InstallOrRepairIntroPanel(introHost, registerUndo: true);
            WireSceneReferences(install.controller, hudController, registerUndo: true);

            Selection.activeGameObject = install.controller.gameObject;
            EditorSceneManager.MarkSceneDirty(introHost.gameObject.scene);
            Debug.Log("[CombatBattleIntroSetupEditor] Battle intro panel installed in the open scene.");
        }

        [MenuItem("Tools/Chainbound/Combat/Install Battle Intro In Open Scene", false, 210)]
        public static void InstallBattleIntroInOpenScene()
        {
            CreateBattleIntroPanelInScene();
        }

        [MenuItem("Tools/Chainbound/Combat/Create Or Update Combat Battle Canvas Prefab", false, 211)]
        public static void CreateOrUpdateCombatBattleCanvasPrefab()
        {
            CombatBattleCanvasSplitEditor.SplitCombatBattleCanvasPrefab(force: false);

            var root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            if (root == null)
            {
                Debug.LogError("[CombatBattleIntroSetupEditor] Failed to load prefab: " + CanvasPrefabPath);
                return;
            }

            try
            {
                var introHost = ResolveIntroHostingRoot(root.transform as RectTransform);
                if (introHost == null)
                {
                    Debug.LogError("[CombatBattleIntroSetupEditor] CombatBattleCanvas prefab is missing an intro hosting root.");
                    return;
                }

                InstallOrRepairIntroPanel(introHost, registerUndo: false);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);

                Debug.Log("[CombatBattleIntroSetupEditor] Combat battle canvas prefab updated with battle intro panel.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static InstallResult InstallOrRepairIntroPanel(RectTransform introHostRoot, bool registerUndo)
        {
            var introRoot = FindDescendant(introHostRoot, IntroRootName);
            if (introRoot == null)
                introRoot = CreateRect(IntroRootName, introHostRoot, registerUndo);

            Stretch(introRoot, 0f, 0f, 1f, 1f);
            introRoot.SetAsLastSibling();
            int uiLayer = LayerMask.NameToLayer("UI");
            introRoot.gameObject.layer = uiLayer >= 0 ? uiLayer : 5;

            var canvasGroup = GetOrAdd<CanvasGroup>(introRoot.gameObject, registerUndo);
            var controller = GetOrAdd<BattleIntroController>(introRoot.gameObject, registerUndo);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var backdrop = FindDescendant(introRoot, BackdropName);
            if (backdrop == null)
                backdrop = CreateRect(BackdropName, introRoot, registerUndo);

            Stretch(backdrop, 0f, 0f, 1f, 1f);
            var backdropImage = GetOrAdd<Image>(backdrop.gameObject, registerUndo);
            backdropImage.color = BackdropColor;
            backdropImage.raycastTarget = false;

            var card = FindDescendant(introRoot, CardName);
            if (card == null)
                card = CreateRect(CardName, introRoot, registerUndo);

            card.anchorMin = new Vector2(0.14f, 0.35f);
            card.anchorMax = new Vector2(0.86f, 0.66f);
            card.offsetMin = new Vector2(0f, 0f);
            card.offsetMax = new Vector2(0f, 0f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one;

            var cardImage = GetOrAdd<Image>(card.gameObject, registerUndo);
            cardImage.color = CardColor;
            cardImage.raycastTarget = false;
            GetOrAdd<Shadow>(card.gameObject, registerUndo);

            var cardLayout = GetOrAdd<VerticalLayoutGroup>(card.gameObject, registerUndo);
            cardLayout.padding = new RectOffset(48, 48, 36, 36);
            cardLayout.spacing = 14f;
            cardLayout.childAlignment = TextAnchor.MiddleCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            var eyebrow = FindOrCreateText(card, EyebrowName, registerUndo);
            ConfigureText(eyebrow, "ENCOUNTER", 30f, FontStyles.Bold, TextAlignmentOptions.Center, EyebrowColor, false, false);

            var message = FindOrCreateText(card, MessageName, registerUndo);
            ConfigureText(message, "A wild Chainling has appeared!", 64f, FontStyles.Bold, TextAlignmentOptions.Center, BodyColor, true, true);
            message.enableAutoSizing = true;
            message.fontSizeMin = 28f;
            message.fontSizeMax = 64f;

            var messageLayout = GetOrAdd<LayoutElement>(message.gameObject, registerUndo);
            messageLayout.minHeight = 110f;

            var hint = FindOrCreateText(card, SkipHintName, registerUndo);
            ConfigureText(hint, "Tap, click, or press Space to skip", 26f, FontStyles.Normal, TextAlignmentOptions.Center, HintColor, true, false);

            ApplyBattleIntroControllerReferences(controller, canvasGroup, message);
            return new InstallResult
            {
                controller = controller,
                root = introRoot,
            };
        }

        private static void WireSceneReferences(BattleIntroController introController, CombatBattleUGUIController hudController, bool registerUndo)
        {
            var presenter = Object.FindObjectOfType<Combat2BattlefieldPresenter>(true);
            var battleController = Object.FindObjectOfType<CombatBattleController>(true);

            if (presenter != null)
            {
                if (registerUndo)
                    Undo.RecordObject(presenter, "Wire Battle Intro Presenter");

                var presenterSo = new SerializedObject(presenter);
                presenterSo.FindProperty("battleIntroController").objectReferenceValue = introController;
                if (hudController != null)
                    presenterSo.FindProperty("hudController").objectReferenceValue = hudController;
                presenterSo.FindProperty("hideHudDuringBattleIntro").boolValue = true;
                presenterSo.FindProperty("useBattleIntroGlitch").boolValue = true;
                presenterSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
            }

            if (battleController != null)
            {
                if (registerUndo)
                    Undo.RecordObject(battleController, "Wire Battle Intro Controller");

                var battleSo = new SerializedObject(battleController);
                if (presenter != null)
                    battleSo.FindProperty("presenter").objectReferenceValue = presenter;
                battleSo.FindProperty("waitForBattleIntro").boolValue = true;
                battleSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(battleController);
            }

            if (hudController != null && battleController != null)
            {
                if (registerUndo)
                    Undo.RecordObject(hudController, "Wire Battle HUD Controller");

                var hudSo = new SerializedObject(hudController);
                hudSo.FindProperty("battleController").objectReferenceValue = battleController;
                hudSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hudController);
            }

            EnsureBossBridgeInScene(introController, registerUndo);
        }

        private static void EnsureBossBridgeInScene(BattleIntroController introController, bool registerUndo)
        {
            var bridge = Object.FindObjectOfType<BattleIntroBossDialogueBridge>(true);
            if (bridge == null)
            {
                var go = new GameObject(BridgeObjectName);
                if (registerUndo)
                    Undo.RegisterCreatedObjectUndo(go, "Create Battle Intro Boss Dialogue Bridge");

                bridge = go.AddComponent<BattleIntroBossDialogueBridge>();
            }

            if (registerUndo)
                Undo.RecordObject(bridge, "Wire Battle Intro Boss Dialogue Bridge");

            bridge.SetIntroController(introController);
            EditorUtility.SetDirty(bridge);
        }

        private static void ApplyBattleIntroControllerReferences(BattleIntroController controller, CanvasGroup panel, TMP_Text message)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("messagePanel").objectReferenceValue = panel;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("fadeDuration").floatValue = 0.16f;
            so.FindProperty("typewriterCharsPerSecond").floatValue = 42f;
            so.FindProperty("autoAdvanceDelay").floatValue = 1.8f;
            so.FindProperty("useUnscaledTime").boolValue = true;
            so.FindProperty("allowSkip").boolValue = true;
            so.FindProperty("hidePanelOnAwake").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static RectTransform ResolveIntroHostingRoot(RectTransform searchRoot)
        {
            if (searchRoot == null)
                return null;

            var introCanvas = FindDescendant(searchRoot, "CombatIntroCanvas");
            if (introCanvas != null)
            {
                var introSafeArea = FindDescendant(introCanvas, "SafeAreaRoot");
                return introSafeArea != null ? introSafeArea : introCanvas;
            }

            var legacySafeArea = FindDescendant(searchRoot, "SafeAreaRoot");
            return legacySafeArea != null ? legacySafeArea : searchRoot;
        }

        private static RectTransform ResolveSceneCanvasRoot(bool createIfMissing)
        {
            var hud = Object.FindObjectOfType<CombatBattleUGUIController>(true);
            if (hud != null)
                return ResolveIntroHostingRoot(FindPrefabRoot(hud.transform) as RectTransform);

            var hudCanvas = GameObject.Find("CombatHudCanvas");
            if (hudCanvas != null)
                return ResolveIntroHostingRoot(FindPrefabRoot(hudCanvas.transform) as RectTransform);

            var existingCanvas = Object.FindObjectOfType<Canvas>(true);
            if (existingCanvas != null && existingCanvas.GetComponent<CombatBattleUGUIController>() != null)
                return ResolveIntroHostingRoot(FindPrefabRoot(existingCanvas.transform) as RectTransform);

            if (!createIfMissing)
                return null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[CombatBattleIntroSetupEditor] Combat battle canvas prefab not found at " + CanvasPrefabPath);
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[CombatBattleIntroSetupEditor] Failed to instantiate combat battle canvas prefab.");
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Combat Battle Canvas");
            return ResolveIntroHostingRoot(instance.transform as RectTransform);
        }

        private static Transform FindPrefabRoot(Transform node)
        {
            if (node == null)
                return null;

            Transform current = node;
            while (current.parent != null)
                current = current.parent;
            return current;
        }

        private static RectTransform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            var rects = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].name == name)
                    return rects[i];
            }

            return null;
        }

        private static RectTransform CreateRect(string name, Transform parent, bool registerUndo)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (registerUndo)
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text FindOrCreateText(Transform parent, string name, bool registerUndo)
        {
            var child = FindDescendant(parent, name);
            if (child == null)
                child = CreateRect(name, parent, registerUndo);

            return GetOrAdd<TextMeshProUGUI>(child.gameObject, registerUndo);
        }

        private static T GetOrAdd<T>(GameObject target, bool registerUndo) where T : Component
        {
            var existing = target.GetComponent<T>();
            if (existing != null)
                return existing;

            if (registerUndo)
                return Undo.AddComponent<T>(target);

            return target.AddComponent<T>();
        }

        private static void ConfigureText(
            TMP_Text text,
            string value,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color,
            bool wordWrap,
            bool expandWidth)
        {
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = wordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            var layout = text.GetComponent<LayoutElement>();
            if (layout == null)
                layout = text.gameObject.AddComponent<LayoutElement>();

            layout.flexibleWidth = expandWidth ? 1f : 0f;
            layout.minWidth = 0f;
        }

        private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private struct InstallResult
        {
            public BattleIntroController controller;
            public RectTransform root;
        }
    }
}
