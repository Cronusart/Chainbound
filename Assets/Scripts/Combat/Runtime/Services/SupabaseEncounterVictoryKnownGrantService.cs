using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class SupabaseEncounterVictoryKnownGrantService : MonoBehaviour, IEncounterVictoryKnownGrantPersistence
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsAvailable => supabaseConfig != null;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<EncounterVictoryKnownGrantResult> GrantAsync(EncounterVictoryKnownGrantRequest request)
        {
            if (supabaseConfig == null)
                return Failed("supabase_config_missing");
            if (request == null)
                return Failed("grant_request_missing");
            if (string.IsNullOrWhiteSpace(request.moveId))
                return Failed("move_id_required");
            if (string.IsNullOrWhiteSpace(request.idempotencyKey))
                return Failed("idempotency_key_required");

            GameSessionContext session = GameSessionContext.Instance;
            string json = JsonUtility.ToJson(new GrantRequest
            {
                player_profile_id = !string.IsNullOrWhiteSpace(request.playerProfileId)
                    ? request.playerProfileId
                    : session != null ? session.PlayerProfileId : "",
                move_id = request.moveId,
                idempotency_key = request.idempotencyKey,
                encounter_id = request.narrativeEncounterId ?? "",
                battle_seed = request.battleSeed,
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                "ownership_grant_event_known",
                json);

            if (!response.IsSuccess)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[SupabaseEncounterVictoryKnownGrantService] ownership_grant_event_known returned {response.statusCode}: {response.body}",
                        this);
                }

                return Failed($"ownership_grant_event_known_http_{response.statusCode}");
            }

            GrantResponse payload = JsonUtility.FromJson<GrantResponse>(response.body);
            if (payload == null)
                return Failed("ownership_grant_event_known_invalid_payload");

            if (enableDebugLogs && payload.success)
            {
                Debug.Log(
                    $"[SupabaseEncounterVictoryKnownGrantService] Granted victory Known move={payload.move_id} known={payload.known_instance_id}.",
                    this);
            }

            return new EncounterVictoryKnownGrantResult
            {
                success = payload.success,
                alreadyGranted = payload.already_granted,
                moveId = payload.move_id,
                knownInstanceId = payload.known_instance_id,
                moveItemId = payload.move_item_id,
                error = payload.error,
            };
        }

        public static SupabaseEncounterVictoryKnownGrantService ResolveOrCreate(SupabaseConfigSO config = null)
        {
            SupabaseEncounterVictoryKnownGrantService existing =
                FindFirstObjectByType<SupabaseEncounterVictoryKnownGrantService>(FindObjectsInactive.Include);
            if (existing == null)
            {
                SolanaServiceBridge bridge = SolanaServiceBridge.Instance
                    ?? FindFirstObjectByType<SolanaServiceBridge>(FindObjectsInactive.Include);
                GameObject host = bridge != null ? bridge.gameObject : new GameObject("[SupabaseEncounterVictoryKnownGrantService]");
                if (bridge == null)
                    DontDestroyOnLoad(host);
                existing = host.GetComponent<SupabaseEncounterVictoryKnownGrantService>();
                if (existing == null)
                    existing = host.AddComponent<SupabaseEncounterVictoryKnownGrantService>();
            }

            if (config == null)
                config = Resources.Load<SupabaseConfigSO>("SupabaseConfig");

            if (config != null)
                existing.Configure(config);

            return existing;
        }

        private static EncounterVictoryKnownGrantResult Failed(string error)
        {
            return new EncounterVictoryKnownGrantResult
            {
                success = false,
                error = error ?? "",
            };
        }

        [Serializable]
        private class GrantRequest
        {
            public string player_profile_id;
            public string move_id;
            public string idempotency_key;
            public string encounter_id;
            public int battle_seed;
        }

        [Serializable]
        private class GrantResponse
        {
            public bool success;
            public bool already_granted;
            public string move_id;
            public string known_instance_id;
            public string move_item_id;
            public string error;
        }
    }
}
