using Chainbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Editor
{
    /// <summary>
    /// One-shot authoring for combat SFX components and references across GameServices,
    /// CombatBattleCanvas, and the open combat scene.
    /// </summary>
    public static class CombatSfxPrefabAuthoring
    {
        private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
        private const string CanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        private const string CombatTestHarnessScenePath = "Assets/Scenes/Combat2_TestHarness.unity";
        private const string CatalogAssetPath = "Assets/Data/Combat/Audio/CombatBattleSfxCatalog.asset";

        private const string UiNavigatePath = "Assets/Data/Audio/UI/UI_navigate.asset";
        private const string UiConfirmPath = "Assets/Data/Audio/UI/UI_confirm.asset";
        private const string UiCancelPath = "Assets/Data/Audio/UI/UI_cancel.asset";

        private const string KeyboardPressPath = "Assets/Data/Audio/UI/Keyboard/UI_OSK_Click.asset";
        private const string KeyboardDeniedPath = "Assets/Data/Audio/UI/UI_keyboard_denied.asset";
        private const string KeyboardBackspacePath = "Assets/Data/Audio/UI/Keyboard/UI_OSK_Delete.asset";
        private const string KeyboardConfirmPath = "Assets/Data/Audio/UI/Keyboard/UI_OSK_Enter.asset";
        private const string KeyboardNavigatePath = "Assets/Data/Audio/UI/UI_keyboard_navigate.asset";

        private const string CombatAudioBridgeObjectName = "CombatAudioBridge";
        private const string CombatUiSfxObjectName = "CombatUiSfxPlayer";
        private const string CombatSfxOrchestratorObjectName = "CombatSfxOrchestrator";
        private const string CombatAudioServicesObjectName = "CombatAudioServices";

        [MenuItem("Tools/Chainbound/Combat/SFX/Scaffold Combat SFX Wiring (All)", false, 220)]
        public static void ScaffoldAll()
        {
            ScaffoldGameServicesPrefab();
            ScaffoldCombatBattleCanvasPrefab();
            ScaffoldCombatTestHarnessScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatSfxPrefabAuthoring] Combat SFX scaffold complete.");
        }

        [MenuItem("Tools/Chainbound/Combat/SFX/Scaffold Combat SFX In Combat2 Test Harness", false, 224)]
        public static void ScaffoldCombatTestHarnessMenuItem()
        {
            ScaffoldCombatTestHarnessScene();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch-mode entry point for CI/local automation.</summary>
        public static void ScaffoldAllBatch()
        {
            ScaffoldAll();
            EditorApplication.Exit(0);
        }

        public static void ScaffoldCombatTestHarnessScene()
        {
            if (!System.IO.File.Exists(CombatTestHarnessScenePath))
            {
                Debug.LogWarning("[CombatSfxPrefabAuthoring] Scene not found: " + CombatTestHarnessScenePath);
                return;
            }

            EditorSceneManager.OpenScene(CombatTestHarnessScenePath, OpenSceneMode.Single);
            ScaffoldOpenCombatScene();
            EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("Tools/Chainbound/Combat/SFX/Scaffold Combat SFX In GameServices Prefab", false, 221)]
        public static void ScaffoldGameServicesMenuItem()
        {
            ScaffoldGameServicesPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Chainbound/Combat/SFX/Scaffold Combat SFX In Combat Battle Canvas Prefab", false, 222)]
        public static void ScaffoldCombatBattleCanvasMenuItem()
        {
            ScaffoldCombatBattleCanvasPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Chainbound/Combat/SFX/Scaffold Combat SFX In Open Scene", false, 223)]
        public static void ScaffoldOpenCombatSceneMenuItem()
        {
            ScaffoldOpenCombatScene();
            AssetDatabase.SaveAssets();
        }

        public static void ScaffoldGameServicesPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
            if (root == null)
            {
                Debug.LogError("[CombatSfxPrefabAuthoring] Failed to load " + GameServicesPrefabPath);
                return;
            }

            try
            {
                CombatAudioBridge bridge = EnsureChildComponent<CombatAudioBridge>(
                    root.transform,
                    CombatAudioBridgeObjectName);

                CombatSfxOrchestrator orchestrator = root.GetComponentInChildren<CombatSfxOrchestrator>(true);
                if (orchestrator == null)
                    orchestrator = EnsureChildComponent<CombatSfxOrchestrator>(root.transform, CombatSfxOrchestratorObjectName);

                CombatBattleSfxCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CombatBattleSfxCatalogSO>(CatalogAssetPath);
                if (catalog != null)
                    ApplyOrchestratorReferences(orchestrator, catalog, bridge);
                else
                    Debug.LogWarning("[CombatSfxPrefabAuthoring] Missing catalog; existing prefab catalog reference was preserved.");

                CombatUiSfxPlayer uiSfx = EnsureChildComponent<CombatUiSfxPlayer>(
                    root.transform,
                    CombatUiSfxObjectName);

                ApplyUiSfxPlayerReferences(uiSfx);

                OnScreenKeyboardService keyboardService = root.GetComponentInChildren<OnScreenKeyboardService>(true);
                if (keyboardService != null)
                    ApplyOnScreenKeyboardSfxReferences(keyboardService, uiSfx, bridge);

                PrefabUtility.SaveAsPrefabAsset(root, GameServicesPrefabPath);
                Debug.Log("[CombatSfxPrefabAuthoring] Updated " + GameServicesPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ScaffoldCombatBattleCanvasPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            if (root == null)
            {
                Debug.LogError("[CombatSfxPrefabAuthoring] Failed to load " + CanvasPrefabPath);
                return;
            }

            try
            {
                Transform hudCanvas = root.transform.Find("CombatHudCanvas");
                GameObject hudHost = hudCanvas != null ? hudCanvas.gameObject : root;

                CombatUiSfxPlayer uiSfx = hudHost.GetComponent<CombatUiSfxPlayer>();
                if (uiSfx == null)
                    uiSfx = hudHost.AddComponent<CombatUiSfxPlayer>();

                ApplyUiSfxPlayerReferences(uiSfx);
                EditorUtility.SetDirty(uiSfx);

                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log("[CombatSfxPrefabAuthoring] Updated " + CanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ScaffoldOpenCombatScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[CombatSfxPrefabAuthoring] No active scene loaded; skipping scene wiring.");
                return;
            }

            EnsureAudioServiceExists();

            CombatBattleSfxCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CombatBattleSfxCatalogSO>(CatalogAssetPath);
            if (catalog == null)
            {
                Debug.LogError("[CombatSfxPrefabAuthoring] Missing catalog asset at " + CatalogAssetPath);
                return;
            }

            CombatAudioBridge bridge = Object.FindObjectOfType<CombatAudioBridge>(true);
            if (bridge == null)
            {
                Transform servicesRoot = EnsureSceneRoot(CombatAudioServicesObjectName);
                bridge = EnsureChildComponent<CombatAudioBridge>(servicesRoot, CombatAudioBridgeObjectName);
            }

            Transform audioRoot = bridge.transform.parent != null
                ? bridge.transform.parent
                : EnsureSceneRoot(CombatAudioServicesObjectName);

            if (bridge.transform.parent != audioRoot)
                bridge.transform.SetParent(audioRoot, false);

            CombatSfxOrchestrator orchestrator = Object.FindObjectOfType<CombatSfxOrchestrator>(true);
            if (orchestrator == null)
                orchestrator = EnsureChildComponent<CombatSfxOrchestrator>(audioRoot, CombatSfxOrchestratorObjectName);

            ApplyOrchestratorReferences(orchestrator, catalog, bridge);

            CombatUiSfxPlayer canvasUiSfx = ResolveCanvasUiSfxPlayer();

            WirePresenter(Object.FindObjectOfType<Combat2BattlefieldPresenter>(true), catalog, orchestrator, bridge);
            WirePostBattleRouter(Object.FindObjectOfType<CombatPostBattleRouter>(true), catalog, orchestrator);
            WireSelectionCoordinator(Object.FindObjectOfType<CombatSelectionCoordinatorUGUI>(true), canvasUiSfx);
            WireMoveSelectPanels(Object.FindObjectsOfType<CombatMoveSelectPanelUGUI>(true), canvasUiSfx, bridge);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CombatSfxPrefabAuthoring] Updated open scene '" + scene.name + "'.");
        }

        private static void EnsureAudioServiceExists()
        {
            if (Object.FindObjectOfType<AudioService>(true) != null)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[CombatSfxPrefabAuthoring] AudioService not found and GameServices prefab is missing. " +
                    "Combat SFX will not play until AudioService exists in the scene.");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                Debug.LogWarning("[CombatSfxPrefabAuthoring] Failed to instantiate GameServices prefab for AudioService.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate GameServices For Combat Audio");
            Debug.Log("[CombatSfxPrefabAuthoring] Instantiated GameServices prefab because AudioService was missing.");
        }

        private static CombatUiSfxPlayer ResolveCanvasUiSfxPlayer()
        {
            CombatBattleUGUIController hud = Object.FindObjectOfType<CombatBattleUGUIController>(true);
            if (hud != null)
            {
                CombatUiSfxPlayer existing = hud.GetComponent<CombatUiSfxPlayer>();
                if (existing != null)
                    return existing;

                CombatUiSfxPlayer added = Undo.AddComponent<CombatUiSfxPlayer>(hud.gameObject);
                ApplyUiSfxPlayerReferences(added);
                return added;
            }

            CombatUiSfxPlayer discovered = Object.FindObjectOfType<CombatUiSfxPlayer>(true);
            if (discovered != null)
                return discovered;

            Transform root = EnsureSceneRoot(CombatUiSfxObjectName);
            CombatUiSfxPlayer created = EnsureChildComponent<CombatUiSfxPlayer>(root, CombatUiSfxObjectName);
            ApplyUiSfxPlayerReferences(created);
            return created;
        }

        private static Transform EnsureSceneRoot(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
                return existing.transform;

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created.transform;
        }

        private static T EnsureChildComponent<T>(Transform parent, string objectName) where T : Component
        {
            Transform child = parent.Find(objectName);
            GameObject go = child != null ? child.gameObject : null;
            if (go == null)
            {
                go = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
                go.transform.SetParent(parent, false);
            }

            T component = go.GetComponent<T>();
            if (component == null)
                component = Undo.AddComponent<T>(go);

            return component;
        }

        private static void ApplyUiSfxPlayerReferences(CombatUiSfxPlayer player)
        {
            if (player == null)
                return;

            SerializedObject so = new SerializedObject(player);
            so.FindProperty("navigate").objectReferenceValue = LoadSound(UiNavigatePath);
            so.FindProperty("confirm").objectReferenceValue = LoadSound(UiConfirmPath);
            so.FindProperty("cancel").objectReferenceValue = LoadSound(UiCancelPath);
            so.FindProperty("invalid").objectReferenceValue = LoadSound(UiCancelPath);
            so.FindProperty("targetChanged").objectReferenceValue = LoadSound(UiNavigatePath);
            so.FindProperty("warnIfNotUiBus").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);
        }

        private static void ApplyOnScreenKeyboardSfxReferences(
            OnScreenKeyboardService keyboardService,
            CombatUiSfxPlayer uiSfx,
            CombatAudioBridge bridge)
        {
            if (keyboardService == null)
                return;

            SerializedObject keyboardSo = new SerializedObject(keyboardService);
            keyboardSo.FindProperty("uiSfxBehaviour").objectReferenceValue = uiSfx;
            keyboardSo.FindProperty("audioBridgeBehaviour").objectReferenceValue = bridge;
            keyboardSo.FindProperty("keyPressSound").objectReferenceValue = LoadSound(KeyboardPressPath);
            keyboardSo.FindProperty("inputDeniedSound").objectReferenceValue = LoadSound(KeyboardDeniedPath);
            keyboardSo.FindProperty("backspaceSound").objectReferenceValue = LoadSound(KeyboardBackspacePath);
            keyboardSo.FindProperty("confirmSound").objectReferenceValue = LoadSound(KeyboardConfirmPath);
            keyboardSo.FindProperty("cancelSound").objectReferenceValue = LoadSound(UiCancelPath);
            keyboardSo.FindProperty("navigateSound").objectReferenceValue = LoadSound(KeyboardNavigatePath);
            keyboardSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(keyboardService);
        }

        private static void ApplyOrchestratorReferences(
            CombatSfxOrchestrator orchestrator,
            CombatBattleSfxCatalogSO catalog,
            CombatAudioBridge bridge)
        {
            if (orchestrator == null)
                return;

            SerializedObject so = new SerializedObject(orchestrator);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("audioBridgeBehaviour").objectReferenceValue = bridge;
            so.FindProperty("autoDiscoverAudioBridge").boolValue = true;
            so.FindProperty("enableDebugLogs").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(orchestrator);
        }

        private static void WirePresenter(
            Combat2BattlefieldPresenter presenter,
            CombatBattleSfxCatalogSO catalog,
            CombatSfxOrchestrator orchestrator,
            CombatAudioBridge bridge)
        {
            if (presenter == null)
                return;

            SerializedObject so = new SerializedObject(presenter);
            so.FindProperty("battleSfxCatalog").objectReferenceValue = catalog;
            so.FindProperty("combatSfxOrchestratorBehaviour").objectReferenceValue = orchestrator;
            SerializedProperty bridgeProperty = so.FindProperty("combatAudioBridgeBehaviour");
            if (bridgeProperty != null)
                bridgeProperty.objectReferenceValue = bridge;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static void WirePostBattleRouter(
            CombatPostBattleRouter router,
            CombatBattleSfxCatalogSO catalog,
            CombatSfxOrchestrator orchestrator)
        {
            if (router == null)
                return;

            SerializedObject so = new SerializedObject(router);
            so.FindProperty("battleSfxCatalog").objectReferenceValue = catalog;
            so.FindProperty("combatSfxOrchestratorBehaviour").objectReferenceValue = orchestrator;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(router);
        }

        private static void WireSelectionCoordinator(
            CombatSelectionCoordinatorUGUI coordinator,
            CombatUiSfxPlayer uiSfx)
        {
            if (coordinator == null || uiSfx == null)
                return;

            SerializedObject so = new SerializedObject(coordinator);
            so.FindProperty("uiSfxBehaviour").objectReferenceValue = uiSfx;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);
        }

        private static void WireMoveSelectPanels(
            CombatMoveSelectPanelUGUI[] panels,
            CombatUiSfxPlayer uiSfx,
            CombatAudioBridge bridge)
        {
            if (panels == null)
                return;

            for (int i = 0; i < panels.Length; i++)
            {
                CombatMoveSelectPanelUGUI panel = panels[i];
                if (panel == null)
                    continue;

                SerializedObject so = new SerializedObject(panel);
                so.FindProperty("uiSfxBehaviour").objectReferenceValue = uiSfx;
                so.FindProperty("audioBridgeBehaviour").objectReferenceValue = bridge;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(panel);
            }
        }

        private static SoundEventSO LoadSound(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<SoundEventSO>(assetPath);
        }
    }
}
