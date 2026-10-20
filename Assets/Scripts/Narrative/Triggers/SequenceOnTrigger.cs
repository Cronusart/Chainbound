using UnityEngine;

/// <summary>
/// Starts a child sequence track when a trigger id fires.
/// The sequence stays off the catalog auto-start list, so a main quest rail is left running.
/// </summary>
public class SequenceOnTrigger : MonoBehaviour
{
    [SerializeField] private string triggerId = "";
    [SerializeField] private SequenceAsset sequence;
    [SerializeField] private string trackId = "";
    [SerializeField] private SequenceTrackType trackType = SequenceTrackType.NPC;
    [SerializeField] private TrackContextMode contextMode = TrackContextMode.Child;
    [SerializeField] private TrackCancelPolicy cancelPolicy = TrackCancelPolicy.ManualOnly;

    private TriggerRegistry registry;

    public string TriggerId => triggerId != null ? triggerId.Trim() : "";
    public SequenceAsset Sequence => sequence;
    public string TrackId => trackId != null ? trackId.Trim() : "";

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        registry = FindFirstObjectByType<TriggerRegistry>();
        if (registry == null)
            registry = TriggerRegistry.Instance;

        if (registry != null)
            registry.OnTriggerFired += HandleTriggerFired;
    }

    private void OnDisable()
    {
        if (registry != null)
            registry.OnTriggerFired -= HandleTriggerFired;
        registry = null;
    }

    private void HandleTriggerFired(string firedId)
    {
        if (sequence == null || string.IsNullOrWhiteSpace(TriggerId) || string.IsNullOrWhiteSpace(TrackId))
            return;

        if (!string.Equals(firedId, TriggerId, System.StringComparison.Ordinal))
            return;

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogWarning($"[SequenceOnTrigger] SequenceRunner is missing. Trigger '{TriggerId}' was ignored.", this);
            return;
        }

        if (runner.IsChildTrackRunning(TrackId))
            return;

        if (!runner.TryStartChildTrack(TrackId, sequence, trackType, contextMode, cancelPolicy, out string error))
            Debug.LogWarning($"[SequenceOnTrigger] Could not start '{TrackId}': {error}", this);
    }
}
