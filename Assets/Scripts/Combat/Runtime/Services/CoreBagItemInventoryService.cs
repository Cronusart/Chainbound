using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production bag inventory adapter. Merges profile stacks + verified Core collectibles
    /// via Supabase (DAS stays server-side). Mirrors <see cref="CoreCosmeticInventoryService"/>.
    /// </summary>
    public class CoreBagItemInventoryService : MonoBehaviour, IInventoryService, IInventoryItemProvenanceService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private PlayerItemCatalogSO catalogOverride;
        [SerializeField] private int cacheTtlSeconds = 120;
        [SerializeField] private bool enableDebugLogs = true;

        private InventorySnapshot cachedSnapshot;
        private readonly Dictionary<string, string> assetIdsByItemId = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool TryGetCachedRamBalance(out long ramBalance)
        {
            if (cachedSnapshot == null)
            {
                ramBalance = 0;
                return false;
            }

            ramBalance = cachedSnapshot.ramBalance;
            return true;
        }
        private string cacheKey;
        private DateTime cacheExpiresAtUtc;
        private IWalletService boundWallet;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        private void OnEnable() => TryBindWalletEvents();
        private void OnDisable() => UnbindWalletEvents();

        public async Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress)
        {
            TryBindWalletEvents();

            string profileId = FirstNonEmpty(playerProfileId, GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "");
            string wallet = FirstNonEmpty(walletAddress, GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");
            string key = $"{profileId}|{wallet}";

            if (cachedSnapshot != null &&
                string.Equals(cacheKey, key, StringComparison.Ordinal) &&
                DateTime.UtcNow < cacheExpiresAtUtc)
            {
                return CloneSnapshot(cachedSnapshot);
            }

            ClearItemInventoryCache();
            cacheKey = key;

            if (supabaseConfig == null)
            {
                LogWarning("SupabaseConfigSO was not assigned.");
                return EmptySnapshot(wallet, "supabase_config_missing");
            }

            try
            {
                string json = JsonUtility.ToJson(new ItemInventoryRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_get_item_inventory",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_get_item_inventory returned {response.statusCode}: {response.body}");
                    string rpcError = TryExtractError(response.body);
                    return EmptySnapshot(
                        wallet,
                        !string.IsNullOrWhiteSpace(rpcError)
                            ? rpcError
                            : $"item_inventory_http_{response.statusCode}");
                }

                InventorySnapshot snapshot = BuildSnapshotFromJson(response.body, wallet, catalogOverride);
                if (!string.IsNullOrWhiteSpace(snapshot.lastError))
                {
                    LogWarning(snapshot.lastError);
                    return snapshot;
                }

                for (int i = 0; i < snapshot.stacks.Count; i++)
                {
                    MenuInventoryItemStack stack = snapshot.stacks[i];
                    if (stack == null || string.IsNullOrWhiteSpace(stack.coreAssetId))
                        continue;
                    if (!assetIdsByItemId.ContainsKey(stack.itemId))
                        assetIdsByItemId[stack.itemId] = stack.coreAssetId;
                }

                cachedSnapshot = snapshot;
                cacheExpiresAtUtc = DateTime.UtcNow.AddSeconds(Mathf.Max(15, cacheTtlSeconds));
                if (enableDebugLogs)
                    Debug.Log($"[CoreBagItemInventoryService] Loaded {snapshot.stacks.Count} bag stack(s).", this);

                return CloneSnapshot(snapshot);
            }
            catch (Exception ex)
            {
                LogWarning($"FetchInventoryAsync failed: {ex.Message}");
                ClearItemInventoryCache();
                return EmptySnapshot(wallet, "item_inventory_exception");
            }
        }

        public IReadOnlyDictionary<string, string> GetAssetIdsByItemId()
        {
            return new Dictionary<string, string>(assetIdsByItemId, StringComparer.Ordinal);
        }

        public bool TryGetAssetIdForItemId(string itemId, out string assetId)
        {
            assetId = null;
            string normalized = (itemId ?? "").Trim();
            return normalized.Length > 0 && assetIdsByItemId.TryGetValue(normalized, out assetId);
        }

        public void ClearItemInventoryCache()
        {
            cachedSnapshot = null;
            assetIdsByItemId.Clear();
            cacheKey = null;
            cacheExpiresAtUtc = DateTime.MinValue;
        }

        /// <summary>
        /// Parses <c>profile_get_item_inventory</c> JSON. Guest bags may omit
        /// <c>wallet_public_key</c>; profile stacks still populate.
        /// </summary>
        public static InventorySnapshot BuildSnapshotFromJson(
            string json,
            string walletFallback,
            PlayerItemCatalogSO catalogOverride = null)
        {
            if (string.IsNullOrWhiteSpace(json))
                return EmptySnapshot(walletFallback, "profile_get_item_inventory returned an invalid payload.");

            ItemInventoryResponse payload;
            try
            {
                payload = JsonUtility.FromJson<ItemInventoryResponse>(json);
            }
            catch (Exception)
            {
                return EmptySnapshot(walletFallback, "profile_get_item_inventory returned an invalid payload.");
            }

            if (payload == null || !payload.success)
            {
                string rpcError = payload != null && !string.IsNullOrWhiteSpace(payload.error)
                    ? payload.error
                    : "profile_get_item_inventory returned an invalid payload.";
                return EmptySnapshot(walletFallback, rpcError);
            }

            var snapshot = new InventorySnapshot
            {
                connectivity = MenuConnectivityState.Ready,
                ramBalance = payload.ram_balance,
                cluster = FirstNonEmpty(payload.cluster, "devnet"),
                walletPublicKey = FirstNonEmpty(payload.wallet_public_key, walletFallback),
                stacks = new List<MenuInventoryItemStack>(),
                consumableIds = new List<string>(),
                cosmeticAssetIds = new List<string>(),
                lastError = "",
            };

            ItemStackPayload[] rows = payload.stacks ?? Array.Empty<ItemStackPayload>();
            for (int i = 0; i < rows.Length; i++)
            {
                ItemStackPayload row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.item_id) || row.quantity <= 0)
                    continue;

                MenuInventoryItemStack stack = ResolveStack(row, catalogOverride);
                snapshot.stacks.Add(stack);
                snapshot.consumableIds.Add(stack.itemId);
            }

            return snapshot;
        }

        private static MenuInventoryItemStack ResolveStack(ItemStackPayload row, PlayerItemCatalogSO catalogOverride)
        {
            string itemId = row.item_id.Trim();
            PlayerItemDefinition def = null;
            if (catalogOverride != null)
                catalogOverride.TryGet(itemId, out def);
            if (def == null)
                PlayerItemCatalogSeed.TryGet(itemId, out def);

            var ownership = PlayerItemOwnershipKind.ProfileStack;
            if (!string.IsNullOrWhiteSpace(row.core_asset_id))
                ownership = PlayerItemOwnershipKind.CoreCollectible;
            else if (def != null)
                ownership = def.ownershipKind;

            return new MenuInventoryItemStack
            {
                itemId = itemId,
                quantity = row.quantity,
                coreAssetId = (row.core_asset_id ?? "").Trim(),
                ownershipKind = ownership,
                categoryKey = !string.IsNullOrWhiteSpace(row.category)
                    ? row.category.Trim()
                    : def != null
                        ? PlayerItemCategoryKeys.ToKey(def.category)
                        : PlayerItemCategoryKeys.Field,
                displayName = !string.IsNullOrWhiteSpace(row.display_name)
                    ? row.display_name.Trim()
                    : def != null
                        ? def.displayName
                        : itemId,
                effectFamilyKey = def != null
                    ? PlayerItemCategoryKeys.ToKey(def.effectFamily)
                    : "",
            };
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

        private void HandleWalletChanged(string _) => ClearItemInventoryCache();
        private void HandleWalletDisconnected() => ClearItemInventoryCache();

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CoreBagItemInventoryService] {message}", this);
        }

        public static InventorySnapshot EmptySnapshot(string wallet, string lastError = "")
        {
            bool hasError = !string.IsNullOrWhiteSpace(lastError);
            return new InventorySnapshot
            {
                connectivity = hasError
                    ? MenuConnectivityState.Error
                    : string.IsNullOrWhiteSpace(wallet) ? MenuConnectivityState.Offline : MenuConnectivityState.Ready,
                ramBalance = 0,
                stacks = new List<MenuInventoryItemStack>(),
                consumableIds = new List<string>(),
                cosmeticAssetIds = new List<string>(),
                walletPublicKey = wallet ?? "",
                lastError = lastError ?? "",
            };
        }

        private static InventorySnapshot CloneSnapshot(InventorySnapshot source)
        {
            if (source == null)
                return EmptySnapshot("");

            var clone = new InventorySnapshot
            {
                connectivity = source.connectivity,
                ramBalance = source.ramBalance,
                cluster = source.cluster,
                walletPublicKey = source.walletPublicKey,
                lastError = source.lastError ?? "",
                consumableIds = source.consumableIds != null
                    ? new List<string>(source.consumableIds)
                    : new List<string>(),
                cosmeticAssetIds = source.cosmeticAssetIds != null
                    ? new List<string>(source.cosmeticAssetIds)
                    : new List<string>(),
                stacks = new List<MenuInventoryItemStack>(),
            };

            if (source.stacks != null)
            {
                for (int i = 0; i < source.stacks.Count; i++)
                {
                    MenuInventoryItemStack s = source.stacks[i];
                    if (s == null)
                        continue;
                    clone.stacks.Add(new MenuInventoryItemStack
                    {
                        itemId = s.itemId,
                        quantity = s.quantity,
                        coreAssetId = s.coreAssetId,
                        ownershipKind = s.ownershipKind,
                        categoryKey = s.categoryKey,
                        displayName = s.displayName,
                        effectFamilyKey = s.effectFamilyKey,
                    });
                }
            }

            return clone;
        }

        private static string TryExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "";
            try
            {
                ItemInventoryResponse payload = JsonUtility.FromJson<ItemInventoryResponse>(body);
                return payload != null ? payload.error : "";
            }
            catch
            {
                return "";
            }
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : (fallback ?? "").Trim();
        }

        [Serializable]
        private class ItemInventoryRequest
        {
            public string player_profile_id;
            public string wallet_address;
        }

        [Serializable]
        private class ItemInventoryResponse
        {
            public bool success;
            public string error;
            public string player_profile_id;
            public string wallet_public_key;
            public string cluster;
            public long ram_balance;
            public ItemStackPayload[] stacks = Array.Empty<ItemStackPayload>();
        }

        [Serializable]
        private class ItemStackPayload
        {
            public string item_id;
            public int quantity;
            public string core_asset_id;
            public string category;
            public string display_name;
        }
    }
}
