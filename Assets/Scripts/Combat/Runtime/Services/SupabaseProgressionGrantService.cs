using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseProgressionGrantService : MonoBehaviour, IProgressionGrantPersistence
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsAvailable => supabaseConfig != null;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<ProgressionGrantPersistenceResult> GrantProgressionKnownMoveAsync(ProgressionGrantRequest request)
        {
            if (supabaseConfig == null)
                return ProgressionGrantPersistenceResult.Failed("supabase_config_missing");
            if (request == null)
                return ProgressionGrantPersistenceResult.Failed("grant_request_missing");
            if (string.IsNullOrWhiteSpace(request.chainlingBackendId))
                return ProgressionGrantPersistenceResult.Failed("chainling_id_required");
            if (string.IsNullOrWhiteSpace(request.moveId))
                return ProgressionGrantPersistenceResult.Failed("move_id_required");

            GameSessionContext session = GameSessionContext.Instance;
            string json = JsonUtility.ToJson(new GrantRequest
            {
                player_profile_id = !string.IsNullOrWhiteSpace(request.playerProfileId)
                    ? request.playerProfileId
                    : session != null ? session.PlayerProfileId : "",
                wallet_address = !string.IsNullOrWhiteSpace(request.walletAddress)
                    ? request.walletAddress
                    : session != null ? session.WalletAddress : "",
                auth_user_id = !string.IsNullOrWhiteSpace(request.authUserId)
                    ? request.authUserId
                    : session != null ? session.AuthUserId : "",
                chainling_id = request.chainlingBackendId,
                move_id = request.moveId,
                source_event_id = request.sourceEventId,
                idempotency_key = request.idempotencyKey,
                event_type = request.eventType.ToString(),
                star_index = request.starIndex,
                mastery_cycle_index = request.masteryCycleIndex,
                battle_seed = request.battleSeed,
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                "profile_grant_progression_ordinal",
                json);

            if (!response.IsSuccess)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[SupabaseProgressionGrantService] profile_grant_progression_ordinal returned {response.statusCode}: {response.body}", this);
                return ProgressionGrantPersistenceResult.Failed($"profile_grant_progression_ordinal_http_{response.statusCode}");
            }

            GrantResponse payload = JsonUtility.FromJson<GrantResponse>(response.body);
            if (payload == null)
                return ProgressionGrantPersistenceResult.Failed("profile_grant_progression_ordinal_invalid_payload");

            if (enableDebugLogs && payload.success)
                Debug.Log($"[SupabaseProgressionGrantService] Granted progression Known move={payload.move_id} item={payload.move_item_id}.", this);

            return new ProgressionGrantPersistenceResult
            {
                success = payload.success,
                alreadyApplied = payload.already_applied,
                moveItemId = payload.move_item_id,
                knownInstanceId = payload.known_id,
                moveId = payload.move_id,
                ownedCopyCount = payload.owned_copy_count,
                error = payload.error,
            };
        }

        [Serializable]
        private class GrantRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string chainling_id;
            public string move_id;
            public string source_event_id;
            public string idempotency_key;
            public string event_type;
            public int star_index;
            public int mastery_cycle_index;
            public int battle_seed;
        }

        [Serializable]
        private class GrantResponse
        {
            public bool success;
            public bool already_applied;
            public string move_item_id;
            public string known_id;
            public string move_id;
            public int owned_copy_count;
            public string error;
        }
    }
}
