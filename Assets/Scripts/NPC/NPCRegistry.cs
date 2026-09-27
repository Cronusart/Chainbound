using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton registry mapping NPC IDs to NPCController instances.
/// NPCs self-register on Awake and unregister on Destroy.
/// Used by sequence steps to find NPCs by ID.
/// </summary>
public class NPCRegistry : MonoBehaviour
{
    private static NPCRegistry instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static NPCRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NPCRegistry>();
                if (instance == null)
                {
                    GameObject go = new GameObject("NPCRegistry");
                    instance = go.AddComponent<NPCRegistry>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Registry of NPCs by ID
    private readonly Dictionary<string, NPCController> npcs = new Dictionary<string, NPCController>();
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[NPCRegistry] Duplicate instance found. Destroying {gameObject.name}");
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
    /// Register an NPC by ID.
    /// </summary>
    public void RegisterNPC(string npcId, NPCController controller)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[NPCRegistry] Cannot register NPC with empty ID!");
            return;
        }
        
        if (controller == null)
        {
            Debug.LogWarning($"[NPCRegistry] Cannot register NPC '{npcId}' with null controller!");
            return;
        }
        
        if (npcs.ContainsKey(npcId))
        {
            Debug.LogWarning($"[NPCRegistry] NPC '{npcId}' already registered! Overwriting.");
        }
        
        npcs[npcId] = controller;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCRegistry] Registered NPC: {npcId}");
        }
    }
    
    /// <summary>
    /// Unregister an NPC by ID.
    /// </summary>
    public void UnregisterNPC(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        
        if (npcs.Remove(npcId) && enableDebugLogs)
        {
            Debug.Log($"[NPCRegistry] Unregistered NPC: {npcId}");
        }
    }
    
    /// <summary>
    /// Get NPC controller by ID.
    /// </summary>
    public NPCController GetNPC(string npcId)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            return null;
        }
        
        npcs.TryGetValue(npcId, out NPCController controller);
        return controller;
    }
    
    /// <summary>
    /// Check if an NPC is registered.
    /// </summary>
    public bool IsRegistered(string npcId)
    {
        return !string.IsNullOrEmpty(npcId) && npcs.ContainsKey(npcId);
    }
    
    /// <summary>
    /// Get all registered NPC IDs.
    /// </summary>
    public IEnumerable<string> GetAllNPCIds()
    {
        return npcs.Keys;
    }
}

