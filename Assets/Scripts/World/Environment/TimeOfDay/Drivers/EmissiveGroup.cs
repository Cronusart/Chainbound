using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers a group of renderers that should glow at night (windows, lamps, etc.).
/// Uses MaterialPropertyBlock for efficient per-instance emission without material instancing.
/// 
/// Setup:
/// 1. Attach to a parent object containing emissive renderers (windows, lamps)
/// 2. Assign renderers manually or use auto-find options
/// 3. Select the accent group type (WindowAmbient, NeonSign, LampPost, etc.)
/// 4. Specify the emission property name used by your shader
/// 
/// Note: Renderers must use materials with an emission property (_EmissionColor or custom).
/// </summary>
public class EmissiveGroup : MonoBehaviour
{
    [Header("Accent Group Assignment")]
    [Tooltip("Select accent group by preset type (uses config's group settings)")]
    [SerializeField] private NightAccentType accentGroupType = NightAccentType.WindowAmbient;
    
    [Tooltip("Or specify group by name (overrides type if not empty)")]
    [SerializeField] private string accentGroupName = "";
    
    [Tooltip("Use custom settings below instead of config group settings")]
    [SerializeField] private bool useCustomSettings = false;
    
    [Header("Renderers")]
    [Tooltip("Renderers to control. Leave empty to auto-find children.")]
    [SerializeField] private List<Renderer> renderers = new List<Renderer>();
    
    [Tooltip("Auto-find renderers in children on Awake")]
    [SerializeField] private bool autoFindChildren = true;
    
    [Tooltip("Include inactive child renderers")]
    [SerializeField] private bool includeInactive = false;
    
    [Header("Material Settings")]
    [Tooltip("Emission color property name in shader")]
    [SerializeField] private string emissionPropertyName = "_EmissionColor";
    
    [Tooltip("Additional property names to try (fallbacks)")]
    [SerializeField] private string[] fallbackPropertyNames = new string[] { "_EmissiveColor", "_Emission" };
    
    [Header("Custom Settings (when useCustomSettings = true)")]
    [Tooltip("Custom emission color (only used if useCustomSettings is true)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color customEmissionColor = new Color(1f, 0.9f, 0.7f) * 2f;
    
    [Tooltip("Custom emission intensity (only used if useCustomSettings is true)")]
    [Range(0f, 10f)]
    [SerializeField] private float customEmissionIntensity = 2f;
    
    [Header("Modifiers")]
    [Tooltip("Additional intensity multiplier for this specific group")]
    [Range(0f, 5f)]
    [SerializeField] private float intensityMultiplier = 1f;
    
    [Tooltip("Random intensity variation per renderer (0 = all same)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float intensityVariation = 0.1f;
    
    [Header("Optional Point Light")]
    [Tooltip("Attach a point light that turns on with emission")]
    [SerializeField] private Light attachedPointLight;
    
    [Tooltip("Auto-create point light based on accent group settings")]
    [SerializeField] private bool autoCreatePointLight = false;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached data
    private MaterialPropertyBlock propertyBlock;
    private int emissionPropertyId;
    private bool isInitialized = false;
    private float[] intensityOffsets;
    private float flickerSeed;
    
    // State
    private float currentIntensity = 0f;
    private Color currentColor = Color.black;
    
    // Cached group reference
    private NightAccentGroup cachedAccentGroup;
    
    /// <summary>List of controlled renderers.</summary>
    public IReadOnlyList<Renderer> Renderers => renderers;
    
    /// <summary>Current emission intensity (0-1+).</summary>
    public float CurrentIntensity => currentIntensity;
    
    /// <summary>World position (for distance culling).</summary>
    public Vector3 Position => transform.position;
    
    /// <summary>Attached point light (if any). Returns null if component is disabled to prevent conflicts.</summary>
    public Light AttachedLight => enabled ? attachedPointLight : null;
    
    /// <summary>Assigned accent group type.</summary>
    public NightAccentType AccentGroupType => accentGroupType;
    
    /// <summary>Assigned accent group name (if specified).</summary>
    public string AccentGroupName => accentGroupName;
    
    /// <summary>Whether this group uses custom settings instead of config.</summary>
    public bool UsesCustomSettings => useCustomSettings;
    
    /// <summary>Flicker seed for per-instance variation.</summary>
    public float FlickerSeed => flickerSeed;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        propertyBlock = new MaterialPropertyBlock();
        
        // Find emission property ID
        emissionPropertyId = Shader.PropertyToID(emissionPropertyName);
        
        // Generate unique flicker seed based on position
        flickerSeed = transform.position.x * 100f + transform.position.z * 10f + transform.position.y;
        
        // Auto-find renderers if needed
        if (autoFindChildren && (renderers == null || renderers.Count == 0))
        {
            FindChildRenderers();
        }
        
        // Remove null entries
        renderers.RemoveAll(r => r == null);
        
        // Initialize intensity variation offsets
        intensityOffsets = new float[renderers.Count];
        for (int i = 0; i < renderers.Count; i++)
        {
            intensityOffsets[i] = 1f + Random.Range(-intensityVariation, intensityVariation);
        }
        
        // Turn off point light initially
        if (attachedPointLight != null)
        {
            attachedPointLight.enabled = false;
        }
        
        isInitialized = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[EmissiveGroup] Initialized with {renderers.Count} renderers on {gameObject.name}, type={accentGroupType}");
        }
    }
    
    private void FindChildRenderers()
    {
        renderers.Clear();
        
        Renderer[] found = includeInactive 
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>();
        
        foreach (var r in found)
        {
            // Check if renderer's material has the emission property
            if (r.sharedMaterial != null && HasEmissionProperty(r.sharedMaterial))
            {
                renderers.Add(r);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[EmissiveGroup] Auto-found {renderers.Count} emissive renderers in children");
        }
    }
    
    private bool HasEmissionProperty(Material mat)
    {
        if (mat.HasProperty(emissionPropertyName))
            return true;
        
        foreach (var fallback in fallbackPropertyNames)
        {
            if (mat.HasProperty(fallback))
            {
                emissionPropertyName = fallback;
                emissionPropertyId = Shader.PropertyToID(fallback);
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Get the accent group from a config, caching the result.
    /// </summary>
    public NightAccentGroup GetAccentGroup(NightAccentConfigSO config)
    {
        if (config == null) return null;
        
        // Return cached if available
        if (cachedAccentGroup != null) return cachedAccentGroup;
        
        // Try by name first
        if (!string.IsNullOrEmpty(accentGroupName))
        {
            cachedAccentGroup = config.GetGroup(accentGroupName);
        }
        
        // Fall back to type
        if (cachedAccentGroup == null)
        {
            cachedAccentGroup = config.GetGroup(accentGroupType);
        }
        
        // Fall back to default
        if (cachedAccentGroup == null)
        {
            cachedAccentGroup = config.GetDefaultGroup();
        }
        
        return cachedAccentGroup;
    }
    
    /// <summary>
    /// Clear cached accent group (call when config changes).
    /// </summary>
    public void ClearCachedGroup()
    {
        cachedAccentGroup = null;
    }
    
    /// <summary>
    /// Set emission using accent group settings from config.
    /// </summary>
    /// <param name="fadeProgress">0-1 fade progress (from time of day)</param>
    /// <param name="config">Night accent config to get group settings from</param>
    public void SetEmissionFromGroup(float fadeProgress, NightAccentConfigSO config)
    {
        if (!isInitialized) Initialize();
        
        // If using custom settings, use those instead
        if (useCustomSettings)
        {
            Color customColor = customEmissionColor * customEmissionIntensity;
            SetEmissionDirect(fadeProgress, customColor, customEmissionIntensity);
            return;
        }
        
        // Get the accent group
        NightAccentGroup group = GetAccentGroup(config);
        if (group == null || !group.Enabled)
        {
            TurnOff();
            return;
        }
        
        // Apply flicker if enabled
        float flicker = group.EnableFlicker ? group.GetFlickerMultiplier(flickerSeed) : 1f;
        float intensity = fadeProgress * flicker;
        
        // Get colors from group
        Color emissionColor = group.FinalEmissionColor;
        
        SetEmissionDirect(intensity, emissionColor, group.EmissionIntensity);
        
        // Update point light with group settings
        if (attachedPointLight != null && group.EnablePointLights)
        {
            attachedPointLight.enabled = intensity > 0.01f;
            if (attachedPointLight.enabled)
            {
                attachedPointLight.intensity = group.PointLightIntensity * intensity * intensityMultiplier;
                attachedPointLight.color = group.PointLightColor;
                attachedPointLight.range = group.PointLightRange;
            }
        }
    }
    
    /// <summary>
    /// Set emission intensity and color directly.
    /// </summary>
    private void SetEmissionDirect(float intensity, Color emissionColor, float baseIntensity)
    {
        currentIntensity = intensity;
        currentColor = emissionColor * intensity * intensityMultiplier;
        
        // Apply to all renderers
        for (int i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            
            // Get existing property block
            renderer.GetPropertyBlock(propertyBlock);
            
            // Apply emission with per-renderer variation
            Color variedColor = currentColor * intensityOffsets[i];
            propertyBlock.SetColor(emissionPropertyId, variedColor);
            
            // Apply property block
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    /// <summary>
    /// Set emission intensity and color. Legacy method for backward compatibility.
    /// </summary>
    /// <param name="intensity">0-1+ intensity factor</param>
    /// <param name="baseColor">Base emission color</param>
    public void SetEmission(float intensity, Color baseColor)
    {
        if (!isInitialized) Initialize();
        
        // If using custom settings, blend with base color
        Color finalColor = useCustomSettings 
            ? customEmissionColor * customEmissionIntensity 
            : baseColor;
        
        SetEmissionDirect(intensity, finalColor, 1f);
        
        // Update point light (legacy behavior)
        if (attachedPointLight != null)
        {
            attachedPointLight.enabled = intensity > 0.01f;
            if (attachedPointLight.enabled)
            {
                attachedPointLight.intensity = intensity * intensityMultiplier;
                attachedPointLight.color = finalColor;
            }
        }
    }
    
    /// <summary>
    /// Turn off all emission.
    /// </summary>
    public void TurnOff()
    {
        SetEmissionDirect(0f, Color.black, 0f);
        
        if (attachedPointLight != null)
        {
            attachedPointLight.enabled = false;
        }
    }
    
    /// <summary>
    /// Add a renderer to this group at runtime.
    /// </summary>
    public void AddRenderer(Renderer renderer)
    {
        if (renderer == null || renderers.Contains(renderer)) return;
        
        renderers.Add(renderer);
        
        // Extend intensity offsets array
        System.Array.Resize(ref intensityOffsets, renderers.Count);
        intensityOffsets[renderers.Count - 1] = 1f + Random.Range(-intensityVariation, intensityVariation);
        
        // Apply current state to new renderer
        if (currentIntensity > 0)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(emissionPropertyId, currentColor * intensityOffsets[renderers.Count - 1]);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    /// <summary>
    /// Remove a renderer from this group.
    /// </summary>
    public void RemoveRenderer(Renderer renderer)
    {
        int index = renderers.IndexOf(renderer);
        if (index < 0) return;
        
        // Clear emission before removing
        if (renderer != null)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(emissionPropertyId, Color.black);
            renderer.SetPropertyBlock(propertyBlock);
        }
        
        renderers.RemoveAt(index);
    }
    
    private void OnDisable()
    {
        // Reset emission when disabled
        if (isInitialized)
        {
            TurnOff();
        }
    }
    
    private void OnValidate()
    {
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        intensityVariation = Mathf.Clamp(intensityVariation, 0f, 0.5f);
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw connections to renderers
        Gizmos.color = Color.yellow;
        foreach (var r in renderers)
        {
            if (r != null)
            {
                Gizmos.DrawLine(transform.position, r.bounds.center);
                Gizmos.DrawWireSphere(r.bounds.center, 0.1f);
            }
        }
    }
#endif
}

