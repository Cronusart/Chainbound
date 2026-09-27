using UnityEngine;

[CreateAssetMenu(fileName = "Condition_Signal", menuName = "Chainbound/Narrative/Conditions/Narrative Signal")]
public class ConditionSignal : SequenceCondition
{
    [SerializeField] private string signalId = "";

    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(signalId))
        {
            Debug.LogWarning("[ConditionSignal] signalId is empty.");
            return false;
        }
        var bus = NarrativeSignalBus.Instance;
        return bus != null && bus.HasSignal(signalId);
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(signalId))
        {
            Debug.LogWarning("[ConditionSignal] signalId is not set.");
            return false;
        }
        return true;
    }
}
