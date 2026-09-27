using System.Collections.Generic;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public sealed class ChainlingPartyColumnSummary
    {
        public bool isEmpty;
        public string emptyMessage = "—";
        public bool supportsDrillDown;
        public float xpBarFill01;
        public string xpLabel = "";
        public bool hasXpSegment;
        public int xpSegmentEarned;
        public int xpSegmentSize;
        public int starCount;
        public int cycleTier;
        public string cycleLabel = "";
        public string formLabel = "";
        public string typeLabel = "";
        public string variantLabel = "";
        public bool isEntangled;
        public ChainlingType primaryType = ChainlingType.None;
        public ChainlingType secondaryType = ChainlingType.None;
        public string speciesDisplayName = "";
        public SpeciesCombatRole combatRole = SpeciesCombatRole.Unspecified;
        public string combatRoleLabel = "";
        public string descriptionHook = "";
        public ChainlingPartyMoveRowSummary signatureMove;
        public string encountersSummaryLine = "";
        public string attributeProgressionFooter = "";
        public List<ChainlingAttributeRadarAxis> attributeAxes = new List<ChainlingAttributeRadarAxis>();
        public ArchiveBindingState archiveState = ArchiveBindingState.Dossier;
        public int moveEquippedCount;
        public int moveMaxSlots;
        public ChainlingPartyColumnVitals vitals;
        public List<ChainlingPartyDisplayLine> lines = new List<ChainlingPartyDisplayLine>();
        public List<ChainlingPartyMoveRowSummary> moveRows = new List<ChainlingPartyMoveRowSummary>();
        public List<ChainlingPartyBadgeDisplay> topBadges = new List<ChainlingPartyBadgeDisplay>();
        /// <summary>Career PvE damage dealt — drives Stats balance bar (not parsed from line text).</summary>
        public long damageDealt;
        /// <summary>Career PvE damage taken — drives Stats balance bar.</summary>
        public long damageTaken;

        public static ChainlingPartyColumnSummary Empty(string message = "—")
        {
            return new ChainlingPartyColumnSummary
            {
                isEmpty = true,
                emptyMessage = string.IsNullOrWhiteSpace(message) ? "—" : message,
                supportsDrillDown = false,
            };
        }
    }
}
