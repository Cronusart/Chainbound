using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Payload for opening an Inventory Use/Inspect ceremony on the shared UsePopup shell.
    /// </summary>
    public sealed class InventoryItemCeremonyContext
    {
        public InventoryItemCeremonyId Ceremony;
        public string ItemDisplayName = "";
        public string EffectLabel = "";
        public PlayerItemDefinition Definition;
        public MenuInventoryItemStack Stack;
        public InventoryItemPresentationEntry Presentation;
        public System.Collections.Generic.IReadOnlyList<PlayerRosterEntry> PartyTargets;
        public SpeciesDatabaseSO SpeciesDatabase;

        public int CurrentFavorScore;
        public int AmpouleFavorPoints;
        public int PartyFavorPoints;
        public int AmpouleBattlesRemaining;
        public float LuckStrength01;
        public int ApplyFavorPoints;
        public int ApplyFavorBattles;
        public int ProjectedFavorScore;
        public float ProjectedLuckStrength01;

        public string InspectBodyText = "";
        public string SelfStatusText = "";
        public string SelfApplyText = "";
    }
}
