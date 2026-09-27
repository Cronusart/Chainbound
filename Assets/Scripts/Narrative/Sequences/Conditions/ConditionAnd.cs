using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Condition that evaluates multiple conditions with AND logic.
/// </summary>
[CreateAssetMenu(fileName = "Condition_And", menuName = "Chainbound/Narrative/Conditions/And")]
public class ConditionAnd : SequenceCondition
{
    [Header("Conditions")]
    [Tooltip("List of conditions (all must be true)")]
    [SerializeField] private List<SequenceCondition> conditions = new List<SequenceCondition>();
    
    public override bool Evaluate(SequenceContext context)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return true; // Empty AND is true
        }
        
        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                continue;
            }
            
            if (!condition.Evaluate(context))
            {
                return false;
            }
        }
        
        return true;
    }
    
    public override bool Validate()
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.LogWarning("[ConditionAnd] No conditions set!");
            return false;
        }
        
        foreach (var condition in conditions)
        {
            if (condition != null && !condition.Validate())
            {
                return false;
            }
        }
        
        return true;
    }
}

