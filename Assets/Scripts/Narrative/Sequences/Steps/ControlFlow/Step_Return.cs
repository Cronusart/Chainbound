using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that returns from a called sequence.
/// </summary>
[CreateAssetMenu(fileName = "Step_Return", menuName = "Chainbound/Narrative/Steps/Control Flow/Return")]
public class Step_Return : SequenceStep
{
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_Return] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        // Pop call stack
        CallStackFrame returnFrame = runner.PopCallStack();
        
        if (returnFrame == null)
        {
            Debug.LogWarning("[Step_Return] No call stack frame to return to! Ending sequence.");
            runner.StopSequence();
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_Return] Returning to sequence: {returnFrame.Sequence?.name} at step {returnFrame.ReturnStepIndex}");
        }
        
        // Return to calling sequence
        runner.JumpToStep(returnFrame.ReturnStepIndex);
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}

