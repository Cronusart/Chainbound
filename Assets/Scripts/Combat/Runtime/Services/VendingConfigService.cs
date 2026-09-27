using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Fetches authoritative vending levers from Supabase <c>get_vending_config</c>
    /// and applies them as a runtime override via <see cref="BalanceConfigService"/>.
    /// Falls back to local <see cref="VendingConfigSO"/> defaults when offline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VendingConfigService : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private VendingConfigSO localFallback;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float refreshIntervalSeconds = 300f;
        [SerializeField] private bool fetchOnStart = true;

        public static VendingConfigService Instance { get; private set; }

        public VendingConfigSO ActiveConfig { get; private set; }
        public string LastError { get; private set; }
        public string LastConfigHash { get; private set; }
        public long LastFetchUnixUtc { get; private set; }
        public bool HasServerOverride { get; private set; }

        private VendingConfigSO _runtimeOverride;
        private float _refreshTimer;
        private bool _fetchInFlight;

        public void Configure(SupabaseConfigSO config, VendingConfigSO fallback = null)
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
                Debug.LogWarning("[VendingConfigService] Duplicate found. Destroying.");
                Destroy(this);
                return;
            }

            Instance = this;
            ActiveConfig = localFallback != null ? localFallback : BalanceConfigRuntime.Vending;
        }

        private void Start()
        {
            if (fetchOnStart)
                _ = RefreshAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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

        public async Task<VendingConfigSO> RefreshAsync()
        {
            if (_fetchInFlight)
                return ActiveConfig;

            LastError = string.Empty;
            if (supabaseConfig == null)
            {
                LastError = "SupabaseConfigSO was not assigned.";
                if (enableDebugLogs)
                    Debug.LogWarning($"[VendingConfigService] {LastError}");
                ApplyLocalFallback();
                return ActiveConfig;
            }

            _fetchInFlight = true;
            try
            {
                string json = JsonUtility.ToJson(new VendingConfigRequest
                {
                    event_id = string.Empty,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "get_vending_config",
                    json);

                if (!response.IsSuccess)
                {
                    LastError = $"get_vending_config returned {response.statusCode}: {response.body}";
                    if (enableDebugLogs)
                        Debug.LogWarning($"[VendingConfigService] {LastError}");
                    ApplyLocalFallback();
                    return ActiveConfig;
                }

                VendingConfigResponse dto = JsonUtility.FromJson<VendingConfigResponse>(response.body);
                if (dto == null || !dto.success)
                {
                    LastError = "get_vending_config returned invalid payload.";
                    ApplyLocalFallback();
                    return ActiveConfig;
                }

                ApplyServerDto(dto);
                LastFetchUnixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[VendingConfigService] Applied server config v{dto.config_version} " +
                        $"hash={dto.config_hash} gen={dto.generator_version}");
                }

                return ActiveConfig;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                if (enableDebugLogs)
                    Debug.LogWarning($"[VendingConfigService] {LastError}");
                ApplyLocalFallback();
                return ActiveConfig;
            }
            finally
            {
                _fetchInFlight = false;
            }
        }

        private void ApplyServerDto(VendingConfigResponse dto)
        {
            if (_runtimeOverride == null)
            {
                _runtimeOverride = ScriptableObject.CreateInstance<VendingConfigSO>();
                _runtimeOverride.hideFlags = HideFlags.HideAndDontSave;
                _runtimeOverride.ResetToDefaults();
            }

            float[] tiers = dto.rarity_tier_weights_percent != null && dto.rarity_tier_weights_percent.Length == 6
                ? dto.rarity_tier_weights_percent
                : null;

            _runtimeOverride.ApplyServerOverride(
                dto.config_version,
                dto.generator_version,
                tiers,
                dto.ram_cost_get_known,
                dto.ram_cost_stamp_known,
                dto.vending_daily_cap,
                dto.event_favor_boost);

            ActiveConfig = _runtimeOverride;
            LastConfigHash = string.IsNullOrEmpty(dto.config_hash)
                ? _runtimeOverride.ComputeConfigHash()
                : dto.config_hash;
            HasServerOverride = true;

            if (BalanceConfigRuntime.Active != null)
                BalanceConfigRuntime.Active.SetVendingOverride(_runtimeOverride);
        }

        private void ApplyLocalFallback()
        {
            ActiveConfig = localFallback != null ? localFallback : VendingConfigSO.CreateRuntimeDefault();
            LastConfigHash = ActiveConfig.ComputeConfigHash();
            HasServerOverride = false;

            if (BalanceConfigRuntime.Active != null)
                BalanceConfigRuntime.Active.ClearVendingOverride();
        }

        [Serializable]
        private class VendingConfigRequest
        {
            public string event_id;
        }

        [Serializable]
        private class VendingConfigResponse
        {
            public bool success;
            public int config_version;
            public int generator_version;
            public string config_hash;
            public float[] rarity_tier_weights_percent;
            public int ram_cost_get_known;
            public int ram_cost_stamp_known;
            public int vending_daily_cap;
            public float event_favor_boost;
        }
    }
}
