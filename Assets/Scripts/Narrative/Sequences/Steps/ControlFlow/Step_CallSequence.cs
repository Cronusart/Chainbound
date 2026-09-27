using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step that calls a sub-sequence and returns when it completes.
/// </summary>
[CreateAssetMenu(fileName = "Step_CallSequence", menuName = "Chainbound/Narrative/Steps/Control Flow/Call Sequence")]
public class Step_CallSequence : SequenceStep
{
    [System.Serializable]
    public class ParameterBinding
    {
        [Tooltip("Variable name in called sequence")]
        public string targetVariable;
        
        [Tooltip("Variable name in current context (or literal value)")]
        public string sourceVariable;
        
        [Tooltip("If true, sourceVariable is treated as a literal value")]
        public bool isLiteral;
    }
    
    [Header("Sub-Sequence")]
    [Tooltip("Sequence asset to call")]
    [SerializeField] private SequenceAsset subSequence;
    
    [Header("Parameter Bindings")]
    [Tooltip("Variable bindings to pass to sub-sequence")]
    [SerializeField] private List<ParameterBinding> parameterBindings = new List<ParameterBinding>();
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (subSequence == null)
        {
            Debug.LogError("[Step_CallSequence] Sub-sequence is not set!");
            onComplete?.Invoke(StepOutcome.Failed("Sub-sequence is not set"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_CallSequence] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_CallSequence] Calling sub-sequence: {subSequence.name}");
        }
        
        // Create call stack frame
        CallStackFrame frame = new CallStackFrame(
            runner.CurrentSequence,
            runner.CurrentStepIndex + 1, // Return to next step
            null
        );
        
        // Push call stack
        runner.PushCallStack(frame);
        
        // Apply parameter bindings to context
        foreach (var binding in parameterBindings)
        {
            if (string.IsNullOrEmpty(binding.targetVariable))
            {
                continue;
            }
            
            if (binding.isLiteral)
            {
                // Set literal value
                context.Variables.Set<string>(binding.targetVariable, binding.sourceVariable ?? "", VariableScope.Local);
            }
            else
            {
                // Copy variable from source to target
                if (context.Variables.TryGet<object>(binding.sourceVariable, out object sourceValue))
                {
                    // Try to preserve type
                    if (sourceValue is bool b) context.Variables.Set(binding.targetVariable, b, VariableScope.Local);
                    else if (sourceValue is int i) context.Variables.Set(binding.targetVariable, i, VariableScope.Local);
                    else if (sourceValue is float f) context.Variables.Set(binding.targetVariable, f, VariableScope.Local);
                    else if (sourceValue is Vector3 v3) context.Variables.Set(binding.targetVariable, v3, VariableScope.Local);
                    else context.Variables.Set<string>(binding.targetVariable, sourceValue?.ToString() ?? "", VariableScope.Local);
                }
            }
        }
        
        // Save current sequence and switch to sub-sequence
        SequenceAsset currentSeq = runner.CurrentSequence;
        int currentIndex = runner.CurrentStepIndex;
        
        // Start sub-sequence
        runner.StartSequence(subSequence);
        
        // Wait for sub-sequence to complete
        while (runner.IsRunning && runner.CurrentSequence == subSequence)
        {
            yield return null;
        }
        
        // Sub-sequence completed - restore context
        // Note: Variable bindings are local, so they're automatically cleared
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_CallSequence] Sub-sequence completed, returning to: {currentSeq?.name}");
        }
        
        // Pop call stack
        CallStackFrame returnFrame = runner.PopCallStack();
        
        // Return to calling sequence
        if (returnFrame != null && returnFrame.Sequence == currentSeq)
        {
            runner.JumpToStep(returnFrame.ReturnStepIndex);
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    public override bool Validate()
    {
        if (subSequence == null)
        {
            Debug.LogWarning("[Step_CallSequence] Sub-sequence is not set!");
            return false;
        }
        return true;
    }
}

