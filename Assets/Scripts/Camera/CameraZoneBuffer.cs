using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Buffer volume for camera zone hysteresis. Player must leave every inner
/// piece and every buffer piece before the camera restores.
/// Extra inner boxes get matching buffer copies so compound shapes stay one zone.
/// </summary>
public class CameraZoneBuffer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraZoneTrigger parentTrigger;
    [SerializeField] private string triggerTag = "Player";
    
    [Header("Safety Net (Jump Detection)")]
    [Tooltip("Enable periodic safety check to ensure player has truly exited zones (fixes jump edge cases)")]
    [SerializeField] private bool enableSafetyNet = true;
    
    [Tooltip("How often to run safety check (seconds). Lower = more responsive but higher cost.")]
    [SerializeField] private float safetyCheckInterval = 0.15f;
    
    [Tooltip("Only run safety check when zone is activated (recommended for performance)")]
    [SerializeField] private bool onlyCheckWhenActive = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private readonly List<Collider> bufferColliders = new List<Collider>();
    private bool isInitialized = false;
    private float lastSafetyCheckTime = 0f;
    private bool playerInBuffer = false;
    
    private HashSet<Collider> activePlayerColliders = new HashSet<Collider>();

    public void Initialize(CameraZoneTrigger trigger, string playerTag)
    {
        parentTrigger = trigger;
        triggerTag = playerTag;
        isInitialized = true;
        RefreshBufferColliders(bindRelays: true);
    }

    public bool IsPlayerPhysicallyInside()
    {
        RefreshBufferColliders(bindRelays: false);
        return CameraZoneOccupancy.IsPlayerPhysicallyInside(bufferColliders, triggerTag);
    }
    
    private void Awake()
    {
        RefreshBufferColliders(bindRelays: Application.isPlaying);
        
        if (parentTrigger == null)
            parentTrigger = GetComponentInParent<CameraZoneTrigger>();
        
        if (parentTrigger != null)
            isInitialized = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEnter(other);
    }
    
    private void OnTriggerExit(Collider other)
    {
        NotifyPlayerExit(other);
    }

    public void NotifyPlayerEnter(Collider other)
    {
        if (!isInitialized || parentTrigger == null) return;
        if (!IsPlayerCollider(other)) return;
        
        activePlayerColliders.Add(other);
        playerInBuffer = true;
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneBuffer] Player entered buffer: {gameObject.name} (collider: {other.name})");
        
        parentTrigger.OnBufferZoneEnter();
    }

    public void NotifyPlayerExit(Collider other)
    {
        if (!isInitialized || parentTrigger == null) return;
        if (!IsPlayerCollider(other)) return;
        
        activePlayerColliders.Remove(other);
        
        if (enableDebugLogs)
            Debug.Log($"[CameraZoneBuffer] Player exited buffer piece: {gameObject.name} (collider: {other.name})");

        if (!IsPlayerPhysicallyInside())
        {
            playerInBuffer = false;
            parentTrigger.OnBufferZoneExit();
        }
        
        if (enableSafetyNet)
            StartCoroutine(DelayedSafetyCheck());
    }

    internal void RefreshBufferColliders(bool bindRelays)
    {
        CameraZoneOccupancy.CollectBufferColliders(this, bufferColliders, markAsTrigger: Application.isPlaying);
        if (!bindRelays || !Application.isPlaying)
            return;

        for (int i = 0; i < bufferColliders.Count; i++)
            CameraZoneOccupancy.EnsureRelay(bufferColliders[i], this);
    }
    
    private void Update()
    {
        if (!enableSafetyNet || !isInitialized || parentTrigger == null)
            return;
        
        if (onlyCheckWhenActive && !parentTrigger.IsActivated)
            return;
        
        if (Time.time - lastSafetyCheckTime < safetyCheckInterval)
            return;
        
        lastSafetyCheckTime = Time.time;
        PerformSafetyCheck();
    }
    
    private System.Collections.IEnumerator DelayedSafetyCheck()
    {
        yield return new WaitForSeconds(0.05f);
        PerformSafetyCheck();
    }
    
    private void PerformSafetyCheck()
    {
        if (parentTrigger == null)
            return;
        
        if (!parentTrigger.IsActivated)
            return;
        
        List<Collider> collidersToRemove = new List<Collider>();
        foreach (Collider col in activePlayerColliders)
        {
            if (col == null || col.gameObject == null)
                collidersToRemove.Add(col);
        }
        foreach (Collider col in collidersToRemove)
            activePlayerColliders.Remove(col);
        
        bool anyColliderInBuffer = IsPlayerPhysicallyInside();
        bool anyColliderInInner = parentTrigger.IsPlayerPhysicallyInsideInner();
        
        if (!anyColliderInBuffer && !anyColliderInInner)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[CameraZoneBuffer] Safety net: Player is physically outside all zones. Triggering hard reset.");
            
            HardReset();
            parentTrigger.HardReset();
        }
        else if (playerInBuffer != anyColliderInBuffer)
        {
            if (enableDebugLogs)
                Debug.Log($"[CameraZoneBuffer] Safety check: Correcting buffer state: {playerInBuffer} -> {anyColliderInBuffer}");

            playerInBuffer = anyColliderInBuffer;
            if (!playerInBuffer)
                parentTrigger.OnBufferZoneExit();
            else
                parentTrigger.OnBufferZoneEnter();
        }
    }
    
    public void HardReset()
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[CameraZoneBuffer] HARD RESET: Clearing all state for {gameObject.name}");
        
        activePlayerColliders.Clear();
        playerInBuffer = false;
    }
    
    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        
        if (col.CompareTag(triggerTag))
            return true;
        
        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(triggerTag))
                return true;
            parent = parent.parent;
        }
        
        return false;
    }
    
    private void OnDrawGizmos()
    {
        RefreshBufferColliders(bindRelays: false);
        for (int i = 0; i < bufferColliders.Count; i++)
        {
            Collider bufferCollider = bufferColliders[i];
            if (bufferCollider == null)
                continue;

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            CameraZoneOccupancy.DrawColliderGizmo(bufferCollider, false);
            Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
            CameraZoneOccupancy.DrawColliderGizmo(bufferCollider, true);
        }
    }
}
