using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Step_StopTrack", menuName = "Chainbound/Narrative/Steps/Tracks/Stop Track")]
public class Step_StopTrack : SequenceStep
{
    [SerializeField] private string trackId = "";
    [SerializeField] private TrackStopMode stopMode = TrackStopMode.Immediate;

    public string TrackId => trackId;
    public TrackStopMode StopMode => stopMode;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            onComplete?.Invoke(StepOutcome.Failed("trackId is empty"));
            yield break;
        }

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }

        runner.StopChildTrack(trackId, stopMode);
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(trackId))
        {
            Debug.LogWarning("[Step_StopTrack] trackId is not set.");
            return false;
        }
        return true;
    }
}
