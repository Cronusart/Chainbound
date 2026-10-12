using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class ProfileItemDeltaLine
    {
        public string itemId;
        public int quantity = 1;
    }

    [Serializable]
    public class ProfileItemDeltaResult
    {
        public bool ok;
        public bool alreadyApplied;
        public string error;

        public static ProfileItemDeltaResult Ok(bool alreadyApplied)
        {
            return new ProfileItemDeltaResult
            {
                ok = true,
                alreadyApplied = alreadyApplied,
                error = "",
            };
        }

        public static ProfileItemDeltaResult Failed(string message)
        {
            return new ProfileItemDeltaResult
            {
                ok = false,
                alreadyApplied = false,
                error = message ?? "",
            };
        }
    }

    /// <summary>
    /// Posts an idempotent bag grant/consume to
    /// <c>profile_apply_profile_item_delta</c>. The server owns stack math.
    /// </summary>
    public sealed class CoreBagProfileItemDeltaService : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public static CoreBagProfileItemDeltaService EnsureInstance()
        {
            CoreBagProfileItemDeltaService existing =
                FindFirstObjectByType<CoreBagProfileItemDeltaService>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            Transform host = ResolveHost();
            CoreBagProfileItemDeltaService created = host.gameObject.AddComponent<CoreBagProfileItemDeltaService>();
            created.EnsureConfig();
            return created;
        }

        public async Task<ProfileItemDeltaResult> ApplyAsync(
            string idempotencyKey,
            ProfileItemDeltaLine[] grants,
            ProfileItemDeltaLine[] consumes)
        {
            EnsureConfig();
            if (supabaseConfig == null)
                return ProfileItemDeltaResult.Failed("supabase_config_missing");

            string key = (idempotencyKey ?? "").Trim();
            if (key.Length == 0)
                return ProfileItemDeltaResult.Failed("idempotency_key_required");

            string profileId = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.PlayerProfileId ?? "").Trim()
                : "";
            string wallet = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.WalletAddress ?? "").Trim()
                : "";

            try
            {
                string json = JsonUtility.ToJson(new ProfileItemDeltaRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                    idempotency_key = key,
                    grants = ToWire(grants),
                    consumes = ToWire(consumes),
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_apply_profile_item_delta",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_apply_profile_item_delta returned {response.statusCode}: {response.body}");
                    string rpcError = TryExtractError(response.body);
                    return ProfileItemDeltaResult.Failed(
                        !string.IsNullOrWhiteSpace(rpcError)
                            ? rpcError
                            : $"profile_item_delta_http_{response.statusCode}");
                }

                ProfileItemDeltaResponse payload = JsonUtility.FromJson<ProfileItemDeltaResponse>(response.body);
                if (payload == null || !payload.success || !payload.ok)
                {
                    return ProfileItemDeltaResult.Failed(
                        payload != null && !string.IsNullOrWhiteSpace(payload.error)
                            ? payload.error
                            : "profile_item_delta_invalid_payload");
                }

                InvalidateInventoryCache();
                PlayerProfileReadStore.ResolveOrCreate().MarkInventoryStale(profileId, wallet);
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[CoreBagProfileItemDeltaService] Applied '{key}' (already_applied={payload.already_applied}).",
                        this);
                }

                return ProfileItemDeltaResult.Ok(payload.already_applied);
            }
            catch (Exception ex)
            {
                LogWarning($"ApplyAsync failed: {ex.Message}");
                return ProfileItemDeltaResult.Failed("profile_item_delta_exception");
            }
        }

        private static ProfileItemDeltaWireLine[] ToWire(ProfileItemDeltaLine[] lines)
        {
            if (lines == null || lines.Length == 0)
                return Array.Empty<ProfileItemDeltaWireLine>();

            var wire = new ProfileItemDeltaWireLine[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                ProfileItemDeltaLine line = lines[i];
                wire[i] = new ProfileItemDeltaWireLine
                {
                    item_id = line != null ? line.itemId ?? "" : "",
                    quantity = line != null ? line.quantity : 0,
                };
            }

            return wire;
        }

        private void EnsureConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
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

            GameObject go = new GameObject("CoreBagProfileItemDeltaService");
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
                ProfileItemDeltaResponse payload = JsonUtility.FromJson<ProfileItemDeltaResponse>(body);
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
                Debug.LogWarning($"[CoreBagProfileItemDeltaService] {message}", this);
        }

        [Serializable]
        private class ProfileItemDeltaRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string idempotency_key;
            public ProfileItemDeltaWireLine[] grants;
            public ProfileItemDeltaWireLine[] consumes;
        }

        [Serializable]
        private class ProfileItemDeltaWireLine
        {
            public string item_id;
            public int quantity;
        }

        [Serializable]
        private class ProfileItemDeltaResponse
        {
            public bool success;
            public bool ok;
            public bool already_applied;
            public string error;
        }
    }
}
