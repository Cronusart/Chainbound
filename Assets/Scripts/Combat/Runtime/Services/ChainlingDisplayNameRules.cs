using System.Text;

namespace Chainbound.Combat
{
    /// <summary>Canonical player-entered Chainling display-name rules.</summary>
    public static class ChainlingDisplayNameRules
    {
        public const int MinimumLength = 2;
        public const int MaximumLength = 12;
        public const int PersistenceMaximumLength = 16;

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            char previous = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!IsAllowedCharacter(character))
                    continue;
                if (character == ' ' && (builder.Length == 0 || previous == ' '))
                    continue;
                builder.Append(character);
                previous = character;
            }

            return builder.ToString().Trim();
        }

        public static bool IsValid(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            if (trimmed.Length < MinimumLength || trimmed.Length > MaximumLength || trimmed.Contains("  "))
                return false;

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (!IsAllowedCharacter(trimmed[i]))
                    return false;
            }

            return true;
        }

        public static string ResolveOrFallback(string value, string fallback)
        {
            string sanitized = Sanitize(value);
            if (IsValid(sanitized))
                return sanitized;

            sanitized = Sanitize(fallback);
            if (sanitized.Length > PersistenceMaximumLength)
                sanitized = sanitized.Substring(0, PersistenceMaximumLength).TrimEnd();
            return IsPersistenceValid(sanitized) ? sanitized : "Chainling";
        }

        public static bool IsPersistenceValid(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            if (trimmed.Length < 1 || trimmed.Length > PersistenceMaximumLength || trimmed.Contains("  "))
                return false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (!IsAllowedCharacter(trimmed[i]))
                    return false;
            }
            return true;
        }

        private static bool IsAllowedCharacter(char character)
        {
            return char.IsLetter(character) || character == ' ' || character == '\'' || character == '-';
        }
    }
}
