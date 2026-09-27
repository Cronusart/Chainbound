using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds battle-intro copy from <see cref="BattleIntroCopyConfigSO"/> templates and trait order.
    /// </summary>
    public static class WildEncounterIntroFormatter
    {
        public static BattleIntroCopyResult Format(
            BattleIntroCopyConfigSO config,
            IReadOnlyList<BattleIntroCopySubject> subjects,
            bool isPvP,
            bool isWild,
            string opponentTeamName,
            Color vfxAccent)
        {
            config = config != null ? config : BattleIntroCopyConfigSO.CreateRuntimeDefault();
            var result = new BattleIntroCopyResult();

            if (isPvP)
            {
                AppendLiteral(result, ResolveTemplate(config.pvpTemplate, "Opponent has challenged you!"));
                return result;
            }

            if (!isWild)
            {
                string teamName = string.IsNullOrWhiteSpace(opponentTeamName) ? "Opponent" : opponentTeamName.Trim();
                var teamSubject = new BattleIntroCopySubject { displayName = teamName };
                AppendTemplateWithSingleSubject(
                    result,
                    config,
                    ResolveTemplate(config.trainerTemplate, "{team} has challenged you!"),
                    BattleIntroCopyConfigSO.TeamToken,
                    teamSubject,
                    vfxAccent,
                    nameOnly: true);
                return result;
            }

            int count = subjects != null ? subjects.Count : 0;
            if (count >= 2)
            {
                AppendWildDual(result, config, subjects[0], subjects[1], vfxAccent);
                return result;
            }

            BattleIntroCopySubject subject = count == 1
                ? subjects[0]
                : new BattleIntroCopySubject { displayName = "Unknown" };
            AppendTemplateWithSingleSubject(
                result,
                config,
                ResolveTemplate(config.wildSingleTemplate, "A wild {subject} has appeared!"),
                BattleIntroCopyConfigSO.SubjectToken,
                subject,
                vfxAccent,
                nameOnly: false);
            return result;
        }

        public static string ToPlainText(string richText)
        {
            if (string.IsNullOrEmpty(richText))
                return string.Empty;

            var sb = new StringBuilder(richText.Length);
            bool inTag = false;
            for (int i = 0; i < richText.Length; i++)
            {
                char c = richText[i];
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (!inTag)
                    sb.Append(c);
            }

            return sb.ToString()
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&");
        }

        private static void AppendWildDual(
            BattleIntroCopyResult result,
            BattleIntroCopyConfigSO config,
            BattleIntroCopySubject first,
            BattleIntroCopySubject second,
            Color vfxAccent)
        {
            string template = ResolveTemplate(
                config.wildDualTemplate,
                "A wild {subject0} and a wild {subject1} have appeared!");

            int token0 = template.IndexOf(BattleIntroCopyConfigSO.Subject0Token, StringComparison.Ordinal);
            int token1 = template.IndexOf(BattleIntroCopyConfigSO.Subject1Token, StringComparison.Ordinal);
            if (token0 < 0 || token1 < 0 || token1 < token0)
            {
                AppendTemplateWithSingleSubject(
                    result,
                    config,
                    template,
                    BattleIntroCopyConfigSO.Subject0Token,
                    first,
                    vfxAccent,
                    nameOnly: false);
                return;
            }

            AppendLiteral(result, template.Substring(0, token0));
            AppendSubject(result, config, first, vfxAccent, nameOnly: false);
            int after0 = token0 + BattleIntroCopyConfigSO.Subject0Token.Length;
            AppendLiteral(result, template.Substring(after0, token1 - after0));
            AppendSubject(result, config, second, vfxAccent, nameOnly: false);
            AppendLiteral(result, template.Substring(token1 + BattleIntroCopyConfigSO.Subject1Token.Length));
        }

        private static void AppendTemplateWithSingleSubject(
            BattleIntroCopyResult result,
            BattleIntroCopyConfigSO config,
            string template,
            string token,
            BattleIntroCopySubject subject,
            Color vfxAccent,
            bool nameOnly)
        {
            int index = template.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                AppendLiteral(result, template);
                return;
            }

            AppendLiteral(result, template.Substring(0, index));
            AppendSubject(result, config, subject, vfxAccent, nameOnly);
            AppendLiteral(result, template.Substring(index + token.Length));
        }

        private static void AppendSubject(
            BattleIntroCopyResult result,
            BattleIntroCopyConfigSO config,
            BattleIntroCopySubject subject,
            Color vfxAccent,
            bool nameOnly)
        {
            List<BattleIntroTraitSlot> order = config.traitOrder;
            if (order == null || order.Count == 0)
            {
                order = new List<BattleIntroTraitSlot>
                {
                    BattleIntroTraitSlot.Mutated,
                    BattleIntroTraitSlot.Entangled,
                    BattleIntroTraitSlot.Name,
                    BattleIntroTraitSlot.Variant,
                };
            }

            bool wroteAny = false;
            string separator = config.traitSeparator ?? " ";
            for (int i = 0; i < order.Count; i++)
            {
                BattleIntroTraitSlot slot = order[i];
                if (nameOnly && slot != BattleIntroTraitSlot.Name)
                    continue;

                if (!TryResolveTraitText(config, slot, subject, out string text))
                    continue;

                if (wroteAny)
                    AppendLiteral(result, separator);

                Color color = config.ResolveTraitColor(slot, subject, vfxAccent);
                bool colorize = config.GetStyle(slot) != null &&
                    config.GetStyle(slot).colorize &&
                    color.a > 0.001f;
                AppendToken(
                    result,
                    text,
                    colorize ? color : (Color?)null,
                    ToSpanKind(slot),
                    subject.entangledType);
                wroteAny = true;
            }

            if (!wroteAny)
                AppendToken(result, "Unknown", null, BattleIntroCopySpanKind.Name, ChainlingType.None);
        }

        private static bool TryResolveTraitText(
            BattleIntroCopyConfigSO config,
            BattleIntroTraitSlot slot,
            BattleIntroCopySubject subject,
            out string text)
        {
            text = null;
            BattleIntroTraitStyle style = config.GetStyle(slot);
            if (style == null || !style.includeWhenActive)
                return false;

            switch (slot)
            {
                case BattleIntroTraitSlot.Mutated:
                    if (!subject.isMutated)
                        return false;
                    text = style.label;
                    break;
                case BattleIntroTraitSlot.Entangled:
                    if (!subject.isEntangled)
                        return false;
                    text = style.label;
                    break;
                case BattleIntroTraitSlot.Name:
                    text = string.IsNullOrWhiteSpace(subject.displayName) ? "Unknown" : subject.displayName.Trim();
                    break;
                case BattleIntroTraitSlot.Variant:
                    if (!subject.isVariant)
                        return false;
                    text = style.label;
                    break;
                default:
                    return false;
            }

            return !string.IsNullOrWhiteSpace(text);
        }

        private static void AppendToken(
            BattleIntroCopyResult result,
            string text,
            Color? color,
            BattleIntroCopySpanKind kind,
            ChainlingType entangledType)
        {
            string visible = text ?? string.Empty;
            var span = new BattleIntroCopySpan
            {
                visibleStart = result.visibleLength,
                visibleLength = visible.Length,
                kind = kind,
                entangledType = entangledType,
            };
            result.spans.Add(span);
            result.visibleLength += visible.Length;

            string escaped = EscapeRichText(visible);
            if (color.HasValue)
            {
                string hex = ColorUtility.ToHtmlStringRGB(color.Value);
                result.richText += $"<color=#{hex}>{escaped}</color>";
            }
            else
            {
                result.richText += escaped;
            }
        }

        private static void AppendLiteral(BattleIntroCopyResult result, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            result.richText += text;
            result.visibleLength += ToPlainText(text).Length;
        }

        private static string ResolveTemplate(string template, string fallback)
        {
            return string.IsNullOrWhiteSpace(template) ? fallback : template;
        }

        private static BattleIntroCopySpanKind ToSpanKind(BattleIntroTraitSlot slot)
        {
            return slot switch
            {
                BattleIntroTraitSlot.Mutated => BattleIntroCopySpanKind.Mutated,
                BattleIntroTraitSlot.Entangled => BattleIntroCopySpanKind.Entangled,
                BattleIntroTraitSlot.Variant => BattleIntroCopySpanKind.Variant,
                _ => BattleIntroCopySpanKind.Name,
            };
        }

        public static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
