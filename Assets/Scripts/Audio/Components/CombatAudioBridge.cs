using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatAudioBridge : MonoBehaviour, ICombatAudioBridge
{
    [Header("Music Ducking")]
    [Tooltip("If enabled, successful combat SFX one-shots briefly ask MusicConductor to lower scene music.")]
    [SerializeField] private bool duckMusicForSfxOneShots = true;

    [SerializeField] private bool enableDebugLogs;

    private readonly Dictionary<int, AudioHandle> _activeLoops = new Dictionary<int, AudioHandle>();
    private AudioHandle _postBattleSting;
    private readonly Dictionary<int, MusicDuckToken> _activeDucks = new Dictionary<int, MusicDuckToken>();
    private int _nextLoopId = 1;
    private int _nextDuckId = 1;
    private static AudioClip _lockLoopClip;

    public void PlayOneShot(UnityEngine.Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
    {
        if (TryPlay(soundEvent, position, follow, volumeScale, out _))
            RequestMusicDuckIfCombatSfx(soundEvent);
    }

    public AudioLoopToken StartLoop(UnityEngine.Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
    {
        if (!TryPlay(soundEvent, position, follow, volumeScale, out var handle))
            return default;

        var token = new AudioLoopToken(_nextLoopId++);
        _activeLoops[token.Id] = handle;
        return token;
    }

    public void StopLoop(AudioLoopToken token, float fadeOut = 0f)
    {
        if (!token.IsValid)
            return;

        if (!_activeLoops.TryGetValue(token.Id, out var handle))
            return;

        if (handle.IsValid && AudioService.Instance != null)
            AudioService.Instance.Stop(handle, fadeOut);

        _activeLoops.Remove(token.Id);
    }

    public void SetLoopPitch(AudioLoopToken token, float pitch)
    {
        if (!token.IsValid || !_activeLoops.TryGetValue(token.Id, out var handle) || !handle.IsValid)
            return;
        handle.SetPitch(pitch);
    }

    public void SetLoopVolume(AudioLoopToken token, float volume)
    {
        if (!token.IsValid || !_activeLoops.TryGetValue(token.Id, out var handle) || !handle.IsValid)
            return;
        handle.SetVolume(volume);
    }

    public AudioLoopToken StartCaptureLockLoop(float volumeScale = 1f)
    {
        var service = AudioService.Instance;
        if (service == null)
            return default;

        AudioClip clip = ResolveLockLoopClip();
        if (clip == null)
            return default;

        AudioHandle handle = service.PlayClip(clip, AudioBus.SFX, true, Mathf.Clamp01(volumeScale), 1f);
        if (!handle.IsValid)
            return default;

        var token = new AudioLoopToken(_nextLoopId++);
        _activeLoops[token.Id] = handle;
        return token;
    }

    public CombatMusicDuckToken AcquireMusicDuck(
        string reason,
        float targetMultiplier,
        float attackSeconds,
        float releaseSeconds)
    {
        MusicConductor conductor = FindFirstObjectByType<MusicConductor>();
        if (conductor == null)
            return default;

        MusicDuckToken inner = conductor.AcquireDuckToken(
            string.IsNullOrWhiteSpace(reason) ? "capture-lock-loop" : reason,
            targetMultiplier,
            attackSeconds,
            0f,
            releaseSeconds);
        if (!inner.IsValid)
            return default;

        int id = _nextDuckId++;
        _activeDucks[id] = inner;
        return new CombatMusicDuckToken(id);
    }

        public void ReleaseMusicDuck(CombatMusicDuckToken token)
        {
            if (!token.IsValid)
                return;
            if (!_activeDucks.TryGetValue(token.Id, out MusicDuckToken inner))
                return;
            inner.Release();
            _activeDucks.Remove(token.Id);
        }

        public void StopSceneMusic(float fadeSeconds = 0f)
        {
            MusicConductor conductor = MusicConductor.Instance;
            if (conductor != null)
                conductor.Stop(fadeSeconds);
        }

    public void PlayPostBattleSting(UnityEngine.Object soundEvent, float volumeScale = 1f)
    {
        if (soundEvent == null)
            return;

        FadeOutPostBattleSting(0f);
        if (TryPlay(soundEvent, null, null, volumeScale, out AudioHandle handle))
            _postBattleSting = handle;
    }

    public void FadeOutPostBattleSting(float fadeSeconds)
    {
        if (!_postBattleSting.IsValid)
            return;

        AudioHandle handle = _postBattleSting;
        _postBattleSting = default;
        if (AudioService.Instance != null)
            AudioService.Instance.Stop(handle, Mathf.Max(0f, fadeSeconds));
    }

    private void OnDisable()
    {
        StopAllLoops();
        ReleaseAllDucks();
    }

    private void OnDestroy()
    {
        FadeOutPostBattleSting(CombatSfxOrchestrator.PostBattleStingFadeSeconds);
        StopAllLoops();
        ReleaseAllDucks();
    }

    private bool TryPlay(UnityEngine.Object soundObject, Vector3? position, Transform follow, float volumeScale, out AudioHandle handle)
    {
        handle = default;

        var soundEvent = soundObject as SoundEventSO;
        if (soundEvent == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CombatAudioBridge] Ignored payload '{soundObject}' because it is not a SoundEventSO.", this);
            return false;
        }

        var service = AudioService.Instance;
        if (service == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[CombatAudioBridge] AudioService.Instance is null.", this);
            return false;
        }

        handle = service.Play(soundEvent, position, follow, volumeScale);
        return handle.IsValid;
    }

    private void RequestMusicDuckIfCombatSfx(UnityEngine.Object soundObject)
    {
        if (!duckMusicForSfxOneShots)
            return;

        var soundEvent = soundObject as SoundEventSO;
        if (soundEvent == null || soundEvent.OutputBus != AudioBus.SFX)
            return;

        MusicConductor conductor = FindFirstObjectByType<MusicConductor>();
        if (conductor != null)
            conductor.RequestCombatSfxDuck();
    }

    private void StopAllLoops()
    {
        if (_activeLoops.Count == 0)
            return;

        var loopIds = new List<int>(_activeLoops.Keys);
        for (int i = 0; i < loopIds.Count; i++)
        {
            int loopId = loopIds[i];
            if (_activeLoops.TryGetValue(loopId, out var handle) && handle.IsValid && AudioService.Instance != null)
                AudioService.Instance.Stop(handle);
        }

        _activeLoops.Clear();
    }

    private void ReleaseAllDucks()
    {
        if (_activeDucks.Count == 0)
            return;

        var duckIds = new List<int>(_activeDucks.Keys);
        for (int i = 0; i < duckIds.Count; i++)
        {
            if (_activeDucks.TryGetValue(duckIds[i], out MusicDuckToken inner))
                inner.Release();
        }

        _activeDucks.Clear();
    }

    private static AudioClip ResolveLockLoopClip()
    {
        if (_lockLoopClip != null)
            return _lockLoopClip;

        const int sampleRate = 22050;
        const float duration = 0.25f;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float a = Mathf.Sin(2f * Mathf.PI * 110f * t);
            float b = Mathf.Sin(2f * Mathf.PI * 165f * t);
            float wobble = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 6f * t);
            data[i] = (a * 0.65f + b * 0.35f) * 0.22f * wobble;
        }

        _lockLoopClip = AudioClip.Create("CaptureLockWobble", samples, 1, sampleRate, false);
        _lockLoopClip.SetData(data, 0);
        return _lockLoopClip;
    }
}
