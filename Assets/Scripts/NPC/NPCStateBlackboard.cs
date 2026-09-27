using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Typed state variables for internal NPC logic.
/// Used by NPCController and subsystems to track state without hard-coding values.
/// </summary>
public class NPCStateBlackboard : MonoBehaviour
{
    [Header("State")]
    [Tooltip("Current internal state (for NPC logic, not animation state)")]
    [SerializeField] private NPCInternalState currentState = NPCInternalState.Idle;
    
    [Tooltip("Current target waypoint ID (if following route)")]
    [SerializeField] private string currentTargetWaypointId = "";
    
    [Tooltip("Current target position (if moving to world position)")]
    [SerializeField] private Vector3 currentTargetPosition = Vector3.zero;
    
    [Tooltip("Is using world position target (true) or waypoint ID (false)")]
    [SerializeField] private bool useWorldPositionTarget = false;
    
    [Header("Flags")]
    [Tooltip("Custom boolean flags for NPC logic")]
    [SerializeField] private SerializableDictionary<string, bool> flags = new SerializableDictionary<string, bool>();
    
    // Properties
    public NPCInternalState CurrentState
    {
        get => currentState;
        set => currentState = value;
    }
    
    public string CurrentTargetWaypointId
    {
        get => currentTargetWaypointId;
        set
        {
            currentTargetWaypointId = value;
            useWorldPositionTarget = false;
        }
    }
    
    public Vector3 CurrentTargetPosition
    {
        get => currentTargetPosition;
        set
        {
            currentTargetPosition = value;
            useWorldPositionTarget = true;
        }
    }
    
    public bool UseWorldPositionTarget => useWorldPositionTarget;
    
    /// <summary>
    /// Set a flag value.
    /// </summary>
    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        flags[key] = value;
    }
    
    /// <summary>
    /// Get a flag value (defaults to false if not set).
    /// </summary>
    public bool GetFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        flags.TryGetValue(key, out bool value);
        return value;
    }
    
    /// <summary>
    /// Clear a flag.
    /// </summary>
    public void ClearFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        flags.Remove(key);
    }
    
    /// <summary>
    /// Clear all flags.
    /// </summary>
    public void ClearAllFlags()
    {
        flags.Clear();
    }
}

/// <summary>
/// Internal state enum for NPC logic.
/// </summary>
public enum NPCInternalState
{
    Idle,
    MovingToWaypoint,
    FollowingRoute,
    FollowingTarget,
    WaitingForInteraction,
    NarrativeControlled
}

/// <summary>
/// Serializable dictionary for Unity inspector support.
/// </summary>
[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();
    
    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }
    
    public void OnAfterDeserialize()
    {
        Clear();
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            this[keys[i]] = values[i];
        }
    }
}

