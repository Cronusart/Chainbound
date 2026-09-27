using SignalJam.VisualFx;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "SpriteFlipbookVfx_", menuName = "Chainbound/Combat/Presentation/VFX/Sprite Flipbook VFX")]
    public class SpriteFlipbookVfxDefinitionSO : CombatVfxDefinitionSO
    {
        public SpriteAnimationSequence sprites = new SpriteAnimationSequence();
        [Min(0.01f)] public float fps = 12f;
        public Vector2 spriteSize = Vector2.one;
        [Tooltip("Shared authored canvas in pixels. Zero uses each frame's source rect; set this for padded or texture-sheet frames.")]
        public Vector2Int spriteCanvasSize = Vector2Int.zero;
        public Material material;
        [Tooltip("Optional shared Pixel Battle rendering preset. The flipbook frames remain the source texture unless this preset explicitly overrides them.")]
        public PixelBattleVfxPresetSO pixelBattlePreset;

        public float GetDurationSeconds()
        {
            if (sprites == null || sprites.Length <= 1 || fps <= 0f)
                return 0.05f;

            return (sprites.Length - 1) / fps;
        }
    }
}
