using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that logs a debug message.
/// Useful for debugging sequence flow.
/// </summary>
[CreateAssetMenu(fileName = "Step_Log", menuName = "Chainbound/Narrative/Steps/Log")]
public class Step_Log : SequenceStep
{
    [Header("Log Message")]
    [Tooltip("Message to log")]
    [TextArea(1, 3)]
    [SerializeField] private string message = "";
    
    [Tooltip("Log level")]
    [SerializeField] private LogLevel logLevel = LogLevel.Log;
    
    public enum LogLevel
    {
        Log,
        Warning,
        Error
    }
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(message))
        {
            message = $"Step executed: {GetStepName()}";
        }
        
        // Replace variables in message
        string finalMessage = message
            .Replace("{PlayerName}", context.PlayerName)
            .Replace("{Starter}", context.StarterSelected)
            .Replace("{SequenceId}", context.CurrentSequenceId)
            .Replace("{StepIndex}", context.CurrentStepIndex.ToString());
        
        switch (logLevel)
        {
            case LogLevel.Log:
                Debug.Log($"[Sequence] {finalMessage}");
                break;
            case LogLevel.Warning:
                Debug.LogWarning($"[Sequence] {finalMessage}");
                break;
            case LogLevel.Error:
                Debug.LogError($"[Sequence] {finalMessage}");
                break;
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
}

