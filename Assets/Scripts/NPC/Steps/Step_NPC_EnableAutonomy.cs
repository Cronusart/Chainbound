using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that enables or disables NPC autonomy (route following + micro-wander).
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_EnableAutonomy", menuName = "Chainbound/Narrative/Steps/NPC/Enable Autonomy")]
public class Step_NPC_EnableAutonomy : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to enable/disable autonomy")]
    [SerializeField] private string npcId = "";
    
    [Header("Settings")]
    [Tooltip("Enable or disable autonomy")]
    [SerializeField] private bool enable = true;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_EnableAutonomy] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_EnableAutonomy] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        npc.SetAutonomyEnabled(enable);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_EnableAutonomy] {(enable ? "Enabled" : "Disabled")} autonomy for NPC '{npcId}'");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_EnableAutonomy] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

