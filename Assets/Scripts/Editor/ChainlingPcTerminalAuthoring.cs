#if UNITY_EDITOR
using System.Reflection;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ChainlingPcTerminalAuthoring
{
    public const string ScreenPrefabPath = "Assets/Prefabs/UI/PlayerMenu/ChainlingPcTerminal.prefab";
    private const string ScenePath = "Assets/Scenes/player_room.unity";
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string InputActionsPath = "Assets/Scripts/Movement/InputSystem_Actions.inputactions";

    [MenuItem("Tools/Chainbound/UI/Author Player Room Chainling PC")]
    public static void AuthorFromMenu()
    {
        Author();
    }

    public static void Author()
    {
        ChainlingPcTerminalScreen screenPrefab = EnsureScreenPrefab();
        AuthorPlayerRoom(screenPrefab);
        AuthorGameServicesGate();
        AssetDatabase.SaveAssets();
        Debug.Log("[ChainlingPcTerminalAuthoring] Authored Chainling PC prefab, player_room terminal, and menu unlock gate.");
    }

    private static ChainlingPcTerminalScreen EnsureScreenPrefab()
    {
        EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(ScreenPrefabPath);

        var root = new GameObject("ChainlingPcTerminal");
        root.AddComponent<RectTransform>();
        ChainlingPcTerminalScreen screen = root.AddComponent<ChainlingPcTerminalScreen>();
        screen.Configure(
            AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(
                "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset"),
            AssetDatabase.LoadAssetAtPath<CombatVisualDatabaseSO>(
                "Assets/Data/Combat/ChainlingSpecies/Visual/Database/CombatVisualDatabase.asset"),
            AssetDatabase.LoadAssetAtPath<ChainlingCardWidget>(
                "Assets/Prefabs/UI/PlayerMenu/ChainlingCardView.prefab"),
            AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(
                "Assets/Resources/SupabaseConfig.asset"));
        screen.BuildDefaultLayoutForEditor();

        MenuInputContextController input = root.GetComponent<MenuInputContextController>();
        if (input == null)
            input = root.AddComponent<MenuInputContextController>();
        var inputSerialized = new SerializedObject(input);
        inputSerialized.FindProperty("inputActions").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsPath);
        inputSerialized.FindProperty("enableMobileHudNavigation").boolValue = true;
        inputSerialized.FindProperty("routeNavigationThroughController").boolValue = true;
        inputSerialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(input.OnCancel, screen.Close);

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<ChainlingPcTerminalScreen>(ScreenPrefabPath);
    }

    private static void AuthorPlayerRoom(ChainlingPcTerminalScreen screenPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject monitor = FindIntroMonitor(scene);
        if (monitor == null)
            throw new System.InvalidOperationException("player_room intro Monitor interactable was not found.");

        StripLegacyTerminalFromMonitor(monitor);
        CleanupMisplacedPcObjects(scene);

        Transform parent = monitor.transform.parent;
        GameObject pc = FindDirectChild(parent, ChainlingPcTerminalIds.PlayerRoomPcObjectName);
        if (pc == null)
        {
            pc = new GameObject(ChainlingPcTerminalIds.PlayerRoomPcObjectName);
            pc.transform.SetParent(parent, false);
        }

        pc.layer = monitor.layer;
        pc.transform.SetPositionAndRotation(monitor.transform.position, monitor.transform.rotation);
        pc.transform.localScale = monitor.transform.localScale;
        pc.SetActive(false);

        CopyCollider(monitor, pc);
        EnsureIndicator(monitor, pc);

        ChainlingPcTerminalInteractable binding = pc.GetComponent<ChainlingPcTerminalInteractable>();
        if (binding == null)
            binding = pc.AddComponent<ChainlingPcTerminalInteractable>();
        binding.Configure(ChainlingPcTerminalIds.PlayerRoomTerminalId, screenPrefab);

        InteractableObject interactable = pc.GetComponent<InteractableObject>();
        if (interactable == null)
            interactable = pc.AddComponent<InteractableObject>();

        var interactableSerialized = new SerializedObject(interactable);
        interactableSerialized.FindProperty("triggerMode").enumValueIndex = 0;
        interactableSerialized.FindProperty("playerTag").stringValue = "Player";
        Transform indicator = pc.transform.Find("Indicator");
        if (indicator != null)
        {
            InteractableIndicator indicatorComponent = indicator.GetComponent<InteractableIndicator>();
            if (indicatorComponent != null)
                interactableSerialized.FindProperty("indicator").objectReferenceValue = indicatorComponent;
        }

        interactableSerialized.ApplyModifiedPropertiesWithoutUndo();
        WireOpenTerminal(interactable, binding);

        AuthorPresenceGate(
            parent,
            ChainlingPcTerminalIds.IntroGateName,
            monitor,
            activeWhenTrue: false,
            defaultActive: true);
        AuthorPresenceGate(
            parent,
            ChainlingPcTerminalIds.PcGateName,
            pc,
            activeWhenTrue: true,
            defaultActive: false);

        EditorUtility.SetDirty(interactable);
        EditorUtility.SetDirty(binding);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AuthorGameServicesGate()
    {
        GameObject servicesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
        if (servicesPrefab == null)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
        try
        {
            GameplayMenuWorldFlagGate gate = root.GetComponent<GameplayMenuWorldFlagGate>();
            if (gate == null)
                gate = root.AddComponent<GameplayMenuWorldFlagGate>();

            var serialized = new SerializedObject(gate);
            serialized.FindProperty("unlockFlagKey").stringValue = ChainlingPcTerminalIds.UnlockFlagKey;
            serialized.FindProperty("legacyUnlockFlagKey").stringValue = ChainlingPcTerminalIds.LegacyUnlockFlagKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameServicesPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void StripLegacyTerminalFromMonitor(GameObject monitor)
    {
        ChainlingPcTerminalInteractable legacy = monitor.GetComponent<ChainlingPcTerminalInteractable>();
        InteractableObject interactable = monitor.GetComponent<InteractableObject>();
        if (interactable != null && legacy != null)
            RemovePersistentListener(interactable, legacy, nameof(ChainlingPcTerminalInteractable.OpenTerminal));
        if (legacy != null)
            Object.DestroyImmediate(legacy);
    }

    private static void WireOpenTerminal(InteractableObject interactable, ChainlingPcTerminalInteractable binding)
    {
        FieldInfo field = typeof(InteractableObject).GetField(
            "onInteracted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent interacted = field != null ? field.GetValue(interactable) as UnityEvent : null;
        if (interacted == null)
            throw new System.InvalidOperationException("InteractableObject.onInteracted was unavailable.");

        bool alreadyWired = false;
        for (int i = 0; i < interacted.GetPersistentEventCount(); i++)
        {
            if (interacted.GetPersistentTarget(i) == binding &&
                interacted.GetPersistentMethodName(i) == nameof(ChainlingPcTerminalInteractable.OpenTerminal))
            {
                alreadyWired = true;
                break;
            }
        }

        if (!alreadyWired)
            UnityEventTools.AddPersistentListener(interacted, binding.OpenTerminal);

        field.SetValue(interactable, interacted);
    }

    private static void RemovePersistentListener(InteractableObject interactable, Object target, string methodName)
    {
        FieldInfo field = typeof(InteractableObject).GetField(
            "onInteracted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent interacted = field != null ? field.GetValue(interactable) as UnityEvent : null;
        if (interacted == null)
            return;

        for (int i = interacted.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (interacted.GetPersistentTarget(i) == target &&
                interacted.GetPersistentMethodName(i) == methodName)
            {
                UnityEventTools.RemovePersistentListener(interacted, i);
            }
        }
    }

    private static void AuthorPresenceGate(
        Transform parent,
        string hostName,
        GameObject target,
        bool activeWhenTrue,
        bool defaultActive)
    {
        GameObject host = FindDirectChild(parent, hostName);
        if (host == null)
        {
            host = new GameObject(hostName);
            host.transform.SetParent(parent, false);
        }

        target.SetActive(defaultActive);

        WorldPresenceBinding binding = host.GetComponent<WorldPresenceBinding>();
        if (binding == null)
            binding = host.AddComponent<WorldPresenceBinding>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("presenceId").stringValue = ChainlingPcTerminalIds.UnlockFlagKey;
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.FindProperty("activeWhenTrue").boolValue = activeWhenTrue;
        serialized.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CopyCollider(GameObject source, GameObject dest)
    {
        BoxCollider sourceBox = source.GetComponent<BoxCollider>();
        BoxCollider destBox = dest.GetComponent<BoxCollider>();
        if (destBox == null)
            destBox = dest.AddComponent<BoxCollider>();
        if (sourceBox == null)
        {
            destBox.isTrigger = true;
            destBox.size = Vector3.one;
            return;
        }

        destBox.isTrigger = sourceBox.isTrigger;
        destBox.center = sourceBox.center;
        destBox.size = sourceBox.size;
    }

    private static void EnsureIndicator(GameObject source, GameObject dest)
    {
        Transform existing = dest.transform.Find("Indicator");
        if (existing != null)
            return;

        Transform sourceIndicator = source.transform.Find("Indicator");
        if (sourceIndicator == null)
            return;

        GameObject clone = Object.Instantiate(sourceIndicator.gameObject, dest.transform);
        clone.name = "Indicator";
    }

    private static GameObject FindIntroMonitor(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            QuestProgressInteractionBridge[] bridges =
                root.GetComponentsInChildren<QuestProgressInteractionBridge>(true);
            for (int i = 0; i < bridges.Length; i++)
            {
                QuestProgressInteractionBridge bridge = bridges[i];
                if (bridge == null)
                    continue;
                if (string.Equals(bridge.gameObject.name, ChainlingPcTerminalIds.PlayerRoomMonitorName, System.StringComparison.Ordinal) &&
                    bridge.GetComponent<InteractableObject>() != null)
                {
                    return bridge.gameObject;
                }
            }
        }

        return null;
    }

    private static void CleanupMisplacedPcObjects(Scene scene)
    {
        var toDestroy = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || !IsPcAuthoringObject(current.name))
                    continue;
                if (current.parent != null &&
                    current.parent.GetComponent<QuestProgressInteractionBridge>() == null &&
                    current.parent.GetComponent<InteractableObject>() == null)
                {
                    toDestroy.Add(current.gameObject);
                }
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            if (toDestroy[i] != null)
                Object.DestroyImmediate(toDestroy[i]);
        }
    }

    private static bool IsPcAuthoringObject(string objectName)
    {
        return string.Equals(objectName, ChainlingPcTerminalIds.PlayerRoomPcObjectName, System.StringComparison.Ordinal) ||
               string.Equals(objectName, ChainlingPcTerminalIds.IntroGateName, System.StringComparison.Ordinal) ||
               string.Equals(objectName, ChainlingPcTerminalIds.PcGateName, System.StringComparison.Ordinal);
    }

    private static GameObject FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (AssetDatabase.IsValidFolder(path))
            return;
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
