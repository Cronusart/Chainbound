using System.Collections;
using UnityEngine;

/// <summary>
/// Ambience zone that plays a looping bed with distance-based volume.
/// Place this component in the scene at locations where ambience should be audible.
/// Volume fades based on distance from player (or any target transform).
/// Beds play as 2D by default so Unity's camera AudioListener does not also attenuate or pan them.
/// Enable Is3D on a SoundEventSO to opt that event back into listener spatialization.
///
/// Example use cases:
/// - Ocean sounds near docks
/// - Forest ambience in wooded areas
/// - Cave echoes in underground areas
/// - Machine hums near factories
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class AmbienceZone : MonoBehaviour
{
    [Header("Ambience Configuration")]
    [Tooltip("Looping sound event to play. Set OutputBus to Ambience. Leave Is3D off so volume follows player distance; enable Is3D only to spatialize against the camera listener.")]
    [SerializeField] private SoundEventSO ambienceSound;
    
    [Header("Distance Fade")]
    [Tooltip("Distance at which sound reaches maximum volume")]
    [SerializeField] private float maxVolumeDistance = 5f;
    
    [Tooltip("Distance at which sound fades to zero (beyond this = no sound)")]
    [SerializeField] private float minVolumeDistance = 50f;
    
    [Tooltip("Volume curve for distance fade (X = normalized distance 0-1, Y = volume 0-1)")]
    [SerializeField] private AnimationCurve distanceFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Volume Control")]
    [Tooltip("Maximum volume multiplier (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVolumeMultiplier = 1f;
    
    [Tooltip("Minimum volume multiplier (0-1) - sound never goes below this")]
    [Range(0f, 1f)]
    [SerializeField] private float minVolumeMultiplier = 0f;
    
    [Header("Target Tracking")]
    [Tooltip("Target to track distance from (null = auto-find player)")]
    [SerializeField] private Transform targetTransform;
    
    [Tooltip("If true, searches for Player tag on Start")]
    [SerializeField] private bool autoFindPlayer = true;
    
    [Header("Playback Control")]
    [Tooltip("Start playing automatically on Start")]
    [SerializeField] private bool playOnStart = true;
    
    [Tooltip("Fade in duration when entering zone (seconds)")]
    [SerializeField] private float fadeInDuration = 1f;
    
    [Tooltip("Fade out duration when leaving zone (seconds)")]
    [SerializeField] private float fadeOutDuration = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawGizmos = true;
    
    // Runtime state
    private AudioHandle currentHandle;
    private IAudioService audioService;
    private bool isPlaying = false;
    private Coroutine fadeCoroutine;
    private Coroutine bedSwapCoroutine;
    // Fade multiplier is kept separate from distance-based volume so fades do not fight the zone curve.
    private float currentFadeMultiplier = 1f;
    private float activeFadeTarget = 1f;
    private bool stopAfterFade = false;
    private SoundEventSO runtimeAmbienceSound;
    private bool hasRuntimeAmbienceOverride = false;
    private bool isSwitchingBeds = false;
    
    // Cached components
    private SphereCollider zoneCollider;
    
    private void Awake()
    {
        zoneCollider = GetComponent<SphereCollider>();

        SyncZoneCollider();
        TryAssignTargetTransform();
    }

    private void OnValidate()
    {
        maxVolumeDistance = Mathf.Max(0f, maxVolumeDistance);
        minVolumeDistance = Mathf.Max(maxVolumeDistance, minVolumeDistance);

        maxVolumeMultiplier = Mathf.Clamp01(maxVolumeMultiplier);
        minVolumeMultiplier = Mathf.Clamp01(minVolumeMultiplier);
        if (minVolumeMultiplier > maxVolumeMultiplier)
            minVolumeMultiplier = maxVolumeMultiplier;

        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        if (zoneCollider == null)
            zoneCollider = GetComponent<SphereCollider>();

        SyncZoneCollider();
    }
    
    private void Start()
    {
        TryAssignAudioService();
        ValidateAmbienceSound(CurrentAmbienceSound);

        if (playOnStart)
        {
            RefreshPlaybackState();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[AmbienceZone] Initialized: {gameObject.name}, MaxDist: {maxVolumeDistance}, MinDist: {minVolumeDistance}");
        }
    }
    
    private void Update()
    {
        if (audioService == null)
            TryAssignAudioService();

        if (audioService == null)
            return;

        if (targetTransform == null && autoFindPlayer)
        {
            TryAssignTargetTransform();
        }

        if (CurrentAmbienceSound == null)
        {
            if (isPlaying && !isSwitchingBeds)
                StopAmbience();

            return;
        }

        if (playOnStart && !isSwitchingBeds)
        {
            RefreshPlaybackState();
        }

        if (isPlaying)
        {
            ApplyCurrentVolume(GetTrackedDistance());
        }
    }
    
    /// <summary>
    /// Start playing the ambience sound.
    /// </summary>
    public void StartAmbience()
    {
        StartAmbience(-1f);
    }

    private void StartAmbience(float fadeInDurationOverride)
    {
        SoundEventSO activeBed = CurrentAmbienceSound;
        if (activeBed == null || audioService == null || isSwitchingBeds)
            return;

        if (isPlaying)
        {
            if (fadeCoroutine == null && currentFadeMultiplier >= 0.999f)
                return;

            StartFade(1f, ResolveFadeInDuration(fadeInDurationOverride), false);
            return;
        }

        currentHandle = PlayZoneSound(activeBed);
        if (!currentHandle.IsValid)
        {
            Debug.LogWarning($"[AmbienceZone] Failed to play ambience sound on {gameObject.name}");
            return;
        }

        isPlaying = true;
        float fadeInSeconds = ResolveFadeInDuration(fadeInDurationOverride);
        currentFadeMultiplier = fadeInSeconds > 0f ? 0f : 1f;
        ApplyCurrentVolume(GetTrackedDistance());

        if (fadeInSeconds > 0f)
        {
            StartFade(1f, fadeInSeconds, false);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[AmbienceZone] Started ambience: {gameObject.name} ({activeBed.name})");
        }
    }
    
    /// <summary>
    /// Stop playing the ambience sound.
    /// </summary>
    public void StopAmbience()
    {
        if (isSwitchingBeds)
            return;

        if (!isPlaying)
            return;

        if (fadeCoroutine == null && currentFadeMultiplier <= 0.001f)
        {
            StopPlaybackImmediate();
            return;
        }

        if (fadeOutDuration > 0f && currentHandle.IsValid)
        {
            StartFade(0f, fadeOutDuration, true);
        }
        else
        {
            StopPlaybackImmediate();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[AmbienceZone] Stopped ambience: {gameObject.name}");
        }
    }

    private void StartFade(float targetMultiplier, float duration, bool stopWhenFinished)
    {
        if (!currentHandle.IsValid || audioService == null)
            return;

        if (fadeCoroutine != null &&
            Mathf.Approximately(activeFadeTarget, targetMultiplier) &&
            stopAfterFade == stopWhenFinished)
        {
            return;
        }

        StopActiveFade();

        activeFadeTarget = targetMultiplier;
        stopAfterFade = stopWhenFinished;

        if (duration <= 0f)
        {
            currentFadeMultiplier = targetMultiplier;
            ApplyCurrentVolume(GetTrackedDistance());

            if (stopWhenFinished)
                StopPlaybackImmediate();

            return;
        }

        fadeCoroutine = StartCoroutine(FadeMultiplierCoroutine(targetMultiplier, duration, stopWhenFinished));
    }

    private IEnumerator FadeMultiplierCoroutine(float targetMultiplier, float duration, bool stopWhenFinished)
    {
        float elapsed = 0f;
        float startMultiplier = currentFadeMultiplier;

        while (elapsed < duration && currentHandle.IsValid)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentFadeMultiplier = Mathf.Lerp(startMultiplier, targetMultiplier, t);
            ApplyCurrentVolume(GetTrackedDistance());
            yield return null;
        }

        currentFadeMultiplier = targetMultiplier;

        if (stopWhenFinished)
        {
            StopPlaybackImmediate();
        }
        else
        {
            ApplyCurrentVolume(GetTrackedDistance());
            fadeCoroutine = null;
        }
    }

    private void RefreshPlaybackState()
    {
        RefreshPlaybackState(-1f);
    }

    private void RefreshPlaybackState(float fadeInDurationOverride)
    {
        bool shouldPlay = ShouldBeAudibleAtCurrentDistance();

        if (shouldPlay)
            StartAmbience(fadeInDurationOverride);
        else
            StopAmbience();
    }

    private void ApplyCurrentVolume(float distance)
    {
        if (!currentHandle.IsValid || audioService == null)
            return;

        float distanceVolume = EvaluateDistanceVolume(distance);
        float finalVolume = Mathf.Clamp01(distanceVolume * currentFadeMultiplier);
        audioService.SetVolume(currentHandle, finalVolume);
    }

    private float EvaluateDistanceVolume(float distance)
    {
        if (!HasTrackedTarget())
            return maxVolumeMultiplier;

        if (Mathf.Approximately(maxVolumeDistance, minVolumeDistance))
            return distance <= maxVolumeDistance ? maxVolumeMultiplier : minVolumeMultiplier;

        float normalizedDistance = Mathf.InverseLerp(maxVolumeDistance, minVolumeDistance, distance);
        float curveValue = distanceFadeCurve != null
            ? distanceFadeCurve.Evaluate(normalizedDistance)
            : 1f - normalizedDistance;

        return Mathf.Lerp(minVolumeMultiplier, maxVolumeMultiplier, curveValue);
    }

    private float GetTrackedDistance()
    {
        return HasTrackedTarget()
            ? Vector3.Distance(transform.position, targetTransform.position)
            : maxVolumeDistance;
    }

    private bool HasTrackedTarget()
    {
        return targetTransform != null;
    }

    private void StopActiveFade()
    {
        if (fadeCoroutine == null)
            return;

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void StopPlaybackImmediate()
    {
        StopActiveFade();
        StopPendingBedSwap();

        if (currentHandle.IsValid && audioService != null)
        {
            audioService.Stop(currentHandle);
        }

        currentHandle = default;
        isPlaying = false;
        currentFadeMultiplier = 1f;
        activeFadeTarget = 1f;
        stopAfterFade = false;
    }

    private void StopPendingBedSwap()
    {
        if (bedSwapCoroutine == null)
            return;

        StopCoroutine(bedSwapCoroutine);
        bedSwapCoroutine = null;
        isSwitchingBeds = false;
    }

    private void SyncZoneCollider()
    {
        if (zoneCollider == null)
            return;

        zoneCollider.isTrigger = true;
        zoneCollider.radius = minVolumeDistance;
    }

    private void TryAssignTargetTransform()
    {
        if (targetTransform != null || !autoFindPlayer)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            targetTransform = player.transform;
    }

    private bool IsTrackedCollider(Collider other)
    {
        if (other == null)
            return false;

        if (targetTransform == null)
            return other.CompareTag("Player");

        Transform otherTransform = other.transform;
        return otherTransform == targetTransform || otherTransform.IsChildOf(targetTransform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!playOnStart || isSwitchingBeds || !IsTrackedCollider(other))
            return;

        RefreshPlaybackState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (isSwitchingBeds || !IsTrackedCollider(other))
        {
            return;
        }

        RefreshPlaybackState();
    }
    
    private void OnDisable()
    {
        StopPlaybackImmediate();
    }
    
    private void OnDestroy()
    {
        StopPlaybackImmediate();
    }
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        
        // Draw max volume distance (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, maxVolumeDistance);
        
        // Draw min volume distance (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minVolumeDistance);
        
        // Draw zone center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw line to target if assigned
        if (targetTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetTransform.position);
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Set the target transform to track distance from.
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;

        if (playOnStart && !isSwitchingBeds)
        {
            RefreshPlaybackState();
        }
    }

    /// <summary>
    /// Override the looping bed at runtime. Pass null to revert to the serialized default bed.
    /// </summary>
    public void SetAmbienceBed(SoundEventSO newBed, float transitionSeconds)
    {
        SoundEventSO previousBed = CurrentAmbienceSound;
        hasRuntimeAmbienceOverride = newBed != null;
        runtimeAmbienceSound = newBed;

        SoundEventSO resolvedBed = CurrentAmbienceSound;
        if (previousBed == resolvedBed && !isSwitchingBeds)
        {
            if (!isPlaying && playOnStart && IsPlaybackEligible)
                StartAmbience(Mathf.Max(0f, transitionSeconds));

            return;
        }

        ValidateAmbienceSound(resolvedBed);

        if (enableDebugLogs)
        {
            string bedName = resolvedBed != null ? resolvedBed.name : "None";
            Debug.Log($"[AmbienceZone] {gameObject.name} switching ambience bed to {bedName}");
        }

        if (!isActiveAndEnabled)
            return;

        if (audioService == null)
            TryAssignAudioService();

        if (audioService == null)
            return;

        StopPendingBedSwap();

        float clampedTransition = Mathf.Max(0f, transitionSeconds);
        if (isPlaying)
        {
            bool resumeManualPlayback = !playOnStart;
            bedSwapCoroutine = StartCoroutine(SwapBedCoroutine(clampedTransition, resumeManualPlayback));
            return;
        }

        if (playOnStart && IsPlaybackEligible)
        {
            StartAmbience(clampedTransition);
        }
    }
    
    /// <summary>
    /// Check if ambience is currently playing.
    /// </summary>
    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Serialized default ambience bed used as the fallback when no override is active.
    /// </summary>
    public SoundEventSO DefaultAmbienceSound => ambienceSound;

    /// <summary>
    /// Current runtime ambience bed, falling back to the serialized default when no override is active.
    /// </summary>
    public SoundEventSO CurrentAmbienceSound => hasRuntimeAmbienceOverride ? runtimeAmbienceSound : ambienceSound;

    /// <summary>
    /// Whether the target is close enough for the zone to be eligible for playback.
    /// </summary>
    public bool IsPlaybackEligible => CurrentAmbienceSound != null && audioService != null && ShouldBeAudibleAtCurrentDistance();

    /// <summary>
    /// Distance-curve volume at the current target distance, independent of fade state.
    /// </summary>
    public float CurrentDistanceVolume => EvaluateDistanceVolume(GetTrackedDistance());
    
    /// <summary>
    /// Get current distance from target (or -1 if no target).
    /// </summary>
    public float CurrentDistance => targetTransform != null 
        ? Vector3.Distance(transform.position, targetTransform.position) 
        : -1f;
    
    #endregion

    private IEnumerator SwapBedCoroutine(float transitionSeconds, bool resumeManualPlayback)
    {
        isSwitchingBeds = true;
        StopActiveFade();

        if (currentHandle.IsValid && audioService != null)
        {
            audioService.Stop(currentHandle, transitionSeconds);
        }

        currentHandle = default;
        isPlaying = false;
        currentFadeMultiplier = 1f;
        activeFadeTarget = 1f;
        stopAfterFade = false;

        if (transitionSeconds > 0f)
            yield return new WaitForSeconds(transitionSeconds);

        isSwitchingBeds = false;
        bedSwapCoroutine = null;

        if (!isActiveAndEnabled || CurrentAmbienceSound == null)
            yield break;

        if (resumeManualPlayback)
            StartAmbience(transitionSeconds);
        else if (playOnStart)
            RefreshPlaybackState(transitionSeconds);
    }

    private bool TryAssignAudioService()
    {
        audioService = AudioService.Instance;
        return audioService != null;
    }

    /// <summary>
    /// Play an ambience event with this zone's spatial policy:
    /// 2D by default so volume follows player distance, or 3D at the zone when the event has Is3D enabled.
    /// </summary>
    public AudioHandle PlayZoneSound(SoundEventSO soundEvent, float volumeScale = 1f)
    {
        if (soundEvent == null)
            return default;

        if (audioService == null)
            TryAssignAudioService();

        if (audioService == null)
            return default;

        return soundEvent.Is3D
            ? audioService.Play(soundEvent, transform.position, volumeScale: volumeScale)
            : audioService.Play(soundEvent, volumeScale: volumeScale);
    }

    private void ValidateAmbienceSound(SoundEventSO soundEvent)
    {
        if (soundEvent == null)
            return;

        if (soundEvent.Is3D && enableDebugLogs)
        {
            Debug.Log($"[AmbienceZone] Sound event '{soundEvent.name}' is marked 3D; spatializing against the camera AudioListener in addition to the player-distance curve.");
        }

        if (!soundEvent.Loop)
        {
            Debug.LogWarning($"[AmbienceZone] Sound event '{soundEvent.name}' is not looping. Ambient beds should usually use loop=true.");
        }
    }

    private bool ShouldBeAudibleAtCurrentDistance()
    {
        return !HasTrackedTarget() || GetTrackedDistance() <= minVolumeDistance;
    }

    private float ResolveFadeInDuration(float fadeInDurationOverride)
    {
        return fadeInDurationOverride >= 0f ? fadeInDurationOverride : fadeInDuration;
    }
}
