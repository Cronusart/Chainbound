using System.Collections.Generic;
using System.Text;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Debug helpers for inspecting party tab read-model output during harness/dev sessions.
    /// </summary>
    public static class ChainlingPartyReadModelDebug
    {
        public static string BuildColumnSummaryText(
            ChainlingPartyViewMode mode,
            ChainlingPartyColumnContext ctx)
        {
            ChainlingPartyColumnSummary summary = ChainlingPartyColumnViewModelBuilder.BuildSummary(mode, ctx);
            if (summary.isEmpty)
                return $"[{mode}] empty: {summary.emptyMessage}";

            var builder = new StringBuilder();
            builder.Append('[').Append(mode).Append("] ");
            for (int i = 0; i < summary.lines.Count; i++)
            {
                ChainlingPartyDisplayLine line = summary.lines[i];
                if (i > 0)
                    builder.Append(" | ");
                builder.Append(line.Label).Append('=').Append(line.Value);
            }

            if (mode == ChainlingPartyViewMode.General)
                builder.Append(" | xpFill=").Append(summary.xpBarFill01.ToString("0.00"));

            if (summary.vitals != null)
                builder.Append(" | hp=").Append(summary.vitals.label);

            if (mode == ChainlingPartyViewMode.Stats)
            {
                builder.Append(" | dealt=").Append(summary.damageDealt);
                builder.Append(" | taken=").Append(summary.damageTaken);
            }

            if (mode == ChainlingPartyViewMode.Detail)
            {
                builder.Append(" | species=").Append(summary.speciesDisplayName);
                builder.Append(" | role=").Append(summary.combatRoleLabel);
                if (summary.signatureMove != null)
                    builder.Append(" | signature=").Append(summary.signatureMove.moveName);
                builder.Append(" | encounters=").Append(summary.encountersSummaryLine);
            }

            return builder.ToString();
        }

        public static ChainlingPartyColumnContext CreateContext(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            int slotIndex,
            MoveDatabaseSO moveDatabase,
            ModifierCatalogSO modifierCatalog,
            ChainlingVisualIdentityDatabaseSO identityDatabase,
            CombatVisualDatabaseSO visualDatabase,
            ProgressionConfigSO progressionConfig,
            ChainlingBadgeCatalogSO badgeCatalog,
            IReadOnlyList<MenuMoveItem> moveInventory = null,
            ChainlingProgressionRewardTablesSO progressionRewardTables = null,
            ChainlingProgressionEventCatalogSO progressionEventCatalog = null,
            SpeciesDatabaseSO speciesDatabase = null,
            EncounterCatalogSourceSO encounterCatalog = null)
        {
            return new ChainlingPartyColumnContext
            {
                entry = entry,
                species = species,
                speciesDatabase = speciesDatabase,
                slotIndex = slotIndex,
                moveDatabase = moveDatabase,
                modifierCatalog = modifierCatalog,
                identityDatabase = identityDatabase,
                visualDatabase = visualDatabase,
                progressionConfig = progressionConfig,
                progressionRewardTables = progressionRewardTables,
                progressionEventCatalog = progressionEventCatalog,
                badgeCatalog = badgeCatalog,
                moveInventory = moveInventory,
                encounterCatalog = encounterCatalog,
            };
        }

        public static ChainlingPartyColumnSummary BuildSummaryFromHarness(
            ChainlingsMenuTestHarness harness,
            ChainlingPartyViewMode mode,
            int slotIndex,
            MoveDatabaseSO moveDatabase,
            ModifierCatalogSO modifierCatalog,
            ChainlingVisualIdentityDatabaseSO identityDatabase,
            CombatVisualDatabaseSO visualDatabase,
            ProgressionConfigSO progressionConfig,
            ChainlingBadgeCatalogSO badgeCatalog)
        {
            if (harness == null ||
                !harness.TryCreatePartyReadModelInputs(
                    slotIndex,
                    out PlayerRosterEntry entry,
                    out SpeciesDefinitionSO species,
                    out IReadOnlyList<MenuMoveItem> moveInventory))
            {
                return ChainlingPartyColumnSummary.Empty("Assign at a PC.");
            }

            ChainlingPartyColumnContext ctx = CreateContext(
                entry,
                species,
                slotIndex,
                moveDatabase,
                modifierCatalog,
                identityDatabase,
                visualDatabase,
                progressionConfig,
                badgeCatalog,
                moveInventory,
                speciesDatabase: harness.SpeciesDatabase);

            return ChainlingPartyColumnViewModelBuilder.BuildSummary(mode, ctx);
        }
    }
}
