using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that jumps to a label unconditionally.
/// </summary>
[CreateAssetMenu(fileName = "Step_JumpToLabel", menuName = "Chainbound/Narrative/Steps/Control Flow/Jump To Label")]
public class Step_JumpToLabel : SequenceStep
{
    [Header("Jump Target")]
    [Tooltip("Label name to jump to")]
    [SerializeField] private string targetLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(targetLabel))
        {
            Debug.LogError("[Step_JumpToLabel] Target label is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Target label is empty"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_JumpToLabel] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_JumpToLabel] Jumping to label: {targetLabel}");
        }
        
        runner.JumpToLabel(targetLabel);
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(targetLabel))
        {
            Debug.LogWarning("[Step_JumpToLabel] Target label is not set!");
            return false;
        }
        return true;
    }
}

