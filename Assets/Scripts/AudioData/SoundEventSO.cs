using UnityEngine;

/// <summary>
/// ScriptableObject defining a sound event for SFX, UI, or Ambience.
/// This shared data type is isolated from the predefined Assembly-CSharp assembly so
/// data-driven combat assemblies can serialize it directly.
/// </summary>
[CreateAssetMenu(fileName = "SoundEvent", menuName = "Chainbound/Audio/SFX/Sound Event")]
public class SoundEventSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique ID for this sound event (used for lookup and debugging)")]
    [SerializeField] private string id = "";

    [Header("Audio Clips")]
    [Tooltip("Audio clips to play (randomly selected)")]
    [SerializeField] private AudioClip[] clips = new AudioClip[0];

    [Header("Routing")]
    [Tooltip("Output bus (SFX, UI, or Ambience)")]
    [SerializeField] private AudioBus outputBus = AudioBus.SFX;

    [Header("Playback")]
    [SerializeField] private bool loop = false;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float volumeMin = 1f;
    [Range(0f, 1f)] [SerializeField] private float volumeMax = 1f;

    [Header("Pitch")]
    [Range(-3f, 3f)] [SerializeField] private float pitchMin = 1f;
    [Range(-3f, 3f)] [SerializeField] private float pitchMax = 1f;

    [Header("3D Spatialization")]
    [Tooltip("When true and a world position is supplied, Unity spatializes against the AudioListener (usually the camera). Leave this off for ambience unless you want listener pan and rolloff.")]
    [SerializeField] private bool is3D = false;
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 500f;

    [Header("Polyphony")]
    [SerializeField] private string polyphonyKey = "";
    [SerializeField] private int maxInstances = 0;
    [SerializeField] private float cooldownSeconds = 0f;

    public string Id => id;
    public AudioClip[] Clips => clips;
    public AudioBus OutputBus => outputBus;
    public bool Loop => loop;
    public float VolumeMin => volumeMin;
    public float VolumeMax => volumeMax;
    public float PitchMin => pitchMin;
    public float PitchMax => pitchMax;
    public bool Is3D => is3D;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public string PolyphonyKey => polyphonyKey;
    public int MaxInstances => maxInstances;
    public float CooldownSeconds => cooldownSeconds;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    public float GetRandomVolume() => Random.Range(volumeMin, volumeMax);
    public float GetRandomPitch() => Random.Range(pitchMin, pitchMax);

    private void OnValidate()
    {
        volumeMin = Mathf.Clamp01(volumeMin);
        volumeMax = Mathf.Clamp(volumeMax, volumeMin, 1f);
        pitchMin = Mathf.Clamp(pitchMin, -3f, 3f);
        pitchMax = Mathf.Clamp(pitchMax, pitchMin, 3f);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        maxInstances = Mathf.Max(0, maxInstances);
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
    }
}
