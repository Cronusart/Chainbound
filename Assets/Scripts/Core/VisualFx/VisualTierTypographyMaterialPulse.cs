using System;
using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Pulses TMP material glow/underlay intensity using a palette curve.
    /// Play-mode only — editor preview uses static shared materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualTierTypographyMaterialPulse : MonoBehaviour
    {
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");

        [SerializeField] private TMP_Text targetText;
        [SerializeField] private VisualTierPaletteSO palette;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private float intensityMin = 0.45f;
        [SerializeField] private float intensityMax = 1.75f;
        [SerializeField] private bool pulseGlowOuter = true;

        private float baseGlowPower = 0.35f;
        private float baseGlowOuter = 0.36f;
        private float baseUnderlaySoftness = 0.35f;
        private bool cachedBaseValues;
        private bool configured;

        public void Configure(
            TMP_Text text,
            VisualTierPaletteSO pulsePalette,
            float pulseMin = 0.45f,
            float pulseMax = 1.75f,
            bool scaleGlowOuter = true)
        {
            targetText = text;
            palette = pulsePalette;
            intensityMin = Mathf.Min(pulseMin, pulseMax);
            intensityMax = Mathf.Max(pulseMin, pulseMax);
            pulseGlowOuter = scaleGlowOuter;
            cachedBaseValues = false;
            configured = targetText != null && palette != null;
            enabled = Application.isPlaying && configured;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                enabled = false;
                return;
            }

            if (configured && targetText != null && palette != null)
                enabled = true;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || targetText == null || palette == null)
                return;

            if (!TryResolveWorkingMaterial(out Material material))
                return;

            if (!cachedBaseValues)
            {
                if (material.HasProperty(GlowPowerId))
                    baseGlowPower = material.GetFloat(GlowPowerId);
                if (material.HasProperty(GlowOuterId))
                    baseGlowOuter = material.GetFloat(GlowOuterId);
                if (material.HasProperty(UnderlaySoftnessId))
                    baseUnderlaySoftness = material.GetFloat(UnderlaySoftnessId);
                cachedBaseValues = true;
            }

            float pulse = palette.EvaluatePulse01(useUnscaledTime ? Time.unscaledTime : Time.time);
            float intensity = Mathf.Lerp(intensityMin, intensityMax, pulse);

            if (material.HasProperty(GlowPowerId))
                material.SetFloat(GlowPowerId, baseGlowPower * intensity);

            if (pulseGlowOuter && material.HasProperty(GlowOuterId))
                material.SetFloat(GlowOuterId, baseGlowOuter * intensity);

            if (material.HasProperty(UnderlaySoftnessId))
                material.SetFloat(UnderlaySoftnessId, baseUnderlaySoftness * intensity);
        }

        private bool TryResolveWorkingMaterial(out Material material)
        {
            material = null;
            if (targetText == null)
                return false;

            try
            {
                Material shared = targetText.fontSharedMaterial;
                if (shared == null)
                    return false;

                material = targetText.fontMaterial;
                return material != null;
            }
            catch (MissingReferenceException)
            {
                cachedBaseValues = false;
                enabled = false;
                return false;
            }
            catch (ArgumentException)
            {
                cachedBaseValues = false;
                enabled = false;
                return false;
            }
        }
    }
}
