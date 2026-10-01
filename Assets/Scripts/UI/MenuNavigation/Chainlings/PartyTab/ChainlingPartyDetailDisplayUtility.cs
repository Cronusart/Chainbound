using System.Collections.Generic;
using System.Text;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Party Entry (Detail mode) read-model: types, combat role, description hook, signature move, encounters.
    /// </summary>
    public static class ChainlingPartyDetailDisplayUtility
    {
        public const string SpeciesNameLabel = "Species";
        public const string PreferredTypeLabel = "Preferred type";
        public const string PreferredRoleLabel = "Preferred role";

        private const int SummaryMaxChars = 140;
        private const string StarterEncountersLine = "Starter · Loom Lab";
        private const string UnloggedEncountersLine = "Origins unlogged";
        private const string EmptySignatureName = "No signature authored";

        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            summary.signatureMove = null;
            summary.speciesDisplayName = "";
            summary.combatRole = SpeciesCombatRole.Unspecified;
            summary.combatRoleLabel = "";
            summary.descriptionHook = "";
            summary.encountersSummaryLine = "";
            summary.secondaryType = ChainlingType.None;

            if (!ctx.HasFilledChainling || ctx.species == null)
                return;

            SpeciesDefinitionSO species = ctx.species;
            summary.speciesDisplayName = string.IsNullOrWhiteSpace(species.displayName)
                ? species.speciesId
                : species.displayName;
            summary.primaryType = species.primaryType;
            summary.secondaryType = species.secondaryType;
            summary.combatRole = SpeciesCombatRoleResolver.Resolve(species);
            summary.combatRoleLabel = SpeciesCombatRoleResolver.FormatLabel(summary.combatRole);
            summary.descriptionHook = TruncateDescription(species.description);
            summary.signatureMove = BuildSignatureMove(species);
            summary.encountersSummaryLine = BuildEncountersSummaryLine(species, null);
            summary.supportsDrillDown = true;

            // Sparse debug lines only — Entry layout binds typed fields.
            summary.lines.Add(new ChainlingPartyDisplayLine(
                SpeciesNameLabel,
                summary.speciesDisplayName));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                PreferredTypeLabel,
                FormatPreferredTypeValue(summary.primaryType, summary.secondaryType)));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                PreferredRoleLabel,
                summary.combatRoleLabel));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                "Signature",
                summary.signatureMove != null && !summary.signatureMove.isEmpty
                    ? summary.signatureMove.moveName
                    : EmptySignatureName));
            summary.lines.Add(new ChainlingPartyDisplayLine("Encounters", summary.encountersSummaryLine));
        }

        /// <summary>
        /// Catalog type only (species default) — never entangled mutation prefix.
        /// </summary>
        public static string FormatPreferredTypeValue(ChainlingType primary, ChainlingType secondary)
        {
            string primaryName = MoveSelectDisplayUtility.FormatTypeName(primary);
            if (secondary == ChainlingType.None || secondary == primary)
                return primaryName;

            return $"{primaryName} · {MoveSelectDisplayUtility.FormatTypeName(secondary)}";
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.EncounterOrigin;
            drillDown.encounterRows.Clear();
            drillDown.headerText = "";
            drillDown.bodyText = "";
            if (!ctx.HasFilledChainling || ctx.species == null)
                return;

            if (ctx.species.isStarter)
            {
                drillDown.headerText = "Starter Chainling";
                drillDown.bodyText = string.IsNullOrWhiteSpace(ctx.species.description)
                    ? "This Chainling was chosen as your starter partner."
                    : ctx.species.description;
                return;
            }

            drillDown.headerText = "Encounter Origins";
            drillDown.bodyText = string.IsNullOrWhiteSpace(ctx.species.description)
                ? ""
                : ctx.species.description;
            drillDown.encounterRows.Add(new ChainlingPartyEncounterRow
            {
                zoneName = "Unavailable",
                detail = "Encounter zone lookup is not available yet.",
            });
        }

        public static string BuildEncountersSummaryLine(
            SpeciesDefinitionSO species,
            IReadOnlyList<string> zoneDisplayNames)
        {
            if (species != null && species.isStarter)
                return StarterEncountersLine;

            if (zoneDisplayNames != null && zoneDisplayNames.Count > 0)
            {
                if (zoneDisplayNames.Count == 1)
                    return zoneDisplayNames[0] ?? UnloggedEncountersLine;
                if (zoneDisplayNames.Count == 2)
                    return $"{zoneDisplayNames[0]} · {zoneDisplayNames[1]}";
                return $"{zoneDisplayNames.Count} zones";
            }

            return UnloggedEncountersLine;
        }

        private static ChainlingPartyMoveRowSummary BuildSignatureMove(SpeciesDefinitionSO species)
        {
            MoveDefinitionSO flagship = ResolveSignatureMove(species);
            if (flagship == null)
            {
                return new ChainlingPartyMoveRowSummary
                {
                    isEmpty = true,
                    moveName = EmptySignatureName,
                    statLabel = "",
                    moveType = ChainlingType.None,
                    rarity = RarityTier.Common,
                };
            }

            string moveName = !string.IsNullOrWhiteSpace(flagship.displayName)
                ? flagship.displayName
                : (!string.IsNullOrWhiteSpace(flagship.moveId) ? flagship.moveId : "Signature");

            return new ChainlingPartyMoveRowSummary
            {
                isEmpty = false,
                moveName = moveName,
                statLabel = PartyTabMoveDisplayUtility.FormatStatLabel(flagship),
                moveType = flagship.baseType,
                rarity = flagship.rarity,
            };
        }

        private static MoveDefinitionSO ResolveSignatureMove(SpeciesDefinitionSO species)
        {
            if (species?.progressionMovePool != null)
            {
                for (int i = 0; i < species.progressionMovePool.Count; i++)
                {
                    SpeciesProgressionMoveEntry entry = species.progressionMovePool[i];
                    if (entry != null && entry.isSignature && entry.move != null)
                        return entry.move;
                }
            }

            return species != null ? species.flagshipMove : null;
        }

        private static string TruncateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "No lore entry authored yet.";

            string trimmed = description.Trim();
            if (trimmed.Length <= SummaryMaxChars)
                return trimmed;

            var builder = new StringBuilder(SummaryMaxChars + 1);
            int lastSpace = -1;
            for (int i = 0; i < trimmed.Length && builder.Length < SummaryMaxChars - 1; i++)
            {
                char c = trimmed[i];
                builder.Append(c);
                if (char.IsWhiteSpace(c))
                    lastSpace = builder.Length - 1;
            }

            if (lastSpace > 0)
                builder.Length = lastSpace;

            builder.Append('…');
            return builder.ToString();
        }
    }
}
