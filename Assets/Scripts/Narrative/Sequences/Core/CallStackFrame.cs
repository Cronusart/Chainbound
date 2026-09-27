using UnityEngine;

/// <summary>
/// Frame in the sequence call stack for tracking sub-sequence calls.
/// </summary>
public class CallStackFrame
{
    /// <summary>Sequence asset that was called.</summary>
    public SequenceAsset Sequence;
    
    /// <summary>Step index to return to after sub-sequence completes.</summary>
    public int ReturnStepIndex;
    
    /// <summary>Label to return to (alternative to step index).</summary>
    public string ReturnLabel;
    
    /// <summary>Context variables at call time (for parameter passing).</summary>
    public SequenceContext ContextSnapshot;
    
    public CallStackFrame(SequenceAsset sequence, int returnStepIndex, string returnLabel = null)
    {
        Sequence = sequence;
        ReturnStepIndex = returnStepIndex;
        ReturnLabel = returnLabel;
    }
}

