using System;
using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Applies typography channel presets to TMP targets.
    /// </summary>
    public static class VisualTierTypographyApplication
    {
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");

        public static void Apply(
            TMP_Text text,
            VisualTierProfileSO profile,
            float baseFontSize = 0f,
            float externalScaleMultiplier = 1f)
        {
            if (text == null || profile == null)
                return;

            VisualTierTypographyPresetSO preset = profile.TypographyPreset;
            if (preset == null)
                return;

            VisualTierPaletteSO palette = profile.ResolvePaletteForTypography();
            Material sharedFallback = ResolvePreferredSharedMaterial(text, preset);

            // Always restore a live shared material before any TMP mesh/material access.
            ClearMaterialInstance(text, sharedFallback);

            Color gradientStart = default;
            Color gradientEnd = default;
            bool hasVertexGradient = preset.UseVertexGradient;
            if (hasVertexGradient)
            {
                gradientStart = ResolveGradientColor(
                    palette,
                    preset.VertexGradientStartSlot,
                    preset.CustomVertexGradientStart,
                    preset.VertexGradientTopSlot,
                    preset.CustomVertexGradientTop);
                gradientEnd = ResolveGradientColor(
                    palette,
                    preset.VertexGradientEndSlot,
                    preset.CustomVertexGradientEnd,
                    preset.VertexGradientBottomSlot,
                    preset.CustomVertexGradientBottom);
            }

            if (preset.FontAsset != null)
                text.font = preset.FontAsset;

            Material workingMaterial = ResolveWorkingMaterial(text, preset, out bool appliedPresetMaterial);
            if (workingMaterial != null &&
                (appliedPresetMaterial || preset.MaterialAssignment != VisualTierMaterialAssignmentMode.PropertyBlockOnly))
            {
                ApplyMaterialProperties(workingMaterial, preset, palette, gradientStart, gradientEnd, hasVertexGradient);
            }
            else if (workingMaterial != null && HasShaderOverrides(preset))
            {
                if (TryGetFontMaterial(text, out Material instanceMaterial) && instanceMaterial != null)
                    ApplyMaterialProperties(instanceMaterial, preset, palette, gradientStart, gradientEnd, hasVertexGradient);
            }

            if (preset.ApplyFaceColorToTextColor)
            {
                Color face = VisualTierPaletteUtility.ResolveTintedSlot(
                    palette,
                    preset.FaceColorSlot,
                    preset.CustomFaceColor,
                    preset.FaceColorTint,
                    text.color);
                text.color = face;
            }

            if (baseFontSize > 0f)
                text.fontSize = baseFontSize * preset.FontSizeMultiplier;
            else if (!Mathf.Approximately(preset.FontSizeMultiplier, 1f))
                text.fontSize *= preset.FontSizeMultiplier;

            float scale = preset.TransformScaleMultiplier * externalScaleMultiplier;
            if (!Mathf.Approximately(scale, 1f))
                text.rectTransform.localScale = Vector3.one * scale;

            ApplyVertexGradient(text, preset, gradientStart, gradientEnd);
            ConfigurePulseDriver(text, preset, palette);

            if (preset.UseVertexGradient)
                SafeForceMeshUpdate(text);
        }

        /// <summary>
        /// Restores a serializable shared material and strips any destroyed/runtime instance refs.
        /// Safe to call when TMP currently points at a destroyed material.
        /// </summary>
        public static void ClearMaterialInstance(TMP_Text text, Material fallbackSharedMaterial = null)
        {
            if (text == null)
                return;

            Material preferredShared = IsLiveMaterial(fallbackSharedMaterial)
                ? fallbackSharedMaterial
                : ResolveFontDefaultMaterial(text);

            TryGetSharedMaterial(text, out Material previousShared);

            // Assign a live shared material first so TMP never clones from a destroyed source.
            // Never assign null through fontMaterial — TMP's setter calls value.GetInstanceID().
            if (IsLiveMaterial(preferredShared))
                SafeSetSharedMaterial(text, preferredShared);

            // Drop orphaned runtime clones that were parked on fontSharedMaterial.
            // Do not touch fontMaterial getter — it creates a new instance from shared.
            if (IsLiveMaterial(previousShared) &&
                !ReferenceEquals(previousShared, preferredShared) &&
                !IsAssetMaterial(previousShared))
            {
                DestroyMaterialObject(previousShared);
            }

            EnsureSharedMaterial(text, preferredShared);
        }

        /// <summary>
        /// Strips runtime material instances and keeps a serializable shared preset for prefab/scene saves.
        /// </summary>
        public static void PrepareForPrefabSerialization(TMP_Text text, VisualTierProfileSO profile)
        {
            if (text == null)
                return;

            Material sharedPreset = profile != null ? profile.TypographyPreset?.MaterialPreset : null;
            ClearMaterialInstance(text, sharedPreset);

            if (IsLiveMaterial(sharedPreset))
                SafeSetSharedMaterial(text, sharedPreset);

            // Gradient typography requires a white face; otherwise Distance Field multiplies
            // vertex colors into a flat tint of the face color.
            if (profile != null &&
                profile.TypographyPreset != null &&
                profile.TypographyPreset.UseVertexGradient &&
                IsLiveMaterial(sharedPreset) &&
                sharedPreset.HasProperty("_FaceColor"))
            {
                sharedPreset.SetColor("_FaceColor", Color.white);
            }

            EnsureSharedMaterial(text, sharedPreset);

            VisualTierTypographyBoundsGradientDriver boundsDriver =
                text.GetComponent<VisualTierTypographyBoundsGradientDriver>();
            if (boundsDriver != null)
                boundsDriver.ApplyNow();
        }

        public static bool SafeForceMeshUpdate(TMP_Text text)
        {
            if (text == null)
                return false;

            if (!TryGetSharedMaterial(text, out Material shared) || !IsLiveMaterial(shared))
            {
                Material fallback = ResolveFontDefaultMaterial(text);
                if (!IsLiveMaterial(fallback))
                    return false;

                SafeSetSharedMaterial(text, fallback);
            }

            try
            {
                text.ForceMeshUpdate(true, false);
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static Color ResolveGradientColor(
            VisualTierPaletteSO palette,
            VisualTierColorSlot slot,
            Color customColor,
            VisualTierColorSlot legacySlot,
            Color legacyCustomColor)
        {
            VisualTierColorSlot resolvedSlot = slot != VisualTierColorSlot.None ? slot : legacySlot;
            Color custom = resolvedSlot == VisualTierColorSlot.Custom
                ? customColor.a > 0f ? customColor : legacyCustomColor
                : legacyCustomColor.a > 0f && legacyCustomColor != Color.white
                    ? legacyCustomColor
                    : customColor;

            Color raw = VisualTierPaletteUtility.ResolveSlot(
                palette,
                resolvedSlot,
                custom,
                custom);

            return new Color(
                Mathf.Clamp01(raw.r),
                Mathf.Clamp01(raw.g),
                Mathf.Clamp01(raw.b),
                Mathf.Clamp01(raw.a));
        }

        private static void ApplyVertexGradient(
            TMP_Text text,
            VisualTierTypographyPresetSO preset,
            Color start,
            Color end)
        {
            VisualTierTypographyBoundsGradientDriver boundsDriver =
                text != null ? text.GetComponent<VisualTierTypographyBoundsGradientDriver>() : null;

            if (text == null || preset == null || !preset.UseVertexGradient)
            {
                if (text != null)
                    text.enableVertexGradient = false;

                if (boundsDriver != null)
                    boundsDriver.DisableDriver();

                return;
            }

            text.color = Color.white;

            if (preset.GradientScope == VisualTierGradientScope.PerTextBounds)
            {
                text.enableVertexGradient = false;

                if (boundsDriver == null)
                    boundsDriver = text.gameObject.AddComponent<VisualTierTypographyBoundsGradientDriver>();

                boundsDriver.Configure(
                    text,
                    new VisualTierTypographyBoundsGradient.Settings
                    {
                        Enabled = true,
                        Mode = preset.VertexGradientMode,
                        Start = start,
                        End = end,
                        ScrollOffset01 = 0f,
                    });
                boundsDriver.ConfigureShimmer(
                    preset.AnimateGradientShimmer,
                    preset.GradientShimmerSpeed,
                    preset.GradientShimmerUseUnscaledTime);
                return;
            }

            if (boundsDriver != null)
                boundsDriver.DisableDriver();

            switch (preset.VertexGradientMode)
            {
                case VisualTierVertexGradientMode.Vertical:
                    text.colorGradient = new VertexGradient(start, start, end, end);
                    break;

                case VisualTierVertexGradientMode.SolanaDiagonal:
                    text.colorGradient = new VertexGradient(start, end, end, start);
                    break;

                default:
                    text.colorGradient = new VertexGradient(start, end, start, end);
                    break;
            }

            text.enableVertexGradient = true;
        }

        private static Color ResolveChannelColor(
            VisualTierTypographyPresetSO preset,
            VisualTierPaletteSO palette,
            VisualTierTypographyChannelColorMode mode,
            bool driveFromPalette,
            VisualTierColorSlot slot,
            Color customColor,
            Color gradientStart,
            Color gradientEnd,
            bool hasVertexGradient)
        {
            if (!driveFromPalette)
                return customColor;

            if (hasVertexGradient && mode != VisualTierTypographyChannelColorMode.Uniform)
            {
                switch (mode)
                {
                    case VisualTierTypographyChannelColorMode.GradientStart:
                        return gradientStart;
                    case VisualTierTypographyChannelColorMode.GradientEnd:
                        return gradientEnd;
                    case VisualTierTypographyChannelColorMode.GradientMidpoint:
                        return Color.Lerp(gradientStart, gradientEnd, 0.5f);
                }
            }

            return VisualTierPaletteUtility.ResolveSlot(
                palette,
                slot,
                customColor,
                customColor);
        }

        private static void ConfigurePulseDriver(
            TMP_Text text,
            VisualTierTypographyPresetSO preset,
            VisualTierPaletteSO palette)
        {
            if (text == null)
                return;

            VisualTierTypographyMaterialPulse pulse = text.GetComponent<VisualTierTypographyMaterialPulse>();
            if (preset != null && preset.AnimatePalettePulse && palette != null)
            {
                if (pulse == null)
                    pulse = text.gameObject.AddComponent<VisualTierTypographyMaterialPulse>();
                pulse.Configure(
                    text,
                    palette,
                    preset.PulseIntensityMin,
                    preset.PulseIntensityMax,
                    preset.PulseGlowOuter);
            }
            else if (pulse != null)
            {
                pulse.enabled = false;
            }
        }

        private static void EnsureSharedMaterial(TMP_Text text, Material fallbackSharedMaterial)
        {
            if (text == null)
                return;

            if (TryGetSharedMaterial(text, out Material shared) && IsLiveMaterial(shared))
                return;

            if (IsLiveMaterial(fallbackSharedMaterial))
            {
                SafeSetSharedMaterial(text, fallbackSharedMaterial);
                return;
            }

            Material fontDefault = ResolveFontDefaultMaterial(text);
            if (IsLiveMaterial(fontDefault))
                SafeSetSharedMaterial(text, fontDefault);
        }

        private static Material ResolveWorkingMaterial(
            TMP_Text text,
            VisualTierTypographyPresetSO preset,
            out bool appliedPresetMaterial)
        {
            appliedPresetMaterial = false;

            Material preferredShared = ResolvePreferredSharedMaterial(text, preset);

            if (preset.MaterialPreset == null)
            {
                if (TryGetSharedMaterial(text, out Material existingShared) && IsLiveMaterial(existingShared))
                    return existingShared;

                return preferredShared;
            }

            VisualTierMaterialAssignmentMode mode = ResolveAssignmentMode(preset);

            switch (mode)
            {
                case VisualTierMaterialAssignmentMode.SharedMaterial:
                    SafeSetSharedMaterial(text, preset.MaterialPreset);
                    appliedPresetMaterial = true;
                    return preset.MaterialPreset;

                case VisualTierMaterialAssignmentMode.MaterialInstance:
                {
                    // Prefer an explicit preview/runtime clone assigned as shared.
                    // Avoid TMP.fontMaterial cloning from a possibly-destroyed shared source.
                    Material working = CreateWorkingMaterialInstance(text, preset.MaterialPreset);
                    SafeSetSharedMaterial(text, working);
                    appliedPresetMaterial = true;
                    return working;
                }

                case VisualTierMaterialAssignmentMode.PropertyBlockOnly:
                    if (TryGetSharedMaterial(text, out Material shared) && IsLiveMaterial(shared))
                        return shared;
                    return preferredShared;

                default:
                    SafeSetSharedMaterial(text, preset.MaterialPreset);
                    appliedPresetMaterial = true;
                    return preset.MaterialPreset;
            }
        }

        private static VisualTierMaterialAssignmentMode ResolveAssignmentMode(VisualTierTypographyPresetSO preset)
        {
            if (preset == null)
                return VisualTierMaterialAssignmentMode.SharedMaterial;

            return preset.MaterialAssignment;
        }

        private static Material CreateWorkingMaterialInstance(TMP_Text text, Material source)
        {
            if (!IsLiveMaterial(source))
                return source;

            // Reuse an existing non-asset working material when possible.
            if (TryGetSharedMaterial(text, out Material current) &&
                IsLiveMaterial(current) &&
                !IsAssetMaterial(current) &&
                current.shader == source.shader)
            {
                current.CopyPropertiesFromMaterial(source);
                return current;
            }

            Material clone = new Material(source)
            {
                name = source.name + " (Instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return clone;
        }

        private static Material ResolvePreferredSharedMaterial(TMP_Text text, VisualTierTypographyPresetSO preset)
        {
            if (preset != null && IsLiveMaterial(preset.MaterialPreset))
                return preset.MaterialPreset;

            if (TryGetSharedMaterial(text, out Material shared) && IsLiveMaterial(shared))
                return shared;

            return ResolveFontDefaultMaterial(text);
        }

        private static Material ResolveFontDefaultMaterial(TMP_Text text)
        {
            if (text == null)
                return null;

            TMP_FontAsset font = text.font;
            if (font != null && IsLiveMaterial(font.material))
                return font.material;

            return null;
        }

        private static bool TryGetSharedMaterial(TMP_Text text, out Material material)
        {
            material = null;
            if (text == null)
                return false;

            try
            {
                material = text.fontSharedMaterial;
                return IsLiveMaterial(material);
            }
            catch (MissingReferenceException)
            {
                material = null;
                return false;
            }
            catch (ArgumentException)
            {
                material = null;
                return false;
            }
        }

        private static bool TryGetFontMaterial(TMP_Text text, out Material material)
        {
            material = null;
            if (text == null)
                return false;

            // fontMaterial clones from shared; refuse if shared is already dead.
            if (!TryGetSharedMaterial(text, out _))
                return false;

            try
            {
                material = text.fontMaterial;
                return IsLiveMaterial(material);
            }
            catch (MissingReferenceException)
            {
                material = null;
                return false;
            }
            catch (ArgumentException)
            {
                material = null;
                return false;
            }
        }

        private static void SafeSetSharedMaterial(TMP_Text text, Material material)
        {
            if (text == null || !IsLiveMaterial(material))
                return;

            try
            {
                text.fontSharedMaterial = material;
            }
            catch (MissingReferenceException)
            {
                // Ignore — caller will retry EnsureSharedMaterial if needed.
            }
            catch (ArgumentException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        private static void SafeSetFontMaterial(TMP_Text text, Material material)
        {
            // TMP_Text.fontMaterial setter calls value.GetInstanceID() with no null check.
            if (text == null || !IsLiveMaterial(material))
                return;

            try
            {
                text.fontMaterial = material;
            }
            catch (MissingReferenceException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NullReferenceException)
            {
            }
        }

        private static bool IsLiveMaterial(Material material)
        {
            return material != null;
        }

        private static bool IsAssetMaterial(Material material)
        {
            if (!IsLiveMaterial(material))
                return false;

#if UNITY_EDITOR
            return !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(material));
#else
            return false;
#endif
        }

        private static void DestroyMaterialObject(Material material)
        {
            if (!IsLiveMaterial(material) || IsAssetMaterial(material))
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(material);
                return;
            }
#endif
            UnityEngine.Object.Destroy(material);
        }

        private static bool HasShaderOverrides(VisualTierTypographyPresetSO preset)
        {
            return preset.DriveOutlineFromPalette ||
                   preset.DriveUnderlayFromPalette ||
                   preset.DriveGlowFromPalette ||
                   (preset.MaterialPropertyOverrides != null && preset.MaterialPropertyOverrides.Length > 0);
        }

        private static void ApplyMaterialProperties(
            Material material,
            VisualTierTypographyPresetSO preset,
            VisualTierPaletteSO palette,
            Color gradientStart,
            Color gradientEnd,
            bool hasVertexGradient)
        {
            if (!IsLiveMaterial(material))
                return;

            Color face = VisualTierPaletteUtility.ResolveTintedSlot(
                palette,
                preset.FaceColorSlot,
                preset.CustomFaceColor,
                preset.FaceColorTint,
                Color.white);

            if (hasVertexGradient)
                face = Color.white;

            SetColorIfPresent(material, FaceColorId, face);

            if (preset.DriveOutlineFromPalette)
            {
                Color outline = ResolveChannelColor(
                    preset,
                    palette,
                    preset.OutlineColorMode,
                    true,
                    preset.OutlineColorSlot,
                    preset.CustomOutlineColor,
                    gradientStart,
                    gradientEnd,
                    hasVertexGradient);
                SetColorIfPresent(material, OutlineColorId, outline);
            }

            SetFloatIfPresent(material, OutlineWidthId, preset.OutlineWidth);

            if (preset.DriveUnderlayFromPalette)
            {
                Color underlay = ResolveChannelColor(
                    preset,
                    palette,
                    preset.UnderlayColorMode,
                    true,
                    preset.UnderlayColorSlot,
                    preset.CustomUnderlayColor,
                    gradientStart,
                    gradientEnd,
                    hasVertexGradient);
                if (hasVertexGradient && preset.UnderlayColorMode != VisualTierTypographyChannelColorMode.Uniform)
                    underlay.a = preset.CustomUnderlayColor.a;
                SetColorIfPresent(material, UnderlayColorId, underlay);
            }

            SetFloatIfPresent(material, UnderlayOffsetXId, preset.UnderlayOffset.x);
            SetFloatIfPresent(material, UnderlayOffsetYId, preset.UnderlayOffset.y);
            SetFloatIfPresent(material, UnderlayDilateId, preset.UnderlayDilate);
            SetFloatIfPresent(material, UnderlaySoftnessId, preset.UnderlaySoftness);

            if (preset.DriveGlowFromPalette)
            {
                Color glow = ResolveChannelColor(
                    preset,
                    palette,
                    preset.GlowColorMode,
                    true,
                    preset.GlowColorSlot,
                    preset.CustomGlowColor,
                    gradientStart,
                    gradientEnd,
                    hasVertexGradient);
                SetColorIfPresent(material, GlowColorId, glow);
            }

            SetFloatIfPresent(material, GlowPowerId, preset.GlowPower);

            VisualTierShaderPropertyOverride[] overrides = preset.MaterialPropertyOverrides;
            if (overrides == null)
                return;

            for (int i = 0; i < overrides.Length; i++)
                overrides[i].ApplyTo(material);
        }

        private static void SetColorIfPresent(Material material, int propertyId, Color color)
        {
            if (material.HasProperty(propertyId))
                material.SetColor(propertyId, color);
        }

        private static void SetFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }
    }
}
