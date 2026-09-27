using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Step_WaitForTimeOfDay", menuName = "Chainbound/Narrative/Steps/Waits/Wait For Time Of Day")]
public class Step_WaitForTimeOfDay : SequenceStep
{
    [SerializeField] private TimeOfDayConditionMode mode = TimeOfDayConditionMode.IsNight;
    [SerializeField] private float minNormalized = 0f;
    [SerializeField] private float maxNormalized = 1f;

    [Header("Timeout")]
    [SerializeField] private float timeoutSeconds = 0f;
    [SerializeField] private string onTimeoutLabel = "";

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        float elapsed = 0f;
        while (timeoutSeconds <= 0f || elapsed < timeoutSeconds)
        {
            if (EvaluateWait(mode, minNormalized, maxNormalized))
            {
                onComplete?.Invoke(StepOutcome.Success());
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        SequenceRunner runner = SequenceRunner.Instance;
        if (!string.IsNullOrEmpty(onTimeoutLabel) && runner != null)
        {
            runner.JumpToLabel(onTimeoutLabel);
            onComplete?.Invoke(StepOutcome.Success("Timeout — jumped to label"));
        }
        else
            onComplete?.Invoke(StepOutcome.Failed("Timeout waiting for time of day"));
    }

    private static bool EvaluateWait(TimeOfDayConditionMode m, float minN, float maxN)
    {
        var tod = TimeOfDayManager.Instance;
        if (tod == null)
            return false;
        switch (m)
        {
            case TimeOfDayConditionMode.IsNight:
                return tod.IsNight;
            case TimeOfDayConditionMode.IsDay:
                return !tod.IsNight;
            case TimeOfDayConditionMode.NormalizedTimeInRange:
                float t = Mathf.Repeat(tod.NormalizedTime, 1f);
                if (minN <= maxN)
                    return t >= minN && t <= maxN;
                return t >= minN || t <= maxN;
            default:
                return false;
        }
    }
}
