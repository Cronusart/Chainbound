using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent soundtrack service for cross-scene music.
/// Supports optional intro clips, seamless loop scheduling, scene policy lookup, and A/B deck crossfades.
/// </summary>
public class MusicConductor : MonoBehaviour, IMusicConductor
{
    private sealed class MusicDeck
    {
        public AudioSource introSource;
        public AudioSource loopSource;
        public SoundtrackCueSO cue;
        public float cueVolumeLinear = 1f;
        public float gain;
    }

    private sealed class ScriptedDuckRequest
    {
        public float targetMultiplier;
        public float holdUntil;
        public float releaseSeconds;
    }

    private sealed class EnvironmentRequest
    {
        public MusicEnvironmentProfileSO profile;
        public long sequence;
    }

    private static MusicConductor instance;

    public static MusicConductor Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<MusicConductor>();
                if (instance == null)
                {
                    GameObject go = new GameObject("MusicConductor");
                    instance = go.AddComponent<MusicConductor>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Audio Mixer")]
    [Tooltip("Main audio mixer asset shared with AudioService.")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Scene Soundtracks")]
    [Tooltip("Central scene-to-soundtrack policy catalog.")]
    [SerializeField] private SceneSoundtrackCatalogSO sceneSoundtrackCatalog;

    [Header("Scheduling")]
    [Tooltip("Small DSP lead-in used when scheduling new soundtrack sources.")]
    [Range(0.01f, 0.25f)]
    [SerializeField] private float schedulingLeadSeconds = 0.05f;

    [Tooltip("Fade used when an unmapped scene stops music.")]
    [Range(0f, 5f)]
    [SerializeField] private float defaultStopFadeSeconds = 0.5f;

    [Tooltip("If true, calls LoadAudioData() on the next requested cue before scheduling it.")]
    [SerializeField] private bool prewarmIncomingCue = true;

    [Header("Combat Ducking")]
    [Tooltip("Temporarily lower scene music when combat SFX plays through CombatAudioBridge.")]
    [SerializeField] private bool enableCombatSfxDucking = true;

    [Tooltip("Music volume multiplier while ducked. 0.75 is roughly a subtle -2.5 dB dip.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float combatDuckVolumeMultiplier = 0.75f;

    [Tooltip("Seconds to fade music down after a combat SFX starts.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float combatDuckAttackSeconds = 0.035f;

    [Tooltip("Minimum seconds to keep music ducked after each combat SFX request.")]
    [Range(0f, 2f)]
    [SerializeField] private float combatDuckHoldSeconds = 0.12f;

    [Tooltip("Seconds to fade music back up after combat SFX ducking ends.")]
    [Range(0f, 2f)]
    [SerializeField] private float combatDuckReleaseSeconds = 0.28f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugUI = false;

    private readonly MusicDeck[] decks = new MusicDeck[2];
    private AudioMixerGroup musicGroup;
    private readonly AudioLowPassFilter[] deckLowPassFilters = new AudioLowPassFilter[4];

    private int activeDeckIndex = -1;
    private int incomingDeckIndex = -1;

    private SoundtrackCueSO currentCue;
    private SoundtrackCueSO pendingCue;
    private double songDspStartTime;
    private Coroutine deckTransitionCoroutine;

    private string boundOverrideSceneName;
    private SoundtrackCueSO boundOverrideCue;

    private bool startupSceneSyncIssued;
    private Coroutine combatDuckCoroutine;
    private float combatDuckMultiplier = 1f;
    private float combatDuckHoldUntil;
    private readonly Dictionary<int, ScriptedDuckRequest> scriptedDuckRequests = new Dictionary<int, ScriptedDuckRequest>();
    private int nextScriptedDuckTokenId = 1;
    private Coroutine scriptedDuckTransitionCoroutine;
    private float scriptedDuckMultiplier = 1f;
    private readonly Dictionary<int, EnvironmentRequest> environmentRequests = new Dictionary<int, EnvironmentRequest>();
    private int nextEnvironmentTokenId = 1;
    private long nextEnvironmentSequence = 1;
    private Coroutine environmentTransitionCoroutine;
    private float environmentCutoffHz = 22000f;
    private float environmentGainDb;
    private float environmentGainLinear = 1f;

    public float CurrentEnvironmentCutoffHz => environmentCutoffHz;
    public float CurrentEnvironmentGainDb => environmentGainDb;
    public string CurrentEnvironmentProfileId
    {
        get
        {
            MusicEnvironmentProfileSO profile = ResolveEnvironmentProfile();
            return profile != null ? profile.ProfileId : string.Empty;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[MusicConductor] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeSources();
        InitializeMixer();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (!startupSceneSyncIssued)
        {
            startupSceneSyncIssued = true;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
                RequestSceneSoundtrack(activeScene.name, SoundtrackTriggerMoment.ImmediateOnSceneLoadedFallback);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (scriptedDuckTransitionCoroutine != null)
            StopCoroutine(scriptedDuckTransitionCoroutine);
        if (environmentTransitionCoroutine != null)
            StopCoroutine(environmentTransitionCoroutine);
        scriptedDuckRequests.Clear();
        environmentRequests.Clear();
        if (instance == this)
            instance = null;
    }

    public void PlayCue(SoundtrackCueSO cue, float fadeSeconds = 0f, bool forceRestart = false)
    {
        if (cue == null)
        {
            Stop(fadeSeconds);
            return;
        }

        if (!ValidateCue(cue))
            return;

        if (!forceRestart && (currentCue == cue || pendingCue == cue))
        {
            if (enableDebugLogs)
                Debug.Log($"[MusicConductor] Cue '{cue.Id}' already active or pending; ignoring duplicate request.");
            return;
        }

        BeginCueTransition(cue, Mathf.Max(0f, fadeSeconds));
    }

    public void RequestSceneSoundtrack(
        string sceneName,
        SoundtrackTriggerMoment moment,
        SoundtrackCueSO overrideCue = null,
        bool forceRestart = false)
    {
        if (string.IsNullOrWhiteSpace(sceneName) && overrideCue == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[MusicConductor] Scene soundtrack request ignored because sceneName and overrideCue were empty.");
            return;
        }

        if (overrideCue == null &&
            !string.IsNullOrWhiteSpace(boundOverrideSceneName) &&
            !string.Equals(sceneName, boundOverrideSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            boundOverrideSceneName = null;
            boundOverrideCue = null;
        }

        if (overrideCue != null)
        {
            boundOverrideSceneName = sceneName;
            boundOverrideCue = overrideCue;
        }

        SoundtrackCueSO resolvedOverrideCue = overrideCue;
        if (resolvedOverrideCue == null &&
            !string.IsNullOrWhiteSpace(sceneName) &&
            !string.IsNullOrWhiteSpace(boundOverrideSceneName) &&
            string.Equals(sceneName, boundOverrideSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            resolvedOverrideCue = boundOverrideCue;
        }

        if (resolvedOverrideCue != null)
        {
            if (!ValidateCue(resolvedOverrideCue))
                return;

            PlayCue(resolvedOverrideCue, resolvedOverrideCue.DefaultTransitionSeconds, forceRestart);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[MusicConductor] Scene '{sceneName}' used explicit soundtrack override '{resolvedOverrideCue.Id}' at {moment}.");
            }
            return;
        }

        SceneSoundtrackCatalogSO.SceneSoundtrackEntry entry = null;
        bool hasCatalogEntry = sceneSoundtrackCatalog != null &&
                               sceneSoundtrackCatalog.TryGetEntry(sceneName, out entry);

        if (hasCatalogEntry)
        {
            if (entry.triggerMoment != moment && !ShouldUseDirectLoadFallback(moment))
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[MusicConductor] Scene '{sceneName}' ignored at {moment}; entry is configured for {entry.triggerMoment}.");
                }
                return;
            }

            if (entry.triggerMoment != moment && enableDebugLogs)
            {
                Debug.Log(
                    $"[MusicConductor] Scene '{sceneName}' direct-load fallback using cue configured for {entry.triggerMoment}.");
            }

            if (entry.cue == null)
            {
                Stop(entry.fadeSeconds);

                if (enableDebugLogs)
                    Debug.Log($"[MusicConductor] Scene '{sceneName}' explicitly stops music at {moment}.");
                return;
            }

            if (!ValidateCue(entry.cue))
                return;

            PlayCue(entry.cue, entry.fadeSeconds, forceRestart || entry.forceRestart);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[MusicConductor] Scene '{sceneName}' resolved cue '{entry.cue.Id}' at {moment} " +
                    $"(fade={entry.fadeSeconds:F2}, forceRestart={forceRestart || entry.forceRestart}).");
            }
            return;
        }

        float stopFade = sceneSoundtrackCatalog != null
            ? sceneSoundtrackCatalog.UnmappedStopFadeSeconds
            : defaultStopFadeSeconds;

        Stop(stopFade);

        if (enableDebugLogs)
            Debug.Log($"[MusicConductor] Scene '{sceneName}' is unmapped; stopping music (fade={stopFade:F2}).");
    }

    public void RequestActiveOverworldChunkSoundtrack(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (sceneSoundtrackCatalog == null ||
            !sceneSoundtrackCatalog.TryGetEntry(sceneName, out SceneSoundtrackCatalogSO.SceneSoundtrackEntry entry))
        {
            if (enableDebugLogs)
                Debug.Log($"[MusicConductor] Active overworld chunk '{sceneName}' has no soundtrack mapping; keeping the current cue.");
            return;
        }

        if (entry.cue == null)
        {
            Stop(entry.fadeSeconds);
            return;
        }

        PlayCue(entry.cue, entry.fadeSeconds, entry.forceRestart);
        if (enableDebugLogs)
        {
            Debug.Log(
                $"[MusicConductor] Active overworld chunk '{sceneName}' requested '{entry.cue.Id}' " +
                $"(fade={entry.fadeSeconds:F2}, forceRestart={entry.forceRestart}).");
        }
    }

    public void Stop(float fadeSeconds = 0f)
    {
        CancelDeckTransition(keepDominantDeckOnly: false);

        if (activeDeckIndex < 0 && incomingDeckIndex < 0)
        {
            currentCue = null;
            pendingCue = null;
            songDspStartTime = 0d;
            return;
        }

        int dominantDeck = GetDominantDeckIndex();
        if (dominantDeck < 0)
        {
            StopAllDecksImmediate();
            currentCue = null;
            pendingCue = null;
            songDspStartTime = 0d;
            return;
        }

        activeDeckIndex = dominantDeck;
        incomingDeckIndex = -1;
        pendingCue = null;

        if (fadeSeconds <= 0f)
        {
            StopAllDecksImmediate();
            currentCue = null;
            songDspStartTime = 0d;
            return;
        }

        deckTransitionCoroutine = StartCoroutine(FadeOutAndStopCoroutine(dominantDeck, fadeSeconds));
    }

    public string GetCurrentCueId()
    {
        if (pendingCue != null)
            return pendingCue.Id;

        return currentCue != null ? currentCue.Id : string.Empty;
    }

    public double GetSongDspStartTime()
    {
        return songDspStartTime;
    }

    public void RequestCombatSfxDuck()
    {
        if (!enableCombatSfxDucking)
            return;

        if (activeDeckIndex < 0 && incomingDeckIndex < 0)
            return;

        combatDuckHoldUntil = Mathf.Max(combatDuckHoldUntil, Time.unscaledTime + Mathf.Max(0f, combatDuckHoldSeconds));

        if (combatDuckCoroutine != null)
            StopCoroutine(combatDuckCoroutine);

        combatDuckCoroutine = StartCoroutine(CombatDuckCoroutine());
    }

    public MusicDuckToken AcquireDuckToken(
        string reason,
        float targetMultiplier = 0.05f,
        float attackSeconds = 0.08f,
        float holdSeconds = 0f,
        float releaseSeconds = 0.5f)
    {
        int tokenId = nextScriptedDuckTokenId++;
        if (nextScriptedDuckTokenId <= 0)
            nextScriptedDuckTokenId = 1;

        scriptedDuckRequests[tokenId] = new ScriptedDuckRequest
        {
            targetMultiplier = Mathf.Clamp01(targetMultiplier),
            holdUntil = Time.unscaledTime + Mathf.Max(0f, holdSeconds),
            releaseSeconds = Mathf.Max(0f, releaseSeconds),
        };

        TransitionScriptedDuck(ResolveScriptedDuckTarget(), Mathf.Max(0f, attackSeconds));
        if (enableDebugLogs)
            Debug.Log($"[MusicConductor] Acquired music duck '{reason}' (token={tokenId}, target={targetMultiplier:F2}).");
        return new MusicDuckToken(this, tokenId);
    }

    public MusicEnvironmentToken AcquireEnvironment(MusicEnvironmentProfileSO profile, string reason = "")
    {
        if (profile == null)
            return default;

        int tokenId = nextEnvironmentTokenId++;
        if (nextEnvironmentTokenId <= 0)
            nextEnvironmentTokenId = 1;

        environmentRequests[tokenId] = new EnvironmentRequest
        {
            profile = profile,
            sequence = nextEnvironmentSequence++,
        };

        TransitionEnvironment(ResolveEnvironmentProfile(), profile.EnterFadeSeconds);
        if (enableDebugLogs)
            Debug.Log($"[MusicConductor] Acquired music environment '{profile.ProfileId}' for '{reason}' (token={tokenId}).");
        return new MusicEnvironmentToken(this, tokenId);
    }

    internal void ReleaseEnvironmentToken(int tokenId)
    {
        if (!environmentRequests.TryGetValue(tokenId, out EnvironmentRequest released))
            return;

        environmentRequests.Remove(tokenId);
        float duration = released.profile != null ? released.profile.ExitFadeSeconds : 0f;
        TransitionEnvironment(ResolveEnvironmentProfile(), duration);
    }

    internal void ReleaseDuckToken(int tokenId)
    {
        if (!scriptedDuckRequests.TryGetValue(tokenId, out ScriptedDuckRequest request))
            return;

        float remainingHold = request.holdUntil - Time.unscaledTime;
        if (remainingHold > 0f)
        {
            StartCoroutine(ReleaseDuckAfterHold(tokenId, remainingHold));
            return;
        }

        RemoveDuckRequest(tokenId, request.releaseSeconds);
    }

    // Legacy no-op hook kept temporarily so old token handles fail safe if any survive in content.
    public MusicLayerToken AcquireLayerToken(string reason, float fadeSeconds = 0f)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning(
                $"[MusicConductor] AcquireLayerToken('{reason}') ignored. Layer-token music is legacy; " +
                "scene soundtracks now use intro->loop cues.");
        }

        return default;
    }

    // Legacy no-op hook kept temporarily so old token handles fail safe if any survive in content.
    public void ReleaseLayerToken(MusicLayerToken token, float fadeSeconds = 0f)
    {
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
            return;

        bool hasCatalogEntry = sceneSoundtrackCatalog != null &&
                               sceneSoundtrackCatalog.TryGetEntry(scene.name, out _);
        if (!ShouldApplySceneLoadedSoundtrack(mode, hasCatalogEntry))
            return;

        RequestSceneSoundtrack(scene.name, SoundtrackTriggerMoment.ImmediateOnSceneLoadedFallback);
    }

    /// <summary>
    /// Additive loads only establish residency. Soundtrack ownership changes
    /// when OverworldStreamingService makes a loaded chunk active.
    /// </summary>
    public static bool ShouldApplySceneLoadedSoundtrack(LoadSceneMode mode, bool sceneHasCatalogEntry)
    {
        return mode != LoadSceneMode.Additive;
    }

    /// <summary>
    /// Allows editor/direct scene boot to play mapped soundtracks when SceneLoader did not
    /// publish TransitionStart/BeforeReveal. Suppressed while SceneLoader owns the transition.
    /// </summary>
    private static bool ShouldUseDirectLoadFallback(SoundtrackTriggerMoment moment)
    {
        if (moment != SoundtrackTriggerMoment.ImmediateOnSceneLoadedFallback)
            return false;

        SceneLoader loader = SceneLoader.Instance;
        return loader == null || !loader.IsTransitioning;
    }

    private void InitializeSources()
    {
        for (int i = 0; i < decks.Length; i++)
        {
            decks[i] = new MusicDeck();

            GameObject deckRoot = new GameObject($"SoundtrackDeck_{i}");
            deckRoot.transform.SetParent(transform, false);

            GameObject introGo = new GameObject("Intro");
            introGo.transform.SetParent(deckRoot.transform, false);
            decks[i].introSource = introGo.AddComponent<AudioSource>();
            ConfigureDeckSource(decks[i].introSource, loop: false);

            GameObject loopGo = new GameObject("Loop");
            loopGo.transform.SetParent(deckRoot.transform, false);
            decks[i].loopSource = loopGo.AddComponent<AudioSource>();
            ConfigureDeckSource(decks[i].loopSource, loop: true);

            deckLowPassFilters[i * 2] = introGo.AddComponent<AudioLowPassFilter>();
            deckLowPassFilters[i * 2 + 1] = loopGo.AddComponent<AudioLowPassFilter>();

            SetDeckGain(i, 0f);
        }
    }

    private void ConfigureDeckSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void InitializeMixer()
    {
        if (audioMixer == null)
        {
            Debug.LogError("[MusicConductor] AudioMixer not assigned. Soundtracks will be unavailable.");
            return;
        }

        AudioMixerGroup[] allGroups = audioMixer.FindMatchingGroups(string.Empty);
        musicGroup = FindGroup(allGroups, "Music_Base") ?? FindGroup(allGroups, "Music");
        if (musicGroup == null)
        {
            Debug.LogError("[MusicConductor] Mixer group 'Music' was not found. Soundtracks will be unavailable.");
            return;
        }

        for (int i = 0; i < decks.Length; i++)
        {
            decks[i].introSource.outputAudioMixerGroup = musicGroup;
            decks[i].loopSource.outputAudioMixerGroup = musicGroup;
        }


        ApplyEnvironmentProcessing();
    }

    private AudioMixerGroup FindGroup(AudioMixerGroup[] groups, string exactName)
    {
        foreach (AudioMixerGroup group in groups)
        {
            if (group == null)
                continue;

            if (group.name == exactName)
                return group;
        }

        return null;
    }

    private void BeginCueTransition(SoundtrackCueSO cue, float fadeSeconds)
    {
        CancelDeckTransition(keepDominantDeckOnly: true);

        int outgoingDeck = activeDeckIndex;
        int incomingDeck = outgoingDeck == 0 ? 1 : 0;
        if (outgoingDeck < 0)
            incomingDeck = 0;

        PrepareDeck(incomingDeck, cue);

        pendingCue = cue;
        incomingDeckIndex = incomingDeck;

        if (outgoingDeck < 0 || fadeSeconds <= 0f)
        {
            if (outgoingDeck >= 0)
                StopDeck(outgoingDeck);

            SetDeckGain(incomingDeck, 1f);
            activeDeckIndex = incomingDeck;
            incomingDeckIndex = -1;
            currentCue = cue;
            pendingCue = null;
            return;
        }

        deckTransitionCoroutine = StartCoroutine(CrossfadeDecksCoroutine(outgoingDeck, incomingDeck, cue, fadeSeconds));
    }

    private void PrepareDeck(int deckIndex, SoundtrackCueSO cue)
    {
        MusicDeck deck = decks[deckIndex];
        StopDeck(deckIndex);

        if (prewarmIncomingCue)
            PrewarmCue(cue);

        deck.cue = cue;
        deck.cueVolumeLinear = cue.VolumeLinear;
        SetDeckGain(deckIndex, 0f);

        AudioClip introClip = cue.IntroClip;
        AudioClip loopClip = cue.LoopClip;
        double startDspTime = AudioSettings.dspTime + schedulingLeadSeconds;

        deck.introSource.clip = introClip;
        deck.introSource.loop = false;
        deck.loopSource.clip = loopClip;
        deck.loopSource.loop = true;

        if (introClip != null)
        {
            double introDuration = GetClipDurationSeconds(introClip);
            deck.introSource.PlayScheduled(startDspTime);
            deck.loopSource.PlayScheduled(startDspTime + introDuration);
        }
        else
        {
            deck.loopSource.PlayScheduled(startDspTime);
        }

        songDspStartTime = startDspTime;
    }

    private IEnumerator CrossfadeDecksCoroutine(int outgoingDeck, int incomingDeck, SoundtrackCueSO cue, float fadeSeconds)
    {
        float outgoingStartGain = decks[outgoingDeck].gain;
        float incomingStartGain = decks[incomingDeck].gain;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSeconds);

            Vector2 equalPower = EvaluateEqualPowerGains(t);
            SetDeckGain(outgoingDeck, outgoingStartGain * equalPower.x);
            SetDeckGain(incomingDeck, Mathf.Lerp(incomingStartGain, 1f, equalPower.y));
            yield return null;
        }

        SetDeckGain(outgoingDeck, 0f);
        StopDeck(outgoingDeck);
        SetDeckGain(incomingDeck, 1f);

        activeDeckIndex = incomingDeck;
        incomingDeckIndex = -1;
        currentCue = cue;
        pendingCue = null;
        deckTransitionCoroutine = null;
    }

    private static Vector2 EvaluateEqualPowerGains(float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        float angle = t * Mathf.PI * 0.5f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private IEnumerator FadeOutAndStopCoroutine(int deckIndex, float fadeSeconds)
    {
        float startGain = decks[deckIndex].gain;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeSeconds);
            SetDeckGain(deckIndex, Mathf.Lerp(startGain, 0f, t));
            yield return null;
        }

        StopDeck(deckIndex);
        activeDeckIndex = -1;
        currentCue = null;
        pendingCue = null;
        songDspStartTime = 0d;
        deckTransitionCoroutine = null;
    }

    private void CancelDeckTransition(bool keepDominantDeckOnly)
    {
        if (deckTransitionCoroutine != null)
        {
            StopCoroutine(deckTransitionCoroutine);
            deckTransitionCoroutine = null;
        }

        if (!keepDominantDeckOnly)
            return;

        int dominantDeck = GetDominantDeckIndex();
        for (int i = 0; i < decks.Length; i++)
        {
            if (i == dominantDeck)
                continue;

            StopDeck(i);
        }

        activeDeckIndex = dominantDeck;
        incomingDeckIndex = -1;
        pendingCue = null;
        currentCue = dominantDeck >= 0 ? decks[dominantDeck].cue : currentCue;
    }

    private int GetDominantDeckIndex()
    {
        int bestIndex = -1;
        float bestGain = 0f;

        for (int i = 0; i < decks.Length; i++)
        {
            if (decks[i].cue == null)
                continue;

            if (decks[i].gain > bestGain)
            {
                bestGain = decks[i].gain;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
            return bestIndex;

        if (activeDeckIndex >= 0 && decks[activeDeckIndex].cue != null)
            return activeDeckIndex;

        if (incomingDeckIndex >= 0 && decks[incomingDeckIndex].cue != null)
            return incomingDeckIndex;

        return -1;
    }

    private void SetDeckGain(int deckIndex, float gain)
    {
        if (deckIndex < 0 || deckIndex >= decks.Length)
            return;

        MusicDeck deck = decks[deckIndex];
        deck.gain = Mathf.Clamp01(gain);
        float volume = deck.gain * deck.cueVolumeLinear * combatDuckMultiplier * scriptedDuckMultiplier * environmentGainLinear;

        if (deck.introSource != null)
            deck.introSource.volume = volume;
        if (deck.loopSource != null)
            deck.loopSource.volume = volume;
    }

    private IEnumerator CombatDuckCoroutine()
    {
        float targetMultiplier = Mathf.Clamp(combatDuckVolumeMultiplier, 0.1f, 1f);
        yield return FadeCombatDuckMultiplier(combatDuckMultiplier, targetMultiplier, combatDuckAttackSeconds);

        while (Time.unscaledTime < combatDuckHoldUntil)
            yield return null;

        yield return FadeCombatDuckMultiplier(combatDuckMultiplier, 1f, combatDuckReleaseSeconds);
        combatDuckMultiplier = 1f;
        ApplyCurrentDeckGains();
        combatDuckCoroutine = null;
    }

    private IEnumerator FadeCombatDuckMultiplier(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            combatDuckMultiplier = to;
            ApplyCurrentDeckGains();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            combatDuckMultiplier = Mathf.Lerp(from, to, t);
            ApplyCurrentDeckGains();
            yield return null;
        }

        combatDuckMultiplier = to;
        ApplyCurrentDeckGains();
    }

    private IEnumerator ReleaseDuckAfterHold(int tokenId, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (!scriptedDuckRequests.TryGetValue(tokenId, out ScriptedDuckRequest request))
            yield break;

        RemoveDuckRequest(tokenId, request.releaseSeconds);
    }

    private void RemoveDuckRequest(int tokenId, float releaseSeconds)
    {
        if (!scriptedDuckRequests.Remove(tokenId))
            return;

        TransitionScriptedDuck(ResolveScriptedDuckTarget(), Mathf.Max(0f, releaseSeconds));
    }

    private float ResolveScriptedDuckTarget()
    {
        float target = 1f;
        foreach (ScriptedDuckRequest request in scriptedDuckRequests.Values)
            target = Mathf.Min(target, request.targetMultiplier);
        return target;
    }

    private void TransitionScriptedDuck(float targetMultiplier, float duration)
    {
        if (scriptedDuckTransitionCoroutine != null)
            StopCoroutine(scriptedDuckTransitionCoroutine);

        scriptedDuckTransitionCoroutine = StartCoroutine(
            FadeScriptedDuckMultiplier(scriptedDuckMultiplier, Mathf.Clamp01(targetMultiplier), duration));
    }

    private IEnumerator FadeScriptedDuckMultiplier(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            scriptedDuckMultiplier = to;
            ApplyCurrentDeckGains();
            scriptedDuckTransitionCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            scriptedDuckMultiplier = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            ApplyCurrentDeckGains();
            yield return null;
        }

        scriptedDuckMultiplier = to;
        ApplyCurrentDeckGains();
        scriptedDuckTransitionCoroutine = null;
    }

    private void ApplyCurrentDeckGains()
    {
        for (int i = 0; i < decks.Length; i++)
        {
            if (decks[i] != null)
                SetDeckGain(i, decks[i].gain);
        }
    }

    private MusicEnvironmentProfileSO ResolveEnvironmentProfile()
    {
        MusicEnvironmentProfileSO winner = null;
        long winningSequence = long.MinValue;
        foreach (EnvironmentRequest request in environmentRequests.Values)
        {
            if (request == null || request.profile == null)
                continue;
            if (winner == null || request.profile.Priority > winner.Priority ||
                (request.profile.Priority == winner.Priority && request.sequence > winningSequence))
            {
                winner = request.profile;
                winningSequence = request.sequence;
            }
        }

        return winner;
    }

    private void TransitionEnvironment(MusicEnvironmentProfileSO profile, float duration)
    {
        float targetCutoff = profile != null ? profile.LowPassCutoffHz : 22000f;
        float targetGainDb = profile != null ? profile.GainDb : 0f;

        if (environmentTransitionCoroutine != null)
            StopCoroutine(environmentTransitionCoroutine);
        environmentTransitionCoroutine = StartCoroutine(
            FadeEnvironmentCoroutine(environmentCutoffHz, environmentGainDb, targetCutoff, targetGainDb, Mathf.Max(0f, duration)));
    }

    private IEnumerator FadeEnvironmentCoroutine(float fromCutoff, float fromGainDb, float toCutoff, float toGainDb, float seconds)
    {
        if (seconds <= 0f)
        {
            environmentCutoffHz = toCutoff;
            environmentGainDb = toGainDb;
            ApplyEnvironmentProcessing();
            environmentTransitionCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        float fromLog = Mathf.Log(Mathf.Max(10f, fromCutoff));
        float toLog = Mathf.Log(Mathf.Max(10f, toCutoff));
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            t = t * t * (3f - 2f * t);
            environmentCutoffHz = Mathf.Exp(Mathf.Lerp(fromLog, toLog, t));
            environmentGainDb = Mathf.Lerp(fromGainDb, toGainDb, t);
            ApplyEnvironmentProcessing();
            yield return null;
        }

        environmentCutoffHz = toCutoff;
        environmentGainDb = toGainDb;
        ApplyEnvironmentProcessing();
        environmentTransitionCoroutine = null;
    }

    private void ApplyEnvironmentProcessing()
    {
        environmentCutoffHz = Mathf.Clamp(environmentCutoffHz, 10f, 22000f);
        environmentGainLinear = Mathf.Pow(10f, environmentGainDb / 20f);
        for (int i = 0; i < deckLowPassFilters.Length; i++)
        {
            AudioLowPassFilter filter = deckLowPassFilters[i];
            if (filter != null)
                filter.cutoffFrequency = environmentCutoffHz;
        }
        ApplyCurrentDeckGains();
    }

    private void StopDeck(int deckIndex)
    {
        if (deckIndex < 0 || deckIndex >= decks.Length)
            return;

        MusicDeck deck = decks[deckIndex];
        if (deck.introSource != null)
        {
            deck.introSource.Stop();
            deck.introSource.clip = null;
        }

        if (deck.loopSource != null)
        {
            deck.loopSource.Stop();
            deck.loopSource.clip = null;
        }

        deck.cue = null;
        deck.cueVolumeLinear = 1f;
        deck.gain = 0f;
    }

    private void StopAllDecksImmediate()
    {
        for (int i = 0; i < decks.Length; i++)
            StopDeck(i);

        activeDeckIndex = -1;
        incomingDeckIndex = -1;
    }

    private void PrewarmCue(SoundtrackCueSO cue)
    {
        if (cue == null)
            return;

        if (cue.IntroClip != null)
            cue.IntroClip.LoadAudioData();

        if (cue.LoopClip != null)
            cue.LoopClip.LoadAudioData();
    }

    private bool ValidateCue(SoundtrackCueSO cue)
    {
        if (cue == null)
            return false;

        if (!cue.IsValid)
        {
            Debug.LogWarning($"[MusicConductor] Soundtrack cue '{cue.name}' is missing its loop clip.");
            return false;
        }

        if (musicGroup == null)
        {
            Debug.LogWarning("[MusicConductor] Music mixer group is unavailable. Cannot play soundtrack cue.");
            return false;
        }

        return true;
    }

    private double GetClipDurationSeconds(AudioClip clip)
    {
        if (clip == null || clip.frequency <= 0)
            return 0d;

        return (double)clip.samples / clip.frequency;
    }

    private void OnGUI()
    {
        if (!showDebugUI)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 220));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Music Conductor");
        GUILayout.Label($"Current Cue: {currentCue?.Id ?? "<none>"}");
        GUILayout.Label($"Pending Cue: {pendingCue?.Id ?? "<none>"}");
        GUILayout.Label($"DSP Start Time: {songDspStartTime:F3}");
        GUILayout.Label($"Bound Override Scene: {boundOverrideSceneName ?? "<none>"}");
        GUILayout.Label($"Bound Override Cue: {boundOverrideCue?.Id ?? "<none>"}");

        for (int i = 0; i < decks.Length; i++)
        {
            GUILayout.Label(
                $"Deck {i}: cue={decks[i].cue?.Id ?? "<none>"}, gain={decks[i].gain:F2}, " +
                $"introPlaying={(decks[i].introSource != null && decks[i].introSource.isPlaying)}, " +
                $"loopPlaying={(decks[i].loopSource != null && decks[i].loopSource.isPlaying)}");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
