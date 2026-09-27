using System;
using DG.Tweening;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Reusable glitch visual effect overlay. Syncs mesh and texture from a source MeshRenderer,
    /// applies the SignalGlitch shader, and drives intensity/scale/dissolve via DOTween.
    /// Modes: GlitchIn (spawn), GlitchOut (faint), GlitchLoop (continuous).
    /// Lives in Combat for assembly reasons; can be used by Narrative/NPC via prefab references.
    /// </summary>
    public class SignalGlitchEffect : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The SignalGlitch material. Assign the material instance used by the overlay MeshRenderer.")]
        [SerializeField] private Material glitchMaterial;
        [Tooltip("The visual to overlay (syncs mesh + texture). Auto-found in siblings if null.")]
        [SerializeField] private MeshRenderer sourceMeshRenderer;

        [Header("Durations")]
        [SerializeField] private float defaultGlitchInDuration = 0.4f;
        [SerializeField] private float defaultGlitchOutDuration = 0.35f;

        [Header("Loop Mode")]
        [SerializeField] private float loopIntensity = 0.3f;

        [Header("Curves")]
        [Tooltip("Intensity over time for GlitchIn (1 -> 0).")]
        [SerializeField] private AnimationCurve glitchInCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Tooltip("Intensity over time for GlitchOut (0 -> 1).")]
        [SerializeField] private AnimationCurve glitchOutCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Scale over time (0->1 for in, 1->0 for out).")]
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private MeshRenderer _overlayRenderer;
        private MeshFilter _overlayFilter;
        private MaterialPropertyBlock _propertyBlock;
        private MaterialPropertyBlock _sourceBlock;
        private Sequence _activeSequence;
        private Tween _loopTween;
        private float _currentIntensity;
        private float _currentDissolve;
        private bool _isPlaying;

        private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
        private static readonly int DissolveThresholdId = Shader.PropertyToID("_DissolveThreshold");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

        public bool IsPlaying => _isPlaying;
        public float GlitchInDuration => defaultGlitchInDuration;
        public float GlitchOutDuration => defaultGlitchOutDuration;

        public void Configure(MeshRenderer source, Material material)
        {
            if (source != null)
                sourceMeshRenderer = source;

            if (_overlayRenderer == null)
                _overlayRenderer = GetComponent<MeshRenderer>();
            if (_overlayFilter == null)
                _overlayFilter = GetComponent<MeshFilter>();
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
            if (_sourceBlock == null)
                _sourceBlock = new MaterialPropertyBlock();

            if (material != null)
            {
                glitchMaterial = material;
                if (_overlayRenderer != null)
                    _overlayRenderer.sharedMaterial = material;
            }
            else if (glitchMaterial == null && _overlayRenderer != null)
            {
                glitchMaterial = _overlayRenderer.sharedMaterial;
            }

            if (sourceMeshRenderer == null)
                sourceMeshRenderer = FindSourceMeshRenderer();

            SyncRendererPresentationFromSource();
            SyncMeshFromSource();
            if (_overlayRenderer != null && !_isPlaying)
                _overlayRenderer.enabled = false;
        }

        private void Awake()
        {
            _overlayRenderer = GetComponent<MeshRenderer>();
            _overlayFilter = GetComponent<MeshFilter>();
            _propertyBlock = new MaterialPropertyBlock();
            _sourceBlock = new MaterialPropertyBlock();

            if (_overlayRenderer == null)
            {
                Debug.LogWarning("[SignalGlitchEffect] No MeshRenderer on " + gameObject.name);
                return;
            }

            if (glitchMaterial == null && _overlayRenderer.sharedMaterial != null)
                glitchMaterial = _overlayRenderer.sharedMaterial;

            if (sourceMeshRenderer == null)
                sourceMeshRenderer = FindSourceMeshRenderer();

            SyncRendererPresentationFromSource();
            SyncMeshFromSource();
            _overlayRenderer.enabled = false;
        }

        private void SyncRendererPresentationFromSource()
        {
            if (sourceMeshRenderer == null || _overlayRenderer == null)
                return;

            // Runtime-created overlays do not inherit their source object's Unity layer or
            // SRP rendering-layer mask. Match the source so cameras that render the player
            // can also render the glitch overlay, just like the authored battle overlays.
            gameObject.layer = sourceMeshRenderer.gameObject.layer;
            _overlayRenderer.renderingLayerMask = sourceMeshRenderer.renderingLayerMask;
            _overlayRenderer.sortingLayerID = sourceMeshRenderer.sortingLayerID;
            _overlayRenderer.sortingOrder = sourceMeshRenderer.sortingOrder;
            _overlayRenderer.rendererPriority = sourceMeshRenderer.rendererPriority;
        }

        private MeshRenderer FindSourceMeshRenderer()
        {
            if (transform.parent == null) return null;
            var renderers = transform.parent.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                if (mr != _overlayRenderer && mr.gameObject.name.Contains("Visual"))
                    return mr;
            }
            foreach (var mr in renderers)
            {
                if (mr != _overlayRenderer)
                    return mr;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (!_isPlaying || sourceMeshRenderer == null || _overlayRenderer == null) return;
            SyncTextureFromSource();
        }

        private void SyncMeshFromSource()
        {
            if (_overlayFilter == null || sourceMeshRenderer == null) return;
            var sourceFilter = sourceMeshRenderer.GetComponent<MeshFilter>();
            if (sourceFilter != null && sourceFilter.sharedMesh != null)
                _overlayFilter.sharedMesh = sourceFilter.sharedMesh;
        }

        private void SyncTextureFromSource()
        {
            if (sourceMeshRenderer == null || _overlayRenderer == null) return;
            sourceMeshRenderer.GetPropertyBlock(_sourceBlock);
            _overlayRenderer.GetPropertyBlock(_propertyBlock);
            if (_sourceBlock.HasTexture(MainTexId))
            {
                _propertyBlock.SetTexture(MainTexId, _sourceBlock.GetTexture(MainTexId));
                if (_sourceBlock.HasVector(MainTexStId))
                    _propertyBlock.SetVector(MainTexStId, _sourceBlock.GetVector(MainTexStId));
            }
            ApplyPropertyBlock();
        }

        private void ApplyPropertyBlock()
        {
            if (_overlayRenderer == null) return;
            _propertyBlock.SetFloat(GlitchIntensityId, _currentIntensity);
            _propertyBlock.SetFloat(DissolveThresholdId, _currentDissolve);
            _overlayRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Hide the source visual without showing the overlay. Covered sanctuary
        /// spawn uses this so GlitchIn materializes instead of hiding a visible body.
        /// </summary>
        public void HideSourceUntilGlitchIn()
        {
            if (sourceMeshRenderer != null)
                sourceMeshRenderer.enabled = false;
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;
        }

        /// <summary>Scale up with glitch, then resolve to true visual.</summary>
        public void PlayGlitchIn(float duration = -1, Action onComplete = null)
        {
            PlayGlitchIn(duration, unscaledTime: false, onComplete);
        }

        public void PlayGlitchIn(float duration, bool unscaledTime, Action onComplete)
        {
            KillActive();
            if (sourceMeshRenderer == null || _overlayRenderer == null) return;

            float d = duration > 0 ? duration : defaultGlitchInDuration;
            _isPlaying = true;

            SyncMeshFromSource();
            SyncTextureFromSource();

            sourceMeshRenderer.enabled = false;
            _overlayRenderer.enabled = true;
            transform.localScale = Vector3.zero;
            _currentIntensity = 1f;
            _currentDissolve = 0f;
            ApplyPropertyBlock();

            var scaleTween = DOTween.To(
                () => 0f,
                t =>
                {
                    float s = scaleCurve != null ? scaleCurve.Evaluate(t) : t;
                    transform.localScale = Vector3.one * s;
                },
                1f, d).SetEase(Ease.Linear);

            var intensityTween = DOTween.To(
                () => 1f,
                x =>
                {
                    float t = 1f - x;
                    _currentIntensity = glitchInCurve != null ? glitchInCurve.Evaluate(t) : x;
                    ApplyPropertyBlock();
                },
                0f, d).SetEase(Ease.Linear);

            if (unscaledTime)
            {
                scaleTween.SetUpdate(true);
                intensityTween.SetUpdate(true);
            }

            Sequence sequence = DOTween.Sequence()
                .Join(scaleTween)
                .Join(intensityTween);
            if (unscaledTime)
                sequence.SetUpdate(true);

            _activeSequence = sequence.OnComplete(() =>
            {
                _activeSequence = null;
                _isPlaying = false;
                _overlayRenderer.enabled = false;
                sourceMeshRenderer.enabled = true;
                transform.localScale = Vector3.one;
                onComplete?.Invoke();
            });
        }

        /// <summary>Intense glitch as it scales down and dissolves.</summary>
        public void PlayGlitchOut(float duration = -1, Action onComplete = null)
        {
            PlayGlitchOutInternal(duration, scaleDown: true, unscaledTime: false, onComplete);
        }

        public void PlayGlitchOut(float duration, bool unscaledTime, Action onComplete)
        {
            PlayGlitchOutInternal(duration, scaleDown: true, unscaledTime, onComplete);
        }

        /// <summary>Glitch and dissolve in place without shrinking. Used for sacred stone departure.</summary>
        public void PlayDissolveOut(float duration = -1, Action onComplete = null)
        {
            PlayGlitchOutInternal(duration, scaleDown: false, unscaledTime: false, onComplete);
        }

        private void PlayGlitchOutInternal(float duration, bool scaleDown, bool unscaledTime, Action onComplete)
        {
            KillActive();
            if (sourceMeshRenderer == null || _overlayRenderer == null) return;

            float d = duration > 0 ? duration : defaultGlitchOutDuration;
            _isPlaying = true;

            SyncMeshFromSource();
            SyncTextureFromSource();

            sourceMeshRenderer.enabled = false;
            _overlayRenderer.enabled = true;
            transform.localScale = Vector3.one;
            _currentIntensity = 0f;
            _currentDissolve = 0f;
            ApplyPropertyBlock();

            var intensityTween = DOTween.To(
                () => 0f,
                x =>
                {
                    _currentIntensity = glitchOutCurve != null ? glitchOutCurve.Evaluate(x) : x;
                    ApplyPropertyBlock();
                },
                1f, d).SetEase(Ease.Linear);

            Tween scaleTween = null;
            if (scaleDown)
            {
                scaleTween = DOTween.To(
                    () => 1f,
                    t =>
                    {
                        float s = scaleCurve != null ? scaleCurve.Evaluate(t) : t;
                        transform.localScale = Vector3.one * s;
                    },
                    0f, d).SetEase(Ease.Linear);
            }

            float dissolveStart = d * 0.45f;
            float dissolveDur = d * 0.55f;
            var dissolveTween = DOTween.Sequence()
                .AppendInterval(dissolveStart)
                .Append(DOTween.To(
                    () => 0f,
                    x =>
                    {
                        _currentDissolve = x;
                        ApplyPropertyBlock();
                    },
                    1f, dissolveDur).SetEase(Ease.Linear));

            Sequence sequence = DOTween.Sequence()
                .Join(intensityTween)
                .Join(dissolveTween);
            if (scaleTween != null)
                sequence.Join(scaleTween);
            if (unscaledTime)
            {
                intensityTween.SetUpdate(true);
                if (scaleTween != null)
                    scaleTween.SetUpdate(true);
                sequence.SetUpdate(true);
            }

            _activeSequence = sequence.OnComplete(() =>
            {
                _activeSequence = null;
                _isPlaying = false;
                _overlayRenderer.enabled = false;
                onComplete?.Invoke();
            });
        }

        /// <summary>Ongoing looped glitch at loopIntensity.</summary>
        public void PlayGlitchLoop()
        {
            KillActive();
            if (sourceMeshRenderer == null || _overlayRenderer == null) return;

            SyncMeshFromSource();
            SyncTextureFromSource();

            _overlayRenderer.enabled = true;
            transform.localScale = Vector3.one;
            _currentIntensity = loopIntensity;
            _currentDissolve = 0f;
            ApplyPropertyBlock();
            _isPlaying = true;

            float minI = loopIntensity * 0.6f;
            float maxI = loopIntensity * 1.2f;
            _loopTween = DOTween.To(
                () => _currentIntensity,
                x =>
                {
                    _currentIntensity = x;
                    ApplyPropertyBlock();
                },
                maxI, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>Stop any active glitch, hide overlay.</summary>
        public void StopGlitch()
        {
            KillActive();
            _isPlaying = false;
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;
            if (sourceMeshRenderer != null)
                sourceMeshRenderer.enabled = true;
            if (transform != null)
                transform.localScale = Vector3.one;
        }

        private void KillActive()
        {
            _activeSequence?.Kill();
            _activeSequence = null;
            _loopTween?.Kill();
            _loopTween = null;
        }

        private void OnDisable()
        {
            KillActive();
            _isPlaying = false;
        }
    }
}
