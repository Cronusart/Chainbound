namespace Chainbound.Combat
{
    /// <summary>
    /// Presentation-safe AP mutation categories carried on legacy AP events.
    /// </summary>
    public static class CombatApMutationSource
    {
        public const string MoveCost = "MoveCost";
        public const string Drain = "Drain";
        public const string Gain = "Gain";
        public const string Refund = "Refund";
        public const string Steal = "Steal";
        public const string Transfer = "Transfer";
        public const string Blocked = "Blocked";
        public const string Capped = "Capped";
        public const string TurnRegen = "TurnRegen";
    }
}
