using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    [CreateAssetMenu(fileName = "RarityPalette", menuName = "Chainbound/UI/Menu/Rarity Palette")]
    public class RarityPaletteSO : ScriptableObject
    {
        [SerializeField] private Color common = new Color(0.78f, 0.82f, 0.9f, 1f);
        [SerializeField] private Color uncommon = new Color(0.25f, 0.85f, 0.42f, 1f);
        [SerializeField] private Color rare = new Color(0.25f, 0.55f, 1f, 1f);
        [SerializeField] private Color epic = new Color(0.72f, 0.35f, 1f, 1f);
        [SerializeField] private Color legendary = new Color(1f, 0.72f, 0.22f, 1f);
        [SerializeField] private Color mythic = new Color(1f, 0.32f, 0.42f, 1f);

        public Color GetColor(RarityTier rarity)
        {
            switch (rarity)
            {
                case RarityTier.Uncommon: return uncommon;
                case RarityTier.Rare: return rare;
                case RarityTier.Epic: return epic;
                case RarityTier.Legendary: return legendary;
                case RarityTier.Mythic: return mythic;
                default: return common;
            }
        }
    }
}
