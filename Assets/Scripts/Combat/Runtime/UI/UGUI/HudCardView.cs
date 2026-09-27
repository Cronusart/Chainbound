using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class HudCardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image typeIconImage;
        [SerializeField] private Image hpBackImage;
        [SerializeField] private Image hpLagFillImage;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Image hpFrameOverlayImage;
        [SerializeField] private Image captureLowHpMarkerImage;
        [SerializeField] private TMP_Text captureLowHpLabel;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private RectTransform apPipRoot;
        [SerializeField] private Image apPipTemplate;
        [SerializeField] private RectTransform starBarRoot;
        [SerializeField] private RectTransform statusIconRoot;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Image activeTurnOutline;
        [SerializeField] private Image targetOutline;
        [SerializeField] private RectTransform activePulseTarget;
        [SerializeField] private RectTransform targetPulseTarget;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CombatHudAnimator hudAnimator;
        [SerializeField] private CombatHudCardBackgroundState backgroundState;

        [Header("Presentation")]
        [SerializeField] private bool autoBuildIfMissing = true;
        [SerializeField] private int defaultMaxApPips = 10;
        [SerializeField] private float koAlpha = 0.45f;
        [SerializeField] private Color hpHighColor = new Color(0.20f, 0.80f, 0.40f, 1f);
        [SerializeField] private Color hpLowColor = new Color(0.86f, 0.24f, 0.20f, 1f);
        [SerializeField] private Color hpDamageLagColor = new Color(1f, 0.78f, 0.32f, 0.65f);
        [SerializeField] private float hpDamageLagDelay = 0.10f;
        [SerializeField] private float hpDamageLagDuration = 0.28f;
        [SerializeField] private Ease hpDamageLagEase = Ease.OutCubic;
        [Header("AP Pips (sprite swap)")]
        [Tooltip("Sprite for active/usable AP cells. If null, falls back to apPipOnColor tint.")]
        [SerializeField] private Sprite apPipActiveSprite;
        [Tooltip("Sprite for dormant/inactive AP cells. If null, falls back to apPipOffColor tint.")]
        [SerializeField] private Sprite apPipInactiveSprite;
        [Tooltip("Spacing between AP pip centers in layout units.")]
        [SerializeField] private float apPipSpacing = 16f;
        [Tooltip("Width of each AP pip in layout units. Use for pixel alignment with other UI.")]
        [SerializeField] private float apPipSize = 14f;
        [Tooltip("Fallback colors when sprites are not assigned.")]
        [SerializeField] private Color apPipOnColor = new Color(0.16f, 0.76f, 0.98f, 1f);
        [SerializeField] private Color apPipOffColor = new Color(0.24f, 0.24f, 0.24f, 0.75f);
        [SerializeField] private Color activeOutlineColor = new Color(0.95f, 0.89f, 0.22f, 0.22f);
        [SerializeField] private Color targetOutlineColor = new Color(0.95f, 0.33f, 0.33f, 0.22f);
        [Header("AP Grant Animation (when AP increases each round)")]
        [Tooltip("Duration of the bounce effect when AP is granted.")]
        [SerializeField] private float apGrantBounceDuration = 0.25f;
        [Tooltip("Peak scale during bounce (1 = no scale change).")]
        [SerializeField] private float apGrantBounceScale = 1.15f;
        [Tooltip("Duration of the fade-in effect for newly granted AP pips.")]
        [SerializeField] private float apGrantFadeDuration = 0.2f;

        [Header("Impact Reactions")]
        [Tooltip("Optional AnchorContainer for position shake/heal. Use when the card root is under a GridLayoutGroup (layout overwrites root position). Pulse always uses the card root so the frame Image scales. Defaults to this card's RectTransform.")]
        [SerializeField] private RectTransform shakeTarget;
        [Tooltip("Pixel intensity of the shake on damage.")]
        [SerializeField] private float damageShakeIntensity = 6f;
        [Tooltip("Duration of the damage shake.")]
        [SerializeField] private float damageShakeDuration = 0.18f;
        [Tooltip("Vibrato (shakes per second) of the damage shake.")]
        [SerializeField] private int damageShakeVibrato = 25;
        [Tooltip("Pixels to ascend on heal.")]
        [SerializeField] private float healAscendDistance = 8f;
        [Tooltip("Duration of the heal ascend + return.")]
        [SerializeField] private float healAscendDuration = 0.35f;
        [Tooltip("Peak scale pulse during heal ascend.")]
        [SerializeField] private float healPulseScale = 1.03f;

        [Header("Star Bar (0-5 stars, replaces level text)")]
        [Tooltip("Sprite for each star icon. Assign to enable star bar.")]
        [SerializeField] private Sprite starSprite;
        [Tooltip("Spacing between star icons in layout units.")]
        [SerializeField] private float starSpacing = 4f;
        [Tooltip("Size of each star icon (width and height).")]
        [SerializeField] private float starIconSize = 16f;
        [Tooltip("Optional 9-sliced background sprite. Bar expands to fit star count.")]
        [SerializeField] private Sprite starBarBackgroundSprite;

        private readonly List<Image> _apPips = new List<Image>(16);
        private readonly List<Image> _starIcons = new List<Image>(5);
        private string _boundInstanceId;
        private int _lastDisplayedAp = -1;
        private readonly List<Tween> _apGrantTweens = new List<Tween>(10);
        private Tween _impactReactionTween;
        private Vector2 _baseAnchoredPosition;
        private bool _hasBaseAnchoredPosition;
        private static Sprite _defaultSprite;
        private Tween _hpLagTween;
        private float _hpTarget01 = -1f;
        private bool _hasHpVisualState;
        private bool _isActiveTurn;
        private bool _isTargeted;
        private bool _hiddenInPlace;
        private bool _hasDefaultBackgroundSprites;
        private Sprite _defaultBackgroundNormal;
        private Sprite _defaultBackgroundActive;
        private Sprite _defaultBackgroundTarget;
        private Sprite _defaultBackgroundActiveTarget;
        private bool _hasDefaultHpFrameOverlaySprites;
        private Sprite _defaultHpFrameOverlayNormal;
        private Sprite _defaultHpFrameOverlayActive;
        private Sprite _defaultHpFrameOverlayTarget;
        private Sprite _defaultHpFrameOverlayActiveTarget;

        public RectTransform RectTransform => transform as RectTransform;
        public string BoundInstanceId => _boundInstanceId;
        public bool IsActiveTurn => _isActiveTurn;
        public bool IsTargeted => _isTargeted;
        public bool IsHiddenInPlace => _hiddenInPlace;

        public void ConfigureBackgroundSprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            EnsureBuilt();
            Sprite resolvedNormal = normal != null
                ? normal
                : _hasDefaultBackgroundSprites
                    ? _defaultBackgroundNormal
                    : (backgroundImage != null ? backgroundImage.sprite : null);
            _defaultBackgroundNormal = resolvedNormal;
            _defaultBackgroundActive = active;
            _defaultBackgroundTarget = target;
            _defaultBackgroundActiveTarget = activeTarget;
            _hasDefaultBackgroundSprites = true;
            ApplyBackgroundSprites(resolvedNormal, active, target, activeTarget);
        }

        private void ApplyBackgroundSprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            if (backgroundState == null)
                backgroundState = GetOrAddComponent<CombatHudCardBackgroundState>(gameObject);
            backgroundState.SetBackgroundImage(backgroundImage);
            backgroundState.SetSprites(normal, active, target, activeTarget);
        }

        /// <summary>
        /// Configure HP frame overlay sprites (shadows/highlights baked per state).
        /// Syncs with HUD card background state — same active/target logic, no lag.
        /// </summary>
        public void ConfigureHpFrameOverlaySprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            EnsureBuilt();
            _defaultHpFrameOverlayNormal = normal;
            _defaultHpFrameOverlayActive = active;
            _defaultHpFrameOverlayTarget = target;
            _defaultHpFrameOverlayActiveTarget = activeTarget;
            _hasDefaultHpFrameOverlaySprites = true;
            ApplyHpFrameOverlaySprites(normal, active, target, activeTarget);
        }

        private void ApplyHpFrameOverlaySprites(Sprite normal, Sprite active, Sprite target, Sprite activeTarget = null)
        {
            if (backgroundState == null)
                backgroundState = GetOrAddComponent<CombatHudCardBackgroundState>(gameObject);
            var overlay = EnsureHpFrameOverlayImage();
            if (overlay != null)
            {
                backgroundState.SetHpFrameOverlayImage(overlay);
                backgroundState.SetHpFrameOverlaySprites(normal, active, target, activeTarget);
            }
        }

        public void ApplyHudCardVariant(ChainlingHudCardVariantDefinitionSO variant)
        {
            EnsureBuilt();
            CaptureDefaultHudCardStateIfNeeded();
            if (variant == null)
            {
                RestoreDefaultHudCardVariant();
                return;
            }

            ApplyBackgroundSprites(
                variant.normalSprite != null ? variant.normalSprite : _defaultBackgroundNormal,
                variant.activeSprite != null ? variant.activeSprite : _defaultBackgroundActive,
                variant.targetSprite != null ? variant.targetSprite : _defaultBackgroundTarget,
                variant.activeTargetSprite != null ? variant.activeTargetSprite : _defaultBackgroundActiveTarget);

            ApplyHpFrameOverlaySprites(
                variant.hpFrameOverlayNormal != null ? variant.hpFrameOverlayNormal : _defaultHpFrameOverlayNormal,
                variant.hpFrameOverlayActive != null ? variant.hpFrameOverlayActive : _defaultHpFrameOverlayActive,
                variant.hpFrameOverlayTarget != null ? variant.hpFrameOverlayTarget : _defaultHpFrameOverlayTarget,
                variant.hpFrameOverlayActiveTarget != null ? variant.hpFrameOverlayActiveTarget : _defaultHpFrameOverlayActiveTarget);
        }

        public void RestoreDefaultHudCardVariant()
        {
            EnsureBuilt();
            if (_hasDefaultBackgroundSprites)
            {
                ApplyBackgroundSprites(
                    _defaultBackgroundNormal,
                    _defaultBackgroundActive,
                    _defaultBackgroundTarget,
                    _defaultBackgroundActiveTarget);
            }

            if (_hasDefaultHpFrameOverlaySprites)
            {
                ApplyHpFrameOverlaySprites(
                    _defaultHpFrameOverlayNormal,
                    _defaultHpFrameOverlayActive,
                    _defaultHpFrameOverlayTarget,
                    _defaultHpFrameOverlayActiveTarget);
            }
        }

        private void CaptureDefaultHudCardStateIfNeeded()
        {
            if (!_hasDefaultBackgroundSprites)
            {
                _defaultBackgroundNormal = backgroundImage != null ? backgroundImage.sprite : null;
                _defaultBackgroundActive = null;
                _defaultBackgroundTarget = null;
                _defaultBackgroundActiveTarget = null;
                _hasDefaultBackgroundSprites = true;
            }

            if (!_hasDefaultHpFrameOverlaySprites)
            {
                _defaultHpFrameOverlayNormal = hpFrameOverlayImage != null ? hpFrameOverlayImage.sprite : null;
                _defaultHpFrameOverlayActive = null;
                _defaultHpFrameOverlayTarget = null;
                _defaultHpFrameOverlayActiveTarget = null;
                _hasDefaultHpFrameOverlaySprites = true;
            }
        }

        private void Awake()
        {
            EnsureBuilt();
            // Pulse uses the card root (frame Image lives here). Shake uses AnchorContainer when set
            // so GridLayoutGroup on the HUD panel cannot overwrite shake offsets.
            if (RectTransform != null)
                RectTransform.localScale = Vector3.one;
            var shakeRoot = GetShakeRoot();
            if (shakeRoot != null && shakeRoot != RectTransform)
                shakeRoot.localScale = Vector3.one;
            CacheBaseAnchoredPosition(shakeRoot);
            ConfigureAnimatorTargets();
            SetActiveTurn(false);
            SetTargeted(false);
        }

        private void OnDisable()
        {
            hudAnimator?.StopAllPulses();
            KillHpLagTween();
            KillApGrantTweens();
            KillImpactReactionTween(restorePulse: false);
        }

        private void Reset()
        {
            EnsureBuilt();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && autoBuildIfMissing)
                EnsureBuilt();
        }

        public void BindChainling(ChainlingInstance instance)
        {
            string nextId = instance != null ? instance.instanceId : string.Empty;
            if (_boundInstanceId != nextId)
                ResetHpVisualState();

            _boundInstanceId = nextId;

            if (instance == null)
            {
                SetEmpty(true);
                return;
            }

            SetEmpty(false);
            _lastDisplayedAp = -1;
            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(instance.displayName) ? "--" : instance.displayName;

            UpdateStarBar(Mathf.Clamp(instance.stars, 0, CombatConstants.STAR_MAX));
            UpdateVitals(instance.displayHP, instance.EffectiveMaxHP, instance.displayAP, CombatConstants.AP_CAP);
            SetKO(!instance.isAlive);
        }

        public void SetTypeIcon(Sprite icon)
        {
            if (typeIconImage == null)
                return;

            typeIconImage.sprite = icon;
            typeIconImage.enabled = icon != null;
        }

        public void UpdateVitals(int currentHP, int maxHP, int currentAP, int maxAP)
        {
            int safeMaxHp = Mathf.Max(1, maxHP);
            int clampedHp = Mathf.Clamp(currentHP, 0, safeMaxHp);
            int safeMaxAp = Mathf.Max(1, maxAP > 0 ? maxAP : defaultMaxApPips);
            int clampedAp = Mathf.Clamp(currentAP, 0, safeMaxAp);
            float hp01 = safeMaxHp > 0 ? (float)clampedHp / safeMaxHp : 0f;

            if (hpText != null)
                hpText.text = $"{clampedHp}/{safeMaxHp}";

            UpdateHpBar(hp01);

            EnsureApPipPool(safeMaxAp);
            for (int i = 0; i < _apPips.Count; i++)
            {
                if (_apPips[i] == null)
                    continue;
                _apPips[i].enabled = i < safeMaxAp;
                bool isActive = i < clampedAp;
                if (isActive && apPipActiveSprite != null)
                {
                    _apPips[i].sprite = apPipActiveSprite;
                    _apPips[i].color = Color.white;
                }
                else if (!isActive && apPipInactiveSprite != null)
                {
                    _apPips[i].sprite = apPipInactiveSprite;
                    _apPips[i].color = Color.white;
                }
                else
                {
                    _apPips[i].color = isActive ? apPipOnColor : apPipOffColor;
                }
            }

            PlayApGrantAnimationIfNeeded(clampedAp);
            _lastDisplayedAp = clampedAp;
        }

        /// <summary>
        /// Scanner-only capture threshold presentation. The marker is anchored to the
        /// HP frame and the label appears once the target crosses the hidden threshold.
        /// </summary>
        public void SetCaptureLowHpMarker(bool visible, int thresholdBasisPoints, bool isLowHp)
        {
            EnsureBuilt();
            RectTransform hpFrame = FindRect("HPFrame");
            if (visible && hpFrame != null)
            {
                float threshold = Mathf.Clamp01(thresholdBasisPoints / 10000f);
                captureLowHpMarkerImage = captureLowHpMarkerImage != null
                    ? captureLowHpMarkerImage
                    : EnsureImage("CaptureLowHpMarker", hpFrame, threshold, 0f, threshold, 1f, new Color(0.20f, 0.92f, 1f, 0.95f));
                RectTransform marker = captureLowHpMarkerImage.rectTransform;
                marker.anchorMin = new Vector2(threshold, 0f);
                marker.anchorMax = new Vector2(threshold, 1f);
                marker.sizeDelta = new Vector2(3f, 0f);
                marker.anchoredPosition = Vector2.zero;
                captureLowHpMarkerImage.color = isLowHp
                    ? new Color(1f, 0.85f, 0.22f, 1f)
                    : new Color(0.20f, 0.92f, 1f, 0.95f);
                captureLowHpMarkerImage.enabled = true;
                captureLowHpMarkerImage.transform.SetAsLastSibling();

                if (captureLowHpLabel == null)
                    captureLowHpLabel = EnsureText("CaptureLowHpLabel", RectTransform, 0.38f, 0.27f, 0.95f, 0.43f, 17, TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.22f, 1f));
                captureLowHpLabel.text = isLowHp ? "LOW TARGET HP" : string.Empty;
                captureLowHpLabel.enabled = isLowHp;
            }
            else
            {
                if (captureLowHpMarkerImage != null)
                    captureLowHpMarkerImage.enabled = false;
                if (captureLowHpLabel != null)
                    captureLowHpLabel.enabled = false;
            }
        }

        private void PlayApGrantAnimationIfNeeded(int newAp)
        {
            if (_lastDisplayedAp < 0 || newAp <= _lastDisplayedAp)
                return;

            KillApGrantTweens();

            float bounceDur = Mathf.Max(0.01f, apGrantBounceDuration);
            float fadeDur = Mathf.Max(0.01f, apGrantFadeDuration);
            float scalePeak = Mathf.Max(1f, apGrantBounceScale);

            for (int i = _lastDisplayedAp; i < newAp && i < _apPips.Count; i++)
            {
                var pip = _apPips[i];
                if (pip == null || !pip.enabled)
                    continue;

                var rt = pip.rectTransform;

                Color startColor = new Color(pip.color.r, pip.color.g, pip.color.b, 0f);
                pip.color = startColor;
                rt.localScale = Vector3.one;

                Color endColor = new Color(pip.color.r, pip.color.g, pip.color.b, 1f);
                var fadeTween = DOTween.To(() => pip.color, x => pip.color = x, endColor, fadeDur)
                    .SetUpdate(true)
                    .SetLink(pip.gameObject, LinkBehaviour.KillOnDisable);
                _apGrantTweens.Add(fadeTween);

                var bounceSeq = DOTween.Sequence()
                    .Append(rt.DOScale(scalePeak, bounceDur * 0.4f).SetEase(Ease.OutQuad))
                    .Append(rt.DOScale(1f, bounceDur * 0.6f).SetEase(Ease.OutBack))
                    .SetUpdate(true)
                    .SetLink(rt.gameObject, LinkBehaviour.KillOnDisable);
                _apGrantTweens.Add(bounceSeq);
            }
        }

        private void KillApGrantTweens()
        {
            for (int i = 0; i < _apGrantTweens.Count; i++)
            {
                if (_apGrantTweens[i] != null && _apGrantTweens[i].IsActive())
                    _apGrantTweens[i].Kill();
            }
            _apGrantTweens.Clear();
        }

        public void SetEmpty(bool isEmpty)
        {
            if (emptyLabel != null)
                emptyLabel.enabled = isEmpty;

            if (isEmpty)
            {
                SetCaptureLowHpMarker(false, 0, false);
                RestoreDefaultHudCardVariant();
                _boundInstanceId = string.Empty;
                _lastDisplayedAp = -1;
                KillApGrantTweens();
                if (nameText != null) nameText.text = "EMPTY";
                UpdateStarBar(0);
                if (hpText != null) hpText.text = "--";
                if (typeIconImage != null) typeIconImage.enabled = false;
                SetHpBarImmediate(0f);
                for (int i = 0; i < _apPips.Count; i++)
                {
                    if (_apPips[i] == null) continue;
                    if (apPipInactiveSprite != null)
                    {
                        _apPips[i].sprite = apPipInactiveSprite;
                        _apPips[i].color = Color.white;
                    }
                    else
                    {
                        _apPips[i].color = apPipOffColor;
                    }
                }
            }

            if (!_hiddenInPlace && canvasGroup != null)
                canvasGroup.alpha = isEmpty ? 0.6f : 1f;

            if (isEmpty)
            {
                SetActiveTurn(false);
                SetTargeted(false);
            }
        }

        public void SetKO(bool isKo)
        {
            if (isKo)
                RestoreDefaultHudCardVariant();

            if (_hiddenInPlace || canvasGroup == null)
                return;

            canvasGroup.alpha = isKo ? koAlpha : 1f;
        }

        public void SetHiddenInPlace(bool hidden)
        {
            _hiddenInPlace = hidden;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (hidden)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                SetEmpty(true);
                SetActiveTurn(false);
                SetTargeted(false);
                canvasGroup.alpha = 0f;
            }
            else
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
        }

        public void PlayDamageShake()
        {
            var shakeRoot = GetShakeRoot();
            if (shakeRoot == null) return;

            KillImpactReactionTween(restorePulse: false);
            CacheBaseAnchoredPosition(shakeRoot);
            shakeRoot.anchoredPosition = _baseAnchoredPosition;

            // Position-only shake on AnchorContainer (layout-safe). Card frame is on the root Image
            // and pulses via root scale; GridLayoutGroup would overwrite root anchoredPosition.
            _impactReactionTween = shakeRoot.DOShakeAnchorPos(
                    Mathf.Max(0.05f, damageShakeDuration),
                    Mathf.Max(1f, damageShakeIntensity),
                    Mathf.Max(1, damageShakeVibrato),
                    90f, false, true, ShakeRandomnessMode.Harmonic)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() =>
                {
                    if (shakeRoot != null)
                        shakeRoot.anchoredPosition = _baseAnchoredPosition;
                });
        }

        public void PlayHealAscend()
        {
            var shakeRoot = GetShakeRoot();
            var pulseRoot = RectTransform;
            if (shakeRoot == null || pulseRoot == null) return;

            // Heal briefly owns scale on the card root (frame + content) — pause pulse first.
            if (hudAnimator != null)
                hudAnimator.StopAllPulses();

            KillImpactReactionTween(restorePulse: false);
            CacheBaseAnchoredPosition(shakeRoot);
            shakeRoot.anchoredPosition = _baseAnchoredPosition;
            pulseRoot.localScale = Vector3.one;

            float halfDur = Mathf.Max(0.05f, healAscendDuration) * 0.5f;
            Vector2 peakPos = _baseAnchoredPosition + new Vector2(0f, Mathf.Max(1f, healAscendDistance));
            float peakScale = Mathf.Max(1f, healPulseScale);

            _impactReactionTween = DOTween.Sequence()
                .Append(shakeRoot.DOAnchorPos(peakPos, halfDur).SetEase(Ease.OutSine))
                .Join(pulseRoot.DOScale(peakScale, halfDur).SetEase(Ease.OutSine))
                .Append(shakeRoot.DOAnchorPos(_baseAnchoredPosition, halfDur).SetEase(Ease.InOutSine))
                .Join(pulseRoot.DOScale(1f, halfDur).SetEase(Ease.InOutSine))
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() =>
                {
                    if (shakeRoot != null)
                        shakeRoot.anchoredPosition = _baseAnchoredPosition;
                    if (pulseRoot != null)
                        pulseRoot.localScale = Vector3.one;

                    // Re-sync from card flags (SetActiveTurn early-outs, so we must restart here).
                    ResumeSelectionPulse();
                });
        }

        /// <summary>
        /// Layout-safe motion target for position shake/heal. Prefab assigns AnchorContainer here
        /// so GridLayoutGroup on the HUD panel cannot overwrite offsets. Card frame Image stays on root.
        /// </summary>
        private RectTransform GetShakeRoot()
        {
            return shakeTarget != null ? shakeTarget : RectTransform;
        }

        private void CacheBaseAnchoredPosition(RectTransform rt)
        {
            if (rt == null) return;
            _baseAnchoredPosition = rt.anchoredPosition;
            _hasBaseAnchoredPosition = true;
        }

        private void KillImpactReactionTween(bool restorePulse = true)
        {
            if (_impactReactionTween != null && _impactReactionTween.IsActive())
                _impactReactionTween.Kill(complete: false);
            _impactReactionTween = null;

            var shakeRoot = GetShakeRoot();
            if (_hasBaseAnchoredPosition && shakeRoot != null)
                shakeRoot.anchoredPosition = _baseAnchoredPosition;

            if (restorePulse && (_isActiveTurn || _isTargeted))
                ResumeSelectionPulse();
            else if (RectTransform != null && (hudAnimator == null || !hudAnimator.IsPulsing))
                RectTransform.localScale = Vector3.one;
        }

        private void ResumeSelectionPulse()
        {
            if (hudAnimator == null)
                return;

            // Re-apply from card state flags so locked resting scale is used.
            if (_isActiveTurn)
                hudAnimator.PlayActivePulseLoop();
            else
                hudAnimator.StopActivePulseLoop();

            if (_isTargeted)
                hudAnimator.PlayTargetPulse();
            else
                hudAnimator.StopTargetPulse();
        }

        public void SetActiveTurn(bool isActive)
        {
            if (_isActiveTurn == isActive)
                return;

            _isActiveTurn = isActive;

            if (backgroundState != null)
                backgroundState.SetActive(isActive);

            if (activeTurnOutline != null)
                activeTurnOutline.enabled = false;

            if (hudAnimator != null)
            {
                if (isActive)
                    hudAnimator.PlayActivePulseLoop();
                else
                    hudAnimator.StopActivePulseLoop();
            }
            else if (!isActive && RectTransform != null)
            {
                RectTransform.localScale = Vector3.one;
            }
        }

        public void SetTargeted(bool isTargeted)
        {
            if (_isTargeted == isTargeted)
                return;

            _isTargeted = isTargeted;

            if (backgroundState != null)
                backgroundState.SetTarget(isTargeted);

            if (targetOutline != null)
                targetOutline.enabled = false;

            if (hudAnimator != null)
            {
                if (isTargeted)
                    hudAnimator.PlayTargetPulse();
                else
                    hudAnimator.StopTargetPulse();
            }
            else if (!isTargeted && !_isActiveTurn && RectTransform != null)
            {
                RectTransform.localScale = Vector3.one;
            }
        }

        private void EnsureBuilt()
        {
            if (!autoBuildIfMissing)
            {
                CacheExistingReferences();
                return;
            }

            var root = RectTransform;
            if (root == null)
                return;

            if (canvasGroup == null)
                canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);

            bool createdBackground = false;
            if (backgroundImage == null)
            {
                var img = GetComponent<Image>();
                if (img == null)
                {
                    backgroundImage = gameObject.AddComponent<Image>();
                    createdBackground = true;
                }
                else
                    backgroundImage = img;
            }

            if (hudAnimator == null)
                hudAnimator = GetOrAddComponent<CombatHudAnimator>(gameObject);
            if (backgroundState == null)
                backgroundState = GetOrAddComponent<CombatHudCardBackgroundState>(gameObject);
            GetOrAddComponent<CanvasRenderer>(gameObject);

            backgroundImage.raycastTarget = false;
            if (backgroundImage.sprite == null)
                backgroundImage.sprite = GetDefaultSprite();
            if (createdBackground)
            {
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.color = new Color(0.06f, 0.09f, 0.12f, 0.86f);
            }
            backgroundState.SetBackgroundImage(backgroundImage);

            if (root.sizeDelta.sqrMagnitude <= 1f)
                root.sizeDelta = new Vector2(420f, 170f);

            typeIconImage = EnsureImage("TypeIcon", root, 0.03f, 0.68f, 0.14f, 0.95f, Color.white);
            typeIconImage.preserveAspect = true;
            typeIconImage.raycastTarget = false;

            nameText = EnsureText("NameText", root, 0.15f, 0.71f, 0.70f, 0.95f, 28, TextAlignmentOptions.Left, Color.white);
            EnsureStarBar();

            var hpFrame = EnsureRect("HPFrame", root, 0.05f, 0.43f, 0.95f, 0.62f);
            hpBackImage = EnsureImage("HPBack", hpFrame, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.45f));
            hpLagFillImage = EnsureImage("HPDamageFill", hpFrame, 0f, 0f, 1f, 1f, hpDamageLagColor);
            hpFillImage = EnsureImage("HPFill", hpFrame, 0f, 0f, 1f, 1f, hpHighColor);
            hpFrameOverlayImage = EnsureImage("HPFrameOverlay", hpFrame, 0f, 0f, 1f, 1f, Color.white);
            ConfigureHpFrameOverlayImage(hpFrameOverlayImage);
            EnsureHpBarReferences();

            hpText = EnsureText("HPText", root, 0.05f, 0.29f, 0.95f, 0.42f, 20, TextAlignmentOptions.Left, new Color(0.92f, 0.95f, 0.98f, 1f));

            apPipRoot = EnsureRect("APPipRoot", root, 0.05f, 0.10f, 0.95f, 0.26f);
            apPipTemplate = EnsureImage("APPipTemplate", apPipRoot, 0f, 0f, 0f, 1f, apPipOnColor);
            apPipTemplate.rectTransform.anchorMin = new Vector2(0f, 0f);
            apPipTemplate.rectTransform.anchorMax = new Vector2(0f, 1f);
            apPipTemplate.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, apPipSize), 0f);
            apPipTemplate.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            apPipTemplate.enabled = false;
            apPipTemplate.raycastTarget = false;

            statusIconRoot = EnsureRect("StatusIconRow", root, 0.05f, 0.01f, 0.95f, 0.08f);

            emptyLabel = EnsureText("EmptyLabel", root, 0f, 0f, 1f, 1f, 32, TextAlignmentOptions.Center, new Color(0.88f, 0.88f, 0.90f, 1f));
            emptyLabel.text = "EMPTY";
            emptyLabel.enabled = false;

            activeTurnOutline = EnsureImage("ActiveTurnOutline", root, 0f, 0f, 1f, 1f, activeOutlineColor);
            activeTurnOutline.raycastTarget = false;
            activeTurnOutline.enabled = false;
            activeTurnOutline.type = Image.Type.Sliced;

            targetOutline = EnsureImage("TargetOutline", root, 0f, 0f, 1f, 1f, targetOutlineColor);
            targetOutline.raycastTarget = false;
            targetOutline.enabled = false;
            targetOutline.type = Image.Type.Sliced;

            ConfigureAnimatorTargets();
        }

        private void CacheExistingReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (hudAnimator == null)
                hudAnimator = GetComponent<CombatHudAnimator>();
            if (backgroundState == null)
                backgroundState = GetComponent<CombatHudCardBackgroundState>();

            if (nameText == null)
                nameText = FindText("NameText");
            if (hpText == null)
                hpText = FindText("HPText");
            if (emptyLabel == null)
                emptyLabel = FindText("EmptyLabel");

            if (typeIconImage == null)
                typeIconImage = FindImage("TypeIcon");
            if (hpBackImage == null)
                hpBackImage = FindImage("HPBack");
            if (hpLagFillImage == null)
                hpLagFillImage = FindImage("HPDamageFill");
            if (hpFillImage == null)
                hpFillImage = FindImage("HPFill");
            if (hpFrameOverlayImage == null)
                hpFrameOverlayImage = FindImage("HPFrameOverlay");
            if (captureLowHpMarkerImage == null)
                captureLowHpMarkerImage = FindImage("CaptureLowHpMarker");
            if (captureLowHpLabel == null)
                captureLowHpLabel = FindText("CaptureLowHpLabel");
            if (apPipTemplate == null)
                apPipTemplate = FindImage("APPipTemplate");
            if (activeTurnOutline == null)
                activeTurnOutline = FindImage("ActiveTurnOutline");
            if (targetOutline == null)
                targetOutline = FindImage("TargetOutline");

            if (apPipRoot == null)
                apPipRoot = FindRect("APPipRoot");
            if (starBarRoot == null)
                starBarRoot = FindRect("StarBarRoot");
            if (statusIconRoot == null)
                statusIconRoot = FindRect("StatusIconRow");

            EnsureStarBar();
            EnsureHpBarReferences();
            ConfigureAnimatorTargets();
        }

        private void ConfigureAnimatorTargets()
        {
            if (activeTurnOutline != null)
                activeTurnOutline.raycastTarget = false;
            if (targetOutline != null)
                targetOutline.raycastTarget = false;

            if (activeTurnOutline != null)
                activeTurnOutline.enabled = false;
            if (targetOutline != null)
                targetOutline.enabled = false;

            // Always pulse the HudCard root. The card frame/background Image lives on the root;
            // shakeTarget (AnchorContainer) only holds inner content for layout-safe shakes.
            var pulseRoot = RectTransform;
            activePulseTarget = pulseRoot;
            targetPulseTarget = pulseRoot;

            if (hudAnimator != null)
            {
                hudAnimator.SetBaseScale(Vector3.one);
                hudAnimator.ConfigureTargets(activePulseTarget, targetPulseTarget);
            }
        }

        private void EnsureHpBarReferences()
        {
            var root = RectTransform;
            var hpFrame = FindRect("HPFrame");
            if (hpFrame == null && autoBuildIfMissing && root != null)
                hpFrame = EnsureRect("HPFrame", root, 0.05f, 0.43f, 0.95f, 0.62f);

            if (hpBackImage == null && hpFrame != null && autoBuildIfMissing)
                hpBackImage = EnsureImage("HPBack", hpFrame, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.45f));
            if (hpLagFillImage == null && hpFrame != null && autoBuildIfMissing)
                hpLagFillImage = EnsureImage("HPDamageFill", hpFrame, 0f, 0f, 1f, 1f, hpDamageLagColor);
            if (hpFillImage == null && hpFrame != null && autoBuildIfMissing)
                hpFillImage = EnsureImage("HPFill", hpFrame, 0f, 0f, 1f, 1f, hpHighColor);

            ConfigureHpBarImage(hpBackImage, Image.Type.Simple, 1f, new Color(0f, 0f, 0f, 0.45f));
            ConfigureHpBarImage(hpLagFillImage, Image.Type.Filled, _hasHpVisualState ? Mathf.Clamp01(_hpTarget01) : 1f, hpDamageLagColor);
            ConfigureHpBarImage(hpFillImage, Image.Type.Filled, _hasHpVisualState ? Mathf.Clamp01(_hpTarget01) : 1f, hpHighColor);

            if (hpBackImage != null)
                hpBackImage.rectTransform.SetSiblingIndex(0);
            if (hpLagFillImage != null)
                hpLagFillImage.rectTransform.SetSiblingIndex(1);
            if (hpFillImage != null)
                hpFillImage.rectTransform.SetSiblingIndex(2);
            if (hpFrameOverlayImage != null)
            {
                ConfigureHpFrameOverlayImage(hpFrameOverlayImage);
                hpFrameOverlayImage.rectTransform.SetSiblingIndex(3);
            }
        }

        private void ConfigureHpFrameOverlayImage(Image image)
        {
            if (image == null) return;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = Color.white;
            image.preserveAspect = true; // Pixel-perfect aspect for overlay sprite
            // Do NOT set enabled here — it is managed by CombatHudCardBackgroundState.Refresh().
            // EnsureHpBarReferences calls this repeatedly; setting enabled=false would override Refresh().
        }

        private Image EnsureHpFrameOverlayImage()
        {
            var hpFrame = FindRect("HPFrame");
            if (hpFrame == null) return null;
            if (hpFrameOverlayImage == null)
                hpFrameOverlayImage = FindImage("HPFrameOverlay");
            if (hpFrameOverlayImage == null)
            {
                hpFrameOverlayImage = EnsureImage("HPFrameOverlay", hpFrame, 0f, 0f, 1f, 1f, Color.white);
                ConfigureHpFrameOverlayImage(hpFrameOverlayImage);
                hpFrameOverlayImage.enabled = false; // Disabled until Refresh() sets sprite; do not set in ConfigureHpFrameOverlayImage (EnsureHpBarReferences calls it repeatedly)
            }
            return hpFrameOverlayImage;
        }

        private void EnsureApPipPool(int requiredCount)
        {
            if (apPipRoot == null || apPipTemplate == null)
                return;

            float pipWidth = Mathf.Max(4f, apPipSize);
            float pipSpacing = Mathf.Max(1f, apPipSpacing);

            while (_apPips.Count < requiredCount)
            {
                var pip = Instantiate(apPipTemplate, apPipRoot);
                pip.name = $"APPip_{_apPips.Count:D2}";
                pip.enabled = true;
                pip.raycastTarget = false;
                var rt = pip.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(pipWidth, 0f);
                rt.anchoredPosition = new Vector2(_apPips.Count * pipSpacing, 0f);
                _apPips.Add(pip);
            }

            // Apply current apPipSize to all pips (handles config changes)
            for (int i = 0; i < _apPips.Count && i < requiredCount; i++)
            {
                if (_apPips[i] != null)
                    _apPips[i].rectTransform.sizeDelta = new Vector2(pipWidth, 0f);
            }

            // Center the pip group within APPipRoot (anchored to parent center)
            float groupWidth = requiredCount * pipSpacing - (pipSpacing - pipWidth);
            float startX = -groupWidth * 0.5f + pipWidth * 0.5f;
            for (int i = 0; i < _apPips.Count; i++)
            {
                if (_apPips[i] == null) continue;
                var rt = _apPips[i].rectTransform;
                rt.anchoredPosition = new Vector2(startX + i * pipSpacing, 0f);
            }
        }

        private void EnsureStarBar()
        {
            var root = RectTransform;
            if (root == null)
                return;

            if (starBarRoot == null)
                starBarRoot = EnsureRect("StarBarRoot", root, 0.70f, 0.71f, 0.95f, 0.95f);

            var vlg = GetOrAddComponent<VerticalLayoutGroup>(starBarRoot.gameObject);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = Mathf.Max(0f, starSpacing);
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            var csf = GetOrAddComponent<ContentSizeFitter>(starBarRoot.gameObject);
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (starBarBackgroundSprite != null)
            {
                var bgExisting = starBarRoot.Find("StarBarBackground");
                if (bgExisting == null)
                {
                    var bg = EnsureImage("StarBarBackground", starBarRoot, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.3f));
                    bg.sprite = starBarBackgroundSprite;
                    bg.type = Image.Type.Sliced;
                    bg.raycastTarget = false;
                    var bgLe = GetOrAddComponent<LayoutElement>(bg.rectTransform.gameObject);
                    bgLe.ignoreLayout = true;
                    bg.rectTransform.SetAsFirstSibling();
                }
            }

            EnsureStarBarIcons();
        }

        private void EnsureStarBarIcons()
        {
            if (starBarRoot == null)
                return;

            _starIcons.Clear();
            float starSize = Mathf.Max(4f, starIconSize);
            while (_starIcons.Count < CombatConstants.STAR_MAX)
            {
                var starName = $"Star_{_starIcons.Count}";
                var existing = starBarRoot.Find(starName);
                Image img;
                if (existing != null)
                {
                    img = existing.GetComponent<Image>();
                    if (img == null)
                        img = existing.gameObject.AddComponent<Image>();
                    var le = existing.GetComponent<LayoutElement>();
                    if (le != null)
                    {
                        le.preferredWidth = starSize;
                        le.preferredHeight = starSize;
                        le.minWidth = starSize;
                        le.minHeight = starSize;
                    }
                }
                else
                {
                    var starRect = EnsureRect(starName, starBarRoot, 0f, 0f, 1f, 1f);
                    img = starRect.gameObject.GetComponent<Image>();
                    if (img == null)
                        img = starRect.gameObject.AddComponent<Image>();
                    GetOrAddComponent<CanvasRenderer>(starRect.gameObject);
                    img.sprite = starSprite != null ? starSprite : GetDefaultSprite();
                    img.color = Color.white;
                    img.raycastTarget = false;
                    img.preserveAspect = true;

                    var le = GetOrAddComponent<LayoutElement>(starRect.gameObject);
                    le.preferredWidth = starSize;
                    le.preferredHeight = starSize;
                    le.minWidth = starSize;
                    le.minHeight = starSize;
                    le.flexibleWidth = 0f;
                    le.flexibleHeight = 0f;
                }

                if (starSprite != null && img.sprite != starSprite)
                    img.sprite = starSprite;

                img.rectTransform.SetSiblingIndex(starBarRoot.childCount - 1);
                _starIcons.Add(img);
            }

            // Ensure star bar visibility: hide if no sprite configured
            if (starBarRoot.gameObject.activeSelf != (starSprite != null))
                starBarRoot.gameObject.SetActive(starSprite != null);
        }

        private void UpdateStarBar(int stars)
        {
            int clamped = Mathf.Clamp(stars, 0, CombatConstants.STAR_MAX);
            for (int i = 0; i < _starIcons.Count; i++)
            {
                if (_starIcons[i] != null)
                    _starIcons[i].gameObject.SetActive(i < clamped);
            }
        }

        private void UpdateHpBar(float hp01)
        {
            EnsureHpBarReferences();

            if (hpFillImage == null)
                return;

            float clampedHp01 = Mathf.Clamp01(hp01);
            Color frontColor = Color.Lerp(hpLowColor, hpHighColor, clampedHp01);
            hpFillImage.color = frontColor;
            if (hpLagFillImage != null)
                hpLagFillImage.color = hpDamageLagColor;

            if (!_hasHpVisualState || !Application.isPlaying)
            {
                SetHpBarImmediate(clampedHp01);
                return;
            }

            if (Mathf.Approximately(_hpTarget01, clampedHp01))
                return;

            if (clampedHp01 >= _hpTarget01)
            {
                SetHpBarImmediate(clampedHp01);
                return;
            }

            _hpTarget01 = clampedHp01;
            hpFillImage.fillAmount = clampedHp01;

            KillHpLagTween();
            if (hpLagFillImage == null)
                return;

            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (hpDamageLagDelay > 0f)
                sequence.AppendInterval(hpDamageLagDelay);

            sequence.Append(
                DOTween.To(
                    () => hpLagFillImage.fillAmount,
                    value => hpLagFillImage.fillAmount = value,
                    clampedHp01,
                    Mathf.Max(0.01f, hpDamageLagDuration))
                .SetEase(hpDamageLagEase));

            _hpLagTween = sequence;
        }

        private void SetHpBarImmediate(float hp01)
        {
            EnsureHpBarReferences();

            float clampedHp01 = Mathf.Clamp01(hp01);
            KillHpLagTween();
            _hpTarget01 = clampedHp01;
            _hasHpVisualState = true;

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = clampedHp01;
                hpFillImage.color = Color.Lerp(hpLowColor, hpHighColor, clampedHp01);
            }

            if (hpLagFillImage != null)
            {
                hpLagFillImage.fillAmount = clampedHp01;
                hpLagFillImage.color = hpDamageLagColor;
            }
        }

        private void ResetHpVisualState()
        {
            KillHpLagTween();
            _hpTarget01 = -1f;
            _hasHpVisualState = false;
        }

        private void ConfigureHpBarImage(Image image, Image.Type imageType, float fillAmount, Color defaultColor)
        {
            if (image == null)
                return;

            if (image.sprite == null)
                image.sprite = GetDefaultSprite();

            image.type = imageType;
            image.raycastTarget = false;

            if (imageType == Image.Type.Filled)
            {
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
                image.fillAmount = Mathf.Clamp01(fillAmount);
            }
            else
            {
                image.fillAmount = 1f;
            }

            if (image == hpLagFillImage && image.color.a <= 0f)
                image.color = defaultColor;
            else if (image == hpBackImage && image.color.a <= 0f)
                image.color = defaultColor;
            else if (image == hpFillImage && image.color.a <= 0f)
                image.color = defaultColor;
        }

        private void KillHpLagTween()
        {
            if (_hpLagTween != null && _hpLagTween.IsActive())
                _hpLagTween.Kill();
            _hpLagTween = null;
        }

        private Image EnsureImage(string name, Transform parent, float minX, float minY, float maxX, float maxY, Color color)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY);
            var img = rect.gameObject.GetComponent<Image>();
            bool createdImage = (img == null);
            if (img == null)
                img = rect.gameObject.AddComponent<Image>();
            GetOrAddComponent<CanvasRenderer>(rect.gameObject);
            if (img.sprite == null)
                img.sprite = GetDefaultSprite();
            if (createdImage)
                img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text EnsureText(string name, Transform parent, float minX, float minY, float maxX, float maxY, int size, TextAlignmentOptions align, Color color)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY);
            var text = rect.gameObject.GetComponent<TextMeshProUGUI>();
            bool createdText = (text == null);
            if (text == null)
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            GetOrAddComponent<CanvasRenderer>(rect.gameObject);
            if (createdText)
            {
                text.fontSize = size;
                text.alignment = align;
                text.color = color;
            }
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.text = string.IsNullOrEmpty(text.text) ? "--" : text.text;
            return text;
        }

        private RectTransform EnsureRect(string name, Transform parent, float minX, float minY, float maxX, float maxY)
        {
            var existing = parent.Find(name) as RectTransform;
            RectTransform rect;
            if (existing != null)
            {
                rect = existing;
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private RectTransform FindRect(string name)
        {
            return FindNamedComponentInChildren<RectTransform>(name);
        }

        private TMP_Text FindText(string name)
        {
            return FindNamedComponentInChildren<TMP_Text>(name);
        }

        private Image FindImage(string name)
        {
            return FindNamedComponentInChildren<Image>(name);
        }

        private T FindNamedComponentInChildren<T>(string name) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].name == name)
                    return components[i];
            }

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }

        private static Sprite GetDefaultSprite()
        {
            if (_defaultSprite != null)
                return _defaultSprite;

            var texture = Texture2D.whiteTexture;
            _defaultSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _defaultSprite.name = "HudCard_DefaultSprite";
            _defaultSprite.hideFlags = HideFlags.HideAndDontSave;
            return _defaultSprite;
        }
    }
}
