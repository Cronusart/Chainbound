using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Component for windows that show outdoor time-of-day cues even when indoors.
/// Uses MaterialPropertyBlock for efficient per-instance updates without material instancing.
/// 
/// Setup:
/// 1. Attach to window GameObject (or parent)
/// 2. Assign Renderer reference (or auto-finds)
/// 3. Configure emission/tint curves/gradients
/// 4. Set shader property names to match your window material
/// </summary>
public class WindowTimeOfDayObject : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Time source override (optional). Leave empty to use TimeOfDayManager singleton. " +
             "NOTE: Cross-scene references will be lost on scene reload - prefer using singleton.")]
    [SerializeField] private MonoBehaviour timeSourceOverride;
    
    [Tooltip("Renderer to update. Auto-finds if null.")]
    [SerializeField] private Renderer targetRenderer;
    
    [Tooltip("Auto-find renderer on this object or children")]
    [SerializeField] private bool autoFindRenderer = true;
    
    [Header("Shader Properties")]
    [Tooltip("Material property name for emission color")]
    [SerializeField] private string emissionProperty = "_EmissionColor";
    
    [Tooltip("Material property name for base color/tint")]
    [SerializeField] private string tintProperty = "_BaseColor";
    
    [Header("Day Settings")]
    [Tooltip("Emission color during day (simulates daylight through window)")]
    [SerializeField] private Gradient dayEmissionColor = CreateDefaultDayEmission();
    
    [Tooltip("Emission strength curve over time (peaks at midday)")]
    [SerializeField] private AnimationCurve dayEmissionStrengthByTime = CreateDefaultDayStrength();
    
    [Tooltip("Base emission intensity multiplier")]
    [Range(0f, 10f)]
    [SerializeField] private float dayEmissionIntensity = 2f;
    
    [Header("Night Settings")]
    [Tooltip("Window tint color at night (darker/bluer)")]
    [SerializeField] private Gradient nightWindowTint = CreateDefaultNightTint();
    
    [Tooltip("Night tint strength curve over time")]
    [SerializeField] private AnimationCurve nightTintStrengthByTime = CreateDefaultNightStrength();
    
    [Tooltip("Base tint intensity multiplier")]
    [Range(0f, 1f)]
    [SerializeField] private float nightTintIntensity = 0.3f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Runtime state
    private ITimeOfDayService timeService;
    private MaterialPropertyBlock mpb;
    private bool isSubscribed = false;
    
    // Cached property IDs
    private int emissionColorId = -1;
    private int tintColorId = -1;
    
    // ============================================
    // LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        // Resolve time source with robust null/zombie checking
        ResolveTimeService();
        
        // Auto-find renderer
        if (targetRenderer == null && autoFindRenderer)
        {
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }
        }
        
        // Create material property block
        if (targetRenderer != null)
        {
            mpb = new MaterialPropertyBlock();
            
            // Cache property IDs
            emissionColorId = Shader.PropertyToID(emissionProperty);
            tintColorId = Shader.PropertyToID(tintProperty);
        }
        else
        {
            Debug.LogWarning($"[WindowTimeOfDayObject] No Renderer found on {gameObject.name}!");
        }
    }
    
    private void OnEnable()
    {
        // Subscribe to scene loaded events to refresh connections
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Subscribe to TimeOfDayManager availability event (for reconnection after scene loads)
        TimeOfDayManager.OnInstanceAvailable += OnTimeManagerAvailable;
        
        // ============================================
        // RETRY TIME SERVICE RESOLUTION
        // ============================================
        // If timeService is null (failed in Awake or scene reload), try again
        if (timeService == null)
        {
            ResolveTimeService();
            
            if (enableDebugLogs)
            {
                if (timeService != null)
                {
                    Debug.Log($"[WindowTimeOfDayObject] Time service resolved in OnEnable: {(timeService as MonoBehaviour)?.name}");
                }
                else
                {
                    Debug.LogWarning($"[WindowTimeOfDayObject] Time service still null in OnEnable for {gameObject.name}. Starting delayed retry...");
                }
            }
        }
        
        Subscribe();
        
        // Apply initial state
        if (timeService != null)
        {
            UpdateWindowVisuals(timeService.NormalizedTime);
        }
        else
        {
            // Start delayed retry coroutine
            if (Application.isPlaying)
            {
                StartCoroutine(DelayedTimeServiceRetry());
            }
            
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[WindowTimeOfDayObject] No timeService found on enable for {gameObject.name}! Will retry after delay.");
            }
        }
    }
    
    /// <summary>
    /// Called when TimeOfDayManager broadcasts its availability.
    /// </summary>
    private void OnTimeManagerAvailable(TimeOfDayManager manager)
    {
        if (manager == null) return;
        
        // Check if we need to reconnect
        bool needsReconnect = timeService == null || (timeService is MonoBehaviour mb && mb == null);
        
        if (needsReconnect)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[WindowTimeOfDayObject] {gameObject.name} received OnInstanceAvailable from {manager.gameObject.name}. Reconnecting...");
            }
            
            timeService = manager;
            Subscribe();
            UpdateWindowVisuals(timeService.NormalizedTime);
        }
    }
    
    /// <summary>
    /// Delayed retry for time service resolution.
    /// Waits a few frames then tries again, handling DontDestroyOnLoad timing issues.
    /// </summary>
    private System.Collections.IEnumerator DelayedTimeServiceRetry()
    {
        // Wait a few frames for DontDestroyOnLoad objects to be fully registered
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        
        if (timeService == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[WindowTimeOfDayObject] {gameObject.name} attempting delayed time service resolution...");
            }
            
            ResolveTimeService();
            
            if (timeService != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[WindowTimeOfDayObject] {gameObject.name} delayed retry SUCCEEDED: {(timeService as MonoBehaviour)?.name}");
                }
                
                Subscribe();
                UpdateWindowVisuals(timeService.NormalizedTime);
            }
            else
            {
                Debug.LogError($"[WindowTimeOfDayObject] {gameObject.name} delayed retry FAILED - TimeOfDayManager not found!");
            }
        }
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
    }
    
    /// <summary>
    /// Called when a new scene loads. Refreshes time service connection.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshTimeService();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[WindowTimeOfDayObject] Scene loaded: {scene.name}, timeService valid: {timeService != null}");
        }
    }
    
    // ============================================
    // SUBSCRIPTION
    // ============================================
    
    private void Subscribe()
    {
        if (isSubscribed || timeService == null) return;
        
        timeService.OnTimeNormalizedChanged += UpdateWindowVisuals;
        isSubscribed = true;
    }
    
    private void Unsubscribe()
    {
        if (!isSubscribed || timeService == null) return;
        
        timeService.OnTimeNormalizedChanged -= UpdateWindowVisuals;
        isSubscribed = false;
    }
    
    // ============================================
    // TIME SERVICE RESOLUTION
    // ============================================
    
    /// <summary>
    /// Resolve the time service. ALWAYS prefers singleton for cross-scene reliability.
    /// Only uses serialized override if it's valid AND in the same scene.
    /// </summary>
    private void ResolveTimeService()
    {
        // ALWAYS try singleton first - it's the most reliable for cross-scene scenarios
        timeService = TimeOfDayManager.Instance;
        
        if (timeService != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[WindowTimeOfDayObject] {gameObject.name} using TimeOfDayManager singleton: {(timeService as MonoBehaviour)?.name}");
            }
            return;
        }
        
        // Fallback: Check if serialized override is valid (same-scene reference only)
        bool hasValidOverride = timeSourceOverride != null && 
                                (!(timeSourceOverride is MonoBehaviour) || (MonoBehaviour)timeSourceOverride != null);
        
        if (hasValidOverride && timeSourceOverride is ITimeOfDayService service)
        {
            timeService = service;
            if (enableDebugLogs)
            {
                Debug.Log($"[WindowTimeOfDayObject] {gameObject.name} using serialized timeSourceOverride: {timeSourceOverride.name}");
            }
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[WindowTimeOfDayObject] {gameObject.name}: No TimeOfDayManager instance found! " +
                            "Ensure TimeOfDayManager exists in GameServicesContainer.");
        }
    }
    
    /// <summary>
    /// Refresh the time service connection. Call if time service was lost.
    /// </summary>
    public void RefreshTimeService()
    {
        // Check if current service is still valid
        bool needsRefresh = timeService == null;
        
        // Also check for Unity "zombie" objects
        if (!needsRefresh && timeService is MonoBehaviour mb)
        {
            needsRefresh = mb == null; // Unity's == will return true if destroyed
        }
        
        if (needsRefresh)
        {
            Unsubscribe();
            
            // Use the robust resolution method
            ResolveTimeService();
            
            if (timeService != null)
            {
                Subscribe();
                UpdateWindowVisuals(timeService.NormalizedTime);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[WindowTimeOfDayObject] Time service refreshed, t={timeService.NormalizedTime:F3}");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"[WindowTimeOfDayObject] Failed to refresh time service for {gameObject.name}!");
            }
        }
    }
    
    // ============================================
    // VISUAL UPDATE
    // ============================================
    
    private void UpdateWindowVisuals(float normalizedTime)
    {
        if (targetRenderer == null || mpb == null) return;
        
        // Calculate day/night blend factor
        // Use light intensity as proxy (0 = night, 1 = day)
        float dayFactor = GetDayFactor(normalizedTime);
        float nightFactor = 1f - dayFactor;
        
        // Get current property block
        targetRenderer.GetPropertyBlock(mpb);
        
        // Apply day emission
        Color dayEmission = dayEmissionColor.Evaluate(normalizedTime);
        float dayStrength = dayEmissionStrengthByTime.Evaluate(normalizedTime);
        Color finalEmission = dayEmission * dayEmissionIntensity * dayStrength * dayFactor;
        
        // Apply night tint (reduces emission and adds dark tint)
        Color nightTint = nightWindowTint.Evaluate(normalizedTime);
        float nightStrength = nightTintStrengthByTime.Evaluate(normalizedTime);
        Color finalTint = Color.Lerp(Color.white, nightTint, nightTintIntensity * nightStrength * nightFactor);
        
        // Blend emission (day emission fades at night)
        finalEmission *= dayFactor;
        
        // Apply to material property block
        mpb.SetColor(emissionColorId, finalEmission);
        mpb.SetColor(tintColorId, finalTint);
        
        // Set property block back
        targetRenderer.SetPropertyBlock(mpb);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[WindowTimeOfDayObject] Updated window visuals at t={normalizedTime:F3}, dayFactor={dayFactor:F2}");
        }
    }
    
    private float GetDayFactor(float normalizedTime)
    {
        // Use a simple curve: peaks at midday (0.5), low at night
        // This could be improved by querying TimeOfDayConfigSO if available
        float t = normalizedTime;
        
        // Map to day intensity: 0.25 (dawn) to 0.75 (dusk) = day
        if (t < 0.25f || t >= 0.75f)
        {
            // Night: low factor
            if (t >= 0.75f)
            {
                // Evening to midnight: fade out
                return Mathf.InverseLerp(1f, 0.75f, t);
            }
            else
            {
                // Midnight to dawn: fade in
                return Mathf.InverseLerp(0f, 0.25f, t);
            }
        }
        else
        {
            // Day: high factor
            return 1f;
        }
    }
    
    // ============================================
    // DEFAULT CREATION
    // ============================================
    
    private static Gradient CreateDefaultDayEmission()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f) * 0.5f, 0f),      // Night: very dim
                new GradientColorKey(new Color(1f, 0.9f, 0.7f) * 1.5f, 0.25f),     // Dawn: warm
                new GradientColorKey(new Color(1f, 0.98f, 0.95f) * 2f, 0.5f),       // Noon: bright white
                new GradientColorKey(new Color(1f, 0.7f, 0.5f) * 1.5f, 0.75f),      // Dusk: warm
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f) * 0.5f, 1f)        // Night: very dim
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultDayStrength()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.1f);    // Night: low
        curve.AddKey(0.25f, 0.5f); // Dawn: rising
        curve.AddKey(0.5f, 1f);    // Noon: peak
        curve.AddKey(0.75f, 0.5f); // Dusk: falling
        curve.AddKey(1f, 0.1f);    // Night: low
        return curve;
    }
    
    private static Gradient CreateDefaultNightTint()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.3f), 0f),      // Night: dark blue
                new GradientColorKey(new Color(0.3f, 0.3f, 0.4f), 0.25f),   // Dawn: lighter
                new GradientColorKey(new Color(0.9f, 0.9f, 0.95f), 0.5f),   // Day: almost white (no tint)
                new GradientColorKey(new Color(0.3f, 0.3f, 0.4f), 0.75f),   // Dusk: darker
                new GradientColorKey(new Color(0.2f, 0.2f, 0.3f), 1f)        // Night: dark blue
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultNightStrength()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);      // Night: full tint
        curve.AddKey(0.25f, 0.3f); // Dawn: fading
        curve.AddKey(0.5f, 0f);    // Day: no tint
        curve.AddKey(0.75f, 0.3f); // Dusk: increasing
        curve.AddKey(1f, 1f);      // Night: full tint
        return curve;
    }
    
    // ============================================
    // EDITOR SUPPORT
    // ============================================
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Update property IDs if names changed
        if (Application.isPlaying && targetRenderer != null)
        {
            emissionColorId = Shader.PropertyToID(emissionProperty);
            tintColorId = Shader.PropertyToID(tintProperty);
            
            if (timeService != null)
            {
                UpdateWindowVisuals(timeService.NormalizedTime);
            }
        }
    }
#endif
}
