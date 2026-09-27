using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that makes an NPC face a target (player, waypoint, or direction).
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_FaceTarget", menuName = "Chainbound/Narrative/Steps/NPC/Face Target")]
public class Step_NPC_FaceTarget : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to face target")]
    [SerializeField] private string npcId = "";
    
    [Header("Target")]
    [Tooltip("Type of target to face")]
    [SerializeField] private FaceTargetType targetType = FaceTargetType.Player;
    
    [Tooltip("Target ID (waypoint ID if targetType is WaypointId, ignored otherwise)")]
    [SerializeField] private string targetId = "";
    
    [Tooltip("Used when Target Type is Direction (world / camera / player-relative).")]
    [SerializeField] private NPCFaceDirectionPreset directionPreset = NPCFaceDirectionPreset.WorldNorth;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_FaceTarget] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_FaceTarget] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        if (targetType == FaceTargetType.WaypointId && string.IsNullOrEmpty(targetId))
        {
            Debug.LogError("[Step_NPC_FaceTarget] Target ID is empty for WaypointId type!");
            onComplete?.Invoke(StepOutcome.Failed("Target ID is empty"));
            yield break;
        }
        
        if (targetType == FaceTargetType.Direction)
            npc.FaceDirection(directionPreset);
        else
            npc.FaceTarget(targetId, targetType);
        
        if (enableDebugLogs)
        {
            if (targetType == FaceTargetType.Direction)
                Debug.Log($"[Step_NPC_FaceTarget] NPC '{npcId}' facing preset {directionPreset}");
            else
                Debug.Log($"[Step_NPC_FaceTarget] NPC '{npcId}' facing {targetType} (ID: {targetId})");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_FaceTarget] NPC ID is not set!");
            return false;
        }
        
        if (targetType == FaceTargetType.WaypointId && string.IsNullOrEmpty(targetId))
        {
            Debug.LogWarning("[Step_NPC_FaceTarget] Target ID is not set for WaypointId type!");
            return false;
        }
        
        return true;
    }
}

