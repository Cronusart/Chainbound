using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatSlotGroundCircle : MonoBehaviour
    {
        public enum CircleState
        {
            Off,
            ActiveActor,
            TargetingAttack,
            TargetingSupport
        }

        [Header("Renderer")]
        [SerializeField] private SpriteRenderer ringRenderer;
        [Tooltip("Optional: scale this transform instead of the ring's. Use a dedicated child to avoid parent hierarchy affecting the pulse.")]
        [SerializeField] private Transform ringScalePivot;

        [Header("Colors")]
        [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color activeActorColor = new Color(0.95f, 0.90f, 0.24f, 0.90f);
        [SerializeField] private Color attackTargetColor = new Color(1f, 0.32f, 0.28f, 0.90f);
        [SerializeField] private Color supportTargetColor = new Color(0.26f, 1f, 0.56f, 0.90f);

        [Header("Pulse")]
        [SerializeField] private float activePulseScale = 1.05f;
        [SerializeField] private float targetPulseScale = 1.10f;
        [SerializeField] private float pulseUpDuration = 0.48f;
        [SerializeField] private float pulseDownDuration = 0.38f;
        [SerializeField] private float pulseHold = 0.06f;
        [SerializeField] private Ease pulseEaseUp = Ease.OutElastic;
        [SerializeField] private Ease pulseEaseDown = Ease.InOutSine;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;

        private Tween _pulseTween;
        private Vector3 _baseScale = Vector3.one;
        private CircleState _state = CircleState.Off;
        private static Sprite _fallbackRingSprite;

        public CircleState State => _state;

        private Transform ScaleTransform => ringScalePivot != null ? ringScalePivot : (ringRenderer != null ? ringRenderer.transform : null);

        private void Awake()
        {
            ResolveFallbacks();
            var scaleTrans = ScaleTransform;
            if (scaleTrans != null)
                _baseScale = scaleTrans.localScale;
            if (ringRenderer != null)
            {
                ringRenderer.color = offColor;
                ringRenderer.enabled = false;
            }
            _state = CircleState.Off;
        }

        private void OnDisable()
        {
            StopPulse(resetScale: true);
            if (ringRenderer != null)
            {
                ringRenderer.color = offColor;
                ringRenderer.enabled = false;
            }
            _state = CircleState.Off;
        }

        public void SetState(CircleState state)
        {
            ResolveFallbacks();
            ApplyTuningOverrides();

            if (ringRenderer == null)
            {
                _state = state;
                return;
            }

            if (_state == state && state != CircleState.Off)
                return;

            _state = state;
            StopPulse(resetScale: true);

            switch (state)
            {
                case CircleState.ActiveActor:
                    ApplyVisual(activeActorColor, true, activePulseScale);
                    break;
                case CircleState.TargetingAttack:
                    ApplyVisual(attackTargetColor, true, targetPulseScale);
                    break;
                case CircleState.TargetingSupport:
                    ApplyVisual(supportTargetColor, true, targetPulseScale);
                    break;
                default:
                    ApplyVisual(offColor, false, 1f);
                    break;
            }
        }

        public void SetIntentFromMove(MoveInstance move)
        {
            if (move == null)
            {
                SetState(CircleState.Off);
                return;
            }

            SetState(IsSupportIntent(move) ? CircleState.TargetingSupport : CircleState.TargetingAttack);
        }

        public static bool IsSupportIntent(MoveInstance move)
        {
            if (move == null)
                return false;

            if (move.category == MoveCategory.Support || move.category == MoveCategory.StatusUtility)
            {
                return move.targeting == MoveTargeting.Self
                    || move.targeting == MoveTargeting.SingleAlly
                    || move.targeting == MoveTargeting.AllAllies;
            }

            return move.targeting == MoveTargeting.Self
                || move.targeting == MoveTargeting.SingleAlly
                || move.targeting == MoveTargeting.AllAllies;
        }

        private void ApplyVisual(Color color, bool pulse, float pulseScale)
        {
            if (ringRenderer == null)
                return;

            ringRenderer.color = color;
            ringRenderer.enabled = color.a > 0.001f;
            if (!ringRenderer.enabled)
                return;

            var scaleTrans = ScaleTransform;
            if (scaleTrans != null)
                scaleTrans.localScale = _baseScale;
            if (!pulse)
                return;

            if (scaleTrans == null)
                return;

            Vector3 peakScale = _baseScale * Mathf.Max(1f, pulseScale);
            float upDur = Mathf.Max(0.01f, pulseUpDuration);
            float downDur = Mathf.Max(0.01f, pulseDownDuration);

            var seq = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .Append(scaleTrans.DOScale(peakScale, upDur).SetEase(pulseEaseUp))
                .AppendInterval(Mathf.Max(0f, pulseHold))
                .Append(scaleTrans.DOScale(_baseScale, downDur).SetEase(pulseEaseDown))
                .SetLoops(-1, LoopType.Restart);

            _pulseTween = seq;
        }

        private void StopPulse(bool resetScale)
        {
            if (_pulseTween != null && _pulseTween.IsActive())
                _pulseTween.Kill();
            _pulseTween = null;

            if (resetScale)
            {
                var scaleTrans = ScaleTransform;
                if (scaleTrans != null)
                    scaleTrans.localScale = _baseScale;
            }
        }

        private void ResolveFallbacks()
        {
            if (ringRenderer == null)
                ringRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (ringScalePivot == null && ringRenderer != null)
                ringScalePivot = ringRenderer.transform;
            if (ringRenderer != null)
            {
                if (ringRenderer.sprite == null)
                    ringRenderer.sprite = GetFallbackRingSprite();
                if (ringRenderer.sortingOrder == 0)
                    ringRenderer.sortingOrder = -5;
            }
            if (uiAnimTuning == null)
                uiAnimTuning = GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null)
                uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);
        }

        private void ApplyTuningOverrides()
        {
            if (uiAnimTuning == null)
                return;

            activePulseScale = Mathf.Max(1f, uiAnimTuning.groundCirclePulseScale);
            targetPulseScale = Mathf.Max(1f, uiAnimTuning.groundCirclePulseScale);
            pulseUpDuration = Mathf.Max(0.01f, uiAnimTuning.groundCirclePulseUpDuration);
            pulseDownDuration = Mathf.Max(0.01f, uiAnimTuning.groundCirclePulseDownDuration);
            pulseHold = Mathf.Max(0f, uiAnimTuning.groundCirclePulseHold);
            pulseEaseUp = uiAnimTuning.groundCirclePulseEaseUp;
            pulseEaseDown = uiAnimTuning.groundCirclePulseEaseDown;
            useUnscaledTime = uiAnimTuning.useUnscaledTime;
        }

        private static Sprite GetFallbackRingSprite()
        {
            if (_fallbackRingSprite != null)
                return _fallbackRingSprite;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CombatGroundCircle_Ring";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.hideFlags = HideFlags.HideAndDontSave;

            float center = (size - 1) * 0.5f;
            float inner = size * 0.42f;
            float outer = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inRing = dist >= inner && dist <= outer;
                    texture.SetPixel(x, y, inRing ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply(false, true);
            _fallbackRingSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
            _fallbackRingSprite.name = "CombatGroundCircle_RingSprite";
            _fallbackRingSprite.hideFlags = HideFlags.HideAndDontSave;
            return _fallbackRingSprite;
        }
    }
}
