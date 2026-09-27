/// <summary>
/// Policy for handling step failures.
/// </summary>
public enum StepFailurePolicy
{
    /// <summary>Use the runner's default failure policy.</summary>
    UseRunner,
    
    /// <summary>Abort the entire sequence.</summary>
    AbortSequence,
    
    /// <summary>Skip this step and continue to the next one.</summary>
    SkipAndContinue,
    
    /// <summary>Retry this step with configured attempts.</summary>
    RetryStep,
    
    /// <summary>Jump to the specified label on failure.</summary>
    GotoLabel,
    
    /// <summary>Run a fallback sequence.</summary>
    RunFallback
}

