using SignalJam.VisualFx;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Paints configured vertex gradients onto battle-intro trait spans after TMP mesh generation.
    /// Does not restyle the whole intro line.
    /// </summary>
    public static class BattleIntroCopySpanApplier
    {
        public static void Apply(TMP_Text text, BattleIntroCopyConfigSO config, BattleIntroCopySpan[] spans)
        {
            if (text == null || !TryBuildSettings(config, spans, out var settings))
                return;

            text.ForceMeshUpdate(true);
            Paint(text.textInfo, config, spans, settings);

            try
            {
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
            catch (System.Exception)
            {
            }
        }

        public static void Paint(
            TMP_TextInfo textInfo,
            BattleIntroCopyConfigSO config,
            BattleIntroCopySpan[] spans)
        {
            if (!TryBuildSettings(config, spans, out var settings))
                return;

            Paint(textInfo, config, spans, settings);
        }

        private static void Paint(
            TMP_TextInfo textInfo,
            BattleIntroCopyConfigSO config,
            BattleIntroCopySpan[] spans,
            VisualTierTypographyBoundsGradient.Settings settings)
        {
            if (textInfo == null || textInfo.characterCount <= 0)
                return;

            for (int i = 0; i < spans.Length; i++)
            {
                BattleIntroCopySpan span = spans[i];
                if (span.visibleLength <= 0)
                    continue;

                BattleIntroTraitSlot slot = ToSlot(span.kind);
                if (!config.ShouldApplyVertexGradient(slot))
                    continue;

                VisualTierTypographyBoundsGradient.ApplyToCharacterRange(
                    textInfo,
                    span.visibleStart,
                    span.visibleLength,
                    settings);
            }
        }

        private static bool TryBuildSettings(
            BattleIntroCopyConfigSO config,
            BattleIntroCopySpan[] spans,
            out VisualTierTypographyBoundsGradient.Settings settings)
        {
            settings = default;
            if (config == null || spans == null || spans.Length == 0)
                return false;

            if (!config.applyVariantVertexGradient)
                return false;

            if (!config.TryResolveVariantGradient(
                    out Color start,
                    out Color end,
                    out VisualTierVertexGradientMode mode))
            {
                return false;
            }

            settings = new VisualTierTypographyBoundsGradient.Settings
            {
                Enabled = true,
                Mode = mode,
                Start = start,
                End = end,
                ScrollOffset01 = 0f,
            };
            return true;
        }

        private static BattleIntroTraitSlot ToSlot(BattleIntroCopySpanKind kind)
        {
            return kind switch
            {
                BattleIntroCopySpanKind.Mutated => BattleIntroTraitSlot.Mutated,
                BattleIntroCopySpanKind.Entangled => BattleIntroTraitSlot.Entangled,
                BattleIntroCopySpanKind.Variant => BattleIntroTraitSlot.Variant,
                _ => BattleIntroTraitSlot.Name,
            };
        }
    }
}
