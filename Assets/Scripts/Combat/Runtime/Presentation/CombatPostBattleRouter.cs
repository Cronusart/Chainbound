using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chainbound.Combat
{
    /// <summary>
    /// Persists post-battle rewards and returns to the overworld after the uGUI flow completes.
    /// Presentation (banner / report / rewards) lives on <see cref="CombatPostBattleReportController"/>.
    /// </summary>
    public class CombatPostBattleRouter : MonoBehaviour, ICombatRewardsRouter, ICombatRewardPreparation
    {
        [Header("References")]
        [SerializeField] private PvPMoveSelectController pvpMoveSelectController;

        [Tooltip("Authored post-battle overlay. Falls back to FindFirstObjectByType when unset.")]
        [SerializeField] private CombatPostBattleReportController battleReport;

        [Header("Rewards")]
        [Tooltip("Post-battle XP/RAM lever board. If omitted, BalanceConfigRuntime.PostBattleRewards is used.")]
        [SerializeField] private PostBattleRewardsConfigSO rewardConfig;

        [Tooltip("Optional progression lever board override. If omitted, BalanceConfigRuntime.Progression is used.")]
        [SerializeField] private ProgressionConfigSO progressionConfig;

        [Tooltip("Species database used to resolve star/mastery progression move pools.")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;

        [Tooltip("Gate percent tables for star/mastery progression Known grants.")]
        [SerializeField] private ChainlingProgressionRewardTablesSO progressionRewardTables;

        [Tooltip("Optional event overlay catalog for limited-time progression move boosts.")]
        [SerializeField] private ChainlingProgressionEventCatalogSO progressionEventCatalog;

        [Tooltip("Optional: MonoBehaviour implementing IBattleRewardPersistence.")]
        [SerializeField] private MonoBehaviour rewardPersistenceOverride;

        [Tooltip("Optional: MonoBehaviour implementing IProgressionGrantPersistence.")]
        [SerializeField] private MonoBehaviour progressionGrantPersistenceOverride;

        [Tooltip("Optional: MonoBehaviour implementing IEncounterVictoryKnownGrantPersistence.")]
        [SerializeField] private MonoBehaviour victoryKnownGrantPersistenceOverride;

        [Header("Audio")]
        [Tooltip("Optional catalog used to create a local CombatSfxOrchestrator when no orchestrator is assigned or discoverable.")]
        [SerializeField] private CombatBattleSfxCatalogSO battleSfxCatalog;
        [Tooltip("Optional MonoBehaviour implementing ICombatSfxOrchestrator.")]
        [SerializeField] private MonoBehaviour combatSfxOrchestratorBehaviour;

        [Header("Scene Return")]
        [SerializeField] private string homeSceneName = "player_room";
        [SerializeField] private string homeSpawnId = "home_bed";

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        public event Action OnPostBattleComplete;
        public event Action<OverworldReturnInfo> OnReturnToOverworld;

        private enum RouterState
        {
            Idle,
            PersistingRewards,
            PvPMoveSelect,
        }

        private RouterState _state = RouterState.Idle;
        private BattleResult _result;
        private BattleRewardSummary _rewardSummary;
        private bool _progressionGrantsApplied;
        private bool _victoryKnownGrantsApplied;
        private Task _progressionGrantTask;
        private Task _victoryKnownGrantTask;
        private PreparedBattleRewards _preparedRewards;

        private InputActionMap _playerMap;

        private IBattleRewardCalculator _rewardCalculator;
        private IBattleRewardPersistence _rewardPersistence;
        private IProgressionGrantPersistence _progressionGrantPersistence;
        private StarProgressionGrantService _progressionGrantService;
        private IEncounterVictoryKnownGrantPersistence _victoryKnownGrantPersistence;
        private EncounterVictoryKnownGrantService _victoryKnownGrantService;
        private PostBattleRewardsConfigSO _resolvedRewardConfig;
        private ProgressionConfigSO _resolvedProgressionConfig;
        private ICombatSfxOrchestrator _combatSfxOrchestrator;
        private CombatSfxOrchestrator _runtimeCombatSfxOrchestrator;
        private CombatPostBattleReportController _battleReport;

        private void OnEnable()
        {
            if (pvpMoveSelectController != null)
                pvpMoveSelectController.OnSelectionComplete += HandlePvPSelectionComplete;

            ResolveBattleReport();
        }

        private void OnDisable()
        {
            if (pvpMoveSelectController != null)
                pvpMoveSelectController.OnSelectionComplete -= HandlePvPSelectionComplete;

            if (_battleReport != null)
                _battleReport.OnReportDismissed -= HandleOutcomeDismissed;
            _battleReport = null;
        }

        private void Start()
        {
            SetupInput();
            SetupRewardServices();
            ResolveBattleReport();
        }

        private void ResolveBattleReport()
        {
            CombatPostBattleReportController report = battleReport != null
                ? battleReport
                : FindFirstObjectByType<CombatPostBattleReportController>(FindObjectsInactive.Include);
            if (report == _battleReport)
                return;
            if (_battleReport != null)
                _battleReport.OnReportDismissed -= HandleOutcomeDismissed;
            _battleReport = report;
            if (_battleReport != null)
                _battleReport.OnReportDismissed += HandleOutcomeDismissed;
        }

        private void SetupInput()
        {
            if (inputActions == null)
                return;

            _playerMap = inputActions.FindActionMap("Player");
        }

        private void SetupRewardServices()
        {
            _resolvedRewardConfig = rewardConfig != null
                ? rewardConfig
                : BalanceConfigRuntime.PostBattleRewards;
            _resolvedProgressionConfig = progressionConfig != null
                ? progressionConfig
                : BalanceConfigRuntime.Progression;

            if (rewardConfig == null)
                Debug.LogWarning("[PostBattleRouter] No PostBattleRewardsConfigSO assigned. Using BalanceConfigRuntime defaults.");

            ChainlingProgressionRewardTablesSO tables = progressionRewardTables != null
                ? progressionRewardTables
                : _resolvedRewardConfig != null
                    ? _resolvedRewardConfig.progressionRewardTables
                    : null;

            _rewardCalculator = new StandardBattleRewardCalculator(
                _resolvedRewardConfig,
                _resolvedProgressionConfig,
                FavorRuntime.Config);
            _rewardPersistence = rewardPersistenceOverride as IBattleRewardPersistence ?? new NullBattleRewardPersistence();
            _progressionGrantPersistence = ResolveProgressionGrantPersistence();
            _progressionGrantService = new StarProgressionGrantService(
                speciesDatabase,
                tables,
                progressionEventCatalog,
                _progressionGrantPersistence);
            _victoryKnownGrantPersistence = ResolveVictoryKnownGrantPersistence();
            _victoryKnownGrantService = new EncounterVictoryKnownGrantService(_victoryKnownGrantPersistence);
        }

        public void RouteToRewards(BattleResult result)
        {
            PreparedBattleRewards prepared = PrepareRewards(result);
            _result = result;
            _rewardSummary = prepared != null ? prepared.summary : _rewardSummary;

            if (result != null && result.casualSpineMatch)
            {
                _result = result;
                Debug.Log($"[PvPMatch] Casual match {result.pvpMatchId} finished state-winner={result.winnerTeamIndex}.");
                return;
            }

            if (result != null && result.IsPvP)
            {
                RouteToPvPSelection(result);
                return;
            }

            BeginImmediatePersistence(result);
        }

        public PreparedBattleRewards PrepareRewards(BattleResult result)
        {
            if (result != null && result.casualSpineMatch)
            {
                _result = result;
                return null;
            }

            if (_preparedRewards != null && ReferenceEquals(_preparedRewards.result, result))
                return _preparedRewards;

            if (_rewardCalculator == null)
                SetupRewardServices();
            ResolveBattleReport();

            _result = result;
            _progressionGrantsApplied = false;
            _victoryKnownGrantsApplied = false;
            BattleDefeatRecoveryUtility.ApplyToPveDefeat(
                result,
                _resolvedRewardConfig != null ? _resolvedRewardConfig : BalanceConfigRuntime.PostBattleRewards);
            _rewardSummary = _rewardCalculator != null
                ? _rewardCalculator.Calculate(result)
                : new BattleRewardSummary();
            _progressionGrantTask = ApplyProgressionGrantsCoreAsync();
            _victoryKnownGrantTask = ApplyVictoryKnownGrantsCoreAsync();
            _preparedRewards = new PreparedBattleRewards
            {
                result = result,
                summary = _rewardSummary,
                progressionCompletion = _progressionGrantTask,
                victoryKnownCompletion = _victoryKnownGrantTask,
            };
            _battleReport?.SetPreparedRewards(_preparedRewards);
            return _preparedRewards;
        }

        private void HandleOutcomeDismissed(BattleResult result)
        {
            if (result != null && result.casualSpineMatch)
            {
                _result = result;
                RouteToRewards(result);
                return;
            }

            PreparedBattleRewards prepared = PrepareRewards(result);
            _result = result;
            _rewardSummary = prepared != null ? prepared.summary : _rewardSummary;

            bool presented = _battleReport != null && _battleReport.DidPresentThisBattle;
            if (!presented)
            {
                ICombatSfxOrchestrator sfx = ResolveCombatSfxOrchestrator();
                sfx?.PlayPostBattle(result);
                BattleOutcomePresentation outcome = BattleOutcomePresentation.Resolve(result);
                if (outcome.ShowRewardsPage)
                {
                    sfx?.PlayXpShare(_rewardSummary);
                    sfx?.PlayStarUp(_rewardSummary);
                    sfx?.PlayRewardApplied(_rewardSummary);
                }
            }

            RouteToRewards(result);
        }

        private void RouteToPvPSelection(BattleResult result)
        {
            _state = RouterState.PvPMoveSelect;

            if (pvpMoveSelectController != null)
            {
                pvpMoveSelectController.BeginSelection(result.pvpMatchId, result.PlayerWon);
                Debug.Log("[PostBattleRouter] Routing to PvP move selection.");
                return;
            }

            Debug.LogWarning("[PostBattleRouter] PvP flow missing PvPMoveSelectController. Falling back to persistence.");
            BeginImmediatePersistence(result);
        }

        private void HandlePvPSelectionComplete(MoveInstance claimed)
        {
            Debug.Log($"[PostBattleRouter] PvP selection complete. Claimed: {(claimed != null ? claimed.displayName : "none")}");

            if (_result == null)
            {
                CompleteFlow();
                return;
            }

            BeginImmediatePersistence(_result);
        }

        private void BeginImmediatePersistence(BattleResult result)
        {
            _result = result;
            _rewardSummary = _rewardSummary != null
                ? _rewardSummary
                : _rewardCalculator != null
                ? _rewardCalculator.Calculate(result)
                : new BattleRewardSummary();
            _state = RouterState.PersistingRewards;
            PersistOutcomeAndCompleteAsync();
        }

        private async void PersistOutcomeAndCompleteAsync()
        {
            BattleResult capturedResult = _result;
            bool returnBeforePersist = capturedResult != null &&
                (capturedResult.wasFled || capturedResult.skipPostBattlePresentation);

            if (returnBeforePersist)
                CompleteFlow();

            bool success = true;

            try
            {
                if (_progressionGrantTask != null)
                    await _progressionGrantTask;
                else
                    await ApplyProgressionGrantsCoreAsync();

                if (_victoryKnownGrantTask != null)
                    await _victoryKnownGrantTask;
                else
                    await ApplyVictoryKnownGrantsCoreAsync();

                if (_rewardPersistence != null && _rewardPersistence.IsAvailable)
                {
                    string walletAddress = ResolveWalletAddress();
                    BattleOutcomeBundle bundle = BattleStatsAggregator.BuildOutcomeBundle(capturedResult, _rewardSummary);
                    success = await _rewardPersistence.PersistOutcomeAsync(bundle, walletAddress);
                    if (success)
                        PatchCachedRosterHp(capturedResult);
                }
            }
            catch (Exception ex)
            {
                success = false;
                Debug.LogWarning($"[PostBattleRouter] Reward persistence failed: {ex.Message}");
            }

            if (!success)
                Debug.LogWarning("[PostBattleRouter] Reward persistence returned false. Continuing flow anyway.");

            NotifyFavorBattleCompleted(capturedResult);
            if (!returnBeforePersist)
                CompleteFlow();
        }

        private static void NotifyFavorBattleCompleted(BattleResult result)
        {
            if (result == null || result.battleType != BattleType.PvE)
                return;

            IFavorService favor = FavorRuntime.Active;
            if (favor == null)
            {
                FavorService[] services = FindObjectsOfType<FavorService>(true);
                if (services != null && services.Length > 0)
                    favor = services[0];
            }

            favor?.NotifyBattleCompleted();
        }

        private async Task ApplyProgressionGrantsCoreAsync()
        {
            if (_progressionGrantsApplied)
                return;

            if (_progressionGrantService == null ||
                _rewardSummary == null ||
                _result == null ||
                _result.battleType != BattleType.PvE)
            {
                return;
            }

            GameSessionContext session = GameSessionContext.Instance;
            await _progressionGrantService.ApplyGrantsAsync(
                _result,
                _rewardSummary,
                session != null ? session.PlayerProfileId : "",
                ResolveWalletAddress(),
                session != null ? session.AuthUserId : "");
            _progressionGrantsApplied = true;
        }

        private async Task ApplyVictoryKnownGrantsCoreAsync()
        {
            if (_victoryKnownGrantsApplied)
                return;

            if (_victoryKnownGrantService == null ||
                _result == null ||
                _result.battleType != BattleType.PvE)
            {
                return;
            }

            GameSessionContext session = GameSessionContext.Instance;
            List<EncounterVictoryKnownGrantResult> grants = await _victoryKnownGrantService.ApplyGrantsAsync(
                _result,
                session != null ? session.PlayerProfileId : "");

            if (grants != null && grants.Count > 0)
            {
                _result.victoryKnownGrantResults = grants;
                if (_rewardSummary != null)
                    _rewardSummary.victoryKnownGrants = grants;
            }

            _victoryKnownGrantsApplied = true;
        }

        private static void PatchCachedRosterHp(BattleResult result)
        {
            if (result == null || result.battleType != BattleType.PvE || result.playerParticipants == null)
                return;

            SupabasePlayerRosterProvider[] rosterProviders = FindObjectsOfType<SupabasePlayerRosterProvider>(true);
            for (int i = 0; i < rosterProviders.Length; i++)
            {
                SupabasePlayerRosterProvider provider = rosterProviders[i];
                if (provider?.CachedRoster == null)
                    continue;

                int patched = ChainlingRosterHpMutation.PatchRosterFromParticipants(
                    provider.CachedRoster,
                    result.playerParticipants);
                if (patched > 0)
                    Debug.Log($"[PostBattleRouter] Patched HP on {patched} cached roster Chainling(s).");
            }

            SupabasePlayerLineupPartyProvider[] partyProviders = FindObjectsOfType<SupabasePlayerLineupPartyProvider>(true);
            for (int i = 0; i < partyProviders.Length; i++)
            {
                SupabasePlayerLineupPartyProvider provider = partyProviders[i];
                if (provider?.CachedParty == null)
                    continue;

                int patched = ChainlingRosterHpMutation.PatchPartyFromParticipants(
                    provider.CachedParty,
                    result.playerParticipants);
                if (patched > 0)
                    Debug.Log($"[PostBattleRouter] Patched HP on {patched} cached party Chainling(s).");
            }
        }

        private void CompleteFlow()
        {
            var completedResult = _result;
            _state = RouterState.Idle;
            _result = null;
            _rewardSummary = null;
            _progressionGrantsApplied = false;
            _victoryKnownGrantsApplied = false;
            _progressionGrantTask = null;
            _victoryKnownGrantTask = null;
            _preparedRewards = null;

            _playerMap?.Enable();

            ReturnToOverworld(completedResult);

            Debug.Log("[PostBattleRouter] Post-battle flow complete. Returning to overworld.");
            OnPostBattleComplete?.Invoke();
        }

        private void ReturnToOverworld(BattleResult result)
        {
            var bridge = FindObjectOfType<SolanaServiceBridge>();
            IEncounterReturnContextService returnContextService = bridge != null
                ? bridge.EncounterReturnContextService
                : null;

            BattleOutcomePresentation outcome = BattleOutcomePresentation.Resolve(result);
            bool playerDefeated = outcome.Kind == BattleOutcomeKind.Defeat;

            if (playerDefeated)
            {
                returnContextService?.ClearReturnContext();
                bridge?.ClearActiveEncounter();
                RaiseReturnToOverworld(new OverworldReturnInfo
                {
                    sceneName = homeSceneName,
                    spawnId = homeSpawnId,
                    useExactPosition = false,
                    isDefeatReturn = true,
                });
                return;
            }

            var returnCtx = returnContextService?.GetReturnContext();
            if (returnCtx != null && returnCtx.isValid)
            {
                RaiseReturnToOverworld(new OverworldReturnInfo
                {
                    sceneName = returnCtx.sceneId,
                    useExactPosition = true,
                    position = returnCtx.position,
                    rotation = returnCtx.rotation,
                    returnSpawnId = returnCtx.returnSpawnId ?? "",
                    loadedChunkIds = returnCtx.loadedChunkIds,
                });
            }
            else
            {
                Debug.LogWarning("[PostBattleRouter] No return context found. Going home.");
                RaiseReturnToOverworld(new OverworldReturnInfo
                {
                    sceneName = homeSceneName,
                    spawnId = homeSpawnId,
                    useExactPosition = false,
                });
            }

            returnContextService?.ClearReturnContext();
            bridge?.ClearActiveEncounter();
        }

        private void RaiseReturnToOverworld(OverworldReturnInfo info)
        {
            if (OnReturnToOverworld == null)
            {
                Debug.LogError(
                    "[PostBattleRouter] OnReturnToOverworld has no listeners. Scene will not unload. " +
                    "Add PostBattleSceneReturnHandler to the same GameObject as CombatPostBattleRouter (or assign it) so SceneLoader can return from battle.");
                return;
            }

            Debug.Log(
                $"[PostBattleRouter] Invoking OnReturnToOverworld: scene='{info.sceneName}', " +
                $"useExactPosition={info.useExactPosition}, spawnId='{info.spawnId}', returnSpawnId='{info.returnSpawnId}'.");

            OnReturnToOverworld.Invoke(info);
        }

        private ICombatSfxOrchestrator ResolveCombatSfxOrchestrator()
        {
            if (_combatSfxOrchestrator != null)
                return _combatSfxOrchestrator;

            if (combatSfxOrchestratorBehaviour != null && combatSfxOrchestratorBehaviour is ICombatSfxOrchestrator assigned)
            {
                _combatSfxOrchestrator = assigned;
                ConfigureConcreteSfxOrchestrator(assigned);
                return _combatSfxOrchestrator;
            }

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatSfxOrchestrator discovered)
                {
                    combatSfxOrchestratorBehaviour = behaviours[i];
                    _combatSfxOrchestrator = discovered;
                    ConfigureConcreteSfxOrchestrator(discovered);
                    return _combatSfxOrchestrator;
                }
            }

            if (battleSfxCatalog == null)
                return null;

            if (_runtimeCombatSfxOrchestrator == null)
                _runtimeCombatSfxOrchestrator = gameObject.AddComponent<CombatSfxOrchestrator>();

            _runtimeCombatSfxOrchestrator.SetCatalog(battleSfxCatalog);
            combatSfxOrchestratorBehaviour = _runtimeCombatSfxOrchestrator;
            _combatSfxOrchestrator = _runtimeCombatSfxOrchestrator;
            return _combatSfxOrchestrator;
        }

        private IProgressionGrantPersistence ResolveProgressionGrantPersistence()
        {
            if (progressionGrantPersistenceOverride is IProgressionGrantPersistence assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IProgressionGrantPersistence discovered)
                {
                    progressionGrantPersistenceOverride = behaviours[i];
                    return discovered;
                }
            }

            return null;
        }

        private IEncounterVictoryKnownGrantPersistence ResolveVictoryKnownGrantPersistence()
        {
            if (victoryKnownGrantPersistenceOverride is IEncounterVictoryKnownGrantPersistence assigned)
                return assigned;

            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IEncounterVictoryKnownGrantPersistence discovered)
                {
                    victoryKnownGrantPersistenceOverride = behaviours[i];
                    return discovered;
                }
            }

            return null;
        }

        private void ConfigureConcreteSfxOrchestrator(ICombatSfxOrchestrator orchestrator)
        {
            if (battleSfxCatalog == null || !(orchestrator is CombatSfxOrchestrator concrete))
                return;

            if (concrete.Catalog == null)
                concrete.SetCatalog(battleSfxCatalog);
        }

        private string ResolveWalletAddress()
        {
            var bridge = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance
                : FindObjectOfType<SolanaServiceBridge>();

            return bridge?.WalletService?.PublicKey;
        }
    }
}
