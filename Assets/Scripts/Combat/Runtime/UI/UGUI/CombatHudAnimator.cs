using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Active-turn / target pulse for HUD cards.
    /// Resting scale is locked so mid-pulse kills never compound into oversized cards.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatHudAnimator : MonoBehaviour
    {
        [Header("Targets (Overlay Children)")]
        [SerializeField] private RectTransform activePulseTarget;
        [SerializeField] private RectTransform targetPulseTarget;

        [Header("Pulse")]
        [SerializeField] private float activePulseScale = 1.06f;
        [SerializeField] private float targetPulseScale = 1.04f;
        [SerializeField] private float pulseUpDuration = 0.12f;
        [SerializeField] private float pulseDownDuration = 0.14f;
        [SerializeField] private float targetPulseHold = 0.04f;
        [SerializeField] private Ease pulseEaseOut = Ease.OutSine;
        [SerializeField] private Ease pulseEaseIn = Ease.InOutSine;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;

        private Tween _activePulseLoop;
        private Tween _targetPulseTween;
        private Tween _sharedPulseLoop;
        private Vector3 _restingScale = Vector3.one;
        private bool _hasRestingScale;
        private bool _activePulseRequested;
        private bool _targetPulseRequested;

        private void Awake()
        {
            ResolveFallbacks();
            EnsureRestingScale();
            ResetScale();
        }

        private void OnEnable()
        {
            ResolveFallbacks();
            EnsureRestingScale();
            RefreshPulseState();
        }

        private void OnDisable()
        {
            StopAllPulses();
        }

        public void ConfigureTargets(RectTransform activeTarget, RectTransform targetTarget)
        {
            if (activeTarget != null)
                activePulseTarget = activeTarget;
            if (targetTarget != null)
                targetPulseTarget = targetTarget;

            ResolveFallbacks();
            // Do not recapture from live localScale — that re-bases mid-pulse and compounds size.
            EnsureRestingScale();
            RefreshPulseState();
        }

        /// <summary>
        /// Explicit resting scale for the pulse target (defaults to Vector3.one).
        /// </summary>
        public void SetBaseScale(Vector3 scale)
        {
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                scale = Vector3.one;

            _restingScale = scale;
            _hasRestingScale = true;
            ResetScale();
        }

        public void ResetScale()
        {
            Vector3 resting = GetRestingScale();
            if (IsValidPulseTarget(activePulseTarget))
                activePulseTarget.localScale = resting;
            if (IsValidPulseTarget(targetPulseTarget) && targetPulseTarget != activePulseTarget)
                targetPulseTarget.localScale = resting;
        }

        public void PlayActivePulse()
        {
            PlayActivePulseLoop();
        }

        public void PlayActivePulseLoop()
        {
            _activePulseRequested = true;
            RefreshPulseState();
        }

        public void StopActivePulseLoop()
        {
            _activePulseRequested = false;
            RefreshPulseState();
        }

        public void PlayTargetPulse()
        {
            _targetPulseRequested = true;
            RefreshPulseState();
        }

        public void StopTargetPulse()
        {
            _targetPulseRequested = false;
            RefreshPulseState();
        }

        public void StopAllPulses()
        {
            _activePulseRequested = false;
            _targetPulseRequested = false;
            KillPulseTweens();
            ResetScale();
        }

        /// <summary>
        /// Re-apply the current pulse request after an external tween (heal/shake) touched scale.
        /// </summary>
        public void RestorePulseAfterExternalMotion()
        {
            RefreshPulseState();
        }

        public bool IsPulsing => _activePulseRequested || _targetPulseRequested;

        private void ResolveFallbacks()
        {
            if (uiAnimTuning == null)
                uiAnimTuning = GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null)
                uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);

            if (activePulseTarget == null)
                activePulseTarget = transform as RectTransform;
            if (targetPulseTarget == null)
                targetPulseTarget = activePulseTarget != null ? activePulseTarget : transform as RectTransform;

            if (activePulseTarget == null && targetPulseTarget != null)
                activePulseTarget = targetPulseTarget;
            if (targetPulseTarget == null && activePulseTarget != null)
                targetPulseTarget = activePulseTarget;
        }

        private void ApplyTuningOverrides()
        {
            if (uiAnimTuning == null)
                return;

            activePulseScale = Mathf.Max(1f, uiAnimTuning.activePulseScale);
            targetPulseScale = Mathf.Max(1f, uiAnimTuning.targetPulseScale);
            pulseUpDuration = Mathf.Max(0.01f, uiAnimTuning.pulseUpDuration);
            pulseDownDuration = Mathf.Max(0.01f, uiAnimTuning.pulseDownDuration);
            targetPulseHold = Mathf.Max(0f, uiAnimTuning.targetPulseHold);
            pulseEaseOut = uiAnimTuning.pulseEaseOut;
            pulseEaseIn = uiAnimTuning.pulseEaseIn;
            useUnscaledTime = uiAnimTuning.useUnscaledTime;
        }

        private void RefreshPulseState()
        {
            ResolveFallbacks();
            ApplyTuningOverrides();
            EnsureRestingScale();
            KillPulseTweens();
            ResetScale();

            Vector3 resting = GetRestingScale();
            bool sharedTarget = IsValidPulseTarget(activePulseTarget)
                                && activePulseTarget == targetPulseTarget;

            if (sharedTarget)
            {
                if (!_activePulseRequested && !_targetPulseRequested)
                    return;

                float sharedScale = 1f;
                if (_activePulseRequested)
                    sharedScale = Mathf.Max(sharedScale, activePulseScale);
                if (_targetPulseRequested)
                    sharedScale = Mathf.Max(sharedScale, targetPulseScale);

                float sharedHold = _targetPulseRequested ? targetPulseHold : 0f;
                _sharedPulseLoop = CreatePulseLoop(activePulseTarget, resting, sharedScale, sharedHold);
                return;
            }

            if (_activePulseRequested && IsValidPulseTarget(activePulseTarget))
                _activePulseLoop = CreatePulseLoop(activePulseTarget, resting, activePulseScale, 0f);

            if (_targetPulseRequested && IsValidPulseTarget(targetPulseTarget))
                _targetPulseTween = CreatePulseLoop(targetPulseTarget, resting, targetPulseScale, targetPulseHold);
        }

        private void EnsureRestingScale()
        {
            if (_hasRestingScale)
                return;

            // Lock once. Prefer authored scale if already at rest; otherwise Vector3.one.
            RectTransform sample = activePulseTarget != null ? activePulseTarget : targetPulseTarget;
            if (sample != null
                && !_activePulseRequested
                && !_targetPulseRequested
                && sample.localScale.x > 0f
                && sample.localScale.y > 0f
                && sample.localScale.z > 0f)
            {
                _restingScale = sample.localScale;
            }
            else
            {
                _restingScale = Vector3.one;
            }

            _hasRestingScale = true;
        }

        private Vector3 GetRestingScale()
        {
            EnsureRestingScale();
            return _restingScale;
        }

        private Tween CreatePulseLoop(RectTransform target, Vector3 baseScale, float scaleMultiplier, float holdDuration)
        {
            if (!IsValidPulseTarget(target))
                return null;

            float clampedScale = Mathf.Max(1f, scaleMultiplier);
            if (Mathf.Approximately(clampedScale, 1f))
                return null;

            // Absolute peak from locked resting scale — never from current mid-tween scale.
            Vector3 peakScale = baseScale * clampedScale;
            target.localScale = baseScale;

            var sequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .Append(target.DOScale(peakScale, Mathf.Max(0.01f, pulseUpDuration)).SetEase(pulseEaseOut));

            if (holdDuration > 0f)
                sequence.AppendInterval(holdDuration);

            sequence.Append(target.DOScale(baseScale, Mathf.Max(0.01f, pulseDownDuration)).SetEase(pulseEaseIn))
                .SetLoops(-1, LoopType.Restart);

            return sequence;
        }

        private void KillPulseTweens()
        {
            KillTween(ref _sharedPulseLoop);
            KillTween(ref _activePulseLoop);
            KillTween(ref _targetPulseTween);
        }

        private static void KillTween(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
                tween.Kill(complete: false);
            tween = null;
        }

        private static bool IsValidPulseTarget(RectTransform target)
        {
            return target != null;
        }
    }
}
