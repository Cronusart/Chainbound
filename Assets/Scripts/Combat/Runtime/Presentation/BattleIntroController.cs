using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using SignalJam.UI.VisualFx;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Owns the combat-local battle intro panel and the optional external intro handshake.
    /// </summary>
    [ExecuteAlways]
    public class BattleIntroController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup messagePanel;
        [SerializeField] private TMP_Text messageText;

        [Header("Timing")]
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private float typewriterCharsPerSecond = 40f;
        [SerializeField] private float autoAdvanceDelay = 2f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("VFX")]
        [SerializeField] private BattleIntroVfxProfileSO fallbackVfxProfile;
        [SerializeField] private RectTransform bannerRoot;
        [SerializeField] private Image bannerBackdrop;
        [SerializeField] private Image bannerGlint;
        [SerializeField] private MonoBehaviour bannerGlintAnimator;
        [SerializeField] private RectTransform textGlintMask;
        [SerializeField] private TMP_Text textGlintLabel;
        [SerializeField] private TMP_Text textGlintSource;
        [SerializeField, FormerlySerializedAs("leftPixelBurst")]
        private ParticleSystem topEdgeParticleBurst;
        [SerializeField, FormerlySerializedAs("rightPixelBurst")]
        private ParticleSystem bottomEdgeParticleBurst;
        [SerializeField] private Image screenPulseOverlay;
        [SerializeField] private bool scaffoldMissingVfxAtRuntime = true;
        [Tooltip("Added to the host Canvas sorting order so bursts draw above the intro card.")]
        [SerializeField] private int particleBurstSortingOffset = 8;

        [Header("Interaction")]
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private bool hidePanelOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Editor Preview")]
        [SerializeField] private bool previewInEditMode = false;
        [SerializeField, TextArea(2, 4)] private string previewMessage = "A wild Chainling has appeared!";

        /// <summary>TMP punch / fallback punch tweens only — killed when a new punch plays.</summary>
        private readonly List<Tween> _textPunchTweens = new List<Tween>();

        /// <summary>Banner glint, text glint, screen pulse — not cancelled by <see cref="PlayTextPunch"/>.</summary>
        private readonly List<Tween> _introAccentTweens = new List<Tween>();
        private Coroutine _activeRoutine;
        private bool _waitingForExternalIntro;
        private bool _skipRequested;
        private bool _allowSkipCurrent = true;
        private Sequence _activeVfxSequence;
        private RectTransform _panelRect;
        private Vector2 _basePanelAnchoredPosition;
        private Vector3 _basePanelScale = Vector3.one;
        private Color _baseTextColor = Color.white;
        private Color _baseBackdropColor = Color.white;
        private BattleIntroVfxProfileSO _runtimeFallbackProfile;
        private object _tmpAnimator;
        private Image _runtimeBannerGlintImage;
        private MonoBehaviour _runtimeBannerGlintAnimator;
        private BattleIntroCopyConfigSO _activeCopyConfig;
        private BattleIntroCopySpan[] _activeCopySpans;
        private bool _copySpanPreRenderSubscribed;

        private Coroutine _dualSweepBurstRoutine;
        private bool _sweepEmitterAnchorsCaptured;
        private Vector2 _sweepTopAnchoredRestore;
        private Vector2 _sweepBottomAnchoredRestore;

        private const float BannerFrameGlintFadeInRatio = 0.12f;
        private const float BannerFrameGlintFadeOutRatio = 0.12f;

        /// <summary>
        /// Baselines matching <see cref="BattleIntroVfxProfileSO"/> defaults. Used as 1.0× when scaling sweep-burst tuning from the profile.
        /// </summary>
        private const float DefaultSweepProfileLifetime = 0.28f;
        private const float DefaultSweepProfileSpeed = 70f;

        private static Material _burstParticleOverlayMaterialCache;

        public bool IsPlaying => _activeRoutine != null || _waitingForExternalIntro;

        public event Func<BattleIntroRequest, bool> OnExternalIntroRequested;
        public event Action OnIntroComplete;

        private void Awake()
        {
            CacheBaseState();

            if (!Application.isPlaying)
            {
                ApplyEditorPreviewState();
                return;
            }

            if (hidePanelOnAwake)
                HidePanelImmediate();
        }

        private void OnEnable()
        {
            CacheBaseState();

            if (!Application.isPlaying)
                ApplyEditorPreviewState();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                CancelIntro();
            else
                KillVfx();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyEditorPreviewState();
        }

        private void Update()
        {
            if (!_allowSkipCurrent || !allowSkip || !IsPlaying)
                return;

            bool touchBegan = false;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (Input.GetTouch(i).phase == TouchPhase.Began)
                    {
                        touchBegan = true;
                        break;
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0)
                || touchBegan)
            {
                _skipRequested = true;
            }
        }

        public Coroutine PlayLocalIntro(BattleIntroRequest request)
        {
            CancelIntro();
            _activeRoutine = StartCoroutine(PlayLocalIntroRoutine(request));
            return _activeRoutine;
        }

        public Coroutine PlayExternalIntro(BattleIntroRequest request)
        {
            CancelIntro();
            _activeRoutine = StartCoroutine(PlayExternalIntroRoutine(request));
            return _activeRoutine;
        }

        public void NotifyExternalIntroComplete()
        {
            _waitingForExternalIntro = false;
        }

        public void CancelIntro()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            _waitingForExternalIntro = false;
            _skipRequested = false;
            UnsubscribeCopySpanPreRender();
            HidePanelImmediate();
        }

        public void SkipCurrentIntro()
        {
            if (_allowSkipCurrent && allowSkip)
                _skipRequested = true;
        }

        [ContextMenu("Show Editor Preview")]
        private void ShowEditorPreview()
        {
            previewInEditMode = true;
            ApplyEditorPreviewState();
        }

        [ContextMenu("Hide Editor Preview")]
        private void HideEditorPreview()
        {
            previewInEditMode = false;
            ApplyEditorPreviewState();
        }

        [ContextMenu("Scaffold Runtime VFX References")]
        private void ScaffoldRuntimeVfxReferences()
        {
            EnsureVfxReferences();
        }

        private IEnumerator PlayLocalIntroRoutine(BattleIntroRequest request)
        {
            _skipRequested = false;
            _allowSkipCurrent = request.allowSkip;

            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] PlayLocalIntroRoutine started.", this);

            if (messagePanel == null || messageText == null || string.IsNullOrWhiteSpace(request.messageText))
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[BattleIntroController] Local intro skipped because panel/text/message was missing.", this);

                CompleteIntro();
                yield break;
            }

            BattleIntroVfxProfileSO profile = ResolveRuntimeVfxProfile(request);
            if (profile == null || !CanPlayVfxIntro())
            {
                yield return PlayFadeTypewriterIntroRoutine(request);
                yield break;
            }

            yield return PlayVfxIntroRoutine(request, profile);
        }

        private IEnumerator PlayFadeTypewriterIntroRoutine(BattleIntroRequest request)
        {
            messagePanel.gameObject.SetActive(true);
            messagePanel.blocksRaycasts = false;
            messagePanel.interactable = false;
            messageText.richText = true;
            messageText.color = _baseTextColor;
            messageText.text = request.messageText;
            messageText.maxVisibleCharacters = 0;

            yield return FadePanel(0f, 1f);
            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] Fade in complete.", this);

            yield return RevealText(request.messageText);
            ApplyCopySpanStyles(request);
            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] Typewriter complete.", this);

            if (!_skipRequested && autoAdvanceDelay > 0f)
                yield return WaitForDelay(autoAdvanceDelay);
            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] Auto-advance delay complete.", this);

            yield return FadePanel(1f, 0f);
            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] Fade out complete.", this);

            CompleteIntro();
        }

        private IEnumerator PlayVfxIntroRoutine(BattleIntroRequest request, BattleIntroVfxProfileSO profile)
        {
            PrepareVfxState(request.messageText, profile);

            _activeVfxSequence = BuildEntrySequence(profile);
            yield return WaitForTweenOrSkip(_activeVfxSequence);

            if (!_skipRequested)
                PlayGlintAndBurst(profile);

            if (!_skipRequested && profile.enablePixelBurst)
                EmitPixelBursts(profile);

            yield return RevealText(request.messageText);
            ApplyCopySpanStyles(request);

            if (!_skipRequested)
            {
                PlayTextPunch(profile);

                float accentDuration = ComputeBannerAccentDurationSeconds(profile);
                if (accentDuration > 0f)
                    yield return WaitForDelay(accentDuration);

                if (!_skipRequested && profile.holdDuration > 0f)
                    yield return WaitForDelay(profile.holdDuration);
            }

            _activeVfxSequence = BuildExitSequence(profile);
            yield return WaitForTweenOrSkip(_activeVfxSequence);

            CompleteIntro();
        }

        private IEnumerator PlayExternalIntroRoutine(BattleIntroRequest request)
        {
            _skipRequested = false;
            _allowSkipCurrent = request.allowSkip;

            bool handled = RaiseExternalIntroRequested(request);
            if (!handled)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[BattleIntroController] External intro request had no handler. Falling back to local intro.", this);

                if (!string.IsNullOrWhiteSpace(request.messageText))
                {
                    yield return PlayLocalIntroRoutine(request);
                    yield break;
                }

                CompleteIntro();
                yield break;
            }

            _waitingForExternalIntro = true;
            while (_waitingForExternalIntro)
                yield return null;

            CompleteIntro();
        }

        private bool RaiseExternalIntroRequested(BattleIntroRequest request)
        {
            var handlers = OnExternalIntroRequested;
            if (handlers == null)
                return false;

            bool handled = false;
            var delegates = handlers.GetInvocationList();
            for (int i = 0; i < delegates.Length; i++)
            {
                try
                {
                    if (((Func<BattleIntroRequest, bool>)delegates[i]).Invoke(request))
                        handled = true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            return handled;
        }

        private BattleIntroVfxProfileSO ResolveRuntimeVfxProfile(BattleIntroRequest request)
        {
            if (request.vfxProfile != null)
                return request.vfxProfile;

            if (fallbackVfxProfile != null)
                return fallbackVfxProfile;

            if (_runtimeFallbackProfile == null)
                _runtimeFallbackProfile = BattleIntroVfxProfileSO.CreateRuntimeFallback();

            return _runtimeFallbackProfile;
        }

        private bool CanPlayVfxIntro()
        {
            EnsureVfxReferences();
            return messagePanel != null && messageText != null && _panelRect != null;
        }

        private void PrepareVfxState(string content, BattleIntroVfxProfileSO profile)
        {
            CacheBaseState();
            KillVfx();
            EnsureVfxReferences();
            AlignParticleBurstRenderer(topEdgeParticleBurst);
            AlignParticleBurstRenderer(bottomEdgeParticleBurst);

            messagePanel.gameObject.SetActive(true);
            messagePanel.blocksRaycasts = false;
            messagePanel.interactable = false;
            messagePanel.alpha = 1f;

            if (_panelRect != null)
            {
                CombatBannerStripMotion.ApplyEnterStart(
                    _panelRect,
                    _basePanelAnchoredPosition,
                    _basePanelScale,
                    CombatBannerStripMotion.Settings.FromIntroProfile(profile));
            }

            if (bannerBackdrop != null)
            {
                bannerBackdrop.color = profile.panelTint;
                bannerBackdrop.raycastTarget = false;
            }

            // Keep authored body color; name accent is embedded via TMP rich-text tags.
            messageText.raycastTarget = false;
            messageText.richText = true;
            messageText.color = _baseTextColor;
            messageText.text = content;
            messageText.maxVisibleCharacters = 0;
            ApplyTextMaterialEmphasis(messageText, profile);

            ResetGlintState();
            ClearParticles();
        }

        private Sequence BuildEntrySequence(BattleIntroVfxProfileSO profile)
        {
            CombatBannerStripMotion.Settings settings = CombatBannerStripMotion.Settings.FromIntroProfile(profile);
            Sequence sequence = CombatBannerStripMotion.BuildEnter(
                    _panelRect,
                    _basePanelAnchoredPosition,
                    _basePanelScale,
                    settings,
                    useUnscaledTime)
                .SetTarget(this);

            if (messagePanel != null && messagePanel.alpha < 0.999f)
                sequence.Insert(0f, messagePanel.DOFade(1f, Mathf.Max(0.04f, settings.slideInDuration)).SetEase(Ease.Linear));

            return sequence;
        }

        private Sequence BuildExitSequence(BattleIntroVfxProfileSO profile)
        {
            CombatBannerStripMotion.Settings settings = CombatBannerStripMotion.Settings.FromIntroProfile(profile);
            CanvasGroup contents = ResolveIntroContentsGroup();
            Sequence sequence = CombatBannerStripMotion.BuildExit(
                    _panelRect,
                    contents,
                    _basePanelAnchoredPosition,
                    _basePanelScale,
                    settings,
                    useUnscaledTime)
                .SetTarget(this);

            if (bannerGlint != null)
                sequence.Join(bannerGlint.DOFade(0f, Mathf.Max(0.04f, settings.collapseDuration)));

            return sequence;
        }

        private CanvasGroup ResolveIntroContentsGroup()
        {
            if (messageText == null)
                return null;

            CanvasGroup group = messageText.GetComponent<CanvasGroup>();
            if (group == null)
                group = messageText.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private void PlayTextPunch(BattleIntroVfxProfileSO profile)
        {
            KillTextPunchTweensOnly();
            if (messageText == null || profile.textPunchDuration <= 0f || profile.textPunchScale <= 0f)
                return;

            if (TryPlayTmpAnimatorPunch(profile))
                return;

            Tween fallbackTween = messageText.transform
                .DOPunchScale(Vector3.one * profile.textPunchScale, profile.textPunchDuration, 6, 0.75f)
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);
            _textPunchTweens.Add(fallbackTween);
        }

        private bool TryPlayTmpAnimatorPunch(BattleIntroVfxProfileSO profile)
        {
            if (messageText == null || !messageText.gameObject.activeInHierarchy)
                return false;

            Type animatorType = FindLoadedType("DG.Tweening.DOTweenTMPAnimator");
            if (animatorType == null)
                return false;

            try
            {
                _tmpAnimator = Activator.CreateInstance(animatorType, messageText);
                PropertyInfo textInfoProperty = animatorType.GetProperty("textInfo", BindingFlags.Instance | BindingFlags.Public);
                var textInfo = textInfoProperty?.GetValue(_tmpAnimator) as TMP_TextInfo;
                if (textInfo == null)
                    return false;

                MethodInfo punchMethod = animatorType.GetMethod(
                    "DOPunchCharScale",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(int), typeof(float), typeof(float), typeof(int), typeof(float) },
                    null);

                if (punchMethod == null)
                    return false;

                int charCount = textInfo.characterCount;
                for (int i = 0; i < charCount; i++)
                {
                    if (!textInfo.characterInfo[i].isVisible)
                        continue;

                    object result = punchMethod.Invoke(
                        _tmpAnimator,
                        new object[] { i, profile.textPunchScale, profile.textPunchDuration, 6, 0.75f });

                    if (result is Tween tween)
                    {
                        tween.SetDelay(i * profile.textPunchStagger)
                            .SetUpdate(useUnscaledTime)
                            .SetTarget(this);
                        _textPunchTweens.Add(tween);
                    }
                }

                return _textPunchTweens.Count > 0;
            }
            catch (Exception ex)
            {
                DisposeTmpAnimator();
                if (enableDebugLogs)
                    Debug.LogWarning($"[BattleIntroController] TMP character punch unavailable: {ex.Message}", this);
                return false;
            }
        }

        private void PlayGlintAndBurst(BattleIntroVfxProfileSO profile)
        {
            if (!PlayFrameAnimatorGlint(profile))
                PlayProceduralBannerGlint(profile);

            PlayTextGlint(profile);
            PlayScreenPulse(profile);
        }

        private float ComputeBannerAccentDurationSeconds(BattleIntroVfxProfileSO profile)
        {
            float maxAccent = 0f;
            if (profile.enableTextGlint && profile.textGlintDuration > 0f)
                maxAccent = Mathf.Max(maxAccent, profile.textGlintDelay + profile.textGlintDuration);

            Image glintImage = ResolveBannerGlintImage(profile);
            MonoBehaviour animator = ResolveBannerGlintAnimator(glintImage, profile);
            if (glintImage != null && animator != null && profile.bannerGlintDuration > 0f)
            {
                int frameCount = ResolveBannerSpriteFrameCount(profile, animator);
                float fadeIn;
                float sustain;
                float fadeOut;
                ComputeFrameBannerGlintPhaseDurations(profile, frameCount, out fadeIn, out sustain, out fadeOut);
                maxAccent = Mathf.Max(maxAccent, fadeIn + sustain + fadeOut);
            }
            else if (bannerGlint != null && profile.bannerGlintDuration > 0f)
                maxAccent = Mathf.Max(maxAccent, profile.bannerGlintDuration);

            // Sweeps run in parallel with the typewriter; include span so rapid RevealText cannot advance before emit ends.
            if (profile.enablePixelBurst && profile.pixelBurstCount > 0 && profile.pixelSweepPreset != null)
            {
                float sweepSpan = ResolveSweepDurationSeconds(profile) + profile.pixelSweepPreset.BottomStreamDelaySeconds;
                maxAccent = Mathf.Max(maxAccent, sweepSpan);
            }

            return maxAccent;
        }

        /// <summary>
        /// Effective sweep scatter/travel duration for paired-edge bursts: optional profile override, else preset value.
        /// </summary>
        private static float ResolveSweepDurationSeconds(BattleIntroVfxProfileSO profile)
        {
            if (profile?.pixelSweepPreset == null)
                return 0.02f;
            if (profile.pixelSweepDurationOverrideSeconds > 0f)
                return Mathf.Max(0.02f, profile.pixelSweepDurationOverrideSeconds);
            return Mathf.Max(0.02f, profile.pixelSweepPreset.SweepDurationSeconds);
        }

        private Image ResolveBannerGlintImage(BattleIntroVfxProfileSO profile)
        {
            if (profile != null && profile.bannerGlintImageTarget != null)
                return profile.bannerGlintImageTarget;

            return bannerGlint;
        }

        private MonoBehaviour ResolveBannerGlintAnimator(Image glintImage, BattleIntroVfxProfileSO profile)
        {
            if (profile != null && profile.bannerGlintFrameAnimatorTarget != null)
                return profile.bannerGlintFrameAnimatorTarget;

            MonoBehaviour onSameObject = TryGetUIImageFrameAnimatorComponent(glintImage);
            if (onSameObject != null)
                return onSameObject;

            return bannerGlintAnimator;
        }

        private int ResolveBannerSpriteFrameCount(BattleIntroVfxProfileSO profile, MonoBehaviour animator)
        {
            if (profile.bannerGlintFrames != null && profile.bannerGlintFrames.Count > 0)
                return profile.bannerGlintFrames.Count;

            int count = TryReadUiImageAnimatorSerializedAuthoringFrameCount(animator);
            if (count > 0)
                return Mathf.Max(1, count);

            if (enableDebugLogs)
                Debug.LogWarning("[BattleIntroController] Banner glint has no sprites (profile list empty, UIImageFrameAnimator has 0 frames).", this);
            return 1;
        }

        private static float FrameCountToPlaybackSeconds(int frameCount, float fps)
        {
            fps = Mathf.Max(1f, fps);
            if (frameCount <= 1)
                return 0.05f;

            return frameCount / fps;
        }

        private void ComputeFrameBannerGlintPhaseDurations(
            BattleIntroVfxProfileSO profile,
            int frameCount,
            out float fadeIn,
            out float sustain,
            out float fadeOut)
        {
            float fps = Mathf.Max(1f, profile.bannerGlintFps);
            float playback = FrameCountToPlaybackSeconds(frameCount, fps);
            float minSustain = playback + Mathf.Max(0f, profile.bannerGlintFrameTailSeconds);
            sustain = Mathf.Max(profile.bannerGlintDuration, minSustain);
            fadeIn = Mathf.Clamp(sustain * BannerFrameGlintFadeInRatio, 0.04f, 0.18f);
            fadeOut = Mathf.Clamp(sustain * BannerFrameGlintFadeOutRatio, 0.04f, 0.18f);
        }

        private bool PlayFrameAnimatorGlint(BattleIntroVfxProfileSO profile)
        {
            Image glintImage = ResolveBannerGlintImage(profile);
            MonoBehaviour animator = ResolveBannerGlintAnimator(glintImage, profile);
            if (glintImage == null || animator == null || profile.bannerGlintDuration <= 0f)
                return false;

            int frameCount = ResolveBannerSpriteFrameCount(profile, animator);
            float fadeIn;
            float sustain;
            float fadeOut;
            ComputeFrameBannerGlintPhaseDurations(profile, frameCount, out fadeIn, out sustain, out fadeOut);

            _runtimeBannerGlintImage = glintImage;
            _runtimeBannerGlintAnimator = animator;

            glintImage.raycastTarget = false;
            glintImage.enabled = true;
            glintImage.gameObject.SetActive(true);

            Color color = Color.white;
            color.a = 0f;
            glintImage.color = color;

            InvokeFrameAnimator(animator, "SetFPS", profile.bannerGlintFps);
            InvokeFrameAnimator(animator, "SetLoop", false);

            if (profile.bannerGlintFrames != null && profile.bannerGlintFrames.Count > 0)
                InvokeFrameAnimator(animator, "Play", profile.bannerGlintFrames);
            else
                InvokeFrameAnimator(animator, "Play", null);

            var sequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);
            sequence.Append(glintImage.DOFade(profile.bannerGlintColor.a, fadeIn));
            sequence.AppendInterval(sustain);
            sequence.Append(glintImage.DOFade(0f, fadeOut));
            _introAccentTweens.Add(sequence);

            return true;
        }

        private void PlayProceduralBannerGlint(BattleIntroVfxProfileSO profile)
        {
            if (bannerGlint == null || profile.bannerGlintDuration <= 0f)
                return;

            _runtimeBannerGlintImage = bannerGlint;
            _runtimeBannerGlintAnimator = null;

            RectTransform glintRect = bannerGlint.rectTransform;
            RectTransform parentRect = bannerRoot != null ? bannerRoot : _panelRect;
            float parentWidth = parentRect != null ? Mathf.Max(parentRect.rect.width, 1f) : 640f;
            float halfDistance = parentWidth * 0.58f;

            glintRect.anchorMin = new Vector2(0.5f, 0f);
            glintRect.anchorMax = new Vector2(0.5f, 1f);
            glintRect.pivot = new Vector2(0.5f, 0.5f);
            glintRect.sizeDelta = new Vector2(profile.bannerGlintWidth, 0f);
            glintRect.anchoredPosition = new Vector2(-halfDistance, 0f);

            Color color = profile.bannerGlintColor;
            color.a = 0f;
            bannerGlint.color = color;
            bannerGlint.raycastTarget = false;
            bannerGlint.enabled = true;
            bannerGlint.gameObject.SetActive(true);

            var sequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);
            sequence.Append(bannerGlint.DOFade(profile.bannerGlintColor.a, profile.bannerGlintDuration * 0.2f));
            sequence.Join(glintRect.DOAnchorPosX(halfDistance, profile.bannerGlintDuration).SetEase(Ease.OutCubic));
            sequence.Append(bannerGlint.DOFade(0f, profile.bannerGlintDuration * 0.25f));
            _introAccentTweens.Add(sequence);
        }

        private void PlayTextGlint(BattleIntroVfxProfileSO profile)
        {
            if (!profile.enableTextGlint || textGlintMask == null || textGlintLabel == null || profile.textGlintDuration <= 0f)
                return;

            TMP_Text sourceText = textGlintSource != null ? textGlintSource : messageText;
            if (sourceText == null)
                return;

            RectTransform labelRect = textGlintLabel.rectTransform;
            RectTransform textRect = sourceText.rectTransform;
            CopyRectTransform(textRect, labelRect);
            CopyTmpSettings(sourceText, textGlintLabel);
            textGlintLabel.text = sourceText.text;
            textGlintLabel.maxVisibleCharacters = sourceText.text == null ? 0 : sourceText.text.Length;
            textGlintLabel.color = profile.textGlintColor;
            textGlintLabel.raycastTarget = false;

            RectTransform maskRect = textGlintMask;
            maskRect.gameObject.SetActive(true);
            maskRect.SetAsLastSibling();
            maskRect.anchorMin = textRect.anchorMin;
            maskRect.anchorMax = textRect.anchorMax;
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.sizeDelta = new Vector2(profile.textGlintWidth, textRect.rect.height);

            float sweepWidth = Mathf.Max(textRect.rect.width, 1f) + profile.textGlintWidth;
            maskRect.anchoredPosition = textRect.anchoredPosition + new Vector2(-sweepWidth * 0.5f, 0f);

            var sequence = DOTween.Sequence()
                .SetDelay(profile.textGlintDelay)
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);
            sequence.Append(maskRect.DOAnchorPosX(textRect.anchoredPosition.x + sweepWidth * 0.5f, profile.textGlintDuration).SetEase(Ease.InOutCubic));
            sequence.AppendCallback(() => maskRect.gameObject.SetActive(false));
            _introAccentTweens.Add(sequence);
        }

        private void PlayScreenPulse(BattleIntroVfxProfileSO profile)
        {
            if (!profile.enableScreenPulse || screenPulseOverlay == null || profile.screenPulseDuration <= 0f)
                return;

            screenPulseOverlay.raycastTarget = false;
            screenPulseOverlay.enabled = true;
            screenPulseOverlay.gameObject.SetActive(true);

            Color color = profile.screenPulseColor;
            color.a = 0f;
            screenPulseOverlay.color = color;

            float halfDuration = profile.screenPulseDuration * 0.5f;
            var sequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);
            sequence.Append(screenPulseOverlay.DOFade(profile.screenPulseColor.a, halfDuration));
            sequence.Append(screenPulseOverlay.DOFade(0f, halfDuration));
            sequence.AppendCallback(() => screenPulseOverlay.gameObject.SetActive(false));
            _introAccentTweens.Add(sequence);
        }

        private void EmitPixelBursts(BattleIntroVfxProfileSO profile)
        {
            int count = Mathf.Max(0, profile.pixelBurstCount);
            if (count <= 0)
                return;

            RestoreSweepEmitterAnchors();

            Canvas.ForceUpdateCanvases();

            AlignParticleBurstRenderer(topEdgeParticleBurst);
            AlignParticleBurstRenderer(bottomEdgeParticleBurst);

            RectTransform sweepLayoutCard = bannerRoot != null ? bannerRoot : _panelRect;

            if (profile.pixelSweepPreset != null)
            {
                CaptureSweepEmitterAnchorsForBurst();

                if (_dualSweepBurstRoutine != null)
                {
                    StopCoroutine(_dualSweepBurstRoutine);
                    _dualSweepBurstRoutine = null;
                }

                ComputeSweepBurstScales(profile, out float lifetimeScale, out float speedScale);
                float sweepDurationSeconds = ResolveSweepDurationSeconds(profile);

                _dualSweepBurstRoutine = StartCoroutine(
                    CoDualSweepBurst(
                        profile.pixelSweepPreset,
                        profile.pixelBurstColor,
                        count,
                        sweepLayoutCard,
                        sweepDurationSeconds,
                        lifetimeScale,
                        speedScale));
                return;
            }

            EmitLegacySplitBursts(profile, count);
        }

        /// <summary>
        /// With a sweep preset, profile lifetime/speed are treated as scales vs Inspector defaults (1.0× = default profile values).
        /// Legacy single-frame burst still uses absolute lifetime/speed from the profile.
        /// </summary>
        private static void ComputeSweepBurstScales(BattleIntroVfxProfileSO profile, out float lifetimeScale, out float speedScale)
        {
            lifetimeScale = Mathf.Max(0.01f, profile.pixelBurstLifetime / DefaultSweepProfileLifetime);
            speedScale = Mathf.Max(0.01f, profile.pixelBurstSpeed / DefaultSweepProfileSpeed);
        }

        private void CaptureSweepEmitterAnchorsForBurst()
        {
            _sweepEmitterAnchorsCaptured = true;

            if (topEdgeParticleBurst != null && topEdgeParticleBurst.transform is RectTransform topRt)
                _sweepTopAnchoredRestore = topRt.anchoredPosition;

            if (bottomEdgeParticleBurst != null && bottomEdgeParticleBurst.transform is RectTransform bottomRt)
                _sweepBottomAnchoredRestore = bottomRt.anchoredPosition;
        }

        private void RestoreSweepEmitterAnchors()
        {
            if (!_sweepEmitterAnchorsCaptured)
                return;

            if (topEdgeParticleBurst != null && topEdgeParticleBurst.transform is RectTransform topRt)
                topRt.anchoredPosition = _sweepTopAnchoredRestore;

            if (bottomEdgeParticleBurst != null && bottomEdgeParticleBurst.transform is RectTransform bottomRt)
                bottomRt.anchoredPosition = _sweepBottomAnchoredRestore;

            _sweepEmitterAnchorsCaptured = false;
        }

        private IEnumerator CoDualSweepBurst(
            UiSweepParticlePresetSO preset,
            Color contextualTint,
            int totalBudget,
            RectTransform sweepLayoutCard,
            float sweepDurationSeconds,
            float burstLifetimeScale,
            float burstSpeedScale)
        {
            try
            {
                IEnumerator sweep = UiDualSweepParticleBurst.CoPlay(
                    topEdgeParticleBurst,
                    bottomEdgeParticleBurst,
                    preset,
                    contextualTint,
                    totalBudget,
                    useUnscaledTime,
                    sweepLayoutCard,
                    sweepDurationSeconds,
                    burstLifetimeScale,
                    burstSpeedScale);

                while (sweep.MoveNext())
                    yield return sweep.Current;
            }
            finally
            {
                RestoreSweepEmitterAnchors();
                _dualSweepBurstRoutine = null;
            }
        }

        private void EmitLegacySplitBursts(BattleIntroVfxProfileSO profile, int count)
        {
            EmitLegacyPixelBurst(topEdgeParticleBurst, profile, Mathf.CeilToInt(count * 0.5f));
            EmitLegacyPixelBurst(bottomEdgeParticleBurst, profile, Mathf.FloorToInt(count * 0.5f));
        }

        private static void EmitLegacyPixelBurst(ParticleSystem particleSystem, BattleIntroVfxProfileSO profile, int count)
        {
            if (particleSystem == null || count <= 0)
                return;

            particleSystem.gameObject.SetActive(true);
            var main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Mathf.Max(profile.pixelBurstLifetime, 0.05f);
            main.startLifetime = profile.pixelBurstLifetime;
            main.startSpeed = profile.pixelBurstSpeed;
            main.startColor = profile.pixelBurstColor;
            main.maxParticles = Mathf.Max(main.maxParticles, count);

            var emission = particleSystem.emission;
            emission.enabled = false;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
            particleSystem.Emit(count);
        }

        private IEnumerator RevealText(string content)
        {
            if (messageText == null)
                yield break;

            if (string.IsNullOrEmpty(content))
            {
                messageText.maxVisibleCharacters = 0;
                yield break;
            }

            messageText.richText = true;
            messageText.text = content;
            messageText.ForceMeshUpdate(true);
            // Use parsed character count so rich-text tags do not inflate the typewriter length.
            int totalChars = messageText.textInfo != null
                ? Mathf.Max(0, messageText.textInfo.characterCount)
                : content.Length;
            float revealSpeed = Mathf.Max(1f, typewriterCharsPerSecond);
            float visibleChars = 0f;
            messageText.maxVisibleCharacters = 0;

            while (messageText.maxVisibleCharacters < totalChars)
            {
                if (_skipRequested)
                    break;

                visibleChars += GetDeltaTime() * revealSpeed;
                messageText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(visibleChars), 0, totalChars);
                yield return null;
            }

            messageText.maxVisibleCharacters = totalChars;
        }

        private void ApplyCopySpanStyles(BattleIntroRequest request)
        {
            _activeCopyConfig = request.copyConfig;
            _activeCopySpans = request.copySpans;
            SubscribeCopySpanPreRender();
            BattleIntroCopySpanApplier.Apply(messageText, _activeCopyConfig, _activeCopySpans);
        }

        private void SubscribeCopySpanPreRender()
        {
            if (_copySpanPreRenderSubscribed || messageText == null)
                return;

            messageText.OnPreRenderText += HandleCopySpanPreRender;
            _copySpanPreRenderSubscribed = true;
        }

        private void UnsubscribeCopySpanPreRender()
        {
            if (!_copySpanPreRenderSubscribed)
                return;

            if (messageText != null)
                messageText.OnPreRenderText -= HandleCopySpanPreRender;

            _copySpanPreRenderSubscribed = false;
            _activeCopyConfig = null;
            _activeCopySpans = null;
        }

        private void HandleCopySpanPreRender(TMP_TextInfo textInfo)
        {
            BattleIntroCopySpanApplier.Paint(textInfo, _activeCopyConfig, _activeCopySpans);
        }

        private IEnumerator WaitForDelay(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_skipRequested)
                    yield break;

                elapsed += GetDeltaTime();
                yield return null;
            }
        }

        private IEnumerator FadePanel(float from, float to)
        {
            if (messagePanel == null)
                yield break;

            if (fadeDuration <= 0f)
            {
                messagePanel.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            messagePanel.alpha = from;
            while (elapsed < fadeDuration)
            {
                elapsed += GetDeltaTime();
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                messagePanel.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            messagePanel.alpha = to;
        }

        private IEnumerator WaitForTweenOrSkip(Tween tween)
        {
            while (!_skipRequested && tween != null && tween.IsActive() && !tween.IsComplete())
                yield return null;

            if (_skipRequested && tween != null && tween.IsActive())
                tween.Kill(false);
        }

        private void CompleteIntro()
        {
            if (enableDebugLogs)
                Debug.Log("[BattleIntroController] CompleteIntro() called.", this);

            _waitingForExternalIntro = false;
            _skipRequested = false;
            _allowSkipCurrent = true;
            _activeRoutine = null;
            UnsubscribeCopySpanPreRender();
            HidePanelImmediate();
            OnIntroComplete?.Invoke();
        }

        private void HidePanelImmediate()
        {
            KillVfx();

            if (messageText != null)
            {
                messageText.text = string.Empty;
                messageText.maxVisibleCharacters = 0;
                messageText.color = _baseTextColor;
                CanvasGroup textGroup = messageText.GetComponent<CanvasGroup>();
                if (textGroup != null)
                    textGroup.alpha = 1f;
            }

            if (bannerBackdrop != null)
                bannerBackdrop.color = _baseBackdropColor;

            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = _basePanelAnchoredPosition;
                _panelRect.localScale = _basePanelScale;
            }

            if (messagePanel == null)
                return;

            messagePanel.alpha = 0f;
            messagePanel.blocksRaycasts = false;
            messagePanel.interactable = false;
            // Do NOT call SetActive(false) - messagePanel is on the same GameObject as this controller.
            // Deactivating it would prevent StartCoroutine from running when PlayExternalIntro/PlayLocalIntro is called.
        }

        private void ApplyEditorPreviewState()
        {
            if (previewInEditMode)
            {
                if (messageText != null)
                {
                    messageText.text = previewMessage ?? string.Empty;
                    messageText.maxVisibleCharacters = string.IsNullOrEmpty(previewMessage) ? 0 : previewMessage.Length;
                }

                if (messagePanel != null)
                {
                    messagePanel.alpha = 1f;
                    messagePanel.blocksRaycasts = false;
                    messagePanel.interactable = false;
                }

                return;
            }

            if (hidePanelOnAwake)
                HidePanelImmediate();
        }

        private void CacheBaseState()
        {
            _panelRect = ResolvePanelRect();

            if (_panelRect != null)
            {
                _basePanelAnchoredPosition = _panelRect.anchoredPosition;
                _basePanelScale = _panelRect.localScale;
            }

            if (messageText != null)
                _baseTextColor = messageText.color;

            if (bannerBackdrop == null)
                bannerBackdrop = ResolveBannerBackdrop();

            if (bannerBackdrop != null)
                _baseBackdropColor = bannerBackdrop.color;
        }

        private RectTransform ResolvePanelRect()
        {
            if (bannerRoot != null)
                return bannerRoot;

            if (messagePanel == null)
                return null;

            Transform introCard = messagePanel.transform.Find("IntroCard");
            if (introCard != null)
                return introCard as RectTransform;

            if (messageText != null && messageText.transform.parent is RectTransform parentRect)
                return parentRect;

            return messagePanel.transform as RectTransform;
        }

        private Image ResolveBannerBackdrop()
        {
            RectTransform root = bannerRoot != null ? bannerRoot : _panelRect;
            return root != null ? root.GetComponent<Image>() : null;
        }

        private void EnsureVfxReferences()
        {
            if (messagePanel == null)
                return;

            if (bannerRoot == null)
                bannerRoot = ResolvePanelRect();

            if (_panelRect == null)
                _panelRect = ResolvePanelRect();

            if (bannerBackdrop == null)
                bannerBackdrop = ResolveBannerBackdrop();

            if (!scaffoldMissingVfxAtRuntime || !Application.isPlaying)
                return;

            Transform root = bannerRoot != null ? bannerRoot : messagePanel.transform;

            if (bannerGlint == null)
                bannerGlint = CreateImageChild(root, "BannerGlint", true, setAsFirstSibling: false);

            if (textGlintMask == null)
                textGlintMask = CreateTextGlint(root);

            if (textGlintLabel == null && textGlintMask != null)
                textGlintLabel = textGlintMask.GetComponentInChildren<TMP_Text>(true);

            if (screenPulseOverlay == null)
            {
                screenPulseOverlay = CreateImageChild(messagePanel.transform, "ScreenPulseOverlay", true, setAsFirstSibling: true);
                screenPulseOverlay.gameObject.SetActive(false);
            }

            if (topEdgeParticleBurst == null)
            {
                topEdgeParticleBurst = CreateEdgeSweepParticle(
                    root,
                    "TopEdgeSweepBurst",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    Vector3.zero);
            }

            if (bottomEdgeParticleBurst == null)
            {
                bottomEdgeParticleBurst = CreateEdgeSweepParticle(
                    root,
                    "BottomEdgeSweepBurst",
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector3(0f, 0f, 180f));
            }

            SetGraphicRaycastTargetsOff();
        }

        private Image CreateImageChild(Transform parent, string childName, bool stretch, bool setAsFirstSibling)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            if (setAsFirstSibling)
                go.transform.SetAsFirstSibling();
            else
                go.transform.SetAsLastSibling();

            var rect = (RectTransform)go.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }

            Image image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateTextGlint(Transform parent)
        {
            var maskObject = new GameObject("TextGlintMask", typeof(RectTransform), typeof(RectMask2D));
            maskObject.transform.SetParent(parent, false);
            maskObject.SetActive(false);

            RectTransform maskRect = (RectTransform)maskObject.transform;
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);

            var labelObject = new GameObject("TextGlintLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(maskObject.transform, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            TMP_Text sourceText = textGlintSource != null ? textGlintSource : messageText;
            if (sourceText != null)
                CopyTmpSettings(sourceText, label);
            label.raycastTarget = false;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            return maskRect;
        }

        private ParticleSystem CreateEdgeSweepParticle(Transform parent, string childName, Vector2 anchor, Vector2 pivot, Vector3 localEuler)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(ParticleSystem));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localEulerAngles = localEuler;

            ParticleSystem particleSystem = go.GetComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startSize = 4f;
            main.startLifetime = 0.24f;
            main.startSpeed = 70f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particleSystem.emission;
            emission.enabled = false;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(0f, 0f, 0f);

            AlignParticleBurstRenderer(particleSystem);
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particleSystem;
        }

        private void AlignParticleBurstRenderer(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
                return;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                return;

            Canvas host = messagePanel != null ? messagePanel.GetComponentInParent<Canvas>() : null;
            if (host == null)
                host = particleSystem.GetComponentInParent<Canvas>();

            Canvas rootCanvas = host != null ? host.rootCanvas : host;
            if (rootCanvas != null)
            {
                particleSystem.gameObject.layer = rootCanvas.gameObject.layer;
                renderer.sortingLayerID = rootCanvas.sortingLayerID;
                renderer.sortingOrder = rootCanvas.sortingOrder + particleBurstSortingOffset;
            }

            // Scene view tooling can imply visibility even when Overlay + URP skips null-material draws in GameCam.
            if (renderer.sharedMaterial == null)
            {
                Material fallback = ResolveBurstParticleOverlayMaterial();
                if (fallback != null)
                    renderer.sharedMaterial = fallback;
            }

            renderer.allowOcclusionWhenDynamic = false;
        }

        private static Material ResolveBurstParticleOverlayMaterial()
        {
            if (_burstParticleOverlayMaterialCache != null)
                return _burstParticleOverlayMaterialCache;

            string[] shaderCandidates =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Universal Render Pipeline/Particles/Simple Lit",
                "Particles/Unlit",
                "Legacy Shaders/Particles/Alpha Blended Premultiply",
                "Sprites/Default",
            };

            for (int i = 0; i < shaderCandidates.Length; i++)
            {
                Shader shader = Shader.Find(shaderCandidates[i]);
                if (shader == null)
                    continue;

                _burstParticleOverlayMaterialCache = new Material(shader)
                {
                    name = "_Runtime_UI_BurstParticles",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return _burstParticleOverlayMaterialCache;
            }

            return null;
        }

        private static void ResetBannerGlintImageState(Image image)
        {
            if (image == null)
                return;

            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.raycastTarget = false;
            image.enabled = true;
        }

        private void ResetGlintState()
        {
            Image bannerA = _runtimeBannerGlintImage;
            Image bannerB = bannerGlint;
            ResetBannerGlintImageState(bannerA);
            if (bannerB != null && bannerB != bannerA)
                ResetBannerGlintImageState(bannerB);

            if (textGlintMask != null)
                textGlintMask.gameObject.SetActive(false);

            if (textGlintLabel != null)
            {
                textGlintLabel.text = string.Empty;
                textGlintLabel.maxVisibleCharacters = 0;
                textGlintLabel.raycastTarget = false;
            }

            if (screenPulseOverlay != null)
            {
                Color color = screenPulseOverlay.color;
                color.a = 0f;
                screenPulseOverlay.color = color;
                screenPulseOverlay.raycastTarget = false;
                screenPulseOverlay.gameObject.SetActive(false);
            }
        }

        private void ClearParticles()
        {
            ClearParticleSystem(topEdgeParticleBurst);
            ClearParticleSystem(bottomEdgeParticleBurst);
        }

        private static void ClearParticleSystem(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
                return;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }

        private void KillVfx()
        {
            if (_activeVfxSequence != null && _activeVfxSequence.IsActive())
                _activeVfxSequence.Kill(false);
            _activeVfxSequence = null;

            if (_dualSweepBurstRoutine != null)
            {
                StopCoroutine(_dualSweepBurstRoutine);
                _dualSweepBurstRoutine = null;
            }

            RestoreSweepEmitterAnchors();

            KillAllIntroTweens();
            StopResolvedBannerFrameAnimator();
            ResetGlintState();
            ClearParticles();
            _runtimeBannerGlintAnimator = null;
            _runtimeBannerGlintImage = null;
        }

        private void KillAllIntroTweens()
        {
            KillTextPunchTweensOnly();
            KillIntroAccentTweens();
        }

        /// <summary>Clears punch tweens only (and TMP DOTween animator). Safe to call while banner/text glints are playing.</summary>
        private void KillTextPunchTweensOnly()
        {
            for (int i = 0; i < _textPunchTweens.Count; i++)
            {
                Tween tween = _textPunchTweens[i];
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
            }

            _textPunchTweens.Clear();
            DisposeTmpAnimator();
        }

        private void KillIntroAccentTweens()
        {
            for (int i = 0; i < _introAccentTweens.Count; i++)
            {
                Tween tween = _introAccentTweens[i];
                if (tween != null && tween.IsActive())
                    tween.Kill(false);
            }

            _introAccentTweens.Clear();
        }

        private void DisposeTmpAnimator()
        {
            if (_tmpAnimator == null)
                return;

            Type type = _tmpAnimator.GetType();
            type.GetMethod("Reset", BindingFlags.Instance | BindingFlags.Public)?.Invoke(_tmpAnimator, null);
            type.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public)?.Invoke(_tmpAnimator, null);
            _tmpAnimator = null;
        }

        private void StopResolvedBannerFrameAnimator()
        {
            MonoBehaviour target = _runtimeBannerGlintAnimator != null ? _runtimeBannerGlintAnimator : bannerGlintAnimator;
            if (target != null)
                InvokeFrameAnimator(target, "Stop", null);
        }

        private void InvokeFrameAnimator(MonoBehaviour target, string methodName, object argument)
        {
            if (target == null)
                return;

            try
            {
                Type type = target.GetType();
                MethodInfo method;
                object[] args;

                if (argument == null)
                {
                    method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    args = Array.Empty<object>();
                    if (method == null && string.Equals(methodName, "Play", StringComparison.Ordinal))
                    {
                        method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(List<Sprite>) }, null);
                        args = new object[] { null };
                    }
                }
                else
                {
                    method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, new[] { argument.GetType() }, null);
                    args = new[] { argument };
                }

                method?.Invoke(target, args);
            }
            catch (Exception ex)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[BattleIntroController] Failed to call {methodName} on frame animator: {ex.Message}", this);
            }
        }

        private static Type _cachedUiImageFrameAnimatorType;

        private static Type ResolveUiImageFrameAnimatorType()
        {
            return _cachedUiImageFrameAnimatorType ??= FindLoadedType("UIImageFrameAnimator");
        }

        private static MonoBehaviour TryGetUIImageFrameAnimatorComponent(Image image)
        {
            if (image == null)
                return null;

            Type componentType = ResolveUiImageFrameAnimatorType();
            if (componentType == null)
                return null;

            Component found = image.GetComponent(componentType);
            return found as MonoBehaviour;
        }

        private static bool BehaviourIsUIImageFrameAnimator(MonoBehaviour animator)
        {
            if (animator == null)
                return false;

            Type expected = ResolveUiImageFrameAnimatorType();
            Type actual = animator.GetType();
            if (expected != null)
                return expected.IsAssignableFrom(actual);

            return string.Equals(actual.Name, "UIImageFrameAnimator", StringComparison.Ordinal);
        }

        private static int TryReadUiImageAnimatorSerializedAuthoringFrameCount(MonoBehaviour animator)
        {
            if (!BehaviourIsUIImageFrameAnimator(animator))
                return 0;

            PropertyInfo prop = animator.GetType().GetProperty(
                "SerializedAuthoringFrameCount",
                BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || prop.PropertyType != typeof(int))
                return 0;

            object value = prop.GetValue(animator);
            return value is int count ? count : 0;
        }

        private static Type FindLoadedType(string fullName)
        {
            Type type = Type.GetType(fullName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private void SetGraphicRaycastTargetsOff()
        {
            if (bannerBackdrop != null) bannerBackdrop.raycastTarget = false;
            if (bannerGlint != null) bannerGlint.raycastTarget = false;
            if (screenPulseOverlay != null) screenPulseOverlay.raycastTarget = false;
            if (messageText != null) messageText.raycastTarget = false;
            if (textGlintLabel != null) textGlintLabel.raycastTarget = false;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
                return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyTmpSettings(TMP_Text source, TMP_Text target)
        {
            if (source == null || target == null)
                return;

            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.fontSize = source.fontSize;
            target.fontSizeMin = source.fontSizeMin;
            target.fontSizeMax = source.fontSizeMax;
            target.enableAutoSizing = source.enableAutoSizing;
            target.alignment = source.alignment;
            target.fontStyle = source.fontStyle;
            target.textWrappingMode = source.textWrappingMode;
            target.overflowMode = source.overflowMode;
            target.margin = source.margin;
        }

        private static void ApplyTextMaterialEmphasis(TMP_Text text, BattleIntroVfxProfileSO profile)
        {
            if (text == null || profile == null)
                return;

            text.outlineColor = profile.textOutlineColor;
            text.outlineWidth = profile.textOutlineWidth;
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
