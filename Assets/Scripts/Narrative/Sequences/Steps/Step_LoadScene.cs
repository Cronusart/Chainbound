using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Step that loads a scene with fade transition.
/// Spawns player at specified spawn point in new scene.
/// Uses SceneLoader so the step properly waits for full load (warmup, spawn, etc.) before completing.
/// </summary>
[CreateAssetMenu(fileName = "Step_LoadScene", menuName = "Chainbound/Narrative/Steps/Load Scene")]
public class Step_LoadScene : SequenceStep
{
    [Header("Scene Settings")]
    [Tooltip("Scene name to load")]
    [SerializeField] private string sceneName = "";
    
    [Tooltip("Spawn point ID in new scene")]
    [SerializeField] private string spawnId = "";
    
    [Tooltip("Loading transition style (FullLoading = progress bar + min display time, QuickFade = brief fade)")]
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.FullLoading;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[Step_LoadScene] Scene name is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Scene name is empty"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_LoadScene] Starting load: {sceneName} (spawn: {spawnId}, style: {transitionStyle})");
        }
        
        context.Variables.Set("currentSceneId", sceneName, VariableScope.Local);
        if (!string.IsNullOrEmpty(spawnId))
            context.Variables.Set("currentSpawnId", spawnId, VariableScope.Local);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() &&
            string.Equals(activeScene.name, sceneName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(spawnId))
                SpawnService.Instance?.SpawnPlayerAt(spawnId);
            if (enableDebugLogs)
                Debug.Log($"[Step_LoadScene] Already in '{sceneName}'; skipped reload.");
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }

        SceneLoader sceneLoader = context.SceneLoader;
        if (sceneLoader == null)
        {
            Debug.LogError("[Step_LoadScene] SceneLoader not found! Ensure SceneLoader exists in the scene (e.g. on GameServices).");
            onComplete?.Invoke(StepOutcome.Failed("SceneLoader not found"));
            yield break;
        }

        if (sceneLoader.IsTransitioning)
        {
            Debug.LogError("[Step_LoadScene] SceneLoader is already transitioning. Wait for the current load to complete.");
            onComplete?.Invoke(StepOutcome.Failed("SceneLoader busy"));
            yield break;
        }

        bool requestFinished = false;
        SceneTransitionResult transitionResult = default;
        bool accepted = sceneLoader.RequestTransition(new SceneTransitionRequest(
            sceneName,
            spawnId,
            transitionStyle,
            sourceId: $"SequenceStep.{name}",
            completed: result =>
            {
                transitionResult = result;
                requestFinished = true;
                if (enableDebugLogs)
                    Debug.Log($"[Step_LoadScene] SceneLoader completion fired: {sceneName} ({result.Outcome})");
            }));
        if (!accepted && !requestFinished)
        {
            onComplete?.Invoke(StepOutcome.Failed("SceneLoader rejected request"));
            yield break;
        }

        // Unity's scene operation cannot be cancelled safely. The loader owns
        // long-load messaging and recovery, so this step waits for its structured result.
        while (!requestFinished)
            yield return null;

        if (!transitionResult.Succeeded)
        {
            Debug.LogError($"[Step_LoadScene] Scene load failed: {transitionResult.Message}");
            onComplete?.Invoke(StepOutcome.Failed(transitionResult.Message));
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[Step_LoadScene] Scene fully loaded and ready: {sceneName}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[Step_LoadScene] Scene name is not set!");
            return false;
        }
        return true;
    }
}

