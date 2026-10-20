using System;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Pure formatters for the profile career sheet and share copy.
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

        public static readonly string[] CareerLabels =
        {
            "UNIQUE SPECIES",
            "CAPTURES",
            "ENTANGLED",
            "WINS",
            "LOSSES",
            "KOS",
            "KO'D",
            "RAM EARNED",
            "RAM SPENT",
            "REBIRTHS",
            "STAR UPS"
        };

        public static string FormatCount(int value)
        {
            return Math.Max(0, value).ToString("N0");
        }

        public static string FormatRam(long value)
        {
            return Math.Max(0L, value).ToString("N0");
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
