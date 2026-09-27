using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining music cues per scene.
/// Used by SceneTransitionManager to automatically change music when loading scenes.
/// </summary>
[CreateAssetMenu(fileName = "SceneMusicConfig", menuName = "Chainbound/Audio/Music/Scene Music Config")]
public class SceneMusicConfigSO : ScriptableObject
{
    [Serializable]
    public class SceneMusicEntry
    {
        [Tooltip("Scene name (must match exactly)")]
        public string sceneName;
        
        [Tooltip("Music cue to play in this scene")]
        public MusicCueSO musicCue;
        
        [Tooltip("Crossfade duration when transitioning to this scene's music")]
        [Range(0f, 5f)]
        public float transitionSeconds = 1f;
        
        [Tooltip("Start with music layer active")]
        public bool startWithLayerActive = false;
    }
    
    [Header("Scene Music Mappings")]
    [Tooltip("Music cues for each scene")]
    [SerializeField] private List<SceneMusicEntry> sceneMusicMap = new List<SceneMusicEntry>();
    
    [Header("Defaults")]
    [Tooltip("Default music cue when no scene mapping found (null = stop music)")]
    [SerializeField] private MusicCueSO defaultMusicCue;
    
    [Tooltip("Default transition duration")]
    [Range(0f, 5f)]
    [SerializeField] private float defaultTransitionSeconds = 1f;
    
    // Runtime lookup dictionary
    private Dictionary<string, SceneMusicEntry> lookup;
    private bool isInitialized = false;
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        lookup = new Dictionary<string, SceneMusicEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in sceneMusicMap)
        {
            if (!string.IsNullOrEmpty(entry.sceneName) && !lookup.ContainsKey(entry.sceneName))
            {
                lookup[entry.sceneName] = entry;
            }
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Get music entry for a scene.
    /// </summary>
    /// <param name="sceneName">Scene name to look up</param>
    /// <returns>Scene music entry, or null if not found</returns>
    public SceneMusicEntry GetMusicEntry(string sceneName)
    {
        Initialize();
        
        if (lookup.TryGetValue(sceneName, out var entry))
        {
            return entry;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get music cue for a scene.
    /// </summary>
    /// <param name="sceneName">Scene name to look up</param>
    /// <returns>Music cue, or default cue if not found</returns>
    public MusicCueSO GetMusicCue(string sceneName)
    {
        var entry = GetMusicEntry(sceneName);
        if (entry != null && entry.musicCue != null)
        {
            return entry.musicCue;
        }
        return defaultMusicCue;
    }
    
    /// <summary>
    /// Get transition duration for a scene.
    /// </summary>
    /// <param name="sceneName">Scene name to look up</param>
    /// <returns>Transition duration in seconds</returns>
    public float GetTransitionSeconds(string sceneName)
    {
        var entry = GetMusicEntry(sceneName);
        if (entry != null)
        {
            return entry.transitionSeconds;
        }
        return defaultTransitionSeconds;
    }
    
    /// <summary>
    /// Check if scene should start with music layer active.
    /// </summary>
    /// <param name="sceneName">Scene name to look up</param>
    /// <returns>True if layer should start active</returns>
    public bool ShouldStartWithLayerActive(string sceneName)
    {
        var entry = GetMusicEntry(sceneName);
        return entry != null && entry.startWithLayerActive;
    }
    
    /// <summary>
    /// Default music cue (used when no scene mapping found).
    /// </summary>
    public MusicCueSO DefaultMusicCue => defaultMusicCue;
    
    /// <summary>
    /// Default transition duration.
    /// </summary>
    public float DefaultTransitionSeconds => defaultTransitionSeconds;
}
