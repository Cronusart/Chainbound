using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Condition that evaluates multiple conditions with OR logic.
/// </summary>
[CreateAssetMenu(fileName = "Condition_Or", menuName = "Chainbound/Narrative/Conditions/Or")]
public class ConditionOr : SequenceCondition
{
    [Header("Conditions")]
    [Tooltip("List of conditions (at least one must be true)")]
    [SerializeField] private List<SequenceCondition> conditions = new List<SequenceCondition>();
    
    public override bool Evaluate(SequenceContext context)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return false; // Empty OR is false
        }
        
        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                continue;
            }
            
            if (condition.Evaluate(context))
            {
                return true;
            }
        }
        
        return false;
    }
    
    public override bool Validate()
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.LogWarning("[ConditionOr] No conditions set!");
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

