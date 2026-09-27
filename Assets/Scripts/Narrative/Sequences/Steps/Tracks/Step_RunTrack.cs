using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Starts a child narrative track (non-authoritative by default).
/// </summary>
[CreateAssetMenu(fileName = "Step_RunTrack", menuName = "Chainbound/Narrative/Steps/Tracks/Run Track")]
public class Step_RunTrack : SequenceStep
{
    [Header("Track")]
    [SerializeField] private string trackId = "ambient_1";
    [SerializeField] private SequenceTrackType trackType = SequenceTrackType.Ambient;
    [SerializeField] private SequenceAsset sequence;

    [Header("Context")]
    [SerializeField] private TrackContextMode contextMode = TrackContextMode.Child;

    [Header("Lifecycle")]
    [SerializeField] private TrackCancelPolicy cancelPolicy = TrackCancelPolicy.StopOnParentEnd;
    [SerializeField] private TrackBlockingMode blockingMode = TrackBlockingMode.NonBlocking;

    public string TrackId => trackId;
    public SequenceTrackType TrackType => trackType;
    public SequenceAsset Sequence => sequence;
    public TrackContextMode ContextMode => contextMode;
    public TrackCancelPolicy CancelPolicy => cancelPolicy;
    public TrackBlockingMode BlockingMode => blockingMode;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            onComplete?.Invoke(StepOutcome.Failed("trackId is empty"));
            yield break;
        }
        if (sequence == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("sequence is null"));
            yield break;
        }

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }

        if (!runner.TryStartChildTrack(trackId, sequence, trackType, contextMode, cancelPolicy, out string err))
        {
            onComplete?.Invoke(StepOutcome.Failed(err));
            yield break;
        }

        if (blockingMode == TrackBlockingMode.WaitForComplete)
        {
            while (runner.IsChildTrackRunning(trackId))
                yield return null;
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_RunTrack] Track '{trackId}' started (blocking={blockingMode})");

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(trackId))
        {
            Debug.LogWarning("[Step_RunTrack] trackId is not set.");
            return false;
        }
        if (sequence == null)
        {
            Debug.LogWarning("[Step_RunTrack] sequence is not assigned.");
            return false;
        }
        return true;
    }
}
