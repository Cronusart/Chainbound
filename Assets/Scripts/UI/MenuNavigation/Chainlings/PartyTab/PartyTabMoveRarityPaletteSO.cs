using Chainbound.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [CreateAssetMenu(
        fileName = "PartyTabMoveRarityPalette",
        menuName = "Chainbound/UI/Party Tab Move Rarity Palette")]
    public sealed class PartyTabMoveRarityPaletteSO : ScriptableObject
    {
        [Header("Row Background")]
        [Tooltip("White + low-alpha body for rarity tinting (9-slice). Swap to rarityaccent_movesrow when ready.")]
        [SerializeField] private Sprite rowBackgroundSprite;
        [SerializeField] private Image.Type rowBackgroundImageType = Image.Type.Sliced;
        [SerializeField] private float pixelsPerUnitMultiplier = 1f;

        [Header("Rarity Tints")]
        [SerializeField] private Color commonColor = new Color(0.72f, 0.76f, 0.80f, 1f);
        [SerializeField] private Color uncommonColor = new Color(0.42f, 0.88f, 0.52f, 1f);
        [SerializeField] private Color rareColor = new Color(0.42f, 0.68f, 0.98f, 1f);
        [SerializeField] private Color epicColor = new Color(0.72f, 0.48f, 0.98f, 1f);
        [SerializeField] private Color legendaryColor = new Color(0.98f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color mythicColor = new Color(0.98f, 0.42f, 0.58f, 1f);
        [SerializeField] private Color emptyRowColor = new Color(0.64f, 0.72f, 0.82f, 0.35f);

        public Sprite RowBackgroundSprite => rowBackgroundSprite;
        public Image.Type RowBackgroundImageType => rowBackgroundImageType;
        public float PixelsPerUnitMultiplier => pixelsPerUnitMultiplier;

        public Color ResolveRarityColor(RarityTier rarity, bool emptyRow)
        {
            if (emptyRow)
                return emptyRowColor;

            switch (rarity)
            {
                case RarityTier.Uncommon:
                    return uncommonColor;
                case RarityTier.Rare:
                    return rareColor;
                case RarityTier.Epic:
                    return epicColor;
                case RarityTier.Legendary:
                    return legendaryColor;
                case RarityTier.Mythic:
                    return mythicColor;
                default:
                    return commonColor;
            }
        }
    }
}
