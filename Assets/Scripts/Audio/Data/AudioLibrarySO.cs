using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional ScriptableObject for ID-based lookup of audio assets.
/// Provides convenience methods for finding SoundEventSO and SoundtrackCueSO by ID.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Chainbound/Audio/Library/Audio Library")]
public class AudioLibrarySO : ScriptableObject
{
    [Header("Sound Events")]
    [Tooltip("Sound events indexed by ID")]
    [SerializeField] private List<SoundEventSO> soundEvents = new List<SoundEventSO>();
    
    [Header("Soundtracks")]
    [Tooltip("Soundtrack cues indexed by ID")]
    [SerializeField] private List<SoundtrackCueSO> soundtrackCues = new List<SoundtrackCueSO>();
    
    // Runtime lookup dictionaries
    private Dictionary<string, SoundEventSO> soundEventLookup;
    private Dictionary<string, SoundtrackCueSO> soundtrackCueLookup;
    private bool isInitialized = false;
    
    /// <summary>
    /// Initialize lookup dictionaries. Called automatically on first access.
    /// </summary>
    private void Initialize()
    {
        if (isInitialized) return;
        
        soundEventLookup = new Dictionary<string, SoundEventSO>();
        soundtrackCueLookup = new Dictionary<string, SoundtrackCueSO>();
        
        foreach (var soundEvent in soundEvents)
        {
            if (soundEvent != null && !string.IsNullOrEmpty(soundEvent.Id))
            {
                if (soundEventLookup.ContainsKey(soundEvent.Id))
                {
                    Debug.LogWarning($"[AudioLibrarySO] Duplicate sound event ID '{soundEvent.Id}' in library '{name}'");
                }
                else
                {
                    soundEventLookup[soundEvent.Id] = soundEvent;
                }
            }
        }
        
        foreach (var soundtrackCue in soundtrackCues)
        {
            if (soundtrackCue != null && !string.IsNullOrEmpty(soundtrackCue.Id))
            {
                if (soundtrackCueLookup.ContainsKey(soundtrackCue.Id))
                {
                    Debug.LogWarning($"[AudioLibrarySO] Duplicate soundtrack cue ID '{soundtrackCue.Id}' in library '{name}'");
                }
                else
                {
                    soundtrackCueLookup[soundtrackCue.Id] = soundtrackCue;
                }
            }
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Get a sound event by ID.
    /// </summary>
    /// <param name="id">Sound event ID</param>
    /// <returns>Sound event, or null if not found</returns>
    public SoundEventSO GetSoundEvent(string id)
    {
        Initialize();
        soundEventLookup.TryGetValue(id, out SoundEventSO result);
        return result;
    }
    
    /// <summary>
    /// Get a soundtrack cue by ID.
    /// </summary>
    /// <param name="id">Soundtrack cue ID</param>
    /// <returns>Soundtrack cue, or null if not found</returns>
    public SoundtrackCueSO GetSoundtrackCue(string id)
    {
        Initialize();
        soundtrackCueLookup.TryGetValue(id, out SoundtrackCueSO result);
        return result;
    }
    
    /// <summary>
    /// Check if a sound event exists.
    /// </summary>
    public bool HasSoundEvent(string id)
    {
        Initialize();
        return soundEventLookup.ContainsKey(id);
    }
    
    /// <summary>
    /// Check if a soundtrack cue exists.
    /// </summary>
    public bool HasSoundtrackCue(string id)
    {
        Initialize();
        return soundtrackCueLookup.ContainsKey(id);
    }
}
