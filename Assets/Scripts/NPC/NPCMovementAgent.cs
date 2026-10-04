using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent-based movement agent for NPCs with node graph route runner.
/// Handles movement, dwell at nodes, micro-wander during dwell, and weighted next-node selection.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovementAgent : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Base movement speed (world units per second)")]
    [SerializeField] private float moveSpeed = 2f;
    
    [Tooltip("Rotation speed for facing direction (degrees per second)")]
    [SerializeField] private float rotationSpeed = 360f;
    
    [Header("NavMesh Settings")]
    [Tooltip("Stop movement when this close to target")]
    [SerializeField] private float stopDistance = 0.3f;
    
    [Tooltip("Radius to sample NavMesh when setting destinations")]
    [SerializeField] private float destinationSampleRadius = 1.5f;
    
    [Tooltip("Attempts to find valid NavMesh position for micro-wander")]
    [SerializeField] private int microSampleAttempts = 5;

    [Header("Character Blocking")]
    [Tooltip("If true, the NPC pauses when the player is occupying its immediate movement space.")]
    [SerializeField] private bool respectPlayerBlocker = true;

    [Tooltip("Extra spacing added between the NPC and player blocker circles before pausing.")]
    [SerializeField] private float playerBlockSeparationBuffer = 0.05f;

    [Tooltip("Additional distance required before an NPC resumes after pausing for the player.")]
    [SerializeField] private float playerBlockResumeBuffer = 0.15f;
    
    // Refcount: while > 0, player-block pausing is suppressed (narrative MoveTo/Follow only).
    private int narrativePlayerBlockSuppressDepth;
    
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private MeshRendererNPCVisual visual;
    [SerializeField] private NPCShadowCaster shadowCaster;
    [SerializeField] private TopDownCameraController3D cameraController;
    
    [Header("Visual / Facing")]
    [Tooltip("What NavMeshAgent vector drives directional sprites and root rotation. Actual velocity matches real motion (better under camera yaw + follow/repath). Desired matches steering. Fallback uses actual when moving, otherwise desired (reduces idle flicker when acceleration lags steering).")]
    [SerializeField] private NPCVisualVelocitySource visualVelocitySource = NPCVisualVelocitySource.ActualVelocityWithDesiredFallback;
    [Tooltip("Minimum confident movement speed before we update the cached idle-facing direction.")]
    [SerializeField] private float stableWalkThreshold = 0.3f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawDebugPath = false;
    
    // State
    private bool movementEnabled = true;
    private bool routeRunnerActive = false;
    private Coroutine routeCoroutine = null;
    
    // Current route state
    private NPCRouteAsset currentRoute;
    private NPCRouteScheduleSO.RouteConfig currentScheduleConfig;
    private RouteNode currentNode;
    private int currentNodeIndex = 0;
    private string previousNodeId = "";
    
    // Animation state tracking
    private NPCAnimationState currentAnimState = NPCAnimationState.Idle;
    private Vector2 lastStableWalkDirection;
    private bool hasStableWalkDirection;
    private Vector2 capturedIdleFacing;
    private bool hasCapturedIdleFacing;
    private Transform followFacingTarget;
    private Transform facingTargetOverride;
    private int presentationLockDepth;
    private Collider solidCollider;
    private bool pausedForPlayerBlock;
    
    /// <summary>Set true by <see cref="StopMovement"/> / cancel so <see cref="MoveToPosition"/> exits early.</summary>
    private bool navigationMoveToCancelled;
    
    // Arrival detection thresholds
    private const float VELOCITY_IDLE_THRESHOLD = 0.01f;
    private const float ARRIVAL_EPSILON = 0.05f;
    
    private enum NPCVisualVelocitySource
    {
        DesiredVelocity,
        ActualVelocity,
        ActualVelocityWithDesiredFallback
    }
    
    // Events for external monitoring
    public event Action<RouteNode> OnNodeReached;
    public event Action<RouteNode> OnNodeDwellStarted;
    public event Action<RouteNode> OnNodeDwellEnded;
    public event Action OnRouteCompleted;
    public event Action OnRouteStopped;
    
    // Properties
    public bool IsRouteRunnerActive => routeRunnerActive;
    public NPCRouteAsset CurrentRoute => currentRoute;
    public RouteNode CurrentNode => currentNode;
    public int CurrentNodeIndex => currentNodeIndex;
    public float DestinationSampleRadius => destinationSampleRadius;
    public int MicroSampleAttempts => microSampleAttempts;
    public bool IsPresentationLocked => presentationLockDepth > 0;
    
    /// <summary>
    /// Effective gate for <see cref="UpdatePlayerBlocking"/>: prefab <see cref="respectPlayerBlocker"/>
    /// unless narrative code has called <see cref="BeginNarrativePlayerBlockSuppress"/>.
    /// </summary>
    private bool EffectiveRespectPlayerBlocker =>
        respectPlayerBlocker && narrativePlayerBlockSuppressDepth <= 0;
    
    /// <summary>
    /// Narrative sequence steps call this when driving the NPC through scripted movement
    /// and the NPC should not pause for the player (e.g. push-through during dialogue).
    /// Pair with <see cref="EndNarrativePlayerBlockSuppress"/> in <c>finally</c>.
    /// </summary>
    public void BeginNarrativePlayerBlockSuppress()
    {
        narrativePlayerBlockSuppressDepth++;
        if (narrativePlayerBlockSuppressDepth == 1)
            ReleasePlayerBlockPause();
    }
    
    /// <summary>
    /// Decrements the suppress refcount from <see cref="BeginNarrativePlayerBlockSuppress"/>.
    /// </summary>
    public void EndNarrativePlayerBlockSuppress()
    {
        if (narrativePlayerBlockSuppressDepth <= 0)
        {
            Debug.LogWarning(
                $"[NPCMovementAgent] {gameObject.name}: EndNarrativePlayerBlockSuppress without matching Begin (depth={narrativePlayerBlockSuppressDepth}).",
                this);
            return;
        }
        
        narrativePlayerBlockSuppressDepth--;
    }
    
    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        solidCollider = FindPrimarySolidCollider();
        
        if (visual == null)
            visual = GetComponentInChildren<MeshRendererNPCVisual>();

        if (shadowCaster == null)
            shadowCaster = GetComponentInChildren<NPCShadowCaster>();

        if (cameraController == null)
        {
            TopDownCameraController3D resolvedController = SceneCameraResolver.ResolveGameplayCameraController();
            if (resolvedController != null)
                cameraController = resolvedController;
        }
        else if (SceneCameraResolver.HasLivePersistentCamera &&
                 SceneCameraResolver.GetCamera(cameraController) != SceneCameraResolver.PersistentGameplayCamera)
        {
            cameraController = SceneCameraResolver.ResolveGameplayCameraController();
        }
        
        ConfigureAgent();
    }
    
    private void ConfigureAgent()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stopDistance;
            agent.updateRotation = false; // We handle rotation manually for 2.5D visuals
            agent.updatePosition = true;
            agent.angularSpeed = rotationSpeed;
        }
    }
    
    private void Start()
    {
        EnsureOnNavMesh();
        SeedStableWalkDirectionFromTransform();
        RegisterSolidCollider();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            RegisterSolidCollider();
    }

    private void OnDisable()
    {
        UnregisterSolidCollider();
    }

    private void OnDestroy()
    {
        UnregisterSolidCollider();
    }
    
    /// <summary>
    /// Ensures the NPC is on a valid NavMesh position at startup.
    /// </summary>
    private void EnsureOnNavMesh()
    {
        if (agent == null) return;
        
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            if (Vector3.Distance(transform.position, hit.position) > 0.1f)
            {
                agent.Warp(hit.position);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCMovementAgent] {gameObject.name} warped to NavMesh at {hit.position}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[NPCMovementAgent] {gameObject.name} could not find NavMesh within 2m of spawn position!");
        }
    }

    private Collider FindPrimarySolidCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate != null && !candidate.isTrigger)
                return candidate;
        }

        return null;
    }

    private void RegisterSolidCollider()
    {
        if (solidCollider == null)
            solidCollider = FindPrimarySolidCollider();

        if (solidCollider != null && !solidCollider.isTrigger)
            CharacterCollisionRegistry.RegisterNpcCollider(solidCollider);
    }

    private void UnregisterSolidCollider()
    {
        if (solidCollider != null)
            CharacterCollisionRegistry.UnregisterNpcCollider(solidCollider);
    }

    private void UpdatePlayerBlocking()
    {
        if (!EffectiveRespectPlayerBlocker || agent == null)
        {
            ReleasePlayerBlockPause();
            return;
        }

        if (!movementEnabled || !agent.enabled || !agent.isOnNavMesh)
        {
            ReleasePlayerBlockPause();
            return;
        }

        float idleThresholdSqr = VELOCITY_IDLE_THRESHOLD * VELOCITY_IDLE_THRESHOLD;
        bool hasNavigationIntent = agent.hasPath || agent.desiredVelocity.sqrMagnitude > idleThresholdSqr;
        if (!hasNavigationIntent)
        {
            ReleasePlayerBlockPause();
            return;
        }

        if (!TryGetPlanarBlockCircle(solidCollider, out Vector2 npcCenter, out float npcRadius) ||
            !CharacterCollisionRegistry.TryGetPreferredPlayerCollider(out Collider playerCollider) ||
            !TryGetPlanarBlockCircle(playerCollider, out Vector2 playerCenter, out float playerRadius))
        {
            ReleasePlayerBlockPause();
            return;
        }

        Vector2 toPlayer = playerCenter - npcCenter;
        float requiredDistance = npcRadius + playerRadius + Mathf.Max(0f, playerBlockSeparationBuffer);
        float resumeDistance = requiredDistance + Mathf.Max(0f, playerBlockResumeBuffer);
        float sqrDistanceToPlayer = toPlayer.sqrMagnitude;

        Vector2 travelDirection = new Vector2(agent.desiredVelocity.x, agent.desiredVelocity.z);
        bool movingTowardPlayer = travelDirection.sqrMagnitude > idleThresholdSqr &&
                                  Vector2.Dot(travelDirection.normalized, toPlayer.normalized) > 0.1f;

        bool shouldPause = sqrDistanceToPlayer <= (requiredDistance * requiredDistance) &&
                           (movingTowardPlayer || pausedForPlayerBlock);

        if (shouldPause)
        {
            if (!pausedForPlayerBlock)
            {
                pausedForPlayerBlock = true;
                agent.isStopped = true;

                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCMovementAgent] {gameObject.name} paused for player blocker.");
                }
            }

            return;
        }

        if (pausedForPlayerBlock && sqrDistanceToPlayer >= (resumeDistance * resumeDistance))
        {
            pausedForPlayerBlock = false;

            if (movementEnabled && (routeRunnerActive || agent.hasPath))
                agent.isStopped = false;

            if (enableDebugLogs)
            {
                Debug.Log($"[NPCMovementAgent] {gameObject.name} resumed after player blocker cleared.");
            }
        }
    }

    private void ReleasePlayerBlockPause()
    {
        if (!pausedForPlayerBlock)
            return;

        pausedForPlayerBlock = false;

        if (agent != null && movementEnabled && (routeRunnerActive || agent.hasPath))
            agent.isStopped = false;
    }

    private static bool TryGetPlanarBlockCircle(Collider collider, out Vector2 center, out float radius)
    {
        center = Vector2.zero;
        radius = 0f;

        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;

        Bounds bounds = collider.bounds;
        if (bounds.size.sqrMagnitude <= 0.000001f)
            return false;

        center = new Vector2(bounds.center.x, bounds.center.z);
        radius = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        return true;
    }
    
    private void Update()
    {
        ValidateRegisteredSolidCollider();
        RefreshPresentationReferences();
        SyncPresentationWithCameraYaw();
        UpdatePlayerBlocking();

        if (IsPresentationLocked)
            return;

        if (!movementEnabled)
        {
            ApplyBestIdleFacing();
            SetAnimationState(NPCAnimationState.Idle);
            return;
        }
        
        UpdateVisualFromVelocity();
    }
    
    /// <summary>
    /// Update visual direction and animation based on agent velocity.
    /// Direction passed to <see cref="UpdateVisualDirection"/> comes from <see cref="visualVelocitySource"/>.
    /// </summary>
    private void UpdateVisualFromVelocity()
    {
        if (agent == null) return;
        
        Vector2 planarDesired = new Vector2(agent.desiredVelocity.x, agent.desiredVelocity.z);
        Vector2 planarActual = new Vector2(agent.velocity.x, agent.velocity.z);
        float magDesired = planarDesired.magnitude;
        float magActual = planarActual.magnitude;
        
        bool TryGetMoveDirection(out Vector2 moveDir, out float speedForWalkIdle)
        {
            moveDir = Vector2.zero;
            speedForWalkIdle = 0f;
            
            switch (visualVelocitySource)
            {
                case NPCVisualVelocitySource.DesiredVelocity:
                    if (magDesired <= VELOCITY_IDLE_THRESHOLD)
                        return false;
                    moveDir = planarDesired / magDesired;
                    speedForWalkIdle = magDesired;
                    return true;
                    
                case NPCVisualVelocitySource.ActualVelocity:
                    if (magActual <= VELOCITY_IDLE_THRESHOLD)
                        return false;
                    moveDir = planarActual / magActual;
                    speedForWalkIdle = magActual;
                    return true;
                    
                 case NPCVisualVelocitySource.ActualVelocityWithDesiredFallback:
                     if (magActual > VELOCITY_IDLE_THRESHOLD)
                     {
                         moveDir = planarActual / magActual;
                         speedForWalkIdle = magActual;
                        return true;
                    }
                     if (magDesired > VELOCITY_IDLE_THRESHOLD)
                     {
                         moveDir = planarDesired / magDesired;
                         // When steering has started but actual velocity has not caught up yet,
                         // keep the walk loop alive so the NPC does not glide in an idle frame.
                         speedForWalkIdle = magDesired;
                         return true;
                     }
                     return false;
                    
                default:
                    return false;
            }
        }
        
        if (!TryGetMoveDirection(out Vector2 moveDir2, out float speedForWalk))
        {
            ApplyBestIdleFacing();
            SetAnimationState(NPCAnimationState.Idle);
            return;
        }

        bool hasFacingOverride = TryGetFacingTargetOverrideDirection(out Vector2 overrideFacingDirection);
        bool hasFollowFacing = TryGetFollowFacingDirection(out Vector2 followFacingDirection);
        Vector2 worldFacingDirection = hasFacingOverride
            ? overrideFacingDirection
            : hasFollowFacing ? followFacingDirection : moveDir2;

        if (hasFacingOverride)
        {
            CacheStableWalkDirection(overrideFacingDirection, force: true);
        }
        else if (hasFollowFacing)
        {
            CacheStableWalkDirection(followFacingDirection, force: true);
        }
        else if (speedForWalk > stableWalkThreshold)
        {
            CacheStableWalkDirection(moveDir2);
        }

        ApplyFacing(worldFacingDirection);

        bool walking = speedForWalk > VELOCITY_IDLE_THRESHOLD;
        SetAnimationState(walking ? NPCAnimationState.Walk : NPCAnimationState.Idle);
    }

    private void RefreshPresentationReferences()
    {
        Camera boundCamera = SceneCameraResolver.GetCamera(cameraController);
        if (cameraController == null || !SceneCameraResolver.IsUsableFor(boundCamera, gameObject))
            cameraController = SceneCameraResolver.ResolveGameplayCameraController();

        if (visual == null)
            visual = GetComponentInChildren<MeshRendererNPCVisual>();

        if (shadowCaster == null)
            shadowCaster = GetComponentInChildren<NPCShadowCaster>();
    }

    private void SyncPresentationWithCameraYaw()
    {
        int yawIndex = cameraController != null ? cameraController.YawIndex90 : 0;

        if (visual != null)
            visual.UpdateVisualForCameraYaw(yawIndex);

        if (shadowCaster != null)
            shadowCaster.UpdateShadowForCameraYaw(yawIndex);
    }

    private void ApplyBestIdleFacing()
    {
        if (TryGetFacingTargetOverrideDirection(out Vector2 overrideFacingDirection))
        {
            CacheStableWalkDirection(overrideFacingDirection, force: true);
            ApplyFacing(overrideFacingDirection);
            return;
        }

        if (TryGetFollowFacingDirection(out Vector2 followFacingDirection))
        {
            CacheStableWalkDirection(followFacingDirection, force: true);
            ApplyFacing(followFacingDirection);
            return;
        }

        if (TryGetStableWalkDirection(out Vector2 stableFacingDirection))
        {
            ApplyFacing(stableFacingDirection);
            return;
        }

        if (TryGetTransformFacingDirection(out Vector2 transformFacingDirection))
        {
            ApplyFacing(transformFacingDirection);
        }
    }

    private void UpdateVisualDirection(Vector2 worldDirection)
    {
        if (visual == null || worldDirection.sqrMagnitude <= 0.0001f)
            return;

        visual.SetDirection(ConvertWorldDirectionToScreen(worldDirection));
    }

    private Vector2 ConvertWorldDirectionToScreen(Vector2 worldDirection)
    {
        Vector2 normalized = worldDirection.normalized;
        if (cameraController == null)
            return normalized;

        Vector3 cameraForward = cameraController.GetCameraForwardXZ();
        Vector3 cameraRight = cameraController.GetCameraRightXZ();

        Vector2 rightBasis = new Vector2(cameraRight.x, cameraRight.z);
        Vector2 forwardBasis = new Vector2(cameraForward.x, cameraForward.z);

        float screenX = Vector2.Dot(normalized, rightBasis);
        float screenY = Vector2.Dot(normalized, forwardBasis);
        return new Vector2(screenX, screenY);
    }

    private void ApplyFacing(Vector2 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector2 normalized = worldDirection.normalized;
        UpdateVisualDirection(normalized);

        Vector3 direction = new Vector3(normalized.x, 0f, normalized.y);
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private bool TryGetFollowFacingDirection(out Vector2 worldFacingDirection)
    {
        worldFacingDirection = Vector2.zero;

        if (followFacingTarget == null)
            return false;

        Vector3 toTarget = followFacingTarget.position - transform.position;
        Vector2 targetDirection = new Vector2(toTarget.x, toTarget.z);
        if (targetDirection.sqrMagnitude <= 0.0001f)
            return false;

        worldFacingDirection = targetDirection.normalized;
        return true;
    }

    private void ValidateRegisteredSolidCollider()
    {
        if (solidCollider != null && !solidCollider.isTrigger)
            return;

        UnregisterSolidCollider();
        solidCollider = FindPrimarySolidCollider();
        RegisterSolidCollider();
    }

    private bool TryGetFacingTargetOverrideDirection(out Vector2 worldFacingDirection)
    {
        worldFacingDirection = Vector2.zero;

        if (facingTargetOverride == null)
            return false;

        Vector3 toTarget = facingTargetOverride.position - transform.position;
        Vector2 targetDirection = new Vector2(toTarget.x, toTarget.z);
        if (targetDirection.sqrMagnitude <= 0.0001f)
            return false;

        worldFacingDirection = targetDirection.normalized;
        return true;
    }

    private bool TryGetStableWalkDirection(out Vector2 worldFacingDirection)
    {
        if (hasStableWalkDirection && lastStableWalkDirection.sqrMagnitude > 0.0001f)
        {
            worldFacingDirection = lastStableWalkDirection.normalized;
            return true;
        }

        worldFacingDirection = Vector2.zero;
        return false;
    }

    private bool TryGetTransformFacingDirection(out Vector2 worldFacingDirection)
    {
        Vector3 facing = transform.forward;
        worldFacingDirection = new Vector2(facing.x, facing.z);
        if (worldFacingDirection.sqrMagnitude <= 0.0001f)
        {
            worldFacingDirection = Vector2.zero;
            return false;
        }

        worldFacingDirection.Normalize();
        return true;
    }

    private void CacheStableWalkDirection(Vector2 worldDirection, bool force = false)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        if (!force && worldDirection.magnitude < stableWalkThreshold)
            return;

        lastStableWalkDirection = worldDirection.normalized;
        hasStableWalkDirection = true;
    }

    private void SeedStableWalkDirectionFromTransform()
    {
        if (hasStableWalkDirection)
            return;

        if (TryGetTransformFacingDirection(out Vector2 transformFacingDirection))
        {
            CacheStableWalkDirection(transformFacingDirection, force: true);
        }
    }
    
    // ============================================
    // ROUTE RUNNER
    // ============================================
    
    /// <summary>
    /// Set and start following a route.
    /// </summary>
    public void SetRoute(NPCRouteAsset route, NPCRouteScheduleSO.RouteConfig scheduleConfig = null)
    {
        // Stop any existing route
        StopRoute();
        
        currentRoute = route;
        currentScheduleConfig = scheduleConfig;
        currentNodeIndex = 0;
        previousNodeId = "";
        
        if (route == null || route.NodeCount == 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCMovementAgent] {gameObject.name} route cleared");
            }
            return;
        }
        
        // Apply speed multiplier
        if (agent != null)
        {
            agent.speed = moveSpeed * route.SpeedMultiplier;
        }
        
        // Start route runner
        routeCoroutine = StartCoroutine(RouteLoop());
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMovementAgent] {gameObject.name} starting route: {route.name} ({route.NodeCount} nodes)");
        }
    }
    
    /// <summary>
    /// Stop the current route.
    /// </summary>
    public void StopRoute()
    {
        if (routeCoroutine != null)
        {
            StopCoroutine(routeCoroutine);
            routeCoroutine = null;
        }
        
        routeRunnerActive = false;
        currentRoute = null;
        currentScheduleConfig = null;
        currentNode = null;
        currentNodeIndex = 0;
        previousNodeId = "";
        
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
            agent.speed = moveSpeed; // Reset to base speed
        }
        
        SetAnimationState(NPCAnimationState.Idle);
        OnRouteStopped?.Invoke();
    }
    
    /// <summary>
    /// Main route loop coroutine.
    /// </summary>
    private IEnumerator RouteLoop()
    {
        routeRunnerActive = true;
        
        // Find first valid node
        currentNode = FindFirstValidNode();
        if (currentNode == null)
        {
            Debug.LogError($"[NPCMovementAgent] {gameObject.name} could not find any valid node in route");
            StopRoute();
            yield break;
        }
        
        while (routeRunnerActive && movementEnabled && currentRoute != null)
        {
            // 1. Move to current node's waypoint
            Vector3 waypointPos = GetWaypointPosition(currentNode.waypointId);
            if (waypointPos == Vector3.zero)
            {
                Debug.LogWarning($"[NPCMovementAgent] {gameObject.name} waypoint not found: {currentNode.waypointId}");
                yield return AdvanceToNextNode();
                continue;
            }
            
            // Navigate to waypoint
            yield return NavigateToPosition(waypointPos);
            
            if (!routeRunnerActive || !movementEnabled) break;
            
            // 2. Node reached - fire event
            OnNodeReached?.Invoke(currentNode);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCMovementAgent] {gameObject.name} reached node {currentNodeIndex}: {currentNode.nodeId}");
            }
            
            // 3. Dwell at node (with micro-wander)
            OnNodeDwellStarted?.Invoke(currentNode);
            yield return DwellAtNode(currentNode, waypointPos);
            OnNodeDwellEnded?.Invoke(currentNode);
            
            if (!routeRunnerActive || !movementEnabled) break;
            
            // 4. Choose and advance to next node
            yield return AdvanceToNextNode();
        }
        
        routeRunnerActive = false;
    }
    
    /// <summary>
    /// Find the first valid node (one with a reachable waypoint).
    /// </summary>
    private RouteNode FindFirstValidNode()
    {
        if (currentRoute == null) return null;
        
        for (int i = 0; i < currentRoute.NodeCount; i++)
        {
            var node = currentRoute.GetNodeByIndex(i);
            if (node == null) continue;
            
            Vector3 pos = GetWaypointPosition(node.waypointId);
            if (pos != Vector3.zero && TrySampleNavMeshPosition(pos, out _))
            {
                currentNodeIndex = i;
                return node;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Advance to the next node based on route rules.
    /// </summary>
    private IEnumerator AdvanceToNextNode()
    {
        if (currentRoute == null)
        {
            routeRunnerActive = false;
            yield break;
        }
        
        string prevId = currentNode?.nodeId ?? "";
        var (nextNode, nextIndex) = currentRoute.ChooseNextNode(currentNode, currentNodeIndex, previousNodeId);
        
        if (nextNode == null)
        {
            // Route ended (non-looping)
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCMovementAgent] {gameObject.name} route completed");
            }
            OnRouteCompleted?.Invoke();
            routeRunnerActive = false;
            yield break;
        }
        
        previousNodeId = prevId;
        currentNode = nextNode;
        currentNodeIndex = nextIndex;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMovementAgent] {gameObject.name} advancing to node {currentNodeIndex}: {currentNode.nodeId}");
        }
    }
    
    /// <summary>
    /// Dwell at a node with optional micro-wander.
    /// </summary>
    private IEnumerator DwellAtNode(RouteNode node, Vector3 anchorPos)
    {
        // Get effective settings
        Vector2 waitRange = currentRoute.GetEffectiveWaitTimeRange(node);
        MicroWanderSettings wanderSettings = currentRoute.GetEffectiveMicroWander(node);
        
        // Apply schedule multipliers if available
        if (currentScheduleConfig != null)
        {
            waitRange = waitRange * currentScheduleConfig.waitTimeMultiplier;
            
            if (wanderSettings != null && wanderSettings.enabled)
            {
                wanderSettings = new MicroWanderSettings
                {
                    enabled = currentScheduleConfig.microWanderEnabled && wanderSettings.enabled,
                    radius = wanderSettings.radius * currentScheduleConfig.wanderRadiusMultiplier,
                    intervalRange = wanderSettings.intervalRange * currentScheduleConfig.wanderIntervalMultiplier,
                    dwellRange = wanderSettings.dwellRange * currentScheduleConfig.wanderDwellMultiplier,
                    maxStepsPerWait = wanderSettings.maxStepsPerWait
                };
            }
        }
        
        // Calculate total dwell time
        float totalWaitTime = UnityEngine.Random.Range(waitRange.x, waitRange.y);
        float elapsedTime = 0f;
        int wanderSteps = 0;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMovementAgent] {gameObject.name} dwelling at {node.nodeId} for {totalWaitTime:F1}s (wander: {wanderSettings?.enabled ?? false})");
        }
        
        while (elapsedTime < totalWaitTime && routeRunnerActive && movementEnabled)
        {
            // Check if micro-wander is enabled
            bool canWander = wanderSettings != null && wanderSettings.enabled && wanderSteps < wanderSettings.maxStepsPerWait;
            
            if (canWander)
            {
                // Wait for interval before wandering
                float interval = UnityEngine.Random.Range(wanderSettings.intervalRange.x, wanderSettings.intervalRange.y);
                float waitStart = elapsedTime;
                
                while (elapsedTime - waitStart < interval && elapsedTime < totalWaitTime && routeRunnerActive && movementEnabled)
                {
                    yield return null;
                    elapsedTime += Time.deltaTime;
                }
                
                if (!routeRunnerActive || !movementEnabled || elapsedTime >= totalWaitTime)
                    break;
                
                // Execute micro-wander hop
                yield return MicroWanderHop(anchorPos, wanderSettings.radius);
                wanderSteps++;
                
                if (!routeRunnerActive || !movementEnabled)
                    break;
                
                // Idle dwell after hop
                float dwellTime = UnityEngine.Random.Range(wanderSettings.dwellRange.x, wanderSettings.dwellRange.y);
                float dwellStart = elapsedTime;
                
                while (elapsedTime - dwellStart < dwellTime && elapsedTime < totalWaitTime && routeRunnerActive && movementEnabled)
                {
                    yield return null;
                    elapsedTime += Time.deltaTime;
                }
            }
            else
            {
                // No wander - just wait
                yield return null;
                elapsedTime += Time.deltaTime;
            }
        }
    }
    
    /// <summary>
    /// Execute a single micro-wander hop.
    /// </summary>
    private IEnumerator MicroWanderHop(Vector3 anchorPos, float radius)
    {
        // Pick random position within radius
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
        Vector3 targetPos = anchorPos + new Vector3(randomOffset.x, 0f, randomOffset.y);
        
        // Sample to NavMesh
        if (!TrySampleNavMeshPosition(targetPos, out Vector3 sampledPos))
        {
            // Failed to sample - skip this hop
            yield break;
        }
        
        // Navigate to hop position
        yield return NavigateToPosition(sampledPos, 2f); // Shorter timeout for micro-wander
    }
    
    /// <summary>
    /// Navigate to a world position using NavMeshAgent.
    /// </summary>
    private IEnumerator NavigateToPosition(Vector3 position, float timeout = 30f)
    {
        if (!SetDestinationToPosition(position))
        {
            yield break;
        }
        
        float startTime = Time.time;
        
        while (Time.time - startTime < timeout && routeRunnerActive && movementEnabled)
        {
            if (HasArrived())
            {
                yield break;
            }
            
            yield return null;
        }
        
        // Timeout - reset path
        if (agent != null && !HasArrived())
        {
            agent.ResetPath();
        }
    }
    
    // ============================================
    // NAVIGATION HELPERS
    // ============================================
    
    /// <summary>
    /// Get waypoint position from registry.
    /// </summary>
    private Vector3 GetWaypointPosition(string waypointId)
    {
        if (string.IsNullOrEmpty(waypointId)) return Vector3.zero;
        
        var marker = NPCWaypointRegistry.Instance.GetWaypoint(waypointId);
        return marker != null ? marker.Position : Vector3.zero;
    }
    
    /// <summary>
    /// Set destination to a world position using NavMesh sampling.
    /// </summary>
    private bool SetDestinationToPosition(Vector3 position)
    {
        if (agent == null) return false;
        
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, destinationSampleRadius, NavMesh.AllAreas))
        {
            agent.stoppingDistance = stopDistance;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            return true;
        }
        
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[NPCMovementAgent] {gameObject.name} could not sample NavMesh at {position}");
        }
        return false;
    }
    
    /// <summary>
    /// Check if the agent has arrived at its destination.
    /// </summary>
    private bool HasArrived()
    {
        if (agent == null) return true;
        if (agent.pathPending) return false;
        
        if (agent.remainingDistance <= agent.stoppingDistance + ARRIVAL_EPSILON)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < VELOCITY_IDLE_THRESHOLD)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Try to sample a valid NavMesh position near the given world position.
    /// </summary>
    public bool TrySampleNavMeshPosition(Vector3 worldPosition, out Vector3 sampledPosition, float radius = -1f)
    {
        if (radius < 0f) radius = destinationSampleRadius;
        
        if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }
        
        sampledPosition = Vector3.zero;
        return false;
    }
    
    private void SetAnimationState(NPCAnimationState state)
    {
        if (IsPresentationLocked)
            return;

        if (currentAnimState == state) return;
        
        currentAnimState = state;
        
        if (visual != null)
        {
            visual.SetAnimationState(state);
        }
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Enable or disable movement.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        
        if (agent != null)
        {
            if (!enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                agent.isStopped = false;
            }
        }
        
        SetAnimationState(NPCAnimationState.Idle);
    }
    
    /// <summary>
    /// Move to waypoint ID (async).
    /// </summary>
    public IEnumerator MoveToWaypoint(string waypointId, float timeout = 30f)
    {
        Vector3 pos = GetWaypointPosition(waypointId);
        if (pos == Vector3.zero)
        {
            Debug.LogError($"[NPCMovementAgent] {gameObject.name} cannot move to waypoint '{waypointId}': waypoint not found!");
            yield break;
        }
        
        yield return MoveToPosition(pos, timeout);
    }
    
    /// <summary>
    /// Move to world position (async).
    /// </summary>
    public IEnumerator MoveToPosition(Vector3 position, float timeout = 30f)
    {
        navigationMoveToCancelled = false;
        
        if (!SetDestinationToPosition(position))
        {
            Debug.LogError($"[NPCMovementAgent] {gameObject.name} failed to set destination to {position}");
            yield break;
        }
        
        float startTime = Time.time;
        
        while (Time.time - startTime < timeout)
        {
            if (navigationMoveToCancelled)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCMovementAgent] {gameObject.name} MoveToPosition cancelled.");
                }
                yield break;
            }
            
            if (HasArrived())
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[NPCMovementAgent] {gameObject.name} reached position: {position}");
                }
                yield break;
            }
            
            yield return null;
        }
        
        // Timeout
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        
        Debug.LogWarning($"[NPCMovementAgent] {gameObject.name} timed out moving to position: {position}");
    }
    
    /// <summary>
    /// Face target (player, waypoint, or direction).
    /// </summary>
    public void FaceTarget(string targetId, FaceTargetType targetType)
    {
        Vector3 targetPos = Vector3.zero;
        
        switch (targetType)
        {
            case FaceTargetType.Player:
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    targetPos = player.transform.position;
                }
                break;
                
            case FaceTargetType.WaypointId:
                targetPos = GetWaypointPosition(targetId);
                break;
            
            case FaceTargetType.Direction:
                return;
        }
        
        if (targetPos != Vector3.zero)
        {
            Vector3 direction = targetPos - transform.position;
            direction.y = 0f;
            
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = targetRotation;

                Vector2 facingDirection = new Vector2(direction.x, direction.z).normalized;
                CacheStableWalkDirection(facingDirection, force: true);
                UpdateVisualDirection(facingDirection);
            }
        }
    }

    /// <summary>
    /// Snap facing toward a runtime transform target.
    /// </summary>
    public void SnapFaceTarget(Transform target)
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = targetRotation;

        Vector2 facingDirection = new Vector2(direction.x, direction.z).normalized;
        CacheStableWalkDirection(facingDirection, force: true);
        UpdateVisualDirection(facingDirection);
    }

    /// <summary>
    /// Snap NPC rotation and directional sprites to a preset (world, camera-relative, or player-relative).
    /// </summary>
    public void FaceDirection(NPCFaceDirectionPreset preset)
    {
        RefreshPresentationReferences();
        
        Vector3 xz = Vector3.zero;
        
        switch (preset)
        {
            case NPCFaceDirectionPreset.WorldNorth:
                xz = Vector3.forward;
                break;
            case NPCFaceDirectionPreset.WorldSouth:
                xz = Vector3.back;
                break;
            case NPCFaceDirectionPreset.WorldEast:
                xz = Vector3.right;
                break;
            case NPCFaceDirectionPreset.WorldWest:
                xz = Vector3.left;
                break;
            case NPCFaceDirectionPreset.TowardCamera:
            case NPCFaceDirectionPreset.AwayFromCamera:
            {
                Transform camT = null;
                if (cameraController != null)
                    camT = cameraController.transform;
                if (camT == null)
                {
                    Camera gameplayCamera = SceneCameraResolver.ResolveGameplayCamera();
                    if (gameplayCamera != null)
                        camT = gameplayCamera.transform;
                }
                
                if (camT != null)
                {
                    Vector3 toCam = camT.position - transform.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f)
                    {
                        xz = toCam.normalized;
                    }
                    else if (cameraController != null)
                    {
                        xz = -cameraController.GetCameraForwardXZ();
                    }
                    
                    if (preset == NPCFaceDirectionPreset.AwayFromCamera)
                        xz = -xz;
                }
                break;
            }
            case NPCFaceDirectionPreset.TowardPlayer:
            case NPCFaceDirectionPreset.AwayFromPlayer:
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    Vector3 toPlayer = playerGo.transform.position - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f)
                    {
                        xz = toPlayer.normalized;
                        if (preset == NPCFaceDirectionPreset.AwayFromPlayer)
                            xz = -xz;
                    }
                }
                break;
            }
        }
        
        if (xz.sqrMagnitude < 0.01f)
            return;
        
        xz.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(xz);
        transform.rotation = targetRotation;
        
        Vector2 facingDirection = new Vector2(xz.x, xz.z).normalized;
        CacheStableWalkDirection(facingDirection, force: true);
        UpdateVisualDirection(facingDirection);
    }

    public void SetFollowFacingTarget(Transform target)
    {
        followFacingTarget = target;
    }

    public void SetFacingTargetOverride(Transform target, bool snapImmediately = false)
    {
        facingTargetOverride = target;
        if (snapImmediately && target != null)
            SnapFaceTarget(target);
    }

    public void ClearFacingTargetOverride()
    {
        facingTargetOverride = null;
    }

    /// <summary>
    /// Store the facing the NPC should return to after a player-facing interaction.
    /// Route locomotion still replaces this once the NPC walks again.
    /// </summary>
    public void CaptureIdleFacing()
    {
        if (TryGetStableWalkDirection(out Vector2 stableFacing))
        {
            capturedIdleFacing = stableFacing;
            hasCapturedIdleFacing = true;
            return;
        }

        if (TryGetTransformFacingDirection(out Vector2 transformFacing))
        {
            capturedIdleFacing = transformFacing;
            hasCapturedIdleFacing = true;
            return;
        }

        hasCapturedIdleFacing = false;
    }

    /// <summary>
    /// Write the captured pre-interaction facing back into the idle cache.
    /// </summary>
    public void RestoreCapturedIdleFacing()
    {
        if (!hasCapturedIdleFacing)
            return;

        CacheStableWalkDirection(capturedIdleFacing, force: true);
        hasCapturedIdleFacing = false;
    }

    /// <summary>
    /// Begin a generic presentation lock so gameplay-driven visual state cannot overwrite a temporary one-shot.
    /// </summary>
    public void BeginPresentationLock()
    {
        presentationLockDepth++;
    }

    /// <summary>
    /// End a generic presentation lock previously started by <see cref="BeginPresentationLock"/>.
    /// </summary>
    public void EndPresentationLock()
    {
        presentationLockDepth = Mathf.Max(0, presentationLockDepth - 1);
    }
    
    /// <summary>
    /// Stop all movement.
    /// </summary>
    public void StopMovement()
    {
        navigationMoveToCancelled = true;
        StopRoute();
        
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        
        SetAnimationState(NPCAnimationState.Idle);
    }
    
    /// <summary>
    /// Request early exit from an in-flight <see cref="MoveToPosition"/> (e.g. fire-and-forget + stop step).
    /// </summary>
    public void CancelNavigation()
    {
        navigationMoveToCancelled = true;
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }
    
    /// <summary>
    /// Check if movement agent has an active route.
    /// </summary>
    public bool HasTarget()
    {
        return routeRunnerActive || (agent != null && agent.hasPath);
    }
    
    /// <summary>
    /// Check if movement agent is idle.
    /// </summary>
    public bool IsIdle()
    {
        if (routeRunnerActive) return false;
        if (agent == null) return true;
        return !agent.hasPath || agent.velocity.sqrMagnitude < VELOCITY_IDLE_THRESHOLD;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugPath) return;
        
        if (agent != null && agent.hasPath)
        {
            Gizmos.color = Color.yellow;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
            
            if (corners.Length > 0)
            {
                Gizmos.DrawWireSphere(corners[corners.Length - 1], stopDistance);
            }
        }
        
        // Draw current node info
        if (currentNode != null)
        {
            Vector3 pos = GetWaypointPosition(currentNode.waypointId);
            if (pos != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pos, 0.5f);
            }
        }
    }
#endif
}

/// <summary>
/// Animation state enum for NPCs.
/// </summary>
public enum NPCAnimationState
{
    Idle,
    Walk,
    Run
}
