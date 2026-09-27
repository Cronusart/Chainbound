using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Combat2 Test Harness MonoBehaviour.
    /// Provides configurable team rosters, full 2v2 action generation,
    /// and minimal debug UI for testing the combat engine.
    ///
    /// Setup:
    /// 1. Attach to a GameObject in the Combat2_TestHarness scene
    /// 2. Configure team rosters in the Inspector (or leave empty for defaults)
    /// 3. Press Play â†’ use debug UI to run battles
    /// </summary>
    public class Combat2TestHarness : MonoBehaviour, ICombatStateSource
    {
        [Header("Data Sources (optional â€” uses test data if null)")]
        [SerializeField] private TypeChemistryMatrixSO chemistryMatrix;
        [SerializeField] private StatusCatalogSO statusCatalog;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;

        public MoveDatabaseSO MoveDatabase => moveDatabase;


        [Header("Battle Settings")]
        [SerializeField] private int seed = 42;
        [SerializeField] private bool deterministicMode = true;
        [SerializeField] private bool chemistryStrictMode = true;

        [Header("Auto-Sim")]
        [SerializeField] private int autoSimTurns = 20;
        [SerializeField] private int fastSimBattleCount = 100;

        [Header("Team 0 â€” Player (configure in Inspector)")]
        [SerializeField] private List<TestChainlingConfig> team0Roster = new List<TestChainlingConfig>();
        [SerializeField] private AIPersonality team0AI = AIPersonality.Aggressive;

        [Header("Team 1 â€” Opponent (configure in Inspector)")]
        [SerializeField] private List<TestChainlingConfig> team1Roster = new List<TestChainlingConfig>();
        [SerializeField] private AIPersonality team1AI = AIPersonality.Aggressive;

        // Runtime state
        private CombatResolver _resolver;
        private CombatState _state;
        private ICombatAI _ai0;
        private ICombatAI _ai1;
        private bool _battleStarted;
        private int _turnCount;

        // Stepwise turn execution state
        private List<CombatResolver.CombatActionStep> _currentActionQueue;
        private int _currentActionIndex;
        private bool _turnSequenceActive;

        // UI accessors
        public CombatState State => _state;
        public CombatEventLog EventLog => _resolver?.Log;
        public bool BattleStarted => _battleStarted;
        public int TurnCount => _turnCount;

        // UI state
        private Vector2 _logScrollPosition;
        private string _logText = "";
        private bool _showDetailedLog = false;
        private bool _showStatAudit = false;

        private void Start()
        {
            if (deterministicMode)
                Debug.Log($"[Combat2 Harness] Deterministic mode ON â€” seed: {seed}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Battle Controls
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void StartBattle()
        {
            int battleSeed = deterministicMode ? seed : Environment.TickCount;

            _resolver = new CombatResolver(chemistryMatrix, statusCatalog, battleSeed, chemistryStrictMode, modifierCatalog);
            _ai0 = CreateAI(team0AI);
            _ai1 = CreateAI(team1AI);

            var t0 = BuildTeam(0, $"Player ({_ai0.PersonalityName})", team0Roster);
            var t1 = BuildTeam(1, $"Opponent ({_ai1.PersonalityName})", team1Roster);

            _state = _resolver.InitializeBattle(t0, t1);
            _battleStarted = true;
            _turnCount = 0;
            UpdateLogDisplay();
            Debug.Log($"[Combat2 Harness] Battle started â€” seed {battleSeed} | " +
                      $"P: {_ai0.PersonalityName} vs O: {_ai1.PersonalityName}");
        }

        public void ExecuteOneTurn()
        {
            if (!_battleStarted || _state == null || !_state.isActive) return;

            // AI generates actions AFTER AP gain (inside ExecuteTurnWithAI)
            // This fixes the Turn 1 pass bug
            bool continues = _resolver.ExecuteTurnWithAI(_state, _ai0, _ai1);
            _turnCount++;
            UpdateLogDisplay();

            if (!continues)
            {
                string winner = _state.winnerTeamIndex switch
                {
                    0 => _state.team0.teamName,
                    1 => _state.team1.teamName,
                    -2 => "Draw",
                    _ => "Unknown"
                };
                Debug.Log($"[Combat2 Harness] Battle ended after {_turnCount} turns. Winner: {winner}");
            }
        }

        public void BeginTurnSequence()
        {
            if (!_battleStarted || _state == null || !_state.isActive)
                return;

            if (_turnSequenceActive)
                return;

            if (_ai0 == null) _ai0 = CreateAI(team0AI);
            if (_ai1 == null) _ai1 = CreateAI(team1AI);

            _resolver.BeginTurn(_state);
            var actions0 = _ai0.GenerateActions(_state, 0, _resolver.Rng);
            var actions1 = _ai1.GenerateActions(_state, 1, _resolver.Rng);

            _currentActionQueue = _resolver.BuildActionQueueForCurrentTurn(_state, actions0, actions1);
            _currentActionIndex = 0;
            _turnSequenceActive = true;
            _turnCount++;
            UpdateLogDisplay();
        }

        public void ExecuteNextActionInTurn()
        {
            if (!_battleStarted || _state == null || !_state.isActive)
                return;

            if (!_turnSequenceActive)
                BeginTurnSequence();

            if (!_turnSequenceActive || _currentActionQueue == null)
                return;

            if (_currentActionIndex >= _currentActionQueue.Count)
            {
                bool continues = _resolver.EndTurn(_state);
                _turnSequenceActive = false;
                if (!continues)
                {
                    string winner = _state.winnerTeamIndex switch
                    {
                        0 => _state.team0.teamName,
                        1 => _state.team1.teamName,
                        -2 => "Draw",
                        _ => "Unknown"
                    };
                    Debug.Log($"[Combat2 Harness] Battle ended after {_turnCount} turns. Winner: {winner}");
                }
                UpdateLogDisplay();
                return;
            }

            bool stillActive = _resolver.ExecuteActionStep(_state, _currentActionQueue[_currentActionIndex]);
            _currentActionIndex++;
            UpdateLogDisplay();

            if (!stillActive)
                _turnSequenceActive = false;
        }

        public void ExecuteTurnSequence()
        {
            BeginTurnSequence();
            while (_turnSequenceActive && _state != null && _state.isActive)
                ExecuteNextActionInTurn();
        }

        public void AutoSimulate()
        {
            if (!_battleStarted) StartBattle();
            for (int i = 0; i < autoSimTurns && _state.isActive; i++)
                ExecuteOneTurn();
        }

        public void FastSimulate()
        {
            int team0Wins = 0, team1Wins = 0, draws = 0;
            var simAI0 = CreateAI(team0AI);
            var simAI1 = CreateAI(team1AI);

            for (int b = 0; b < fastSimBattleCount; b++)
            {
                int bSeed = deterministicMode ? seed + b : Environment.TickCount + b;
                var resolver = new CombatResolver(chemistryMatrix, statusCatalog, bSeed, chemistryStrictMode, modifierCatalog);
                var t0 = BuildTeam(0, "P", team0Roster);
                var t1 = BuildTeam(1, "O", team1Roster);
                var st = resolver.InitializeBattle(t0, t1);

                for (int t = 0; t < 100 && st.isActive; t++)
                {
                    resolver.ExecuteTurnWithAI(st, simAI0, simAI1);
                }

                switch (st.winnerTeamIndex)
                {
                    case 0: team0Wins++; break;
                    case 1: team1Wins++; break;
                    default: draws++; break;
                }
            }

            Debug.Log($"[Combat2 FastSim] {fastSimBattleCount} battles " +
                      $"({simAI0.PersonalityName} vs {simAI1.PersonalityName}): " +
                      $"Team0={team0Wins}, Team1={team1Wins}, Draws={draws}");
        }

        public void ResetBattle()
        {
            _battleStarted = false;
            _state = null;
            _resolver = null;
            _turnCount = 0;
            _currentActionQueue = null;
            _currentActionIndex = 0;
            _turnSequenceActive = false;
            _logText = "Battle reset. Press Start to begin.";
        }

        public void ExportLog()
        {
            if (_resolver == null || _resolver.Log == null)
                return;

            if (!CombatBattleLogExporter.TryExport(_resolver.Log, out var result))
                return;

            Debug.Log($"[Combat2 Harness] Battle log exported to: {result.directoryPath}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // AI Factory
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Team Building â€” Inspector config â†’ runtime TeamState
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private TeamState BuildTeam(int index, string name, List<TestChainlingConfig> roster)
        {
            var team = new TeamState(index, name);

            // 1) Try species database if configured
            if (roster.Count > 0)
            {
                foreach (var cfg in roster)
                {
                    // Try from species database first
                    if (speciesDatabase != null && !string.IsNullOrEmpty(cfg.speciesId))
                    {
                        if (AddSpeciesToTeam(team, cfg))
                            continue;
                    }
                    // Fall back to inline config
                    team.chainlings.Add(CreateFromConfig(cfg));
                }
                return team;
            }

            // 2) Default hardcoded rosters (2 active + 1 bench per team)
            if (index == 0)
                BuildDefaultTeam0(team);
            else
                BuildDefaultTeam1(team);

            return team;
        }

        private void BuildDefaultTeam0(TeamState team)
        {
            // Lead: Validape (Node)
            var lead = CombatFactory.CreateTestChainling("Validape", ChainlingType.Node, stars: 1,
                hp: 110, mAtk: 65, mDef: 55, rAtk: 50, rDef: 55, spd: 55);
            lead.moves.Add(CombatFactory.CreateTestMove(
                "Hammer Sync", MoveCategory.DamagingMelee, ChainlingType.Node, 110, 2, 95));
            lead.moves.Add(CombatFactory.CreateTestMove(
                "Baseline Striker", MoveCategory.DamagingMelee, ChainlingType.None, 75, 2, 95));
            lead.moves.Add(CombatFactory.CreateTestMove(
                "Pack Rally", MoveCategory.Support, ChainlingType.Node, 0, 3, 0));

            // Partner: Kitsauto (Bot)
            var partner = CombatFactory.CreateTestChainling("Kitsauto", ChainlingType.Bot, stars: 1,
                hp: 95, mAtk: 70, mDef: 45, rAtk: 55, rDef: 45, spd: 75);
            partner.moves.Add(CombatFactory.CreateTestMove(
                "Blade Flurry", MoveCategory.DamagingMelee, ChainlingType.Bot, 30, 2, 95, hitCount: 3));
            partner.moves.Add(CombatFactory.CreateTestMove(
                "Baseline Jab", MoveCategory.DamagingMelee, ChainlingType.None, 40, 1, 95));

            // Bench: Node Backup
            var bench = CombatFactory.CreateTestChainling("NodeReserve", ChainlingType.Node, stars: 1,
                hp: 100, mAtk: 55, mDef: 60, rAtk: 55, rDef: 60, spd: 50);
            bench.moves.Add(CombatFactory.CreateTestMove(
                "Node Pulse", MoveCategory.DamagingRanged, ChainlingType.Node, 55, 2, 95));
            bench.moves.Add(CombatFactory.CreateTestMove(
                "Baseline Jab", MoveCategory.DamagingMelee, ChainlingType.None, 40, 1, 95));

            team.chainlings.Add(lead);      // 0 â†’ active slot 0
            team.chainlings.Add(partner);    // 1 â†’ active slot 1
            team.chainlings.Add(bench);      // 2 â†’ bench
        }

        private void BuildDefaultTeam1(TeamState team)
        {
            // Lead: Gilarime (Frozen)
            var lead = CombatFactory.CreateTestChainling("Gilarime", ChainlingType.Frozen, stars: 1,
                hp: 120, mAtk: 50, mDef: 65, rAtk: 60, rDef: 60, spd: 45);
            lead.moves.Add(CombatFactory.CreateTestMove(
                "Cryo Ray", MoveCategory.DamagingRanged, ChainlingType.Frozen, 110, 2, 95));
            lead.moves.Add(CombatFactory.CreateTestMove(
                "Frost Shot", MoveCategory.DamagingRanged, ChainlingType.Frozen, 100, 2, 95));

            // Partner: Shard Scout
            var partner = CombatFactory.CreateTestChainling("ShardScout", ChainlingType.Shard, stars: 1,
                hp: 90, mAtk: 55, mDef: 50, rAtk: 65, rDef: 50, spd: 70);
            partner.moves.Add(CombatFactory.CreateTestMove(
                "Shard Toss", MoveCategory.DamagingRanged, ChainlingType.Shard, 55, 2, 95));
            partner.moves.Add(CombatFactory.CreateTestMove(
                "Quick Jab", MoveCategory.DamagingMelee, ChainlingType.None, 40, 1, 95));

            // Bench: Frozen Backup
            var bench = CombatFactory.CreateTestChainling("FrostGuard", ChainlingType.Frozen, stars: 1,
                hp: 105, mAtk: 45, mDef: 70, rAtk: 50, rDef: 65, spd: 40);
            bench.moves.Add(CombatFactory.CreateTestMove(
                "Cold Snap", MoveCategory.DamagingRanged, ChainlingType.Frozen, 50, 2, 95));
            bench.moves.Add(CombatFactory.CreateTestMove(
                "Baseline Jab", MoveCategory.DamagingMelee, ChainlingType.None, 40, 1, 95));

            team.chainlings.Add(lead);      // 0 â†’ active slot 0
            team.chainlings.Add(partner);    // 1 â†’ active slot 1
            team.chainlings.Add(bench);      // 2 â†’ bench
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Species Database Integration
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool AddSpeciesToTeam(TeamState team, TestChainlingConfig cfg)
        {
            var def = speciesDatabase.GetById(cfg.speciesId);
            if (def == null) return false;

            var equipped = new List<MoveDefinitionSO>();
            if (def.exclusiveMoves != null)
                foreach (var i in def.exclusiveMoves) AddUnique(equipped, i);
            if (def.learnableMoves != null)
                foreach (var i in def.learnableMoves) AddUnique(equipped, i);
            if (equipped.Count > 4)
                equipped = equipped.GetRange(0, 4);

            var instance = CombatFactory.CreateChainling(def,
                stars: cfg.stars >= 0 ? cfg.stars : 1,
                rebirthCount: cfg.rebirthCount >= 0 ? cfg.rebirthCount : 0,
                evolutionStage: cfg.evolutionStage > 0 ? cfg.evolutionStage : 1,
                equippedMoves: equipped,
                rng: _resolver?.Rng);
            team.chainlings.Add(instance);
            return true;
        }

        private void AddUnique(List<MoveDefinitionSO> list, MoveDefinitionSO def)
        {
            if (def != null && !list.Contains(def)) list.Add(def);
        }

        /// <summary>Create a Chainling from inline Inspector config (no species DB needed).</summary>
        private ChainlingInstance CreateFromConfig(TestChainlingConfig cfg)
        {
            var c = CombatFactory.CreateTestChainling(
                cfg.displayName,
                cfg.type,
                stars: cfg.stars >= 0 ? cfg.stars : 1,
                rebirthCount: cfg.rebirthCount >= 0 ? cfg.rebirthCount : 0,
                hp: cfg.hp > 0 ? cfg.hp : 100,
                cfg.mAtk > 0 ? cfg.mAtk : 60,
                cfg.mDef > 0 ? cfg.mDef : 50,
                cfg.rAtk > 0 ? cfg.rAtk : 60,
                cfg.rDef > 0 ? cfg.rDef : 50,
                cfg.spd > 0 ? cfg.spd : 60);

            foreach (var insc in cfg.moves)
            {
                if (string.IsNullOrEmpty(insc.name)) continue;
                c.moves.Add(CombatFactory.CreateTestMove(
                    insc.name, insc.category, insc.type,
                    insc.power, insc.apCost, insc.accuracy, insc.hitCount > 0 ? insc.hitCount : 1));
            }

            return c;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Debug UI
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20));

            GUILayout.Label("<b>Combat2 Test Harness</b>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Battle")) StartBattle();
            if (GUILayout.Button("Reset")) ResetBattle();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Turn")) ExecuteOneTurn();
            if (GUILayout.Button($"Auto ({autoSimTurns})")) AutoSimulate();
            if (GUILayout.Button($"Fast Sim ({fastSimBattleCount})")) FastSimulate();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Next Action")) ExecuteNextActionInTurn();
            if (GUILayout.Button("Turn (Stepwise)")) ExecuteTurnSequence();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Battle Log")) ExportLog();
            _showDetailedLog = GUILayout.Toggle(_showDetailedLog, "Detailed");
            _showStatAudit = GUILayout.Toggle(_showStatAudit, "Stat Audit");
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed:", GUILayout.Width(40));
            string seedStr = GUILayout.TextField(seed.ToString(), GUILayout.Width(100));
            if (int.TryParse(seedStr, out int newSeed)) seed = newSeed;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (_state != null)
            {
                DrawTeamStatus(_state.team0, "Team 0 (Player)");
                GUILayout.Space(5);
                DrawTeamStatus(_state.team1, "Team 1 (Opponent)");
                GUILayout.Space(5);
                GUILayout.Label($"Turn: {_state.currentTurn} | Phase: {_state.currentPhase} | Active: {_state.isActive}");
                if (_showStatAudit)
                    DrawStatAudit();
            }

            GUILayout.Space(10);

            GUILayout.Label("<b>Combat Log</b>", new GUIStyle(GUI.skin.label) { richText = true });
            _logScrollPosition = GUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(300));
            GUILayout.Label(_logText, new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true });
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawStatAudit()
        {
            if (_state == null) return;

            var actor = _state.team0 != null ? _state.team0.GetActiveInSlot(0) : null;
            var target = _state.team1 != null ? _state.team1.GetActiveInSlot(0) : null;
            if (actor == null || target == null)
                return;

            GUILayout.Space(6);
            GUILayout.Label("<b>Stat Audit (Lead vs Lead)</b>", new GUIStyle(GUI.skin.label) { richText = true });
            DrawCoreStatRow("ATK", actor.GetStatBreakdown(CoreStat.MeleeAtk), target.GetStatBreakdown(CoreStat.MeleeDef));
            DrawCoreStatRow("RATK", actor.GetStatBreakdown(CoreStat.RangedAtk), target.GetStatBreakdown(CoreStat.RangedDef));
            DrawCoreStatRow("SPD", actor.GetStatBreakdown(CoreStat.Speed), target.GetStatBreakdown(CoreStat.Speed));
        }

        private void DrawCoreStatRow(string label, StatBreakdown left, StatBreakdown right)
        {
            GUILayout.Label(
                $"  {label}: {left.finalInt} (B{left.baseValue}+IV{left.ivValue}, Px{left.progressionMultiplier:F2}, S{left.stageValue} x{left.stageMultiplier:F2})" +
                $" vs {right.finalInt} (B{right.baseValue}+IV{right.ivValue}, Px{right.progressionMultiplier:F2}, S{right.stageValue} x{right.stageMultiplier:F2})");
        }

        private void DrawTeamStatus(TeamState team, string label)
        {
            var richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label($"<b>{label}</b> (alive: {team.AliveCount}/{team.chainlings.Count})", richLabel);

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                var c = team.GetActiveInSlot(slot);
                string slotLabel = slot == 0 ? "Lead" : "Partner";
                if (c == null)
                {
                    int idx = team.activeIndices[slot];
                    if (idx >= 0 && idx < team.chainlings.Count)
                        GUILayout.Label($"  [{slotLabel}] <b>{team.chainlings[idx].displayName}</b> <color=red>KO</color>", richLabel);
                    else
                        GUILayout.Label($"  [{slotLabel}] â€” empty");
                    continue;
                }

                string aliveText = c.isAlive ? "<color=green>ALIVE</color>" : "<color=red>KO</color>";
                GUILayout.Label($"  [{slotLabel}] <b>{c.displayName}</b> [{c.primaryType}] {aliveText}", richLabel);
                GUILayout.Label($"    HP: {c.currentHP}/{c.EffectiveMaxHP}  AP: {c.currentAP}/{CombatConstants.AP_CAP}");

                if (c.statuses.Count > 0)
                {
                    string statuses = "    Statuses: ";
                    for (int i = 0; i < c.statuses.Count; i++)
                        statuses += $"{c.statuses[i].statusId}({c.statuses[i].turnsRemaining}) ";
                    GUILayout.Label(statuses);
                }
            }

            int benchIdx = team.GetBenchIndex();
            if (benchIdx >= 0)
            {
                var bench = team.chainlings[benchIdx];
                GUILayout.Label($"  [Bench] {bench.displayName} [{bench.primaryType}] " +
                    $"HP:{bench.currentHP}/{bench.EffectiveMaxHP}");
            }
            else
            {
                GUILayout.Label("  [Bench] â€” none");
            }
        }

        private void UpdateLogDisplay()
        {
            if (_resolver == null) return;

            var events = _showDetailedLog
                ? _resolver.Log.Events
                : (IReadOnlyList<CombatEvent>)_resolver.Log.GetLastEvents(30);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < events.Count; i++)
                sb.AppendLine(events[i].message);
            _logText = sb.ToString();
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Inspector-Configurable Data Structures
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Inspector-configurable Chainling for the test harness.
    /// Fill either speciesId (to pull from SpeciesDatabaseSO) or inline stats.
    /// </summary>
    [Serializable]
    public class TestChainlingConfig
    {
        [Header("Species DB Lookup (leave empty for inline stats)")]
        [Tooltip("Species ID to look up in SpeciesDatabaseSO (e.g., 'validape', 'gilarime').")]
        public string speciesId;

        [Header("Inline Stats (used if speciesId is empty)")]
        public string displayName = "TestChainling";
        public ChainlingType type = ChainlingType.Node;
        public int level = 50;
        public int hp = 100;
        public int mAtk = 60;
        public int mDef = 50;
        public int rAtk = 60;
        public int rDef = 50;
        public int spd = 60;

        [Header("Inline Stamped moves (used if speciesId is empty)")]
        public List<TestMoveConfig> moves = new List<TestMoveConfig>();

        [Header("Evolution")]
        public int evolutionStage = 1;

        [Header("Progression (stars + rebirth)")]
        [Tooltip("Star level (0-5). -1 = use default 1.")]
        public int stars = 1;

        [Tooltip("Rebirth count (0-5). -1 = use default 0.")]
        public int rebirthCount = 0;
    }

    /// <summary>
    /// Inspector-configurable move for inline Chainling setup.
    /// </summary>
    [Serializable]
    public class TestMoveConfig
    {
        public string name = "Test Move";
        public MoveCategory category = MoveCategory.DamagingMelee;
        public ChainlingType type = ChainlingType.None;
        public int power = 60;
        public int apCost = 2;
        public int accuracy = 95;
        public int hitCount = 1;
    }

    /// <summary>
    /// Selectable AI personality for the test harness Inspector.
    /// Each maps to an ICombatAI implementation.
    /// In the full game, these will be assignable per species definition.
    /// </summary>
    public enum AIPersonality
    {
        [Tooltip("First affordable damaging move, no targeting intelligence.")]
        Dumb,

        [Tooltip("Maximizes damage output. Targets weakest enemy. Never heals or swaps.")]
        Aggressive,

        [Tooltip("Prioritizes survival. Heals allies, swaps low-HP Chainlings, buffs self.")]
        Defensive,

        [Tooltip("Leads with debuffs and status moves. Spreads control across enemies.")]
        Utility,

        [Tooltip("Picks a random legal action each turn. Good for fuzz testing balance.")]
        Random,
    }
}
