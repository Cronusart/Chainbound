using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step that switches on a variable value and jumps to matching case label.
/// </summary>
[CreateAssetMenu(fileName = "Step_Switch", menuName = "Chainbound/Narrative/Steps/Control Flow/Switch")]
public class Step_Switch : SequenceStep
{
    [System.Serializable]
    public class SwitchCase
    {
        [Tooltip("Value to match (as string - will be compared to variable)")]
        public string value;
        
        [Tooltip("Label to jump to if value matches")]
        public string label;
    }
    
    [Header("Variable")]
    [Tooltip("Variable name to switch on")]
    [SerializeField] private string variableKey = "";
    
    [Header("Cases")]
    [Tooltip("List of value-label pairs")]
    [SerializeField] private List<SwitchCase> cases = new List<SwitchCase>();
    
    [Header("Default")]
    [Tooltip("Label to jump to if no case matches (optional)")]
    [SerializeField] private string defaultLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(variableKey))
        {
            Debug.LogError("[Step_Switch] Variable key is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Variable key is empty"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_Switch] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        // Get variable value as string
        string variableValue = "";
        SequenceVariableStore vars = context.Variables;
        
        if (vars.TryGet<string>(variableKey, out string strVal))
        {
            variableValue = strVal ?? "";
        }
        else if (vars.TryGet<int>(variableKey, out int intVal))
        {
            variableValue = intVal.ToString();
        }
        else if (vars.TryGet<float>(variableKey, out float floatVal))
        {
            variableValue = floatVal.ToString();
        }
        else if (vars.TryGet<bool>(variableKey, out bool boolVal))
        {
            variableValue = boolVal.ToString();
        }
        else
        {
            Debug.LogWarning($"[Step_Switch] Variable '{variableKey}' not found or unsupported type!");
            // Use default label if variable doesn't exist
            if (!string.IsNullOrEmpty(defaultLabel))
            {
                runner.JumpToLabel(defaultLabel);
            }
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_Switch] Switching on '{variableKey}' = '{variableValue}'");
        }
        
        // Find matching case
        foreach (var switchCase in cases)
        {
            if (switchCase.value == variableValue)
            {
                if (!string.IsNullOrEmpty(switchCase.label))
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[Step_Switch] Case matched, jumping to: {switchCase.label}");
                    }
                    runner.JumpToLabel(switchCase.label);
                    onComplete?.Invoke(StepOutcome.Success());
                    yield break;
                }
            }
        }
        
        // No case matched - use default label
        if (!string.IsNullOrEmpty(defaultLabel))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_Switch] No case matched, jumping to default: {defaultLabel}");
            }
            runner.JumpToLabel(defaultLabel);
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(variableKey))
        {
            Debug.LogWarning("[Step_Switch] Variable key is not set!");
            return false;
        }
        
        if (cases == null || cases.Count == 0)
        {
            Debug.LogWarning("[Step_Switch] No switch cases set!");
            return false;
        }
        
        foreach (var switchCase in cases)
        {
            if (string.IsNullOrEmpty(switchCase.label))
            {
                Debug.LogWarning("[Step_Switch] Switch case has empty label!");
                return false;
            }
        }
        
        return true;
    }
}

