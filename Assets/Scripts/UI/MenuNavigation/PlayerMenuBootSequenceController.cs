using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class PlayerMenuBootSequenceController : MonoBehaviour
    {
        [Header("Boot Frames")]
        [SerializeField] private Image bootOverlayImage;
        [SerializeField] private CanvasGroup bootOverlayGroup;
        [SerializeField] private Sprite[] bootFrames = Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float fps = 12f;
        [SerializeField, Min(1)] private int revealStartFrame = 5;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Targets")]
        [SerializeField] private RectTransform backgroundTarget;
        [SerializeField] private RectTransform menuPanelTarget;
        [SerializeField] private CanvasGroup menuPanelGroup;

        [Header("Background Reveal")]
        [SerializeField, Min(0f)] private float revealDuration = 0.16f;
        [SerializeField] private Ease revealEase = Ease.OutSine;

        [Header("Menu Panel Reveal")]
        [SerializeField, Range(0.01f, 1f)] private float menuPanelStartScale = 0.92f;
        [Tooltip("When enabled, the menu panel reveal starts after the background finishes opening instead of waiting for the full boot flipbook.")]
        [SerializeField] private bool startMenuPanelAfterBackgroundReveal = true;
        [SerializeField, Min(0f)] private float contentDelay = 0.02f;
        [SerializeField, Min(0.01f)] private float contentFadeDuration = 0.1f;
        [SerializeField, Min(1f)] private float overshootScale = 1.05f;
        [SerializeField, Min(0.01f)] private float overshootDuration = 0.14f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.2f;
        [SerializeField] private Ease overshootEase = Ease.OutSine;
        [SerializeField] private Ease settleEase = Ease.InOutSine;

        [Header("Overlay Exit")]
        [Tooltip("When enabled, the boot overlay starts fading when the menu panel reveal begins, allowing panel glitch to overlap visibly with the end of the boot sequence.")]
        [SerializeField] private bool fadeOverlayWithMenuPanelReveal = true;
        [SerializeField, Min(0f)] private float overlayFadeDuration = 0.08f;
        [SerializeField] private Ease overlayFadeEase = Ease.Linear;

        [Header("Exit Frames")]
        [SerializeField] private Sprite[] exitFrames = Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float exitFps = 12f;
        [SerializeField] private bool playCloseAnimation = true;

        [Header("Background Close")]
        [SerializeField, Min(0f)] private float closeBackgroundDelay = 0.04f;
        [SerializeField, Min(0f)] private float closeBackgroundDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] private float closeBackgroundEndScaleY = 0f;
        [SerializeField] private Ease closeBackgroundEase = Ease.InSine;

        [Header("Menu Panel Close")]
        [SerializeField, Min(0f)] private float closePanelDelay = 0f;
        [SerializeField, Min(1f)] private float closePanelOvershootScale = 1.03f;
        [SerializeField, Min(0.01f)] private float closePanelOvershootDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float closePanelScaleDownDuration = 0.18f;
        [SerializeField, Range(0f, 1f)] private float closePanelEndScale = 0f;
        [SerializeField, Min(0f)] private float closePanelFadeDuration = 0.12f;
        [SerializeField] private Ease closePanelOvershootEase = Ease.OutSine;
        [SerializeField] private Ease closePanelScaleDownEase = Ease.InSine;

        [Header("Exit Overlay")]
        [SerializeField, Min(0f)] private float exitOverlayFadeInDuration = 0.04f;
        [SerializeField, Min(0f)] private float exitOverlayFadeOutDuration = 0f;
        [SerializeField] private Ease exitOverlayFadeEase = Ease.Linear;

        private readonly List<Sprite> resolvedFrames = new List<Sprite>(10);
        private Sequence activeSequence;
        private Coroutine frameCoroutine;
        private Action completionHandler;
        private Vector3 backgroundRestScale = Vector3.one;
        private Vector3 menuPanelRestScale = Vector3.one;
        private bool restScalesCaptured;

        public bool IsPlaying { get; private set; }
        public bool HasBootFrames => ResolveFrames(bootFrames).Count > 0;
        public bool HasExitFrames => ResolveFrames(exitFrames).Count > 0;
        public float RevealDelaySeconds => ResolveRevealDelaySeconds();
        public float ContentRevealDelaySeconds => ResolveContentRevealDelaySeconds(ResolveFrames(bootFrames).Count);

        private void Awake()
        {
            EnsureHierarchy();
            CaptureRestScales(false);
            ApplyIdleState();
        }

        private void OnDisable()
        {
            Cancel();
        }

        public void ConfigureTargets(RectTransform background, RectTransform menuPanel)
        {
            if (background != null)
                backgroundTarget = background;

            if (menuPanel != null)
            {
                menuPanelTarget = menuPanel;
                menuPanelGroup = GetOrAddCanvasGroup(menuPanelTarget.gameObject);
            }

            CaptureRestScales(backgroundTarget != null || menuPanelTarget != null);
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureHierarchy();
            ApplyIdleState();
        }

        public void PlayOpenSequence(Action onComplete, Action onContentRevealStarted = null)
        {
            EnsureHierarchy();
            Cancel();
            completionHandler = onComplete;
            CaptureRestScales(false);

            IReadOnlyList<Sprite> frames = ResolveFrames(bootFrames);
            if (frames.Count == 0)
            {
                ApplyInstantOpenState();
                CompleteOpenSequence();
                return;
            }

            IsPlaying = true;
            PrepareAnimatedOpenState(frames);
            frameCoroutine = StartCoroutine(PlayFrames(frames, fps));
            activeSequence = BuildOpenSequence(frames.Count, onContentRevealStarted)
                .OnComplete(CompleteOpenSequence);
        }

        public void PlayCloseSequence(Action onComplete)
        {
            EnsureHierarchy();
            Cancel();
            completionHandler = onComplete;
            CaptureRestScales(false);

            if (!playCloseAnimation)
            {
                CompleteCloseSequence();
                return;
            }

            IReadOnlyList<Sprite> frames = ResolveFrames(exitFrames);
            IsPlaying = true;
            PrepareAnimatedCloseState(frames);

            if (frames.Count > 0)
                frameCoroutine = StartCoroutine(PlayFrames(frames, exitFps));

            activeSequence = BuildCloseSequence(frames.Count)
                .OnComplete(CompleteCloseSequence);
        }

        public void Cancel()
        {
            if (frameCoroutine != null)
            {
                StopCoroutine(frameCoroutine);
                frameCoroutine = null;
            }

            activeSequence?.Kill(false);
            activeSequence = null;
            KillAnimatedTargets();
            HideBootOverlay();
            IsPlaying = false;
            completionHandler = null;
        }

        public void ApplyInstantOpenState()
        {
            EnsureHierarchy();
            CaptureRestScales(false);

            if (backgroundTarget != null)
                backgroundTarget.localScale = backgroundRestScale;

            if (menuPanelTarget != null)
                menuPanelTarget.localScale = menuPanelRestScale;

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 1f;
                menuPanelGroup.interactable = true;
                menuPanelGroup.blocksRaycasts = true;
            }

            HideBootOverlay();
        }

        public void RestoreRestState()
        {
            ApplyInstantOpenState();
        }

        private Sequence BuildOpenSequence(int frameCount, Action onContentRevealStarted)
        {
            float frameDuration = 1f / Mathf.Max(1f, fps);
            float bootDuration = frameCount * frameDuration;
            float revealDelay = ResolveRevealDelaySeconds();
            float contentStart = ResolveContentRevealDelaySeconds(frameCount);

            Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

            if (backgroundTarget != null)
            {
                sequence.Insert(
                    revealDelay,
                    backgroundTarget
                        .DOScaleY(backgroundRestScale.y, Mathf.Max(0f, revealDuration))
                        .SetEase(revealEase)
                        .SetUpdate(useUnscaledTime));
            }

            if (menuPanelTarget != null)
            {
                Vector3 overshoot = menuPanelRestScale * Mathf.Max(1f, overshootScale);
                sequence.InsertCallback(contentStart, () => onContentRevealStarted?.Invoke());
                sequence.Insert(
                    contentStart,
                    menuPanelTarget
                        .DOScale(overshoot, Mathf.Max(0.01f, overshootDuration))
                        .SetEase(overshootEase)
                        .SetUpdate(useUnscaledTime));
                sequence.Insert(
                    contentStart + Mathf.Max(0.01f, overshootDuration),
                    menuPanelTarget
                        .DOScale(menuPanelRestScale, Mathf.Max(0.01f, settleDuration))
                        .SetEase(settleEase)
                        .SetUpdate(useUnscaledTime));
            }

            if (menuPanelGroup != null)
            {
                if (menuPanelTarget == null)
                    sequence.InsertCallback(contentStart, () => onContentRevealStarted?.Invoke());

                sequence.Insert(
                    contentStart,
                    menuPanelGroup
                        .DOFade(1f, Mathf.Max(0.01f, contentFadeDuration))
                        .SetEase(Ease.Linear)
                        .SetUpdate(useUnscaledTime));
            }

            float overlayExitAt = fadeOverlayWithMenuPanelReveal ? contentStart : bootDuration;
            if (bootOverlayGroup != null)
            {
                if (overlayFadeDuration > 0f)
                {
                    sequence.Insert(
                        overlayExitAt,
                        bootOverlayGroup
                            .DOFade(0f, overlayFadeDuration)
                            .SetEase(overlayFadeEase)
                            .SetUpdate(useUnscaledTime));
                }
                else
                {
                    sequence.InsertCallback(overlayExitAt, HideBootOverlay);
                }
            }

            float overlayEnd = overlayFadeDuration > 0f
                ? overlayExitAt + overlayFadeDuration
                : contentStart;
            float minimumDuration = Mathf.Max(
                bootDuration,
                overlayEnd,
                revealDelay + Mathf.Max(0f, revealDuration),
                contentStart + Mathf.Max(0.01f, overshootDuration) + Mathf.Max(0.01f, settleDuration));
            sequence.InsertCallback(minimumDuration, () => { });
            return sequence;
        }

        private Sequence BuildCloseSequence(int frameCount)
        {
            float frameDuration = 1f / Mathf.Max(1f, exitFps);
            float frameDurationTotal = frameCount > 0 ? frameCount * frameDuration : 0f;
            Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);

            if (backgroundTarget != null)
            {
                sequence.Insert(
                    Mathf.Max(0f, closeBackgroundDelay),
                    backgroundTarget
                        .DOScaleY(backgroundRestScale.y * Mathf.Clamp01(closeBackgroundEndScaleY), Mathf.Max(0f, closeBackgroundDuration))
                        .SetEase(closeBackgroundEase)
                        .SetUpdate(useUnscaledTime));
            }

            float panelDelay = Mathf.Max(0f, closePanelDelay);
            float overshootDurationSafe = Mathf.Max(0.01f, closePanelOvershootDuration);
            float scaleDownDurationSafe = Mathf.Max(0.01f, closePanelScaleDownDuration);

            if (menuPanelTarget != null)
            {
                Vector3 overshoot = menuPanelRestScale * Mathf.Max(1f, closePanelOvershootScale);
                Vector3 endScale = menuPanelRestScale * Mathf.Clamp01(closePanelEndScale);
                sequence.Insert(
                    panelDelay,
                    menuPanelTarget
                        .DOScale(overshoot, overshootDurationSafe)
                        .SetEase(closePanelOvershootEase)
                        .SetUpdate(useUnscaledTime));
                sequence.Insert(
                    panelDelay + overshootDurationSafe,
                    menuPanelTarget
                        .DOScale(endScale, scaleDownDurationSafe)
                        .SetEase(closePanelScaleDownEase)
                        .SetUpdate(useUnscaledTime));
            }

            if (menuPanelGroup != null && closePanelFadeDuration > 0f)
            {
                float fadeStart = panelDelay + overshootDurationSafe;
                sequence.Insert(
                    fadeStart,
                    menuPanelGroup
                        .DOFade(0f, Mathf.Min(closePanelFadeDuration, scaleDownDurationSafe))
                        .SetEase(Ease.Linear)
                        .SetUpdate(useUnscaledTime));
            }

            if (bootOverlayGroup != null && frameCount > 0)
            {
                if (exitOverlayFadeInDuration > 0f)
                {
                    sequence.Insert(
                        0f,
                        bootOverlayGroup
                            .DOFade(1f, exitOverlayFadeInDuration)
                            .SetEase(exitOverlayFadeEase)
                            .SetUpdate(useUnscaledTime));
                }

                if (exitOverlayFadeOutDuration > 0f)
                {
                    float fadeOutStart = Mathf.Max(0f, frameDurationTotal - exitOverlayFadeOutDuration);
                    sequence.Insert(
                        fadeOutStart,
                        bootOverlayGroup
                            .DOFade(0f, exitOverlayFadeOutDuration)
                            .SetEase(exitOverlayFadeEase)
                            .SetUpdate(useUnscaledTime));
                }
            }

            float minimumDuration = Mathf.Max(
                frameDurationTotal,
                Mathf.Max(0f, closeBackgroundDelay) + Mathf.Max(0f, closeBackgroundDuration),
                panelDelay + overshootDurationSafe + scaleDownDurationSafe);
            sequence.InsertCallback(minimumDuration, () => { });
            return sequence;
        }

        private IEnumerator PlayFrames(IReadOnlyList<Sprite> frames, float framesPerSecond)
        {
            float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
            for (int i = 0; i < frames.Count && IsPlaying; i++)
            {
                if (bootOverlayImage != null && bootOverlayImage.enabled)
                    bootOverlayImage.sprite = frames[i];

                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(frameDuration);
                else
                    yield return new WaitForSeconds(frameDuration);
            }

            frameCoroutine = null;
        }

        private void PrepareAnimatedOpenState(IReadOnlyList<Sprite> frames)
        {
            if (backgroundTarget != null)
                backgroundTarget.localScale = new Vector3(backgroundRestScale.x, 0f, backgroundRestScale.z);

            if (menuPanelTarget != null)
                menuPanelTarget.localScale = menuPanelRestScale * Mathf.Clamp(menuPanelStartScale, 0.01f, 1f);

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 0f;
                menuPanelGroup.interactable = false;
                menuPanelGroup.blocksRaycasts = false;
            }

            if (bootOverlayImage != null)
            {
                bootOverlayImage.sprite = frames[0];
                bootOverlayImage.raycastTarget = false;
                bootOverlayImage.enabled = true;
            }

            ShowBootOverlay(1f);
        }

        private void PrepareAnimatedCloseState(IReadOnlyList<Sprite> frames)
        {
            if (backgroundTarget != null)
                backgroundTarget.localScale = backgroundRestScale;

            if (menuPanelTarget != null)
                menuPanelTarget.localScale = menuPanelRestScale;

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 1f;
                menuPanelGroup.interactable = false;
                menuPanelGroup.blocksRaycasts = false;
            }

            if (frames.Count <= 0)
            {
                HideBootOverlay();
                return;
            }

            if (bootOverlayImage != null)
            {
                bootOverlayImage.sprite = frames[0];
                bootOverlayImage.raycastTarget = false;
                bootOverlayImage.enabled = true;
            }

            ShowBootOverlay(exitOverlayFadeInDuration > 0f ? 0f : 1f);
        }

        private void CompleteOpenSequence()
        {
            if (backgroundTarget != null)
                backgroundTarget.localScale = backgroundRestScale;

            if (menuPanelTarget != null)
                menuPanelTarget.localScale = menuPanelRestScale;

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 1f;
                menuPanelGroup.interactable = true;
                menuPanelGroup.blocksRaycasts = true;
            }

            HideBootOverlay();

            StopFramePlayback();
            activeSequence = null;
            IsPlaying = false;

            Action handler = completionHandler;
            completionHandler = null;
            handler?.Invoke();
        }

        private void CompleteCloseSequence()
        {
            if (backgroundTarget != null)
                backgroundTarget.localScale = new Vector3(
                    backgroundRestScale.x,
                    backgroundRestScale.y * Mathf.Clamp01(closeBackgroundEndScaleY),
                    backgroundRestScale.z);

            if (menuPanelTarget != null)
                menuPanelTarget.localScale = menuPanelRestScale * Mathf.Clamp01(closePanelEndScale);

            if (menuPanelGroup != null)
            {
                menuPanelGroup.alpha = 0f;
                menuPanelGroup.interactable = false;
                menuPanelGroup.blocksRaycasts = false;
            }

            HideBootOverlay();

            StopFramePlayback();
            activeSequence = null;
            IsPlaying = false;

            Action handler = completionHandler;
            completionHandler = null;
            handler?.Invoke();
        }

        private void StopFramePlayback()
        {
            if (frameCoroutine != null)
            {
                StopCoroutine(frameCoroutine);
                frameCoroutine = null;
            }
        }

        private void KillAnimatedTargets()
        {
            if (backgroundTarget != null)
                backgroundTarget.DOKill();
            if (menuPanelTarget != null)
                menuPanelTarget.DOKill();
            if (menuPanelGroup != null)
                menuPanelGroup.DOKill();
            if (bootOverlayGroup != null)
                bootOverlayGroup.DOKill();
            if (bootOverlayImage != null)
                bootOverlayImage.DOKill();
        }

        private void HideBootOverlay()
        {
            if (bootOverlayImage != null)
            {
                bootOverlayImage.DOKill();
                bootOverlayImage.enabled = false;
            }

            if (bootOverlayGroup != null)
            {
                bootOverlayGroup.DOKill();
                bootOverlayGroup.alpha = 0f;
                bootOverlayGroup.interactable = false;
                bootOverlayGroup.blocksRaycasts = false;
                bootOverlayGroup.gameObject.SetActive(false);
            }
        }

        private void ShowBootOverlay(float alpha)
        {
            if (bootOverlayImage != null)
                bootOverlayImage.enabled = true;

            if (bootOverlayGroup != null)
            {
                bootOverlayGroup.DOKill();
                bootOverlayGroup.alpha = Mathf.Clamp01(alpha);
                bootOverlayGroup.interactable = false;
                bootOverlayGroup.blocksRaycasts = false;
                bootOverlayGroup.gameObject.SetActive(true);
            }
        }

        private void ApplyIdleState()
        {
            if (bootOverlayImage != null)
                bootOverlayImage.raycastTarget = false;

            HideBootOverlay();
        }

        private void EnsureHierarchy()
        {
            if (transform is RectTransform rootRect)
                Stretch(rootRect);

            transform.SetAsLastSibling();
            // Boot draws over the terminal. An open chainling action sheet stays in front of that panel.
            Transform actionSheet = transform.parent != null ? transform.parent.Find("ActionSheet") : null;
            if (actionSheet != null && actionSheet.gameObject.activeSelf)
                actionSheet.SetAsLastSibling();

            if (bootOverlayImage == null)
                bootOverlayImage = transform.Find("BootOverlay")?.GetComponent<Image>();

            if (bootOverlayImage == null)
            {
                GameObject overlay = new GameObject(
                    "BootOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                overlay.transform.SetParent(transform, false);
                bootOverlayImage = overlay.GetComponent<Image>();
                Stretch(bootOverlayImage.rectTransform);
            }

            bootOverlayImage.color = Color.white;
            bootOverlayImage.type = Image.Type.Simple;
            bootOverlayImage.preserveAspect = false;
            bootOverlayImage.raycastTarget = false;

            if (bootOverlayGroup == null)
                bootOverlayGroup = bootOverlayImage.GetComponent<CanvasGroup>();
            if (bootOverlayGroup == null)
                bootOverlayGroup = bootOverlayImage.gameObject.AddComponent<CanvasGroup>();
        }

        private void CaptureRestScales(bool force)
        {
            if (restScalesCaptured && !force)
                return;

            if (backgroundTarget == null && menuPanelTarget == null)
                return;

            if (backgroundTarget != null)
                backgroundRestScale = ResolveRestScale(backgroundTarget.localScale);
            if (menuPanelTarget != null)
                menuPanelRestScale = ResolveRestScale(menuPanelTarget.localScale);

            restScalesCaptured = true;
        }

        private IReadOnlyList<Sprite> ResolveFrames(Sprite[] sourceFrames)
        {
            resolvedFrames.Clear();
            if (sourceFrames == null)
                return resolvedFrames;

            for (int i = 0; i < sourceFrames.Length; i++)
            {
                if (sourceFrames[i] != null)
                    resolvedFrames.Add(sourceFrames[i]);
            }

            return resolvedFrames;
        }

        private float ResolveRevealDelaySeconds()
        {
            int oneBasedFrame = Mathf.Max(1, revealStartFrame);
            return (oneBasedFrame - 1) / Mathf.Max(1f, fps);
        }

        private float ResolveContentRevealDelaySeconds(int frameCount)
        {
            float frameDuration = 1f / Mathf.Max(1f, fps);
            float bootDuration = Mathf.Max(0, frameCount) * frameDuration;
            float backgroundRevealEnd = ResolveRevealDelaySeconds() + Mathf.Max(0f, revealDuration);
            float baseStart = startMenuPanelAfterBackgroundReveal && backgroundTarget != null
                ? backgroundRevealEnd
                : bootDuration;

            return baseStart + Mathf.Max(0f, contentDelay);
        }

        private static Vector3 ResolveRestScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? scale.z : 1f);
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
