using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class MoveLearnStatsRecorder : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public Task<bool> RecordLevelUpMoveGrantedAsync(string chainlingBackendId, string moveId)
        {
            return RecordProgressionMoveGrantedAsync(chainlingBackendId, moveId, "level_up");
        }

        public Task<bool> RecordStarUpMoveGrantedAsync(string chainlingBackendId, string moveId)
        {
            return RecordProgressionMoveGrantedAsync(chainlingBackendId, moveId, "star_up");
        }

        public async Task<bool> RecordProgressionMoveGrantedAsync(
            string chainlingBackendId,
            string moveId,
            string source)
        {
            if (supabaseConfig == null)
            {
                Debug.LogWarning("[MoveLearnStatsRecorder] SupabaseConfigSO was not assigned.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(chainlingBackendId))
            {
                if (enableDebugLogs)
                    Debug.Log("[MoveLearnStatsRecorder] Skipped progression move grant stat; backend Chainling id was empty.", this);
                return false;
            }

            GameSessionContext session = GameSessionContext.Instance;
            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "progression" : source.Trim();
            string normalizedMoveId = string.IsNullOrWhiteSpace(moveId) ? "" : moveId.Trim();

            string json = JsonUtility.ToJson(new MetricIncrementRequest
            {
                player_profile_id = session != null ? session.PlayerProfileId : "",
                wallet_address = session != null ? session.WalletAddress : "",
                auth_user_id = session != null ? session.AuthUserId : "",
                chainling_id = chainlingBackendId.Trim(),
                metric_key = ChainlingMetricIds.PveProgressionMovesGranted,
                amount = 1,
                source = normalizedSource,
                move_id = normalizedMoveId,
                idempotency_key = BuildIdempotencyKey(session, chainlingBackendId, normalizedMoveId, normalizedSource),
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                "profile_increment_chainling_metric",
                json);

            if (!response.IsSuccess)
            {
                Debug.LogWarning($"[MoveLearnStatsRecorder] profile_increment_chainling_metric returned {response.statusCode}: {response.body}", this);
                return false;
            }

            MetricIncrementResponse payload = JsonUtility.FromJson<MetricIncrementResponse>(response.body);
            bool success = payload != null && payload.success;
            if (enableDebugLogs && success)
                Debug.Log($"[MoveLearnStatsRecorder] Recorded progression move grant for {chainlingBackendId} move={normalizedMoveId}.", this);

            return success;
        }

        private static string BuildIdempotencyKey(
            GameSessionContext session,
            string chainlingBackendId,
            string moveId,
            string source)
        {
            string profileId = session != null ? session.PlayerProfileId : "";
            return $"metric:{profileId}:{chainlingBackendId}:{ChainlingMetricIds.PveProgressionMovesGranted}:{source}:{moveId}";
        }

        [Serializable]
        private class MetricIncrementRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string chainling_id;
            public string metric_key;
            public int amount;
            public string source;
            public string move_id;
            public string idempotency_key;
        }

        [Serializable]
        private class MetricIncrementResponse
        {
            public bool success;
            public string error;
        }
    }
}
