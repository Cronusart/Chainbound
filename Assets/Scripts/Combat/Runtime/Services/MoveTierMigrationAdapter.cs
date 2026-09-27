using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// One-way storage-boundary adapter for pre-Ownership-Spine rows.
    /// Historical zero-modifier values become Known; historical one/two-modifier values
    /// become Stamped. Callers receive only the two-state domain model.
    /// </summary>
    public static class MoveTierMigrationAdapter
    {
        public static MoveTier FromStoredValue(string storedTier, string modifier1Id, string modifier2Id)
        {
            string value = storedTier != null ? storedTier.Trim() : string.Empty;
            if (string.Equals(value, "ordinal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "known", StringComparison.OrdinalIgnoreCase)
                || value == "0")
            {
                return MoveTier.Known;
            }

            if (string.Equals(value, "inscription", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "rune", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "stamped", StringComparison.OrdinalIgnoreCase)
                || value == "1"
                || value == "2")
            {
                return MoveTier.Stamped;
            }

            return string.IsNullOrWhiteSpace(modifier1Id) && string.IsNullOrWhiteSpace(modifier2Id)
                ? MoveTier.Known
                : MoveTier.Stamped;
        }
    }
}
