using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Serializable snapshot of an active PvP wager match.
    /// Persisted locally (e.g., to PlayerPrefs or a JSON file) so the
    /// app can resume the match after backgrounding, crash, or reconnect.
    ///
    /// Design constraints:
    ///   - Must be serializable to JSON via Unity's JsonUtility or System.Text.Json.
    ///   - Must contain enough info to resume any state transition.
    ///   - Tx signatures are stored for idempotent retry via <see cref="IChainTxOrchestrator"/>.
    /// </summary>
    [Serializable]
    public class MatchPersistenceData
    {
        /// <summary>Unique match identifier.</summary>
        public string matchId;

        /// <summary>Current state of the match state machine.</summary>
        public WagerMatchState state;

        /// <summary>Instance IDs of the local player's wagered Stamped moves.</summary>
        public List<string> playerStampedMoveIds;

        /// <summary>Instance IDs of the opponent's wagered Stamped moves.</summary>
        public List<string> opponentStampedMoveIds;

        /// <summary>
        /// Transaction signatures for each submitted chain operation, keyed by idempotency key.
        /// Used for resume: on reconnect, any pending signatures are re-confirmed
        /// via <see cref="IChainTxOrchestrator.ResumeConfirmationAsync"/>.
        /// </summary>
        public List<TxSignatureEntry> txSignatures;

        /// <summary>True if the local player forfeited (timeout or explicit).</summary>
        public bool forfeitedByLocal;

        /// <summary>True if the local player is the winner.</summary>
        public bool localPlayerIsWinner;

        /// <summary>Index selected by the winner (-1 if not yet selected).</summary>
        public int selectedIndex;

        /// <summary>UTC ticks when this match was created.</summary>
        public long createdAtTicks;

        /// <summary>UTC ticks when this snapshot was last saved.</summary>
        public long lastSavedTicks;

        /// <summary>Timeout in seconds for forfeit detection.</summary>
        public float forfeitTimeoutSeconds;

        public MatchPersistenceData()
        {
            playerStampedMoveIds = new List<string>();
            opponentStampedMoveIds = new List<string>();
            txSignatures = new List<TxSignatureEntry>();
            selectedIndex = -1;
            forfeitTimeoutSeconds = 120f; // Default: 2 minutes
        }
    }

    /// <summary>
    /// A single tx signature entry for match persistence.
    /// </summary>
    [Serializable]
    public struct TxSignatureEntry
    {
        /// <summary>The idempotency key used when submitting the transaction.</summary>
        public string idempotencyKey;

        /// <summary>The transaction signature (base58).</summary>
        public string txSignature;

        /// <summary>The status at last save.</summary>
        public TxStatus status;
    }
}
