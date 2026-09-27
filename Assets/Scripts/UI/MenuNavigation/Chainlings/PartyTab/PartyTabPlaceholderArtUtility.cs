using System.Collections.Generic;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Runtime placeholder sprites for party tab pixel-art slots (pedestal + shimmer).
    /// Swap assigned PartyTabFrameAnimator frames when final art lands.
    /// </summary>
    public static class PartyTabPlaceholderArtUtility
    {
        private static List<Sprite> pedestalFrames;
        private static List<Sprite> shimmerFrames;
        private static Sprite emptyStarSprite;
        private static List<Sprite> filledStarShimmerFrames;

        public static IReadOnlyList<Sprite> GetPedestalFrames()
        {
            EnsurePedestalFrames();
            return pedestalFrames;
        }

        public static IReadOnlyList<Sprite> GetShimmerFrames()
        {
            EnsureShimmerFrames();
            return shimmerFrames;
        }

        public static Sprite GetEmptyStarSprite()
        {
            EnsureStarArt();
            return emptyStarSprite;
        }

        public static IReadOnlyList<Sprite> GetFilledStarShimmerFrames()
        {
            EnsureStarArt();
            return filledStarShimmerFrames;
        }

        private static void EnsureStarArt()
        {
            if (emptyStarSprite != null && filledStarShimmerFrames != null && filledStarShimmerFrames.Count > 0)
                return;

            const int size = 32;
            emptyStarSprite = CreateStarSprite(size, filled: false, highlight: 0f);
            filledStarShimmerFrames = new List<Sprite>(4)
            {
                CreateStarSprite(size, filled: true, highlight: 0.15f),
                CreateStarSprite(size, filled: true, highlight: 0.45f),
                CreateStarSprite(size, filled: true, highlight: 0.75f),
                CreateStarSprite(size, filled: true, highlight: 0.45f),
            };
        }

        private static Sprite CreateStarSprite(int size, bool filled, float highlight)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            Color32 body = filled
                ? new Color32(240, 190, 64, 255)
                : new Color32(90, 98, 110, 255);
            Color32 edge = filled
                ? new Color32(40, 28, 8, 255)
                : new Color32(50, 56, 66, 255);
            Color32 glint = new Color32(255, 255, 220, (byte)Mathf.Clamp(Mathf.RoundToInt(255f * highlight), 0, 255));

            int cx = size / 2;
            int cy = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!IsInsideStar(x, y, cx, cy, size))
                        continue;

                    bool border = !IsInsideStar(x, y, cx, cy, size - 2);
                    pixels[y * size + x] = border ? edge : body;

                    if (filled && highlight > 0.01f && x >= cx - 1 && x <= cx + 1 && y >= cy + 4 && y <= cy + 8)
                        pixels[y * size + x] = glint;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private static bool IsInsideStar(int x, int y, int cx, int cy, int size)
        {
            float nx = (x - cx) / (size * 0.5f);
            float ny = (y - cy) / (size * 0.5f);
            float dist = nx * nx + ny * ny;
            if (dist > 1.05f)
                return false;

            float angle = Mathf.Atan2(ny, nx);
            float spike = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(angle * 2.5f));
            return dist <= spike * spike;
        }

        private static void EnsurePedestalFrames()
        {
            if (pedestalFrames != null && pedestalFrames.Count > 0)
                return;

            pedestalFrames = new List<Sprite>(3)
            {
                CreateSolidSprite(new Color(0.35f, 0.38f, 0.42f, 0.55f)),
                CreateSolidSprite(new Color(0.42f, 0.45f, 0.5f, 0.72f)),
                CreateSolidSprite(new Color(0.35f, 0.38f, 0.42f, 0.55f)),
            };
        }

        private static void EnsureShimmerFrames()
        {
            if (shimmerFrames != null && shimmerFrames.Count > 0)
                return;

            var accent = new Color(0.2f, 0.85f, 0.75f, 1f);
            shimmerFrames = new List<Sprite>(4)
            {
                CreateSolidSprite(new Color(accent.r, accent.g, accent.b, 0.15f)),
                CreateSolidSprite(new Color(accent.r, accent.g, accent.b, 0.45f)),
                CreateSolidSprite(new Color(accent.r, accent.g, accent.b, 0.75f)),
                CreateSolidSprite(new Color(accent.r, accent.g, accent.b, 0.45f)),
            };
        }

        private static Sprite CreateSolidSprite(Color color, int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            var c32 = (Color32)color;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = c32;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
