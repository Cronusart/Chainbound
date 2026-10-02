using System;
using System.Collections;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 3D top-down camera controller with rig hierarchy, dynamic zoom, and camera moments.
/// Supports camera profiles for easy switching (Explore, Battle, etc.).
/// Enforces 90° yaw increments for 4 cardinal perspectives.
/// </summary>
public class TopDownCameraController3D : MonoBehaviour, ICombatCameraController, ICombatIntroCameraController, ICombatBattlefieldFramingController
{
    private sealed class IntroFramingToken
    {
        public CameraStateSnapshot Snapshot;
    }

    /// <summary>Runtime clone used only for intro framing; never an authored asset.</summary>
    private CameraProfile _runtimeIntroProfile;

    /// <summary>Runtime clone used only for settled battlefield follow-offset framing.</summary>
    private CameraProfile _runtimeBattlefieldFramingProfile;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody targetRb; // Optional: for velocity-based zoom

    [Header("Camera Profile")]
    [SerializeField] private CameraProfile defaultProfile;
    [SerializeField] private CameraProfile currentProfile;

    [Header("Boundaries (XZ Plane)")]
    [SerializeField] private bool useBoundaries = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-10, -10);
    [SerializeField] private Vector2 maxXZ = new Vector2(10, 10);

    [Header("Pixel Perfect (Retro Snap)")]
    [SerializeField] private bool usePixelSnap = false;
    [SerializeField] private float pixelsPerUnit = 16f;

    [Header("Manual Override")]
    [SerializeField] private bool useManualMovementMode = false;
    [SerializeField] private CameraMovementMode manualMovementMode = CameraMovementMode.Walk;

    [Header("Aspect Ratio")]
    [Tooltip("Enforce target aspect ratio (useful for Solana Play device)")]
    [SerializeField] private bool enforceAspectRatio = false;
    [Tooltip("Target aspect ratio (width/height). Default 16:9 for Solana Play")]
    [SerializeField] private float targetAspectRatio = 16f / 9f;

    [Header("Depth Blur (Optional)")]
    [Tooltip("Enable depth blur for far objects")]
    [SerializeField] private bool enableDepthBlur = false;
    [Tooltip("Focus distance (objects closer than this are sharp)")]
    [SerializeField] private float focusDistance = 5f;
    [Tooltip("Blur intensity for far objects")]
    [SerializeField] private float blurIntensity = 2f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Header("Events")]
    [Tooltip("Fired when camera yaw rotation starts")]
    public UnityEvent OnYawRotationStarted;
    
    [Tooltip("Fired when camera yaw rotation completes")]
    public UnityEvent OnYawRotationComplete;
    
    [Tooltip("Fired when pitch override starts")]
    public UnityEvent OnPitchOverrideStarted;
    
    [Tooltip("Fired when pitch override completes")]
    public UnityEvent OnPitchOverrideComplete;

    // Rig hierarchy references (cached)
    private Transform rigRoot;
    private Transform yawPivot;
    private Transform pitchPivot;
    private Camera cam;

    // State
    private Vector3 velocity = Vector3.zero; // XZ velocity for SmoothDamp
    private float yVelocity = 0f; // Y velocity for SmoothDamp (separate smoothing)
    private float currentZoom;
    private float zoomVelocity = 0f;
    private float targetZoom;
    private float currentYaw = 0f;
    private float targetYaw = 0f;
    private bool isRotatingYaw = false;
    private Coroutine yawRotationCoroutine;
    private float yawRotationDurationOverride = -1f;
    
    // 90° yaw tracking (0-3 for N/E/S/W perspectives)
    private int yawIndex90 = 0;
    private int defaultYawIndex90;
    
    // Pitch override state
    private float currentPitch = 45f;
    private float targetPitch = 45f;
    private bool hasPitchOverride = false;
    private Coroutine pitchTransitionCoroutine;

    // Profile blending
    private CameraProfile activeProfile;
    private bool isBlendingProfile = false;
    private Coroutine blendCoroutine;

    // Position override (for camera shots - pause follow while moving rig)
    private bool pauseFollowPosition = false;

    /// <summary>True while a profile blend is in progress.</summary>
    public bool IsBlendingProfile => isBlendingProfile;

    /// <summary>Camera's follow target. Used to skip position move when shot target is same.</summary>
    public Transform Target => target;
    
    // Camera shake state
    private Vector3 shakeOffset = Vector3.zero;
    private Coroutine shakeCoroutine;
    private bool isShaking = false;

    private void Awake()
    {
        // 1. Initialize profile FIRST
        if (currentProfile == null && defaultProfile != null)
        {
            currentProfile = defaultProfile;
        }
        activeProfile = currentProfile;

        // 2. Cache camera component
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("[TopDownCameraController3D] No Camera component found!");
            return;
        }

        // Set default clipping planes immediately (before profile is applied)
        // This prevents clipping issues even if profile hasn't loaded yet
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        
        // Reset camera rect to full screen (fixes aspect ratio issues)
        cam.rect = new Rect(0, 0, 1, 1);
        
        // Initialize camera projection and FOV immediately to prevent black screen
        if (activeProfile != null)
        {
            cam.orthographic = activeProfile.isOrthographic;
            if (!activeProfile.isOrthographic)
            {
                cam.fieldOfView = activeProfile.fieldOfView; // Set FOV immediately
            }
        }
        else
        {
            // Default to perspective with 90 FOV if no profile
            cam.orthographic = false;
            cam.fieldOfView = 90f;
        }

        // 3. Setup rig hierarchy (now profile is available)
        SetupRigHierarchy();

        // 4. Apply profile (will override above defaults if profile exists)
        if (activeProfile != null)
        {
            ApplyProfileImmediate(activeProfile);
        }

        defaultYawIndex90 = yawIndex90;
    }

    private void Start()
    {
        // Auto-find target if not assigned
        if (target == null)
        {
            SpawnService spawnService = FindFirstObjectByType<SpawnService>();
            bool isCombatScene = spawnService != null
                ? spawnService.IsCombatScene(gameObject.scene)
                : CombatSceneUtility.IsCombatScene(gameObject.scene);
            bool allowOverworldPlayerTarget = !isCombatScene;
            GameObject player = allowOverworldPlayerTarget && spawnService != null
                ? spawnService.GetResolvedPlayerRoot()
                : null;
            if (player == null && allowOverworldPlayerTarget)
            {
                PlayerMovement3D movement = FindFirstObjectByType<PlayerMovement3D>();
                player = movement != null ? movement.gameObject : null;
            }
            if (player != null)
                target = player.transform;
        }

        // Auto-find target Rigidbody if not assigned
        if (targetRb == null && target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }

        // Initialize zoom and ensure camera is properly set up
        if (currentProfile != null)
        {
            targetZoom = currentProfile.walkZoom;
            currentZoom = targetZoom;
            
            // Ensure camera projection is set correctly
            cam.orthographic = currentProfile.isOrthographic;
            if (!currentProfile.isOrthographic)
            {
                // Set FOV immediately to prevent black screen
                cam.fieldOfView = currentProfile.fieldOfView;
            }
            
            ApplyZoom(currentZoom);
        }
        else
        {
            // Fallback: ensure camera has valid FOV
            if (!cam.orthographic && cam.fieldOfView <= 0)
            {
                cam.fieldOfView = 60f;
            }
        }

        // Apply aspect ratio enforcement
        if (enforceAspectRatio && cam != null)
        {
            ApplyAspectRatio();
        }

        // Setup depth blur if enabled
        if (enableDepthBlur)
        {
            SetupDepthBlur();
        }
    }

    /// <summary>
    /// Setup depth blur component for far object blurring.
    /// </summary>
    private void SetupDepthBlur()
    {
        TopDownDepthBlur depthBlur = GetComponent<TopDownDepthBlur>();
        if (depthBlur == null)
        {
            depthBlur = gameObject.AddComponent<TopDownDepthBlur>();
            if (enableDebugLogs)
                Debug.Log("[TopDownCameraController3D] Added TopDownDepthBlur component");
        }

        // Configure blur settings
        depthBlur.SetFocusDistance(focusDistance);
        depthBlur.SetBlurIntensity(blurIntensity);
        depthBlur.SetBlurEnabled(true);
    }

    private void LateUpdate()
    {
        if (target == null || rigRoot == null) return;

        UpdateFollow();
        UpdateZoom();
        ApplyCameraShake();
    }

    /// <summary>
    /// Setup camera rig hierarchy: Root -> YawPivot -> PitchPivot -> Camera
    /// Prevents duplication by checking if rig already exists.
    /// </summary>
    private void SetupRigHierarchy()
    {
        // Check if we're already in a valid rig hierarchy to prevent duplication
        Transform currentParent = transform.parent;
        bool alreadyInRig = false;

        // Walk up the hierarchy to find if we're already in a rig
        while (currentParent != null)
        {
            if (currentParent.name == "CameraRigRoot")
            {
                // Found existing rig root - use it
                rigRoot = currentParent;
                alreadyInRig = true;
                
                // Find existing pivots
                if (rigRoot.childCount > 0)
                {
                    Transform child = rigRoot.GetChild(0);
                    if (child.name == "CameraYawPivot")
                    {
                        yawPivot = child;
                        if (yawPivot.childCount > 0)
                        {
                            Transform grandchild = yawPivot.GetChild(0);
                            if (grandchild.name == "CameraPitchPivot")
                            {
                                pitchPivot = grandchild;
                                // Camera should be child of pitch pivot
                                if (transform.parent == pitchPivot)
                                {
                                    if (enableDebugLogs)
                                        Debug.Log("[TopDownCameraController3D] Using existing rig hierarchy");
                                    return; // Already properly set up
                                }
                            }
                        }
                    }
                }
                break;
            }
            currentParent = currentParent.parent;
        }

        // If not in a rig, create one
        if (!alreadyInRig)
        {
            // Find or create rig root
            rigRoot = transform.parent;
            if (rigRoot == null || rigRoot.name != "CameraRigRoot")
            {
                GameObject rootObj = new GameObject("CameraRigRoot");
                rootObj.transform.SetParent(transform.parent);
                rootObj.transform.position = transform.position;
                rootObj.transform.rotation = Quaternion.identity;
                transform.SetParent(rootObj.transform);
                rigRoot = rootObj.transform;
                
                if (enableDebugLogs)
                    Debug.Log("[TopDownCameraController3D] Created CameraRigRoot");
            }
        }

        // Find or create yaw pivot
        if (yawPivot == null)
        {
            if (rigRoot.childCount > 0)
            {
                // Check if first child is yaw pivot
                Transform firstChild = rigRoot.GetChild(0);
                if (firstChild.name == "CameraYawPivot")
                {
                    yawPivot = firstChild;
                }
                else
                {
                    // Check all children for yaw pivot
                    for (int i = 0; i < rigRoot.childCount; i++)
                    {
                        if (rigRoot.GetChild(i).name == "CameraYawPivot")
                        {
                            yawPivot = rigRoot.GetChild(i);
                            break;
                        }
                    }
                }
            }
            
            if (yawPivot == null)
            {
                GameObject yawObj = new GameObject("CameraYawPivot");
                yawObj.transform.SetParent(rigRoot);
                yawObj.transform.localPosition = Vector3.zero;
                yawObj.transform.localRotation = Quaternion.identity;
                yawPivot = yawObj.transform;
                
                if (enableDebugLogs)
                    Debug.Log("[TopDownCameraController3D] Created CameraYawPivot");
            }
        }

        // Find or create pitch pivot
        if (pitchPivot == null)
        {
            if (yawPivot.childCount > 0)
            {
                Transform firstChild = yawPivot.GetChild(0);
                if (firstChild.name == "CameraPitchPivot")
                {
                    pitchPivot = firstChild;
                }
                else
                {
                    // Check all children
                    for (int i = 0; i < yawPivot.childCount; i++)
                    {
                        if (yawPivot.GetChild(i).name == "CameraPitchPivot")
                        {
                            pitchPivot = yawPivot.GetChild(i);
                            break;
                        }
                    }
                }
            }
            
            if (pitchPivot == null)
            {
                GameObject pitchObj = new GameObject("CameraPitchPivot");
                pitchObj.transform.SetParent(yawPivot);
                pitchObj.transform.localPosition = Vector3.zero;
                pitchObj.transform.localRotation = Quaternion.identity;
                pitchPivot = pitchObj.transform;
                
                if (enableDebugLogs)
                    Debug.Log("[TopDownCameraController3D] Created CameraPitchPivot");
            }
        }

        // Move camera to pitch pivot if not already there
        if (transform.parent != pitchPivot)
        {
            // Store current world position to maintain it
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            
            transform.SetParent(pitchPivot);
            
            // Restore world position/rotation
            transform.position = worldPos;
            transform.rotation = worldRot;
            
            if (enableDebugLogs)
                Debug.Log("[TopDownCameraController3D] Moved camera to pitch pivot");
        }

        // Apply initial pitch
        if (pitchPivot != null)
        {
            if (currentProfile != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(currentProfile.pitchAngle, 0f, 0f);
            }
            else
            {
                pitchPivot.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        // Apply initial local offset
        if (currentProfile != null && transform != null)
        {
            transform.localPosition = currentProfile.followOffset;
        }
    }

    /// <summary>
    /// Pause/resume rig position updates from follow. Used by camera shots when moving rig to a different target.
    /// </summary>
    public void SetPauseFollowPosition(bool pause)
    {
        pauseFollowPosition = pause;
    }

    /// <summary>
    /// Whether follow-position updates are currently paused.
    /// </summary>
    public bool IsFollowPositionPaused => pauseFollowPosition;

    /// <summary>
    /// Set rig root position directly. Use when pauseFollowPosition is true (e.g. during camera shot).
    /// </summary>
    public void SetRigPosition(Vector3 worldPos)
    {
        if (rigRoot != null)
            rigRoot.position = worldPos;
    }

    /// <summary>
    /// Get current rig root position.
    /// </summary>
    public Vector3 GetRigPosition()
    {
        return rigRoot != null ? rigRoot.position : transform.position;
    }

    /// <summary>
    /// Update camera follow position on XZ plane and optionally Y axis.
    /// </summary>
    private void UpdateFollow()
    {
        if (pauseFollowPosition) return;

        Vector3 desiredPosition = target.position;
        
        // Handle Y-axis following (for terrain height)
        bool followY = activeProfile != null && activeProfile.followYAxis;
        if (followY)
        {
            // Calculate desired Y position (target Y + offset)
            float desiredY = target.position.y + (activeProfile != null ? activeProfile.yAxisOffset : 25f);
            
            // Clamp Y position to min/max bounds
            desiredY = Mathf.Clamp(desiredY, 
                activeProfile != null ? activeProfile.minYPosition : -100f,
                activeProfile != null ? activeProfile.maxYPosition : 1000f);
            
            desiredPosition.y = desiredY;
        }
        else
        {
            // Maintain stable height (original behavior)
            desiredPosition.y = rigRoot.position.y;
        }

        // Apply boundaries if enabled (XZ only)
        if (useBoundaries)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minXZ.x, maxXZ.x);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minXZ.y, maxXZ.y);
        }

        // Smooth position - handle XZ and Y separately for different smoothing rates
        Vector3 smoothedPosition = rigRoot.position;
        
        if (activeProfile != null && activeProfile.useSmoothDamp)
        {
            // Smooth XZ with standard smoothing
            Vector3 xzDesired = new Vector3(desiredPosition.x, 0f, desiredPosition.z);
            Vector3 xzCurrent = new Vector3(rigRoot.position.x, 0f, rigRoot.position.z);
            Vector3 xzSmoothed = Vector3.SmoothDamp(xzCurrent, xzDesired, ref velocity, 
                activeProfile.followSmoothTime);
            
            smoothedPosition.x = xzSmoothed.x;
            smoothedPosition.z = xzSmoothed.z;
            
            // Smooth Y separately (can have different smoothing time)
            if (followY)
            {
                smoothedPosition.y = Mathf.SmoothDamp(rigRoot.position.y, desiredPosition.y, 
                    ref yVelocity, activeProfile.yAxisSmoothTime);
            }
            else
            {
                smoothedPosition.y = rigRoot.position.y; // Keep current Y if not following
            }
        }
        else
        {
            // Lerp mode - smooth XZ and Y together
            float speed = activeProfile != null ? activeProfile.followSpeed : 2f;
            smoothedPosition = Vector3.Lerp(rigRoot.position, desiredPosition, speed * Time.deltaTime);
        }

        // Apply pixel snap if enabled (XZ only)
        if (usePixelSnap)
        {
            smoothedPosition = PixelSnapXZ(smoothedPosition);
        }

        rigRoot.position = smoothedPosition;
    }

    /// <summary>
    /// Update zoom based on movement state or manual override.
    /// During profile blend, uses activeProfile.defaultZoom (runtime copy) so LateUpdate reads animated values.
    /// </summary>
    private void UpdateZoom()
    {
        if (activeProfile == null) return;

        // During blend: use profile's defaultZoom (runtime copy is animated by blend coroutine)
        if (isBlendingProfile)
        {
            targetZoom = activeProfile.defaultZoom;
            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, activeProfile.zoomSmoothTime);
            ApplyZoom(currentZoom);
            return;
        }

        // Determine target zoom
        if (useManualMovementMode)
        {
            targetZoom = activeProfile.GetZoomForState(manualMovementMode);
        }
        else if (targetRb != null)
        {
            // Auto-detect movement mode from velocity
            // Unity 6+ uses linearVelocity, older versions use velocity
#if UNITY_6000_0_OR_NEWER
            float speed = targetRb.linearVelocity.magnitude;
#else
            float speed = targetRb.velocity.magnitude;
#endif
            CameraMovementMode mode = DetermineMovementMode(speed);
            targetZoom = activeProfile.GetZoomForState(mode);
        }
        else
        {
            // Fallback to walk zoom
            targetZoom = activeProfile.walkZoom;
        }

        // Smooth zoom transition
        currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, 
            activeProfile.zoomSmoothTime);

        ApplyZoom(currentZoom);
    }

    /// <summary>
    /// Determine movement mode from speed.
    /// </summary>
    private CameraMovementMode DetermineMovementMode(float speed)
    {
        if (speed < activeProfile.idleThreshold)
            return CameraMovementMode.Idle;
        else if (speed >= activeProfile.sprintThreshold)
            return CameraMovementMode.Sprint;
        else
            return CameraMovementMode.Walk;
    }

    /// <summary>
    /// Apply zoom to camera (works for both orthographic and perspective).
    /// </summary>
    private void ApplyZoom(float zoom)
    {
        if (cam == null) return;

        zoom += GetAmbientZoomOffset();
        zoom = Mathf.Clamp(zoom, activeProfile.minZoom, activeProfile.maxZoom);

        if (cam.orthographic)
        {
            cam.orthographicSize = zoom;
        }
        else
        {
            // For perspective mode: zoom affects FOV
            // Lower FOV = more zoomed in (narrower view, closer)
            // Higher FOV = more zoomed out (wider view, farther)
            // 
            // Zoom mapping:
            // - Lower zoom value (idle) = Lower FOV (zoomed in)
            // - Higher zoom value (sprint) = Higher FOV (zoomed out)
            
            // Calculate FOV range based on base FOV (60)
            // Idle (minZoom) should be zoomed IN = lower FOV
            // Sprint (maxZoom) should be zoomed OUT = higher FOV
            float baseFOV = activeProfile.fieldOfView;
            float minFOV = baseFOV * 0.7f; // Idle: 70% of base (zoomed in) = ~42 FOV
            float maxFOV = baseFOV * 1.6f; // Sprint: 160% of base (zoomed out) = ~96 FOV (clamped to 120)
            
            // Normalize zoom to 0-1 range
            // minZoom (idle) = 0, maxZoom (sprint) = 1
            float normalizedZoom = (zoom - activeProfile.minZoom) / (activeProfile.maxZoom - activeProfile.minZoom);
            
            // Map: lower zoom (0) → lower FOV (zoomed in), higher zoom (1) → higher FOV (zoomed out)
            float fov = Mathf.Lerp(minFOV, maxFOV, normalizedZoom);
            cam.fieldOfView = Mathf.Clamp(fov, 30f, 120f);
        }
    }

    /// <summary>
    /// Pixel snap XZ position (retro-style, not true pixel-perfect 3D).
    /// </summary>
    private Vector3 PixelSnapXZ(Vector3 position)
    {
        float pixelSize = 1f / pixelsPerUnit;
        position.x = Mathf.Round(position.x / pixelSize) * pixelSize;
        position.z = Mathf.Round(position.z / pixelSize) * pixelSize;
        return position;
    }

    /// <summary>
    /// Rotate camera yaw by 90° steps (enforces 4 cardinal perspectives).
    /// </summary>
    /// <param name="steps">Number of 90° steps (positive = clockwise, negative = counterclockwise)</param>
    /// <param name="immediate">If true, snap immediately without animation</param>
    public void RotateYaw90(int steps, bool immediate = false, float durationOverride = -1f)
    {
        if (yawPivot == null) return;
        
        // Update yaw index (0-3)
        yawIndex90 = (yawIndex90 + steps) % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        targetYaw = yawIndex90 * 90f;

        if (immediate)
        {
            yawRotationDurationOverride = -1f;
            currentYaw = targetYaw;
            yawPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
        else
        {
            yawRotationDurationOverride = durationOverride;
            // Stop current rotation if in progress
            if (yawRotationCoroutine != null)
            {
                StopCoroutine(yawRotationCoroutine);
            }
            yawRotationCoroutine = StartCoroutine(RotateYawCoroutine());
        }
    }
    
    /// <summary>
    /// Set absolute yaw index (0-3 for cardinal directions).
    /// 0 = default/North, 1 = East (90°), 2 = South (180°), 3 = West (270°)
    /// </summary>
    public void SetYawIndex90(int index, bool immediate = false, float durationOverride = -1f)
    {
        if (yawPivot == null) return;
        
        // Clamp to 0-3
        yawIndex90 = index % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        targetYaw = yawIndex90 * 90f;
        
        if (immediate)
        {
            yawRotationDurationOverride = -1f;
            currentYaw = targetYaw;
            yawPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
        else
        {
            yawRotationDurationOverride = durationOverride;
            if (yawRotationCoroutine != null)
            {
                StopCoroutine(yawRotationCoroutine);
            }
            yawRotationCoroutine = StartCoroutine(RotateYawCoroutine());
        }
    }
    
    /// <summary>
    /// Legacy: Rotate camera yaw by 45° steps (converts to 90° internally).
    /// </summary>
    /// <param name="steps">Number of 45° steps (will be converted to 90° - every 2 steps = 1 cardinal step)</param>
    /// <param name="immediate">If true, snap immediately without animation</param>
    [Obsolete("Use RotateYaw90 for 90° increments. This method converts 45° steps to 90° (2 steps = 1 rotation).")]
    public void RotateYawSteps(int steps, bool immediate = false)
    {
        // Convert 45° steps to 90° steps (round to nearest)
        int steps90 = Mathf.RoundToInt(steps / 2f);
        if (steps90 == 0 && steps != 0)
        {
            steps90 = steps > 0 ? 1 : -1; // Minimum 1 step
        }
        RotateYaw90(steps90, immediate);
    }

    /// <summary>
    /// Smooth yaw rotation coroutine.
    /// </summary>
    private IEnumerator RotateYawCoroutine()
    {
        isRotatingYaw = true;
        OnYawRotationStarted?.Invoke();
        
        float duration = yawRotationDurationOverride > 0f
            ? yawRotationDurationOverride
            : activeProfile != null ? activeProfile.yawRotateTime : 0.2f;
        float elapsed = 0f;
        float startYaw = currentYaw;

        // Handle shortest rotation path
        float delta = Mathf.DeltaAngle(startYaw, targetYaw);
        float endYaw = startYaw + delta;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Use smooth step for nicer easing
            t = t * t * (3f - 2f * t);
            currentYaw = Mathf.LerpAngle(startYaw, endYaw, t);
            yawPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            yield return null;
        }

        // Snap to exact 90° increment to avoid drift
        currentYaw = yawIndex90 * 90f;
        targetYaw = currentYaw;
        yawPivot.localRotation = Quaternion.Euler(0f, currentYaw, 0f);

        isRotatingYaw = false;
        yawRotationCoroutine = null;
        yawRotationDurationOverride = -1f;
        
        OnYawRotationComplete?.Invoke();
    }
    
    #region Pitch Override
    
    /// <summary>
    /// Set pitch target with optional blend time.
    /// This overrides the profile pitch until ClearPitchOverride is called.
    /// </summary>
    public void SetPitchTarget(float pitch, float blendTime = 0.3f)
    {
        targetPitch = pitch;
        hasPitchOverride = true;
        
        if (pitchTransitionCoroutine != null)
        {
            StopCoroutine(pitchTransitionCoroutine);
        }
        
        if (blendTime <= 0f)
        {
            currentPitch = targetPitch;
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }
        }
        else
        {
            pitchTransitionCoroutine = StartCoroutine(TransitionPitchCoroutine(blendTime));
        }
    }
    
    /// <summary>
    /// Clear pitch override and restore profile pitch.
    /// </summary>
    public void ClearPitchOverride(float blendTime = 0.3f)
    {
        if (!hasPitchOverride) return;
        
        hasPitchOverride = false;
        targetPitch = activeProfile != null ? activeProfile.pitchAngle : 45f;
        
        if (pitchTransitionCoroutine != null)
        {
            StopCoroutine(pitchTransitionCoroutine);
        }
        
        if (blendTime <= 0f)
        {
            currentPitch = targetPitch;
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }
        }
        else
        {
            pitchTransitionCoroutine = StartCoroutine(TransitionPitchCoroutine(blendTime));
        }
    }
    
    private IEnumerator TransitionPitchCoroutine(float duration)
    {
        OnPitchOverrideStarted?.Invoke();
        
        float startPitch = currentPitch;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // Smooth step
            currentPitch = Mathf.Lerp(startPitch, targetPitch, t);
            
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }
            
            yield return null;
        }
        
        currentPitch = targetPitch;
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }
        
        pitchTransitionCoroutine = null;
        OnPitchOverrideComplete?.Invoke();
    }
    
    #endregion
    
    #region State Snapshot
    
    /// <summary>
    /// Capture current camera state as a snapshot.
    /// </summary>
    public CameraStateSnapshot GetStateSnapshot()
    {
        CameraProfile authored = ResolveAuthoredSnapshotProfile();
        float snapshotPitch = hasPitchOverride
            ? currentPitch
            : (authored != null ? authored.pitchAngle : (activeProfile != null ? activeProfile.pitchAngle : 45f));
        return CameraStateSnapshot.Create(
            authored,
            targetZoom,
            yawIndex90,
            snapshotPitch,
            hasPitchOverride
        );
    }

    /// <summary>
    /// Authored profile for zone restore snapshots. Skips runtime blend/intro clones
    /// so nested occupancy cannot persist a temporary copy as the overworld default.
    /// </summary>
    public CameraProfile ResolveAuthoredSnapshotProfile()
    {
        if (IsAuthoredSnapshotProfile(currentProfile))
            return currentProfile;
        if (IsAuthoredSnapshotProfile(defaultProfile))
            return defaultProfile;
        return currentProfile != null ? currentProfile : defaultProfile;
    }

    private bool IsAuthoredSnapshotProfile(CameraProfile profile)
    {
        if (profile == null)
            return false;
        if (ReferenceEquals(profile, _runtimeIntroProfile) ||
            ReferenceEquals(profile, _runtimeBattlefieldFramingProfile))
            return false;

        string profileName = profile.name;
        return !profileName.EndsWith(" (Runtime)") &&
               !profileName.EndsWith(" (IntroBeat)") &&
               !profileName.EndsWith(" (TOD Runtime)") &&
               !profileName.EndsWith(" (BattlefieldFraming)");
    }
    
    /// <summary>
    /// Restore camera state from a snapshot.
    /// </summary>
    public void RestoreStateSnapshot(CameraStateSnapshot snapshot, float blendTime = 0.5f)
    {
        if (!snapshot.isValid)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot restore invalid snapshot!");
            return;
        }

        // Restore profile
        if (snapshot.profile != null)
        {
            ApplyProfile(snapshot.profile, blendTime);
        }

        // Restore zoom
        targetZoom = snapshot.zoomTarget;

        // Restore yaw
        SetYawIndex90(snapshot.yawIndex90, blendTime <= 0f);

        // Restore pitch
        if (snapshot.hasPitchOverride)
        {
            SetPitchTarget(snapshot.pitchAngle, blendTime);
        }
        else
        {
            ClearPitchOverride(blendTime);
        }
    }

    /// <summary>
    /// Restore state from snapshot and yield until blend completes. Use in coroutines.
    /// </summary>
    public IEnumerator RestoreStateSnapshotAndWait(CameraStateSnapshot snapshot, float blendTime = 0.3f)
    {
        if (!snapshot.isValid)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot restore invalid snapshot!");
            yield break;
        }

        if (snapshot.profile != null)
        {
            yield return StartCoroutine(ApplyProfileAndWait(snapshot.profile, blendTime));
        }

        targetZoom = snapshot.zoomTarget;
        SetYawIndex90(snapshot.yawIndex90, blendTime <= 0f);
        if (snapshot.hasPitchOverride)
        {
            SetPitchTarget(snapshot.pitchAngle, blendTime);
        }
        else
        {
            ClearPitchOverride(blendTime);
        }

        // Let pitch/yaw transitions settle
        yield return new WaitForSeconds(Mathf.Max(0.05f, blendTime * 0.5f));
    }

    /// <inheritdoc />
    public object CaptureIntroFraming()
    {
        CameraProfile restoreProfile = ResolveAuthoredIntroSourceProfile();
        if (restoreProfile == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] CaptureIntroFraming — no authored camera profile.");
            return new IntroFramingToken { Snapshot = CameraStateSnapshot.Invalid };
        }

        // Capture BattleProfile (authored), not whatever transform/runtime clone is live.
        return new IntroFramingToken
        {
            Snapshot = CameraStateSnapshot.Create(
                restoreProfile,
                restoreProfile.defaultZoom,
                yawIndex90,
                restoreProfile.pitchAngle,
                hasPitchOverride: false),
        };
    }

    /// <inheritdoc />
    public IEnumerator BlendToIntroFraming(
        float followOffsetYDelta,
        float followOffsetZ,
        float pitchAngle,
        float blendSeconds)
    {
        CameraProfile source = ResolveAuthoredIntroSourceProfile();
        if (source == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot blend intro framing — no authored camera profile.");
            yield break;
        }

        DestroyRuntimeIntroProfile();

        // Runtime clone only — never mutate BattleProfile / authored assets.
        CameraProfile introProfile = Instantiate(source);
        introProfile.name = source.name + " (IntroBeat)";
        Vector3 offset = introProfile.followOffset;
        offset.y = source.followOffset.y + followOffsetYDelta;
        offset.z = followOffsetZ;
        introProfile.followOffset = offset;
        introProfile.pitchAngle = pitchAngle;
        _runtimeIntroProfile = introProfile;

        ClearPitchOverride(0f);
        yield return StartCoroutine(ApplyProfileAndWait(introProfile, Mathf.Max(0f, blendSeconds)));

        // Keep authored profile pointer; LateUpdate still reads activeProfile (intro clone).
        currentProfile = source;

        // Instant apply only: kill follow SmoothDamp lag. Gradual blend-in should ease naturally.
        if (blendSeconds <= 0f)
            SnapToTarget();
    }

    private CameraProfile ResolveAuthoredIntroSourceProfile()
    {
        if (currentProfile != null && !ReferenceEquals(currentProfile, _runtimeIntroProfile))
            return currentProfile;
        if (defaultProfile != null && !ReferenceEquals(defaultProfile, _runtimeIntroProfile))
            return defaultProfile;
        if (activeProfile != null && !ReferenceEquals(activeProfile, _runtimeIntroProfile))
            return activeProfile;
        return null;
    }

    /// <inheritdoc />
    public IEnumerator RestoreIntroFraming(object snapshotToken, float blendSeconds)
    {
        if (!(snapshotToken is IntroFramingToken token) || !token.Snapshot.isValid)
        {
            Debug.LogWarning("[TopDownCameraController3D] Intro framing restore skipped — invalid snapshot token.");
            yield break;
        }

        ClearPitchOverride(0f);
        DestroyRuntimeBattlefieldFramingProfile();
        yield return StartCoroutine(RestoreStateSnapshotAndWait(token.Snapshot, Mathf.Max(0f, blendSeconds)));
        DestroyRuntimeIntroProfile();
    }

    /// <summary>
    /// Soft-blend only followOffset.y/z on a runtime clone of the authored battle profile.
    /// This deliberately leaves target, pitch, zoom, and shake state untouched.
    /// </summary>
    public IEnumerator SoftBlendBattlefieldFollowOffset(
        float followOffsetYDelta,
        float followOffsetZ,
        float blendSeconds)
    {
        if (_runtimeIntroProfile != null)
            yield break;

        CameraProfile source = ResolveAuthoredBattlefieldSourceProfile();
        if (source == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot blend battlefield framing — no authored camera profile.");
            yield break;
        }

        if (_runtimeBattlefieldFramingProfile == null || !ReferenceEquals(currentProfile, source))
        {
            DestroyRuntimeBattlefieldFramingProfile();
            _runtimeBattlefieldFramingProfile = Instantiate(source);
            _runtimeBattlefieldFramingProfile.name = source.name + " (BattlefieldFraming)";
            activeProfile = _runtimeBattlefieldFramingProfile;
        }

        Vector3 startOffset = activeProfile.followOffset;
        Vector3 targetOffset = source.followOffset;
        targetOffset.y += followOffsetYDelta;
        if (!float.IsNaN(followOffsetZ))
            targetOffset.z = followOffsetZ;

        float duration = Mathf.Max(0f, blendSeconds);
        if (duration <= 0f)
        {
            activeProfile.followOffset = targetOffset;
            currentProfile = source;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            Vector3 offset = startOffset;
            offset.y = Mathf.Lerp(startOffset.y, targetOffset.y, t);
            offset.z = Mathf.Lerp(startOffset.z, targetOffset.z, t);
            activeProfile.followOffset = offset;
            yield return null;
        }

        activeProfile.followOffset = targetOffset;
        currentProfile = source;
    }

    private CameraProfile ResolveAuthoredBattlefieldSourceProfile()
    {
        if (currentProfile != null &&
            !ReferenceEquals(currentProfile, _runtimeIntroProfile) &&
            !ReferenceEquals(currentProfile, _runtimeBattlefieldFramingProfile))
            return currentProfile;
        if (defaultProfile != null &&
            !ReferenceEquals(defaultProfile, _runtimeIntroProfile) &&
            !ReferenceEquals(defaultProfile, _runtimeBattlefieldFramingProfile))
            return defaultProfile;
        return null;
    }

    private void DestroyRuntimeBattlefieldFramingProfile()
    {
        if (_runtimeBattlefieldFramingProfile == null)
            return;

        if (ReferenceEquals(activeProfile, _runtimeBattlefieldFramingProfile))
            activeProfile = currentProfile != null ? currentProfile : defaultProfile;

        Destroy(_runtimeBattlefieldFramingProfile);
        _runtimeBattlefieldFramingProfile = null;
    }

    private void DestroyRuntimeIntroProfile()
    {
        if (_runtimeIntroProfile == null)
            return;

        CameraProfile fallback = defaultProfile;
        if (currentProfile != null && !ReferenceEquals(currentProfile, _runtimeIntroProfile))
            fallback = currentProfile;

        if (ReferenceEquals(activeProfile, _runtimeIntroProfile))
            activeProfile = fallback;
        if (ReferenceEquals(currentProfile, _runtimeIntroProfile))
            currentProfile = fallback;

        Destroy(_runtimeIntroProfile);
        _runtimeIntroProfile = null;
    }
    
    #endregion
    
    #region Camera-Relative Movement Support
    
    /// <summary>
    /// Get camera forward direction on XZ plane (for camera-relative movement).
    /// </summary>
    public Vector3 GetCameraForwardXZ()
    {
        if (yawPivot == null) return Vector3.forward;
        
        Vector3 forward = yawPivot.forward;
        forward.y = 0f;
        return forward.normalized;
    }
    
    /// <summary>
    /// Get camera right direction on XZ plane (for camera-relative movement).
    /// </summary>
    public Vector3 GetCameraRightXZ()
    {
        if (yawPivot == null) return Vector3.right;
        
        Vector3 right = yawPivot.right;
        right.y = 0f;
        return right.normalized;
    }
    
    /// <summary>
    /// Convert input direction to world direction based on camera yaw.
    /// </summary>
    /// <param name="input">Input vector (X = horizontal, Y = vertical/forward)</param>
    /// <returns>World-space direction on XZ plane</returns>
    public Vector3 InputToWorldDirection(Vector2 input)
    {
        Vector3 forward = GetCameraForwardXZ();
        Vector3 right = GetCameraRightXZ();
        
        return (right * input.x + forward * input.y).normalized;
    }
    
    #endregion

    /// <summary>
    /// Apply camera profile with optional blending.
    /// </summary>
    public void ApplyProfile(CameraProfile profile, float blendTime = 0f)
    {
        if (profile == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot apply null profile!");
            return;
        }

        if (blendTime <= 0f)
        {
            if (blendCoroutine != null)
            {
                StopCoroutine(blendCoroutine);
                blendCoroutine = null;
                isBlendingProfile = false;
            }
            ApplyProfileImmediate(profile);
        }
        else
        {
            if (blendCoroutine != null)
                StopCoroutine(blendCoroutine);
            blendCoroutine = StartCoroutine(BlendProfileCoroutine(profile, blendTime));
        }
    }

    /// <summary>
    /// Apply profile immediately without blending.
    /// </summary>
    private void ApplyProfileImmediate(CameraProfile profile)
    {
        activeProfile = profile;
        currentProfile = profile;

        // Apply projection
        if (cam != null)
        {
            cam.orthographic = profile.isOrthographic;
            
            // Set FOV for perspective mode (Cassette Beasts style)
            if (!profile.isOrthographic)
            {
                cam.fieldOfView = profile.fieldOfView;
            }
            
            // Set clipping planes to prevent near-side clipping
            cam.nearClipPlane = profile.nearClipPlane;
            cam.farClipPlane = profile.farClipPlane;
        }

        // Apply pitch
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(profile.pitchAngle, 0f, 0f);
        }

        // Apply offset (shake will be added in LateUpdate)
        if (transform != null)
        {
            transform.localPosition = profile.followOffset;
        }

        // Apply zoom
        targetZoom = profile.defaultZoom;
        currentZoom = targetZoom;
        ApplyZoom(currentZoom);
    }

    /// <summary>
    /// Blend to a profile using a runtime copy. Never modifies the original ScriptableObject.
    /// Animates a runtime profile's values; LateUpdate (ApplyCameraShake, UpdateZoom) reads from it - no conflict.
    /// </summary>
    /// <param name="targetProfile">Template profile (original SO). Never modified. Used only as source for Instantiate and target values.</param>
    private IEnumerator BlendProfileCoroutine(CameraProfile targetProfile, float blendTime)
    {
        if (targetProfile == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot blend to null profile!");
            yield break;
        }

        // GUARDRAIL: Create runtime copy - never modify the original ScriptableObject asset.
        // Only the runtime copy is animated; original stays pristine. Use Tools > Chainbound > Camera > Profiles
        // to validate/reset profiles against a template.
        CameraProfile runtimeProfile = Instantiate(targetProfile);
        runtimeProfile.name = targetProfile.name + " (Runtime)";
        Debug.Assert(runtimeProfile != targetProfile, "[TopDownCameraController3D] Guardrail: runtime copy must differ from template.");

        CameraProfile oldProfile = activeProfile;
        isBlendingProfile = true;

        // Store old values (from current state for pitch/zoom, from oldProfile for others)
        float oldPitch = pitchPivot != null ? pitchPivot.localEulerAngles.x : (oldProfile != null ? oldProfile.pitchAngle : 45f);
        if (oldPitch > 180f) oldPitch -= 360f;
        float oldZoom = currentZoom;
        Vector3 oldOffset = oldProfile != null ? oldProfile.followOffset : Vector3.zero;
        float oldFOV = oldProfile != null ? oldProfile.fieldOfView : 60f;
        bool oldOrtho = oldProfile != null ? oldProfile.isOrthographic : false;
        float oldIdle = oldProfile != null ? oldProfile.idleZoom : oldZoom;
        float oldWalk = oldProfile != null ? oldProfile.walkZoom : oldZoom;
        float oldSprint = oldProfile != null ? oldProfile.sprintZoom : oldZoom;
        Vector3 oldAmbientBobAmplitude = oldProfile != null ? oldProfile.ambientBobAmplitude : Vector3.zero;
        float oldAmbientBobFrequency = oldProfile != null ? oldProfile.ambientBobFrequency : 0f;
        float oldAmbientZoomAmplitude = oldProfile != null ? oldProfile.ambientZoomAmplitude : 0f;
        float oldAmbientZoomFrequency = oldProfile != null ? oldProfile.ambientZoomFrequency : 0f;

        // Keep projection mode during blend (ortho vs perspective) - switch only at end
        runtimeProfile.isOrthographic = oldOrtho;

        // Switch to runtime copy immediately - LateUpdate will read from it
        ApplyProfileImmediate(runtimeProfile);

        if (enableDebugLogs)
            Debug.Log($"[Blend START] old={oldProfile?.name ?? "null"} -> {targetProfile.name} (runtime copy) blendTime={blendTime:F2}s");

        float elapsed = 0f;
        while (elapsed < blendTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / blendTime);
            float tSmooth = t * t * (3f - 2f * t);

            // Animate runtime profile - LateUpdate reads these values, no overwrite conflict
            runtimeProfile.pitchAngle = Mathf.Lerp(oldPitch, targetProfile.pitchAngle, tSmooth);
            runtimeProfile.followOffset = Vector3.Lerp(oldOffset, targetProfile.followOffset, tSmooth);
            runtimeProfile.defaultZoom = Mathf.Lerp(oldZoom, targetProfile.defaultZoom, tSmooth);
            runtimeProfile.idleZoom = Mathf.Lerp(oldIdle, targetProfile.idleZoom, tSmooth);
            runtimeProfile.walkZoom = Mathf.Lerp(oldWalk, targetProfile.walkZoom, tSmooth);
            runtimeProfile.sprintZoom = Mathf.Lerp(oldSprint, targetProfile.sprintZoom, tSmooth);
            runtimeProfile.ambientBobAmplitude = Vector3.Lerp(oldAmbientBobAmplitude, targetProfile.ambientBobAmplitude, tSmooth);
            runtimeProfile.ambientBobFrequency = Mathf.Lerp(oldAmbientBobFrequency, targetProfile.ambientBobFrequency, tSmooth);
            runtimeProfile.ambientZoomAmplitude = Mathf.Lerp(oldAmbientZoomAmplitude, targetProfile.ambientZoomAmplitude, tSmooth);
            runtimeProfile.ambientZoomFrequency = Mathf.Lerp(oldAmbientZoomFrequency, targetProfile.ambientZoomFrequency, tSmooth);
            runtimeProfile.fieldOfView = Mathf.Lerp(oldFOV, targetProfile.fieldOfView, tSmooth);
            runtimeProfile.nearClipPlane = Mathf.Lerp(oldProfile != null ? oldProfile.nearClipPlane : 0.01f, targetProfile.nearClipPlane, tSmooth);
            runtimeProfile.farClipPlane = Mathf.Lerp(oldProfile != null ? oldProfile.farClipPlane : 1000f, targetProfile.farClipPlane, tSmooth);

            // Projection: keep old until end
            if (cam != null)
            {
                cam.orthographic = oldOrtho;
            }

            yield return null;
        }

        // Switch to original asset - same values, no visual snap. Keeps assets in sync.
        ApplyProfileImmediate(targetProfile);

        // Clean up runtime copy
        Destroy(runtimeProfile);

        if (enableDebugLogs)
            Debug.Log($"[Blend END] Switched to original {targetProfile.name}");

        isBlendingProfile = false;
        blendCoroutine = null;
    }

    /// <summary>
    /// Reset to default profile.
    /// </summary>
    public void ResetToDefaultProfile(float blendTime = 0f)
    {
        if (defaultProfile != null)
        {
            ApplyProfile(defaultProfile, blendTime);
        }
    }

    /// <summary>
    /// Apply profile and yield until blend completes. Use in coroutines when you need to wait.
    /// </summary>
    public IEnumerator ApplyProfileAndWait(CameraProfile profile, float blendTime = 0f)
    {
        if (profile == null)
        {
            Debug.LogWarning("[TopDownCameraController3D] Cannot apply null profile!");
            yield break;
        }

        if (blendTime <= 0f)
        {
            if (blendCoroutine != null)
            {
                StopCoroutine(blendCoroutine);
                blendCoroutine = null;
                isBlendingProfile = false;
            }
            ApplyProfileImmediate(profile);
            yield break;
        }

        if (blendCoroutine != null)
            StopCoroutine(blendCoroutine);
        blendCoroutine = StartCoroutine(BlendProfileCoroutine(profile, blendTime));
        yield return blendCoroutine;
        blendCoroutine = null;
    }

    /// <summary>
    /// Reset to default profile and yield until blend completes.
    /// </summary>
    public IEnumerator ResetToDefaultProfileAndWait(float blendTime = 0.3f)
    {
        if (defaultProfile == null)
        {
            yield break;
        }

        yield return StartCoroutine(ApplyProfileAndWait(defaultProfile, blendTime));
    }

    /// <summary>
    /// Set movement mode manually (overrides velocity-based detection).
    /// </summary>
    public void SetMovementMode(CameraMovementMode mode)
    {
        useManualMovementMode = true;
        manualMovementMode = mode;
    }

    /// <summary>
    /// Clear manual movement mode (re-enable velocity-based detection).
    /// </summary>
    public void ClearMovementMode()
    {
        useManualMovementMode = false;
    }

    /// <summary>
    /// Set zoom target manually.
    /// </summary>
    public void SetZoomTarget(float zoom)
    {
        targetZoom = zoom;
    }

    /// <summary>
    /// Get current zoom level.
    /// </summary>
    public float CurrentZoom => currentZoom;
    
    /// <summary>
    /// Get target zoom level.
    /// </summary>
    public float TargetZoom => targetZoom;

    /// <summary>
    /// Get current camera profile.
    /// </summary>
    public CameraProfile CurrentProfile => currentProfile;

    /// <summary>
    /// Profile authored on the camera prefab. Used outside every camera zone,
    /// and inside a zone that has no profile of its own.
    /// </summary>
    public CameraProfile DefaultProfile => defaultProfile;
    
    /// <summary>
    /// Get active camera profile (may differ during blending).
    /// </summary>
    public CameraProfile ActiveProfile => activeProfile;
    
    /// <summary>
    /// Get current yaw index (0-3 for cardinal directions).
    /// </summary>
    public int YawIndex90 => yawIndex90;

    /// <summary>
    /// Authored yaw index before zone/combat offsets. Relative camera zones
    /// apply as an offset from this value so re-entry cannot stack.
    /// </summary>
    public int DefaultYawIndex90 => defaultYawIndex90;
    
    /// <summary>
    /// Get current yaw angle in degrees.
    /// </summary>
    public float CurrentYaw => currentYaw;
    
    /// <summary>
    /// Check if camera is currently rotating.
    /// </summary>
    public bool IsRotatingYaw => isRotatingYaw;
    
    /// <summary>
    /// Get current pitch angle from override/transition state.
    /// </summary>
    public float CurrentPitch => currentPitch;

    /// <summary>
    /// Get live pitch from the pitch pivot (profile-blend safe).
    /// Prefer this for visual compensation that must track animated profile blends.
    /// </summary>
    public float GetEffectivePitch()
    {
        if (pitchPivot != null)
        {
            float pitch = pitchPivot.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;

            return pitch;
        }

        if (activeProfile != null)
            return activeProfile.pitchAngle;

        return ExplorationVisualPitchYOffset.DefaultReferencePitch;
    }

    /// <summary>
    /// Elevation angle (degrees above horizontal) from subject to the live camera position.
    /// Matches boom height toward a world point; billboard tilt uses <see cref="GetLookElevationDegrees"/>.
    /// </summary>
    public float GetViewPitchToward(Vector3 subjectWorldPosition)
    {
        Transform cameraTransform = cam != null ? cam.transform : transform;
        Vector3 delta = cameraTransform.position - subjectWorldPosition;
        float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;

        if (horizontalDistance < 0.001f)
            return delta.y > 0f ? 90f : 0f;

        return Mathf.Atan2(delta.y, horizontalDistance) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Degrees the live camera looks below the horizon, from camera forward.
    /// Matches FaceCameraForward billboard tilt during profile blends and look-at.
    /// </summary>
    public float GetLookElevationDegrees()
    {
        Transform cameraTransform = cam != null ? cam.transform : transform;
        Vector3 forward = cameraTransform.forward;
        float horizontal = new Vector2(forward.x, forward.z).magnitude;

        if (horizontal < 0.001f)
            return forward.y < 0f ? 90f : 0f;

        return Mathf.Atan2(-forward.y, horizontal) * Mathf.Rad2Deg;
    }
    
    /// <summary>
    /// Check if pitch is currently overridden.
    /// </summary>
    public bool HasPitchOverride => hasPitchOverride;
    
    /// <summary>
    /// Get the yaw rotation duration from active profile.
    /// </summary>
    public float YawRotationDuration => activeProfile != null ? activeProfile.yawRotateTime : 0.2f;

    /// <summary>
    /// Set zoom level.
    /// </summary>
    public void SetZoom(float zoom)
    {
        targetZoom = zoom;
        currentZoom = zoom;
        ApplyZoom(zoom);
    }

    /// <summary>
    /// Adjust zoom by delta amount.
    /// </summary>
    public void AdjustZoom(float deltaZoom)
    {
        SetZoom(targetZoom + deltaZoom);
    }

    /// <summary>
    /// Snap camera to target instantly (useful for scene transitions).
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null || rigRoot == null) return;

        Vector3 snapPosition = target.position;
        
        // Handle Y-axis following
        bool followY = activeProfile != null && activeProfile.followYAxis;
        if (followY)
        {
            float desiredY = target.position.y + (activeProfile != null ? activeProfile.yAxisOffset : 25f);
            desiredY = Mathf.Clamp(desiredY,
                activeProfile != null ? activeProfile.minYPosition : -100f,
                activeProfile != null ? activeProfile.maxYPosition : 1000f);
            snapPosition.y = desiredY;
        }
        else
        {
            snapPosition.y = rigRoot.position.y;
        }

        if (useBoundaries)
        {
            snapPosition.x = Mathf.Clamp(snapPosition.x, minXZ.x, maxXZ.x);
            snapPosition.z = Mathf.Clamp(snapPosition.z, minXZ.y, maxXZ.y);
        }

        if (usePixelSnap)
        {
            snapPosition = PixelSnapXZ(snapPosition);
        }

        rigRoot.position = snapPosition;
        velocity = Vector3.zero;
        yVelocity = 0f; // Reset Y velocity too
    }

    /// <summary>
    /// Set target transform.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRb = target != null ? target.GetComponent<Rigidbody>() : null;
    }

    /// <summary>
    /// Apply aspect ratio enforcement (letterboxing for consistent framing).
    /// </summary>
    private void ApplyAspectRatio()
    {
        if (cam == null) return;

        // Only apply if screen is valid
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            cam.rect = new Rect(0, 0, 1, 1);
            return;
        }

        float currentAspect = (float)Screen.width / Screen.height;
        float targetAspect = targetAspectRatio;

        if (Mathf.Approximately(currentAspect, targetAspect))
        {
            // Already correct aspect ratio
            cam.rect = new Rect(0, 0, 1, 1);
            return;
        }

        Rect newRect;

        if (currentAspect > targetAspect)
        {
            // Screen is wider than target - letterbox sides
            float scaleHeight = currentAspect / targetAspect;
            float normalizedWidth = 1.0f / scaleHeight;
            float barThickness = (1.0f - normalizedWidth) / 2.0f;
            newRect = new Rect(barThickness, 0, normalizedWidth, 1);
        }
        else
        {
            // Screen is taller than target - letterbox top/bottom
            float scaleWidth = targetAspect / currentAspect;
            float normalizedHeight = 1.0f / scaleWidth;
            float barThickness = (1.0f - normalizedHeight) / 2.0f;
            newRect = new Rect(0, barThickness, 1, normalizedHeight);
        }

        // Validate rect before applying (prevent out-of-bounds errors)
        if (newRect.x >= 0 && newRect.y >= 0 && 
            newRect.x + newRect.width <= 1 && 
            newRect.y + newRect.height <= 1 &&
            newRect.width > 0 && newRect.height > 0)
        {
            cam.rect = newRect;
        }
        else
        {
            // Fallback to full screen if calculation is invalid
            Debug.LogWarning($"[TopDownCameraController3D] Invalid camera rect calculated: {newRect}. Using full screen.");
            cam.rect = new Rect(0, 0, 1, 1);
        }
    }
    
    #region Camera Shake
    
    /// <summary>
    /// Apply camera shake with configurable intensity and duration.
    /// Shake is applied as an offset to the camera's local position.
    /// </summary>
    /// <param name="intensity">Shake intensity (how far the camera moves)</param>
    /// <param name="duration">Shake duration in seconds</param>
    /// <param name="frequency">Shake frequency (how many shakes per second)</param>
    public void ShakeCamera(float intensity, float duration, float frequency = 20f)
    {
        if (isShaking && shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        
        shakeCoroutine = StartCoroutine(ShakeCameraCoroutine(intensity, duration, frequency));
    }
    
    /// <summary>
    /// Apply camera shake with dynamic intensity based on fall height.
    /// </summary>
    /// <param name="fallHeight">Height the player fell from</param>
    /// <param name="minShakeHeight">Minimum fall height to trigger shake</param>
    /// <param name="maxShakeHeight">Fall height for maximum shake intensity</param>
    /// <param name="minIntensity">Minimum shake intensity (at minShakeHeight)</param>
    /// <param name="maxIntensity">Maximum shake intensity (at maxShakeHeight)</param>
    /// <param name="minDuration">Minimum shake duration (at minShakeHeight)</param>
    /// <param name="maxDuration">Maximum shake duration (at maxShakeHeight)</param>
    /// <param name="frequency">Shake frequency (how many shakes per second)</param>
    public void ShakeCameraFromFallHeight(float fallHeight, float minShakeHeight, float maxShakeHeight, 
        float minIntensity, float maxIntensity, float minDuration, float maxDuration, float frequency = 20f)
    {
        if (fallHeight < minShakeHeight)
            return; // No shake for small falls
        
        // Calculate dynamic intensity and duration based on fall height
        float normalizedHeight = Mathf.Clamp01((fallHeight - minShakeHeight) / (maxShakeHeight - minShakeHeight));
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, normalizedHeight);
        float duration = Mathf.Lerp(minDuration, maxDuration, normalizedHeight);
        
        ShakeCamera(intensity, duration, frequency);
    }
    
    /// <summary>
    /// Stop camera shake immediately.
    /// </summary>
    public void StopCameraShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        
        shakeOffset = Vector3.zero;
        isShaking = false;
        
        // Restore camera local position
        if (transform != null && activeProfile != null)
        {
            transform.localPosition = activeProfile.followOffset;
        }
    }
    
    /// <summary>
    /// Check if camera is currently shaking.
    /// </summary>
    public bool IsShaking => isShaking;
    
    /// <summary>
    /// Apply profile offset and pitch from activeProfile.
    /// During blend, activeProfile is a runtime copy with animated values - no conflict with blend coroutine.
    /// Called from LateUpdate to ensure it's applied after all other camera updates.
    /// </summary>
    private void ApplyCameraShake()
    {
        if (activeProfile == null) return;

        // During blend: apply pitch from animated runtime profile (avoids LateUpdate overwriting blend)
        if (isBlendingProfile && pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(activeProfile.pitchAngle, 0f, 0f);
        }

        if (transform != null)
        {
            transform.localPosition = activeProfile.followOffset + GetAmbientLocalOffset() + shakeOffset;
        }
    }

    private Vector3 GetAmbientLocalOffset()
    {
        if (activeProfile == null)
            return Vector3.zero;

        if (activeProfile.ambientMotionOnlyWhenFollowPaused && !pauseFollowPosition)
            return Vector3.zero;

        if (activeProfile.ambientBobAmplitude.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        float bobPhase = Time.time * Mathf.Max(0f, activeProfile.ambientBobFrequency) * Mathf.PI * 2f;
        return activeProfile.ambientBobAmplitude * Mathf.Sin(bobPhase);
    }

    private float GetAmbientZoomOffset()
    {
        if (activeProfile == null)
            return 0f;

        if (activeProfile.ambientMotionOnlyWhenFollowPaused && !pauseFollowPosition)
            return 0f;

        if (Mathf.Approximately(activeProfile.ambientZoomAmplitude, 0f))
            return 0f;

        float zoomPhase = Time.time * Mathf.Max(0f, activeProfile.ambientZoomFrequency) * Mathf.PI * 2f;
        return activeProfile.ambientZoomAmplitude * Mathf.Sin(zoomPhase);
    }
    
    /// <summary>
    /// Camera shake coroutine - generates random offset per frame.
    /// </summary>
    private IEnumerator ShakeCameraCoroutine(float intensity, float duration, float frequency)
    {
        isShaking = true;
        float elapsed = 0f;
        float interval = 1f / frequency;
        float nextShakeTime = 0f;
        
        Vector3 originalOffset = activeProfile != null ? activeProfile.followOffset : Vector3.zero;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Generate new shake offset at specified frequency
            if (elapsed >= nextShakeTime)
            {
                // Random offset in XZ plane (top-down camera)
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(0f, intensity);
                
                shakeOffset = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f, // No Y shake for top-down
                    Mathf.Sin(angle) * distance
                );
                
                nextShakeTime = elapsed + interval;
            }
            
            // Decay intensity over time (smooth fade-out)
            float decay = 1f - (elapsed / duration);
            shakeOffset *= decay;
            
            yield return null;
        }
        
        // Fade out smoothly
        float fadeTime = 0.1f;
        float fadeElapsed = 0f;
        Vector3 startOffset = shakeOffset;
        
        while (fadeElapsed < fadeTime)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / fadeTime;
            shakeOffset = Vector3.Lerp(startOffset, Vector3.zero, t);
            yield return null;
        }
        
        shakeOffset = Vector3.zero;
        isShaking = false;
        shakeCoroutine = null;
    }
    
    #endregion
}

