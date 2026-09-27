#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NarrativeInteractionAuthoringEditor
{
    [MenuItem("Tools/Chainbound/Narrative/Validate Canonical Interaction Stack")]
    public static void ValidateCanonicalInteractionStack()
    {
        List<InteractionAuthoringIssue> issues = new List<InteractionAuthoringIssue>();
        ValidateOpenScenes(issues);
        ValidatePrefabs(issues);
        LogIssues(issues);
    }

    private static void ValidateOpenScenes(List<InteractionAuthoringIssue> issues)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                InteractionAuthoringValidation.AppendIssues(roots[rootIndex], issues);
            }
        }
    }

    private static void ValidatePrefabs(List<InteractionAuthoringIssue> issues)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                InteractionAuthoringValidation.AppendIssues(prefabRoot, issues);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static void LogIssues(List<InteractionAuthoringIssue> issues)
    {
        int warningCount = 0;
        int errorCount = 0;

        for (int i = 0; i < issues.Count; i++)
        {
            InteractionAuthoringIssue issue = issues[i];
            string message = $"[NarrativeInteractionAuthoringEditor] {issue.Severity}: {issue.HierarchyPath} - {issue.Message}";

            if (issue.Severity == InteractionAuthoringIssueSeverity.Error)
            {
                errorCount++;
                Debug.LogError(message, issue.Context);
            }
            else
            {
                warningCount++;
                Debug.LogWarning(message, issue.Context);
            }
        }

        if (issues.Count == 0)
        {
            Debug.Log("[NarrativeInteractionAuthoringEditor] Canonical interaction authoring validation passed.");
            return;
        }

        Debug.LogWarning(
            $"[NarrativeInteractionAuthoringEditor] Validation complete. Errors={errorCount}, Warnings={warningCount}.");
    }
}
#endif
