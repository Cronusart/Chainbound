using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Pixel-art cycle numeral icons (I–V + Max) for Inventory Use, Party UI, and combat HUD.
    /// rebirthCount 0 → Cycle I, …, 4 → Cycle V, 5 → Max Cycle.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CycleIconPalette",
        menuName = "Chainbound/Combat/UI/Cycle Icon Palette",
        order = 46)]
    public sealed class CycleIconPaletteSO : ScriptableObject
    {
        [Header("Cycle Icons (rebirth 0..4 → I..V)")]
        [SerializeField] private Sprite cycleI;
        [SerializeField] private Sprite cycleII;
        [SerializeField] private Sprite cycleIII;
        [SerializeField] private Sprite cycleIV;
        [SerializeField] private Sprite cycleV;

        [Header("Max Cycle (rebirth >= REBIRTH_MAX)")]
        [SerializeField] private Sprite cycleMax;

        [Header("Fallback")]
        [SerializeField] private Sprite fallbackIcon;

        public Sprite GetIconForRebirthCount(int rebirthCount)
        {
            int tier = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount);
            if (tier >= CombatConstants.REBIRTH_MAX)
                return Coalesce(cycleMax, cycleV, fallbackIcon);

            switch (tier)
            {
                case 0: return Coalesce(cycleI, fallbackIcon);
                case 1: return Coalesce(cycleII, fallbackIcon);
                case 2: return Coalesce(cycleIII, fallbackIcon);
                case 3: return Coalesce(cycleIV, fallbackIcon);
                case 4: return Coalesce(cycleV, fallbackIcon);
                default: return fallbackIcon;
            }
        }

        public bool TryGetIconForRebirthCount(int rebirthCount, out Sprite icon)
        {
            icon = GetIconForRebirthCount(rebirthCount);
            return icon != null;
        }

        public void WarmPresentation()
        {
            TouchSprite(cycleI);
            TouchSprite(cycleII);
            TouchSprite(cycleIII);
            TouchSprite(cycleIV);
            TouchSprite(cycleV);
            TouchSprite(cycleMax);
            TouchSprite(fallbackIcon);
        }

        private static void TouchSprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture2D texture = sprite.texture;
            if (texture != null)
                _ = texture.width;
        }

        private static Sprite Coalesce(params Sprite[] candidates)
        {
            if (candidates == null)
                return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                    return candidates[i];
            }

            return null;
        }
    }
}
