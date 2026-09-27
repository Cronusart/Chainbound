using UnityEngine;

/// <summary>
/// Standard authored soundtrack asset for cross-scene music.
/// Plays an optional intro clip once, then hands off seamlessly to a looping core clip.
/// </summary>
[CreateAssetMenu(fileName = "SoundtrackCue", menuName = "Chainbound/Audio/Music/Soundtrack Cue")]
public class SoundtrackCueSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique ID for this soundtrack cue (used for lookup and debugging).")]
    [SerializeField] private string id = "";

    [Header("Clips")]
    [Tooltip("Optional intro clip. Plays once before the looping core begins.")]
    [SerializeField] private AudioClip introClip;

    [Tooltip("Required looping core clip.")]
    [SerializeField] private AudioClip loopClip;

    [Header("Playback")]
    [Tooltip("Default transition time used when this cue is requested without an explicit fade override.")]
    [Range(0f, 5f)]
    [SerializeField] private float defaultTransitionSeconds = 0.75f;

    [Tooltip("Per-cue trim volume in decibels. Useful for balancing tracks before the shared Music mixer bus.")]
    [Range(-80f, 6f)]
    [SerializeField] private float volumeDb = 0f;

    public string Id => id;
    public AudioClip IntroClip => introClip;
    public AudioClip LoopClip => loopClip;
    public float DefaultTransitionSeconds => defaultTransitionSeconds;
    public float VolumeDb => volumeDb;
    public float VolumeLinear => Mathf.Pow(10f, volumeDb / 20f);

    public bool HasIntro => introClip != null;
    public bool IsValid => loopClip != null;

    private void OnValidate()
    {
        defaultTransitionSeconds = Mathf.Max(0f, defaultTransitionSeconds);

        if (introClip != null && loopClip != null)
        {
            if (introClip.frequency != loopClip.frequency)
            {
                Debug.LogWarning(
                    $"[SoundtrackCueSO] '{name}': intro clip sample rate ({introClip.frequency}) " +
                    $"differs from loop clip sample rate ({loopClip.frequency}).");
            }

            if (introClip.channels != loopClip.channels)
            {
                Debug.LogWarning(
                    $"[SoundtrackCueSO] '{name}': intro clip channel count ({introClip.channels}) " +
                    $"differs from loop clip channel count ({loopClip.channels}).");
            }
        }
    }
}
