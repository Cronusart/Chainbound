using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry service for sequence triggers.
/// Tracks triggers by ID and provides awaitable wait functionality.
/// </summary>
public class TriggerRegistry : MonoBehaviour
{
    private static TriggerRegistry instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static TriggerRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<TriggerRegistry>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TriggerRegistry");
                    instance = go.AddComponent<TriggerRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Registry of triggers by ID
    private readonly Dictionary<string, bool> triggerStates = new Dictionary<string, bool>();
    private readonly Dictionary<string, System.Action> triggerCallbacks = new Dictionary<string, System.Action>();
    
    // Events
    public event Action<string> OnTriggerFired;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[TriggerRegistry] Duplicate instance found. Destroying {gameObject.name}");
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
    /// Register a trigger by ID.
    /// </summary>
    public void RegisterTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        
        triggerStates[triggerId] = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TriggerRegistry] Registered trigger: {triggerId}");
        }
    }
    
    /// <summary>
    /// Unregister a trigger.
    /// </summary>
    public void UnregisterTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        
        triggerStates.Remove(triggerId);
        triggerCallbacks.Remove(triggerId);
    }
    
    /// <summary>
    /// Fire a trigger (mark as triggered).
    /// </summary>
    public void FireTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;
        
        if (!triggerStates.ContainsKey(triggerId))
        {
            // Auto-register if not registered
            RegisterTrigger(triggerId);
        }
        
        triggerStates[triggerId] = true;
        
        // Invoke callback if waiting
        if (triggerCallbacks.TryGetValue(triggerId, out System.Action callback))
        {
            callback?.Invoke();
            triggerCallbacks.Remove(triggerId);
        }
        
        OnTriggerFired?.Invoke(triggerId);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TriggerRegistry] Trigger fired: {triggerId}");
        }
    }
    
    /// <summary>
    /// Check if a trigger has been fired.
    /// </summary>
    public bool IsTriggered(string triggerId)
    {
        triggerStates.TryGetValue(triggerId, out bool triggered);
        return triggered;
    }
    
    /// <summary>
    /// Reset a trigger (un-fire it).
    /// </summary>
    public void ResetTrigger(string triggerId)
    {
        if (triggerStates.ContainsKey(triggerId))
        {
            triggerStates[triggerId] = false;
        }
    }
    
    /// <summary>
    /// Wait for a trigger to fire. Returns coroutine for async completion.
    /// </summary>
    public Coroutine WaitForTrigger(string triggerId, MonoBehaviour runner)
    {
        if (string.IsNullOrEmpty(triggerId))
        {
            Debug.LogWarning("[TriggerRegistry] Cannot wait for empty trigger ID!");
            return null;
        }
        
        // Register if not registered
        if (!triggerStates.ContainsKey(triggerId))
        {
            RegisterTrigger(triggerId);
        }
        
        // Check if already triggered
        if (triggerStates[triggerId])
        {
            return null; // Already triggered, no need to wait
        }
        
        return runner.StartCoroutine(WaitForTriggerCoroutine(triggerId));
    }
    
    private IEnumerator WaitForTriggerCoroutine(string triggerId)
    {
        // Wait until triggered
        while (!triggerStates[triggerId])
        {
            yield return null;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TriggerRegistry] Wait complete for trigger: {triggerId}");
        }
    }
}

