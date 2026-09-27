using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that waits for an event to fire (using TriggerRegistry).
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForEvent", menuName = "Chainbound/Narrative/Steps/Waits/Wait For Event")]
public class Step_WaitForEvent : SequenceStep
{
    [Header("Event")]
    [Tooltip("Event ID to wait for")]
    [SerializeField] private string eventId = "";
    
    [Header("Timeout")]
    [Tooltip("Timeout in seconds (0 = no timeout)")]
    [SerializeField] private float timeoutSeconds = 0f;
    
    [Tooltip("Label to jump to on timeout (optional)")]
    [SerializeField] private string onTimeoutLabel = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogError("[Step_WaitForEvent] Event ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Event ID is empty"));
            yield break;
        }
        
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError("[Step_WaitForEvent] TriggerRegistry not found!");
            onComplete?.Invoke(StepOutcome.Failed("TriggerRegistry not found"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_WaitForEvent] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForEvent] Waiting for event: {eventId}");
        }
        
        // Check if already triggered
        if (registry.IsTriggered(eventId))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_WaitForEvent] Event already triggered: {eventId}");
            }
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        // Register event callback
        bool eventFired = false;
        System.Action<string> onEventFired = (triggerId) =>
        {
            if (triggerId == eventId)
            {
                eventFired = true;
            }
        };
        
        // Subscribe to event
        registry.OnTriggerFired += onEventFired;
        
        // Wait for event or timeout
        float elapsed = 0f;
        while (!eventFired && (timeoutSeconds <= 0f || elapsed < timeoutSeconds))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Unsubscribe
        registry.OnTriggerFired -= onEventFired;
        
        if (eventFired)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_WaitForEvent] Event fired: {eventId}");
            }
            onComplete?.Invoke(StepOutcome.Success());
        }
        else
        {
            // Timeout
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[Step_WaitForEvent] Timeout waiting for event: {eventId}");
            }
            
            if (!string.IsNullOrEmpty(onTimeoutLabel))
            {
                runner.JumpToLabel(onTimeoutLabel);
                onComplete?.Invoke(StepOutcome.Success("Timeout - jumped to label"));
            }
            else
            {
                onComplete?.Invoke(StepOutcome.Failed($"Timeout waiting for event: {eventId}"));
            }
        }
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogWarning("[Step_WaitForEvent] Event ID is not set!");
            return false;
        }
        return true;
    }
}

