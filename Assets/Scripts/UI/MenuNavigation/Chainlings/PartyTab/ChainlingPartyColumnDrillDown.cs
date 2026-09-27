using System.Collections.Generic;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public enum ChainlingPartyDrillDownKind
    {
        None,
        MoveLearnBreakdown,
        AttributeBreakdown,
        MoveModifierSheet,
        EncounterOrigin,
        CosmeticPreview,
        FullStatSheet,
    }

    public sealed class ChainlingPartyMoveLearnRow
    {
        public string moveId;
        public string moveName;
        public string typeLabel;
        public string rarityLabel;
        public string learnPercentLabel;
        public bool learned;
        public int ownedCopyCount;
        public bool isExclusive;
    }

    public sealed class ChainlingPartyAttributeBreakdownSection
    {
        public string label;
        public int total;
        public int baseValue;
        public int ivBonus;
        public int cycleAutoFlat;
        public int cyclePickFlat;
    }

    public sealed class ChainlingPartyMoveModifierRow
    {
        public int slotIndex;
        public string moveName;
        public string typeLabel;
        public string categoryLabel;
        public string modifier1Label;
        public string modifier2Label;
    }

    public sealed class ChainlingPartyCosmeticSlotRow
    {
        public string slotLabel;
        public string equippedLabel;
    }

    public sealed class ChainlingPartyEncounterRow
    {
        public string zoneName;
        public string detail;
    }

    public sealed class ChainlingPartyColumnDrillDown
    {
        public ChainlingPartyDrillDownKind kind = ChainlingPartyDrillDownKind.None;
        public List<ChainlingPartyMoveLearnRow> moveLearnRows = new List<ChainlingPartyMoveLearnRow>();
        public List<ChainlingPartyAttributeBreakdownSection> attributeSections = new List<ChainlingPartyAttributeBreakdownSection>();
        public List<ChainlingPartyMoveModifierRow> moveModifierRows = new List<ChainlingPartyMoveModifierRow>();
        public List<ChainlingPartyCosmeticSlotRow> cosmeticSlots = new List<ChainlingPartyCosmeticSlotRow>();
        public List<ChainlingPartyEncounterRow> encounterRows = new List<ChainlingPartyEncounterRow>();
        public List<ChainlingPartyDisplayLine> statLines = new List<ChainlingPartyDisplayLine>();
        public List<ChainlingPartyBadgeDisplay> badges = new List<ChainlingPartyBadgeDisplay>();
        public string headerText = "";
        public string bodyText = "";

        public static ChainlingPartyColumnDrillDown None()
        {
            return new ChainlingPartyColumnDrillDown { kind = ChainlingPartyDrillDownKind.None };
        }
    }
}
