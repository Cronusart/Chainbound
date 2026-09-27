using UnityEngine;

/// <summary>
/// Trigger that fires when player is within distance threshold.
/// Polls distance (throttled) and fires once when threshold crossed.
/// 
/// Setup:
/// 1. Place in scene at trigger point
/// 2. Set triggerId and distance threshold
/// 3. Call Arm() when sequence wants to listen
/// </summary>
public class DistanceTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Unique trigger ID")]
    [SerializeField] private string triggerId = "";
    
    [Tooltip("Distance threshold")]
    [SerializeField] private float distanceThreshold = 5f;
    
    [Tooltip("Fire only once")]
    [SerializeField] private bool fireOnce = true;
    
    [Tooltip("Start armed")]
    [SerializeField] private bool startArmed = false;
    
    [Header("Player Detection")]
    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Update rate (Hz)")]
    [Range(1f, 30f)]
    [SerializeField] private float updateRateHz = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showGizmo = true;
    
    // State
    private bool isArmed = false;
    private bool hasFired = false;
    private float lastUpdateTime = 0f;
    private GameObject playerObject;
    
    /// <summary>Is this trigger currently armed?</summary>
    public bool IsArmed => isArmed;
    
    /// <summary>Has this trigger fired?</summary>
    public bool HasFired => hasFired;
    
    /// <summary>Trigger ID (for lookup).</summary>
    public string TriggerId => triggerId;
    
    private void Awake()
    {
        isArmed = startArmed;
        
        // Find player
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null)
            {
                playerObject = found;
            }
        }
    }
    
    private void OnEnable()
    {
        // Register with trigger registry
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry != null)
        {
            registry.RegisterTrigger(triggerId);
        }
    }
    
    private void OnDisable()
    {
        // Unregister from trigger registry
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry != null)
        {
            registry.UnregisterTrigger(triggerId);
        }
    }
    
    private void Update()
    {
        if (!isArmed || hasFired) return;
        if (playerObject == null) return;
        
        // Throttle updates
        float updateInterval = 1f / updateRateHz;
        if (Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;
        
        // Check distance
        float distance = Vector3.Distance(transform.position, playerObject.transform.position);
        
        if (distance <= distanceThreshold)
        {
            Fire();
        }
    }
    
    /// <summary>
    /// Arm this trigger (enable firing).
    /// </summary>
    public void Arm()
    {
        isArmed = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DistanceTrigger] Armed trigger: {triggerId}");
        }
    }
    
    /// <summary>
    /// Disarm this trigger (disable firing).
    /// </summary>
    public void Disarm()
    {
        isArmed = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DistanceTrigger] Disarmed trigger: {triggerId}");
        }
    }
    
    /// <summary>
    /// Manually fire this trigger.
    /// </summary>
    public void Fire()
    {
        if (!isArmed)
        {
            return;
        }
        
        if (hasFired && fireOnce)
        {
            return;
        }
        
        hasFired = true;
        
        // Fire in registry
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry != null)
        {
            registry.FireTrigger(triggerId);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DistanceTrigger] Trigger fired: {triggerId}");
        }
        
        // Disarm if fire once
        if (fireOnce)
        {
            isArmed = false;
        }
    }
    
    /// <summary>
    /// Reset trigger state.
    /// </summary>
    public void Reset()
    {
        hasFired = false;
        isArmed = startArmed;
        
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry != null)
        {
            registry.ResetTrigger(triggerId);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        
        // Draw distance threshold sphere
        Gizmos.color = isArmed ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, distanceThreshold);
        
        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (distanceThreshold + 0.5f), $"{triggerId}\n{distanceThreshold}m");
        #endif
    }
#endif
}

