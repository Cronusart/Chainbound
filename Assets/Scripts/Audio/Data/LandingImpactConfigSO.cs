using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration asset for landing impact sounds per surface type.
/// Volume scales dynamically based on fall height.
/// </summary>
[CreateAssetMenu(fileName = "LandingImpactConfig", menuName = "Chainbound/Audio/SFX/Landing Impact Config")]
public class LandingImpactConfigSO : ScriptableObject
{
    [Serializable]
    public class SurfaceLandingEntry
    {
        [Tooltip("Surface type this entry applies to")]
        public SurfaceType surfaceType;
        
        [Tooltip("Sound event to play on landing")]
        public SoundEventSO soundEvent;
        
        [Header("Volume Scaling (Optional Override)")]
        [Tooltip("If true, use per-surface volume range instead of global")]
        public bool useCustomVolumeRange = false;
        
        [Tooltip("Minimum volume multiplier (at min fall height)")]
        [Range(0f, 1f)]
        public float minVolume = 0.3f;
        
        [Tooltip("Maximum volume multiplier (at max fall height)")]
        [Range(0f, 1f)]
        public float maxVolume = 1f;
    }
    
    [Header("Surface Landing Sounds")]
    [Tooltip("Landing sound entries per surface type")]
    [SerializeField] private List<SurfaceLandingEntry> surfaceLandings = new List<SurfaceLandingEntry>();
    
    [Header("Default Landing Sound")]
    [Tooltip("Fallback sound when no surface-specific entry exists")]
    [SerializeField] private SoundEventSO defaultLandingSound;
    
    [Header("Fall Height Thresholds")]
    [Tooltip("Minimum fall height to trigger landing sound (below this = no sound)")]
    [SerializeField] private float minFallHeightForSound = 1f;
    
    [Tooltip("Fall height for maximum volume (above this = max volume)")]
    [SerializeField] private float maxFallHeightForSound = 8f;
    
    [Header("Global Volume Scaling")]
    [Tooltip("Minimum volume multiplier (at min fall height)")]
    [Range(0f, 1f)]
    [SerializeField] private float globalMinVolume = 0.2f;
    
    [Tooltip("Maximum volume multiplier (at max fall height)")]
    [Range(0f, 1f)]
    [SerializeField] private float globalMaxVolume = 1f;
    
    [Header("Volume Curve")]
    [Tooltip("Curve for volume interpolation (X = normalized fall height 0-1, Y = volume multiplier 0-1)")]
    [SerializeField] private AnimationCurve volumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    // Runtime lookup
    private Dictionary<SurfaceType, SurfaceLandingEntry> lookup;
    private bool isInitialized = false;
    
    /// <summary>
    /// Minimum fall height to trigger landing sound.
    /// </summary>
    public float MinFallHeightForSound => minFallHeightForSound;
    
    /// <summary>
    /// Fall height at which maximum volume is reached.
    /// </summary>
    public float MaxFallHeightForSound => maxFallHeightForSound;
    
    private void OnEnable()
    {
        // Reset on domain reload
        isInitialized = false;
        lookup = null;
    }
    
    private void Initialize()
    {
        if (isInitialized && lookup != null) return;
        
        lookup = new Dictionary<SurfaceType, SurfaceLandingEntry>();
        
        if (surfaceLandings != null)
        {
            foreach (var entry in surfaceLandings)
            {
                if (entry != null && entry.soundEvent != null && !lookup.ContainsKey(entry.surfaceType))
                {
                    lookup[entry.surfaceType] = entry;
                }
            }
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Get the landing entry for a surface type.
    /// </summary>
    public SurfaceLandingEntry GetLandingEntry(SurfaceType surfaceType)
    {
        Initialize();
        
        if (lookup == null) return null;
        
        if (lookup.TryGetValue(surfaceType, out var entry))
        {
            return entry;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the sound event for a surface type (falls back to default).
    /// </summary>
    public SoundEventSO GetLandingSound(SurfaceType surfaceType)
    {
        var entry = GetLandingEntry(surfaceType);
        if (entry != null && entry.soundEvent != null)
        {
            return entry.soundEvent;
        }
        
        return defaultLandingSound;
    }
    
    /// <summary>
    /// Calculate volume multiplier based on fall height.
    /// </summary>
    /// <param name="fallHeight">Actual fall height in units</param>
    /// <param name="surfaceType">Optional surface type for per-surface volume range</param>
    /// <returns>Volume multiplier (0-1)</returns>
    public float CalculateVolumeMultiplier(float fallHeight, SurfaceType? surfaceType = null)
    {
        // Below minimum threshold = no sound
        if (fallHeight < minFallHeightForSound)
        {
            return 0f;
        }
        
        // Normalize fall height to 0-1 range
        float normalizedHeight = Mathf.Clamp01(
            (fallHeight - minFallHeightForSound) / (maxFallHeightForSound - minFallHeightForSound)
        );
        
        // Apply curve
        float curveValue = volumeCurve.Evaluate(normalizedHeight);
        
        // Get volume range (per-surface or global)
        float minVol = globalMinVolume;
        float maxVol = globalMaxVolume;
        
        if (surfaceType.HasValue)
        {
            var entry = GetLandingEntry(surfaceType.Value);
            if (entry != null && entry.useCustomVolumeRange)
            {
                minVol = entry.minVolume;
                maxVol = entry.maxVolume;
            }
        }
        
        // Interpolate volume
        return Mathf.Lerp(minVol, maxVol, curveValue);
    }
    
    /// <summary>
    /// Check if fall height is sufficient to trigger landing sound.
    /// </summary>
    public bool ShouldPlayLandingSound(float fallHeight)
    {
        return fallHeight >= minFallHeightForSound;
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Debug: Print Surface Mappings")]
    private void PrintSurfaceMappings()
    {
        Debug.Log("=== LandingImpactConfigSO Surface Mappings ===");
        Debug.Log($"Min Fall Height: {minFallHeightForSound}");
        Debug.Log($"Max Fall Height: {maxFallHeightForSound}");
        Debug.Log($"Global Volume Range: {globalMinVolume} - {globalMaxVolume}");
        Debug.Log("Surface Entries:");
        
        if (surfaceLandings != null)
        {
            foreach (var entry in surfaceLandings)
            {
                string soundName = entry.soundEvent != null ? entry.soundEvent.name : "NULL";
                Debug.Log($"  {entry.surfaceType} -> {soundName}");
            }
        }
        
        string defaultName = defaultLandingSound != null ? defaultLandingSound.name : "NULL";
        Debug.Log($"Default: {defaultName}");
        Debug.Log("=============================================");
    }
    #endif
}
