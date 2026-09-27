#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the authored starter preview UI prefab and wires it into the persistent
/// GameServices prefab. The runtime builder remains available as a fallback for
/// older scenes, but normal onboarding now uses the prefab hierarchy.
/// </summary>
public static class StarterSelectionPrefabAuthoring
{
    private const string PrefabFolder = "Assets/Prefabs/UI/StarterSelection";
    private const string PrefabPath = PrefabFolder + "/StarterSelectionUI.prefab";
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";

    [MenuItem("Chainbound/Starters/Create Authored Starter Selection Prefab")]
    public static void CreateAndWire()
    {
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder(PrefabFolder);

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (panelPrefab == null)
            panelPrefab = CreatePanelPrefab();
        else
            Debug.Log("[StarterSelectionPrefabAuthoring] Existing authored prefab found; preserving its layout and rewiring GameServices only.");
        if (panelPrefab == null)
        {
            Debug.LogError("[StarterSelectionPrefabAuthoring] Failed to create the starter selection prefab.");
            return;
        }

        WireGameServicesPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = panelPrefab;

        Debug.Log(
            "[StarterSelectionPrefabAuthoring] Created and wired " +
            PrefabPath + ". Open it in Prefab Mode to author the shared starter panel layout, labels, and button styling.");
    }

    private static GameObject CreatePanelPrefab()
    {
        GameObject authoringRoot = new GameObject("StarterSelectionUI", typeof(RectTransform));
        StarterSelectionUI controller = authoringRoot.AddComponent<StarterSelectionUI>();

        try
        {
            MethodInfo buildMethod = typeof(StarterSelectionUI).GetMethod(
                "BuildRuntimeUi",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (buildMethod == null)
            {
                Debug.LogError("[StarterSelectionPrefabAuthoring] StarterSelectionUI.BuildRuntimeUi was not found.");
                return null;
            }

            // Reuse the existing fallback hierarchy so the authored prefab starts
            // visually identical to the current runtime panel.
            buildMethod.Invoke(controller, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(authoringRoot, PrefabPath);
            return prefab;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authoringRoot);
        }
    }

    private static void WireGameServicesPrefab()
    {
        GameObject gameServicesRoot = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
        if (gameServicesRoot == null)
        {
            Debug.LogError(
                "[StarterSelectionPrefabAuthoring] Could not load " + GameServicesPrefabPath + ".");
            return;
        }

        try
        {
            StarterSelectionService service =
                gameServicesRoot.GetComponentInChildren<StarterSelectionService>(true);
            if (service == null)
            {
                Debug.LogError(
                    "[StarterSelectionPrefabAuthoring] StarterSelectionService was not found in GameServices.prefab.");
                return;
            }

            StarterSelectionUI existingPanel =
                gameServicesRoot.GetComponentInChildren<StarterSelectionUI>(true);
            if (existingPanel != null)
                UnityEngine.Object.DestroyImmediate(existingPanel.gameObject);

            GameObject panelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (panelAsset == null)
            {
                Debug.LogError(
                    "[StarterSelectionPrefabAuthoring] Could not load the generated panel prefab at " +
                    PrefabPath + ".");
                return;
            }

            GameObject panelInstance =
                (GameObject)PrefabUtility.InstantiatePrefab(panelAsset, gameServicesRoot.transform);
            panelInstance.name = "StarterSelectionUI";

            StarterSelectionUI panelController = panelInstance.GetComponent<StarterSelectionUI>();
            SerializedObject serializedService = new SerializedObject(service);
            SerializedProperty selectionUiProperty = serializedService.FindProperty("selectionUI");
            if (selectionUiProperty == null)
            {
                Debug.LogError(
                    "[StarterSelectionPrefabAuthoring] StarterSelectionService.selectionUI was not found.");
                return;
            }

            selectionUiProperty.objectReferenceValue = panelController;
            serializedService.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(service);
            PrefabUtility.SaveAsPrefabAsset(gameServicesRoot, GameServicesPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(gameServicesRoot);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int separator = folderPath.LastIndexOf('/');
        if (separator <= 0)
            return;

        string parent = folderPath.Substring(0, separator);
        string folderName = folderPath.Substring(separator + 1);
        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif
