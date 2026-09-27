using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production cosmetic inventory adapter. It calls Supabase instead of DAS directly;
    /// Supabase is responsible for Core collection, authority, owner, and catalog checks.
    /// </summary>
    public class CoreCosmeticInventoryService : MonoBehaviour, ICosmeticInventoryService, ICosmeticInventoryProvenanceService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private int cacheTtlSeconds = 120;
        [SerializeField] private bool enableDebugLogs = true;

        private readonly List<string> cachedCosmeticIds = new List<string>();
        private readonly Dictionary<string, string> assetIdsByCosmeticId = new Dictionary<string, string>(StringComparer.Ordinal);

        private string cacheKey;
        private string lastKnownCluster = "devnet";
        private DateTime cacheExpiresAtUtc;
        private IWalletService boundWallet;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        private void OnEnable()
        {
            TryBindWalletEvents();
        }

        private void OnDisable()
        {
            UnbindWalletEvents();
        }

        public async Task<IReadOnlyList<string>> FetchOwnedCosmeticIdsAsync(string playerProfileId, string walletAddress)
        {
            TryBindWalletEvents();

            string profileId = FirstNonEmpty(playerProfileId, GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "");
            string wallet = FirstNonEmpty(walletAddress, GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");
            string key = BuildCacheKey(profileId, wallet, lastKnownCluster);

            if (cachedCosmeticIds.Count > 0 &&
                string.Equals(cacheKey, key, StringComparison.Ordinal) &&
                DateTime.UtcNow < cacheExpiresAtUtc)
            {
                return cachedCosmeticIds.AsReadOnly();
            }

            cachedCosmeticIds.Clear();
            assetIdsByCosmeticId.Clear();
            cacheKey = key;

            if (supabaseConfig == null)
            {
                LogWarning("SupabaseConfigSO was not assigned.");
                return cachedCosmeticIds.AsReadOnly();
            }

            if (string.IsNullOrWhiteSpace(profileId) && string.IsNullOrWhiteSpace(wallet))
            {
                LogWarning("Player profile id or wallet address was empty.");
                return cachedCosmeticIds.AsReadOnly();
            }

            try
            {
                string json = JsonUtility.ToJson(new CosmeticInventoryRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_get_cosmetic_inventory",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_get_cosmetic_inventory returned {response.statusCode}: {response.body}");
                    return cachedCosmeticIds.AsReadOnly();
                }

                CosmeticInventoryResponse payload = JsonUtility.FromJson<CosmeticInventoryResponse>(response.body);
                if (payload == null || !payload.success)
                {
                    LogWarning(payload != null ? payload.error : "profile_get_cosmetic_inventory returned an invalid payload.");
                    return cachedCosmeticIds.AsReadOnly();
                }

                lastKnownCluster = FirstNonEmpty(payload.cluster, lastKnownCluster);
                cacheKey = BuildCacheKey(profileId, wallet, lastKnownCluster);

                List<CosmeticRecordPayload> records = payload.cosmetics ?? new List<CosmeticRecordPayload>();
                for (int i = 0; i < records.Count; i++)
                {
                    CosmeticRecordPayload record = records[i];
                    if (record == null || string.IsNullOrWhiteSpace(record.cosmetic_id) || string.IsNullOrWhiteSpace(record.asset_id))
                        continue;

                    string cosmeticId = record.cosmetic_id.Trim();
                    if (assetIdsByCosmeticId.ContainsKey(cosmeticId))
                        continue;

                    cachedCosmeticIds.Add(cosmeticId);
                    assetIdsByCosmeticId[cosmeticId] = record.asset_id.Trim();
                }

                cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(Mathf.Max(15, cacheTtlSeconds));

                if (enableDebugLogs)
                    Debug.Log($"[CoreCosmeticInventoryService] Loaded {cachedCosmeticIds.Count} verified Core cosmetic(s).", this);

                return cachedCosmeticIds.AsReadOnly();
            }
            catch (Exception ex)
            {
                LogWarning($"FetchOwnedCosmeticIdsAsync failed: {ex.Message}");
                cachedCosmeticIds.Clear();
                assetIdsByCosmeticId.Clear();
                return cachedCosmeticIds.AsReadOnly();
            }
        }

        public IReadOnlyDictionary<string, string> GetAssetIdsByCosmeticId()
        {
            return new Dictionary<string, string>(assetIdsByCosmeticId, StringComparer.Ordinal);
        }

        public bool TryGetAssetIdForCosmeticId(string cosmeticId, out string assetId)
        {
            assetId = null;
            string normalized = string.IsNullOrWhiteSpace(cosmeticId) ? "" : cosmeticId.Trim();
            return normalized.Length > 0 && assetIdsByCosmeticId.TryGetValue(normalized, out assetId);
        }

        public void ClearCosmeticCache()
        {
            cachedCosmeticIds.Clear();
            assetIdsByCosmeticId.Clear();
            cacheKey = null;
            lastKnownCluster = "devnet";
            cacheExpiresAtUtc = DateTime.MinValue;
        }

        private void TryBindWalletEvents()
        {
            IWalletService wallet = SolanaServiceBridge.Instance != null ? SolanaServiceBridge.Instance.WalletService : null;
            if (wallet == null || ReferenceEquals(wallet, boundWallet))
                return;

            UnbindWalletEvents();
            boundWallet = wallet;
            boundWallet.OnConnected += HandleWalletChanged;
            boundWallet.OnAccountChanged += HandleWalletChanged;
            boundWallet.OnDisconnected += HandleWalletDisconnected;
        }

        private void UnbindWalletEvents()
        {
            if (boundWallet == null)
                return;

            boundWallet.OnConnected -= HandleWalletChanged;
            boundWallet.OnAccountChanged -= HandleWalletChanged;
            boundWallet.OnDisconnected -= HandleWalletDisconnected;
            boundWallet = null;
        }

        private void HandleWalletChanged(string _)
        {
            ClearCosmeticCache();
        }

        private void HandleWalletDisconnected()
        {
            ClearCosmeticCache();
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CoreCosmeticInventoryService] {message}", this);
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : (fallback ?? "").Trim();
        }

        private static string BuildCacheKey(string profileId, string wallet, string cluster)
        {
            return $"{profileId}|{wallet}|{FirstNonEmpty(cluster, "devnet")}";
        }

        [Serializable]
        private class CosmeticInventoryRequest
        {
            public string player_profile_id;
            public string wallet_address;
        }

        [Serializable]
        private class CosmeticInventoryResponse
        {
            public bool success;
            public string error;
            public string player_profile_id;
            public string wallet_public_key;
            public string cluster;
            public List<CosmeticRecordPayload> cosmetics = new List<CosmeticRecordPayload>();
        }

        [Serializable]
        private class CosmeticRecordPayload
        {
            public string asset_id;
            public string cosmetic_id;
            public string slot;
            public string collection;
            public string update_authority;
            public int schema_version;
            public string verified_at;
        }
    }
}
