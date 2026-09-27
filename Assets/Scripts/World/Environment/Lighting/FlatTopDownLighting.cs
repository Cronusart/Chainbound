using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Automatically configures lighting for classic top-down Game Boy aesthetic.
/// Ensures flat, unlit appearance without darkened surfaces.
/// </summary>
public class FlatTopDownLighting : MonoBehaviour
{
    [Header("Lighting Configuration")]
    [Tooltip("Disable all shadow casting")]
    [SerializeField] private bool disableShadows = true;
    
    [Tooltip("Set ambient lighting to flat mode")]
    [SerializeField] private bool useFlatAmbient = true;
    
    [Tooltip("Ambient intensity (1.0 = full brightness)")]
    [SerializeField] private float ambientIntensity = 1.0f;
    
    [Tooltip("Ambient color (white = no tint)")]
    [SerializeField] private Color ambientColor = Color.white;
    
    [Header("Directional Light")]
    [Tooltip("Find and configure main directional light")]
    [SerializeField] private bool configureDirectionalLight = true;
    
    [Tooltip("Directional light intensity")]
    [SerializeField] private float directionalIntensity = 1.0f;
    
    [Tooltip("Directional light color")]
    [SerializeField] private Color directionalColor = Color.white;
    
    [Tooltip("Disable shadows on directional light")]
    [SerializeField] private bool disableDirectionalShadows = true;
    
    [Header("Scene Objects")]
    [Tooltip("Automatically disable shadows on all renderers in scene")]
    [SerializeField] private bool disableObjectShadows = false;
    
    [Tooltip("Layer mask for objects to disable shadows on (empty = all)")]
    [SerializeField] private LayerMask shadowDisableLayers = -1;
    
    [Header("Runtime Toggle")]
    [Tooltip("Enable/disable flat lighting at runtime")]
    [SerializeField] private bool enableFlatLighting = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Light[] sceneLights;
    private Renderer[] sceneRenderers;
    private bool isConfigured = false;

    private void Start()
    {
        if (enableFlatLighting)
        {
            ConfigureFlatLighting();
        }
    }

    /// <summary>
    /// Configure lighting for flat top-down aesthetic.
    /// </summary>
    [ContextMenu("Configure Flat Lighting")]
    public void ConfigureFlatLighting()
    {
        if (enableDebugLogs)
            Debug.Log("[FlatTopDownLighting] Configuring flat top-down lighting...");

        // Configure ambient lighting
        if (useFlatAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor * ambientIntensity;
            RenderSettings.ambientIntensity = ambientIntensity;

            if (enableDebugLogs)
                Debug.Log($"[FlatTopDownLighting] Set ambient to Flat mode, Intensity: {ambientIntensity}, Color: {ambientColor}");
        }

        // Configure directional lights
        if (configureDirectionalLight)
        {
            sceneLights = FindObjectsOfType<Light>();
            foreach (Light light in sceneLights)
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = directionalIntensity;
                    light.color = directionalColor;
                    
                    if (disableDirectionalShadows)
                    {
                        light.shadows = LightShadows.None;
                    }

                    if (enableDebugLogs)
                        Debug.Log($"[FlatTopDownLighting] Configured directional light: {light.name}, Shadows: {light.shadows}");
                }
            }
        }

        // Disable shadows on scene objects
        if (disableObjectShadows)
        {
            sceneRenderers = FindObjectsOfType<Renderer>();
            int disabledCount = 0;
            
            foreach (Renderer renderer in sceneRenderers)
            {
                // Check layer mask if specified
                if (shadowDisableLayers != -1 && (shadowDisableLayers & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                disabledCount++;
            }

            if (enableDebugLogs)
                Debug.Log($"[FlatTopDownLighting] Disabled shadows on {disabledCount} renderers");
        }

        isConfigured = true;

        if (enableDebugLogs)
            Debug.Log("[FlatTopDownLighting] Flat lighting configuration complete!");
    }

    /// <summary>
    /// Restore default lighting settings.
    /// </summary>
    [ContextMenu("Restore Default Lighting")]
    public void RestoreDefaultLighting()
    {
        if (enableDebugLogs)
            Debug.Log("[FlatTopDownLighting] Restoring default lighting...");

        // Restore ambient
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;

        // Restore lights
        if (sceneLights != null)
        {
            foreach (Light light in sceneLights)
            {
                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.Soft;
                }
            }
        }

        // Restore renderers
        if (sceneRenderers != null)
        {
            foreach (Renderer renderer in sceneRenderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        isConfigured = false;

        if (enableDebugLogs)
            Debug.Log("[FlatTopDownLighting] Default lighting restored.");
    }

    /// <summary>
    /// Toggle flat lighting on/off.
    /// </summary>
    public void ToggleFlatLighting()
    {
        enableFlatLighting = !enableFlatLighting;
        
        if (enableFlatLighting)
        {
            ConfigureFlatLighting();
        }
        else
        {
            RestoreDefaultLighting();
        }
    }

    private void OnValidate()
    {
        // Auto-apply changes in editor when values change
        if (isConfigured && Application.isPlaying)
        {
            ConfigureFlatLighting();
        }
    }
}

