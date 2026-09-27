using UnityEngine;

namespace SignalJam.VisualFx
{
    public enum VisualTierAuraAttachMode
    {
        ChildLocal = 0,
        WorldFollow = 1,
        UiOverlay = 2,
    }

    /// <summary>
    /// Particle aura preset attachable to sprites, UI, or 3D objects.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualTierAuraPreset",
        menuName = "Chainbound/VFX/Aura Preset")]
    public sealed class VisualTierAuraPresetSO : ScriptableObject
    {
        [Header("Palette")]
        [SerializeField] private VisualTierPaletteSO palette;

        [Header("Prefab")]
        [Tooltip("Optional authored aura prefab. When null, existing bound ParticleSystems are configured in place.")]
        [SerializeField] private GameObject auraPrefab;

        [SerializeField] private VisualTierAuraAttachMode attachMode = VisualTierAuraAttachMode.ChildLocal;
        [SerializeField] private Vector3 localPosition = Vector3.zero;
        [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private bool inheritTargetLossyScale;

        [Header("Authoring Preservation")]
        [Tooltip("When true, only tint/emission multipliers are applied; shape/trails/velocity stay authored.")]
        [SerializeField] private bool preserveAuthoringModules = true;

        [Header("Color")]
        [SerializeField] private VisualTierColorSlot startColorSlot = VisualTierColorSlot.Primary;
        [SerializeField] private Color customStartColor = Color.white;
        [SerializeField] private Color startColorTint = Color.white;
        [SerializeField] private bool driveColorOverLifetime;
        [SerializeField] private VisualTierColorSlot colorOverLifetimeSlot = VisualTierColorSlot.Glow;
        [SerializeField] private Color customColorOverLifetime = Color.white;
        [Range(0f, 1f)]
        [SerializeField] private float colorOverLifetimeAlpha = 0.35f;

        [Header("Multipliers")]
        [Min(0f)]
        [SerializeField] private float startSizeMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float startSpeedMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float lifetimeMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float emissionRateMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float shapeScaleMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private int maxParticlesOverride;

        [Header("Playback")]
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnApply = true;
        [SerializeField] private ParticleSystemSimulationSpace simulationSpace =
            ParticleSystemSimulationSpace.Local;

        [Header("Renderer")]
        [SerializeField] private Material rendererMaterialOverride;
        [SerializeField] private string sortingLayerName;
        [SerializeField] private int sortingOrder;

        [Header("Animation")]
        [SerializeField] private bool animateIntensityFromPalettePulse;
        [Range(0f, 2f)]
        [SerializeField] private float pulseEmissionMultiplier = 1f;

        public VisualTierPaletteSO Palette => palette;
        public GameObject AuraPrefab => auraPrefab;
        public VisualTierAuraAttachMode AttachMode => attachMode;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale;
        public bool InheritTargetLossyScale => inheritTargetLossyScale;
        public bool PreserveAuthoringModules => preserveAuthoringModules;
        public VisualTierColorSlot StartColorSlot => startColorSlot;
        public Color CustomStartColor => customStartColor;
        public Color StartColorTint => startColorTint;
        public bool DriveColorOverLifetime => driveColorOverLifetime;
        public VisualTierColorSlot ColorOverLifetimeSlot => colorOverLifetimeSlot;
        public Color CustomColorOverLifetime => customColorOverLifetime;
        public float ColorOverLifetimeAlpha => colorOverLifetimeAlpha;
        public float StartSizeMultiplier => startSizeMultiplier;
        public float StartSpeedMultiplier => startSpeedMultiplier;
        public float LifetimeMultiplier => lifetimeMultiplier;
        public float EmissionRateMultiplier => emissionRateMultiplier;
        public float ShapeScaleMultiplier => shapeScaleMultiplier;
        public int MaxParticlesOverride => maxParticlesOverride;
        public bool Loop => loop;
        public bool PlayOnApply => playOnApply;
        public ParticleSystemSimulationSpace SimulationSpace => simulationSpace;
        public Material RendererMaterialOverride => rendererMaterialOverride;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;
        public bool AnimateIntensityFromPalettePulse => animateIntensityFromPalettePulse;
        public float PulseEmissionMultiplier => pulseEmissionMultiplier;

        private void OnValidate()
        {
            startSizeMultiplier = Mathf.Max(0f, startSizeMultiplier);
            startSpeedMultiplier = Mathf.Max(0f, startSpeedMultiplier);
            lifetimeMultiplier = Mathf.Max(0f, lifetimeMultiplier);
            emissionRateMultiplier = Mathf.Max(0f, emissionRateMultiplier);
            shapeScaleMultiplier = Mathf.Max(0f, shapeScaleMultiplier);
            maxParticlesOverride = Mathf.Max(0, maxParticlesOverride);
            colorOverLifetimeAlpha = Mathf.Clamp01(colorOverLifetimeAlpha);
            pulseEmissionMultiplier = Mathf.Max(0f, pulseEmissionMultiplier);
        }
    }
}
