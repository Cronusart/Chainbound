using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Camera zone behavior that can be activated via triggers or events.
/// Handles camera profile/zoom/yaw/pitch changes with proper state save/restore.
/// Supports both trigger colliders and scripted activation (dialogues, cutscenes).
/// </summary>
public class CameraZoneBehavior : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private TopDownCameraController3D cameraController;
    
    [Header("Player References")]
    [SerializeField] private PlayerMovement3D playerMovement;
    
    [Tooltip("Player animation controller (for rotating facing direction during camera rotation)")]
    [SerializeField] private PlayerAnimationController3D_Mesh playerAnimationController;
    
    [Header("Player Facing Direction")]
    [Tooltip("How to handle player facing direction during camera transitions")]
    [SerializeField] private FacingDirectionBehavior facingBehavior = FacingDirectionBehavior.FaceUp;
    
    public enum FacingDirectionBehavior
    {
        [Tooltip("Do nothing - keep current facing direction")]
        KeepCurrent,
        [Tooltip("Force player to face Up when zone activates (recommended for trigger zones)")]
        FaceUp,
        [Tooltip("Rotate facing direction to match camera rotation")]
        RotateWithCamera
    }
    
    [Header("Zone Profile")]
    [Tooltip("Camera profile to apply when zone activates (optional)")]
    [SerializeField] private CameraProfile zoneProfile;
    
    [Tooltip("Blend time for profile transition")]
    [SerializeField] private float profileBlendTime = 0.5f;
    
    [Header("Zoom Override")]
    [Tooltip("Override zoom level (0 = use current/zone profile default)")]
    [SerializeField] private float zoomOverride = 0f;
    
    [Tooltip("Smooth zoom transition time")]
    [SerializeField] private float zoomTransitionTime = 0.5f;
    
    [Header("Yaw Rotation (90° Only)")]
    [Tooltip("Yaw offset in 90° steps from the camera default (-3 to +3). 0 = no change, 1 = 90° clockwise. Applied as an absolute target so re-entry cannot stack.")]
    [Range(-3, 3)]
    [SerializeField] private int yawRotation90Steps = 0;
    
    [Tooltip("If true, set absolute yaw index instead of relative rotation")]
    [SerializeField] private bool useAbsoluteYaw = false;
    
    [Tooltip("Absolute yaw index (0-3) if useAbsoluteYaw is true. 0=N, 1=E, 2=S, 3=W")]
    [Range(0, 3)]
    [SerializeField] private int absoluteYawIndex = 0;
    
    [Tooltip("Rotation duration (seconds)")]
    [SerializeField] private float rotationDuration = 0.3f;
    
    [Header("Pitch Override")]
    [Tooltip("Enable pitch angle override")]
    [SerializeField] private bool overridePitch = false;
    
    [Tooltip("Pitch angle override (degrees)")]
    [Range(0f, 90f)]
    [SerializeField] private float pitchOverrideAngle = 45f;
    
    [Tooltip("Pitch transition time")]
    [SerializeField] private float pitchTransitionTime = 0.3f;
    
    [Header("Player Freeze")]
    [Tooltip("Freeze player movement during camera transition")]
    [SerializeField] private bool freezePlayerDuringTransition = true;
    
    [Tooltip("Extra buffer time after rotation completes before unfreezing")]
    [SerializeField] private float freezeExtraBuffer = 0.1f;
    
    [Header("Exit Behavior")]
    [Tooltip("What to do when zone deactivates")]
    [SerializeField] private ExitBehavior exitBehavior = ExitBehavior.RestoreSnapshot;
    
    [Tooltip("Profile to restore on exit (if RestoreProfile)")]
    [SerializeField] private CameraProfile exitProfile;

    [Header("Scene Presentation")]
    [Tooltip("GameObjects enabled while this zone is active (e.g. destination floor). Applied after yaw rotation, or immediately if yaw does not change.")]
    [SerializeField] private List<GameObject> enableWhenActive = new List<GameObject>();
    [Tooltip("GameObjects disabled while this zone is active (e.g. origin floor that would occlude the camera). Applied after yaw rotation, or immediately if yaw does not change.")]
    [SerializeField] private List<GameObject> disableWhenActive = new List<GameObject>();
    
    [Header("Events")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;
    public UnityEvent OnCameraRotationStarted;
    public UnityEvent OnCameraRotationComplete;
    public UnityEvent OnPlayerFrozen;
    public UnityEvent OnPlayerUnfrozen;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // State
    private bool isActive = false;
    private CameraStateSnapshot previousSnapshot;
    private CameraStateSnapshot heldRestoreSnapshot;
    private Coroutine freezeCoroutine;
    private Coroutine activationCoroutine;
    private readonly List<PresentationSnapshot> presentationSnapshots = new List<PresentationSnapshot>();
    private bool hasPresentationSnapshot;

    private struct PresentationSnapshot
    {
        public GameObject Target;
        public bool WasActive;
    }
    
    public enum ExitBehavior
    {
        RestoreSnapshot,    // Full state restore (profile, yaw, zoom, pitch)
        RestoreProfileOnly, // Only restore profile (keep other changes)
        DoNothing           // Keep zone settings after exit
    }
    
    /// <summary>
    /// Check if zone is currently active.
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>True while activation or its camera transition is still being applied.</summary>
    public bool IsTransitioning =>
        activationCoroutine != null ||
        (cameraController != null && (cameraController.IsRotatingYaw || cameraController.IsBlendingProfile));

    /// <summary>True once the active zone has applied its final player/camera state.</summary>
    public bool IsTransitionSettled => isActive && !IsTransitioning;
    
    private void Awake()
    {
        ResolveCameraController();
        
        // Try to find player references (may need to refresh later if player persists across scenes)
        RefreshPlayerReferences();
    }
    
    /// <summary>
    /// Refresh player references. Call this when player may have changed (e.g., scene load with persisted player).
    /// </summary>
    public void RefreshPlayerReferences()
    {
        // Several player children carry the Player tag for collision routing. Resolve
        // the movement root explicitly so a tagged Silhouette/FallCollider cannot make
        // the zone skip its final collider, visual, and shadow yaw synchronization.
        GameObject player = PlayerRootResolver.ResolveActivePlayer();
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement3D>();
            if (enableDebugLogs && playerMovement != null)
            {
                Debug.Log($"[CameraZoneBehavior] Found PlayerMovement3D on {player.name}");
            }

            playerAnimationController = player.GetComponentInChildren<PlayerAnimationController3D_Mesh>(true);
            if (enableDebugLogs && playerAnimationController != null)
            {
                Debug.Log($"[CameraZoneBehavior] Found PlayerAnimationController3D_Mesh on {playerAnimationController.gameObject.name}");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[CameraZoneBehavior] Could not resolve the active player root");
        }
        
        ResolveCameraController();
    }

    private void ResolveCameraController()
    {
        TopDownCameraController3D resolved = SceneCameraResolver.ResolveGameplayCameraController();
        if (resolved != null)
        {
            cameraController = resolved;
            return;
        }

        resolved = SceneCameraResolver.ResolveGameplayCameraController(includeInactive: true);
        if (resolved != null)
        {
            cameraController = resolved;
            return;
        }

        if (cameraController != null)
            return;

        cameraController = FindFirstObjectByType<TopDownCameraController3D>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Drops active occupancy without restoring camera yaw/profile.
    /// Used when this zone is unloading or was a false trigger, and another
    /// still-loaded zone still contains the player.
    /// </summary>
    public void DismissWithoutRestore()
    {
        isActive = false;
        StopZoneCoroutines();
        if (playerMovement != null)
            playerMovement.SetFrozen(false);
    }

    /// <summary>
    /// Re-apply this zone's yaw, profile, zoom, pitch, and object lists without
    /// recapturing the restore snapshot. Used when overlapping occupancy returns
    /// to this zone after a nested zone dismisses, and when spawn/stream resync
    /// must replace a stale inner profile. If this zone was dismissed without
    /// restore, <see cref="Activate"/> resumes with the preserved snapshot.
    /// </summary>
    public void ReassertPresentation()
    {
        if (!isActive)
        {
            Activate();
            return;
        }

        RefreshPlayerReferences();
        if (cameraController == null)
        {
            Debug.LogWarning($"[CameraZoneBehavior] No camera controller assigned: {gameObject.name}");
            return;
        }

        if (playerMovement != null)
            playerMovement.SetCameraController(cameraController);

        if (CameraAlreadyMatchesZonePresentation())
        {
            ApplyPresentation();
            return;
        }

        StopZoneCoroutines();
        activationCoroutine = StartCoroutine(ActivationSequence(freezePlayer: false));
    }
    
    /// <summary>
    /// Activate zone behavior. Can be called by triggers or scripts.
    /// </summary>
    public void Activate()
    {
        if (isActive)
        {
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBehavior] Zone already active: {gameObject.name}");
            return;
        }
        
        // Refresh player references before activation
        // This is critical when player persists across scenes (DontDestroyOnLoad)
        // as the references from Awake() may be stale or null
        RefreshPlayerReferences();
        
        // Also refresh the player's camera controller reference
        // This ensures PlayerMovement3D uses the correct camera for input transformation
        if (playerMovement != null && cameraController != null)
        {
            playerMovement.SetCameraController(cameraController);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[CameraZoneBehavior] Set player camera controller to: {cameraController.gameObject.name}");
            }
        }
        
        if (cameraController == null)
        {
            Debug.LogWarning($"[CameraZoneBehavior] No camera controller assigned: {gameObject.name}");
            return;
        }
        
        isActive = true;

        // Nested exclusive presentation dismisses this zone without restore.
        // Keep the first overworld snapshot until a full Deactivate; recapturing
        // would store the inner zone's profile/yaw as the restore target.
        if (!heldRestoreSnapshot.isValid)
        {
            heldRestoreSnapshot = cameraController.GetStateSnapshot();
            if (!useAbsoluteYaw && heldRestoreSnapshot.yawIndex90 == ResolveZoneYawIndex())
                heldRestoreSnapshot.yawIndex90 = cameraController.DefaultYawIndex90;

            CapturePresentationSnapshot();
        }

        previousSnapshot = heldRestoreSnapshot;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[CameraZoneBehavior] Activating zone: {gameObject.name}. " +
                     $"PlayerMovement: {(playerMovement != null ? playerMovement.gameObject.name : "NULL")}, " +
                     $"PlayerAnimation: {(playerAnimationController != null ? playerAnimationController.gameObject.name : "NULL")}, " +
                     $"Camera: {(cameraController != null ? cameraController.gameObject.name : "NULL")}");
        }
        
        // Start activation coroutine to handle freeze/transitions
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
        }
        activationCoroutine = StartCoroutine(ActivationSequence(freezePlayerDuringTransition));
        
        OnActivated?.Invoke();
    }
    
    /// <summary>
    /// Deactivate zone behavior and restore previous state.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive && !heldRestoreSnapshot.isValid)
        {
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBehavior] Zone already inactive: {gameObject.name}");
            return;
        }
        
        isActive = false;
        if (heldRestoreSnapshot.isValid)
            previousSnapshot = heldRestoreSnapshot;
        heldRestoreSnapshot = CameraStateSnapshot.Invalid;
        
        // Refresh player references in case they changed during activation
        RefreshPlayerReferences();
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneBehavior] Deactivating zone: {gameObject.name}");
        
        // Stop any active coroutines
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }
        
        // Handle exit behavior
        switch (exitBehavior)
        {
            case ExitBehavior.RestoreSnapshot:
                RestorePresentationSnapshot();
                if (previousSnapshot.isValid && cameraController != null)
                {
                    previousSnapshot.yawIndex90 = ResolveRestoreYawIndex(previousSnapshot);

                    // Calculate yaw delta for player facing rotation
                    int currentYawIndex = cameraController.YawIndex90;
                    int targetYawIndex = previousSnapshot.yawIndex90;
                    int yawDeltaSteps = targetYawIndex - currentYawIndex;
                    
                    // Normalize to shortest path (-2 to +2)
                    if (yawDeltaSteps > 2) yawDeltaSteps -= 4;
                    if (yawDeltaSteps < -2) yawDeltaSteps += 4;
                    
                    // Handle player facing direction based on behavior setting
                    if (playerAnimationController != null)
                    {
                        switch (facingBehavior)
                        {
                            case FacingDirectionBehavior.FaceUp:
                                // Keep facing up - simple and consistent
                                playerAnimationController.ForceFaceUp();
                                if (enableDebugLogs)
                                    Debug.Log("[CameraZoneBehavior] On exit: Forced player to face Up");
                                break;
                                
                            case FacingDirectionBehavior.RotateWithCamera:
                                // Rotate facing direction to match camera restore
                                if (yawDeltaSteps != 0)
                                {
                                    playerAnimationController.RotateFacingDirection(yawDeltaSteps);
                                    if (enableDebugLogs)
                                        Debug.Log($"[CameraZoneBehavior] On exit: Rotated facing by {yawDeltaSteps} steps");
                                }
                                break;
                                
                            case FacingDirectionBehavior.KeepCurrent:
                            default:
                                // Do nothing
                                break;
                        }
                    }
                    
                    // NOTE: Collider update is deferred until AFTER camera restore completes
                    // to prevent false trigger re-entries
                    
                    // Freeze player during restore if enabled
                    if (freezePlayerDuringTransition)
                    {
                        // RestoreWithFreeze handles collider update after transitions complete
                        StartCoroutine(RestoreWithFreeze());
                    }
                    else
                    {
                        cameraController.RestoreStateSnapshot(previousSnapshot, profileBlendTime);
                        if (playerMovement != null && yawDeltaSteps != 0 && isActiveAndEnabled)
                        {
                            StartCoroutine(DeferredColliderUpdate(previousSnapshot.yawIndex90, profileBlendTime));
                        }
                    }
                }
                break;
                
            case ExitBehavior.RestoreProfileOnly:
                if (exitProfile != null && cameraController != null)
                {
                    cameraController.ApplyProfile(exitProfile, profileBlendTime);
                }
                else if (previousSnapshot.isValid && previousSnapshot.profile != null)
                {
                    cameraController.ApplyProfile(previousSnapshot.profile, profileBlendTime);
                }
                break;
                
            case ExitBehavior.DoNothing:
                // Keep current camera state
                break;
        }
        
        OnDeactivated?.Invoke();
    }
    
    /// <summary>
    /// Toggle zone active state.
    /// </summary>
    public void SetZoneActive(bool active)
    {
        if (active)
            Activate();
        else
            Deactivate();
    }
    
    /// <summary>
    /// Activate zone for a specific duration, then auto-deactivate.
    /// Useful for dialogue/cutscene moments.
    /// </summary>
    public void ActivateTemporary(float duration)
    {
        Activate();
        StartCoroutine(DeactivateAfterDelay(duration));
    }
    
    /// <summary>
    /// Alias for Activate() - semantic method for dialogue systems.
    /// </summary>
    public void ActivateForDialogue()
    {
        Activate();
    }
    
    private IEnumerator ActivationSequence(bool freezePlayer)
    {
        // Apply yaw as an absolute target. Relative steps are an offset from the
        // camera's default yaw so encounter respawn / scene re-entry cannot stack.
        int previousYawIndex = cameraController.YawIndex90;
        int targetYawIndex = ResolveZoneYawIndex();
        bool hasYawChange = targetYawIndex != previousYawIndex;
        bool cameraAlreadySettled = CameraAlreadyMatchesZonePresentation();

        bool applyFreeze = freezePlayer &&
                           freezePlayerDuringTransition &&
                           playerMovement != null &&
                           !HigherLevelHoldOwnsPlayer() &&
                           !cameraAlreadySettled;

        // Freeze player if needed
        if (applyFreeze)
        {
            playerMovement.SetFrozen(true);
            OnPlayerFrozen?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log("[CameraZoneBehavior] Player frozen");
        }
        else if (enableDebugLogs && cameraAlreadySettled && freezePlayer && freezePlayerDuringTransition)
        {
            Debug.Log("[CameraZoneBehavior] Skipping freeze; camera already matches zone yaw/profile");
        }
        
        // Calculate total transition duration
        float totalDuration = 0f;
        
        int yawDeltaSteps = 0;
        
        if (hasYawChange)
        {
            OnCameraRotationStarted?.Invoke();

            yawDeltaSteps = targetYawIndex - previousYawIndex;
            if (yawDeltaSteps > 2) yawDeltaSteps -= 4;
            if (yawDeltaSteps < -2) yawDeltaSteps += 4;

            cameraController.SetYawIndex90(targetYawIndex, false, rotationDuration);
            totalDuration = Mathf.Max(totalDuration, rotationDuration);
        }
        
        // Handle player facing direction based on behavior setting
        if (playerAnimationController != null)
        {
            switch (facingBehavior)
            {
                case FacingDirectionBehavior.FaceUp:
                    // Simple and reliable - force player to face up
                    playerAnimationController.ForceFaceUp();
                    if (enableDebugLogs)
                        Debug.Log("[CameraZoneBehavior] Forced player to face Up");
                    break;
                    
                case FacingDirectionBehavior.RotateWithCamera:
                    // Rotate facing direction to match camera rotation
                    if (yawDeltaSteps != 0)
                    {
                        playerAnimationController.RotateFacingDirection(yawDeltaSteps);
                        if (enableDebugLogs)
                            Debug.Log($"[CameraZoneBehavior] Rotated player facing direction by {yawDeltaSteps} steps");
                    }
                    break;
                    
                case FacingDirectionBehavior.KeepCurrent:
                default:
                    // Do nothing - keep current facing direction
                    if (enableDebugLogs)
                        Debug.Log("[CameraZoneBehavior] Keeping current player facing direction");
                    break;
            }
        }
        
        // NOTE: Collider update is deferred until AFTER transitions complete
        // to prevent false trigger exits during the freeze period
        
        // Apply profile only when the authored asset actually changes. Re-applying
        // the same profile restarts a blend and would freeze the player for no camera move.
        if (zoneProfile != null && !ZoneProfileAlreadyMatches())
        {
            cameraController.ApplyProfile(zoneProfile, profileBlendTime);
            totalDuration = Mathf.Max(totalDuration, profileBlendTime);
        }
        
        // Apply zoom override
        if (zoomOverride > 0f)
        {
            cameraController.SetZoomTarget(zoomOverride);
            totalDuration = Mathf.Max(totalDuration, zoomTransitionTime);
        }
        else if (zoneProfile != null && !cameraAlreadySettled)
        {
            // Use zone profile's default zoom
            cameraController.SetZoomTarget(zoneProfile.defaultZoom);
        }
        // If zoomOverride == 0 and no zone profile, keep current zoom
        
        // Apply pitch override
        if (overridePitch)
        {
            cameraController.SetPitchTarget(pitchOverrideAngle, pitchTransitionTime);
            totalDuration = Mathf.Max(totalDuration, pitchTransitionTime);
        }
        
        // Covered scene loads can freeze scaled time; occupancy still has to settle.
        if (totalDuration > 0f)
            yield return new WaitForSecondsRealtime(totalDuration);
        
        if (hasYawChange)
        {
            OnCameraRotationComplete?.Invoke();
        }

        ApplyPresentation();
        
        // Add extra buffer before unfreezing
        if (applyFreeze && freezeExtraBuffer > 0f)
            yield return new WaitForSecondsRealtime(freezeExtraBuffer);
        
        // Update player collider center and ground check offset AFTER transitions complete
        // This prevents false trigger exits during the freeze period
        if (playerMovement != null && hasYawChange)
        {
            int newYawIndex = cameraController.YawIndex90;
            playerMovement.UpdateColliderForCameraYaw(newYawIndex);
            playerMovement.UpdateGroundCheckForCameraYaw(newYawIndex);
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBehavior] Updated player collider and ground check for yaw index {newYawIndex}");
        }
        
        // Unfreeze player
        if (applyFreeze)
        {
            playerMovement.SetFrozen(false);
            OnPlayerUnfrozen?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log("[CameraZoneBehavior] Player unfrozen");
        }
        
        activationCoroutine = null;
    }
    
    private static bool HigherLevelHoldOwnsPlayer()
    {
        if (SanctuarySpawnDirector.IsPresenting)
            return true;

        SceneLoader loader = SceneLoader.Instance;
        if (loader != null && loader.IsTransitioning)
            return true;

        InputLockService inputLock = InputLockService.Instance;
        return inputLock != null && inputLock.IsMovementLocked;
    }

    private IEnumerator RestoreWithFreeze()
    {
        bool applyFreeze = playerMovement != null && !HigherLevelHoldOwnsPlayer();
        if (applyFreeze)
        {
            playerMovement.SetFrozen(true);
            OnPlayerFrozen?.Invoke();
        }
        
        // Player facing rotation is already handled in Deactivate() before this coroutine
        cameraController.RestoreStateSnapshot(previousSnapshot, profileBlendTime);
        
        // Wait for restore transitions
        float waitTime = Mathf.Max(profileBlendTime, cameraController.YawRotationDuration);
        yield return new WaitForSeconds(waitTime + freezeExtraBuffer);
        
        // Update player collider and ground check AFTER transitions complete
        if (playerMovement != null && previousSnapshot.isValid)
        {
            playerMovement.UpdateColliderForCameraYaw(previousSnapshot.yawIndex90);
            playerMovement.UpdateGroundCheckForCameraYaw(previousSnapshot.yawIndex90);
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBehavior] Restored player collider and ground check for yaw index {previousSnapshot.yawIndex90}");
        }
        
        if (applyFreeze)
        {
            playerMovement.SetFrozen(false);
            OnPlayerUnfrozen?.Invoke();
        }
    }
    
    /// <summary>
    /// Deferred collider update for non-freeze mode.
    /// Waits for camera transition before updating collider and ground check.
    /// </summary>
    private IEnumerator DeferredColliderUpdate(int yawIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (playerMovement != null)
        {
            playerMovement.UpdateColliderForCameraYaw(yawIndex);
            playerMovement.UpdateGroundCheckForCameraYaw(yawIndex);
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBehavior] Deferred collider and ground check update to yaw index {yawIndex}");
        }
    }
    
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Deactivate();
    }
    
    private void OnDisable()
    {
        if (!isActive)
        {
            StopZoneCoroutines();
            if (playerMovement != null)
                playerMovement.SetFrozen(false);
            return;
        }

        CameraZoneTrigger trigger = GetComponent<CameraZoneTrigger>();
        if (CameraZoneOccupancy.AnyLiveZoneContainsPlayer(trigger))
        {
            DismissWithoutRestore();
            return;
        }

        bool restoreWithFreeze = freezePlayerDuringTransition;
        freezePlayerDuringTransition = false;
        Deactivate();
        freezePlayerDuringTransition = restoreWithFreeze;
    }

    private void StopZoneCoroutines()
    {
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
    }

    private int ResolveZoneYawIndex()
    {
        if (cameraController == null)
            return 0;

        return useAbsoluteYaw
            ? Mod4(absoluteYawIndex)
            : Mod4(cameraController.DefaultYawIndex90 + yawRotation90Steps);
    }

    private bool ZoneYawAlreadyMatches()
    {
        return cameraController != null &&
               cameraController.YawIndex90 == ResolveZoneYawIndex();
    }

    private bool ZoneProfileAlreadyMatches()
    {
        return zoneProfile == null ||
               (cameraController != null && cameraController.CurrentProfile == zoneProfile);
    }

    /// <summary>
    /// True when this zone would not move the camera. Freeze exists to hold the
    /// player during yaw/profile/zoom/pitch transitions, not occupancy itself.
    /// Compare <see cref="TopDownCameraController3D.CurrentProfile"/> (authored SO),
    /// not ActiveProfile (runtime blend clone).
    /// </summary>
    private bool CameraAlreadyMatchesZonePresentation()
    {
        return ZoneYawAlreadyMatches() &&
               ZoneProfileAlreadyMatches() &&
               zoomOverride <= 0f &&
               !overridePitch;
    }

    /// <summary>
    /// Yaw used when this zone fully deactivates because the player is outside
    /// every live zone, including buffers. Nested and overlapping zones dismiss
    /// without this restore and reassert the zone that still contains the player.
    /// </summary>
    private int ResolveRestoreYawIndex(CameraStateSnapshot snapshot)
    {
        if (cameraController == null)
            return snapshot.yawIndex90;

        return cameraController.DefaultYawIndex90;
    }

    private static int Mod4(int value)
    {
        int n = value % 4;
        return n < 0 ? n + 4 : n;
    }

    private void CapturePresentationSnapshot()
    {
        presentationSnapshots.Clear();
        HashSet<GameObject> seen = new HashSet<GameObject>();
        CaptureList(enableWhenActive, seen);
        CaptureList(disableWhenActive, seen);
        hasPresentationSnapshot = true;
    }

    private void CaptureList(List<GameObject> objects, HashSet<GameObject> seen)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject target = objects[i];
            if (target == null || !seen.Add(target))
                continue;

            presentationSnapshots.Add(new PresentationSnapshot
            {
                Target = target,
                WasActive = target.activeSelf
            });
        }
    }

    private void ApplyPresentation()
    {
        SetActiveList(enableWhenActive, true);
        SetActiveList(disableWhenActive, false);
    }

    private static void SetActiveList(List<GameObject> objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Count; i++)
        {
            GameObject target = objects[i];
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }

    private void RestorePresentationSnapshot()
    {
        if (!hasPresentationSnapshot)
            return;

        for (int i = 0; i < presentationSnapshots.Count; i++)
        {
            PresentationSnapshot entry = presentationSnapshots[i];
            if (entry.Target != null && entry.Target.activeSelf != entry.WasActive)
                entry.Target.SetActive(entry.WasActive);
        }
    }
    
    #region Editor Helpers
    
    /// <summary>
    /// Draw gizmo in editor for visualization.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Draw an icon indicating this is a camera zone behavior
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw yaw direction indicator
        if (useAbsoluteYaw || yawRotation90Steps != 0)
        {
            Gizmos.color = Color.cyan;
            int targetIndex = useAbsoluteYaw ? absoluteYawIndex : yawRotation90Steps;
            float angle = targetIndex * 90f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Gizmos.DrawRay(transform.position, direction * 2f);
        }
    }
    
    #endregion
}


