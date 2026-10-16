using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Main combat resolver pipeline for 2v2 battles.
    /// Each team fields 2 active Chainlings with 1 on the bench.
    /// When an active Chainling faints the bench auto-swaps in.
    /// Orchestrates: turn order â†’ action â†’ hit check â†’ damage â†’ chemistry â†’ status â†’ AP â†’ KO â†’ auto-swap.
    /// Pure C# engine â€” no MonoBehaviour dependency. Fully deterministic when seeded.
    /// </summary>
    public class CombatResolver
    {
        private readonly ChemistryResolver _chemistry;
        private readonly StatusProcessor _statusProcessor;
        private readonly CombatEventLog _log;
        private readonly CombatRng _rng;
        private readonly StatusCatalogSO _statusCatalog;
        private readonly ModifierCatalogSO _modifierCatalog;
        private readonly CombatResolverConfigSO _resolverConfig;
        private readonly CombatRulesetSO _ruleset;
        private readonly CombatActionEligibilityService _eligibilityService;
        private readonly UtilityEffectExecutor _utilityEffectExecutor;
        private readonly CombatApService _apService;
        private CombatState _currentState;
        private int _stepActionCursor = -1;
        private bool _passAReactiveCastInProgress;
        private ConfigStampPayload _configStamp;
        // Granted during an action and ticked at the shared end of that turn.
        // Duration 1 is the rest of this turn: later actions can still use it, then it expires.
        private const int RestOfTurnDuration = 1;

        private static readonly CoreStat[] RandomStageStats =
        {
            CoreStat.MeleeAtk,
            CoreStat.MeleeDef,
            CoreStat.RangedAtk,
            CoreStat.RangedDef,
            CoreStat.Speed
        };

        public CombatEventLog Log => _log;
        public CombatRng Rng => _rng;
        public CombatResolverConfigSO ResolverConfig => _resolverConfig;
        public CombatRulesetSO Ruleset => _ruleset;
        public ConfigStampPayload ConfigStamp => _configStamp;
        public string RulesetId => _configStamp != null ? _configStamp.rulesetId : null;
        public string ConfigHash => _configStamp != null ? _configStamp.configHash : null;

        public struct CombatActionStep
        {
            public ChainlingInstance actor;
            public CombatAction action;
            public TeamState actingTeam;
            public TeamState opposingTeam;
            public int speed;
            public bool hasPriority;
        }

        public CombatResolver(
            TypeChemistryMatrixSO chemistryMatrix,
            StatusCatalogSO statusCatalog,
            int seed,
            bool chemistryStrictMode = true,
            ModifierCatalogSO modifierCatalog = null,
            CombatResolverConfigSO resolverConfig = null,
            CombatRulesetSO ruleset = null)
        {
            _rng = new CombatRng(seed);
            _log = new CombatEventLog(seed);
            _log.AttachRng(_rng);
            _statusCatalog = statusCatalog;
            _modifierCatalog = modifierCatalog;
            _ruleset = ruleset != null ? ruleset : BalanceConfigRuntime.Ruleset;
            _resolverConfig = resolverConfig != null
                ? resolverConfig
                : (_ruleset != null ? _ruleset.ResolveResolverConfig() : BalanceConfigRuntime.ResolverConfig);
            _apService = new CombatApService(_log, _resolverConfig);
            _eligibilityService = new CombatActionEligibilityService();
            _chemistry = new ChemistryResolver(chemistryMatrix, statusCatalog, _log, chemistryStrictMode, _resolverConfig);
            _statusProcessor = new StatusProcessor(statusCatalog, _log, _apService, _resolverConfig);
            _utilityEffectExecutor = new UtilityEffectExecutor(_log, _apService, _statusProcessor);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Battle Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Initialize a new 2v2 battle.
        /// Each team must have up to ROSTER_SIZE (3) Chainlings.
        /// The first 2 are placed on the field; the 3rd sits on the bench.
        /// </summary>
        public CombatState InitializeBattle(TeamState team0, TeamState team1)
        {
            var state = new CombatState
            {
                team0 = team0,
                team1 = team1,
                seed = _rng.Seed,
                isActive = true,
                currentTurn = 0,
                currentPhase = TurnPhase.NotStarted,
            };
            _log.SetForensicContext(0, -1);

            // Register all Chainling & move names so log messages are readable
            RegisterTeamNames(team0);
            RegisterTeamNames(team1);

            ActivateStartingLineup(team0);
            ActivateStartingLineup(team1);
            _log.SyncEntityRefs(state);
            StampAndLogConfig();

            // Build a readable roster summary
            string roster0 = BuildRosterSummary(team0);
            string roster1 = BuildRosterSummary(team1);

            _log.LogMessage(CombatEventType.BattleStart,
                $"Battle started (2v2)! Seed={_rng.Seed}\n" +
                $"  {team0.teamName}: {roster0}\n" +
                $"  {team1.teamName}: {roster1}");
            EnsureTurnActionLedger(state);
            _currentState = state;
            ProcessBattleStartModifiers(state);

            return state;
        }

        private void StampAndLogConfig()
        {
            CombatRulesetSO ruleset = _ruleset != null ? _ruleset : BalanceConfigRuntime.Ruleset;
            CombatResolverConfigSO resolver = _resolverConfig != null
                ? _resolverConfig
                : (ruleset != null ? ruleset.ResolveResolverConfig() : BalanceConfigRuntime.ResolverConfig);

            ModifierBalanceOverlaySO overlay = BalanceConfigRuntime.ActiveModifierOverlay;

            _configStamp = new ConfigStampPayload
            {
                rulesetId = ruleset != null ? ruleset.rulesetId : string.Empty,
                configVersion = ruleset != null ? ruleset.configVersion : 0,
                configHash = ruleset != null ? ruleset.ComputeConfigHash() : string.Empty,
                resolverConfigVersion = resolver != null ? resolver.configVersion : 0,
                resolverConfigHash = resolver != null ? resolver.ComputeConfigHash() : string.Empty,
                overlayId = overlay != null ? overlay.overlayId : null,
            };

            _log.SetConfigStampMetadata(_configStamp.rulesetId, _configStamp.configHash);
            _log.LogForensic(CombatForensicEventType.ConfigStamp, _configStamp, null, null, 0, -1);
        }

        private static TurnActionLedger EnsureTurnActionLedger(CombatState state)
        {
            if (state == null)
                return null;
            if (state.turnActionLedger == null)
                state.turnActionLedger = new TurnActionLedger();
            return state.turnActionLedger;
        }

        private static void BeginLedgerTurn(CombatState state)
        {
            TurnActionLedger ledger = EnsureTurnActionLedger(state);
            if (ledger != null)
                ledger.BeginTurn(state.currentTurn);
        }

        private static void EndLedgerTurn(CombatState state)
        {
            TurnActionLedger ledger = EnsureTurnActionLedger(state);
            if (ledger != null)
                ledger.EndTurn(state.currentTurn);
        }

        private void RegisterTeamNames(TeamState team)
        {
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var c = team.chainlings[i];
                _log.RegisterName(c.instanceId, CombatNarrationNaming.FormatChainlingName(team, c));
                for (int j = 0; j < c.moves.Count; j++)
                    _log.RegisterName(c.moves[j].instanceId, c.moves[j].displayName);
            }
        }

        private string BuildRosterSummary(TeamState team)
        {
            var parts = new List<string>();
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var c = team.chainlings[i];
                string role = i < CombatConstants.ACTIVE_PER_TEAM ? (i == 0 ? "Lead" : "Partner") : "Bench";
                parts.Add($"{c.displayName} [{c.primaryType}] ({role})");
            }
            return string.Join(", ", parts);
        }

        /// <summary>Place the first ACTIVE_PER_TEAM Chainlings on the field.</summary>
        private void ActivateStartingLineup(TeamState team)
        {
            team.activeIndices.Clear();
            int activated = 0;
            for (int i = 0; i < team.chainlings.Count && activated < CombatConstants.ACTIVE_PER_TEAM; i++)
            {
                team.activeIndices.Add(i);
                team.chainlings[i].isActive = true;
                activated++;
            }
            // Any remaining Chainlings start on the bench
            for (int i = activated; i < team.chainlings.Count; i++)
            {
                team.chainlings[i].isActive = false;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Turn Execution
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Execute one full turn.
        /// In 2v2 each active Chainling may act. Actions are provided per team
        /// (one action per active slot, packed into action lists).
        /// Simpler overload: pass one action per team (lead Chainling acts).
        /// Returns false if the battle is over.
        /// </summary>
        public bool ExecuteTurn(CombatState state, CombatAction action0, CombatAction action1)
        {
            // Wrap single actions into per-slot lists for the full 2v2 pipeline
            var actions0 = new List<CombatAction> { action0 };
            var actions1 = new List<CombatAction> { action1 };

            // Add pass actions for partner slots if they exist
            if (state.team0.GetActiveInSlot(1) != null)
                actions0.Add(CombatAction.Pass(0));
            if (state.team1.GetActiveInSlot(1) != null)
                actions1.Add(CombatAction.Pass(1));

            return ExecuteTurn2v2(state, actions0, actions1);
        }

        /// <summary>
        /// AI-driven turn execution. Actions are generated AFTER AP gain so the AI
        /// sees the correct AP values. This fixes the Turn 1 pass bug.
        /// </summary>
        public bool ExecuteTurnWithAI(CombatState state, ICombatAI ai0, ICombatAI ai1)
        {
            if (!state.isActive) return false;
            _currentState = state;

            state.currentTurn++;
            state.currentPhase = TurnPhase.TurnStart;
            _stepActionCursor = -1;
            BeginLedgerTurn(state);
            _log.SetForensicContext(state.currentTurn, -1);
            _log.SyncEntityRefs(state);
            _log.LogMessage(CombatEventType.TurnStart, $"=== Turn {state.currentTurn} ===", state.currentTurn);

            // AP gain first
            ProcessStartOfTurnForTeam(state.team0, state.currentTurn);
            ProcessStartOfTurnForTeam(state.team1, state.currentTurn);
            ProcessStartOfTurnTriggersForTeam(state.team0, state.currentTurn);
            ProcessStartOfTurnTriggersForTeam(state.team1, state.currentTurn);

            // NOW generate actions (AI sees correct AP)
            state.currentPhase = TurnPhase.ActionSelection;
            var actions0 = ai0.GenerateActions(state, 0, _rng);
            var actions1 = ai1.GenerateActions(state, 1, _rng);

            // Hand off to the core pipeline (skips start-of-turn since we already did it)
            return ExecuteTurn2v2Internal(state, actions0, actions1, skipStartOfTurn: true);
        }

        /// <summary>
        /// Full 2v2 turn execution with pre-built action lists.
        /// Each team provides one action per active slot.
        /// </summary>
        public bool ExecuteTurn2v2(CombatState state, List<CombatAction> actions0, List<CombatAction> actions1)
        {
            return ExecuteTurn2v2Internal(state, actions0, actions1, skipStartOfTurn: false);
        }

        private bool ExecuteTurn2v2Internal(CombatState state,
            List<CombatAction> actions0, List<CombatAction> actions1, bool skipStartOfTurn)
        {
            if (!state.isActive) return false;
            _currentState = state;

            if (!skipStartOfTurn)
            {
                state.currentTurn++;
                state.currentPhase = TurnPhase.TurnStart;
                _stepActionCursor = -1;
                BeginLedgerTurn(state);
                _log.SetForensicContext(state.currentTurn, -1);
                _log.SyncEntityRefs(state);
                _log.LogMessage(CombatEventType.TurnStart, $"=== Turn {state.currentTurn} ===", state.currentTurn);

                // â”€â”€ Start of Turn â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                ProcessStartOfTurnForTeam(state.team0, state.currentTurn);
                ProcessStartOfTurnForTeam(state.team1, state.currentTurn);
                ProcessStartOfTurnTriggersForTeam(state.team0, state.currentTurn);
                ProcessStartOfTurnTriggersForTeam(state.team1, state.currentTurn);
            }

            // â”€â”€ Action Resolution â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            state.currentPhase = TurnPhase.ActionResolution;

            // Build a combined list of (Chainling, Action, TeamState, OpposingTeam) sorted by Speed
            var actionQueue = BuildActionQueue(state, actions0, actions1);

            // Execute: Phase 1 (swaps) first, then Phase 2 (combat). Each phase in speed order.
            for (int i = 0; i < actionQueue.Count; i++)
            {
                var entry = actionQueue[i];
                if (!state.isActive) break;
                if (entry.actor == null || !entry.actor.isAlive) continue;

                _log.SetForensicContext(state.currentTurn, i);
                _log.SyncEntityRefs(state);
                string phaseReason = entry.action.actionType == CombatActionType.Swap
                    ? "SwapPhase"
                    : entry.action.actionType == CombatActionType.Capture
                        ? "CapturePhase"
                    : HasPriorTag(entry) ? "PriorityBlock" : "CombatPhase";
                _log.LogForensic(
                    CombatForensicEventType.ActorSelected,
                    new ActorSelectedPayload
                    {
                        reason = phaseReason,
                        queueIndex = i,
                    },
                    entry.actor.instanceId,
                    null,
                    state.currentTurn,
                    i);

                ResolveAction(state, entry.actingTeam, entry.opposingTeam, entry.action, entry.actor, entry.hasPriority);

                // After every action check for KOs and auto-swap
                ProcessAutoSwaps(state, state.currentTurn);
                if (CheckBattleEnd(state))
                {
                    LogTurnStateDigest(state);
                    return false;
                }
            }

            // â”€â”€ End of Turn â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            state.currentPhase = TurnPhase.EndOfTurn;
            state.pendingCaptureAttempt = null;
            state.ResetSwapReservations();
            EndLedgerTurn(state);
            _log.SetForensicContext(state.currentTurn, -1);

            var turnTicks = new List<TurnEndStatusTickEntry>(8);
            ProcessEndOfTurnForTeam(state.team0, state.currentTurn, turnTicks);
            ProcessEndOfTurnForTeam(state.team1, state.currentTurn, turnTicks);
            TickFieldEntryEffects(state);
            LogTurnEndForensics(state, turnTicks);

            // Auto-swap after End-of-Turn DoT KOs
            ProcessAutoSwaps(state, state.currentTurn);
            if (CheckBattleEnd(state))
            {
                LogTurnStateDigest(state);
                return false;
            }

            LogTurnStateDigest(state);
            _log.LogMessage(CombatEventType.TurnEnd, $"=== End Turn {state.currentTurn} ===", state.currentTurn);
            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Start / End of Turn (per team, all active Chainlings)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ProcessStartOfTurnForTeam(TeamState team, int turn)
        {
            if (team?.chainlings != null)
            {
                for (int i = 0; i < team.chainlings.Count; i++)
                {
                    if (team.chainlings[i] != null)
                        team.chainlings[i].hitsTakenThisTurn = 0;
                }
            }
            ApplyPendingTurnStartStatusesForTeam(team, turn);
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                var c = team.GetActiveInSlot(s);
                if (c != null && c.isAlive)
                {
                    _statusProcessor.ProcessStartOfTurn(c, turn, _rng);
                }
            }
        }

        private void ProcessStartOfTurnTriggersForTeam(TeamState team, int turn)
        {
            if (team == null)
                return;
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                ChainlingInstance c = team.GetActiveInSlot(s);
                if (c == null || !c.isAlive)
                    continue;
                ProcessArmedWindows(c, team, null, ArmedWindowTrigger.OwnerTurnStart, turn);
                CheckPassiveTriggers(c, turn, PassiveTriggerPoint.TurnStart);
            }
        }

        private void ApplyPendingTurnStartStatusesForTeam(TeamState team, int turn)
        {
            if (team == null || team.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null || chainling.pendingTurnStartStatuses == null)
                    continue;

                for (int p = chainling.pendingTurnStartStatuses.Count - 1; p >= 0; p--)
                {
                    PendingStatusApplication pending = chainling.pendingTurnStartStatuses[p];
                    if (pending == null || pending.applyOnTurn > turn)
                        continue;

                    _statusProcessor.ApplyStatus(chainling, pending.statusId,
                        pending.duration, pending.sourceId, turn);
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{chainling.displayName} gained pending {pending.statusId}.",
                        turn);
                    chainling.pendingTurnStartStatuses.RemoveAt(p);
                }
            }
        }

        private void ProcessEndOfTurnForTeam(TeamState team, int turn, List<TurnEndStatusTickEntry> turnTicks)
        {
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                var c = team.GetActiveInSlot(s);
                if (c != null && c.isAlive)
                {
                    int buffCountBefore = CountActiveBuffStatuses(c);
                    _statusProcessor.ProcessEndOfTurn(c, turn, turnTicks);
                    if (!c.isAlive)
                    {
                        HandleEndOfTurnKo(team, c, turn);
                        continue;
                    }
                    int expiredBuffs = Math.Max(0, buffCountBefore - CountActiveBuffStatuses(c));
                    if (expiredBuffs > 0)
                        CheckPassiveTriggers(c, turn, PassiveTriggerPoint.OnBuffExpired, damage: expiredBuffs);
                    CheckPassiveTriggers(c, turn, PassiveTriggerPoint.EndOfTurn);
                    ProcessMitigationTimers(c, turn);
                    ProcessAccuracyTimers(c, turn);
                    ProcessReactiveApTimers(c, turn);
                    ProcessBarrierTimers(c, turn);
                    TickArmedWindows(c, turn);
                    c.apDonationUsedThisTurn = false;
                }
            }

            TickBenchedRecharge(team, turn);
        }

        private void TickBenchedRecharge(TeamState team, int turn)
        {
            if (team?.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling == null || !chainling.isAlive || chainling.isActive)
                    continue;
                if (team.activeIndices != null && team.activeIndices.Contains(i))
                    continue;
                _statusProcessor.TickRechargeWhileBenched(chainling, turn);
            }
        }

        /// <summary>
        /// Player-facing recharge length. Authored rechargeTurns includes the granting
        /// turn's end tick, so the skipped upcoming turns are one less. A sentinel of
        /// 100 or more is the once-per-battle lock (Fox Feint uses 999).
        /// </summary>
        public static string FormatPlayerFacingRecharge(string displayName, int rechargeTurns)
        {
            string name = string.IsNullOrWhiteSpace(displayName) ? "Move" : displayName;
            if (rechargeTurns >= 100)
                return $"{name} cannot be used again this battle.";

            int skippedTurns = Math.Max(0, rechargeTurns - 1);
            if (skippedTurns <= 0)
                return $"{name} can be used again next turn.";
            string unit = skippedTurns == 1 ? "turn" : "turns";
            return $"{name} now recharging ({skippedTurns} {unit})";
        }

        private void HandleEndOfTurnKo(TeamState defeatedTeam, ChainlingInstance defeated, int turn)
        {
            if (defeatedTeam == null || defeated == null)
                return;

            EnsureTurnActionLedger(_currentState)?.RecordKo(defeated.instanceId, defeatedTeam.teamIndex);
            TeamState opposingTeam = _currentState != null
                ? _currentState.GetOpposingTeam(defeatedTeam.teamIndex)
                : null;
            ProcessArmedWindowsForTeam(opposingTeam, defeated, ArmedWindowTrigger.FoeKO, turn);
            CheckPassiveTriggersForTeam(
                opposingTeam,
                turn,
                PassiveTriggerPoint.OnKO,
                defeated,
                sourceMove: null);
            CheckPassiveTriggersForTeam(
                defeatedTeam,
                turn,
                PassiveTriggerPoint.AllyKO,
                defeated,
                sourceMove: null,
                exclude: defeated);
        }

        private void LogTurnStateDigest(CombatState state)
        {
            if (state == null || _log == null)
                return;

            string digest = BuildTurnStateDigest(state);
            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.TurnEnd,
                turn = state.currentTurn,
                message = digest,
                metadataJson = digest,
            });
            Debug.Log("[PvPTurn] state " + digest);
        }

        private static string BuildTurnStateDigest(CombatState state)
        {
            var builder = new StringBuilder();
            builder.Append("turn=").Append(state.currentTurn);
            AppendTeamDigest(builder, state.team0);
            AppendTeamDigest(builder, state.team1);
            return builder.ToString();
        }

        private static void AppendTeamDigest(StringBuilder builder, TeamState team)
        {
            if (team == null)
            {
                builder.Append("|team=-");
                return;
            }

            builder.Append("|team=").Append(team.teamIndex).Append(";act=");
            if (team.activeIndices != null)
            {
                for (int i = 0; i < team.activeIndices.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append(team.activeIndices[i]);
                }
            }

            if (team.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                builder.Append(";c").Append(i).Append('=');
                if (chainling == null)
                {
                    builder.Append('-');
                    continue;
                }

                builder.Append(chainling.instanceId);
                builder.Append(" hp=").Append(chainling.currentHP).Append('/').Append(chainling.EffectiveMaxHP);
                builder.Append(" ap=").Append(chainling.currentAP);
                builder.Append(" a=").Append(chainling.isAlive ? '1' : '0');
                builder.Append(" st=")
                    .Append(chainling.stageMeleeAtk).Append(',')
                    .Append(chainling.stageMeleeDef).Append(',')
                    .Append(chainling.stageRangedAtk).Append(',')
                    .Append(chainling.stageRangedDef).Append(',')
                    .Append(chainling.stageSpeed).Append(',')
                    .Append(chainling.stageAccuracy).Append(',')
                    .Append(chainling.stageEvasion).Append(',')
                    .Append(chainling.stageCritRate);
                builder.Append(" m=").Append(chainling.GetTotalMitigation().ToString("0.0000", CultureInfo.InvariantCulture));
                builder.Append(" x=").Append(chainling.GetAccuracySourceMultiplier().ToString("0.0000", CultureInfo.InvariantCulture));
                builder.Append(" fx=");
                AppendStatusDigest(builder, chainling.statuses);
            }
        }

        private static void AppendStatusDigest(StringBuilder builder, List<StatusInstance> statuses)
        {
            if (statuses == null || statuses.Count == 0)
                return;

            var ordered = new List<StatusInstance>(statuses.Count);
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i] != null)
                    ordered.Add(statuses[i]);
            }

            ordered.Sort((left, right) =>
            {
                int id = ((int)left.statusId).CompareTo((int)right.statusId);
                if (id != 0)
                    return id;
                return left.turnsRemaining.CompareTo(right.turnsRemaining);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append((int)ordered[i].statusId);
                builder.Append(':').Append(ordered[i].turnsRemaining);
                builder.Append(':').Append(ordered[i].counterValue);
            }
        }

        private void LogTurnEndForensics(CombatState state, List<TurnEndStatusTickEntry> turnTicks)
        {
            if (!CombatLogSettings.EnableForensicLogging || state == null)
                return;

            _log.SyncEntityRefs(state);

            var ticksPayload = new TurnEndStatusTicksPayload();
            if (turnTicks != null && turnTicks.Count > 0)
                ticksPayload.ticks.AddRange(turnTicks);

            _log.LogForensic(
                CombatForensicEventType.TurnEndStatusTicks,
                ticksPayload,
                null,
                null,
                state.currentTurn,
                -1);

            var snapshotPayload = new TurnEndSnapshotPayload();
            AddTeamSnapshot(state.team0, snapshotPayload.entities);
            AddTeamSnapshot(state.team1, snapshotPayload.entities);

            _log.LogForensic(
                CombatForensicEventType.TurnEndSnapshot,
                snapshotPayload,
                null,
                null,
                state.currentTurn,
                -1);
        }

        private void AddTeamSnapshot(TeamState team, List<EntityTurnSnapshotPayload> output)
        {
            if (team == null || team.chainlings == null || output == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling == null)
                    continue;

                var entity = new EntityTurnSnapshotPayload
                {
                    entityRef = _log.ResolveEntityRef(chainling.instanceId),
                    speciesId = chainling.speciesId,
                    displayName = _log.Name(chainling.instanceId),
                    hp = chainling.currentHP,
                    maxHp = chainling.EffectiveMaxHP,
                    ap = chainling.currentAP,
                    maxAp = _resolverConfig.apCap,
                    isAlive = chainling.isAlive,
                    primaryType = chainling.primaryType,
                    secondaryType = chainling.secondaryType,
                    coatingType = chainling.coatingType,
                    stageMeleeAtk = chainling.stageMeleeAtk,
                    stageMeleeDef = chainling.stageMeleeDef,
                    stageRangedAtk = chainling.stageRangedAtk,
                    stageRangedDef = chainling.stageRangedDef,
                    stageSpeed = chainling.stageSpeed,
                    stageAccuracy = chainling.stageAccuracy,
                    stageEvasion = chainling.stageEvasion,
                    stageCritRate = chainling.stageCritRate,
                    mitigationTotal = chainling.GetTotalMitigation(),
                    mitigationSourceCount = chainling.mitigationSources != null ? chainling.mitigationSources.Count : 0,
                    accuracyBonusMultiplier = chainling.GetAccuracySourceMultiplier(),
                    accuracySourceCount = chainling.accuracySources != null ? chainling.accuracySources.Count : 0,
                };

                if (chainling.statuses != null)
                {
                    for (int s = 0; s < chainling.statuses.Count; s++)
                    {
                        var status = chainling.statuses[s];
                        if (status == null || status.IsExpired)
                            continue;

                        entity.statuses.Add(new StatusSnapshotPayload
                        {
                            statusId = status.statusId.ToString(),
                            turnsRemaining = status.turnsRemaining,
                            consumed = status.isConsumed,
                        });
                    }
                }

                output.Add(entity);
            }
        }

        private static float ResolveTieBreakRoll(List<TieBreakTrace> tieBreaks, string actorInstanceId)
        {
            if (tieBreaks == null || string.IsNullOrEmpty(actorInstanceId))
                return -1f;

            for (int i = 0; i < tieBreaks.Count; i++)
            {
                var tie = tieBreaks[i];
                if (tie == null || tie.actorIds == null || tie.rolls == null)
                    continue;

                int max = Math.Min(tie.actorIds.Count, tie.rolls.Count);
                for (int j = 0; j < max; j++)
                {
                    if (tie.actorIds[j] == actorInstanceId)
                        return tie.rolls[j];
                }
            }

            return -1f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Action Queue (Speed-ordered)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private struct ActionQueueEntry
        {
            public ChainlingInstance actor;
            public CombatAction action;
            public TeamState actingTeam;
            public TeamState opposingTeam;
            public int teamIndex;
            public int slotIndex;
            public int stars;
            public int rebirthCount;
            public int progressionTieBreak;
            public int speed;
            public StatBreakdown speedBreakdown;
            public bool hasPriority;
        }

        private List<ActionQueueEntry> BuildActionQueue(CombatState state,
            List<CombatAction> actions0, List<CombatAction> actions1)
        {
            actions0 = NormalizeActionsForQueue(state, state.team0, actions0, 0);
            actions1 = NormalizeActionsForQueue(state, state.team1, actions1, 1);
            EnforceSingleCapture(actions0);
            EnforceSingleCapture(actions1);

            var queue = new List<ActionQueueEntry>();

            // Team 0 actions
            for (int s = 0; s < Math.Min(actions0.Count, state.team0.activeIndices.Count); s++)
            {
                var actor = state.team0.GetActiveInSlot(s);
                if (actor != null && actor.isAlive)
                {
                    var speedBreakdown = actor.GetStatBreakdown(CoreStat.Speed);
                    queue.Add(new ActionQueueEntry
                    {
                        actor = actor,
                        action = actions0[s],
                        actingTeam = state.team0,
                        opposingTeam = state.team1,
                        teamIndex = 0,
                        slotIndex = s,
                        stars = actor.stars,
                        rebirthCount = actor.rebirthCount,
                        progressionTieBreak = actor.ProgressionTieBreakScore,
                        speed = speedBreakdown.finalInt,
                        speedBreakdown = speedBreakdown,
                        hasPriority = ResolvePriorityForAction(actor, actions0[s], state.currentTurn)
                    });
                }
            }

            // Team 1 actions
            for (int s = 0; s < Math.Min(actions1.Count, state.team1.activeIndices.Count); s++)
            {
                var actor = state.team1.GetActiveInSlot(s);
                if (actor != null && actor.isAlive)
                {
                    var speedBreakdown = actor.GetStatBreakdown(CoreStat.Speed);
                    queue.Add(new ActionQueueEntry
                    {
                        actor = actor,
                        action = actions1[s],
                        actingTeam = state.team1,
                        opposingTeam = state.team0,
                        teamIndex = 1,
                        slotIndex = s,
                        stars = actor.stars,
                        rebirthCount = actor.rebirthCount,
                        progressionTieBreak = actor.ProgressionTieBreakScore,
                        speed = speedBreakdown.finalInt,
                        speedBreakdown = speedBreakdown,
                        hasPriority = ResolvePriorityForAction(actor, actions1[s], state.currentTurn)
                    });
                }
            }

            // Primary sort: Speed desc, then stars, then rebirth.
            queue.Sort((a, b) =>
            {
                if (a.speed != b.speed)
                    return b.speed.CompareTo(a.speed);
                if (a.stars != b.stars)
                    return b.stars.CompareTo(a.stars);
                if (a.rebirthCount != b.rebirthCount)
                    return b.rebirthCount.CompareTo(a.rebirthCount);
                return string.CompareOrdinal(a.actor.instanceId, b.actor.instanceId);
            });

            // 50/50 tie break for groups tied on (speed, stars, rebirth), with explicit trace.
            var tieBreaks = new List<TieBreakTrace>();
            for (int i = 0; i < queue.Count;)
            {
                int j = i + 1;
                while (j < queue.Count &&
                       queue[j].speed == queue[i].speed &&
                       queue[j].stars == queue[i].stars &&
                       queue[j].rebirthCount == queue[i].rebirthCount)
                    j++;

                int count = j - i;
                if (count > 1)
                {
                    var tie = new TieBreakTrace();
                    var group = new List<ActionQueueEntry>(count);
                    for (int k = i; k < j; k++)
                        group.Add(queue[k]);

                    var rolled = new List<(ActionQueueEntry entry, float roll)>(count);
                    for (int k = 0; k < group.Count; k++)
                    {
                        float roll = _rng.NextFloat();
                        rolled.Add((group[k], roll));
                        tie.actorIds.Add(group[k].actor.instanceId);
                        tie.rolls.Add(roll);
                    }

                    rolled.Sort((x, y) =>
                    {
                        int cmp = y.roll.CompareTo(x.roll);
                        if (cmp != 0) return cmp;
                        return string.CompareOrdinal(x.entry.actor.instanceId, y.entry.actor.instanceId);
                    });

                    for (int k = 0; k < rolled.Count; k++)
                    {
                        queue[i + k] = rolled[k].entry;
                        tie.resolvedOrder.Add(rolled[k].entry.actor.instanceId);
                    }

                    tieBreaks.Add(tie);
                    _log.LogDetailedEvent(
                        CombatEventType.SpeedTieResolved,
                        state.currentTurn,
                        $"SpeedTieRoll: resolved speed tie between {tie.actorIds.Count} actors using 50/50 rolls.",
                        JsonUtility.ToJson(tie));
                }

                i = j;
            }

            var initiativePayload = new InitiativeTracePayload { turn = state.currentTurn };
            for (int i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                initiativePayload.candidates.Add(new InitiativeActorTrace
                {
                    actorId = entry.actor.instanceId,
                    teamIndex = entry.teamIndex,
                    slotIndex = entry.slotIndex,
                    stars = entry.stars,
                    rebirthCount = entry.rebirthCount,
                    speedFinal = entry.speed,
                    speedBreakdown = entry.speedBreakdown
                });
                initiativePayload.finalOrder.Add(entry.actor.instanceId);
            }
            initiativePayload.tieBreaks = tieBreaks;

            string summary;
            if (queue.Count >= 2)
            {
                var a = queue[0];
                var b = queue[1];
                if (a.speed == b.speed && a.stars == b.stars && a.rebirthCount == b.rebirthCount)
                    summary = $"Speed tie! {_log.Name(a.actor.instanceId)} moved first.";
                else
                    summary = $"{_log.Name(a.actor.instanceId)} moved first (Speed {a.speed} vs {b.speed}).";
            }
            else if (queue.Count == 1)
            {
                summary = $"{_log.Name(queue[0].actor.instanceId)} is the only acting chainling.";
            }
            else
            {
                summary = "No valid actors in initiative order.";
            }

            _log.LogDetailedEvent(
                CombatEventType.InitiativeResolved,
                state.currentTurn,
                summary,
                JsonUtility.ToJson(initiativePayload));

            // Split into swap phase, priority block (PRIOR tag), combat block, then capture.
            var swapPhase = new List<ActionQueueEntry>();
            var priorityBlock = new List<ActionQueueEntry>();
            var combatPhase = new List<ActionQueueEntry>();
            var capturePhase = new List<ActionQueueEntry>();
            for (int i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                if (entry.action.actionType == CombatActionType.Swap)
                    swapPhase.Add(entry);
                else if (entry.action.actionType == CombatActionType.Capture)
                    capturePhase.Add(entry);
                else if (HasPriorTag(entry))
                    priorityBlock.Add(entry);
                else
                    combatPhase.Add(entry);
            }

            // Execution order: swaps → priority block → normal combat → capture
            var executionOrder = new List<ActionQueueEntry>();
            executionOrder.AddRange(swapPhase);
            executionOrder.AddRange(priorityBlock);
            executionOrder.AddRange(combatPhase);
            executionOrder.AddRange(capturePhase);

            if (CombatLogSettings.EnableForensicLogging)
            {
                var forensicOrder = new TurnOrderComputedPayload
                {
                    tieBreakRuleUsed = "speed -> stars -> rebirth -> 50/50 rng",
                };

                for (int i = 0; i < queue.Count; i++)
                {
                    var entry = queue[i];
                    forensicOrder.candidates.Add(new ForensicActorInfo
                    {
                        actorRef = _log.ResolveEntityRef(entry.actor.instanceId),
                        speciesId = entry.actor.speciesId,
                        displayName = _log.Name(entry.actor.instanceId),
                        stars = entry.stars,
                        rebirthCount = entry.rebirthCount,
                        speedFinal = entry.speed,
                        tieBreakRoll = ResolveTieBreakRoll(tieBreaks, entry.actor.instanceId),
                    });
                }

                forensicOrder.swapPhaseOrder.AddRange(swapPhase.ConvertAll(e => e.actor.instanceId));
                forensicOrder.priorityBlockOrder.AddRange(priorityBlock.ConvertAll(e => e.actor.instanceId));
                forensicOrder.combatPhaseOrder.AddRange(combatPhase.ConvertAll(e => e.actor.instanceId));
                forensicOrder.capturePhaseOrder.AddRange(capturePhase.ConvertAll(e => e.actor.instanceId));
                forensicOrder.finalOrder.AddRange(executionOrder.ConvertAll(e => e.actor.instanceId));

                for (int i = 0; i < tieBreaks.Count; i++)
                {
                    var tie = tieBreaks[i];
                    var group = new TieBreakGroupPayload();
                    group.actorInstanceIds.AddRange(tie.actorIds);
                    group.rolls.AddRange(tie.rolls);
                    group.resolvedOrder.AddRange(tie.resolvedOrder);
                    forensicOrder.tieBreakGroups.Add(group);
                }

                _log.LogForensic(
                    CombatForensicEventType.TurnOrderComputed,
                    forensicOrder,
                    null,
                    null,
                    state.currentTurn,
                    -1);
            }

            return executionOrder;
        }

        private List<CombatAction> NormalizeActionsForQueue(CombatState state, TeamState team, List<CombatAction> actions, int teamIndex)
        {
            int activeSlotCount = team != null && team.activeIndices != null ? team.activeIndices.Count : 0;
            if (activeSlotCount <= 0)
                return new List<CombatAction>();

            var incoming = actions ?? new List<CombatAction>();
            if (incoming.Count == activeSlotCount)
                return incoming;

            int aliveActiveCount = CountAliveActiveSlots(team);
            string strategy;
            List<CombatAction> normalized;

            if (incoming.Count == aliveActiveCount)
            {
                strategy = "AliveSlotExpansion";
                normalized = ExpandAliveSlotActions(team, incoming, teamIndex, activeSlotCount);
            }
            else
            {
                strategy = incoming.Count < activeSlotCount ? "PassPadding" : "SlotTruncation";
                normalized = PadOrTrimActions(incoming, teamIndex, activeSlotCount);
            }

            string message =
                $"Action queue normalization applied for team {teamIndex}: " +
                $"received {incoming.Count}, active slots {activeSlotCount}, alive active slots {aliveActiveCount}, strategy {strategy}.";
            _log.LogMessage(CombatEventType.ValidationError, message, state != null ? state.currentTurn : -1);
            Debug.LogWarning($"[CombatResolver] {message}");
            return normalized;
        }

        private static int CountAliveActiveSlots(TeamState team)
        {
            if (team == null || team.activeIndices == null)
                return 0;

            int count = 0;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var actor = team.GetActiveInSlot(slot);
                if (actor != null && actor.isAlive)
                    count++;
            }

            return count;
        }

        private static List<CombatAction> ExpandAliveSlotActions(TeamState team, List<CombatAction> incoming, int teamIndex, int activeSlotCount)
        {
            var normalized = new List<CombatAction>(activeSlotCount);
            int sourceIndex = 0;

            for (int slot = 0; slot < activeSlotCount; slot++)
            {
                var actor = team.GetActiveInSlot(slot);
                if (actor != null && actor.isAlive && sourceIndex < incoming.Count)
                    normalized.Add(incoming[sourceIndex++] ?? CombatAction.Pass(teamIndex));
                else
                    normalized.Add(CombatAction.Pass(teamIndex));
            }

            return normalized;
        }

        private static List<CombatAction> PadOrTrimActions(List<CombatAction> incoming, int teamIndex, int activeSlotCount)
        {
            var normalized = new List<CombatAction>(activeSlotCount);
            for (int slot = 0; slot < activeSlotCount; slot++)
            {
                if (slot < incoming.Count)
                    normalized.Add(incoming[slot] ?? CombatAction.Pass(teamIndex));
                else
                    normalized.Add(CombatAction.Pass(teamIndex));
            }

            return normalized;
        }

        private static void EnforceSingleCapture(List<CombatAction> actions)
        {
            if (actions == null)
                return;

            bool seen = false;
            for (int i = 0; i < actions.Count; i++)
            {
                CombatAction action = actions[i];
                if (action == null || action.actionType != CombatActionType.Capture)
                    continue;
                if (!seen)
                {
                    seen = true;
                    continue;
                }

                actions[i] = CombatAction.Pass(action.teamIndex);
            }
        }

        public void BeginTurn(CombatState state)
        {
            if (!state.isActive) return;
            _currentState = state;

            state.currentTurn++;
            state.currentPhase = TurnPhase.TurnStart;
            _stepActionCursor = 0;
            BeginLedgerTurn(state);
            _log.SetForensicContext(state.currentTurn, -1);
            _log.SyncEntityRefs(state);
            _log.LogMessage(CombatEventType.TurnStart, $"=== Turn {state.currentTurn} ===", state.currentTurn);

            ProcessStartOfTurnForTeam(state.team0, state.currentTurn);
            ProcessStartOfTurnForTeam(state.team1, state.currentTurn);
            ProcessStartOfTurnTriggersForTeam(state.team0, state.currentTurn);
            ProcessStartOfTurnTriggersForTeam(state.team1, state.currentTurn);

            state.currentPhase = TurnPhase.ActionSelection;
        }

        public List<CombatActionStep> BuildActionQueueForCurrentTurn(CombatState state,
            List<CombatAction> actions0, List<CombatAction> actions1)
        {
            if (!state.isActive) return new List<CombatActionStep>();

            state.currentPhase = TurnPhase.ActionResolution;
            var queue = BuildActionQueue(state, actions0, actions1);
            _stepActionCursor = 0;
            return ConvertQueue(queue);
        }

        public bool ExecuteActionStep(CombatState state, CombatActionStep step)
        {
            if (!state.isActive) return false;
            _currentState = state;

            if (step.actor == null || !step.actor.isAlive)
            {
                if (step.action != null && step.action.actionType == CombatActionType.Capture)
                {
                    _log.LogMessage(
                        CombatEventType.ActionFailed,
                        "Capture cancelled — recorder is not eligible to act.",
                        state.currentTurn);
                }
                return state.isActive;
            }

            int actionIndex = _stepActionCursor >= 0 ? _stepActionCursor : 0;
            _log.SetForensicContext(state.currentTurn, actionIndex);
            _log.SyncEntityRefs(state);
            _log.LogForensic(
                CombatForensicEventType.ActorSelected,
                new ActorSelectedPayload
                {
                    reason = "StepwiseExecution",
                    queueIndex = actionIndex,
                },
                step.actor.instanceId,
                null,
                state.currentTurn,
                actionIndex);

            ResolveAction(state, step.actingTeam, step.opposingTeam, step.action, step.actor, step.hasPriority);
            _stepActionCursor = actionIndex + 1;

            ProcessAutoSwaps(state, state.currentTurn);
            if (CheckBattleEnd(state))
            {
                LogTurnStateDigest(state);
                return false;
            }

            return state.isActive;
        }

        public bool EndTurn(CombatState state)
        {
            if (!state.isActive) return false;
            _currentState = state;

            state.currentPhase = TurnPhase.EndOfTurn;
            state.pendingCaptureAttempt = null;
            state.ResetSwapReservations();
            EndLedgerTurn(state);
            _log.SetForensicContext(state.currentTurn, -1);

            var turnTicks = new List<TurnEndStatusTickEntry>(8);
            ProcessEndOfTurnForTeam(state.team0, state.currentTurn, turnTicks);
            ProcessEndOfTurnForTeam(state.team1, state.currentTurn, turnTicks);
            TickFieldEntryEffects(state);
            LogTurnEndForensics(state, turnTicks);

            ProcessAutoSwaps(state, state.currentTurn);
            if (CheckBattleEnd(state))
            {
                LogTurnStateDigest(state);
                return false;
            }

            LogTurnStateDigest(state);
            _log.LogMessage(CombatEventType.TurnEnd, $"=== End Turn {state.currentTurn} ===", state.currentTurn);
            return true;
        }

        private static List<CombatActionStep> ConvertQueue(List<ActionQueueEntry> queue)
        {
            var result = new List<CombatActionStep>(queue.Count);
            for (int i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                result.Add(new CombatActionStep
                {
                    actor = entry.actor,
                    action = entry.action,
                    actingTeam = entry.actingTeam,
                    opposingTeam = entry.opposingTeam,
                    speed = entry.speed,
                    hasPriority = entry.hasPriority,
                });
            }
            return result;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Auto-Swap on KO
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Check every active slot on both teams. If any active Chainling has fainted
        /// and there is a bench Chainling available, auto-swap it in.
        /// </summary>
        private void ProcessAutoSwaps(CombatState state, int turn)
        {
            AutoSwapForTeam(state, state.team0, turn);
            AutoSwapForTeam(state, state.team1, turn);
        }

        private void AutoSwapForTeam(CombatState state, TeamState team, int turn)
        {
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0) continue; // Slot already marked empty (previously handled KO)
                if (idx >= team.chainlings.Count) continue;

                var active = team.chainlings[idx];
                if (active.isAlive) continue; // Still alive, no swap needed

                // This active Chainling just fainted â€” try to swap from bench
                var swappedIn = team.AutoSwapFromBench(slot);
                if (swappedIn != null)
                {
                    ResetChemistrySwapState(active, turn);
                    active.ClearArmedUtilityWindows();
                    bool eligibleNextTurn = swappedIn.isAlive;
                    _log.LogForensic(
                        CombatForensicEventType.SwapResolved,
                        new SwapResolvedPayload
                        {
                            teamName = team.teamName,
                            outgoingDisplayName = _log.Name(active.instanceId),
                            incomingDisplayName = _log.Name(swappedIn.instanceId),
                            outgoingRef = new CombatEntityRef { teamId = team.teamIndex, slotId = slot, instanceId = active.instanceId },
                            incomingRef = new CombatEntityRef { teamId = team.teamIndex, slotId = slot, instanceId = swappedIn.instanceId },
                            swapType = "forced",
                            incomingCanActThisTurn = false,
                            incomingEligibleNextTurn = eligibleNextTurn,
                            note = eligibleNextTurn ? string.Empty : "Incoming chainling is not eligible next turn.",
                        },
                        active.instanceId,
                        new List<string> { swappedIn.instanceId },
                        turn);
                    _log.Log(new CombatEvent
                    {
                        eventType = CombatEventType.AutoSwapIn,
                        turn = turn,
                        sourceId = active.instanceId,
                        targetId = swappedIn.instanceId,
                        intValue = slot,
                        intValue2 = team.teamIndex,
                        message = $"{team.teamName}: {active.displayName} fainted! " +
                                  $"{swappedIn.displayName} auto-swapped in from bench (slot {slot})",
                    });

                    SwapModifierResolver.ProcessSwapModifiers(
                        state: state,
                        team: team,
                        outgoing: active,
                        incoming: swappedIn,
                        turn: turn,
                        log: _log,
                        rng: _rng,
                        modifierCatalog: _modifierCatalog,
                        apService: _apService);
                    ApplyFieldEntryEffects(state, team, swappedIn, turn);
                }
                else
                {
                    // No bench available â€” mark slot as -1 so we don't spam this message
                    team.activeIndices[slot] = -1;
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{team.teamName}: {active.displayName} fainted â€” no bench available, slot {slot} empty", turn);
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Action Resolution
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ResolveAction(CombatState state, TeamState actingTeam, TeamState opposingTeam,
            CombatAction action, ChainlingInstance actor, bool hasPriority = false)
        {
            if (actor == null || !actor.isAlive) return;

            // Check skip conditions
            if (actor.lostActionThisTurn)
            {
                actor.lostActionThisTurn = false;
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} lost their action", state.currentTurn);
                CancelCaptureBecauseRecorderIneligible(state, action);
                return;
            }

            if (actor.skippingNextTurn)
            {
                actor.skippingNextTurn = false;
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} skipped their action", state.currentTurn);
                CancelCaptureBecauseRecorderIneligible(state, action);
                return;
            }

            // Check Rekt (lose action)
            if (HasStatus(actor, StatusId.Rekt))
            {
                float rektChance = _resolverConfig.rektLoseActionChance;
                float roll;
                bool triggered = _rng.Check(rektChance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = "Status.RektLoseAction",
                        chance = rektChance,
                        roll = roll,
                        triggered = triggered,
                    },
                    actor.instanceId,
                    new List<string> { actor.instanceId },
                    state.currentTurn);

                if (triggered)
                {
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{actor.displayName} is Rekt and lost their action!", state.currentTurn);
                    CancelCaptureBecauseRecorderIneligible(state, action);
                    return;
                }
            }

            // Check Glitched Mind (mis-act)
            if (HasStatus(actor, StatusId.GlitchedMind))
            {
                float glitchedChance = _resolverConfig.glitchedMindMisactChance;
                float roll;
                bool triggered = _rng.Check(glitchedChance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = "Status.GlitchedMindMisAct",
                        chance = glitchedChance,
                        roll = roll,
                        triggered = triggered,
                    },
                    actor.instanceId,
                    new List<string> { actor.instanceId },
                    state.currentTurn);

                if (triggered)
                {
                    action = BuildRandomLegalMisAct(state, actingTeam, actor);
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{actor.displayName} is Glitched and mis-acted as {action.actionType}!",
                        state.currentTurn);
                }
            }

            EnsureTurnActionLedger(state)?.RecordActorAction(actor.instanceId);

            switch (action.actionType)
            {
                case CombatActionType.UseMove:
                    ResolveMove(state, actingTeam, opposingTeam, action, actor, hasPriority);
                    break;

                case CombatActionType.Swap:
                    _log.LogForensic(
                        CombatForensicEventType.ActionChosen,
                        new ActionChosenPayload
                        {
                            actorDisplayName = _log.Name(actor.instanceId),
                            actionType = CombatActionType.Swap.ToString(),
                            moveId = string.Empty,
                            moveName = "Swap",
                            category = "Swap",
                            targetingMode = "Bench",
                            apCost = 0,
                            selectedTargetTeamId = actingTeam != null ? actingTeam.teamIndex : -1,
                            selectedTargetSlotId = action.swapTargetIndex,
                            selectedTargetId = string.Empty,
                        },
                        actor.instanceId,
                        null,
                        state.currentTurn);
                    ResolveSwap(state, actingTeam, action.swapTargetIndex, actor);
                    break;

                case CombatActionType.Pass:
                    _log.LogForensic(
                        CombatForensicEventType.ActionChosen,
                        new ActionChosenPayload
                        {
                            actorDisplayName = _log.Name(actor.instanceId),
                            actionType = CombatActionType.Pass.ToString(),
                            moveId = string.Empty,
                            moveName = "Pass",
                            category = "Pass",
                            targetingMode = "None",
                            apCost = 0,
                            selectedTargetTeamId = -1,
                            selectedTargetSlotId = -1,
                            selectedTargetId = string.Empty,
                        },
                        actor.instanceId,
                        null,
                        state.currentTurn);
                    _log.LogMessage(CombatEventType.ActionSelected,
                        $"{actor.displayName} passed", state.currentTurn);
                    break;

                case CombatActionType.Capture:
                    ResolveCapture(state, opposingTeam, action, actor);
                    break;
            }

            actor.hasActedThisTurn = true;
        }

        private void CancelCaptureBecauseRecorderIneligible(CombatState state, CombatAction action)
        {
            if (action == null || action.actionType != CombatActionType.Capture)
                return;

            if (state != null)
                state.pendingCaptureAttempt = null;

            _log.LogMessage(
                CombatEventType.ActionFailed,
                "Capture cancelled — recorder is not eligible to act.",
                state != null ? state.currentTurn : 0);
        }

        private void ResolveCapture(
            CombatState state,
            TeamState opposingTeam,
            CombatAction action,
            ChainlingInstance actor)
        {
            state.pendingCaptureAttempt = null;
            ChainlingInstance target = FindCaptureTarget(opposingTeam, action != null ? action.captureTargetInstanceId : null);
            if (target == null || !target.isAlive)
            {
                _log.LogMessage(
                    CombatEventType.ActionFailed,
                    $"{actor.displayName}'s capture was cancelled — the target is no longer eligible.",
                    state.currentTurn);
                return;
            }

            _log.LogForensic(
                CombatForensicEventType.ActionChosen,
                new ActionChosenPayload
                {
                    actorDisplayName = _log.Name(actor.instanceId),
                    actionType = CombatActionType.Capture.ToString(),
                    moveId = string.Empty,
                    moveName = "Capture",
                    category = "Capture",
                    targetingMode = "SingleEnemy",
                    apCost = 0,
                    selectedTargetTeamId = opposingTeam != null ? opposingTeam.teamIndex : 1,
                    selectedTargetSlotId = -1,
                    selectedTargetId = target.instanceId,
                },
                actor.instanceId,
                new List<string> { target.instanceId },
                state.currentTurn);
            _log.LogMessage(
                CombatEventType.ActionSelected,
                $"{actor.displayName} records {target.displayName} at end of turn",
                state.currentTurn);

            state.pendingCaptureAttempt = new PendingCaptureAttempt
            {
                actorInstanceId = actor.instanceId,
                targetInstanceId = target.instanceId,
                orbItemId = action != null ? action.captureOrbItemId ?? string.Empty : string.Empty,
                teamIndex = action != null ? action.teamIndex : 0,
            };
        }

        private static ChainlingInstance FindCaptureTarget(TeamState opposingTeam, string instanceId)
        {
            if (opposingTeam == null || opposingTeam.chainlings == null)
                return null;

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                for (int i = 0; i < opposingTeam.chainlings.Count; i++)
                {
                    ChainlingInstance candidate = opposingTeam.chainlings[i];
                    if (candidate != null &&
                        string.Equals(candidate.instanceId, instanceId, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            List<ChainlingInstance> actives = opposingTeam.GetActiveChainlings();
            if (actives != null && actives.Count == 1)
                return actives[0];
            return null;
        }

        private void ResolveMove(CombatState state, TeamState actingTeam,
            TeamState opposingTeam, CombatAction action, ChainlingInstance actor, bool hasPriority = false)
        {
            // Pick a target â€” default to the opposing team's lead active Chainling
            int moveIndex = action != null ? action.moveIndex : -1;

            if (moveIndex < 0 || moveIndex >= actor.moves.Count)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"Invalid move index {moveIndex} for {actor.displayName}", state.currentTurn);
                return;
            }

            var move = actor.moves[moveIndex];
            if (move == null)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"{actor.displayName} attempted to use a null move reference", state.currentTurn);
                return;
            }

            MoveSelectionEvaluation selection = _eligibilityService.EvaluateMoveSelection(actor, move, state);
            if (!selection.canSelect)
            {
                if (selection.reason == "InsufficientAP")
                {
                    LogNoApActionFailed(actor, move, state.currentTurn);
                    actor.hasActedThisTurn = true;
                    return;
                }

                _log.LogMessage(
                    CombatEventType.ValidationError,
                    $"{actor.displayName} cannot select {move.displayName}: {selection.reason}",
                    state.currentTurn);
                return;
            }
            bool conditionMet = selection.conditionMet;
            int effectiveApCost = _eligibilityService.GetEffectiveApCost(actor, move);

            bool hadExplicitTarget = action != null && action.targetTeamIndex >= 0 && action.targetSlotIndex >= 0;
            bool targetRetargeted;
            string retargetReason;
            var target = ResolveSingleSelectedTarget(actingTeam, opposingTeam, actor, move, action, out targetRetargeted, out retargetReason);
            string selectedTargetId = string.Empty;
            ChainlingInstance explicitSelectedTarget = null;
            if (hadExplicitTarget)
            {
                var selectedTeam = GetTeamByIndex(actingTeam, opposingTeam, action.targetTeamIndex);
                explicitSelectedTarget = selectedTeam != null ? selectedTeam.GetActiveInSlot(action.targetSlotIndex) : null;
                selectedTargetId = explicitSelectedTarget != null ? explicitSelectedTarget.instanceId : string.Empty;
            }

            _log.LogForensic(
                CombatForensicEventType.ActionChosen,
                new ActionChosenPayload
                {
                    actorDisplayName = _log.Name(actor.instanceId),
                    actionType = CombatActionType.UseMove.ToString(),
                    moveId = move.instanceId,
                    moveName = move.displayName,
                    category = move.category.ToString(),
                    targetingMode = move.targeting.ToString(),
                    apCost = effectiveApCost,
                    selectedTargetTeamId = action != null ? action.targetTeamIndex : -1,
                    selectedTargetSlotId = action != null ? action.targetSlotIndex : -1,
                    selectedTargetId = selectedTargetId,
                },
                actor.instanceId,
                target != null ? new List<string> { target.instanceId } : null,
                state.currentTurn);

            _log.LogTargeting(
                state.currentTurn,
                CombatEventType.TargetingPhaseStarted,
                actor.instanceId,
                string.Empty,
                move.instanceId,
                $"{actor.displayName} started targeting for {move.displayName} ({move.targeting}).");

            if (hadExplicitTarget)
            {
                _log.LogTargeting(
                    state.currentTurn,
                    CombatEventType.TargetsSelected,
                    actor.instanceId,
                    target != null ? target.instanceId : string.Empty,
                    move.instanceId,
                    $"{actor.displayName} selected target slot [{action.targetTeamIndex}:{action.targetSlotIndex}] for {move.displayName}.");
            }

            if (targetRetargeted)
            {
                _log.LogTargeting(
                    state.currentTurn,
                    CombatEventType.TargetRetargeted,
                    actor.instanceId,
                    target != null ? target.instanceId : string.Empty,
                    move.instanceId,
                    $"{actor.displayName}'s target was retargeted to a valid alive target.");
                _log.LogForensic(
                    CombatForensicEventType.Retargeted,
                    new TargetsResolvedPayload
                    {
                        retargeted = true,
                        retargetReason = retargetReason,
                    },
                    actor.instanceId,
                    target != null ? new List<string> { target.instanceId } : null,
                    state.currentTurn);
            }

            int surchargeBeforeSpend = Math.Max(0, actor.nextMoveApSurcharge);
            if (!_apService.TrySpendCost(actor, effectiveApCost, state.currentTurn, "MoveCost", out int paidCost))
            {
                LogNoApActionFailed(actor, move, state.currentTurn);
                actor.hasActedThisTurn = true;
                return;
            }

            if (surchargeBeforeSpend > 0)
            {
                actor.nextMoveApSurcharge = 0;
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{actor.displayName}'s AP surcharge was consumed.",
                    state.currentTurn);
            }

            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.MoveUsed,
                turn = state.currentTurn,
                sourceId = actor.instanceId,
                targetId = target?.instanceId ?? "",
                moveId = move.instanceId,
                boolValue = hasPriority,
                message = hasPriority
                    ? $"{actor.displayName} struck first with priority {move.displayName}" +
                      (target != null ? $" → {target.displayName}" : "")
                    : $"{actor.displayName} used {move.displayName}" +
                      (target != null ? $" → {target.displayName}" : ""),
            });

            ProcessLowHpModifierWindows(actor, move, state.currentTurn);

            // Branch by category
            if (move.category == MoveCategory.DamagingMelee ||
                move.category == MoveCategory.DamagingRanged)
            {
                var resolvedTargets = BuildResolvedTargetsForMove(actingTeam, opposingTeam, actor, move, target);
                List<ChainlingInstance> selectedTargets = null;
                if (hadExplicitTarget)
                {
                    selectedTargets = new List<ChainlingInstance>(1);
                    if (explicitSelectedTarget != null)
                        selectedTargets.Add(explicitSelectedTarget);
                }
                LogResolvedTargets(state.currentTurn, actor, move, resolvedTargets, selectedTargets, targetRetargeted, retargetReason);
                ResolveDamagingMove(state, actingTeam, opposingTeam, actor, target, move);
                EnsureTurnActionLedger(state)?.RecordDamagingMove(
                    actingTeam != null ? actingTeam.teamIndex : -1,
                    actor.instanceId);
            }
            else if (move.category == MoveCategory.Passive)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"Passive move {move.displayName} cannot be used as an action",
                    state.currentTurn);
            }
            else
            {
                ResolveUtilityMove(state, actingTeam, opposingTeam, actor, move, action, target, conditionMet, paidCost);
            }

            CheckPassiveTriggers(actor, state.currentTurn, PassiveTriggerPoint.OnAction, target, 0, move);

            // TriggerOnce is also valid for authored active/utility moves (for example,
            // an armed one-shot dodge). Record successful use in the existing battle set
            // so eligibility can reject a second cast without a move-id special case.
            if (move.category != MoveCategory.Passive && move.triggerOncePerBattle && actor.triggeredPassiveIds != null)
                actor.triggeredPassiveIds.Add(move.instanceId);

            ProcessActionEconomyModifiers(state, actingTeam, opposingTeam, actor, target, move, conditionMet);

            // Apply recharge if needed. The granting turn's end tick consumes one
            // count, so the log reports the upcoming turns the move cannot be selected.
            if (move.rechargeTurns > 0)
            {
                var rechargeStatus = new StatusInstance(
                    StatusId.Recharging, StatusClass.Consumed, StatusOrigin.General,
                    move.rechargeTurns, move.instanceId);
                rechargeStatus.rechargingMoveId = move.instanceId;
                actor.statuses.Add(rechargeStatus);
                _log.LogMessage(CombatEventType.MoveRecharging,
                    FormatPlayerFacingRecharge(move.displayName, move.rechargeTurns),
                    state.currentTurn);
            }

            // Tier-aware modifier gate: only non-Known moves get AP refund check
            if (move.tier != MoveTier.Known)
                CheckAPRefund(actor, move, state.currentTurn);
            actor.hasActedThisTurn = true;
        }

        private void LogNoApActionFailed(ChainlingInstance actor, MoveInstance move, int turn)
        {
            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.ActionFailed,
                turn = turn,
                sourceId = actor != null ? actor.instanceId : string.Empty,
                moveId = move != null ? move.instanceId : string.Empty,
                message = actor != null && move != null
                    ? $"{actor.displayName} could not use {move.displayName}: NO AP"
                    : "Action failed: NO AP",
            });

            _log.LogForensic(
                CombatForensicEventType.ActionChosen,
                new ActionChosenPayload
                {
                    actorDisplayName = actor != null ? _log.Name(actor.instanceId) : string.Empty,
                    actionType = CombatActionType.UseMove.ToString(),
                    moveId = move != null ? move.instanceId : string.Empty,
                    moveName = move != null ? move.displayName : string.Empty,
                    category = move != null ? move.category.ToString() : string.Empty,
                    targetingMode = move != null ? move.targeting.ToString() : string.Empty,
                    apCost = _eligibilityService.GetEffectiveApCost(actor, move),
                    selectedTargetTeamId = -1,
                    selectedTargetSlotId = -1,
                    selectedTargetId = string.Empty,
                },
                actor != null ? actor.instanceId : null,
                actor != null ? new List<string> { actor.instanceId } : null,
                turn);
        }

        private void ResolveDamagingMove(CombatState state, TeamState actingTeam,
            TeamState opposingTeam, ChainlingInstance attacker, ChainlingInstance defender,
            MoveInstance move)
        {
            DamageModifierContext modifierContext = BuildDamageModifierContext(state, actingTeam, attacker, defender, move, state.currentTurn);
            MoveResolveProfile resolveProfile = MoveResolveProfileUtility.Resolve(move);
            MoveHitCountTuning hitTuning = resolveProfile.hitCount ?? new MoveHitCountTuning();

            // Determine hit count
            int minHits = Math.Max(1, move.hitCount);
            int maxHits = Math.Max(minHits, move.hitCountMax);
            int hitCount = minHits;
            if (hitTuning.forceMaximumHits)
            {
                hitCount = maxHits;
            }
            else if (hitTuning.forceMinimumHits)
            {
                hitCount = minHits;
            }
            else if (maxHits > minHits)
            {
                float multiHitRoll;
                hitCount = _rng.NextInt(minHits, maxHits, out multiHitRoll);
                _log.LogForensic(
                    CombatForensicEventType.MultiHitRolled,
                    new MultiHitRolledPayload
                    {
                        minHits = minHits,
                        maxHits = maxHits,
                        rolledHits = hitCount,
                        capped = hitCount >= maxHits,
                        roll = multiHitRoll,
                    },
                    attacker.instanceId,
                    null,
                    state.currentTurn);
            }
            if (hitTuning.flatHitBonus != 0)
                hitCount += hitTuning.flatHitBonus;
            if (modifierContext != null && modifierContext.extraHits > 0)
                hitCount += modifierContext.extraHits;
            if (hitTuning.maxTotalHitsCap > 0)
                hitCount = Math.Min(hitCount, hitTuning.maxTotalHitsCap);
            hitCount = Math.Max(1, hitCount);

            bool isMultiHit = hitCount > 1;
            if (isMultiHit)
            {
                _log.LogMessage(CombatEventType.MultiHitStart,
                    $"{move.displayName}: {hitCount} hits", state.currentTurn);
            }

            ChainlingType attackType = DamageCalculator.ResolveAttackType(attacker, move, modifierContext);

            // Check for Broadcast/Bridged â€” hit all foes
            bool anchored = HasStatus(attacker, StatusId.Anchored);
            bool hasSplinter = HasStatus(attacker, StatusId.Splinter);
            bool hitAllFoes = !anchored && (HasStatus(attacker, StatusId.Broadcast) || hasSplinter);
            bool moveHitsAllEnemies = move.targeting == MoveTargeting.AllEnemies || move.targeting == MoveTargeting.Field;
            bool modifierExpandsTargets = modifierContext != null && modifierContext.expandSingleTargetToAllEnemies;
            List<ChainlingInstance> targets;

            // Anchored suppresses the next spread-capable damaging move and is
            // consumed by the next damaging move even when that move was already
            // single-target.
            if (anchored)
                ConsumeStatus(attacker, StatusId.Anchored);

            if (anchored && (moveHitsAllEnemies || modifierExpandsTargets))
            {
                ChainlingInstance anchoredTarget = defender != null && defender.isAlive
                    ? defender
                    : GetFirstAliveActive(opposingTeam);
                targets = anchoredTarget != null
                    ? new List<ChainlingInstance> { anchoredTarget }
                    : new List<ChainlingInstance>();
            }
            else if (!anchored && (hitAllFoes || moveHitsAllEnemies || modifierExpandsTargets))
            {
                targets = opposingTeam.GetActiveChainlings();
                if (hitAllFoes)
                {
                    ConsumeStatus(attacker, StatusId.Broadcast);
                    ConsumeStatus(attacker, StatusId.Splinter);
                }
            }
            else
            {
                if (defender == null || !defender.isAlive)
                    return;

                // Damage redirect: if defender has DamageIntercept, reroute to interceptor
                if (HasStatus(defender, StatusId.DamageIntercept) &&
                    defender.redirectInterceptor != null &&
                    defender.redirectInterceptor.isAlive &&
                    defender.redirectInterceptor != defender)
                {
                    var interceptor = defender.redirectInterceptor;
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{defender.displayName}'s damage intercepted by {interceptor.displayName}!", state.currentTurn);
                    ConsumeStatus(defender, StatusId.DamageIntercept);
                    defender.redirectInterceptor = null;
                    targets = new List<ChainlingInstance> { interceptor };
                }
                else
                {
                    targets = new List<ChainlingInstance> { defender };
                }
            }

            if (hasSplinter && !anchored && targets.Count > 1)
            {
                if (modifierContext == null)
                    modifierContext = new DamageModifierContext();
                modifierContext.damageMultiplier *= _resolverConfig.splinterDamageMultiplier;
            }
            else if (hasSplinter && !anchored && targets.Count == 1)
            {
                if (modifierContext == null)
                    modifierContext = new DamageModifierContext();
                modifierContext.damageMultiplier *= _resolverConfig.splinterSingleTargetDamageMultiplier;
            }

            if (modifierContext != null
                && modifierContext.bounceToOneAdjacent
                && !modifierContext.expandSingleTargetToAllEnemies
                && targets.Count == 1)
            {
                ChainlingInstance bounceTarget = FindAdjacentActive(opposingTeam, targets[0]);
                if (bounceTarget != null)
                    targets.Add(bounceTarget);
            }

            bool attackerChemistryReactionApplied = false;
            var defenderChemistryReactionApplied = new HashSet<ChainlingInstance>();

            foreach (var target in targets)
            {
                if (!target.isAlive) continue;

                bool isBounceTarget = modifierContext != null
                    && modifierContext.bounceToOneAdjacent
                    && targets.Count > 1
                    && target != targets[0];
                int hitsThisTarget = isBounceTarget ? 1 : hitCount;
                float bouncePower = isBounceTarget
                    ? (modifierContext.bouncePowerMultiplier > 0f ? modifierContext.bouncePowerMultiplier : 0.50f)
                    : 1f;

                if (!isBounceTarget && resolveProfile.utilityExecutionTiming == MoveUtilityExecutionTiming.ResolveBeforeFirstHit)
                    ApplyDamagingMoveUtilityEffects(attacker, target, move, state.currentTurn);

                for (int hit = 0; hit < hitsThisTarget; hit++)
                {
                    if (!target.isAlive) break;

                    float accMult;
                    float evaMult;
                    float hitChance = DamageCalculator.ComputeHitChance(attacker, target, move, modifierContext, out accMult, out evaMult);
                    float hitRoll = -1f;
                    bool hits;
                    MoveAccuracyTuning accuracyTuning = resolveProfile.accuracy ?? new MoveAccuracyTuning();
                    if (accuracyTuning.forceMiss)
                    {
                        hits = false;
                    }
                    else if (accuracyTuning.forceHit || move.baseAccuracy <= 0)
                    {
                        hits = true;
                    }
                    else
                    {
                        hitRoll = _rng.NextFloat();
                        hits = hitRoll < hitChance;
                    }

                    bool armedDodge = false;
                    if (hits && ProcessArmedWindows(
                            target,
                            opposingTeam,
                            attacker,
                            ArmedWindowTrigger.IncomingHit,
                            state.currentTurn))
                    {
                        hits = false;
                        armedDodge = true;
                    }

                    _log.LogForensic(
                        CombatForensicEventType.HitCheck,
                        new HitCheckPayload
                        {
                            attackerDisplayName = _log.Name(attacker.instanceId),
                            targetDisplayName = _log.Name(target.instanceId),
                            moveId = move.instanceId,
                            moveName = move.displayName,
                            hitIndex = hit + 1,
                            totalHits = hitCount,
                            hitChance = hitChance,
                            roll = hitRoll,
                            didHit = hits,
                            missReason = hits ? string.Empty : armedDodge ? "ArmedDodge" : "AccuracyCheckFailed",
                            attackerAccuracyStage = attacker.stageAccuracy,
                            defenderEvasionStage = target.stageEvasion,
                            attackerAccuracyMultiplier = accMult,
                            defenderEvasionMultiplier = evaMult,
                        },
                        attacker.instanceId,
                        new List<string> { target.instanceId },
                        state.currentTurn);

                    if (!hits)
                    {
                        EnsureTurnActionLedger(state)?.RecordMiss(attacker.instanceId);
                        bool isDodge = armedDodge || evaMult > accMult;
                        if (isDodge)
                            _log.LogDodge(state.currentTurn, attacker.instanceId, target.instanceId, move.instanceId);
                        else
                            _log.LogMiss(state.currentTurn, attacker.instanceId, target.instanceId, move.instanceId);
                        if (ShouldApplyMoveEffectsAtResolve(attacker, target, move, state.currentTurn, logSkipped: false))
                        {
                            if (resolveProfile.applyStatusEffectsOnMiss)
                                ApplyMoveStatuses(attacker, target, move, state.currentTurn);
                            if (resolveProfile.applyStageChangesOnMiss)
                                ApplyMoveStageChanges(attacker, target, move, state.currentTurn);
                        }
                        CheckPassiveTriggers(attacker, state.currentTurn, PassiveTriggerPoint.OnMiss, target, 0, move);
                        CheckPassiveTriggers(target, state.currentTurn, PassiveTriggerPoint.OnMiss, attacker, 0, move);
                        ProcessArmedWindowsForTeam(
                            opposingTeam,
                            attacker,
                            ArmedWindowTrigger.EnemyMiss,
                            state.currentTurn);
                        if (!attacker.isAlive)
                            return;
                        continue;
                    }

                    EnsureTurnActionLedger(state)?.RecordHit(attacker.instanceId, target.instanceId);

                    ChemistryResolvedPayload chemistryPayload;
                    ChemistryResult chemResult = _chemistry.Resolve(
                        attackType, attacker, target, state.currentTurn, _rng,
                        CombatLogSettings.EnableForensicLogging,
                        out chemistryPayload,
                        allowAttackerReaction: !attackerChemistryReactionApplied,
                        allowDefenderReaction: !defenderChemistryReactionApplied.Contains(target));
                    if (chemResult.attackerReactionApplied)
                        attackerChemistryReactionApplied = true;
                    if (chemResult.defenderReactionApplied)
                        defenderChemistryReactionApplied.Add(target);
                    if (chemistryPayload != null)
                    {
                        chemistryPayload.chemAdv = chemResult.chemAdvCount;
                        chemistryPayload.chemDis = chemResult.chemDisCount;
                        chemistryPayload.exponent = chemResult.NetChemistry;
                        chemistryPayload.chemistryMultiplier = chemResult.DamageMultiplier;
                        chemistryPayload.moveId = move.instanceId;
                        chemistryPayload.moveName = move.displayName;
                        _log.LogForensic(
                            CombatForensicEventType.ChemistryResolved,
                            chemistryPayload,
                            attacker.instanceId,
                            new List<string> { target.instanceId },
                            state.currentTurn);
                    }

                    ApplyChemistryAwareModifierBonuses(modifierContext, attacker, target, move, chemResult, state.currentTurn);
                    float critRate = DamageCalculator.ComputeCritRate(attacker, move, modifierContext);
                    float critRoll = _rng.NextFloat();
                    bool isCrit = critRoll < critRate;

                    DamageCalculator.ResolveDamageStats(move, modifierContext, out CoreStat atkStat, out CoreStat defStat);
                    StatBreakdown atkBreakdown = attacker.GetStatBreakdown(atkStat);
                    StatBreakdown defBreakdown = target.GetStatBreakdown(defStat);
                    int hpBefore = target.currentHP;
                    DamageModifierContext hitModifierContext = modifierContext != null
                        ? modifierContext.Clone()
                        : new DamageModifierContext();
                    if (isBounceTarget)
                        hitModifierContext.powerMultiplier *= bouncePower;
                    bool firstIncomingHit = target.hitsTakenThisTurn == 0;
                    ApplyDefensivePassiveMitigation(hitModifierContext, target, move, state.currentTurn);
                    hitModifierContext.damageMultiplier *= ResolveConditionalDamageMultiplier(
                        state,
                        actingTeam,
                        opposingTeam,
                        attacker,
                        target,
                        move,
                        hit);
                    int damage = DamageCalculator.CalculateDamageDetailed(
                        attacker, target, move, chemResult, isCrit, _rng, hitModifierContext, _resolverConfig, out DamageComputationTrace trace);
                    target.hitsTakenThisTurn++;
                    if (firstIncomingHit)
                        TryImmutableLedgerAp(target, state.currentTurn);

                    // Barrier absorption — check bypass conditions first
                    int incomingDamage = damage;
                    bool wallActiveBeforeHit = CombatWallStateUtility.HasActiveWallState(target);
                    string wallSourceBeforeHit = CombatWallStateUtility.ResolveLatestWallSourceTag(target);
                    int activeBarrierCountBeforeHit = CombatWallStateUtility.CountActiveBarriers(target);
                    BarrierBypassResult bypassResult = CheckBarrierBypass(attacker, move, chemResult, hit, modifierContext, state.currentTurn);
                    bool bypassBarrier = bypassResult.bypassed;
                    int penetrationDamage = 0;
                    if (!bypassBarrier && modifierContext != null && modifierContext.barrierPenetrationPercent > 0f)
                        penetrationDamage = Math.Max(0, (int)Math.Floor(incomingDamage * modifierContext.barrierPenetrationPercent));
                    float wallReflectPercent = CombatWallEffectUtility.ResolveWallReflectPercent(target, wallSourceBeforeHit);
                    int reflectedDamage;
                    int barrierApDrain;
                    damage = target.ApplyBarrierAbsorption(damage, bypassBarrier, out reflectedDamage, out barrierApDrain);
                    reflectedDamage = ApplyStickyMirrorReflectBoost(target, reflectedDamage);
                    if (!bypassBarrier && damage == 0 && penetrationDamage > 0)
                        damage = Math.Min(incomingDamage, penetrationDamage);

                    string payloadWallSourceTag = ResolvePayloadWallSourceTag(wallSourceBeforeHit, wallSourceBeforeHit);
                    int attackerApDrained = ResolveWallStrikeApDrain(
                        target,
                        attacker,
                        state.currentTurn,
                        wallActiveBeforeHit && !bypassBarrier,
                        barrierApDrain,
                        payloadWallSourceTag);

                    if (incomingDamage > 0 && damage == 0)
                    {
                        bool wallActiveAfterBlock = CombatWallStateUtility.HasActiveWallState(target);
                        int activeBarrierCountAfterBlock = CombatWallStateUtility.CountActiveBarriers(target);
                        string wallSourceAfterBlock = CombatWallStateUtility.ResolveLatestWallSourceTag(target);
                        payloadWallSourceTag = ResolvePayloadWallSourceTag(wallSourceBeforeHit, wallSourceAfterBlock);
                        var blockedPayload = CreateWallInterceptPayload(
                            hit,
                            hitCount,
                            WallInterceptMode.WallBlocked,
                            wallActiveBeforeHit,
                            wallActiveAfterBlock,
                            bypassResult,
                            move,
                            trace.mitigation,
                            incomingDamage,
                            0,
                            barrierFullyBlocked: true,
                            mitigationSourcesExpired: 0,
                            barriersExpired: Math.Max(0, activeBarrierCountBeforeHit - activeBarrierCountAfterBlock),
                            wallExpiredThisStep: wallActiveBeforeHit && !wallActiveAfterBlock,
                            partialPenetration: false,
                            payloadWallSourceTag,
                            reflectedDamage,
                            attackerApDrained,
                            wallReflectPercent,
                            trace.preMitigationDamage);
                        _log.LogBarrierBlocked(state.currentTurn, attacker.instanceId, target.instanceId, move.instanceId, blockedPayload.ToJson());
                        int attackerHpBeforeReflect = attacker.currentHP;
                        bool attackerKoByReflect = false;
                        if (reflectedDamage > 0 && attacker.isAlive)
                        {
                            attacker.currentHP = Math.Max(0, attacker.currentHP - reflectedDamage);
                            attackerKoByReflect = attacker.currentHP <= 0;
                            _log.LogMessage(CombatEventType.SystemMessage,
                                $"{attacker.displayName} took {reflectedDamage} reflected barrier damage", state.currentTurn);
                        }
                        if (resolveProfile.utilityExecutionTiming == MoveUtilityExecutionTiming.AfterEachConfirmedHit)
                            ApplyDamagingMoveUtilityEffects(attacker, target, move, state.currentTurn, 0);
                        if (attackerKoByReflect)
                        {
                            HandleReflectedAttackerKo(
                                state, actingTeam, opposingTeam, attacker, target, move, attackerHpBeforeReflect);
                            break;
                        }
                        continue;
                    }

                    int attackerHpBeforePartialReflect = attacker.currentHP;
                    bool attackerKoByPartialReflect = false;
                    if (reflectedDamage > 0 && attacker.isAlive)
                    {
                        attacker.currentHP = Math.Max(0, attacker.currentHP - reflectedDamage);
                        attackerKoByPartialReflect = attacker.currentHP <= 0;
                        _log.LogMessage(CombatEventType.SystemMessage,
                            $"{attacker.displayName} took {reflectedDamage} reflected barrier damage", state.currentTurn);
                    }

                    target.currentHP -= damage;
                    if (target.currentHP < 0) target.currentHP = 0;
                    int hpAfter = target.currentHP;

                    int expectedDelta = Math.Min(damage, hpBefore);
                    int actualDelta = hpBefore - hpAfter;
                    bool mismatch = expectedDelta != actualDelta;
                    EnsureTurnActionLedger(state)?.RecordHpDamage(target.instanceId, actualDelta);

                    int expiredMitigationByHit = target.ConsumeMitigationHitBudgets(move.contactType);
                    if (expiredMitigationByHit > 0)
                    {
                        _log.LogMessage(
                            CombatEventType.SystemMessage,
                            $"{target.displayName} mitigation windows expired from hit budget ({expiredMitigationByHit}).",
                            state.currentTurn);
                    }

                    bool wallActiveAfterHit = CombatWallStateUtility.HasActiveWallState(target);
                    int activeBarrierCountAfterHit = CombatWallStateUtility.CountActiveBarriers(target);
                    string wallSourceAfterHit = CombatWallStateUtility.ResolveLatestWallSourceTag(target);
                    payloadWallSourceTag = ResolvePayloadWallSourceTag(wallSourceBeforeHit, wallSourceAfterHit);
                    var wallPayload = CreateWallInterceptPayload(
                        hit,
                        hitCount,
                        ClassifyWallInterceptMode(
                            wallActiveBeforeHit,
                            wallActiveAfterHit,
                            bypassResult.bypassed,
                            incomingDamage,
                            damage),
                        wallActiveBeforeHit,
                        wallActiveAfterHit,
                        bypassResult,
                        move,
                        trace.mitigation,
                        incomingDamage,
                        damage,
                        barrierFullyBlocked: false,
                        mitigationSourcesExpired: expiredMitigationByHit,
                        barriersExpired: Math.Max(0, activeBarrierCountBeforeHit - activeBarrierCountAfterHit),
                        wallExpiredThisStep: wallActiveBeforeHit && !wallActiveAfterHit,
                        partialPenetration: !bypassBarrier && penetrationDamage > 0 && damage > 0,
                        payloadWallSourceTag,
                        reflectedDamage,
                        attackerApDrained,
                        wallReflectPercent,
                        trace.preMitigationDamage);

                    int reactiveApGain = target.currentHP > 0
                        ? target.GrantReactiveApOnDamage(attacker, apply: false)
                        : 0;
                    if (reactiveApGain > 0)
                    {
                        _apService.Gain(target, reactiveApGain, state.currentTurn, "ReactiveAPOnDamage");
                    }

                    var damagePayload = new DamageTracePayload
                    {
                        attackerId = attacker.instanceId,
                        defenderId = target.instanceId,
                        moveId = move.instanceId,
                        atkStatUsed = trace.atkStatUsed,
                        defStatUsed = trace.defStatUsed,
                        atkBreakdown = atkBreakdown,
                        defBreakdown = defBreakdown,
                        atkDefRatio = trace.atkDefRatio,
                        basePower = trace.basePower,
                        randomFactor = trace.randomFactor,
                        stab = trace.stab,
                        chemistry = trace.chemistry,
                        crit = trace.crit,
                        mitigation = trace.mitigation,
                        mitigationFloor = trace.mitigationFloor,
                        tuningMultiplier = trace.tuningMultiplier,
                        finalDamage = trace.finalDamage,
                        hpBefore = hpBefore,
                        hpAfter = hpAfter,
                        expectedDelta = expectedDelta,
                        actualDelta = actualDelta,
                        damageApplicationMismatch = mismatch,
                        mismatchReason = mismatch ? "HP delta mismatch after clamp/mitigation application." : ""
                    };

                    _log.LogDamage(state.currentTurn, attacker.instanceId, target.instanceId,
                        move.instanceId, damage, isCrit, chemResult.DamageMultiplier,
                        hpAfter, target.EffectiveMaxHP, wallPayload.ToJson());
                    _log.LogDetailedEvent(
                        CombatEventType.StatBreakdown,
                        state.currentTurn,
                        $"{attacker.displayName} vs {target.displayName}: {atkStat} {atkBreakdown.finalInt} / {defStat} {defBreakdown.finalInt} = {trace.atkDefRatio:F2}",
                        JsonUtility.ToJson(damagePayload),
                        attacker.instanceId,
                        target.instanceId,
                        move.instanceId);

                    string damageType = move.category == MoveCategory.DamagingMelee ? "melee" : "ranged";
                    _log.LogForensic(
                        CombatForensicEventType.DamageResolved,
                        new DamageResolvedPayload
                        {
                            attackerDisplayName = _log.Name(attacker.instanceId),
                            targetDisplayName = _log.Name(target.instanceId),
                            moveId = move.instanceId,
                            moveName = move.displayName,
                            hitIndex = hit + 1,
                            totalHits = hitCount,
                            hpBefore = hpBefore,
                            hpAfter = hpAfter,
                            maxHp = target.EffectiveMaxHP,
                            packet = new DamagePacketPayload
                            {
                                movePower = trace.basePower,
                                damageType = damageType,
                                attackerStatUsed = CombatLogTypeNames.GetStatName(atkStat),
                                defenderStatUsed = CombatLogTypeNames.GetStatName(defStat),
                                attackerAtkFinal = atkBreakdown.finalInt,
                                defenderDefFinal = defBreakdown.finalInt,
                                levelFactor = trace.levelFactor,
                                randomMultiplier = trace.randomFactor,
                                stabMultiplier = trace.stab,
                                chemistryAdv = chemResult.chemAdvCount,
                                chemistryDis = chemResult.chemDisCount,
                                chemistryExponent = chemResult.NetChemistry,
                                chemistryMultiplier = chemResult.DamageMultiplier,
                                critRate = critRate,
                                critRoll = critRoll,
                                critMultiplier = trace.crit,
                                wasCrit = isCrit,
                                finalDamage = damage,
                                clampReason = damage <= 0 ? "ClampedToZero" : string.Empty,
                            }
                        },
                        attacker.instanceId,
                        new List<string> { target.instanceId },
                        state.currentTurn);

                    EmitDamageContextModifierOutcomes(
                        modifierContext,
                        attacker,
                        target,
                        move,
                        state.currentTurn,
                        isCrit,
                        bypassBarrier,
                        penetrationDamage,
                        actualDelta);

                    bool shouldApplyMoveEffects = !isBounceTarget && ShouldApplyMoveEffectsAtResolve(attacker, target, move, state.currentTurn, logSkipped: false);
                    if (shouldApplyMoveEffects)
                    {
                        ApplyMoveStatuses(attacker, target, move, state.currentTurn);
                        ApplyMoveStageChanges(attacker, target, move, state.currentTurn);
                        if (resolveProfile.applyHealingOnDamagingMoves)
                            ApplyMoveHealing(attacker, target, move, state.currentTurn);
                    }
                    if (!isBounceTarget && resolveProfile.utilityExecutionTiming == MoveUtilityExecutionTiming.AfterEachConfirmedHit)
                        ApplyDamagingMoveUtilityEffects(attacker, target, move, state.currentTurn, actualDelta);
                    if (!attackerKoByPartialReflect)
                        ProcessAfterDamageModifiers(attacker, target, move, damage, isCrit, state.currentTurn);
                    WakeOnDamage(target, state.currentTurn);
                    ProcessRiposteModifiers(target, attacker, move, damage, state.currentTurn);

                    // Check for passive move triggers (e.g., HP threshold triggers)
                    CheckPassiveTriggers(target, state.currentTurn, PassiveTriggerPoint.AfterDamage, attacker, damage, move);
                    if (!attacker.isAlive)
                        break;
                    if (!attackerKoByPartialReflect)
                        CheckPassiveTriggers(attacker, state.currentTurn, PassiveTriggerPoint.OnHit, target, damage, move, chemResult.chemAdvCount > 0);
                    CheckPassiveTriggersForTeam(
                        actingTeam,
                        state.currentTurn,
                        PassiveTriggerPoint.OnAllyHit,
                        target,
                        move,
                        exclude: attacker);
                    if (isCrit && !attackerKoByPartialReflect)
                        CheckPassiveTriggers(attacker, state.currentTurn, PassiveTriggerPoint.OnCrit, target, damage, move);

                    // Siphoned heal
                    if (!attackerKoByPartialReflect && HasStatus(attacker, StatusId.Siphoned))
                    {
                        int heal = (int)(damage * 0.25f);
                        attacker.currentHP = Math.Min(attacker.EffectiveMaxHP, attacker.currentHP + heal);
                        LogHealApplied(state.currentTurn, attacker, attacker, move, heal, "Siphoned");
                    }

                    // KO check
                    if (target.currentHP <= 0)
                    {
                        target.isAlive = false;
                        EnsureTurnActionLedger(state)?.RecordKo(
                            target.instanceId,
                            opposingTeam != null ? opposingTeam.teamIndex : -1);
                        ProcessOnKOModifiers(attacker, move, target, state.currentTurn);
                        ProcessArmedWindowsForTeam(
                            actingTeam,
                            target,
                            ArmedWindowTrigger.FoeKO,
                            state.currentTurn);
                        CheckPassiveTriggersForTeam(
                            actingTeam,
                            state.currentTurn,
                            PassiveTriggerPoint.OnKO,
                            target,
                            move);
                        CheckPassiveTriggersForTeam(
                            opposingTeam,
                            state.currentTurn,
                            PassiveTriggerPoint.AllyKO,
                            target,
                            move,
                            exclude: target);
                        _log.LogForensic(
                            CombatForensicEventType.KOResolved,
                            new KOResolvedPayload
                            {
                                targetDisplayName = _log.Name(target.instanceId),
                                hpBefore = hpBefore,
                                hpAfter = target.currentHP,
                                koSource = move.displayName,
                            },
                            attacker.instanceId,
                            new List<string> { target.instanceId },
                            state.currentTurn);
                        int targetSlot = FindSlotOf(opposingTeam, target);
                        _log.LogKO(
                            state.currentTurn,
                            target.instanceId,
                            attacker.instanceId,
                            move.instanceId,
                            opposingTeam != null ? opposingTeam.teamIndex : -1,
                            targetSlot);
                        if (attackerKoByPartialReflect)
                        {
                            HandleReflectedAttackerKo(
                                state, actingTeam, opposingTeam, attacker, target, move, attackerHpBeforePartialReflect);
                        }
                        break;
                    }

                    if (attackerKoByPartialReflect)
                    {
                        HandleReflectedAttackerKo(
                            state, actingTeam, opposingTeam, attacker, target, move, attackerHpBeforePartialReflect);
                        break;
                    }
                }

                if (resolveProfile.utilityExecutionTiming == MoveUtilityExecutionTiming.AfterMoveComplete)
                    ApplyDamagingMoveUtilityEffects(attacker, target, move, state.currentTurn);
            }

            if (isMultiHit)
            {
                _log.LogMessage(CombatEventType.MultiHitEnd, "Multi-hit complete", state.currentTurn);
            }
        }

        private void HandleReflectedAttackerKo(
            CombatState state,
            TeamState actingTeam,
            TeamState opposingTeam,
            ChainlingInstance attacker,
            ChainlingInstance reflector,
            MoveInstance move,
            int hpBefore)
        {
            if (attacker == null || !attacker.isAlive || attacker.currentHP > 0)
                return;

            attacker.isAlive = false;
            int defeatedTeamIndex = actingTeam != null ? actingTeam.teamIndex : -1;
            EnsureTurnActionLedger(state)?.RecordKo(attacker.instanceId, defeatedTeamIndex);
            ProcessArmedWindowsForTeam(opposingTeam, attacker, ArmedWindowTrigger.FoeKO, state.currentTurn);
            CheckPassiveTriggersForTeam(opposingTeam, state.currentTurn, PassiveTriggerPoint.OnKO, attacker, null);
            CheckPassiveTriggersForTeam(
                actingTeam,
                state.currentTurn,
                PassiveTriggerPoint.AllyKO,
                attacker,
                null,
                exclude: attacker);
            _log.LogForensic(
                CombatForensicEventType.KOResolved,
                new KOResolvedPayload
                {
                    targetDisplayName = _log.Name(attacker.instanceId),
                    hpBefore = hpBefore,
                    hpAfter = 0,
                    koSource = "BarrierReflect",
                },
                reflector != null ? reflector.instanceId : string.Empty,
                new List<string> { attacker.instanceId },
                state.currentTurn);
            _log.LogKO(
                state.currentTurn,
                attacker.instanceId,
                reflector != null ? reflector.instanceId : string.Empty,
                move != null ? move.instanceId : string.Empty,
                defeatedTeamIndex,
                FindSlotOf(actingTeam, attacker));
        }

        private void ResolveUtilityMove(CombatState state, TeamState actingTeam,
            TeamState opposingTeam, ChainlingInstance actor, MoveInstance move,
            CombatAction action, ChainlingInstance selectedTarget, bool conditionMet, int paidCost)
        {
            // Position swap: swap active slot positions between actor and target ally
            if (move.definitionId == "position_swap" ||
                (move.instanceId != null && move.instanceId.Contains("position_swap")))
            {
                var swapTarget = selectedTarget;
                if (swapTarget == null || !IsChainlingOnTeam(actingTeam, swapTarget))
                    swapTarget = FindAlly(actingTeam, actor);

                if (swapTarget != null && swapTarget != actor)
                {
                    int actorSlot = FindSlotOf(actingTeam, actor);
                    int targetSlot = FindSlotOf(actingTeam, swapTarget);
                    if (actorSlot >= 0 && targetSlot >= 0 && actorSlot != targetSlot)
                    {
                        int tmp = actingTeam.activeIndices[actorSlot];
                        actingTeam.activeIndices[actorSlot] = actingTeam.activeIndices[targetSlot];
                        actingTeam.activeIndices[targetSlot] = tmp;
                        _log.LogMessage(CombatEventType.ActionSelected,
                            $"{actor.displayName} swapped positions with {swapTarget.displayName}", state.currentTurn);
                    }
                }
                else
                {
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{actor.displayName} has no partner to swap positions with", state.currentTurn);
                }

                ApplyMoveStatuses(actor, selectedTarget ?? actor, move, state.currentTurn);
                ApplyMoveStageChanges(actor, selectedTarget ?? actor, move, state.currentTurn);
                LogResolvedTargets(state.currentTurn, actor, move,
                    swapTarget != null ? new List<ChainlingInstance> { swapTarget } : new List<ChainlingInstance>());
                return;
            }

            // AP transfer moves
            if (IsDefinition(move, "ap_donation") || IsDefinition(move, "ap_redistribution") || IsDefinition(move, "ap_router"))
            {
                ResolveAPTransfer(state, actingTeam, actor, move, selectedTarget, paidCost);
                return;
            }

            // Damage redirect moves
            if (IsDefinition(move, "damage_intercept") || IsDefinition(move, "signal_redirect"))
            {
                ResolveDamageRedirect(state, actingTeam, actor, move, selectedTarget);
                return;
            }

            // Buff/debuff transfer moves
            if (IsDefinition(move, "effect_relay") || IsDefinition(move, "relay_packet"))
            {
                ResolveEffectTransfer(state, actingTeam, actor, move, selectedTarget);
                return;
            }

            var resolvedTargets = new List<ChainlingInstance>(4);

            switch (move.targeting)
            {
                case MoveTargeting.Self:
                    AddUniqueTarget(resolvedTargets, actor);
                    break;

                case MoveTargeting.SingleEnemy:
                case MoveTargeting.RandomEnemy:
                {
                    var enemyTarget = selectedTarget;
                    if (enemyTarget == null || !IsChainlingOnTeam(opposingTeam, enemyTarget))
                        enemyTarget = ResolveSingleSelectedTarget(actingTeam, opposingTeam, actor, move, action, out _, out _);
                    AddUniqueTarget(resolvedTargets, enemyTarget);
                    break;
                }

                case MoveTargeting.SingleAlly:
                {
                    var allyTarget = selectedTarget;
                    if (allyTarget == null || !IsChainlingOnTeam(actingTeam, allyTarget))
                        allyTarget = ResolveSingleSelectedTarget(actingTeam, opposingTeam, actor, move, action, out _, out _);
                    if (allyTarget == null)
                        allyTarget = actor;
                    AddUniqueTarget(resolvedTargets, allyTarget);
                    break;
                }

                case MoveTargeting.AllAllies:
                    AddAliveTargets(actingTeam, resolvedTargets);
                    break;

                case MoveTargeting.AllEnemies:
                    AddAliveTargets(opposingTeam, resolvedTargets);
                    break;

                case MoveTargeting.Field:
                    AddAliveTargets(actingTeam, resolvedTargets);
                    AddAliveTargets(opposingTeam, resolvedTargets);
                    break;

                default:
                    AddUniqueTarget(resolvedTargets, selectedTarget ?? GetFirstAliveActive(opposingTeam));
                    break;
            }

            if (move.hpCostPercent > 0 && move.tags != null && move.tags.Contains("FULL_ABSORB_WALL") && actor != null && actor.isAlive)
            {
                int sacrifice = (int)(actor.EffectiveMaxHP * move.hpCostPercent / 100f);
                sacrifice = Math.Max(1, sacrifice);
                int hpBefore = actor.currentHP;
                actor.currentHP = Math.Max(1, actor.currentHP - sacrifice);
                int actualSacrifice = hpBefore - actor.currentHP;
                if (actualSacrifice > 0)
                {
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{actor.displayName} paid {actualSacrifice} HP to use {move.displayName}.", state.currentTurn);
                }
            }

            for (int i = 0; i < resolvedTargets.Count; i++)
                ApplyUtilityEffects(actor, resolvedTargets[i], move, state.currentTurn, conditionMet, opposingTeam);

            // Group Hug is its own effect: a shared +1 AP for the active pair.
            // The host support move stays single-target. It does not copy that move.
            if (TryGetMoveModifier(move, "4.5-a", out ResolvedMoveModifier groupHug) &&
                move.category == MoveCategory.Support &&
                actingTeam != null)
            {
                List<ChainlingInstance> allies = actingTeam.GetActiveChainlings();
                int hugged = 0;
                for (int i = 0; i < allies.Count; i++)
                {
                    ChainlingInstance ally = allies[i];
                    if (ally == null || !ally.isAlive)
                        continue;
                    if (_apService.Gain(ally, 1, state.currentTurn, "4.5-a") > 0)
                        hugged++;
                }

                LogModifierOutcome(
                    groupHug,
                    actor,
                    selectedTarget ?? actor,
                    move,
                    state.currentTurn,
                    hugged > 0,
                    hugged > 0
                        ? "Group Hug granted +1 AP to each active ally"
                        : "Group Hug found no ally who could gain AP",
                    "+1 AP",
                    actor.instanceId,
                    ModifierPopupMoment.OnProcSucceeded);
            }

            LogResolvedTargets(state.currentTurn, actor, move, resolvedTargets);
        }

        private void ProcessActionEconomyModifiers(
            CombatState state,
            TeamState actingTeam,
            TeamState opposingTeam,
            ChainlingInstance actor,
            ChainlingInstance selectedTarget,
            MoveInstance move,
            bool conditionMet)
        {
            if (state == null || actor == null || move == null || !actor.isAlive)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (!mod.sectionId.Equals("3.2-a", StringComparison.OrdinalIgnoreCase) &&
                    !mod.sectionId.Equals("3.3-a", StringComparison.OrdinalIgnoreCase))
                    continue;

                float chance = RollModifierPercent(mod, mod.sectionId.Equals("3.2-a", StringComparison.OrdinalIgnoreCase) ? 0.10f : 0.12f);
                float roll;
                bool triggered = _rng.Check(chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"Modifier.ActionEconomy.{move.instanceId}.{mod.sectionId}",
                        chance = chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    actor.instanceId,
                    selectedTarget != null ? new List<string> { selectedTarget.instanceId } : null,
                    state.currentTurn);

                if (!triggered)
                {
                    LogModifierOutcome(mod, actor, selectedTarget ?? actor, move, state.currentTurn, false, "Action economy proc failed");
                    continue;
                }

                MoveInstance followUp = mod.sectionId.Equals("3.2-a", StringComparison.OrdinalIgnoreCase)
                    ? CreateModifierRepeatMove(move)
                    : CreateTypelessBonusStrike(move);

                ChainlingInstance target = selectedTarget != null && selectedTarget.isAlive
                    ? selectedTarget
                    : GetFirstAliveActive(opposingTeam);

                _log.Log(new CombatEvent
                {
                    eventType = CombatEventType.BonusActionGranted,
                    turn = state.currentTurn,
                    sourceId = actor.instanceId,
                    targetId = target != null ? target.instanceId : string.Empty,
                    moveId = move.instanceId,
                    message = $"{actor.displayName} gained a modifier follow-up from {mod.sectionId}.",
                });

                if (IsDamagingMove(followUp))
                {
                    ResolveDamagingMove(state, actingTeam, opposingTeam, actor, target, followUp);
                    EnsureTurnActionLedger(state)?.RecordDamagingMove(
                        actingTeam != null ? actingTeam.teamIndex : -1,
                        actor.instanceId);
                }
                else if (target != null)
                {
                    ApplyUtilityEffects(actor, target, followUp, state.currentTurn, conditionMet, opposingTeam);
                }

                LogModifierOutcome(
                    mod,
                    actor,
                    target ?? actor,
                    move,
                    state.currentTurn,
                    true,
                    "Action economy follow-up applied",
                    mod.sectionId.Equals("3.2-a", StringComparison.OrdinalIgnoreCase) ? "REPEAT" : "BONUS",
                    actor.instanceId,
                    ModifierPopupMoment.OnProcSucceeded);
            }
        }

        private static MoveInstance CreateModifierRepeatMove(MoveInstance source)
        {
            var repeat = new MoveInstance
            {
                instanceId = $"{source.instanceId}_modifier_repeat",
                definitionId = $"{source.definitionId}_modifier_repeat",
                displayName = $"{source.displayName} Repeat",
                tier = MoveTier.Known,
                category = source.category,
                targeting = source.targeting,
                contactType = source.contactType,
                baseType = source.baseType,
                basePower = source.basePower,
                apCost = 0,
                baseAccuracy = source.baseAccuracy,
                hitCount = source.hitCount,
                hitCountMax = source.hitCountMax,
                rarity = source.rarity,
                healPercentMaxHP = source.healPercentMaxHP,
                hpCostPercent = source.hpCostPercent,
                resolveProfile = MoveResolveProfileUtility.CloneOrDefault(source.resolveProfile),
            };

            if (source.statusEffects != null)
                repeat.statusEffects.AddRange(source.statusEffects);
            if (source.stageChanges != null)
                repeat.stageChanges.AddRange(source.stageChanges);
            if (source.tags != null)
                repeat.tags.AddRange(source.tags);
            if (source.utilityEffects != null)
                repeat.utilityEffects.AddRange(source.utilityEffects);

            return repeat;
        }

        private static MoveInstance CreateTypelessBonusStrike(MoveInstance source)
        {
            return new MoveInstance
            {
                instanceId = $"{source.instanceId}_modifier_bonus_strike",
                definitionId = "modifier_bonus_strike",
                displayName = "Bonus Strike",
                tier = MoveTier.Known,
                category = MoveCategory.DamagingRanged,
                targeting = MoveTargeting.SingleEnemy,
                contactType = ContactType.None,
                baseType = ChainlingType.None,
                basePower = Math.Max(10, source.basePower > 0 ? source.basePower / 2 : 20),
                apCost = 0,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }

        /// <summary>Find the active partner ally on the same team (not the actor itself).</summary>
        private ChainlingInstance FindAlly(TeamState team, ChainlingInstance actor)
        {
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                var c = team.GetActiveInSlot(s);
                if (c != null && c.isAlive && c != actor)
                    return c;
            }
            return null;
        }

        private int CountBuffedPartners(TeamState team, ChainlingInstance actor)
        {
            if (team == null || actor == null || team.activeIndices == null)
                return 0;

            int count = 0;
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                ChainlingInstance ally = team.GetActiveInSlot(s);
                if (ally == null || ally == actor || !ally.isAlive || ally.statuses == null)
                    continue;

                for (int i = 0; i < ally.statuses.Count; i++)
                {
                    StatusInstance status = ally.statuses[i];
                    if (status == null || status.IsExpired)
                        continue;

                    StatusDefinitionSO def = _statusCatalog != null ? _statusCatalog.Get(status.statusId) : null;
                    if (def != null && def.isBuff)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private ChainlingInstance ResolveSingleSelectedTarget(TeamState actingTeam, TeamState opposingTeam,
            ChainlingInstance actor, MoveInstance move, CombatAction action, out bool retargeted, out string retargetReason)
        {
            retargeted = false;
            retargetReason = string.Empty;
            if (move == null)
                return null;

            if (move.targeting == MoveTargeting.Self)
                return actor;
            if (move.targeting == MoveTargeting.AllAllies ||
                move.targeting == MoveTargeting.AllEnemies ||
                move.targeting == MoveTargeting.Field)
                return null;

            if (move.targeting == MoveTargeting.RandomEnemy)
                return GetRandomAliveActive(opposingTeam);

            bool requiresEnemy = move.targeting == MoveTargeting.SingleEnemy
                                 || move.targeting == MoveTargeting.RandomEnemy
                                 || move.category == MoveCategory.DamagingMelee
                                 || move.category == MoveCategory.DamagingRanged;
            bool requiresAlly = move.targeting == MoveTargeting.SingleAlly;

            bool hasExplicitTarget = action != null && action.targetTeamIndex >= 0 && action.targetSlotIndex >= 0;
            if (hasExplicitTarget)
            {
                var explicitTeam = GetTeamByIndex(actingTeam, opposingTeam, action.targetTeamIndex);
                var explicitTarget = explicitTeam != null ? explicitTeam.GetActiveInSlot(action.targetSlotIndex) : null;
                if (explicitTarget != null && explicitTarget.isAlive &&
                    IsTargetTeamAllowed(explicitTeam, actingTeam, opposingTeam, requiresEnemy, requiresAlly))
                {
                    return explicitTarget;
                }
                retargeted = true;
                retargetReason = "Selected target was invalid, dead, or not in an allowed team.";
            }

            if (requiresEnemy)
                return GetFirstAliveActive(opposingTeam);
            if (requiresAlly)
                return FindAlly(actingTeam, actor) ?? actor;
            return GetFirstAliveActive(opposingTeam);
        }

        private ChainlingInstance GetRandomAliveActive(TeamState team)
        {
            if (team == null)
                return null;

            List<ChainlingInstance> candidates = team.GetActiveChainlings();
            if (candidates.Count == 0)
                return null;

            return candidates[_rng.NextInt(0, candidates.Count - 1)];
        }

        private static bool IsTargetTeamAllowed(TeamState team, TeamState actingTeam, TeamState opposingTeam,
            bool requiresEnemy, bool requiresAlly)
        {
            if (team == null)
                return false;
            if (requiresEnemy)
                return opposingTeam != null && team.teamIndex == opposingTeam.teamIndex;
            if (requiresAlly)
                return actingTeam != null && team.teamIndex == actingTeam.teamIndex;
            return true;
        }

        private TeamState GetTeamByIndex(TeamState actingTeam, TeamState opposingTeam, int teamIndex)
        {
            if (actingTeam != null && actingTeam.teamIndex == teamIndex)
                return actingTeam;
            if (opposingTeam != null && opposingTeam.teamIndex == teamIndex)
                return opposingTeam;
            return null;
        }

        private static bool IsChainlingOnTeam(TeamState team, ChainlingInstance chainling)
        {
            if (team == null || chainling == null || team.activeIndices == null)
                return false;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var candidate = team.GetActiveInSlot(slot);
                if (candidate != null && candidate.instanceId == chainling.instanceId)
                    return true;
            }
            return false;
        }

        private static TeamState FindTeamContaining(CombatState state, ChainlingInstance chainling)
        {
            if (state == null || chainling == null)
                return null;
            if (ContainsChainling(state.team0, chainling))
                return state.team0;
            if (ContainsChainling(state.team1, chainling))
                return state.team1;
            return null;
        }

        private static TeamState FindOpposingTeamContaining(CombatState state, ChainlingInstance chainling)
        {
            TeamState own = FindTeamContaining(state, chainling);
            return own != null ? state.GetOpposingTeam(own.teamIndex) : null;
        }

        private static bool ContainsChainling(TeamState team, ChainlingInstance chainling)
        {
            if (team?.chainlings == null || chainling == null)
                return false;
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance candidate = team.chainlings[i];
                if (candidate == chainling ||
                    (candidate != null && string.Equals(candidate.instanceId, chainling.instanceId, StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        private static ChainlingInstance GetFirstAliveActive(TeamState team)
        {
            if (team == null || team.activeIndices == null)
                return null;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var candidate = team.GetActiveInSlot(slot);
                if (candidate != null && candidate.isAlive)
                    return candidate;
            }
            return null;
        }

        private ChainlingInstance FindAdjacentActive(TeamState team, ChainlingInstance target)
        {
            if (team == null || team.activeIndices == null || target == null)
                return null;

            int slot = -1;
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                if (team.GetActiveInSlot(s) == target)
                {
                    slot = s;
                    break;
                }
            }
            if (slot < 0)
                return null;

            ChainlingInstance left = slot > 0 ? team.GetActiveInSlot(slot - 1) : null;
            ChainlingInstance right = slot + 1 < team.activeIndices.Count ? team.GetActiveInSlot(slot + 1) : null;
            bool leftOk = left != null && left.isAlive && left != target;
            bool rightOk = right != null && right.isAlive && right != target;
            if (leftOk && rightOk)
                return _rng.Check(0.5f) ? left : right;
            if (leftOk)
                return left;
            if (rightOk)
                return right;
            return null;
        }

        private List<ChainlingInstance> BuildResolvedTargetsForMove(TeamState actingTeam, TeamState opposingTeam,
            ChainlingInstance actor, MoveInstance move, ChainlingInstance singleTarget)
        {
            var targets = new List<ChainlingInstance>(4);
            if (move == null)
                return targets;

            bool isDamaging = move.category == MoveCategory.DamagingMelee ||
                              move.category == MoveCategory.DamagingRanged;
            bool hitAllFoes = isDamaging && !HasStatus(actor, StatusId.Anchored) &&
                              (HasStatus(actor, StatusId.Broadcast) || HasStatus(actor, StatusId.Splinter) ||
                                             HasMoveModifier(move, "4.2-a") || HasMoveModifier(move, "4.4-a"));

            if (move.targeting == MoveTargeting.AllAllies)
            {
                AddAliveTargets(actingTeam, targets);
            }
            else if (move.targeting == MoveTargeting.AllEnemies || hitAllFoes)
            {
                AddAliveTargets(opposingTeam, targets);
            }
            else if (move.targeting == MoveTargeting.Field)
            {
                AddAliveTargets(actingTeam, targets);
                AddAliveTargets(opposingTeam, targets);
            }
            else if (move.targeting == MoveTargeting.Self)
            {
                AddUniqueTarget(targets, actor);
            }
            else
            {
                AddUniqueTarget(targets, singleTarget);
            }

            return targets;
        }

        private static void AddAliveTargets(TeamState team, List<ChainlingInstance> output)
        {
            if (team == null || output == null || team.activeIndices == null)
                return;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var candidate = team.GetActiveInSlot(slot);
                AddUniqueTarget(output, candidate);
            }
        }

        private static void AddUniqueTarget(List<ChainlingInstance> output, ChainlingInstance target)
        {
            if (output == null || target == null || !target.isAlive)
                return;
            for (int i = 0; i < output.Count; i++)
            {
                if (output[i] != null && output[i].instanceId == target.instanceId)
                    return;
            }
            output.Add(target);
        }

        private void LogResolvedTargets(int turn, ChainlingInstance actor, MoveInstance move, List<ChainlingInstance> targets,
            List<ChainlingInstance> selectedTargets = null, bool retargeted = false, string retargetReason = "")
        {
            string targetId = targets != null && targets.Count == 1 && targets[0] != null
                ? targets[0].instanceId
                : string.Empty;
            string resolvedSummary = "none";
            if (targets != null && targets.Count > 0)
            {
                var names = new List<string>(targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    var c = targets[i];
                    if (c != null)
                        names.Add(_log.Name(c.instanceId));
                }
                resolvedSummary = names.Count > 0 ? string.Join(", ", names) : "none";
            }

            _log.LogTargeting(
                turn,
                CombatEventType.TargetsResolved,
                actor != null ? actor.instanceId : string.Empty,
                targetId,
                move != null ? move.instanceId : string.Empty,
                $"Resolved targets: {resolvedSummary}");

            var payload = new TargetsResolvedPayload
            {
                retargeted = retargeted,
                retargetReason = retargetReason ?? string.Empty,
            };

            if (selectedTargets != null)
            {
                for (int i = 0; i < selectedTargets.Count; i++)
                {
                    var selected = selectedTargets[i];
                    if (selected == null) continue;
                    payload.selectedTargets.Add(_log.ResolveEntityRef(selected.instanceId));
                    payload.selectedTargetNames.Add(_log.Name(selected.instanceId));
                }
            }

            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var resolved = targets[i];
                    if (resolved == null) continue;
                    payload.resolvedTargets.Add(_log.ResolveEntityRef(resolved.instanceId));
                    payload.resolvedTargetNames.Add(_log.Name(resolved.instanceId));
                }
            }

            List<string> targetIds = null;
            if (targets != null && targets.Count > 0)
            {
                targetIds = new List<string>(targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                        targetIds.Add(targets[i].instanceId);
                }
            }

            _log.LogForensic(
                CombatForensicEventType.TargetsResolved,
                payload,
                actor != null ? actor.instanceId : string.Empty,
                targetIds,
                turn);
        }

        private void LogHealApplied(int turn, ChainlingInstance source, ChainlingInstance target,
            MoveInstance move, int amount, string reasonLabel)
        {
            if (target == null || amount <= 0)
                return;

            string sourceId = source != null ? source.instanceId : string.Empty;
            string moveId = move != null ? move.instanceId : string.Empty;
            string moveName = move != null ? move.displayName : reasonLabel;
            string reason = string.IsNullOrEmpty(reasonLabel) ? moveName : reasonLabel;

            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.HealApplied,
                turn = turn,
                sourceId = sourceId,
                targetId = target.instanceId,
                moveId = moveId,
                intValue = amount,
                message = $"{target.displayName} healed {amount} HP ({reason})",
            });
            _log.LogMessage(CombatEventType.HPHealed,
                $"{target.displayName} healed {amount} HP from {moveName}", turn);
        }

        private void ApplyUtilityEffects(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            bool conditionMet,
            TeamState opposingTeam = null)
        {
            bool shouldApply = ShouldApplyMoveEffectsAtResolve(source, target, move, turn, logSkipped: true);
            if (!shouldApply)
                return;

            if (IsAllySupportBlockedByIsolation(source, target, move))
            {
                _log.LogStatus(turn, CombatEventType.StatusImmune,
                    target.instanceId, StatusId.Isolated, 0, move.instanceId);
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{target.displayName} is Isolated and cannot receive ally support.", turn);
                return;
            }

            ApplyMoveStatuses(source, target, move, turn);
            ApplyMoveStageChanges(source, target, move, turn);
            ApplyMoveHealing(source, target, move, turn);
            ProcessPassAOnUseModifiers(source, target, move, turn);
            ProcessSharedBuffModifiers(source, target, move, turn);

            TeamState sourceTeam = FindTeamContaining(_currentState, source);
            TeamState targetTeam = opposingTeam ?? FindTeamContaining(_currentState, target);
            _utilityEffectExecutor.Execute(
                source,
                target,
                move,
                turn,
                shouldApply,
                _rng,
                opposingTeam,
                CreateUtilityContext(sourceTeam, targetTeam, 0));
            ProcessSupportSynergyModifiers(source, target, move, turn);
        }

        private bool IsAllySupportBlockedByIsolation(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move)
        {
            if (source == null || target == null || move == null || target == source ||
                !HasStatus(target, StatusId.Isolated))
                return false;

            // Field effects resolve against the field, even when their target
            // expansion includes allied Chainlings.
            if (move.targeting == MoveTargeting.Field)
                return false;

            if (move.category == MoveCategory.Support || move.healPercentMaxHP > 0)
                return true;

            if (move.stageChanges != null)
            {
                for (int i = 0; i < move.stageChanges.Count; i++)
                {
                    if (!move.stageChanges[i].targetsSelf)
                        return true;
                }
            }

            if (move.utilityEffects != null)
            {
                for (int i = 0; i < move.utilityEffects.Count; i++)
                {
                    UtilityEffectPayload effect = move.utilityEffects[i];
                    if (effect == null)
                        continue;

                    switch (effect.effectType)
                    {
                        case UtilityEffectType.GainAP:
                        case UtilityEffectType.TransferAP:
                        case UtilityEffectType.ChangeStage:
                        case UtilityEffectType.RandomStageChange:
                        case UtilityEffectType.FieldEntryStageChange:
                        case UtilityEffectType.BonusHealPercentIfSpeedHigher:
                        case UtilityEffectType.HealPercentIfStageAtLeast:
                            return true;
                    }
                }
            }

            return false;
        }

        private bool ShouldApplyMoveEffectsAtResolve(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            bool logSkipped)
        {
            if (move == null)
                return false;

            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            bool legacyConditionMet = _eligibilityService.EvaluateActiveConditionAtResolve(source, move);
            bool legacyPass = move.activeConditionType == ActiveConditionType.None || legacyConditionMet;
            MoveResolveConditionEvaluation resolveEvaluation =
                MoveResolveProfileUtility.EvaluateConditions(
                    profile,
                    source,
                    target,
                    CreateResolveEvaluationContext(source, target));

            bool shouldApply;
            switch (profile.conditionalEffectPolicy)
            {
                case MoveConditionalEffectPolicy.AlwaysApply:
                    shouldApply = true;
                    break;

                case MoveConditionalEffectPolicy.LegacyActiveConditionOnly:
                    shouldApply = legacyPass;
                    break;

                case MoveConditionalEffectPolicy.ResolveConditionsOnly:
                    shouldApply = resolveEvaluation.passed;
                    break;

                case MoveConditionalEffectPolicy.LegacyOrResolveConditions:
                    shouldApply = legacyPass || resolveEvaluation.passed;
                    break;

                default:
                    shouldApply = legacyPass && resolveEvaluation.passed;
                    break;
            }

            if (!shouldApply && logSkipped)
            {
                string reason = !legacyPass
                    ? "ActiveConditionNotMetAtResolve"
                    : !string.IsNullOrWhiteSpace(resolveEvaluation.reason)
                        ? resolveEvaluation.reason
                        : "ResolveConditionNotMet";
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{move.displayName} conditional effects skipped ({reason}).",
                    turn);
            }

            return shouldApply;
        }

        private float ResolveConditionalDamageMultiplier(
            CombatState state,
            TeamState sourceTeam,
            TeamState targetTeam,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int hitIndex)
        {
            MoveResolveProfile profile = MoveResolveProfileUtility.Resolve(move);
            MoveConditionalDamageTuning tuning = profile.conditionalDamage;
            if (tuning == null)
                return 1f;

            var evaluationContext = new MoveResolveEvaluationContext
            {
                sourceTeam = sourceTeam,
                targetTeam = targetTeam,
                ledger = EnsureTurnActionLedger(state),
                statusCatalog = _statusCatalog,
            };
            MoveResolveConditionEvaluation evaluation = MoveResolveProfileUtility.EvaluateConditions(
                tuning.conditions,
                tuning.requireAllConditions,
                source,
                target,
                evaluationContext);
            if (!evaluation.passed)
            {
                if (tuning.conditions != null && tuning.conditions.Count > 0)
                {
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{move.displayName} conditional damage skipped ({evaluation.reason}).",
                        state.currentTurn);
                }
                return 1f;
            }

            float multiplier = tuning.multiplier > 0f ? tuning.multiplier : 1f;
            if (tuning.useScalingCondition && tuning.bonusMultiplierPerCount != 0f)
            {
                int count = MoveResolveProfileUtility.ResolveConditionCount(
                    tuning.scalingCondition,
                    source,
                    target,
                    evaluationContext);
                multiplier += tuning.bonusMultiplierPerCount * Math.Max(0, count);
            }
            if (tuning.maxMultiplier > 0f)
                multiplier = Math.Min(tuning.maxMultiplier, multiplier);

            if (tuning.randomMultipliers != null && tuning.randomMultipliers.Count > 0)
            {
                int selected = _rng.NextInt(0, tuning.randomMultipliers.Count - 1, out float roll);
                float randomMultiplier = Math.Max(0f, tuning.randomMultipliers[selected]);
                multiplier *= randomMultiplier;
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"ConditionalDamage.{move.instanceId}.Hit{hitIndex + 1}.Option{selected}",
                        chance = 1f / tuning.randomMultipliers.Count,
                        roll = roll,
                        triggered = true,
                    },
                    source != null ? source.instanceId : string.Empty,
                    target != null ? new List<string> { target.instanceId } : null,
                    state.currentTurn);
            }

            return Math.Max(0f, multiplier);
        }

        private MoveResolveEvaluationContext CreateResolveEvaluationContext(
            ChainlingInstance source,
            ChainlingInstance target)
        {
            return new MoveResolveEvaluationContext
            {
                sourceTeam = FindTeamContaining(_currentState, source),
                targetTeam = FindTeamContaining(_currentState, target) ??
                             FindOpposingTeamContaining(_currentState, source),
                ledger = EnsureTurnActionLedger(_currentState),
                statusCatalog = _statusCatalog,
            };
        }

        private UtilityEffectExecutionContext CreateUtilityContext(
            TeamState sourceTeam,
            TeamState targetTeam,
            int damageDealt)
        {
            return new UtilityEffectExecutionContext
            {
                state = _currentState,
                sourceTeam = sourceTeam,
                targetTeam = targetTeam,
                ledger = EnsureTurnActionLedger(_currentState),
                damageDealt = damageDealt,
            };
        }

        private bool ProcessArmedWindows(
            ChainlingInstance owner,
            TeamState ownerTeam,
            ChainlingInstance eventSource,
            ArmedWindowTrigger trigger,
            int turn)
        {
            if (owner?.armedUtilityWindows == null || owner.armedUtilityWindows.Count == 0)
                return false;

            for (int i = owner.armedUtilityWindows.Count - 1; i >= 0; i--)
            {
                ArmedUtilityWindow window = owner.armedUtilityWindows[i];
                if (window == null)
                {
                    owner.armedUtilityWindows.RemoveAt(i);
                    continue;
                }
                if (window.trigger != trigger || window.activateOnTurn > turn)
                    continue;

                _utilityEffectExecutor.ApplyArmedWindowEffects(owner, ownerTeam, eventSource, window, turn);
                if (window.hitsRemaining > 0)
                    window.hitsRemaining--;
                if (window.hitsRemaining == 0)
                    owner.armedUtilityWindows.RemoveAt(i);

                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{owner.displayName}'s {window.sourceTag} window fired ({trigger}).",
                    turn);
                return true;
            }
            return false;
        }

        private void ProcessArmedWindowsForTeam(
            TeamState team,
            ChainlingInstance eventSource,
            ArmedWindowTrigger trigger,
            int turn)
        {
            if (team == null)
                return;
            List<ChainlingInstance> actives = team.GetActiveChainlings();
            for (int i = 0; i < actives.Count; i++)
                ProcessArmedWindows(actives[i], team, eventSource, trigger, turn);
        }

        private static void TickArmedWindows(ChainlingInstance chainling, int turn)
        {
            if (chainling?.armedUtilityWindows == null)
                return;
            for (int i = chainling.armedUtilityWindows.Count - 1; i >= 0; i--)
            {
                ArmedUtilityWindow window = chainling.armedUtilityWindows[i];
                if (window == null)
                {
                    chainling.armedUtilityWindows.RemoveAt(i);
                    continue;
                }
                if (window.trigger == ArmedWindowTrigger.OwnerTurnStart && window.activateOnTurn > turn)
                    continue;
                if (window.turnsRemaining > 0)
                    window.turnsRemaining--;
                if (window.turnsRemaining <= 0)
                    chainling.armedUtilityWindows.RemoveAt(i);
            }
        }

        private CombatAction BuildRandomLegalMisAct(
            CombatState state,
            TeamState actingTeam,
            ChainlingInstance actor)
        {
            int teamIndex = actingTeam != null ? actingTeam.teamIndex : 0;
            TeamState opposingTeam = state != null ? state.GetOpposingTeam(teamIndex) : null;
            List<ScoredAction> legalActions = AIHelpers.GetLegalActions(
                actor,
                actingTeam,
                opposingTeam,
                teamIndex,
                state,
                _eligibilityService);
            int optionCount = Math.Max(1, legalActions.Count);
            int selected = _rng.NextInt(0, optionCount - 1, out float roll);
            _log.LogForensic(
                CombatForensicEventType.ProcRoll,
                new ProcRollPayload
                {
                    procId = $"Status.GlitchedMindAction.{selected}",
                    chance = 1f / optionCount,
                    roll = roll,
                    triggered = true,
                },
                actor != null ? actor.instanceId : string.Empty,
                null,
                state != null ? state.currentTurn : 0);

            return selected >= 0 && selected < legalActions.Count
                ? legalActions[selected].action
                : CombatAction.Pass(teamIndex);
        }

        private int CountActiveBuffStatuses(ChainlingInstance chainling)
        {
            if (chainling?.statuses == null || _statusCatalog == null)
                return 0;
            int count = 0;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance status = chainling.statuses[i];
                StatusDefinitionSO definition = status != null ? _statusCatalog.Get(status.statusId) : null;
                if (status != null && !status.IsExpired && definition != null && definition.isBuff)
                    count++;
            }
            return count;
        }

        private void TickFieldEntryEffects(CombatState state)
        {
            if (state?.fieldEntryEffects == null)
                return;
            for (int i = state.fieldEntryEffects.Count - 1; i >= 0; i--)
            {
                CombatFieldEntryEffect effect = state.fieldEntryEffects[i];
                if (effect == null)
                {
                    state.fieldEntryEffects.RemoveAt(i);
                    continue;
                }
                effect.turnsRemaining--;
                if (effect.turnsRemaining <= 0)
                    state.fieldEntryEffects.RemoveAt(i);
            }
        }

        private void ApplyFieldEntryEffects(CombatState state, TeamState enteringTeam, ChainlingInstance entering, int turn)
        {
            if (state?.fieldEntryEffects == null || enteringTeam == null || entering == null)
                return;
            for (int i = 0; i < state.fieldEntryEffects.Count; i++)
            {
                CombatFieldEntryEffect effect = state.fieldEntryEffects[i];
                if (effect == null || effect.turnsRemaining <= 0)
                    continue;
                bool isOpposing = effect.sourceTeamIndex != enteringTeam.teamIndex;
                if (effect.affectsOpposingTeam != isOpposing)
                    continue;

                float roll;
                bool triggered = _rng.Check(effect.chance > 0f ? effect.chance : 1f, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"FieldEntry.{effect.sourceTag}.{effect.stageStat}",
                        chance = effect.chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    effect.sourceTag,
                    new List<string> { entering.instanceId },
                    turn);
                if (!triggered)
                    continue;

                int before = entering.GetCombatStage(effect.stageStat);
                entering.SetCombatStage(effect.stageStat, before + effect.stageAmount);
                int actual = entering.GetCombatStage(effect.stageStat) - before;
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{entering.displayName} entered {effect.sourceTag}: {effect.stageStat} {actual:+#;-#;0}.",
                    turn);
            }
        }

        private void ApplyMoveHealing(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            if (target == null || move == null)
                return;

            if (move.healPercentMaxHP > 0)
            {
                int healAmount = (int)(target.EffectiveMaxHP * move.healPercentMaxHP / 100f);
                healAmount = Math.Max(1, healAmount);
                target.currentHP = Math.Min(target.EffectiveMaxHP, target.currentHP + healAmount);
                LogHealApplied(turn, source, target, move, healAmount, move.displayName);
            }
            else if (move.tags != null && move.tags.Contains("HEAL"))
            {
                int healAmount = move.basePower > 0 ? move.basePower : 20;
                target.currentHP = Math.Min(target.EffectiveMaxHP, target.currentHP + healAmount);
                LogHealApplied(turn, source, target, move, healAmount, move.displayName);
            }
        }

        private void ApplyDamagingMoveUtilityEffects(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            int damageDealt = 0)
        {
            if (move == null || move.utilityEffects == null || move.utilityEffects.Count == 0)
                return;

            bool shouldApply = ShouldApplyMoveEffectsAtResolve(source, target, move, turn, logSkipped: true);
            TeamState sourceTeam = FindTeamContaining(_currentState, source);
            TeamState targetTeam = FindTeamContaining(_currentState, target);
            _utilityEffectExecutor.Execute(
                source,
                target,
                move,
                turn,
                shouldApply,
                _rng,
                targetTeam,
                CreateUtilityContext(sourceTeam, targetTeam, damageDealt));
        }

        private void ProcessSupportSynergyModifiers(ChainlingInstance source, ChainlingInstance target, MoveInstance move, int turn)
        {
            if (source == null || target == null || move == null || move.category != MoveCategory.Support)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (mod.sectionId.Equals("7.1-b", StringComparison.OrdinalIgnoreCase))
                {
                    float mitigation = RollModifierPercent(mod, 0.15f);
                    if (mitigation <= 0f)
                        continue;

                    target.AddMitigationSource(mitigation, RestOfTurnDuration, $"Modifier.{mod.sectionId}");
                    LogModifierOutcome(
                        mod,
                        source,
                        target,
                        move,
                        turn,
                        true,
                        "Support synergy mitigation applied",
                        "MITIGATE",
                        target.instanceId,
                        ModifierPopupMoment.OnSynergyMet);
                }
                else if (mod.sectionId.Equals("7.1-c", StringComparison.OrdinalIgnoreCase))
                {
                    float roll;
                    int idx = _rng.NextInt(0, RandomStageStats.Length - 1, out roll);
                    CoreStat stat = RandomStageStats[idx];
                    int oldStage = target.GetStage(stat);
                    target.SetStage(stat, oldStage + 1);
                    int newStage = target.GetStage(stat);
                    int delta = newStage - oldStage;
                    if (delta == 0)
                        continue;

                    _log.LogStageChange(turn, target.instanceId, stat, delta, newStage);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload
                        {
                            procId = $"Modifier.{mod.sectionId}.RandomStat",
                            chance = 1f,
                            roll = roll,
                            triggered = true,
                        },
                        source.instanceId,
                        new List<string> { target.instanceId },
                        turn);
                    LogModifierOutcome(
                        mod,
                        source,
                        target,
                        move,
                        turn,
                        true,
                        "Support synergy stage applied",
                        "STAT +1",
                        target.instanceId,
                        ModifierPopupMoment.OnSynergyMet);
                }
            }
        }

        private void ApplyMoveStatuses(ChainlingInstance source, ChainlingInstance target,
            MoveInstance move, int turn)
        {
            var appliedStatusModifiers = new List<ResolvedMoveModifier>();
            var emittedStatusModifierSections = new List<string>();
            float statusChanceMultiplier = ResolveStatusChanceMultiplier(source, target, move, turn, appliedStatusModifiers);
            for (int i = 0; i < move.statusEffects.Count; i++)
            {
                var effect = move.statusEffects[i];
                var statusTarget = effect.targetOverride == MoveTargeting.Self ? source : target;
                StatusDefinitionSO statusDefinition = _statusCatalog != null ? _statusCatalog.Get(effect.statusId) : null;
                if (statusTarget == target && source != null && target != null && source != target &&
                    statusDefinition != null && statusDefinition.isDebuff &&
                    TryReflectIncomingEffect(target, source, turn))
                {
                    statusTarget = source;
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{target.displayName} reflected {effect.statusId} to {source.displayName}.",
                        turn);
                }
                int duration = effect.duration + ResolveStatusDurationBonus(source, statusTarget, move, turn, appliedStatusModifiers);
                float chance = Math.Min(1f, Math.Max(0f, effect.chance * statusChanceMultiplier));
                float roll;
                bool triggered = _rng.Check(chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"MoveStatus.{move.instanceId}.{effect.statusId}.{i}",
                        chance = chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    source != null ? source.instanceId : string.Empty,
                    statusTarget != null ? new List<string> { statusTarget.instanceId } : null,
                    turn);

                if (triggered)
                {
                    if (IsTurnStartPendingStatus(effect.statusId))
                    {
                        QueueTurnStartStatus(statusTarget, effect.statusId, duration, move.instanceId, turn + 1);
                    }
                    else
                    {
                        bool applied = _statusProcessor.ApplyStatus(statusTarget, effect.statusId,
                            duration, move.instanceId, turn);
                        if (applied)
                            TrySpiteCounter(statusTarget, source, effect.statusId, duration, turn, statusDefinition);
                    }

                    EmitStatusModifierOutcomes(
                        appliedStatusModifiers,
                        emittedStatusModifierSections,
                        source,
                        statusTarget,
                        move,
                        turn);
                }
            }
        }

        private void TrySpiteCounter(
            ChainlingInstance recipient,
            ChainlingInstance attacker,
            StatusId statusId,
            int duration,
            int turn,
            StatusDefinitionSO statusDefinition)
        {
            if (recipient == null || attacker == null || recipient == attacker || !attacker.isAlive)
                return;
            if (statusDefinition == null || !statusDefinition.isDebuff)
                return;
            if (recipient.moves == null)
                return;

            bool hasSpite = false;
            for (int i = 0; i < recipient.moves.Count; i++)
            {
                MoveInstance passive = recipient.moves[i];
                if (passive != null && passive.category == MoveCategory.Passive &&
                    string.Equals(passive.definitionId, "spite_counter", StringComparison.Ordinal))
                {
                    hasSpite = true;
                    break;
                }
            }
            if (!hasSpite)
                return;

            bool mirrored = _rng.Check(0.33f, out float roll);
            _log.LogForensic(
                CombatForensicEventType.ProcRoll,
                new ProcRollPayload { procId = "Passive.spite_counter", chance = 0.33f, roll = roll, triggered = mirrored },
                recipient.instanceId,
                new List<string> { attacker.instanceId },
                turn);
            if (!mirrored)
                return;

            if (_statusProcessor.ApplyStatus(attacker, statusId, duration, "spite_counter", turn))
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{recipient.displayName}'s Spite Counter mirrored {statusId} onto {attacker.displayName}.",
                    turn);
            }
        }

        private float ResolveStatusChanceMultiplier(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            List<ResolvedMoveModifier> appliedModifiers)
        {
            float multiplier = 1f;
            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (!mod.sectionId.Equals("2.4-a", StringComparison.OrdinalIgnoreCase))
                    continue;
                multiplier *= 1f + RollModifierPercent(mod, 0.10f);
                appliedModifiers?.Add(mod);
            }
            return multiplier;
        }

        private int ResolveStatusDurationBonus(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            List<ResolvedMoveModifier> appliedModifiers)
        {
            int bonus = 0;
            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (mod.sectionId.Equals("2.5-a", StringComparison.OrdinalIgnoreCase))
                {
                    bonus += 1;
                    appliedModifiers?.Add(mod);
                }
                else if (mod.sectionId.Equals("2.5-b", StringComparison.OrdinalIgnoreCase))
                {
                    float chance = RollModifierPercent(mod, 0.25f);
                    float roll;
                    bool triggered = _rng.Check(chance, out roll);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload
                        {
                            procId = $"Modifier.StatusDuration.{move.instanceId}.{mod.sectionId}",
                            chance = chance,
                            roll = roll,
                            triggered = triggered,
                        },
                        source != null ? source.instanceId : string.Empty,
                        target != null ? new List<string> { target.instanceId } : null,
                        turn);
                    if (triggered)
                    {
                        bonus += 1;
                        appliedModifiers?.Add(mod);
                    }
                    else
                    {
                        LogModifierOutcome(mod, source, target, move, turn, false, "Status duration proc failed");
                    }
                }
            }
            return bonus;
        }

        private void EmitStatusModifierOutcomes(
            List<ResolvedMoveModifier> modifiers,
            List<string> emittedSections,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            if (modifiers == null || modifiers.Count == 0)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (string.IsNullOrWhiteSpace(mod.sectionId))
                    continue;
                if (emittedSections != null && emittedSections.Contains(mod.sectionId))
                    continue;

                emittedSections?.Add(mod.sectionId);
                LogModifierOutcome(
                    mod,
                    source,
                    target,
                    move,
                    turn,
                    true,
                    "Status modifier applied after status proof",
                    ResolveModifierDisplayText(mod),
                    target != null ? target.instanceId : source != null ? source.instanceId : string.Empty,
                    ModifierPopupMoment.OnStatusApplied);
            }
        }

        private void QueueTurnStartStatus(
            ChainlingInstance target,
            StatusId statusId,
            int duration,
            string sourceId,
            int applyOnTurn)
        {
            if (target == null || statusId == StatusId.None)
                return;
            if (target.pendingTurnStartStatuses == null)
                target.pendingTurnStartStatuses = new List<PendingStatusApplication>();

            target.pendingTurnStartStatuses.Add(new PendingStatusApplication
            {
                statusId = statusId,
                duration = Math.Max(1, duration),
                sourceId = sourceId,
                applyOnTurn = applyOnTurn,
            });

            _log.LogMessage(
                CombatEventType.SystemMessage,
                $"{target.displayName} will gain {statusId} next turn.",
                Math.Max(0, applyOnTurn - 1));
        }

        private static bool IsTurnStartPendingStatus(StatusId statusId)
        {
            return statusId == StatusId.FrozenAssets || statusId == StatusId.ThreadLock;
        }

        private void ApplyMoveStageChanges(ChainlingInstance source, ChainlingInstance target,
            MoveInstance move, int turn)
        {
            for (int i = 0; i < move.stageChanges.Count; i++)
            {
                var change = move.stageChanges[i];
                var stageTarget = change.targetsSelf ? source : target;
                if (!change.targetsSelf && change.stages < 0 && source != null && target != null && source != target &&
                    TryReflectIncomingEffect(target, source, turn))
                {
                    stageTarget = source;
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{target.displayName} reflected a {change.stat} debuff to {source.displayName}.",
                        turn);
                }
                float roll;
                bool triggered = _rng.Check(change.chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"MoveStage.{move.instanceId}.{change.stat}.{i}",
                        chance = change.chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    source != null ? source.instanceId : string.Empty,
                    stageTarget != null ? new List<string> { stageTarget.instanceId } : null,
                    turn);

                if (triggered)
                {
                    int oldStage = stageTarget.GetStage(change.stat);
                    int clampedChange = change.stages;
                    if (clampedChange < 0)
                        clampedChange = Math.Max(clampedChange, -_resolverConfig.stageActiveChangeCap);

                    stageTarget.SetStage(change.stat, oldStage + clampedChange);
                    _log.LogStageChange(turn, stageTarget.instanceId, change.stat,
                        clampedChange, stageTarget.GetStage(change.stat));
                }
            }
        }

        /// <summary>
        /// Check and trigger passive Stamped moves for a Chainling.
        /// Called after HP changes (damage/healing) to check for threshold-based triggers.
        /// </summary>
        private void CheckPassiveTriggersForTeam(
            TeamState team,
            int turn,
            PassiveTriggerPoint triggerPoint,
            ChainlingInstance other,
            MoveInstance sourceMove,
            ChainlingInstance exclude = null)
        {
            if (team == null)
                return;
            List<ChainlingInstance> actives = team.GetActiveChainlings();
            for (int i = 0; i < actives.Count; i++)
            {
                ChainlingInstance chainling = actives[i];
                if (chainling == null || chainling == exclude)
                    continue;
                CheckPassiveTriggers(chainling, turn, triggerPoint, other, 0, sourceMove);
            }
        }

        private void CheckPassiveTriggers(
            ChainlingInstance chainling,
            int turn,
            PassiveTriggerPoint triggerPoint,
            ChainlingInstance other = null,
            int damage = 0,
            MoveInstance sourceMove = null,
            bool superEffective = false)
        {
            if (chainling == null || !chainling.isAlive) return;

            for (int i = 0; i < chainling.moves.Count; i++)
            {
                var move = chainling.moves[i];

                // Only check passive Stamped moves with triggers
                if (move.category != MoveCategory.Passive) continue;

                // Check if already triggered (for once-per-battle passives)
                if (move.triggerOncePerBattle && chainling.triggeredPassiveIds != null &&
                    chainling.triggeredPassiveIds.Contains(move.instanceId))
                    continue;

                // Check trigger condition
                bool shouldTrigger = false;
                switch (move.triggerCondition)
                {
                    case PassiveTriggerCondition.HPBelowThreshold:
                        if (triggerPoint != PassiveTriggerPoint.AfterDamage)
                            break;
                        float hpPercent = (float)chainling.currentHP / chainling.EffectiveMaxHP * 100f;
                        if (hpPercent <= move.hpThresholdPercent)
                        {
                            shouldTrigger = true;
                        }
                        break;

                    case PassiveTriggerCondition.OnDamageTaken:
                        // This is called after damage, so it should trigger
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.AfterDamage && damage > 0;
                        break;

                    case PassiveTriggerCondition.OnMiss:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnMiss;
                        break;

                    case PassiveTriggerCondition.EndOfTurn:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.EndOfTurn;
                        break;

                    case PassiveTriggerCondition.TurnStart:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.TurnStart;
                        break;

                    case PassiveTriggerCondition.OnHit:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnHit;
                        break;

                    case PassiveTriggerCondition.OnCrit:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnCrit;
                        break;

                    case PassiveTriggerCondition.OnAllyHit:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnAllyHit;
                        break;

                    case PassiveTriggerCondition.OnBuffExpired:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnBuffExpired && damage > 0;
                        break;

                    case PassiveTriggerCondition.OnAction:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnAction;
                        break;

                    case PassiveTriggerCondition.OnKO:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.OnKO;
                        break;

                    case PassiveTriggerCondition.OnAllyKO:
                        shouldTrigger = triggerPoint == PassiveTriggerPoint.AllyKO;
                        break;

                    case PassiveTriggerCondition.None:
                        shouldTrigger = ShouldTriggerImplicitPassive(chainling, move, triggerPoint, other, damage, sourceMove);
                        break;

                    // Other trigger conditions can be added here as needed
                    default:
                        break;
                }

                if (shouldTrigger)
                {
                    MoveResolveConditionEvaluation conditionEvaluation = MoveResolveProfileUtility.EvaluateConditions(
                        MoveResolveProfileUtility.Resolve(move),
                        chainling,
                        other ?? chainling,
                        CreateResolveEvaluationContext(chainling, other));
                    if (!conditionEvaluation.passed)
                    {
                        _log.LogMessage(
                            CombatEventType.SystemMessage,
                            $"{move.displayName} passive skipped ({conditionEvaluation.reason}).",
                            turn);
                        continue;
                    }
                    bool applied = TriggerPassiveMove(chainling, move, turn, triggerPoint, other, damage, sourceMove, superEffective);

                    // Once-per-battle passives are spent when the trigger lands, including a
                    // heal that cannot land because the fighter is already at 0 HP. Swap, bench,
                    // and in-battle revive keep this set on the same instance.
                    if (move.triggerOncePerBattle && chainling.triggeredPassiveIds != null)
                        chainling.triggeredPassiveIds.Add(move.instanceId);
                    if (!applied)
                        continue;
                }
            }
        }

        /// <summary>
        /// Apply the effects of a triggered passive move.
        /// </summary>
        private bool TriggerPassiveMove(
            ChainlingInstance chainling,
            MoveInstance move,
            int turn,
            PassiveTriggerPoint triggerPoint,
            ChainlingInstance other,
            int damage,
            MoveInstance sourceMove,
            bool superEffective = false)
        {
            if (string.Equals(move.definitionId, "shattered_armor", StringComparison.Ordinal) && !superEffective)
                return false;

            bool appliedAny = false;
            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.PassiveTriggered,
                turn = turn,
                sourceId = chainling.instanceId,
                moveId = move.instanceId,
                message = $"{chainling.displayName}'s {move.displayName} triggered!",
            });

            // Apply stage changes (targets self for passive Stamped moves)
            for (int i = 0; i < move.stageChanges.Count; i++)
            {
                var change = move.stageChanges[i];
                float roll;
                bool triggered = _rng.Check(change.chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"PassiveStage.{move.instanceId}.{change.stat}.{i}",
                        chance = change.chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    chainling.instanceId,
                    new List<string> { chainling.instanceId },
                    turn);

                if (triggered)
                {
                    int oldStage = chainling.GetStage(change.stat);
                    int clampedChange = change.stages;
                    if (clampedChange < 0)
                        clampedChange = Math.Max(clampedChange, -_resolverConfig.stageActiveChangeCap);

                    chainling.SetStage(change.stat, oldStage + clampedChange);
                    _log.LogStageChange(turn, chainling.instanceId, change.stat,
                        clampedChange, chainling.GetStage(change.stat));
                    appliedAny = true;
                }
            }

            // Apply status effects (targets self for passive Stamped moves)
            for (int i = 0; i < move.statusEffects.Count; i++)
            {
                var effect = move.statusEffects[i];
                float roll;
                bool triggered = _rng.Check(effect.chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"PassiveStatus.{move.instanceId}.{effect.statusId}.{i}",
                        chance = effect.chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    chainling.instanceId,
                    new List<string> { chainling.instanceId },
                    turn);

                if (triggered)
                {
                    var statusTarget = effect.targetOverride == MoveTargeting.Self ? chainling : chainling;
                    _statusProcessor.ApplyStatus(statusTarget, effect.statusId,
                        effect.duration, move.instanceId, turn);
                    appliedAny = true;
                }
            }

            if (move.healPercentMaxHP > 0 && chainling.currentHP > 0)
            {
                int heal = Math.Max(1, (int)(chainling.EffectiveMaxHP * move.healPercentMaxHP / 100f));
                int before = chainling.currentHP;
                chainling.currentHP = Math.Min(chainling.EffectiveMaxHP, chainling.currentHP + heal);
                int actual = chainling.currentHP - before;
                if (actual > 0)
                {
                    LogHealApplied(turn, chainling, chainling, move, actual, move.displayName);
                    appliedAny = true;
                }
            }

            if (move.utilityEffects != null && move.utilityEffects.Count > 0)
            {
                TeamState sourceTeam = FindTeamContaining(_currentState, chainling);
                TeamState targetTeam = sourceTeam != null && _currentState != null
                    ? _currentState.GetOpposingTeam(sourceTeam.teamIndex)
                    : FindTeamContaining(_currentState, other);
                _utilityEffectExecutor.Execute(
                    chainling,
                    other ?? chainling,
                    move,
                    turn,
                    true,
                    _rng,
                    targetTeam,
                    CreateUtilityContext(sourceTeam, targetTeam, damage));
                appliedAny = true;
            }

            string defId = move.utilityEffects != null && move.utilityEffects.Count > 0
                ? string.Empty
                : move.definitionId ?? string.Empty;
            if (triggerPoint == PassiveTriggerPoint.TurnStart)
            {
                if (defId == "turn_start_ap_gain")
                {
                    appliedAny |= _apService.Gain(chainling, 1, turn, defId) > 0;
                }
            }
            else if (triggerPoint == PassiveTriggerPoint.OnHit)
            {
                if (defId == "shattered_armor" && superEffective && chainling.MarkOncePerTurn(defId, turn))
                {
                    chainling.AddMitigationSource(0.05f, 1, -1, defId, defId);
                    appliedAny |= _apService.Gain(chainling, 1, turn, defId) > 0;
                    _log.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{chainling.displayName}'s {move.displayName} grants 5% mitigation for the rest of this turn.",
                        turn);
                }
                else if (defId == "on_hit_momentum" && chainling.GetStage(CoreStat.MeleeAtk) < 3)
                {
                    float roll;
                    bool triggered = _rng.Check(0.25f, out roll);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload { procId = $"Passive.{defId}", chance = 0.25f, roll = roll, triggered = triggered },
                        chainling.instanceId,
                        new List<string> { chainling.instanceId },
                        turn);
                    if (triggered)
                    {
                        int oldStage = chainling.GetStage(CoreStat.MeleeAtk);
                        chainling.SetStage(CoreStat.MeleeAtk, Math.Min(3, oldStage + 1));
                        _log.LogStageChange(turn, chainling.instanceId, CoreStat.MeleeAtk,
                            chainling.GetStage(CoreStat.MeleeAtk) - oldStage, chainling.GetStage(CoreStat.MeleeAtk));
                        appliedAny = true;
                    }
                }
            }
            else if (triggerPoint == PassiveTriggerPoint.OnCrit)
            {
                if (defId == "on_crit_bonus")
                    appliedAny |= _apService.Gain(chainling, 1, turn, defId) > 0;
            }
            else if (triggerPoint == PassiveTriggerPoint.OnMiss)
            {
                if (defId == "precision_focus")
                {
                    float roll;
                    bool triggered = _rng.Check(0.75f, out roll);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload { procId = $"Passive.{defId}", chance = 0.75f, roll = roll, triggered = triggered },
                        chainling.instanceId,
                        new List<string> { chainling.instanceId },
                        turn);
                    if (triggered)
                        appliedAny |= _apService.Refund(chainling, 1, turn, defId) > 0;
                }
                else if (defId == "reactive_ping" && other != null && other.isAlive)
                {
                    int ping = Math.Max(1, (int)Math.Floor(other.EffectiveMaxHP * 0.05f));
                    appliedAny = ApplyPassiveStrike(chainling, other, move, ping, turn, "ping");
                }
            }
            else if (triggerPoint == PassiveTriggerPoint.AfterDamage)
            {
                if (defId == "conductive_counter" && other != null && other.isAlive && sourceMove != null &&
                    sourceMove.contactType == ContactType.Contact && damage > 0 &&
                    chainling.MarkOncePerTurn(defId, turn))
                {
                    int counter = Math.Max(1, (int)Math.Floor(damage * 0.15f));
                    appliedAny = ApplyPassiveStrike(chainling, other, move, counter, turn, "counter");
                    _apService.Gain(chainling, 1, turn, defId);
                }
            }
            else if (triggerPoint == PassiveTriggerPoint.EndOfTurn)
            {
                if (defId == "double_or_nothing")
                {
                    float roll;
                    bool gain = _rng.Check(0.60f, out roll);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload { procId = $"Passive.{defId}", chance = 0.60f, roll = roll, triggered = gain },
                        chainling.instanceId,
                        new List<string> { chainling.instanceId },
                        turn);
                    appliedAny |= gain
                        ? _apService.Gain(chainling, 1, turn, defId) > 0
                        : _apService.Drain(chainling, 1, turn, defId) > 0;
                }
                else if (defId == "meme_momentum")
                {
                    appliedAny |= ApplyMemeMomentum(chainling, move, turn);
                }
            }

            if (triggerPoint == PassiveTriggerPoint.EndOfTurn &&
                string.Equals(move.definitionId, "patience_passive", StringComparison.Ordinal))
            {
                appliedAny |= ApplyPatienceHeal(chainling, move, turn);
            }

            return appliedAny;
        }

        private static bool ShouldTriggerImplicitPassive(
            ChainlingInstance chainling,
            MoveInstance move,
            PassiveTriggerPoint triggerPoint,
            ChainlingInstance other,
            int damage,
            MoveInstance sourceMove)
        {
            if (chainling == null || move == null)
                return false;

            string defId = move.definitionId ?? string.Empty;
            switch (triggerPoint)
            {
                case PassiveTriggerPoint.TurnStart:
                    return defId == "turn_start_ap_gain";

                case PassiveTriggerPoint.OnHit:
                    return defId == "on_hit_momentum";

                case PassiveTriggerPoint.OnCrit:
                    return defId == "on_crit_bonus";

                case PassiveTriggerPoint.AfterDamage:
                    return damage > 0 && defId == "conductive_counter";
            }

            return false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Swap
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private struct ResolvedMoveModifier
        {
            public string sectionId;
            public ModifierDefinitionSO definition;
            public ModifierInstance runtime;
        }

        private enum PassiveTriggerPoint
        {
            TurnStart,
            BeforeDamage,
            AfterDamage,
            OnHit,
            OnCrit,
            OnMiss,
            OnKO,
            AllyKO,
            EndOfTurn,
            OnAllyHit,
            OnBuffExpired,
            OnAction,
        }

        private DamageModifierContext BuildDamageModifierContext(
            CombatState state,
            TeamState actingTeam,
            ChainlingInstance attacker,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            var context = new DamageModifierContext();
            if (attacker == null || move == null)
                return context;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                string section = mod.sectionId;
                if (string.IsNullOrWhiteSpace(section))
                    continue;

                ModifierTriggerCondition trigger = ResolveModifierTrigger(mod);
                if (trigger == ModifierTriggerCondition.OnLowHP && !IsLowHp(attacker, mod))
                {
                    LogModifierOutcome(mod, attacker, target, move, turn, false, "Low HP condition not met");
                    continue;
                }
                if (trigger != ModifierTriggerCondition.None &&
                    trigger != ModifierTriggerCondition.OnLowHP &&
                    trigger != ModifierTriggerCondition.OnHPBelow30 &&
                    trigger != ModifierTriggerCondition.OnAllyKO)
                {
                    continue;
                }

                bool applied = false;
                if (StartsWithSection(section, "2.1"))
                {
                    float pct = RollModifierPercent(mod, 0f);
                    context.powerMultiplier *= 1f + pct;
                    if (section.Equals("2.1-e", StringComparison.OrdinalIgnoreCase))
                        context.critChanceBonus += 0.05f;
                    applied = true;
                }
                else if (StartsWithSection(section, "2.2"))
                {
                    context.accuracyBonus += RollModifierPercent(mod, 0f);
                    if (section.Equals("2.2-d", StringComparison.OrdinalIgnoreCase))
                        context.ignoreEvasionStages = Math.Max(context.ignoreEvasionStages, 1);
                    applied = true;
                }
                else if (section.Equals("2.7-a", StringComparison.OrdinalIgnoreCase))
                {
                    int emptyBench = actingTeam != null ? actingTeam.CountEmptyBenchSlots() : 0;
                    if (emptyBench > 0)
                    {
                        context.accuracyBonus += RollModifierPercent(mod, 0.03f) * emptyBench;
                        applied = true;
                    }
                }
                else if (StartsWithSection(section, "2.3"))
                {
                    context.critChanceBonus += RollModifierPercent(mod, 0f);
                    if (section.Equals("2.3-e", StringComparison.OrdinalIgnoreCase))
                        context.critMultiplierBonus += 0.25f;
                    applied = true;
                }
                else if (section.Equals("4.1-a", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsMultiMove(move))
                    {
                        context.extraHits += 1;
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Extra hit requires MULTI move");
                    }
                }
                else if (section.Equals("4.2-a", StringComparison.OrdinalIgnoreCase))
                {
                    if (move.targeting == MoveTargeting.SingleEnemy)
                    {
                        context.bounceToOneAdjacent = true;
                        context.bouncePowerMultiplier = 0.50f;
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Bounce requires single-target enemy damage");
                    }
                }
                else if (section.Equals("4.3-a", StringComparison.OrdinalIgnoreCase))
                {
                    context.powerMultiplier *= 0.90f;
                    applied = true;
                }
                else if (section.Equals("4.3-b", StringComparison.OrdinalIgnoreCase) ||
                         section.Equals("4.8-a", StringComparison.OrdinalIgnoreCase))
                {
                    context.barrierBypassChance = Math.Max(context.barrierBypassChance, 1f);
                    applied = true;
                }
                else if (section.Equals("4.6-a", StringComparison.OrdinalIgnoreCase))
                {
                    context.barrierBypassChance = Math.Max(context.barrierBypassChance, RollModifierPercent(mod, 0f));
                    applied = true;
                }
                else if (section.Equals("4.7-a", StringComparison.OrdinalIgnoreCase))
                {
                    context.barrierPenetrationPercent = Math.Max(context.barrierPenetrationPercent, RollModifierPercent(mod, 0f));
                    applied = true;
                }
                else if (section.Equals("4.4-a", StringComparison.OrdinalIgnoreCase))
                {
                    if (move.targeting == MoveTargeting.SingleEnemy)
                    {
                        context.expandSingleTargetToAllEnemies = true;
                        context.expandedTargetPowerMultiplier = Math.Min(
                            context.expandedTargetPowerMultiplier,
                            Math.Max(0.05f, 1f - RollModifierPercent(mod, 0.35f)));
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Spread requires single-target enemy damage");
                    }
                }
                else if (section.Equals("5.1-a", StringComparison.OrdinalIgnoreCase))
                {
                    ChainlingType infusion = mod.runtime != null ? mod.runtime.infusionType : ChainlingType.None;
                    if (infusion != ChainlingType.None)
                    {
                        context.hasAttackTypeOverride = true;
                        context.attackTypeOverride = infusion;
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "No infusion type supplied");
                    }
                }
                else if (section.Equals("5.2-d", StringComparison.OrdinalIgnoreCase) && IsDamagingMove(move))
                {
                    context.inverseAttackScaling = true;
                    applied = true;
                }
                else if (StartsWithSection(section, "5.2") && IsDamagingMove(move))
                {
                    CoreStat subStat = ResolveSubstitutionStat(mod);
                    if (ShouldApplyStatSubstitution(section, attacker, move, subStat))
                    {
                        context.hasAtkStatOverride = true;
                        context.atkStatOverride = subStat;
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Stat substitution condition not met");
                    }
                }
                else if (section.Equals("6.3-a", StringComparison.OrdinalIgnoreCase))
                {
                    if (target != null && target.EffectiveMaxHP > 0 && target.currentHP * 100f / target.EffectiveMaxHP < 50f)
                    {
                        context.damageMultiplier *= 1f + RollModifierPercent(mod, 0.25f);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Target HP condition not met");
                    }
                }
                else if (section.Equals("6.3-b", StringComparison.OrdinalIgnoreCase))
                {
                    if (target != null && attacker.currentHP < target.currentHP)
                    {
                        context.damageMultiplier *= 1f + RollModifierPercent(mod, 0.25f);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Attacker HP condition not met");
                    }
                }
                else if (section.Equals("6.3-d", StringComparison.OrdinalIgnoreCase))
                {
                    TurnActionLedger ledger = EnsureTurnActionLedger(state);
                    int teamIndex = actingTeam != null ? actingTeam.teamIndex : -1;
                    if (ledger != null && ledger.DidAllyKoOccurThisTurn(teamIndex))
                    {
                        context.powerMultiplier *= 1f + RollModifierPercent(mod, 0.25f);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "No ally KO this turn");
                    }
                }
                else if (section.Equals("6.3-e", StringComparison.OrdinalIgnoreCase))
                {
                    TurnActionLedger ledger = EnsureTurnActionLedger(state);
                    if (target != null && ledger != null && ledger.DidTargetMissLastTurn(target.instanceId))
                    {
                        context.accuracyBonus += 0.20f;
                        context.powerMultiplier *= 1.15f;
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Target did not miss last turn");
                    }
                }
                else if (section.Equals("7.1-a", StringComparison.OrdinalIgnoreCase))
                {
                    int buffedPartners = CountBuffedPartners(actingTeam, attacker);
                    if (buffedPartners > 0)
                    {
                        float pct = RollModifierPercent(mod, 0.10f);
                        context.powerMultiplier *= 1f + (pct * buffedPartners);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "No buffed partner");
                    }
                }
                else if (section.Equals("7.2-a", StringComparison.OrdinalIgnoreCase))
                {
                    TurnActionLedger ledger = EnsureTurnActionLedger(state);
                    int teamIndex = actingTeam != null ? actingTeam.teamIndex : -1;
                    if (ledger != null && ledger.DidAllyUseDamagingMoveThisTurn(teamIndex, attacker.instanceId))
                    {
                        context.powerMultiplier *= 1f + RollModifierPercent(mod, 0.20f);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "No ally attack earlier this turn");
                    }
                }
                else if (section.Equals("7.2-b", StringComparison.OrdinalIgnoreCase))
                {
                    TurnActionLedger ledger = EnsureTurnActionLedger(state);
                    if (target != null && ledger != null && ledger.WasTargetHitThisTurn(target.instanceId))
                    {
                        context.powerMultiplier *= 1f + RollModifierPercent(mod, 0.45f);
                        applied = true;
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "Target was not hit earlier this turn");
                    }
                }
                else if (section.Equals("10.1-b", StringComparison.OrdinalIgnoreCase))
                {
                    context.powerMultiplier *= 1f + RollModifierPercent(mod, 0.25f);
                    context.accuracyBonus -= 0.15f;
                    applied = true;
                }
                else if (section.Equals("10.1-c", StringComparison.OrdinalIgnoreCase))
                {
                    float pct = RollModifierPercent(mod, 0.20f);
                    context.powerMultiplier *= _rng.Check(0.5f) ? 1f + pct : Math.Max(0.1f, 1f - pct);
                    applied = true;
                }
                else if (section.Equals("10.2-a", StringComparison.OrdinalIgnoreCase))
                {
                    context.powerMultiplier *= 1f + RollModifierPercent(mod, 0.03f);
                    applied = true;
                }

                if (applied)
                {
                    context.AddApplied(section);
                }
            }

            if (context.expandSingleTargetToAllEnemies && context.expandedTargetPowerMultiplier > 0f && context.expandedTargetPowerMultiplier < 1f)
                context.powerMultiplier *= context.expandedTargetPowerMultiplier;

            return context;
        }

        private bool ApplyPassiveStrike(
            ChainlingInstance source,
            ChainlingInstance victim,
            MoveInstance passive,
            int amount,
            int turn,
            string verb)
        {
            if (source == null || victim == null || passive == null || amount <= 0 || !victim.isAlive)
                return false;

            victim.currentHP = Math.Max(0, victim.currentHP - amount);
            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.PassiveDamageDealt,
                turn = turn,
                sourceId = source.instanceId,
                targetId = victim.instanceId,
                moveId = passive.instanceId,
                intValue = amount,
                message = $"{victim.displayName} took {amount} {verb} damage from {source.displayName}'s {passive.displayName}.",
            });

            if (victim.currentHP > 0)
                return true;

            victim.currentHP = 0;
            victim.isAlive = false;
            TeamState victimTeam = FindTeamContaining(_currentState, victim);
            TeamState sourceTeam = FindTeamContaining(_currentState, source);
            int teamIndex = victimTeam != null ? victimTeam.teamIndex : -1;
            EnsureTurnActionLedger(_currentState)?.RecordKo(victim.instanceId, teamIndex);
            if (sourceTeam != null)
            {
                ProcessArmedWindowsForTeam(sourceTeam, victim, ArmedWindowTrigger.FoeKO, turn);
                CheckPassiveTriggersForTeam(sourceTeam, turn, PassiveTriggerPoint.OnKO, victim, passive);
            }
            if (victimTeam != null)
            {
                CheckPassiveTriggersForTeam(
                    victimTeam,
                    turn,
                    PassiveTriggerPoint.AllyKO,
                    victim,
                    passive,
                    exclude: victim);
            }
            _log.LogKO(
                turn,
                victim.instanceId,
                source.instanceId,
                passive.instanceId,
                teamIndex,
                FindSlotOf(victimTeam, victim));
            return true;
        }

        private bool ApplyMemeMomentum(ChainlingInstance chainling, MoveInstance move, int turn)
        {
            TurnActionLedger ledger = EnsureTurnActionLedger(_currentState);
            bool landedHit = false;
            if (ledger != null && ledger.actorTargetsHitThisTurn != null)
            {
                for (int i = 0; i < ledger.actorTargetsHitThisTurn.Count; i++)
                {
                    if (string.Equals(ledger.actorTargetsHitThisTurn[i].actorId, chainling.instanceId, StringComparison.Ordinal))
                    {
                        landedHit = true;
                        break;
                    }
                }
            }

            bool attacked = ledger != null && ledger.DidActorUseDamagingMoveThisTurn(chainling.instanceId);
            if (!landedHit && !attacked)
                return false;

            int stageDelta = landedHit ? 2 : -1;
            bool stageChanged = ApplyMemeStage(chainling, CoreStat.MeleeAtk, stageDelta, turn);
            stageChanged |= ApplyMemeStage(chainling, CoreStat.RangedAtk, stageDelta, turn);

            bool apChanged = landedHit
                ? _apService.Gain(chainling, 2, turn, move.definitionId) > 0
                : _apService.Drain(chainling, 1, turn, move.definitionId) > 0;
            _log.LogMessage(
                CombatEventType.SystemMessage,
                landedHit
                    ? $"{chainling.displayName}'s {move.displayName} surges after a landed hit."
                    : $"{chainling.displayName}'s {move.displayName} slips after a miss.",
                turn);
            return stageChanged || apChanged;
        }

        private bool ApplyMemeStage(ChainlingInstance chainling, CoreStat stat, int stageDelta, int turn)
        {
            int oldStage = chainling.GetStage(stat);
            chainling.SetStage(stat, oldStage + stageDelta);
            int appliedDelta = chainling.GetStage(stat) - oldStage;
            if (appliedDelta == 0)
                return false;
            _log.LogStageChange(turn, chainling.instanceId, stat, appliedDelta, chainling.GetStage(stat));
            return true;
        }

        private void TryImmutableLedgerAp(ChainlingInstance defender, int turn)
        {
            if (defender?.moves == null)
                return;
            for (int i = 0; i < defender.moves.Count; i++)
            {
                MoveInstance move = defender.moves[i];
                if (move?.category != MoveCategory.Passive ||
                    !string.Equals(move.definitionId, "immutable_ledger", StringComparison.Ordinal))
                    continue;
                _apService.Gain(defender, 1, turn, move.definitionId);
                return;
            }
        }

        private bool ApplyPatienceHeal(ChainlingInstance chainling, MoveInstance move, int turn)
        {
            TurnActionLedger ledger = EnsureTurnActionLedger(_currentState);
            if (ledger != null && ledger.DidActorUseDamagingMoveThisTurn(chainling.instanceId))
                return false;
            if (chainling.currentHP <= 0)
                return false;

            int heal = Math.Max(1, (int)(chainling.EffectiveMaxHP * 0.05f));
            int before = chainling.currentHP;
            chainling.currentHP = Math.Min(chainling.EffectiveMaxHP, chainling.currentHP + heal);
            int actual = chainling.currentHP - before;
            if (actual <= 0)
                return false;
            LogHealApplied(turn, chainling, chainling, move, actual, move.displayName);
            return true;
        }

        private void ApplyDefensivePassiveMitigation(
            DamageModifierContext context,
            ChainlingInstance defender,
            MoveInstance incomingMove,
            int turn)
        {
            if (context == null || defender == null || incomingMove == null || defender.moves == null)
                return;

            for (int i = 0; i < defender.moves.Count; i++)
            {
                MoveInstance passive = defender.moves[i];
                if (passive == null || passive.category != MoveCategory.Passive)
                    continue;

                string defId = passive.definitionId ?? string.Empty;
                float mitigation = 0f;
                bool hasAuthoredMitigation = false;
                if (passive.utilityEffects != null)
                {
                    for (int e = 0; e < passive.utilityEffects.Count; e++)
                    {
                        UtilityEffectPayload effect = passive.utilityEffects[e];
                        if (effect == null || effect.effectType != UtilityEffectType.IncomingDamageMitigation)
                            continue;
                        hasAuthoredMitigation = true;
                        if (!MatchesContactFilter(effect.contactFilter, incomingMove.contactType))
                            continue;
                        float authored = effect.magnitude > 1f ? effect.magnitude / 100f : effect.magnitude;
                        mitigation = 1f - ((1f - mitigation) * (1f - Math.Max(0f, authored)));
                    }
                }

                // Backward-compatible fallback for assets not yet reimported from CSV.
                bool ledger = defId == "immutable_ledger";
                if (ledger && defender.hitsTakenThisTurn > 0)
                    continue;

                if (!hasAuthoredMitigation)
                {
                    if (defId == "defensive_reflex")
                        mitigation = 0.20f;
                    else if (ledger)
                        mitigation = 0.20f;
                    else if (defId == "firewall_passive" && incomingMove.contactType == ContactType.Projectile)
                        mitigation = 0.25f;
                }

                if (mitigation <= 0f)
                    continue;

                context.additionalMitigation = 1f - ((1f - context.additionalMitigation) * (1f - mitigation));
                _log.Log(new CombatEvent
                {
                    eventType = CombatEventType.PassiveTriggered,
                    turn = turn,
                    sourceId = defender.instanceId,
                    moveId = passive.instanceId,
                    message = $"{defender.displayName}'s {passive.displayName} triggered!",
                });
            }
        }

        private static bool MatchesContactFilter(UtilityContactFilter filter, ContactType contactType)
        {
            switch (filter)
            {
                case UtilityContactFilter.Contact: return contactType == ContactType.Contact;
                case UtilityContactFilter.Projectile: return contactType == ContactType.Projectile;
                case UtilityContactFilter.None: return contactType == ContactType.None;
                default: return true;
            }
        }

        private void ApplyChemistryAwareModifierBonuses(
            DamageModifierContext context,
            ChainlingInstance attacker,
            ChainlingInstance target,
            MoveInstance move,
            ChemistryResult chemResult,
            int turn)
        {
            if (context == null || move == null)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (mod.sectionId.Equals("2.6-a", StringComparison.OrdinalIgnoreCase))
                {
                    string marker = "2.6-a:chem";
                    if (context.appliedModifierIds.Contains(marker))
                        continue;

                    if (chemResult.chemAdvCount > 0)
                    {
                        context.critChanceBonus += RollModifierPercent(mod, 0.08f);
                        context.AddApplied(marker);
                    }
                    else
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "No chemistry advantage");
                    }
                }
                else if (mod.sectionId.Equals("10.1-a", StringComparison.OrdinalIgnoreCase))
                {
                    string marker = "10.1-a:inverseCrit";
                    if (context.appliedModifierIds.Contains(marker))
                        continue;

                    float currentCrit = DamageCalculator.ComputeCritRate(attacker, move, context);
                    context.critChanceBonus += Math.Max(0f, 0.5f - currentCrit);
                    context.AddApplied(marker);
                }
            }
        }

        private void EmitDamageContextModifierOutcomes(
            DamageModifierContext context,
            ChainlingInstance attacker,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            bool isCrit,
            bool bypassBarrier,
            int penetrationDamage,
            int actualDamage)
        {
            if (context == null || move == null || context.appliedModifierIds == null || context.appliedModifierIds.Count == 0)
                return;

            List<ResolvedMoveModifier> modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < context.appliedModifierIds.Count; i++)
            {
                string marker = context.appliedModifierIds[i];
                string section = NormalizeModifierOutcomeMarker(marker);
                if (string.IsNullOrWhiteSpace(section))
                    continue;

                if (StartsWithSection(section, "2.3") && !isCrit)
                    continue;
                if ((section.Equals("2.6-a", StringComparison.OrdinalIgnoreCase) ||
                     section.Equals("10.1-a", StringComparison.OrdinalIgnoreCase)) && !isCrit)
                    continue;
                if (section.Equals("4.3-a", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (section.Equals("4.3-b", StringComparison.OrdinalIgnoreCase) && !bypassBarrier)
                    continue;
                if (section.Equals("4.8-a", StringComparison.OrdinalIgnoreCase) && !bypassBarrier)
                    continue;
                if (section.Equals("4.6-a", StringComparison.OrdinalIgnoreCase) && !bypassBarrier)
                    continue;
                if (section.Equals("4.7-a", StringComparison.OrdinalIgnoreCase) && penetrationDamage <= 0)
                    continue;
                if (actualDamage <= 0 && !section.Equals("4.3-b", StringComparison.OrdinalIgnoreCase) &&
                    !section.Equals("4.8-a", StringComparison.OrdinalIgnoreCase) &&
                    !section.Equals("4.6-a", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!context.TryMarkOutcomeEmitted(marker))
                    continue;

                ResolvedMoveModifier mod = FindResolvedModifier(modifiers, section);
                string displayText = ResolveModifierDisplayText(mod);
                ModifierPopupMoment moment = isCrit && (StartsWithSection(section, "2.3") ||
                                                        section.Equals("2.6-a", StringComparison.OrdinalIgnoreCase) ||
                                                        section.Equals("10.1-a", StringComparison.OrdinalIgnoreCase))
                    ? ModifierPopupMoment.OnCritApplied
                    : StartsWithSection(section, "7.")
                        ? ModifierPopupMoment.OnSynergyMet
                        : ModifierPopupMoment.OnHitApplied;

                if (section.Equals("4.3-b", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("4.8-a", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("4.6-a", StringComparison.OrdinalIgnoreCase))
                    displayText = "BYPASS";

                LogModifierOutcome(
                    mod,
                    attacker,
                    target,
                    move,
                    turn,
                    true,
                    "Damage modifier applied after hit proof",
                    displayText,
                    attacker != null ? attacker.instanceId : string.Empty,
                    moment);
            }
        }

        private static string NormalizeModifierOutcomeMarker(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return string.Empty;
            int separator = marker.IndexOf(':');
            return separator >= 0 ? marker.Substring(0, separator) : marker;
        }

        private static ResolvedMoveModifier FindResolvedModifier(List<ResolvedMoveModifier> modifiers, string sectionId)
        {
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (string.Equals(modifiers[i].sectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                        return modifiers[i];
                }
            }

            return new ResolvedMoveModifier
            {
                sectionId = sectionId,
            };
        }

        private void ProcessAfterDamageModifiers(
            ChainlingInstance attacker,
            ChainlingInstance target,
            MoveInstance move,
            int damage,
            bool isCrit,
            int turn)
        {
            if (attacker == null || target == null || move == null || damage <= 0)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                ModifierTriggerCondition trigger = ResolveModifierTrigger(mod);
                if (trigger == ModifierTriggerCondition.OnHit)
                {
                    if (!CheckModifierTriggerChance(mod))
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "On-hit proc failed");
                        continue;
                    }

                    if (IsOnHitLifestealModifier(mod))
                    {
                        int heal = Math.Max(1, (int)Math.Floor(damage * RollModifierPercent(mod, 0.05f)));
                        int before = attacker.currentHP;
                        attacker.currentHP = Math.Min(attacker.EffectiveMaxHP, attacker.currentHP + heal);
                        int actual = attacker.currentHP - before;
                        if (actual > 0)
                            LogHealApplied(turn, attacker, attacker, move, actual, $"modifier {mod.sectionId}");
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "On-hit heal applied",
                            ResolveModifierDisplayText(mod), attacker.instanceId, ModifierPopupMoment.OnHitApplied);
                    }
                    else if (IsPostCastBarrierModifier(mod))
                    {
                        ApplyPostCastBarrierModifier(attacker, move, mod, turn);
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "Post-cast barrier granted",
                            ResolveModifierDisplayText(mod), attacker.instanceId, ModifierPopupMoment.OnHitApplied);
                    }
                    else if (mod.sectionId.Equals("6.1-b", StringComparison.OrdinalIgnoreCase))
                    {
                        _apService.Steal(attacker, target, 1, turn, $"Modifier.{mod.sectionId}");
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "On-hit AP drain applied",
                            ResolveModifierDisplayText(mod), target.instanceId, ModifierPopupMoment.OnHitApplied);
                    }
                    else if (mod.sectionId.Equals("6.1-c", StringComparison.OrdinalIgnoreCase))
                    {
                        _statusProcessor.ApplyStatus(target, StatusId.Staggered, 1, move.instanceId, turn);
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "Staggered applied",
                            ResolveModifierDisplayText(mod), target.instanceId, ModifierPopupMoment.OnHitApplied);
                    }
                }
                else if (trigger == ModifierTriggerCondition.OnCrit && isCrit)
                {
                    if (!CheckModifierTriggerChance(mod))
                    {
                        LogModifierOutcome(mod, attacker, target, move, turn, false, "On-crit proc failed");
                        continue;
                    }

                    if (mod.sectionId.Equals("6.2-a", StringComparison.OrdinalIgnoreCase))
                    {
                        _apService.Gain(attacker, 1, turn, $"Modifier.{mod.sectionId}");
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "Crit AP gained",
                            ResolveModifierDisplayText(mod), attacker.instanceId, ModifierPopupMoment.OnCritApplied);
                    }
                    else if (mod.sectionId.Equals("6.2-b", StringComparison.OrdinalIgnoreCase))
                    {
                        DamageCalculator.ResolveDamageStats(move, out _, out CoreStat defStat);
                        int oldStage = target.GetStage(defStat);
                        target.SetStage(defStat, oldStage - 2);
                        _log.LogStageChange(turn, target.instanceId, defStat, target.GetStage(defStat) - oldStage, target.GetStage(defStat));
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "Crit defense stage drop applied",
                            ResolveModifierDisplayText(mod), target.instanceId, ModifierPopupMoment.OnCritApplied);
                    }
                    else if (mod.sectionId.Equals("6.2-c", StringComparison.OrdinalIgnoreCase))
                    {
                        _statusProcessor.ApplyStatus(attacker, StatusId.Surging, 1, move.instanceId, turn);
                        LogModifierOutcome(mod, attacker, target, move, turn, true, "Surging applied",
                            ResolveModifierDisplayText(mod), attacker.instanceId, ModifierPopupMoment.OnCritApplied);
                    }
                }
            }
        }

        private void ProcessLowHpModifierWindows(ChainlingInstance actor, MoveInstance move, int turn)
        {
            if (actor == null || move == null || !actor.isAlive)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (!mod.sectionId.Equals("6.3-c", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsLowHp(actor, mod))
                {
                    LogModifierOutcome(mod, actor, actor, move, turn, false, "Low HP mitigation condition not met");
                    continue;
                }

                float mitigation = RollModifierPercent(mod, 0.20f);
                if (mitigation <= 0f)
                    continue;

                actor.AddMitigationSource(mitigation, RestOfTurnDuration, $"Modifier.{mod.sectionId}");
                LogModifierOutcome(
                    mod,
                    actor,
                    actor,
                    move,
                    turn,
                    true,
                    "Low HP mitigation applied",
                    "MITIGATE",
                    actor.instanceId,
                    ModifierPopupMoment.OnProcSucceeded);
            }
        }

        private void LogModifierOutcome(
            ResolvedMoveModifier mod,
            ChainlingInstance actor,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            bool triggered,
            string reason,
            string displayText = null,
            string anchorChainlingId = null,
            ModifierPopupMoment moment = ModifierPopupMoment.Unknown)
        {
            if (string.IsNullOrWhiteSpace(mod.sectionId))
                return;

            string sourceId = actor != null ? actor.instanceId : string.Empty;
            string targetId = target != null ? target.instanceId : string.Empty;
            string moveId = move != null ? move.instanceId : string.Empty;
            ModifierCategory category = ResolveModifierCategory(mod);
            _log.Log(ModifierOutcomePayload.CreateEvent(
                turn,
                mod.sectionId,
                category,
                string.IsNullOrWhiteSpace(displayText) ? ResolveModifierDisplayText(mod) : displayText,
                string.IsNullOrWhiteSpace(anchorChainlingId) ? sourceId : anchorChainlingId,
                moment == ModifierPopupMoment.Unknown ? ResolveModifierMoment(mod) : moment,
                triggered,
                reason,
                sourceId,
                targetId,
                moveId));
        }

        private static ModifierCategory ResolveModifierCategory(ResolvedMoveModifier mod)
        {
            if (mod.definition != null)
                return mod.definition.category;
            if (mod.runtime != null)
                return mod.runtime.category;

            string section = mod.sectionId ?? string.Empty;
            if (StartsWithSection(section, "1.") || StartsWithSection(section, "4.3"))
                return ModifierCategory.INIT;
            if (StartsWithSection(section, "2.") || StartsWithSection(section, "10."))
                return ModifierCategory.POT;
            if (StartsWithSection(section, "3."))
                return ModifierCategory.EFF;
            if (StartsWithSection(section, "4."))
                return ModifierCategory.AUG;
            if (StartsWithSection(section, "5."))
                return ModifierCategory.TRX;
            if (StartsWithSection(section, "6."))
                return ModifierCategory.TRG;
            if (StartsWithSection(section, "7."))
                return ModifierCategory.SYN;
            return ModifierCategory.POT;
        }

        private static ModifierPopupMoment ResolveModifierMoment(ResolvedMoveModifier mod)
        {
            ModifierTriggerCondition trigger = ResolveModifierTrigger(mod);
            if (trigger == ModifierTriggerCondition.OnCrit)
                return ModifierPopupMoment.OnCritApplied;
            if (trigger == ModifierTriggerCondition.OnHit)
                return ModifierPopupMoment.OnHitApplied;
            if (trigger == ModifierTriggerCondition.OnKO)
                return ModifierPopupMoment.OnKoTrigger;
            if (trigger == ModifierTriggerCondition.OnEscape)
                return ModifierPopupMoment.OnEscapeTrigger;
            if (trigger == ModifierTriggerCondition.OnSwapIn || trigger == ModifierTriggerCondition.OnSwapOut)
                return ModifierPopupMoment.OnSwapTrigger;
            if (trigger == ModifierTriggerCondition.OnStatusApplied)
                return ModifierPopupMoment.OnStatusApplied;

            string section = mod.sectionId ?? string.Empty;
            if (StartsWithSection(section, "1.") || StartsWithSection(section, "4.3"))
                return ModifierPopupMoment.OnPriorityApplied;
            if (StartsWithSection(section, "7."))
                return ModifierPopupMoment.OnSynergyMet;
            return ModifierPopupMoment.OnProcSucceeded;
        }

        private static string ResolveModifierDisplayText(ResolvedMoveModifier mod)
        {
            string section = mod.sectionId ?? string.Empty;

            if (StartsWithSection(section, "1.") || StartsWithSection(section, "4.3"))
                return "PRIORITY";
            if (StartsWithSection(section, "2.1") || section.Equals("10.1-b", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("10.1-c", StringComparison.OrdinalIgnoreCase) || section.Equals("10.2-a", StringComparison.OrdinalIgnoreCase))
                return "POWER";
            if (StartsWithSection(section, "2.2") || section.Equals("2.7-a", StringComparison.OrdinalIgnoreCase))
                return "ACCURACY";
            if (StartsWithSection(section, "2.3") || section.Equals("2.6-a", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("10.1-a", StringComparison.OrdinalIgnoreCase))
                return "CRIT";
            if (section.Equals("2.4-a", StringComparison.OrdinalIgnoreCase))
                return "STATUS+";
            if (StartsWithSection(section, "2.5"))
                return "DURATION";
            if (StartsWithSection(section, "3.1"))
                return "REFUND";
            if (StartsWithSection(section, "3."))
                return "BONUS";
            if (section.Equals("4.1-a", StringComparison.OrdinalIgnoreCase))
                return "EXTRA HIT";
            if (section.Equals("4.2-a", StringComparison.OrdinalIgnoreCase))
                return "BOUNCE";
            if (section.Equals("4.4-a", StringComparison.OrdinalIgnoreCase))
                return "SPREAD";
            if (section.Equals("4.5-a", StringComparison.OrdinalIgnoreCase))
                return "+1 AP";
            if (section.Equals("4.6-a", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("4.8-a", StringComparison.OrdinalIgnoreCase))
                return "BYPASS";
            if (section.Equals("4.7-a", StringComparison.OrdinalIgnoreCase))
                return "PIERCE";
            if (section.Equals("5.1-a", StringComparison.OrdinalIgnoreCase))
                return "TYPE";
            if (StartsWithSection(section, "5.2"))
                return ResolveSubstitutionStat(mod).ToString().ToUpperInvariant();
            if (IsOnHitLifestealModifier(mod) || section.Equals("6.8-a", StringComparison.OrdinalIgnoreCase))
                return "HEAL";
            if (IsPostCastBarrierModifier(mod))
                return "BARRIER";
            if (StartsWithSection(section, "8.1"))
                return "MAXHP";
            if (section.Equals("6.1-b", StringComparison.OrdinalIgnoreCase))
                return "AP DRAIN";
            if (section.Equals("6.1-c", StringComparison.OrdinalIgnoreCase))
                return "STAGGER";
            if (section.Equals("6.2-a", StringComparison.OrdinalIgnoreCase))
                return "+1 AP";
            if (section.Equals("6.2-b", StringComparison.OrdinalIgnoreCase))
                return "DEF -2";
            if (section.Equals("6.2-c", StringComparison.OrdinalIgnoreCase))
                return "SURGE";
            if (section.Equals("6.5-a", StringComparison.OrdinalIgnoreCase))
                return "KO AP";
            if (section.Equals("6.5-b", StringComparison.OrdinalIgnoreCase))
                return "SPEED +1";
            if (section.Equals("6.6-a", StringComparison.OrdinalIgnoreCase))
                return "SLOW";
            if (section.Equals("6.6-b", StringComparison.OrdinalIgnoreCase))
                return "CHIP";
            if (section.Equals("6.8-b", StringComparison.OrdinalIgnoreCase))
                return "PRE-EMPT";
            if (section.Equals("6.8-c", StringComparison.OrdinalIgnoreCase))
                return "RIPOSTE";
            if (section.Equals("6.8-d", StringComparison.OrdinalIgnoreCase))
                return "MIRROR";
            if (section.Equals("6.8-e", StringComparison.OrdinalIgnoreCase))
                return "WARD";
            if (section.Equals("6.8-f", StringComparison.OrdinalIgnoreCase))
                return "CLEANSE";
            if (section.Equals("6.8-g", StringComparison.OrdinalIgnoreCase))
                return "SPOTLIGHT";
            if (StartsWithSection(section, "6."))
                return "TRIGGER";
            if (section.Equals("7.3-a", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("7.1-d", StringComparison.OrdinalIgnoreCase))
                return "SHARE";
            if (StartsWithSection(section, "7."))
                return "SYNC";
            if (section.Equals("10.2-b", StringComparison.OrdinalIgnoreCase))
                return "STATS";

            return "MOD";
        }

        private void ProcessBattleStartModifiers(CombatState state)
        {
            if (state == null)
                return;

            ProcessBattleStartTeam(state.team0, state.team1, 0);
            ProcessBattleStartTeam(state.team1, state.team0, 0);
        }

        private void ProcessBattleStartTeam(TeamState team, TeamState opposing, int turn)
        {
            if (team?.chainlings == null)
                return;

            for (int c = 0; c < team.chainlings.Count; c++)
            {
                ChainlingInstance actor = team.chainlings[c];
                if (actor == null || !actor.isAlive || actor.moves == null)
                    continue;

                for (int m = 0; m < actor.moves.Count; m++)
                {
                    MoveInstance move = actor.moves[m];
                    if (move == null)
                        continue;

                    var modifiers = ResolveMoveModifiers(move);
                    for (int i = 0; i < modifiers.Count; i++)
                    {
                        ResolvedMoveModifier mod = modifiers[i];
                        if (mod.sectionId.Equals("6.8-b", StringComparison.OrdinalIgnoreCase))
                        {
                            if (RollPassAChance(mod, actor, move, turn, 0.08f, "Pre-emptive"))
                                TryCastMoveFromModifier(actor, move, opposing, turn, mod, "Pre-emptive cast");
                        }
                        else if (mod.sectionId.Equals("6.8-e", StringComparison.OrdinalIgnoreCase) &&
                                 move.category == MoveCategory.Passive)
                        {
                            ApplySparseWard(actor, team, move, mod, turn);
                        }
                        else if (mod.sectionId.Equals("6.8-f", StringComparison.OrdinalIgnoreCase) &&
                                 move.category == MoveCategory.Passive)
                        {
                            TryCleanseSoft(actor, move, mod, turn);
                        }
                    }
                }
            }
        }

        private void ProcessRiposteModifiers(
            ChainlingInstance defender,
            ChainlingInstance attacker,
            MoveInstance incomingMove,
            int damage,
            int turn)
        {
            if (defender == null || !defender.isAlive || damage <= 0 || defender.moves == null)
                return;

            TeamState opposing = FindTeamContaining(_currentState, attacker);
            for (int m = 0; m < defender.moves.Count; m++)
            {
                MoveInstance move = defender.moves[m];
                if (move == null)
                    continue;

                var modifiers = ResolveMoveModifiers(move);
                for (int i = 0; i < modifiers.Count; i++)
                {
                    ResolvedMoveModifier mod = modifiers[i];
                    if (!mod.sectionId.Equals("6.8-c", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (RollPassAChance(mod, defender, move, turn, 0.08f, "Riposte"))
                        TryCastMoveFromModifier(defender, move, opposing, turn, mod, "Riposte cast");
                }
            }
        }

        private void ProcessPassAOnUseModifiers(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            if (source == null || move == null)
                return;

            TeamState sourceTeam = FindTeamContaining(_currentState, source);
            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (mod.sectionId.Equals("6.8-a", StringComparison.OrdinalIgnoreCase))
                    ApplyPatchKitHeal(source, move, mod, turn);
                else if (mod.sectionId.Equals("6.8-e", StringComparison.OrdinalIgnoreCase))
                    ApplySparseWard(source, sourceTeam, move, mod, turn);
                else if (mod.sectionId.Equals("6.8-f", StringComparison.OrdinalIgnoreCase))
                    TryCleanseSoft(source, move, mod, turn);
                else if (mod.sectionId.Equals("6.8-g", StringComparison.OrdinalIgnoreCase))
                    ApplySpotlight(source, sourceTeam, move, mod, turn);
            }
        }

        private void ProcessSharedBuffModifiers(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            if (source == null || move == null)
                return;

            var modifiers = ResolveMoveModifiers(move);
            ResolvedMoveModifier shared = default;
            bool hasShared = false;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].sectionId.Equals("7.3-a", StringComparison.OrdinalIgnoreCase) ||
                    modifiers[i].sectionId.Equals("7.1-d", StringComparison.OrdinalIgnoreCase))
                {
                    shared = modifiers[i];
                    hasShared = true;
                    break;
                }
            }

            if (!hasShared)
                return;

            TeamState team = FindTeamContaining(_currentState, source);
            ChainlingInstance ally = FindAlly(team, source);
            if (ally == null || !ally.isAlive)
            {
                LogModifierOutcome(shared, source, source, move, turn, false, "No ally for shared buff");
                return;
            }

            bool copied = false;
            if (move.statusEffects != null)
            {
                for (int i = 0; i < move.statusEffects.Count; i++)
                {
                    var effect = move.statusEffects[i];
                    bool selfTargeted = effect.targetOverride == MoveTargeting.Self ||
                                        move.targeting == MoveTargeting.Self;
                    if (!selfTargeted)
                        continue;

                    StatusDefinitionSO def = _statusCatalog != null ? _statusCatalog.Get(effect.statusId) : null;
                    if (def == null || !def.isBuff)
                        continue;

                    _statusProcessor.ApplyStatus(ally, effect.statusId, Math.Max(1, effect.duration), move.instanceId, turn);
                    copied = true;
                }
            }

            if (move.stageChanges != null)
            {
                for (int i = 0; i < move.stageChanges.Count; i++)
                {
                    var change = move.stageChanges[i];
                    if (!change.targetsSelf || change.stages == 0)
                        continue;
                    int oldStage = ally.GetStage(change.stat);
                    ally.SetStage(change.stat, oldStage + change.stages);
                    _log.LogStageChange(turn, ally.instanceId, change.stat, change.stages, ally.GetStage(change.stat));
                    copied = true;
                }
            }

            LogModifierOutcome(
                shared,
                source,
                ally,
                move,
                turn,
                copied,
                copied ? "Self-buff copied to ally" : "No self-buff to share");
        }

        private void ApplyPatchKitHeal(
            ChainlingInstance source,
            MoveInstance move,
            ResolvedMoveModifier mod,
            int turn)
        {
            float pct = RollModifierPercent(mod, 0.08f);
            int healAmount = Math.Max(1, (int)(source.EffectiveMaxHP * pct));
            int before = source.currentHP;
            source.currentHP = Math.Min(source.EffectiveMaxHP, source.currentHP + healAmount);
            int actual = source.currentHP - before;
            if (actual > 0)
                LogHealApplied(turn, source, source, move, actual, $"modifier {mod.sectionId}");
            LogModifierOutcome(mod, source, source, move, turn, actual > 0, "Patch Kit heal");
        }

        private void ApplySparseWard(
            ChainlingInstance actor,
            TeamState team,
            MoveInstance move,
            ResolvedMoveModifier mod,
            int turn)
        {
            int emptyBench = team != null ? team.CountEmptyBenchSlots() : 0;
            if (emptyBench <= 0)
            {
                LogModifierOutcome(mod, actor, actor, move, turn, false, "No empty bench slots");
                return;
            }

            float perSlot = RollModifierPercent(mod, 0.05f);
            float mitigation = perSlot * emptyBench;
            if (mitigation <= 0f)
                return;

            actor.AddMitigationSource(mitigation, RestOfTurnDuration, $"Modifier.{mod.sectionId}");
            LogModifierOutcome(
                mod,
                actor,
                actor,
                move,
                turn,
                true,
                $"Sparse Ward +{mitigation * 100f:F1}% from {emptyBench} empty bench slot(s)");
        }

        private void TryCleanseSoft(
            ChainlingInstance actor,
            MoveInstance move,
            ResolvedMoveModifier mod,
            int turn)
        {
            if (!RollPassAChance(mod, actor, move, turn, 0.08f, "Cleanse Soft"))
                return;

            int cleansed = _statusProcessor.Cleanse(actor, 1, turn);
            LogModifierOutcome(
                mod,
                actor,
                actor,
                move,
                turn,
                cleansed > 0,
                cleansed > 0 ? "Cleared 1 self debuff" : "No self debuff to clear");
        }

        private void ApplySpotlight(
            ChainlingInstance source,
            TeamState team,
            MoveInstance move,
            ResolvedMoveModifier mod,
            int turn)
        {
            ChainlingInstance ally = FindAlly(team, source);
            if (ally == null || !ally.isAlive)
            {
                LogModifierOutcome(mod, source, source, move, turn, false, "No ally for Spotlight");
                return;
            }

            float accuracy = RollModifierPercent(mod, 0.08f);
            if (accuracy <= 0f)
                return;

            ally.AddAccuracySource(accuracy, 1, $"Modifier.{mod.sectionId}");
            LogModifierOutcome(mod, source, ally, move, turn, true, "Spotlight accuracy granted");
        }

        private bool TryReflectIncomingEffect(ChainlingInstance defender, ChainlingInstance attacker, int turn)
        {
            if (ProcessArmedWindows(
                    defender,
                    FindTeamContaining(_currentState, defender),
                    attacker,
                    ArmedWindowTrigger.IncomingDebuff,
                    turn))
            {
                return true;
            }

            return TryStickyMirrorStatusReflect(defender, attacker, turn);
        }

        private bool TryStickyMirrorStatusReflect(
            ChainlingInstance defender,
            ChainlingInstance attacker,
            int turn)
        {
            if (defender?.moves == null)
                return false;

            for (int m = 0; m < defender.moves.Count; m++)
            {
                MoveInstance move = defender.moves[m];
                if (move == null)
                    continue;

                var modifiers = ResolveMoveModifiers(move);
                for (int i = 0; i < modifiers.Count; i++)
                {
                    ResolvedMoveModifier mod = modifiers[i];
                    if (!mod.sectionId.Equals("6.8-d", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (RollPassAChance(mod, defender, move, turn, 0.08f, "Sticky Mirror"))
                    {
                        LogModifierOutcome(mod, defender, attacker, move, turn, true, "Status mirror");
                        return true;
                    }
                }
            }

            return false;
        }

        private int ApplyStickyMirrorReflectBoost(ChainlingInstance defender, int reflectedDamage)
        {
            if (defender == null || reflectedDamage <= 0)
                return reflectedDamage;

            float boost = 0f;
            if (defender.moves != null)
            {
                for (int m = 0; m < defender.moves.Count; m++)
                {
                    MoveInstance move = defender.moves[m];
                    if (move == null)
                        continue;
                    var modifiers = ResolveMoveModifiers(move);
                    for (int i = 0; i < modifiers.Count; i++)
                    {
                        if (!modifiers[i].sectionId.Equals("6.8-d", StringComparison.OrdinalIgnoreCase))
                            continue;
                        boost = Math.Max(boost, RollModifierPercent(modifiers[i], 0.08f));
                    }
                }
            }

            if (boost <= 0f)
                return reflectedDamage;

            return Math.Max(reflectedDamage, (int)Math.Floor(reflectedDamage * (1f + boost)));
        }

        private bool RollPassAChance(
            ResolvedMoveModifier mod,
            ChainlingInstance actor,
            MoveInstance move,
            int turn,
            float fallbackPercent,
            string label)
        {
            float chance = RollModifierPercent(mod, fallbackPercent);
            float roll;
            bool triggered = _rng.Check(chance, out roll);
            _log.LogForensic(
                CombatForensicEventType.ProcRoll,
                new ProcRollPayload
                {
                    procId = $"PassA.{label}.{move.instanceId}.{mod.sectionId}",
                    chance = chance,
                    roll = roll,
                    triggered = triggered,
                },
                actor != null ? actor.instanceId : string.Empty,
                actor != null ? new List<string> { actor.instanceId } : null,
                turn);
            if (!triggered)
                LogModifierOutcome(mod, actor, actor, move, turn, false, $"{label} chance failed");
            return triggered;
        }

        private bool TryCastMoveFromModifier(
            ChainlingInstance actor,
            MoveInstance move,
            TeamState opposing,
            int turn,
            ResolvedMoveModifier mod,
            string reason)
        {
            if (_passAReactiveCastInProgress || actor == null || move == null || !actor.isAlive)
                return false;

            // Fail closed: Pass A auto-cast hosts are Util/Sup/Pas only.
            if (IsDamagingMove(move))
            {
                LogModifierOutcome(mod, actor, actor, move, turn, false, "Auto-cast skipped damaging host");
                return false;
            }

            _passAReactiveCastInProgress = true;
            try
            {
                TeamState acting = FindTeamContaining(_currentState, actor);
                ChainlingInstance selected = ResolvePassACastTarget(acting, opposing, actor, move);
                ApplyUtilityEffects(actor, selected ?? actor, move, turn, true, opposing);
                LogModifierOutcome(mod, actor, selected ?? actor, move, turn, true, reason);
                return true;
            }
            finally
            {
                _passAReactiveCastInProgress = false;
            }
        }

        private ChainlingInstance ResolvePassACastTarget(
            TeamState acting,
            TeamState opposing,
            ChainlingInstance actor,
            MoveInstance move)
        {
            if (move == null)
                return actor;

            switch (move.targeting)
            {
                case MoveTargeting.Self:
                case MoveTargeting.AllAllies:
                case MoveTargeting.Field:
                    return actor;
                case MoveTargeting.SingleAlly:
                    return FindAlly(acting, actor) ?? actor;
                default:
                    return GetFirstAliveActive(opposing) ?? actor;
            }
        }

        private float RollModifierPercent(ResolvedMoveModifier mod, float fallbackPercent)
        {
            float min = fallbackPercent;
            float max = fallbackPercent;

            if (HasRuntimeEffectValue(mod.runtime))
            {
                min = mod.runtime.effectValue;
                max = mod.runtime.effectValueLocked
                    ? mod.runtime.effectValue
                    : (mod.runtime.effectValueMax > 0f ? mod.runtime.effectValueMax : mod.runtime.effectValue);
            }
            else if (mod.definition != null)
            {
                ModifierBalanceOverlaySO overlay = BalanceConfigRuntime.ActiveModifierOverlay;
                float effectMin = overlay != null
                    ? overlay.ResolveEffectMin(mod.definition)
                    : mod.definition.effectMin;
                float effectMax = overlay != null
                    ? overlay.ResolveEffectMax(mod.definition)
                    : mod.definition.effectMax;

                if (effectMin > 0f)
                {
                    min = effectMin;
                    max = effectMax > 0f ? effectMax : effectMin;
                }
            }

            float raw = Math.Abs(max - min) > 0.0001f
                ? _rng.NextFloat(Math.Min(min, max), Math.Max(min, max))
                : min;
            return raw > 1f ? raw / 100f : raw;
        }


        private static bool IsOnHitLifestealModifier(ResolvedMoveModifier mod)
        {
            if (string.IsNullOrWhiteSpace(mod.sectionId))
                return false;
            if (!StartsWithSection(mod.sectionId, "6.1-"))
                return false;
            if (mod.sectionId.Equals("6.1-b", StringComparison.OrdinalIgnoreCase) ||
                mod.sectionId.Equals("6.1-c", StringComparison.OrdinalIgnoreCase))
                return false;

            string desc = mod.definition != null ? mod.definition.effectDescription : string.Empty;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                return desc.IndexOf("Heal", StringComparison.OrdinalIgnoreCase) >= 0
                    || desc.IndexOf("damage dealt", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return mod.sectionId.Equals("6.1-a", StringComparison.OrdinalIgnoreCase)
                || mod.sectionId.Equals("6.1-d", StringComparison.OrdinalIgnoreCase)
                || mod.sectionId.Equals("6.1-e", StringComparison.OrdinalIgnoreCase)
                || mod.sectionId.Equals("6.1-f", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPostCastBarrierModifier(ResolvedMoveModifier mod)
        {
            if (string.IsNullOrWhiteSpace(mod.sectionId))
                return false;
            if (StartsWithSection(mod.sectionId, "6.7-"))
                return true;
            return mod.definition != null && mod.definition.slotType == ModifierSlotType.Barrier;
        }

        private void ApplyPostCastBarrierModifier(
            ChainlingInstance attacker,
            MoveInstance move,
            ResolvedMoveModifier mod,
            int turn)
        {
            if (attacker == null)
                return;

            string section = mod.sectionId ?? string.Empty;
            bool replaced;

            if (section.Equals("6.7-c", StringComparison.OrdinalIgnoreCase))
            {
                float pct = RollModifierPercent(mod, 0.15f);
                int pool = Math.Max(1, (int)Math.Round(attacker.EffectiveMaxHP * pct));
                replaced = attacker.AddBarrier(
                    BarrierType.DamageThreshold,
                    hitsRemaining: 0,
                    damageThreshold: pool,
                    turnsRemaining: 2,
                    sourceTag: $"modifier.{section}",
                    reflectPercent: 0f);
            }
            else
            {
                float hitsRaw = 1f;
                if (HasRuntimeEffectValue(mod.runtime))
                    hitsRaw = mod.runtime.effectValue;
                else if (mod.definition != null && mod.definition.effectMin > 0f)
                    hitsRaw = mod.definition.effectMin;

                int hits = Math.Max(1, (int)Math.Round(hitsRaw));
                int turns = section.Equals("6.7-b", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                replaced = attacker.AddBarrier(
                    BarrierType.SingleHit,
                    hitsRemaining: hits,
                    damageThreshold: 0,
                    turnsRemaining: turns,
                    sourceTag: $"modifier.{section}",
                    reflectPercent: 0f);
            }

            string note = replaced ? " (replaced prior wall/shield)" : string.Empty;
            _log.LogMessage(
                CombatEventType.SystemMessage,
                $"{attacker.displayName} gained a post-cast barrier from {section}{note}.",
                turn);
        }


        private static bool StartsWithSection(string sectionId, string prefix)
        {
            if (string.IsNullOrWhiteSpace(sectionId) || string.IsNullOrWhiteSpace(prefix))
                return false;
            return sectionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetMoveModifier(MoveInstance move, string sectionId, out ResolvedMoveModifier modifier)
        {
            modifier = default;
            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (string.Equals(modifiers[i].sectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    modifier = modifiers[i];
                    return true;
                }
            }

            return false;
        }

        private bool HasMoveModifier(MoveInstance move, string sectionId)
        {
            ResolvedMoveModifier _;
            return TryGetMoveModifier(move, sectionId, out _);
        }

        private static bool IsDamagingMove(MoveInstance move)
        {
            return move != null &&
                   (move.category == MoveCategory.DamagingMelee ||
                    move.category == MoveCategory.DamagingRanged);
        }

        private static bool IsMultiMove(MoveInstance move)
        {
            if (move == null)
                return false;
            if (move.hitCountMax > 1 || move.hitCount > 1)
                return true;
            return move.tags != null && move.tags.Contains("MULTI");
        }

        private static CoreStat ResolveSubstitutionStat(ResolvedMoveModifier mod)
        {
            if (mod.runtime != null && mod.runtime.substitutionStat != CoreStat.MeleeAtk)
                return mod.runtime.substitutionStat;
            if (mod.definition != null && mod.definition.isStatSubstitution)
                return mod.definition.substitutionStat;
            return mod.runtime != null ? mod.runtime.substitutionStat : CoreStat.MeleeAtk;
        }

        private static bool ShouldApplyStatSubstitution(
            string sectionId,
            ChainlingInstance attacker,
            MoveInstance move,
            CoreStat subStat)
        {
            if (attacker == null || move == null)
                return false;
            if (sectionId.Equals("5.2-e", StringComparison.OrdinalIgnoreCase))
            {
                DamageCalculator.ResolveDamageStats(move, out CoreStat defaultAtk, out _);
                return attacker.GetEffectiveStat(CoreStat.Speed) > attacker.GetEffectiveStat(defaultAtk);
            }
            return true;
        }

        private static bool IsLowHp(ChainlingInstance actor, ResolvedMoveModifier mod)
        {
            if (actor == null || actor.EffectiveMaxHP <= 0)
                return false;
            float threshold = mod.definition != null && mod.definition.lowHpThresholdPercent > 0f
                ? mod.definition.lowHpThresholdPercent
                : 30f;
            return actor.currentHP * 100f / actor.EffectiveMaxHP < threshold;
        }

        private void ProcessOnKOModifiers(ChainlingInstance attacker, MoveInstance move, ChainlingInstance defeatedTarget, int turn)
        {
            if (attacker == null || move == null)
                return;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                if (ResolveModifierTrigger(mod) != ModifierTriggerCondition.OnKO)
                    continue;
                if (!CheckModifierTriggerChance(mod))
                {
                    LogModifierOutcome(mod, attacker, defeatedTarget, move, turn, false, "OnKO proc failed");
                    continue;
                }

                string desc = GetModifierDescription(mod);
                bool triggered = false;

                if (ContainsIgnoreCase(desc, "ap"))
                {
                    int apGain = Math.Max(1, (int)Math.Round(ResolveModifierRawValue(mod, 1f)));
                    triggered |= _apService.Gain(attacker, apGain, turn, $"Modifier.{mod.sectionId}") > 0;
                }

                if (ContainsIgnoreCase(desc, "speed stage"))
                {
                    int oldStage = attacker.GetStage(CoreStat.Speed);
                    attacker.SetStage(CoreStat.Speed, oldStage + 1);
                    int newStage = attacker.GetStage(CoreStat.Speed);
                    int delta = newStage - oldStage;
                    if (delta != 0)
                    {
                        _log.LogStageChange(turn, attacker.instanceId, CoreStat.Speed, delta, newStage);
                        triggered = true;
                    }
                }

                if (triggered)
                {
                    LogModifierOutcome(
                        mod,
                        attacker,
                        defeatedTarget,
                        move,
                        turn,
                        true,
                        "OnKO effect applied",
                        ResolveModifierDisplayText(mod),
                        attacker.instanceId,
                        ModifierPopupMoment.OnKoTrigger);
                }
            }
        }

        private void ProcessOnEscapeModifiers(CombatState state, TeamState escapingTeam, ChainlingInstance escapingActor, int turn)
        {
            if (state == null || escapingTeam == null || escapingActor == null || !escapingActor.isAlive)
                return;

            TeamState opposingTeam = escapingTeam.teamIndex == state.team0.teamIndex ? state.team1 : state.team0;
            if (opposingTeam == null)
                return;

            var opponents = opposingTeam.GetActiveChainlings();
            for (int i = 0; i < opponents.Count; i++)
            {
                var opponent = opponents[i];
                if (opponent == null || !opponent.isAlive || opponent.moves == null)
                    continue;

                bool triggeredForOpponent = false;
                for (int m = 0; m < opponent.moves.Count && escapingActor.isAlive && !triggeredForOpponent; m++)
                {
                    var move = opponent.moves[m];
                    if (move == null)
                        continue;

                    var modifiers = ResolveMoveModifiers(move);
                    for (int k = 0; k < modifiers.Count && escapingActor.isAlive && !triggeredForOpponent; k++)
                    {
                        var mod = modifiers[k];
                        if (ResolveModifierTrigger(mod) != ModifierTriggerCondition.OnEscape)
                            continue;
                        if (!CheckModifierTriggerChance(mod))
                            continue;

                        string desc = GetModifierDescription(mod);
                        bool applied = false;

                        if (ContainsIgnoreCase(desc, "slow"))
                        {
                            int oldStage = escapingActor.GetStage(CoreStat.Speed);
                            escapingActor.SetStage(CoreStat.Speed, oldStage - 1);
                            int newStage = escapingActor.GetStage(CoreStat.Speed);
                            int delta = newStage - oldStage;
                            if (delta != 0)
                            {
                                _log.LogStageChange(turn, escapingActor.instanceId, CoreStat.Speed, delta, newStage);
                                applied = true;
                            }
                        }

                        if (ContainsIgnoreCase(desc, "chip") || ContainsIgnoreCase(desc, "maxhp"))
                        {
                            float pct = ResolveModifierPercent(mod, 0.05f);
                            int damage = Math.Max(1, (int)Math.Floor(escapingActor.EffectiveMaxHP * pct));
                            int before = escapingActor.currentHP;
                            escapingActor.currentHP = Math.Max(0, escapingActor.currentHP - damage);
                            int actual = before - escapingActor.currentHP;
                            if (actual > 0)
                            {
                                _log.LogMessage(
                                    CombatEventType.HPDotDamage,
                                    $"{escapingActor.displayName} took {actual} chip damage from escape trigger.",
                                    turn);
                                applied = true;
                            }

                            if (escapingActor.currentHP <= 0)
                            {
                                escapingActor.isAlive = false;
                                _log.LogKO(turn, escapingActor.instanceId);
                            }
                        }

                        if (applied)
                        {
                            LogModifierOutcome(
                                mod,
                                opponent,
                                escapingActor,
                                move,
                                turn,
                                true,
                                "OnEscape effect applied",
                                ResolveModifierDisplayText(mod),
                                escapingActor.instanceId,
                                ModifierPopupMoment.OnEscapeTrigger);
                            triggeredForOpponent = true;
                        }
                    }
                }
            }
        }

        private List<ResolvedMoveModifier> ResolveMoveModifiers(MoveInstance move)
        {
            var list = new List<ResolvedMoveModifier>(4);
            if (move == null)
                return list;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    var runtime = move.modifiers[i];
                    string section = runtime != null ? NormalizeSectionId(runtime.definitionSectionId) : string.Empty;
                    AddResolvedModifier(list, seen, section, runtime);
                }
            }

            AddResolvedModifier(list, seen, NormalizeSectionId(move.modifier1SectionId), null);
            AddResolvedModifier(list, seen, NormalizeSectionId(move.modifier2SectionId), null);
            if (move.modifierIds != null)
            {
                for (int i = 0; i < move.modifierIds.Length; i++)
                    AddResolvedModifier(list, seen, NormalizeSectionId(move.modifierIds[i]), null);
            }

            return list;
        }

        private void AddResolvedModifier(
            List<ResolvedMoveModifier> list,
            HashSet<string> seen,
            string sectionId,
            ModifierInstance runtime)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return;
            if (!seen.Add(sectionId))
                return;

            list.Add(new ResolvedMoveModifier
            {
                sectionId = sectionId,
                runtime = runtime,
                definition = _modifierCatalog != null ? _modifierCatalog.GetBySectionId(sectionId) : null,
            });
        }

        private static string NormalizeSectionId(string sectionId)
        {
            return string.IsNullOrWhiteSpace(sectionId) ? string.Empty : sectionId.Trim();
        }

        private static bool HasRuntimeEffectValue(ModifierInstance runtime)
        {
            return runtime != null
                && (runtime.effectValueLocked
                    || runtime.effectValue > 0f
                    || runtime.effectValueMax > 0f);
        }

        private static ModifierTriggerCondition ResolveModifierTrigger(ResolvedMoveModifier mod)
        {
            ModifierTriggerCondition trigger = ModifierTriggerCondition.None;
            if (mod.runtime != null && mod.runtime.triggerCondition != ModifierTriggerCondition.None)
                trigger = mod.runtime.triggerCondition;
            else if (mod.definition != null)
                trigger = mod.definition.triggerCondition;

            if (trigger == ModifierTriggerCondition.OnHPBelow30)
                return ModifierTriggerCondition.OnLowHP;
            return trigger;
        }

        private bool CheckModifierTriggerChance(ResolvedMoveModifier mod)
        {
            float chance = 1f;
            if (mod.definition != null && mod.definition.triggerChance > 0f)
                chance = mod.definition.triggerChance;
            if (chance >= 1f)
                return true;
            return _rng.Check(chance);
        }

        private static float ResolveModifierRawValue(ResolvedMoveModifier mod, float fallback)
        {
            if (HasRuntimeEffectValue(mod.runtime))
                return mod.runtime.effectValue;
            if (mod.definition != null && mod.definition.effectMin > 0f)
                return mod.definition.effectMin;
            return fallback;
        }

        private static float ResolveModifierPercent(ResolvedMoveModifier mod, float fallbackPercent)
        {
            float raw = ResolveModifierRawValue(mod, fallbackPercent);
            return raw > 1f ? raw / 100f : raw;
        }

        private static string GetModifierDescription(ResolvedMoveModifier mod)
        {
            return mod.definition != null ? mod.definition.effectDescription : string.Empty;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                return false;
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private void ResolveSwap(CombatState state, TeamState team, int targetIndex, ChainlingInstance actor)
        {
            // Check Frozen Assets
            if (HasStatus(actor, StatusId.FrozenAssets))
            {
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} cannot swap â€” Frozen Assets!", state.currentTurn);
                return;
            }

            if (targetIndex < 0 || targetIndex >= team.chainlings.Count)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"Invalid swap target index {targetIndex}", state.currentTurn);
                return;
            }

            // Engine-level safety: prevent double-swap to same bench this turn.
            // If this team already consumed their bench swap, convert to Pass and log.
            var reservedBenchIndices = new HashSet<int>();
            if (team.teamIndex == 0 && state.swapReservedBenchIndexTeam0 >= 0)
                reservedBenchIndices.Add(state.swapReservedBenchIndexTeam0);
            if (team.teamIndex == 1 && state.swapReservedBenchIndexTeam1 >= 0)
                reservedBenchIndices.Add(state.swapReservedBenchIndexTeam1);

            List<int> legalSwapTargets = _eligibilityService.GetAvailableSwapTargets(team, actor, reservedBenchIndices);
            if (!legalSwapTargets.Contains(targetIndex))
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{actor.displayName} attempted an invalid swap target ({targetIndex}) and the action was rejected.",
                    state.currentTurn);
                return;
            }

            var newActive = team.chainlings[targetIndex];

            // Trigger opponent OnEscape modifiers before executing the swap.
            ProcessOnEscapeModifiers(state, team, actor, state.currentTurn);
            if (!actor.isAlive)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{actor.displayName} was interrupted and could not complete the swap.",
                    state.currentTurn);
                return;
            }

            // Find which active slot the actor is in
            int actorSlot = -1;
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                if (team.chainlings[team.activeIndices[s]] == actor)
                {
                    actorSlot = s;
                    break;
                }
            }

            if (actorSlot < 0)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"{actor.displayName} is not in an active slot", state.currentTurn);
                return;
            }

            // Perform swap.
            // Cassette Beasts-style AP continuity: AP belongs to the active slot context,
            // so the incoming Chainling inherits the outgoing actor's current AP.
            int slotAp = actor.currentAP;
            ResetChemistrySwapState(actor, state.currentTurn);
            actor.ClearArmedUtilityWindows();
            actor.isActive = false;
            newActive.isActive = true;
            newActive.currentAP = slotAp;
            newActive.ClampAP();
            team.activeIndices[actorSlot] = targetIndex;

            // Reserve this bench index for the rest of the turn (only one swap to bench per team per turn)
            if (team.teamIndex == 0)
                state.swapReservedBenchIndexTeam0 = targetIndex;
            else
                state.swapReservedBenchIndexTeam1 = targetIndex;

            bool incomingEligibleNextTurn = newActive.isAlive;
            _log.LogForensic(
                CombatForensicEventType.SwapResolved,
                new SwapResolvedPayload
                {
                    teamName = team.teamName,
                    outgoingDisplayName = _log.Name(actor.instanceId),
                    incomingDisplayName = _log.Name(newActive.instanceId),
                    outgoingRef = new CombatEntityRef { teamId = team.teamIndex, slotId = actorSlot, instanceId = actor.instanceId },
                    incomingRef = new CombatEntityRef { teamId = team.teamIndex, slotId = actorSlot, instanceId = newActive.instanceId },
                    swapType = "voluntary",
                    incomingCanActThisTurn = false,
                    incomingEligibleNextTurn = incomingEligibleNextTurn,
                    note = incomingEligibleNextTurn ? string.Empty : "Incoming chainling is not eligible next turn.",
                },
                actor.instanceId,
                new List<string> { newActive.instanceId },
                state.currentTurn);

            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.SwapPerformed,
                turn = state.currentTurn,
                sourceId = actor.instanceId,
                targetId = newActive.instanceId,
                intValue = actorSlot,
                intValue2 = team.teamIndex,
                message = $"{team.teamName}: {actor.displayName} swapped out, {newActive.displayName} swapped in",
            });

            SwapModifierResolver.ProcessSwapModifiers(
                state,
                team,
                actor,
                newActive,
                state.currentTurn,
                _log,
                _rng,
                _modifierCatalog,
                _apService);
            ApplyFieldEntryEffects(state, team, newActive, state.currentTurn);
        }

        private void ResetChemistrySwapState(ChainlingInstance outgoing, int turn)
        {
            if (outgoing?.statuses == null)
                return;

            for (int i = outgoing.statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance status = outgoing.statuses[i];
                if (status == null || status.statusId != StatusId.CascadeFailure)
                    continue;

                int counterBefore = status.counterValue;
                outgoing.statuses.RemoveAt(i);
                _log.LogStatus(turn, CombatEventType.StatusCleansed,
                    outgoing.instanceId, StatusId.CascadeFailure, 0, "SwapReset");
                _log.LogForensic(
                    CombatForensicEventType.StatusCleansed,
                    new StatusEventPayload
                    {
                        targetDisplayName = outgoing.displayName,
                        statusId = StatusId.CascadeFailure.ToString(),
                        origin = StatusOrigin.ChemistryOnly.ToString(),
                        durationBefore = status.turnsRemaining,
                        durationAfter = 0,
                        counterBefore = counterBefore,
                        counterAfter = 0,
                        blockReason = "SwapReset",
                        sourceId = "SwapReset",
                    },
                    outgoing.instanceId,
                    new List<string> { outgoing.instanceId },
                    turn);
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{outgoing.displayName}'s Cascade Failure counter reset on swap-out.", turn);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Helpers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static bool IsDefinition(MoveInstance move, string defId)
        {
            if (move == null) return false;
            if (move.definitionId == defId) return true;
            return move.instanceId != null && move.instanceId.Contains(defId);
        }

        // ── AP Transfer ────────────────────────────────────────────────────────

        private void ResolveAPTransfer(CombatState state, TeamState actingTeam,
            ChainlingInstance actor, MoveInstance move, ChainlingInstance selectedTarget, int paidCost)
        {
            var target = selectedTarget ?? FindAlly(actingTeam, actor);
            if (target == null || target == actor)
            {
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} has no valid target for AP transfer", state.currentTurn);
                return;
            }

            if (HasStatus(target, StatusId.Isolated))
            {
                _log.LogStatus(state.currentTurn, CombatEventType.StatusImmune,
                    target.instanceId, StatusId.Isolated, 0, move.instanceId);
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{target.displayName} is Isolated and cannot receive ally support.", state.currentTurn);
                return;
            }

            int transferAmount = Math.Max(0, paidCost);

            if (IsDefinition(move, "ap_donation"))
            {
                if (actor.apDonationUsedThisTurn)
                {
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{actor.displayName} already used AP Donation this turn", state.currentTurn);
                    return;
                }
                int actual = _apService.TransferPaidCost(actor, target, transferAmount, state.currentTurn, move.definitionId);
                actor.apDonationUsedThisTurn = true;
            }
            else if (IsDefinition(move, "ap_redistribution"))
            {
                _apService.TransferPaidCost(actor, target, transferAmount, state.currentTurn, move.definitionId);
            }
            else if (IsDefinition(move, "ap_router"))
            {
                _apService.TransferPaidCost(actor, target, transferAmount, state.currentTurn, move.definitionId);
            }

            ApplyMoveStatuses(actor, target, move, state.currentTurn);
            ApplyMoveStageChanges(actor, target, move, state.currentTurn);
            LogResolvedTargets(state.currentTurn, actor, move, new List<ChainlingInstance> { target });
        }

        // ── Damage Redirect ────────────────────────────────────────────────────

        private void ResolveDamageRedirect(CombatState state, TeamState actingTeam,
            ChainlingInstance actor, MoveInstance move, ChainlingInstance selectedTarget)
        {
            var target = selectedTarget ?? FindAlly(actingTeam, actor);
            if (target == null || target == actor)
            {
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} has no valid target for damage redirect", state.currentTurn);
                return;
            }

            target.redirectInterceptor = actor;
            _statusProcessor.ApplyStatus(target, StatusId.DamageIntercept, 1, move.instanceId, state.currentTurn);
            _log.LogMessage(CombatEventType.ActionSelected,
                $"{actor.displayName} will intercept the next single-target attack on {target.displayName}", state.currentTurn);

            ApplyMoveStatuses(actor, target, move, state.currentTurn);
            ApplyMoveStageChanges(actor, target, move, state.currentTurn);
            LogResolvedTargets(state.currentTurn, actor, move, new List<ChainlingInstance> { target });
        }

        // ── Buff/Debuff Transfer ───────────────────────────────────────────────

        private void ResolveEffectTransfer(CombatState state, TeamState actingTeam,
            ChainlingInstance actor, MoveInstance move, ChainlingInstance selectedTarget)
        {
            var target = selectedTarget ?? FindAlly(actingTeam, actor);
            if (target == null)
            {
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{actor.displayName} has no valid target for effect transfer", state.currentTurn);
                return;
            }

            if (IsDefinition(move, "effect_relay"))
            {
                // Move 1 General debuff from target (ally) to self, or cleanse if none
                StatusInstance debuffToMove = null;
                int debuffIdx = -1;
                for (int i = 0; i < target.statuses.Count; i++)
                {
                    var s = target.statuses[i];
                    var def = _statusCatalog?.Get(s.statusId);
                    if (s != null && !s.IsExpired && def != null && def.isDebuff && s.origin == StatusOrigin.General)
                    {
                        debuffToMove = s;
                        debuffIdx = i;
                        break;
                    }
                }

                if (debuffToMove != null)
                {
                    target.statuses.RemoveAt(debuffIdx);
                    _statusProcessor.ApplyStatus(actor, debuffToMove.statusId,
                        debuffToMove.turnsRemaining, move.instanceId, state.currentTurn);
                    _log.LogMessage(CombatEventType.ActionSelected,
                        $"{actor.displayName} absorbed {debuffToMove.statusId} from {target.displayName}", state.currentTurn);
                }
                else
                {
                    _log.LogMessage(CombatEventType.ActionSelected,
                        $"{actor.displayName} cleansed {target.displayName} (no debuffs to relay)", state.currentTurn);
                }
            }
            else if (IsDefinition(move, "relay_packet"))
            {
                // Move 1 General buff or debuff between allies
                StatusInstance statusToMove = null;
                int statusIdx = -1;
                for (int i = 0; i < target.statuses.Count; i++)
                {
                    var s = target.statuses[i];
                    if (s != null && !s.IsExpired && s.origin == StatusOrigin.General)
                    {
                        statusToMove = s;
                        statusIdx = i;
                        break;
                    }
                }

                if (statusToMove != null)
                {
                    target.statuses.RemoveAt(statusIdx);
                    _statusProcessor.ApplyStatus(actor, statusToMove.statusId,
                        statusToMove.turnsRemaining, move.instanceId, state.currentTurn);
                    _log.LogMessage(CombatEventType.ActionSelected,
                        $"{actor.displayName} relayed {statusToMove.statusId} from {target.displayName}", state.currentTurn);
                }
                else
                {
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{target.displayName} has no General statuses to relay", state.currentTurn);
                }
            }

            ApplyMoveStageChanges(actor, target, move, state.currentTurn);
            LogResolvedTargets(state.currentTurn, actor, move, new List<ChainlingInstance> { target });
        }

        /// <summary>
        /// Check whether this hit should bypass barriers.
        /// Bypass sources: chemistry advantage, Relay status (full bypass, consumed),
        /// Unsealed (50% roll), modifier 4.3-b (first hit only).
        /// </summary>
        private int ResolveWallStrikeApDrain(
            ChainlingInstance target,
            ChainlingInstance attacker,
            int turn,
            bool wallWasStruck,
            int barrierApDrain,
            string wallSourceTag)
        {
            if (!wallWasStruck || attacker == null || !attacker.isAlive || target == null)
                return 0;

            int apDrain = Math.Max(0, barrierApDrain);
            if (apDrain <= 0)
                apDrain = CombatWallEffectUtility.ResolveMitigationWallApDrainOnHit(target, wallSourceTag);

            if (apDrain > 0)
                _apService?.Drain(attacker, apDrain, turn, "WallHitRetaliation");

            return apDrain;
        }

        private static WallInterceptPayload CreateWallInterceptPayload(
            int zeroBasedHitIndex,
            int totalHits,
            WallInterceptMode mode,
            bool wallActiveBeforeHit,
            bool wallActiveAfterHit,
            BarrierBypassResult bypassResult,
            MoveInstance move,
            float mitigationPercent,
            int incomingDamage,
            int finalHpDamage,
            bool barrierFullyBlocked,
            int mitigationSourcesExpired,
            int barriersExpired,
            bool wallExpiredThisStep,
            bool partialPenetration,
            string wallSourceTag,
            int reflectedDamage = 0,
            int attackerApDrained = 0,
            float wallReflectPercent = 0f,
            int preMitigationDamage = 0)
        {
            ContactType contactType = move != null ? move.contactType : ContactType.None;
            MoveCategory moveCategory = move != null ? move.category : MoveCategory.Support;
            int damageAbsorbed = Math.Max(0, incomingDamage - finalHpDamage);
            if (barrierFullyBlocked && incomingDamage > 0)
                damageAbsorbed = incomingDamage;

            // Percent mitigation is applied in DamageCalculator before incomingDamage is captured.
            if (wallActiveBeforeHit && !bypassResult.bypassed && preMitigationDamage > finalHpDamage)
                damageAbsorbed = Math.Max(damageAbsorbed, preMitigationDamage - finalHpDamage);

            return WallInterceptPayload.Create(
                zeroBasedHitIndex + 1,
                Math.Max(1, totalHits),
                mode,
                wallActiveBeforeHit,
                wallActiveAfterHit,
                bypassResult.bypassed,
                bypassResult.reason,
                contactType,
                moveCategory,
                mitigationPercent,
                incomingDamage,
                finalHpDamage,
                barrierFullyBlocked,
                mitigationSourcesExpired,
                barriersExpired,
                wallExpiredThisStep,
                partialPenetration,
                wallSourceTag,
                damageAbsorbed,
                reflectedDamage,
                attackerApDrained,
                wallReflectPercent);
        }

        private static WallInterceptMode ClassifyWallInterceptMode(
            bool wallActiveBeforeHit,
            bool wallActiveAfterHit,
            bool bypassed,
            int incomingDamage,
            int finalDamage)
        {
            if (!wallActiveBeforeHit)
                return WallInterceptMode.None;
            if (bypassed)
                return WallInterceptMode.Bypass;
            if (finalDamage <= 0)
                return WallInterceptMode.WallHitNoDamage;
            if (!wallActiveAfterHit)
                return WallInterceptMode.WallBrokenThrough;
            if (incomingDamage > 0)
                return WallInterceptMode.WallMitigated;
            return WallInterceptMode.None;
        }

        private static string ResolvePayloadWallSourceTag(string beforeHit, string afterHit)
        {
            if (!string.IsNullOrWhiteSpace(beforeHit))
                return beforeHit;
            return afterHit ?? string.Empty;
        }

        private readonly struct BarrierBypassResult
        {
            public readonly bool bypassed;
            public readonly string reason;

            public BarrierBypassResult(bool bypassed, string reason)
            {
                this.bypassed = bypassed;
                this.reason = reason ?? string.Empty;
            }
        }

        private BarrierBypassResult CheckBarrierBypass(
            ChainlingInstance attacker,
            MoveInstance move,
            ChemistryResult chemResult,
            int hitIndex,
            DamageModifierContext modifierContext,
            int turn)
        {
            // Relay: full bypass for next hit, consumed on use
            if (HasStatus(attacker, StatusId.Relay))
            {
                ConsumeStatus(attacker, StatusId.Relay);
                return new BarrierBypassResult(true, "Relay");
            }

            // Unsealed: 50% chance to bypass single-hit barriers
            if (HasStatus(attacker, StatusId.Unsealed))
            {
                if (_rng.Check(0.5f))
                    return new BarrierBypassResult(true, "Unsealed");
            }

            // Modifier 4.3-b: bypass barriers on first hit only
            if (hitIndex == 0 && modifierContext != null && modifierContext.barrierBypassChance >= 1f)
                return new BarrierBypassResult(true, "ModifierGuaranteedFirstHit");

            // Modifier 4.6-a and similar: rolled first-hit barrier bypass.
            if (hitIndex == 0 && modifierContext != null && modifierContext.barrierBypassChance > 0f)
            {
                float roll;
                bool triggered = _rng.Check(modifierContext.barrierBypassChance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"BarrierBypass.{move.instanceId}",
                        chance = modifierContext.barrierBypassChance,
                        roll = roll,
                        triggered = triggered,
                    },
                    attacker != null ? attacker.instanceId : string.Empty,
                    null,
                    turn);
                if (triggered)
                    return new BarrierBypassResult(true, "ModifierRolledFirstHit");
            }

            return new BarrierBypassResult(false, string.Empty);
        }

        /// <summary>Find the active slot index for a Chainling on a team, or -1 if not found.</summary>
        private static int FindSlotOf(TeamState team, ChainlingInstance chainling)
        {
            if (team == null || chainling == null || team.activeIndices == null)
                return -1;
            for (int s = 0; s < team.activeIndices.Count; s++)
            {
                int idx = team.activeIndices[s];
                if (idx >= 0 && idx < team.chainlings.Count && team.chainlings[idx] == chainling)
                    return s;
            }
            return -1;
        }

        private static bool HasPriorTag(ActionQueueEntry entry)
        {
            return entry.hasPriority;
        }

        private bool ResolvePriorityForAction(ChainlingInstance actor, CombatAction action, int turn)
        {
            if (action == null || action.actionType != CombatActionType.UseMove)
                return false;
            int moveIndex = action.moveIndex;
            if (actor == null || moveIndex < 0 || moveIndex >= actor.moves.Count)
                return false;

            MoveInstance move = actor.moves[moveIndex];
            if (move == null)
                return false;
            if (move.tags != null && (move.tags.Contains("PRIOR") || move.tags.Contains("PRIO")))
                return true;

            var modifiers = ResolveMoveModifiers(move);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ResolvedMoveModifier mod = modifiers[i];
                if (mod.sectionId.Equals("4.3-a", StringComparison.OrdinalIgnoreCase) ||
                    mod.sectionId.Equals("4.3-b", StringComparison.OrdinalIgnoreCase))
                {
                    LogModifierOutcome(mod, actor, actor, move, turn, true, "Priority modifier applied");
                    return true;
                }

                if (mod.sectionId.Equals("1.1-a", StringComparison.OrdinalIgnoreCase) ||
                    mod.sectionId.Equals("1.3-a", StringComparison.OrdinalIgnoreCase) ||
                    mod.sectionId.Equals("1.3-b", StringComparison.OrdinalIgnoreCase))
                {
                    float chance = RollModifierPercent(mod, 0.08f);
                    float roll;
                    bool triggered = _rng.Check(chance, out roll);
                    _log.LogForensic(
                        CombatForensicEventType.ProcRoll,
                        new ProcRollPayload
                        {
                            procId = $"Priority.{move.instanceId}.{mod.sectionId}",
                            chance = chance,
                            roll = roll,
                            triggered = triggered,
                        },
                        actor.instanceId,
                        new List<string> { actor.instanceId },
                        turn);
                    LogModifierOutcome(mod, actor, actor, move, turn, triggered,
                        triggered ? "Priority chance succeeded" : "Priority chance failed");
                    if (triggered)
                        return true;
                }
                else if (mod.sectionId.Equals("1.2-a", StringComparison.OrdinalIgnoreCase) && IsLowHp(actor, mod))
                {
                    LogModifierOutcome(mod, actor, actor, move, turn, true, "Low HP priority applied");
                    return true;
                }
            }

            return false;
        }

        private bool HasStatus(ChainlingInstance chainling, StatusId statusId)
        {
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == statusId && !chainling.statuses[i].IsExpired)
                    return true;
            }
            return false;
        }

        private void ConsumeStatus(ChainlingInstance chainling, StatusId statusId)
        {
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == statusId && !chainling.statuses[i].isConsumed)
                {
                    chainling.statuses[i].Consume();
                    return;
                }
            }
        }

        private bool IsRecharging(ChainlingInstance chainling, string moveInstanceId)
        {
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == StatusId.Recharging &&
                    chainling.statuses[i].rechargingMoveId == moveInstanceId &&
                    !chainling.statuses[i].IsExpired)
                    return true;
            }
            return false;
        }

        private void ProcessMitigationTimers(ChainlingInstance chainling, int turn)
        {
            if (chainling == null)
                return;

            bool wallActiveBefore = CombatWallStateUtility.HasActiveWallState(chainling);
            string wallSourceBefore = CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);
            int expired = chainling.TickMitigationSources();
            if (expired > 0)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{chainling.displayName} mitigation effects expired ({expired}).",
                    turn);

                bool wallActiveAfter = CombatWallStateUtility.HasActiveWallState(chainling);
                if (wallActiveBefore && !wallActiveAfter)
                    LogWallExpiredFromTimer(chainling, turn, wallSourceBefore, expired, 0);
            }
        }

        private void ProcessBarrierTimers(ChainlingInstance chainling, int turn)
        {
            if (chainling == null)
                return;

            bool wallActiveBefore = CombatWallStateUtility.HasActiveWallState(chainling);
            string wallSourceBefore = CombatWallStateUtility.ResolveLatestWallSourceTag(chainling);
            int expired = chainling.TickBarriers();
            if (expired > 0)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{chainling.displayName} barrier effects expired ({expired}).",
                    turn);

                bool wallActiveAfter = CombatWallStateUtility.HasActiveWallState(chainling);
                if (wallActiveBefore && !wallActiveAfter)
                    LogWallExpiredFromTimer(chainling, turn, wallSourceBefore, 0, expired);
            }
        }

        private void LogWallExpiredFromTimer(
            ChainlingInstance chainling,
            int turn,
            string wallSourceTag,
            int mitigationSourcesExpired,
            int barriersExpired)
        {
            if (chainling == null)
                return;

            var payload = WallInterceptPayload.Create(
                hitIndex: 0,
                totalHits: 0,
                mode: WallInterceptMode.WallHitNoDamage,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: false,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.None,
                moveCategory: MoveCategory.Support,
                mitigationPercent: 0f,
                incomingDamage: 0,
                finalHpDamage: 0,
                barrierFullyBlocked: false,
                mitigationSourcesExpired: mitigationSourcesExpired,
                barriersExpired: barriersExpired,
                wallExpiredThisStep: true,
                partialPenetration: false,
                wallSourceTag: wallSourceTag);
            _log.LogWallExpired(turn, chainling.instanceId, wallSourceTag, payload.ToJson());
        }

        private void ProcessAccuracyTimers(ChainlingInstance chainling, int turn)
        {
            if (chainling == null)
                return;

            int expired = chainling.TickAccuracySources();
            if (expired > 0)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{chainling.displayName} temporary accuracy effects expired ({expired}).",
                    turn);
            }
        }

        private void ProcessReactiveApTimers(ChainlingInstance chainling, int turn)
        {
            if (chainling == null)
                return;

            int expired = chainling.TickReactiveApWindows();
            if (expired > 0)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{chainling.displayName} reactive AP windows expired ({expired}).",
                    turn);
            }
        }

        private void CheckAPRefund(ChainlingInstance actor, MoveInstance move, int turn)
        {
            var resolved = ResolveMoveModifiers(move);
            for (int m = 0; m < resolved.Count; m++)
            {
                var mod = resolved[m];
                if (!StartsWithSection(mod.sectionId, "3.1"))
                    continue;

                ModifierCategory category = mod.definition != null
                    ? mod.definition.category
                    : (mod.runtime != null ? mod.runtime.category : ModifierCategory.POT);
                if (category != ModifierCategory.EFF)
                    continue;

                float chance = 0f;
                if (HasRuntimeEffectValue(mod.runtime))
                    chance = mod.runtime.effectValue / 100f;
                else if (mod.definition != null && mod.definition.effectMin > 0f)
                    chance = mod.definition.effectMin > 1f ? mod.definition.effectMin / 100f : mod.definition.effectMin;

                if (chance <= 0f)
                    continue;

                float roll;
                bool triggered = _rng.Check(chance, out roll);
                _log.LogForensic(
                    CombatForensicEventType.ProcRoll,
                    new ProcRollPayload
                    {
                        procId = $"Modifier.EFF.{move.instanceId}",
                        chance = chance,
                        roll = roll,
                        triggered = triggered,
                    },
                    actor.instanceId,
                    new List<string> { actor.instanceId },
                    turn);

                if (triggered)
                {
                    float refundScale = 1f;
                    if (HasRuntimeEffectValue(mod.runtime))
                    {
                        float rawRefund = mod.runtime.effectValueLocked
                            ? mod.runtime.effectValue
                            : (mod.runtime.effectValueMax > 0f ? mod.runtime.effectValueMax : mod.runtime.effectValue);
                        refundScale = rawRefund > 1f ? rawRefund / 100f : rawRefund;
                    }
                    else if (mod.definition != null && mod.definition.effectMax > 0f)
                    {
                        refundScale = mod.definition.effectMax > 1f
                            ? mod.definition.effectMax / 100f
                            : mod.definition.effectMax;
                    }

                    int refund = Math.Max(1, (int)(move.apCost * refundScale));
                    _apService.Refund(actor, refund, turn, $"Modifier.{mod.sectionId}");
                    LogModifierOutcome(mod, actor, actor, move, turn, true, "AP refund applied");
                }
            }
        }

        private void WakeOnDamage(ChainlingInstance target, int turn)
        {
            for (int i = target.statuses.Count - 1; i >= 0; i--)
            {
                if (target.statuses[i].statusId == StatusId.Offline)
                {
                    _log.LogMessage(CombatEventType.StatusConsumed,
                        $"{target.displayName} woke up from Offline!", turn);
                    target.statuses.RemoveAt(i);
                    target.skippingNextTurn = false;
                }
            }
        }

        private bool CheckBattleEnd(CombatState state)
        {
            bool team0Dead = state.team0.IsDefeated;
            bool team1Dead = state.team1.IsDefeated;

            if (team0Dead && team1Dead)
            {
                state.isActive = false;
                state.winnerTeamIndex = -2;
                state.currentPhase = TurnPhase.BattleOver;
                _log.LogMessage(CombatEventType.Draw, "Battle ended in a draw!", state.currentTurn);
                return true;
            }
            else if (team0Dead)
            {
                state.isActive = false;
                state.winnerTeamIndex = 1;
                state.currentPhase = TurnPhase.BattleOver;
                _log.LogMessage(CombatEventType.WinDeclared,
                    $"{state.team1.teamName} wins!", state.currentTurn);
                return true;
            }
            else if (team1Dead)
            {
                state.isActive = false;
                state.winnerTeamIndex = 0;
                state.currentPhase = TurnPhase.BattleOver;
                _log.LogMessage(CombatEventType.WinDeclared,
                    $"{state.team0.teamName} wins!", state.currentTurn);
                return true;
            }

            return false;
        }
    }
}
