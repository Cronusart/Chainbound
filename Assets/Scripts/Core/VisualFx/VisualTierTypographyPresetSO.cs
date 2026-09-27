using System;
using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    public enum VisualTierMaterialAssignmentMode
    {
        /// <summary>Replace sharedMaterial on the renderer/text (no instance).</summary>
        SharedMaterial = 0,

        /// <summary>Create/use a material instance for per-object overrides.</summary>
        MaterialInstance = 1,

        /// <summary>Keep existing material; only apply MaterialPropertyBlock overrides.</summary>
        PropertyBlockOnly = 2,
    }

    /// <summary>
    /// Typography / TMP material preset for a visual tier or variant.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualTierTypographyPreset",
        menuName = "Chainbound/VFX/Typography Preset")]
    public sealed class VisualTierTypographyPresetSO : ScriptableObject
    {
        [Header("Palette")]
        [Tooltip("Optional palette for this channel. Profiles can override with a shared palette.")]
        [SerializeField] private VisualTierPaletteSO palette;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private Material materialPreset;
        [SerializeField] private VisualTierMaterialAssignmentMode materialAssignment =
            VisualTierMaterialAssignmentMode.SharedMaterial;

        [Header("Face")]
        [SerializeField] private VisualTierColorSlot faceColorSlot = VisualTierColorSlot.Primary;
        [SerializeField] private Color customFaceColor = Color.white;
        [SerializeField] private Color faceColorTint = Color.white;
        [SerializeField] private bool applyFaceColorToTextColor = true;

        [Header("Outline / Underlay")]
        [SerializeField] private bool driveOutlineFromPalette = true;
        [SerializeField] private VisualTierTypographyChannelColorMode outlineColorMode =
            VisualTierTypographyChannelColorMode.Uniform;
        [SerializeField] private VisualTierColorSlot outlineColorSlot = VisualTierColorSlot.Shadow;
        [SerializeField] private Color customOutlineColor = Color.black;
        [Range(0f, 1f)]
        [SerializeField] private float outlineWidth = 0.2f;
        [SerializeField] private bool driveUnderlayFromPalette;
        [SerializeField] private VisualTierTypographyChannelColorMode underlayColorMode =
            VisualTierTypographyChannelColorMode.Uniform;
        [SerializeField] private VisualTierColorSlot underlayColorSlot = VisualTierColorSlot.Glow;
        [SerializeField] private Color customUnderlayColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Vector2 underlayOffset = new Vector2(0f, -0.4f);
        [Range(0f, 1f)]
        [SerializeField] private float underlayDilate = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float underlaySoftness = 0.35f;

        [Header("Glow")]
        [SerializeField] private bool driveGlowFromPalette;
        [SerializeField] private VisualTierTypographyChannelColorMode glowColorMode =
            VisualTierTypographyChannelColorMode.Uniform;
        [SerializeField] private VisualTierColorSlot glowColorSlot = VisualTierColorSlot.Glow;
        [SerializeField] private Color customGlowColor = Color.white;
        [Range(0f, 1f)]
        [SerializeField] private float glowPower = 0.35f;

        [Header("Transform")]
        [Min(0.01f)]
        [SerializeField] private float fontSizeMultiplier = 1f;
        [Min(0.01f)]
        [SerializeField] private float transformScaleMultiplier = 1f;

        [Header("Vertex Gradient")]
        [SerializeField] private bool useVertexGradient;
        [SerializeField] private VisualTierGradientScope gradientScope = VisualTierGradientScope.PerCharacter;
        [SerializeField] private VisualTierVertexGradientMode vertexGradientMode =
            VisualTierVertexGradientMode.Horizontal;
        [SerializeField] private VisualTierColorSlot vertexGradientStartSlot = VisualTierColorSlot.Primary;
        [SerializeField] private VisualTierColorSlot vertexGradientEndSlot = VisualTierColorSlot.Accent;
        [SerializeField] private Color customVertexGradientStart = Color.white;
        [SerializeField] private Color customVertexGradientEnd = Color.white;

        [Tooltip("Legacy aliases kept for existing assets.")]
        [SerializeField] private VisualTierColorSlot vertexGradientTopSlot = VisualTierColorSlot.Primary;
        [SerializeField] private VisualTierColorSlot vertexGradientBottomSlot = VisualTierColorSlot.Accent;
        [SerializeField] private Color customVertexGradientTop = Color.white;
        [SerializeField] private Color customVertexGradientBottom = Color.white;

        [Header("Animation")]
        [SerializeField] private bool animatePalettePulse;
        [Tooltip("Glow/underlay intensity multiplier at pulse trough.")]
        [SerializeField] private float pulseIntensityMin = 0.45f;
        [Tooltip("Glow/underlay intensity multiplier at pulse peak.")]
        [SerializeField] private float pulseIntensityMax = 1.75f;
        [Tooltip("Also scale TMP _GlowOuter with the pulse for a more obvious halo.")]
        [SerializeField] private bool pulseGlowOuter = true;

        [Header("Gradient Shimmer")]
        [Tooltip("Scroll the per-text-bounds gradient so colors travel through the label (Play mode).")]
        [SerializeField] private bool animateGradientShimmer;
        [Tooltip("Full gradient loops per second along the primary axis.")]
        [SerializeField] [Min(0f)] private float gradientShimmerSpeed = 0.35f;
        [SerializeField] private bool gradientShimmerUseUnscaledTime = true;

        [Header("Advanced Shader Overrides")]
        [SerializeField] private VisualTierShaderPropertyOverride[] materialPropertyOverrides =
            Array.Empty<VisualTierShaderPropertyOverride>();

        public VisualTierPaletteSO Palette => palette;
        public TMP_FontAsset FontAsset => fontAsset;
        public Material MaterialPreset => materialPreset;
        public VisualTierMaterialAssignmentMode MaterialAssignment => materialAssignment;
        public VisualTierColorSlot FaceColorSlot => faceColorSlot;
        public Color CustomFaceColor => customFaceColor;
        public Color FaceColorTint => faceColorTint;
        public bool ApplyFaceColorToTextColor => applyFaceColorToTextColor;
        public bool DriveOutlineFromPalette => driveOutlineFromPalette;
        public VisualTierTypographyChannelColorMode OutlineColorMode => outlineColorMode;
        public VisualTierColorSlot OutlineColorSlot => outlineColorSlot;
        public Color CustomOutlineColor => customOutlineColor;
        public float OutlineWidth => outlineWidth;
        public bool DriveUnderlayFromPalette => driveUnderlayFromPalette;
        public VisualTierTypographyChannelColorMode UnderlayColorMode => underlayColorMode;
        public VisualTierColorSlot UnderlayColorSlot => underlayColorSlot;
        public Color CustomUnderlayColor => customUnderlayColor;
        public Vector2 UnderlayOffset => underlayOffset;
        public float UnderlayDilate => underlayDilate;
        public float UnderlaySoftness => underlaySoftness;
        public bool DriveGlowFromPalette => driveGlowFromPalette;
        public VisualTierTypographyChannelColorMode GlowColorMode => glowColorMode;
        public VisualTierColorSlot GlowColorSlot => glowColorSlot;
        public Color CustomGlowColor => customGlowColor;
        public float GlowPower => glowPower;
        public float FontSizeMultiplier => fontSizeMultiplier;
        public float TransformScaleMultiplier => transformScaleMultiplier;
        public bool UseVertexGradient => useVertexGradient;
        public VisualTierGradientScope GradientScope => gradientScope;
        public VisualTierVertexGradientMode VertexGradientMode => vertexGradientMode;
        public VisualTierColorSlot VertexGradientStartSlot => vertexGradientStartSlot;
        public VisualTierColorSlot VertexGradientEndSlot => vertexGradientEndSlot;
        public Color CustomVertexGradientStart => customVertexGradientStart;
        public Color CustomVertexGradientEnd => customVertexGradientEnd;
        public VisualTierColorSlot VertexGradientTopSlot => vertexGradientTopSlot;
        public VisualTierColorSlot VertexGradientBottomSlot => vertexGradientBottomSlot;
        public Color CustomVertexGradientTop => customVertexGradientTop;
        public Color CustomVertexGradientBottom => customVertexGradientBottom;
        public bool AnimatePalettePulse => animatePalettePulse;
        public float PulseIntensityMin => pulseIntensityMin;
        public float PulseIntensityMax => pulseIntensityMax;
        public bool PulseGlowOuter => pulseGlowOuter;
        public bool AnimateGradientShimmer => animateGradientShimmer;
        public float GradientShimmerSpeed => gradientShimmerSpeed;
        public bool GradientShimmerUseUnscaledTime => gradientShimmerUseUnscaledTime;
        public VisualTierShaderPropertyOverride[] MaterialPropertyOverrides => materialPropertyOverrides;
    }
}
