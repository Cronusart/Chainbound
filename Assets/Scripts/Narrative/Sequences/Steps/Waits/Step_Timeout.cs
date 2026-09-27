using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that wraps another step with a timeout.
/// </summary>
[CreateAssetMenu(fileName = "Step_Timeout", menuName = "Chainbound/Narrative/Steps/Waits/Timeout")]
public class Step_Timeout : SequenceStep
{
    [Header("Inner Step")]
    [Tooltip("Step to execute with timeout")]
    [SerializeField] private SequenceStep innerStep;
    
    [Header("Timeout")]
    [Tooltip("Timeout in seconds")]
    [SerializeField] private float timeoutSeconds = 5f;
    
    [Header("On Timeout")]
    [Tooltip("Label to jump to on timeout (optional)")]
    [SerializeField] private string onTimeoutLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (innerStep == null)
        {
            Debug.LogError("[Step_Timeout] Inner step is not set!");
            onComplete?.Invoke(StepOutcome.Failed("Inner step is not set"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_Timeout] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_Timeout] Executing step with timeout: {timeoutSeconds}s");
        }
        
        // Execute inner step
        StepOutcome innerOutcome = StepOutcome.Success();
        bool innerCompleted = false;
        
        IEnumerator innerCoroutine = innerStep.Execute(context, (result) =>
        {
            innerOutcome = result;
            innerCompleted = true;
        });
        
        // Wait for inner step or timeout
        float elapsed = 0f;
        bool timedOut = false;
        
        while (!innerCompleted && elapsed < timeoutSeconds)
        {
            if (innerCoroutine.MoveNext())
            {
                yield return innerCoroutine.Current;
            }
            else
            {
                // Coroutine finished but callback not called yet
                yield return null;
            }
            
            elapsed += Time.deltaTime;
            
            if (elapsed >= timeoutSeconds)
            {
                timedOut = true;
                break;
            }
        }
        
        if (timedOut)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[Step_Timeout] Step timed out after {timeoutSeconds}s");
            }
            
            if (!string.IsNullOrEmpty(onTimeoutLabel))
            {
                runner.JumpToLabel(onTimeoutLabel);
                onComplete?.Invoke(StepOutcome.Success("Timeout - jumped to label"));
            }
            else
            {
                onComplete?.Invoke(StepOutcome.Failed($"Step timed out after {timeoutSeconds}s"));
            }
        }
        else
        {
            // Inner step completed
            onComplete?.Invoke(innerOutcome);
        }
    }
    
    public override bool Validate()
    {
        if (innerStep == null)
        {
            Debug.LogWarning("[Step_Timeout] Inner step is not set!");
            return false;
        }
        
        if (timeoutSeconds <= 0f)
        {
            Debug.LogWarning("[Step_Timeout] Timeout must be greater than 0!");
            return false;
        }
        
        return innerStep.Validate();
    }
}

