using System;

namespace Chainbound.Combat
{
    public static class ChainlingTypeParseUtility
    {
        public static ChainlingType ParseOrNone(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ChainlingType.None;

            string trimmed = value.Trim();
            if (string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
                return ChainlingType.None;

            return Enum.TryParse(trimmed, true, out ChainlingType parsed)
                ? parsed
                : ChainlingType.None;
        }

        public static string ToStorageId(ChainlingType type)
        {
            return type == ChainlingType.None ? "none" : type.ToString();
        }
    }
}
