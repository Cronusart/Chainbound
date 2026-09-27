namespace Chainbound.Combat
{
    /// <summary>
    /// Canonical move loadout capacity rules.
    /// See Docs/Specs/Combat/MOVE_LOADOUT_RULES_AND_UI_SPEC.md.
    /// </summary>
    public static class MoveLoadoutSlotRules
    {
        public const int BaseMoveSlots = 4;
        public const int SlotsPerRebirth = 1;
        public const int MaxMoveSlots = 9;
        public const int MaxPassiveMoves = 2;
        public const int VisibleRowsBeforeScroll = 4;
        public const int OnChainStorageSlots = 16;

        public static int ResolveMaxMoveSlots(int rebirthCount)
        {
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount);
            int slots = BaseMoveSlots + (SlotsPerRebirth * rebirth);
            return Clamp(slots, BaseMoveSlots, MaxMoveSlots);
        }

        public static bool ShouldScrollMoveList(int equippedMoveCount)
        {
            return equippedMoveCount > VisibleRowsBeforeScroll;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
