using UnityEngine;
using System.Collections;

/// <summary>
/// Animation controller for 3D top-down movement using MeshRenderer.
/// Bridges PlayerMovement3D with MeshRendererPlayerVisual.
/// 
/// Features:
/// - Supports idle, walk, and sprint animations
/// - Stardew Valley-style diagonals (horizontal takes priority)
/// - Preserves last facing direction when movement stops
/// - Configurable deadzone for movement detection
/// </summary>
public class PlayerAnimationController3D_Mesh : MonoBehaviour
{
    [Header("References")]
    [Tooltip("MeshRendererPlayerVisual component (on Visual child)")]
    [SerializeField] private MeshRendererPlayerVisual meshVisual;
    
    [Tooltip("PlayerMovement3D component (on PlayerRoot) - optional, only needed for Update() polling")]
    [SerializeField] private PlayerMovement3D playerMovement;
    
    [Header("Movement Detection")]
    [Tooltip("Minimum input magnitude to be considered 'moving'. Below this = idle.")]
    [SerializeField] private float movementDeadzone = 0.1f;
    
    [Header("Direction Settings")]
    [Tooltip("If true, diagonal inputs resolve to left/right (Stardew Valley style)")]
    [SerializeField] private bool useStardewDiagonals = true;
    
    [Tooltip("Minimum input magnitude on an axis to count as moving in that direction")]
    [SerializeField] private float diagonalThreshold = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Last non-zero direction - persists into idle
    private Vector2 lastFacingDirection = Vector2.down;
    
    // Cached state to detect changes
    private MeshRendererPlayerVisual.AnimationState lastState = MeshRendererPlayerVisual.AnimationState.Idle;
    
    // Animation lock (prevents updates during landing impact, etc.)
    private bool isAnimationLocked = false;
    private Coroutine animationLockCoroutine = null;
    private bool hasExternalAnimationLock = false;

    private void Awake()
    {
        // Auto-find components if not assigned
        if (meshVisual == null)
        {
            meshVisual = GetComponentInChildren<MeshRendererPlayerVisual>();
        }
        
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement3D>();
        }
        
        if (meshVisual == null)
        {
            Debug.LogError("[PlayerAnimationController3D_Mesh] MeshRendererPlayerVisual not found!");
        }
    }

    /// <summary>
    /// Update animation based on XZ movement input.
    /// Called from PlayerMovement3D's FixedUpdate.
    /// 
    /// This is the primary method for updating animations.
    /// </summary>
    /// <param name="movementInput">2D vector representing XZ movement (X = X, Y = Z in world space)</param>
    /// <param name="isSprinting">Whether player is currently sprinting</param>
    /// <param name="isSquatting">Whether player is holding jump (squat animation)</param>
    /// <param name="isJumping">Whether player is currently jumping (airborne)</param>
    public void UpdateAnimation(Vector2 movementInput, bool isSprinting, bool isSquatting = false, bool isJumping = false)
    {
        if (meshVisual == null)
            return;
        
        // If animation is locked (e.g., during landing impact), don't update
        if (isAnimationLocked)
            return;
        
        // Priority: Jump > Squat > Movement > Idle
        MeshRendererPlayerVisual.AnimationState targetState;
        
        if (isJumping)
        {
            // Jumping (airborne) - show jump animation
            targetState = MeshRendererPlayerVisual.AnimationState.Jump;
        }
        else if (isSquatting)
        {
            // Holding jump while grounded - show squat animation
            targetState = MeshRendererPlayerVisual.AnimationState.Squat;
        }
        else
        {
            // Normal movement states
            bool isMoving = movementInput.magnitude > movementDeadzone;
            
            if (isMoving)
            {
                // Moving - use Walk or Sprint based on input
                targetState = isSprinting 
                    ? MeshRendererPlayerVisual.AnimationState.Sprint 
                    : MeshRendererPlayerVisual.AnimationState.Walk;
                
                // Update facing direction from movement input
                Vector2 directionToUse = GetDirectionForAnimation(movementInput);
                lastFacingDirection = directionToUse;
                meshVisual.SetDirection(directionToUse);
                
                if (enableDebugLogs && targetState != lastState)
                {
                    Debug.Log($"[AnimController] Moving - Input: {movementInput}, Direction: {directionToUse}, State: {targetState}");
                }
            }
            else
            {
                // Not moving - idle, but keep last facing direction
                targetState = MeshRendererPlayerVisual.AnimationState.Idle;
                
                // IMPORTANT: Don't change direction - keep last facing direction
                meshVisual.SetDirection(lastFacingDirection);
                
                if (enableDebugLogs && targetState != lastState)
                {
                    Debug.Log($"[AnimController] Idle - Facing: {lastFacingDirection}");
                }
            }
        }
        
        // Update state on visual
        meshVisual.SetAnimationState(targetState);
        lastState = targetState;
    }
    
    /// <summary>
    /// Get animation direction from input, applying Stardew Valley diagonal rules.
    /// 
    /// Stardew diagonals: when moving diagonally, horizontal direction takes priority.
    /// This creates the "Stardew Valley" feel where diagonal movement shows left/right sprites.
    /// </summary>
    private Vector2 GetDirectionForAnimation(Vector2 input)
    {
        if (!useStardewDiagonals)
        {
            // No Stardew diagonals - just normalize input
            return input.magnitude > 0.01f ? input.normalized : lastFacingDirection;
        }
        
        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);
        
        bool movingHorizontally = absX > diagonalThreshold;
        bool movingVertically = absY > diagonalThreshold;
        
        if (movingHorizontally && movingVertically)
        {
            // Diagonal movement - use horizontal direction (Stardew style)
            return input.x > 0 ? Vector2.right : Vector2.left;
        }
        else if (movingHorizontally)
        {
            // Pure horizontal
            return input.x > 0 ? Vector2.right : Vector2.left;
        }
        else if (movingVertically)
        {
            // Pure vertical
            return input.y > 0 ? Vector2.up : Vector2.down;
        }
        
        // Below threshold on both axes - keep last direction
        return lastFacingDirection;
    }
    
    /// <summary>
    /// Manually set the facing direction.
    /// Useful for cutscenes, dialogue, or other scripted moments.
    /// </summary>
    public void SetFacingDirection(Vector2 direction)
    {
        lastFacingDirection = GetDirectionForAnimation(direction);
        if (meshVisual != null)
        {
            meshVisual.SetDirection(lastFacingDirection);
        }
    }
    
    /// <summary>
    /// Set facing direction using the Direction enum directly.
    /// </summary>
    public void SetFacingDirection(MeshRendererPlayerVisual.Direction direction)
    {
        // Convert enum to Vector2 for storage
        switch (direction)
        {
            case MeshRendererPlayerVisual.Direction.Down:
                lastFacingDirection = Vector2.down;
                break;
            case MeshRendererPlayerVisual.Direction.Up:
                lastFacingDirection = Vector2.up;
                break;
            case MeshRendererPlayerVisual.Direction.Left:
                lastFacingDirection = Vector2.left;
                break;
            case MeshRendererPlayerVisual.Direction.Right:
                lastFacingDirection = Vector2.right;
                break;
        }
        
        if (meshVisual != null)
        {
            meshVisual.SetDirection(direction);
        }
    }
    
    /// <summary>
    /// Get current facing direction as Vector2.
    /// </summary>
    public Vector2 GetFacingDirection()
    {
        return lastFacingDirection;
    }
    
    /// <summary>
    /// Get current facing direction as Direction enum.
    /// </summary>
    public MeshRendererPlayerVisual.Direction GetFacingDirectionEnum()
    {
        if (meshVisual != null)
        {
            return meshVisual.GetDirection();
        }
        
        // Fallback: convert lastFacingDirection to enum
        float absX = Mathf.Abs(lastFacingDirection.x);
        float absY = Mathf.Abs(lastFacingDirection.y);
        
        if (absX > absY)
        {
            return lastFacingDirection.x > 0 
                ? MeshRendererPlayerVisual.Direction.Right 
                : MeshRendererPlayerVisual.Direction.Left;
        }
        else
        {
            return lastFacingDirection.y > 0 
                ? MeshRendererPlayerVisual.Direction.Up 
                : MeshRendererPlayerVisual.Direction.Down;
        }
    }
    
    /// <summary>
    /// Get current animation state.
    /// </summary>
    public MeshRendererPlayerVisual.AnimationState GetAnimationState()
    {
        return meshVisual != null ? meshVisual.GetAnimationState() : lastState;
    }
    
    /// <summary>
    /// Rotate facing direction by 90° steps to match camera rotation.
    /// Call this when camera yaw changes to keep player facing the correct screen-relative direction.
    /// </summary>
    /// <param name="steps90">Number of 90° steps (positive = clockwise, negative = counterclockwise)</param>
    public void RotateFacingDirection(int steps90)
    {
        if (meshVisual == null)
        {
            Debug.LogWarning("[AnimController] RotateFacingDirection: meshVisual is null!");
            return;
        }
        
        // Normalize steps to 0-3 range
        steps90 = steps90 % 4;
        if (steps90 < 0) steps90 += 4;
        
        if (steps90 == 0)
        {
            if (enableDebugLogs)
                Debug.Log("[AnimController] RotateFacingDirection: steps90 is 0, no rotation needed.");
            return;
        }
        
        // Get current direction as enum
        MeshRendererPlayerVisual.Direction currentDir = GetFacingDirectionEnum();
        
        // Direction enum: Down=0, Up=1, Left=2, Right=3
        // Clockwise rotation order: Down -> Right -> Up -> Left -> Down
        // We need a mapping for clockwise rotation, not just index increment
        
        // Clockwise rotation lookup table (each entry is the direction after 1 clockwise step)
        // Down(0) -> Right(3), Up(1) -> Left(2), Left(2) -> Down(0), Right(3) -> Up(1)
        MeshRendererPlayerVisual.Direction[] clockwiseNext = new MeshRendererPlayerVisual.Direction[]
        {
            MeshRendererPlayerVisual.Direction.Right, // Down -> Right
            MeshRendererPlayerVisual.Direction.Left,  // Up -> Left
            MeshRendererPlayerVisual.Direction.Down,  // Left -> Down
            MeshRendererPlayerVisual.Direction.Up     // Right -> Up
        };
        
        MeshRendererPlayerVisual.Direction newDir = currentDir;
        
        // Apply rotation steps
        for (int i = 0; i < steps90; i++)
        {
            newDir = clockwiseNext[(int)newDir];
        }
        
        if (enableDebugLogs)
            Debug.Log($"[AnimController] RotateFacingDirection: {currentDir} -> {newDir} ({steps90} clockwise steps)");
        
        // Apply new direction to visual
        SetFacingDirection(newDir);
        
        // Also update lastFacingDirection to match
        switch (newDir)
        {
            case MeshRendererPlayerVisual.Direction.Down:
                lastFacingDirection = Vector2.down;
                break;
            case MeshRendererPlayerVisual.Direction.Up:
                lastFacingDirection = Vector2.up;
                break;
            case MeshRendererPlayerVisual.Direction.Left:
                lastFacingDirection = Vector2.left;
                break;
            case MeshRendererPlayerVisual.Direction.Right:
                lastFacingDirection = Vector2.right;
                break;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[AnimController] Updated lastFacingDirection to: {lastFacingDirection}");
    }
    
    /// <summary>
    /// Force player to face up (north) direction.
    /// Useful for camera zone transitions where "up" is the natural direction.
    /// </summary>
    public void ForceFaceUp()
    {
        if (enableDebugLogs)
            Debug.Log("[AnimController] ForceFaceUp called");
        
        if (meshVisual == null)
        {
            Debug.LogWarning("[AnimController] ForceFaceUp: meshVisual is null!");
            return;
        }
        
        SetFacingDirection(MeshRendererPlayerVisual.Direction.Up);
        lastFacingDirection = Vector2.up;
        
        if (enableDebugLogs)
            Debug.Log("[AnimController] Player now facing Up");
    }
    
    /// <summary>
    /// Force player to face a specific direction.
    /// </summary>
    public void ForceFaceDirection(MeshRendererPlayerVisual.Direction direction)
    {
        if (enableDebugLogs)
            Debug.Log($"[AnimController] ForceFaceDirection: {direction}");
        
        if (meshVisual == null)
        {
            Debug.LogWarning("[AnimController] ForceFaceDirection: meshVisual is null!");
            return;
        }
        
        SetFacingDirection(direction);
        
        switch (direction)
        {
            case MeshRendererPlayerVisual.Direction.Down:
                lastFacingDirection = Vector2.down;
                break;
            case MeshRendererPlayerVisual.Direction.Up:
                lastFacingDirection = Vector2.up;
                break;
            case MeshRendererPlayerVisual.Direction.Left:
                lastFacingDirection = Vector2.left;
                break;
            case MeshRendererPlayerVisual.Direction.Right:
                lastFacingDirection = Vector2.right;
                break;
        }
    }
    
    /// <summary>
    /// Set facing direction to match a specific camera yaw index.
    /// Useful when camera snaps to absolute yaw rather than rotating by delta.
    /// </summary>
    /// <param name="cameraYawIndex90">Camera yaw index (0-3)</param>
    /// <param name="originalWorldDirection">The world direction player was facing before camera change</param>
    public void SetFacingForCameraYaw(int cameraYawIndex90, Vector2 originalWorldDirection)
    {
        if (meshVisual == null) return;
        
        // Convert world direction to screen-relative direction based on camera yaw
        // Camera yaw 0 = no rotation, 1 = 90° CW, 2 = 180°, 3 = 270° CW
        
        // Rotate the world direction by negative camera yaw to get screen direction
        Vector2 screenDir = RotateVector2(-cameraYawIndex90 * 90f, originalWorldDirection);
        
        // Convert to facing direction
        SetFacingDirection(screenDir);
    }
    
    /// <summary>
    /// Rotate a Vector2 by degrees (clockwise).
    /// </summary>
    private Vector2 RotateVector2(float degrees, Vector2 v)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }
    
    /// <summary>
    /// Check if player is currently moving (based on last update).
    /// </summary>
    public bool IsMoving()
    {
        return lastState != MeshRendererPlayerVisual.AnimationState.Idle;
    }
    
    /// <summary>
    /// Get movement deadzone value.
    /// </summary>
    public float GetMovementDeadzone()
    {
        return movementDeadzone;
    }
    
    /// <summary>
    /// Lock animation updates (prevents animation changes during landing impact, etc.).
    /// When locked, UpdateAnimation will not change the animation state.
    /// </summary>
    public void SetAnimationLocked(bool locked)
    {
        if (!locked)
        {
            hasExternalAnimationLock = false;
        }

        isAnimationLocked = locked;
    }
    
    /// <summary>
    /// Check if animation is currently locked.
    /// </summary>
    public bool IsAnimationLocked => isAnimationLocked;
    
    /// <summary>
    /// Lock animation to a specific state for a duration.
    /// During the lock, UpdateAnimation will not change the animation state.
    /// </summary>
    /// <param name="state">The animation state to lock to</param>
    /// <param name="duration">Duration in seconds to maintain the lock</param>
    public void LockAnimationState(MeshRendererPlayerVisual.AnimationState state, float duration)
    {
        if (meshVisual == null) return;

        CancelTimedAnimationLock();
        hasExternalAnimationLock = false;
        isAnimationLocked = true;
        meshVisual.SetAnimationState(state); // Immediately set the locked animation state
        lastState = state;
        animationLockCoroutine = StartCoroutine(UnlockAnimationAfterDelay(duration));
        
        if (enableDebugLogs)
        {
            Debug.Log($"[AnimController] Animation locked to {state} for {duration:F2} seconds.");
        }
    }

    /// <summary>
    /// Apply a manual animation override for cross-system presentation like encounter transitions.
    /// This cancels any active timed lock so the override cannot be released early by a stale coroutine.
    /// </summary>
    public void BeginExternalAnimationLock(MeshRendererPlayerVisual.AnimationState state)
    {
        if (meshVisual == null) return;

        CancelTimedAnimationLock();
        hasExternalAnimationLock = true;
        isAnimationLocked = true;
        meshVisual.SetAnimationState(state);
        lastState = state;

        if (enableDebugLogs)
        {
            Debug.Log($"[AnimController] External animation lock started: {state}");
        }
    }

    /// <summary>
    /// Begin a generic presentation lock so gameplay movement cannot overwrite a temporary one-shot visual.
    /// </summary>
    public void BeginPresentationLock()
    {
        BeginExternalAnimationLock(GetAnimationState());
    }

    /// <summary>
    /// Release a manual animation override previously started by BeginExternalAnimationLock.
    /// </summary>
    public void EndExternalAnimationLock()
    {
        if (!hasExternalAnimationLock)
            return;

        hasExternalAnimationLock = false;
        isAnimationLocked = false;

        if (enableDebugLogs)
        {
            Debug.Log("[AnimController] External animation lock ended.");
        }
    }

    /// <summary>
    /// End a generic presentation lock previously started by <see cref="BeginPresentationLock"/>.
    /// </summary>
    public void EndPresentationLock()
    {
        EndExternalAnimationLock();
    }

    private void CancelTimedAnimationLock()
    {
        if (animationLockCoroutine == null)
            return;

        StopCoroutine(animationLockCoroutine);
        animationLockCoroutine = null;
    }
    
    private IEnumerator UnlockAnimationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (hasExternalAnimationLock)
        {
            animationLockCoroutine = null;
            yield break;
        }

        isAnimationLocked = false;
        animationLockCoroutine = null;
        if (enableDebugLogs)
        {
            Debug.Log("[AnimController] Animation unlocked.");
        }
    }
}
