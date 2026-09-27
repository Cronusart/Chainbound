using UnityEngine;

/// <summary>
/// Condition that negates another condition.
/// </summary>
[CreateAssetMenu(fileName = "Condition_Not", menuName = "Chainbound/Narrative/Conditions/Not")]
public class ConditionNot : SequenceCondition
{
    [Header("Condition")]
    [Tooltip("Condition to negate")]
    [SerializeField] private SequenceCondition condition;
    
    public override bool Evaluate(SequenceContext context)
    {
        if (condition == null)
        {
            Debug.LogWarning("[ConditionNot] Condition is not set!");
            return false;
        }
        
        return !condition.Evaluate(context);
    }
    
    public override bool Validate()
    {
        if (condition == null)
        {
            Debug.LogWarning("[ConditionNot] Condition is not set!");
            return false;
        }
        
        return condition.Validate();
    }
}

