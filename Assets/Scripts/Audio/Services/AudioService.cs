using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Service for managing SFX, UI, and Ambience playback.
/// Implements pooling, polyphony control, and mixer routing.
/// </summary>
public class AudioService : MonoBehaviour, IAudioService
{
    private static AudioService instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static AudioService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AudioService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("AudioService");
                    instance = go.AddComponent<AudioService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Audio Mixer")]
    [Tooltip("Main audio mixer asset")]
    [SerializeField] private AudioMixer audioMixer;
    
    [Header("Pool Settings")]
    [Tooltip("Initial pool size for 2D sources")]
    [SerializeField] private int poolSize2D = 16;
    
    [Tooltip("Initial pool size for 3D sources")]
    [SerializeField] private int poolSize3D = 8;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Pool of AudioSources
    private Queue<AudioSource> pool2D = new Queue<AudioSource>();
    private Queue<AudioSource> pool3D = new Queue<AudioSource>();
    private List<AudioSource> activeSources = new List<AudioSource>();
    
    // Polyphony tracking
    private Dictionary<string, List<AudioSource>> polyphonyInstances = new Dictionary<string, List<AudioSource>>();
    private Dictionary<string, float> polyphonyCooldowns = new Dictionary<string, float>();
    
    // Handle tracking
    private int nextHandleId = 1;
    private Dictionary<int, AudioSource> handleToSource = new Dictionary<int, AudioSource>();
    private Dictionary<AudioSource, int> sourceToHandle = new Dictionary<AudioSource, int>();
    
    // Mixer groups (cached)
    private AudioMixerGroup masterGroup;
    private AudioMixerGroup musicGroup;
    private AudioMixerGroup musicBaseGroup;
    private AudioMixerGroup musicLayerGroup;
    private AudioMixerGroup sfxGroup;
    private AudioMixerGroup uiGroup;
    private AudioMixerGroup ambienceGroup;
    private readonly Dictionary<AudioBus, float> requestedBusVolumes = new Dictionary<AudioBus, float>();
    private bool lifecycleMuted;
    [SerializeField] private float lifecycleMuteDb = -80f;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[AudioService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
        
        InitializeMixer();
        InitializePools();
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    
    private void Update()
    {
        // Update cooldowns
        var keysToUpdate = new List<string>(polyphonyCooldowns.Keys);
        foreach (var key in keysToUpdate)
        {
            polyphonyCooldowns[key] -= Time.deltaTime;
            if (polyphonyCooldowns[key] <= 0f)
            {
                polyphonyCooldowns.Remove(key);
            }
        }
        
        // Clean up finished sources
        for (int i = activeSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = activeSources[i];
            if (source == null || !source.isPlaying)
            {
                ReturnSourceToPool(source);
                activeSources.RemoveAt(i);
            }
        }
    }
    
    private void InitializeMixer()
    {
        if (audioMixer == null)
        {
            Debug.LogError("[AudioService] AudioMixer not assigned! Audio will not work.");
            return;
        }
        
        // Get mixer groups — FindMatchingGroups returns all groups whose path contains
        // the search string, so use exact names to avoid Music matching Music_Base etc.
        var allGroups = audioMixer.FindMatchingGroups(string.Empty);

        masterGroup     = FindGroup(allGroups, "Master");
        musicGroup      = FindGroup(allGroups, "Music");
        musicBaseGroup  = FindGroup(allGroups, "Music_Base");
        musicLayerGroup = FindGroup(allGroups, "Music_Layer");
        sfxGroup        = FindGroup(allGroups, "SFX");
        uiGroup         = FindGroup(allGroups, "UI");
        ambienceGroup   = FindGroup(allGroups, "Ambience");

        foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
        {
            string parameter = GetBusParameterName(bus);
            if (!string.IsNullOrEmpty(parameter) && audioMixer.GetFloat(parameter, out float value))
                requestedBusVolumes[bus] = value;
        }

        if (sfxGroup == null || uiGroup == null || ambienceGroup == null ||
            musicGroup == null || musicBaseGroup == null)
        {
            Debug.LogError("[AudioService] Required mixer groups not found! Ensure mixer has Music, Music_Base, SFX, UI, and Ambience groups.");
        }
    }
    
    private void InitializePools()
    {
        // Create 2D pool
        for (int i = 0; i < poolSize2D; i++)
        {
            AudioSource source = CreatePooledSource(false);
            pool2D.Enqueue(source);
        }
        
        // Create 3D pool
        for (int i = 0; i < poolSize3D; i++)
        {
            AudioSource source = CreatePooledSource(true);
            pool3D.Enqueue(source);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioService] Initialized pools: {poolSize2D} 2D sources, {poolSize3D} 3D sources");
        }
    }
    
    private AudioSource CreatePooledSource(bool is3D)
    {
        GameObject go = new GameObject(is3D ? "AudioSource3D" : "AudioSource2D");
        go.transform.SetParent(transform);
        go.SetActive(false);
        
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = is3D ? 1f : 0f;
        
        return source;
    }
    
    private AudioSource GetPooledSource(bool is3D)
    {
        Queue<AudioSource> pool = is3D ? pool3D : pool2D;
        
        if (pool.Count > 0)
        {
            AudioSource source = pool.Dequeue();
            source.gameObject.SetActive(true);
            return source;
        }
        
        // Pool exhausted - create new source
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[AudioService] Pool exhausted for {(is3D ? "3D" : "2D")} sources. Creating new source.");
        }
        
        AudioSource newSource = CreatePooledSource(is3D);
        newSource.gameObject.SetActive(true);
        return newSource;
    }
    
    private void ReturnSourceToPool(AudioSource source)
    {
        if (source == null) return;
        
        // Clean up handle tracking
        if (sourceToHandle.TryGetValue(source, out int handleId))
        {
            handleToSource.Remove(handleId);
            sourceToHandle.Remove(source);
        }
        
        // Clean up polyphony tracking
        foreach (var kvp in polyphonyInstances)
        {
            kvp.Value.Remove(source);
        }
        
        // Reset source
        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.gameObject.SetActive(false);
        
        // Return to appropriate pool
        bool is3D = source.spatialBlend > 0.5f;
        Queue<AudioSource> pool = is3D ? pool3D : pool2D;
        pool.Enqueue(source);
    }
    
    // ============================================
    // IAudioService Implementation
    // ============================================
    
    public AudioHandle Play(SoundEventSO soundEvent, Vector3? position = null, Transform followTransform = null, float volumeScale = 1f)
    {
        if (soundEvent == null)
        {
            Debug.LogWarning("[AudioService] Cannot play null sound event");
            return new AudioHandle();
        }
        
        if (soundEvent.Clips == null || soundEvent.Clips.Length == 0)
        {
            Debug.LogWarning($"[AudioService] Sound event '{soundEvent.name}' has no clips");
            return new AudioHandle();
        }
        
        // Check polyphony
        if (!string.IsNullOrEmpty(soundEvent.PolyphonyKey))
        {
            // Check cooldown
            if (polyphonyCooldowns.TryGetValue(soundEvent.PolyphonyKey, out float cooldown) && cooldown > 0f)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[AudioService] Sound '{soundEvent.Id}' on cooldown ({cooldown:F2}s remaining)");
                }
                return new AudioHandle();
            }
            
            // Check instance limit
            if (soundEvent.MaxInstances > 0)
            {
                if (!polyphonyInstances.TryGetValue(soundEvent.PolyphonyKey, out List<AudioSource> instances))
                {
                    instances = new List<AudioSource>();
                    polyphonyInstances[soundEvent.PolyphonyKey] = instances;
                }
                
                // Remove finished instances
                instances.RemoveAll(s => s == null || !s.isPlaying);
                
                if (instances.Count >= soundEvent.MaxInstances)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[AudioService] Sound '{soundEvent.Id}' polyphony limit reached ({instances.Count}/{soundEvent.MaxInstances})");
                    }
                    return new AudioHandle();
                }
            }
        }
        
        // Get clip and randomize
        AudioClip clip = soundEvent.GetRandomClip();
        if (clip == null)
        {
            Debug.LogWarning($"[AudioService] Sound event '{soundEvent.name}' has null clip");
            return new AudioHandle();
        }
        
        // Determine if 3D
        bool is3D = soundEvent.Is3D && (position.HasValue || followTransform != null);
        
        // Get pooled source
        AudioSource source = GetPooledSource(is3D);
        
        // Configure source
        source.clip = clip;
        source.volume = Mathf.Clamp01(soundEvent.GetRandomVolume() * Mathf.Max(0f, volumeScale));
        source.pitch = soundEvent.GetRandomPitch();
        source.loop = soundEvent.Loop;
        source.spatialBlend = is3D ? soundEvent.SpatialBlend : 0f;
        source.minDistance = soundEvent.MinDistance;
        source.maxDistance = soundEvent.MaxDistance;
        
        // Set position
        if (is3D)
        {
            if (followTransform != null)
            {
                source.transform.SetParent(followTransform);
                source.transform.localPosition = Vector3.zero;
            }
            else if (position.HasValue)
            {
                source.transform.SetParent(null);
                source.transform.position = position.Value;
            }
        }
        else
        {
            source.transform.SetParent(transform);
            source.transform.localPosition = Vector3.zero;
        }
        
        // Route to mixer group
        AudioMixerGroup group = GetMixerGroup(soundEvent.OutputBus);
        source.outputAudioMixerGroup = group;
        
        // Play
        source.Play();
        activeSources.Add(source);
        
        // Track polyphony
        if (!string.IsNullOrEmpty(soundEvent.PolyphonyKey))
        {
            if (!polyphonyInstances.TryGetValue(soundEvent.PolyphonyKey, out List<AudioSource> instances))
            {
                instances = new List<AudioSource>();
                polyphonyInstances[soundEvent.PolyphonyKey] = instances;
            }
            instances.Add(source);
            
            // Set cooldown
            if (soundEvent.CooldownSeconds > 0f)
            {
                polyphonyCooldowns[soundEvent.PolyphonyKey] = soundEvent.CooldownSeconds;
            }
        }
        
        // Create handle
        int handleId = nextHandleId++;
        handleToSource[handleId] = source;
        sourceToHandle[source] = handleId;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioService] Playing '{soundEvent.Id}' (handle={handleId}, 3D={is3D})");
        }
        
        return new AudioHandle(handleId, this);
    }

    public AudioHandle PlayClip(
        AudioClip clip,
        AudioBus bus,
        bool loop,
        float volume = 1f,
        float pitch = 1f,
        Vector3? position = null,
        Transform followTransform = null)
    {
        if (clip == null)
            return new AudioHandle();

        bool is3D = position.HasValue || followTransform != null;
        AudioSource source = GetPooledSource(is3D);
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, -3f, 3f);
        source.loop = loop;
        source.spatialBlend = is3D ? 1f : 0f;
        if (is3D)
        {
            if (followTransform != null)
            {
                source.transform.SetParent(followTransform);
                source.transform.localPosition = Vector3.zero;
            }
            else
            {
                source.transform.SetParent(null);
                source.transform.position = position.Value;
            }
        }
        else
        {
            source.transform.SetParent(transform);
            source.transform.localPosition = Vector3.zero;
        }

        source.outputAudioMixerGroup = GetMixerGroup(bus);
        source.Play();
        activeSources.Add(source);

        int handleId = nextHandleId++;
        handleToSource[handleId] = source;
        sourceToHandle[source] = handleId;
        return new AudioHandle(handleId, this);
    }
    
    public void Stop(AudioHandle handle, float fadeOutSeconds = 0f)
    {
        if (!handle.IsValid) return;
        StopInternal(handle.id, fadeOutSeconds);
    }
    
    internal void StopInternal(int handleId, float fadeOutSeconds = 0f)
    {
        if (!handleToSource.TryGetValue(handleId, out AudioSource source))
        {
            return;
        }
        
        if (fadeOutSeconds > 0f && source != null && source.isPlaying)
        {
            StartCoroutine(FadeOutCoroutine(source, fadeOutSeconds));
        }
        else
        {
            ReturnSourceToPool(source);
            activeSources.Remove(source);
        }
    }
    
    private IEnumerator FadeOutCoroutine(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        
        while (elapsed < duration && source != null && source.isPlaying)
        {
            // Audio transitions must complete while menus, application
            // interruptions, or loading overlays have gameplay time paused.
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        if (source != null)
        {
            ReturnSourceToPool(source);
            activeSources.Remove(source);
        }
    }
    
    public void SetPitch(AudioHandle handle, float pitch)
    {
        if (!handle.IsValid) return;
        SetPitchInternal(handle.id, pitch);
    }
    
    internal void SetPitchInternal(int handleId, float pitch)
    {
        if (!handleToSource.TryGetValue(handleId, out AudioSource source)) return;
        if (source == null) return;
        source.pitch = Mathf.Clamp(pitch, -3f, 3f);
    }
    
    public void SetVolume(AudioHandle handle, float volume)
    {
        if (!handle.IsValid) return;
        SetVolumeInternal(handle.id, volume);
    }
    
    internal void SetVolumeInternal(int handleId, float volume)
    {
        if (!handleToSource.TryGetValue(handleId, out AudioSource source)) return;
        if (source == null) return;
        source.volume = Mathf.Clamp01(volume);
    }
    
    public bool IsPlaying(AudioHandle handle)
    {
        if (!handle.IsValid) return false;
        if (!handleToSource.TryGetValue(handle.id, out AudioSource source)) return false;
        return source != null && source.isPlaying;
    }
    
    public void SetBusVolume(AudioBus bus, float volumeDb)
    {
        if (audioMixer == null) return;
        
        string paramName = GetBusParameterName(bus);
        if (string.IsNullOrEmpty(paramName)) return;
        
        requestedBusVolumes[bus] = volumeDb;
        audioMixer.SetFloat(paramName, lifecycleMuted ? lifecycleMuteDb : volumeDb);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[AudioService] Set {bus} volume to {volumeDb:F1} dB");
        }
    }
    
    public float GetBusVolume(AudioBus bus)
    {
        if (audioMixer == null) return 0f;
        
        string paramName = GetBusParameterName(bus);
        if (string.IsNullOrEmpty(paramName)) return 0f;
        
        if (requestedBusVolumes.TryGetValue(bus, out float requested))
            return requested;
        if (audioMixer.GetFloat(paramName, out float volumeDb))
        {
            return volumeDb;
        }
        
        return 0f;
    }

    public void SetLifecycleMute(bool muted)
    {
        lifecycleMuted = muted;
        if (audioMixer == null) return;
        foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
        {
            string parameter = GetBusParameterName(bus);
            if (string.IsNullOrEmpty(parameter)) continue;
            float requested = requestedBusVolumes.TryGetValue(bus, out float value) ? value : 0f;
            audioMixer.SetFloat(parameter, muted ? lifecycleMuteDb : requested);
        }
    }
    
    private AudioMixerGroup GetMixerGroup(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.SFX:        return sfxGroup;
            case AudioBus.UI:         return uiGroup;
            case AudioBus.Ambience:   return ambienceGroup;
            case AudioBus.MusicBase:  return musicBaseGroup;
            case AudioBus.MusicLayer: return musicLayerGroup;
            default:                  return masterGroup;
        }
    }

    private string GetBusParameterName(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.SFX:        return "SFXVolDb";
            case AudioBus.UI:         return "UIVolDb";
            case AudioBus.Ambience:   return "AmbVolDb";
            case AudioBus.MusicBase:  return "MusicVolDb";
            case AudioBus.MusicLayer: return "MusicLayerVolDb";
            default:                  return "MasterVolDb";
        }
    }

    /// <summary>
    /// Finds an exact-name mixer group from a pre-fetched array.
    /// FindMatchingGroups("Music") also matches "Music_Base", so we compare the
    /// full path suffix (the group name is the last segment after '/').
    /// </summary>
    private static AudioMixerGroup FindGroup(AudioMixerGroup[] groups, string exactName)
    {
        foreach (var g in groups)
        {
            if (g != null && g.name == exactName)
                return g;
        }
        return null;
    }
}
