using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives ambient light and fog settings based on time-of-day.
/// Works with Unity's built-in RenderSettings and URP if available.
/// 
/// Setup:
/// 1. Attach to a manager object in your scene
/// 2. Assign a TimeOfDayManager reference (or uses singleton)
/// 3. Assign a TimeOfDayConfigSO
/// </summary>
[ExecuteAlways]
public class FogAmbientTimeDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Tooltip("Configuration. Uses manager's config if null.")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Header("Ambient Light")]
    [Tooltip("Control ambient light intensity")]
    [SerializeField] private bool controlAmbientIntensity = true;
    
    [Tooltip("Control ambient light color")]
    [SerializeField] private bool controlAmbientColor = true;
    
    [Tooltip("Ambient intensity multiplier")]
    [SerializeField] private float ambientIntensityMultiplier = 1f;
    
    [Header("Fog")]
    [Tooltip("Control fog density")]
    [SerializeField] private bool controlFogDensity = true;
    
    [Tooltip("Control fog color")]
    [SerializeField] private bool controlFogColor = true;
    
    [Tooltip("Fog density multiplier")]
    [SerializeField] private float fogDensityMultiplier = 1f;
    
    [Tooltip("Enable fog in render settings")]
    [SerializeField] private bool enableFog = true;
    
    [Header("Fog Mode Settings")]
    [Tooltip("Fog mode to use")]
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    
    [Tooltip("Linear fog start distance (if using Linear mode)")]
    [SerializeField] private float linearFogStart = 10f;
    
    [Tooltip("Linear fog end distance (if using Linear mode)")]
    [SerializeField] private float linearFogEnd = 100f;
    
    [Header("Reflection Probe (Optional)")]
    [Tooltip("Optional: Reflection probe to update intensity")]
    [SerializeField] private ReflectionProbe reflectionProbe;
    
    [Tooltip("Control reflection probe intensity")]
    [SerializeField] private bool controlReflectionIntensity = false;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached state
    private float lastAppliedTime = -1f;
    private bool isSubscribed = false;
    
    private void OnEnable()
    {
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
        
        // Configure fog settings
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = fogMode;
            if (fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = linearFogStart;
                RenderSettings.fogEndDistance = linearFogEnd;
            }
        }
        
        Subscribe();
        
        // Apply initial state
        if (timeManager != null)
        {
            ApplyTimeOfDay(timeManager.NormalizedTime);
        }
    }
    
    private void OnDisable()
    {
        Unsubscribe();
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
        if (config == null) return;
        
        // Skip if indoors (TimeOfDayVisualApplier is handling visuals)
        if (TimeOfDayVisualApplier.IsAnyIndoors) return;
        
        // Skip if time hasn't changed significantly
        if (Mathf.Approximately(normalizedTime, lastAppliedTime)) return;
        lastAppliedTime = normalizedTime;
        
        // Apply ambient intensity
        if (controlAmbientIntensity)
        {
            float intensity = config.EvaluateAmbientIntensity(normalizedTime) * ambientIntensityMultiplier;
            RenderSettings.ambientIntensity = intensity;
            
            // Also adjust reflection intensity if available
            if (controlReflectionIntensity && reflectionProbe != null)
            {
                reflectionProbe.intensity = intensity;
            }
        }
        
        // Apply ambient color
        if (controlAmbientColor)
        {
            Color ambientColor = config.EvaluateAmbientColor(normalizedTime);
            
            // Apply based on ambient mode
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    RenderSettings.ambientLight = ambientColor;
                    break;
                    
                case AmbientMode.Trilight:
                    // Adjust trilight colors proportionally
                    RenderSettings.ambientSkyColor = ambientColor;
                    RenderSettings.ambientEquatorColor = ambientColor * 0.8f;
                    RenderSettings.ambientGroundColor = ambientColor * 0.5f;
                    break;
                    
                case AmbientMode.Skybox:
                    // Skybox mode uses skybox material tint instead
                    // We'll still set ambient light for fallback
                    RenderSettings.ambientLight = ambientColor;
                    break;
            }
        }
        
        // Apply fog settings
        if (enableFog)
        {
            RenderSettings.fog = true;
            
            if (controlFogDensity)
            {
                float density = config.EvaluateFogDensity(normalizedTime) * fogDensityMultiplier;
                RenderSettings.fogDensity = density;
            }
            
            if (controlFogColor)
            {
                RenderSettings.fogColor = config.EvaluateFogColor(normalizedTime);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[FogAmbientTimeDriver] Applied t={normalizedTime:F3}: " +
                     $"ambient={RenderSettings.ambientIntensity:F2}, " +
                     $"fog density={RenderSettings.fogDensity:F4}");
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
        ambientIntensityMultiplier = Mathf.Max(0f, ambientIntensityMultiplier);
        fogDensityMultiplier = Mathf.Max(0f, fogDensityMultiplier);
        linearFogStart = Mathf.Max(0f, linearFogStart);
        linearFogEnd = Mathf.Max(linearFogStart + 1f, linearFogEnd);
        
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
    /// Toggle fog on/off at runtime.
    /// </summary>
    public void SetFogEnabled(bool enabled)
    {
        enableFog = enabled;
        RenderSettings.fog = enabled;
    }
}

