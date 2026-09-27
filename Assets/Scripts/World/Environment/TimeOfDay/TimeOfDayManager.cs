using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TimeSourceMode
{
    LocalSimulated,
    SolanaCstRealTime
}

/// <summary>
/// Central time-of-day manager that advances time, broadcasts state changes,
/// and supports pausing/holding time for gameplay gates (dialogue, quests, tutorials).
/// 
/// Setup:
/// 1. Add to a persistent GameObject in your scene
/// 2. Assign a TimeOfDayConfigSO
/// 3. Subscribe to events or use the static Instance
/// </summary>
public class TimeOfDayManager : MonoBehaviour, ITimeOfDayController
{
    // ============================================
    // SINGLETON (Optional - prefer scene reference)
    // ============================================
    
    private static TimeOfDayManager instance;
    
    /// <summary>
    /// Event fired when TimeOfDayManager instance becomes available.
    /// Useful for components that need to connect after scene loads.
    /// </summary>
    public static event Action<TimeOfDayManager> OnInstanceAvailable;
    
    /// <summary>
    /// Singleton instance. Prefer using scene references via ITimeOfDayService.
    /// </summary>
    public static TimeOfDayManager Instance
    {
        get
        {
            // Check for both C# null and Unity destroyed object ("zombie")
            bool isNullOrDestroyed = ReferenceEquals(instance, null) || instance == null;
            
            if (isNullOrDestroyed)
            {
                // Clear zombie reference if present
                instance = null;
                
                // Try to find existing instance (including in DontDestroyOnLoad)
                // Use FindObjectsOfType (plural) as it's more reliable for DontDestroyOnLoad objects
                var managers = FindObjectsOfType<TimeOfDayManager>();
                if (managers.Length > 0)
                {
                    instance = managers[0];
                    Debug.Log($"[TimeOfDayManager] Found instance via FindObjectsOfType: {instance.gameObject.name} (ID: {instance.GetInstanceID()})");
                }
                else
                {
                    Debug.LogWarning("[TimeOfDayManager] No instance found in scene! Time-of-day features will not work. " +
                                    "Ensure TimeOfDayManager exists in GameServicesContainer.");
                }
            }
            return instance;
        }
    }
    
    /// <summary>
    /// Force re-broadcast of instance availability. 
    /// Call after scene loads to ensure all listeners reconnect.
    /// </summary>
    public static void BroadcastInstanceAvailable()
    {
        if (instance != null)
        {
            OnInstanceAvailable?.Invoke(instance);
        }
    }
    
    // ============================================
    // CONFIGURATION
    // ============================================
    
    [Header("Configuration")]
    [Tooltip("Time-of-day configuration asset")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Header("Initial State")]
    [Tooltip("Starting normalized time (0 = midnight, 0.5 = noon)")]
    [Range(0f, 1f)]
    [SerializeField] private float startingTime = 0.1f;
    
    [Tooltip("Start with time paused (for intro sequences)")]
    [SerializeField] private bool startPaused = false;
    
    [Header("Time Source")]
    [Tooltip("LocalSimulated = config-driven cycle. SolanaCstRealTime = Solana cluster CST time.")]
    [SerializeField] private TimeSourceMode timeSource = TimeSourceMode.LocalSimulated;

    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Tooltip("Preview time in editor (only works when ExecuteInEditMode)")]
    [Range(0f, 1f)]
    [SerializeField] private float editorPreviewTime = 0.5f;
    
    [Tooltip("Show debug info in game view")]
    [SerializeField] private bool showDebugUI = false;

#if UNITY_EDITOR
    [Header("Editor Override (Play Mode Only)")]
    [Tooltip("When enabled, forces normalizedTime to the override value during Play Mode.")]
    [SerializeField] private bool enableEditorTimeOverride = false;

    [Range(0f, 1f)]
    [SerializeField] private float editorOverrideNormalizedTime = 0.5f;

    [Tooltip("When enabled, forces the segment instead of deriving it from time.")]
    [SerializeField] private bool useEditorOverrideSegment = false;

    [SerializeField] private TimeSegment editorOverrideSegmentValue = TimeSegment.Night;
#endif
    
    // ============================================
    // STATE
    // ============================================
    
    private float normalizedTime;
    private TimeSegment currentSegment;
    private bool wasNight;
    
    private float lastBroadcastTime;
    private float lastBroadcastNormalizedTime;
    private bool loggedSolanaPresentationFallback;
    
    // Pause/Hold tracking
    private int nextHandleId = 1;
    private readonly Dictionary<int, PauseRequest> activePauses = new Dictionary<int, PauseRequest>();
    private readonly Dictionary<int, HoldRequest> activeHolds = new Dictionary<int, HoldRequest>();
    
    private struct PauseRequest
    {
        public string reason;
    }
    
    private struct HoldRequest
    {
        public string reason;
        public float? forcedTime;
        public TimeSegment? forcedSegment;
    }
    
    // ============================================
    // EVENTS (ITimeOfDayService)
    // ============================================
    
    public event Action<float> OnTimeNormalizedChanged;
    public event Action<TimeSegment, TimeSegment> OnSegmentChanged;
    public event Action<bool> OnDayNightChanged;
    public event Action<bool> OnPausedChanged;
    public event Action<bool, string> OnHoldChanged;
    
    // ============================================
    // PROPERTIES (ITimeOfDayService)
    // ============================================
    
    public float NormalizedTime => normalizedTime;
    public TimeSegment CurrentSegment => currentSegment;
    public bool IsNight => currentSegment == TimeSegment.Night;
    public bool IsPaused => activePauses.Count > 0;
    public bool IsHeld => activeHolds.Count > 0;
    
    public string HoldReason
    {
        get
        {
            if (activeHolds.Count == 0) return string.Empty;
            
            // Return the first hold's reason
            foreach (var hold in activeHolds.Values)
            {
                return hold.reason;
            }
            return string.Empty;
        }
    }
    
    /// <summary>Configuration asset.</summary>
    public TimeOfDayConfigSO Config => config;
    
    /// <summary>Real-time speed multiplier (1 = normal).</summary>
    public float TimeScale { get; set; } = 1f;
    
    /// <summary>Active time source mode.</summary>
    public TimeSourceMode ActiveTimeSource => timeSource;
    
    // ============================================
    // LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        // Singleton registration with proper Unity null check
        // Use ReferenceEquals for C# null, then Unity's == for destroyed object check
        bool hasExistingInstance = !ReferenceEquals(instance, null) && instance != null;
        
        // Log the current state for debugging
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Awake on {gameObject.name} (ID: {GetInstanceID()})" +
                     $"\n  - hasExistingInstance: {hasExistingInstance}" +
                     $"\n  - ReferenceEquals(instance, null): {ReferenceEquals(instance, null)}" +
                     $"\n  - instance == null (Unity): {instance == null}" +
                     (hasExistingInstance ? $"\n  - existing instance time: {instance.normalizedTime:F3}" : ""));
        }
        
        if (hasExistingInstance && instance != this)
        {
            // IMPORTANT: Log this even without debug mode to help diagnose issues
            Debug.Log($"[TimeOfDayManager] Duplicate detected! Keeping existing (t={instance.normalizedTime:F3}, ID: {instance.GetInstanceID()}). Destroying: {gameObject.name} (ID: {GetInstanceID()})");
            Destroy(gameObject);
            return;
        }
        
        // If instance was a "zombie" (C# reference exists but Unity destroyed), clean up
        if (!hasExistingInstance && !ReferenceEquals(instance, null))
        {
            Debug.Log("[TimeOfDayManager] Clearing stale zombie instance reference.");
            instance = null;
        }
        
        instance = this;
        
        // Persist across scene loads
        // NOTE: Only call DontDestroyOnLoad if we're a root object.
        // If we're a child of GameServicesContainer (which already has DontDestroyOnLoad), skip this.
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // We're a child - parent should handle DontDestroyOnLoad
            if (enableDebugLogs)
            {
                Debug.Log($"[TimeOfDayManager] Skipping DontDestroyOnLoad (child of {transform.parent.name})");
            }
        }
        
        // Initialize from the live clock in Solana mode so the first frame is not startingTime.
        normalizedTime = timeSource == TimeSourceMode.SolanaCstRealTime
            ? ResolveSolanaPresentationTime()
            : startingTime;
        UpdateSegment(forceEvent: false);
        wasNight = IsNight;
        lastBroadcastNormalizedTime = normalizedTime;
        
        // Log initialization (always, for debugging scene setup)
        Debug.Log($"[TimeOfDayManager] Initialized as primary instance: {gameObject.name} (ID: {GetInstanceID()}), t={normalizedTime:F3}, segment={currentSegment}");
    }
    
    private void Start()
    {
        // Subscribe to scene loaded events to broadcast time after transitions
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Apply initial pause if configured
        if (startPaused)
        {
            AcquirePause("Initial pause");
        }
        
        // Broadcast initial state
        OnTimeNormalizedChanged?.Invoke(normalizedTime);
    }
    
    /// <summary>
    /// Called when a new scene loads. Broadcasts current time to any new subscribers.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Scene loaded: {scene.name}, broadcasting current time: {normalizedTime:F3}");
        }
        
        // Wait a frame for new subscribers to register, then broadcast
        StartCoroutine(BroadcastAfterDelay());
    }
    
    private System.Collections.IEnumerator BroadcastAfterDelay()
    {
        yield return null; // Wait one frame
        yield return null; // Wait another frame for safety
        
        // Broadcast instance availability first (for components that need to reconnect)
        OnInstanceAvailable?.Invoke(this);
        
        // Broadcast current time to all subscribers
        OnTimeNormalizedChanged?.Invoke(normalizedTime);
        
        // Also broadcast segment and day/night state
        // Note: For scene load broadcasts, old and new segment are the same (no transition)
        OnSegmentChanged?.Invoke(currentSegment, currentSegment);
        OnDayNightChanged?.Invoke(IsNight);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Broadcast complete after scene load. Listeners can now connect.");
        }
    }
    
    private void Update()
    {
        // Safeguard: Ensure we're still the registered instance
        // This handles edge cases where the static reference got cleared
        if (instance != this)
        {
            // Check if current instance is a zombie or null
            bool instanceIsNullOrZombie = ReferenceEquals(instance, null) || instance == null;
            if (instanceIsNullOrZombie)
            {
                instance = this;
                if (enableDebugLogs)
                {
                    Debug.Log($"[TimeOfDayManager] Re-registered as singleton instance (t={normalizedTime:F3})");
                }
            }
        }
        
        // ── Editor override takes absolute priority in Play Mode ──
#if UNITY_EDITOR
        if (enableEditorTimeOverride && Application.isPlaying)
        {
            normalizedTime = editorOverrideNormalizedTime;
            if (useEditorOverrideSegment)
            {
                ForceSegment(editorOverrideSegmentValue);
            }
            else
            {
                UpdateSegment(forceEvent: true);
            }
            BroadcastTimeIfNeeded();
            return;
        }
#endif

        // Skip time advancement if held or paused
        if (IsHeld)
        {
            ApplyHoldTime();
        }
        else if (!IsPaused)
        {
            if (timeSource == TimeSourceMode.SolanaCstRealTime)
            {
                AdvanceSolanaTime();
            }
            else
            {
                AdvanceTime();
            }
        }
        
        // Throttled time broadcast
        BroadcastTimeIfNeeded();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        if (instance == this)
        {
            instance = null;
        }
    }
    
    // ============================================
    // TIME ADVANCEMENT
    // ============================================
    
    private void AdvanceTime()
    {
        if (config == null) return;
        
        // Calculate time delta
        float realDelta = Time.deltaTime * TimeScale;
        float normalizedDelta = realDelta / config.FullDaySeconds;
        
        // Advance time (wrap at 1.0)
        normalizedTime += normalizedDelta;
        if (normalizedTime >= 1f)
        {
            normalizedTime -= 1f;
        }
        
        // Check for segment change
        UpdateSegment(forceEvent: true);
    }
    
    /// <summary>
    /// Optional callback for Solana CST time. Set by Chainbound.Combat at runtime
    /// to avoid assembly circular dependency. Returns (normalizedTime, hasSynced).
    /// </summary>
    public static Func<(float normalizedTime, bool hasSynced)?> GetSolanaCstTime;

    private void AdvanceSolanaTime()
    {
        float previous = normalizedTime;
        (float normalizedTime, bool hasSynced)? result = GetSolanaCstTime?.Invoke();
        if (result != null && result.Value.hasSynced)
        {
            normalizedTime = TimeOfDayClock.Wrap01(result.Value.normalizedTime);
        }
        else
        {
            // Presentation-only. Durable mechanics use server-side Solana block time.
            normalizedTime = TimeOfDayClock.FromDeviceUtcMinusSix();
            if (!loggedSolanaPresentationFallback)
            {
                loggedSolanaPresentationFallback = true;
                Debug.LogWarning("[TimeOfDayManager] Solana time is unavailable; using device UTC-6 for presentation only.");
            }
        }

        if (Mathf.Abs(normalizedTime - previous) > 0.0005f)
            lastBroadcastTime = -1f;

        UpdateSegment(forceEvent: true);
    }

    private static float ResolveSolanaPresentationTime()
    {
        (float normalizedTime, bool hasSynced)? result = GetSolanaCstTime?.Invoke();
        if (result != null && result.Value.hasSynced)
            return TimeOfDayClock.Wrap01(result.Value.normalizedTime);

        return TimeOfDayClock.FromDeviceUtcMinusSix();
    }

    private void ForceSegment(TimeSegment target)
    {
        if (target != currentSegment)
        {
            TimeSegment old = currentSegment;
            currentSegment = target;
            OnSegmentChanged?.Invoke(old, target);

            bool isNightNow = IsNight;
            if (isNightNow != wasNight)
            {
                wasNight = isNightNow;
                OnDayNightChanged?.Invoke(isNightNow);
            }
        }
    }

    private void ApplyHoldTime()
    {
        if (activeHolds.Count == 0) return;
        
        // Get the first (most recent) hold
        float? forcedTime = null;
        TimeSegment? forcedSegment = null;
        
        foreach (var hold in activeHolds.Values)
        {
            forcedTime = hold.forcedTime;
            forcedSegment = hold.forcedSegment;
            break;
        }
        
        // Apply forced time
        if (forcedTime.HasValue)
        {
            normalizedTime = forcedTime.Value;
        }
        else if (forcedSegment.HasValue && config != null)
        {
            normalizedTime = config.GetSegmentMidpoint(forcedSegment.Value);
        }
        
        // Update segment (but don't fire repeated events)
        UpdateSegment(forceEvent: false);
    }
    
    private void UpdateSegment(bool forceEvent)
    {
        if (config == null) return;
        
        TimeSegment newSegment = config.GetSegment(normalizedTime);
        
        if (newSegment != currentSegment)
        {
            TimeSegment oldSegment = currentSegment;
            currentSegment = newSegment;
            
            if (forceEvent)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[TimeOfDayManager] Segment changed: {oldSegment} -> {newSegment} at t={normalizedTime:F3}");
                }
                
                OnSegmentChanged?.Invoke(oldSegment, newSegment);
                
                // Check day/night transition
                bool isNightNow = IsNight;
                if (isNightNow != wasNight)
                {
                    wasNight = isNightNow;
                    OnDayNightChanged?.Invoke(isNightNow);
                }
            }
        }
    }
    
    private void BroadcastTimeIfNeeded()
    {
        if (config == null) return;
        
        float timeSinceLastBroadcast = Time.time - lastBroadcastTime;
        
        if (timeSinceLastBroadcast >= config.UpdateInterval)
        {
            // Only broadcast if time actually changed
            if (!Mathf.Approximately(normalizedTime, lastBroadcastNormalizedTime))
            {
                lastBroadcastTime = Time.time;
                lastBroadcastNormalizedTime = normalizedTime;
                OnTimeNormalizedChanged?.Invoke(normalizedTime);
            }
        }
    }
    
    // ============================================
    // PAUSE/HOLD SYSTEM (ITimeOfDayController)
    // ============================================
    
    public PauseHandle AcquirePause(string reason)
    {
        int id = nextHandleId++;
        activePauses[id] = new PauseRequest { reason = reason };
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Pause acquired: '{reason}' (id={id}, total={activePauses.Count})");
        }
        
        // Notify if this is the first pause
        if (activePauses.Count == 1)
        {
            OnPausedChanged?.Invoke(true);
        }
        
        return new PauseHandle(id, reason, ReleasePause);
    }
    
    private void ReleasePause(int id)
    {
        if (!activePauses.TryGetValue(id, out var request))
        {
            return;
        }
        
        activePauses.Remove(id);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Pause released: '{request.reason}' (id={id}, remaining={activePauses.Count})");
        }
        
        // Notify if this was the last pause
        if (activePauses.Count == 0)
        {
            OnPausedChanged?.Invoke(false);
        }
    }
    
    public HoldHandle AcquireHold(string reason, float? forcedTimeNormalized = null, TimeSegment? forcedSegment = null)
    {
        int id = nextHandleId++;
        activeHolds[id] = new HoldRequest
        {
            reason = reason,
            forcedTime = forcedTimeNormalized,
            forcedSegment = forcedSegment
        };
        
        if (enableDebugLogs)
        {
            string timeInfo = forcedTimeNormalized.HasValue ? $"t={forcedTimeNormalized.Value:F3}" :
                              forcedSegment.HasValue ? $"segment={forcedSegment.Value}" : "current";
            Debug.Log($"[TimeOfDayManager] Hold acquired: '{reason}' at {timeInfo} (id={id}, total={activeHolds.Count})");
        }
        
        // Apply the hold immediately
        ApplyHoldTime();
        
        // Notify if this is the first hold
        if (activeHolds.Count == 1)
        {
            OnHoldChanged?.Invoke(true, reason);
        }
        
        return new HoldHandle(id, reason, forcedTimeNormalized, forcedSegment, ReleaseHold);
    }
    
    private void ReleaseHold(int id)
    {
        if (!activeHolds.TryGetValue(id, out var request))
        {
            return;
        }
        
        activeHolds.Remove(id);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Hold released: '{request.reason}' (id={id}, remaining={activeHolds.Count})");
        }
        
        // Notify if this was the last hold
        if (activeHolds.Count == 0)
        {
            OnHoldChanged?.Invoke(false, string.Empty);
        }
        else
        {
            // Apply the next hold in the stack
            ApplyHoldTime();
            
            // Notify with new hold's reason
            foreach (var hold in activeHolds.Values)
            {
                OnHoldChanged?.Invoke(true, hold.reason);
                break;
            }
        }
    }
    
    public void SetTime(float normalizedTimeValue)
    {
        // Wrap to [0..1) range
        normalizedTimeValue = normalizedTimeValue - Mathf.Floor(normalizedTimeValue);
        
        normalizedTime = normalizedTimeValue;
        UpdateSegment(forceEvent: true);
        
        // Broadcast immediately
        OnTimeNormalizedChanged?.Invoke(normalizedTime);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayManager] Time set to {normalizedTime:F3}, segment={currentSegment}");
        }
    }
    
    // ============================================
    // CONVENIENCE METHODS
    // ============================================
    
    /// <summary>
    /// Set time to the midpoint of a specific segment.
    /// </summary>
    public void SetTimeToSegment(TimeSegment segment)
    {
        if (config != null)
        {
            SetTime(config.GetSegmentMidpoint(segment));
        }
    }
    
    /// <summary>
    /// Skip to the next segment transition.
    /// </summary>
    public void SkipToNextSegment()
    {
        if (config == null) return;
        
        TimeSegment next;
        switch (currentSegment)
        {
            case TimeSegment.Night:
                next = TimeSegment.Dawn;
                break;
            case TimeSegment.Dawn:
                next = TimeSegment.Day;
                break;
            case TimeSegment.Day:
                next = TimeSegment.Dusk;
                break;
            case TimeSegment.Dusk:
            default:
                next = TimeSegment.Night;
                break;
        }
        
        SetTime(config.GetSegmentMidpoint(next));
    }
    
    /// <summary>
    /// Get time in hours (0-24) for display purposes.
    /// </summary>
    public float GetTimeInHours()
    {
        return TimeOfDayClock.NormalizedToHours(normalizedTime);
    }
    
    /// <summary>
    /// Get formatted time string (HH:MM).
    /// </summary>
    public string GetTimeString()
    {
        float hours = GetTimeInHours();
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.FloorToInt((hours - h) * 60f);
        return $"{h:D2}:{m:D2}";
    }
    
    // ============================================
    // DEBUG UI
    // ============================================
    
    private void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"Time: {GetTimeString()} CST ({normalizedTime:F3})");
        GUILayout.Label($"Segment: {currentSegment}");
        GUILayout.Label($"Source: {timeSource}");
        GUILayout.Label($"Is Night: {IsNight}");
        GUILayout.Label($"Paused: {IsPaused} ({activePauses.Count} handles)");
        GUILayout.Label($"Held: {IsHeld} ({activeHolds.Count} handles)");
        if (IsHeld)
        {
            GUILayout.Label($"  Reason: {HoldReason}");
        }
#if UNITY_EDITOR
        if (enableEditorTimeOverride)
        {
            GUILayout.Label($"  EDITOR OVERRIDE ACTIVE");
        }
#endif
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    
    // ============================================
    // EDITOR PREVIEW
    // ============================================
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && config != null)
        {
            normalizedTime = editorPreviewTime;
            UpdateSegment(forceEvent: false);
            OnTimeNormalizedChanged?.Invoke(normalizedTime);
        }

        if (Application.isPlaying && enableEditorTimeOverride)
        {
            normalizedTime = editorOverrideNormalizedTime;
            if (useEditorOverrideSegment)
                ForceSegment(editorOverrideSegmentValue);
            else
                UpdateSegment(forceEvent: true);
            OnTimeNormalizedChanged?.Invoke(normalizedTime);
        }
    }
#endif
}

