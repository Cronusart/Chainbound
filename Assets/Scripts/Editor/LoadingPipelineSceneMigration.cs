using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Idempotent project wiring for the production loading pipeline.</summary>
public static class LoadingPipelineSceneMigration
{
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string MainMenuPath = "Assets/Scenes/main_menu.unity";
    private const string LoomvillePath = "Assets/Scenes/Loomville.unity";
    private const string CombatTestPath = "Assets/Scenes/Combat2_TestHarness.unity";
    private const string GameServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string LoadingScreenPath = "Assets/Prefabs/UI/LoadingScreenUI/LoadingScreenUI.prefab";
    private const string ConfigPath = "Assets/Data/Loading/SceneLoadingConfig_Default.asset";

    [MenuItem("Tools/Signal Jam/Loading/Apply Production Bootstrap Wiring")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void Apply()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ConfigureBuildSettings();
            ConfigureLoadingPrefab();
            ConfigureGameServicesPrefab();
            ConfigureBootstrapScene();
            ConfigureMainMenuScene();
            ConfigureSceneWithoutGameServices(CombatTestPath);
            ConfigureLoomvilleScene();
            DisableSceneLocalEventSystems();
            AssetDatabase.SaveAssets();
            Debug.Log("[Loading Migration] Production Bootstrap and scene-loading wiring applied.");
        }
        finally
        {
            if (!Application.isBatchMode && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void ConfigureBuildSettings()
    {
        List<EditorBuildSettingsScene> remainder = new List<EditorBuildSettingsScene>();
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        for (int i = 0; i < existing.Length; i++)
        {
            if (!SamePath(existing[i].path, BootstrapPath) && !SamePath(existing[i].path, MainMenuPath))
                remainder.Add(existing[i]);
        }

        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(BootstrapPath, true),
            new EditorBuildSettingsScene(MainMenuPath, true),
        };
        ordered.AddRange(remainder);
        EditorBuildSettings.scenes = ordered.ToArray();
    }

    private static void ConfigureLoadingPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(LoadingScreenPath);
        try
        {
            root.transform.localScale = Vector3.one;
            LoadingScreenUI ui = root.GetComponent<LoadingScreenUI>();
            if (ui == null)
                ui = root.GetComponentInChildren<LoadingScreenUI>(true);
            if (ui == null)
                throw new InvalidOperationException("LoadingScreenUI prefab has no LoadingScreenUI component.");

            SerializedObject serialized = new SerializedObject(ui);
            serialized.FindProperty("startCoveredOnAwake").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Canvas canvas = ui.GetComponent<Canvas>();
            if (canvas != null)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x <= 0f)
                scaler.referenceResolution = new Vector2(1920f, 1080f);

            PrefabUtility.SaveAsPrefabAsset(root, LoadingScreenPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureGameServicesPrefab()
    {
        SceneLoadingConfigSO config = AssetDatabase.LoadAssetAtPath<SceneLoadingConfigSO>(ConfigPath);
        if (config == null)
            throw new InvalidOperationException($"Missing loading config at '{ConfigPath}'.");

        GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPath);
        try
        {
            SceneLoader loader = root.GetComponentInChildren<SceneLoader>(true);
            if (loader == null)
                throw new InvalidOperationException("GameServices prefab has no SceneLoader.");

            SerializedObject serialized = new SerializedObject(loader);
            serialized.FindProperty("loadingConfig").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            NormalizePersistentEventSystem(root);
            PrefabUtility.SaveAsPrefabAsset(root, GameServicesPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void NormalizePersistentEventSystem(GameObject root)
    {
        EventSystem[] systems = root.GetComponentsInChildren<EventSystem>(true);
        EventSystem authoritative;
        if (systems.Length == 0)
        {
            GameObject host = new GameObject("EventSystem");
            host.transform.SetParent(root.transform, false);
            authoritative = host.AddComponent<EventSystem>();
        }
        else
        {
            authoritative = systems[0];
        }

        PersistentUiEventSystem source = root.GetComponentInChildren<PersistentUiEventSystem>(true);
        PersistentUiEventSystem owner = authoritative.GetComponent<PersistentUiEventSystem>();
        if (owner == null)
            owner = authoritative.gameObject.AddComponent<PersistentUiEventSystem>();

        if (source != null && source != owner)
        {
            EditorUtility.CopySerialized(source, owner);
            GameObject sourceObject = source.gameObject;
            UnityEngine.Object.DestroyImmediate(source);
            if (sourceObject != authoritative.gameObject && sourceObject.GetComponents<Component>().Length == 1 &&
                sourceObject.transform.childCount == 0)
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem duplicate = systems[i];
            if (duplicate == null || duplicate == authoritative)
                continue;
            BaseInputModule[] modules = duplicate.GetComponents<BaseInputModule>();
            for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                UnityEngine.Object.DestroyImmediate(modules[moduleIndex]);
            UnityEngine.Object.DestroyImmediate(duplicate);
        }
    }

    private static void ConfigureBootstrapScene()
    {
        Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        BootstrapStartupController[] existing = UnityEngine.Object.FindObjectsByType<BootstrapStartupController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        BootstrapStartupController controller = null;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].gameObject.scene != scene)
                continue;
            if (controller == null)
                controller = existing[i];
            else
                UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
        }

        if (controller == null)
        {
            GameObject startup = new GameObject("[Bootstrap Startup]");
            SceneManager.MoveGameObjectToScene(startup, scene);
            startup.AddComponent<BootstrapStartupController>();
        }
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMainMenuScene()
    {
        ConfigureSceneWithoutGameServices(MainMenuPath);
    }

    private static void ConfigureSceneWithoutGameServices(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (SamePath(prefabPath, GameServicesPath))
                UnityEngine.Object.DestroyImmediate(root);
        }
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureLoomvilleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LoomvillePath, OpenSceneMode.Single);
        SceneReadinessWarmup[] warmups = UnityEngine.Object.FindObjectsByType<SceneReadinessWarmup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        SceneReadinessWarmup warmup = null;
        for (int i = 0; i < warmups.Length; i++)
        {
            if (warmups[i].gameObject.scene != scene)
                continue;
            if (warmup == null)
                warmup = warmups[i];
            else
                UnityEngine.Object.DestroyImmediate(warmups[i].gameObject);
        }

        if (warmup == null)
        {
            GameObject coordinator = new GameObject("[Scene Readiness]");
            SceneManager.MoveGameObjectToScene(coordinator, scene);
            warmup = coordinator.AddComponent<SceneReadinessWarmup>();
        }

        SerializedObject warmupSerialized = new SerializedObject(warmup);
        warmupSerialized.FindProperty("warmupId").stringValue = "loomville-readiness";
        warmupSerialized.FindProperty("order").intValue = 100;
        warmupSerialized.FindProperty("weight").floatValue = 3f;
        warmupSerialized.FindProperty("requireActiveCamera").boolValue = true;
        warmupSerialized.FindProperty("requirePlayer").boolValue = true;
        warmupSerialized.FindProperty("expectLightingCoordinator").boolValue = true;
        warmupSerialized.FindProperty("expectAmbienceZone").boolValue = true;
        warmupSerialized.ApplyModifiedPropertiesWithoutUndo();

        GrassPatchManager[] grassManagers = UnityEngine.Object.FindObjectsByType<GrassPatchManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < grassManagers.Length; i++)
        {
            if (grassManagers[i].gameObject.scene != scene)
                continue;
            SerializedObject grassSerialized = new SerializedObject(grassManagers[i]);
            grassSerialized.FindProperty("deferInitializationToSceneWarmup").boolValue = true;
            grassSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void DisableSceneLocalEventSystems()
    {
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (!buildScenes[i].enabled || string.IsNullOrWhiteSpace(buildScenes[i].path))
                continue;

            Scene scene = EditorSceneManager.OpenScene(buildScenes[i].path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            bool changed = false;
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                EventSystem[] systems = roots[rootIndex].GetComponentsInChildren<EventSystem>(true);
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    EventSystem system = systems[systemIndex];
                    if (system.GetComponentInParent<GameServicesContainer>(true) != null)
                        continue;
                    if (system.enabled)
                    {
                        system.enabled = false;
                        changed = true;
                    }

                    BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
                    for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                    {
                        if (!modules[moduleIndex].enabled)
                            continue;
                        modules[moduleIndex].enabled = false;
                        changed = true;
                    }
                }
            }

            if (changed)
                EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            left?.Replace('\\', '/'),
            right?.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
