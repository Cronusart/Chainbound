using UnityEngine;

/// <summary>
/// True when <see cref="TriggerRegistry.IsTriggered"/> for the ID matches <see cref="expectedTriggered"/>.
/// Use with <see cref="Step_If"/> / <see cref="Step_Branch"/> after resets or to skip duplicate waits.
/// </summary>
[CreateAssetMenu(fileName = "Condition_TriggerState", menuName = "Chainbound/Narrative/Conditions/Trigger State")]
public class ConditionTriggerState : SequenceCondition
{
    [Tooltip("Trigger ID in TriggerRegistry.")]
    [SerializeField] private string triggerId = "";

    [Tooltip("If true, condition passes when the trigger has fired; if false, when it has not.")]
    [SerializeField] private bool expectedTriggered = true;

    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogWarning("[ConditionTriggerState] Trigger ID is empty!");
            return false;
        }

        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry == null)
        {
            Debug.LogWarning("[ConditionTriggerState] TriggerRegistry not found!");
            return false;
        }

        bool isTriggered = registry.IsTriggered(triggerId);
        return isTriggered == expectedTriggered;
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogWarning("[ConditionTriggerState] Trigger ID is not set!");
            return false;
        }

        return true;
    }
}
