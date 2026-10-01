using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace Chainbound.Combat
{
    /// <summary>
    /// Stamp-strip sprites for the live card. Glyphs come from PixeloidSans SDF
    /// (the same family baked by tools/library-card-render) and are distressed
    /// with the ink.json knobs (edge 3, noise_max 45, speckle 0.1, ink RGB 55,78,74).
    /// The ResultPanel shows these Images. It does not leave undressed TMP on the card.
    /// Per-label tilt stays on the RectTransform via LibraryCardLayout, matching layout.json rotations.
    /// </summary>
    public static class LibraryCardStampStrip
    {
        static readonly Color32 Ink = new Color32(55, 78, 74, 255);
        const int NoiseMax = 45;
        const float SpeckleChance = 0.1f;
        const int SpecklePunchMin = 215;
        const int SpecklePunchMax = 255;
        const string FontResource = "TextMesh Pro/Fonts/PixeloidSans SDF";

        static TMP_FontAsset cachedFont;
        static Color32[] atlasPixels;
        static int atlasWidth;
        static int atlasHeight;
        static bool atlasUsesAlpha = true;

        public static Sprite Bake(
            string text,
            float fontSize,
            float maxWidth,
            int seed,
            TMP_FontAsset font = null,
            float lineHeight = 0f,
            Color32 ink = default,
            bool distress = true)
        {
            if (ink.a == 0)
                ink = Ink;
            text = (text ?? string.Empty).ToUpperInvariant().Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.Length == 0)
                return null;

            font = font != null ? font : LoadFont();
            if (font == null)
                return null;

            EnsureAtlas(font);
            if (atlasPixels == null || atlasWidth <= 0)
                return null;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();

            float size = Mathf.Max(18f, fontSize);
            float point = Mathf.Max(1f, font.faceInfo.pointSize);
            float scale = size / point;
            float longest = 0f;
            for (int i = 0; i < lines.Length; i++)
                longest = Mathf.Max(longest, Measure(lines[i], font, scale));
            if (longest > maxWidth)
            {
                float fit = maxWidth / Mathf.Max(1f, longest);
                scale *= Mathf.Clamp(fit, 18f / size, 1f);
                size = point * scale;
            }

            float step = lineHeight > 0.5f
                ? lineHeight * (size / Mathf.Max(1f, fontSize))
                : size * 1.15f;
            if (lines.Length <= 1)
                step = size * 1.45f;

            float maxLineW = 0f;
            for (int i = 0; i < lines.Length; i++)
                maxLineW = Mathf.Max(maxLineW, Measure(lines[i], font, scale));

            int width = Mathf.Clamp(Mathf.CeilToInt(maxLineW) + 8, 8, 1400);
            int height = Mathf.Clamp(Mathf.CeilToInt(step * lines.Length + size * 0.25f) + 8, 8, 512);
            var pixels = new Color32[width * height];

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                float baseline = size * 1.05f + lineIndex * step;
                float pen = 4f;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ' ')
                    {
                        pen += size * 0.45f;
                        continue;
                    }

                    if (!font.characterLookupTable.TryGetValue(c, out TMP_Character character) || character == null || character.glyph == null)
                        continue;

                    GlyphMetrics metrics = character.glyph.metrics;
                    GlyphRect rect = character.glyph.glyphRect;
                    int gw = Mathf.Max(1, Mathf.RoundToInt(metrics.width * scale));
                    int gh = Mathf.Max(1, Mathf.RoundToInt(metrics.height * scale));
                    int dx = Mathf.RoundToInt(pen + metrics.horizontalBearingX * scale);
                    // dy is the glyph top measured down from the top of the strip.
                    // SetPixels32 is bottom-up, so BlitGlyph flips that row.
                    int dy = Mathf.RoundToInt(baseline - metrics.horizontalBearingY * scale);
                    BlitGlyph(pixels, width, height, dx, dy, gw, gh, rect);
                    pen += metrics.horizontalAdvance * scale;
                }
            }

            if (distress)
                Distress(pixels, width, height, seed, ink);
            else
                Tint(pixels, ink);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "LibraryCardStamp"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0f, 1f), 100f);
        }

        public static void Release(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static TMP_FontAsset LoadFont()
        {
            if (cachedFont == null)
                cachedFont = Resources.Load<TMP_FontAsset>(FontResource);
            return cachedFont;
        }

        static float Measure(string text, TMP_FontAsset font, float scale)
        {
            float width = 0f;
            float point = Mathf.Max(1f, font.faceInfo.pointSize);
            float size = point * scale;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    width += size * 0.45f;
                    continue;
                }

                if (font.characterLookupTable.TryGetValue(c, out TMP_Character character) && character != null && character.glyph != null)
                    width += character.glyph.metrics.horizontalAdvance * scale;
                else
                    width += size * 0.5f;
            }

            return width;
        }

        static void EnsureAtlas(TMP_FontAsset font)
        {
            Texture atlas = font.atlasTexture;
            if (atlas == null)
                return;
            if (atlasPixels != null && atlasWidth == atlas.width && atlasHeight == atlas.height)
                return;

            RenderTexture rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(atlas, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, atlas.width, atlas.height), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            atlasPixels = copy.GetPixels32();
            atlasWidth = atlas.width;
            atlasHeight = atlas.height;
            atlasUsesAlpha = AtlasUsesAlpha(atlasPixels);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(copy);
            else
                UnityEngine.Object.DestroyImmediate(copy);
        }

        static bool AtlasUsesAlpha(Color32[] pixels)
        {
            int sample = Mathf.Min(pixels.Length, 4000);
            int alpha = 0;
            int color = 0;
            for (int i = 0; i < sample; i += 4)
            {
                if (pixels[i].a > 140)
                    alpha++;
                if (pixels[i].g > 140)
                    color++;
            }

            return alpha >= color;
        }

        static void BlitGlyph(Color32[] dst, int dw, int dh, int dx, int dy, int gw, int gh, GlyphRect rect)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return;
            for (int y = 0; y < gh; y++)
            {
                int py = (dh - 1) - (dy + y);
                if (py < 0 || py >= dh)
                    continue;
                float v = gh <= 1 ? 0f : y / (float)(gh - 1);
                int sy = rect.y + Mathf.Clamp(Mathf.RoundToInt((1f - v) * (rect.height - 1)), 0, rect.height - 1);
                for (int x = 0; x < gw; x++)
                {
                    int px = dx + x;
                    if (px < 0 || px >= dw)
                        continue;
                    float u = gw <= 1 ? 0f : x / (float)(gw - 1);
                    int sx = rect.x + Mathf.Clamp(Mathf.RoundToInt(u * (rect.width - 1)), 0, rect.width - 1);
                    int si = sy * atlasWidth + sx;
                    if (si < 0 || si >= atlasPixels.Length)
                        continue;
                    Color32 sample = atlasPixels[si];
                    byte sdf = atlasUsesAlpha ? sample.a : sample.g;
                    if (sdf < 128)
                        continue;
                    dst[py * dw + px] = new Color32(255, 255, 255, 255);
                }
            }
        }

        static void Tint(Color32[] pixels, Color32 ink)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 8)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                pixels[i] = new Color32(ink.r, ink.g, ink.b, pixels[i].a);
            }
        }

        static void Distress(Color32[] pixels, int width, int height, int seed, Color32 ink)
        {
            var dilated = new Color32[pixels.Length];
            Filter(pixels, dilated, width, height, true);
            Filter(dilated, pixels, width, height, false);

            var rng = new System.Random(seed);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 8)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                int n = rng.Next(0, NoiseMax + 1);
                if (rng.NextDouble() < SpeckleChance)
                    n = Math.Max(n, rng.Next(SpecklePunchMin, SpecklePunchMax + 1));
                int alpha = Math.Max(0, pixels[i].a - n);
                pixels[i] = alpha < 8
                    ? new Color32(0, 0, 0, 0)
                    : new Color32(ink.r, ink.g, ink.b, (byte)alpha);
            }
        }

        static void Filter(Color32[] src, Color32[] dst, int width, int height, bool dilate)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = dilate ? (byte)0 : (byte)255;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int sy = y + oy;
                        if (sy < 0 || sy >= height)
                            continue;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int sx = x + ox;
                            if (sx < 0 || sx >= width)
                                continue;
                            byte a = src[sy * width + sx].a;
                            if (dilate)
                            {
                                if (a > value)
                                    value = a;
                            }
                            else if (a < value)
                            {
                                value = a;
                            }
                        }
                    }

                    dst[y * width + x] = value > 0 ? new Color32(255, 255, 255, value) : new Color32(0, 0, 0, 0);
                }
            }
        }
    }
}
