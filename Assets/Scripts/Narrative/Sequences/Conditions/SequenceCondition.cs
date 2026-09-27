using UnityEngine;

/// <summary>
/// Base class for sequence conditions.
/// Conditions evaluate to true/false based on context state.
/// </summary>
public abstract class SequenceCondition : ScriptableObject
{
    /// <summary>
    /// Evaluate this condition.
    /// </summary>
    /// <param name="context">Sequence context with variables and services</param>
    /// <returns>True if condition is met, false otherwise</returns>
    public abstract bool Evaluate(SequenceContext context);
    
    /// <summary>
    /// Validate condition configuration (called in editor).
    /// </summary>
    public virtual bool Validate()
    {
        return true;
    }
}

