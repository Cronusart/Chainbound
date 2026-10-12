using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production world-pickup bag grant. Posts <c>spawn_id</c> to
    /// <c>profile_grant_world_pickup</c>; the server owns the reward row.
    /// </summary>
    public sealed class CoreBagWorldPickupGrantService : MonoBehaviour, IPlayerItemInventory
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public static CoreBagWorldPickupGrantService EnsureInstance()
        {
            CoreBagWorldPickupGrantService existing = FindFirstObjectByType<CoreBagWorldPickupGrantService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.EnsureInventoryReader();
                return existing;
            }

            Transform host = ResolveHost();
            CoreBagWorldPickupGrantService created = host.gameObject.AddComponent<CoreBagWorldPickupGrantService>();
            created.EnsureConfig();
            created.EnsureInventoryReader();
            return created;
        }

        private void Awake()
        {
            EnsureConfig();
            EnsureInventoryReader();
        }

        public async Task<WorldPickupGrantResult> GrantWorldPickupAsync(string spawnId)
        {
            EnsureConfig();
            if (supabaseConfig == null)
                return WorldPickupGrantResult.Failed("supabase_config_missing");

            string normalized = (spawnId ?? "").Trim();
            if (normalized.Length == 0)
                return WorldPickupGrantResult.Failed("spawn_id_required");

            string profileId = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.PlayerProfileId ?? "").Trim()
                : "";
            string wallet = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.WalletAddress ?? "").Trim()
                : "";

            try
            {
                string json = JsonUtility.ToJson(new WorldPickupGrantRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                    spawn_id = normalized,
                    idempotency_key = $"world-pickup-{normalized}",
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_grant_world_pickup",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_grant_world_pickup returned {response.statusCode}: {response.body}");
                    string rpcError = TryExtractError(response.body);
                    return WorldPickupGrantResult.Failed(
                        !string.IsNullOrWhiteSpace(rpcError)
                            ? rpcError
                            : $"world_pickup_grant_http_{response.statusCode}");
                }

                WorldPickupGrantResponse payload = JsonUtility.FromJson<WorldPickupGrantResponse>(response.body);
                if (payload == null || !payload.success || !payload.ok)
                {
                    return WorldPickupGrantResult.Failed(
                        payload != null && !string.IsNullOrWhiteSpace(payload.error)
                            ? payload.error
                            : "world_pickup_grant_invalid_payload");
                }

                InvalidateInventoryCache();
                PlayerProfileReadStore.ResolveOrCreate().MarkInventoryStale(profileId, wallet);

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[CoreBagWorldPickupGrantService] Granted '{payload.item_id}' x{payload.quantity} at '{normalized}' (already_claimed={payload.already_claimed}).",
                        this);
                }

                return WorldPickupGrantResult.Ok(
                    payload.item_id,
                    payload.quantity,
                    payload.remaining_quantity,
                    payload.already_claimed);
            }
            catch (Exception ex)
            {
                LogWarning($"GrantWorldPickupAsync failed: {ex.Message}");
                return WorldPickupGrantResult.Failed("world_pickup_grant_exception");
            }
        }

        private void EnsureConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
        }

        private void EnsureInventoryReader()
        {
            CoreBagItemInventoryService reader = GetComponent<CoreBagItemInventoryService>();
            if (reader == null)
                reader = gameObject.AddComponent<CoreBagItemInventoryService>();
            reader.Configure(supabaseConfig);
        }

        private static void InvalidateInventoryCache()
        {
            CoreBagItemInventoryService[] bags = FindObjectsOfType<CoreBagItemInventoryService>(true);
            for (int i = 0; i < bags.Length; i++)
            {
                if (bags[i] != null)
                    bags[i].ClearItemInventoryCache();
            }
        }

        private static Transform ResolveHost()
        {
            if (GameSessionContext.Instance != null)
                return GameSessionContext.Instance.transform;

            GameObject go = new GameObject("CoreBagWorldPickupGrantService");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            return go.transform;
        }

        private static string TryExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "";
            try
            {
                WorldPickupGrantResponse payload = JsonUtility.FromJson<WorldPickupGrantResponse>(body);
                return payload != null ? payload.error : "";
            }
            catch
            {
                return "";
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CoreBagWorldPickupGrantService] {message}", this);
        }

        [Serializable]
        private class WorldPickupGrantRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string spawn_id;
            public string idempotency_key;
        }

        [Serializable]
        private class WorldPickupGrantResponse
        {
            public bool success;
            public bool ok;
            public bool already_claimed;
            public string error;
            public string item_id;
            public int quantity;
            public int remaining_quantity;
        }
    }
}
