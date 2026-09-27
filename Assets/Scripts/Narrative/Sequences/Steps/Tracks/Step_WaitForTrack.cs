using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Waits until a child track completes or a narrative signal is raised.
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForTrack", menuName = "Chainbound/Narrative/Steps/Tracks/Wait For Track")]
public class Step_WaitForTrack : SequenceStep
{
    [Header("Wait")]
    [SerializeField] private string trackId = "";
    [SerializeField] private TrackWaitCompletionMode completionMode = TrackWaitCompletionMode.Completed;
    [Tooltip("When completionMode is Signaled, wait for this signal id.")]
    [SerializeField] private string signalId = "";

    [Header("Timeout")]
    [SerializeField] private float timeoutSeconds = 0f;
    [SerializeField] private string onTimeoutLabel = "";

    public string TrackId => trackId;
    public TrackWaitCompletionMode CompletionMode => completionMode;
    public string SignalId => signalId;
    public float TimeoutSeconds => timeoutSeconds;
    public string OnTimeoutLabel => onTimeoutLabel;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }

        float elapsed = 0f;
        while (timeoutSeconds <= 0f || elapsed < timeoutSeconds)
        {
            bool done = false;
            switch (completionMode)
            {
                case TrackWaitCompletionMode.Completed:
                    done = string.IsNullOrEmpty(trackId) || !runner.IsChildTrackRunning(trackId);
                    break;
                case TrackWaitCompletionMode.Signaled:
                    var bus = NarrativeSignalBus.Instance;
                    done = bus != null && !string.IsNullOrEmpty(signalId) && bus.HasSignal(signalId);
                    break;
            }

            if (done)
            {
                onComplete?.Invoke(StepOutcome.Success());
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!string.IsNullOrEmpty(onTimeoutLabel))
        {
            runner.JumpToLabel(onTimeoutLabel);
            onComplete?.Invoke(StepOutcome.Success("Timeout — jumped to label"));
        }
        else
            onComplete?.Invoke(StepOutcome.Failed("Timeout waiting for track/signal"));
    }

    public override bool Validate()
    {
        if (completionMode == TrackWaitCompletionMode.Completed && string.IsNullOrEmpty(trackId))
        {
            Debug.LogWarning("[Step_WaitForTrack] trackId empty for Completed mode.");
            return false;
        }
        if (completionMode == TrackWaitCompletionMode.Signaled && string.IsNullOrEmpty(signalId))
        {
            Debug.LogWarning("[Step_WaitForTrack] signalId empty for Signaled mode.");
            return false;
        }
        return true;
    }
}
