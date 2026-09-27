using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// One-shot scale-up + alpha fade for static Pixel Battle quads (e.g. impact cores).
    /// Works alongside <see cref="PixelBattleVfxApplicator"/> by animating _Color alpha
    /// and transform scale via MaterialPropertyBlock without replacing preset styling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelBattleVfxScaleFadePulse : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Targets")]
        [SerializeField] private Transform pulseTransform;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Min(0)] private int materialSlotIndex;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float duration = 0.35f;
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool disableRendererWhenComplete = true;

        [Header("Scale")]
        [SerializeField, Min(0f)] private float startScaleMultiplier = 1f;
        [SerializeField, Min(0f)] private float endScaleMultiplier = 2.25f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Fade")]
        [SerializeField, Range(0f, 1f)] private float startAlphaMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float endAlphaMultiplier;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        private MaterialPropertyBlock propertyBlock;
        private Vector3 authoredLocalScale = Vector3.one;
        private Color baseTint = Color.white;
        private float elapsed;
        private bool playing;
        private bool baselineCaptured;
        private bool finished;

        public float Duration => duration;

        public void Configure(
            float pulseDuration,
            float endScaleMultiplier,
            float endAlphaMultiplier = 0f,
            AnimationCurve scale = null,
            AnimationCurve fade = null)
        {
            duration = Mathf.Max(0.01f, pulseDuration);
            this.endScaleMultiplier = Mathf.Max(0f, endScaleMultiplier);
            this.endAlphaMultiplier = Mathf.Clamp01(endAlphaMultiplier);
            if (scale != null && scale.length > 0)
                scaleCurve = scale;
            if (fade != null && fade.length > 0)
                fadeCurve = fade;

            Restart();
        }

        public void Restart()
        {
            ResolveTargets();
            CaptureAuthoredScale();
            elapsed = 0f;
            playing = playOnEnable;
            finished = false;
            baselineCaptured = false;

            if (targetRenderer != null)
                targetRenderer.enabled = true;

            ApplyInstant(0f);
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Restart();
        }

        private void Update()
        {
            if (!playing || finished)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            StepPlayback(deltaTime);
        }

        public void StepPlayback(float deltaTime)
        {
            if (finished)
                return;

            ResolveTargets();
            if (pulseTransform == null || targetRenderer == null)
                return;

            if (!baselineCaptured)
                CaptureBaselineTint();

            elapsed += Mathf.Max(0f, deltaTime);

            if (elapsed < startDelay)
                return;

            float effectTime = elapsed - startDelay;
            float normalizedTime = duration > 0f ? Mathf.Clamp01(effectTime / duration) : 1f;
            ApplyInstant(normalizedTime);

            if (effectTime >= duration)
                Complete();
        }

        private void ApplyInstant(float normalizedTime)
        {
            if (pulseTransform == null || targetRenderer == null)
                return;

            if (!baselineCaptured)
                CaptureBaselineTint();

            float scaleT = scaleCurve != null && scaleCurve.length > 0
                ? scaleCurve.Evaluate(normalizedTime)
                : normalizedTime;
            float scaleMultiplier = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, scaleT);
            pulseTransform.localScale = authoredLocalScale * scaleMultiplier;

            float fadeT = fadeCurve != null && fadeCurve.length > 0
                ? fadeCurve.Evaluate(normalizedTime)
                : normalizedTime;
            float alphaMultiplier = Mathf.Lerp(startAlphaMultiplier, endAlphaMultiplier, fadeT);

            Color color = baseTint;
            color.a *= alphaMultiplier;

            propertyBlock ??= new MaterialPropertyBlock();
            int slot = Mathf.Max(0, materialSlotIndex);
            targetRenderer.GetPropertyBlock(propertyBlock, slot);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock, slot);
        }

        private void Complete()
        {
            finished = true;
            playing = false;

            if (disableRendererWhenComplete && targetRenderer != null)
                targetRenderer.enabled = false;
        }

        private void ResolveTargets()
        {
            if (pulseTransform == null)
                pulseTransform = transform;

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }

        private void CaptureAuthoredScale()
        {
            if (pulseTransform == null)
                return;

            authoredLocalScale = pulseTransform.localScale;
            if (authoredLocalScale == Vector3.zero)
                authoredLocalScale = Vector3.one;
        }

        private void CaptureBaselineTint()
        {
            if (targetRenderer == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            int slot = Mathf.Max(0, materialSlotIndex);
            targetRenderer.GetPropertyBlock(propertyBlock, slot);

            Material referenceMaterial = ResolveReferenceMaterial(slot);
            if (propertyBlock.HasColor(ColorId))
            {
                baseTint = propertyBlock.GetColor(ColorId);
            }
            else if (referenceMaterial != null && referenceMaterial.HasProperty(ColorId))
            {
                baseTint = referenceMaterial.GetColor(ColorId);
            }
            else
            {
                baseTint = Color.white;
            }

            baselineCaptured = true;
        }

        private Material ResolveReferenceMaterial(int slot)
        {
            Material[] materials = targetRenderer.sharedMaterials;
            if (materials != null && slot < materials.Length && materials[slot] != null)
                return materials[slot];

            return targetRenderer.sharedMaterial;
        }

        private void OnValidate()
        {
            materialSlotIndex = Mathf.Max(0, materialSlotIndex);
            duration = Mathf.Max(0.01f, duration);
            startDelay = Mathf.Max(0f, startDelay);
            startScaleMultiplier = Mathf.Max(0f, startScaleMultiplier);
            endScaleMultiplier = Mathf.Max(0f, endScaleMultiplier);
            startAlphaMultiplier = Mathf.Clamp01(startAlphaMultiplier);
            endAlphaMultiplier = Mathf.Clamp01(endAlphaMultiplier);

            if (scaleCurve == null || scaleCurve.length == 0)
                scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            if (fadeCurve == null || fadeCurve.length == 0)
                fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        }
    }
}
