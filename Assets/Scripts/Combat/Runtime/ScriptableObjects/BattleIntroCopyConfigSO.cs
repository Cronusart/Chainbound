using System;
using System.Collections.Generic;
using SignalJam.VisualFx;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class BattleIntroTraitStyle
    {
        [Tooltip("When false, this trait is omitted even if the encounter rolled it.")]
        public bool includeWhenActive = true;

        [Tooltip("Shown label for prefix/suffix traits. Ignored for Name (uses the Chainling display name).")]
        public string label = string.Empty;

        public bool colorize = true;
        public BattleIntroTraitColorSource colorSource = BattleIntroTraitColorSource.Solid;
        public VisualTierColorSlot paletteSlot = VisualTierColorSlot.Primary;
        public Color fallbackColor = Color.white;

        [Tooltip("After the typewriter finishes, paint a vertex gradient on this span (Variant typically uses Cycle V).")]
        public bool applyVertexGradient;
    }

    /// <summary>
    /// Lever board for battle-intro copy, trait order, and per-trait typography.
    /// Presentation assets may override this; otherwise <see cref="BalanceConfigRuntime.BattleIntroCopy"/> is used.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BattleIntroCopyConfig",
        menuName = "Chainbound/Combat/Config/Battle Intro Copy Config")]
    public sealed class BattleIntroCopyConfigSO : ScriptableObject
    {
        public const string SubjectToken = "{subject}";
        public const string Subject0Token = "{subject0}";
        public const string Subject1Token = "{subject1}";
        public const string TeamToken = "{team}";

        [Header("Identity")]
        [Min(1)] public int configVersion = 1;

        [Header("Templates")]
        [Tooltip("Wild single. Tokens: {subject}")]
        [TextArea(1, 3)]
        public string wildSingleTemplate = "A wild {subject} has appeared!";

        [Tooltip("Wild dual. Tokens: {subject0} {subject1}")]
        [TextArea(1, 3)]
        public string wildDualTemplate = "A wild {subject0} and a wild {subject1} have appeared!";

        [Tooltip("Trainer / named opponent. Tokens: {team}")]
        [TextArea(1, 3)]
        public string trainerTemplate = "{team} has challenged you!";

        [Tooltip("PvP. No subject tokens required.")]
        [TextArea(1, 3)]
        public string pvpTemplate = "Opponent has challenged you!";

        [Header("Trait Order")]
        [Tooltip("Stable adjective stack. Not roll chronology. Default: mutated, entangled, name, variant.")]
        public List<BattleIntroTraitSlot> traitOrder = new List<BattleIntroTraitSlot>();

        [Tooltip("Inserted between adjacent active traits (e.g. mutated + entangled + name).")]
        public string traitSeparator = " ";

        [Header("Trait Styles")]
        public BattleIntroTraitStyle mutatedStyle = CreateStyle(
            "mutated",
            BattleIntroTraitColorSource.MutatedPalette,
            new Color(0.82f, 0.22f, 0.58f, 1f));

        public BattleIntroTraitStyle entangledStyle = CreateStyle(
            "entangled",
            BattleIntroTraitColorSource.EntangledTypeDisplay,
            new Color(0.97f, 0.48f, 0.69f, 1f));

        public BattleIntroTraitStyle nameStyle = CreateStyle(
            string.Empty,
            BattleIntroTraitColorSource.VfxAccent,
            new Color(1f, 0.78f, 0.22f, 1f));

        public BattleIntroTraitStyle variantStyle = CreateStyle(
            "variant",
            BattleIntroTraitColorSource.VariantProfile,
            new Color(0.6f, 0.27f, 1f, 1f),
            applyVertexGradient: true);

        [Header("Palettes")]
        [Tooltip("Type badge colors for entangled labels. Fallback colors are used when unset.")]
        public ChainlingTypeDisplaySO typeDisplay;

        [Tooltip("Mutated identity palette. Create via Chainbound/VFX/Visual Tier Palette.")]
        public VisualTierPaletteSO mutatedPalette;

        [Tooltip("Variant typography profile. Default: Cycle V / Max Cycle (Profile_cycle_v).")]
        public VisualTierProfileSO variantProfile;

        [Tooltip("When true, variant spans receive the variant profile vertex gradient after the typewriter completes.")]
        public bool applyVariantVertexGradient = true;

        public BattleIntroTraitStyle GetStyle(BattleIntroTraitSlot slot)
        {
            return slot switch
            {
                BattleIntroTraitSlot.Mutated => mutatedStyle,
                BattleIntroTraitSlot.Entangled => entangledStyle,
                BattleIntroTraitSlot.Name => nameStyle,
                BattleIntroTraitSlot.Variant => variantStyle,
                _ => nameStyle,
            };
        }

        public Color ResolveTraitColor(
            BattleIntroTraitSlot slot,
            BattleIntroCopySubject subject,
            Color vfxAccent)
        {
            BattleIntroTraitStyle style = GetStyle(slot);
            if (style == null || !style.colorize)
                return default;

            switch (style.colorSource)
            {
                case BattleIntroTraitColorSource.None:
                    return default;

                case BattleIntroTraitColorSource.Solid:
                    return style.fallbackColor;

                case BattleIntroTraitColorSource.MutatedPalette:
                    return ResolvePaletteColor(mutatedPalette, style.paletteSlot, style.fallbackColor);

                case BattleIntroTraitColorSource.EntangledTypeDisplay:
                {
                    ChainlingType type = subject.entangledType;
                    if (type == ChainlingType.None)
                        return style.fallbackColor;

                    ChainlingTypeDisplaySO.TypeDisplayEntry entry = typeDisplay != null
                        ? typeDisplay.GetEntryOrFallback(type)
                        : ChainlingTypeDisplaySO.CreateFallback(type);
                    return entry.badgeColor.a > 0.001f ? entry.badgeColor : style.fallbackColor;
                }

                case BattleIntroTraitColorSource.VariantProfile:
                {
                    VisualTierPaletteSO palette = variantProfile != null
                        ? variantProfile.ResolvePaletteForTypography()
                        : null;
                    return ResolvePaletteColor(palette, style.paletteSlot, style.fallbackColor);
                }

                case BattleIntroTraitColorSource.VfxAccent:
                    return vfxAccent.a > 0.001f ? vfxAccent : style.fallbackColor;

                default:
                    return style.fallbackColor;
            }
        }

        public bool ShouldApplyVertexGradient(BattleIntroTraitSlot slot)
        {
            BattleIntroTraitStyle style = GetStyle(slot);
            if (style == null || !style.applyVertexGradient)
                return false;

            if (slot == BattleIntroTraitSlot.Variant && !applyVariantVertexGradient)
                return false;

            return true;
        }

        public bool TryResolveVariantGradient(out Color start, out Color end, out VisualTierVertexGradientMode mode)
        {
            start = variantStyle != null ? variantStyle.fallbackColor : Color.white;
            end = start;
            mode = VisualTierVertexGradientMode.Horizontal;

            VisualTierTypographyPresetSO preset = variantProfile != null ? variantProfile.TypographyPreset : null;
            VisualTierPaletteSO palette = variantProfile != null
                ? variantProfile.ResolvePaletteForTypography()
                : null;

            if (preset == null || !preset.UseVertexGradient)
                return false;

            start = VisualTierPaletteUtility.ResolveSlot(
                palette,
                preset.VertexGradientStartSlot,
                preset.CustomVertexGradientStart,
                start);
            end = VisualTierPaletteUtility.ResolveSlot(
                palette,
                preset.VertexGradientEndSlot,
                preset.CustomVertexGradientEnd,
                end);
            mode = preset.VertexGradientMode;
            return true;
        }

        public static Color ResolvePaletteColor(
            VisualTierPaletteSO palette,
            VisualTierColorSlot slot,
            Color fallback)
        {
            if (palette == null || slot == VisualTierColorSlot.None)
                return fallback;

            return VisualTierPaletteUtility.ResolveSlot(palette, slot, fallback, fallback);
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashBattleIntroCopy(this);

        public static BattleIntroCopyConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<BattleIntroCopyConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 1;
            wildSingleTemplate = "A wild {subject} has appeared!";
            wildDualTemplate = "A wild {subject0} and a wild {subject1} have appeared!";
            trainerTemplate = "{team} has challenged you!";
            pvpTemplate = "Opponent has challenged you!";
            traitSeparator = " ";
            ResetTraitOrder();
            mutatedStyle = CreateStyle(
                "mutated",
                BattleIntroTraitColorSource.MutatedPalette,
                new Color(0.82f, 0.22f, 0.58f, 1f));
            entangledStyle = CreateStyle(
                "entangled",
                BattleIntroTraitColorSource.EntangledTypeDisplay,
                new Color(0.97f, 0.48f, 0.69f, 1f));
            nameStyle = CreateStyle(
                string.Empty,
                BattleIntroTraitColorSource.VfxAccent,
                new Color(1f, 0.78f, 0.22f, 1f));
            variantStyle = CreateStyle(
                "variant",
                BattleIntroTraitColorSource.VariantProfile,
                new Color(0.6f, 0.27f, 1f, 1f),
                applyVertexGradient: true);
            applyVariantVertexGradient = true;
        }

        public void ResetTraitOrder()
        {
            if (traitOrder == null)
                traitOrder = new List<BattleIntroTraitSlot>(4);
            else
                traitOrder.Clear();

            traitOrder.Add(BattleIntroTraitSlot.Mutated);
            traitOrder.Add(BattleIntroTraitSlot.Entangled);
            traitOrder.Add(BattleIntroTraitSlot.Name);
            traitOrder.Add(BattleIntroTraitSlot.Variant);
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            if (string.IsNullOrEmpty(wildSingleTemplate))
                wildSingleTemplate = "A wild {subject} has appeared!";
            if (string.IsNullOrEmpty(wildDualTemplate))
                wildDualTemplate = "A wild {subject0} and a wild {subject1} have appeared!";
            if (string.IsNullOrEmpty(trainerTemplate))
                trainerTemplate = "{team} has challenged you!";
            if (string.IsNullOrEmpty(pvpTemplate))
                pvpTemplate = "Opponent has challenged you!";
            if (traitSeparator == null)
                traitSeparator = " ";
            if (traitOrder == null || traitOrder.Count == 0)
                ResetTraitOrder();

            mutatedStyle ??= CreateStyle(
                "mutated",
                BattleIntroTraitColorSource.MutatedPalette,
                new Color(0.82f, 0.22f, 0.58f, 1f));
            entangledStyle ??= CreateStyle(
                "entangled",
                BattleIntroTraitColorSource.EntangledTypeDisplay,
                new Color(0.97f, 0.48f, 0.69f, 1f));
            nameStyle ??= CreateStyle(
                string.Empty,
                BattleIntroTraitColorSource.VfxAccent,
                new Color(1f, 0.78f, 0.22f, 1f));
            variantStyle ??= CreateStyle(
                "variant",
                BattleIntroTraitColorSource.VariantProfile,
                new Color(0.6f, 0.27f, 1f, 1f),
                applyVertexGradient: true);
        }

        private static BattleIntroTraitStyle CreateStyle(
            string label,
            BattleIntroTraitColorSource source,
            Color fallback,
            bool applyVertexGradient = false)
        {
            return new BattleIntroTraitStyle
            {
                includeWhenActive = true,
                label = label,
                colorize = true,
                colorSource = source,
                paletteSlot = VisualTierColorSlot.Primary,
                fallbackColor = fallback,
                applyVertexGradient = applyVertexGradient,
            };
        }
    }
}
