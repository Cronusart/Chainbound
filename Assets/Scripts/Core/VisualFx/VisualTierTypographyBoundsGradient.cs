using TMPro;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Samples a gradient across full TMP text bounds (not per glyph).
    /// </summary>
    public static class VisualTierTypographyBoundsGradient
    {
        public struct Settings
        {
            public bool Enabled;
            public VisualTierVertexGradientMode Mode;
            public Color Start;
            public Color End;
            /// <summary>0-1 scroll along the primary gradient axis (parallax shimmer).</summary>
            public float ScrollOffset01;
        }

        public static Color32 Sample(
            Vector3 localPosition,
            float minX,
            float maxX,
            float minY,
            float maxY,
            VisualTierVertexGradientMode mode,
            Color start,
            Color end,
            float scrollOffset01 = 0f)
        {
            float width = Mathf.Max(0.0001f, maxX - minX);
            float height = Mathf.Max(0.0001f, maxY - minY);
            float nx = Mathf.Clamp01((localPosition.x - minX) / width);
            float ny = Mathf.Clamp01((localPosition.y - minY) / height);
            Color sampled = SampleNormalized(nx, ny, mode, start, end, scrollOffset01);
            return sampled;
        }

        public static Color SampleNormalized(
            float nx,
            float ny,
            VisualTierVertexGradientMode mode,
            Color start,
            Color end,
            float scrollOffset01 = 0f)
        {
            float scroll = Mathf.Repeat(scrollOffset01, 1f);

            switch (mode)
            {
                case VisualTierVertexGradientMode.Vertical:
                {
                    float t = Mathf.Repeat(ny + scroll, 1f);
                    return Color.Lerp(start, end, t);
                }

                case VisualTierVertexGradientMode.SolanaDiagonal:
                {
                    float sx = Mathf.Repeat(nx + scroll, 1f);
                    float sy = Mathf.Repeat(ny + scroll * 0.5f, 1f);
                    Color top = Color.Lerp(start, end, sx);
                    Color bottom = Color.Lerp(end, start, sx);
                    return Color.Lerp(top, bottom, sy);
                }

                default:
                {
                    float t = Mathf.Repeat(nx + scroll, 1f);
                    return Color.Lerp(start, end, t);
                }
            }
        }

        public static void ApplyToTextInfo(TMP_TextInfo textInfo, Settings settings)
        {
            ApplyToCharacterRange(textInfo, 0, textInfo != null ? textInfo.characterCount : 0, settings);
        }

        public static void ApplyToCharacterRange(
            TMP_TextInfo textInfo,
            int visibleStart,
            int visibleLength,
            Settings settings)
        {
            if (textInfo == null || !settings.Enabled || visibleLength <= 0)
                return;

            int start = Mathf.Max(0, visibleStart);
            int end = Mathf.Min(textInfo.characterCount, start + visibleLength);
            if (end <= start)
                return;

            if (!TryComputeRangeBounds(textInfo, start, end, out float minX, out float maxX, out float minY, out float maxY))
                return;

            for (int i = start; i < end; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                int materialIndex = character.materialReferenceIndex;
                int vertexIndex = character.vertexIndex;
                Color32[] colors = textInfo.meshInfo[materialIndex].colors32;

                colors[vertexIndex + 0] = Sample(
                    character.bottomLeft, minX, maxX, minY, maxY,
                    settings.Mode, settings.Start, settings.End, settings.ScrollOffset01);
                colors[vertexIndex + 1] = Sample(
                    character.topLeft, minX, maxX, minY, maxY,
                    settings.Mode, settings.Start, settings.End, settings.ScrollOffset01);
                colors[vertexIndex + 2] = Sample(
                    character.topRight, minX, maxX, minY, maxY,
                    settings.Mode, settings.Start, settings.End, settings.ScrollOffset01);
                colors[vertexIndex + 3] = Sample(
                    character.bottomRight, minX, maxX, minY, maxY,
                    settings.Mode, settings.Start, settings.End, settings.ScrollOffset01);

                character.vertex_BL.color = colors[vertexIndex + 0];
                character.vertex_TL.color = colors[vertexIndex + 1];
                character.vertex_TR.color = colors[vertexIndex + 2];
                character.vertex_BR.color = colors[vertexIndex + 3];
                textInfo.characterInfo[i] = character;
            }
        }

        public static bool TryComputeVisibleBounds(
            TMP_TextInfo textInfo,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            return TryComputeRangeBounds(
                textInfo,
                0,
                textInfo != null ? textInfo.characterCount : 0,
                out minX,
                out maxX,
                out minY,
                out maxY);
        }

        public static bool TryComputeRangeBounds(
            TMP_TextInfo textInfo,
            int start,
            int end,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;
            bool found = false;

            if (textInfo == null)
                return false;

            int clampedStart = Mathf.Max(0, start);
            int clampedEnd = Mathf.Min(textInfo.characterCount, end);
            for (int i = clampedStart; i < clampedEnd; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                IncludePoint(character.bottomLeft, ref minX, ref maxX, ref minY, ref maxY);
                IncludePoint(character.topLeft, ref minX, ref maxX, ref minY, ref maxY);
                IncludePoint(character.topRight, ref minX, ref maxX, ref minY, ref maxY);
                IncludePoint(character.bottomRight, ref minX, ref maxX, ref minY, ref maxY);
                found = true;
            }

            return found;
        }

        private static void IncludePoint(
            Vector3 point,
            ref float minX,
            ref float maxX,
            ref float minY,
            ref float maxY)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }
    }
}
