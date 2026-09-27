using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Clears a trigger ID in <see cref="TriggerRegistry"/> so <see cref="Step_WaitForEvent"/> / interact flows can fire it again.
/// Optional full unregister removes the ID from the registry entirely.
/// </summary>
[CreateAssetMenu(fileName = "Step_ResetTrigger", menuName = "Chainbound/Narrative/Steps/Reset Trigger")]
public class Step_ResetTrigger : SequenceStep
{
    [Header("Trigger")]
    [Tooltip("Trigger ID to reset (same string as FireTrigger / Wait For Event).")]
    [SerializeField] private string triggerId = "";

    [Tooltip("If true, calls UnregisterTrigger instead of only ResetTrigger (removes ID from registry).")]
    [SerializeField] private bool alsoUnregister;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogError("[Step_ResetTrigger] Trigger ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Trigger ID is empty"));
            yield break;
        }

        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError("[Step_ResetTrigger] TriggerRegistry not found!");
            onComplete?.Invoke(StepOutcome.Failed("TriggerRegistry not found"));
            yield break;
        }

        if (alsoUnregister)
            registry.UnregisterTrigger(triggerId);
        else
            registry.ResetTrigger(triggerId);

        if (enableDebugLogs)
        {
            Debug.Log(
                alsoUnregister
                    ? $"[Step_ResetTrigger] Unregistered trigger: {triggerId}"
                    : $"[Step_ResetTrigger] Reset trigger: {triggerId}");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogWarning("[Step_ResetTrigger] Trigger ID is not set!");
            return false;
        }

        return true;
    }
}
