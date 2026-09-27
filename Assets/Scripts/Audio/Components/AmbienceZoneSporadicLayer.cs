using UnityEngine;

/// <summary>
/// Optional one-shot layer for ambience zones. Uses the parent zone's audibility and distance curve.
/// One-shots play as 2D by default so volume follows the player; enable Is3D on a SoundEventSO to spatialize.
/// Can be driven by a runtime sporadic profile override or fall back to local inspector settings.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AmbienceZone))]
public class AmbienceZoneSporadicLayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AmbienceZone ambienceZone;
    [SerializeField] private SporadicAmbienceProfileSO defaultProfile;

    [Header("Fallback One-Shot Pool")]
    [SerializeField] private SoundEventSO[] oneShotPool = new SoundEventSO[0];
    [SerializeField] private float minCooldownSeconds = 8f;
    [SerializeField] private float maxCooldownSeconds = 20f;
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private IAudioService audioService;
    private float nextPlayTime = 0f;
    private bool hasScheduledPlay = false;
    private int lastPlayedIndex = -1;
    private SporadicAmbienceProfileSO runtimeProfileOverride;
    private bool hasRuntimeProfileOverride = false;

    private void Awake()
    {
        if (ambienceZone == null)
            ambienceZone = GetComponent<AmbienceZone>();
    }

    private void OnEnable()
    {
        if (!hasScheduledPlay)
            ScheduleNextPlay();
    }

    private void Start()
    {
        if (audioService == null)
            audioService = AudioService.Instance;

        if (!hasScheduledPlay)
            ScheduleNextPlay();
    }

    private void OnValidate()
    {
        minCooldownSeconds = Mathf.Max(0f, minCooldownSeconds);
        maxCooldownSeconds = Mathf.Max(minCooldownSeconds, maxCooldownSeconds);

        if (ambienceZone == null)
            ambienceZone = GetComponent<AmbienceZone>();
    }

    private void Update()
    {
        SoundEventSO[] activePool = ResolveOneShotPool();
        if (ambienceZone == null || activePool == null || activePool.Length == 0)
            return;

        if (audioService == null)
            audioService = AudioService.Instance;

        if (audioService == null)
            return;

        if (!hasScheduledPlay)
            ScheduleNextPlay();

        if (Time.unscaledTime < nextPlayTime)
            return;

        if (!ambienceZone.IsPlaybackEligible)
            return;

        float zoneVolume = ambienceZone.CurrentDistanceVolume;
        if (zoneVolume <= 0.001f)
            return;

        bool shouldAvoidImmediateRepeat = ResolveAvoidImmediateRepeat();
        int nextIndex = GetNextPlayableIndex(activePool, shouldAvoidImmediateRepeat);
        ScheduleNextPlay();

        if (nextIndex < 0)
            return;

        SoundEventSO soundEvent = activePool[nextIndex];
        if (soundEvent == null)
            return;

        if (soundEvent.Loop)
        {
            Debug.LogWarning($"[AmbienceZoneSporadicLayer] Sound event '{soundEvent.name}' is looping. Sporadic one-shots should use loop=false.");
            return;
        }

        ambienceZone.PlayZoneSound(soundEvent, zoneVolume);
        lastPlayedIndex = nextIndex;

        if (enableDebugLogs)
        {
            Debug.Log($"[AmbienceZoneSporadicLayer] {gameObject.name} played sporadic one-shot '{soundEvent.name}' at volume scale {zoneVolume:F2}");
        }
    }

    private void ScheduleNextPlay()
    {
        float minCooldown = ResolveMinCooldownSeconds();
        float maxCooldown = ResolveMaxCooldownSeconds();
        float cooldown = Mathf.Approximately(minCooldown, maxCooldown)
            ? minCooldown
            : Random.Range(minCooldown, maxCooldown);

        nextPlayTime = Time.unscaledTime + cooldown;
        hasScheduledPlay = true;
    }

    private int GetNextPlayableIndex(SoundEventSO[] activePool, bool shouldAvoidImmediateRepeat)
    {
        int validChoiceCount = 0;

        for (int i = 0; i < activePool.Length; i++)
        {
            if (activePool[i] == null)
                continue;

            if (shouldAvoidImmediateRepeat && activePool.Length > 1 && i == lastPlayedIndex)
                continue;

            validChoiceCount++;
        }

        if (validChoiceCount == 0)
        {
            for (int i = 0; i < activePool.Length; i++)
            {
                if (activePool[i] != null)
                    return i;
            }

            return -1;
        }

        int targetChoice = Random.Range(0, validChoiceCount);

        for (int i = 0; i < activePool.Length; i++)
        {
            if (activePool[i] == null)
                continue;

            if (shouldAvoidImmediateRepeat && activePool.Length > 1 && i == lastPlayedIndex)
                continue;

            if (targetChoice == 0)
                return i;

            targetChoice--;
        }

        return -1;
    }

    /// <summary>
    /// Apply a runtime profile override. Pass null to return to the serialized default or inline fallback data.
    /// </summary>
    public void SetSporadicProfile(SporadicAmbienceProfileSO profile, bool rescheduleNextPlay = true)
    {
        SporadicAmbienceProfileSO previousOverride = CurrentProfileOverride;
        hasRuntimeProfileOverride = profile != null;
        runtimeProfileOverride = profile;

        if (previousOverride == CurrentProfileOverride)
            return;

        if (enableDebugLogs)
        {
            string profileName = CurrentProfileOverride != null ? CurrentProfileOverride.name : "Default/Inline";
            Debug.Log($"[AmbienceZoneSporadicLayer] {gameObject.name} switched sporadic profile to {profileName}");
        }

        if (rescheduleNextPlay)
        {
            RestartSchedule();
        }
    }

    /// <summary>
    /// Default profile used when there is no runtime override. Null means use the inline fallback fields.
    /// </summary>
    public SporadicAmbienceProfileSO DefaultProfile => defaultProfile;

    /// <summary>
    /// Runtime override profile currently applied by a controller. Null means fallback to the default profile or inline fields.
    /// </summary>
    public SporadicAmbienceProfileSO CurrentProfileOverride => hasRuntimeProfileOverride ? runtimeProfileOverride : null;

    private void RestartSchedule()
    {
        lastPlayedIndex = -1;
        hasScheduledPlay = false;
        ScheduleNextPlay();
    }

    private SporadicAmbienceProfileSO ResolveActiveProfile()
    {
        return hasRuntimeProfileOverride ? runtimeProfileOverride : defaultProfile;
    }

    private SoundEventSO[] ResolveOneShotPool()
    {
        SporadicAmbienceProfileSO activeProfile = ResolveActiveProfile();
        return activeProfile != null ? activeProfile.OneShotPool : oneShotPool;
    }

    private float ResolveMinCooldownSeconds()
    {
        SporadicAmbienceProfileSO activeProfile = ResolveActiveProfile();
        return activeProfile != null ? activeProfile.MinCooldownSeconds : minCooldownSeconds;
    }

    private float ResolveMaxCooldownSeconds()
    {
        SporadicAmbienceProfileSO activeProfile = ResolveActiveProfile();
        return activeProfile != null ? activeProfile.MaxCooldownSeconds : maxCooldownSeconds;
    }

    private bool ResolveAvoidImmediateRepeat()
    {
        SporadicAmbienceProfileSO activeProfile = ResolveActiveProfile();
        return activeProfile != null ? activeProfile.AvoidImmediateRepeat : avoidImmediateRepeat;
    }
}
