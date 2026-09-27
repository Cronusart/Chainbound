using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized input lock service using token-based handles.
/// Prevents multiple systems from fighting over input control.
/// 
/// Usage:
/// var handle = InputLockService.Instance.AcquireLock("Dialogue", InputLockChannel.All);
/// // ... do blocking operation ...
/// handle.Release();
/// </summary>
public class InputLockService : MonoBehaviour
{
    private static InputLockService instance;

    /// <summary>
    /// Raised after a lock is registered. UI adapters can react immediately while
    /// ignoring locks owned by themselves.
    /// </summary>
    public event Action<InputLockChannel, UnityEngine.Object> LockAcquired;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static InputLockService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<InputLockService>();
                if (instance == null)
                {
                    if (!Application.isPlaying)
                        return null;
                    GameObject go = new GameObject("InputLockService");
                    instance = go.AddComponent<InputLockService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugUI = false;
    
    // Lock tracking
    private int nextHandleId = 1;
    private readonly Dictionary<int, LockRequest> activeLocks = new Dictionary<int, LockRequest>();
    
    private struct LockRequest
    {
        public string reason;
        public InputLockChannel channel;
        public UnityEngine.Object owner;
        public bool hasOwner;
        public int ownerSceneHandle;
        public bool ownerWasPersistent;
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Acquire an input lock. Returns a handle that must be released when done.
    /// </summary>
    /// <param name="reason">Reason for locking (for debugging)</param>
    /// <param name="channel">Which input channel to lock</param>
    /// <returns>Handle to release when done</returns>
    public InputLockHandle AcquireLock(
        string reason,
        InputLockChannel channel = InputLockChannel.All,
        UnityEngine.Object owner = null)
    {
        int id = nextHandleId++;
        ResolveOwnerScene(owner, out int ownerSceneHandle, out bool ownerWasPersistent);
        activeLocks[id] = new LockRequest
        {
            reason = reason,
            channel = channel,
            owner = owner,
            hasOwner = owner != null,
            ownerSceneHandle = ownerSceneHandle,
            ownerWasPersistent = ownerWasPersistent,
        };

        LockAcquired?.Invoke(channel, owner);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[InputLockService] Lock acquired: '{reason}' on {channel} (id={id}, total={activeLocks.Count})");
        }
        
        return new InputLockHandle(id, reason, channel, ReleaseLock);
    }
    
    /// <summary>
    /// Check if a specific channel is currently locked.
    /// </summary>
    /// <param name="channel">Channel to check</param>
    /// <returns>True if locked</returns>
    public bool IsLocked(InputLockChannel channel)
    {
        foreach (var lockReq in activeLocks.Values)
        {
            if (lockReq.channel == InputLockChannel.All || lockReq.channel == channel)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Check if movement is locked.
    /// </summary>
    public bool IsMovementLocked => IsLocked(InputLockChannel.Movement) || IsLocked(InputLockChannel.All);
    
    /// <summary>
    /// Check if interaction is locked.
    /// </summary>
    public bool IsInteractionLocked => IsLocked(InputLockChannel.Interaction) || IsLocked(InputLockChannel.All);

    /// <summary>
    /// Check if the gameplay player menu is locked. Narrative/dialogue locks authored
    /// with <see cref="InputLockChannel.All"/> suppress the menu automatically.
    /// </summary>
    public bool IsPlayerMenuLocked => IsLocked(InputLockChannel.PlayerMenu);

    /// <summary>
    /// True when movement or interaction is locked (including <see cref="InputLockChannel.All"/>).
    /// Encounter zones and similar world volumes skip launches while frozen.
    /// </summary>
    public bool IsWorldGameplayFrozen => IsMovementLocked || IsInteractionLocked;

    /// <summary>
    /// Looks up a live service without spawning one. Missing service means not frozen.
    /// </summary>
    public static bool BlocksWorldGameplay
    {
        get
        {
            InputLockService service = instance != null
                ? instance
                : FindFirstObjectByType<InputLockService>();
            return service != null && service.IsWorldGameplayFrozen;
        }
    }
    
    /// <summary>
    /// Get all active lock reasons (for debugging).
    /// </summary>
    public List<string> GetActiveLockReasons()
    {
        var reasons = new List<string>();
        foreach (var lockReq in activeLocks.Values)
        {
            reasons.Add($"{lockReq.reason} ({lockReq.channel})");
        }
        return reasons;
    }
    
    /// <summary>
    /// Get count of active locks.
    /// </summary>
    public int ActiveLockCount => activeLocks.Count;
    
    private void ReleaseLock(int id)
    {
        if (!activeLocks.TryGetValue(id, out var request))
        {
            return;
        }
        
        activeLocks.Remove(id);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[InputLockService] Lock released: '{request.reason}' (id={id}, remaining={activeLocks.Count})");
        }
    }
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[InputLockService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (activeLocks.Count == 0)
            return;

        List<int> staleIds = null;
        foreach (KeyValuePair<int, LockRequest> pair in activeLocks)
        {
            LockRequest request = pair.Value;
            bool ownerDestroyed = request.hasOwner && request.owner == null;
            bool ownedByUnloadedScene = request.hasOwner &&
                                        !request.ownerWasPersistent &&
                                        request.ownerSceneHandle == scene.handle;
            if (!ownerDestroyed && !ownedByUnloadedScene)
                continue;

            staleIds ??= new List<int>();
            staleIds.Add(pair.Key);
        }

        if (staleIds == null)
            return;

        for (int i = 0; i < staleIds.Count; i++)
            ReleaseLock(staleIds[i]);
    }

    private static void ResolveOwnerScene(
        UnityEngine.Object owner,
        out int sceneHandle,
        out bool isPersistent)
    {
        sceneHandle = -1;
        isPersistent = false;
        if (owner == null)
            return;

        GameObject ownerObject = owner as GameObject;
        if (owner is Component component)
            ownerObject = component.gameObject;
        if (ownerObject == null)
            return;

        Scene scene = ownerObject.scene;
        if (!scene.IsValid())
            return;

        sceneHandle = scene.handle;
        isPersistent = string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
    }
    
    // ============================================
    // DEBUG UI
    // ============================================
    
    private void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 400, 140));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Input Lock Service");
        GUILayout.Label($"Movement Locked: {IsMovementLocked}");
        GUILayout.Label($"Interaction Locked: {IsInteractionLocked}");
        GUILayout.Label($"Active Locks: {activeLocks.Count}");
        
        if (activeLocks.Count > 0)
        {
            GUILayout.Label("Reasons:");
            foreach (var lockReq in activeLocks.Values)
            {
                GUILayout.Label($"  - {lockReq.reason} ({lockReq.channel})");
            }
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

