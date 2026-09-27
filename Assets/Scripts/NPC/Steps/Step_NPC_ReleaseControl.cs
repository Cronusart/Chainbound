using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that releases narrative control of an NPC (releases the claim from <see cref="Step_NPC_ClaimControl"/>).
/// Prefers the stored <see cref="ClaimHandle"/> in <see cref="SequenceContext"/>; falls back to
/// <see cref="NPCController.ReleaseAllClaims"/> if none is stored (legacy / external claims).
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_ReleaseControl", menuName = "Chainbound/Narrative/Steps/NPC/Release Control")]
public class Step_NPC_ReleaseControl : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to release control of (must match Claim Control step)")]
    [SerializeField] private string npcId = "";
    
    [Header("Fallback")]
    [Tooltip("If true, also call ReleaseAllClaims on the NPC when no handle was stored (clears every claim on that NPC).")]
    [SerializeField] private bool releaseAllClaimsIfNoStoredHandle = true;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_ReleaseControl] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_ReleaseControl] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        bool released = context.TryReleaseStoredNpcClaim(npcId);
        if (!released && releaseAllClaimsIfNoStoredHandle)
        {
            npc.ReleaseAllClaims();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_NPC_ReleaseControl] Released control of NPC '{npcId}' " +
                $"(storedHandle={(released ? "yes" : "no")}, fallbackReleaseAll={(!released && releaseAllClaimsIfNoStoredHandle)})");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_ReleaseControl] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

