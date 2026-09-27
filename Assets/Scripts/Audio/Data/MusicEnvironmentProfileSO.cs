using UnityEngine;

/// <summary>
/// Data-driven processing applied to the persistent soundtrack without
/// changing cue playback or the player's Music volume preference.
/// </summary>
[CreateAssetMenu(fileName = "MusicEnvironmentProfile", menuName = "Chainbound/Audio/Music/Environment Profile")]
public sealed class MusicEnvironmentProfileSO : ScriptableObject
{
    [SerializeField] private string profileId = "";
    [SerializeField, Range(10f, 22000f)] private float lowPassCutoffHz = 22000f;
    [SerializeField, Range(-24f, 6f)] private float gainDb = 0f;
    [SerializeField, Min(0f)] private float enterFadeSeconds = 1f;
    [SerializeField, Min(0f)] private float exitFadeSeconds = 1f;
    [SerializeField] private int priority;

    public string ProfileId => profileId;
    public float LowPassCutoffHz => lowPassCutoffHz;
    public float GainDb => gainDb;
    public float EnterFadeSeconds => enterFadeSeconds;
    public float ExitFadeSeconds => exitFadeSeconds;
    public int Priority => priority;

    private void OnValidate()
    {
        profileId = profileId != null ? profileId.Trim() : string.Empty;
        lowPassCutoffHz = Mathf.Clamp(lowPassCutoffHz, 10f, 22000f);
        gainDb = Mathf.Clamp(gainDb, -24f, 6f);
        enterFadeSeconds = Mathf.Max(0f, enterFadeSeconds);
        exitFadeSeconds = Mathf.Max(0f, exitFadeSeconds);
    }
}
