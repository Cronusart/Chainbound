using System.Text.RegularExpressions;

/// <summary>
/// Runtime text-token resolver for dialogue lines.
///
/// Looks up <c>{tokenName}</c> placeholders (case-insensitive identifier) and
/// replaces them with session-scoped values. Unknown tokens are left intact so
/// missing bindings are visible at runtime instead of silently disappearing.
///
/// Call-tags currently supported:
///   {playerName} -> GameSessionContext.DisplayName (fallback: "Trainer").
///
/// Extend by adding cases to <see cref="Lookup"/>; the rest of the pipeline is
/// token-agnostic.
/// </summary>
public static class DialogueTextTokens
{
    private const string FallbackPlayerName = "Trainer";

    private static readonly Regex TokenPattern =
        new Regex(@"\{(?<key>[a-zA-Z_][a-zA-Z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// Replace supported <c>{token}</c> placeholders in <paramref name="raw"/>.
    /// Null/empty input is returned unchanged. Braces that don't match the
    /// identifier pattern (e.g. TMP-style rich text) are ignored.
    /// </summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        if (raw.IndexOf('{') < 0)
            return raw;

        return TokenPattern.Replace(raw, match =>
        {
            string resolved = Lookup(match.Groups["key"].Value);
            return resolved ?? match.Value;
        });
    }

    private static string Lookup(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        switch (key.ToLowerInvariant())
        {
            case "playername":
                return SafeName(GameSessionContext.Instance?.DisplayName);
            default:
                return null;
        }
    }

    private static string SafeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return FallbackPlayerName;

        return raw.Trim();
    }
}
