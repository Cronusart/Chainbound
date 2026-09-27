using System.Collections;
using UnityEngine;

/// <summary>
/// Buffer zone collider for SceneTransitionTrigger hysteresis system.
/// This should be a child object that extends beyond the main transition trigger.
/// 
/// Player must exit BOTH the inner trigger zone AND this buffer zone for the transition to arm.
/// This prevents immediate re-triggering when player spawns inside the transition zone.
/// 
/// Setup:
/// 1. Create child GameObject of SceneTransitionTrigger
/// 2. Add Collider (BoxCollider recommended) and set "Is Trigger" = true
/// 3. Make collider larger than parent trigger (extends outward)
/// 4. Add this component
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionBuffer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Parent transition trigger (auto-found if null)")]
    [SerializeField] private SceneTransitionTrigger parentTrigger;
    
    [Tooltip("Player tag to detect")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("Safety Net (Jump Detection)")]
    [Tooltip("Enable periodic safety check for jump edge cases")]
    [SerializeField] private bool enableSafetyNet = true;
    
    [Tooltip("How often to run safety check (seconds)")]
    [SerializeField] private float safetyCheckInterval = 0.15f;
    
    [Tooltip("Only run safety check when player is tracked as inside buffer")]
    [SerializeField] private bool onlyCheckWhenInside = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private Collider bufferCollider;
    private bool isInitialized = false;
    private bool playerInBuffer = false;
    private float lastSafetyCheckTime = 0f;
    
    /// <summary>
    /// Initialize the buffer zone with parent trigger reference.
    /// Called by SceneTransitionTrigger during setup.
    /// </summary>
    public void Initialize(SceneTransitionTrigger trigger, string tag)
    {
        parentTrigger = trigger;
        playerTag = tag;
        isInitialized = true;
        
        bufferCollider = GetComponent<Collider>();
        if (bufferCollider != null)
        {
            bufferCollider.isTrigger = true;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionBuffer] Initialized: {gameObject.name} for trigger: {parentTrigger.name}");
        }
    }
    
    private void Awake()
    {
        bufferCollider = GetComponent<Collider>();
        if (bufferCollider != null)
        {
            bufferCollider.isTrigger = true;
        }
        
        // Try to find parent trigger if not initialized
        if (parentTrigger == null)
        {
            parentTrigger = GetComponentInParent<SceneTransitionTrigger>();
        }
        
        if (parentTrigger != null)
        {
            isInitialized = true;
        }
    }
    
    private void Update()
    {
        // Safety net for jump edge cases
        if (!enableSafetyNet || !isInitialized || parentTrigger == null)
            return;
        
        // Only check when player is tracked as inside buffer
        if (onlyCheckWhenInside && !playerInBuffer)
            return;
        
        // Throttle checks
        if (Time.time - lastSafetyCheckTime < safetyCheckInterval)
            return;
        
        lastSafetyCheckTime = Time.time;
        PerformSafetyCheck();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized || parentTrigger == null) return;
        if (!IsPlayerCollider(other)) return;
        
        playerInBuffer = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionBuffer] Player entered buffer: {gameObject.name} (collider: {other.name})");
        }
        
        parentTrigger.OnBufferZoneEnter();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!isInitialized || parentTrigger == null) return;
        if (!IsPlayerCollider(other)) return;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionBuffer] Player exited buffer: {gameObject.name} (collider: {other.name})");
        }
        
        // Delayed check to confirm player has truly exited
        StartCoroutine(DelayedExitCheck());
    }
    
    private IEnumerator DelayedExitCheck()
    {
        // Wait a small delay to let physics settle
        yield return new WaitForSeconds(0.05f);
        
        // Verify player is truly outside buffer
        if (!IsAnyPlayerColliderInZone(bufferCollider))
        {
            playerInBuffer = false;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionBuffer] Confirmed player exited buffer: {gameObject.name}");
            }
            
            parentTrigger?.OnBufferZoneExit();
        }
    }
    
    /// <summary>
    /// Safety net: Verify player is actually outside buffer zone.
    /// Catches edge cases where trigger events might be missed during jumps.
    /// </summary>
    private void PerformSafetyCheck()
    {
        if (parentTrigger == null || bufferCollider == null)
            return;
        
        bool actuallyInBuffer = IsAnyPlayerColliderInZone(bufferCollider);
        
        // If we think player is in buffer but they're actually outside
        if (playerInBuffer && !actuallyInBuffer)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[SceneTransitionBuffer] Safety net: Player is outside buffer but state says inside. Correcting.");
            }
            
            playerInBuffer = false;
            parentTrigger.OnBufferZoneExit();
        }
        // If we think player is outside buffer but they're actually inside
        else if (!playerInBuffer && actuallyInBuffer)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[SceneTransitionBuffer] Safety net: Player is inside buffer but state says outside. Correcting.");
            }
            
            playerInBuffer = true;
            parentTrigger.OnBufferZoneEnter();
        }
    }
    
    /// <summary>
    /// Check if a collider belongs to the player (handles both capsule and falling colliders).
    /// </summary>
    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        
        // Direct tag check
        if (col.CompareTag(playerTag))
            return true;
        
        // Check parent GameObjects (for falling collider which is a child)
        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag))
                return true;
            parent = parent.parent;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if any player collider is inside a zone collider using bounds overlap.
    /// </summary>
    private bool IsAnyPlayerColliderInZone(Collider zoneCol)
    {
        if (zoneCol == null) return false;
        
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null) return false;
        
        Collider[] playerColliders = playerObj.GetComponentsInChildren<Collider>();
        if (playerColliders == null || playerColliders.Length == 0) return false;
        
        foreach (Collider playerCol in playerColliders)
        {
            if (playerCol == null || !playerCol.enabled) continue;
            if (playerCol.isTrigger) continue; // Skip trigger colliders
            
            // Bounds intersection check
            if (zoneCol.bounds.Intersects(playerCol.bounds))
            {
                return true;
            }
        }
        
        return false;
    }
    
    #region Gizmos
    
    private void OnDrawGizmos()
    {
        if (bufferCollider == null)
            bufferCollider = GetComponent<Collider>();
        
        if (bufferCollider != null)
        {
            // Pink/Magenta for buffer zone (matches your diagram)
            Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.2f);
            
            if (bufferCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.6f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (bufferCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.6f);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (bufferCollider is CapsuleCollider capsule)
            {
                // Approximate capsule gizmo
                Vector3 center = transform.position + capsule.center;
                Gizmos.DrawWireSphere(center, capsule.radius);
            }
        }
    }
    
    #endregion
}

