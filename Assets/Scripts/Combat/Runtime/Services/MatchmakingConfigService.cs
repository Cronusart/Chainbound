using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Fetches get_pvp_config and refuses casual queue when the server board cannot be confirmed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchmakingConfigService : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool fetchOnStart = true;

        public static MatchmakingConfigService Instance { get; private set; }

        public bool HasServerOverride { get; private set; }
        public bool ServerCasualEnabled { get; private set; } = true;
        public string LastError { get; private set; }
        public string ServerConfigHash { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PvPMatch] Duplicate MatchmakingConfigService.");
                Destroy(this);
                return;
            }

            Instance = this;
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

        public void Configure(SupabaseConfigSO config)
        {
            if (config != null)
                supabaseConfig = config;
        }

        public async Task<bool> RefreshAsync()
        {
            LastError = string.Empty;
            HasServerOverride = false;
            if (supabaseConfig == null)
            {
                LastError = "server_unreachable";
                return false;
            }

            try
            {
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "get_pvp_config",
                    "{}");
                if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.body))
                {
                    LastError = "server_unreachable";
                    return false;
                }

                PvpConfigResponse dto = JsonUtility.FromJson<PvpConfigResponse>(response.body);
                if (dto == null || !dto.success)
                {
                    LastError = "server_unreachable";
                    return false;
                }

                MatchmakingConfigSO probe = ScriptableObject.CreateInstance<MatchmakingConfigSO>();
                probe.hideFlags = HideFlags.HideAndDontSave;
                probe.ApplyServerOverride(
                    dto.config_version,
                    dto.casual_enabled,
                    dto.ranked_enabled,
                    dto.require_wallet_for_casual,
                    dto.require_wallet_for_ranked,
                    dto.queue_timeout_seconds,
                    dto.turn_forfeit_missed_turns,
                    dto.turn_action_time_limit_seconds,
                    dto.max_app_version_skew,
                    dto.default_casual_ruleset_id,
                    dto.rematch_cooldown_seconds,
                    dto.lobby_max_players,
                    dto.rating_band_start,
                    dto.widen_per_second,
                    dto.band_cap,
                    dto.result_window_seconds,
                    dto.lock_deadline_seconds);

                if (!string.Equals(probe.ComputeConfigHash(), dto.config_hash, StringComparison.OrdinalIgnoreCase))
                {
                    LastError = "config_mismatch";
                    Debug.LogWarning("[PvPMatch] Server matchmaking payload does not match its hash.");
                    Destroy(probe);
                    return false;
                }

                ServerCasualEnabled = probe.casualEnabled;
                MatchmakingConfigSO baked = BalanceConfigRuntime.Matchmaking;
                if (!MatchmakingConfigSO.AcceptsServerBoard(baked, probe, dto.config_hash))
                {
                    LastError = "config_mismatch";
                    Debug.LogWarning("[PvPMatch] Baked matchmaking hash does not match the server. Override was not applied.");
                    Destroy(probe);
                    return false;
                }

                ServerConfigHash = dto.config_hash;
                HasServerOverride = true;
                if (BalanceConfigRuntime.Active != null)
                    BalanceConfigRuntime.Active.SetMatchmakingOverride(probe);
                else
                    Destroy(probe);
                return true;
            }
            catch (Exception ex)
            {
                LastError = "server_unreachable";
                Debug.LogWarning($"[PvPMatch] get_pvp_config failed: {ex.Message}");
                return false;
            }
        }

        [Serializable]
        private class PvpConfigResponse
        {
            public bool success;
            public int config_version;
            public string config_hash;
            public bool casual_enabled;
            public bool ranked_enabled;
            public bool require_wallet_for_casual;
            public bool require_wallet_for_ranked;
            public int queue_timeout_seconds;
            public int turn_forfeit_missed_turns;
            public int turn_action_time_limit_seconds;
            public int max_app_version_skew;
            public string default_casual_ruleset_id;
            public int rematch_cooldown_seconds;
            public int lobby_max_players;
            public int rating_band_start;
            public int widen_per_second;
            public int band_cap;
            public int result_window_seconds;
            public int lock_deadline_seconds;
        }
    }
}
