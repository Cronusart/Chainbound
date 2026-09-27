using System.Collections;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Step that waits for a trigger to fire.
/// Completes when trigger is fired.
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForTrigger", menuName = "Chainbound/Narrative/Steps/Wait For Trigger")]
public class Step_WaitForTrigger : SequenceStep
{
    [Header("Trigger")]
    [Tooltip("Trigger ID to wait for")]
    [SerializeField] private string triggerId = "";
    
    [Tooltip("Arm trigger before waiting (if not already armed)")]
    [SerializeField] private bool armTrigger = true;

    [Header("Return position")]
    [Tooltip(
        "When the trigger fires, save the player's current position/scene for post-battle return (same store as encounter return). " +
        "Pair with Start Encounter → Skip Save Return Context so the Narrative encounter does not overwrite this snapshot.")]
    [SerializeField] private bool saveReturnSnapshotOnTriggerComplete;

    [Tooltip("When saving return snapshot, optional SpawnPoint id for post-battle (authored placement).")]
    [SerializeField] private string returnSpawnPointId = "";
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogError("[Step_WaitForTrigger] Trigger ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Trigger ID is empty"));
            yield break;
        }
        
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError("[Step_WaitForTrigger] TriggerRegistry not found!");
            onComplete?.Invoke(StepOutcome.Failed("TriggerRegistry not found"));
            yield break;
        }
        
        // Arm trigger if needed
        if (armTrigger)
        {
            // Find trigger in scene and arm it
            SequenceTrigger seqTrigger = FindTriggerInScene<SequenceTrigger>(triggerId);
            if (seqTrigger != null && !seqTrigger.IsArmed)
            {
                seqTrigger.Arm();
            }
            
            DistanceTrigger distTrigger = FindTriggerInScene<DistanceTrigger>(triggerId);
            if (distTrigger != null && !distTrigger.IsArmed)
            {
                distTrigger.Arm();
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForTrigger] Waiting for trigger: {triggerId}");
        }
        
        // Wait for trigger
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner != null)
        {
            Coroutine waitCoroutine = registry.WaitForTrigger(triggerId, runner);
            if (waitCoroutine != null)
            {
                yield return waitCoroutine;
            }
        }
        
        // Double-check trigger state
        while (!registry.IsTriggered(triggerId))
        {
            yield return null;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForTrigger] Trigger fired: {triggerId}");
        }

        if (saveReturnSnapshotOnTriggerComplete)
            EncounterReturnSnapshotUtility.SaveFromActivePlayer(enableDebugLogs, returnSpawnPointId);
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    private T FindTriggerInScene<T>(string id) where T : MonoBehaviour
    {
        T[] triggers = FindObjectsOfType<T>();
        foreach (var trigger in triggers)
        {
            // Check trigger ID via public property
            if (trigger is SequenceTrigger seqTrigger && seqTrigger.TriggerId == id)
            {
                return trigger;
            }
            else if (trigger is DistanceTrigger distTrigger && distTrigger.TriggerId == id)
            {
                return trigger;
            }
        }
        return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogWarning("[Step_WaitForTrigger] Trigger ID is not set!");
            return false;
        }
        return true;
    }
}

