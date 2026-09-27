using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform motionPivot;
        [SerializeField] private Transform textRoot;
        [SerializeField] private Transform spriteRoot;

        [Header("Legacy Fallback Style")]
        [SerializeField] private Color damageColor = new Color(1f, 0.33f, 0.28f, 1f);
        [SerializeField] private Color healColor = new Color(0.26f, 1f, 0.56f, 1f);
        [SerializeField] private Color critColor = new Color(1f, 0.91f, 0.28f, 1f);
        [SerializeField] private Color impactColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color blockColor = new Color(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color missColor = new Color(0.6f, 0.6f, 0.65f, 1f);
        [SerializeField] private Color dodgeColor = new Color(1f, 0.95f, 0.6f, 1f);

        [Header("Legacy Fallback Motion")]
        [SerializeField] private float duration = 0.95f;
        [SerializeField] private float floatDistance = 0.75f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float cameraFacingOffset = 0.35f;
        [SerializeField] private int textSortingOrder = 150;

        private Sequence _sequence;
        private Action<DamagePopup> _onComplete;
        private bool _playing;
        private Vector3 _rootBaseScale = Vector3.one;
        private Vector3 _motionPivotBaseLocalPosition = Vector3.zero;
        private MaterialPropertyBlock _spritePropertyBlock;
        private bool _billboardEnabled;
        private Camera _renderCamera;

        private void Awake()
        {
            ResolveFallbacks();
            _rootBaseScale = transform.localScale;
            if (motionPivot != null)
                _motionPivotBaseLocalPosition = motionPivot.localPosition;
            _spritePropertyBlock = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            KillSequence();
            _playing = false;
            _onComplete = null;
            ResetVisualState();
        }

        private void LateUpdate()
        {
            if (!_billboardEnabled || !_playing)
                return;

            var cam = _renderCamera != null ? _renderCamera : Camera.main;
            if (cam == null)
                return;

            Vector3 toCamera = cam.transform.position - transform.position;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, cam.transform.up);
        }

        public void Play(Vector3 worldPosition, int amount, bool isHeal, bool isCrit, string impactText,
            CombatUiAnimTuning tuning, Camera renderCamera, Action<DamagePopup> onComplete)
        {
            CombatPopupCategory category = CombatPopupLegacyMapper.ResolveLegacyCategory(amount, isHeal, isCrit, impactText);
            CombatPopupPlayRequest request = string.IsNullOrEmpty(impactText)
                ? CombatPopupPlayRequest.ForAmount(category, worldPosition, amount, isHeal)
                : CombatPopupPlayRequest.ForLabel(category, worldPosition, impactText);

            CombatPopupVisualProfileSO profile = BuildLegacyProfile(category, isHeal, isCrit, impactText, tuning);
            Play(request, profile, renderCamera, onComplete);
        }

        public void Play(CombatPopupPlayRequest request, CombatPopupVisualProfileSO profile, Camera renderCamera,
            Action<DamagePopup> onComplete)
        {
            ResolveFallbacks();
            KillSequence();
            ResetVisualState();

            _renderCamera = renderCamera != null ? renderCamera : Camera.main;
            _onComplete = onComplete;
            _playing = true;

            profile ??= BuildLegacyProfile(request.category, request.isHeal, false, request.label, null);

            int facingSign = request.facingSign != 0 ? request.facingSign : 1;
            float amountScaleFactor = request.useNumericLabel
                ? CombatPopupAmountScaleUtility.ResolveScaleFactor(profile, request.amount)
                : 1f;
            ApplyWorldPlacement(request.worldPosition, profile, amountScaleFactor);
            ApplyChannels(request, profile, amountScaleFactor, facingSign);
            _billboardEnabled = profile.billboardToCamera;

            _sequence = BuildAnimationSequence(profile, amountScaleFactor, facingSign);
            if (_sequence == null)
                Complete();
        }

        public void Play(CombatPopupPlayRequest request, CombatPopupVisualProfileSO profile, Action<DamagePopup> onComplete)
        {
            Play(request, profile, null, onComplete);
        }

        public void Play(Vector3 worldPosition, int amount, bool isHeal, bool isCrit, string impactText,
            CombatUiAnimTuning tuning, Action<DamagePopup> onComplete)
        {
            Play(worldPosition, amount, isHeal, isCrit, impactText, tuning, null, onComplete);
        }

        private void ApplyWorldPlacement(Vector3 worldPosition, CombatPopupVisualProfileSO profile, float amountScaleFactor)
        {
            Vector3 targetPosition = worldPosition + Vector3.up * Mathf.Max(0f, profile.worldYOffset);
            targetPosition = ApplyCameraFacingOffset(targetPosition);
            SetWorldPosition(targetPosition);
            Vector3 scaledStart = profile.startScale * amountScaleFactor;
            transform.localScale = Vector3.Scale(_rootBaseScale, scaledStart);
        }

        private Vector3 ApplyCameraFacingOffset(Vector3 worldPosition)
        {
            if (cameraFacingOffset <= 0f)
                return worldPosition;

            Camera cam = _renderCamera != null ? _renderCamera : Camera.main;
            if (cam == null)
                return worldPosition;

            Vector3 toCamera = cam.transform.position - worldPosition;
            if (toCamera.sqrMagnitude < 0.0001f)
                return worldPosition;

            return worldPosition + toCamera.normalized * cameraFacingOffset;
        }

        private void SetWorldPosition(Vector3 worldPosition)
        {
            if (transform.parent != null)
                transform.SetParent(null, true);

            if (transform is RectTransform rectTransform)
                rectTransform.SetPositionAndRotation(worldPosition, rectTransform.rotation);
            else
                transform.SetPositionAndRotation(worldPosition, transform.rotation);
        }

        private void ApplyChannels(
            CombatPopupPlayRequest request,
            CombatPopupVisualProfileSO profile,
            float amountScaleFactor,
            int facingSign)
        {
            string label = ResolveLabel(request);

            if (profile.showText && textMesh != null)
            {
                textMesh.gameObject.SetActive(true);
                if (profile.fontAsset != null)
                    textMesh.font = profile.fontAsset;
                if (profile.textMaterialPreset != null)
                    textMesh.fontSharedMaterial = profile.textMaterialPreset;
                float fontSize = CombatPopupAmountScaleUtility.ResolveFontSize(profile, request.amount, profile.fontSize);
                textMesh.fontSize = fontSize;
                textMesh.alignment = profile.textAlignment;
                textMesh.outlineWidth = profile.textOutlineWidth;
                textMesh.outlineColor = profile.textOutlineColor;
                textMesh.text = label;
                textMesh.color = new Color(profile.textColor.r, profile.textColor.g, profile.textColor.b, 1f);
                textMesh.alpha = 1f;
                textMesh.renderer.sortingOrder = Mathf.Max(textSortingOrder, profile.spriteSortingOrder);
                textMesh.ForceMeshUpdate(true, true);

                Transform root = textRoot != null ? textRoot : textMesh.transform;
                root.localPosition = CombatPopupMotionResolver.MirrorVector(profile.textLocalOffset, facingSign);
                root.localScale = profile.textLocalScale;
            }
            else if (textMesh != null)
            {
                textMesh.gameObject.SetActive(false);
            }

            if (profile.showSprite && spriteRenderer != null)
            {
                spriteRenderer.gameObject.SetActive(true);
                spriteRenderer.sprite = profile.sprite;
                if (profile.spriteMaterial != null)
                    spriteRenderer.sharedMaterial = profile.spriteMaterial;
                spriteRenderer.sortingOrder = profile.spriteSortingOrder;
                spriteRenderer.color = new Color(profile.spriteColor.r, profile.spriteColor.g, profile.spriteColor.b, 1f);

                if (!string.IsNullOrWhiteSpace(profile.spriteColorPropertyName))
                {
                    spriteRenderer.GetPropertyBlock(_spritePropertyBlock);
                    _spritePropertyBlock.SetColor(profile.spriteColorPropertyName, profile.spriteColor);
                    spriteRenderer.SetPropertyBlock(_spritePropertyBlock);
                }

                Transform root = spriteRoot != null ? spriteRoot : spriteRenderer.transform;
                root.localPosition = CombatPopupMotionResolver.MirrorVector(profile.spriteLocalOffset, facingSign);
                root.localScale = profile.spriteLocalScale;
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.gameObject.SetActive(false);
            }
        }

        private Sequence BuildAnimationSequence(
            CombatPopupVisualProfileSO profile,
            float amountScaleFactor,
            int facingSign)
        {
            float liveDuration = Mathf.Max(0.05f, profile.duration);
            Vector3 scaledStart = profile.startScale * amountScaleFactor;
            Vector3 scaledEnd = profile.endScale * amountScaleFactor;
            CombatPopupMotionLayers rootLayers = CombatPopupMotionResolver.ResolveRootLayers(profile);

            var sequence = DOTween.Sequence()
                .SetUpdate(profile.useUnscaledTime)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (profile.startDelay > 0f)
                sequence.AppendInterval(profile.startDelay);

            Transform translateTransform = transform;
            Transform impulseTransform = motionPivot != null ? motionPivot : transform;

            CombatPopupMotionResolver.AppendLayerMotion(
                sequence,
                profile,
                facingSign,
                liveDuration,
                translateTransform,
                impulseTransform,
                rootLayers,
                useLocalSpace: false);

            if (profile.playScalePulse)
            {
                float upDuration = Mathf.Max(0.03f, liveDuration * 0.25f);
                float downDuration = Mathf.Max(0.03f, liveDuration * 0.35f);
                Vector3 pulseScale = Vector3.Scale(_rootBaseScale, scaledStart * profile.scalePulseMultiplier);
                sequence.Join(transform.DOScale(pulseScale, upDuration).SetEase(profile.scaleEase));
                sequence.Append(transform.DOScale(Vector3.Scale(_rootBaseScale, scaledEnd), downDuration).SetEase(Ease.InOutSine));
            }
            else if (scaledEnd != scaledStart)
            {
                sequence.Join(transform.DOScale(Vector3.Scale(_rootBaseScale, scaledEnd), liveDuration).SetEase(profile.scaleEase));
            }

            AppendChannelMotion(sequence, profile, facingSign, liveDuration, textRoot, profile.textMotionLayers, profile.textMoveOffset, profile.textArcControlOffset);
            AppendChannelMotion(sequence, profile, facingSign, liveDuration, spriteRoot, profile.spriteMotionLayers, profile.spriteMoveOffset, profile.spriteArcControlOffset);

            if (profile.holdBeforeFade > 0f)
                sequence.AppendInterval(profile.holdBeforeFade);

            AppendFadeTweens(sequence, profile, liveDuration);
            sequence.OnComplete(Complete);
            return sequence;
        }

        private static void AppendChannelMotion(
            Sequence sequence,
            CombatPopupVisualProfileSO profile,
            int facingSign,
            float liveDuration,
            Transform channelRoot,
            CombatPopupMotionLayers channelLayers,
            Vector3 channelMoveOffset,
            Vector3 channelArcOffset)
        {
            if (channelRoot == null || !CombatPopupMotionResolver.HasChannelOverride(channelLayers))
                return;

            CombatPopupMotionResolver.AppendLayerMotion(
                sequence,
                profile,
                facingSign,
                liveDuration,
                channelRoot,
                channelRoot,
                channelLayers,
                useLocalSpace: true,
                channelMoveOffset,
                channelArcOffset);
        }

        private void AppendFadeTweens(Sequence sequence, CombatPopupVisualProfileSO profile, float liveDuration)
        {
            float fadeStart = profile.startDelay;

            if (profile.fadeText && textMesh != null && textMesh.gameObject.activeSelf)
            {
                float fadeDuration = profile.textFadeDuration > 0f ? profile.textFadeDuration : liveDuration;
                textMesh.alpha = 1f;
                sequence.Insert(
                    fadeStart + profile.textFadeDelay,
                    textMesh.DOFade(0f, fadeDuration)
                        .SetEase(profile.fadeEase)
                        .SetTarget(textMesh));
            }

            if (profile.fadeSprite && spriteRenderer != null && spriteRenderer.gameObject.activeSelf)
            {
                float fadeDuration = profile.spriteFadeDuration > 0f ? profile.spriteFadeDuration : liveDuration;
                Color spriteColor = spriteRenderer.color;
                spriteRenderer.color = new Color(spriteColor.r, spriteColor.g, spriteColor.b, 1f);
                sequence.Insert(
                    fadeStart + profile.spriteFadeDelay,
                    spriteRenderer.DOFade(0f, fadeDuration).SetEase(profile.fadeEase));
            }
        }

        private static string ResolveLabel(CombatPopupPlayRequest request)
        {
            if (!request.useNumericLabel && !string.IsNullOrEmpty(request.label))
                return request.label;

            int amount = Mathf.Abs(request.amount);
            if (request.isHeal)
                return $"+{amount}";
            return $"-{amount}";
        }

        private CombatPopupVisualProfileSO BuildLegacyProfile(
            CombatPopupCategory category,
            bool isHeal,
            bool isCrit,
            string impactText,
            CombatUiAnimTuning tuning)
        {
            var profile = ScriptableObject.CreateInstance<CombatPopupVisualProfileSO>();
            profile.showText = true;
            profile.showSprite = false;
            profile.billboardToCamera = billboardToCamera;
            profile.duration = tuning != null ? tuning.popupDuration : duration;
            profile.moveOffset = new Vector3(0f, tuning != null ? tuning.popupFloatDistance : floatDistance, 0f);
            profile.worldYOffset = 0f;
            profile.moveEase = moveEase;
            profile.fadeEase = fadeEase;
            profile.useUnscaledTime = tuning != null ? tuning.useUnscaledTime : useUnscaledTime;
            profile.textColor = ResolveLegacyTextColor(category, isHeal, isCrit, impactText);
            profile.playScalePulse = !string.IsNullOrEmpty(impactText) && impactText.Length <= 12;
            profile.rootMotionLayers = CombatPopupMotionLayers.Translate;
            profile.translateMode = CombatPopupTranslateMode.FloatUp;
            return profile;
        }

        private Color ResolveLegacyTextColor(CombatPopupCategory category, bool isHeal, bool isCrit, string impactText)
        {
            switch (category)
            {
                case CombatPopupCategory.Heal:
                    return healColor;
                case CombatPopupCategory.Crit:
                    return critColor;
                case CombatPopupCategory.Blocked:
                    return blockColor;
                case CombatPopupCategory.Miss:
                    return missColor;
                case CombatPopupCategory.Dodge:
                    return dodgeColor;
                case CombatPopupCategory.Damage:
                    return damageColor;
                default:
                    return impactColor;
            }
        }

        private void Complete()
        {
            if (!_playing)
                return;

            _playing = false;
            _billboardEnabled = false;
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(this);
        }

        private void ResetVisualState()
        {
            transform.localScale = _rootBaseScale;
            if (motionPivot != null)
                motionPivot.localPosition = _motionPivotBaseLocalPosition;
            ResetChannelTransform(textRoot);
            ResetChannelTransform(spriteRoot);
            ResetTextAlpha();
            if (textMesh != null)
                textMesh.text = string.Empty;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
                spriteRenderer.SetPropertyBlock(null);
                Color c = spriteRenderer.color;
                spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        private void ResetTextAlpha()
        {
            if (textMesh == null)
                return;

            textMesh.alpha = 1f;
            Color c = textMesh.color;
            textMesh.color = new Color(c.r, c.g, c.b, 1f);
            textMesh.ForceMeshUpdate(true, true);
        }

        private void KillSequence()
        {
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
            transform.DOKill();
            if (motionPivot != null)
                motionPivot.DOKill();
            if (textRoot != null)
                textRoot.DOKill();
            if (spriteRoot != null)
                spriteRoot.DOKill();
            if (textMesh != null)
                DOTween.Kill(textMesh);
            if (spriteRenderer != null)
                DOTween.Kill(spriteRenderer);
            ResetTextAlpha();
        }

        private static void ResetChannelTransform(Transform channelRoot)
        {
            if (channelRoot == null)
                return;

            channelRoot.DOKill();
            channelRoot.localPosition = Vector3.zero;
            channelRoot.localScale = Vector3.one;
        }

        private void ResolveFallbacks()
        {
            if (textMesh == null)
                textMesh = GetComponentInChildren<TextMeshPro>(true);
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (motionPivot == null)
            {
                Transform found = transform.Find("MotionPivot");
                if (found == null && (textMesh != null || spriteRenderer != null))
                {
                    var pivotGo = new GameObject("MotionPivot");
                    pivotGo.transform.SetParent(transform, false);
                    motionPivot = pivotGo.transform;

                    ReparentUnderPivot(textMesh != null ? textMesh.transform : null, motionPivot);
                    ReparentUnderPivot(spriteRenderer != null ? spriteRenderer.transform : null, motionPivot);
                }
                else
                {
                    motionPivot = found;
                }
            }

            if (textRoot == null && textMesh != null)
                textRoot = textMesh.transform;
            if (spriteRoot == null && spriteRenderer != null)
                spriteRoot = spriteRenderer.transform;
        }

        private static void ReparentUnderPivot(Transform child, Transform pivot)
        {
            if (child == null || pivot == null || child.parent == pivot)
                return;

            child.SetParent(pivot, true);
        }
    }
}
