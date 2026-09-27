using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Resolves palette-driven colors for channel presets.
    /// </summary>
    public static class VisualTierPaletteUtility
    {
        public static Color ResolveSlot(
            VisualTierPaletteSO palette,
            VisualTierColorSlot slot,
            Color customColor,
            Color fallback = default)
        {
            if (slot == VisualTierColorSlot.None)
                return fallback;

            if (palette != null)
                return palette.ResolveColor(slot, customColor);

            return slot == VisualTierColorSlot.Custom ? customColor : fallback;
        }

        public static Color ResolveTintedSlot(
            VisualTierPaletteSO palette,
            VisualTierColorSlot slot,
            Color customColor,
            Color tint,
            Color fallback = default)
        {
            Color resolved = ResolveSlot(palette, slot, customColor, fallback);
            return resolved * tint;
        }
    }
}
