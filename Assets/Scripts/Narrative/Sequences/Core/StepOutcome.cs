using UnityEngine;

/// <summary>
/// Result of step execution.
/// </summary>
public enum StepResult
{
    /// <summary>Step completed successfully.</summary>
    Success,
    
    /// <summary>Step is still running (async operation in progress).</summary>
    Running,
    
    /// <summary>Step failed (error occurred).</summary>
    Failed,
    
    /// <summary>Step was cancelled (sequence stopped or interrupted).</summary>
    Cancelled
}

/// <summary>
/// Outcome of a step execution, including result and optional metadata.
/// </summary>
public struct StepOutcome
{
    /// <summary>Result of the step execution.</summary>
    public StepResult Result;
    
    /// <summary>Optional message describing the outcome.</summary>
    public string Message;
    
    /// <summary>Optional label to jump to on failure (overrides failure policy).</summary>
    public string FailureLabel;
    
    /// <summary>Create a successful outcome.</summary>
    public static StepOutcome Success(string message = "")
    {
        return new StepOutcome
        {
            Result = StepResult.Success,
            Message = message,
            FailureLabel = null
        };
    }
    
    /// <summary>Create a running outcome (step continues asynchronously).</summary>
    public static StepOutcome Running(string message = "")
    {
        return new StepOutcome
        {
            Result = StepResult.Running,
            Message = message,
            FailureLabel = null
        };
    }
    
    /// <summary>Create a failed outcome.</summary>
    public static StepOutcome Failed(string message, string failureLabel = null)
    {
        return new StepOutcome
        {
            Result = StepResult.Failed,
            Message = message,
            FailureLabel = failureLabel
        };
    }
    
    /// <summary>Create a cancelled outcome.</summary>
    public static StepOutcome Cancelled(string message = "")
    {
        return new StepOutcome
        {
            Result = StepResult.Cancelled,
            Message = message,
            FailureLabel = null
        };
    }
}

