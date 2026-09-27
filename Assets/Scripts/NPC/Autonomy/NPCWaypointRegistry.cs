using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton registry mapping waypoint IDs to NPCWaypointMarker instances.
/// Waypoints self-register on Awake.
/// </summary>
public class NPCWaypointRegistry : MonoBehaviour
{
    private static NPCWaypointRegistry instance;
    
    public static NPCWaypointRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NPCWaypointRegistry>();
                if (instance == null)
                {
                    GameObject go = new GameObject("NPCWaypointRegistry");
                    instance = go.AddComponent<NPCWaypointRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private readonly Dictionary<string, NPCWaypointMarker> waypoints = new Dictionary<string, NPCWaypointMarker>();
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[NPCWaypointRegistry] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    
    /// <summary>
    /// Register a waypoint by ID.
    /// </summary>
    public void RegisterWaypoint(string waypointId, NPCWaypointMarker marker)
    {
        if (string.IsNullOrEmpty(waypointId))
        {
            Debug.LogWarning("[NPCWaypointRegistry] Cannot register waypoint with empty ID!");
            return;
        }
        
        if (marker == null)
        {
            Debug.LogWarning($"[NPCWaypointRegistry] Cannot register waypoint '{waypointId}' with null marker!");
            return;
        }
        
        if (waypoints.ContainsKey(waypointId))
        {
            Debug.LogWarning($"[NPCWaypointRegistry] Waypoint '{waypointId}' already registered! Overwriting.");
        }
        
        waypoints[waypointId] = marker;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCWaypointRegistry] Registered waypoint: {waypointId} at {marker.Position}");
        }
    }
    
    /// <summary>
    /// Unregister a waypoint by ID.
    /// </summary>
    public void UnregisterWaypoint(string waypointId)
    {
        if (string.IsNullOrEmpty(waypointId)) return;
        
        if (waypoints.Remove(waypointId) && enableDebugLogs)
        {
            Debug.Log($"[NPCWaypointRegistry] Unregistered waypoint: {waypointId}");
        }
    }
    
    /// <summary>
    /// Get waypoint marker by ID.
    /// </summary>
    public NPCWaypointMarker GetWaypoint(string waypointId)
    {
        if (string.IsNullOrEmpty(waypointId))
        {
            return null;
        }
        
        waypoints.TryGetValue(waypointId, out NPCWaypointMarker marker);
        return marker;
    }
    
    /// <summary>
    /// Get waypoint position by ID.
    /// </summary>
    public Vector3 GetWaypointPosition(string waypointId)
    {
        var marker = GetWaypoint(waypointId);
        return marker != null ? marker.Position : Vector3.zero;
    }
    
    /// <summary>
    /// Check if a waypoint is registered.
    /// </summary>
    public bool IsRegistered(string waypointId)
    {
        return !string.IsNullOrEmpty(waypointId) && waypoints.ContainsKey(waypointId);
    }
}

