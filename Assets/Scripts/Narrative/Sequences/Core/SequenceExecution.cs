using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutable runtime state for one running sequence (main or child track).
/// </summary>
public sealed class SequenceExecution
{
    public string ExecutionId { get; set; }
    public string TrackId { get; set; } = "main";
    public SequenceTrackType TrackType { get; set; } = SequenceTrackType.Main;
    public SequenceAsset Sequence { get; set; }
    public SequenceContext Context { get; set; }
    /// <summary>Only the main track uses persistent checkpoint state.</summary>
    public SequenceState PersistentState { get; set; }
    public int CurrentStepIndex { get; set; } = -1;
    public bool IsRunning { get; set; }
    public StepOutcome LastStepOutcome { get; set; }
    public Dictionary<string, int> LabelIndex { get; } = new Dictionary<string, int>();
    public Stack<CallStackFrame> CallStack { get; } = new Stack<CallStackFrame>();
    public Coroutine Coroutine { get; set; }
    public TrackCancelPolicy CancelPolicy { get; set; } = TrackCancelPolicy.StopOnParentEnd;
    public bool GracefulStopRequested { get; set; }
}
