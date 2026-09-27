using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// UI-agnostic API for the winner's Stamped selection after a PvP wager match.
    ///
    /// Design:
    ///   - The winner sees the opponent's escrowed Stamped moves as anonymized entries
    ///     (no rarity, no modifiers, no names â€” just a stable internal ID and index).
    ///   - The UI may render these as face-down cards, silhouettes, or card backs.
    ///   - Selection is index-based and deterministic. No RNG. No VRF.
    ///   - VRF is reserved for Stamped acquisition + modifier rolls only (future phase).
    ///   - UI secrecy is cosmetic only â€” the data is hidden client-side, not cryptographically.
    ///
    /// Usage:
    ///   1. Call <see cref="GetOpponentEscrowedMoves"/> to get the anonymized array.
    ///   2. Present the array to the winner (UI decides visual representation).
    ///   3. Call <see cref="SelectMove"/> with the chosen index.
    ///   4. The service handles transfer + unlock via <see cref="IEscrowWagerService"/>.
    /// </summary>
    public class WinnerSelectionAPI
    {
        private readonly IEscrowWagerService _escrowService;

        public WinnerSelectionAPI(IEscrowWagerService escrowService)
        {
            _escrowService = escrowService ?? throw new ArgumentNullException(nameof(escrowService));
        }

        /// <summary>
        /// Get the opponent's escrowed Stamped moves as anonymized entries.
        /// Returns an array where each entry has only an internal ID and an index.
        /// No rarity, no modifiers, no names are exposed.
        /// </summary>
        /// <param name="matchId">The active match ID.</param>
        /// <returns>Read-only list of anonymized Stamped entries.</returns>
        public IReadOnlyList<AnonymizedMove> GetOpponentEscrowedMoves(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
                throw new ArgumentException("matchId cannot be null or empty.", nameof(matchId));

            return _escrowService.GetOpponentEscrowedMoves(matchId);
        }

        /// <summary>
        /// The winner selects one Stamped by index.
        /// Deterministic claim â€” no RNG, no VRF.
        ///
        /// On success:
        ///   - Selected Stamped is transferred to the winner's inventory.
        ///   - Remaining Stamped moves are unlocked and returned to the loser.
        ///   - Match state transitions to Settled.
        /// </summary>
        /// <param name="matchId">The active match ID.</param>
        /// <param name="index">Zero-based index into the anonymized array.</param>
        /// <returns>The claimed Stamped instance (now unlocked and owned by the winner).</returns>
        public Task<MoveInstance> SelectMove(string matchId, int index)
        {
            if (string.IsNullOrEmpty(matchId))
                throw new ArgumentException("matchId cannot be null or empty.", nameof(matchId));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Selection index must be >= 0.");

            return _escrowService.SettleAsync(matchId, index);
        }
    }
}
