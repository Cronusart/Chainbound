using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabasePlayerStatsProvider : MonoBehaviour, IPlayerStatsProvider
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public PlayerStatsSnapshot CachedStats { get; private set; }
        public string LastError { get; private set; }

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<PlayerStatsSnapshot> FetchStatsAsync(string playerProfileId, string walletAddress)
        {
            LastError = "";
            CachedStats = null;

            if (supabaseConfig == null)
                return Fail("SupabaseConfigSO was not assigned.");

            string profileId = !string.IsNullOrWhiteSpace(playerProfileId)
                ? playerProfileId.Trim()
                : (GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "");
            string wallet = !string.IsNullOrWhiteSpace(walletAddress)
                ? walletAddress.Trim()
                : (GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");

            if (string.IsNullOrWhiteSpace(profileId) && string.IsNullOrWhiteSpace(wallet))
                return Fail("Player profile id or wallet address was empty.");

            PlayerRamBalanceStore.ReadToken ramRead = PlayerRamBalanceStore.BeginProfileRead(profileId);

            try
            {
                string json = JsonUtility.ToJson(new StatsRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, "profile_get_stats", json);
                if (!response.IsSuccess)
                    return Fail($"profile_get_stats returned {response.statusCode}: {response.body}");

                StatsResponse payload = JsonUtility.FromJson<StatsResponse>(response.body);
                if (payload == null)
                    return Fail("profile_get_stats returned an invalid payload.");

                StatsPayload stats = payload.stats ?? new StatsPayload();
                CachedStats = new PlayerStatsSnapshot
                {
                    playerProfileId = payload.player_profile_id,
                    displayName = payload.display_name,
                    walletAddress = payload.wallet_address,
                    ramBalance = payload.ram_balance,
                    handbookUnlocked = payload.progression_state != null && payload.progression_state.handbook_unlocked,
                    battlesWon = stats.battles_won,
                    battlesLost = stats.battles_lost,
                    battlesDrawn = stats.battles_drawn,
                    battlesFled = stats.battles_fled,
                    totalKosScored = stats.total_kos_scored,
                    totalKosTaken = stats.total_kos_taken,
                    totalCaptures = stats.total_captures,
                    entangledCaptures = stats.entangled_captures,
                    captureAttempts = stats.capture_attempts,
                    captureOrbsUsed = stats.capture_orbs_used,
                    captureTimeouts = stats.capture_timeouts,
                    captureTargetsEscaped = stats.capture_targets_escaped,
                    capturePerfectLocks = stats.capture_perfect_locks,
                    capturesAboveLowHp = stats.captures_above_low_hp,
                    capturesByOrbJson = stats.captures_by_orb ?? "{}",
                    fastestCaptureMilliseconds = stats.fastest_capture_milliseconds,
                    extremeProfileCaptures = stats.extreme_profile_captures,
                    entangleOrbCaptures = stats.entangle_orb_captures,
                    captureAchievementIds = payload.capture_achievement_ids != null
                        ? new System.Collections.Generic.List<string>(payload.capture_achievement_ids)
                        : new System.Collections.Generic.List<string>(),
                    totalRebirths = stats.total_rebirths,
                    totalStarUps = stats.total_star_ups,
                    totalMasteryCycles = stats.total_mastery_cycles,
                    totalRamEarned = stats.total_ram_earned,
                    totalRamSpent = stats.total_ram_spent,
                    totalKnownMovesAcquired = stats.total_ordinals_acquired,
                    totalStampedMovesCreated = stats.total_inscriptions_created,
                    candyMachineRolls = stats.candy_machine_rolls,
                    candyMachineStamps = stats.candy_machine_stamps,
                    uniqueSpeciesCaptured = stats.unique_species_captured,
                };

                if (!PlayerRamBalanceStore.TryApplyProfileRead(ramRead, CachedStats.ramBalance, payload.ram_balance_version) &&
                    PlayerRamBalanceStore.TryGet(profileId, out long currentRamBalance))
                {
                    CachedStats.ramBalance = currentRamBalance;
                }

                if (enableDebugLogs)
                    Debug.Log($"[SupabasePlayerStatsProvider] Loaded stats for profile={CachedStats.playerProfileId}.", this);

                return CachedStats;
            }
            catch (Exception ex)
            {
                return Fail($"FetchStatsAsync exception: {ex.Message}");
            }
        }

        private PlayerStatsSnapshot Fail(string reason)
        {
            LastError = reason;
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabasePlayerStatsProvider] {reason}", this);
            return null;
        }

        [Serializable]
        private class StatsRequest
        {
            public string player_profile_id;
            public string wallet_address;
        }

        [Serializable]
        private class StatsResponse
        {
            public string player_profile_id;
            public string display_name;
            public string wallet_address;
            public long ram_balance;
            public long ram_balance_version;
            public ProgressionPayload progression_state;
            public StatsPayload stats;
            public string[] capture_achievement_ids;
        }

        [Serializable]
        private class ProgressionPayload
        {
            public bool handbook_unlocked;
        }

        [Serializable]
        private class StatsPayload
        {
            public int battles_won;
            public int battles_lost;
            public int battles_drawn;
            public int battles_fled;
            public int total_kos_scored;
            public int total_kos_taken;
            public int total_captures;
            public int entangled_captures;
            public int capture_attempts;
            public int capture_orbs_used;
            public int capture_timeouts;
            public int capture_targets_escaped;
            public int capture_perfect_locks;
            public int captures_above_low_hp;
            public string captures_by_orb;
            public int fastest_capture_milliseconds;
            public int extreme_profile_captures;
            public int entangle_orb_captures;
            public int total_rebirths;
            public int total_star_ups;
            public int total_mastery_cycles;
            public long total_ram_earned;
            public long total_ram_spent;
            public int total_ordinals_acquired;
            public int total_inscriptions_created;
            public int candy_machine_rolls;
            public int candy_machine_stamps;
            public int unique_species_captured;
        }
    }
}
