using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Building-side trigger component for VisualNoClip system.
/// Uses physics push collider(s) that scale up to push falling collider away from buildings.
/// 
/// Setup:
/// 1. Create trigger colliders around building (BoxCollider, SphereCollider, or CapsuleCollider)
/// 2. Set collider layer to "VisualNoClip"
/// 3. Attach this component to the trigger GameObject
/// 4. Create child GameObject(s) with VisualNoClipPushCollider component(s)
/// 5. Assign push colliders in the array below, or enable auto-find
/// 
/// Multiple Push Colliders:
/// - For large areas (like cliffs), you can have multiple push colliders (one per side)
/// - All push colliders activate simultaneously when falling collider enters
/// - Each push collider can have different size multipliers and animation speeds
/// </summary>
[RequireComponent(typeof(Collider))]
public class VisualNoClipTrigger : MonoBehaviour
{
    [Header("Physics Push")]
    [Tooltip("Array of VisualNoClipPushCollider components. Add/remove entries as needed. Auto-found if empty and auto-find is enabled.")]
    [SerializeField] private VisualNoClipPushCollider[] pushColliders = new VisualNoClipPushCollider[0];
    
    [Tooltip("If true, automatically finds all push colliders in children on Awake (if array is empty)")]
    [SerializeField] private bool autoFindAllPushColliders = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private Collider triggerCollider;
    
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"[VisualNoClipTrigger] No Collider found on {gameObject.name}!");
            return;
        }
        
        // Ensure trigger is enabled
        triggerCollider.isTrigger = true;
        
        // Auto-find all push colliders if enabled and array is empty
        if (autoFindAllPushColliders && (pushColliders == null || pushColliders.Length == 0))
        {
            VisualNoClipPushCollider[] foundColliders = GetComponentsInChildren<VisualNoClipPushCollider>();
            if (foundColliders != null && foundColliders.Length > 0)
            {
                pushColliders = foundColliders;
                if (enableDebugLogs)
                {
                    Debug.Log($"[VisualNoClipTrigger] Auto-found {pushColliders.Length} push collider(s) in children.");
                }
            }
        }
        
        // Filter out null entries and create clean array
        if (pushColliders != null && pushColliders.Length > 0)
        {
            List<VisualNoClipPushCollider> validColliders = new List<VisualNoClipPushCollider>();
            foreach (var pc in pushColliders)
            {
                if (pc != null)
                {
                    validColliders.Add(pc);
                }
            }
            pushColliders = validColliders.ToArray();
        }
        
        if ((pushColliders == null || pushColliders.Length == 0) && enableDebugLogs)
        {
            Debug.LogWarning($"[VisualNoClipTrigger] No VisualNoClipPushCollider found on {gameObject.name} or children. Create child GameObject(s) with VisualNoClipPushCollider component(s) or assign them manually.");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[VisualNoClipTrigger] Initialized on {gameObject.name}, Push Colliders: {pushColliders.Length}");
        }
    }
    
    /// <summary>
    /// Check if a collider is the player's falling collider.
    /// Uses push colliders' direct references if available for reliable detection.
    /// </summary>
    private bool IsFallingCollider(Collider col)
    {
        if (col == null)
            return false;
        
        // Check any push collider's direct reference (most reliable)
        if (pushColliders != null && pushColliders.Length > 0)
        {
            foreach (var pc in pushColliders)
            {
                if (pc != null && pc.IsFallingColliderReference(col))
                {
                    return true;
                }
            }
        }
        
        // Fallback: Check by name pattern
        if (col.name.Contains("FallingCollider"))
            return true;
        
        // Fallback: Check if parent has Player tag (falling collider is child of PlayerRoot)
        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag("Player"))
            {
                // Additional check: is this likely the falling collider?
                // Falling collider is usually a SphereCollider
                if (col is SphereCollider)
                {
                    return true;
                }
            }
            parent = parent.parent;
        }
        
        return false;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (pushColliders == null || pushColliders.Length == 0)
            return;
        
        // Check if this is the falling collider and notify all push colliders
        if (IsFallingCollider(other))
        {
            int notifiedCount = 0;
            foreach (var pc in pushColliders)
            {
                if (pc != null)
                {
                    pc.NotifyFallingColliderEntered(other);
                    notifiedCount++;
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipTrigger] OnTriggerEnter: Falling collider '{other.name}' (GameObject: {other.gameObject.name}) entered trigger. Notified {notifiedCount} push collider(s).");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[VisualNoClipTrigger] OnTriggerEnter: Collider '{other.name}' entered but is NOT falling collider. Ignoring.");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (pushColliders == null || pushColliders.Length == 0)
            return;
        
        // Check if this is the falling collider and notify all push colliders
        if (IsFallingCollider(other))
        {
            int notifiedCount = 0;
            foreach (var pc in pushColliders)
            {
                if (pc != null)
                {
                    pc.NotifyFallingColliderExited(other);
                    notifiedCount++;
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[VisualNoClipTrigger] OnTriggerExit: Falling collider '{other.name}' (GameObject: {other.gameObject.name}) exited trigger. Notified {notifiedCount} push collider(s).");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[VisualNoClipTrigger] OnTriggerExit: Collider '{other.name}' exited but is NOT falling collider. Ignoring.");
        }
    }
    
    /// <summary>
    /// Get the trigger collider (for safety checks in push collider).
    /// </summary>
    public Collider GetTriggerCollider()
    {
        return triggerCollider;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
        
        if (triggerCollider != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange tint
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (triggerCollider is CapsuleCollider capsule)
            {
                // Draw capsule approximation
                Vector3 center = transform.position + capsule.center;
                Gizmos.DrawSphere(center + Vector3.up * (capsule.height * 0.5f - capsule.radius), capsule.radius);
                Gizmos.DrawSphere(center - Vector3.up * (capsule.height * 0.5f - capsule.radius), capsule.radius);
            }
        }
        
    }
}

