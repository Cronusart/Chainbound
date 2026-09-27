using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Obtain-panel title copy: prompt sentence with the item name tinted in
    /// its inventory-tab category color. Stacks of 2+ use qty then the catalog name.
    /// </summary>
    public static class ItemObtainFoundCopy
    {
        public static string FormatTitle(string displayName, PlayerItemCategory category, int quantity = 1)
        {
            string hex = ColorUtility.ToHtmlStringRGB(PlayerItemCategoryPalette.Resolve(category));
            int qty = Mathf.Max(1, quantity);
            if (qty > 1)
            {
                string name = string.IsNullOrWhiteSpace(displayName) ? "item" : displayName.Trim();
                return $"You've found x{qty} <color=#{hex}>{EscapeRichText(name)}</color>!";
            }

            string spoken = NormalizeSpokenName(displayName);
            string article = IndefiniteArticle(spoken);
            return $"You've found {article} <color=#{hex}>{EscapeRichText(spoken)}</color>!";
        }

        public static string NormalizeSpokenName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "item";
            return displayName.Trim().ToLowerInvariant();
        }

        public static string IndefiniteArticle(string spokenName)
        {
            if (string.IsNullOrEmpty(spokenName))
                return "a";

            char c = char.ToLowerInvariant(spokenName[0]);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ? "an" : "a";
        }

        public static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
