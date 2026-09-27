using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that increments a counter on an NPC's memory.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPCMemory_IncrementCounter", menuName = "Chainbound/Narrative/Steps/NPC/Memory/Increment Counter")]
public class Step_NPCMemory_IncrementCounter : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to increment counter on")]
    [SerializeField] private string npcId = "";
    
    [Header("Counter")]
    [Tooltip("Counter key name")]
    [SerializeField] private string counterKey = "";
    
    [Tooltip("Amount to increment (can be negative to decrement)")]
    [SerializeField] private int incrementAmount = 1;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPCMemory_IncrementCounter] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPCMemory_IncrementCounter] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        NPCMemoryComponent memory = npc.GetComponent<NPCMemoryComponent>();
        if (memory == null)
        {
            Debug.LogError($"[Step_NPCMemory_IncrementCounter] NPC '{npcId}' has no NPCMemoryComponent!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' has no memory component"));
            yield break;
        }
        
        if (string.IsNullOrEmpty(counterKey))
        {
            Debug.LogError("[Step_NPCMemory_IncrementCounter] Counter key is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Counter key is empty"));
            yield break;
        }
        
        memory.IncrementCounter(counterKey, incrementAmount);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPCMemory_IncrementCounter] Incremented counter '{counterKey}' by {incrementAmount} on NPC '{npcId}' (new: {memory.GetCounter(counterKey)})");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPCMemory_IncrementCounter] NPC ID is not set!");
            return false;
        }
        
        if (string.IsNullOrEmpty(counterKey))
        {
            Debug.LogWarning("[Step_NPCMemory_IncrementCounter] Counter key is not set!");
            return false;
        }
        
        return true;
    }
}

