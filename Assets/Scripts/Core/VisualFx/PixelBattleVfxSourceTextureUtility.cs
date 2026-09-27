using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Shared source-frame metadata for Pixel Battle renderers.
    /// Sprite owners use explicit canvas metadata when available, while atlas
    /// frames retain their authored texture transform.
    /// </summary>
    public static class PixelBattleVfxSourceTextureUtility
    {
        public static Vector2 ResolveCanvasSize(Sprite sprite, Vector2Int explicitCanvasSize)
        {
            if (explicitCanvasSize.x > 0 && explicitCanvasSize.y > 0)
                return new Vector2(explicitCanvasSize.x, explicitCanvasSize.y);

            return sprite != null ? sprite.rect.size : Vector2.zero;
        }

        public static Vector4 ResolveTextureSt(Sprite sprite, Vector2Int explicitCanvasSize)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(1f, 1f, 0f, 0f);

            Texture texture = sprite.texture;
            Rect sourceRect = sprite.textureRect;

            // A standalone source PNG with an explicit matching canvas is a
            // full-frame source. Keep its UV transform identity even if an
            // importer or packing step reports a narrower textureRect.
            if (explicitCanvasSize.x > 0 && explicitCanvasSize.y > 0 &&
                texture.width == explicitCanvasSize.x && texture.height == explicitCanvasSize.y)
            {
                sourceRect = new Rect(0f, 0f, texture.width, texture.height);
            }

            float textureWidth = Mathf.Max(1f, texture.width);
            float textureHeight = Mathf.Max(1f, texture.height);
            return new Vector4(
                sourceRect.width / textureWidth,
                sourceRect.height / textureHeight,
                sourceRect.x / textureWidth,
                sourceRect.y / textureHeight);
        }

        public static Vector2 ResolveTextureCanvasSize(Texture texture)
        {
            return texture != null
                ? new Vector2(texture.width, texture.height)
                : Vector2.zero;
        }
    }
}
