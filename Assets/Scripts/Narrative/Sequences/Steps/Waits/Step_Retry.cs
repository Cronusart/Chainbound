using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that retries another step on failure.
/// </summary>
[CreateAssetMenu(fileName = "Step_Retry", menuName = "Chainbound/Narrative/Steps/Waits/Retry")]
public class Step_Retry : SequenceStep
{
    [Header("Inner Step")]
    [Tooltip("Step to retry")]
    [SerializeField] private SequenceStep innerStep;
    
    [Header("Retry Settings")]
    [Tooltip("Maximum number of attempts (including first attempt)")]
    [SerializeField] private int maxAttempts = 3;
    
    [Tooltip("Delay between retries in seconds")]
    [SerializeField] private float backoffSeconds = 1f;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (innerStep == null)
        {
            Debug.LogError("[Step_Retry] Inner step is not set!");
            onComplete?.Invoke(StepOutcome.Failed("Inner step is not set"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_Retry] Executing step with retry: max {maxAttempts} attempts");
        }
        
        StepOutcome finalOutcome = StepOutcome.Failed("All retry attempts failed");
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_Retry] Attempt {attempt}/{maxAttempts}");
            }
            
            // Execute inner step
            StepOutcome attemptOutcome = StepOutcome.Success();
            bool attemptCompleted = false;
            
            IEnumerator attemptCoroutine = innerStep.Execute(context, (result) =>
            {
                attemptOutcome = result;
                attemptCompleted = true;
            });
            
            // Wait for attempt to complete
            while (attemptCoroutine.MoveNext())
            {
                yield return attemptCoroutine.Current;
            }
            
            while (!attemptCompleted)
            {
                yield return null;
            }
            
            // Check if attempt succeeded
            if (attemptOutcome.Result == StepResult.Success)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_Retry] Attempt {attempt} succeeded");
                }
                onComplete?.Invoke(attemptOutcome);
                yield break;
            }
            
            // Attempt failed
            finalOutcome = attemptOutcome;
            
            // Wait before retry (except on last attempt)
            if (attempt < maxAttempts)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_Retry] Attempt {attempt} failed, waiting {backoffSeconds}s before retry");
                }
                
                float elapsed = 0f;
                while (elapsed < backoffSeconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }
        
        // All attempts failed
        if (enableDebugLogs)
        {
            Debug.LogError($"[Step_Retry] All {maxAttempts} attempts failed");
        }
        onComplete?.Invoke(finalOutcome);
    }
    
    public override bool Validate()
    {
        if (innerStep == null)
        {
            Debug.LogWarning("[Step_Retry] Inner step is not set!");
            return false;
        }
        
        if (maxAttempts <= 0)
        {
            Debug.LogWarning("[Step_Retry] Max attempts must be greater than 0!");
            return false;
        }
        
        return innerStep.Validate();
    }
}

