using System;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Pure formatters for the landscape profile ID card and share copy.
    /// </summary>
    public static class ProfileCardFormatter
    {
        public const string FallbackDisplayName = "PLAYER";
        public const string GuestChip = "GUEST";
        public const string SecuredChip = "SECURED";
        public const string DefaultShareTemplate = "just checked $NAME's chainbound id";

        public static string FormatDisplayName(string sessionName, string statsName = null)
        {
            string raw = FirstNonEmpty(sessionName, statsName, FallbackDisplayName);
            if (raw.StartsWith("$", StringComparison.Ordinal))
                return raw;

            return "$" + raw;
        }

        public static string FormatLinkChip(bool isGuestSession)
        {
            return isGuestSession ? GuestChip : SecuredChip;
        }

        public static string FormatUniqueSpecies(int uniqueSpeciesCaptured)
        {
            return $"{Math.Max(0, uniqueSpeciesCaptured)} species";
        }

        public static string FormatCaptures(int totalCaptures, int entangledCaptures)
        {
            return $"{Math.Max(0, totalCaptures)} captured   {Math.Max(0, entangledCaptures)} entangled";
        }

        public static string FormatStampedMoves(int movesCreated)
        {
            return $"{Math.Max(0, movesCreated)} stamped";
        }

        public static string FormatRecord(int battlesWon, int battlesLost)
        {
            if (battlesWon <= 0 && battlesLost <= 0)
                return "";

            return $"{Math.Max(0, battlesWon)}W-{Math.Max(0, battlesLost)}L";
        }

        public static string FormatShareCopy(string template, string displayName)
        {
            string name = FormatDisplayName(displayName);
            if (string.IsNullOrWhiteSpace(template))
                return DefaultShareTemplate.Replace("$NAME", name);

            return template.Replace("$NAME", name);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return FallbackDisplayName;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }

            return FallbackDisplayName;
        }
    }
}
