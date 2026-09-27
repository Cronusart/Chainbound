namespace Chainbound.Combat
{
    /// <summary>
    /// State machine states for a PvP Stamped wager match.
    ///
    /// Flow:
    ///   Idle â†’ CreateMatch â†’ AwaitDeposits â†’ Locked â†’ InProgress
    ///        â†’ VictoryDeclared â†’ WinnerSelecting â†’ Settled
    ///
    /// Forfeits (timeout or explicit disconnect) skip directly to VictoryDeclared,
    /// allowing the non-forfeiting player to still select from the opponent's escrow.
    ///
    /// The state is persisted locally (see <see cref="MatchPersistenceData"/>)
    /// so that the app can resume mid-match after backgrounding or reconnect.
    /// </summary>
    public enum WagerMatchState
    {
        /// <summary>No active match. Ready to create one.</summary>
        Idle = 0,

        /// <summary>Match has been created; awaiting both players' deposits.</summary>
        CreateMatch = 1,

        /// <summary>One or more deposits are pending confirmation.</summary>
        AwaitDeposits = 2,

        /// <summary>All deposits confirmed. Stamped moves are locked in escrow.</summary>
        Locked = 3,

        /// <summary>Combat is in progress.</summary>
        InProgress = 4,

        /// <summary>A winner has been declared (or forfeit resolved). Awaiting winner's selection.</summary>
        VictoryDeclared = 5,

        /// <summary>Winner is currently choosing an Stamped from the opponent's escrow.</summary>
        WinnerSelecting = 6,

        /// <summary>Settlement complete. Selected Stamped transferred, remainder unlocked.</summary>
        Settled = 7,
    }
}
