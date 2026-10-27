using System.Collections;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Plays an authored RAM bundle sheet and a slight unscaled scale pulse.
    /// The pulse lives on a child so focus and press motion can scale the parent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RamBundleCardPresenter : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image artImage;
        [SerializeField] private RectTransform pulseRoot;
        [SerializeField] private RectTransform revealRoot;
        [SerializeField] private CanvasGroup revealGroup;
        [SerializeField] private CanvasGroup selectionGlow;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private int ramAmount;
        [SerializeField] private float framesPerSecond = 8f;
        [SerializeField] private float pulseScale = 1.03f;
        [SerializeField] private float pulseHalfSeconds = 0.9f;
        [SerializeField] private float pulseDelay;
        [SerializeField] private bool featured;

        private Coroutine playback;
        private Sequence revealSequence;
        private Tweener glowTween;
        private Vector2 revealRestPosition;
        private bool revealPositionCaptured;

        public int RamAmount => ramAmount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            ShowFrame(0);
            playback = StartCoroutine(PlayFrames());
            StartPulse();
            SetGlowImmediate(featured ? 0.22f : 0f);
        }

        private void OnDisable()
        {
            if (playback != null)
            {
                StopCoroutine(playback);
                playback = null;
            }

            DOTween.Kill(this, false);
            revealSequence?.Kill(false);
            revealSequence = null;
            glowTween?.Kill(false);
            glowTween = null;
            if (pulseRoot != null)
                pulseRoot.localScale = Vector3.one;
            ResetReveal();
        }

        public void PlayReveal()
        {
            if (revealRoot == null)
                return;

            if (!revealPositionCaptured)
            {
                revealRestPosition = revealRoot.anchoredPosition;
                revealPositionCaptured = true;
            }

            revealSequence?.Kill(false);
            revealRoot.DOKill(false);
            revealRoot.anchoredPosition = revealRestPosition + new Vector2(0f, -48f);
            revealRoot.localScale = Vector3.one * 0.72f;
            revealRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-5f, 5f, pulseDelay * 2f));
            if (revealGroup != null)
                revealGroup.alpha = 0f;

            float delay = Mathf.Max(0f, pulseDelay) * 0.55f;
            revealSequence = DOTween.Sequence()
                .SetDelay(delay)
                .Append(revealRoot.DOAnchorPos(revealRestPosition, 0.48f).SetEase(Ease.OutCubic))
                .Join(revealRoot.DOScale(1f, 0.5f).SetEase(Ease.OutBack))
                .Join(revealRoot.DOLocalRotate(Vector3.zero, 0.42f).SetEase(Ease.OutCubic))
                .SetUpdate(true)
                .SetTarget(this);

            if (revealGroup != null)
                revealSequence.Join(revealGroup.DOFade(1f, 0.18f).SetEase(Ease.OutQuad));

            revealSequence.AppendCallback(() => FlashGlow(featured ? 0.9f : 0.65f));
        }

        public void SetTenderPrice(RamTender tender, string requestedTender)
        {
            if (priceLabel == null)
                return;

            if (tender == null)
            {
                string label = string.IsNullOrWhiteSpace(requestedTender) ? "SOL" : requestedTender.ToUpperInvariant();
                priceLabel.text = $"QUOTE IN {label}";
                priceLabel.color = new Color(0.66f, 0.72f, 0.84f, 1f);
                return;
            }

            string symbol = string.IsNullOrWhiteSpace(tender.tender)
                ? (string.IsNullOrWhiteSpace(requestedTender) ? "SOL" : requestedTender)
                : tender.tender;
            priceLabel.text = $"{tender.FormatAmount()} {symbol.ToUpperInvariant()}";
            priceLabel.color = new Color(1f, 0.83f, 0.28f, 1f);
        }

        public void OnSelect(BaseEventData eventData)
        {
            AnimateGlow(0.95f, 0.14f);
            if (pulseRoot != null)
            {
                pulseRoot.DOKill(false);
                pulseRoot.DOScale(Mathf.Max(1.08f, pulseScale), 0.18f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetTarget(this);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            AnimateGlow(featured ? 0.22f : 0f, 0.18f);
            StartPulse();
        }

        private void ShowFrame(int index)
        {
            if (artImage == null || frames == null || frames.Length == 0)
                return;

            Sprite sprite = frames[index % frames.Length];
            if (sprite != null)
                artImage.sprite = sprite;
        }

        private IEnumerator PlayFrames()
        {
            if (frames == null || frames.Length <= 1)
                yield break;

            float wait = 1f / Mathf.Max(1f, framesPerSecond);
            int index = 0;
            while (isActiveAndEnabled)
            {
                ShowFrame(index);
                yield return new WaitForSecondsRealtime(wait);
                index = (index + 1) % frames.Length;
            }
        }

        private void StartPulse()
        {
            if (pulseRoot == null)
                return;

            pulseRoot.DOKill(false);
            pulseRoot.localScale = Vector3.one;
            pulseRoot
                .DOScale(Mathf.Max(1f, pulseScale), Mathf.Max(0.2f, pulseHalfSeconds))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Mathf.Max(0f, pulseDelay))
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void FlashGlow(float peak)
        {
            if (selectionGlow == null)
                return;

            glowTween?.Kill(false);
            selectionGlow.alpha = featured ? 0.22f : 0f;
            glowTween = selectionGlow.DOFade(peak, 0.16f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() => selectionGlow.alpha = featured ? 0.22f : 0f);
        }

        private void AnimateGlow(float alpha, float duration)
        {
            if (selectionGlow == null)
                return;

            glowTween?.Kill(false);
            glowTween = selectionGlow.DOFade(alpha, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void SetGlowImmediate(float alpha)
        {
            if (selectionGlow != null)
                selectionGlow.alpha = alpha;
        }

        private void ResetReveal()
        {
            if (revealRoot != null)
            {
                if (revealPositionCaptured)
                    revealRoot.anchoredPosition = revealRestPosition;
                revealRoot.localScale = Vector3.one;
                revealRoot.localRotation = Quaternion.identity;
            }

            if (revealGroup != null)
                revealGroup.alpha = 1f;
            SetGlowImmediate(featured ? 0.22f : 0f);
        }
    }
}
