using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public sealed class ChainlingPartyMoveRowSummary
    {
        public bool isEmpty;
        public string moveName = "—";
        public string statLabel = "—";
        public ChainlingType moveType = ChainlingType.None;
        public RarityTier rarity = RarityTier.Common;

        public static ChainlingPartyMoveRowSummary EmptySlot()
        {
            return new ChainlingPartyMoveRowSummary { isEmpty = true };
        }
    }
}
