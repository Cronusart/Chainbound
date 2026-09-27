using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives directional light properties based on time-of-day.
/// Controls intensity, color, shadow strength, and sun angle rotation.
/// 
/// IMPORTANT: This driver is for OUTDOOR scenes only!
/// For indoor scenes, use IndoorDirectionalLight component instead.
/// 
/// Setup:
/// 1. Attach to your scene's directional light (or a manager object)
/// 2. Assign the directional light reference
/// 3. Assign a TimeOfDayManager reference (or uses singleton)
/// 4. Assign a TimeOfDayConfigSO
/// </summary>
[ExecuteAlways]
public class DirectionalLightTimeDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The directional light to control. Auto-finds OUTDOOR light only if null.")]
    [SerializeField] private Light directionalLight;
    
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Tooltip("Configuration. Uses manager's config if null.")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Header("Features")]
    [Tooltip("Control light intensity")]
    [SerializeField] private bool controlIntensity = true;
    
    [Tooltip("Control light color")]
    [SerializeField] private bool controlColor = true;
    
    [Tooltip("Control shadow strength")]
    [SerializeField] private bool controlShadowStrength = true;
    
    [Tooltip("Rotate light based on sun angle (X rotation)")]
    [SerializeField] private bool controlSunAngle = true;
    
    [Header("Intensity Settings")]
    [Tooltip("Base intensity multiplier applied to curve")]
    [SerializeField] private float intensityMultiplier = 1f;
    
    [Tooltip("Minimum intensity (prevents complete darkness)")]
    [SerializeField] private float minIntensity = 0.05f;
    
    [Header("Sun Rotation")]
    [Tooltip("Base Y rotation (azimuth) of the sun")]
    [SerializeField] private float baseYawAngle = -30f;
    
    [Tooltip("Base Z rotation (roll) of the sun")]
    [SerializeField] private float baseRollAngle = 0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached state
    private float lastAppliedTime = -1f;
    private bool isSubscribed = false;
    private bool initialApplyPending = false;
    
    private void OnEnable()
    {
        // Subscribe to scene loaded events to re-find directional light after transitions
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Auto-find OUTDOOR directional light only (exclude indoor lights)
        if (directionalLight == null)
        {
            TryFindOutdoorDirectionalLight();
        }
        
        // Auto-find time manager
        if (timeManager == null)
        {
            timeManager = TimeOfDayManager.Instance;
        }
        
        // Auto-get config
        if (config == null && timeManager != null)
        {
            config = timeManager.Config;
        }
        
        Subscribe();
        
        // Delay initial apply to let indoor systems set up first
        // This prevents overwriting indoor light rotation on scene load
        initialApplyPending = true;
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }
    
    private void LateUpdate()
    {
        // Apply initial state in LateUpdate to give indoor systems time to set up
        if (initialApplyPending && Application.isPlaying)
        {
            initialApplyPending = false;
            
            // Only apply if we're not indoors
            if (!TimeOfDayVisualApplier.IsAnyIndoors && timeManager != null)
            {
                ApplyTimeOfDay(timeManager.NormalizedTime);
            }
        }
    }
    
    /// <summary>
    /// Called when a scene loads. Re-finds the directional light.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find outdoor directional light in new scene
        TryFindOutdoorDirectionalLight();
        
        // Reset last applied time to force update
        lastAppliedTime = -1f;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DirectionalLightTimeDriver] Scene loaded: {scene.name}, directionalLight: {(directionalLight != null ? directionalLight.name : "null")}");
        }
    }
    
    /// <summary>
    /// Find an outdoor directional light (one without IndoorDirectionalLight component).
    /// </summary>
    private void TryFindOutdoorDirectionalLight()
    {
        // First check if we have one on this GameObject
        Light selfLight = GetComponent<Light>();
        if (selfLight != null && selfLight.type == LightType.Directional)
        {
            // Make sure it's not an indoor light
            if (selfLight.GetComponent<IndoorDirectionalLight>() == null)
            {
                directionalLight = selfLight;
                return;
            }
        }
        
        // Search for outdoor directional lights in scene (exclude indoor lights)
        Light[] lights = FindObjectsOfType<Light>();
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                // Skip lights marked as indoor
                if (light.GetComponent<IndoorDirectionalLight>() != null)
                {
                    continue;
                }
                
                directionalLight = light;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[DirectionalLightTimeDriver] Found outdoor directional light: {light.name}");
                }
                return;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("[DirectionalLightTimeDriver] No outdoor directional light found in scene.");
        }
    }
    
    private void Subscribe()
    {
        if (isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged += ApplyTimeOfDay;
        isSubscribed = true;
    }
    
    private void Unsubscribe()
    {
        if (!isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged -= ApplyTimeOfDay;
        isSubscribed = false;
    }
    
    private void ApplyTimeOfDay(float normalizedTime)
    {
        if (directionalLight == null || config == null) return;
        
        // Skip if indoors (TimeOfDayVisualApplier is handling visuals)
        if (TimeOfDayVisualApplier.IsAnyIndoors) return;
        
        // Safety check: Don't control lights that have IndoorDirectionalLight component
        if (directionalLight.GetComponent<IndoorDirectionalLight>() != null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[DirectionalLightTimeDriver] Skipping {directionalLight.name} - it's an indoor light!");
            }
            directionalLight = null; // Clear reference so we don't keep checking
            return;
        }
        
        // Skip if time hasn't changed significantly
        if (Mathf.Approximately(normalizedTime, lastAppliedTime)) return;
        lastAppliedTime = normalizedTime;
        
        // Apply intensity
        if (controlIntensity)
        {
            float intensity = config.EvaluateLightIntensity(normalizedTime) * intensityMultiplier;
            directionalLight.intensity = Mathf.Max(intensity, minIntensity);
        }
        
        // Apply color
        if (controlColor)
        {
            directionalLight.color = config.EvaluateLightColor(normalizedTime);
        }
        
        // Apply shadow strength
        if (controlShadowStrength)
        {
            directionalLight.shadowStrength = config.EvaluateShadowStrength(normalizedTime);
        }
        
        // Apply sun angle rotation
        if (controlSunAngle)
        {
            float sunAngle = config.EvaluateSunAngle(normalizedTime);
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, baseYawAngle, baseRollAngle);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[DirectionalLightTimeDriver] Applied t={normalizedTime:F3}: " +
                     $"intensity={directionalLight.intensity:F2}, " +
                     $"color={directionalLight.color}, " +
                     $"shadow={directionalLight.shadowStrength:F2}");
        }
    }
    
    // ============================================
    // EDITOR SUPPORT
    // ============================================
    
#if UNITY_EDITOR
    private void Update()
    {
        // In edit mode, continuously update based on manager's preview time
        if (!Application.isPlaying && timeManager != null)
        {
            ApplyTimeOfDay(timeManager.NormalizedTime);
        }
    }
    
    private void OnValidate()
    {
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        minIntensity = Mathf.Max(0f, minIntensity);
        
        // Apply immediately in editor
        if (timeManager != null)
        {
            ApplyTimeOfDay(timeManager.NormalizedTime);
        }
    }
#endif
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Manually apply a specific time (for preview or override).
    /// </summary>
    public void PreviewTime(float normalizedTime)
    {
        ApplyTimeOfDay(normalizedTime);
    }
    
    /// <summary>
    /// Set the directional light reference at runtime.
    /// </summary>
    public void SetLight(Light light)
    {
        directionalLight = light;
        if (timeManager != null)
        {
            ApplyTimeOfDay(timeManager.NormalizedTime);
        }
    }
}

