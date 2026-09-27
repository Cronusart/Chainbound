using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Waits for a fixed duration before continuing the sequence.
/// Composable primitive for pacing between dialogue, facing, camera, or other steps.
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitSeconds", menuName = "Chainbound/Narrative/Steps/Waits/Wait Seconds")]
public class Step_WaitSeconds : SequenceStep
{
    [Header("Timing")]
    [Tooltip("Seconds to wait before the step completes.")]
    [Min(0f)]
    [SerializeField] private float durationSeconds = 0.5f;

    [Tooltip("If true, uses unscaled time (ignores Time.timeScale). Prefer for cutscenes/dialogue when the game may be paused.")]
    [SerializeField] private bool useUnscaledTime = true;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (durationSeconds <= 0f)
        {
            if (enableDebugLogs)
                Debug.Log($"[Step_WaitSeconds] '{GetStepName()}': duration <= 0, completing immediately.");
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_WaitSeconds] '{GetStepName()}': waiting {durationSeconds:F2}s " +
                $"(unscaled={useUnscaledTime})");
        }

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(durationSeconds);
        else
            yield return new WaitForSeconds(durationSeconds);

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        if (durationSeconds < 0f)
        {
            Debug.LogWarning($"[Step_WaitSeconds] '{GetStepName()}': durationSeconds should be >= 0.");
            return false;
        }

        return true;
    }
}
