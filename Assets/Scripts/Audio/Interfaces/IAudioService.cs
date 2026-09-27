using UnityEngine;

/// <summary>
/// Interface for audio service.
/// Handles SFX, UI, and Ambience playback with pooling and polyphony control.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Play a sound event.
    /// </summary>
    /// <param name="soundEvent">Sound event to play</param>
    /// <param name="position">World position for 3D sounds (null for 2D)</param>
    /// <param name="followTransform">Transform to follow for 3D sounds (null if not following)</param>
    /// <param name="volumeScale">Optional multiplier applied on top of the SoundEventSO's randomized volume</param>
    /// <returns>Handle for controlling playback, or null if playback failed</returns>
    AudioHandle Play(SoundEventSO soundEvent, Vector3? position = null, Transform followTransform = null, float volumeScale = 1f);
    
    /// <summary>
    /// Stop a playing sound.
    /// </summary>
    /// <param name="handle">Handle returned from Play()</param>
    /// <param name="fadeOutSeconds">Fade out duration (0 = immediate stop)</param>
    void Stop(AudioHandle handle, float fadeOutSeconds = 0f);
    
    /// <summary>
    /// Set volume for a bus in decibels.
    /// </summary>
    /// <param name="bus">Bus to adjust</param>
    /// <param name="volumeDb">Volume in decibels</param>
    void SetBusVolume(AudioBus bus, float volumeDb);
    
    /// <summary>
    /// Get current volume for a bus in decibels.
    /// </summary>
    /// <param name="bus">Bus to query</param>
    /// <returns>Volume in decibels</returns>
    float GetBusVolume(AudioBus bus);

    /// <summary>Temporarily mutes mixer buses for an app lifecycle interruption without changing user settings.</summary>
    void SetLifecycleMute(bool muted);
    
    /// <summary>
    /// Set pitch of a playing sound at runtime.
    /// </summary>
    /// <param name="handle">Handle returned from Play()</param>
    /// <param name="pitch">Pitch value (-3 to 3, 1 = normal)</param>
    void SetPitch(AudioHandle handle, float pitch);
    
    /// <summary>
    /// Set volume of a playing sound at runtime.
    /// </summary>
    /// <param name="handle">Handle returned from Play()</param>
    /// <param name="volume">Volume value (0 to 1)</param>
    void SetVolume(AudioHandle handle, float volume);

    /// <summary>
    /// Play a raw clip on a mixer bus. Used for procedural combat loops.
    /// </summary>
    AudioHandle PlayClip(
        AudioClip clip,
        AudioBus bus,
        bool loop,
        float volume = 1f,
        float pitch = 1f,
        Vector3? position = null,
        Transform followTransform = null);
    
    /// <summary>
    /// Check if a handle is still playing.
    /// </summary>
    /// <param name="handle">Handle to check</param>
    /// <returns>True if still playing</returns>
    bool IsPlaying(AudioHandle handle);
}

/// <summary>
/// Handle for a playing audio instance.
/// Used to stop or control playback.
/// </summary>
public struct AudioHandle
{
    internal readonly int id;
    private readonly AudioService service;
    
    /// <summary>True if this is a valid handle.</summary>
    public bool IsValid => service != null && id > 0;
    
    internal AudioHandle(int id, AudioService service)
    {
        this.id = id;
        this.service = service;
    }
    
    /// <summary>
    /// Stop this audio instance.
    /// </summary>
    /// <param name="fadeOutSeconds">Fade out duration (0 = immediate stop)</param>
    public void Stop(float fadeOutSeconds = 0f)
    {
        if (IsValid)
        {
            service.StopInternal(id, fadeOutSeconds);
        }
    }
    
    /// <summary>
    /// Set pitch of this audio instance at runtime.
    /// </summary>
    /// <param name="pitch">Pitch value (-3 to 3, 1 = normal)</param>
    public void SetPitch(float pitch)
    {
        if (IsValid)
        {
            service.SetPitchInternal(id, pitch);
        }
    }
    
    /// <summary>
    /// Set volume of this audio instance at runtime.
    /// </summary>
    /// <param name="volume">Volume value (0 to 1)</param>
    public void SetVolume(float volume)
    {
        if (IsValid)
        {
            service.SetVolumeInternal(id, volume);
        }
    }
}
