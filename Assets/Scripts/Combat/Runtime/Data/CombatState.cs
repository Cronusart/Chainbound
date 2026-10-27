using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Master state container for a battle.
    /// Holds both teams, turn counter, RNG seed, and phase info.
    /// Fully serializable for replay and logging.
    /// </summary>
    [Serializable]
    public class CombatState
    {
        /// <summary>Team 0 (typically the player).</summary>
        public TeamState team0;

        /// <summary>Team 1 (typically the opponent / AI).</summary>
        public TeamState team1;

        /// <summary>Current turn number (1-based).</summary>
        public int currentTurn;

        /// <summary>Turn on which Fee Collector already chose its one drain. -1 means not yet.</summary>
        public int feeCollectorAppliedTurn = -1;

        /// <summary>Current phase of the turn.</summary>
        public TurnPhase currentPhase;

        /// <summary>RNG seed used for this battle.</summary>
        public int seed;

        /// <summary>Is the battle still active?</summary>
        public bool isActive;

        /// <summary>Boss battles resolve a mutual wipe by KO order instead of declaring a draw.</summary>
        public bool isBossBattle;

        /// <summary>Winner team index (-1 = no winner yet, -2 = draw).</summary>
        public int winnerTeamIndex;

        /// <summary>
        /// Per-turn swap reservation: bench index already consumed by a swap this turn, per team.
        /// Prevents double-swap to the same (single) bench. -1 = no swap consumed yet.
        /// Reset at end of turn.
        /// </summary>
        public int swapReservedBenchIndexTeam0 = -1;

        /// <summary>Same as swapReservedBenchIndexTeam0 for team 1.</summary>
        public int swapReservedBenchIndexTeam1 = -1;

        /// <summary>Serializable turn memory used by modifier resolution and replay diagnostics.</summary>
        public TurnActionLedger turnActionLedger = new TurnActionLedger();

        /// <summary>Persistent, data-authored entry effects that apply to later swap-ins.</summary>
        public List<CombatFieldEntryEffect> fieldEntryEffects = new List<CombatFieldEntryEffect>();

        /// <summary>
        /// Capture action that passed resolver eligibility and is waiting for presentation.
        /// Difficulty (HP, statuses) must be read from live instances when the attempt starts.
        /// </summary>
        public PendingCaptureAttempt pendingCaptureAttempt;

        /// <summary>True if the given team has already used their bench swap this turn.</summary>
        public bool IsBenchSwapReservedForTeam(int teamIndex)
        {
            if (teamIndex == 0) return swapReservedBenchIndexTeam0 >= 0;
            if (teamIndex == 1) return swapReservedBenchIndexTeam1 >= 0;
            return false;
        }

        /// <summary>Reset per-turn swap reservations (call at end of turn).</summary>
        public void ResetSwapReservations()
        {
            swapReservedBenchIndexTeam0 = -1;
            swapReservedBenchIndexTeam1 = -1;
        }

        public CombatState()
        {
            team0 = new TeamState(0, "Team A");
            team1 = new TeamState(1, "Team B");
            currentTurn = 0;
            currentPhase = TurnPhase.NotStarted;
            isActive = false;
            isBossBattle = false;
            winnerTeamIndex = -1;
            turnActionLedger = new TurnActionLedger();
            fieldEntryEffects = new List<CombatFieldEntryEffect>();
            pendingCaptureAttempt = null;
        }

        public PendingCaptureAttempt ConsumePendingCaptureAttempt()
        {
            PendingCaptureAttempt pending = pendingCaptureAttempt;
            pendingCaptureAttempt = null;
            return pending;
        }

        /// <summary>Get team by index.</summary>
        public TeamState GetTeam(int index) => index == 0 ? team0 : team1;

        /// <summary>Get the opposing team.</summary>
        public TeamState GetOpposingTeam(int index) => index == 0 ? team1 : team0;
    }

    [Serializable]
    public class CombatFieldEntryEffect
    {
        public int sourceTeamIndex;
        public bool affectsOpposingTeam = true;
        public int turnsRemaining;
        public CombatStageStat stageStat;
        public int stageAmount;
        public float chance = 1f;
        public string stackKey;
        public string sourceTag;
    }

    /// <summary>
    /// Phases of a combat turn, per 02A Â§3 turn timing.
    /// </summary>
    public enum TurnPhase
    {
        NotStarted,
        TurnStart,          // Start of Turn triggers, baseline AP gain
        ActionSelection,    // Players choose actions
        ActionResolution,   // Hit check, damage, chemistry, status application
        EndOfTurn,          // DoT/regen, status AP changes, decrement durations
        BattleOver,
    }

    /// <summary>
    /// An action a player can take during the Action Selection phase.
    /// </summary>
    [Serializable]
    public class CombatAction
    {
        public CombatActionType actionType;

        /// <summary>Index into the Chainling's move list (for UseMove).</summary>
        public int moveIndex;

        /// <summary>Index of the Chainling to swap to (for Swap).</summary>
        public int swapTargetIndex;

        /// <summary>The team index this action belongs to.</summary>
        public int teamIndex;

        /// <summary>Explicit target team index for UseMove; -1 means auto-resolve.</summary>
        public int targetTeamIndex = -1;

        /// <summary>Explicit target slot index for UseMove; -1 means auto-resolve.</summary>
        public int targetSlotIndex = -1;

        /// <summary>Wild instance id for Capture. Resolved at the capture phase, not at queue time.</summary>
        public string captureTargetInstanceId;

        /// <summary>Inventory orb item id for Capture. Authority spends this only if the attempt starts.</summary>
        public string captureOrbItemId;

        public CombatAction() { }

        public static CombatAction UseMove(int teamIndex, int moveIndex)
        {
            return new CombatAction
            {
                actionType = CombatActionType.UseMove,
                moveIndex = moveIndex,
                teamIndex = teamIndex,
                targetTeamIndex = -1,
                targetSlotIndex = -1,
            };
        }

        public static CombatAction UseMove(int teamIndex, int moveIndex, int targetTeamIndex, int targetSlotIndex)
        {
            return new CombatAction
            {
                actionType = CombatActionType.UseMove,
                moveIndex = moveIndex,
                teamIndex = teamIndex,
                targetTeamIndex = targetTeamIndex,
                targetSlotIndex = targetSlotIndex,
            };
        }

        public static CombatAction Swap(int teamIndex, int swapTargetIndex)
        {
            return new CombatAction
            {
                actionType = CombatActionType.Swap,
                swapTargetIndex = swapTargetIndex,
                teamIndex = teamIndex,
            };
        }

        public static CombatAction Pass(int teamIndex)
        {
            return new CombatAction
            {
                actionType = CombatActionType.Pass,
                teamIndex = teamIndex,
            };
        }

        public static CombatAction Capture(int teamIndex, string targetInstanceId, string orbItemId)
        {
            return new CombatAction
            {
                actionType = CombatActionType.Capture,
                teamIndex = teamIndex,
                captureTargetInstanceId = targetInstanceId ?? string.Empty,
                captureOrbItemId = orbItemId ?? string.Empty,
                targetTeamIndex = 1,
                targetSlotIndex = -1,
            };
        }
    }

    /// <summary>
    /// Types of actions available during combat.
    /// </summary>
    public enum CombatActionType
    {
        UseMove,
        Swap,
        Pass,
        Capture,
    }

    /// <summary>
    /// Capture that survived combat-phase eligibility and is waiting for the trading-trace attempt.
    /// </summary>
    [Serializable]
    public class PendingCaptureAttempt
    {
        public string actorInstanceId;
        public string targetInstanceId;
        public string orbItemId;
        public int teamIndex;
    }
}
