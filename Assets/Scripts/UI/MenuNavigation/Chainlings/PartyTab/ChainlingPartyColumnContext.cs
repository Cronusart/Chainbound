using System.Collections.Generic;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public sealed class ChainlingPartyColumnContext
    {
        public PlayerRosterEntry entry;
        public SpeciesDefinitionSO species;
        public SpeciesDatabaseSO speciesDatabase;
        public int slotIndex;
        public MoveDatabaseSO moveDatabase;
        public ModifierCatalogSO modifierCatalog;
        public ChainlingVisualIdentityDatabaseSO identityDatabase;
        public CombatVisualDatabaseSO visualDatabase;
        public ProgressionConfigSO progressionConfig;
        public ChainlingProgressionRewardTablesSO progressionRewardTables;
        public ChainlingProgressionEventCatalogSO progressionEventCatalog;
        public ChainlingBadgeCatalogSO badgeCatalog;
        public IReadOnlyList<MenuMoveItem> moveInventory;
        public EncounterCatalogSourceSO encounterCatalog;

        public bool HasFilledChainling =>
            entry != null && entry.snapshot != null && !string.IsNullOrWhiteSpace(entry.snapshot.speciesId);
    }
}
