using System.Text;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared display-name rules for players and Chainlings.
    /// Typed names are 2–12. Saved names are 1–16. Both use the same characters and shape.
    /// </summary>
    public static class ChainlingDisplayNameRules
    {
        public const int MinimumLength = 2;
        public const int MaximumLength = 12;
        public const int PersistenceMaximumLength = 16;

        public static string Sanitize(string value)
        {
            return FilterCharacters(value).Trim();
        }

        /// <summary>
        /// Drops disallowed characters and collapsed spaces without trimming a trailing space,
        /// so a player can type the space between two words.
        /// </summary>
        public static string FilterLiveInput(string value)
        {
            return FilterCharacters(value);
        }

        public static bool IsAllowedCharacter(char character)
        {
            return IsNameLetter(character) || IsNameDigit(character) || IsNameSpecial(character);
        }

        public static bool IsValid(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return trimmed.Length >= MinimumLength
                && trimmed.Length <= MaximumLength
                && HasValidShape(trimmed);
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
            return trimmed.Length >= 1
                && trimmed.Length <= PersistenceMaximumLength
                && HasValidShape(trimmed);
        }

        private static string FilterCharacters(string value)
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

            return builder.ToString();
        }

        private static bool HasValidShape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Contains("  "))
                return false;

            char first = value[0];
            char last = value[value.Length - 1];
            if (!IsNameLetter(first) && !IsNameDigit(first))
                return false;
            if (IsNameSpecial(last))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (!IsAllowedCharacter(value[i]))
                    return false;
            }

            return true;
        }

        private static bool IsNameLetter(char character)
        {
            return (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= '\u00C0' && character <= '\u00D6')
                || (character >= '\u00D8' && character <= '\u00F6')
                || (character >= '\u00F8' && character <= '\u00FF');
        }

        private static bool IsNameDigit(char character)
        {
            return character >= '0' && character <= '9';
        }

        private static bool IsNameSpecial(char character)
        {
            return character == ' '
                || character == '\''
                || character == '-'
                || character == '_'
                || character == '.';
        }
    }
}
