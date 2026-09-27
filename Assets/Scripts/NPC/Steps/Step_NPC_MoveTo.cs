using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Step that moves an NPC to a waypoint ID or world position (async).
/// Supports timeout and failure policy.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_MoveTo", menuName = "Chainbound/Narrative/Steps/NPC/Move To")]
public class Step_NPC_MoveTo : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to move")]
    [SerializeField] private string npcId = "";
    
    [Header("Destination")]
    [Tooltip("Use waypoint ID (true) or world position (false)")]
    [SerializeField] private bool useWaypointId = true;
    
    [Tooltip("Waypoint ID to move to (if useWaypointId is true)")]
    [SerializeField] private string waypointId = "";
    
    [Tooltip("World position to move to (if useWaypointId is false)")]
    [SerializeField] private Vector3 worldPosition = Vector3.zero;
    
    [Header("Settings")]
    [Tooltip("Timeout in seconds (0 = no timeout)")]
    [SerializeField] private float timeout = 30f;
    
    [Header("Autonomy")]
    [Tooltip("While this step runs, suspend schedule/route autonomy so time-of-day cannot restart the route mid-movement. Restores the previous autonomy state when the step ends. Ignored when Fire And Forget is true.")]
    [SerializeField] private bool suspendAutonomyDuringStep = true;
    
    [Header("Pause for player (narrative only)")]
    [Tooltip(
        "When enabled (and Fire And Forget is false), this step temporarily suppresses the NPC's pause-for-player behavior: " +
        "the NavMesh agent keeps steering toward the destination instead of stopping while overlapping the player. " +
        "Unity avoidance and collision layers still apply; this does not knock the player aside. " +
        "Routine autonomous routes never enter this scope. Ignored when Fire And Forget is true.")]
    [FormerlySerializedAs("allowPlayerBlockerPassThroughDuringStep")]
    [SerializeField] private bool suppressPauseForPlayerDuringStep;
    
    [Header("Parallel / Dialogue")]
    [Tooltip("If true, starts MoveTo on the NPC and completes this step immediately. Use Step_NPC_Stop Locomotion to cancel movement.")]
    [SerializeField] private bool fireAndForget;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_MoveTo] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_MoveTo] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        if (useWaypointId)
        {
            if (string.IsNullOrEmpty(waypointId))
            {
                Debug.LogError("[Step_NPC_MoveTo] Waypoint ID is empty!");
                onComplete?.Invoke(StepOutcome.Failed("Waypoint ID is empty"));
                yield break;
            }
        }
        
        if (fireAndForget)
        {
            if (suppressPauseForPlayerDuringStep && enableDebugLogs)
            {
                Debug.LogWarning(
                    "[Step_NPC_MoveTo] suppressPauseForPlayerDuringStep is ignored when Fire And Forget is true; use synchronous MoveTo or drive suppress from custom code.");
            }
            
            if (useWaypointId)
            {
                if (enableDebugLogs)
                    Debug.Log($"[Step_NPC_MoveTo] Fire-and-forget move NPC '{npcId}' to waypoint '{waypointId}'");
                npc.StartCoroutine(npc.MoveToWaypoint(waypointId, timeout));
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"[Step_NPC_MoveTo] Fire-and-forget move NPC '{npcId}' to {worldPosition}");
                npc.StartCoroutine(npc.MoveToPosition(worldPosition, timeout));
            }
            
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        bool restoreAutonomy = false;
        bool enteredPlayerBlockScope = false;
        if (suspendAutonomyDuringStep)
            NPCNarrativeAutonomyScope.Enter(npc, out restoreAutonomy);
        if (suppressPauseForPlayerDuringStep)
        {
            NPCNarrativePlayerBlockScope.Enter(npc);
            enteredPlayerBlockScope = true;
        }
        
        try
        {
            if (useWaypointId)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_NPC_MoveTo] Moving NPC '{npcId}' to waypoint '{waypointId}'");
                }
                
                yield return npc.MoveToWaypoint(waypointId, timeout);
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_NPC_MoveTo] Moving NPC '{npcId}' to position {worldPosition}");
                }
                
                yield return npc.MoveToPosition(worldPosition, timeout);
            }
            
            onComplete?.Invoke(StepOutcome.Success());
        }
        finally
        {
            if (enteredPlayerBlockScope)
                NPCNarrativePlayerBlockScope.Exit(npc);
            if (suspendAutonomyDuringStep)
                NPCNarrativeAutonomyScope.Exit(npc, restoreAutonomy);
        }
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_MoveTo] NPC ID is not set!");
            return false;
        }
        
        if (useWaypointId && string.IsNullOrEmpty(waypointId))
        {
            Debug.LogWarning("[Step_NPC_MoveTo] Waypoint ID is not set!");
            return false;
        }
        
        return true;
    }
}

