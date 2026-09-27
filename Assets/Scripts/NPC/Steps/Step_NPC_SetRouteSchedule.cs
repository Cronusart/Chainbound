using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that sets an NPC's route schedule asset (time-gated routes).
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_SetRouteSchedule", menuName = "Chainbound/Narrative/Steps/NPC/Set Route Schedule")]
public class Step_NPC_SetRouteSchedule : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to set route schedule")]
    [SerializeField] private string npcId = "";
    
    [Header("Route Schedule")]
    [Tooltip("Route schedule asset to assign")]
    [SerializeField] private NPCRouteScheduleSO routeSchedule;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_SetRouteSchedule] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_SetRouteSchedule] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        if (routeSchedule == null)
        {
            Debug.LogWarning($"[Step_NPC_SetRouteSchedule] Route schedule is null! Clearing route schedule for NPC '{npcId}'");
        }
        
        npc.SetRouteSchedule(routeSchedule);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_SetRouteSchedule] Set route schedule for NPC '{npcId}': {(routeSchedule != null ? routeSchedule.name : "null")}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_SetRouteSchedule] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

