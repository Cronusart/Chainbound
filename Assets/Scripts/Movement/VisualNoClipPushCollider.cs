using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Physics-based push collider for VisualNoClip system.
/// Scales up smoothly when falling collider enters trigger, pushing player away from buildings.
/// 
/// Setup:
/// 1. Create a child GameObject under VisualNoClipTrigger
/// 2. Add a BoxCollider (or SphereCollider) - NOT a trigger
/// 3. Set collider layer to interact with player's falling collider layer
/// 4. Attach this component
/// 5. Configure push size, animation speed, and scale axes
/// 
/// Per-Axis Scaling:
/// Use "Scale Axes" to control which directions the collider expands. For example:
/// - North-facing cliff: Scale Axes = (0, 0, 1) - only scale in Z direction
/// - East-facing cliff: Scale Axes = (1, 0, 0) - only scale in X direction
/// - Vertical push: Scale Axes = (0, 1, 0) - only scale in Y direction
/// This prevents the collider from expanding into adjacent cliff sides.
/// </summary>
[RequireComponent(typeof(Collider))]
public class VisualNoClipPushCollider : MonoBehaviour
{
    [Header("Push Collider Settings")]
    [Tooltip("Base size of push collider when inactive (usually matches trigger size)")]
    [SerializeField] private Vector3 baseSize = Vector3.one;
    
    [Tooltip("Size multiplier when active (e.g., 1.2 = 20% larger)")]
    [SerializeField] private float pushSizeMultiplier = 1.2f;
    
    [Tooltip("Which axes to scale when active. X/Y/Z values: 1 = scale this axis, 0 = keep base size. Use this to prevent scaling toward unwanted directions (e.g., only scale Z for north-facing cliff).")]
    [SerializeField] private Vector3 scaleAxes = Vector3.one;
    
    [Tooltip("Animation duration for scaling up/down (seconds)")]
    [SerializeField] private float scaleAnimationDuration = 0.05f;
    
    [Tooltip("Animation curve for scaling (optional - uses linear if null)")]
    [SerializeField] private AnimationCurve scaleCurve;
    
    [Header("Detection")]
    [Tooltip("Direct reference to the player's falling collider GameObject (auto-found if null)")]
    [SerializeField] private GameObject fallingColliderObject;
    
    [Tooltip("Tag for falling collider GameObject (used for auto-finding if reference is null)")]
    [SerializeField] private string fallingColliderTag = "Player";
    
    [Tooltip("Name pattern to identify falling collider (used for auto-finding if reference is null)")]
    [SerializeField] private string fallingColliderName = "FallingCollider";
    
    private Collider fallingColliderComponent; // Cached reference to the actual Collider component
    
    [Header("Safety Check")]
    [Tooltip("Enable periodic safety check to verify falling colliders are still in trigger")]
    [SerializeField] private bool enableSafetyCheck = true;
    
    [Tooltip("How often to run safety check (seconds). Lower = more responsive but higher cost.")]
    [SerializeField] private float safetyCheckInterval = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private Collider pushCollider;
    private Collider parentTriggerCollider; // Reference to parent trigger for bounds checking
    private Vector3 targetSize;
    private Vector3 currentSize;
    private bool isScaling = false;
    private Coroutine scaleCoroutine;
    private float lastSafetyCheckTime = 0f;
    private float lastFindAttemptTime = 0f;
    private const float FIND_ATTEMPT_INTERVAL = 1f; // Only try to find reference once per second
    
    // Track active falling colliders
    private HashSet<Collider> activeFallingColliders = new HashSet<Collider>();
    
    /// <summary>
    /// Check if a given collider matches our falling collider reference.
    /// Public method for VisualNoClipTrigger to use for reliable detection.
    /// </summary>
    public bool IsFallingColliderReference(Collider col)
    {
        if (col == null)
            return false;
        
        // Direct reference comparison (most reliable)
        if (fallingColliderComponent != null && col == fallingColliderComponent)
            return true;
        
        // GameObject comparison
        if (fallingColliderObject != null && col.gameObject == fallingColliderObject)
            return true;
        
        return false;
    }
    
    // Public methods for parent trigger to call
    public void NotifyFallingColliderEntered(Collider fallingCollider)
    {
        if (fallingCollider == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[VisualNoClipPushCollider] NotifyFallingColliderEntered called with null collider!");
            return;
        }
        
        bool isFalling = IsFallingCollider(fallingCollider);
        if (!isFalling)
        {
            if (enableDebugLogs)
                Debug.Log($"[VisualNoClipPushCollider] Collider entered but is NOT falling collider: {fallingCollider.name} (GameObject: {fallingCollider.gameObject.name}). Reference: {(fallingColliderComponent != null ? fallingColliderComponent.name : "null")}");
            return;
        }
        
        if (activeFallingColliders.Add(fallingCollider))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipPushCollider] ✓ FALLING COLLIDER ENTERED: '{fallingCollider.name}' (GameObject: {fallingCollider.gameObject.name}, Enabled: {fallingCollider.enabled}, Active: {fallingCollider.gameObject.activeInHierarchy}). Activating push collider.");
            }
            
            ActivatePush();
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[VisualNoClipPushCollider] Falling collider already in active set: {fallingCollider.name}");
        }
    }
    
    public void NotifyFallingColliderExited(Collider fallingCollider)
    {
        if (fallingCollider == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[VisualNoClipPushCollider] NotifyFallingColliderExited called with null collider!");
            return;
        }
        
        bool isFalling = IsFallingCollider(fallingCollider);
        if (!isFalling)
        {
            if (enableDebugLogs)
                Debug.Log($"[VisualNoClipPushCollider] Collider exited but is NOT falling collider: {fallingCollider.name} (GameObject: {fallingCollider.gameObject.name})");
            return;
        }
        
        if (activeFallingColliders.Remove(fallingCollider))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipPushCollider] ✓ FALLING COLLIDER EXITED: '{fallingCollider.name}' (GameObject: {fallingCollider.gameObject.name}, Enabled: {fallingCollider.enabled}, Active: {fallingCollider.gameObject.activeInHierarchy}). Active count: {activeFallingColliders.Count}");
            }
            
            if (activeFallingColliders.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[VisualNoClipPushCollider] No more active falling colliders. Deactivating push collider.");
                DeactivatePush();
            }
        }
        else if (enableDebugLogs)
        {
            // Collider wasn't in the set - might be a duplicate exit or state issue
            Debug.LogWarning($"[VisualNoClipPushCollider] ⚠ Falling collider exit called but wasn't in active set: '{fallingCollider.name}' (GameObject: {fallingCollider.gameObject.name}, Enabled: {fallingCollider.enabled}). Active count: {activeFallingColliders.Count}. Performing safety reset.");
            HardReset();
        }
    }
    
    private void Awake()
    {
        pushCollider = GetComponent<Collider>();
        if (pushCollider == null)
        {
            Debug.LogError($"[VisualNoClipPushCollider] No Collider found on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        // Ensure push collider is NOT a trigger (needs to push via physics)
        pushCollider.isTrigger = false;
        
        // Find parent trigger collider for safety checks
        VisualNoClipTrigger parentTrigger = GetComponentInParent<VisualNoClipTrigger>();
        if (parentTrigger != null)
        {
            parentTriggerCollider = parentTrigger.GetTriggerCollider();
            if (parentTriggerCollider == null)
            {
                parentTriggerCollider = parentTrigger.GetComponent<Collider>();
            }
        }
        
        // Store base size from collider
        if (pushCollider is BoxCollider box)
        {
            baseSize = box.size;
        }
        else if (pushCollider is SphereCollider sphere)
        {
            baseSize = Vector3.one * sphere.radius;
        }
        else if (pushCollider is CapsuleCollider capsule)
        {
            baseSize = new Vector3(capsule.radius, capsule.height, capsule.radius);
        }
        
        currentSize = baseSize;
        targetSize = baseSize;
        
        // Initially disable/enable to reset state
        pushCollider.enabled = false;
        
        // Find falling collider reference
        FindFallingCollider();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[VisualNoClipPushCollider] Initialized on {gameObject.name}, Base Size: {baseSize}, Parent Trigger: {(parentTriggerCollider != null ? "Found" : "Missing")}, Falling Collider: {(fallingColliderComponent != null ? fallingColliderComponent.name : "Not Found")}");
        }
    }
    
    /// <summary>
    /// Find the falling collider reference (auto-find if not assigned).
    /// </summary>
    private void FindFallingCollider()
    {
        // If reference is assigned, use it
        if (fallingColliderObject != null)
        {
            fallingColliderComponent = fallingColliderObject.GetComponent<Collider>();
            if (fallingColliderComponent == null)
            {
                fallingColliderComponent = fallingColliderObject.GetComponentInChildren<Collider>();
            }
            
            if (fallingColliderComponent != null && enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipPushCollider] Found falling collider from assigned reference: {fallingColliderComponent.name}");
            }
            return;
        }
        
        // Auto-find by tag and name pattern
        GameObject playerObj = GameObject.FindGameObjectWithTag(fallingColliderTag);
        if (playerObj != null)
        {
            // Look for child with "FallingCollider" in name
            Transform fallingTransform = playerObj.transform.Find("FallingCollider");
            if (fallingTransform == null)
            {
                // Search all children
                foreach (Transform child in playerObj.transform)
                {
                    if (child.name.Contains(fallingColliderName))
                    {
                        fallingTransform = child;
                        break;
                    }
                }
            }
            
            if (fallingTransform != null)
            {
                fallingColliderObject = fallingTransform.gameObject;
                fallingColliderComponent = fallingTransform.GetComponent<Collider>();
                
                if (fallingColliderComponent != null && enableDebugLogs)
                {
                    Debug.Log($"[VisualNoClipPushCollider] Auto-found falling collider: {fallingColliderComponent.name}");
                }
            }
        }
        
        if (fallingColliderComponent == null && enableDebugLogs)
        {
            Debug.LogWarning($"[VisualNoClipPushCollider] Could not find falling collider! Assign it manually in the inspector or ensure player has a child named '{fallingColliderName}'.");
        }
    }
    
    private void Update()
    {
        // Skip if no active colliders being tracked
        if (activeFallingColliders.Count == 0)
            return;
        
        // Try to find falling collider reference if not set (may be auto-created by PlayerMovement3D)
        if (fallingColliderComponent == null && Time.time - lastFindAttemptTime > FIND_ATTEMPT_INTERVAL)
        {
            lastFindAttemptTime = Time.time;
            FindFallingCollider();
        }
        
        // Throttle checks
        if (Time.time - lastSafetyCheckTime < safetyCheckInterval)
            return;
        
        lastSafetyCheckTime = Time.time;
        
        // CRITICAL: Check if our direct reference to the falling collider is disabled
        // This is the most reliable check since OnTriggerExit doesn't fire when a collider is disabled
        if (fallingColliderComponent != null && !fallingColliderComponent.enabled)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipPushCollider] Direct reference check: Falling collider '{fallingColliderComponent.name}' is DISABLED (player landed). Forcing deactivation.");
            }
            
            // Clear all tracked colliders and deactivate
            activeFallingColliders.Clear();
            DeactivatePush();
            return;
        }
        
        // Additional safety check if enableSafetyCheck is true
        if (enableSafetyCheck)
        {
            PerformSafetyCheck();
        }
    }
    
    /// <summary>
    /// Safety check: Verify falling colliders still exist, are enabled, and are inside the trigger.
    /// Force reset if they're not (prevents stuck push colliders).
    /// This is critical because falling colliders get disabled on landing, so OnTriggerExit never fires.
    /// </summary>
    private void PerformSafetyCheck()
    {
        if (activeFallingColliders.Count == 0)
            return;
        
        if (enableDebugLogs && Time.frameCount % 30 == 0) // Log every ~0.5s at 60fps
        {
            Debug.Log($"[VisualNoClipPushCollider] Safety check running. Active colliders: {activeFallingColliders.Count}, Direct ref enabled: {(fallingColliderComponent != null ? fallingColliderComponent.enabled.ToString() : "null")}");
        }
        
        // Check each tracked falling collider
        List<Collider> collidersToRemove = new List<Collider>();
        
        foreach (Collider fallingCol in activeFallingColliders)
        {
            // CRITICAL: Check if collider GameObject still exists and is active
            if (fallingCol == null || fallingCol.gameObject == null || !fallingCol.gameObject.activeInHierarchy)
            {
                // Collider or GameObject is destroyed/deactivated - remove it
                collidersToRemove.Add(fallingCol);
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[VisualNoClipPushCollider] Safety check: Falling collider GameObject is null or inactive! Removing from active set.");
                }
                continue;
            }
            
            // Check if collider component is enabled - THIS IS THE KEY CHECK
            if (!fallingCol.enabled)
            {
                // Collider is disabled (player landed, falling collider deactivated) - remove it
                collidersToRemove.Add(fallingCol);
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[VisualNoClipPushCollider] Safety check: Falling collider '{fallingCol.name}' is DISABLED (player landed)! Removing from active set.");
                }
                continue;
            }
            
            // Check if collider bounds intersect with trigger bounds (only if we have trigger reference)
            if (parentTriggerCollider != null && !IsColliderInsideTrigger(fallingCol, parentTriggerCollider))
            {
                // Collider is outside trigger - remove it
                collidersToRemove.Add(fallingCol);
                
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[VisualNoClipPushCollider] Safety check: Falling collider {fallingCol.name} is outside trigger! Removing from active set.");
                }
            }
        }
        
        // Remove colliders that are no longer valid
        foreach (Collider col in collidersToRemove)
        {
            activeFallingColliders.Remove(col);
        }
        
        // If no more active colliders, smoothly deactivate
        if (collidersToRemove.Count > 0 && activeFallingColliders.Count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipPushCollider] Safety check: All falling colliders removed. Deactivating push collider.");
            }
            
            DeactivatePush();
        }
    }
    
    /// <summary>
    /// Check if a collider is inside the trigger collider using bounds intersection.
    /// </summary>
    private bool IsColliderInsideTrigger(Collider col, Collider triggerCol)
    {
        if (col == null || triggerCol == null)
            return false;
        
        // Use bounds intersection for reliable detection
        Bounds colBounds = col.bounds;
        Bounds triggerBounds = triggerCol.bounds;
        
        return triggerBounds.Intersects(colBounds);
    }
    
    /// <summary>
    /// Hard reset - immediately deactivate and clear all state.
    /// Use this when safety check detects falling collider is no longer in trigger.
    /// </summary>
    public void HardReset()
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[VisualNoClipPushCollider] HARD RESET: Clearing all state and deactivating push collider.");
        }
        
        // Stop any scaling coroutine
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
        
        // Clear all tracked colliders
        activeFallingColliders.Clear();
        
        // Immediately reset size and disable
        currentSize = baseSize;
        targetSize = baseSize;
        ApplySizeToCollider(baseSize);
        
        if (pushCollider != null)
        {
            pushCollider.enabled = false;
        }
        
        isScaling = false;
    }
    
    
    /// <summary>
    /// Check if a collider is the player's falling collider.
    /// Uses direct reference if available, otherwise falls back to name/tag matching.
    /// </summary>
    private bool IsFallingCollider(Collider col)
    {
        if (col == null)
            return false;
        
        // Primary check: Direct reference comparison (most reliable)
        if (fallingColliderComponent != null && col == fallingColliderComponent)
        {
            if (enableDebugLogs && Time.frameCount % 60 == 0) // Throttle debug logs
            {
                Debug.Log($"[VisualNoClipPushCollider] Detected falling collider via direct reference: {col.name}");
            }
            return true;
        }
        
        // Fallback: Check if it's the same GameObject as our reference
        if (fallingColliderObject != null && col.gameObject == fallingColliderObject)
        {
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[VisualNoClipPushCollider] Detected falling collider via GameObject reference: {col.name}");
            }
            return true;
        }
        
        // Legacy fallback: Check by name pattern (if reference not assigned)
        if (fallingColliderComponent == null)
        {
            if (col.name.Contains(fallingColliderName))
                return true;
            
            // Check if parent has Player tag
            Transform parent = col.transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag(fallingColliderTag))
                {
                    // Additional check: is this likely the falling collider?
                    if (col is SphereCollider)
                    {
                        return true;
                    }
                }
                parent = parent.parent;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Activate push collider - scale up smoothly.
    /// </summary>
    private void ActivatePush()
    {
        if (pushCollider == null)
            return;
        
        // Calculate target size with per-axis scaling control
        // Only scale axes where scaleAxes value is 1.0, keep base size for axes where it's 0.0
        targetSize = new Vector3(
            baseSize.x * (1f + (pushSizeMultiplier - 1f) * Mathf.Clamp01(scaleAxes.x)),
            baseSize.y * (1f + (pushSizeMultiplier - 1f) * Mathf.Clamp01(scaleAxes.y)),
            baseSize.z * (1f + (pushSizeMultiplier - 1f) * Mathf.Clamp01(scaleAxes.z))
        );
        
        // Enable collider if not already enabled
        if (!pushCollider.enabled)
        {
            pushCollider.enabled = true;
            currentSize = baseSize; // Start from base
        }
        
        // Start scaling animation
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleColliderCoroutine(currentSize, targetSize));
        
        if (enableDebugLogs)
            Debug.Log($"[VisualNoClipPushCollider] Activating push - scaling to {targetSize} (scaleAxes: {scaleAxes})");
    }
    
    /// <summary>
    /// Deactivate push collider - scale down smoothly.
    /// </summary>
    private void DeactivatePush()
    {
        if (pushCollider == null)
            return;
        
        // Don't deactivate if we're already at base size and disabled
        if (!pushCollider.enabled && currentSize == baseSize)
            return;
        
        // Calculate target size (back to base)
        targetSize = baseSize;
        
        // Start scaling animation
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleColliderCoroutine(currentSize, targetSize, true));
        
        if (enableDebugLogs)
            Debug.Log($"[VisualNoClipPushCollider] Deactivating push - scaling to {targetSize}");
    }
    
    /// <summary>
    /// Smoothly scale collider size over time.
    /// </summary>
    private IEnumerator ScaleColliderCoroutine(Vector3 startSize, Vector3 endSize, bool disableOnComplete = false)
    {
        isScaling = true;
        float elapsed = 0f;
        
        while (elapsed < scaleAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleAnimationDuration);
            
            // Apply curve if available
            if (scaleCurve != null && scaleCurve.length > 0)
            {
                t = scaleCurve.Evaluate(t);
            }
            
            // Interpolate size
            currentSize = Vector3.Lerp(startSize, endSize, t);
            
            // Apply to collider
            ApplySizeToCollider(currentSize);
            
            yield return null;
        }
        
        // Ensure final size
        currentSize = endSize;
        ApplySizeToCollider(currentSize);
        
        // Disable collider if deactivating
        if (disableOnComplete && pushCollider != null)
        {
            pushCollider.enabled = false;
        }
        
        isScaling = false;
        scaleCoroutine = null;
    }
    
    /// <summary>
    /// Apply size to collider based on type.
    /// </summary>
    private void ApplySizeToCollider(Vector3 size)
    {
        if (pushCollider == null)
            return;
        
        if (pushCollider is BoxCollider box)
        {
            box.size = size;
        }
        else if (pushCollider is SphereCollider sphere)
        {
            // Use average of X/Y/Z for sphere radius
            sphere.radius = (size.x + size.y + size.z) / 3f;
        }
        else if (pushCollider is CapsuleCollider capsule)
        {
            capsule.radius = (size.x + size.z) / 2f;
            capsule.height = size.y;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (pushCollider == null)
            pushCollider = GetComponent<Collider>();
        
        if (pushCollider != null)
        {
            // Draw current size in green
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            DrawColliderGizmo(pushCollider, currentSize);
            
            // Draw target size in yellow (if different)
            if (targetSize != currentSize)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                DrawColliderGizmo(pushCollider, targetSize);
            }
        }
    }
    
    private void DrawColliderGizmo(Collider col, Vector3 size)
    {
        if (col is BoxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, size);
        }
        else if (col is SphereCollider sphere)
        {
            float radius = (size.x + size.y + size.z) / 3f;
            Gizmos.DrawSphere(transform.position + sphere.center, radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            float radius = (size.x + size.z) / 2f;
            Gizmos.DrawWireSphere(transform.position + capsule.center, radius);
        }
    }
}

