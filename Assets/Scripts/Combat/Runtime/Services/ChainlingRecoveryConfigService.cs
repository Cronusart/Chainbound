using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// GameServices bridge for the passive-recovery lever board. Fetches presentation
    /// metadata from Supabase; health itself is never mutated by this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingRecoveryConfigService : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private ChainlingRecoveryConfigSO localFallback;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float refreshIntervalSeconds = 300f;
        [SerializeField] private bool fetchOnStart = true;

        public static ChainlingRecoveryConfigService Instance { get; private set; }

        public ChainlingRecoveryConfigSO ActiveConfig { get; private set; }
        public string LastError { get; private set; }
        public string LastConfigHash { get; private set; }
        public long LastFetchUnixUtc { get; private set; }
        public bool HasServerOverride { get; private set; }

        private ChainlingRecoveryConfigSO _runtimeOverride;
        private float _refreshTimer;
        private bool _fetchInFlight;

        public void Configure(SupabaseConfigSO config, ChainlingRecoveryConfigSO fallback = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (fallback != null)
                localFallback = fallback;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ChainlingRecoveryConfigService] Duplicate found. Destroying.");
                Destroy(this);
                return;
            }

            Instance = this;
            ActiveConfig = localFallback != null ? localFallback : BalanceConfigRuntime.ChainlingRecovery;
        }

        private void Start()
        {
            if (fetchOnStart)
                _ = RefreshAsync();
        }

        private void Update()
        {
            if (refreshIntervalSeconds <= 0f)
                return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < refreshIntervalSeconds)
                return;

            _refreshTimer = 0f;
            _ = RefreshAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public async Task<ChainlingRecoveryConfigSO> RefreshAsync()
        {
            if (_fetchInFlight)
                return ActiveConfig;

            LastError = string.Empty;
            if (supabaseConfig == null)
            {
                LastError = "SupabaseConfigSO was not assigned.";
                LogFallback();
                ApplyLocalFallback();
                return ActiveConfig;
            }

            _fetchInFlight = true;
            try
            {
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "get_chainling_recovery_config",
                    "{}");

                if (!response.IsSuccess)
                {
                    LastError = $"get_chainling_recovery_config returned {response.statusCode}: {response.body}";
                    LogFallback();
                    ApplyLocalFallback();
                    return ActiveConfig;
                }

                RecoveryConfigResponse dto = JsonUtility.FromJson<RecoveryConfigResponse>(response.body);
                if (dto == null || !dto.success)
                {
                    LastError = "get_chainling_recovery_config returned invalid payload.";
                    LogFallback();
                    ApplyLocalFallback();
                    return ActiveConfig;
                }

                ApplyServerDto(dto);
                LastFetchUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ChainlingRecoveryConfigService] Applied server config v{dto.config_version} " +
                        $"hash={LastConfigHash} interval={dto.interval_seconds}s heal={dto.heal_basis_points}bp.");
                }
                return ActiveConfig;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                LogFallback();
                ApplyLocalFallback();
                return ActiveConfig;
            }
            finally
            {
                _fetchInFlight = false;
            }
        }

        private void ApplyServerDto(RecoveryConfigResponse dto)
        {
            if (_runtimeOverride == null)
            {
                _runtimeOverride = ScriptableObject.CreateInstance<ChainlingRecoveryConfigSO>();
                _runtimeOverride.hideFlags = HideFlags.HideAndDontSave;
                _runtimeOverride.ResetToDefaults();
            }

            _runtimeOverride.ApplyServerOverride(
                dto.config_version,
                dto.enabled,
                dto.interval_seconds,
                dto.heal_basis_points,
                dto.max_catchup_intervals,
                dto.storage_only);

            ActiveConfig = _runtimeOverride;
            LastConfigHash = string.IsNullOrEmpty(dto.config_hash)
                ? _runtimeOverride.ComputeConfigHash()
                : dto.config_hash;
            HasServerOverride = true;

            if (BalanceConfigRuntime.Active != null)
                BalanceConfigRuntime.Active.SetChainlingRecoveryOverride(_runtimeOverride);
        }

        private void ApplyLocalFallback()
        {
            ActiveConfig = localFallback != null ? localFallback : ChainlingRecoveryConfigSO.CreateRuntimeDefault();
            LastConfigHash = ActiveConfig.ComputeConfigHash();
            HasServerOverride = false;

            if (BalanceConfigRuntime.Active != null)
                BalanceConfigRuntime.Active.ClearChainlingRecoveryOverride();
        }

        private void LogFallback()
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ChainlingRecoveryConfigService] {LastError}");
        }

        [Serializable]
        private class RecoveryConfigResponse
        {
            public bool success;
            public int config_version;
            public string config_hash;
            public bool enabled;
            public int interval_seconds;
            public int heal_basis_points;
            public int max_catchup_intervals;
            public bool storage_only;
        }
    }
}
