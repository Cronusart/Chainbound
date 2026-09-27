using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that claims control of an NPC (Hard or Soft claim).
/// Returns a handle that should be released later with Step_NPC_ReleaseControl.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_ClaimControl", menuName = "Chainbound/Narrative/Steps/NPC/Claim Control")]
public class Step_NPC_ClaimControl : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to claim control of")]
    [SerializeField] private string npcId = "";
    
    [Header("Claim Type")]
    [Tooltip("Hard: Frozen immediately. Soft: Suppressed (finish current micro-action then yield)")]
    [SerializeField] private ClaimType claimType = ClaimType.Hard;
    
    [Tooltip("Reason for claiming (for debugging)")]
    [SerializeField] private string reason = "";
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_ClaimControl] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_ClaimControl] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        ClaimHandle handle = npc.ClaimControl(claimType, reason);
        context.StoreNpcClaim(npcId, handle);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_ClaimControl] Claimed control of NPC '{npcId}' ({claimType}): '{reason}'");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_ClaimControl] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

