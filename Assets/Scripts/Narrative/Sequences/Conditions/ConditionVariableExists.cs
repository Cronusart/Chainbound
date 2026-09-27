using UnityEngine;

/// <summary>
/// Condition that checks if a variable exists.
/// </summary>
[CreateAssetMenu(fileName = "Condition_VariableExists", menuName = "Chainbound/Narrative/Conditions/Variable Exists")]
public class ConditionVariableExists : SequenceCondition
{
    [Header("Variable")]
    [Tooltip("Variable name to check")]
    [SerializeField] private string variableName = "";
    
    [Tooltip("If true, checks that variable does NOT exist")]
    [SerializeField] private bool negate = false;
    
    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(variableName))
        {
            return false;
        }
        
        bool exists = context.Variables.HasKey(variableName);
        return negate ? !exists : exists;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("[ConditionVariableExists] Variable name is not set!");
            return false;
        }
        return true;
    }
}

