using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that sets an NPC's animation state (Idle/Walk/Run).
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_SetAnimState", menuName = "Chainbound/Narrative/Steps/NPC/Set Anim State")]
public class Step_NPC_SetAnimState : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to set animation state")]
    [SerializeField] private string npcId = "";
    
    [Header("Animation State")]
    [Tooltip("Animation state to set")]
    [SerializeField] private NPCAnimationState animState = NPCAnimationState.Idle;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_SetAnimState] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_SetAnimState] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        // Get visual component
        MeshRendererNPCVisual visual = npc.GetComponentInChildren<MeshRendererNPCVisual>();
        if (visual == null)
        {
            Debug.LogError($"[Step_NPC_SetAnimState] NPC '{npcId}' has no MeshRendererNPCVisual component!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' has no visual component"));
            yield break;
        }
        
        visual.SetAnimationState(animState);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_SetAnimState] Set NPC '{npcId}' animation state to: {animState}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_SetAnimState] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

