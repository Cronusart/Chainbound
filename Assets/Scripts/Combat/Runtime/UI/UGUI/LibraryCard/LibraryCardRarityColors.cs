using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Library-card dots match PartyTabMoveRarityPalette / ChainlingPartyMoveRowDisplay bright hues, not the former Folio muted hues.
    /// Bake dots use the same RGB converted 0-1 to 0-255 in tools/library-card-render/render.py.
    /// </summary>
    public static class LibraryCardRarityColors
    {
        public static Color For(RarityTier tier)
        {
            switch (tier)
            {
                case RarityTier.Uncommon:
                    return new Color(0.42f, 0.88f, 0.52f, 1f);
                case RarityTier.Rare:
                    return new Color(0.42f, 0.68f, 0.98f, 1f);
                case RarityTier.Epic:
                    return new Color(0.72f, 0.48f, 0.98f, 1f);
                case RarityTier.Legendary:
                    return new Color(0.98f, 0.72f, 0.28f, 1f);
                case RarityTier.Mythic:
                    return new Color(0.98f, 0.42f, 0.58f, 1f);
                default:
                    return new Color(0.72f, 0.76f, 0.80f, 1f);
            }
        }
    }
}

