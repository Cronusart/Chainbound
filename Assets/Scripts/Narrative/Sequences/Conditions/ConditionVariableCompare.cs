using UnityEngine;

/// <summary>
/// Condition that compares a variable to a value.
/// </summary>
[CreateAssetMenu(fileName = "Condition_VariableCompare", menuName = "Chainbound/Narrative/Conditions/Variable Compare")]
public class ConditionVariableCompare : SequenceCondition
{
    [Header("Variable")]
    [Tooltip("Variable name to check")]
    [SerializeField] private string variableName = "";
    
    [Header("Comparison")]
    [Tooltip("Type of comparison")]
    [SerializeField] private ComparisonType comparisonType = ComparisonType.Equals;
    
    [Header("Value (as string - will be parsed based on variable type)")]
    [Tooltip("Value to compare against")]
    [SerializeField] private string compareValue = "";
    
    public enum ComparisonType
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }
    
    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("[ConditionVariableCompare] Variable name is empty!");
            return false;
        }
        
        // Try to get variable as different types
        SequenceVariableStore vars = context.Variables;
        
        // Try bool first
        if (vars.TryGet<bool>(variableName, out bool boolValue))
        {
            if (bool.TryParse(compareValue, out bool compareBool))
            {
                return CompareBool(boolValue, compareBool);
            }
        }
        
        // Try int
        if (vars.TryGet<int>(variableName, out int intValue))
        {
            if (int.TryParse(compareValue, out int compareInt))
            {
                return CompareInt(intValue, compareInt);
            }
        }
        
        // Try float
        if (vars.TryGet<float>(variableName, out float floatValue))
        {
            if (float.TryParse(compareValue, out float compareFloat))
            {
                return CompareFloat(floatValue, compareFloat);
            }
        }
        
        // Try string
        if (vars.TryGet<string>(variableName, out string stringValue))
        {
            return CompareString(stringValue ?? "", compareValue ?? "");
        }
        
        // Variable doesn't exist or type mismatch
        return false;
    }
    
    private bool CompareBool(bool value, bool compare)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equals:
                return value == compare;
            case ComparisonType.NotEquals:
                return value != compare;
            default:
                Debug.LogWarning("[ConditionVariableCompare] Invalid comparison type for bool!");
                return false;
        }
    }
    
    private bool CompareInt(int value, int compare)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equals:
                return value == compare;
            case ComparisonType.NotEquals:
                return value != compare;
            case ComparisonType.GreaterThan:
                return value > compare;
            case ComparisonType.LessThan:
                return value < compare;
            case ComparisonType.GreaterThanOrEqual:
                return value >= compare;
            case ComparisonType.LessThanOrEqual:
                return value <= compare;
            default:
                return false;
        }
    }
    
    private bool CompareFloat(float value, float compare)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equals:
                return Mathf.Approximately(value, compare);
            case ComparisonType.NotEquals:
                return !Mathf.Approximately(value, compare);
            case ComparisonType.GreaterThan:
                return value > compare;
            case ComparisonType.LessThan:
                return value < compare;
            case ComparisonType.GreaterThanOrEqual:
                return value >= compare;
            case ComparisonType.LessThanOrEqual:
                return value <= compare;
            default:
                return false;
        }
    }
    
    private bool CompareString(string value, string compare)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equals:
                return value == compare;
            case ComparisonType.NotEquals:
                return value != compare;
            default:
                Debug.LogWarning("[ConditionVariableCompare] Invalid comparison type for string!");
                return false;
        }
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("[ConditionVariableCompare] Variable name is not set!");
            return false;
        }
        return true;
    }
}

