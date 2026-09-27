using UnityEngine;

/// <summary>
/// Scene MonoBehaviour that registers waypoint ID with position.
/// Waypoints are registered at runtime and can be referenced by route assets.
/// </summary>
public class NPCWaypointMarker : MonoBehaviour
{
    [Header("Waypoint")]
    [Tooltip("Unique ID for this waypoint (used by route assets)")]
    [SerializeField] private string waypointId = "";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    public string WaypointId => waypointId;
    public Vector3 Position => transform.position;
    
    private void Awake()
    {
        // Register with waypoint registry
        if (!string.IsNullOrEmpty(waypointId))
        {
            NPCWaypointRegistry.Instance.RegisterWaypoint(waypointId, this);
        }
        else
        {
            Debug.LogWarning($"[NPCWaypointMarker] Waypoint on {gameObject.name} has no waypointId!");
        }
    }
    
    private void OnDestroy()
    {
        // Unregister
        if (!string.IsNullOrEmpty(waypointId))
        {
            NPCWaypointRegistry.Instance.UnregisterWaypoint(waypointId);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        if (!string.IsNullOrEmpty(waypointId))
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, waypointId);
        }
    }
#endif
}

