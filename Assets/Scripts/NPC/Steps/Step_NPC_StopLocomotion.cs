using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Stops fire-and-forget NPC follow or background MoveTo. Call after dialogue or parallel beats.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_StopLocomotion", menuName = "Chainbound/Narrative/Steps/NPC/Stop Locomotion")]
public class Step_NPC_StopLocomotion : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to stop")]
    [SerializeField] private string npcId = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_StopLocomotion] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_StopLocomotion] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        npc.StopNarrativeLocomotion();
        
        if (enableDebugLogs)
            Debug.Log($"[Step_NPC_StopLocomotion] Stopped locomotion for '{npcId}'.");
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_StopLocomotion] NPC ID is not set!");
            return false;
        }
        
        return true;
    }
}
