namespace Chainbound.Combat
{
    /// <summary>
    /// Player-facing labels, icons, and tooltips using the shared chemistry-family rules.
    /// </summary>
    public static class ChemistryStatusPresentation
    {
        public static ChemistryStatusFamily GetFamily(StatusId statusId)
        {
            return ChemistryStatusRules.GetFamily(statusId);
        }

        public static string GetFamilyLabel(StatusId statusId)
        {
            return GetFamily(statusId).ToString().ToUpperInvariant();
        }

        public static string GetIconKey(StatusId statusId)
        {
            ChemistryStatusFamily family = GetFamily(statusId);
            return family == ChemistryStatusFamily.None
                ? string.Empty
                : $"status_{family.ToString().ToLowerInvariant()}";
        }

        public static string FormatCombatLabel(StatusDefinitionSO definition, StatusId fallbackId)
        {
            string name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : fallbackId != StatusId.None ? fallbackId.ToString() : "Status";
            ChemistryStatusFamily family = GetFamily(definition != null ? definition.statusId : fallbackId);
            string label = family == ChemistryStatusFamily.None
                ? name.ToUpperInvariant()
                : $"{family.ToString().ToUpperInvariant()} · {name.ToUpperInvariant()}";
            string behavior = GetCompactBehavior(definition != null ? definition.statusId : fallbackId);
            return string.IsNullOrEmpty(behavior) ? label : $"{label} ({behavior})";
        }

        public static string GetTooltip(StatusDefinitionSO definition, StatusId fallbackId)
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.effectSummary))
                return definition.effectSummary.Trim();
            return fallbackId != StatusId.None ? fallbackId.ToString() : "Status";
        }

        private static string GetCompactBehavior(StatusId statusId)
        {
            switch (statusId)
            {
                case StatusId.Firewall:
                    return "PROJECTILE -30%";
                case StatusId.Slowed:
                    return "SPD -2 / 25% LOSS";
                case StatusId.CascadeFailure:
                    return "3 = 25% MAXHP";
                case StatusId.Splinter:
                    return "65% SPREAD / +15% SOLO";
                case StatusId.Isolated:
                    return "NO ALLY SUPPORT";
                default:
                    return string.Empty;
            }
        }
    }
}
