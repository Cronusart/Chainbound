using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class PlayerMenuTabButtonVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image buttonImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform motionTarget;
        [SerializeField] private Image shimmerImage;

        [Header("Profile")]
        [SerializeField] private PlayerMenuTabButtonVisualProfileSO profile;

        [Header("Fallback")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Image.Type imageType = Image.Type.Sliced;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color lockedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        [SerializeField] private bool preserveAuthoredLabelAppearance = true;

        private readonly List<Sprite> runtimeShimmerFrames = new List<Sprite>();
        private Vector3 baseScale = Vector3.one;
        private bool baseScaleCaptured;
        private bool selected;
        private bool locked;
        private Tweener scaleTween;
        private Coroutine shimmerRoutine;

        public PlayerMenuTabButtonVisualProfileSO Profile => profile;

        private void Awake()
        {
            ResolveReferences();
            CaptureBaseScale();
            SetShimmerVisible(false);
        }

        private void OnDisable()
        {
            StopShimmer();
            KillScaleTween();
        }

        private void OnDestroy()
        {
            StopShimmer();
            KillScaleTween();
        }

        public void ConfigureProfile(PlayerMenuTabButtonVisualProfileSO visualProfile)
        {
            if (visualProfile != null)
                profile = visualProfile;
        }

        public void ApplyState(bool hasTab, bool isSelected, bool isLocked, bool animate)
        {
            ResolveReferences();
            CaptureBaseScale();

            selected = hasTab && isSelected;
            locked = hasTab && isLocked;

            ApplyImageState();
            ApplyLabelState();
            ApplyScale(animate && Application.isPlaying);
            ApplyShimmerState();
        }

        public void PlayPressed()
        {
            ResolveReferences();
            CaptureBaseScale();

            if (motionTarget == null || !Application.isPlaying)
                return;

            KillScaleTween();
            Vector3 press = baseScale * ResolvePressScale();
            Vector3 rest = ResolveTargetScale();
            Sequence sequence = DOTween.Sequence().SetUpdate(ResolveUseUnscaledTime());
            sequence.Append(motionTarget.DOScale(press, ResolvePressDuration()).SetEase(ResolvePressEase()));
            sequence.Append(motionTarget.DOScale(rest, ResolvePressDuration()).SetEase(ResolvePressReturnEase()));
        }

        public void EnsureShimmerChildForEditor()
        {
            ResolveReferences();
            EnsureShimmerImage();
        }

        private void EnsureShimmerImage()
        {
            if (shimmerImage != null)
            {
                shimmerImage.raycastTarget = false;
                shimmerImage.color = Color.white;
                Stretch(shimmerImage.rectTransform);
                shimmerImage.transform.SetAsFirstSibling();
                return;
            }

            GameObject shimmer = new GameObject(
                "Shimmer",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            shimmer.transform.SetParent(transform, false);
            shimmerImage = shimmer.GetComponent<Image>();
            shimmerImage.raycastTarget = false;
            shimmerImage.color = Color.white;
            Stretch(shimmerImage.rectTransform);
            shimmerImage.transform.SetAsFirstSibling();
            SetShimmerVisible(false);
        }

        private void ResolveReferences()
        {
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);
            if (motionTarget == null)
                motionTarget = transform as RectTransform;
            if (shimmerImage == null)
            {
                Transform shimmer = transform.Find("Shimmer");
                if (shimmer != null)
                    shimmerImage = shimmer.GetComponent<Image>();
            }
        }

        private void CaptureBaseScale()
        {
            if (baseScaleCaptured || motionTarget == null)
                return;

            baseScale = motionTarget.localScale;
            if (Mathf.Abs(baseScale.x) < 0.0001f)
                baseScale.x = 1f;
            if (Mathf.Abs(baseScale.y) < 0.0001f)
                baseScale.y = 1f;
            if (Mathf.Abs(baseScale.z) < 0.0001f)
                baseScale.z = 1f;
            baseScaleCaptured = true;
        }

        private void ApplyImageState()
        {
            if (buttonImage == null)
                return;

            Sprite sprite = locked ? ResolveLockedSprite() : selected ? ResolveSelectedSprite() : ResolveNormalSprite();
            if (sprite != null)
                buttonImage.sprite = sprite;

            buttonImage.type = ResolveImageType();
            buttonImage.preserveAspect = ResolvePreserveAspect();
            buttonImage.color = locked ? ResolveLockedColor() : selected ? ResolveSelectedColor() : ResolveNormalColor();
        }

        private void ApplyLabelState()
        {
            if (label == null)
                return;

            if (preserveAuthoredLabelAppearance)
                return;

            label.color = locked ? ResolveLockedColor() : selected ? ResolveSelectedColor() : ResolveNormalColor();
        }

        private void ApplyScale(bool animate)
        {
            if (motionTarget == null)
                return;

            Vector3 targetScale = ResolveTargetScale();
            KillScaleTween();

            if (!animate)
            {
                motionTarget.localScale = targetScale;
                return;
            }

            float duration = selected ? ResolveFocusDuration() : ResolveUnfocusDuration();
            Ease ease = selected ? ResolveFocusEase() : ResolveUnfocusEase();
            scaleTween = motionTarget
                .DOScale(targetScale, duration)
                .SetEase(ease)
                .SetUpdate(ResolveUseUnscaledTime());
        }

        private void ApplyShimmerState()
        {
            if (selected && ResolveShimmerFrames(runtimeShimmerFrames) > 0)
            {
                EnsureShimmerImage();
                StartShimmer();
            }
            else
            {
                StopShimmer();
            }
        }

        private void StartShimmer()
        {
            if (shimmerImage == null || runtimeShimmerFrames.Count == 0)
                return;

            StopShimmer();
            SetShimmerVisible(true);
            shimmerRoutine = StartCoroutine(ShimmerLoop());
        }

        private void StopShimmer()
        {
            if (shimmerRoutine != null)
            {
                StopCoroutine(shimmerRoutine);
                shimmerRoutine = null;
            }

            SetShimmerVisible(false);
        }

        private IEnumerator ShimmerLoop()
        {
            float frameDelay = 1f / ResolveShimmerFps();
            float pause = ResolveShimmerPause();

            while (true)
            {
                for (int i = 0; i < runtimeShimmerFrames.Count; i++)
                {
                if (shimmerImage != null)
                    shimmerImage.sprite = runtimeShimmerFrames[i];

                    if (ResolveUseUnscaledTime())
                        yield return new WaitForSecondsRealtime(frameDelay);
                    else
                        yield return new WaitForSeconds(frameDelay);
                }

                if (pause > 0f)
                {
                    if (ResolveUseUnscaledTime())
                        yield return new WaitForSecondsRealtime(pause);
                    else
                        yield return new WaitForSeconds(pause);
                }
            }
        }

        private void SetShimmerVisible(bool visible)
        {
            if (shimmerImage == null)
                return;

            shimmerImage.enabled = visible;
            shimmerImage.raycastTarget = false;
        }

        private int ResolveShimmerFrames(List<Sprite> target)
        {
            target.Clear();
            IReadOnlyList<Sprite> frames = profile != null ? profile.ShimmerFrames : null;
            if (frames == null)
                return 0;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                    target.Add(frames[i]);
            }

            return target.Count;
        }

        private Vector3 ResolveTargetScale()
        {
            return selected ? baseScale * ResolveSelectedScale() : baseScale;
        }

        private void KillScaleTween()
        {
            if (scaleTween != null && scaleTween.IsActive())
                scaleTween.Kill();
            scaleTween = null;

            if (motionTarget != null)
                motionTarget.DOKill();
        }

        private Sprite ResolveNormalSprite() => profile != null && profile.NormalSprite != null ? profile.NormalSprite : normalSprite;
        private Sprite ResolveSelectedSprite() => profile != null && profile.SelectedSprite != null ? profile.SelectedSprite : selectedSprite;
        private Sprite ResolveLockedSprite() => profile != null && profile.LockedSprite != null ? profile.LockedSprite : lockedSprite != null ? lockedSprite : ResolveNormalSprite();
        private Image.Type ResolveImageType() => profile != null ? profile.ImageType : imageType;
        private bool ResolvePreserveAspect() => profile != null && profile.PreserveAspect;
        private Color ResolveNormalColor() => profile != null ? profile.NormalColor : normalColor;
        private Color ResolveSelectedColor() => profile != null ? profile.SelectedColor : selectedColor;
        private Color ResolveLockedColor() => profile != null ? profile.LockedColor : lockedColor;
        private float ResolveSelectedScale() => profile != null ? profile.SelectedScale : 1.05f;
        private float ResolveFocusDuration() => profile != null ? profile.FocusDuration : 0.14f;
        private float ResolveUnfocusDuration() => profile != null ? profile.UnfocusDuration : 0.14f;
        private Ease ResolveFocusEase() => profile != null ? profile.FocusEase : Ease.OutSine;
        private Ease ResolveUnfocusEase() => profile != null ? profile.UnfocusEase : Ease.InOutSine;
        private float ResolvePressScale() => profile != null ? profile.PressScale : 0.94f;
        private float ResolvePressDuration() => profile != null ? profile.PressDuration : 0.08f;
        private Ease ResolvePressEase() => profile != null ? profile.PressEase : Ease.OutSine;
        private Ease ResolvePressReturnEase() => profile != null ? profile.PressReturnEase : Ease.OutBack;
        private bool ResolveUseUnscaledTime() => profile == null || profile.UseUnscaledTime;
        private float ResolveShimmerFps() => profile != null ? profile.ShimmerFps : 12f;
        private float ResolveShimmerPause() => profile != null ? profile.ShimmerPauseDuration : 0.6f;

        private static void Stretch(RectTransform rt)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
