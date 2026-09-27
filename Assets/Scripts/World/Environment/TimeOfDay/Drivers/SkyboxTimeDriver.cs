using UnityEngine;

/// <summary>
/// Drives skybox material properties based on time-of-day.
/// Works with procedural skybox or custom skybox shaders.
/// 
/// Setup:
/// 1. Attach to a manager object
/// 2. Assign a TimeOfDayManager reference (or uses singleton)
/// 3. Assign a TimeOfDayConfigSO
/// </summary>
[ExecuteAlways]
public class SkyboxTimeDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Tooltip("Configuration. Uses manager's config if null.")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Tooltip("Skybox material to modify. Uses RenderSettings.skybox if null.")]
    [SerializeField] private Material skyboxMaterial;
    
    [Header("Features")]
    [Tooltip("Control skybox tint color")]
    [SerializeField] private bool controlTint = true;
    
    [Tooltip("Control skybox exposure/intensity")]
    [SerializeField] private bool controlExposure = true;
    
    [Header("Property Names")]
    [Tooltip("Tint color property name")]
    [SerializeField] private string tintPropertyName = "_Tint";
    
    [Tooltip("Exposure property name")]
    [SerializeField] private string exposurePropertyName = "_Exposure";
    
    [Tooltip("Fallback property names for different skybox shaders")]
    [SerializeField] private string[] tintFallbacks = new string[] { "_SkyTint", "_Color" };
    
    [SerializeField] private string[] exposureFallbacks = new string[] { "_SkyExposure", "_Intensity" };
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached
    private int tintPropertyId;
    private int exposurePropertyId;
    private float lastAppliedTime = -1f;
    private bool isSubscribed = false;
    private bool propertyIdsResolved = false;
    
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
        
        // Get skybox material
        if (skyboxMaterial == null)
        {
            skyboxMaterial = RenderSettings.skybox;
        }
        
        // Resolve property IDs
        ResolvePropertyIds();
        
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
    
    private void ResolvePropertyIds()
    {
        if (propertyIdsResolved || skyboxMaterial == null) return;
        
        // Resolve tint property
        if (skyboxMaterial.HasProperty(tintPropertyName))
        {
            tintPropertyId = Shader.PropertyToID(tintPropertyName);
        }
        else
        {
            foreach (var fallback in tintFallbacks)
            {
                if (skyboxMaterial.HasProperty(fallback))
                {
                    tintPropertyId = Shader.PropertyToID(fallback);
                    tintPropertyName = fallback;
                    break;
                }
            }
        }
        
        // Resolve exposure property
        if (skyboxMaterial.HasProperty(exposurePropertyName))
        {
            exposurePropertyId = Shader.PropertyToID(exposurePropertyName);
        }
        else
        {
            foreach (var fallback in exposureFallbacks)
            {
                if (skyboxMaterial.HasProperty(fallback))
                {
                    exposurePropertyId = Shader.PropertyToID(fallback);
                    exposurePropertyName = fallback;
                    break;
                }
            }
        }
        
        propertyIdsResolved = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SkyboxTimeDriver] Resolved properties: tint={tintPropertyName}, exposure={exposurePropertyName}");
        }
    }
    
    private void ApplyTimeOfDay(float normalizedTime)
    {
        if (skyboxMaterial == null || config == null) return;
        
        // Skip if indoors (TimeOfDayVisualApplier is handling visuals)
        if (TimeOfDayVisualApplier.IsAnyIndoors) return;
        
        // Skip if time hasn't changed significantly
        if (Mathf.Approximately(normalizedTime, lastAppliedTime)) return;
        lastAppliedTime = normalizedTime;
        
        // Apply tint
        if (controlTint && skyboxMaterial.HasProperty(tintPropertyId))
        {
            Color tint = config.EvaluateSkyboxTint(normalizedTime);
            skyboxMaterial.SetColor(tintPropertyId, tint);
        }
        
        // Apply exposure
        if (controlExposure && skyboxMaterial.HasProperty(exposurePropertyId))
        {
            float exposure = config.EvaluateSkyboxExposure(normalizedTime);
            skyboxMaterial.SetFloat(exposurePropertyId, exposure);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SkyboxTimeDriver] Applied t={normalizedTime:F3}");
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
        propertyIdsResolved = false; // Re-resolve on change
        
        // Apply immediately in editor
        if (timeManager != null)
        {
            ResolvePropertyIds();
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
    /// Set the skybox material at runtime.
    /// </summary>
    public void SetSkyboxMaterial(Material material)
    {
        skyboxMaterial = material;
        propertyIdsResolved = false;
        ResolvePropertyIds();
        
        if (timeManager != null)
        {
            ApplyTimeOfDay(timeManager.NormalizedTime);
        }
    }
}

