using System.Collections;
using Chainbound.Combat;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ChainlingMenuVisualPresenter : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private PartyTabFrameAnimator animator;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private int idleLoopsPerFlourish = 2;
        [SerializeField] private float identityFadeSeconds = 0.16f;

        private ResolvedChainlingVisualIdentity resolvedIdentity;
        private ResolvedChainlingVisualClip idleClip;
        private ResolvedChainlingVisualClip flourishClip;
        private ResolvedChainlingVisualClip swapInClip;
        private int idleLoopCount;
        private Coroutine delayedIdleRoutine;
        private Tween fadeTween;
        private string boundVisualKey;
        private bool boundFilled;

        private void Awake()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (animator != null)
                animator.LoopCompleted += HandleIdleLoopCompleted;
        }

        private void OnDisable()
        {
            if (animator != null)
                animator.LoopCompleted -= HandleIdleLoopCompleted;
            StopDelayedPlayback();
            fadeTween?.Kill();
            animator?.Stop();
        }

        public void Bind(ResolvedChainlingVisualIdentity resolved, bool filled, bool animateIdentityChange)
        {
            EnsureBuilt();
            string visualKey = BuildVisualKey(resolved);
            bool sameBoundVisual =
                filled &&
                boundFilled &&
                string.Equals(boundVisualKey, visualKey, System.StringComparison.Ordinal);

            resolvedIdentity = resolved;

            if (!filled || resolvedIdentity == null)
            {
                animator.Stop();
                if (targetImage != null)
                    targetImage.enabled = false;
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                boundFilled = false;
                boundVisualKey = null;
                return;
            }

            if (sameBoundVisual)
            {
                if (targetImage != null)
                {
                    targetImage.enabled = true;
                    targetImage.preserveAspect = true;
                }

                if (canvasGroup != null)
                {
                    fadeTween?.Kill();
                    canvasGroup.alpha = 1f;
                }

                if (Application.isPlaying && animator != null && !animator.IsPlaying)
                    PlayIdle();
                return;
            }

            boundFilled = true;
            boundVisualKey = visualKey;
            idleLoopCount = 0;

            idleClip = ChainlingVisualSequenceResolver.Resolve(resolvedIdentity, ChainlingAnimSlot.Idle);
            flourishClip = ChainlingVisualSequenceResolver.Resolve(resolvedIdentity, ChainlingAnimSlot.IdleFlourish);
            swapInClip = ChainlingVisualSequenceResolver.Resolve(resolvedIdentity, ChainlingAnimSlot.SwapIn);

            if (targetImage != null)
            {
                targetImage.enabled = idleClip.HasSequence || swapInClip.HasSequence;
                targetImage.preserveAspect = true;
            }

            if (canvasGroup != null)
            {
                fadeTween?.Kill();
                if (animateIdentityChange && Application.isPlaying)
                {
                    canvasGroup.alpha = 0f;
                    fadeTween = canvasGroup
                        .DOFade(1f, identityFadeSeconds)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true);
                }
                else
                {
                    canvasGroup.alpha = 1f;
                }
            }

            if (Application.isPlaying)
                PlayIdle();
            else
                SampleIdleFirstFrame();
        }

        public void PrepareForEnter()
        {
            StopDelayedPlayback();
            fadeTween?.Kill();
            animator?.Stop();
            idleLoopCount = 0;
        }

        public void PlaySwapIn(float delaySeconds)
        {
            if (resolvedIdentity == null)
                return;

            StopDelayedPlayback();
            idleLoopCount = 0;
            float delay = Mathf.Max(0f, delaySeconds);

            if (swapInClip.HasSequence)
            {
                PlayClip(swapInClip, loop: false);
                animator.PlayOneShot(PlayIdle, delay);
                return;
            }

            delayedIdleRoutine = StartCoroutine(PlayIdleAfterDelay(delay));
        }

        public void PlayIdle()
        {
            if (!idleClip.HasSequence)
                return;

            PlayClip(idleClip, loop: true);
            animator.Play();
        }

        public void Pause()
        {
            animator?.Stop();
        }

        private void StopDelayedPlayback()
        {
            if (delayedIdleRoutine != null)
            {
                StopCoroutine(delayedIdleRoutine);
                delayedIdleRoutine = null;
            }
        }

        private IEnumerator PlayIdleAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSecondsRealtime(delaySeconds);

            delayedIdleRoutine = null;
            PlayIdle();
        }

        private void HandleIdleLoopCompleted()
        {
            if (!flourishClip.HasSequence || animator == null)
                return;

            idleLoopCount++;
            if (idleLoopCount % Mathf.Max(1, idleLoopsPerFlourish) != 0)
                return;

            PlayClip(flourishClip, loop: false);
            animator.PlayOneShot(PlayIdle);
        }

        private void PlayClip(ResolvedChainlingVisualClip clip, bool loop)
        {
            if (!clip.HasSequence || animator == null)
                return;

            animator.SetFrames(clip.sequence);
            animator.SetFPS(clip.fps);
            animator.SetLoop(loop);
            if (targetImage != null)
                targetImage.enabled = true;
        }

        private void SampleIdleFirstFrame()
        {
            if (!idleClip.HasSequence || animator == null)
                return;

            PlayClip(idleClip, loop: true);
            animator.Stop();
        }

        private void EnsureBuilt()
        {
            if (targetImage == null)
                targetImage = GetComponent<Image>();
            if (animator == null)
                animator = GetComponent<PartyTabFrameAnimator>();
            if (animator == null)
                animator = gameObject.AddComponent<PartyTabFrameAnimator>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private static string BuildVisualKey(ResolvedChainlingVisualIdentity resolved)
        {
            if (resolved == null)
                return "";

            ChainlingVisualIdentity identity = resolved.identity;
            return string.Join(
                "|",
                resolved.speciesId ?? "",
                resolved.visualVariantId ?? "",
                resolved.baseProfile != null ? resolved.baseProfile.GetEntityId().ToString() : "",
                identity != null ? identity.idleStyleId ?? "" : "",
                identity != null ? identity.idleFlourishId ?? "" : "",
                identity != null ? identity.partyColumnVariantId ?? "" : "");
        }
    }
}
