using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preset types for common accent lighting categories.
/// </summary>
public enum NightAccentType
{
    Custom,         // User-defined
    WindowAmbient,  // Warm, soft glow from windows
    LampPost,       // Street lamps, warm white
    NeonSign,       // Bright, colorful neon
    Firelight,      // Flickering warm fire/candle
    Industrial,     // Cool industrial lighting
    Magical         // Fantasy/mystical glow
}

/// <summary>
/// Configuration for a single group of night accent lights.
/// Each group can have different colors, intensities, and behaviors.
/// </summary>
[Serializable]
public class NightAccentGroup
{
    [Header("Group Identity")]
    [Tooltip("Unique name for this accent group (used for lookup)")]
    [SerializeField] private string groupName = "Default";
    
    [Tooltip("Preset type (affects default values when created)")]
    [SerializeField] private NightAccentType presetType = NightAccentType.Custom;
    
    [Tooltip("Enable this accent group")]
    [SerializeField] private bool enabled = true;
    
    [Header("Emission Settings")]
    [Tooltip("Base emission color for this accent group")]
    [ColorUsage(true, true)]
    [SerializeField] private Color emissionColor = new Color(1f, 0.9f, 0.7f) * 2f;
    
    [Tooltip("Emission intensity multiplier")]
    [Range(0f, 10f)]
    [SerializeField] private float emissionIntensity = 2f;
    
    [Tooltip("Fade duration when toggling accents on/off (seconds)")]
    [SerializeField] private float fadeDuration = 1f;
    
    [Header("Point Light Settings")]
    [Tooltip("Enable point lights for this group")]
    [SerializeField] private bool enablePointLights = false;
    
    [Tooltip("Point light range")]
    [SerializeField] private float pointLightRange = 5f;
    
    [Tooltip("Point light intensity")]
    [SerializeField] private float pointLightIntensity = 1f;
    
    [Tooltip("Point light color (if different from emission)")]
    [SerializeField] private Color pointLightColor = new Color(1f, 0.9f, 0.7f);
    
    [Tooltip("Use emission color for point light instead of separate color")]
    [SerializeField] private bool useEmissionColorForLight = true;
    
    [Header("Flicker Effect")]
    [Tooltip("Enable subtle flicker effect for atmosphere")]
    [SerializeField] private bool enableFlicker = false;
    
    [Tooltip("Flicker frequency range (Hz)")]
    [SerializeField] private Vector2 flickerFrequencyRange = new Vector2(0.5f, 2f);
    
    [Tooltip("Flicker intensity range (multiplier)")]
    [SerializeField] private Vector2 flickerIntensityRange = new Vector2(0.9f, 1.1f);
    
    [Header("Time Overrides (Optional)")]
    [Tooltip("Use custom turn on/off times for this group (otherwise uses global)")]
    [SerializeField] private bool useCustomTiming = false;
    
    [Tooltip("Normalized time to start turning on (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float customTurnOnTime = 0.75f;
    
    [Tooltip("Normalized time to start turning off (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float customTurnOffTime = 0.25f;
    
    // Flicker state (per-instance for variation)
    private float flickerSeed;
    
    // ============================================
    // PUBLIC ACCESSORS
    // ============================================
    
    public string GroupName => groupName;
    public NightAccentType PresetType => presetType;
    public bool Enabled => enabled;
    
    public Color EmissionColor => emissionColor;
    public float EmissionIntensity => emissionIntensity;
    public Color FinalEmissionColor => emissionColor * emissionIntensity;
    public float FadeDuration => fadeDuration;
    
    public bool EnablePointLights => enablePointLights;
    public float PointLightRange => pointLightRange;
    public float PointLightIntensity => pointLightIntensity;
    public Color PointLightColor => useEmissionColorForLight ? emissionColor : pointLightColor;
    
    public bool EnableFlicker => enableFlicker;
    public Vector2 FlickerFrequencyRange => flickerFrequencyRange;
    public Vector2 FlickerIntensityRange => flickerIntensityRange;
    
    public bool UseCustomTiming => useCustomTiming;
    public float CustomTurnOnTime => customTurnOnTime;
    public float CustomTurnOffTime => customTurnOffTime;
    
    // ============================================
    // METHODS
    // ============================================
    
    /// <summary>
    /// Initialize flicker seed for variation between instances.
    /// </summary>
    public void InitializeFlicker()
    {
        flickerSeed = UnityEngine.Random.Range(0f, 1000f);
    }
    
    /// <summary>
    /// Get current flicker multiplier using Perlin noise for smooth variation.
    /// </summary>
    public float GetFlickerMultiplier()
    {
        if (!enableFlicker) return 1f;
        
        float frequency = (flickerFrequencyRange.x + flickerFrequencyRange.y) * 0.5f;
        float noise = Mathf.PerlinNoise(Time.time * frequency + flickerSeed, flickerSeed * 0.5f);
        return Mathf.Lerp(flickerIntensityRange.x, flickerIntensityRange.y, noise);
    }
    
    /// <summary>
    /// Get flicker multiplier with custom seed for per-object variation.
    /// </summary>
    public float GetFlickerMultiplier(float objectSeed)
    {
        if (!enableFlicker) return 1f;
        
        float frequency = (flickerFrequencyRange.x + flickerFrequencyRange.y) * 0.5f;
        float noise = Mathf.PerlinNoise(Time.time * frequency + objectSeed, objectSeed * 0.5f);
        return Mathf.Lerp(flickerIntensityRange.x, flickerIntensityRange.y, noise);
    }
    
    /// <summary>
    /// Create a preset configuration based on type.
    /// </summary>
    public static NightAccentGroup CreatePreset(NightAccentType type)
    {
        var group = new NightAccentGroup();
        group.presetType = type;
        
        switch (type)
        {
            case NightAccentType.WindowAmbient:
                group.groupName = "Window Ambient";
                group.emissionColor = new Color(1f, 0.85f, 0.6f) * 1.5f; // Warm amber
                group.emissionIntensity = 1.5f;
                group.enablePointLights = false;
                group.enableFlicker = false;
                break;
                
            case NightAccentType.LampPost:
                group.groupName = "Lamp Post";
                group.emissionColor = new Color(1f, 0.95f, 0.8f) * 2f; // Warm white
                group.emissionIntensity = 2.5f;
                group.enablePointLights = true;
                group.pointLightRange = 8f;
                group.pointLightIntensity = 1.2f;
                group.enableFlicker = false;
                break;
                
            case NightAccentType.NeonSign:
                group.groupName = "Neon Sign";
                group.emissionColor = new Color(1f, 0.2f, 0.5f) * 3f; // Pink/magenta
                group.emissionIntensity = 4f;
                group.enablePointLights = true;
                group.pointLightRange = 4f;
                group.pointLightIntensity = 0.8f;
                group.enableFlicker = true;
                group.flickerFrequencyRange = new Vector2(8f, 15f); // Fast flicker
                group.flickerIntensityRange = new Vector2(0.85f, 1.0f);
                break;
                
            case NightAccentType.Firelight:
                group.groupName = "Firelight";
                group.emissionColor = new Color(1f, 0.5f, 0.1f) * 2f; // Orange/fire
                group.emissionIntensity = 2f;
                group.enablePointLights = true;
                group.pointLightRange = 6f;
                group.pointLightIntensity = 1f;
                group.pointLightColor = new Color(1f, 0.6f, 0.2f);
                group.enableFlicker = true;
                group.flickerFrequencyRange = new Vector2(2f, 5f); // Medium flicker
                group.flickerIntensityRange = new Vector2(0.7f, 1.2f);
                break;
                
            case NightAccentType.Industrial:
                group.groupName = "Industrial";
                group.emissionColor = new Color(0.8f, 0.9f, 1f) * 2f; // Cool white/blue
                group.emissionIntensity = 2f;
                group.enablePointLights = true;
                group.pointLightRange = 10f;
                group.pointLightIntensity = 1.5f;
                group.enableFlicker = true;
                group.flickerFrequencyRange = new Vector2(0.1f, 0.3f); // Slow hum
                group.flickerIntensityRange = new Vector2(0.95f, 1.05f);
                break;
                
            case NightAccentType.Magical:
                group.groupName = "Magical";
                group.emissionColor = new Color(0.5f, 0.8f, 1f) * 3f; // Cyan/magical
                group.emissionIntensity = 3f;
                group.enablePointLights = true;
                group.pointLightRange = 5f;
                group.pointLightIntensity = 0.8f;
                group.enableFlicker = true;
                group.flickerFrequencyRange = new Vector2(0.5f, 1.5f);
                group.flickerIntensityRange = new Vector2(0.8f, 1.3f);
                break;
                
            default: // Custom
                group.groupName = "Custom";
                break;
        }
        
        return group;
    }
}

/// <summary>
/// Configuration for night accent lighting with support for multiple groups.
/// Each group (windows, neon, lamps, etc.) can have different colors and behaviors.
/// </summary>
[CreateAssetMenu(fileName = "NightAccentConfig", menuName = "Chainbound/World/Time of Day/Night Accent Config")]
public class NightAccentConfigSO : ScriptableObject
{
    [Header("Global Settings")]
    [Tooltip("Maximum active point lights near player (distance culling across all groups)")]
    [Range(0, 50)]
    [SerializeField] private int maxActivePointLights = 16;
    
    [Tooltip("Distance from player beyond which point lights are disabled")]
    [SerializeField] private float pointLightCullDistance = 25f;
    
    [Header("Global Time Triggers")]
    [Tooltip("Default normalized time to start turning on accents (before night)")]
    [Range(0f, 1f)]
    [SerializeField] private float globalTurnOnTime = 0.75f;
    
    [Tooltip("Default normalized time to start turning off accents (before dawn)")]
    [Range(0f, 1f)]
    [SerializeField] private float globalTurnOffTime = 0.25f;
    
    [Header("Accent Groups")]
    [Tooltip("List of accent lighting groups with individual settings")]
    [SerializeField] private List<NightAccentGroup> accentGroups = new List<NightAccentGroup>();
    
    // Cache for fast lookup by name
    private Dictionary<string, NightAccentGroup> groupLookup;
    private Dictionary<NightAccentType, NightAccentGroup> typeLookup;
    
    // ============================================
    // LEGACY COMPATIBILITY - Default group
    // ============================================
    
    /// <summary>Default emission color (from first group or fallback).</summary>
    public Color EmissionColor => GetDefaultGroup()?.EmissionColor ?? new Color(1f, 0.9f, 0.7f) * 2f;
    
    /// <summary>Default emission intensity (from first group or fallback).</summary>
    public float EmissionIntensity => GetDefaultGroup()?.EmissionIntensity ?? 2f;
    
    /// <summary>Default final emission color (from first group or fallback).</summary>
    public Color FinalEmissionColor => GetDefaultGroup()?.FinalEmissionColor ?? new Color(1f, 0.9f, 0.7f) * 4f;
    
    /// <summary>Default fade duration (from first group or fallback).</summary>
    public float FadeDuration => GetDefaultGroup()?.FadeDuration ?? 1f;
    
    /// <summary>Enable point lights (true if any group has them enabled).</summary>
    public bool EnablePointLights => HasAnyPointLights();
    
    public int MaxActivePointLights => maxActivePointLights;
    public float PointLightCullDistance => pointLightCullDistance;
    
    /// <summary>Default point light range (from first group with lights or fallback).</summary>
    public float PointLightRange => GetFirstGroupWithLights()?.PointLightRange ?? 5f;
    
    /// <summary>Default point light intensity (from first group with lights or fallback).</summary>
    public float PointLightIntensity => GetFirstGroupWithLights()?.PointLightIntensity ?? 1f;
    
    /// <summary>Default point light color (from first group with lights or fallback).</summary>
    public Color PointLightColor => GetFirstGroupWithLights()?.PointLightColor ?? new Color(1f, 0.9f, 0.7f);
    
    /// <summary>Enable flicker (true if any group has it enabled).</summary>
    public bool EnableFlicker => HasAnyFlicker();
    
    public Vector2 FlickerFrequencyRange => GetDefaultGroup()?.FlickerFrequencyRange ?? new Vector2(0.5f, 2f);
    public Vector2 FlickerIntensityRange => GetDefaultGroup()?.FlickerIntensityRange ?? new Vector2(0.9f, 1.1f);
    
    public float TurnOnTime => globalTurnOnTime;
    public float TurnOffTime => globalTurnOffTime;
    
    // ============================================
    // GROUP ACCESS
    // ============================================
    
    /// <summary>All accent groups.</summary>
    public IReadOnlyList<NightAccentGroup> Groups => accentGroups;
    
    /// <summary>Number of accent groups.</summary>
    public int GroupCount => accentGroups.Count;
    
    /// <summary>
    /// Get an accent group by name (case-insensitive).
    /// </summary>
    public NightAccentGroup GetGroup(string groupName)
    {
        EnsureLookupBuilt();
        
        if (string.IsNullOrEmpty(groupName)) return GetDefaultGroup();
        
        if (groupLookup.TryGetValue(groupName.ToLowerInvariant(), out var group))
        {
            return group;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get an accent group by preset type.
    /// </summary>
    public NightAccentGroup GetGroup(NightAccentType type)
    {
        EnsureLookupBuilt();
        
        if (typeLookup.TryGetValue(type, out var group))
        {
            return group;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get an accent group by index.
    /// </summary>
    public NightAccentGroup GetGroup(int index)
    {
        if (index >= 0 && index < accentGroups.Count)
        {
            return accentGroups[index];
        }
        return null;
    }
    
    /// <summary>
    /// Get the first/default group.
    /// </summary>
    public NightAccentGroup GetDefaultGroup()
    {
        return accentGroups.Count > 0 ? accentGroups[0] : null;
    }
    
    /// <summary>
    /// Get all enabled groups.
    /// </summary>
    public IEnumerable<NightAccentGroup> GetEnabledGroups()
    {
        foreach (var group in accentGroups)
        {
            if (group.Enabled)
            {
                yield return group;
            }
        }
    }
    
    // ============================================
    // TIME METHODS
    // ============================================
    
    /// <summary>
    /// Determines if accents should be active based on normalized time (global timing).
    /// </summary>
    public bool ShouldBeActive(float normalizedTime)
    {
        return ShouldBeActive(normalizedTime, globalTurnOnTime, globalTurnOffTime);
    }
    
    /// <summary>
    /// Determines if a specific group should be active based on normalized time.
    /// </summary>
    public bool ShouldBeActive(float normalizedTime, NightAccentGroup group)
    {
        if (group == null || !group.Enabled) return false;
        
        float turnOn = group.UseCustomTiming ? group.CustomTurnOnTime : globalTurnOnTime;
        float turnOff = group.UseCustomTiming ? group.CustomTurnOffTime : globalTurnOffTime;
        
        return ShouldBeActive(normalizedTime, turnOn, turnOff);
    }
    
    private bool ShouldBeActive(float normalizedTime, float turnOn, float turnOff)
    {
        // Handle wrap-around case (night spans midnight)
        if (turnOn > turnOff)
        {
            // Night wraps: active if >= turnOn OR < turnOff
            return normalizedTime >= turnOn || normalizedTime < turnOff;
        }
        else
        {
            // Normal case: active if between turnOn and turnOff
            return normalizedTime >= turnOn && normalizedTime < turnOff;
        }
    }
    
    /// <summary>
    /// Calculate fade progress for smooth transitions (global timing).
    /// Returns 0-1 where 0 = fully off, 1 = fully on.
    /// </summary>
    public float GetFadeProgress(float normalizedTime)
    {
        return GetFadeProgress(normalizedTime, globalTurnOnTime, globalTurnOffTime, FadeDuration);
    }
    
    /// <summary>
    /// Calculate fade progress for a specific group.
    /// </summary>
    public float GetFadeProgress(float normalizedTime, NightAccentGroup group)
    {
        if (group == null || !group.Enabled) return 0f;
        
        float turnOn = group.UseCustomTiming ? group.CustomTurnOnTime : globalTurnOnTime;
        float turnOff = group.UseCustomTiming ? group.CustomTurnOffTime : globalTurnOffTime;
        
        return GetFadeProgress(normalizedTime, turnOn, turnOff, group.FadeDuration);
    }
    
    private float GetFadeProgress(float normalizedTime, float turnOn, float turnOff, float fadeDur)
    {
        bool shouldBeActive = ShouldBeActive(normalizedTime, turnOn, turnOff);
        if (!shouldBeActive) return 0f;
        
        // Convert fade duration to normalized time
        // Assume a 10-minute day cycle (600 seconds) for the conversion
        // fadeDur is in seconds, so fadeDur/600 gives us the normalized fade window
        float fadeWindow = Mathf.Max(fadeDur / 600f, 0.001f); // Minimum window to avoid division by zero
        
        // ============================================
        // WRAPPED CYCLE: turnOn > turnOff (e.g., 0.75 to 0.25)
        // Night spans: turnOn -> 1.0 -> 0.0 -> turnOff
        // ============================================
        if (turnOn > turnOff)
        {
            // Fade-in region: turnOn to turnOn + fadeWindow
            float fadeInStart = turnOn;
            float fadeInEnd = Mathf.Min(turnOn + fadeWindow, 1f);
            
            // Fade-out region: just before turnOff (morning)
            // fadeOutStart wraps around if needed
            float fadeOutStart = turnOff - fadeWindow;
            float fadeOutEnd = turnOff;
            
            // Check if in fade-in region (evening, right after turnOn)
            if (normalizedTime >= fadeInStart && normalizedTime <= fadeInEnd)
            {
                float t = (normalizedTime - fadeInStart) / fadeWindow;
                return Mathf.Clamp01(t);
            }
            
            // Check if in fade-out region (morning, just before turnOff)
            // This region is ONLY near turnOff, not at other times
            if (fadeOutStart >= 0f)
            {
                // fadeOutStart is positive (e.g., 0.24 for turnOff=0.25)
                if (normalizedTime >= fadeOutStart && normalizedTime < fadeOutEnd)
                {
                    float t = (normalizedTime - fadeOutStart) / fadeWindow;
                    return 1f - Mathf.Clamp01(t);
                }
            }
            else
            {
                // fadeOutStart wrapped negative (e.g., turnOff=0.001, fadeWindow=0.01 -> fadeOutStart=-0.009)
                // This means fade-out region spans from (1 + fadeOutStart) to 1.0, then 0 to fadeOutEnd
                float wrappedFadeOutStart = 1f + fadeOutStart;
                if (normalizedTime >= wrappedFadeOutStart || normalizedTime < fadeOutEnd)
                {
                    float distFromStart;
                    if (normalizedTime >= wrappedFadeOutStart)
                    {
                        distFromStart = normalizedTime - wrappedFadeOutStart;
                    }
                    else
                    {
                        distFromStart = (1f - wrappedFadeOutStart) + normalizedTime;
                    }
                    float t = distFromStart / fadeWindow;
                    return 1f - Mathf.Clamp01(t);
                }
            }
            
            // Not in any fade region, but active = fully on
            return 1f;
        }
        // ============================================
        // NORMAL CYCLE: turnOn < turnOff (e.g., 0.25 to 0.75)
        // ============================================
        else
        {
            float fadeInStart = turnOn;
            float fadeInEnd = turnOn + fadeWindow;
            float fadeOutStart = turnOff - fadeWindow;
            float fadeOutEnd = turnOff;
            
            // Check if in fade-in region
            if (normalizedTime >= fadeInStart && normalizedTime < fadeInEnd)
            {
                float t = (normalizedTime - fadeInStart) / fadeWindow;
                return Mathf.Clamp01(t);
            }
            
            // Check if in fade-out region
            if (normalizedTime >= fadeOutStart && normalizedTime < fadeOutEnd)
            {
                float t = (normalizedTime - fadeOutStart) / fadeWindow;
                return 1f - Mathf.Clamp01(t);
            }
            
            // Not in any fade region, but active = fully on
            return 1f;
        }
    }
    
    /// <summary>
    /// Get a randomized flicker value (legacy, uses first group with flicker).
    /// </summary>
    public float GetFlickerMultiplier()
    {
        foreach (var group in accentGroups)
        {
            if (group.EnableFlicker)
            {
                return group.GetFlickerMultiplier();
            }
        }
        return 1f;
    }
    
    // ============================================
    // HELPER METHODS
    // ============================================
    
    private void EnsureLookupBuilt()
    {
        if (groupLookup == null || groupLookup.Count != accentGroups.Count)
        {
            BuildLookup();
        }
    }
    
    private void BuildLookup()
    {
        groupLookup = new Dictionary<string, NightAccentGroup>(StringComparer.OrdinalIgnoreCase);
        typeLookup = new Dictionary<NightAccentType, NightAccentGroup>();
        
        foreach (var group in accentGroups)
        {
            if (!string.IsNullOrEmpty(group.GroupName))
            {
                groupLookup[group.GroupName.ToLowerInvariant()] = group;
            }
            
            if (group.PresetType != NightAccentType.Custom && !typeLookup.ContainsKey(group.PresetType))
            {
                typeLookup[group.PresetType] = group;
            }
        }
    }
    
    private bool HasAnyPointLights()
    {
        foreach (var group in accentGroups)
        {
            if (group.Enabled && group.EnablePointLights) return true;
        }
        return false;
    }
    
    private bool HasAnyFlicker()
    {
        foreach (var group in accentGroups)
        {
            if (group.Enabled && group.EnableFlicker) return true;
        }
        return false;
    }
    
    private NightAccentGroup GetFirstGroupWithLights()
    {
        foreach (var group in accentGroups)
        {
            if (group.Enabled && group.EnablePointLights) return group;
        }
        return null;
    }
    
    // ============================================
    // EDITOR HELPERS
    // ============================================
    
    private void OnValidate()
    {
        pointLightCullDistance = Mathf.Max(1f, pointLightCullDistance);
        
        // Rebuild lookup on change
        groupLookup = null;
        typeLookup = null;
    }
    
    /// <summary>
    /// Add a preset group to the config (Editor helper).
    /// </summary>
    public void AddPresetGroup(NightAccentType type)
    {
        var group = NightAccentGroup.CreatePreset(type);
        accentGroups.Add(group);
        groupLookup = null;
        typeLookup = null;
    }
    
#if UNITY_EDITOR
    [ContextMenu("Add Window Ambient Group")]
    private void AddWindowGroup() => AddPresetGroup(NightAccentType.WindowAmbient);
    
    [ContextMenu("Add Lamp Post Group")]
    private void AddLampGroup() => AddPresetGroup(NightAccentType.LampPost);
    
    [ContextMenu("Add Neon Sign Group")]
    private void AddNeonGroup() => AddPresetGroup(NightAccentType.NeonSign);
    
    [ContextMenu("Add Firelight Group")]
    private void AddFireGroup() => AddPresetGroup(NightAccentType.Firelight);
    
    [ContextMenu("Add Industrial Group")]
    private void AddIndustrialGroup() => AddPresetGroup(NightAccentType.Industrial);
    
    [ContextMenu("Add Magical Group")]
    private void AddMagicalGroup() => AddPresetGroup(NightAccentType.Magical);
#endif
}
