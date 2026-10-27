using System;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Persistent wall sprite on a combat slot. Uses the same depth-writing mesh quad
    /// convention as <see cref="CombatChainlingVisual"/> (fixed world size, atlas UVs).
    /// </summary>
    public class CombatWallVisual : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Transform visualRoot;
        [SerializeField] private CombatWallVisualProfileSO profile;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _quadMesh;

        private Sequence _sequence;
        private Vector3 _baseLocalPosition;
        private Vector3 _restScale = Vector3.one;
        private int _facingSign = 1;
        private bool _breakPlaying;

        private void Awake()
        {
            EnsureRenderingHierarchy();
            _baseLocalPosition = visualRoot.localPosition;
            _restScale = Vector3.one;
        }

        private void OnDestroy()
        {
            KillSequence();
            if (_quadMesh != null)
                Destroy(_quadMesh);
        }

        public void Configure(CombatWallVisualProfileSO visualProfile, int facingSign)
        {
            profile = visualProfile != null ? visualProfile : profile;
            _facingSign = facingSign < 0 ? -1 : 1;
        }

        public void ApplyArt(MoveWallPresentation art, CombatWallVisualProfileSO fallbackProfile)
        {
            if (fallbackProfile != null)
                profile = fallbackProfile;

            EnsureRenderingHierarchy();

            Sprite resolvedSprite = art.wallSprite != null
                ? art.wallSprite
                : profile != null ? profile.defaultWallSprite : null;
            Material resolvedMaterial = art.wallMaterial != null
                ? art.wallMaterial
                : profile != null ? profile.defaultWallMaterial : null;
            Color resolvedTint = art.wallTint.a > 0f
                ? art.wallTint
                : profile != null ? profile.defaultTint : Color.white;

            Vector2 worldSize = ResolveWorldSize(art, resolvedSprite);
            RebuildQuadMesh(worldSize);
            ApplySpriteTexture(resolvedSprite, resolvedTint, _facingSign < 0);

            if (_meshRenderer != null && resolvedMaterial != null)
            {
                _meshRenderer.sharedMaterial = resolvedMaterial;
                if (_meshRenderer.sharedMaterial.renderQueue != 2450)
                    _meshRenderer.sharedMaterial.renderQueue = 2450;
            }

            _restScale = Vector3.one;
            // Scale is owned by Raise / PlayHit / PlayBreak / ForceHide.
            // Never stomp it here — presenter poll sync re-calls ApplyArt via EnsureWallVisual
            // and was killing in-flight DOTween raise/hit/break animations.
        }

        public float Raise()
        {
            if (visualRoot == null)
                return 0f;

            gameObject.SetActive(true);
            _breakPlaying = false;
            KillSequence();

            _baseLocalPosition = visualRoot.localPosition;
            visualRoot.localPosition = _baseLocalPosition;
            visualRoot.localScale = new Vector3(_restScale.x, 0f, _restScale.z);
            float duration = profile != null ? Mathf.Max(0f, profile.raiseDuration) : 0.22f;
            Ease ease = profile != null ? profile.raiseEase : Ease.OutBack;
            _sequence = DOTween.Sequence()
                .SetTarget(visualRoot)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(visualRoot.DOScaleY(_restScale.y, duration).SetEase(ease))
                .OnComplete(() => _sequence = null);
            return duration;
        }

        public float PlayHit()
        {
            if (visualRoot == null || _breakPlaying)
                return 0f;

            gameObject.SetActive(true);
            KillSequence();
            // Capture rest pose after activation — Awake may have run before parenting/anchors settled.
            _baseLocalPosition = visualRoot.localPosition;
            visualRoot.localPosition = _baseLocalPosition;
            visualRoot.localScale = _restScale;

            float shove = profile != null ? profile.hitShoveDistance : 0.08f;
            float stretch = profile != null ? profile.hitStretchY : 1.08f;
            float outDuration = profile != null ? profile.hitOutDuration : 0.06f;
            float recoilDuration = profile != null ? profile.hitRecoilDuration : 0.07f;
            float settleDuration = profile != null ? profile.hitSettleDuration : 0.08f;
            Ease ease = profile != null ? profile.hitEase : Ease.OutQuad;
            Vector3 shoved = _baseLocalPosition + new Vector3(shove * _facingSign, 0f, 0f);
            Vector3 stretchScale = new Vector3(_restScale.x, _restScale.y * stretch, _restScale.z);

            _sequence = DOTween.Sequence()
                .SetTarget(visualRoot)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(visualRoot.DOLocalMove(shoved, outDuration).SetEase(ease))
                .Join(visualRoot.DOScale(stretchScale, outDuration).SetEase(ease))
                .Append(visualRoot.DOLocalMove(_baseLocalPosition - new Vector3(shove * 0.35f * _facingSign, 0f, 0f), recoilDuration).SetEase(Ease.OutQuad))
                .Join(visualRoot.DOScale(new Vector3(_restScale.x, _restScale.y * 0.96f, _restScale.z), recoilDuration).SetEase(Ease.OutQuad))
                .Append(visualRoot.DOLocalMove(_baseLocalPosition, settleDuration).SetEase(Ease.OutQuad))
                .Join(visualRoot.DOScale(_restScale, settleDuration).SetEase(Ease.OutQuad))
                .OnComplete(() => _sequence = null);

            return outDuration + recoilDuration + settleDuration;
        }

        public float PlayBlocked()
        {
            return PlayHit();
        }

        public float PlayRefresh()
        {
            if (_breakPlaying || visualRoot == null || !visualRoot.gameObject.activeSelf)
                return 0f;

            _sequence?.Kill();
            float duration = profile != null ? Mathf.Min(0.14f, Mathf.Max(0.06f, profile.raiseDuration * 0.6f)) : 0.12f;
            Vector3 from = _restScale;
            Vector3 pulse = new Vector3(from.x * 1.04f, from.y * 1.08f, from.z);
            _sequence = DOTween.Sequence();
            _sequence.Append(visualRoot.DOScale(pulse, duration * 0.45f).SetEase(Ease.OutQuad));
            _sequence.Append(visualRoot.DOScale(from, duration * 0.55f).SetEase(Ease.OutQuad));
            return duration;
        }

        public float PlayBreak(Action onComplete = null)
        {
            if (visualRoot == null)
            {
                onComplete?.Invoke();
                return 0f;
            }

            gameObject.SetActive(true);
            _breakPlaying = true;
            KillSequence();

            visualRoot.localPosition = _baseLocalPosition;
            Vector3 current = visualRoot.localScale;
            if (current.y <= 0.001f)
                current = _restScale;
            visualRoot.localScale = current;

            float duration = profile != null ? Mathf.Max(0f, profile.breakDuration) : 0.2f;
            Ease ease = profile != null ? profile.breakEase : Ease.InBack;
            _sequence = DOTween.Sequence()
                .SetTarget(visualRoot)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
                .Append(visualRoot.DOScaleY(0f, duration).SetEase(ease))
                .OnComplete(() =>
                {
                    _sequence = null;
                    _breakPlaying = false;
                    onComplete?.Invoke();
                });
            return duration;
        }

        public void ForceHide()
        {
            KillSequence();
            _breakPlaying = false;
            if (visualRoot != null)
            {
                visualRoot.localPosition = _baseLocalPosition;
                visualRoot.localScale = new Vector3(_restScale.x, 0f, _restScale.z);
            }
            gameObject.SetActive(false);
        }

        private void EnsureRenderingHierarchy()
        {
            if (visualRoot == null)
                visualRoot = transform.Find("Visual");
            if (visualRoot == null)
            {
                var visualGo = new GameObject("Visual");
                visualGo.transform.SetParent(transform, false);
                visualRoot = visualGo.transform;
            }

            _meshFilter = visualRoot.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = visualRoot.gameObject.AddComponent<MeshFilter>();

            _meshRenderer = visualRoot.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = visualRoot.gameObject.AddComponent<MeshRenderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            // Legacy prefabs may still have SpriteRenderer — mesh path is authoritative.
            var legacySpriteRenderer = visualRoot.GetComponent<SpriteRenderer>();
            if (legacySpriteRenderer != null)
            {
                if (Application.isPlaying)
                    Destroy(legacySpriteRenderer);
                else
                    DestroyImmediate(legacySpriteRenderer);
            }
        }

        private Vector2 ResolveWorldSize(MoveWallPresentation art, Sprite sprite)
        {
            Vector2 baseSize = profile != null && profile.spriteSize.sqrMagnitude > 0.0001f
                ? profile.spriteSize
                : new Vector2(5f, 5f);

            Vector2 multiplier = art.wallSpriteScale;
            if (Mathf.Approximately(multiplier.x, 0f) || Mathf.Approximately(multiplier.y, 0f))
            {
                Vector2 fallback = profile != null ? profile.defaultScale : Vector2.one;
                if (Mathf.Approximately(multiplier.x, 0f))
                    multiplier.x = Mathf.Approximately(fallback.x, 0f) ? 1f : fallback.x;
                if (Mathf.Approximately(multiplier.y, 0f))
                    multiplier.y = Mathf.Approximately(fallback.y, 0f) ? 1f : fallback.y;
            }

            Vector2 boxSize = Vector2.Scale(baseSize, multiplier);
            if (profile == null || !profile.preserveSpriteAspect || sprite == null)
                return boxSize;

            Rect rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f || boxSize.x <= 0f || boxSize.y <= 0f)
                return boxSize;

            float spriteAspect = rect.width / rect.height;
            float boxAspect = boxSize.x / boxSize.y;
            if (spriteAspect > boxAspect)
                return new Vector2(boxSize.x, boxSize.x / spriteAspect);

            return new Vector2(boxSize.y * spriteAspect, boxSize.y);
        }

        private void RebuildQuadMesh(Vector2 worldSize)
        {
            if (_meshFilter == null)
                return;

            if (_quadMesh == null)
            {
                _quadMesh = new Mesh { name = "CombatWallQuad" };
                _quadMesh.MarkDynamic();
            }

            float halfWidth = worldSize.x * 0.5f;
            float height = worldSize.y;

            // Bottom-center pivot: wall grows upward from y = 0 (matches ground-rise animation).
            _quadMesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, 0f),
                new Vector3(halfWidth, 0f, 0f),
                new Vector3(-halfWidth, height, 0f),
                new Vector3(halfWidth, height, 0f),
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateNormals();
            _quadMesh.RecalculateBounds();
            _meshFilter.sharedMesh = _quadMesh;
        }

        private void ApplySpriteTexture(Sprite sprite, Color tint, bool flipX)
        {
            if (_meshRenderer == null || _propertyBlock == null)
                return;

            _meshRenderer.GetPropertyBlock(_propertyBlock);

            if (sprite == null || sprite.texture == null)
            {
                _propertyBlock.SetColor(ColorId, tint);
                _meshRenderer.SetPropertyBlock(_propertyBlock);
                return;
            }

            _propertyBlock.SetTexture(MainTexId, sprite.texture);

            Rect rect = sprite.textureRect;
            float textureWidth = sprite.texture.width;
            float textureHeight = sprite.texture.height;
            float uOffset = rect.x / textureWidth;
            float vOffset = rect.y / textureHeight;
            float uScale = rect.width / textureWidth;
            float vScale = rect.height / textureHeight;

            if (flipX)
            {
                uOffset += uScale;
                uScale = -uScale;
            }

            _propertyBlock.SetVector(MainTexStId, new Vector4(uScale, vScale, uOffset, vOffset));
            _propertyBlock.SetColor(ColorId, tint);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void KillSequence()
        {
            if (_sequence != null)
            {
                _sequence.Kill(false);
                _sequence = null;
            }

            if (visualRoot != null)
                DOTween.Kill(visualRoot, false);
        }
    }
}
