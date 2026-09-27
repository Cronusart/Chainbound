using System.Collections;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Runtime-tunable local Y/Z placement offset for the world-space hardware wallet.
    /// The zero offset intentionally preserves the authored placement until adjusted in play mode.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    public sealed class CombatHardwareWalletOffsetController : MonoBehaviour, ICombatBattlefieldFramingOffsetController
    {
        [SerializeField] private Vector3 localPositionOffset = Vector3.zero;
        [Header("1v1 Selector Tilt Lift")]
        [Tooltip("Temporary local Y lift while the move or swap panel is open in settled 1v1. This is added after the 1v1 framing offset and does not modify the authored Transform.")]
        [SerializeField, Min(0f)] private float singleCombatSelectorTiltLiftY = 2.6f;
        [Tooltip("Seconds used to ease the temporary selector tilt lift in and out.")]
        [SerializeField, Min(0f)] private float selectorTiltLiftBlendSeconds = 0.12f;
        [SerializeField, HideInInspector] private Vector3 authoredLocalPosition;
        [SerializeField, HideInInspector] private bool hasAuthoredLocalPosition;

        private bool _battlefieldFramingActive;
        private Vector3 _lastAppliedOffset;
        private Vector3 _offsetBlendStart;
        private Vector3 _offsetBlendTarget;
        private float _offsetBlendProgress = 1f;
        private bool _offsetBlendActive;
        private Vector3 _animationAuthoredLocalPosition;
        private bool _hasAnimationAuthoredLocalPosition;
        private bool _selectorTiltLiftRequested;
        private float _selectorTiltLiftCurrentY;

        private void OnEnable()
        {
            EnsureAuthoredPosition();
            ResetForRuntimeIfNeeded();
            if (Application.isPlaying)
                ApplyAdditiveOffset();
        }

        private void Awake()
        {
            EnsureAuthoredPosition();
            ResetForRuntimeIfNeeded();
        }

        private void OnDisable()
        {
            // Removing this component or temporarily hiding the wallet must not leave
            // its last framing delta baked into the Transform. Keep the requested
            // framing state so OnEnable can reapply it if the presenter is still in
            // settled 1v1 when the wallet is revealed again.
            if (Application.isPlaying)
                RemoveAppliedOffset();
            else
                _lastAppliedOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            // Selector bobbing, wallet reveal/hide, and tilt/stage tweens write their
            // animation position earlier in the frame. Apply this offset last without
            // replacing that animation-authored position.
            if (Application.isPlaying)
            {
                UpdateSelectorTiltLift();
                ApplyAdditiveOffset();
            }
        }

        private void OnValidate()
        {
            EnsureAuthoredPosition();
            localPositionOffset.x = 0f;
            singleCombatSelectorTiltLiftY = Mathf.Max(0f, singleCombatSelectorTiltLiftY);
            selectorTiltLiftBlendSeconds = Mathf.Max(0f, selectorTiltLiftBlendSeconds);

            // Inspector edits in scene/prefab mode must not write into the wallet's
            // authored Transform. The presenter gates application to settled 1v1 at
            // runtime through the framing transition methods.
            if (Application.isPlaying)
            {
                if (_offsetBlendActive)
                    _offsetBlendTarget = ResolveTargetOffset();
                ApplyAdditiveOffset();
            }
        }

        private void EnsureAuthoredPosition()
        {
            if (hasAuthoredLocalPosition)
                return;

            authoredLocalPosition = transform.localPosition;
            hasAuthoredLocalPosition = true;
        }

        private void ResetForRuntimeIfNeeded()
        {
            if (!Application.isPlaying || transform == null)
                return;

            // Preserve the scene-authored runtime position. Only remove an offset that
            // this controller itself may have applied during an edit-mode preview or a
            // previous runtime frame; never replace it with a stale serialized cache.
            Vector3 authoredPosition = _hasAnimationAuthoredLocalPosition
                ? _animationAuthoredLocalPosition
                : transform.localPosition - _lastAppliedOffset;
            _lastAppliedOffset = Vector3.zero;
            _animationAuthoredLocalPosition = authoredPosition;
            _hasAnimationAuthoredLocalPosition = true;
            transform.localPosition = authoredPosition;
        }

        private void ApplyAdditiveOffset()
        {
            if (this == null)
                return;

            Transform walletTransform = transform;
            if (walletTransform == null)
                return;

            if (Application.isPlaying && !isActiveAndEnabled)
                return;

            if (!_hasAnimationAuthoredLocalPosition)
            {
                _animationAuthoredLocalPosition = walletTransform.localPosition - _lastAppliedOffset;
                _hasAnimationAuthoredLocalPosition = true;
            }

            Vector3 offset = ResolveAppliedOffset();
            offset.x = 0f;
            walletTransform.localPosition = _animationAuthoredLocalPosition + offset;
            _lastAppliedOffset = offset;
        }

        public void SetBattlefieldFramingActive(bool active)
        {
            _battlefieldFramingActive = Application.isPlaying && active;
            if (!_battlefieldFramingActive)
                _selectorTiltLiftCurrentY = 0f;
            _offsetBlendActive = false;
            _offsetBlendProgress = 1f;
            if (Application.isPlaying)
                ApplyAdditiveOffset();
            else
                _lastAppliedOffset = Vector3.zero;
        }

        public IEnumerator BlendBattlefieldFraming(bool active, float blendSeconds)
        {
            Vector3 currentFramingOffset = ResolveFramingOffset();
            _battlefieldFramingActive = Application.isPlaying && active;
            if (!_battlefieldFramingActive)
                _selectorTiltLiftCurrentY = 0f;
            _offsetBlendStart = currentFramingOffset;
            _offsetBlendTarget = ResolveTargetOffset();
            _offsetBlendProgress = 0f;
            _offsetBlendActive = Application.isPlaying && !Mathf.Approximately(blendSeconds, 0f);

            float duration = Mathf.Max(0f, blendSeconds);
            if (!Application.isPlaying || duration <= 0f)
            {
                _offsetBlendActive = false;
                _offsetBlendProgress = 1f;
                if (Application.isPlaying)
                    ApplyAdditiveOffset();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _offsetBlendProgress = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            _offsetBlendProgress = 1f;
            _offsetBlendActive = false;
            if (Application.isPlaying)
                ApplyAdditiveOffset();
        }

        private Vector3 ResolveTargetOffset()
        {
            Vector3 target = Application.isPlaying && _battlefieldFramingActive
                ? localPositionOffset
                : Vector3.zero;
            target.x = 0f;
            return target;
        }

        private Vector3 ResolveAppliedOffset()
        {
            Vector3 framingOffset = ResolveFramingOffset();
            framingOffset.y += ResolveSelectorTiltLiftY();
            return framingOffset;
        }

        private Vector3 ResolveFramingOffset()
        {
            if (!_offsetBlendActive)
                return ResolveTargetOffset();

            float easedProgress = Mathf.SmoothStep(0f, 1f, _offsetBlendProgress);
            return Vector3.LerpUnclamped(_offsetBlendStart, _offsetBlendTarget, easedProgress);
        }

        /// <summary>
        /// Requests the temporary lift that keeps the wallet clear of the floor while
        /// its selector uses the move/swap-panel tilt. The lift is ignored unless
        /// the settled 1v1 framing mode is active.
        /// </summary>
        public void SetSelectorTiltLiftActive(bool active)
        {
            _selectorTiltLiftRequested = Application.isPlaying && active;
            if (!_battlefieldFramingActive)
                _selectorTiltLiftCurrentY = 0f;

            if (Application.isPlaying && isActiveAndEnabled)
                ApplyAdditiveOffset();
        }

        private void UpdateSelectorTiltLift()
        {
            float targetY = _battlefieldFramingActive && _selectorTiltLiftRequested
                ? singleCombatSelectorTiltLiftY
                : 0f;
            float duration = Mathf.Max(0f, selectorTiltLiftBlendSeconds);
            if (duration <= 0f)
            {
                _selectorTiltLiftCurrentY = targetY;
                return;
            }

            float referenceDistance = Mathf.Max(
                0.001f,
                singleCombatSelectorTiltLiftY,
                Mathf.Abs(_selectorTiltLiftCurrentY - targetY));
            float maxDelta = referenceDistance * Time.unscaledDeltaTime / duration;
            _selectorTiltLiftCurrentY = Mathf.MoveTowards(_selectorTiltLiftCurrentY, targetY, maxDelta);
        }

        private float ResolveSelectorTiltLiftY()
        {
            return Application.isPlaying && _battlefieldFramingActive && _selectorTiltLiftRequested
                ? _selectorTiltLiftCurrentY
                : 0f;
        }

        private void RemoveAppliedOffset()
        {
            if (transform == null)
                return;

            if (_hasAnimationAuthoredLocalPosition)
                transform.localPosition = _animationAuthoredLocalPosition;
            else if (_lastAppliedOffset != Vector3.zero)
                transform.localPosition -= _lastAppliedOffset;

            _lastAppliedOffset = Vector3.zero;
        }

        /// <summary>
        /// Receives the position authored by selector/stage animation writers. The
        /// controller then adds the gated framing offset on top in LateUpdate.
        /// </summary>
        public void SetAnimationAuthoredLocalPosition(Vector3 position)
        {
            _animationAuthoredLocalPosition = position;
            _hasAnimationAuthoredLocalPosition = true;

            if (!Application.isPlaying)
            {
                transform.localPosition = position;
                return;
            }

            if (isActiveAndEnabled)
                ApplyAdditiveOffset();
        }

        public Vector3 AnimationAuthoredLocalPosition
        {
            get
            {
                if (!_hasAnimationAuthoredLocalPosition && transform != null)
                {
                    _animationAuthoredLocalPosition = transform.localPosition - _lastAppliedOffset;
                    _hasAnimationAuthoredLocalPosition = true;
                }

                return _animationAuthoredLocalPosition;
            }
        }

        [ContextMenu("Capture Current Position As Authored")]
        private void CaptureCurrentPositionAsAuthored()
        {
            Vector3 currentPositionWithoutAppliedOffset = transform.localPosition - _lastAppliedOffset;
            authoredLocalPosition = currentPositionWithoutAppliedOffset;
            hasAuthoredLocalPosition = true;
            _animationAuthoredLocalPosition = currentPositionWithoutAppliedOffset;
            _hasAnimationAuthoredLocalPosition = true;
            if (Application.isPlaying)
                ApplyAdditiveOffset();
        }

        public Vector3 LocalPositionOffset
        {
            get => localPositionOffset;
            set => localPositionOffset = new Vector3(0f, value.y, value.z);
        }
    }
}
