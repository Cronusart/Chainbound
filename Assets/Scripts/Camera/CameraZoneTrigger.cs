using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Camera zone trigger with buffer zone hysteresis to prevent false exits.
///
/// Inner volume is the union of every trigger collider on this object and
/// child objects that are not a buffer. Extra boxes are one area, not extra zones.
/// Buffer copies each inner collider, slightly larger. Player must leave every
/// inner piece and every buffer piece before the camera restores.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CameraZoneTrigger : MonoBehaviour
{
    [Header("Zone Behavior")]
    [Tooltip("CameraZoneBehavior to activate/deactivate (if null, will look for one on this GameObject)")]
    [SerializeField] private CameraZoneBehavior zoneBehavior;
    
    [Header("Trigger Settings")]
    [Tooltip("Tag to detect (usually 'Player')")]
    [SerializeField] private string triggerTag = "Player";
    
    [Tooltip("If true, automatically find behavior on this or parent GameObject")]
    [SerializeField] private bool autoFindBehavior = true;
    
    [Header("Buffer Zone")]
    [Tooltip("If true, automatically create a buffer zone child collider")]
    [SerializeField] private bool useBufferZone = true;
    
    [Tooltip("How much larger the buffer zone is than the inner zone (in units)")]
    [SerializeField] private float bufferZoneExtension = 1.5f;
    
    [Tooltip("Reference to buffer zone (auto-created if null and useBufferZone is true)")]
    [SerializeField] private CameraZoneBuffer bufferZone;
    
    [Header("Safety Net")]
    [Tooltip("Enable periodic safety check to ensure player has truly exited zones")]
    [SerializeField] private bool enableSafetyNet = true;
    
    [Tooltip("How often to run safety check (seconds)")]
    [SerializeField] private float safetyCheckInterval = 0.15f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<Collider> innerColliders = new List<Collider>();
    private bool playerInInnerZone = false;
    private bool playerInBufferZone = false;
    private bool isActivated = false;
    private float lastSafetyCheckTime = 0f;
    
    // Track active player colliders for safety checks
    private HashSet<Collider> activePlayerColliders = new HashSet<Collider>();

    private void Awake()
    {
        RefreshInnerColliders(bindRelays: false);

        // Auto-find zone behavior if not assigned
        if (zoneBehavior == null && autoFindBehavior)
        {
            zoneBehavior = GetComponent<CameraZoneBehavior>();
            if (zoneBehavior == null)
            {
                zoneBehavior = GetComponentInParent<CameraZoneBehavior>();
            }
        }
        
        if (zoneBehavior == null)
        {
            Debug.LogWarning($"[CameraZoneTrigger] No CameraZoneBehavior found for {gameObject.name}. Add one to this GameObject or assign it in the inspector.");
        }
        
        // Create or find buffer zone
        if (useBufferZone)
        {
            SetupBufferZone();
        }

        RefreshInnerColliders(bindRelays: true);
    }

    private void OnEnable()
    {
        RefreshInnerColliders(bindRelays: Application.isPlaying);
        if (useBufferZone && bufferZone == null)
            SetupBufferZone();
    }

    private void Start()
    {
        RefreshInnerColliders(bindRelays: Application.isPlaying);
        if (useBufferZone && bufferZone == null)
            SetupBufferZone();
    }

    /// <summary>
    /// True when <paramref name="position"/> is inside any inner volume piece.
    /// </summary>
    public bool ContainsPosition(Vector3 position)
    {
        RefreshInnerColliders();
        return CameraZoneOccupancy.ContainsPosition(innerColliders, position);
    }

    /// <summary>True when the player is physically inside any inner volume piece.</summary>
    public bool IsPlayerPhysicallyInsideInner()
    {
        RefreshInnerColliders();
        return CameraZoneOccupancy.IsPlayerPhysicallyInside(innerColliders, triggerTag);
    }

    /// <summary>True when the player is physically inside any buffer volume piece.</summary>
    public bool IsPlayerPhysicallyInsideBuffer()
    {
        if (!useBufferZone)
            return false;

        CameraZoneBuffer[] buffers = GetComponentsInChildren<CameraZoneBuffer>(true);
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null && buffers[i].IsPlayerPhysicallyInside())
                return true;
        }

        return false;
    }

    internal void RefreshInnerColliders(bool bindRelays = false)
    {
        CameraZoneOccupancy.CollectInnerColliders(this, innerColliders, markAsTrigger: Application.isPlaying);
        if (!bindRelays || !Application.isPlaying)
            return;

        for (int i = 0; i < innerColliders.Count; i++)
            CameraZoneOccupancy.EnsureRelay(innerColliders[i], this);
    }
    
    private void SetupBufferZone()
    {
        if (bufferZone == null)
            bufferZone = GetComponentInChildren<CameraZoneBuffer>();

        RefreshInnerColliders(bindRelays: false);
        if (innerColliders.Count == 0)
            return;

        if (bufferZone == null)
        {
            GameObject bufferObj = new GameObject("BufferZone");
            bufferObj.transform.SetParent(transform);
            bufferObj.transform.localPosition = Vector3.zero;
            bufferObj.transform.localRotation = Quaternion.identity;
            bufferObj.transform.localScale = Vector3.one;

            for (int i = 0; i < innerColliders.Count; i++)
            {
                Transform host = CameraZoneOccupancy.CreateMatchingBufferHost(
                    innerColliders[i],
                    transform,
                    bufferObj.transform);
                CameraZoneOccupancy.CopyExtendedCollider(
                    innerColliders[i],
                    host,
                    bufferZoneExtension);
            }

            bufferZone = bufferObj.AddComponent<CameraZoneBuffer>();
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneTrigger] Created buffer zone for {gameObject.name} ({innerColliders.Count} volume pieces)");
        }
        else
        {
            AppendMissingBufferCopies();
        }

        bufferZone.Initialize(this, triggerTag);
    }

    private void AppendMissingBufferCopies()
    {
        if (bufferZone == null)
            return;

        var existing = new List<Collider>();
        CameraZoneOccupancy.CollectBufferColliders(bufferZone, existing, markAsTrigger: false);
        for (int i = existing.Count; i < innerColliders.Count; i++)
        {
            Transform host = CameraZoneOccupancy.CreateMatchingBufferHost(
                innerColliders[i],
                transform,
                bufferZone.transform);
            CameraZoneOccupancy.CopyExtendedCollider(
                innerColliders[i],
                host,
                bufferZoneExtension);
        }
    }

    private void Update()
    {
        // Safety net: periodic check that player is actually in/out of zones
        if (!enableSafetyNet || !isActivated)
            return;
        
        if (Time.time - lastSafetyCheckTime < safetyCheckInterval)
            return;
        
        lastSafetyCheckTime = Time.time;
        PerformSafetyCheck();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyPlayerExit(other);
    }

    /// <summary>
    /// Occupancy enter from this object or a child volume relay.
    /// </summary>
    public void NotifyPlayerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        if (zoneBehavior == null) return;
        if (CameraZoneOccupancy.IsSceneTransitionOccupancyDeferred())
            return;

        activePlayerColliders.Add(other);
        playerInInnerZone = true;
        isActivated = true;

        Vector3 samplePosition = SampleOccupancyPosition(other);
        CameraZoneTrigger winner = CameraZoneOccupancy.FindTriggerContaining(samplePosition);
        // On a walked entry, Unity reports overlap when the leading edge of the
        // player collider crosses the volume. The player root/collider transform
        // can still be outside, so point arbitration may temporarily select the
        // containing outer compound. The enter event itself proves physical
        // occupancy; a truly nested inner zone must own presentation immediately
        // because Unity will not send a second enter when the root later crosses.
        bool nestedInnerEdgeEntry = winner != null &&
                                    CameraZoneOccupancy.IsNestedPresentationPair(this, winner) &&
                                    IsPlayerPhysicallyInsideInner();
        bool thisIsWinner = winner == null || winner == this || nestedInnerEdgeEntry;
        if (!thisIsWinner)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CameraZoneTrigger] Occupied {gameObject.name} but nested winner is {winner.gameObject.name}");
            }

            if (CameraZoneOccupancy.IsNestedPresentationPair(winner, this))
                ReleaseOccupancy(restorePresentation: false);

            MarkOwningOverworldChunkActive();
            CameraZoneOccupancy.ReassertWinningZone();
            return;
        }

        CameraZoneOccupancy.DismissContainingOuterZones(this, samplePosition);

        if (!zoneBehavior.IsActive)
        {
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneTrigger] Player entered inner zone - ACTIVATING: {gameObject.name} (collider: {other.name})");

            zoneBehavior.Activate();
            MarkOwningOverworldChunkActive();
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[CameraZoneTrigger] Player re-entered inner zone (already active): {gameObject.name} (collider: {other.name})");
    }

    private Vector3 SampleOccupancyPosition(Collider other)
    {
        GameObject playerRoot = PlayerRootResolver.ResolveActivePlayer(triggerTag);
        if (playerRoot != null)
        {
            Vector3 rootPos = playerRoot.transform.position;
            if (ContainsPosition(rootPos) || OccupiesPosition(rootPos))
                return rootPos;
        }

        return other != null ? other.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Occupancy exit from this object or a child volume relay.
    /// Stays active while the player is still inside any inner piece.
    /// </summary>
    public void NotifyPlayerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        if (zoneBehavior == null) return;
        if (CameraZoneOccupancy.IsSceneTransitionOccupancyDeferred())
            return;

        activePlayerColliders.Remove(other);
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneTrigger] Player exited inner volume piece: {gameObject.name} (collider: {other.name})");

        playerInInnerZone = IsPlayerPhysicallyInsideInner();
        CheckForDeactivation();
    }
    
    /// <summary>
    /// Check if a collider belongs to the player (handles both capsule and falling colliders).
    /// Checks the collider's GameObject and all parents for the Player tag.
    /// </summary>
    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        
        // Check the collider's GameObject
        if (col.CompareTag(triggerTag))
            return true;
        
        // Check parent GameObjects (for falling collider which is a child)
        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(triggerTag))
                return true;
            parent = parent.parent;
        }
        
        return false;
    }
    
    /// <summary>
    /// Called by CameraZoneBuffer when player enters the buffer zone.
    /// </summary>
    public void OnBufferZoneEnter()
    {
        playerInBufferZone = true;
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneTrigger] Player entered buffer zone: {gameObject.name}");
    }
    
    /// <summary>
    /// Called by CameraZoneBuffer when player exits the buffer zone.
    /// </summary>
    public void OnBufferZoneExit()
    {
        playerInBufferZone = false;
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneTrigger] Player exited buffer zone: {gameObject.name}");
        
        // Check if we should deactivate (player left both zones)
        CheckForDeactivation();
    }
    
    /// <summary>
    /// Check if player has left both zones and should deactivate.
    /// </summary>
    private void CheckForDeactivation()
    {
        if (!isActivated) return;
        
        // If using buffer zone, must be outside BOTH zones to deactivate
        if (useBufferZone && bufferZone != null)
        {
            if (playerInInnerZone || playerInBufferZone)
            {
                if (enableDebugLogs)
                    Debug.Log($"[CameraZoneTrigger] Player still in buffer zone, staying active: {gameObject.name}");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[CameraZoneTrigger] Player exited both zones - DEACTIVATING: {gameObject.name}");
        }
        else if (playerInInnerZone)
        {
            return;
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[CameraZoneTrigger] Player exited inner zone (no buffer) - DEACTIVATING: {gameObject.name}");
        }

        CommitOccupancyLoss();
    }
    
    /// <summary>
    /// Check if player is currently inside this trigger (inner zone).
    /// </summary>
    public bool IsPlayerInside => playerInInnerZone;
    
    /// <summary>
    /// Check if player is in the buffer zone.
    /// </summary>
    public bool IsPlayerInBuffer => playerInBufferZone;
    
    /// <summary>
    /// Check if the zone is currently activated.
    /// </summary>
    public bool IsActivated => isActivated;
    
    /// <summary>
    /// Get the associated zone behavior.
    /// </summary>
    public CameraZoneBehavior ZoneBehavior => zoneBehavior;
    
    /// <summary>
    /// Manually set the zone behavior reference.
    /// </summary>
    public void SetZoneBehavior(CameraZoneBehavior behavior)
    {
        zoneBehavior = behavior;
    }
    
    /// <summary>
    /// Force reset the trigger state (use if player teleports).
    /// </summary>
    public void ForceReset()
    {
        playerInInnerZone = false;
        playerInBufferZone = false;
        isActivated = false;
    }
    
    /// <summary>
    /// True when <paramref name="position"/> is inside this zone's inner or buffer volume.
    /// Used for spawn/teleport so occupancy does not depend on physics enter.
    /// </summary>
    public bool OccupiesPosition(Vector3 position)
    {
        return ContainsPosition(position) || CameraZoneOccupancy.BufferContainsPosition(this, position);
    }

    /// <summary>
    /// Manually activate the zone when player is already inside (e.g., after teleport/spawn).
    /// This simulates OnTriggerEnter being called.
    /// </summary>
    public void ForceActivateIfPlayerInside()
    {
        if (zoneBehavior == null)
        {
            Debug.LogWarning($"[CameraZoneTrigger] ForceActivateIfPlayerInside called but zoneBehavior is null on {gameObject.name}");
            return;
        }
        
        GameObject playerObj = PlayerRootResolver.ResolveActivePlayer(triggerTag);
        if (playerObj == null)
        {
            Debug.LogWarning($"[CameraZoneTrigger] ForceActivateIfPlayerInside: Could not find player with tag '{triggerTag}'");
            return;
        }

        ForceActivateAtPosition(playerObj.transform.position);
    }

    /// <summary>
    /// Activate from a known world position (spawn/teleport). Point occupancy
    /// only — collider AABB overlap is not enough. If this zone already owns
    /// the player, presentation is reapplied so a dismissed nested zone cannot
    /// leave its profile on the camera.
    /// </summary>
    public void ForceActivateAtPosition(Vector3 position, bool reassertPresentation = false)
    {
        if (zoneBehavior == null)
        {
            Debug.LogWarning($"[CameraZoneTrigger] ForceActivateAtPosition called but zoneBehavior is null on {gameObject.name}");
            return;
        }

        bool playerInZone = ContainsPosition(position);
        bool playerInBuffer = CameraZoneOccupancy.BufferContainsPosition(this, position);

        if (enableDebugLogs)
        {
            Debug.Log($"[CameraZoneTrigger] ForceActivateAtPosition check on {gameObject.name}: " +
                     $"Position: {position}, In inner zone: {playerInZone}, In buffer: {playerInBuffer}");
        }

        if (playerInZone || playerInBuffer)
        {
            playerInInnerZone = playerInZone;
            playerInBufferZone = playerInBuffer;
            isActivated = true;

            CameraZoneTrigger winner = CameraZoneOccupancy.FindTriggerContaining(position);
            if (winner != null && winner != this)
            {
                if (CameraZoneOccupancy.IsNestedPresentationPair(winner, this))
                    ReleaseOccupancy(restorePresentation: false);
                CameraZoneOccupancy.ReassertWinningZone();
                return;
            }

            CameraZoneOccupancy.DismissContainingOuterZones(this, position);

            if (!zoneBehavior.IsActive)
                zoneBehavior.Activate();
            else
            {
                _ = reassertPresentation;
                zoneBehavior.ReassertPresentation();
            }
            MarkOwningOverworldChunkActive();
        }
        else if (isActivated)
        {
            CommitOccupancyLoss();
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[CameraZoneTrigger] Player not inside zone {gameObject.name}.");
        }
    }
    
    /// <summary>
    /// Force deactivation if player is truly outside all zones (safety net).
    /// Called by CameraZoneBuffer safety check.
    /// </summary>
    public void ForceDeactivationIfOutside()
    {
        if (!isActivated || zoneBehavior == null)
            return;
        
        // Double-check that player is actually outside
        if (!playerInInnerZone && !playerInBufferZone)
        {
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneTrigger] Safety net: Force deactivating {gameObject.name} - player confirmed outside all zones");

            CommitOccupancyLoss();
        }
    }

    /// <summary>
    /// Camera zones already mark traversable authored space. When that space is
    /// an additive overworld chunk, use the same reusable signal to keep Unity's
    /// active scene aligned with the player for spawn and encounter snapshots.
    /// Soundtrack stays owned by mapped scene loads, not this active-scene flip.
    /// </summary>
    private void MarkOwningOverworldChunkActive()
    {
        OverworldStreamingService streaming = OverworldStreamingService.Instance;
        if (streaming != null && streaming.Config != null && streaming.Config.ContainsScene(gameObject.scene.name))
            streaming.SetActiveChunk(gameObject.scene.name);
    }
    
    /// <summary>
    /// Safety net: Verify player is actually inside zones.
    /// Catches edge cases where trigger events might be missed during jumps.
    /// 
    /// IMPORTANT: Unlike VisualNoClipPushCollider, we don't remove disabled colliders.
    /// CameraZone should stay active based on player POSITION, not collider enabled state.
    /// </summary>
    private void PerformSafetyCheck()
    {
        if (!isActivated)
            return;
        
        // Clean up tracked colliders - ONLY remove null/destroyed, NOT disabled
        // (player colliders may be temporarily disabled during camera transitions)
        List<Collider> collidersToRemove = new List<Collider>();
        foreach (Collider col in activePlayerColliders)
        {
            // Only remove if completely destroyed (null) or GameObject destroyed
            if (col == null || col.gameObject == null)
            {
                collidersToRemove.Add(col);
            }
        }
        foreach (Collider col in collidersToRemove)
        {
            activePlayerColliders.Remove(col);
        }
        
        // Check if player is PHYSICALLY inside zones (regardless of which collider is enabled)
        bool anyInInner = IsPlayerPhysicallyInsideInner();
        bool anyInBuffer = IsPlayerPhysicallyInsideBuffer();
        
        // If player is truly outside both zones, trigger hard reset
        if (!anyInInner && !anyInBuffer)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CameraZoneTrigger] Safety net: Player is physically outside all zones. Triggering hard reset for {gameObject.name}");
            }
            
            HardReset();
        }
        else
        {
            // Update state to match reality
            if (playerInInnerZone != anyInInner)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[CameraZoneTrigger] Safety net: Correcting inner zone state: {playerInInnerZone} -> {anyInInner}");
                }
                playerInInnerZone = anyInInner;
            }
            if (playerInBufferZone != anyInBuffer)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[CameraZoneTrigger] Safety net: Correcting buffer zone state: {playerInBufferZone} -> {anyInBuffer}");
                }
                playerInBufferZone = anyInBuffer;
            }
        }
    }
    
    /// <summary>
    /// Clears occupancy. Restores camera presentation only when this is the last
    /// live zone that thought it owned the player (chunk unload / false trigger).
    /// </summary>
    public void ReleaseOccupancy(bool restorePresentation)
    {
        activePlayerColliders.Clear();
        playerInInnerZone = false;
        playerInBufferZone = false;

        if (bufferZone != null)
            bufferZone.HardReset();

        if (!isActivated)
            return;

        isActivated = false;
        if (zoneBehavior == null)
            return;

        if (restorePresentation)
        {
            zoneBehavior.Deactivate();
            EnsurePlayerUnfrozen();
            return;
        }

        zoneBehavior.DismissWithoutRestore();
    }

    /// <summary>
    /// Hard reset: Immediately deactivate camera zone and clear all state.
    /// Use when safety check detects no player colliders in any zone.
    /// This ensures the camera returns to the default scene profile.
    /// </summary>
    public void HardReset()
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[CameraZoneTrigger] HARD RESET: Clearing all state and deactivating zone for {gameObject.name}");
        }

        CommitOccupancyLoss();
    }

    private void CommitOccupancyLoss()
    {
        bool restorePresentation = !CameraZoneOccupancy.AnyLiveZoneContainsPlayer(this);
        ReleaseOccupancy(restorePresentation);
        if (!restorePresentation)
            CameraZoneOccupancy.ReassertWinningZone(except: this);
    }
    
    /// <summary>
    /// Safety method to ensure player is not left frozen after a hard reset.
    /// </summary>
    private void EnsurePlayerUnfrozen()
    {
        GameObject playerObj = PlayerRootResolver.ResolveActivePlayer(triggerTag);
        if (playerObj != null)
        {
            PlayerMovement3D playerMovement = playerObj.GetComponent<PlayerMovement3D>();
            if (playerMovement != null)
            {
                // Small delay to let Deactivate() coroutine finish, then ensure unfrozen
                StartCoroutine(DelayedUnfreeze(playerMovement));
            }
        }
    }
    
    private System.Collections.IEnumerator DelayedUnfreeze(PlayerMovement3D playerMovement)
    {
        // Wait for any Deactivate coroutines to complete
        yield return new WaitForSeconds(0.5f);
        
        // Ensure player is unfrozen
        if (playerMovement != null)
        {
            playerMovement.SetFrozen(false);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[CameraZoneTrigger] Safety: Ensured player is unfrozen after hard reset");
            }
        }
    }

    /// <summary>
    /// Draw gizmo in editor for visualization.
    /// </summary>
    private void OnDrawGizmos()
    {
        RefreshInnerColliders();
        if (innerColliders.Count == 0)
            return;

        for (int i = 0; i < innerColliders.Count; i++)
        {
            Collider inner = innerColliders[i];
            if (inner == null)
                continue;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            CameraZoneOccupancy.DrawColliderGizmo(inner, false);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            CameraZoneOccupancy.DrawColliderGizmo(inner, true);

            if (useBufferZone && bufferZone == null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                CameraZoneOccupancy.DrawBufferPreviewGizmo(inner, bufferZoneExtension);
            }
        }
    }
}
