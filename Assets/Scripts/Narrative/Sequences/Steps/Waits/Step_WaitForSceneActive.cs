using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Waits until <see cref="SceneManager.GetActiveScene"/> name matches the configured scene (Known),
/// or optional timeout with jump label (same pattern as <see cref="Step_WaitForEvent"/>).
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForSceneActive", menuName = "Chainbound/Narrative/Steps/Waits/Wait For Scene Active")]
public class Step_WaitForSceneActive : SequenceStep
{
    [Header("Scene")]
    [Tooltip("Active scene name to wait for (must match Build Settings / SceneManager name exactly).")]
    [SerializeField] private string sceneName = "";

    [Header("Timeout")]
    [Tooltip("Timeout in seconds (0 = wait indefinitely).")]
    [SerializeField] private float timeoutSeconds;

    [Tooltip("Label to jump to on timeout (optional).")]
    [SerializeField] private string onTimeoutLabel = "";

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[Step_WaitForSceneActive] Scene name is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Scene name is empty"));
            yield break;
        }

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_WaitForSceneActive] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }

        if (IsTargetSceneActive())
        {
            if (enableDebugLogs)
                Debug.Log($"[Step_WaitForSceneActive] Already active: {sceneName}");
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_WaitForSceneActive] Waiting for active scene: {sceneName}");

        bool matched = false;
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsTargetSceneActive())
                matched = true;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        float elapsed = 0f;
        while (!matched && (timeoutSeconds <= 0f || elapsed < timeoutSeconds))
        {
            if (IsTargetSceneActive())
            {
                matched = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (matched)
        {
            if (enableDebugLogs)
                Debug.Log($"[Step_WaitForSceneActive] Active scene is now: {sceneName}");
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }

        if (enableDebugLogs)
            Debug.LogWarning($"[Step_WaitForSceneActive] Timeout waiting for active scene: {sceneName}");

        if (!string.IsNullOrEmpty(onTimeoutLabel))
        {
            runner.JumpToLabel(onTimeoutLabel);
            onComplete?.Invoke(StepOutcome.Success("Timeout - jumped to label"));
        }
        else
        {
            onComplete?.Invoke(StepOutcome.Failed($"Timeout waiting for active scene: {sceneName}"));
        }
    }

    private bool IsTargetSceneActive()
    {
        return string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal);
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[Step_WaitForSceneActive] Scene name is not set!");
            return false;
        }

        return true;
    }
}
