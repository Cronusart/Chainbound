using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that adds/subtracts disposition value on an NPC's memory.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPCMemory_AddDisposition", menuName = "Chainbound/Narrative/Steps/NPC/Memory/Add Disposition")]
public class Step_NPCMemory_AddDisposition : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to modify disposition")]
    [SerializeField] private string npcId = "";
    
    [Header("Disposition")]
    [Tooltip("Amount to add/subtract (-100 to +100)")]
    [SerializeField] private int dispositionDelta = 0;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPCMemory_AddDisposition] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPCMemory_AddDisposition] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        NPCMemoryComponent memory = npc.GetComponent<NPCMemoryComponent>();
        if (memory == null)
        {
            Debug.LogError($"[Step_NPCMemory_AddDisposition] NPC '{npcId}' has no NPCMemoryComponent!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' has no memory component"));
            yield break;
        }
        
        memory.AddDisposition(dispositionDelta);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPCMemory_AddDisposition] Added {dispositionDelta} disposition to NPC '{npcId}' (new: {memory.Disposition})");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPCMemory_AddDisposition] NPC ID is not set!");
            return false;
        }
        
        if (dispositionDelta < -100 || dispositionDelta > 100)
        {
            Debug.LogWarning("[Step_NPCMemory_AddDisposition] Disposition delta should be between -100 and +100!");
        }
        
        return true;
    }
}

