using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  PVP WAGER FLOW â€” README
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //
    //  State Machine:
    //    Idle â†’ CreateMatch â†’ AwaitDeposits â†’ Locked â†’ InProgress
    //         â†’ VictoryDeclared â†’ WinnerSelecting â†’ Settled
    //
    //  1. MATCH CREATION
    //     Both players agree to a PvP wager. The orchestrator creates a
    //     match via IEscrowWagerService.CreateMatchAsync, passing both
    //     players' Stamped instance IDs.
    //
    //  2. DEPOSIT & LOCK
    //     Each player's wagered Stamped moves are locked into escrow.
    //     lockState transitions to MoveLockState.Escrowed.
    //     Escrowed Stamped moves CANNOT be unequipped, traded, or burned.
    //
    //  3. COMBAT
    //     The match plays out using the existing CombatResolver.
    //     All Stamped moves remain Escrowed throughout.
    //
    //  4. VICTORY & WINNER SELECTION
    //     When a winner is declared (combat outcome or forfeit):
    //       a. The winner receives an anonymized list of opponent Stamped moves
    //          (no rarity, modifiers, or names â€” just stable IDs + indices).
    //       b. The UI may present these as face-down cards / silhouettes.
    //       c. The winner selects ONE by index (deterministic, no RNG/VRF).
    //       d. The selected Stamped transfers to the winner's inventory.
    //       e. All remaining Stamped moves unlock and return to the loser.
    //
    //  5. DISCONNECT / FORFEIT
    //     If a player disconnects, a configurable timeout starts.
    //     If the timeout expires before reconnection â†’ automatic forfeit.
    //     The winner still gets to select from the escrow pool.
    //     State + tx signatures persist locally for resume safety.
    //
    //  6. RESUME SAFETY
    //     MatchPersistenceData is saved after every state transition.
    //     On app resume, call ResumeAsync() to pick up where we left off:
    //       - Re-confirm any pending chain transactions
    //       - Restore the state machine to the correct phase
    //
    //  NOTE: VRF is NOT used for PvP winner selection. Winner choice is
    //  deterministic and player-driven. VRF is reserved for Stamped
    //  acquisition (rarity + modifier rolls) in a future phase.
    //
    //  NOTE: On-chain escrow (Anchor program) is a future integration.
    //  All IEscrowWagerService calls are currently handled by
    //  StubEscrowWagerService in-memory. Each call has a TODO [ANCHOR-ESCROW]
    //  marker indicating where the real on-chain instruction will go.
    //
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Drives the PvP wager match state machine.
    /// Pure C# â€” no MonoBehaviour. Designed for constructor injection.
    ///
    /// Responsibilities:
    ///   - Orchestrates the full lifecycle: create â†’ deposit â†’ combat â†’ settle.
    ///   - Manages timeout-based forfeits.
    ///   - Persists state locally for resume after app background / crash.
    ///   - Delegates all chain interaction to <see cref="IEscrowWagerService"/>
    ///     and <see cref="IChainTxOrchestrator"/>.
    /// </summary>
    public class WagerMatchOrchestrator
    {
        // â”€â”€ Dependencies â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private readonly IEscrowWagerService _escrow;
        private readonly IChainTxOrchestrator _txOrchestrator;
        private readonly IMoveInventoryService _inventory;

        // â”€â”€ Current Match State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private MatchPersistenceData _current;

        /// <summary>Current match state, or null if no match is active.</summary>
        public MatchPersistenceData CurrentMatch => _current;

        /// <summary>Current state of the active match (Idle if none).</summary>
        public WagerMatchState State => _current?.state ?? WagerMatchState.Idle;

        // â”€â”€ Events (for UI binding) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Fired whenever the match state changes.</summary>
        public event Action<WagerMatchState> OnStateChanged;

        /// <summary>Fired when an error occurs during a state transition.</summary>
        public event Action<string> OnError;

        // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public WagerMatchOrchestrator(
            IEscrowWagerService escrow,
            IChainTxOrchestrator txOrchestrator,
            IMoveInventoryService inventory)
        {
            _escrow = escrow ?? throw new ArgumentNullException(nameof(escrow));
            _txOrchestrator = txOrchestrator ?? throw new ArgumentNullException(nameof(txOrchestrator));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        // â”€â”€ Lifecycle: Create Match â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Create a new PvP wager match. Both players' wagered move IDs are provided.
        /// Validates ranked loadout before proceeding. Friendly matches skip validation.
        /// Transitions: Idle â†’ CreateMatch â†’ AwaitDeposits.
        /// </summary>
        /// <param name="playerChainlings">The local player's roster, used for ranked loadout validation. Pass null to skip validation (friendly match).</param>
        public async Task CreateMatchAsync(
            string[] playerStampedMoveIds,
            string[] opponentStampedMoveIds,
            List<ChainlingInstance> playerChainlings = null)
        {
            if (_current != null && _current.state != WagerMatchState.Idle && _current.state != WagerMatchState.Settled)
            {
                EmitError("Cannot create a new match while one is active.");
                return;
            }

            // Ranked loadout validation (skip for friendly matches where playerChainlings is null)
            if (playerChainlings != null)
            {
                var rankedResult = RankedLoadoutValidator.ValidateTeamForRanked(playerChainlings);
                if (!rankedResult.isValid)
                {
                    EmitError($"Ranked validation failed: {rankedResult.message}");
                    return;
                }
            }

            try
            {
                string matchId = await _escrow.CreateMatchAsync(playerStampedMoveIds, opponentStampedMoveIds);

                _current = new MatchPersistenceData
                {
                    matchId = matchId,
                    state = WagerMatchState.AwaitDeposits,
                    playerStampedMoveIds = new List<string>(playerStampedMoveIds),
                    opponentStampedMoveIds = new List<string>(opponentStampedMoveIds),
                    createdAtTicks = DateTime.UtcNow.Ticks,
                };

                Save();
                EmitStateChanged(WagerMatchState.AwaitDeposits);
            }
            catch (Exception ex)
            {
                EmitError($"CreateMatch failed: {ex.Message}");
            }
        }

        // â”€â”€ Lifecycle: Deposit & Lock â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Deposit and lock all wagered Stamped moves into escrow.
        /// Transitions: AwaitDeposits â†’ Locked.
        /// </summary>
        public async Task DepositAsync()
        {
            if (_current == null || _current.state != WagerMatchState.AwaitDeposits)
            {
                EmitError("Cannot deposit: no match in AwaitDeposits state.");
                return;
            }

            try
            {
                await _escrow.DepositAsync(_current.matchId);
                _current.state = WagerMatchState.Locked;
                Save();
                EmitStateChanged(WagerMatchState.Locked);
            }
            catch (Exception ex)
            {
                EmitError($"Deposit failed: {ex.Message}");
            }
        }

        // â”€â”€ Lifecycle: Start Combat â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Signal that combat has begun. Called by the match setup layer.
        /// Transitions: Locked â†’ InProgress.
        /// </summary>
        public void StartCombat()
        {
            if (_current == null || _current.state != WagerMatchState.Locked)
            {
                EmitError("Cannot start combat: match is not in Locked state.");
                return;
            }

            _current.state = WagerMatchState.InProgress;
            Save();
            EmitStateChanged(WagerMatchState.InProgress);
        }

        // â”€â”€ Lifecycle: Declare Victory â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Declare a winner after combat resolves or forfeit.
        /// Transitions: InProgress â†’ VictoryDeclared.
        /// </summary>
        /// <param name="localPlayerWon">True if the local player is the winner.</param>
        public void DeclareVictory(bool localPlayerWon)
        {
            if (_current == null ||
                (_current.state != WagerMatchState.InProgress && _current.state != WagerMatchState.Locked))
            {
                EmitError("Cannot declare victory: match is not in progress.");
                return;
            }

            _current.localPlayerIsWinner = localPlayerWon;
            _current.state = WagerMatchState.VictoryDeclared;
            Save();
            EmitStateChanged(WagerMatchState.VictoryDeclared);
        }

        // â”€â”€ Lifecycle: Forfeit â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Handle a forfeit (timeout or explicit disconnect).
        /// The non-forfeiting player becomes the winner and can still select.
        /// Transitions: any active state â†’ VictoryDeclared.
        /// </summary>
        /// <param name="localPlayerForfeited">True if the local player timed out / disconnected.</param>
        public async Task ForfeitAsync(bool localPlayerForfeited)
        {
            if (_current == null || _current.state == WagerMatchState.Idle || _current.state == WagerMatchState.Settled)
            {
                EmitError("No active match to forfeit.");
                return;
            }

            try
            {
                await _escrow.ForfeitAsync(_current.matchId, localPlayerForfeited);
                _current.forfeitedByLocal = localPlayerForfeited;
                _current.localPlayerIsWinner = !localPlayerForfeited;
                _current.state = WagerMatchState.VictoryDeclared;
                Save();
                EmitStateChanged(WagerMatchState.VictoryDeclared);
            }
            catch (Exception ex)
            {
                EmitError($"Forfeit failed: {ex.Message}");
            }
        }

        // â”€â”€ Lifecycle: Winner Selection & Settlement â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// The winner selects one Stamped from the opponent's escrow pool.
        /// Transitions: VictoryDeclared â†’ WinnerSelecting â†’ Settled.
        /// </summary>
        /// <param name="index">Zero-based index into the anonymized Stamped moves array.</param>
        /// <returns>The claimed Stamped instance, or null on failure.</returns>
        public async Task<MoveInstance> SelectAndSettleAsync(int index)
        {
            if (_current == null ||
                (_current.state != WagerMatchState.VictoryDeclared && _current.state != WagerMatchState.WinnerSelecting))
            {
                EmitError("Cannot select: match is not in VictoryDeclared or WinnerSelecting state.");
                return null;
            }

            _current.state = WagerMatchState.WinnerSelecting;
            Save();
            EmitStateChanged(WagerMatchState.WinnerSelecting);

            try
            {
                var claimed = await _escrow.SettleAsync(_current.matchId, index);

                _current.selectedIndex = index;
                _current.state = WagerMatchState.Settled;
                Save();
                EmitStateChanged(WagerMatchState.Settled);

                return claimed;
            }
            catch (Exception ex)
            {
                EmitError($"Settlement failed: {ex.Message}");
                // Revert to VictoryDeclared so the winner can try again
                _current.state = WagerMatchState.VictoryDeclared;
                Save();
                return null;
            }
        }

        // â”€â”€ Resume After Background / Reconnect â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Resume a match from persisted data. Call on app wake / reconnect.
        /// Re-confirms any pending chain transactions and restores the state machine.
        /// </summary>
        /// <param name="data">Previously saved persistence data.</param>
        public async Task ResumeAsync(MatchPersistenceData data)
        {
            if (data == null)
            {
                EmitError("Cannot resume: persistence data is null.");
                return;
            }

            _current = data;
            Debug.Log($"[WagerMatchOrchestrator] Resuming match {data.matchId} in state {data.state}.");

            // Re-confirm any pending tx signatures
            if (data.txSignatures != null)
            {
                for (int i = 0; i < data.txSignatures.Count; i++)
                {
                    var entry = data.txSignatures[i];
                    if (entry.status == TxStatus.Submitted)
                    {
                        Debug.Log($"[WagerMatchOrchestrator] Re-confirming pending tx: {entry.txSignature}");
                        var result = await _txOrchestrator.ResumeConfirmationAsync(entry.txSignature);

                        // Update the entry with the confirmed status
                        entry.status = result.status;
                        data.txSignatures[i] = entry;
                    }
                }
            }

            Save();
            EmitStateChanged(data.state);
        }

        // â”€â”€ Timeout Check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Check if the forfeit timeout has expired for the current match.
        /// Should be called periodically (e.g., from an Update loop).
        /// Returns true if the timeout has expired and a forfeit should be triggered.
        /// </summary>
        /// <param name="elapsedSecondsSinceLastOpponentAction">
        /// Seconds since the opponent's last confirmed action (heartbeat, move, etc.).
        /// </param>
        public bool IsOpponentTimeoutExpired(float elapsedSecondsSinceLastOpponentAction)
        {
            if (_current == null || _current.state != WagerMatchState.InProgress)
                return false;

            return elapsedSecondsSinceLastOpponentAction >= _current.forfeitTimeoutSeconds;
        }

        // â”€â”€ Persistence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Save the current match state to local storage.
        /// Override this method or subscribe to <see cref="OnStateChanged"/>
        /// to implement your preferred persistence backend (PlayerPrefs, file, etc.).
        /// </summary>
        protected virtual void Save()
        {
            if (_current == null) return;
            _current.lastSavedTicks = DateTime.UtcNow.Ticks;

            // Default implementation: log only. Override for real persistence.
            // TODO: Implement real persistence (PlayerPrefs, JSON file, etc.)
            Debug.Log($"[WagerMatchOrchestrator] State saved: {_current.matchId} â†’ {_current.state}");
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void EmitStateChanged(WagerMatchState newState)
        {
            Debug.Log($"[WagerMatchOrchestrator] {_current?.matchId}: â†’ {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void EmitError(string message)
        {
            Debug.LogError($"[WagerMatchOrchestrator] {message}");
            OnError?.Invoke(message);
        }
    }
}
