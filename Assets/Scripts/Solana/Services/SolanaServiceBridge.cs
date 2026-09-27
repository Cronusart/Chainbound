using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Solana;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// MonoBehaviour bridge that initializes and holds all Solana-related services.
    /// Only this class may instantiate services.
    /// Attach to the GameServices prefab as a child component.
    /// </summary>
    public class SolanaServiceBridge : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private SolanaConfigSO solanaConfig;

        [Header("Capture Config")]
        [SerializeField] private CaptureConfigSO captureConfig;

        [Header("Authoritative PvE")]
        [SerializeField] private AuthoritativeCombatServiceConfigSO authoritativeCombatConfig;

        [Header("Move Database")]
        [SerializeField] private MoveDatabaseSO moveDatabase;

        [Header("Modifier Catalog")]
        [SerializeField] private ModifierCatalogSO modifierCatalog;

        [Header("Zone Encounter")]
        [Tooltip("Optional: ZoneEncounterProvider MonoBehaviour for the new encounter system.")]
        [SerializeField] private ZoneEncounterProvider zoneEncounterProvider;

        [NonSerialized] private ManualEncounterProviderSO _activeManualEncounter;
        private readonly EncounterReturnContextService _encounterReturnContextService = new EncounterReturnContextService();

        // â”€â”€ Parsed config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private PublicKey _gameProgramPk;
        private PublicKey _vrfProgramPk;
        private PublicKey _vrfOracleQueuePk;
        private string _resolvedRpcEndpoint;
        private const string RpcEndpointEnvVar = "CHAINBOUND_SOLANA_RPC_ENDPOINT";
        private const string RuntimeConfigFileName = "Config.json";

        [Serializable]
        private class RuntimeConfigOverrides
        {
            public string solanaRpcEndpoint;
        }

        // â”€â”€ Service Instances â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private SolanaWalletService _walletService;
        private SolanaRpcService _rpcService;
        private SolanaChainTxOrchestrator _txOrchestrator;
        private ProfileService _profileService;
        private IVrfService _vrfService;
        private MoveVaultService _moveVaultService;
        private ChainlingVaultService _chainlingVaultService;
        private EquippedMoveRegistryService _equippedRegistryService;
        private SolanaTimeService _solanaTimeService;
        private EncounterRollService _encounterRollService;
        private PdaBootstrapService _bootstrapService;
        private PlayerStateCache _playerStateCache;

        // â”€â”€ Public Typed Accessors â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public SolanaConfigSO Config => solanaConfig;
        public IWalletService WalletService => _walletService;
        public SolanaWalletService WalletServiceInstance => _walletService;
        public IRpcService RpcService => _rpcService;
        public IChainTxOrchestrator TxOrchestrator => _txOrchestrator;
        public IProfileService ProfileService => _profileService;
        public IVrfService VrfService => _vrfService;
        public EncounterRollService EncounterRollService => _encounterRollService;
        public ZoneEncounterProvider ZoneEncounterProvider => zoneEncounterProvider;
        public ManualEncounterProviderSO ActiveManualEncounter => _activeManualEncounter;
        public IEncounterReturnContextService EncounterReturnContextService => _encounterReturnContextService;
        public IMoveVaultService MoveVaultService => _moveVaultService;
        public IChainlingVaultService ChainlingVaultService => _chainlingVaultService;
        public IEquippedMoveRegistryService EquippedRegistryService => _equippedRegistryService;
        /// <summary>Profile-first wild capture (Supabase dossier + Known move).</summary>
        [Obsolete("Use CombatSessionService/ICaptureSession. This probability RPC remains only for unsupported legacy clients.")]
        public ICaptureService CaptureService => ResolveCaptureService();
        public ICombatSessionService CombatSessionService => ResolveAuthoritativeCombatService();
        public ICaptureLoadoutService CaptureLoadoutService => ResolveAuthoritativeCombatService();
        public IAuthoritativeCombatAvailability CombatAuthorityAvailability => ResolveAuthoritativeCombatService();

        /// <summary>Shared move database used by wild encounter move rolls.</summary>
        public MoveDatabaseSO MoveDatabase => moveDatabase;

        private ICaptureService ResolveCaptureService()
        {
            SupabaseProfileCaptureService profileCapture =
                FindFirstObjectByType<SupabaseProfileCaptureService>(FindObjectsInactive.Include);
            if (profileCapture != null)
                return profileCapture;

            return SupabaseProfileCaptureService.ResolveOrCreate();
        }

        private AuthoritativeCombatSessionService ResolveAuthoritativeCombatService()
        {
            AuthoritativeCombatSessionService service = GetComponent<AuthoritativeCombatSessionService>();
            if (service == null)
                service = gameObject.AddComponent<AuthoritativeCombatSessionService>();
            service.Configure(authoritativeCombatConfig);
            return service;
        }
        public SolanaTimeService TimeService => _solanaTimeService;
        public PlayerStateCache PlayerState => _playerStateCache;

        // â”€â”€ Singleton â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static SolanaServiceBridge _instance;
        public static SolanaServiceBridge Instance => _instance;

        // â”€â”€ Unity Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[SolanaServiceBridge] Duplicate found. Destroying.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitializeServices();
            EnsureSupabaseCombatPersistence();
        }

        private void EnsureSupabaseCombatPersistence()
        {
            SupabaseConfigSO config = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            if (config == null)
                return;

            SupabaseProgressionGrantService progression = GetComponent<SupabaseProgressionGrantService>();
            if (progression == null)
                progression = gameObject.AddComponent<SupabaseProgressionGrantService>();
            progression.Configure(config);

            SupabaseEncounterVictoryKnownGrantService victoryKnown =
                GetComponent<SupabaseEncounterVictoryKnownGrantService>();
            if (victoryKnown == null)
                victoryKnown = gameObject.AddComponent<SupabaseEncounterVictoryKnownGrantService>();
            victoryKnown.Configure(config);
        }

        private void Update()
        {
            _solanaTimeService?.Tick();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ResumePendingOperations();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            _walletService?.Dispose();
            _rpcService?.Dispose();
        }

        // â”€â”€ Config Validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool ValidateConfig()
        {
            if (solanaConfig == null)
            {
                Debug.LogError("[SolanaServiceBridge] SolanaConfigSO is null. Assign it in the Inspector.");
                return false;
            }
            _resolvedRpcEndpoint = ResolveRpcEndpoint();
            if (string.IsNullOrEmpty(_resolvedRpcEndpoint))
            {
                Debug.LogError("[SolanaServiceBridge] rpcEndpoint is empty in SolanaConfigSO.");
                return false;
            }
            string gameProgramId = NormalizeConfigValue(solanaConfig.gameProgramId);

            if (string.IsNullOrEmpty(gameProgramId))
            {
                Debug.LogError("[SolanaServiceBridge] gameProgramId is empty in SolanaConfigSO.");
                return false;
            }

            try { _gameProgramPk = new PublicKey(gameProgramId); }
            catch (Exception ex)
            {
                Debug.LogError($"[SolanaServiceBridge] gameProgramId is not a valid base58 pubkey: {ex.Message}");
                return false;
            }

            string vrfProgramId = NormalizeConfigValue(solanaConfig.vrfProgramId);
            string vrfQueueId = NormalizeConfigValue(solanaConfig.vrfOracleQueueId);
            if (vrfProgramId != global::Chainbound.Combat.VrfService.ExpectedVrfProgramId ||
                vrfQueueId != global::Chainbound.Combat.VrfService.ExpectedOracleQueueId)
            {
                Debug.LogError("[SolanaServiceBridge] MagicBlock VRF program/queue do not match the reviewed base-layer pins.");
                return false;
            }
            try
            {
                _vrfProgramPk = new PublicKey(vrfProgramId);
                _vrfOracleQueuePk = new PublicKey(vrfQueueId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SolanaServiceBridge] MagicBlock VRF configuration is invalid: {ex.Message}");
                return false;
            }

            return true;
        }

        // â”€â”€ Initialization â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void InitializeServices()
        {
            if (solanaConfig != null && solanaConfig.useStubServices)
            {
                Debug.Log("[SolanaServiceBridge] Using STUB services (demo mode). Blockchain features disabled.");
                return;
            }

            if (!ValidateConfig())
            {
                Debug.LogError("[SolanaServiceBridge] Config validation failed. Services NOT initialized.");
                return;
            }

            Debug.Log("[SolanaServiceBridge] Initializing production Solana services...");

            // â”€â”€ Layer 1: Wallet + RPC â”€â”€
            _walletService = new SolanaWalletService(solanaConfig);
            _rpcService = new SolanaRpcService(_resolvedRpcEndpoint);

            // â”€â”€ Layer 2: Tx Orchestrator â”€â”€
            _txOrchestrator = new SolanaChainTxOrchestrator(_walletService, _rpcService);

            // â”€â”€ Layer 2.5: Solana Time Service (cluster-authoritative time) â”€â”€
            _solanaTimeService = new SolanaTimeService(_rpcService);

            // â”€â”€ Layer 3: Profile Service (RAM authoritative) â”€â”€
            _profileService = new ProfileService(_rpcService, _txOrchestrator, _gameProgramPk);

            // â”€â”€ Layer 4: VRF Service â”€â”€
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            _vrfService = solanaConfig.useDeterministicVrfInEditor
                ? new DeterministicVrfService(Environment.TickCount)
                : new VrfService(_rpcService, _txOrchestrator, _walletService, _gameProgramPk, _vrfProgramPk, _vrfOracleQueuePk);
#else
            _vrfService = new VrfService(
                _rpcService, _txOrchestrator, _walletService, _gameProgramPk, _vrfProgramPk, _vrfOracleQueuePk);
#endif

            // â”€â”€ Layer 5: Move Vault Service (Known moves only; RAM delegated to ProfileService) â”€â”€
            _moveVaultService = new MoveVaultService(
                _rpcService, _txOrchestrator, _walletService, _profileService, _gameProgramPk);

            // â”€â”€ Layer 6: Chainling Vault Service â”€â”€
            _chainlingVaultService = new ChainlingVaultService(
                _rpcService, _txOrchestrator, _walletService, _gameProgramPk);

            // â”€â”€ Layer 7: Equipped Move Registry Service â”€â”€
            _equippedRegistryService = new EquippedMoveRegistryService(
                _rpcService, _txOrchestrator, _walletService, _gameProgramPk);

            // â”€â”€ Layer 8: Upgrade Ticket Service (VRF anti-replay) â”€â”€


            // â”€â”€ Layer 12b: Zone Encounter Roll Service (local seeded RNG) â”€â”€
            _encounterRollService = new EncounterRollService();

            // â”€â”€ Layer 16: Bootstrap + Cache â”€â”€
            _playerStateCache = new PlayerStateCache();
            _bootstrapService = new PdaBootstrapService(
                _profileService, _moveVaultService, _chainlingVaultService,
                _equippedRegistryService, _playerStateCache);

            // â”€â”€ Wire wallet events â”€â”€
            _walletService.OnConnected += HandleWalletConnected;
            _walletService.OnDisconnected += HandleWalletDisconnected;
            _walletService.OnAccountChanged += HandleAccountChanged;

            // Wire TimeOfDayManager Solana CST callback (avoids circular asmdef dependency)
            TimeOfDayManager.GetSolanaCstTime = () =>
            {
                var ts = Instance?.TimeService;
                if (ts == null || !ts.HasClusterSync) return null;
                return (ts.NormalizedCstTimeOfDay, ts.HasClusterSync);
            };

            Debug.Log("[SolanaServiceBridge] All services initialized.");
        }

        private string ResolveRpcEndpoint()
        {
            string envEndpoint = Environment.GetEnvironmentVariable(RpcEndpointEnvVar);
            if (!string.IsNullOrWhiteSpace(envEndpoint))
                return envEndpoint.Trim();

            try
            {
                string runtimeConfigPath = Path.Combine(Application.streamingAssetsPath, RuntimeConfigFileName);
                if (File.Exists(runtimeConfigPath))
                {
                    string json = File.ReadAllText(runtimeConfigPath);
                    var overrides = JsonUtility.FromJson<RuntimeConfigOverrides>(json);
                    if (!string.IsNullOrWhiteSpace(overrides?.solanaRpcEndpoint))
                        return overrides.solanaRpcEndpoint.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaServiceBridge] Failed to read StreamingAssets/{RuntimeConfigFileName}: {ex.Message}");
            }

            return solanaConfig != null ? NormalizeConfigValue(solanaConfig.rpcEndpoint) : string.Empty;
        }

        private static string NormalizeConfigValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }


        // â”€â”€ Wallet Event Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task BootstrapWalletStateAsync(string pubkey)
        {
            if (string.IsNullOrWhiteSpace(pubkey))
            {
                Debug.LogWarning("[SolanaServiceBridge] Cannot bootstrap wallet state without a wallet public key.");
                return;
            }

            if (_bootstrapService != null)
            {
                await _bootstrapService.BootstrapAsync(pubkey);
            }

        }

        private async void HandleWalletConnected(string pubkey)
        {
            Debug.Log($"[SolanaServiceBridge] Wallet connected: {pubkey}.");

            if (_solanaTimeService != null)
            {
                _ = _solanaTimeService.InitialSyncAsync();
            }

            if (solanaConfig == null || !solanaConfig.autoBootstrapPdasOnWalletConnect)
            {
                Debug.Log("[SolanaServiceBridge] PDA bootstrap deferred until an explicit on-chain flow requests it.");
                return;
            }

            Debug.Log("[SolanaServiceBridge] Running PDA bootstrap...");
            await BootstrapWalletStateAsync(pubkey);
        }

        private void HandleWalletDisconnected()
        {
            Debug.Log("[SolanaServiceBridge] Wallet disconnected. Clearing caches.");
            _profileService?.ClearCache();
            _moveVaultService?.ClearCache();
            _chainlingVaultService?.ClearCache();
            _equippedRegistryService?.ClearCache();
            _playerStateCache?.Clear();
        }

        private async void HandleAccountChanged(string newPubkey)
        {
            Debug.Log($"[SolanaServiceBridge] Account changed to: {newPubkey}.");

            HandleWalletDisconnected();

            if (solanaConfig == null || !solanaConfig.autoBootstrapPdasOnWalletConnect)
            {
                Debug.Log("[SolanaServiceBridge] PDA bootstrap deferred after account change.");
                return;
            }

            Debug.Log("[SolanaServiceBridge] Re-bootstrapping PDA state...");
            await BootstrapWalletStateAsync(newPubkey);
        }

        // â”€â”€ Resume â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async void ResumePendingOperations()
        {
            if (_txOrchestrator != null)
                await _txOrchestrator.ResumeAllPendingAsync();

            if (_vrfService != null && _vrfService.HasPendingRequest)
                await _vrfService.ResumePendingAsync();
        }

        public void SetActiveManualEncounter(ManualEncounterProviderSO provider)
        {
            _activeManualEncounter = provider;
            if (zoneEncounterProvider != null)
                zoneEncounterProvider.ActiveResult = null;
        }

        public void ClearActiveEncounter()
        {
            _activeManualEncounter = null;
            if (zoneEncounterProvider != null)
                zoneEncounterProvider.ActiveResult = null;
        }
    }
}
