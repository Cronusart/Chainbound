using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scene object that fires a trigger when activated.
/// Can be collider-based or manually fired.
/// 
/// Setup:
/// 1. Add to GameObject with Collider (set as trigger)
/// 2. Set triggerId
/// 3. Call Arm() when sequence wants to listen for it
/// </summary>
[RequireComponent(typeof(Collider))]
public class SequenceTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Unique trigger ID")]
    [SerializeField] private string triggerId = "";
    
    [Tooltip("Fire only once")]
    [SerializeField] private bool fireOnce = true;
    
    [Tooltip("Start armed (can fire immediately)")]
    [SerializeField] private bool startArmed = false;
    
    [Header("Player Detection")]
    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Fire on trigger enter")]
    [SerializeField] private bool fireOnEnter = true;
    
    [Tooltip("Fire on trigger exit")]
    [SerializeField] private bool fireOnExit = false;
    
    [Header("Events")]
    [Tooltip("Invoked when trigger fires")]
    public UnityEvent OnTriggerFired;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // State
    private bool isArmed = false;
    private bool hasFired = false;
    
    /// <summary>Is this trigger currently armed?</summary>
    public bool IsArmed => isArmed;
    
    /// <summary>Has this trigger fired?</summary>
    public bool HasFired => hasFired;
    
    /// <summary>Trigger ID (for lookup).</summary>
    public string TriggerId => triggerId;
    
    private void Awake()
    {
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        isArmed = startArmed;
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
    
    /// <summary>
    /// Arm this trigger (enable firing).
    /// </summary>
    public void Arm()
    {
        isArmed = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SequenceTrigger] Armed trigger: {triggerId}");
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
            Debug.Log($"[SequenceTrigger] Disarmed trigger: {triggerId}");
        }
    }
    
    /// <summary>
    /// Manually fire this trigger.
    /// </summary>
    public void Fire()
    {
        if (!isArmed)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SequenceTrigger] Trigger {triggerId} not armed, ignoring fire");
            }
            return;
        }
        
        if (hasFired && fireOnce)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SequenceTrigger] Trigger {triggerId} already fired (fireOnce=true)");
            }
            return;
        }

        TriggerRegistry already = TriggerRegistry.Instance;
        if (fireOnce && already != null && already.IsTriggered(triggerId))
        {
            hasFired = true;
            isArmed = false;
            SuppressFurtherContacts();
            return;
        }
        
        hasFired = true;
        
        // Fire in registry
        TriggerRegistry registry = TriggerRegistry.Instance;
        if (registry != null)
        {
            registry.FireTrigger(triggerId);
        }
        
        // Invoke event
        OnTriggerFired?.Invoke();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SequenceTrigger] Trigger fired: {triggerId}");
        }
        
        // Disarm if fire once. Disable the collider too so a second body
        // part, or a later re-entry while this object is still active, cannot
        // start the same beat again. The GameObject stays active so disabling
        // it does not unregister the trigger the waiter is still reading.
        if (fireOnce)
        {
            isArmed = false;
            SuppressFurtherContacts();
        }
    }

    private void SuppressFurtherContacts()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!fireOnEnter) return;
        if (!isArmed) return;
        
        // Check if player
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            Fire();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!fireOnExit) return;
        if (!isArmed) return;
        
        // Check if player
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            Fire();
        }
    }
    
    /// <summary>
    /// Reset trigger state (allow firing again).
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
}

