using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Pixel layout mirrored from tools/library-card-render/layout.json against
    /// librarycard_front.png (1280 x 640, top-left origin). Do not invent a second grid.
    /// Unity UI local space is parent-center, Y up.
    /// </summary>
    public static class LibraryCardLayout
    {
        public const float CardWidth = 1280f;
        public const float CardHeight = 640f;

        public static readonly Vector2 NameOrigin = new Vector2(310f, 120f);
        public const float NameFontSize = 72f;
        public const float NameMaxWidth = 920f;
        public const float NameTilt = 1f;

        public static readonly Vector2 StatsOrigin = new Vector2(320f, 230f);
        public const float StatsFontSize = 40f;
        public const float StatsTilt = -0.88f;
        public const string StatsGap = "      ";

        // Front ROLE / EFFECT (non-attack). Matches layout.json effect_font_size /
        // effect_max_lines / effect_line_height. Line 1 stays at StatsOrigin;
        // line 2 is same x, y += EffectLineHeight (scaled with fitted size in the strip baker).
        public const float EffectStatsFontSize = 36f;
        public const int EffectMaxLines = 2;
        public const float EffectLineHeight = 42f;

        public static readonly Vector2 Mod1Origin = new Vector2(135f, 364f);
        public static readonly Vector2 Mod2Origin = new Vector2(135f, 482f);
        public const float ModFontSize = 38f;
        public const float ModMaxWidth = 980f;
        public const float Mod1Tilt = -0.55f;
        public const float Mod2Tilt = 1.6f;

        // Unused on front today (layout.json effect_origin / effect_font_size_card).
        public static readonly Vector2 EffectOrigin = new Vector2(190f, 468f);
        public const float EffectFontSize = 28f;

        // type_inner [88, 88, 232, 232]
        public static readonly Rect TypeInner = new Rect(88f, 88f, 144f, 144f);
        public const float TypeTilt = -2.4f;

                // Measured empty wells on librarycard_front.png. Also written to layout.json rarity_wells
        // as [x1,y1,x2,y2] (top-left origin). Keep in lockstep with tools/library-card-render/layout.json.
        public static readonly Rect MoveWell = new Rect(932f, 546f, 40f, 41f);
        public static readonly Rect Mod1Well = new Rect(1054f, 551f, 40f, 38f);
        public static readonly Rect Mod2Well = new Rect(1170f, 554f, 40f, 38f);
        public const float DotInset = 8f;


        public static Vector2 TopLeft(Vector2 pixelOrigin)
        {
            return new Vector2(pixelOrigin.x - CardWidth * 0.5f, CardHeight * 0.5f - pixelOrigin.y);
        }

        public static Vector2 CenterOf(Rect pixelRect)
        {
            return TopLeft(new Vector2(pixelRect.x + pixelRect.width * 0.5f, pixelRect.y + pixelRect.height * 0.5f));
        }

        public static void PlaceTopLeft(RectTransform rect, Vector2 pixelOrigin, float tiltDegrees)
        {
            if (rect == null)
                return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = TopLeft(pixelOrigin);
            rect.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
            rect.localScale = Vector3.one;
        }

        public static void PlaceCenter(RectTransform rect, Rect pixelRect, float tiltDegrees, float inset)
        {
            if (rect == null)
                return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = CenterOf(pixelRect);
            float size = Mathf.Max(4f, Mathf.Min(pixelRect.width, pixelRect.height) - inset * 2f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
            rect.localScale = Vector3.one;
        }

        public static bool IsEffectStatsInk(string statsInk)
        {
            return !string.IsNullOrEmpty(statsInk)
                && statsInk.StartsWith("ROLE / EFFECT", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Word-wrap into at most maxLines. Prefer a mid break; slight bias after ';'.
        /// Mirrors tools/library-card-render/render.py choose_wrap_lines.
        /// </summary>
        public static string[] ChooseWrapLines(string text, int maxLines = 2)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
                return new[] { string.Empty };

            maxLines = Mathf.Max(1, maxLines);

            // Keep "ROLE / EFFECT  " on line 1; wrap the effect body only.
            string prefix = string.Empty;
            string body = text;
            const string Marker = "ROLE / EFFECT";
            if (text.StartsWith(Marker, System.StringComparison.OrdinalIgnoreCase))
            {
                string after = text.Substring(Marker.Length);
                int gapLen = 0;
                while (gapLen < after.Length && after[gapLen] == ' ')
                    gapLen++;
                prefix = Marker + (gapLen > 0 ? after.Substring(0, gapLen) : "  ");
                body = after.TrimStart(' ');
            }

            string[] words = body.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (maxLines == 1 || words.Length <= 1)
                return new[] { text };

            var lines = new System.Collections.Generic.List<string> { body };
            while (lines.Count < maxLines)
            {
                int idx = 0;
                for (int i = 1; i < lines.Count; i++)
                {
                    if (lines[i].Length > lines[idx].Length)
                        idx = i;
                }

                string target = lines[idx];
                string[] parts = target.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    break;

                int bestI = 1;
                int bestScore = int.MaxValue;
                // When splitting the first body line, count the ROLE / EFFECT prefix on the left.
                int leftPad = idx == 0 ? prefix.Length : 0;
                for (int i = 1; i < parts.Length; i++)
                {
                    string left = string.Join(" ", parts, 0, i);
                    string right = string.Join(" ", parts, i, parts.Length - i);
                    int score = Mathf.Abs((leftPad + left.Length) - right.Length);
                    if (left.EndsWith(";", System.StringComparison.Ordinal))
                        score -= 8;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestI = i;
                    }
                }

                string leftLine = string.Join(" ", parts, 0, bestI);
                string rightLine = string.Join(" ", parts, bestI, parts.Length - bestI);
                lines.RemoveAt(idx);
                lines.Insert(idx, leftLine);
                lines.Insert(idx + 1, rightLine);
            }

            if (prefix.Length > 0)
                lines[0] = prefix + lines[0];
            return lines.ToArray();
        }

        public static string FormatStatsInk(MoveDefinitionSO move, string effectLine)
        {
            if (move != null &&
                (move.category == MoveCategory.DamagingMelee || move.category == MoveCategory.DamagingRanged))
            {
                string accuracy = move.accuracy <= 0 ? "AUTO" : move.accuracy.ToString();
                return $"POWER: {move.power}{StatsGap}ACCURACY: {accuracy}";
            }

            string effect = string.IsNullOrWhiteSpace(effectLine)
                ? MoveSelectDisplayUtility.FormatPowerOrEffectLabel(move)
                : effectLine.Trim();
            string full = $"ROLE / EFFECT  {effect}".ToUpperInvariant();
            string[] lines = ChooseWrapLines(full, EffectMaxLines);
            return string.Join("\n", lines);
        }

        public static string FormatModStamp(string displayName, string effect)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;
            string name = displayName.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(effect))
                return name;
            return $"{name}{StatsGap}{effect.Trim().ToUpperInvariant()}";
        }
    }
}
