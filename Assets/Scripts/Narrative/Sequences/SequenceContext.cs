using System.Collections.Generic;
using SignalJam.UI.MobileControls;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Runtime context bag for sequence execution.
/// Stores variables, handles, and service references.
/// </summary>
public class SequenceContext
{
    // Typed variable store with scope support
    private readonly SequenceVariableStore variableStore = new SequenceVariableStore();

    /// <summary>
    /// Child track context: copies common fields and reads parent global variables from <paramref name="parent"/>.
    /// </summary>
    public static SequenceContext CreateChildFrom(SequenceContext parent)
    {
        var child = new SequenceContext();
        if (parent == null)
            return child;
        child.PlayerName = parent.PlayerName;
        child.StarterSelected = parent.StarterSelected;
        child.variableStore.SetParentGlobalScope(parent.variableStore);
        return child;
    }
    
    // Stored handles (for cleanup)
    private readonly List<InputLockHandle> inputLocks = new List<InputLockHandle>();
    
    /// <summary>
    /// Named store of <see cref="MobileHudToken"/> handles acquired by mobile-HUD
    /// sequence steps. Designers pick a <c>tokenKey</c> so a paired "release" step
    /// (or sequence teardown) can resolve the correct handle. Mirrors the input-lock
    /// storage pattern.
    /// </summary>
    private readonly Dictionary<string, MobileHudToken> mobileHudTokens = new Dictionary<string, MobileHudToken>();
    private readonly Dictionary<string, GameplayMenuAvailabilityToken> gameplayMenuTokens = new Dictionary<string, GameplayMenuAvailabilityToken>();
    
    /// <summary>
    /// NPC narrative claims from <see cref="Step_NPC_ClaimControl"/> (one active claim per npcId).
    /// </summary>
    private readonly Dictionary<string, ClaimHandle> npcClaimsByNpcId = new Dictionary<string, ClaimHandle>();
    
    // Common variables (typed accessors)
    public string PlayerName { get; set; } = "";
    public string StarterSelected { get; set; } = "";
    public string CurrentSequenceId { get; set; } = "";
    public int CurrentStepIndex { get; set; } = -1;
    
    // Service references (lazy-loaded)
    private InputLockService _inputLockService;
    private DialogueService _dialogueService;
    private CameraShotController _cameraShotController;
    private SceneLoader _sceneLoader;
    private SpawnService _spawnService;
    private MobileHudService _mobileHudService;
    
    /// <summary>
    /// Get the variable store for typed variable access.
    /// </summary>
    public SequenceVariableStore Variables => variableStore;
    
    /// <summary>
    /// Get or set a variable by name (backwards compatibility).
    /// </summary>
    public object GetVariable(string name)
    {
        if (variableStore.TryGet<object>(name, out object value))
        {
            return value;
        }
        return null;
    }
    
    /// <summary>
    /// Set a variable by name (backwards compatibility).
    /// </summary>
    public void SetVariable(string name, object value)
    {
        // Try to preserve type
        if (value is bool b) variableStore.Set(name, b);
        else if (value is int i) variableStore.Set(name, i);
        else if (value is float f) variableStore.Set(name, f);
        else if (value is Vector3 v3) variableStore.Set(name, v3);
        else variableStore.Set(name, value?.ToString() ?? "", VariableScope.Local);
    }
    
    /// <summary>
    /// Get a typed variable.
    /// </summary>
    public T GetVariable<T>(string name)
    {
        return variableStore.Get<T>(name);
    }
    
    /// <summary>
    /// Check if a variable exists.
    /// </summary>
    public bool HasVariable(string name)
    {
        return variableStore.HasKey(name);
    }
    
    /// <summary>
    /// Store an input lock handle for later release.
    /// </summary>
    public void StoreInputLock(InputLockHandle handle)
    {
        if (handle.IsValid)
        {
            inputLocks.Add(handle);
        }
    }
    
    /// <summary>
    /// Release all stored input locks.
    /// </summary>
    public void ReleaseAllInputLocks()
    {
        foreach (var handle in inputLocks)
        {
            if (handle.IsValid && !handle.IsReleased)
            {
                handle.Release();
            }
        }
        inputLocks.Clear();
    }
    
    /// <summary>
    /// Store a <see cref="MobileHudToken"/> under a designer-chosen key so a later
    /// <c>Step_ReleaseMobileHudContext</c> can release it. If the key is already in
    /// use, the existing token is released before being replaced (prevents leaks if
    /// a Set step fires twice with the same key).
    /// </summary>
    public void StoreMobileHudToken(string tokenKey, MobileHudToken token)
    {
        if (string.IsNullOrEmpty(tokenKey) || !token.IsValid) return;
        
        if (mobileHudTokens.TryGetValue(tokenKey, out MobileHudToken existing) && existing.IsValid && !existing.IsReleased)
        {
            Debug.LogWarning(
                $"[SequenceContext] Replacing unreleased MobileHudToken '{tokenKey}'. Releasing previous handle.",
                null);
            existing.Release();
        }
        
        mobileHudTokens[tokenKey] = token;
    }
    
    /// <summary>
    /// Release the stored MobileHudToken for this key, if any.
    /// </summary>
    /// <returns>True if a handle was found and released.</returns>
    public bool TryReleaseMobileHudToken(string tokenKey)
    {
        if (string.IsNullOrEmpty(tokenKey)) return false;
        
        if (!mobileHudTokens.TryGetValue(tokenKey, out MobileHudToken token) || !token.IsValid)
        {
            return false;
        }
        
        if (!token.IsReleased)
        {
            token.Release();
        }
        mobileHudTokens.Remove(tokenKey);
        return true;
    }
    
    /// <summary>
    /// Retrieve the stored token handle by key without releasing it. Returns false
    /// when no token is stored under the key.
    /// </summary>
    public bool TryGetMobileHudToken(string tokenKey, out MobileHudToken token)
    {
        if (string.IsNullOrEmpty(tokenKey))
        {
            token = default;
            return false;
        }
        return mobileHudTokens.TryGetValue(tokenKey, out token);
    }
    
    /// <summary>
    /// Release every stored MobileHudToken (sequence stop / clear / completion safety net).
    /// </summary>
    public void ReleaseAllMobileHudTokens()
    {
        foreach (var kv in mobileHudTokens)
        {
            var handle = kv.Value;
            if (handle.IsValid && !handle.IsReleased)
            {
                handle.Release();
            }
        }
        mobileHudTokens.Clear();
    }

    public void StoreGameplayMenuAvailabilityToken(string tokenKey, GameplayMenuAvailabilityToken token)
    {
        if (string.IsNullOrEmpty(tokenKey) || !token.IsValid) return;

        if (gameplayMenuTokens.TryGetValue(tokenKey, out GameplayMenuAvailabilityToken existing) &&
            existing.IsValid &&
            !existing.IsReleased)
        {
            Debug.LogWarning(
                $"[SequenceContext] Replacing unreleased GameplayMenuAvailabilityToken '{tokenKey}'. Releasing previous handle.",
                null);
            existing.Release();
        }

        gameplayMenuTokens[tokenKey] = token;
    }

    public bool TryReleaseGameplayMenuAvailabilityToken(string tokenKey)
    {
        if (string.IsNullOrEmpty(tokenKey)) return false;

        if (!gameplayMenuTokens.TryGetValue(tokenKey, out GameplayMenuAvailabilityToken token) || !token.IsValid)
            return false;

        if (!token.IsReleased)
            token.Release();

        gameplayMenuTokens.Remove(tokenKey);
        return true;
    }

    public void ReleaseAllGameplayMenuAvailabilityTokens()
    {
        foreach (var kv in gameplayMenuTokens)
        {
            GameplayMenuAvailabilityToken token = kv.Value;
            if (token.IsValid && !token.IsReleased)
                token.Release();
        }

        gameplayMenuTokens.Clear();
    }
    
    /// <summary>
    /// Store a <see cref="ClaimHandle"/> from <see cref="NPCController.ClaimControl"/> so it can be
    /// released by <see cref="Step_NPC_ReleaseControl"/> or cleaned up when the sequence ends.
    /// </summary>
    public void StoreNpcClaim(string npcId, ClaimHandle handle)
    {
        if (string.IsNullOrEmpty(npcId) || handle == null)
            return;
        
        if (npcClaimsByNpcId.TryGetValue(npcId, out ClaimHandle previous) && previous != null)
        {
            Debug.LogWarning(
                $"[SequenceContext] Replacing unstored NPC claim for '{npcId}'. Releasing previous handle.",
                null);
            previous.Release();
        }
        
        npcClaimsByNpcId[npcId] = handle;
    }
    
    /// <summary>
    /// Release the stored claim handle for this NPC id, if any.
    /// </summary>
    /// <returns>True if a handle was found and released.</returns>
    public bool TryReleaseStoredNpcClaim(string npcId)
    {
        if (string.IsNullOrEmpty(npcId))
            return false;
        
        if (!npcClaimsByNpcId.TryGetValue(npcId, out ClaimHandle handle) || handle == null)
            return false;
        
        handle.Release();
        npcClaimsByNpcId.Remove(npcId);
        return true;
    }
    
    /// <summary>
    /// Release every stored NPC claim (sequence stop / clear / completion safety net).
    /// Also drops interaction-session retentions so an aborted track cannot leave an NPC held.
    /// </summary>
    public void ReleaseAllNpcClaims()
    {
        ReleaseAllNpcInteractionRetentions();

        foreach (var kv in npcClaimsByNpcId)
        {
            kv.Value?.Release();
        }
        
        npcClaimsByNpcId.Clear();
    }

    private int npcInteractionRetentions;

    /// <summary>
    /// Holds the active NPC interaction session across dialogue gaps. Returns false when no session exists.
    /// </summary>
    public bool TryRetainNpcInteraction()
    {
        NPCInteractionSessionService service = NPCInteractionSessionService.Instance;
        if (service == null || !service.HasActiveSession)
            return false;

        service.RetainActiveSession();
        npcInteractionRetentions++;
        return true;
    }

    /// <summary>
    /// Drops one interaction-session retention acquired by this context.
    /// </summary>
    public void ReleaseNpcInteractionRetention()
    {
        if (npcInteractionRetentions <= 0)
            return;

        npcInteractionRetentions--;
        NPCInteractionSessionService.Instance?.ReleaseActiveSessionRetention();
    }

    public void ReleaseAllNpcInteractionRetentions()
    {
        while (npcInteractionRetentions > 0)
            ReleaseNpcInteractionRetention();
    }
    
    // Service accessors (lazy-loaded)
    public InputLockService InputLockService
    {
        get
        {
            if (_inputLockService == null)
            {
                _inputLockService = InputLockService.Instance;
            }
            return _inputLockService;
        }
    }
    
    public DialogueService DialogueService
    {
        get
        {
            if (_dialogueService == null)
            {
                _dialogueService = DialogueService.Instance;
            }
            return _dialogueService;
        }
    }
    
    public CameraShotController CameraShotController
    {
        get
        {
            if (_cameraShotController == null)
            {
                _cameraShotController = CameraShotController.Instance;
            }
            return _cameraShotController;
        }
    }
    
    /// <summary>
    /// SceneLoader for LoadScene steps. Properly waits for full completion (warmup, spawn, etc.).
    /// </summary>
    public SceneLoader SceneLoader
    {
        get
        {
            if (_sceneLoader == null)
            {
                _sceneLoader = global::SceneLoader.Instance;
            }
            return _sceneLoader;
        }
    }

    public SpawnService SpawnService
    {
        get
        {
            if (_spawnService == null)
            {
                _spawnService = SpawnService.Instance;
            }
            return _spawnService;
        }
    }
    
    /// <summary>
    /// Authority for mobile HUD context. Used by <c>Step_SetMobileHudContext</c> and
    /// <c>Step_ReleaseMobileHudContext</c>. Lazy-loads via <see cref="MobileHudService.Instance"/>.
    /// </summary>
    public MobileHudService MobileHudService
    {
        get
        {
            if (_mobileHudService == null)
            {
                _mobileHudService = global::SignalJam.UI.MobileControls.MobileHudService.Instance;
            }
            return _mobileHudService;
        }
    }
    
    /// <summary>
    /// Clear all variables and release handles.
    /// </summary>
    public void Clear()
    {
        ReleaseAllNpcClaims();
        ReleaseAllInputLocks();
        ReleaseAllMobileHudTokens();
        ReleaseAllGameplayMenuAvailabilityTokens();
        variableStore.ClearLocal();
        PlayerName = "";
        StarterSelected = "";
        CurrentSequenceId = "";
        CurrentStepIndex = -1;
    }
    
    /// <summary>
    /// Clear all variables (local and global) and release handles.
    /// </summary>
    public void ClearAll()
    {
        ReleaseAllNpcClaims();
        ReleaseAllInputLocks();
        ReleaseAllMobileHudTokens();
        ReleaseAllGameplayMenuAvailabilityTokens();
        variableStore.ClearAll();
        PlayerName = "";
        StarterSelected = "";
        CurrentSequenceId = "";
        CurrentStepIndex = -1;
    }
}

