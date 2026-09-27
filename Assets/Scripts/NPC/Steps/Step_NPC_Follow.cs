using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Step that makes an NPC follow a target (player) within a distance band (async).
/// Use <see cref="completeWhenInFollowBand"/> for one-shot catch-up, or turn it off and set <see cref="maxDuration"/> for timed escort.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_Follow", menuName = "Chainbound/Narrative/Steps/NPC/Follow")]
public class Step_NPC_Follow : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to follow")]
    [SerializeField] private string npcId = "";
    
    [Header("Follow Settings")]
    [Tooltip("Minimum follow distance (NPC stops when closer)")]
    [SerializeField] private float minFollowDistance = 2f;
    
    [Tooltip("Outer radius of the follow band. When farther, NPC paths to this standoff distance (XZ), not to the target's pivot.")]
    [SerializeField] private float maxFollowDistance = 5f;
    
    [Tooltip("Maximum duration to follow (0 = no limit)")]
    [SerializeField] private float maxDuration = 0f;
    
    [Tooltip("Stop following when target is reached")]
    [SerializeField] private bool stopOnArrival = true;

    [Header("Completion")]
    [Tooltip(
        "If true: completes this step as soon as the NPC is within the min/max follow band on XZ (one-shot catch-up). " +
        "If false: keeps following until Max Duration (if > 0), until too close (when Stop On Arrival), or until Step_NPC_StopLocomotion / external stop — use for timed endless escort.")]
    [SerializeField] private bool completeWhenInFollowBand = false;
    
    [Tooltip("Target to follow (empty = follow player)")]
    [SerializeField] private string targetId = "";
    
    [Header("Player")]
    [Tooltip("Player tag (if following player)")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("Autonomy")]
    [Tooltip("While this step runs, suspend schedule/route autonomy so time-of-day cannot restart the route mid-follow. Restores the previous autonomy state when the step ends. Ignored when Fire And Forget is true (use Claim Control + Stop Locomotion to manage exclusivity).")]
    [SerializeField] private bool suspendAutonomyDuringStep = true;
    
    [Header("Pause for player (narrative only)")]
    [Tooltip(
        "When enabled (and Fire And Forget is false), this step temporarily suppresses the NPC's pause-for-player behavior during follow: " +
        "the NavMesh agent keeps steering instead of stopping while overlapping the player. " +
        "Unity avoidance and collision layers still apply; this does not knock the player aside. " +
        "Routine autonomous routes never enter this scope. Ignored when Fire And Forget is true.")]
    [FormerlySerializedAs("allowPlayerBlockerPassThroughDuringStep")]
    [SerializeField] private bool suppressPauseForPlayerDuringStep;
    
    [Header("Parallel / Dialogue")]
    [Tooltip("If true, starts follow on the NPC and completes this step immediately so the next step (e.g. Play Dialogue) can run in parallel. Use Step_NPC_Stop Locomotion after dialogue to stop follow.")]
    [SerializeField] private bool fireAndForget;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_Follow] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_Follow] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        // Get or create follow behavior
        NPCFollowBehavior followBehavior = npc.GetComponent<NPCFollowBehavior>();
        if (followBehavior == null)
        {
            followBehavior = npc.gameObject.AddComponent<NPCFollowBehavior>();
        }
        
        // Find target
        Transform target = null;
        if (!string.IsNullOrEmpty(targetId))
        {
            GameObject targetObj = GameObject.Find(targetId);
            if (targetObj != null)
            {
                target = targetObj.transform;
            }
        }
        
        if (target == null)
        {
            // Default to player
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                target = player.transform;
            }
        }
        
        if (target == null)
        {
            Debug.LogError("[Step_NPC_Follow] Target not found!");
            onComplete?.Invoke(StepOutcome.Failed("Target not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_NPC_Follow] NPC '{npcId}' following {target.name} (min: {minFollowDistance}, max: {maxFollowDistance}) " +
                $"completeInBand={completeWhenInFollowBand} maxDur={maxDuration} fireAndForget={fireAndForget}");
        }
        
        if (fireAndForget)
        {
            if (suppressPauseForPlayerDuringStep && enableDebugLogs)
            {
                Debug.LogWarning(
                    "[Step_NPC_Follow] suppressPauseForPlayerDuringStep is ignored when Fire And Forget is true; use synchronous Follow or drive suppress from custom code.");
            }
            
            followBehavior.StartCoroutine(
                followBehavior.FollowTarget(
                    target,
                    minFollowDistance,
                    maxFollowDistance,
                    maxDuration,
                    stopOnArrival,
                    completeWhenInFollowBand));
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
            yield return followBehavior.FollowTarget(
                target,
                minFollowDistance,
                maxFollowDistance,
                maxDuration,
                stopOnArrival,
                completeWhenInFollowBand);
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
            Debug.LogWarning("[Step_NPC_Follow] NPC ID is not set!");
            return false;
        }
        
        if (minFollowDistance >= maxFollowDistance)
        {
            Debug.LogWarning("[Step_NPC_Follow] Min follow distance must be less than max follow distance!");
            return false;
        }
        
        return true;
    }
}

