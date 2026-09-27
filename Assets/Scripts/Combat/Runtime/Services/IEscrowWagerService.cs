using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages the PvP Stamped wager escrow lifecycle:
    ///   Match creation â†’ Deposit/lock â†’ In-progress â†’ Settlement (winner-choice).
    ///
    /// All escrow state changes will eventually map to on-chain Anchor program instructions.
    /// For now, this service operates entirely in-memory with TODO markers for chain integration.
    ///
    /// Key invariants:
    ///   - Escrowed Stamped moves CANNOT be unequipped, traded, or burned.
    ///   - Winner selects exactly ONE Stamped from the opponent's escrow pool.
    ///   - Remaining Stamped moves return to the loser.
    ///   - Timeout-based forfeits still allow the winner to select.
    ///
    /// TODO [ANCHOR-ESCROW]: Each state transition will map to an Anchor instruction:
    ///   CreateMatch â†’ initialize_escrow
    ///   Deposit     â†’ deposit_inscription
    ///   Settle      â†’ settle_escrow (with winner's selection index)
    ///   Forfeit     â†’ forfeit_escrow
    /// </summary>
    public interface IEscrowWagerService
    {
        /// <summary>
        /// Create a new PvP match and return its unique match ID.
        /// Both players' wagered Stamped moves are identified but not yet locked.
        /// </summary>
        /// <param name="playerStampedMoveIds">Instance IDs of the local player's wagered Stamped moves.</param>
        /// <param name="opponentStampedMoveIds">Instance IDs of the opponent's wagered Stamped moves (received via matchmaking).</param>
        Task<string> CreateMatchAsync(string[] playerStampedMoveIds, string[] opponentStampedMoveIds);

        /// <summary>
        /// Lock all wagered Stamped moves into escrow for the given match.
        /// Sets each Stamped's lockState to Escrowed.
        /// After this call, Stamped moves cannot be unequipped until the match settles.
        /// </summary>
        /// <param name="matchId">The match ID from <see cref="CreateMatchAsync"/>.</param>
        Task DepositAsync(string matchId);

        /// <summary>
        /// Declare a forfeit (timeout or explicit). The non-forfeiting player becomes the winner.
        /// Stamped moves remain escrowed until the winner selects.
        /// </summary>
        /// <param name="matchId">The match ID.</param>
        /// <param name="forfeitingPlayerIsLocal">True if the local player is forfeiting.</param>
        Task ForfeitAsync(string matchId, bool forfeitingPlayerIsLocal);

        /// <summary>
        /// Get the anonymized list of the opponent's escrowed Stamped moves
        /// for the winner-selection UI. No rarity, modifiers, or names are exposed.
        /// </summary>
        /// <param name="matchId">The match ID.</param>
        IReadOnlyList<AnonymizedMove> GetOpponentEscrowedMoves(string matchId);

        /// <summary>
        /// The winner selects one Stamped from the opponent's escrow pool by index.
        /// This is deterministic and player-driven â€” no RNG or VRF.
        ///
        /// On success:
        ///   - The selected Stamped transfers to the winner's inventory.
        ///   - All remaining Stamped moves unlock and return to the loser.
        ///   - The match transitions to Settled.
        /// </summary>
        /// <param name="matchId">The match ID.</param>
        /// <param name="index">Zero-based index into the anonymized Stamped moves array.</param>
        Task<MoveInstance> SettleAsync(string matchId, int index);

        /// <summary>
        /// Query the current state of a match by its ID.
        /// Returns null if no match exists for that ID.
        /// </summary>
        WagerMatchState? GetMatchState(string matchId);
    }
}
