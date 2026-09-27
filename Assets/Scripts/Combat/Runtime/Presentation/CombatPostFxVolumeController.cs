using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chainbound.Combat
{
    /// <summary>
    /// Owns a high-priority combat-only URP Volume and weight-blends temporary post-FX cues.
    /// Isolation: never finds, reads, or writes the GameServices / TimeOfDay Global Volume.
    /// Never calls VolumeProfile.Override — only assigns sharedProfile and animates weight.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatPostFxVolumeController : MonoBehaviour
    {
        private const float DefaultCombatPriority = 100f;
        private const string VolumeLayerName = "Volume";

        [Header("Owned Volume")]
        [Tooltip("Combat-only Volume. Auto-created as a child when unset. Must live on the Volume layer so combat cameras sample it. Never assign the scene/GameServices Global Volume.")]
        [SerializeField] private Volume ownedVolume;

        [Tooltip("URP global priority for the owned Volume. Scene baseline volumes typically use 0.")]
        [SerializeField] private float combatPriority = DefaultCombatPriority;

        [Tooltip("Quick fade-out used when a newer cue replaces an active one.")]
        [SerializeField, Min(0f)] private float replaceFadeOutSeconds = 0.05f;

        [SerializeField] private bool enableDebugLogs;

        private Coroutine _playbackRoutine;
        private int _nextPlaybackId;
        private int _activePlaybackId;
        private float _activeFadeOutSeconds = 0.1f;

        public Volume OwnedVolume
        {
            get
            {
                EnsureOwnedVolume();
                return ownedVolume;
            }
        }

        public bool HasActivePlayback => _activePlaybackId > 0;

        public float CurrentWeight => ownedVolume != null ? ownedVolume.weight : 0f;

        private void Awake()
        {
            EnsureOwnedVolume();
            ApplyIdleState();
        }

        private void OnDisable()
        {
            ForceClear();
        }

        private void OnDestroy()
        {
            ForceClear();
        }

        private void OnValidate()
        {
            combatPriority = Mathf.Max(0f, combatPriority);
            replaceFadeOutSeconds = Mathf.Max(0f, replaceFadeOutSeconds);
            if (ownedVolume != null)
                ConfigureOwnedVolume(ownedVolume);
        }

        /// <summary>
        /// Plays a cue on the combat-owned Volume. Latest cue wins — a new play replaces the active one.
        /// </summary>
        public CombatPostFxPlaybackHandle Play(CombatPostFxCueSO cue)
        {
            if (cue == null || !cue.IsPlayable)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CombatPostFxVolumeController] Play skipped — cue or profile is null.", this);
                return default;
            }

            EnsureOwnedVolume();

            int id = ++_nextPlaybackId;
            _activePlaybackId = id;
            _activeFadeOutSeconds = Mathf.Max(0f, cue.fadeOutSeconds);

            if (_playbackRoutine != null)
                StopCoroutine(_playbackRoutine);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[CombatPostFxVolumeController] Play cue='{cue.name}' profile='{cue.profile.name}' " +
                    $"peak={cue.ResolvePeakWeight():0.###} fadeIn={cue.fadeInSeconds:0.###}s " +
                    $"hold={cue.holdSeconds:0.###}s fadeOut={cue.fadeOutSeconds:0.###}s " +
                    $"volumeLayer={LayerMask.LayerToName(ownedVolume.gameObject.layer)} " +
                    $"priority={ownedVolume.priority}",
                    this);
            }

            _playbackRoutine = StartCoroutine(PlayRoutine(id, cue));
            return new CombatPostFxPlaybackHandle(id);
        }

        /// <summary>
        /// Releases a hold-until-release cue (holdSeconds &lt; 0). No-op if the handle is stale.
        /// </summary>
        public void Release(CombatPostFxPlaybackHandle handle)
        {
            if (!handle.IsValid || handle.Id != _activePlaybackId)
                return;

            if (_playbackRoutine != null)
                StopCoroutine(_playbackRoutine);

            _playbackRoutine = StartCoroutine(FadeOutAndIdle(handle.Id, _activeFadeOutSeconds));
        }

        /// <summary>
        /// Immediately stops playback and forces weight to 0. Safe for battle teardown.
        /// </summary>
        public void ForceClear()
        {
            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
                _playbackRoutine = null;
            }

            _activePlaybackId = 0;
            ApplyIdleState();
        }

        private IEnumerator PlayRoutine(int playbackId, CombatPostFxCueSO cue)
        {
            EnsureOwnedVolume();

            float startWeight = ownedVolume.weight;
            if (startWeight > 0.0001f && replaceFadeOutSeconds > 0f)
            {
                yield return FadeWeight(startWeight, 0f, replaceFadeOutSeconds, playbackId);
                if (playbackId != _activePlaybackId)
                    yield break;
            }
            else if (startWeight > 0f)
            {
                ownedVolume.weight = 0f;
            }

            ownedVolume.sharedProfile = cue.profile;
            float peak = cue.ResolvePeakWeight();

            yield return FadeWeight(ownedVolume.weight, peak, Mathf.Max(0f, cue.fadeInSeconds), playbackId);
            if (playbackId != _activePlaybackId)
                yield break;

            if (cue.holdSeconds < 0f)
            {
                // Hold until Release / replace / ForceClear.
                while (playbackId == _activePlaybackId)
                    yield return null;
                yield break;
            }

            if (cue.holdSeconds > 0f)
                yield return new WaitForSeconds(cue.holdSeconds);

            if (playbackId != _activePlaybackId)
                yield break;

            yield return FadeWeight(ownedVolume.weight, 0f, Mathf.Max(0f, cue.fadeOutSeconds), playbackId);
            if (playbackId != _activePlaybackId)
                yield break;

            ApplyIdleState();
            _activePlaybackId = 0;
            _playbackRoutine = null;
        }

        private IEnumerator FadeOutAndIdle(int playbackId, float fadeOutSeconds)
        {
            EnsureOwnedVolume();
            yield return FadeWeight(ownedVolume.weight, 0f, Mathf.Max(0f, fadeOutSeconds), playbackId);
            if (playbackId != _activePlaybackId)
                yield break;

            ApplyIdleState();
            _activePlaybackId = 0;
            _playbackRoutine = null;
        }

        private IEnumerator FadeWeight(float from, float to, float duration, int playbackId)
        {
            EnsureOwnedVolume();
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);

            if (duration <= 0f)
            {
                ownedVolume.weight = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (playbackId != _activePlaybackId)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ownedVolume.weight = Mathf.Lerp(from, to, t);
                yield return null;
            }

            if (playbackId == _activePlaybackId)
                ownedVolume.weight = to;
        }

        private void ApplyIdleState()
        {
            if (ownedVolume == null)
                return;

            ownedVolume.weight = 0f;
            ownedVolume.sharedProfile = null;
            ConfigureOwnedVolume(ownedVolume);
        }

        private void EnsureOwnedVolume()
        {
            if (ownedVolume != null)
            {
                ConfigureOwnedVolume(ownedVolume);
                return;
            }

            Transform existing = transform.Find("CombatPostFxVolume");
            if (existing == null)
                existing = transform.Find("CombatFXVolume");
            if (existing != null)
                ownedVolume = existing.GetComponent<Volume>();

            if (ownedVolume == null)
            {
                Volume[] childVolumes = GetComponentsInChildren<Volume>(true);
                for (int i = 0; i < childVolumes.Length; i++)
                {
                    if (childVolumes[i] != null)
                    {
                        ownedVolume = childVolumes[i];
                        break;
                    }
                }
            }

            if (ownedVolume == null)
            {
                var go = new GameObject("CombatPostFxVolume");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                ownedVolume = go.AddComponent<Volume>();
            }

            ConfigureOwnedVolume(ownedVolume);
        }

        private void ConfigureOwnedVolume(Volume volume)
        {
            if (volume == null)
                return;

            // Combat cameras use Volume Layer Mask = "Volume" only (not Default).
            int volumeLayer = LayerMask.NameToLayer(VolumeLayerName);
            if (volumeLayer >= 0 && volume.gameObject.layer != volumeLayer)
                volume.gameObject.layer = volumeLayer;

            volume.isGlobal = true;
            volume.priority = combatPriority;
            // Idle contribution is driven by weight; keep component enabled so URP can blend when weight rises.
            if (!volume.enabled)
                volume.enabled = true;
        }
    }
}
