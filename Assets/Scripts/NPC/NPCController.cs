using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main NPC coordinator. Handles interrupt policy, route overrides (token-based),
/// claim/release from narrative system, and routes autonomy to movement.
/// </summary>
[RequireComponent(typeof(NPCMovementAgent))]
[RequireComponent(typeof(NPCStateBlackboard))]
public class NPCController : MonoBehaviour
{
    [Header("NPC Identity")]
    [Tooltip("Unique ID for this NPC (used by sequence steps)")]
    [SerializeField] private string npcId = "";
    
    [Header("Interrupt Policy")]
    [Tooltip("Current interrupt policy affecting autonomy")]
    [SerializeField] private NPCInterruptPolicy interruptPolicy = NPCInterruptPolicy.Normal;
    
    [Header("Autonomy")]
    [Tooltip("Enable autonomy (route following from schedule)")]
    [SerializeField] private bool autonomyEnabled = true;
    
    [Tooltip("Route schedule asset (time-gated routes)")]
    [SerializeField] private NPCRouteScheduleSO routeSchedule;

    [Header("Normal Interaction")]
    [Tooltip("Optional default behavior used for standard interact-and-talk sessions on this NPC.")]
    [SerializeField] private NPCInteractionBehaviorSO interactionBehavior;
    
    [Header("References")]
    [SerializeField] private NPCMovementAgent movementAgent;
    [SerializeField] private NPCStateBlackboard blackboard;
    [SerializeField] private NPCMemoryComponent memory;
    [SerializeField] private MeshRendererNPCVisual visual;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Claim tracking (narrative control)
    private int nextClaimTokenId = 1;
    private readonly Dictionary<int, ClaimToken> activeClaims = new Dictionary<int, ClaimToken>();
    
    private struct ClaimToken
    {
        public int id;
        public ClaimType type;
        public string reason;
    }
    
    // Route override tracking (stackable, deterministic)
    private int nextOverrideTokenId = 1;
    private readonly List<RouteOverrideEntry> activeOverrides = new List<RouteOverrideEntry>();
    
    private class RouteOverrideEntry
    {
        public int tokenId;
        public RouteOverrideMode mode;
        public NPCRouteAsset forceRoute;
        public string reason;
    }
    
    // Time-of-day subscription
    private ITimeOfDayService timeService;
    private TimeSegment lastKnownSegment;
    
    // Events
    public event Action<NPCInterruptPolicy> OnInterruptPolicyChanged;
    public event Action<bool> OnAutonomyEnabledChanged;
    public event Action<RouteOverrideMode> OnRouteOverrideChanged;
    
    // Properties
    public string NPCId => npcId;
    public NPCInterruptPolicy InterruptPolicy => interruptPolicy;
    public bool AutonomyEnabled => autonomyEnabled;
    public NPCRouteScheduleSO RouteSchedule => routeSchedule;
    public NPCInteractionBehaviorSO InteractionBehavior => interactionBehavior;
    public RouteOverrideMode CurrentOverrideMode => GetEffectiveOverrideMode();
    
    private void Awake()
    {
        // Auto-find components
        if (movementAgent == null)
            movementAgent = GetComponent<NPCMovementAgent>();
        if (blackboard == null)
            blackboard = GetComponent<NPCStateBlackboard>();
        if (memory == null)
            memory = GetComponent<NPCMemoryComponent>();
        if (visual == null)
            visual = GetComponentInChildren<MeshRendererNPCVisual>();
        
        // Register with registry
        if (!string.IsNullOrEmpty(npcId))
        {
            NPCRegistry.Instance.RegisterNPC(npcId, this);
        }
        else
        {
            Debug.LogWarning($"[NPCController] NPC on {gameObject.name} has no npcId! It will not be accessible via registry.");
        }
        
        // Find time service
        timeService = FindObjectOfType<TimeOfDayManager>();
        if (timeService == null)
        {
            timeService = FindObjectOfType<MonoBehaviour>() as ITimeOfDayService;
        }
        
        if (timeService != null)
        {
            timeService.OnSegmentChanged += OnTimeSegmentChanged;
            lastKnownSegment = timeService.CurrentSegment;
        }
    }
    
    private void Start()
    {
        // Apply initial interrupt policy
        ApplyInterruptPolicy(interruptPolicy);
        
        // Apply initial route from schedule (if no overrides)
        ApplyEffectiveRoute();
    }
    
    private void OnDestroy()
    {
        // Unregister from registry
        if (!string.IsNullOrEmpty(npcId))
        {
            NPCRegistry.Instance.UnregisterNPC(npcId);
        }
        
        // Unsubscribe from time service
        if (timeService != null)
        {
            timeService.OnSegmentChanged -= OnTimeSegmentChanged;
        }
    }
    
    private void OnTimeSegmentChanged(TimeSegment oldSegment, TimeSegment newSegment)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} time segment changed: {oldSegment} -> {newSegment}");
        }
        
        lastKnownSegment = newSegment;
        
        // Re-apply route (respects overrides)
        ApplyEffectiveRoute();
    }
    
    // ============================================
    // ROUTE OVERRIDE SYSTEM (Token-Based)
    // ============================================
    
    /// <summary>
    /// Acquire a route override. Multiple overrides can stack; highest priority wins.
    /// Priority order: ForceHoldPosition > ForceRoute > DisableAutonomy > DisableSchedule
    /// </summary>
    public RouteOverrideHandle AcquireRouteOverride(RouteOverrideMode mode, string reason = "", NPCRouteAsset forceRoute = null)
    {
        int tokenId = nextOverrideTokenId++;
        
        var entry = new RouteOverrideEntry
        {
            tokenId = tokenId,
            mode = mode,
            forceRoute = (mode == RouteOverrideMode.ForceRoute) ? forceRoute : null,
            reason = reason
        };
        
        activeOverrides.Add(entry);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} route override acquired: {mode} (token={tokenId}, reason='{reason}')");
        }
        
        // Apply effective route based on new override state
        ApplyEffectiveRoute();
        OnRouteOverrideChanged?.Invoke(GetEffectiveOverrideMode());
        
        return new RouteOverrideHandle(tokenId, mode, reason, ReleaseRouteOverride);
    }
    
    private void ReleaseRouteOverride(int tokenId)
    {
        int idx = activeOverrides.FindIndex(e => e.tokenId == tokenId);
        if (idx < 0)
        {
            return;
        }
        
        var entry = activeOverrides[idx];
        activeOverrides.RemoveAt(idx);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} route override released: {entry.mode} (token={tokenId}, remaining={activeOverrides.Count})");
        }
        
        // Re-apply effective route
        ApplyEffectiveRoute();
        OnRouteOverrideChanged?.Invoke(GetEffectiveOverrideMode());
    }
    
    /// <summary>
    /// Get the currently effective override mode (highest priority active override).
    /// </summary>
    private RouteOverrideMode GetEffectiveOverrideMode()
    {
        if (activeOverrides.Count == 0)
            return RouteOverrideMode.None;
        
        // Find highest priority override
        RouteOverrideMode highest = RouteOverrideMode.None;
        
        foreach (var entry in activeOverrides)
        {
            if ((int)entry.mode > (int)highest)
            {
                highest = entry.mode;
            }
        }
        
        return highest;
    }
    
    /// <summary>
    /// Get the force route if ForceRoute mode is active.
    /// </summary>
    private NPCRouteAsset GetForceRouteAsset()
    {
        foreach (var entry in activeOverrides)
        {
            if (entry.mode == RouteOverrideMode.ForceRoute && entry.forceRoute != null)
            {
                return entry.forceRoute;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Apply the effective route based on current overrides, schedule, and autonomy state.
    /// </summary>
    private void ApplyEffectiveRoute()
    {
        if (movementAgent == null) return;
        
        // Check interrupt policy first
        if (interruptPolicy == NPCInterruptPolicy.Frozen)
        {
            movementAgent.StopMovement();
            return;
        }
        
        // Determine effective override
        var overrideMode = GetEffectiveOverrideMode();
        
        switch (overrideMode)
        {
            case RouteOverrideMode.ForceHoldPosition:
                // Stop all movement, remain idle
                movementAgent.StopMovement();
                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCController] {npcId} holding position (override)");
                }
                return;
                
            case RouteOverrideMode.DisableAutonomy:
                // Stop route runner, no auto movement
                movementAgent.StopRoute();
                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCController] {npcId} autonomy disabled (override)");
                }
                return;
                
            case RouteOverrideMode.ForceRoute:
                // Use forced route instead of schedule
                var forceRoute = GetForceRouteAsset();
                if (forceRoute != null)
                {
                    movementAgent.SetRoute(forceRoute, null);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[NPCController] {npcId} using forced route: {forceRoute.name}");
                    }
                }
                return;
                
            case RouteOverrideMode.DisableSchedule:
                // Don't apply schedule routes, but allow current route to continue
                // (or stop if no route active)
                if (!movementAgent.IsRouteRunnerActive)
                {
                    movementAgent.StopRoute();
                }
                return;
                
            case RouteOverrideMode.None:
            default:
                // Normal operation - apply schedule
                break;
        }
        
        // No override - apply from schedule if autonomy enabled
        if (!autonomyEnabled)
        {
            movementAgent.StopRoute();
            return;
        }
        
        ApplyRouteFromSchedule();
    }
    
    /// <summary>
    /// Apply route from schedule based on current time segment.
    /// </summary>
    private void ApplyRouteFromSchedule()
    {
        if (routeSchedule == null || timeService == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCController] {npcId} no route schedule or time service");
            }
            return;
        }
        
        var routeConfig = routeSchedule.GetRouteForSegment(timeService.CurrentSegment);
        if (routeConfig.routeAsset != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCController] {npcId} applying route for {timeService.CurrentSegment}: {routeConfig.routeAsset.name}");
            }
            
            movementAgent.SetRoute(routeConfig.routeAsset, routeConfig);
            
            // Update blink rate on visual
            if (visual != null)
            {
                visual.SetBlinkRateMultiplier(routeConfig.blinkRateMultiplier);
            }
        }
        else
        {
            // No route for this segment - stop
            movementAgent.StopRoute();
        }
    }
    
    // ============================================
    // CLAIM/RELEASE SYSTEM (Narrative Control)
    // ============================================
    
    /// <summary>
    /// Claim control of this NPC (from narrative system).
    /// Returns a handle that must be released when done.
    /// </summary>
    public ClaimHandle ClaimControl(ClaimType type, string reason = "")
    {
        int tokenId = nextClaimTokenId++;
        activeClaims[tokenId] = new ClaimToken
        {
            id = tokenId,
            type = type,
            reason = reason
        };
        
        // Apply interrupt policy based on claim type
        NPCInterruptPolicy newPolicy = type == ClaimType.Hard ? NPCInterruptPolicy.Frozen : NPCInterruptPolicy.Suppressed;
        SetInterruptPolicy(newPolicy);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} claimed ({type}): '{reason}' (token={tokenId}, policy={newPolicy})");
        }
        
        return new ClaimHandle(tokenId, reason, ReleaseClaim);
    }
    
    private void ReleaseClaim(int tokenId)
    {
        if (!activeClaims.TryGetValue(tokenId, out var token))
        {
            return;
        }
        
        activeClaims.Remove(tokenId);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} claim released (token={tokenId}, remaining={activeClaims.Count})");
        }
        
        // If no more claims, restore Normal policy
        if (activeClaims.Count == 0)
        {
            SetInterruptPolicy(NPCInterruptPolicy.Normal);
        }
    }
    
    // ============================================
    // INTERRUPT POLICY
    // ============================================
    
    /// <summary>
    /// Set interrupt policy (called internally or by external systems).
    /// </summary>
    public void SetInterruptPolicy(NPCInterruptPolicy policy)
    {
        if (interruptPolicy == policy)
            return;
        
        NPCInterruptPolicy oldPolicy = interruptPolicy;
        interruptPolicy = policy;
        
        ApplyInterruptPolicy(policy);
        
        OnInterruptPolicyChanged?.Invoke(policy);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} interrupt policy changed: {oldPolicy} -> {policy}");
        }
    }
    
    private void ApplyInterruptPolicy(NPCInterruptPolicy policy)
    {
        switch (policy)
        {
            case NPCInterruptPolicy.Normal:
                // Re-enable movement and re-apply effective route
                if (movementAgent != null)
                {
                    movementAgent.SetMovementEnabled(true);
                }
                ApplyEffectiveRoute();
                break;
                
            case NPCInterruptPolicy.Suppressed:
                // Movement still allowed, but micro-wander may be reduced
                if (movementAgent != null)
                {
                    movementAgent.SetMovementEnabled(true);
                }
                break;
                
            case NPCInterruptPolicy.Frozen:
                // Stop all movement
                if (movementAgent != null)
                {
                    movementAgent.SetMovementEnabled(false);
                    movementAgent.StopMovement();
                }
                break;
        }
    }
    
    // ============================================
    // AUTONOMY CONTROL
    // ============================================
    
    /// <summary>
    /// Enable or disable autonomy (route following from schedule).
    /// </summary>
    public void SetAutonomyEnabled(bool enabled)
    {
        if (autonomyEnabled == enabled)
            return;
        
        autonomyEnabled = enabled;
        
        ApplyEffectiveRoute();
        
        OnAutonomyEnabledChanged?.Invoke(enabled);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCController] {npcId} autonomy {(enabled ? "enabled" : "disabled")}");
        }
    }
    
    /// <summary>
    /// Set route schedule asset.
    /// </summary>
    public void SetRouteSchedule(NPCRouteScheduleSO schedule)
    {
        routeSchedule = schedule;
        ApplyEffectiveRoute();
    }
    
    // ============================================
    // MOVEMENT DELEGATION
    // ============================================
    
    /// <summary>
    /// Move NPC to waypoint ID (async).
    /// </summary>
    public IEnumerator MoveToWaypoint(string waypointId, float timeout = 30f)
    {
        if (movementAgent == null)
        {
            Debug.LogError($"[NPCController] {npcId} cannot move: NPCMovementAgent is null!");
            yield break;
        }
        
        blackboard.CurrentTargetWaypointId = waypointId;
        blackboard.CurrentState = NPCInternalState.MovingToWaypoint;
        
        yield return movementAgent.MoveToWaypoint(waypointId, timeout);
        
        blackboard.CurrentState = NPCInternalState.Idle;
    }
    
    /// <summary>
    /// Move NPC to world position (async).
    /// </summary>
    public IEnumerator MoveToPosition(Vector3 position, float timeout = 30f)
    {
        if (movementAgent == null)
        {
            Debug.LogError($"[NPCController] {npcId} cannot move: NPCMovementAgent is null!");
            yield break;
        }
        
        blackboard.CurrentTargetPosition = position;
        blackboard.CurrentState = NPCInternalState.MovingToWaypoint;
        
        yield return movementAgent.MoveToPosition(position, timeout);
        
        blackboard.CurrentState = NPCInternalState.Idle;
    }
    
    /// <summary>
    /// Face NPC toward target.
    /// </summary>
    public void FaceTarget(string targetId, FaceTargetType targetType)
    {
        if (movementAgent == null) return;
        movementAgent.FaceTarget(targetId, targetType);
    }
    
    /// <summary>
    /// Snap facing to a world / camera / player-relative preset (no locomotion).
    /// </summary>
    public void FaceDirection(NPCFaceDirectionPreset preset)
    {
        if (movementAgent == null) return;
        movementAgent.FaceDirection(preset);
    }

    /// <summary>
    /// Snap NPC rotation toward a runtime transform target.
    /// </summary>
    public void SnapFaceTarget(Transform target)
    {
        if (movementAgent == null) return;
        movementAgent.SnapFaceTarget(target);
    }

    /// <summary>
    /// Override idle / interaction facing toward a runtime transform target until cleared.
    /// </summary>
    public void SetFacingTargetOverride(Transform target, bool snapImmediately = false)
    {
        if (movementAgent == null) return;
        movementAgent.SetFacingTargetOverride(target, snapImmediately);
    }

    /// <summary>
    /// Clear any active runtime facing-target override.
    /// </summary>
    public void ClearFacingTargetOverride()
    {
        if (movementAgent == null) return;
        movementAgent.ClearFacingTargetOverride();
    }

    /// <summary>
    /// Remember the current idle facing so an interaction can restore it later.
    /// </summary>
    public void CaptureIdleFacing()
    {
        if (movementAgent == null) return;
        movementAgent.CaptureIdleFacing();
    }

    /// <summary>
    /// Restore the idle facing captured before this interaction turned the NPC.
    /// </summary>
    public void RestoreCapturedIdleFacing()
    {
        if (movementAgent == null) return;
        movementAgent.RestoreCapturedIdleFacing();
    }
    
    /// <summary>
    /// Ends fire-and-forget follow (if running) or stops NavMesh movement (including background MoveTo).
    /// </summary>
    public void StopNarrativeLocomotion()
    {
        NPCFollowBehavior follow = GetComponent<NPCFollowBehavior>();
        if (follow != null && follow.IsFollowing)
            follow.StopFollowing();
        else
            StopMovement();
    }
    
    /// <summary>
    /// Stop current movement.
    /// </summary>
    public void StopMovement()
    {
        if (movementAgent != null)
        {
            movementAgent.StopMovement();
        }
        blackboard.CurrentState = NPCInternalState.Idle;
    }
    
    /// <summary>
    /// Release all active claims (for sequence cleanup).
    /// </summary>
    public void ReleaseAllClaims()
    {
        var claimIds = new List<int>(activeClaims.Keys);
        
        foreach (int id in claimIds)
        {
            ReleaseClaim(id);
        }
        
        if (enableDebugLogs && claimIds.Count > 0)
        {
            Debug.Log($"[NPCController] {npcId} released all {claimIds.Count} active claims");
        }
    }
    
    /// <summary>
    /// Release all active route overrides.
    /// </summary>
    public void ReleaseAllRouteOverrides()
    {
        int count = activeOverrides.Count;
        activeOverrides.Clear();
        
        ApplyEffectiveRoute();
        OnRouteOverrideChanged?.Invoke(RouteOverrideMode.None);
        
        if (enableDebugLogs && count > 0)
        {
            Debug.Log($"[NPCController] {npcId} released all {count} route overrides");
        }
    }
}

// ============================================
// ENUMS AND SUPPORTING TYPES
// ============================================

/// <summary>
/// Interrupt policy affecting NPC autonomy.
/// </summary>
public enum NPCInterruptPolicy
{
    /// <summary>Normal: autonomy enabled; full route + micro-wander</summary>
    Normal,
    
    /// <summary>Suppressed: route continues but micro-wander may be reduced</summary>
    Suppressed,
    
    /// <summary>Frozen: all movement stopped</summary>
    Frozen
}

/// <summary>
/// Claim type for narrative control.
/// </summary>
public enum ClaimType
{
    /// <summary>Hard claim: Frozen immediately</summary>
    Hard,
    
    /// <summary>Soft claim: Suppressed (route continues)</summary>
    Soft
}

/// <summary>
/// Route override modes (priority order: higher value = higher priority).
/// </summary>
public enum RouteOverrideMode
{
    /// <summary>No override - use schedule</summary>
    None = 0,
    
    /// <summary>Schedule stops applying new routes, current route continues</summary>
    DisableSchedule = 1,
    
    /// <summary>Route runner stopped, no auto movement (narrative can still move NPC)</summary>
    DisableAutonomy = 2,
    
    /// <summary>NPC follows specified route instead of schedule</summary>
    ForceRoute = 3,
    
    /// <summary>Stop all movement, remain idle</summary>
    ForceHoldPosition = 4
}

/// <summary>
/// Handle for claiming NPC control. Release when done.
/// </summary>
public class ClaimHandle
{
    private readonly int tokenId;
    private readonly string reason;
    private readonly Action<int> releaseCallback;
    private bool released = false;
    
    public ClaimHandle(int tokenId, string reason, Action<int> releaseCallback)
    {
        this.tokenId = tokenId;
        this.reason = reason;
        this.releaseCallback = releaseCallback;
    }
    
    public void Release()
    {
        if (released) return;
        released = true;
        releaseCallback?.Invoke(tokenId);
    }
}

/// <summary>
/// Handle for route overrides. Release when done.
/// </summary>
public class RouteOverrideHandle
{
    private readonly int tokenId;
    private readonly RouteOverrideMode mode;
    private readonly string reason;
    private readonly Action<int> releaseCallback;
    private bool released = false;
    
    public RouteOverrideMode Mode => mode;
    public string Reason => reason;
    public bool IsReleased => released;
    
    public RouteOverrideHandle(int tokenId, RouteOverrideMode mode, string reason, Action<int> releaseCallback)
    {
        this.tokenId = tokenId;
        this.mode = mode;
        this.reason = reason;
        this.releaseCallback = releaseCallback;
    }
    
    public void Release()
    {
        if (released) return;
        released = true;
        releaseCallback?.Invoke(tokenId);
    }
}

/// <summary>
/// Type of target to face.
/// </summary>
public enum FaceTargetType
{
    Player,
    WaypointId,
    Direction
}

/// <summary>
/// Standing facing presets for narrative (used with <see cref="FaceTargetType.Direction"/> via <c>Step_NPC_FaceTarget</c>).
/// </summary>
public enum NPCFaceDirectionPreset
{
    WorldNorth,
    WorldSouth,
    WorldEast,
    WorldWest,
    TowardCamera,
    AwayFromCamera,
    TowardPlayer,
    AwayFromPlayer
}
