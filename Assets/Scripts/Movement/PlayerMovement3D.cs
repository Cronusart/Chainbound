using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using SignalJam.UI.MobileControls;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 3D top-down movement controller using Rigidbody on XZ plane.
/// Supports subtle hop/jump for clearing small obstacles while maintaining top-down feel.
/// Preserves legacy behavior: no diagonal speed boost, Input System + MobileInputManager support.
/// 
/// === JUMP MOMENTUM SYSTEM (v2.0) ===
/// Jump preserves takeoff momentum with squat-hold decay:
/// - On jump press: Captures current planar velocity (preJumpPlanarVel)
/// - During squat: Momentum decays via squatMomentumDecayCurve over squatMomentumDecayDuration
/// - On jump release: Takeoff velocity = captured * decayMultiplier * directional modifiers
/// - Quick release = sprint-momentum jump; Long hold = more vertical jump
/// 
/// === AIR STEERING SYSTEM (replaces old airControlPercent speed scaling) ===
/// Old approach: effectiveSpeed *= airControlPercent (reduced air movement speed)
/// New approach: Influence-based steering with momentum preservation
/// - Takeoff momentum is preserved (no speed cap)
/// - Air steering uses acceleration-limited approach toward desired direction
/// - Turn rate capped to prevent instant 180° reversals (maxAirTurnRateDegPerSec)
/// - Anti-reversal penalty when input opposes momentum (oppositionThreshold + backpedalPenaltyMul)
/// - Brief momentum lock after takeoff (momentumLockTimeAfterTakeoff)
/// 
/// === VERTICAL VS DIRECTIONAL JUMP ===
/// - No input at takeoff: Jump is more vertical (verticalJumpCarryMul reduces momentum)
/// - Input held at takeoff: Biases toward input direction (takeoffDirectionalBias)
/// 
/// === GROUNDED VS AIRBORNE MOVEMENT ===
/// - Grounded: Uses deterministic MovePosition for precise control
/// - Airborne: Velocity-driven with ApplyAirSteering() for smooth trajectory shaping
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float sprintSpeedMultiplier = 1.8f;
    
    [Header("Input Detection")]
    [Tooltip("Minimum input magnitude to be considered 'moving'. Below this = idle.")]
    [SerializeField] private float inputDeadzone = 0.05f;
    
    [Header("Jump Settings")]
    [Tooltip("Enable jump functionality")]
    [SerializeField] private bool enableJump = true;
    
    [Tooltip("Upward impulse force for the hop (subtle: 2.0-3.5)")]
    [SerializeField] private float jumpImpulse = 2.5f;
    
    [Tooltip("Gravity multiplier when falling (makes hop snappy - higher = faster fall)")]
    [SerializeField] private float fallGravityMultiplier = 3.0f;
    
    [Tooltip("Gravity multiplier when ascending (makes jump snappy - higher = faster up)")]
    [SerializeField] private float riseGravityMultiplier = 1.5f;
    
    [Tooltip("Air control multiplier (0 = no air control, 1 = full control)")]
    [Range(0f, 1f)]
    [SerializeField] private float airControlPercent = 0.5f;
    
    [Header("Jump Momentum")]
    [Tooltip("Duration over which squat-hold decays preserved momentum (0 = instant decay, higher = slower decay)")]
    [SerializeField] private float squatMomentumDecayDuration = 0.18f;
    
    [Tooltip("Curve mapping squat hold time (0-1) to decay amount (0 = no decay, 1 = full decay). TakeoffMul = 1 - curve(t)")]
    [SerializeField] private AnimationCurve squatMomentumDecayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Tooltip("Fraction of pre-jump momentum preserved when jumping without directional input (vertical jump)")]
    [SerializeField, Range(0f, 1f)] private float verticalJumpCarryMul = 0.35f;
    
    [Tooltip("Bias toward input direction at takeoff (0 = pure momentum, 1 = pure input direction). Keep < 1")]
    [SerializeField, Range(0f, 1f)] private float takeoffDirectionalBias = 0.35f;
    
    [Header("Air Steering")]
    [Tooltip("Planar acceleration per second when steering in air")]
    [SerializeField] private float airSteerAcceleration = 18f;
    
    [Tooltip("Maximum turn rate in degrees per second while airborne (prevents instant reversals)")]
    [SerializeField] private float maxAirTurnRateDegPerSec = 220f;
    
    [Tooltip("Brief window after takeoff where steering is reduced (preserves launch direction)")]
    [SerializeField] private float momentumLockTimeAfterTakeoff = 0.08f;
    
    [Tooltip("Steering strength multiplier during momentum lock window (0-1)")]
    [SerializeField, Range(0f, 1f)] private float momentumLockSteerMultiplier = 0.2f;
    
    [Header("Air Steering Anti-Reversal")]
    [Tooltip("Dot product threshold below which input is considered opposing momentum (-1 = opposite, 0 = perpendicular)")]
    [SerializeField] private float oppositionThreshold = -0.85f;
    
    [Tooltip("Steering acceleration multiplier when input opposes momentum (backpedal penalty)")]
    [SerializeField, Range(0f, 1f)] private float backpedalPenaltyMul = 0.5f;
    
    [Header("Jump Animation")]
    [Tooltip("Minimum time to hold jump before showing squat animation")]
    [SerializeField] private float squatHoldTime = 0.1f;
    
    [Header("Jump State Recovery")]
    [Tooltip("Maximum time a jump can be active while grounded before being reset (prevents stuck jump state from ceiling collisions)")]
    [SerializeField] private float maxJumpTimeWhileGrounded = 0.2f;
    
    [Header("Ground Check")]
    [Tooltip("Distance to check for ground below player")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    
    [Tooltip("Radius of the ground check sphere")]
    [SerializeField] private float groundCheckRadius = 0.3f;
    
    [Tooltip("Y offset from player center to start ground check")]
    [SerializeField] private float groundCheckYOffset = 1.25f;
    
    [Tooltip("XZ offsets for ground check per camera yaw (centered around visual feet). Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] groundCheckXZOffsets = new Vector2[]
    {
        new Vector2(0f, -2f),   // 0°: X=0, Z=-2
        new Vector2(-2f, 0f),   // 90°: X=-2, Z=0
        new Vector2(0f, 2f),    // 180°: X=0, Z=2
        new Vector2(2f, 0f)     // 270°: X=2, Z=0
    };
    
    [Tooltip("Layers considered as ground")]
    [SerializeField] private LayerMask groundLayers = ~0; // Default: everything
    
    [Header("Slope Handling")]
    [Tooltip("Maximum slope angle (in degrees) that player can walk on. 0 = flat only, 45 = moderate slopes, 60 = steep")]
    [SerializeField, Range(0f, 90f)] private float maxWalkableSlopeAngle = 45f;
    
    [Tooltip("Force applied to prevent sliding down slopes when idle (higher = stickier on slopes)")]
    [SerializeField] private float slopeAntiSlideForce = 15f;
    
    [Tooltip("If true, use capsule collider contacts as fallback ground detection (helps with slopes)")]
    [SerializeField] private bool useCapsuleContactFallback = true;
    
    [Tooltip("Additional ground check points around the capsule bottom for better slope detection")]
    [SerializeField] private bool useMultiPointGroundCheck = true;
    
    [Tooltip("Number of additional check points around capsule bottom (4 = cardinal directions)")]
    [SerializeField, Range(2, 8)] private int multiPointCheckCount = 4;
    
    [Tooltip("Radius multiplier for multi-point checks (relative to groundCheckRadius)")]
    [SerializeField, Range(0.5f, 1.5f)] private float multiPointCheckRadiusMul = 0.8f;
    
    [Tooltip("Distance multiplier for multi-point checks (relative to groundCheckDistance)")]
    [SerializeField, Range(0.5f, 2f)] private float multiPointCheckDistanceMul = 1.2f;
    
    // Current ground check offset (updated based on camera yaw)
    private Vector3 currentGroundCheckOffset;
    
    [Header("Visual Ground Check (Ledge Detection)")]
    [Tooltip("If true, ground check is positioned under the visual sprite (detects ledges earlier)")]
    [SerializeField] private bool useVisualGroundCheck = true;
    
    [Tooltip("Forward offset for visual ground check (should match visual forward offset)")]
    [SerializeField] private float visualGroundCheckForwardOffset = 1.1f;
    
    [Tooltip("If true, disables main capsule collider when visual is over void, uses falling collider instead")]
    [SerializeField] private bool swapCollidersOnLedge = true;
    
    [Header("Falling Collider (Ledge Physics)")]
    [Tooltip("Sphere collider used when falling off ledges (positioned at visual location)")]
    [SerializeField] private SphereCollider fallingCollider;
    
    [Tooltip("Radius of the falling sphere collider")]
    [SerializeField] private float fallingColliderRadius = 0.4f;
    
    [Tooltip("Y offset for falling collider center")]
    [SerializeField] private float fallingColliderYOffset = -1f;
    
    [Tooltip("XZ offsets for falling collider per camera yaw. Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] fallingColliderXZOffsets = new Vector2[]
    {
        new Vector2(0f, -2f),   // 0°: X=0, Z=-2 (matches ground check)
        new Vector2(-2f, 0f),   // 90°: X=-2, Z=0
        new Vector2(0f, 2f),    // 180°: X=0, Z=2
        new Vector2(2f, 0f)     // 270°: X=2, Z=0
    };
    
    [Tooltip("Auto-create falling collider if not assigned")]
    [SerializeField] private bool autoCreateFallingCollider = true;
    
    [Tooltip("Time (seconds) to transition capsule center back to normal position after landing (allows physics push)")]
    [SerializeField] private float capsuleCenterTransitionTime = 0.05f;
    
    [Header("Jump Assist")]
    [Tooltip("Time after leaving ground where jump is still allowed (coyote time)")]
    [SerializeField] private float coyoteTime = 0.08f;
    
    [Tooltip("Time before landing where jump input is buffered")]
    [SerializeField] private float jumpBufferTime = 0.08f;
    
    [Header("Fall Impact (Camera Shake & Landing)")]
    [Tooltip("Enable fall impact effects (camera shake + landing animation)")]
    [SerializeField] private bool enableFallImpact = true;
    
    [Tooltip("Minimum fall height to trigger impact effects (below this = no shake/animation)")]
    [SerializeField] private float minFallHeightForImpact = 2.5f;
    
    [Tooltip("Fall height for maximum impact effects (above this = max shake/animation)")]
    [SerializeField] private float maxFallHeightForImpact = 8f;
    
    [Tooltip("Use dynamic impact (intensity scales with fall height) or static values")]
    [SerializeField] private bool useDynamicImpact = true;
    
    [Header("Static Impact Settings (if useDynamicImpact is false)")]
    [Tooltip("Shake intensity for static impact")]
    [SerializeField] private float staticShakeIntensity = 0.15f;
    
    [Tooltip("Shake duration for static impact (seconds)")]
    [SerializeField] private float staticShakeDuration = 0.3f;
    
    [Tooltip("Landing animation lock duration for static impact (seconds)")]
    [SerializeField] private float staticLandingLockDuration = 0.4f;
    
    [Header("Dynamic Impact Settings (if useDynamicImpact is true)")]
    [Tooltip("Minimum shake intensity (at minFallHeightForImpact)")]
    [SerializeField] private float minShakeIntensity = 0.1f;
    
    [Tooltip("Maximum shake intensity (at maxFallHeightForImpact)")]
    [SerializeField] private float maxShakeIntensity = 0.4f;
    
    [Tooltip("Minimum shake duration (at minFallHeightForImpact)")]
    [SerializeField] private float minShakeDuration = 0.2f;
    
    [Tooltip("Maximum shake duration (at maxFallHeightForImpact)")]
    [SerializeField] private float maxShakeDuration = 0.6f;
    
    [Tooltip("Minimum landing animation lock duration (at minFallHeightForImpact)")]
    [SerializeField] private float minLandingLockDuration = 0.3f;
    
    [Tooltip("Maximum landing animation lock duration (at maxFallHeightForImpact)")]
    [SerializeField] private float maxLandingLockDuration = 0.8f;
    
    [Tooltip("Shake frequency (shakes per second)")]
    [SerializeField] private float shakeFrequency = 20f;
    
    [Header("Camera-Relative Movement")]
    [Tooltip("If true, movement input is relative to camera yaw (Up = screen up, regardless of camera rotation)")]
    [SerializeField] private bool useCameraRelativeMovement = true;
    
    [Header("Player Collider")]
    [Tooltip("The capsule collider to adjust based on camera rotation")]
    [SerializeField] private CapsuleCollider playerCollider;
    
    [Tooltip("Y position for collider center")]
    [SerializeField] private float colliderYCenter = 1.75f;
    
    [Tooltip("XZ offsets for player collider center per camera yaw. Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] playerColliderXZOffsets = new Vector2[]
    {
        new Vector2(0f, -1.1f),   // 0°: X=0, Z=-1.1
        new Vector2(-1.1f, 0f),   // 90°: X=-1.1, Z=0
        new Vector2(0f, 1.1f),    // 180°: X=0, Z=1.1
        new Vector2(1.1f, 0f)     // 270°: X=1.1, Z=0
    };

    [Header("NPC Blocking")]
    [Tooltip("If true, the player stops at NPC blockers without using physics shove.")]
    [SerializeField] private bool respectNPCBlockers = true;

    [Tooltip("If true, solid player colliders ignore solid NPC colliders so characters do not shove each other.")]
    [SerializeField] private bool ignoreNPCPhysicsShove = true;

    [Tooltip("Extra spacing added between player and NPC blocker circles.")]
    [SerializeField] private float npcBlockSeparationBuffer = 0.05f;

    [Tooltip("Small backoff applied before contact so movement can slide cleanly along NPCs.")]
    [SerializeField] private float npcBlockSkin = 0.02f;
    
    [Header("Component References")]
    [SerializeField] private PlayerAnimationController3D_Mesh meshAnimationController; // MeshRenderer animation controller
    [SerializeField] private MeshRendererPlayerVisual playerVisual; // Visual quad mesh
    [SerializeField] private PlayerShadowCaster playerShadow; // Shadow caster
    [SerializeField] private OcclusionSilhouette occlusionSilhouette; // Occlusion silhouette
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private MobileInputManager mobileInputManager;
    [SerializeField] private TopDownCameraController3D cameraController;
    [SerializeField] private LandingDustSpawner landingDustSpawner; // Dust cloud spawner for landing impacts
    [SerializeField] private FootstepController footstepController; // For landing impact sounds
    
    [Header("Interaction")]
    [SerializeField] private UnityEvent OnInteract;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Tooltip("Log velocity when input is zero to detect drift")]
    [SerializeField] private bool logDriftDetection = false;
    
    [Tooltip("Visualize ground check in Scene view")]
    [SerializeField] private bool drawGroundCheckGizmo = true;

    // Constants for action names
    private const string ACTION_MAP_NAME = "Player";
    private const string MOVE_ACTION = "Move";
    private const string SPRINT_ACTION = "Sprint";
    private const string INTERACT_ACTION = "Interact";
    private const string JUMP_ACTION = "Jump";

    // Components
    private Rigidbody rb;
    private int currentCameraYawIndex = 0; // Track current camera yaw for collider adjustment
    
    // Input state
    private Vector2 movementInput; // XZ plane input (X = X, Y = Z)
    private bool isSprinting = false;
    private bool jumpInputHeld = false;
    private InputActionMap gameplayMap;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction interactAction;
    private InputAction jumpAction;
    private bool useFallbackKeyReading = false;
    
    // Jump state
    private bool isGrounded = false;
    private bool wasGroundedLastFrame = false;
    private float lastGroundedTime = -100f;
    private float lastJumpInputTime = -100f;
    private bool isJumping = false;
    private bool isSquatting = false;
    private float jumpHoldStartTime = -1f;
    private bool jumpInputWasHeld = false;
    
    // Jump momentum state (captures velocity at squat start for momentum-preserving takeoff)
    private Vector3 preJumpPlanarVel;
    private float squatStartTime;
    private bool capturedPreJumpVel;
    private float lastTakeoffTime;
    private float jumpStartTime; // Track when jump started (for ceiling collision detection)
    
    // Fall height tracking
    private float fallStartHeight = 0f; // Y position when fall started (left ground)
    private float maxFallHeight = 0f; // Highest Y position reached during fall
    private bool isTrackingFall = false;
    private Coroutine landingImpactCoroutine;
    
    // Visual ground check state (ledge detection)
    private bool isVisualOverVoid = false; // True when visual ground check finds no ground
    private bool wasVisualOverVoid = false;
    private bool usingFallingCollider = false; // True when using sphere collider instead of capsule
    
    // Capsule collider transition state (for smooth landing)
    private Vector3 normalCapsuleCenter; // Normal capsule center (camera-relative offset)
    private Vector3 groundCheckCapsuleCenter; // Capsule center at ground check position
    private Coroutine capsuleCenterTransitionCoroutine;
    
    // Freeze state (for camera transitions, dialogues, etc.)
    private bool isFrozen = false;
    private int freezeReferenceCount = 0;
    private bool preservePhysicsDuringFreeze = true; // If true, physics continues during freeze (soft freeze)
    
    // Cache for Physics queries
    private RaycastHit groundHit;
    private Vector3 currentGroundNormal = Vector3.up; // Store ground normal for slope calculations
    private static readonly List<Collider> NpcBlockerBuffer = new List<Collider>(32);

    private const float MobileInputResolveIntervalSeconds = 0.5f;
    private bool mobileInputSubscribed;
    private float nextMobileInputResolveTime;

    private readonly List<IPlayerMovementModifier> movementModifiers = new List<IPlayerMovementModifier>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureSpeedModifierStack();
        
        // Configure Rigidbody for top-down movement with jump support
        // Remove FreezePositionY to allow jumping
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Enable gravity for jump physics
        rb.useGravity = true;

        // Use InputActionAsset directly if available, otherwise create from default
        if (inputActions == null)
        {
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (inputActions == null)
            {
                Debug.LogError("[PlayerMovement3D] InputActionAsset not found! Please assign it in the inspector or place InputSystem_Actions in Resources folder.");
                return;
            }
        }

        // Get the Player action map (matches the Input Actions asset)
        gameplayMap = inputActions.FindActionMap(ACTION_MAP_NAME);
        if (gameplayMap == null)
        {
            Debug.LogError($"[PlayerMovement3D] Action map '{ACTION_MAP_NAME}' not found in Input Actions! Please ensure your InputActionAsset has a map named '{ACTION_MAP_NAME}'.");
            return;
        }

        moveAction = gameplayMap.FindAction(MOVE_ACTION);
        if (moveAction == null)
        {
            Debug.LogError($"[PlayerMovement3D] Action '{MOVE_ACTION}' not found in '{ACTION_MAP_NAME}' action map! Please add a '{MOVE_ACTION}' action to your Input Actions asset.");
            return;
        }

        sprintAction = gameplayMap.FindAction(SPRINT_ACTION);
        if (sprintAction == null)
        {
            Debug.LogWarning($"[PlayerMovement3D] Action '{SPRINT_ACTION}' not found - sprint will be disabled. Add a '{SPRINT_ACTION}' action to your Input Actions asset.");
        }

        interactAction = gameplayMap.FindAction(INTERACT_ACTION);
        if (interactAction == null)
        {
            Debug.LogWarning($"[PlayerMovement3D] Action '{INTERACT_ACTION}' not found - interaction will be disabled. Add an '{INTERACT_ACTION}' action to your Input Actions asset.");
        }
        
        jumpAction = gameplayMap.FindAction(JUMP_ACTION);
        if (jumpAction == null)
        {
            Debug.LogWarning($"[PlayerMovement3D] Action '{JUMP_ACTION}' not found - jump will be disabled. Add a '{JUMP_ACTION}' action to your Input Actions asset.");
        }

        TopDownCameraController3D resolvedController = SceneCameraResolver.ResolveGameplayCameraController();
        if (resolvedController != null)
            cameraController = resolvedController;
        else if (cameraController == null)
            cameraController = FindObjectOfType<TopDownCameraController3D>();
        
        // Auto-find mesh animation controller if not assigned
        if (meshAnimationController == null)
        {
            meshAnimationController = GetComponentInChildren<PlayerAnimationController3D_Mesh>();
        }
        
        // Auto-find player visual if not assigned
        if (playerVisual == null)
        {
            playerVisual = GetComponentInChildren<MeshRendererPlayerVisual>();
        }
        
        // Auto-find landing dust spawner if not assigned
        if (landingDustSpawner == null)
        {
            landingDustSpawner = GetComponentInChildren<LandingDustSpawner>();
        }
        
        // Auto-find footstep controller if not assigned (for landing impact sounds)
        if (footstepController == null)
        {
            footstepController = GetComponent<FootstepController>();
        }
        
        // Auto-find player shadow if not assigned
        if (playerShadow == null)
        {
            playerShadow = GetComponentInChildren<PlayerShadowCaster>();
        }
        
        // Auto-find occlusion silhouette if not assigned
        if (occlusionSilhouette == null)
        {
            occlusionSilhouette = GetComponent<OcclusionSilhouette>();
        }
        
        // Auto-find capsule collider if not assigned
        if (playerCollider == null)
        {
            playerCollider = GetComponent<CapsuleCollider>();
        }
        
        // Ensure offset arrays are initialized
        if (groundCheckXZOffsets == null || groundCheckXZOffsets.Length < 4)
        {
            groundCheckXZOffsets = new Vector2[]
            {
                new Vector2(0f, -2f),
                new Vector2(-2f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 0f)
            };
        }
        
        if (playerColliderXZOffsets == null || playerColliderXZOffsets.Length < 4)
        {
            playerColliderXZOffsets = new Vector2[]
            {
                new Vector2(0f, -1.1f),
                new Vector2(-1.1f, 0f),
                new Vector2(0f, 1.1f),
                new Vector2(1.1f, 0f)
            };
        }
        
        if (fallingColliderXZOffsets == null || fallingColliderXZOffsets.Length < 4)
        {
            fallingColliderXZOffsets = new Vector2[]
            {
                new Vector2(0f, -2f),
                new Vector2(-2f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 0f)
            };
        }
        
        // Apply initial collider center and ground check offset
        // Try to get current camera yaw index, otherwise default to 0
        int initialYawIndex = 0;
        if (cameraController != null)
        {
            initialYawIndex = cameraController.YawIndex90;
        }
        
        UpdateColliderForCameraYaw(initialYawIndex);
        UpdateGroundCheckForCameraYaw(initialYawIndex);
        
        // Update visual position for initial yaw
        if (playerVisual != null)
        {
            playerVisual.UpdateVisualForCameraYaw(initialYawIndex);
        }
        
        // Update shadow position for initial yaw
        if (playerShadow != null)
        {
            playerShadow.UpdateShadowForCameraYaw(initialYawIndex);
        }
        
        // Update silhouette for initial yaw
        if (occlusionSilhouette != null)
        {
            occlusionSilhouette.UpdateSilhouetteForCameraYaw(initialYawIndex);
        }
        
        // Setup falling collider for ledge physics
        SetupFallingCollider();
    }

    private void OnEnable()
    {
        CharacterCollisionRegistry.OnNpcColliderRegistered += OnNpcColliderRegistered;
        CharacterCollisionRegistry.OnNpcColliderUnregistered += OnNpcColliderUnregistered;
        RegisterPlayerSolidColliders();
        SyncIgnoredNpcCollisions();

        // Enable action map (this enables all actions in the map)
        // Don't enable individual actions separately to avoid conflicts
        if (gameplayMap != null)
        {
            gameplayMap.Enable();
        }
        
        // Subscribe to events (actions are already enabled via map)
        if (moveAction != null)
        {
            moveAction.performed += OnMove;
            moveAction.canceled += OnStop;
        }

        if (sprintAction != null)
        {
            sprintAction.performed += OnSprintStart;
            sprintAction.canceled += OnSprintEnd;
        }

        if (interactAction != null)
        {
            interactAction.performed += OnInteractPressed;
        }
        
        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpPressed;
            jumpAction.canceled += OnJumpReleased;
        }

        SubscribeMobileInput();
    }

    /// <summary>
    /// MobileServices lives on the DontDestroyOnLoad services root, so the inspector
    /// reference cannot be serialized from a scene or from PlayerRoot.prefab. Resolve it
    /// lazily and re-poll on a slow cadence until it appears, otherwise every mobile
    /// gating path (HUD suppression, PSG1 priority, on-screen sprint) is silently skipped.
    /// </summary>
    private MobileInputManager MobileInput
    {
        get
        {
            if (mobileInputManager != null)
                return mobileInputManager;

            if (Time.unscaledTime < nextMobileInputResolveTime)
                return null;

            nextMobileInputResolveTime = Time.unscaledTime + MobileInputResolveIntervalSeconds;
            mobileInputManager = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);

            if (mobileInputManager != null && isActiveAndEnabled)
                SubscribeMobileInput();

            return mobileInputManager;
        }
    }

    private void SubscribeMobileInput()
    {
        if (mobileInputSubscribed || mobileInputManager == null)
            return;

        mobileInputManager.OnBPressed += OnMobileJumpPressed;
        mobileInputManager.OnBReleased += OnMobileJumpReleased;
        mobileInputSubscribed = true;
    }

    private void UnsubscribeMobileInput()
    {
        if (!mobileInputSubscribed || mobileInputManager == null)
        {
            mobileInputSubscribed = false;
            return;
        }

        mobileInputManager.OnBPressed -= OnMobileJumpPressed;
        mobileInputManager.OnBReleased -= OnMobileJumpReleased;
        mobileInputSubscribed = false;
    }

    private void OnDisable()
    {
        // Timed speed sources must drop if movement is disabled mid-timer.
        // Wearables and party passives have no expiry and stay registered.
        PlayerMovementSpeedModifierStack speedModifiers = GetComponent<PlayerMovementSpeedModifierStack>();
        if (speedModifiers != null)
            speedModifiers.CancelExpiringSources();

        CharacterCollisionRegistry.OnNpcColliderRegistered -= OnNpcColliderRegistered;
        CharacterCollisionRegistry.OnNpcColliderUnregistered -= OnNpcColliderUnregistered;
        UnregisterPlayerSolidColliders();

        // Unsubscribe from events first
        if (moveAction != null)
        {
            moveAction.performed -= OnMove;
            moveAction.canceled -= OnStop;
        }

        if (sprintAction != null)
        {
            sprintAction.performed -= OnSprintStart;
            sprintAction.canceled -= OnSprintEnd;
        }

        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPressed;
        }
        
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPressed;
            jumpAction.canceled -= OnJumpReleased;
        }

        UnsubscribeMobileInput();

        // Do not disable the shared action map here. The PlayerRoot can be
        // duplicated briefly while Bootstrap activates a destination scene;
        // the scene-owned duplicate is then disabled by SpawnService. Both
        // movement components reference the same InputActionAsset, so one
        // duplicate's OnDisable must not turn off the persistent player's
        // controls. The persistent input owner keeps the map enabled for the
        // lifetime of the gameplay player.

        // Reset movement and sprint state
        movementInput = Vector2.zero;
        if (isSprinting)
        {
            isSprinting = false;
            if (cameraController != null)
            {
                cameraController.ClearMovementMode();
            }
        }
    }

    private void RegisterPlayerSolidColliders()
    {
        if (playerCollider != null && !playerCollider.isTrigger)
            CharacterCollisionRegistry.RegisterPlayerCollider(playerCollider);

        if (fallingCollider != null && !fallingCollider.isTrigger)
            CharacterCollisionRegistry.RegisterPlayerCollider(fallingCollider);
    }

    private void UnregisterPlayerSolidColliders()
    {
        if (playerCollider != null)
            CharacterCollisionRegistry.UnregisterPlayerCollider(playerCollider);

        if (fallingCollider != null)
            CharacterCollisionRegistry.UnregisterPlayerCollider(fallingCollider);
    }

    private void OnNpcColliderRegistered(Collider npcCollider)
    {
        SetNpcCollisionIgnored(npcCollider, ignoreNPCPhysicsShove);
    }

    private void OnNpcColliderUnregistered(Collider npcCollider)
    {
        SetNpcCollisionIgnored(npcCollider, false);
    }

    private void SyncIgnoredNpcCollisions()
    {
        foreach (Collider npcCollider in CharacterCollisionRegistry.NpcSolidColliders)
            SetNpcCollisionIgnored(npcCollider, ignoreNPCPhysicsShove);
    }

    private void SetNpcCollisionIgnored(Collider npcCollider, bool ignored)
    {
        if (npcCollider == null || npcCollider.isTrigger)
            return;

        if (playerCollider != null && !playerCollider.isTrigger)
            Physics.IgnoreCollision(playerCollider, npcCollider, ignored);

        if (fallingCollider != null && !fallingCollider.isTrigger)
            Physics.IgnoreCollision(fallingCollider, npcCollider, ignored);
    }

    private void FixedUpdate()
    {
        // Update ground state
        CheckGrounded();
        
        // Track fall height when airborne
        if (!isGrounded)
        {
            if (!isTrackingFall)
            {
                // Start tracking fall (player just left ground)
                isTrackingFall = true;
                fallStartHeight = transform.position.y;
                maxFallHeight = fallStartHeight;
            }
            else
            {
                // Update max height during fall (track highest point reached)
                if (transform.position.y > maxFallHeight)
                {
                    maxFallHeight = transform.position.y;
                }
            }
        }
        else if (isTrackingFall && wasGroundedLastFrame)
        {
            // Reset tracking if we're grounded (shouldn't happen, but safety check)
            isTrackingFall = false;
            maxFallHeight = 0f;
            fallStartHeight = 0f;
        }
        
        // Handle landing
        if (isGrounded && !wasGroundedLastFrame)
        {
            HandleLanding();
        }
        wasGroundedLastFrame = isGrounded;
        
        // Check for ceiling collision (jump cancelled by hitting ceiling immediately)
        // This must run after ground check to detect when player is grounded but jump state is stuck
        CheckForCeilingCollision();
        
        // If frozen, zero velocity and skip movement processing
        if (isFrozen)
        {
            HandleFrozenState();
            return;
        }
        
        // Check input lock service (movement lock)
        InputLockService inputLock = InputLockService.Instance;
        bool movementLocked = inputLock != null && inputLock.IsMovementLocked;
        
        // Get input
        Vector2 move = movementLocked ? Vector2.zero : GetMovementInput();
        bool sprinting = movementLocked ? false : GetSprintInput();

        // Priority: PSG1 Device > Mobile Virtual Controls > Input System (Keyboard/Gamepad)
        // Only process movement sources when movement is not locked.
        if (!movementLocked)
        {
            // Use mobile input if available and active.
            MobileInputManager mobileInput = MobileInput;
            if (mobileInput != null && mobileInput.IsMobileControlsActive)
            {
                move = mobileInput.GetMovementInput();
                // Check mobile sprint button
                sprinting = mobileInput.IsSprinting();
            }
            // If PSG1 device is connected, mobile controls are disabled and Input System handles everything.
            // Otherwise, read Input System values directly (safer than IsPressed during callbacks).
            else
            {
                // Use fallback key reading if composite binding is missing
                if (useFallbackKeyReading)
                {
                    move = ReadMovementFromIndividualKeys();
                }
                // Read movement input directly from action (fallback if callback didn't fire)
                else if (moveAction != null && moveAction.enabled)
                {
                    try
                    {
                        Vector2 directInput = moveAction.ReadValue<Vector2>();
                        // Use direct read if callback value is zero (might be stale)
                        if (directInput.magnitude > 0.01f || movementInput.magnitude < 0.01f)
                        {
                            move = directInput;
                        }
                    }
                    catch (System.InvalidOperationException)
                    {
                        // Action might be in callback - use cached value
                        // This is safe, we'll get the value next frame
                    }
                }

                // Read sprint state directly (safer than IsPressed during callbacks)
                if (sprintAction != null && sprintAction.enabled)
                {
                    try
                    {
                        sprinting = sprintAction.ReadValue<float>() > 0.5f;
                    }
                    catch (System.InvalidOperationException)
                    {
                        // Action might be in callback - use cached value
                        sprinting = isSprinting;
                    }
                }
            }
        }

        // Normalize if magnitude > 1 (prevents diagonal speed boost)
        if (move.magnitude > 1f)
            move = move.normalized;

        // Update sprint state
        if (sprinting != isSprinting)
        {
            if (sprinting)
                OnSprintStart(default);
            else
                OnSprintEnd(default);
        }
        
        // Handle jump input (with buffer)
        HandleJumpInput();

        // Calculate effective speed (no longer reduced for airborne - air steering handles that)
        float effectiveSpeed = GetEffectiveMoveSpeed(sprinting);
        
        // NOTE: Old airborne speed scaling (`effectiveSpeed *= airControlPercent`) was REMOVED.
        // Air control is now handled by ApplyAirSteering() which provides influence-based
        // trajectory shaping instead of simple speed reduction. This preserves takeoff
        // momentum while still allowing responsive steering.

        // Convert 2D input (XZ plane) to 3D movement vector
        // Apply camera-relative movement if enabled
        Vector3 move3D;
        if (useCameraRelativeMovement && cameraController != null)
        {
            move3D = cameraController.InputToWorldDirection(move);
        }
        else
        {
            // Default: world-aligned movement (Up = +Z, Right = +X)
            move3D = new Vector3(move.x, 0f, move.y);
        }
        
        // --- Grounded vs Airborne movement handling ---
        if (isGrounded)
        {
            // GROUNDED: Use deterministic MovePosition for precise control
            Vector3 intendedVelocity = move3D * effectiveSpeed;
            
            // SLOPE ALIGNMENT: Project movement onto slope surface to maintain consistent speed
            // This prevents slower movement when going uphill
            float slopeAngle = Vector3.Angle(currentGroundNormal, Vector3.up);
            if (slopeAngle > 0.1f && slopeAngle <= maxWalkableSlopeAngle)
            {
                // Project movement onto slope plane to maintain speed consistency
                intendedVelocity = ProjectMovementOntoSlope(intendedVelocity, currentGroundNormal);
            }
            
            // Lock X/Z movement when squatting (holding jump button)
            // NOTE: We still allow the squat to capture momentum (done in HandleJumpInput),
            // but we reduce movement during squat for the visual "crouch" feel
            if (isSquatting)
            {
                intendedVelocity = Vector3.zero;
            }
            
            // Move using Rigidbody MovePosition for XZ. NPC blockers are handled explicitly so
            // the player feels solid against NPCs without trading shove impulses with them.
            Vector3 newPosition = ResolveNpcBlockedPosition(intendedVelocity * Time.fixedDeltaTime);
            rb.MovePosition(newPosition);
            
            // ANTI-SLIDE: Prevent sliding down slopes when idle
            if (move.magnitude < inputDeadzone && slopeAngle > 0.1f && slopeAngle <= maxWalkableSlopeAngle)
            {
                // Apply force to counteract gravity pulling player down slope
                Vector3 antiSlideForce = CalculateSlopeAntiSlideForce(currentGroundNormal);
                rb.AddForce(antiSlideForce, ForceMode.Force);
                
                // Also dampen any velocity along the slope direction when idle
                // This stops existing slide momentum
                Vector3 slopeRight = Vector3.Cross(currentGroundNormal, Vector3.up).normalized;
                Vector3 slopeForward = Vector3.Cross(slopeRight, currentGroundNormal).normalized;
                
                // Get velocity component along slope
                Vector3 velocityAlongSlope = Vector3.Project(rb.linearVelocity, slopeForward);
                
                // Dampen sliding velocity (both directions on slope)
                if (velocityAlongSlope.magnitude > 0.01f)
                {
                    rb.linearVelocity -= velocityAlongSlope * 0.9f; // Strong damping
                }
            }
            
            // Handle drift prevention and velocity enforcement (grounded only)
            HandleVelocityClampingGrounded(move, intendedVelocity);
        }
        else
        {
            // AIRBORNE: Use air steering for influence-based trajectory shaping
            // This preserves takeoff momentum while allowing responsive control
            ApplyAirSteering(move3D, effectiveSpeed);
        }
        
        // Apply enhanced gravity for snappy hops
        ApplyJumpGravity();

        // Update mesh animation controller (with jump state)
        if (meshAnimationController != null)
        {
            bool isJumpingState = isJumping && !isGrounded;
            meshAnimationController.UpdateAnimation(move, sprinting, isSquatting, isJumpingState);
        }
        
        // Update camera movement mode based on input intent (not velocity)
        // This guarantees 3 distinct zoom levels: Idle, Walk, Sprint
        if (cameraController != null)
        {
            if (move.magnitude < inputDeadzone)
            {
                cameraController.SetMovementMode(CameraMovementMode.Idle);
            }
            else if (sprinting)
            {
                cameraController.SetMovementMode(CameraMovementMode.Sprint);
            }
            else
            {
                cameraController.SetMovementMode(CameraMovementMode.Walk);
            }
        }

        if (enableDebugLogs && move.magnitude > 0.1f)
        {
            Debug.Log($"[PlayerMovement3D] Move: {move}, 3D: {move3D}, Speed: {effectiveSpeed}, Sprinting: {isSprinting}");
        }
        
        // Debug logging
        if (enableDebugLogs && move.magnitude > 0.1f)
        {
            Debug.Log($"[PlayerMovement3D] Move: {move}, Speed: {effectiveSpeed}, Grounded: {isGrounded}, Jumping: {isJumping}");
        }
        
        // Drift detection logging
        if (logDriftDetection && move.magnitude < inputDeadzone && isGrounded)
        {
            Vector3 planarVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (planarVel.magnitude > 0.01f)
            {
                Debug.LogWarning($"[PlayerMovement3D] DRIFT DETECTED! Planar Velocity: {planarVel}, Magnitude: {planarVel.magnitude:F4}");
            }
        }
    }
    
    #region Ground Check
    
    /// <summary>
    /// Check if player is grounded using SphereCast with slope-aware detection.
    /// When useVisualGroundCheck is enabled, ground check is positioned under the visual sprite.
    /// Includes multi-point checks and capsule contact fallback for better slope detection.
    /// </summary>
    private void CheckGrounded()
    {
        // Use current ground check offset (updated based on camera yaw)
        Vector3 origin = transform.position + currentGroundCheckOffset;
        
        // Apply visual forward offset if enabled (for ledge detection under sprite)
        if (useVisualGroundCheck && cameraController != null)
        {
            Vector3 cameraForward = cameraController.GetCameraForwardXZ();
            origin += cameraForward * visualGroundCheckForwardOffset;
        }
        
        // Primary ground check: SphereCast downward from visual position
        bool groundCheckResult = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out groundHit,
            groundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
        
        // Track visual over void state
        wasVisualOverVoid = isVisualOverVoid;
        isVisualOverVoid = !groundCheckResult;
        
        // SLOPE-AWARE GROUND DETECTION: Check if primary hit is on a walkable slope
        bool isOnWalkableSlope = false;
        if (groundCheckResult)
        {
            currentGroundNormal = groundHit.normal;
            float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            isOnWalkableSlope = slopeAngle <= maxWalkableSlopeAngle;
            
            if (enableDebugLogs && slopeAngle > 0.1f)
                Debug.Log($"[PlayerMovement3D] Ground check slope angle: {slopeAngle:F1}° (max: {maxWalkableSlopeAngle}°)");
        }
        else
        {
            currentGroundNormal = Vector3.up; // Default to flat
        }
        
        // MULTI-POINT GROUND CHECK: Check additional points around capsule bottom for slopes
        bool multiPointGroundFound = false;
        if (useMultiPointGroundCheck && (!groundCheckResult || !isOnWalkableSlope))
        {
            // Calculate capsule bottom center (approximate)
            Vector3 capsuleBottom = transform.position;
            if (playerCollider != null)
            {
                capsuleBottom = transform.position + playerCollider.center;
                capsuleBottom.y -= playerCollider.height * 0.5f; // Bottom of capsule
            }
            
            float checkRadius = groundCheckRadius * multiPointCheckRadiusMul;
            float checkDistance = groundCheckDistance * multiPointCheckDistanceMul;
            
            // Check points in a circle around capsule bottom
            for (int i = 0; i < multiPointCheckCount; i++)
            {
                float angle = (360f / multiPointCheckCount) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * checkRadius,
                    0f,
                    Mathf.Sin(angle) * checkRadius
                );
                
                Vector3 checkOrigin = capsuleBottom + offset;
                
                if (Physics.SphereCast(
                    checkOrigin,
                    checkRadius * 0.5f,
                    Vector3.down,
                    out RaycastHit hit,
                    checkDistance,
                    groundLayers,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    float hitSlopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                    if (hitSlopeAngle <= maxWalkableSlopeAngle)
                    {
                        multiPointGroundFound = true;
                        currentGroundNormal = hit.normal; // Update ground normal
                        break; // Found walkable ground
                    }
                }
            }
        }
        
        // CAPSULE CONTACT FALLBACK: Check if capsule collider is touching ground (for slopes)
        bool capsuleContactGround = false;
        if (useCapsuleContactFallback && playerCollider != null && !groundCheckResult && !multiPointGroundFound)
        {
            // Get capsule bottom bounds
            Vector3 capsuleCenter = transform.position + playerCollider.center;
            float capsuleBottomY = capsuleCenter.y - (playerCollider.height * 0.5f);
            
            // Check sphere at capsule bottom
            Vector3 capsuleBottomPos = new Vector3(capsuleCenter.x, capsuleBottomY, capsuleCenter.z);
            
            Collider[] contacts = Physics.OverlapSphere(
                capsuleBottomPos,
                playerCollider.radius * 1.1f, // Slightly larger to catch contacts
                groundLayers,
                QueryTriggerInteraction.Ignore
            );
            
            foreach (Collider contact in contacts)
            {
                // Get contact point and normal
                Vector3 closestPoint = contact.ClosestPoint(capsuleBottomPos);
                Vector3 direction = (closestPoint - capsuleBottomPos).normalized;
                
                if (Physics.Raycast(
                    capsuleBottomPos,
                    direction,
                    out RaycastHit contactHit,
                    playerCollider.radius * 1.5f,
                    groundLayers,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    float contactSlopeAngle = Vector3.Angle(contactHit.normal, Vector3.up);
                    if (contactSlopeAngle <= maxWalkableSlopeAngle)
                    {
                        capsuleContactGround = true;
                        currentGroundNormal = contactHit.normal; // Update ground normal
                        break;
                    }
                }
            }
        }
        
        // Determine final grounded state (primary check on walkable slope OR multi-point OR capsule contact)
        bool finalGrounded = (groundCheckResult && isOnWalkableSlope) || multiPointGroundFound || capsuleContactGround;
        
        // Only enter the offset falling-collider mode when the player genuinely walks off a ledge.
        // A jump also makes the visual ground probe report void, but swapping the centered capsule
        // for the camera-offset sphere during ascent can depenetrate the player away from terrace
        // walls. Keeping the capsule for the complete jump lets its rounded shoulder slide over the
        // lip while held movement continues to press toward the platform.
        bool canEnterFallingColliderMode = !isJumping && rb.linearVelocity.y <= 0.01f;

        // Determine actual grounded state
        // When visual is over void during a walk-off fall, let the falling collider handle physics.
        if (useVisualGroundCheck && swapCollidersOnLedge && isVisualOverVoid && !finalGrounded &&
            (usingFallingCollider || canEnterFallingColliderMode))
        {
            // Visual is over void - switch to falling mode
            isGrounded = false;
            currentGroundNormal = Vector3.up; // Reset when falling
            
            // Handle collider swap (capsule -> sphere)
            if (!usingFallingCollider)
            {
                SwitchToFallingCollider();
            }
            
            // DUAL GROUND CHECK: When using falling collider, also check if IT is touching ground
            // This fixes the bug where falling collider catches on a ledge but visual ground check
            // doesn't see ground, causing the player to hang mid-air.
            if (usingFallingCollider && fallingCollider != null)
            {
                Vector3 fallingColliderWorldPos = fallingCollider.transform.position + fallingCollider.center;
                
                // Check if falling collider is touching ground (slightly smaller radius to avoid edge cases)
                bool fallingColliderOnGround = Physics.CheckSphere(
                    fallingColliderWorldPos,
                    fallingColliderRadius * 0.9f,
                    groundLayers,
                    QueryTriggerInteraction.Ignore
                );
                
                if (fallingColliderOnGround)
                {
                    // Falling collider is on ground - switch back to capsule collider
                    isGrounded = true;
                    SwitchToCapsuleCollider();
                    
                    if (enableDebugLogs)
                        Debug.Log($"[PlayerMovement3D] Falling collider detected ground at {fallingColliderWorldPos} - switching to capsule");
                }
            }
        }
        else
        {
            // Normal grounded check (now includes slope-aware detection)
            isGrounded = finalGrounded;
            
            // If we were using falling collider and now grounded, switch back
            if (usingFallingCollider && isGrounded)
            {
                SwitchToCapsuleCollider();
            }
        }
        
        // Update falling collider position to match visual position
        UpdateFallingColliderPosition();
        
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }
    }
    
    /// <summary>
    /// Check if player is within coyote time (recently grounded).
    /// </summary>
    private bool IsWithinCoyoteTime()
    {
        return Time.time - lastGroundedTime <= coyoteTime;
    }
    
    /// <summary>
    /// Check if there's a buffered jump input.
    /// </summary>
    private bool HasBufferedJumpInput()
    {
        return Time.time - lastJumpInputTime <= jumpBufferTime;
    }
    
    /// <summary>
    /// Project movement direction onto slope surface to maintain consistent speed regardless of slope angle.
    /// This prevents slower movement when going uphill.
    /// </summary>
    private Vector3 ProjectMovementOntoSlope(Vector3 movementDirection, Vector3 groundNormal)
    {
        // Project movement onto the plane defined by ground normal
        Vector3 projected = Vector3.ProjectOnPlane(movementDirection, groundNormal);
        
        // Normalize to maintain consistent speed (otherwise going uphill would be slower)
        // The magnitude is preserved by normalizing, then multiplying by original magnitude
        if (projected.sqrMagnitude > 0.01f)
        {
            return projected.normalized * movementDirection.magnitude;
        }
        
        return Vector3.zero; // Can't move on this slope
    }
    
    /// <summary>
    /// Calculate the component of gravity that pulls the player down the slope.
    /// Returns a force vector that counteracts sliding when idle.
    /// </summary>
    private Vector3 CalculateSlopeAntiSlideForce(Vector3 groundNormal)
    {
        // Calculate slope direction (the direction down the slope)
        Vector3 slopeDirection = Vector3.Cross(Vector3.Cross(groundNormal, Vector3.down), groundNormal).normalized;
        
        // Calculate how steep the slope is (0 = flat, 1 = vertical)
        float slopeSteepness = 1f - Vector3.Dot(groundNormal, Vector3.up);
        
        // Apply force to counteract gravity pulling player down slope
        // Force scales with slope steepness - steeper slopes need more force
        return -slopeDirection * slopeAntiSlideForce * slopeSteepness;
    }
    
    /// <summary>
    /// Get the current slope angle in degrees.
    /// </summary>
    public float GetCurrentSlopeAngle()
    {
        return Vector3.Angle(currentGroundNormal, Vector3.up);
    }
    
    #endregion
    
    #region Jump Logic
    
    /// <summary>
    /// Handle jump input with buffering and coyote time.
    /// Jump executes on RELEASE, not press (for squat animation).
    /// </summary>
    private void HandleJumpInput()
    {
        if (!enableJump) return;
        
        // Respect movement lock - jump is part of Movement channel
        InputLockService inputLock = InputLockService.Instance;
        if (inputLock != null && inputLock.IsMovementLocked)
            return;
        
        bool jumpInputCurrentlyHeld = jumpInputHeld;
        
        // Detect jump input release (was held, now not held)
        bool jumpJustReleased = jumpInputWasHeld && !jumpInputCurrentlyHeld;
        
        // Update squat state based on jump input
        if (isGrounded && jumpInputCurrentlyHeld)
        {
            // Holding jump while grounded = squat
            if (!isSquatting)
            {
                // IMPORTANT: Capture pre-jump planar velocity BEFORE entering squat
                // This preserves the momentum snapshot for takeoff calculations
                if (!capturedPreJumpVel)
                {
                    preJumpPlanarVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    squatStartTime = Time.time;
                    capturedPreJumpVel = true;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[PlayerMovement3D] Captured pre-jump velocity: {preJumpPlanarVel}, magnitude: {preJumpPlanarVel.magnitude:F2}");
                }
                
                isSquatting = true;
                jumpHoldStartTime = Time.time;
            }
        }
        else
        {
            // Not holding or not grounded = stop squatting
            if (isSquatting)
            {
                isSquatting = false;
                jumpHoldStartTime = -1f;
                
                // Reset momentum capture if squat ends without jumping
                // (e.g., player released jump without actually jumping, or got knocked off ground)
                if (capturedPreJumpVel)
                {
                    capturedPreJumpVel = false;
                    if (enableDebugLogs)
                        Debug.Log("[PlayerMovement3D] Squat cancelled without jump - reset captured velocity");
                }
            }
        }
        
        // Execute jump on RELEASE (not press)
        if (jumpJustReleased)
        {
            bool canJump = isGrounded || IsWithinCoyoteTime();
            
            if (canJump && !isJumping)
            {
                ExecuteJump();
                lastJumpInputTime = -100f; // Clear buffer after jump executes
            }
            else
            {
                // Can't jump right now - buffer the release for coyote time
                lastJumpInputTime = Time.time;
            }
        }
        else if (jumpInputCurrentlyHeld)
        {
            // While holding jump, update buffer time (for coyote time when released)
            if (isGrounded || IsWithinCoyoteTime())
            {
                lastJumpInputTime = Time.time;
            }
        }
        
        jumpInputWasHeld = jumpInputCurrentlyHeld;
    }
    
    /// <summary>
    /// Calculate takeoff momentum multiplier based on squat hold duration.
    /// Quick release = close to 1 (preserve momentum), long hold = close to 0 (more vertical).
    /// </summary>
    private float GetTakeoffMomentumMultiplier()
    {
        if (squatMomentumDecayDuration <= 0f)
            return 0f; // Instant decay if duration is zero
        
        float t = Mathf.Clamp01((Time.time - squatStartTime) / squatMomentumDecayDuration);
        float decay = squatMomentumDecayCurve.Evaluate(t);
        return 1f - decay;
    }
    
    /// <summary>
    /// Execute the jump impulse with momentum-preserving takeoff.
    /// Takeoff planar velocity is computed from:
    /// - Pre-jump momentum (captured at squat start)
    /// - Squat hold duration (longer hold = more vertical)
    /// - Input direction at takeoff (biases toward desired direction)
    /// </summary>
    private void ExecuteJump()
    {
        isJumping = true;
        
        // --- Compute takeoff planar velocity ---
        
        // Safety: If velocity wasn't captured (e.g., buffered jump from mid-air),
        // use current velocity as fallback with full momentum
        float takeoffMul;
        if (!capturedPreJumpVel)
        {
            preJumpPlanarVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            takeoffMul = 1f; // Full momentum for instant jumps
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Jump executed without squat - using current velocity: {preJumpPlanarVel}");
        }
        else
        {
            // Base multiplier from squat hold duration
            takeoffMul = GetTakeoffMomentumMultiplier();
        }
        
        // Start with preserved momentum (decayed by squat hold)
        Vector3 takeoffPlanar = preJumpPlanarVel * takeoffMul;
        
        // Determine takeoff intent based on current input
        Vector2 currentInput = GetMovementInput();
        bool hasInput = currentInput.sqrMagnitude > 0.01f;
        
        if (hasInput)
        {
            // Directional jump: bias momentum toward input direction
            // Convert input to world direction (camera-relative)
            Vector3 inputDir3D;
            if (useCameraRelativeMovement && cameraController != null)
            {
                inputDir3D = cameraController.InputToWorldDirection(currentInput);
            }
            else
            {
                inputDir3D = new Vector3(currentInput.x, 0f, currentInput.y);
            }
            
            // Calculate target speed based on sprint state (includes volume modifiers)
            float targetSpeed = GetEffectiveMoveSpeed(isSprinting);
            Vector3 desiredPlanar = inputDir3D.normalized * targetSpeed;
            
            // Blend momentum toward desired direction
            takeoffPlanar = Vector3.Lerp(takeoffPlanar, desiredPlanar, takeoffDirectionalBias);
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Directional jump: input={currentInput}, bias={takeoffDirectionalBias:F2}, takeoffPlanar={takeoffPlanar}");
        }
        else
        {
            // Vertical-ish jump: reduce horizontal carry significantly
            takeoffPlanar *= verticalJumpCarryMul;
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Vertical jump: carryMul={verticalJumpCarryMul:F2}, takeoffPlanar={takeoffPlanar}");
        }
        
        // Apply computed takeoff planar velocity (preserve current Y for safety)
        rb.linearVelocity = new Vector3(takeoffPlanar.x, 0f, takeoffPlanar.z);
        
        // Apply upward impulse (volume modifiers can reduce hop height)
        float jumpForce = jumpImpulse * GetExternalJumpMultiplier();
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        
        // Reset momentum capture state and record takeoff time
        capturedPreJumpVel = false;
        lastTakeoffTime = Time.time;
        jumpStartTime = Time.time; // Track jump start for ceiling collision detection
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Jump executed! Impulse: {jumpForce}, takeoffMul: {takeoffMul:F2}, finalPlanar: {takeoffPlanar.magnitude:F2}");
        }
    }
    
    /// <summary>
    /// Detect if jump was cancelled by hitting a ceiling.
    /// This happens when player jumps but immediately hits a ceiling and never leaves the ground,
    /// causing isJumping to stay true and blocking future jumps.
    /// 
    /// Detection methods:
    /// 1. Player is grounded + Y velocity is zero/negative + time passed (immediate ceiling hit)
    /// 2. Player has been grounded for too long while in jump state (timeout fallback)
    /// </summary>
    private void CheckForCeilingCollision()
    {
        if (!isJumping || jumpStartTime < 0f)
            return; // Not jumping or jump start time not set, nothing to check
        
        float timeSinceJump = Time.time - jumpStartTime;
        
        // Only check if player is grounded (ceiling collision means player never left ground)
        if (!isGrounded)
            return; // Player is airborne, jump is proceeding normally
        
        // Method 1: Check for immediate ceiling collision (Y velocity zero/negative)
        const float minTimeForCeilingCheck = 0.05f; // Small delay to avoid false positives
        if (timeSinceJump > minTimeForCeilingCheck)
        {
            float yVelocity = rb.linearVelocity.y;
            
            // If Y velocity is zero or negative and we're grounded, jump was likely cancelled by ceiling
            if (yVelocity <= 0.01f)
            {
                // Reset jump state to allow future jumps
                isJumping = false;
                jumpStartTime = -1f;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerMovement3D] Jump cancelled by ceiling collision detected. Time since jump: {timeSinceJump:F3}s, Y velocity: {yVelocity:F3}");
                }
                return;
            }
        }
        
        // Method 2: Timeout fallback - if player has been grounded for too long while in jump state,
        // reset jump state (catches edge cases where player might be slightly ascending but still grounded)
        if (timeSinceJump > maxJumpTimeWhileGrounded)
        {
            // Player has been grounded too long while in jump state - reset it
            isJumping = false;
            jumpStartTime = -1f;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerMovement3D] Jump state reset due to timeout (grounded for {timeSinceJump:F3}s). This may indicate a ceiling collision.");
            }
        }
    }
    
    /// <summary>
    /// Apply enhanced gravity for snappy hops (faster up and down).
    /// </summary>
    private void ApplyJumpGravity()
    {
        if (isGrounded) return;
        
        Vector3 vel = rb.linearVelocity;
        
        if (vel.y < 0f)
        {
            // Falling: apply extra gravity for snappy descent
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (vel.y > 0f)
        {
            // Ascending: apply extra gravity for snappy ascent (removes floaty feel)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (riseGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }
    
    /// <summary>
    /// Apply air steering: influence-based trajectory shaping instead of speed capping.
    /// This allows the player to shape their arc while preserving takeoff momentum.
    /// Prevents instant 180° reversals via turn rate limiting and anti-reversal penalty.
    /// 
    /// REPLACES the old approach of `effectiveSpeed *= airControlPercent` which simply
    /// reduced airborne movement speed. That method was removed because it made air
    /// movement feel sluggish and didn't properly preserve jump momentum.
    /// </summary>
    /// <param name="inputDir3D">Desired movement direction in world space (normalized, planar)</param>
    /// <param name="targetSpeed">Target speed based on walk/sprint state</param>
    private void ApplyAirSteering(Vector3 inputDir3D, float targetSpeed)
    {
        Vector3 currentPlanar = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 desiredPlanar = inputDir3D * targetSpeed;
        
        // --- Calculate steering strength ---
        float steerMul = airControlPercent;
        
        // Reduce steering during momentum lock window (brief period after takeoff)
        float timeSinceTakeoff = Time.time - lastTakeoffTime;
        if (timeSinceTakeoff < momentumLockTimeAfterTakeoff)
        {
            steerMul *= momentumLockSteerMultiplier;
        }
        
        // --- Anti-reversal penalty ---
        // When input direction opposes current momentum, reduce steering to prevent instant reversals
        const float minMagnitude = 0.01f;
        if (currentPlanar.sqrMagnitude > minMagnitude && desiredPlanar.sqrMagnitude > minMagnitude)
        {
            float dot = Vector3.Dot(currentPlanar.normalized, desiredPlanar.normalized);
            if (dot < oppositionThreshold)
            {
                steerMul *= backpedalPenaltyMul;
                
                if (enableDebugLogs && Time.frameCount % 30 == 0)
                    Debug.Log($"[PlayerMovement3D] Air reversal penalty applied: dot={dot:F2}, steerMul={steerMul:F2}");
            }
        }
        
        // --- Apply acceleration-limited steering ---
        Vector3 delta = desiredPlanar - currentPlanar;
        float maxDelta = airSteerAcceleration * steerMul * Time.fixedDeltaTime;
        Vector3 accelStep = Vector3.ClampMagnitude(delta, maxDelta);
        Vector3 newPlanar = currentPlanar + accelStep;
        
        // --- Apply turn rate cap (prevents sharp direction snapping) ---
        if (currentPlanar.sqrMagnitude > minMagnitude && newPlanar.sqrMagnitude > minMagnitude)
        {
            float maxTurnRadians = maxAirTurnRateDegPerSec * Mathf.Deg2Rad * Time.fixedDeltaTime;
            // RotateTowards rotates currentPlanar toward newPlanar direction, capped by maxTurnRadians
            // The magnitude delta is capped by maxDelta (last parameter)
            newPlanar = Vector3.RotateTowards(currentPlanar, newPlanar, maxTurnRadians, maxDelta);
        }
        
        // Apply new planar velocity, preserve Y
        rb.linearVelocity = new Vector3(newPlanar.x, rb.linearVelocity.y, newPlanar.z);
        rb.angularVelocity = Vector3.zero;
    }
    
    /// <summary>
    /// Handle landing logic.
    /// </summary>
    private void HandleLanding()
    {
        isJumping = false;
        jumpStartTime = -1f; // Reset jump start time
        
        // Calculate fall height and trigger impact effects if needed
        if (isTrackingFall)
        {
            // Fall height = highest point reached during fall - landing position
            // This gives us the total distance fallen from the peak
            float fallHeight = maxFallHeight - transform.position.y;
            TriggerFallImpact(fallHeight);
            isTrackingFall = false;
            maxFallHeight = 0f;
            fallStartHeight = 0f;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("[PlayerMovement3D] Landed!");
        }
    }
    
    #endregion
    
    #region Velocity Handling
    
    /// <summary>
    /// Handle drift prevention for grounded movement only.
    /// Airborne velocity handling is now done by ApplyAirSteering().
    /// 
    /// NOTE: The old HandleVelocityClamping() method was split - airborne damping logic
    /// was REMOVED because it conflicted with the new momentum-preserving air steering.
    /// </summary>
    private void HandleVelocityClampingGrounded(Vector2 move, Vector3 intendedVelocity)
    {
        Vector3 currentVelocity = rb.linearVelocity;
        
        // If squatting, allow controlled slowdown but don't hard-zero
        // (momentum is already captured in HandleJumpInput for takeoff calculation)
        if (isSquatting)
        {
            // Smoothly decelerate during squat instead of hard zeroing
            // This feels more natural and the momentum snapshot is already captured
            Vector3 planarVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            if (planarVel.magnitude > 0.1f)
            {
                // Rapid but smooth deceleration
                planarVel *= 0.85f;
                rb.linearVelocity = new Vector3(planarVel.x, currentVelocity.y, planarVel.z);
            }
            else
            {
                rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            }
            rb.angularVelocity = Vector3.zero;
            return;
        }
        
        if (move.magnitude < inputDeadzone)
        {
            // Grounded with no input: zero planar velocity to prevent drift
            Vector3 planarVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            
            if (planarVelocity.magnitude > 0.01f)
            {
                rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
                
                if (logDriftDetection)
                {
                    Debug.LogWarning($"[PlayerMovement3D] Drift detected! Zeroed planar velocity: {planarVelocity}");
                }
            }
            
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            // Has input: enforce intended planar velocity (deterministic movement)
            // Preserve Y velocity for any vertical motion
            Vector3 enforcedVelocity = new Vector3(intendedVelocity.x, currentVelocity.y, intendedVelocity.z);
            rb.linearVelocity = enforcedVelocity;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private Vector3 ResolveNpcBlockedPosition(Vector3 desiredDisplacement)
    {
        if (!respectNPCBlockers || desiredDisplacement.sqrMagnitude <= 0.000001f)
            return rb.position + desiredDisplacement;

        Collider activePlayerCollider = GetActivePlayerSolidCollider();
        if (!TryGetPlanarBlockCircle(activePlayerCollider, out Vector2 currentCenter, out float playerRadius))
            return rb.position + desiredDisplacement;

        NpcBlockerBuffer.Clear();
        foreach (Collider npcCollider in CharacterCollisionRegistry.NpcSolidColliders)
        {
            if (npcCollider == null || npcCollider == activePlayerCollider || !npcCollider.enabled || npcCollider.isTrigger)
                continue;

            if (!npcCollider.gameObject.activeInHierarchy)
                continue;

            NpcBlockerBuffer.Add(npcCollider);
        }

        if (NpcBlockerBuffer.Count == 0)
            return rb.position + desiredDisplacement;

        Vector2 currentRoot = new Vector2(rb.position.x, rb.position.z);
        Vector2 remaining = new Vector2(desiredDisplacement.x, desiredDisplacement.z);

        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (remaining.sqrMagnitude <= 0.000001f)
                break;

            float bestT = 1f;
            Vector2 bestNormal = Vector2.zero;
            bool hitBlocker = false;
            Vector2 desiredCenter = currentCenter + remaining;

            for (int i = 0; i < NpcBlockerBuffer.Count; i++)
            {
                Collider npcCollider = NpcBlockerBuffer[i];
                if (!TryGetPlanarBlockCircle(npcCollider, out Vector2 blockerCenter, out float blockerRadius))
                    continue;

                float combinedRadius = playerRadius + blockerRadius + Mathf.Max(0f, npcBlockSeparationBuffer);
                if (!TrySweepCircleAgainstCircle(currentCenter, desiredCenter, blockerCenter, combinedRadius, out float hitT, out Vector2 hitNormal))
                    continue;

                if (hitT < bestT)
                {
                    bestT = hitT;
                    bestNormal = hitNormal;
                    hitBlocker = true;
                }
            }

            if (!hitBlocker)
            {
                currentRoot += remaining;
                break;
            }

            float remainingMagnitude = remaining.magnitude;
            float safeT = bestT;
            if (remainingMagnitude > 0.0001f && npcBlockSkin > 0f)
                safeT = Mathf.Max(0f, bestT - (npcBlockSkin / remainingMagnitude));

            Vector2 travel = remaining * Mathf.Clamp01(safeT);
            currentRoot += travel;
            currentCenter += travel;

            Vector2 leftover = remaining - travel;
            if (leftover.sqrMagnitude <= 0.000001f)
                break;

            Vector3 leftover3 = new Vector3(leftover.x, 0f, leftover.y);
            Vector3 bestNormal3 = new Vector3(bestNormal.x, 0f, bestNormal.y);
            Vector3 slide3 = Vector3.ProjectOnPlane(leftover3, bestNormal3);
            remaining = new Vector2(slide3.x, slide3.z);
        }

        return new Vector3(currentRoot.x, rb.position.y + desiredDisplacement.y, currentRoot.y);
    }

    private Collider GetActivePlayerSolidCollider()
    {
        if (usingFallingCollider && fallingCollider != null && fallingCollider.enabled && !fallingCollider.isTrigger)
            return fallingCollider;

        if (playerCollider != null && playerCollider.enabled && !playerCollider.isTrigger)
            return playerCollider;

        return null;
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

    private static bool TrySweepCircleAgainstCircle(
        Vector2 start,
        Vector2 end,
        Vector2 blockerCenter,
        float blockerRadius,
        out float hitT,
        out Vector2 hitNormal)
    {
        hitT = 0f;
        hitNormal = Vector2.zero;

        Vector2 delta = end - start;
        Vector2 relativeStart = start - blockerCenter;
        float radiusSq = blockerRadius * blockerRadius;
        float c = relativeStart.sqrMagnitude - radiusSq;

        if (c <= 0f)
        {
            hitNormal = relativeStart.sqrMagnitude > 0.0001f
                ? relativeStart.normalized
                : (delta.sqrMagnitude > 0.0001f ? -delta.normalized : Vector2.up);
            return true;
        }

        float a = delta.sqrMagnitude;
        if (a <= 0.000001f)
            return false;

        float b = 2f * Vector2.Dot(relativeStart, delta);
        float discriminant = (b * b) - (4f * a * c);
        if (discriminant < 0f)
            return false;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float candidate = (-b - sqrtDiscriminant) / (2f * a);
        if (candidate < 0f || candidate > 1f)
            return false;

        Vector2 hitPoint = start + (delta * candidate);
        Vector2 normal = hitPoint - blockerCenter;
        hitT = Mathf.Clamp01(candidate);
        hitNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : (delta.sqrMagnitude > 0.0001f ? -delta.normalized : Vector2.up);
        return true;
    }
    
    #endregion
    
    #region Input Handling
    
    /// <summary>
    /// Get movement input from various sources.
    /// </summary>
    private Vector2 GetMovementInput()
    {
        Vector2 move = movementInput;
        
        // Priority: Mobile > Input System
        MobileInputManager mobileInput = MobileInput;
        if (mobileInput != null && mobileInput.IsMobileControlsActive)
        {
            move = mobileInput.GetMovementInput();
        }
        else
        {
            if (useFallbackKeyReading)
            {
                move = ReadMovementFromIndividualKeys();
            }
            else if (moveAction != null && moveAction.enabled)
            {
                try
                {
                    Vector2 directInput = moveAction.ReadValue<Vector2>();
                    if (directInput.magnitude > 0.01f || movementInput.magnitude < 0.01f)
                    {
                        move = directInput;
                    }
                }
                catch (System.InvalidOperationException) { }
            }
        }
        
        return move;
    }
    
    /// <summary>
    /// Get sprint input from various sources.
    /// </summary>
    private bool GetSprintInput()
    {
        MobileInputManager mobileInput = MobileInput;
        if (mobileInput != null && mobileInput.IsMobileControlsActive)
        {
            return mobileInput.IsSprinting();
        }
        
        if (sprintAction != null && sprintAction.enabled)
        {
            try
            {
                return sprintAction.ReadValue<float>() > 0.5f;
            }
            catch (System.InvalidOperationException)
            {
                return isSprinting;
            }
        }
        
        return isSprinting;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        try
        {
            movementInput = context.ReadValue<Vector2>();
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Move input: {movementInput}");
        }
        catch (System.InvalidOperationException ex)
        {
            // Fallback: Enable direct key reading if composite binding is missing
            Debug.LogWarning($"[PlayerMovement3D] Error reading move input: {ex.Message}. Using fallback direct key reading. Please configure Move action with a Composite binding (2D Vector Composite).");
            useFallbackKeyReading = true;
            // Don't set movementInput here - let FixedUpdate handle it
        }
    }

    /// <summary>
    /// Fallback method to read movement from individual keys directly.
    /// This is a workaround - the proper solution is to use a Composite binding in Input Actions.
    /// </summary>
    private Vector2 ReadMovementFromIndividualKeys()
    {
        Vector2 move = Vector2.zero;

        // Read keys directly using Keyboard.current (safer than reading from action bindings)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                move.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                move.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                move.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                move.x += 1f;
        }

        // Normalize to prevent diagonal speed boost
        if (move.magnitude > 1f)
            move = move.normalized;

        return move;
    }

    private void OnStop(InputAction.CallbackContext context)
    {
        try
        {
            movementInput = Vector2.zero;
        }
        catch (System.InvalidOperationException ex)
        {
            Debug.LogWarning($"[PlayerMovement3D] Error in OnStop callback: {ex.Message}");
            movementInput = Vector2.zero; // Safe to set directly
        }
    }

    private void OnSprintStart(InputAction.CallbackContext context)
    {
        try
        {
            isSprinting = true;
            // Camera mode is now set in FixedUpdate based on input intent
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Sprint started");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerMovement3D] Error in OnSprintStart: {ex.Message}");
        }
    }

    private void OnSprintEnd(InputAction.CallbackContext context)
    {
        try
        {
            isSprinting = false;
            // Camera mode is now set in FixedUpdate based on input intent
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Sprint ended");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerMovement3D] Error in OnSprintEnd: {ex.Message}");
        }
    }

    /// <summary>
    /// Current planar speed. Serialized moveSpeed is the unmodified baseline;
    /// sprint and every registered IPlayerMovementModifier scale it.
    /// </summary>
    public float GetCurrentSpeed()
    {
        return GetEffectiveMoveSpeed(isSprinting);
    }

    /// <summary>
    /// Register a stackable movement modifier. Values are sampled live each tick.
    /// </summary>
    public void RegisterMovementModifier(IPlayerMovementModifier modifier)
    {
        if (modifier == null || movementModifiers.Contains(modifier))
            return;
        movementModifiers.Add(modifier);
    }

    /// <summary>
    /// Remove a previously registered movement modifier.
    /// </summary>
    public void UnregisterMovementModifier(IPlayerMovementModifier modifier)
    {
        if (modifier == null)
            return;
        movementModifiers.Remove(modifier);
    }

    private float GetEffectiveMoveSpeed(bool sprinting)
    {
        // moveSpeed stays at the authored baseline. Buffs live in movementModifiers.
        float speed = sprinting ? moveSpeed * sprintSpeedMultiplier : moveSpeed;
        return speed * GetExternalSpeedMultiplier();
    }

    private void EnsureSpeedModifierStack()
    {
        if (GetComponent<PlayerMovementSpeedModifierStack>() != null)
            return;
        gameObject.AddComponent<PlayerMovementSpeedModifierStack>();
    }

    private float GetExternalSpeedMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < movementModifiers.Count; i++)
        {
            if (movementModifiers[i] != null)
                multiplier *= movementModifiers[i].MovementSpeedMultiplier;
        }
        return multiplier;
    }

    private float GetExternalJumpMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < movementModifiers.Count; i++)
        {
            if (movementModifiers[i] != null)
                multiplier *= movementModifiers[i].JumpImpulseMultiplier;
        }
        return multiplier;
    }

    /// <summary>
    /// Check if player is currently sprinting.
    /// </summary>
    public bool IsSprinting => isSprinting;

    /// <summary>
    /// Handle interact input (Space key by default).
    /// </summary>
    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        HandleJumpPressed();
    }

    private void OnMobileJumpPressed()
    {
        HandleJumpPressed();
    }

    private void HandleJumpPressed()
    {
        if (!enableJump) return;

        try
        {
            jumpInputHeld = true;
            lastJumpInputTime = Time.time;
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Jump input pressed");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerMovement3D] Error in OnJumpPressed: {ex.Message}");
        }
    }
    
    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        HandleJumpReleased();
    }

    private void OnMobileJumpReleased()
    {
        HandleJumpReleased();
    }

    private void HandleJumpReleased()
    {
        try
        {
            jumpInputHeld = false;
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Jump input released");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerMovement3D] Error in OnJumpReleased: {ex.Message}");
        }
    }
    
    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Check input lock service (interaction lock)
            InputLockService inputLock = InputLockService.Instance;
            if (inputLock != null && inputLock.IsInteractionLocked)
            {
                if (enableDebugLogs)
                    Debug.Log("[PlayerMovement3D] Interact blocked by InputLockService");
                return;
            }
            
            OnInteract?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Interact pressed");
        }
    }
    
    #endregion
    
    #region Freeze State
    
    /// <summary>
    /// Handle frozen state - either preserve physics (soft freeze) or zero velocity (hard freeze).
    /// Called from FixedUpdate when frozen.
    /// </summary>
    private void HandleFrozenState()
    {
        if (preservePhysicsDuringFreeze)
        {
            // SOFT FREEZE: Let physics continue naturally (preserves jump arcs)
            // Only apply gravity modifications if jumping
            ApplyJumpGravity();
            
            // Zero angular velocity to prevent spinning
            rb.angularVelocity = Vector3.zero;
            
            // Only zero planar velocity when grounded (prevents drift after landing)
            if (isGrounded)
            {
                Vector3 currentVelocity = rb.linearVelocity;
                if (Mathf.Abs(currentVelocity.x) > 0.01f || Mathf.Abs(currentVelocity.z) > 0.01f)
                {
                    rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
                }
            }
            
            // Update animation based on current state (show jump/fall if airborne)
            if (meshAnimationController != null)
            {
                bool isJumpingState = !isGrounded;
                meshAnimationController.UpdateAnimation(Vector2.zero, false, false, isJumpingState);
            }
        }
        else
        {
            // HARD FREEZE: Zero all planar velocity immediately (for cutscenes/dialogues)
            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
            
            // Update animation to idle when frozen (preserving direction)
            if (meshAnimationController != null)
            {
                meshAnimationController.UpdateAnimation(Vector2.zero, false, false, false);
            }
        }
        
        // Update camera to idle
        if (cameraController != null)
        {
            cameraController.SetMovementMode(CameraMovementMode.Idle);
        }
    }
    
    /// <summary>
    /// Set the frozen state. When frozen, player cannot process new movement input.
    /// Uses reference counting to handle overlapping freeze requests.
    /// </summary>
    /// <param name="frozen">True to freeze, false to unfreeze</param>
    /// <param name="preservePhysics">If true, physics continues (soft freeze for camera transitions). 
    /// If false, velocity is zeroed (hard freeze for cutscenes).</param>
    public void SetFrozen(bool frozen, bool preservePhysics = true)
    {
        if (frozen)
        {
            freezeReferenceCount++;
            isFrozen = true;
            preservePhysicsDuringFreeze = preservePhysics;
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Frozen (ref count: {freezeReferenceCount}, preserve physics: {preservePhysics})");
        }
        else
        {
            freezeReferenceCount = Mathf.Max(0, freezeReferenceCount - 1);
            isFrozen = freezeReferenceCount > 0;
            
            // If fully unfrozen, reset preserve physics flag to default
            if (!isFrozen)
            {
                preservePhysicsDuringFreeze = true;
            }
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerMovement3D] Unfreeze requested (ref count: {freezeReferenceCount}, still frozen: {isFrozen})");
        }
    }
    
    /// <summary>
    /// Force unfreeze regardless of reference count.
    /// Use sparingly - prefer SetFrozen(false) for proper reference counting.
    /// </summary>
    public void ForceUnfreeze()
    {
        freezeReferenceCount = 0;
        isFrozen = false;
        preservePhysicsDuringFreeze = true;
        
        if (enableDebugLogs)
            Debug.Log("[PlayerMovement3D] Force unfrozen");
    }
    
    /// <summary>
    /// Check if player is currently frozen.
    /// </summary>
    public bool IsFrozen => isFrozen;
    
    /// <summary>
    /// Check if physics is being preserved during freeze (soft freeze mode).
    /// </summary>
    public bool IsPhysicsPreservedDuringFreeze => preservePhysicsDuringFreeze;
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Check if player is currently grounded.
    /// </summary>
    public bool IsGrounded => isGrounded;
    
    /// <summary>
    /// Get the ground check distance.
    /// </summary>
    public float GroundCheckDistance => groundCheckDistance;
    
    /// <summary>
    /// Get the ground check radius.
    /// </summary>
    public float GroundCheckRadius => groundCheckRadius;
    
    /// <summary>
    /// Get the current ground check offset (camera-yaw adjusted).
    /// </summary>
    public Vector3 GroundCheckOffset => currentGroundCheckOffset;
    
    /// <summary>
    /// Get the ground layers mask.
    /// </summary>
    public LayerMask GroundLayers => groundLayers;
    
    /// <summary>
    /// Get the last ground hit info (valid when grounded).
    /// </summary>
    public RaycastHit LastGroundHit => groundHit;
    
    /// <summary>
    /// Check if player is currently in a jump.
    /// </summary>
    public bool IsJumping => isJumping;
    
    /// <summary>
    /// Check if player is airborne (not grounded).
    /// </summary>
    public bool IsAirborne => !isGrounded;
    
    /// <summary>
    /// Get the current Y velocity (useful for animation blending).
    /// </summary>
    public float VerticalVelocity => rb != null ? rb.linearVelocity.y : 0f;
    
    #endregion
    
    #region Collider Adjustment for Camera Rotation
    
    /// <summary>
    /// Update the capsule collider center based on camera yaw index.
    /// Uses manual XZ offsets from playerColliderXZOffsets array.
    /// </summary>
    /// <param name="yawIndex90">Camera yaw index (0-3)</param>
    public void UpdateColliderForCameraYaw(int yawIndex90)
    {
        if (playerCollider == null)
        {
            Debug.LogWarning("[PlayerMovement3D] Cannot update collider - playerCollider is null");
            return;
        }
        
        // Normalize yaw index to 0-3
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;
        
        // Ensure array is valid
        if (playerColliderXZOffsets == null || playerColliderXZOffsets.Length < 4)
        {
            Debug.LogWarning("[PlayerMovement3D] playerColliderXZOffsets array is invalid. Using default offsets.");
            playerColliderXZOffsets = new Vector2[]
            {
                new Vector2(0f, -1.1f),
                new Vector2(-1.1f, 0f),
                new Vector2(0f, 1.1f),
                new Vector2(1.1f, 0f)
            };
        }
        
        // Get XZ offset from array
        Vector2 xzOffset = playerColliderXZOffsets[yawIndex90];
        Vector3 newCenter = new Vector3(xzOffset.x, colliderYCenter, xzOffset.y);
        playerCollider.center = newCenter;
        
        // Update normal capsule center for transition system
        normalCapsuleCenter = newCenter;
        
        // Update player visual position for camera yaw
        if (playerVisual != null)
        {
            playerVisual.UpdateVisualForCameraYaw(yawIndex90);
        }
        
        // Update player shadow position for camera yaw
        if (playerShadow != null)
        {
            playerShadow.UpdateShadowForCameraYaw(yawIndex90);
        }
        
        // Update occlusion silhouette for camera yaw
        if (occlusionSilhouette != null)
        {
            occlusionSilhouette.UpdateSilhouetteForCameraYaw(yawIndex90);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Updated collider center for yaw index {yawIndex90}: {newCenter}");
        }
    }
    
    /// <summary>
    /// Get current camera yaw index being used for collider offset.
    /// </summary>
    public int CurrentColliderYawIndex => currentCameraYawIndex;
    
    /// <summary>
    /// Refresh the camera controller reference. Call this when the player persists
    /// across scenes and needs to reference a new camera controller.
    /// </summary>
    public void RefreshCameraController()
    {
        // Find new camera controller in current scene
        var newCameraController = SceneCameraResolver.ResolveGameplayCameraController();
        
        if (newCameraController != null)
        {
            bool wasNull = cameraController == null;
            bool wasDifferent = !wasNull && cameraController != newCameraController;
            
            cameraController = newCameraController;

            if (playerShadow == null)
                playerShadow = GetComponentInChildren<PlayerShadowCaster>(true);
            playerShadow?.SetCameraController(newCameraController);
            
            if (enableDebugLogs)
            {
                if (wasNull)
                    Debug.Log($"[PlayerMovement3D] Camera controller assigned: {newCameraController.gameObject.name}");
                else if (wasDifferent)
                    Debug.Log($"[PlayerMovement3D] Camera controller updated to: {newCameraController.gameObject.name}");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[PlayerMovement3D] RefreshCameraController: No TopDownCameraController3D found in scene");
        }
    }
    
    /// <summary>
    /// Refresh all components after scene transition.
    /// Call this when player persists across scenes to ensure all references are valid.
    /// </summary>
    public void RefreshAfterSceneTransition()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[PlayerMovement3D] Refreshing components after scene transition");
        }
        
        // Refresh camera controller
        RefreshCameraController();
        
        // Refresh landing dust spawner (cleans up destroyed particles)
        if (landingDustSpawner != null)
        {
            landingDustSpawner.RefreshAfterSceneTransition();
        }
        else
        {
            // Re-find if lost
            landingDustSpawner = GetComponentInChildren<LandingDustSpawner>();
            if (landingDustSpawner != null)
            {
                landingDustSpawner.RefreshAfterSceneTransition();
            }
        }
        
        // Ensure landing impact coroutine is cleared (in case it was running during transition)
        if (landingImpactCoroutine != null)
        {
            StopCoroutine(landingImpactCoroutine);
            landingImpactCoroutine = null;
        }
        
        // Reset fall tracking state
        isTrackingFall = false;
        maxFallHeight = 0f;
        fallStartHeight = 0f;
        
        // Reset jump momentum state
        preJumpPlanarVel = Vector3.zero;
        squatStartTime = 0f;
        capturedPreJumpVel = false;
        lastTakeoffTime = 0f;
        jumpStartTime = -1f;
        isSquatting = false;
        isJumping = false;
        
        // Reset slope detection state
        currentGroundNormal = Vector3.up;
        
        // NOTE: Do NOT ForceUnfreeze here. SceneLoader's transition hold intentionally keeps
        // the player frozen until the loading overlay has faded out. Unfreezing here would let
        // the player move while the loading screen is still visible. SceneLoader.EndSceneTransitionHold
        // handles the unfreeze at the correct time.

        SyncIgnoredNpcCollisions();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Scene transition refresh complete (frozen: {isFrozen}, refCount: {freezeReferenceCount})");
        }
    }
    
    /// <summary>
    /// Set the camera controller reference directly.
    /// </summary>
    public void SetCameraController(TopDownCameraController3D newCameraController)
    {
        if (newCameraController != null)
        {
            cameraController = newCameraController;

            if (playerShadow == null)
                playerShadow = GetComponentInChildren<PlayerShadowCaster>(true);
            playerShadow?.SetCameraController(newCameraController);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerMovement3D] Camera controller set to: {newCameraController.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Get current camera controller reference.
    /// </summary>
    public TopDownCameraController3D CameraController => cameraController;
    
    /// <summary>
    /// Update the ground check offset based on camera yaw index.
    /// Uses manual XZ offsets from groundCheckXZOffsets array.
    /// </summary>
    /// <param name="yawIndex90">Camera yaw index (0-3)</param>
    public void UpdateGroundCheckForCameraYaw(int yawIndex90)
    {
        // Normalize yaw index to 0-3
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        // Ensure array is valid
        if (groundCheckXZOffsets == null || groundCheckXZOffsets.Length < 4)
        {
            Debug.LogWarning("[PlayerMovement3D] groundCheckXZOffsets array is invalid. Using default offsets.");
            groundCheckXZOffsets = new Vector2[]
            {
                new Vector2(0f, -2f),
                new Vector2(-2f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 0f)
            };
        }
        
        // Get XZ offset from array
        Vector2 xzOffset = groundCheckXZOffsets[yawIndex90];
        
        // Combine Y offset with camera-relative XZ offset
        currentGroundCheckOffset = new Vector3(xzOffset.x, groundCheckYOffset, xzOffset.y);
        
        // Update falling collider position to match
        UpdateFallingColliderPosition();
        
        // Update ground check capsule center for when we switch colliders
        groundCheckCapsuleCenter = new Vector3(xzOffset.x, colliderYCenter, xzOffset.y);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Updated ground check offset for yaw index {yawIndex90}: {currentGroundCheckOffset}");
        }
    }
    
    #endregion
    
    #region Falling Collider (Ledge Physics)
    
    /// <summary>
    /// Setup the falling collider for ledge physics.
    /// Creates one if autoCreateFallingCollider is enabled and none is assigned.
    /// </summary>
    private void SetupFallingCollider()
    {
        if (!useVisualGroundCheck || !swapCollidersOnLedge)
            return;
        
        // Auto-create falling collider if needed
        if (fallingCollider == null && autoCreateFallingCollider)
        {
            // Create child object for falling collider
            GameObject fallingColliderObj = new GameObject("FallingCollider");
            fallingColliderObj.transform.SetParent(transform);
            fallingColliderObj.transform.localPosition = Vector3.zero;
            fallingColliderObj.layer = gameObject.layer; // Same layer as player
            
            // Add sphere collider
            fallingCollider = fallingColliderObj.AddComponent<SphereCollider>();
            fallingCollider.radius = fallingColliderRadius;
            fallingCollider.center = new Vector3(0f, fallingColliderYOffset, 0f);
            
            // IMPORTANT: Use the same physics material as the main collider
            // This ensures zero friction and zero bounciness, preventing mid-air sticking
            if (playerCollider != null && playerCollider.material != null)
            {
                fallingCollider.material = playerCollider.material;
            }
            else
            {
                // Try to load the default player physics material
                PhysicsMaterial playerPhysMat = Resources.Load<PhysicsMaterial>("Player_Physicas");
                if (playerPhysMat != null)
                {
                    fallingCollider.material = playerPhysMat;
                }
                else
                {
                    Debug.LogWarning("[PlayerMovement3D] Could not find Player_Physicas physics material. Falling collider may have friction issues.");
                }
            }
            
            // Ensure falling collider is NOT a trigger (needs to collide with walls)
            fallingCollider.isTrigger = false;
            
            if (enableDebugLogs)
                Debug.Log("[PlayerMovement3D] Auto-created falling collider with physics material");
        }
        
        // Ensure physics material is set even if collider was manually assigned
        if (fallingCollider != null)
        {
            if (fallingCollider.material == null && playerCollider != null && playerCollider.material != null)
            {
                fallingCollider.material = playerCollider.material;
            }
            
            // Initially disable falling collider (capsule is primary)
            fallingCollider.enabled = false;
            usingFallingCollider = false;
        }
    }
    
    /// <summary>
    /// Switch from capsule collider to falling sphere collider.
    /// Called when visual is detected over a void (ledge).
    /// 
    /// Before disabling capsule, we move its center to match the ground check XZ position.
    /// This way, when we re-enable it after landing, it starts at the safe "feet" position
    /// and can smoothly transition back to normal, letting physics push it out of walls if needed.
    /// </summary>
    private void SwitchToFallingCollider()
    {
        if (fallingCollider == null || playerCollider == null)
            return;
        
        // Stop any ongoing transition
        if (capsuleCenterTransitionCoroutine != null)
        {
            StopCoroutine(capsuleCenterTransitionCoroutine);
            capsuleCenterTransitionCoroutine = null;
        }
        
        // Store the normal capsule center before modifying it
        normalCapsuleCenter = playerCollider.center;
        
        // Calculate ground check capsule center using falling collider XZ offsets
        int yawIndex = currentCameraYawIndex;
        if (fallingColliderXZOffsets != null && fallingColliderXZOffsets.Length > yawIndex)
        {
            Vector2 xzOffset = fallingColliderXZOffsets[yawIndex];
            groundCheckCapsuleCenter = new Vector3(xzOffset.x, colliderYCenter, xzOffset.y);
        }
        else
        {
            // Fallback to current ground check offset
            groundCheckCapsuleCenter = new Vector3(
                currentGroundCheckOffset.x,
                colliderYCenter,
                currentGroundCheckOffset.z
            );
        }
        
        // Move capsule center to ground check position before disabling
        // This ensures when we re-enable it, it starts at a safe position
        playerCollider.center = groundCheckCapsuleCenter;
        
        // Disable capsule, enable sphere
        playerCollider.enabled = false;
        fallingCollider.enabled = true;
        usingFallingCollider = true;
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement3D] Switched to falling collider. Capsule center moved to ground check position: {groundCheckCapsuleCenter}");
    }
    
    /// <summary>
    /// Switch from falling sphere collider back to capsule collider.
    /// Called when player lands after falling off a ledge.
    /// 
    /// The capsule collider's center was set to ground check position when falling started.
    /// We enable it at that safe position, then smoothly transition the center back to normal.
    /// Unity physics will naturally push the player away from walls if the capsule overlaps geometry.
    /// </summary>
    private void SwitchToCapsuleCollider()
    {
        if (fallingCollider == null || playerCollider == null)
            return;
        
        // Stop any ongoing transition
        if (capsuleCenterTransitionCoroutine != null)
        {
            StopCoroutine(capsuleCenterTransitionCoroutine);
            capsuleCenterTransitionCoroutine = null;
        }
        
        // Capsule center is already at groundCheckCapsuleCenter (set when we started falling)
        // Just enable it - it will spawn at the safe "feet" position
        playerCollider.enabled = true;
        fallingCollider.enabled = false;
        usingFallingCollider = false;
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement3D] Capsule re-enabled at ground check position: {playerCollider.center}");
        
        // Start smooth transition back to normal center position
        // Physics will naturally push the player out of any overlapping geometry
        capsuleCenterTransitionCoroutine = StartCoroutine(TransitionCapsuleCenterToNormal());
    }
    
    /// <summary>
    /// Smoothly transitions capsule collider center from ground check position back to normal.
    /// This allows Unity physics to naturally push the player out of walls if needed.
    /// </summary>
    private System.Collections.IEnumerator TransitionCapsuleCenterToNormal()
    {
        if (playerCollider == null)
            yield break;
        
        Vector3 startCenter = playerCollider.center;
        Vector3 endCenter = normalCapsuleCenter;
        
        // If transition time is very small or zero, just snap
        if (capsuleCenterTransitionTime <= 0.001f)
        {
            playerCollider.center = endCenter;
            capsuleCenterTransitionCoroutine = null;
            yield break;
        }
        
        float elapsed = 0f;
        
        while (elapsed < capsuleCenterTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / capsuleCenterTransitionTime);
            
            // Linear interpolation - physics handles the pushing
            playerCollider.center = Vector3.Lerp(startCenter, endCenter, t);
            
            yield return null;
        }
        
        // Ensure we end exactly at target
        playerCollider.center = endCenter;
        capsuleCenterTransitionCoroutine = null;
        
        if (enableDebugLogs)
            Debug.Log($"[PlayerMovement3D] Capsule center transitioned to normal: {endCenter}");
    }
    
    /// <summary>
    /// Update the falling collider position to match the ground check position (visual feet).
    /// Uses manual XZ offsets from fallingColliderXZOffsets array.
    /// </summary>
    private void UpdateFallingColliderPosition()
    {
        if (fallingCollider == null)
            return;
        
        // Get current camera yaw index
        int yawIndex = currentCameraYawIndex;
        
        // Ensure array is valid
        if (fallingColliderXZOffsets == null || fallingColliderXZOffsets.Length < 4)
        {
            Debug.LogWarning("[PlayerMovement3D] fallingColliderXZOffsets array is invalid. Using default offsets.");
            fallingColliderXZOffsets = new Vector2[]
            {
                new Vector2(0f, -2f),
                new Vector2(-2f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 0f)
            };
        }
        
        // Get XZ offset from array
        Vector2 xzOffset = fallingColliderXZOffsets[yawIndex];
        
        // Update falling collider's local position
        // The center Y offset is already set in the collider itself (fallingColliderYOffset)
        fallingCollider.transform.localPosition = new Vector3(xzOffset.x, 0f, xzOffset.y);
        
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[PlayerMovement3D] Falling collider position updated for yaw {yawIndex}: ({xzOffset.x}, {xzOffset.y})");
        }
    }
    
    /// <summary>
    /// Check if currently using the falling collider (visual over void).
    /// </summary>
    public bool IsUsingFallingCollider => usingFallingCollider;
    
    /// <summary>
    /// Check if the visual is currently over a void (no ground).
    /// </summary>
    public bool IsVisualOverVoid => isVisualOverVoid;
    
    /// <summary>
    /// Get the player's capsule collider.
    /// </summary>
    public CapsuleCollider PlayerCollider => playerCollider;
    
    /// <summary>
    /// Get the player's falling collider.
    /// </summary>
    public SphereCollider FallingCollider => fallingCollider;
    
    /// <summary>
    /// Get the player visual component.
    /// </summary>
    public MeshRendererPlayerVisual PlayerVisual => playerVisual;
    
    #endregion
    
    #region Fall Impact (Camera Shake & Landing Animation)
    
    /// <summary>
    /// Trigger fall impact effects (camera shake + landing animation lock).
    /// </summary>
    private void TriggerFallImpact(float fallHeight)
    {
        if (!enableFallImpact)
            return;
        
        if (fallHeight < minFallHeightForImpact)
            return; // Too small to trigger impact
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Fall impact triggered! Fall height: {fallHeight:F2}");
        }
        
        // Stop any existing landing impact coroutine
        if (landingImpactCoroutine != null)
        {
            StopCoroutine(landingImpactCoroutine);
        }
        
        // Start landing impact sequence
        landingImpactCoroutine = StartCoroutine(LandingImpactCoroutine(fallHeight));
    }
    
    /// <summary>
    /// Coroutine that handles freeze, camera shake, and landing animation lock.
    /// </summary>
    private IEnumerator LandingImpactCoroutine(float fallHeight)
    {
        // Calculate impact intensity and duration
        float shakeIntensity;
        float shakeDuration;
        float landingLockDuration;
        
        if (useDynamicImpact)
        {
            // Dynamic: scale based on fall height
            float normalizedHeight = Mathf.Clamp01((fallHeight - minFallHeightForImpact) / (maxFallHeightForImpact - minFallHeightForImpact));
            shakeIntensity = Mathf.Lerp(minShakeIntensity, maxShakeIntensity, normalizedHeight);
            shakeDuration = Mathf.Lerp(minShakeDuration, maxShakeDuration, normalizedHeight);
            landingLockDuration = Mathf.Lerp(minLandingLockDuration, maxLandingLockDuration, normalizedHeight);
        }
        else
        {
            // Static: use fixed values
            shakeIntensity = staticShakeIntensity;
            shakeDuration = staticShakeDuration;
            landingLockDuration = staticLandingLockDuration;
        }
        
        // Calculate landing animation duration and sync lock duration with it
        float landingAnimationDuration = 0f;
        if (playerVisual != null)
        {
            landingAnimationDuration = playerVisual.GetAnimationDuration(
                MeshRendererPlayerVisual.AnimationState.Landing,
                playerVisual.GetDirection() // Use current facing direction
            );
            
            // Use the longer of the configured lock duration or animation duration
            // This ensures the animation completes before unlock, but respects longer freezes for big falls
            if (landingAnimationDuration > 0f)
            {
                landingLockDuration = Mathf.Max(landingLockDuration, landingAnimationDuration);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerMovement3D] Landing animation duration: {landingAnimationDuration:F3}s, Lock duration: {landingLockDuration:F3}s");
                }
            }
        }
        
        // Freeze player (hard freeze - zero velocity)
        SetFrozen(true, false);
        
        // Trigger camera shake
        if (cameraController != null)
        {
            if (useDynamicImpact)
            {
                cameraController.ShakeCameraFromFallHeight(
                    fallHeight,
                    minFallHeightForImpact,
                    maxFallHeightForImpact,
                    minShakeIntensity,
                    maxShakeIntensity,
                    minShakeDuration,
                    maxShakeDuration,
                    shakeFrequency
                );
            }
            else
            {
                cameraController.ShakeCamera(shakeIntensity, shakeDuration, shakeFrequency);
            }
        }
        
        // Spawn dust cloud at landing position
        if (landingDustSpawner != null)
        {
            landingDustSpawner.SpawnDustCloud(transform.position, fallHeight);
        }
        
        // Play landing impact sound (volume scales with fall height)
        if (footstepController != null && footstepController.HasLandingImpactConfig)
        {
            footstepController.PlayLandingImpact(fallHeight);
        }
        
        // Lock animation to Landing state during impact (synchronized with animation duration)
        if (meshAnimationController != null)
        {
            meshAnimationController.LockAnimationState(MeshRendererPlayerVisual.AnimationState.Landing, landingLockDuration);
        }
        else if (playerVisual != null)
        {
            // Fallback: set directly if no controller
            playerVisual.SetAnimationState(MeshRendererPlayerVisual.AnimationState.Landing);
        }
        
        // Wait for landing animation lock duration (now synchronized with animation)
        yield return new WaitForSeconds(landingLockDuration);
        
        // Unfreeze player
        SetFrozen(false, false);
        
        landingImpactCoroutine = null;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement3D] Landing impact complete. Shake: {shakeIntensity:F2} for {shakeDuration:F2}s, Lock: {landingLockDuration:F2}s");
        }
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        if (!drawGroundCheckGizmo) return;
        
        // Use current ground check offset (or calculate from array if not initialized)
        Vector3 checkOffset;
        if (Application.isPlaying && currentGroundCheckOffset != Vector3.zero)
        {
            checkOffset = currentGroundCheckOffset;
        }
        else
        {
            // In editor, use first array element or default
            if (groundCheckXZOffsets != null && groundCheckXZOffsets.Length > 0)
            {
                checkOffset = new Vector3(groundCheckXZOffsets[0].x, groundCheckYOffset, groundCheckXZOffsets[0].y);
            }
            else
            {
                checkOffset = new Vector3(0f, groundCheckYOffset, -2f);
            }
        }
        Vector3 origin = transform.position + checkOffset;
        
        // Apply visual forward offset if enabled
        if (useVisualGroundCheck && cameraController != null)
        {
            Vector3 cameraForward = cameraController.GetCameraForwardXZ();
            origin += cameraForward * visualGroundCheckForwardOffset;
        }
        else if (useVisualGroundCheck)
        {
            // In editor without camera controller, show forward offset along Z
            origin += Vector3.forward * visualGroundCheckForwardOffset;
        }
        
        Vector3 endPoint = origin + Vector3.down * groundCheckDistance;
        
        // Draw ground check sphere
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawLine(origin, endPoint);
        Gizmos.DrawWireSphere(endPoint, groundCheckRadius);
        
        // Draw visual over void indicator
        if (isVisualOverVoid)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin + Vector3.up * 0.5f, 0.2f);
        }
        
        // Draw falling collider if active
        if (fallingCollider != null && usingFallingCollider)
        {
            Gizmos.color = Color.cyan;
            Vector3 fallingPos = fallingCollider.transform.position + fallingCollider.center;
            Gizmos.DrawWireSphere(fallingPos, fallingCollider.radius);
        }
        
        // Draw connection from root to visual position
        if (useVisualGroundCheck)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(transform.position + checkOffset, origin);
        }
    }
    
    #endregion
}
