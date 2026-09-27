#if UNITY_EDITOR
using System;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ProfileWalletAccessPrefabAuthoring
{
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string PromptPrefabFolder = "Assets/Prefabs/UI";
    private const string PromptPrefabPath = PromptPrefabFolder + "/WalletConnectionPrompt.prefab";
    private const int PromptSortingOrder = 600;

    [MenuItem("Tools/Chainbound/Author Profile Wallet Access UI")]
    public static void Schedule()
    {
        Author();
    }

    [MenuItem("Tools/Chainbound/Repair Profile Wallet Access UI Scale")]
    public static void RepairPersistentPrefabScale()
    {
        InstallPromptPrefabInGameServices();
    }

    private static void Author()
    {
        WalletConnectionPromptService source = null;
        foreach (WalletConnectionPromptService candidate in Resources.FindObjectsOfTypeAll<WalletConnectionPromptService>())
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.name == "main_menu")
            {
                source = candidate;
                break;
            }
        }

        if (source == null)
        {
            Debug.LogError("[ProfileWalletAccessPrefabAuthoring] Open main_menu before authoring the persistent prompt.");
            return;
        }

        try
        {
            EnsureFolder(PromptPrefabFolder);
            ConfigurePromptRoot(source.gameObject);
            source.transform.SetAsLastSibling();
            source.gameObject.SetActive(false);

            string currentPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject);
            if (string.Equals(currentPrefabPath, PromptPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                PrefabUtility.ApplyPrefabInstance(
                    source.gameObject,
                    InteractionMode.UserAction);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    source.gameObject,
                    PromptPrefabPath,
                    InteractionMode.UserAction);
            }

            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            EditorSceneManager.SaveScene(source.gameObject.scene);
            InstallPromptPrefabInGameServices();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ProfileWalletAccessPrefabAuthoring] Authored WalletConnectionPrompt.prefab, " +
                "connected main_menu, and installed the nested prefab in GameServices.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void InstallPromptPrefabInGameServices()
    {
        GameObject promptAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PromptPrefabPath);
        if (promptAsset == null)
        {
            Debug.LogError(
                "[ProfileWalletAccessPrefabAuthoring] Create WalletConnectionPrompt.prefab before repairing GameServices.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
        try
        {
            if (prefabRoot.GetComponent<ProfileWalletAccessCoordinator>() == null)
                prefabRoot.AddComponent<ProfileWalletAccessCoordinator>();
            if (prefabRoot.GetComponent<WalletOperationGuard>() == null)
                prefabRoot.AddComponent<WalletOperationGuard>();

            Transform canvas = prefabRoot.transform.Find("WalletAccessCanvas");
            Transform safeArea = canvas != null ? canvas.Find("SafeArea") : null;
            if (canvas == null || safeArea == null)
                throw new InvalidOperationException("GameServices wallet canvas or safe-area host is missing.");

            Transform existingPrompt = safeArea.Find("WalletConnectionPrompt");
            if (existingPrompt != null)
                UnityEngine.Object.DestroyImmediate(existingPrompt.gameObject);

            WalletConnectionPromptService hostPresenter = safeArea.GetComponent<WalletConnectionPromptService>();
            if (hostPresenter != null)
                UnityEngine.Object.DestroyImmediate(hostPresenter);

            GameObject promptObject = (GameObject)PrefabUtility.InstantiatePrefab(promptAsset, safeArea);
            promptObject.name = "WalletConnectionPrompt";
            promptObject.SetActive(false);
            promptObject.transform.localScale = Vector3.one;
            WalletConnectionPromptService presenter =
                promptObject.GetComponent<WalletConnectionPromptService>();
            if (presenter == null)
                throw new InvalidOperationException("WalletConnectionPrompt prefab has no presenter.");
            AssignCoordinatorPresenter(prefabRoot, presenter);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, GameServicesPrefabPath);
            Debug.Log("[ProfileWalletAccessPrefabAuthoring] Installed nested wallet prompt prefab in GameServices.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigurePromptRoot(GameObject promptObject)
    {
        CanvasGroup group = promptObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = Undo.AddComponent<CanvasGroup>(promptObject);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        Canvas canvas = promptObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(promptObject);
        SerializedObject serializedCanvas = new SerializedObject(canvas);
        SerializedProperty overrideSorting = serializedCanvas.FindProperty("m_OverrideSorting");
        SerializedProperty sortingOrder = serializedCanvas.FindProperty("m_SortingOrder");
        if (overrideSorting == null || sortingOrder == null)
            throw new InvalidOperationException("Canvas sorting properties are unavailable.");
        overrideSorting.boolValue = true;
        sortingOrder.intValue = PromptSortingOrder;
        serializedCanvas.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(canvas);

        if (promptObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(promptObject);

        WalletConnectionPromptService presenter =
            promptObject.GetComponent<WalletConnectionPromptService>();
        if (presenter == null)
            throw new InvalidOperationException("The scene-authored prompt has no presenter component.");

        SerializedObject serializedPresenter = new SerializedObject(presenter);
        SetObjectReference(serializedPresenter, "root", promptObject);
        serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

        Button[] buttons = promptObject.GetComponentsInChildren<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = buttons[index];
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
                layout = Undo.AddComponent<LayoutElement>(button.gameObject);
            layout.minHeight = Mathf.Max(60f, layout.minHeight);

            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(index - 1 + buttons.Length) % buttons.Length],
                selectOnDown = buttons[(index + 1) % buttons.Length],
            };
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"Invalid prefab folder '{folder}'.");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void AssignCoordinatorPresenter(
        GameObject prefabRoot,
        WalletConnectionPromptService presenter)
    {
        ProfileWalletAccessCoordinator coordinator =
            prefabRoot.GetComponent<ProfileWalletAccessCoordinator>();
        if (coordinator == null)
            coordinator = prefabRoot.AddComponent<ProfileWalletAccessCoordinator>();

        SerializedObject serializedCoordinator = new SerializedObject(coordinator);
        SetObjectReference(serializedCoordinator, "promptPresenter", presenter);
        serializedCoordinator.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Missing target property '{propertyName}'.");

        property.objectReferenceValue = value;
    }

}
#endif
