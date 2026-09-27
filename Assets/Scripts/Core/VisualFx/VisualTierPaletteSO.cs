using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Shared color and intensity tokens reused across typography, mesh, and aura channels.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualTierPalette",
        menuName = "Chainbound/VFX/Visual Tier Palette")]
    public sealed class VisualTierPaletteSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string paletteId = "default";

        [Header("Core Tokens")]
        [SerializeField] private Color primaryColor = new Color(0.86f, 0.93f, 1f, 1f);
        [SerializeField] private Color secondaryColor = new Color(0.66f, 0.82f, 1f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.92f, 0.60f, 1f);
        [SerializeField] private Color glowColor = new Color(0.72f, 0.52f, 1f, 1f);
        [SerializeField] private Color rimColor = new Color(1f, 0.70f, 0.86f, 1f);
        [SerializeField] private Color shadowColor = new Color(0.12f, 0.14f, 0.20f, 0.85f);
        [SerializeField] private Color emissiveColor = new Color(0.92f, 0.86f, 1f, 1f);

        [Header("Global Modifiers")]
        [Range(0f, 3f)]
        [SerializeField] private float masterIntensity = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float alphaMultiplier = 1f;

        [Range(0f, 2f)]
        [SerializeField] private float saturationMultiplier = 1f;

        [Tooltip("Optional pulse curve (0-1 time). Used by animated aura/typography helpers.")]
        [SerializeField] private AnimationCurve intensityPulseCurve;

        [Min(0.01f)]
        [SerializeField] private float pulseDurationSeconds = 1.6f;

        public string PaletteId => paletteId;
        public float MasterIntensity => masterIntensity;
        public float AlphaMultiplier => alphaMultiplier;
        public float SaturationMultiplier => saturationMultiplier;
        public AnimationCurve IntensityPulseCurve => intensityPulseCurve;
        public float PulseDurationSeconds => pulseDurationSeconds;

        public Color ResolveColor(VisualTierColorSlot slot, Color customFallback)
        {
            Color baseColor = slot switch
            {
                VisualTierColorSlot.Primary => primaryColor,
                VisualTierColorSlot.Secondary => secondaryColor,
                VisualTierColorSlot.Accent => accentColor,
                VisualTierColorSlot.Glow => glowColor,
                VisualTierColorSlot.Rim => rimColor,
                VisualTierColorSlot.Shadow => shadowColor,
                VisualTierColorSlot.Emissive => emissiveColor,
                VisualTierColorSlot.Custom => customFallback,
                _ => Color.white,
            };

            return ApplyGlobalModifiers(baseColor);
        }

        public Color ApplyGlobalModifiers(Color color)
        {
            Color adjusted = color * masterIntensity;
            adjusted.a *= alphaMultiplier;

            if (Mathf.Approximately(saturationMultiplier, 1f))
                return adjusted;

            Color.RGBToHSV(adjusted, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * saturationMultiplier);
            Color result = Color.HSVToRGB(h, s, v);
            result.a = adjusted.a;
            return result;
        }

        public float EvaluatePulse01(float timeSeconds)
        {
            if (intensityPulseCurve == null || intensityPulseCurve.length == 0)
                return 1f;

            float duration = Mathf.Max(0.01f, pulseDurationSeconds);
            float t = Mathf.Repeat(timeSeconds, duration) / duration;
            return Mathf.Clamp01(intensityPulseCurve.Evaluate(t));
        }

        private void OnValidate()
        {
            masterIntensity = Mathf.Max(0f, masterIntensity);
            alphaMultiplier = Mathf.Clamp01(alphaMultiplier);
            saturationMultiplier = Mathf.Max(0f, saturationMultiplier);
            pulseDurationSeconds = Mathf.Max(0.01f, pulseDurationSeconds);
        }
    }
}
