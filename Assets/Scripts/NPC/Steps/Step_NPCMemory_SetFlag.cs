using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that sets a boolean flag on an NPC's memory.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPCMemory_SetFlag", menuName = "Chainbound/Narrative/Steps/NPC/Memory/Set Flag")]
public class Step_NPCMemory_SetFlag : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to set flag on")]
    [SerializeField] private string npcId = "";
    
    [Header("Flag")]
    [Tooltip("Flag key name")]
    [SerializeField] private string flagKey = "";
    
    [Tooltip("Flag value")]
    [SerializeField] private bool flagValue = true;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPCMemory_SetFlag] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPCMemory_SetFlag] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        NPCMemoryComponent memory = npc.GetComponent<NPCMemoryComponent>();
        if (memory == null)
        {
            Debug.LogError($"[Step_NPCMemory_SetFlag] NPC '{npcId}' has no NPCMemoryComponent!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' has no memory component"));
            yield break;
        }
        
        if (string.IsNullOrEmpty(flagKey))
        {
            Debug.LogError("[Step_NPCMemory_SetFlag] Flag key is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Flag key is empty"));
            yield break;
        }
        
        memory.SetFlag(flagKey, flagValue);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPCMemory_SetFlag] Set flag '{flagKey}' = {flagValue} on NPC '{npcId}'");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPCMemory_SetFlag] NPC ID is not set!");
            return false;
        }
        
        if (string.IsNullOrEmpty(flagKey))
        {
            Debug.LogWarning("[Step_NPCMemory_SetFlag] Flag key is not set!");
            return false;
        }
        
        return true;
    }
}

