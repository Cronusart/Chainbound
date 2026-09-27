using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent memory component for NPCs.
/// Stores disposition, flags, counters, and last interaction time.
/// Used for reactive dialogue and quest outcomes.
/// </summary>
public class NPCMemoryComponent : MonoBehaviour
{
    [Header("Disposition")]
    [Tooltip("NPC's disposition toward player (-100 = hostile, 0 = neutral, +100 = friendly)")]
    [Range(-100, 100)]
    [SerializeField] private int disposition = 0;
    
    [Header("Flags")]
    [Tooltip("Boolean flags for NPC memory (e.g., 'quest_completed', 'gave_item')")]
    [SerializeField] private SerializableDictionary<string, bool> flags = new SerializableDictionary<string, bool>();
    
    [Header("Counters")]
    [Tooltip("Integer counters for NPC memory (e.g., 'times_visited', 'items_given')")]
    [SerializeField] private SerializableDictionary<string, int> counters = new SerializableDictionary<string, int>();
    
    [Header("Interaction Tracking")]
    [Tooltip("Last time player interacted with this NPC (normalized time)")]
    [SerializeField] private float lastInteractionTime = -1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Properties
    public int Disposition
    {
        get => disposition;
        set => disposition = Mathf.Clamp(value, -100, 100);
    }
    
    public float LastInteractionTime => lastInteractionTime;
    
    // ============================================
    // DISPOSITION
    // ============================================
    
    /// <summary>
    /// Add to disposition value (clamped to -100..100).
    /// </summary>
    public void AddDisposition(int amount)
    {
        int oldDisposition = disposition;
        disposition = Mathf.Clamp(disposition + amount, -100, 100);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} disposition: {oldDisposition} -> {disposition} (delta: {amount})");
        }
    }
    
    /// <summary>
    /// Set disposition value (clamped to -100..100).
    /// </summary>
    public void SetDisposition(int value)
    {
        int oldDisposition = disposition;
        disposition = Mathf.Clamp(value, -100, 100);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} disposition set: {oldDisposition} -> {disposition}");
        }
    }
    
    // ============================================
    // FLAGS
    // ============================================
    
    /// <summary>
    /// Set a flag value.
    /// </summary>
    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NPCMemoryComponent] Cannot set flag with empty key!");
            return;
        }
        
        bool oldValue = flags.ContainsKey(key) ? flags[key] : false;
        flags[key] = value;
        
        if (enableDebugLogs && oldValue != value)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} flag '{key}': {oldValue} -> {value}");
        }
    }
    
    /// <summary>
    /// Get a flag value (defaults to false if not set).
    /// </summary>
    public bool GetFlag(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        
        flags.TryGetValue(key, out bool value);
        return value;
    }
    
    /// <summary>
    /// Check if a flag exists.
    /// </summary>
    public bool HasFlag(string key)
    {
        return !string.IsNullOrEmpty(key) && flags.ContainsKey(key);
    }
    
    /// <summary>
    /// Clear a flag.
    /// </summary>
    public void ClearFlag(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        
        if (flags.Remove(key) && enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} cleared flag: {key}");
        }
    }
    
    /// <summary>
    /// Clear all flags.
    /// </summary>
    public void ClearAllFlags()
    {
        flags.Clear();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} cleared all flags");
        }
    }
    
    // ============================================
    // COUNTERS
    // ============================================
    
    /// <summary>
    /// Increment a counter by amount (default 1).
    /// </summary>
    public void IncrementCounter(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NPCMemoryComponent] Cannot increment counter with empty key!");
            return;
        }
        
        int oldValue = counters.ContainsKey(key) ? counters[key] : 0;
        counters[key] = oldValue + amount;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} counter '{key}': {oldValue} -> {counters[key]} (delta: {amount})");
        }
    }
    
    /// <summary>
    /// Decrement a counter by amount (default 1).
    /// </summary>
    public void DecrementCounter(string key, int amount = 1)
    {
        IncrementCounter(key, -amount);
    }
    
    /// <summary>
    /// Set a counter value.
    /// </summary>
    public void SetCounter(string key, int value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NPCMemoryComponent] Cannot set counter with empty key!");
            return;
        }
        
        int oldValue = counters.ContainsKey(key) ? counters[key] : 0;
        counters[key] = value;
        
        if (enableDebugLogs && oldValue != value)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} counter '{key}' set: {oldValue} -> {value}");
        }
    }
    
    /// <summary>
    /// Get a counter value (defaults to 0 if not set).
    /// </summary>
    public int GetCounter(string key)
    {
        if (string.IsNullOrEmpty(key))
            return 0;
        
        counters.TryGetValue(key, out int value);
        return value;
    }
    
    /// <summary>
    /// Check if a counter exists.
    /// </summary>
    public bool HasCounter(string key)
    {
        return !string.IsNullOrEmpty(key) && counters.ContainsKey(key);
    }
    
    /// <summary>
    /// Clear a counter.
    /// </summary>
    public void ClearCounter(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        
        if (counters.Remove(key) && enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} cleared counter: {key}");
        }
    }
    
    /// <summary>
    /// Clear all counters.
    /// </summary>
    public void ClearAllCounters()
    {
        counters.Clear();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCMemoryComponent] {gameObject.name} cleared all counters");
        }
    }
    
    // ============================================
    // INTERACTION TRACKING
    // ============================================
    
    /// <summary>
    /// Record interaction with player (updates last interaction time).
    /// </summary>
    public void RecordInteraction()
    {
        ITimeOfDayService timeService = FindObjectOfType<TimeOfDayManager>();
        if (timeService != null)
        {
            lastInteractionTime = timeService.NormalizedTime;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[NPCMemoryComponent] {gameObject.name} interaction recorded at time: {lastInteractionTime:F3}");
            }
        }
        else
        {
            Debug.LogWarning("[NPCMemoryComponent] Cannot record interaction: TimeOfDayService not found!");
        }
    }
    
    /// <summary>
    /// Get time since last interaction (in normalized time, 0-1).
    /// Returns -1 if never interacted.
    /// </summary>
    public float GetTimeSinceLastInteraction()
    {
        if (lastInteractionTime < 0f)
            return -1f;
        
        ITimeOfDayService timeService = FindObjectOfType<TimeOfDayManager>();
        if (timeService == null)
            return -1f;
        
        float currentTime = timeService.NormalizedTime;
        float elapsed = currentTime - lastInteractionTime;
        
        // Handle wrap-around (if current time < last interaction time, day has wrapped)
        if (elapsed < 0f)
        {
            elapsed = (1f - lastInteractionTime) + currentTime;
        }
        
        return elapsed;
    }
}

