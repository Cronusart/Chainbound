using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that conditionally jumps based on a condition.
/// </summary>
[CreateAssetMenu(fileName = "Step_If", menuName = "Chainbound/Narrative/Steps/Control Flow/If")]
public class Step_If : SequenceStep
{
    [Header("Condition")]
    [Tooltip("Condition to evaluate")]
    [SerializeField] private SequenceCondition condition;
    
    [Header("Jump Targets")]
    [Tooltip("Label to jump to if condition is true")]
    [SerializeField] private string trueLabel = "";
    
    [Tooltip("Label to jump to if condition is false (optional - continues to next step if empty)")]
    [SerializeField] private string falseLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (condition == null)
        {
            Debug.LogError("[Step_If] Condition is not set!");
            onComplete?.Invoke(StepOutcome.Failed("Condition is not set"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_If] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        bool result = condition.Evaluate(context);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_If] Condition evaluated: {result}");
        }
        
        if (result)
        {
            if (!string.IsNullOrEmpty(trueLabel))
            {
                runner.JumpToLabel(trueLabel);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(falseLabel))
            {
                runner.JumpToLabel(falseLabel);
            }
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (condition == null)
        {
            Debug.LogWarning("[Step_If] Condition is not set!");
            return false;
        }
        
        if (string.IsNullOrEmpty(trueLabel) && string.IsNullOrEmpty(falseLabel))
        {
            Debug.LogWarning("[Step_If] Both true and false labels are empty - step will do nothing!");
        }
        
        return condition.Validate();
    }
}

