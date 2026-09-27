using UnityEngine;

public enum TimeOfDayConditionMode
{
    IsNight,
    IsDay,
    NormalizedTimeInRange
}

/// <summary>
/// Evaluates <see cref="TimeOfDayManager"/> state for branching.
/// </summary>
[CreateAssetMenu(fileName = "Condition_TimeOfDay", menuName = "Chainbound/Narrative/Conditions/Time Of Day")]
public class ConditionTimeOfDay : SequenceCondition
{
    [SerializeField] private TimeOfDayConditionMode mode = TimeOfDayConditionMode.IsNight;

    [Tooltip("Inclusive range when mode is NormalizedTimeInRange. If min &gt; max, matches if t &gt;= min OR t &lt;= max.")]
    [SerializeField] private float minNormalized = 0f;
    [SerializeField] private float maxNormalized = 1f;

    public override bool Evaluate(SequenceContext context)
    {
        var tod = TimeOfDayManager.Instance;
        if (tod == null)
        {
            Debug.LogWarning("[ConditionTimeOfDay] TimeOfDayManager not found.");
            return false;
        }

        switch (mode)
        {
            case TimeOfDayConditionMode.IsNight:
                return tod.IsNight;
            case TimeOfDayConditionMode.IsDay:
                return !tod.IsNight;
            case TimeOfDayConditionMode.NormalizedTimeInRange:
                float t = Mathf.Repeat(tod.NormalizedTime, 1f);
                if (minNormalized <= maxNormalized)
                    return t >= minNormalized && t <= maxNormalized;
                return t >= minNormalized || t <= maxNormalized;
            default:
                return false;
        }
    }

    public override bool Validate() => true;
}
