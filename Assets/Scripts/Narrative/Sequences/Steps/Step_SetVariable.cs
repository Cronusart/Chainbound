using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that sets a variable in the sequence context.
/// </summary>
[CreateAssetMenu(fileName = "Step_SetVariable", menuName = "Chainbound/Narrative/Steps/Set Variable")]
public class Step_SetVariable : SequenceStep
{
    [Header("Variable")]
    [Tooltip("Variable name")]
    [SerializeField] private string variableName = "";
    
    [Tooltip("Variable value (as string)")]
    [SerializeField] private string variableValue = "";
    
    [Tooltip("Variable type")]
    [SerializeField] private VariableType variableType = VariableType.String;
    
    [Tooltip("Variable scope")]
    [SerializeField] private VariableScope variableScope = VariableScope.Local;
    
    [Tooltip("Set player name (special case)")]
    [SerializeField] private bool setPlayerName = false;
    
    [Tooltip("Set starter selected (special case)")]
    [SerializeField] private bool setStarterSelected = false;
    
    public enum VariableType
    {
        String,
        Int,
        Float,
        Bool,
        Vector3
    }
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (setPlayerName)
        {
            context.PlayerName = variableValue;
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_SetVariable] Set PlayerName: {variableValue}");
            }
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        if (setStarterSelected)
        {
            context.StarterSelected = variableValue;
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_SetVariable] Set StarterSelected: {variableValue}");
            }
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("[Step_SetVariable] Variable name is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Variable name is empty"));
            yield break;
        }
        
        // Set typed variable
        try
        {
            switch (variableType)
            {
                case VariableType.String:
                    context.Variables.Set(variableName, variableValue, variableScope);
                    break;
                case VariableType.Int:
                    if (int.TryParse(variableValue, out int intVal))
                    {
                        context.Variables.Set(variableName, intVal, variableScope);
                    }
                    else
                    {
                        onComplete?.Invoke(StepOutcome.Failed($"Cannot parse '{variableValue}' as int"));
                        yield break;
                    }
                    break;
                case VariableType.Float:
                    if (float.TryParse(variableValue, out float floatVal))
                    {
                        context.Variables.Set(variableName, floatVal, variableScope);
                    }
                    else
                    {
                        onComplete?.Invoke(StepOutcome.Failed($"Cannot parse '{variableValue}' as float"));
                        yield break;
                    }
                    break;
                case VariableType.Bool:
                    if (bool.TryParse(variableValue, out bool boolVal))
                    {
                        context.Variables.Set(variableName, boolVal, variableScope);
                    }
                    else
                    {
                        onComplete?.Invoke(StepOutcome.Failed($"Cannot parse '{variableValue}' as bool"));
                        yield break;
                    }
                    break;
                case VariableType.Vector3:
                    string[] parts = variableValue.Split(',');
                    if (parts.Length == 3 &&
                        float.TryParse(parts[0], out float x) &&
                        float.TryParse(parts[1], out float y) &&
                        float.TryParse(parts[2], out float z))
                    {
                        context.Variables.Set(variableName, new Vector3(x, y, z), variableScope);
                    }
                    else
                    {
                        onComplete?.Invoke(StepOutcome.Failed($"Cannot parse '{variableValue}' as Vector3 (expected format: x,y,z)"));
                        yield break;
                    }
                    break;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_SetVariable] Set variable '{variableName}' ({variableType}): {variableValue}");
            }
            
            onComplete?.Invoke(StepOutcome.Success());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Step_SetVariable] Error setting variable: {ex.Message}");
            onComplete?.Invoke(StepOutcome.Failed(ex.Message));
        }
        
        yield return null;
    }
    
    public override bool Validate()
    {
        if (!setPlayerName && !setStarterSelected && string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("[Step_SetVariable] Variable name is not set!");
            return false;
        }
        return true;
    }
}

