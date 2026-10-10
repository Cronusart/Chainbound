using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    /// <summary>
    /// Runtime battle controller for real encounters and PvP.
    /// Owns the <see cref="CombatResolver"/>, <see cref="CombatState"/>, and controls
    /// battle phases with coroutine-based turn execution.
    ///
    /// Pulls teams from <see cref="IPlayerPartyProvider"/> and <see cref="IEncounterProvider"/> injected at runtime
    /// (e.g. a combat scene bootstrapper). No implicit ScriptableObject fallbacks.
    ///
    /// Implements <see cref="ICombatStateSource"/> so the battlefield presenter and
    /// UI layers can observe state without coupling to this class directly.
    ///
    /// When <see cref="usePlayerInput"/> is true, the controller pauses on the
    /// player's turn and fires <see cref="OnPlayerTurnWaiting"/> so the UI layer
    /// can collect actions.  Call <see cref="SubmitPlayerActions"/> to resume.
    /// </summary>
    public partial class CombatBattleController : MonoBehaviour, ICombatStateSource
    {
        [Serializable]
        public struct HudSlotRef
        {
            public int teamIndex;
            public int slotIndex;
            public string instanceId;

            public bool IsValid => teamIndex >= 0 && slotIndex >= 0 && !string.IsNullOrEmpty(instanceId);

            public HudSlotRef(int teamIndex, int slotIndex, string instanceId)
            {
                this.teamIndex = teamIndex;
                this.slotIndex = slotIndex;
                this.instanceId = instanceId;
            }
        }

        private struct VitalSnapshot
        {
            public int hp;
            public int ap;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector â€” Data Sources
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Data Sources")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private TypeChemistryMatrixSO chemistryMatrix;
        [SerializeField] private StatusCatalogSO statusCatalog;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [Tooltip("Optional battle ruleset. Resolver config is taken from this (or BalanceConfigRuntime fallback).")]
        [SerializeField] private CombatRulesetSO ruleset;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector â€” Providers
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Player Party")]
        [Tooltip("Optional: any MonoBehaviour implementing IPlayerPartyProvider.")]
        [SerializeField] private MonoBehaviour playerPartyProviderOverride;

        [Header("Encounter / Opponent")]
        [Tooltip("Optional: any MonoBehaviour implementing IEncounterProvider.")]
        [SerializeField] private MonoBehaviour encounterProviderOverride;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector â€” AI & Settings
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("AI")]
        [Tooltip("AI personality for the player side (used when usePlayerInput is false).")]
        [SerializeField] private AIPersonality playerAI = AIPersonality.Aggressive;
        [Tooltip("AI personality for the CPU opponent. Overridden by EncounterSnapshot if provided.")]
        [SerializeField] private AIPersonality cpuAI = AIPersonality.Aggressive;

        [Header("Player Input")]
        [Tooltip("When true, team 0 actions come from the UI via SubmitPlayerActions() instead of AI. " +
                 "When false, both sides use AI (test harness / replay mode).")]
        [SerializeField] private bool usePlayerInput = false;

        [Header("Battle Settings")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool deterministicMode = true;
        [SerializeField] private bool chemistryStrictMode = true;

        [Header("Turn Pacing")]
        [Tooltip("Optional shared pacing/feedback profile for turn flow and presenter holds.")]
        [SerializeField] private CombatBattlePacingProfileSO pacingProfile;
        [Tooltip("Optional presenter reference. When assigned, the controller waits for " +
                 "the presenter's event queue to drain before advancing to the next step.")]
        [SerializeField] private Combat2BattlefieldPresenter presenter;
        [Tooltip("Fallback delay between action steps when no presenter is assigned.")]
        [SerializeField] private float actionStepDelay = 0.6f;
        [Tooltip("Minimum pause between steps even when presenter finishes instantly.")]
        [SerializeField] private float minStepDelay = 0.15f;
        [Tooltip("Delay after end-of-turn before starting the next turn.")]
        [SerializeField] private float turnEndDelay = 0.4f;

        [Header("Auto-Run")]
        [Tooltip("If true, turns execute automatically after StartBattle. " +
                 "If false, call ExecuteNextTurn() manually (useful for step-by-step testing).")]
        [SerializeField] private bool autoRunTurns = true;
        [SerializeField] private int maxTurns = 100;

        [Header("Battle Intro")]
        [Tooltip("When enabled, battle progression waits for the presenter intro flow to complete before turn 1 starts.")]
        [SerializeField] private bool waitForBattleIntro = true;

        [Header("Debug Logging")]
        [SerializeField] private bool enableBattleLogExportHotkey = true;
        [SerializeField] private KeyCode exportBattleLogHotkey = KeyCode.F8;

        [Header("Reward Hook")]
        [Tooltip("Optional: MonoBehaviour implementing ICombatRewardsRouter.")]
        [SerializeField] private MonoBehaviour rewardsRouterOverride;

        [Header("Audio bridge")]
        [Tooltip("Shared ICombatAudioBridge (e.g. CombatAudioBridge) for battle-controller SFX: flee, and future one-shots wired here (rewards/xp/loot, etc.). Use the same instance as CombatPresentationDirector if you only have one bridge. If unset, the first ICombatAudioBridge in loaded scenes is used.")]
        [SerializeField]
        [FormerlySerializedAs("fleeAudioBridge")]
        private MonoBehaviour combatAudioBridge;

        [Header("Flee presentation")]
        [Tooltip("Assign a SignalJam Sound Event asset (SoundEventSO). Output Bus = SFX for mixer routing; loop = false for a one-shot.")]
        [SerializeField] private UnityEngine.Object fleeSound;
        [Tooltip("When true, OnBattleEnded fires after flee animations finish (capped by max wait).")]
        [SerializeField] private bool waitForFleePresentation = true;
        [Tooltip("Upper bound on how long to wait for flee animations before ending the battle.")]
        [SerializeField] private float fleePresentationMaxWaitSeconds = 3f;

        // â”€â”€ PvP Wager (Optional, runtime-injected) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // WagerMatchOrchestrator is a plain C# class (not a MonoBehaviour),
        // so it cannot be serialized in the Inspector. Inject it at runtime
        // via SetWagerOrchestrator() before starting a PvP battle.
        private WagerMatchOrchestrator _wagerOrchestrator;
        private CasualPvpLaunchPayload _casualPvp;
        private IPvPTurnBridge _pvpTurnBridge;
        private List<CombatAction> _casualActions0;
        private List<CombatAction> _casualActions1;
        private bool _casualTurnAborted;
        private bool _casualResultSubmitted;
        private string _casualForfeitState;
        private bool _battleFinalized;
        private IPlayerPartyProvider _runtimePlayerPartyProvider;
        private IEncounterProvider _runtimeEncounterProvider;
        private CombatRulesetSO _runtimeRulesetOverride;
        private EncounterFleePolicy _runtimeFleePolicyOverride = EncounterFleePolicy.UseDifficultyDefault;
        private bool _capturePreviewMode;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Events
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Fired after battle initialization, before the first turn.</summary>
        public event Action<CombatState> OnBattleStarted;

        /// <summary>Fired at the start of each turn with the turn index and action queue preview.</summary>
        public event Action<int, List<CombatResolver.CombatActionStep>> OnTurnBegan;

        /// <summary>Fired after each individual action step is executed.</summary>
        public event Action<CombatEvent> OnActionStepExecuted;

        /// <summary>Fired when a step actor is selected to execute.</summary>
        public event Action<HudSlotRef> OnTurnActorSelected;

        /// <summary>Fired with the resolved HUD target slots for the selected step actor.</summary>
        public event Action<HudSlotRef, List<HudSlotRef>> OnActionTargetsResolved;

        /// <summary>Fired when a chainling's HP/AP values change (instanceId, hp, ap).</summary>
        public event Action<string, int, int> OnVitalsChanged;

        /// <summary>Fired at the end of each turn.</summary>
        public event Action<int> OnTurnEnded;

        /// <summary>Fired when a chainling faints (chainlingId, teamIndex, slotIndex).</summary>
        public event Action<string, int, int> OnChainlingFainted;

        /// <summary>Fired when the battle ends.</summary>
        public event Action<BattleResult> OnBattleEnded;

        /// <summary>
        /// Fired when <see cref="StartBattle"/> cannot form teams (missing/empty providers or resolution failure).
        /// Does not fire when a battle is already in progress.
        /// </summary>
        public event Action<string> OnBattleStartFailed;

        /// <summary>
        /// Fired when the controller is waiting for the player to choose actions.
        /// Parameters: (CombatState state, int playerTeamIndex)
        /// The UI should collect actions for all active chainlings on this team,
        /// then call <see cref="SubmitPlayerActions"/>.
        /// </summary>
        public event Action<CombatState, int> OnPlayerTurnWaiting;

        /// <summary>
        /// Fired when the battle ends and replay data is fully populated.
        /// Consumers can serialize this for later replay or analysis.
        /// </summary>
        public event Action<CombatReplayData> OnReplayDataReady;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Inject a <see cref="WagerMatchOrchestrator"/> for PvP matches.
        /// Call before <see cref="StartBattle"/> so that FinalizeBattle can tag
        /// the result as PvP with the correct match ID.
        /// </summary>
        public void SetWagerOrchestrator(WagerMatchOrchestrator orchestrator)
        {
            _wagerOrchestrator = orchestrator;
        }

        /// <summary>
        /// Arms a casual dual-sim session. Parties stay in seat order: A is team 0, B is team 1.
        /// </summary>
        public void BeginCasualPvp(CasualPvpLaunchPayload payload)
        {
            _casualPvp = payload;
            _pvpTurnBridge = payload != null ? payload.TurnBridge : null;
            _casualForfeitState = null;
            _casualResultSubmitted = false;
            _battleFinalized = false;
        }

        public int LocalControlledTeamIndex => _casualPvp != null ? _casualPvp.LocalTeamIndex : 0;

        public TeamState GetControlledTeam(CombatState state)
        {
            if (state == null)
                return null;
            return LocalControlledTeamIndex == 1 ? state.team1 : state.team0;
        }

        public void SetPlayerPartyProviderOverride(IPlayerPartyProvider provider, MonoBehaviour providerComponent = null)
        {
            _runtimePlayerPartyProvider = provider;
            if (providerComponent != null)
                playerPartyProviderOverride = providerComponent;
        }

        public void SetEncounterProviderOverride(IEncounterProvider provider, UnityEngine.Object providerObject = null)
        {
            _runtimeEncounterProvider = provider;

            if (providerObject is MonoBehaviour monoBehaviour)
                encounterProviderOverride = monoBehaviour;
        }

        /// <summary>
        /// Inject a ruleset for the next battle. Mirrors provider override pattern.
        /// </summary>
        public void SetRulesetOverride(CombatRulesetSO rulesetOverride)
        {
            _runtimeRulesetOverride = rulesetOverride;
            if (rulesetOverride != null)
                ruleset = rulesetOverride;
        }

        /// <summary>
        /// Applies a launch-time flee-policy override without changing shared encounter assets.
        /// Use <see cref="EncounterFleePolicy.UseDifficultyDefault"/> to defer to the encounter.
        /// </summary>
        public void SetFleePolicyOverride(EncounterFleePolicy fleePolicyOverride)
        {
            _runtimeFleePolicyOverride = fleePolicyOverride;
        }

        /// <summary>
        /// Preview/test lever. The opponent passes, and the move selector stays closed
        /// so a harness can submit the turn. Production battles leave this off.
        /// </summary>
        public void SetCapturePreviewMode(bool enabled)
        {
            _capturePreviewMode = enabled;
        }

        public bool CapturePreviewMode => _capturePreviewMode;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Runtime State
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private CombatResolver _resolver;
        private CombatState _state;
        private ICombatAI _playerAI;
        private ICombatAI _cpuAI;
        private bool _battleStarted;
        private int _turnCount;
        private Coroutine _fleeRoutine;
        private bool _scriptedFleeSkipPresentation;
        private float _battleStartTime;
        private Coroutine _battleCoroutine;
        private bool _skipNextBeginTurn;
        private bool _authorityCommitBlocked;
        private bool _captureAttempted;
        private bool _captureSucceeded;
        private BattleCompletionReason _captureCompletionReason;
        private string _capturedBackendChainlingId;
        private BattleRewardSettlement _authoritativeRamSettlement;
        private readonly List<BattleCaptureRecord> _captures = new List<BattleCaptureRecord>();

        // Previous KO state for detecting new faints between steps
        private readonly HashSet<string> _previouslyFainted = new HashSet<string>();
        private readonly Dictionary<string, VitalSnapshot> _lastVitals = new Dictionary<string, VitalSnapshot>();

        // Player input pause/resume
        private List<CombatAction> _pendingPlayerActions;
        private bool _playerActionsReady;
        private bool _isWaitingForPlayerActions;
        private bool _waitingForBattleIntro;
        private bool _resumeAutoRunAfterIntro;

        // Replay recording (passive â€” does not affect gameplay)
        private CombatReplayData _replayData;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // ICombatStateSource
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public CombatState State => _state;
        public CombatEventLog EventLog => _resolver?.Log;
        public bool BattleStarted => _battleStarted;

        /// <summary>Whether the player side uses manual input instead of AI.</summary>
        public bool UsePlayerInput => usePlayerInput;

        /// <summary>Whether turns auto-execute after <see cref="StartBattle"/>.</summary>
        public bool AutoRunTurnsEnabled => autoRunTurns;

        /// <summary>Move definitions database used to resolve runtime move metadata.</summary>
        public MoveDatabaseSO MoveDatabase => moveDatabase;
        public SpeciesDatabaseSO SpeciesDatabase => speciesDatabase;
        public ModifierCatalogSO ModifierCatalog => modifierCatalog;

        /// <summary>Authoritative type chemistry matrix for move matchup preview and runtime resolver.</summary>
        public TypeChemistryMatrixSO TypeChemistryMatrix => chemistryMatrix;

        /// <summary>Status catalog used by resolver and presentation feedback.</summary>
        public StatusCatalogSO StatusCatalog => statusCatalog;

        /// <summary>Shared battle pacing/feedback profile when assigned.</summary>
        public CombatBattlePacingProfileSO PacingProfile => pacingProfile;

        /// <summary>True while battle flow is paused waiting for manual player action selection.</summary>
        public bool IsWaitingForPlayerActions => _isWaitingForPlayerActions;

        /// <summary>True while battle flow is paused waiting for the intro sequence to complete.</summary>
        public bool IsBattleIntroPending => _waitingForBattleIntro;

        /// <summary>True when a direct post-battle router override is assigned.</summary>
        public bool HasRewardsRouterOverride => rewardsRouterOverride is ICombatRewardsRouter;

        /// <summary>
        /// Explicit runtime toggle for manual player action selection.
        /// UI installers can call this to ensure the action selector bridge is active.
        /// </summary>
        public void SetUsePlayerInput(bool enabled)
        {
            usePlayerInput = enabled;
        }

        /// <summary>Expose the resolver's RNG for external consumers that need it.</summary>
        public CombatRng Rng => _resolver?.Rng;

        /// <summary>The replay data for the current/last battle. Null until StartBattle is called.</summary>
        public CombatReplayData ReplayData => _replayData;

        /// <summary>The resolved encounter snapshot for the current or most recent battle.</summary>
        public EncounterSnapshot CurrentEncounter => _replayData != null ? _replayData.encounter : null;

        /// <summary>True when this battle is being driven by the PvP wager flow.</summary>
        public bool IsPvP => _wagerOrchestrator != null;

        /// <summary>True for a casual server-locked dual sim. This does not require a wager orchestrator.</summary>
        public bool IsCasualPvpSession => _casualPvp != null;

        /// <summary>Resolved flee rule for the current encounter, including any launch-time override.</summary>
        public EncounterFleePolicy CurrentFleePolicy => EncounterFleePolicyResolver.Resolve(
            CurrentEncounter,
            _runtimeFleePolicyOverride);

        /// <summary>True when a player-initiated flee can start at this moment.</summary>
        public bool CanPlayerFlee => EvaluatePlayerFlee().Accepted;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Initialize and start a battle using the configured providers.
        /// </summary>
        public void StartBattle()
        {
            if (_battleStarted)
            {
                Debug.LogWarning("[CombatBattleController] Battle already in progress. Call ResetBattle() first.");
                return;
            }

            if (presenter == null)
                presenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);

            AuthoritativeClientBattleState authoritativeState = null;
            if (_casualPvp == null)
                AuthoritativeBattleLaunchContext.TryReadState(out authoritativeState);
            int battleSeed = _casualPvp != null && _casualPvp.Seed != 0
                ? _casualPvp.Seed
                : authoritativeState != null && authoritativeState.seed != 0
                    ? unchecked((int)authoritativeState.seed)
                    : deterministicMode ? seed : Environment.TickCount;
            CombatRulesetSO activeRuleset = ResolveRuleset();
            CombatResolverConfigSO resolverConfig = activeRuleset != null
                ? activeRuleset.ResolveResolverConfig()
                : BalanceConfigRuntime.ResolverConfig;
            _resolver = new CombatResolver(
                chemistryMatrix,
                statusCatalog,
                battleSeed,
                chemistryStrictMode,
                modifierCatalog,
                resolverConfig,
                activeRuleset);

            // Resolve providers
            var playerParty = ResolvePlayerParty();
            var encounter = ResolveEncounter(battleSeed);

            if (playerParty == null || playerParty.Count == 0)
            {
                const string msg = "Player party is empty. Cannot start battle.";
                Debug.LogError("[CombatBattleController] " + msg);
                OnBattleStartFailed?.Invoke(msg);
                return;
            }

            if (encounter == null || encounter.Count == 0)
            {
                const string msg = "Encounter is empty. Cannot start battle.";
                Debug.LogError("[CombatBattleController] " + msg);
                OnBattleStartFailed?.Invoke(msg);
                return;
            }

            // Build teams via RosterSlotUtility (use resolver ruleset so eligibility/mutators match ConfigStamp)
            CombatRulesetSO rosterRuleset = _resolver.Ruleset;
            var team0 = RosterSlotUtility.BuildTeamFromParty(
                0, playerParty, speciesDatabase, moveDatabase, battleSeed, _resolver.Rng, modifierCatalog, rosterRuleset);
            var team1 = RosterSlotUtility.BuildTeamFromEncounter(
                1, encounter, speciesDatabase, moveDatabase, battleSeed, _resolver.Rng, modifierCatalog, rosterRuleset);
            if (_casualPvp != null)
            {
                RosterSlotUtility.StampStablePvpIds(team0);
                RosterSlotUtility.StampStablePvpIds(team1);
                if (_casualPvp.FullHeal)
                {
                    RosterSlotUtility.ApplyFullHeal(team0);
                    RosterSlotUtility.ApplyFullHeal(team1);
                }
            }

            // Create AIs
            _playerAI = CreateAI(playerAI);

            // Encounter may override AI personality
            var cpuPersonality = encounter.aiPersonality;
            _cpuAI = CreateAI(cpuPersonality);

            // Initialize battle
            _state = _resolver.InitializeBattle(team0, team1);
            if (_casualPvp == null && authoritativeState != null && authoritativeState.vitals != null && authoritativeState.vitals.Count > 0)
                ApplyAuthoritativeCheckpoint(authoritativeState, authoritativeState.hasActionCheckpoint);
            _battleStarted = true;
            _turnCount = authoritativeState != null && authoritativeState.hasActionCheckpoint
                ? Mathf.Max(1, authoritativeState.turnNumber)
                : 0;
            _skipNextBeginTurn = authoritativeState != null && authoritativeState.hasActionCheckpoint;
            _authorityCommitBlocked = false;
            _captureAttempted = false;
            _captureSucceeded = false;
            _captureCompletionReason = BattleCompletionReason.Standard;
            _capturedBackendChainlingId = string.Empty;
            _authoritativeRamSettlement = null;
            _captures.Clear();
            _battleStartTime = Time.realtimeSinceStartup;
            _previouslyFainted.Clear();
            _lastVitals.Clear();

            // Record initial faint state (should be empty, but be safe)
            RecordFaintState();

            // Initialize replay data
            _replayData = new CombatReplayData
            {
                seed = battleSeed,
                rulesetId = _resolver.RulesetId,
                configHash = _resolver.ConfigHash,
                chemistryStrictMode = chemistryStrictMode,
                playerParty = playerParty,
                encounter = encounter,
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };

            Debug.Log($"[CombatBattleController] Battle started â€” seed {battleSeed} | " +
                      $"Player({team0.chainlings.Count}) vs {encounter.teamName}({team1.chainlings.Count}) | " +
                      $"AI: {_playerAI.PersonalityName} vs {_cpuAI.PersonalityName}");

            bool shouldWaitForIntro = waitForBattleIntro && presenter != null && presenter.isActiveAndEnabled;
            _waitingForBattleIntro = shouldWaitForIntro;
            _resumeAutoRunAfterIntro = shouldWaitForIntro && autoRunTurns;

            OnBattleStarted?.Invoke(_state);
            EmitVitalsChangedIfNeeded(forceEmitAll: true);

            if (usePlayerInput && !autoRunTurns)
            {
                Debug.LogWarning("[CombatBattleController] usePlayerInput is enabled while autoRunTurns is false. Call ExecuteNextTurn() to request player actions.");
            }

            // Auto-run if configured
            if (autoRunTurns)
            {
                if (_waitingForBattleIntro)
                {
                    Debug.Log("[CombatBattleController] Waiting for battle intro before starting turn flow.");
                }
                else
                {
                    StartAutoRunCoroutine();
                }
            }
        }

        /// <summary>
        /// Execute a single turn. Use when autoRunTurns is false.
        /// </summary>
        public void ExecuteNextTurn()
        {
            if (!_battleStarted || _state == null || !_state.isActive || _authorityCommitBlocked)
                return;

            if (_waitingForBattleIntro)
            {
                Debug.Log("[CombatBattleController] ExecuteNextTurn blocked while battle intro is pending.");
                return;
            }

            if (_battleCoroutine != null)
            {
                Debug.LogWarning("[CombatBattleController] A turn coroutine is already running.");
                return;
            }

            _battleCoroutine = StartCoroutine(ExecuteTurnCoroutine());
        }

        /// <summary>
        /// Attempts a player-initiated flee using the active encounter and PvP rules.
        /// Callers receive a reason suitable for their own feedback surface when it is denied.
        /// </summary>
        public FleeRequestResult RequestFlee(FleeRequestSource source = FleeRequestSource.Legacy)
        {
            FleeRequestResult evaluation = EvaluatePlayerFlee();
            if (!evaluation.Accepted)
            {
                Debug.Log($"[CombatBattleController] Flee request denied ({source}): {evaluation.Message}");
                return evaluation;
            }

            _fleeRoutine = StartCoroutine(FleeBattleRoutine());
            return evaluation;
        }

        /// <summary>
        /// Compatibility entry point for existing player-facing callers. It honors the same policy as
        /// <see cref="RequestFlee"/>; scripted introductions must use <see cref="ForceFleeForScriptedEncounter"/>.
        /// </summary>
        public void FleeBattle()
        {
            RequestFlee(FleeRequestSource.Legacy);
        }

        /// <summary>
        /// Ends a non-PvP battle for authored intro/cutscene flow. This deliberately bypasses the player
        /// flee lock because it represents a scripted outcome, not a player choice.
        /// </summary>
        public FleeRequestResult ForceFleeForScriptedEncounter()
        {
            if (!_battleStarted || _state == null || !_state.isActive)
                return new FleeRequestResult(FleeRequestStatus.BattleInactive, CurrentFleePolicy, "There is no active battle to flee.");
            if (_fleeRoutine != null)
                return new FleeRequestResult(FleeRequestStatus.AlreadyResolving, CurrentFleePolicy, "Your team is already withdrawing.");
            if (IsPvP || IsCasualPvpSession)
                return new FleeRequestResult(FleeRequestStatus.UnavailableInPvP, CurrentFleePolicy, "Scripted flee is unavailable in PvP.");

            _scriptedFleeSkipPresentation = true;
            _fleeRoutine = StartCoroutine(FleeBattleRoutine());
            return new FleeRequestResult(FleeRequestStatus.Accepted, CurrentFleePolicy, "Your team is withdrawing.");
        }

        private FleeRequestResult EvaluatePlayerFlee()
        {
            bool isActive = _battleStarted && _state != null && _state.isActive;
            return EncounterFleePolicyResolver.Evaluate(
                isActive,
                _fleeRoutine != null,
                IsPvP || IsCasualPvpSession,
                CurrentEncounter,
                _runtimeFleePolicyOverride);
        }

        private IEnumerator FleeBattleRoutine()
        {
            try
            {
                if (_battleCoroutine != null)
                {
                    StopCoroutine(_battleCoroutine);
                    _battleCoroutine = null;
                }

                _isWaitingForPlayerActions = false;
                _pendingPlayerActions = null;
                _playerActionsReady = false;
                ClearBattleIntroState();
                presenter?.AbortQueuedPresentation();

                _state.isActive = false;
                _state.currentPhase = TurnPhase.BattleOver;
                _state.winnerTeamIndex = 1;

                Vector3 fleeAudioPos = presenter != null
                    ? presenter.GetPlayerTeamFleeAudioWorldPosition()
                    : Vector3.zero;

                TryPlayFleeSound(fleeAudioPos);

                float fleeAnimDuration = presenter != null
                    ? presenter.PlayFleePresentationForPlayerTeam()
                    : 0f;

                if (waitForFleePresentation && fleeAnimDuration > 0f)
                {
                    float waitSeconds = Mathf.Min(fleeAnimDuration, Mathf.Max(0f, fleePresentationMaxWaitSeconds));
                    if (waitSeconds > 0f)
                        yield return new WaitForSecondsRealtime(waitSeconds);
                }

                bool isPvP = false;
                string pvpMatchId = null;
                if (_wagerOrchestrator != null)
                {
                    var wState = _wagerOrchestrator.State;
                    if (wState == WagerMatchState.InProgress || wState == WagerMatchState.Locked)
                    {
                        isPvP = true;
                        pvpMatchId = _wagerOrchestrator.CurrentMatch?.matchId;
                    }
                }

                var result = new BattleResult
                {
                    winnerTeamIndex = 1,
                    turnCount = _turnCount,
                    durationSeconds = Time.realtimeSinceStartup - _battleStartTime,
                    wasFled = true,
                    skipPostBattlePresentation = _scriptedFleeSkipPresentation,
                    completionReason = BattleCompletionReason.PlayerFled,
                    seed = _resolver != null ? _resolver.Rng.Seed : seed,
                    battleType = isPvP ? BattleType.PvP : BattleType.PvE,
                    pvpMatchId = pvpMatchId,
                    rulesetId = _resolver != null ? _resolver.RulesetId : null,
                    configHash = _resolver != null ? _resolver.ConfigHash : null,
                    authoritativeRamSettlement = ResolveAuthoritativeRamSettlement(),
                };

                var encounter = _replayData != null ? _replayData.encounter : null;
                StampEncounterContext(result, encounter);

                if (_resolver?.Log != null && _state?.team1 != null)
                {
                    result.enemyKOsBySpecies = BattleLogAnalyzer.CountEnemyKOsBySpecies(_resolver.Log.Events, _state.team1);
                    result.totalEnemyKOs = BattleLogAnalyzer.CountEnemyKOs(_resolver.Log.Events, _state.team1);
                    result.playerParticipants = BuildParticipants(
                        _state.team0,
                        _replayData != null ? _replayData.playerParty : null,
                        _resolver.Log);
                    result.pveStatContributions = BattleStatsAggregator.AggregateCombatStats(_resolver.Log.Events, result);
                    string eventHash = BattleStatsAggregator.ComputeLegacyEventHash(_resolver.Log.Events, result.seed);
                    result.forensicHash = BuildForensicHash(eventHash);
                }

                Debug.Log($"[CombatBattleController] Battle fled after {_turnCount} turns ({result.durationSeconds:F1}s)");

                if (_resolver?.Log != null)
                {
                    string team0 = _state?.team0?.teamName ?? "Team0";
                    string team1 = _state?.team1?.teamName ?? "Team1";
                    _resolver.Log.LogBattleEnded(result, team0, team1);
                }

                PrepareRewardsBeforePostBattlePresentation(result);
                OnBattleEnded?.Invoke(result);

                if (_replayData != null)
                {
                    _replayData.result = result;
                    OnReplayDataReady?.Invoke(_replayData);
                }

                RouteRewardsAfterPostBattlePresentation(result);
            }
            finally
            {
                _fleeRoutine = null;
                _scriptedFleeSkipPresentation = false;
            }
        }

        private void TryPlayFleeSound(Vector3 worldPosition)
        {
            if (fleeSound == null)
                return;

            if (ResolveCombatAudioBridge() is ICombatAudioBridge bridge)
            {
                bridge.PlayOneShot(fleeSound, worldPosition);
                return;
            }

            Debug.LogWarning(
                "[CombatBattleController] fleeSound is assigned but no ICombatAudioBridge was found. Assign combatAudioBridge (Audio bridge) or add CombatAudioBridge to the scene.",
                this);
        }

        /// <summary>
        /// Resolves the shared combat audio bridge for battle-controller SFX. Prefer the serialized reference; otherwise the first ICombatAudioBridge in loaded scenes.
        /// </summary>
        private ICombatAudioBridge ResolveCombatAudioBridge()
        {
            if (combatAudioBridge != null && combatAudioBridge is ICombatAudioBridge fromField)
                return fromField;

            return FindFirstCombatAudioBridgeBehaviour() as ICombatAudioBridge;
        }

        private static MonoBehaviour FindFirstCombatAudioBridgeBehaviour()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatAudioBridge)
                    return behaviours[i];
            }

            return null;
        }

        /// <summary>
        /// Reset the battle state so a new battle can be started.
        /// </summary>
        public void ResetBattle()
        {
            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }

            if (_fleeRoutine != null)
            {
                StopCoroutine(_fleeRoutine);
                _fleeRoutine = null;
            }

            _battleStarted = false;
            _state = null;
            _resolver = null;
            _playerAI = null;
            _cpuAI = null;
            _turnCount = 0;
            _captureAttempted = false;
            _captureSucceeded = false;
            _captureCompletionReason = BattleCompletionReason.Standard;
            _capturedBackendChainlingId = string.Empty;
            _authoritativeRamSettlement = null;
            _captures.Clear();
            _previouslyFainted.Clear();
            _lastVitals.Clear();
            _pendingPlayerActions = null;
            _playerActionsReady = false;
            _isWaitingForPlayerActions = false;
            ClearBattleIntroState();
            _replayData = null;
        }

        /// <summary>
        /// Releases the intro gate and resumes auto-run when appropriate.
        /// Safe to call multiple times.
        /// </summary>
        public void NotifyBattleIntroComplete()
        {
            Debug.Log("[CombatBattleController] NotifyBattleIntroComplete() received.", this);

            if (!_waitingForBattleIntro)
            {
                _resumeAutoRunAfterIntro = false;
                return;
            }

            Debug.Log("[CombatBattleController] Clearing _waitingForBattleIntro.", this);
            _waitingForBattleIntro = false;
            bool resumeAutoRun = _resumeAutoRunAfterIntro;
            _resumeAutoRunAfterIntro = false;

            if (resumeAutoRun && autoRunTurns && _battleStarted && _state != null && _state.isActive)
                StartAutoRunCoroutine();
            else if (usePlayerInput && !autoRunTurns && _battleStarted && _state != null && _state.isActive)
            {
                var encounter = CurrentEncounter;
                if (encounter != null && encounter.skipManualTurnAfterIntro)
                    Debug.Log("[CombatBattleController] Skipping ExecuteNextTurn (encounter.skipManualTurnAfterIntro).", this);
                else
                    ExecuteNextTurn();
            }
        }

        /// <summary>
        /// Submit the player's chosen actions for the current turn.
        /// Called by the UI layer after the player has selected actions for all active chainlings.
        /// Only valid when <see cref="usePlayerInput"/> is true and the controller is waiting.
        /// </summary>
        public void SubmitPlayerActions(List<CombatAction> actions)
        {
            if (!usePlayerInput)
            {
                Debug.LogWarning("[CombatBattleController] SubmitPlayerActions called but usePlayerInput is false.");
                return;
            }

            if (!_battleStarted || _state == null)
            {
                Debug.LogWarning("[CombatBattleController] SubmitPlayerActions called but no battle is active.");
                return;
            }

            int controlledTeam = LocalControlledTeamIndex;
            TeamState controlled = controlledTeam == 1 ? _state.team1 : _state.team0;
            _pendingPlayerActions = NormalizeSubmittedActionsForTeam(controlled, actions, controlledTeam);
            _playerActionsReady = true;
            _isWaitingForPlayerActions = false;
        }

        /// <summary>
        /// Applies an authoritative capture result to the local presentation mirror.
        /// Accepted attempts consume the Orb. Lock captures the target; an empty meter
        /// makes that target flee; timeout leaves the target in battle. Removed targets
        /// never create a KO. Capture is already a queued slot action, so this does not
        /// submit turn passes.
        /// </summary>
        public bool ApplyCaptureOutcome(string targetInstanceId, CaptureOutcome outcome)
        {
            if (!_battleStarted || _state == null || !_state.isActive || outcome == null)
                return false;
            if (outcome.Kind == CaptureOutcomeKind.Rejected || outcome.Kind == CaptureOutcomeKind.Interrupted)
                return false;

            _captureAttempted = true;
            if (outcome.Settlement != null)
                _authoritativeRamSettlement = outcome.Settlement;
            if (outcome.RemovesTargetFromBattle)
            {
                if (!TryRemoveCaptureTarget(targetInstanceId, out string removedInstanceId))
                {
                    Debug.LogWarning($"[CombatBattleController] Capture target '{targetInstanceId}' was not an active opponent.");
                    return false;
                }

                if (presenter != null && presenter.isActiveAndEnabled)
                    presenter.PresentCaptureTargetRemoved(removedInstanceId);

                _captureSucceeded |= outcome.Kind == CaptureOutcomeKind.Locked;
                if (outcome.Kind == CaptureOutcomeKind.Locked)
                {
                    if (!string.IsNullOrWhiteSpace(outcome.GrantedChainlingId))
                        _capturedBackendChainlingId = outcome.GrantedChainlingId;
                    RecordSuccessfulCapture(FindCaptureTarget(removedInstanceId), outcome);
                }

                bool terminal = _state.team1.IsDefeated;
                if (terminal)
                {
                    _captureCompletionReason = outcome.Kind == CaptureOutcomeKind.Locked
                        ? BattleCompletionReason.Captured
                        : BattleCompletionReason.TargetEscaped;
                    _state.isActive = false;
                    _state.currentPhase = TurnPhase.BattleOver;
                    _state.winnerTeamIndex = -1;
                    _isWaitingForPlayerActions = false;
                    _pendingPlayerActions = new List<CombatAction>();
                    _playerActionsReady = true;
                    return true;
                }
            }

            return true;
        }

        private bool TryRemoveCaptureTarget(string targetInstanceId, out string removedInstanceId)
        {
            removedInstanceId = targetInstanceId ?? "";
            if (_state == null || _state.team1 == null)
                return false;

            if (!string.IsNullOrWhiteSpace(targetInstanceId) &&
                _state.team1.RemoveActiveWithoutKo(targetInstanceId, out _))
            {
                return true;
            }

            List<ChainlingInstance> actives = _state.team1.GetActiveChainlings();
            if (actives != null &&
                actives.Count == 1 &&
                actives[0] != null &&
                !string.IsNullOrWhiteSpace(actives[0].instanceId) &&
                _state.team1.RemoveActiveWithoutKo(actives[0].instanceId, out _))
            {
                removedInstanceId = actives[0].instanceId;
                return true;
            }

            return false;
        }

        private ChainlingInstance FindCaptureTarget(string instanceId)
        {
            if (_state?.team1?.chainlings == null)
                return null;

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                for (int i = 0; i < _state.team1.chainlings.Count; i++)
                {
                    ChainlingInstance candidate = _state.team1.chainlings[i];
                    if (candidate != null &&
                        string.Equals(candidate.instanceId, instanceId, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            List<ChainlingInstance> actives = _state.team1.GetActiveChainlings();
            if (actives != null && actives.Count == 1)
                return actives[0];
            return null;
        }

        private void RecordSuccessfulCapture(ChainlingInstance target, CaptureOutcome outcome)
        {
            float duration = outcome != null ? outcome.DurationSeconds : 0f;
            if (duration <= 0f && outcome != null && outcome.ElapsedTicks > 0)
                duration = outcome.ElapsedTicks / 30f;

            string fallbackName = target != null && !string.IsNullOrWhiteSpace(target.displayName)
                ? target.displayName
                : target != null && !string.IsNullOrWhiteSpace(target.speciesId)
                    ? target.speciesId
                    : "Chainling";

            _captures.Add(new BattleCaptureRecord
            {
                speciesId = target != null ? target.speciesId ?? "" : "",
                displayName = fallbackName.Trim(),
                subjectLabel = CaptureResultCopy.FormatCapturedSubject(target, fallbackName),
                isMutated = target != null && target.isMutated,
                isEntangled = target != null && target.isEntangled,
                durationSeconds = Mathf.Max(0f, duration),
                succeeded = true,
                backendChainlingId = outcome != null ? outcome.GrantedChainlingId ?? "" : "",
            });
        }

        public bool ExportBattleLog()
        {
            if (_resolver == null || _resolver.Log == null)
            {
                Debug.LogWarning("[CombatBattleController] Cannot export battle log - no active resolver/log.");
                return false;
            }

            if (!CombatBattleLogExporter.TryExport(_resolver.Log, out var result))
            {
                Debug.LogWarning("[CombatBattleController] Battle log export failed.");
                return false;
            }

            Debug.Log($"[CombatBattleController] Battle log exported to: {result.directoryPath}");
            return true;
        }

        private void Update()
        {
            if (!enableBattleLogExportHotkey)
                return;

            if (!Input.GetKeyDown(exportBattleLogHotkey))
                return;

            ExportBattleLog();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Turn Execution
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private List<CombatAction> NormalizeSubmittedActionsForTeam(TeamState team, List<CombatAction> actions, int teamIndex)
        {
            int activeSlotCount = team != null && team.activeIndices != null ? team.activeIndices.Count : 0;
            if (activeSlotCount <= 0)
                return new List<CombatAction>();

            var incoming = actions ?? new List<CombatAction>();
            if (incoming.Count == activeSlotCount)
                return new List<CombatAction>(incoming);

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
                $"[CombatBattleController] Normalized submitted player actions for team {teamIndex}: " +
                $"received {incoming.Count}, active slots {activeSlotCount}, alive active slots {aliveActiveCount}, strategy {strategy}.";
            Debug.LogWarning(message);
            _resolver?.Log?.LogMessage(CombatEventType.ValidationError, message, _state != null ? _state.currentTurn : -1);
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

        private IEnumerator AutoRunBattle()
        {
            while (_state != null && _state.isActive && !_authorityCommitBlocked && _turnCount < maxTurns)
            {
                yield return ExecuteTurnCoroutine();

                if (_state != null && _state.isActive && ResolveTurnEndDelay() > 0f)
                    yield return new WaitForSeconds(ResolveTurnEndDelay());
            }

            _battleCoroutine = null;
        }

        private void StartAutoRunCoroutine()
        {
            if (_battleCoroutine != null)
                StopCoroutine(_battleCoroutine);

            _battleCoroutine = StartCoroutine(AutoRunBattle());
        }

        private IEnumerator ExecuteTurnCoroutine()
        {
            if (_state == null || !_state.isActive)
            {
                _battleCoroutine = null;
                yield break;
            }

            // â”€â”€ Begin Turn â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_skipNextBeginTurn)
            {
                _skipNextBeginTurn = false;
                _state.currentPhase = TurnPhase.ActionSelection;
            }
            else
            {
                _resolver.BeginTurn(_state);
                _turnCount++;
            }
            EmitVitalsChangedIfNeeded();

            // â”€â”€ Generate actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            List<CombatAction> actions0 = null;
            List<CombatAction> actions1 = null;

            if (_casualPvp != null)
            {
                _casualTurnAborted = false;
                yield return RunCasualPvpTurn();
                if (_casualTurnAborted || _state == null || !_state.isActive)
                {
                    _battleCoroutine = null;
                    yield break;
                }

                actions0 = _casualActions0 ?? new List<CombatAction>();
                actions1 = _casualActions1 ?? new List<CombatAction>();
            }
            else if (usePlayerInput)
            {
                // Wait for the UI to provide player actions
                _playerActionsReady = false;
                _pendingPlayerActions = null;
                _isWaitingForPlayerActions = true;
                Debug.Log("[CombatBattleController] Waiting for player action submission.");
                OnPlayerTurnWaiting?.Invoke(_state, 0);

                yield return new WaitUntil(() => _playerActionsReady);
                if (_state == null || !_state.isActive)
                {
                    _isWaitingForPlayerActions = false;
                    FinalizeBattle();
                    _battleCoroutine = null;
                    yield break;
                }
                actions0 = _pendingPlayerActions ?? new List<CombatAction>();
                _pendingPlayerActions = null;
                _playerActionsReady = false;
                _isWaitingForPlayerActions = false;
                Debug.Log($"[CombatBattleController] Player actions received ({actions0.Count}).");

                float confirmDelay = ResolvePostSelectionConfirmDelay();
                if (confirmDelay > 0f)
                    yield return new WaitForSeconds(confirmDelay);
            }
            else
            {
                actions0 = _playerAI.GenerateActions(_state, 0, _resolver.Rng);
            }

            if (_casualPvp == null)
            {
                actions1 = _capturePreviewMode
                    ? CombatCapturePreviewActions.BuildOpponentPasses(_state)
                    : _cpuAI.GenerateActions(_state, 1, _resolver.Rng);
            }

            // â”€â”€ Record turn actions for replay â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_replayData != null)
                _replayData.turnActions.Add(new TurnActionRecord(_turnCount, actions0, actions1));

            // â”€â”€ Build action queue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var actionQueue = _resolver.BuildActionQueueForCurrentTurn(_state, actions0, actions1);

            OnTurnBegan?.Invoke(_turnCount, actionQueue);

            // â”€â”€ Execute steps one at a time â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            int prevEventCount = _resolver.Log.EventCount;

            for (int i = 0; i < actionQueue.Count; i++)
            {
                if (!_state.isActive) break;

                var step = actionQueue[i];
                HudSlotRef actorRef = default;

                int stepEventStart = prevEventCount;
                bool stillActive = _resolver.ExecuteActionStep(_state, step);
                int stepEventEnd = _resolver.Log.EventCount;
                bool narrationHandled = false;

                if (_casualPvp == null && (step.action == null || step.action.actionType != CombatActionType.Capture))
                {
                    yield return CommitAuthoritativeStep(step, i);
                    if (_authorityCommitBlocked)
                    {
                        _battleCoroutine = null;
                        yield break;
                    }
                }

                actorRef = EmitStepSelectionEvents(step);
                narrationHandled = ComposeAndQueueNarration(stepEventStart, stepEventEnd, _turnCount, i);

                // Fire events for new combat events generated by this step
                FireNewEvents(stepEventStart);
                prevEventCount = stepEventEnd;

                // Ensure presenter has events in queue before we wait (avoids race with Update polling)
                if (presenter != null)
                    presenter.EnqueueNewEventsImmediate();

                // Check for new faints
                DetectNewFaints();
                if (actorRef.IsValid)
                    OnActionTargetsResolved?.Invoke(actorRef, new List<HudSlotRef>());

                // Yield for animation pacing: wait for presenter to finish, then a min floor
                if (presenter != null)
                {
                    yield return new WaitUntil(() => presenter == null || presenter.IsEventQueueIdle);
                    if (!narrationHandled)
                    {
                        float stepDelay = ResolveMinStepDelay();
                        if (stepDelay > 0f)
                            yield return new WaitForSeconds(stepDelay);
                    }
                }
                else if (actionStepDelay > 0f)
                {
                    yield return new WaitForSeconds(actionStepDelay);
                }

                // Sync HP bar with damage popup: emit vitals after animations complete
                EmitVitalsChangedIfNeeded();

                yield return PlayPendingCaptureIfAny();
                if (_state == null || !_state.isActive)
                    break;

                if (!stillActive) break;
            }

            // â”€â”€ End Turn â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_state.isActive)
            {
                int endTurnEventStart = prevEventCount;
                bool continues = _resolver.EndTurn(_state);
                int endTurnEventEnd = _resolver.Log.EventCount;

                if (_casualPvp == null)
                {
                    yield return CommitAuthoritativeStep(default, actionQueue.Count, "end_turn");
                    if (_authorityCommitBlocked)
                    {
                        _battleCoroutine = null;
                        yield break;
                    }
                }

                ComposeAndQueueNarration(endTurnEventStart, endTurnEventEnd, _turnCount, actionQueue.Count);

                // Fire any end-of-turn events (DoT damage, status expiry, etc.)
                FireNewEvents(endTurnEventStart);
                if (presenter != null)
                {
                    presenter.EnqueueNewEventsImmediate();
                    yield return new WaitUntil(() => presenter == null || presenter.IsEventQueueIdle);
                }
                DetectNewFaints();
                EmitVitalsChangedIfNeeded();

                OnTurnEnded?.Invoke(_turnCount);

                if (!continues)
                    FinalizeBattle();
            }
            else
            {
                FinalizeBattle();
            }

            _battleCoroutine = null;
        }

        private void ApplyAuthoritativeCheckpoint(AuthoritativeClientBattleState checkpoint, bool restoreTurn)
        {
            if (_state == null || checkpoint?.vitals == null)
                return;

            ApplyTeamCheckpoint(_state.team0, checkpoint.vitals, 0);
            ApplyTeamCheckpoint(_state.team1, checkpoint.vitals, 1);
            if (!restoreTurn)
                return;

            _state.currentTurn = Mathf.Max(1, checkpoint.turnNumber);
            _state.currentPhase = TurnPhase.ActionSelection;
        }

        private static void ApplyTeamCheckpoint(
            TeamState team,
            List<AuthoritativeClientBattleVital> vitals,
            int teamIndex)
        {
            if (team?.chainlings == null)
                return;

            var active = new List<int>(CombatConstants.ACTIVE_PER_TEAM);
            for (int rosterIndex = 0; rosterIndex < team.chainlings.Count; rosterIndex++)
            {
                AuthoritativeClientBattleVital saved = vitals.Find(v =>
                    v != null && v.teamIndex == teamIndex && v.rosterIndex == rosterIndex);
                if (saved == null)
                    continue;

                ChainlingInstance chainling = team.chainlings[rosterIndex];
                chainling.currentHP = Mathf.Clamp(saved.currentHp, 0, Mathf.Max(1, chainling.EffectiveMaxHP));
                chainling.currentAP = Mathf.Max(0, saved.currentAp);
                chainling.ClampAP();
                chainling.isAlive = chainling.currentHP > 0;
                chainling.SyncDisplayVitals();
                if (saved.isActive && chainling.isAlive && active.Count < CombatConstants.ACTIVE_PER_TEAM)
                    active.Add(rosterIndex);
            }

            if (active.Count > 0)
                team.activeIndices = active;
        }

        private IEnumerator CommitAuthoritativeStep(
            CombatResolver.CombatActionStep step,
            int stepIndex,
            string actionOverride = null)
        {
            CombatSessionEnvelope current = AuthoritativeBattleLaunchContext.Current;
            if (current == null || !current.IsResumable || string.IsNullOrWhiteSpace(current.BattleId))
                yield break;

            ICombatSessionService service = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance.CombatSessionService
                : null;
            if (service == null)
            {
                BlockOnAuthorityFailure("Battle authority is unavailable.");
                yield break;
            }

            string actionId = !string.IsNullOrWhiteSpace(actionOverride) ? actionOverride : ResolveActionId(step);
            var command = new CombatTeamCommand
            {
                CommandId = BuildActionCheckpointCommandId(current, stepIndex, actionId),
                ExpectedVersion = current.Version,
                Kind = "action_step",
                ActorId = step.actor != null ? ResolveAuthorityEntityId(step.actor) : string.Empty,
                ActionId = actionId,
                TurnNumber = Mathf.Max(1, _turnCount),
                StepIndex = Mathf.Max(0, stepIndex),
                BattleActive = _state != null && _state.isActive,
                WinnerTeamIndex = _state != null ? _state.winnerTeamIndex : -1,
                EndReason = _state != null && !_state.isActive
                    ? (_state.winnerTeamIndex == 0 ? "Victory" : "Defeat")
                    : string.Empty,
                Vitals = BuildAuthorityVitals(),
            };

            Task<CombatSessionEnvelope> commit = service.SubmitTeamCommandAsync(current.BattleId, command);
            while (!commit.IsCompleted)
                yield return null;

            if (commit.IsFaulted || commit.IsCanceled || commit.Result == null)
            {
                string detail = commit.Exception != null
                    ? commit.Exception.GetBaseException().Message
                    : "The battle checkpoint was not acknowledged.";
                BlockOnAuthorityFailure(detail);
                yield break;
            }

            AuthoritativeBattleLaunchContext.Publish(commit.Result);
        }

        private static string BuildActionCheckpointCommandId(CombatSessionEnvelope current, int stepIndex, string actionId)
        {
            return string.Concat(
                current.BattleId,
                ":",
                current.Version.ToString(),
                ":",
                Mathf.Max(0, stepIndex).ToString(),
                ":",
                actionId ?? string.Empty);
        }

        private List<CombatVitalSnapshot> BuildAuthorityVitals()
        {
            var result = new List<CombatVitalSnapshot>();
            AppendTeamVitals(result, _state?.team0, 0);
            AppendTeamVitals(result, _state?.team1, 1);
            return result;
        }

        private static void AppendTeamVitals(List<CombatVitalSnapshot> destination, TeamState team, int teamIndex)
        {
            if (team?.chainlings == null)
                return;
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                destination.Add(new CombatVitalSnapshot
                {
                    EntityId = ResolveAuthorityEntityId(chainling),
                    TeamIndex = teamIndex,
                    RosterIndex = i,
                    CurrentHp = Mathf.Max(0, chainling.currentHP),
                    CurrentAp = Mathf.Max(0, chainling.currentAP),
                    IsActive = team.activeIndices != null && team.activeIndices.Contains(i),
                });
            }
        }

        private static string ResolveAuthorityEntityId(ChainlingInstance chainling)
        {
            if (chainling == null)
                return string.Empty;
            return string.IsNullOrWhiteSpace(chainling.backendChainlingId)
                ? chainling.instanceId ?? string.Empty
                : chainling.backendChainlingId;
        }

        private static string ResolveActionId(CombatResolver.CombatActionStep step)
        {
            CombatAction action = step.action;
            if (action == null)
                return "unknown";
            if (action.actionType == CombatActionType.UseMove && step.actor?.moves != null &&
                action.moveIndex >= 0 && action.moveIndex < step.actor.moves.Count)
            {
                return step.actor.moves[action.moveIndex]?.definitionId ?? "move";
            }
            return action.actionType.ToString().ToLowerInvariant();
        }

        private void BlockOnAuthorityFailure(string detail)
        {
            _authorityCommitBlocked = true;
            AuthoritativeBattleLaunchContext.SetGateMessage("SIGNAL SYNC UNAVAILABLE — RETRYING");
            Debug.LogError($"[CombatBattleController] Action presentation paused before an uncommitted result: {detail}", this);
        }

        private float ResolvePostSelectionConfirmDelay()
        {
            if (pacingProfile != null)
                return Mathf.Max(0f, pacingProfile.postSelectionConfirmDelay);
            return 0.35f;
        }

        private IEnumerator PlayPendingCaptureIfAny()
        {
            PendingCaptureAttempt pending = _state != null ? _state.ConsumePendingCaptureAttempt() : null;
            if (pending == null)
                yield break;

            CaptureCommandCoordinatorUGUI coordinator = ResolveCaptureCoordinator();
            if (coordinator == null)
                yield break;

            CaptureOutcome outcome = null;
            string targetId = pending.targetInstanceId;
            bool finished = false;
            yield return coordinator.PlayQueuedAttempt(
                _state,
                CurrentEncounter,
                pending,
                (id, result, _) =>
                {
                    targetId = id;
                    outcome = result;
                    finished = true;
                });

            if (!finished)
                yield break;
            if (outcome == null ||
                outcome.Kind == CaptureOutcomeKind.Rejected ||
                outcome.Kind == CaptureOutcomeKind.Interrupted)
            {
                yield break;
            }

            ApplyCaptureOutcome(targetId, outcome);
            EmitVitalsChangedIfNeeded();
        }

        private CaptureCommandCoordinatorUGUI ResolveCaptureCoordinator()
        {
            CaptureCommandCoordinatorUGUI coordinator = GetComponent<CaptureCommandCoordinatorUGUI>();
            if (coordinator == null)
                coordinator = FindFirstObjectByType<CaptureCommandCoordinatorUGUI>(FindObjectsInactive.Include);
            return coordinator;
        }

        private float ResolveMinStepDelay()
        {
            if (pacingProfile != null)
                return Mathf.Max(0f, pacingProfile.minStepDelay);
            return minStepDelay;
        }

        private float ResolveTurnEndDelay()
        {
            if (pacingProfile != null)
                return Mathf.Max(0f, pacingProfile.turnEndDelay);
            return turnEndDelay;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Faint Detection
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void DetectNewFaints()
        {
            if (_state == null) return;

            CheckTeamFaints(_state.team0);
            CheckTeamFaints(_state.team1);
        }

        private void CheckTeamFaints(TeamState team)
        {
            if (team == null || team.chainlings == null) return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var c = team.chainlings[i];
                if (c == null || c.isAlive) continue;

                if (_previouslyFainted.Add(c.instanceId))
                {
                    // Determine slot index (-1 if not in an active slot)
                    int slotIndex = -1;
                    for (int s = 0; s < team.activeIndices.Count; s++)
                    {
                        if (team.activeIndices[s] == i)
                        {
                            slotIndex = s;
                            break;
                        }
                    }

                    OnChainlingFainted?.Invoke(c.instanceId, team.teamIndex, slotIndex);
                }
            }
        }

        private void RecordFaintState()
        {
            if (_state == null) return;
            RecordTeamFaintState(_state.team0);
            RecordTeamFaintState(_state.team1);
        }

        private void RecordTeamFaintState(TeamState team)
        {
            if (team == null || team.chainlings == null) return;
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var c = team.chainlings[i];
                if (c != null && !c.isAlive)
                    _previouslyFainted.Add(c.instanceId);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Event Bridging
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void FireNewEvents(int fromIndex)
        {
            if (_resolver == null) return;

            var events = _resolver.Log.Events;
            for (int i = fromIndex; i < events.Count; i++)
            {
                OnActionStepExecuted?.Invoke(events[i]);
            }
        }

        private HudSlotRef EmitStepSelectionEvents(CombatResolver.CombatActionStep step)
        {
            if (step.actor == null || step.actingTeam == null)
                return default;

            if (!TryBuildHudSlotRef(step.actingTeam, step.actor, out var actorRef))
                return default;

            OnTurnActorSelected?.Invoke(actorRef);

            var targets = BuildTargetsForStep(step, step.actor);
            if (targets.Count > 0)
                OnActionTargetsResolved?.Invoke(actorRef, targets);

            return actorRef;
        }

        private bool TryBuildHudSlotRef(TeamState team, ChainlingInstance chainling, out HudSlotRef slotRef)
        {
            slotRef = default;

            if (team == null || chainling == null || team.activeIndices == null || team.chainlings == null)
                return false;

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0 || idx >= team.chainlings.Count)
                    continue;

                var candidate = team.chainlings[idx];
                if (candidate == null)
                    continue;

                if (candidate.instanceId == chainling.instanceId)
                {
                    slotRef = new HudSlotRef(team.teamIndex, slot, candidate.instanceId);
                    return true;
                }
            }

            return false;
        }

        private List<HudSlotRef> BuildTargetsForStep(CombatResolver.CombatActionStep step, ChainlingInstance actor)
        {
            var targets = new List<HudSlotRef>(4);
            if (step.action == null || actor == null)
                return targets;

            if (step.action.actionType != CombatActionType.UseMove)
                return targets;

            if (actor.moves == null || step.action.moveIndex < 0 || step.action.moveIndex >= actor.moves.Count)
                return targets;

            var move = actor.moves[step.action.moveIndex];
            if (move == null)
                return targets;

            bool hasExplicitTarget = TryResolveExplicitTargetForStep(step, out var explicitTeam, out var explicitTarget);

            bool isDamaging = move.category == MoveCategory.DamagingMelee
                              || move.category == MoveCategory.DamagingRanged;
            if (isDamaging)
            {
                bool hitAllFoes = !HasStatus(actor, StatusId.Anchored) &&
                                  (HasStatus(actor, StatusId.Broadcast) || HasStatus(actor, StatusId.Splinter));
                bool moveHitsAllFoes = move.targeting == MoveTargeting.AllEnemies || move.targeting == MoveTargeting.Field;
                if (hitAllFoes || moveHitsAllFoes)
                {
                    AddAllActiveTargets(step.opposingTeam, targets);
                }
                else
                {
                    var singleTarget = hasExplicitTarget && explicitTeam == step.opposingTeam
                        ? explicitTarget
                        : (step.opposingTeam?.LeadChainling ?? step.opposingTeam?.PartnerChainling);
                    TryAddTarget(targets, step.opposingTeam, singleTarget);
                }

                return targets;
            }

            switch (move.targeting)
            {
                case MoveTargeting.Self:
                    TryAddTarget(targets, step.actingTeam, actor);
                    break;

                case MoveTargeting.SingleEnemy:
                case MoveTargeting.RandomEnemy:
                {
                    var enemy = hasExplicitTarget && explicitTeam == step.opposingTeam
                        ? explicitTarget
                        : (step.opposingTeam?.LeadChainling ?? step.opposingTeam?.PartnerChainling);
                    TryAddTarget(targets, step.opposingTeam, enemy);
                    break;
                }

                case MoveTargeting.SingleAlly:
                {
                    var ally = hasExplicitTarget && explicitTeam == step.actingTeam
                        ? explicitTarget
                        : (FindActiveAlly(step.actingTeam, actor) ?? actor);
                    TryAddTarget(targets, step.actingTeam, ally);
                    break;
                }

                case MoveTargeting.AllAllies:
                    AddAllActiveTargets(step.actingTeam, targets);
                    break;

                case MoveTargeting.AllEnemies:
                    AddAllActiveTargets(step.opposingTeam, targets);
                    break;

                case MoveTargeting.Field:
                    AddAllActiveTargets(step.actingTeam, targets);
                    AddAllActiveTargets(step.opposingTeam, targets);
                    break;
            }

            return targets;
        }

        private bool TryResolveExplicitTargetForStep(CombatResolver.CombatActionStep step, out TeamState team, out ChainlingInstance target)
        {
            team = null;
            target = null;

            if (step.action == null || step.action.targetTeamIndex < 0 || step.action.targetSlotIndex < 0)
                return false;
            if (step.actingTeam == null || step.opposingTeam == null)
                return false;

            if (step.action.targetTeamIndex == step.actingTeam.teamIndex)
                team = step.actingTeam;
            else if (step.action.targetTeamIndex == step.opposingTeam.teamIndex)
                team = step.opposingTeam;
            else
                return false;

            target = team.GetActiveInSlot(step.action.targetSlotIndex);
            return target != null && target.isAlive;
        }

        private void AddAllActiveTargets(TeamState team, List<HudSlotRef> targets)
        {
            if (team == null || team.activeIndices == null || team.chainlings == null)
                return;

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0 || idx >= team.chainlings.Count)
                    continue;

                var chainling = team.chainlings[idx];
                if (chainling != null && chainling.isAlive)
                    TryAddTarget(targets, team, chainling);
            }
        }

        private void TryAddTarget(List<HudSlotRef> targets, TeamState team, ChainlingInstance chainling)
        {
            if (targets == null || team == null || chainling == null)
                return;

            if (!TryBuildHudSlotRef(team, chainling, out var slotRef))
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].instanceId == slotRef.instanceId)
                    return;
            }

            targets.Add(slotRef);
        }

        private ChainlingInstance FindActiveAlly(TeamState team, ChainlingInstance actor)
        {
            if (team == null || team.activeIndices == null)
                return null;

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var candidate = team.GetActiveInSlot(slot);
                if (candidate != null && candidate.isAlive && candidate.instanceId != actor.instanceId)
                    return candidate;
            }

            return null;
        }

        private static bool HasStatus(ChainlingInstance chainling, StatusId status)
        {
            if (chainling == null || chainling.statuses == null)
                return false;

            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == status)
                    return true;
            }

            return false;
        }

        private void EmitVitalsChangedIfNeeded(bool forceEmitAll = false)
        {
            if (_state == null)
                return;

            SyncAllDisplayVitals();
            EmitTeamVitals(_state.team0, forceEmitAll);
            EmitTeamVitals(_state.team1, forceEmitAll);
        }

        private void SyncAllDisplayVitals()
        {
            if (_state == null) return;
            SyncTeamDisplayVitals(_state.team0);
            SyncTeamDisplayVitals(_state.team1);
        }

        private static void SyncTeamDisplayVitals(TeamState team)
        {
            if (team?.chainlings == null) return;
            for (int i = 0; i < team.chainlings.Count; i++)
                team.chainlings[i]?.SyncDisplayVitals();
        }

        private void EmitTeamVitals(TeamState team, bool forceEmitAll)
        {
            if (team == null || team.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling == null || string.IsNullOrEmpty(chainling.instanceId))
                    continue;

                var snapshot = new VitalSnapshot
                {
                    hp = chainling.currentHP,
                    ap = chainling.currentAP,
                };

                if (!forceEmitAll && _lastVitals.TryGetValue(chainling.instanceId, out var last))
                {
                    if (last.hp == snapshot.hp && last.ap == snapshot.ap)
                        continue;
                }

                _lastVitals[chainling.instanceId] = snapshot;
                OnVitalsChanged?.Invoke(chainling.instanceId, snapshot.hp, snapshot.ap);
            }
        }

        private IEnumerator RunCasualPvpTurn()
        {
            _casualActions0 = null;
            _casualActions1 = null;
            if (TryTakeReplayTurn(out List<CombatAction> replay0, out List<CombatAction> replay1))
            {
                _casualActions0 = replay0;
                _casualActions1 = replay1;
                _isWaitingForPlayerActions = false;
                Debug.Log($"[PvPTurn] Replaying turn {_turnCount} match={_casualPvp.MatchId}.");
                yield break;
            }

            if (_pvpTurnBridge == null)
            {
                EndCasualFromServer("cancelled");
                yield break;
            }

            List<CombatAction> resubmit = TakePendingLocalActions(_turnCount);
            bool resentServerPending = false;
            while (_state != null && _state.isActive)
            {
                List<CombatAction> localActions;
                if (resubmit != null)
                {
                    localActions = resubmit;
                    resubmit = null;
                    _isWaitingForPlayerActions = false;
                    Debug.Log($"[PvPTurn] Resubmitting pending turn {_turnCount} match={_casualPvp.MatchId}.");
                }
                else
                {
                    _playerActionsReady = false;
                    _pendingPlayerActions = null;
                    _isWaitingForPlayerActions = true;
                    Debug.Log($"[PvPTurn] Waiting for seat {_casualPvp.Seat} turn {_turnCount}.");
                    OnPlayerTurnWaiting?.Invoke(_state, LocalControlledTeamIndex);

                    float pollIn = 0f;
                    while (!_playerActionsReady)
                    {
                        if (_state == null || !_state.isActive)
                        {
                            _casualTurnAborted = true;
                            _isWaitingForPlayerActions = false;
                            yield break;
                        }

                        pollIn += Time.unscaledDeltaTime;
                        if (pollIn >= 1f && _pvpTurnBridge != null)
                        {
                            pollIn = 0f;
                            Task<PvpTurnCommit> poll = _pvpTurnBridge.PollMatchAsync(_casualPvp.MatchId, CancellationToken.None);
                            yield return new WaitUntil(() => poll.IsCompleted);
                            if (poll.Status == TaskStatus.RanToCompletion && poll.Result != null && poll.Result.Forfeited)
                            {
                                EndCasualFromServer(poll.Result.State);
                                yield break;
                            }
                        }

                        yield return null;
                    }

                    _isWaitingForPlayerActions = false;
                    localActions = _pendingPlayerActions ?? new List<CombatAction>();
                    _pendingPlayerActions = null;
                    _playerActionsReady = false;
                }

                Task<PvpTurnCommit> submit = _pvpTurnBridge.SubmitTurnAndWaitAsync(
                    _casualPvp.MatchId,
                    _casualPvp.Seat,
                    _turnCount,
                    localActions,
                    CancellationToken.None);
                yield return new WaitUntil(() => submit.IsCompleted);
                PvpTurnCommit commit = submit.Status == TaskStatus.RanToCompletion ? submit.Result : null;
                if (commit != null && commit.Committed)
                {
                    _casualActions0 = commit.ActionsA;
                    _casualActions1 = commit.ActionsB;
                    Debug.Log($"[PvPTurn] Committed turn {_turnCount} match={_casualPvp.MatchId}.");
                    yield break;
                }

                if (commit != null && (commit.Forfeited || PvpPartyMapper.IsTerminal(commit.State)))
                {
                    string state = !string.IsNullOrEmpty(commit.State) ? commit.State : "cancelled";
                    EndCasualFromServer(state);
                    yield break;
                }

                if (!resentServerPending &&
                    commit != null &&
                    commit.Error == "turn_conflict" &&
                    commit.PendingLocalActions != null &&
                    commit.PendingLocalActions.Count > 0 &&
                    (commit.TurnIndex == 0 || commit.TurnIndex == _turnCount))
                {
                    resentServerPending = true;
                    resubmit = commit.PendingLocalActions;
                    Debug.LogWarning($"[PvPTurn] Turn {_turnCount} already has a pending action. Resubmitting it.");
                    continue;
                }

                string reason = commit != null && !string.IsNullOrEmpty(commit.Error) ? commit.Error : "rejected";
                Debug.LogWarning($"[PvPTurn] Turn {_turnCount} was not accepted ({reason}). Choose again.");
            }
        }

        private List<CombatAction> TakePendingLocalActions(int turnIndex)
        {
            if (_casualPvp == null || _casualPvp.PendingLocalActions == null || _casualPvp.PendingLocalActions.Count == 0)
                return null;
            if (_casualPvp.PendingTurnIndex != turnIndex)
                return null;

            List<CombatAction> actions = _casualPvp.PendingLocalActions;
            _casualPvp.PendingLocalActions = null;
            return actions;
        }

        private void EndCasualFromServer(string state)
        {
            _casualForfeitState = string.IsNullOrEmpty(state) ? "cancelled" : state;
            _casualTurnAborted = true;
            _isWaitingForPlayerActions = false;
            if (_state != null)
            {
                int winner = PvpPartyMapper.WinnerForForfeit(_casualForfeitState);
                _state.winnerTeamIndex = winner >= 0 ? winner : -2;
                _state.isActive = false;
                _state.currentPhase = TurnPhase.BattleOver;
            }

            Debug.Log($"[PvPMatch] Server ended match {_casualPvp?.MatchId} as {_casualForfeitState}.");
            FinalizeBattle();
        }

        private void StampCasualPvpResult(BattleResult result)
        {
            if (_casualPvp == null || result == null)
                return;

            result.battleType = BattleType.PvP;
            result.pvpMatchId = _casualPvp.MatchId;
            result.casualSpineMatch = true;
            result.viewerTeamIndex = LocalControlledTeamIndex;
            if (!string.IsNullOrEmpty(_casualForfeitState))
            {
                int winner = PvpPartyMapper.WinnerForForfeit(_casualForfeitState);
                if (winner >= 0)
                    result.winnerTeamIndex = winner;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Battle Finalization
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void FinalizeBattle()
        {
            if (_battleFinalized)
                return;
            _battleFinalized = true;
            _isWaitingForPlayerActions = false;
            ClearBattleIntroState();

            // Determine battle type from wager orchestrator
            bool isPvP = false;
            string pvpMatchId = null;
            if (_wagerOrchestrator != null)
            {
                var wState = _wagerOrchestrator.State;
                if (wState == WagerMatchState.InProgress || wState == WagerMatchState.Locked)
                {
                    isPvP = true;
                    pvpMatchId = _wagerOrchestrator.CurrentMatch?.matchId;
                }
            }

            var result = new BattleResult
            {
                winnerTeamIndex = _state != null ? _state.winnerTeamIndex : -1,
                turnCount = _turnCount,
                durationSeconds = Time.realtimeSinceStartup - _battleStartTime,
                wasFled = false,
                completionReason = _captureCompletionReason,
                captureAttempted = _captureAttempted,
                captureSucceeded = _captureSucceeded,
                capturedBackendChainlingId = _capturedBackendChainlingId,
                captures = new List<BattleCaptureRecord>(_captures),
                seed = _resolver != null ? _resolver.Rng.Seed : seed,
                battleType = isPvP ? BattleType.PvP : BattleType.PvE,
                pvpMatchId = pvpMatchId,
                rulesetId = _resolver != null ? _resolver.RulesetId : null,
                configHash = _resolver != null ? _resolver.ConfigHash : null,
                authoritativeRamSettlement = ResolveAuthoritativeRamSettlement(),
            };

            var encounter = _replayData != null ? _replayData.encounter : null;
            StampEncounterContext(result, encounter);

            if (_resolver?.Log != null && _state?.team1 != null)
            {
                result.enemyKOsBySpecies = BattleLogAnalyzer.CountEnemyKOsBySpecies(_resolver.Log.Events, _state.team1);
                result.totalEnemyKOs = BattleLogAnalyzer.CountEnemyKOs(_resolver.Log.Events, _state.team1);
                result.playerParticipants = BuildParticipants(
                    _state.team0,
                    _replayData != null ? _replayData.playerParty : null,
                    _resolver.Log);
                result.pveStatContributions = BattleStatsAggregator.AggregateCombatStats(_resolver.Log.Events, result);
                string eventHash = BattleStatsAggregator.ComputeLegacyEventHash(_resolver.Log.Events, result.seed);
                result.forensicHash = BuildForensicHash(eventHash);
            }

            StampCasualPvpResult(result);
            if (_casualPvp != null && string.IsNullOrEmpty(_casualForfeitState) && _pvpTurnBridge != null && !_casualResultSubmitted)
            {
                _casualResultSubmitted = true;
                StartCoroutine(SettleCasualThenPresent(result));
                return;
            }

            PresentFinalizedBattle(result);
        }

        private BattleRewardSettlement ResolveAuthoritativeRamSettlement()
        {
            if (_authoritativeRamSettlement != null)
                return _authoritativeRamSettlement;

            CombatSessionEnvelope current = AuthoritativeBattleLaunchContext.Current;
            return current != null ? current.Settlement : null;
        }

        private IEnumerator SettleCasualThenPresent(BattleResult result)
        {
            string matchId = _casualPvp != null ? _casualPvp.MatchId : string.Empty;
            string hash = result != null ? result.forensicHash ?? string.Empty : string.Empty;
            Debug.Log($"[PvPMatch] Submitting forensic hash for {matchId}.");
            Task<PvpResultCommit> task = _pvpTurnBridge.SubmitResultAsync(
                matchId,
                hash,
                result != null ? result.winnerTeamIndex : -2,
                CancellationToken.None);
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
            {
                Debug.Log($"[PvPMatch] Server state {task.Result.State} for {matchId}.");
                ApplyAgreedCasualWinner(result, task.Result);
            }

            PresentFinalizedBattle(result);
        }

        private void ApplyAgreedCasualWinner(BattleResult result, PvpResultCommit commit)
        {
            if (result == null || commit == null)
                return;

            if (commit.State == "completed")
            {
                int agreed = PvpPartyMapper.WinnerTeamFromSeat(commit.WinnerSeat);
                if (agreed != -1)
                    result.winnerTeamIndex = agreed;
                return;
            }

            if (commit.State == "forfeit_a" || commit.State == "forfeit_b")
            {
                int agreed = PvpPartyMapper.WinnerTeamFromSeat(commit.WinnerSeat);
                if (agreed < 0)
                    agreed = PvpPartyMapper.WinnerForForfeit(commit.State);
                if (agreed >= 0)
                    result.winnerTeamIndex = agreed;
                return;
            }

            result.winnerTeamIndex = -2;
        }

        private bool TryTakeReplayTurn(out List<CombatAction> actions0, out List<CombatAction> actions1)
        {
            actions0 = null;
            actions1 = null;
            List<CasualPvpCommittedTurn> history = _casualPvp != null ? _casualPvp.History : null;
            if (history == null)
                return false;

            for (int i = 0; i < history.Count; i++)
            {
                CasualPvpCommittedTurn turn = history[i];
                if (turn == null || turn.TurnIndex != _turnCount || turn.ActionsA == null || turn.ActionsB == null)
                    continue;

                actions0 = turn.ActionsA;
                actions1 = turn.ActionsB;
                return true;
            }

            return false;
        }

        private string BuildForensicHash(string eventHash)
        {
            if (_casualPvp != null)
                return BattleStatsAggregator.ComputePvpForensicHash(_casualPvp.MatchId, eventHash);

            string battleId = _resolver != null && _resolver.Log != null ? _resolver.Log.BattleId : null;
            return !string.IsNullOrWhiteSpace(battleId) ? $"{battleId}:{eventHash}" : eventHash;
        }

        private void PresentFinalizedBattle(BattleResult result)
        {
            string winner;
            if (result != null && result.casualSpineMatch)
            {
                winner = result.IsDraw ? "Draw" : result.PlayerWon ? "Local" : "Opponent";
            }
            else
            {
                winner = result.winnerTeamIndex switch
                {
                    0 => "Player",
                    1 => "Opponent",
                    -2 => "Draw",
                    _ => "Unknown"
                };
            }
            Debug.Log($"[CombatBattleController] Battle ended â€” {winner} wins after {_turnCount} turns ({result.durationSeconds:F1}s)");

            if (_resolver?.Log != null)
            {
                string team0 = _state?.team0?.teamName ?? "Team0";
                string team1 = _state?.team1?.teamName ?? "Team1";
                _resolver.Log.LogBattleEnded(result, team0, team1);
            }

            AppendAndQueueBattleResultNarration(result);
            PrepareRewardsBeforePostBattlePresentation(result);
            OnBattleEnded?.Invoke(result);

            // Finalize and emit replay data
            if (_replayData != null)
            {
                _replayData.result = result;
                OnReplayDataReady?.Invoke(_replayData);
            }

            // Route to rewards (stub)
            RouteRewardsAfterPostBattlePresentation(result);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Provider Resolution
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ClearBattleIntroState()
        {
            _waitingForBattleIntro = false;
            _resumeAutoRunAfterIntro = false;
        }

        private List<BattleParticipant> BuildParticipants(TeamState team, PartySnapshot playerParty, CombatEventLog log)
        {
            var participants = new List<BattleParticipant>();
            if (team?.chainlings == null)
                return participants;

            var knockedOutIds = log != null
                ? BattleLogAnalyzer.GetKOInstanceIds(log.Events, team)
                : new HashSet<string>();

            var swapInIds = CollectSwapInInstanceIds(log, team);

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling == null)
                    continue;

                ChainlingSnapshot snapshot = null;
                if (playerParty?.chainlings != null && i >= 0 && i < playerParty.chainlings.Count)
                    snapshot = playerParty.chainlings[i];

                bool startedActive = i < CombatConstants.ACTIVE_PER_TEAM;
                bool wasActive = startedActive
                    || (!string.IsNullOrEmpty(chainling.instanceId) && swapInIds.Contains(chainling.instanceId))
                    || chainling.isActive;

                participants.Add(new BattleParticipant
                {
                    rosterIndex = i,
                    instanceId = chainling.instanceId,
                    speciesId = snapshot != null && !string.IsNullOrEmpty(snapshot.speciesId)
                        ? snapshot.speciesId
                        : chainling.speciesId,
                    displayName = snapshot != null && !string.IsNullOrEmpty(snapshot.displayName)
                        ? snapshot.displayName
                        : chainling.displayName,
                    backendChainlingId = snapshot != null ? snapshot.backendChainlingId : null,
                    masteryXp = snapshot != null ? Mathf.Max(0, snapshot.masteryXp) : 0,
                    stars = snapshot != null ? ChainlingProgressionUtility.NormalizeStars(snapshot.stars) : 0,
                    rebirthCount = snapshot != null ? ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount) : 0,
                    isEntangled = ChainlingProgressionIdentityUtility.IsEntangled(snapshot),
                    knownMoveIds = ChainlingProgressionOwnedMovesUtility.ToList(
                        ChainlingProgressionOwnedMovesUtility.CollectFromSnapshot(snapshot)),
                    wasActive = wasActive,
                    wasBenched = !wasActive,
                    wasKOd = (!string.IsNullOrEmpty(chainling.instanceId) && knockedOutIds.Contains(chainling.instanceId))
                        || !chainling.isAlive,
                    endCurrentHp = chainling.currentHP,
                    endMaxHp = chainling.EffectiveMaxHP,
                });
            }

            return participants;
        }

        private static HashSet<string> CollectSwapInInstanceIds(CombatEventLog log, TeamState team)
        {
            var swapInIds = new HashSet<string>();
            if (log == null || team?.chainlings == null)
                return swapInIds;

            var teamInstanceIds = new HashSet<string>();
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling != null && !string.IsNullOrEmpty(chainling.instanceId))
                    teamInstanceIds.Add(chainling.instanceId);
            }

            for (int i = 0; i < log.Events.Count; i++)
            {
                var evt = log.Events[i];
                if (evt.eventType == CombatEventType.SwapPerformed
                    && !string.IsNullOrEmpty(evt.targetId)
                    && teamInstanceIds.Contains(evt.targetId))
                {
                    swapInIds.Add(evt.targetId);
                }
            }

            var forensicEvents = log.ForensicEvents;
            for (int i = 0; i < forensicEvents.Count; i++)
            {
                var evt = forensicEvents[i];
                if (!string.Equals(evt.eventType, nameof(CombatForensicEventType.SwapResolved), StringComparison.Ordinal))
                    continue;

                if (evt.targets == null)
                    continue;

                for (int j = 0; j < evt.targets.Count; j++)
                {
                    var target = evt.targets[j];
                    if (target == null || string.IsNullOrEmpty(target.instanceId))
                        continue;

                    if (target.teamId == team.teamIndex || teamInstanceIds.Contains(target.instanceId))
                        swapInIds.Add(target.instanceId);
                }
            }

            return swapInIds;
        }

        private CombatRulesetSO ResolveRuleset()
        {
            if (_runtimeRulesetOverride != null)
                return _runtimeRulesetOverride;
            if (ruleset != null)
                return ruleset;
            return BalanceConfigRuntime.Active != null
                ? BalanceConfigRuntime.Active.ActiveRuleset
                : null;
        }

        private PartySnapshot ResolvePlayerParty()
        {
            if (_runtimePlayerPartyProvider != null)
                return _runtimePlayerPartyProvider.GetCurrentParty();

            // 1. Try explicit override
            if (playerPartyProviderOverride != null && playerPartyProviderOverride is IPlayerPartyProvider provider)
                return provider.GetCurrentParty();

            // 2. Explicit MonoBehaviour overrides only for non-runtime injection setups.
            Debug.LogWarning("[CombatBattleController] No player party provider configured.");
            return null;
        }

        private EncounterSnapshot ResolveEncounter(int battleSeed)
        {
            if (_runtimeEncounterProvider != null)
                return _runtimeEncounterProvider.GenerateEncounter(battleSeed);

            // 1. Try explicit override
            if (encounterProviderOverride != null && encounterProviderOverride is IEncounterProvider provider)
                return provider.GenerateEncounter(battleSeed);

            Debug.LogWarning("[CombatBattleController] No encounter provider configured.");
            return null;
        }

        private static void StampEncounterContext(BattleResult result, EncounterSnapshot encounter)
        {
            if (result == null)
                return;

            if (encounter == null)
            {
                result.encounterDifficulty = EncounterDifficulty.Standard;
                return;
            }

            result.encounterDifficulty = encounter.difficulty;
            result.areaId = encounter.areaId ?? "";
            result.zoneId = encounter.zoneId ?? "";
            result.timeSegment = encounter.timeSegment;
            result.narrativeEncounterId = encounter.narrativeEncounterId ?? "";
            if (encounter.victoryKnownGrants != null && encounter.victoryKnownGrants.Count > 0)
            {
                for (int i = 0; i < encounter.victoryKnownGrants.Count; i++)
                    result.pendingVictoryKnownGrants.Add(encounter.victoryKnownGrants[i]);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // AI Factory (mirrors Combat2TestHarness pattern)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static ICombatAI CreateAI(AIPersonality personality)
        {
            switch (personality)
            {
                case AIPersonality.Dumb:       return new DumbAI();
                case AIPersonality.Aggressive:  return new AggressiveAI();
                case AIPersonality.Defensive:   return new DefensiveAI();
                case AIPersonality.Utility:     return new UtilityStatusAI();
                case AIPersonality.Random:      return new RandomAI();
                default:                        return new DumbAI();
            }
        }
    }
}
