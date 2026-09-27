using UnityEngine;

/// <summary>
/// Animation controller for 3D top-down movement.
/// Preserves legacy "Stardew diagonals" behavior: diagonal movement uses left/right sprites.
/// Converts XZ movement (3D) to XY animator parameters (2D-style).
/// </summary>
public class PlayerAnimationController3D : MonoBehaviour
{
    [SerializeField] private Animator animator;
    
    [Header("Movement Direction Settings")]
    [SerializeField] private bool useStardewValleyDiagonals = true;
    [SerializeField] private float diagonalThreshold = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Vector2 lastDirection = Vector2.down;

    /// <summary>
    /// Update animation based on XZ movement input (from 3D world).
    /// Input is treated as XZ plane: X = horizontal, Y = forward/backward (Z in 3D).
    /// </summary>
    /// <param name="movementInput">2D vector representing XZ movement (X = X, Y = Z)</param>
    public void UpdateAnimation(Vector2 movementInput)
    {
        bool isMoving = movementInput.magnitude > 0.1f;

        animator.SetBool("IsMoving", isMoving);

        if (isMoving)
        {
            Vector2 directionToUse = GetDirectionForAnimation(movementInput);
            lastDirection = directionToUse;
            
            animator.SetFloat("MoveX", directionToUse.x);
            animator.SetFloat("MoveY", directionToUse.y);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerAnimationController3D] Moving - Original:{movementInput}, Used:{directionToUse}");
            }
        }
        else
        {
            // Maintain last direction when idle
            animator.SetFloat("MoveX", lastDirection.x);
            animator.SetFloat("MoveY", lastDirection.y);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerAnimationController3D] Idle - Last Direction X:{lastDirection.x}, Y:{lastDirection.y}");
            }
        }
    }
    
    /// <summary>
    /// Get animation direction from input, applying Stardew Valley diagonal rules.
    /// Diagonal movement (both X and Y significant) uses horizontal direction (left/right).
    /// </summary>
    private Vector2 GetDirectionForAnimation(Vector2 input)
    {
        if (!useStardewValleyDiagonals)
        {
            return input.normalized;
        }
        
        // Check if we're moving diagonally (both X and Z have significant values)
        bool movingHorizontally = Mathf.Abs(input.x) > diagonalThreshold;
        bool movingVertically = Mathf.Abs(input.y) > diagonalThreshold; // Y in input = Z in 3D
        
        if (movingHorizontally && movingVertically)
        {
            // Diagonal movement - use horizontal direction (Stardew Valley style)
            // This will trigger left/right sprites for diagonal movement
            if (input.x > 0)
                return Vector2.right;  // Moving diagonally right -> use right sprite
            else
                return Vector2.left;   // Moving diagonally left -> use left sprite
        }
        else if (movingHorizontally)
        {
            // Pure horizontal movement
            return input.x > 0 ? Vector2.right : Vector2.left;
        }
        else if (movingVertically)
        {
            // Pure vertical movement (forward/backward in 3D)
            return input.y > 0 ? Vector2.up : Vector2.down;
        }
        
        // Fallback to normalized input
        return input.normalized;
    }
    
    /// <summary>
    /// Manually set the facing direction.
    /// </summary>
    public void SetFacingDirection(Vector2 direction)
    {
        lastDirection = GetDirectionForAnimation(direction);
        animator.SetFloat("MoveX", lastDirection.x);
        animator.SetFloat("MoveY", lastDirection.y);
    }
    
    /// <summary>
    /// Get current facing direction.
    /// </summary>
    public Vector2 GetFacingDirection()
    {
        return lastDirection;
    }
}

