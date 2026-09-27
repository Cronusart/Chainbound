using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step that evaluates multiple conditions and jumps to the first matching label.
/// </summary>
[CreateAssetMenu(fileName = "Step_Branch", menuName = "Chainbound/Narrative/Steps/Control Flow/Branch")]
public class Step_Branch : SequenceStep
{
    [System.Serializable]
    public class BranchCase
    {
        [Tooltip("Condition to evaluate")]
        public SequenceCondition condition;
        
        [Tooltip("Label to jump to if condition is true")]
        public string label;
    }
    
    [Header("Branch Cases")]
    [Tooltip("List of condition-label pairs (evaluated in order)")]
    [SerializeField] private List<BranchCase> cases = new List<BranchCase>();
    
    [Header("Default")]
    [Tooltip("Label to jump to if no condition matches (optional)")]
    [SerializeField] private string defaultLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_Branch] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        // Evaluate cases in order
        foreach (var branchCase in cases)
        {
            if (branchCase.condition == null)
            {
                continue;
            }
            
            if (branchCase.condition.Evaluate(context))
            {
                if (!string.IsNullOrEmpty(branchCase.label))
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[Step_Branch] Condition matched, jumping to: {branchCase.label}");
                    }
                    runner.JumpToLabel(branchCase.label);
                    onComplete?.Invoke(StepOutcome.Success());
                    yield break;
                }
            }
        }
        
        // No condition matched - use default label
        if (!string.IsNullOrEmpty(defaultLabel))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_Branch] No condition matched, jumping to default: {defaultLabel}");
            }
            runner.JumpToLabel(defaultLabel);
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (cases == null || cases.Count == 0)
        {
            Debug.LogWarning("[Step_Branch] No branch cases set!");
            return false;
        }
        
        foreach (var branchCase in cases)
        {
            if (branchCase.condition == null)
            {
                Debug.LogWarning("[Step_Branch] Branch case has null condition!");
                return false;
            }
            
            if (string.IsNullOrEmpty(branchCase.label))
            {
                Debug.LogWarning("[Step_Branch] Branch case has empty label!");
                return false;
            }
            
            if (!branchCase.condition.Validate())
            {
                return false;
            }
        }
        
        return true;
    }
}

