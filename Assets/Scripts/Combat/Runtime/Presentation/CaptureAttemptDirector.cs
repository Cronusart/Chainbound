using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Staged capture-attempt presentation: orb toss, beam, glitch-out, lock-loop, then exit.
    /// Every tween is killed or completed on timeout so a stuck animation cannot freeze combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaptureAttemptDirector : MonoBehaviour
    {
        public enum Stage
        {
            Idle,
            Rise,
            Hold,
            Toss,
            Hover,
            Beam,
            GlitchOut,
            Challenge,
            Result,
            Exit,
            Complete,
        }

        [SerializeField] private CaptureAttemptOrbAnchor orbAnchor;
        [SerializeField] private GameObject orbGlassFallbackPrefab;
        [SerializeField] private MonoBehaviour audioBridgeBehaviour;
        [SerializeField] private bool autoDiscoverAudioBridge = true;

        private CapturePresentationConfigSO _board;
        private ICombatAudioBridge _audio;
        private Combat2BattlefieldPresenter _battlefield;
        private CaptureChallengeController _challenge;
        private Sequence _activeSequence;
        private Tween _hoverTween;
        private int _generation;
        private string _hiddenInstanceId;
        private bool _targetHidden;
        private AudioLoopToken _lockLoop;
        private CombatMusicDuckToken _musicDuck;
        private Stage _stage = Stage.Idle;

        public Stage CurrentStage => _stage;

        public static CaptureAttemptDirector Resolve(MonoBehaviour host)
        {
            if (host == null)
                return null;

            CaptureAttemptDirector director = host.GetComponent<CaptureAttemptDirector>();
            if (director == null)
                director = host.GetComponentInParent<CaptureAttemptDirector>();
            if (director == null)
                director = FindFirstObjectByType<CaptureAttemptDirector>();
            if (director == null)
                director = host.gameObject.AddComponent<CaptureAttemptDirector>();
            return director;
        }

        public IEnumerator PlayIntro(string targetInstanceId)
        {
            KillActive(restoreTarget: false, resetOrb: false);
            int generation = ++_generation;
            _board = BalanceConfigRuntime.CapturePresentation;
            _audio = CombatAudioBridgeResolver.Resolve(audioBridgeBehaviour, autoDiscoverAudioBridge, out audioBridgeBehaviour);
            _battlefield = FindFirstObjectByType<Combat2BattlefieldPresenter>();
            BindOrb();
            if (orbAnchor == null)
            {
                Debug.LogWarning("[CaptureAttemptDirector] No capture orb found. Skipping intro motion.", this);
                yield break;
            }

            orbAnchor.CacheRestPose();
            orbAnchor.gameObject.SetActive(true);
            Transform root = orbAnchor.MotionRoot;
            Vector3 rest = root.position;
            Vector3 risePos = rest + Vector3.up * _board.riseHeight;
            Vector3 hoverPos = risePos + Vector3.up * _board.tossHoverHeight;
            Vector3 slotPos = hoverPos;
            if (_battlefield != null)
            {
                if (!_battlefield.TryGetBossHoverWorldPosition(_board.tossHoverHeight, out hoverPos))
                    hoverPos = risePos + Vector3.up * _board.tossHoverHeight;
                _battlefield.TryGetSlotAimWorldPosition(targetInstanceId, out slotPos);
            }

            _stage = Stage.Rise;
            yield return PlayMoveScale(
                generation,
                root,
                risePos,
                root.localScale,
                _board.riseSeconds,
                _board.riseEase,
                _board.riseOvershoot);

            _stage = Stage.Hold;
            yield return WaitOrTimeout(generation, _board.postRiseHoldSeconds, _board.postRiseHoldSeconds + _board.stageTimeoutPadding);

            _stage = Stage.Toss;
            PlaySfx(_board.orbTossSound, root.position);
            Vector3 mid = (root.position + hoverPos) * 0.5f + Vector3.up * _board.tossArcHeight;
            yield return PlayPathScale(
                generation,
                root,
                new[] { root.position, mid, hoverPos },
                Vector3.one,
                _board.tossSeconds,
                _board.tossEase);

            _stage = Stage.Hover;
            StartHoverBob(root);
            yield return WaitOrTimeout(generation, _board.preBeamHoverSeconds, _board.preBeamHoverSeconds + _board.stageTimeoutPadding);

            _stage = Stage.Beam;
            PlaySfx(_board.beamSound, root.position);
            orbAnchor.PlayBeamToward(
                slotPos.sqrMagnitude > 0.01f ? slotPos : hoverPos,
                _board.beamColor,
                _board.beamRange,
                _board.beamAngle,
                _board.beamMaterial);
            yield return WaitOrTimeout(generation, _board.beamSeconds, _board.beamSeconds + _board.stageTimeoutPadding);

            _stage = Stage.GlitchOut;
            float hideDuration = 0f;
            if (_battlefield != null)
                hideDuration = _battlefield.PlayCaptureAttemptHide(targetInstanceId);
            if (hideDuration > 0f)
            {
                _hiddenInstanceId = targetInstanceId;
                _targetHidden = true;
            }
            yield return WaitOrTimeout(
                generation,
                hideDuration > 0f ? hideDuration : _board.glitchFallbackSeconds,
                Mathf.Max(_board.glitchFallbackSeconds, hideDuration) + _board.stageTimeoutPadding);

            orbAnchor.StopBeam();
            if (generation != _generation)
                yield break;
            _stage = Stage.Challenge;
        }

        public void BeginChallengeAudio(CaptureChallengeController challenge)
        {
            _challenge = challenge;
            StopLockLoop(0.05f);
            if (_board == null)
                _board = BalanceConfigRuntime.CapturePresentation;
            if (_audio == null)
                _audio = CombatAudioBridgeResolver.Resolve(audioBridgeBehaviour, autoDiscoverAudioBridge, out audioBridgeBehaviour);

            if (_board.lockLoopSound != null)
                _lockLoop = _audio != null ? _audio.StartLoop(_board.lockLoopSound, volumeScale: _board.lockLoopVolume) : default;
            else if (_audio != null)
                _lockLoop = _audio.StartCaptureLockLoop(_board.lockLoopVolume);

            if (_audio != null)
            {
                _musicDuck = _audio.AcquireMusicDuck(
                    "capture-lock-loop",
                    _board.musicDuckMultiplier,
                    _board.musicDuckAttackSeconds,
                    _board.musicDuckReleaseSeconds);
            }

            if (_challenge != null)
            {
                _challenge.MeterPresented -= HandleMeterPresented;
                _challenge.MeterPresented += HandleMeterPresented;
            }
        }

        public void EndChallengeAudio()
        {
            if (_challenge != null)
                _challenge.MeterPresented -= HandleMeterPresented;
            _challenge = null;
            StopLockLoop(0.12f);
            ReleaseDuck();
        }

        public IEnumerator PlayResultAndExit(bool captured, string targetInstanceId, CaptureChallengeController challenge)
        {
            int generation = _generation;
            if (_board == null)
                _board = BalanceConfigRuntime.CapturePresentation;

            _stage = Stage.Result;
            EndChallengeAudio();
            PlaySfx(captured ? _board.successBannerSound : _board.failureBannerSound, Vector3.zero);

            if (challenge != null)
                yield return challenge.PlayClose();

            if (generation != _generation)
                yield break;

            _stage = Stage.Exit;
            KillHover();
            if (orbAnchor != null)
            {
                Transform root = orbAnchor.MotionRoot;
                if (captured)
                {
                    PlaySfx(_board.successVanishSound, root.position);
                    Vector3 up = root.position + Vector3.up * _board.successFloatHeight;
                    yield return PlayMoveScale(
                        generation,
                        root,
                        up,
                        Vector3.zero,
                        _board.successFloatSeconds,
                        Ease.InCubic,
                        0f);
                }
                else
                {
                    PlaySfx(_board.failureVanishSound, root.position);
                    Sequence punch = DOTween.Sequence().SetUpdate(true);
                    punch.Append(root.DOScale(root.localScale * 1.25f, _board.failDisintegrateSeconds * 0.35f).SetEase(Ease.OutBack));
                    punch.Append(root.DOScale(Vector3.zero, _board.failDisintegrateSeconds * 0.65f).SetEase(Ease.InBack));
                    _activeSequence = punch;
                    yield return WaitTweenOrTimeout(generation, punch, _board.failDisintegrateSeconds + _board.stageTimeoutPadding);
                }
            }

            if (!captured && _targetHidden)
            {
                _battlefield?.PlayCaptureAttemptRestore(string.IsNullOrEmpty(targetInstanceId) ? _hiddenInstanceId : targetInstanceId);
                _targetHidden = false;
                _hiddenInstanceId = null;
            }
            else if (captured)
            {
                _targetHidden = false;
                _hiddenInstanceId = null;
            }

            ResetOrbHidden();
            if (generation != _generation)
                yield break;
            _stage = Stage.Complete;
        }

        public void NotifyCaptureRemovalApplied()
        {
            _targetHidden = false;
            _hiddenInstanceId = null;
        }

        public void Abort()
        {
            KillActive(restoreTarget: true, resetOrb: true);
            _stage = Stage.Idle;
        }

        private void OnDisable()
        {
            bool restore = _targetHidden && _stage != Stage.Complete && _stage != Stage.Idle;
            KillActive(restoreTarget: restore, resetOrb: true);
        }

        private void BindOrb()
        {
            if (orbAnchor != null)
                return;

            orbAnchor = FindFirstObjectByType<CaptureAttemptOrbAnchor>(FindObjectsInactive.Include);
            if (orbAnchor != null)
                return;

            GameObject named = GameObject.Find("Orb");
            if (named == null)
                named = GameObject.Find("Orb_Glass");
            if (named == null)
                named = GameObject.Find("Orb_Glass (1)");
            if (named != null)
            {
                orbAnchor = named.GetComponent<CaptureAttemptOrbAnchor>()
                    ?? named.AddComponent<CaptureAttemptOrbAnchor>();
                return;
            }

            if (orbGlassFallbackPrefab == null)
                return;
            Transform parent = _battlefield != null ? _battlefield.transform : transform;
            GameObject instance = Instantiate(orbGlassFallbackPrefab, parent);
            instance.name = "Orb";
            orbAnchor = instance.GetComponent<CaptureAttemptOrbAnchor>()
                ?? instance.AddComponent<CaptureAttemptOrbAnchor>();
        }

        private void HandleMeterPresented(int meterBasisPoints, bool insideBand)
        {
            if (_audio == null || !_lockLoop.IsValid || _board == null)
                return;
            float pitch = CaptureLockMeterPlayback.PitchForMeter(
                meterBasisPoints,
                _board.lockLoopEscapePitch,
                _board.lockLoopLockPitch);
            _audio.SetLoopPitch(_lockLoop, pitch);
        }

        private void PlaySfx(Object sound, Vector3 position)
        {
            _audio?.PlayOneShot(sound, position);
        }

        private void StartHoverBob(Transform root)
        {
            KillHover();
            if (root == null || _board == null)
                return;
            float y = root.position.y;
            _hoverTween = root.DOMoveY(y + _board.hoverBobHeight, _board.hoverBobSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private IEnumerator PlayMoveScale(
            int generation,
            Transform root,
            Vector3 position,
            Vector3 scale,
            float seconds,
            Ease ease,
            float overshoot)
        {
            if (root == null)
                yield break;
            KillTweenSequence();
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            Tween move = overshoot > 1f
                ? root.DOMove(position, seconds).SetEase(ease, overshoot)
                : root.DOMove(position, seconds).SetEase(ease);
            sequence.Join(move);
            sequence.Join(root.DOScale(scale, seconds).SetEase(ease));
            _activeSequence = sequence;
            yield return WaitTweenOrTimeout(generation, sequence, seconds + _board.stageTimeoutPadding);
        }

        private IEnumerator PlayPathScale(
            int generation,
            Transform root,
            Vector3[] path,
            Vector3 scale,
            float seconds,
            Ease ease)
        {
            if (root == null || path == null || path.Length < 2)
                yield break;
            KillTweenSequence();
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(root.DOPath(path, seconds, PathType.CatmullRom).SetEase(ease));
            sequence.Join(root.DOScale(scale, seconds).SetEase(ease));
            _activeSequence = sequence;
            yield return WaitTweenOrTimeout(generation, sequence, seconds + _board.stageTimeoutPadding);
        }

        private IEnumerator WaitTweenOrTimeout(int generation, Tween tween, float timeout)
        {
            float elapsed = 0f;
            while (generation == _generation && tween != null && tween.IsActive() && tween.IsPlaying())
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= timeout)
                {
                    tween.Kill(complete: true);
                    break;
                }
                yield return null;
            }
        }

        private IEnumerator WaitOrTimeout(int generation, float seconds, float timeout)
        {
            float elapsed = 0f;
            float wait = Mathf.Max(0f, seconds);
            float cap = Mathf.Max(wait, timeout);
            while (generation == _generation && elapsed < wait && elapsed < cap)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void KillActive(bool restoreTarget, bool resetOrb)
        {
            _generation++;
            EndChallengeAudio();
            KillTweenSequence();
            KillHover();
            orbAnchor?.StopBeam();
            if (restoreTarget && _targetHidden)
            {
                _battlefield?.PlayCaptureAttemptRestore(_hiddenInstanceId);
                _targetHidden = false;
                _hiddenInstanceId = null;
            }

            if (resetOrb)
                ResetOrbHidden();
            _stage = Stage.Idle;
        }

        private void KillTweenSequence()
        {
            if (_activeSequence == null)
                return;
            _activeSequence.Kill();
            _activeSequence = null;
        }

        private void KillHover()
        {
            if (_hoverTween == null)
                return;
            _hoverTween.Kill();
            _hoverTween = null;
        }

        private void StopLockLoop(float fade)
        {
            if (_audio != null && _lockLoop.IsValid)
                _audio.StopLoop(_lockLoop, fade);
            _lockLoop = default;
        }

        private void ReleaseDuck()
        {
            if (_audio != null && _musicDuck.IsValid)
                _audio.ReleaseMusicDuck(_musicDuck);
            _musicDuck = default;
        }

        private void ResetOrbHidden()
        {
            if (orbAnchor == null)
                return;
            KillHover();
            KillTweenSequence();
            orbAnchor.StopBeam();
            orbAnchor.RestoreRestPose();
            orbAnchor.gameObject.SetActive(false);
        }
    }
}
