using UnityEngine;

namespace SignalJam.UI.VisualFx
{
    /// <summary>
    /// Data-driven playback contract for paired UI edge particle sweeps (e.g., top-L→R and bottom-R→L).
    /// Scene ParticleSystems retain shape/renderer/trails when <see cref="preserveAuthoringModules"/> is true.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSweepParticlePreset", menuName = "Chainbound/UI/VFX/Sweep Particle Preset")]
    public sealed class UiSweepParticlePresetSO : ScriptableObject
    {
        [Tooltip("Keep shape/velocity/trails/renderer from authored ParticleSystems. Runtime applies tint/max particles + sweep emission timing only.")]
        [SerializeField] private bool preserveAuthoringModules = true;

        [Min(0.02f)]
        [SerializeField] private float sweepDurationSeconds = 0.32f;

        [Tooltip(
            "Move each emitter RectTransform along the layout card width while emitting so bursts read as corner-to-corner. Disable to keep authored anchored positions.")]
        [SerializeField] private bool animateEmittersAlongCardWidth = true;

        [Tooltip("Shrinks horizontal travel so sparks stay inside the card bounds.")]
        [Min(0f)]
        [SerializeField] private float horizontalInsetPixels = 4f;

        [Tooltip("Easing along horizontal travel (X = normalized time). Defaults to linear when unset.")]
        [SerializeField] private AnimationCurve sweepProgressCurve;

        [Tooltip("Optional delay before the bottom stream starts (top stream begins immediately).")]
        [Min(0f)]
        [SerializeField] private float bottomStreamDelaySeconds;

        [Tooltip("Multiplies the contextual tint supplied by callers (e.g., battle accent color).")]
        [SerializeField] private Color tintMultiplier = Color.white;

        [Range(0f, 3f)]
        [SerializeField] private float alphaMultiplier = 1f;

        [Header("Bypass authoring (preserveAuthoringModules = false)")]
        [SerializeField] private float proceduralLifetimeSeconds = 0.28f;
        [SerializeField] private float proceduralSpeed = 70f;
        [SerializeField] private float proceduralStartSize = 4f;

        private static readonly AnimationCurve DefaultSweepProgressCurve =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public bool PreserveAuthoringModules => preserveAuthoringModules;
        public float SweepDurationSeconds => sweepDurationSeconds;
        public bool AnimateEmittersAlongCardWidth => animateEmittersAlongCardWidth;
        public float HorizontalInsetPixels => horizontalInsetPixels;
        public float BottomStreamDelaySeconds => bottomStreamDelaySeconds;
        public Color TintMultiplier => tintMultiplier;
        public float AlphaMultiplier => alphaMultiplier;
        public float ProceduralLifetimeSeconds => proceduralLifetimeSeconds;
        public float ProceduralSpeed => proceduralSpeed;
        public float ProceduralStartSize => proceduralStartSize;

        public float EvaluateSweepProgress(float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            AnimationCurve curve = sweepProgressCurve != null && sweepProgressCurve.keys.Length > 0
                ? sweepProgressCurve
                : DefaultSweepProgressCurve;
            return Mathf.Clamp01(curve.Evaluate(linear01));
        }

        private void OnValidate()
        {
            sweepDurationSeconds = Mathf.Max(0.02f, sweepDurationSeconds);
            horizontalInsetPixels = Mathf.Max(0f, horizontalInsetPixels);
            bottomStreamDelaySeconds = Mathf.Max(0f, bottomStreamDelaySeconds);
            proceduralLifetimeSeconds = Mathf.Max(0.01f, proceduralLifetimeSeconds);
            proceduralSpeed = Mathf.Max(0.01f, proceduralSpeed);
            proceduralStartSize = Mathf.Max(0.01f, proceduralStartSize);
        }
    }
}
