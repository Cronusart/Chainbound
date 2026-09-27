using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining footstep sounds per surface type.
/// Maps surface types to sound events with walk/run pitch modifiers.
/// </summary>
[CreateAssetMenu(fileName = "FootstepConfig", menuName = "Chainbound/Audio/SFX/Footstep Config")]
public class FootstepConfigSO : ScriptableObject
{
    [Serializable]
    public class SurfaceFootstepEntry
    {
        [Tooltip("Surface type this entry applies to")]
        public SurfaceType surfaceType = SurfaceType.Default;
        
        [Tooltip("Sound event to play for this surface (should have loop=false for interval-based playback)")]
        public SoundEventSO soundEvent;
        
        [Header("Playback Intervals")]
        [Tooltip("Time between footstep plays when walking (seconds). Lower = faster tempo. Typical range: 0.4-0.6s")]
        [Range(0.05f, 3f)]
        public float walkInterval = 0.5f;
        
        [Tooltip("Time between footstep plays when running/sprinting (seconds). Lower = faster tempo. Typical range: 0.2-0.35s")]
        [Range(0.05f, 2f)]
        public float runInterval = 0.3f;
        
        [Header("Pitch (Optional)")]
        [Tooltip("Use pitch variation (can cause distortion). Keep at 1.0 for tempo-only control.")]
        [Range(0.5f, 2f)]
        public float pitch = 1.0f;
        
        [Tooltip("Enable pitch variation (if false, pitch stays at 1.0)")]
        public bool usePitchVariation = false;
    }
    
    [Header("Surface Mappings")]
    [Tooltip("Footstep sounds per surface type")]
    [SerializeField] private List<SurfaceFootstepEntry> surfaceFootsteps = new List<SurfaceFootstepEntry>();
    
    [Header("Layer Mapping")]
    [Tooltip("Map Unity physics layers to surface types. Index = layer number (0-31)")]
    [SerializeField] private SurfaceType[] layerToSurface = new SurfaceType[32];
    
    [Header("Defaults")]
    [Tooltip("Default surface type when no mapping found")]
    [SerializeField] private SurfaceType defaultSurfaceType = SurfaceType.Default;
    
    [Tooltip("Fade out duration when stopping footsteps (seconds)")]
    [Range(0f, 1f)]
    [SerializeField] private float stopFadeOutSeconds = 0.1f;
    
    // Runtime lookup dictionary
    private Dictionary<SurfaceType, SurfaceFootstepEntry> lookup;
    private bool isInitialized = false;
    
    private void Initialize()
    {
        if (isInitialized && lookup != null) return;
        
        // Always create dictionary, even if list is null/empty
        lookup = new Dictionary<SurfaceType, SurfaceFootstepEntry>();
        
        if (surfaceFootsteps != null)
        {
            foreach (var entry in surfaceFootsteps)
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
    /// Get footstep entry for a surface type.
    /// </summary>
    public SurfaceFootstepEntry GetFootstepEntry(SurfaceType surfaceType)
    {
        Initialize();
        
        // Safety check - ensure lookup is initialized
        if (lookup == null)
        {
            Debug.LogError("[FootstepConfigSO] Lookup dictionary is null after initialization!");
            return null;
        }
        
        if (lookup.TryGetValue(surfaceType, out var entry))
        {
            return entry;
        }
        
        // Fallback to default
        if (surfaceType != defaultSurfaceType && lookup.TryGetValue(defaultSurfaceType, out entry))
        {
            return entry;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get surface type from a Unity physics layer.
    /// </summary>
    public SurfaceType GetSurfaceTypeFromLayer(int layer)
    {
        if (layer >= 0 && layer < layerToSurface.Length)
        {
            SurfaceType result = layerToSurface[layer];
            // Debug: uncomment to see layer mapping
            // Debug.Log($"[FootstepConfigSO] Layer {layer} -> SurfaceType.{result} (array value at index {layer})");
            return result;
        }
        return defaultSurfaceType;
    }
    
    /// <summary>
    /// Debug helper: Print all layer mappings to console.
    /// Call this from inspector context menu or code to verify setup.
    /// </summary>
    [ContextMenu("Debug: Print Layer Mappings")]
    public void DebugPrintLayerMappings()
    {
        Debug.Log("=== FootstepConfigSO Layer Mappings ===");
        for (int i = 0; i < layerToSurface.Length; i++)
        {
            string layerName = UnityEngine.LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                Debug.Log($"  Layer {i} ({layerName}) -> SurfaceType.{layerToSurface[i]}");
            }
        }
        Debug.Log("=== Surface Footstep Entries ===");
        if (surfaceFootsteps != null)
        {
            foreach (var entry in surfaceFootsteps)
            {
                if (entry != null)
                {
                    Debug.Log($"  SurfaceType.{entry.surfaceType} -> {(entry.soundEvent != null ? entry.soundEvent.name : "NULL")}");
                }
            }
        }
        Debug.Log("=== Default Surface Type: " + defaultSurfaceType + " ===");
    }
    
    /// <summary>
    /// Fade out duration for stopping footsteps.
    /// </summary>
    public float StopFadeOutSeconds => stopFadeOutSeconds;
    
    /// <summary>
    /// Default surface type.
    /// </summary>
    public SurfaceType DefaultSurfaceType => defaultSurfaceType;
}
