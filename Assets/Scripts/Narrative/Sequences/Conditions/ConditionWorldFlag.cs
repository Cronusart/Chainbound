using UnityEngine;

/// <summary>
/// True when <see cref="WorldState"/> has a global flag set.
/// </summary>
[CreateAssetMenu(fileName = "Condition_WorldFlag", menuName = "Chainbound/Narrative/Conditions/World Flag")]
public class ConditionWorldFlag : SequenceCondition
{
    [SerializeField] private string flagKey = "";

    [Tooltip("Required flag value when true; when false, condition passes if flag is absent or false.")]
    [SerializeField] private bool expectTrue = true;

    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(flagKey))
        {
            Debug.LogWarning("[ConditionWorldFlag] flagKey is empty.");
            return false;
        }

        var ws = WorldState.Instance;
        if (ws == null)
        {
            Debug.LogWarning("[ConditionWorldFlag] WorldState not found.");
            return false;
        }

        return ws.GetGlobalFlag(flagKey) == expectTrue;
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(flagKey))
        {
            Debug.LogWarning("[ConditionWorldFlag] flagKey is not set.");
            return false;
        }
        return true;
    }
}
