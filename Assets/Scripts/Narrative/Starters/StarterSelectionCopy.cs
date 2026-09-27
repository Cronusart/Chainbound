using Chainbound.Combat;

/// <summary>
/// Intro starter-card copy. Labels identity fields; lore stays unlabeled.
/// </summary>
public static class StarterSelectionCopy
{
    public const string SpeciesLabel = "Species";
    public const string TypeLabel = "Type";
    public const string RoleLabel = "Role";
    public const string MissingLore = "No species notes available yet.";

    public static string FormatLabeled(string label, string value)
    {
        string safeLabel = string.IsNullOrWhiteSpace(label) ? "" : label.Trim();
        string safeValue = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        if (string.IsNullOrEmpty(safeLabel))
            return safeValue;

        return $"{safeLabel}: {safeValue}";
    }

    public static string FormatSpecies(string displayName)
    {
        return FormatLabeled(SpeciesLabel, displayName);
    }

    public static string FormatType(string typeDisplayName)
    {
        return FormatLabeled(TypeLabel, typeDisplayName);
    }

    public static string FormatRole(SpeciesCombatRole role)
    {
        return FormatLabeled(RoleLabel, SpeciesCombatRoleResolver.FormatLabel(role));
    }

    public static string FormatLore(string description)
    {
        return string.IsNullOrWhiteSpace(description) ? MissingLore : description.Trim();
    }
}
