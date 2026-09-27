using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for sequence steps.
/// Each step performs a single action and completes asynchronously.
/// Steps are ScriptableObjects for inspector editing.
/// </summary>
public abstract class SequenceStep : ScriptableObject
{
    [Header("Step Info")]
    [Tooltip("Step name (for debugging)")]
    [SerializeField] protected string stepName = "";
    
    [Tooltip("Enable debug logging for this step")]
    [SerializeField] protected bool enableDebugLogs = false;
    
    [Header("Execute Once")]
    [Tooltip("If true, this step will only execute once per sequence (skipped on resume)")]
    [SerializeField] protected bool executeOnce = false;
    
    [Header("Failure Handling")]
    [Tooltip("How to handle failures for this step")]
    [SerializeField] protected StepFailurePolicy failurePolicy = StepFailurePolicy.UseRunner;
    
    [Tooltip("Label to jump to on failure (if failure policy is GotoLabel)")]
    [SerializeField] protected string onFailLabel = "";
    
    [Tooltip("Number of retry attempts (if failure policy is RetryStep)")]
    [SerializeField] protected int retryAttempts = 0;
    
    // GUID for execute-once tracking (auto-generated)
    [SerializeField, HideInInspector] private string stepGuid = "";
    
    /// <summary>
    /// Unique identifier for this step (for execute-once tracking).
    /// </summary>
    public string StepGuid
    {
        get
        {
            if (string.IsNullOrEmpty(stepGuid))
            {
                stepGuid = Guid.NewGuid().ToString();
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            return stepGuid;
        }
    }
    
    /// <summary>
    /// Check if this step should be skipped due to execute-once semantics.
    /// </summary>
    public bool ShouldSkip(SequenceState state)
    {
        return executeOnce && state != null && state.completedStepGuids.Contains(StepGuid);
    }
    
    /// <summary>
    /// Mark this step as completed (for execute-once tracking).
    /// </summary>
    public void MarkCompleted(SequenceState state)
    {
        if (state != null && executeOnce)
        {
            state.completedStepGuids.Add(StepGuid);
        }
    }
    
    /// <summary>
    /// Execute this step with outcome callback (new signature).
    /// </summary>
    /// <param name="context">Sequence context with variables and services</param>
    /// <param name="onComplete">Callback when step completes with outcome</param>
    /// <returns>Coroutine that completes when step finishes</returns>
    public abstract IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete);
    
    /// <summary>
    /// Legacy Execute method for backwards compatibility.
    /// Wraps the new Execute method and calls onComplete with Success.
    /// </summary>
    /// <param name="context">Sequence context with variables and services</param>
    /// <returns>Coroutine that completes when step finishes</returns>
    public virtual IEnumerator Execute(SequenceContext context)
    {
        // Adapter for backwards compatibility - call new Execute and wait for completion
        bool completed = false;
        StepOutcome outcome = StepOutcome.Success();
        
        IEnumerator newExecute = Execute(context, (result) =>
        {
            outcome = result;
            completed = true;
        });
        
        // Execute the new coroutine
        while (newExecute.MoveNext())
        {
            yield return newExecute.Current;
        }
        
        // Wait for completion callback
        while (!completed)
        {
            yield return null;
        }
        
        // If failed, log warning
        if (outcome.Result == StepResult.Failed)
        {
            Debug.LogWarning($"[SequenceStep] Step '{GetStepName()}' failed: {outcome.Message}");
        }
    }
    
    /// <summary>
    /// Get step name for debugging.
    /// </summary>
    public virtual string GetStepName()
    {
        return string.IsNullOrEmpty(stepName) ? GetType().Name : stepName;
    }
    
    /// <summary>
    /// Validate step configuration (called in editor).
    /// </summary>
    public virtual bool Validate()
    {
        if (failurePolicy == StepFailurePolicy.GotoLabel && string.IsNullOrEmpty(onFailLabel))
        {
            Debug.LogWarning($"[SequenceStep] Step '{GetStepName()}' has GotoLabel policy but no onFailLabel set!");
            return false;
        }
        
        if (failurePolicy == StepFailurePolicy.RetryStep && retryAttempts <= 0)
        {
            Debug.LogWarning($"[SequenceStep] Step '{GetStepName()}' has RetryStep policy but retryAttempts is {retryAttempts}!");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get failure policy for this step.
    /// </summary>
    public StepFailurePolicy GetFailurePolicy() => failurePolicy;
    
    /// <summary>
    /// Get on-fail label for this step.
    /// </summary>
    public string GetOnFailLabel() => onFailLabel;
    
    /// <summary>
    /// Get retry attempts for this step.
    /// </summary>
    public int GetRetryAttempts() => retryAttempts;
}

