using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Step_Signal", menuName = "Chainbound/Narrative/Steps/Tracks/Narrative Signal")]
public class Step_Signal : SequenceStep
{
    [SerializeField] private string signalId = "";
    [SerializeField] private string payload = "";
    [Tooltip("Optional override; default uses main execution track id when available.")]
    [SerializeField] private string sourceTrackId = "";

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(signalId))
        {
            onComplete?.Invoke(StepOutcome.Failed("signalId is empty"));
            yield break;
        }

        var bus = NarrativeSignalBus.Instance;
        if (bus == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("NarrativeSignalBus not found"));
            yield break;
        }

        string track = sourceTrackId;
        if (string.IsNullOrEmpty(track))
        {
            var runner = SequenceRunner.Instance;
            var main = runner != null ? runner.GetMainExecution() : null;
            track = main != null ? main.TrackId : "main";
        }

        string seqId = context?.CurrentSequenceId ?? "";
        bus.Emit(signalId, track, seqId, payload);
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(signalId))
        {
            Debug.LogWarning("[Step_Signal] signalId is not set.");
            return false;
        }
        return true;
    }
}
