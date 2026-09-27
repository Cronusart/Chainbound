using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Inventory-tab category colors. InventoryScreen serialized defaults and
    /// item-obtain title tints both resolve here so they stay aligned.
    /// </summary>
    public static class PlayerItemCategoryPalette
    {
        public static readonly Color Field = new Color(0.2f, 0.85f, 0.75f, 1f);
        public static readonly Color Capture = new Color(0.95f, 0.52f, 0.22f, 1f);
        public static readonly Color Favor = new Color(0.52f, 0.78f, 1f, 1f);
        public static readonly Color Bindery = new Color(0.8f, 0.62f, 1f, 1f);
        public static readonly Color Archive = new Color(1f, 0.78f, 0.28f, 1f);
        public static readonly Color Cosmetic = new Color(1f, 0.48f, 0.7f, 1f);
        public static readonly Color Quest = new Color(0.7f, 0.9f, 0.42f, 1f);

        public static Color Resolve(PlayerItemCategory category)
        {
            switch (category)
            {
                case PlayerItemCategory.Capture: return Capture;
                case PlayerItemCategory.Favor: return Favor;
                case PlayerItemCategory.Bindery: return Bindery;
                case PlayerItemCategory.Archive: return Archive;
                case PlayerItemCategory.Cosmetic: return Cosmetic;
                case PlayerItemCategory.Quest: return Quest;
                default: return Field;
            }
        }

        public static Color Resolve(string categoryKey)
        {
            PlayerItemCategoryKeys.TryParse(categoryKey, out PlayerItemCategory category);
            return Resolve(category);
        }
    }
}
