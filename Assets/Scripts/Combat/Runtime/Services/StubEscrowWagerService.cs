using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Stub escrow wager service for local development.
    /// Manages the PvP Stamped wager lifecycle entirely in-memory.
    ///
    /// TODO [ANCHOR-ESCROW]: Each state transition maps to an Anchor instruction:
    ///   CreateMatchAsync â†’ initialize_escrow(match_id, player_mints[], opponent_mints[])
    ///   DepositAsync     â†’ deposit_inscription(match_id, mint_address) per Stamped
    ///   ForfeitAsync     â†’ forfeit_escrow(match_id)
    ///   SettleAsync       â†’ settle_escrow(match_id, selection_index)
    ///
    /// NOTE: VRF is NOT used for winner selection. The winner's choice is deterministic
    /// and player-driven. VRF is reserved for Stamped acquisition only (future phase).
    /// </summary>
    public class StubEscrowWagerService : IEscrowWagerService
    {
        private readonly IMoveInventoryService _inventory;
        private readonly Dictionary<string, MatchData> _matches = new Dictionary<string, MatchData>();

        public StubEscrowWagerService(IMoveInventoryService inventory)
        {
#if !UNITY_EDITOR && !UNITY_INCLUDE_TESTS
            throw new InvalidOperationException(ClosedBetaFeaturePolicy.DenialFor("ranked_wager"));
#endif
            _inventory = inventory;
        }

        public Task<string> CreateMatchAsync(string[] playerStampedMoveIds, string[] opponentStampedMoveIds)
        {
            string matchId = Guid.NewGuid().ToString("N").Substring(0, 12);

            var match = new MatchData
            {
                matchId = matchId,
                state = WagerMatchState.CreateMatch,
                playerStampedMoveIds = new List<string>(playerStampedMoveIds),
                opponentStampedMoveIds = new List<string>(opponentStampedMoveIds),
                createdAtTicks = DateTime.UtcNow.Ticks,
            };

            _matches[matchId] = match;
            Debug.Log($"[StubEscrowWagerService] Match created: {matchId} " +
                      $"(player: {playerStampedMoveIds.Length} Stamped moves, " +
                      $"opponent: {opponentStampedMoveIds.Length} Stamped moves)");

            // TODO [ANCHOR-ESCROW]: Call initialize_escrow on-chain here.
            return Task.FromResult(matchId);
        }

        public Task DepositAsync(string matchId)
        {
            if (!_matches.TryGetValue(matchId, out var match))
                throw new InvalidOperationException($"Match '{matchId}' not found.");

            if (match.state != WagerMatchState.CreateMatch && match.state != WagerMatchState.AwaitDeposits)
                throw new InvalidOperationException($"Match '{matchId}' is in state {match.state}, cannot deposit.");

            // Lock all player Stamped moves
            foreach (var id in match.playerStampedMoveIds)
            {
                var insc = _inventory.GetByInstanceId(id);
                if (insc != null)
                    insc.lockState = MoveLockState.Escrowed;
            }

            match.state = WagerMatchState.Locked;
            Debug.Log($"[StubEscrowWagerService] Deposited and locked Stamped moves for match {matchId}.");

            // TODO [ANCHOR-ESCROW]: Call deposit_inscription for each mint on-chain.
            return Task.CompletedTask;
        }

        public Task ForfeitAsync(string matchId, bool forfeitingPlayerIsLocal)
        {
            if (!_matches.TryGetValue(matchId, out var match))
                throw new InvalidOperationException($"Match '{matchId}' not found.");

            match.forfeitedByLocal = forfeitingPlayerIsLocal;
            match.state = WagerMatchState.VictoryDeclared;

            Debug.Log($"[StubEscrowWagerService] Forfeit declared for match {matchId}. " +
                      $"Local player forfeited: {forfeitingPlayerIsLocal}.");

            // TODO [ANCHOR-ESCROW]: Call forfeit_escrow on-chain.
            return Task.CompletedTask;
        }

        public IReadOnlyList<AnonymizedMove> GetOpponentEscrowedMoves(string matchId)
        {
            if (!_matches.TryGetValue(matchId, out var match))
                throw new InvalidOperationException($"Match '{matchId}' not found.");

            // Build anonymized list â€” no rarity, modifiers, or names exposed
            var anon = new AnonymizedMove[match.opponentStampedMoveIds.Count];
            for (int i = 0; i < match.opponentStampedMoveIds.Count; i++)
            {
                anon[i] = new AnonymizedMove
                {
                    internalId = match.opponentStampedMoveIds[i],
                    index = i,
                };
            }

            return anon;
        }

        public async Task<MoveInstance> SettleAsync(string matchId, int index)
        {
            if (!_matches.TryGetValue(matchId, out var match))
                throw new InvalidOperationException($"Match '{matchId}' not found.");

            if (match.state != WagerMatchState.VictoryDeclared && match.state != WagerMatchState.WinnerSelecting)
                throw new InvalidOperationException($"Match '{matchId}' is in state {match.state}, cannot settle.");

            if (index < 0 || index >= match.opponentStampedMoveIds.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Selection index {index} out of range [0, {match.opponentStampedMoveIds.Count}).");

            match.state = WagerMatchState.WinnerSelecting;

            // Resolve the selected Stamped
            string selectedId = match.opponentStampedMoveIds[index];
            var selected = _inventory.GetByInstanceId(selectedId);

            // Transfer the selected Stamped to the winner's inventory
            if (selected != null)
            {
                selected.lockState = MoveLockState.Unlocked;
                // In a real implementation, this would be a chain transfer.
                // For stub: the Stamped is already in inventory; just unlock it.
                Debug.Log($"[StubEscrowWagerService] Winner selected Stamped '{selectedId}' from match {matchId}.");
            }

            // Unlock all remaining opponent Stamped moves (return to loser)
            for (int i = 0; i < match.opponentStampedMoveIds.Count; i++)
            {
                if (i == index) continue;
                var remaining = _inventory.GetByInstanceId(match.opponentStampedMoveIds[i]);
                if (remaining != null)
                    remaining.lockState = MoveLockState.Unlocked;
            }

            // Unlock all player Stamped moves
            foreach (var id in match.playerStampedMoveIds)
            {
                var insc = _inventory.GetByInstanceId(id);
                if (insc != null)
                    insc.lockState = MoveLockState.Unlocked;
            }

            match.state = WagerMatchState.Settled;
            match.selectedIndex = index;

            Debug.Log($"[StubEscrowWagerService] Match {matchId} settled. " +
                      $"Selected index: {index}, Stamped: {selectedId}.");

            // TODO [ANCHOR-ESCROW]: Call settle_escrow(match_id, selection_index) on-chain.
            await Task.CompletedTask;
            return selected;
        }

        public WagerMatchState? GetMatchState(string matchId)
        {
            if (_matches.TryGetValue(matchId, out var match))
                return match.state;
            return null;
        }

        // â”€â”€ Internal Match Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private class MatchData
        {
            public string matchId;
            public WagerMatchState state;
            public List<string> playerStampedMoveIds;
            public List<string> opponentStampedMoveIds;
            public bool forfeitedByLocal;
            public int selectedIndex = -1;
            public long createdAtTicks;
        }
    }
}
