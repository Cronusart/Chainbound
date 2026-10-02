using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Book-index row. The selector bar and label ink fade when the row is selected,
    /// then the selector breathes with a subtle pulse so the player can read where
    /// focus sits on the page.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class ChainlingIndexRow : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public static readonly Color IdleInk = new Color(0.184f, 0.231f, 0.173f, 1f);
        public static readonly Color SelectedInk = new Color(0.698f, 0.729f, 0.565f, 1f);
        public static readonly Color SelectorTint = new Color(0.329f, 0.494f, 0.392f, 1f);

        [SerializeField] private Image selector;
        [SerializeField] private float fadeDuration = 0.18f;

        [Header("Focus Pulse")]
        [SerializeField] private bool pulseWhileSelected = true;
        [SerializeField, Range(0.5f, 1f)] private float pulseLowAlpha = 0.84f;
        [SerializeField, Min(0.2f)] private float pulsePeriodSeconds = 1.5f;
        [SerializeField, Min(1f)] private float pulseScaleX = 1.012f;

        private TMP_Text[] labels;
        private Tween fade;
        private Tween pulse;
        private bool isSelected;

        public RectTransform SelectorRect => selector != null ? selector.rectTransform : null;
        public bool IsSelected => isSelected;

        private void Awake()
        {
            labels = GetComponentsInChildren<TMP_Text>(true);
            if (selector == null)
                selector = transform.Find("Selector")?.GetComponent<Image>();
            ApplyInstant(false);
        }

        private void OnDisable()
        {
            StopPulse();
        }

        private void OnDestroy()
        {
            fade?.Kill();
            fade = null;
            StopPulse();
        }

        public void OnSelect(BaseEventData eventData)
        {
            Play(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Play(false);
        }

        public void ApplyInstant(bool selected)
        {
            fade?.Kill();
            fade = null;
            StopPulse();
            isSelected = selected;
            ApplyVisual(selected, 1f);
            if (selected)
                StartPulse();
        }

        private void Play(bool selected)
        {
            fade?.Kill();
            StopPulse();
            isSelected = selected;
            float duration = Mathf.Max(0.01f, fadeDuration);
            float selectorAlpha = selector != null ? selector.color.a : (selected ? 0f : 1f);
            Color ink = labels != null && labels.Length > 0 && labels[0] != null
                ? labels[0].color
                : IdleInk;
            float elapsed = 0f;
            fade = DOTween.To(() => elapsed, value =>
                {
                    elapsed = value;
                    ApplyVisual(selected, value / duration, selectorAlpha, ink);
                }, duration, duration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    fade = null;
                    if (selected)
                        StartPulse();
                });
        }

        private void StartPulse()
        {
            if (!pulseWhileSelected || selector == null || !Application.isPlaying || !isActiveAndEnabled)
                return;

            StopPulse();
            float half = Mathf.Max(0.1f, pulsePeriodSeconds * 0.5f);
            RectTransform rect = selector.rectTransform;
            rect.localScale = Vector3.one;
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            sequence.Join(selector.DOFade(Mathf.Clamp01(pulseLowAlpha), half).SetEase(Ease.InOutSine));
            sequence.Join(rect.DOScaleX(Mathf.Max(1f, pulseScaleX), half).SetEase(Ease.InOutSine));
            sequence.SetLoops(-1, LoopType.Yoyo);
            pulse = sequence;
        }

        private void StopPulse()
        {
            if (pulse != null && pulse.IsActive())
                pulse.Kill(false);
            pulse = null;
            if (selector != null)
            {
                selector.rectTransform.localScale = Vector3.one;
                Color color = selector.color;
                color.a = isSelected ? 1f : color.a;
                selector.color = color;
            }
        }

        private void ApplyVisual(bool selected, float t, float fromSelectorAlpha = 0f, Color fromInk = default)
        {
            if (fromInk == default)
                fromInk = selected ? IdleInk : SelectedInk;

            if (selector != null)
            {
                Color color = SelectorTint;
                color.a = Mathf.Lerp(fromSelectorAlpha, selected ? 1f : 0f, t);
                selector.color = color;
                selector.raycastTarget = false;
            }

            if (labels == null)
                return;

            Color ink = Color.Lerp(fromInk, selected ? SelectedInk : IdleInk, t);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].color = ink;
            }
        }
    }
}
