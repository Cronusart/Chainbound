using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production item use adapter. Field / Favor items go through
    /// <c>profile_use_field_item</c>; Core collectibles use prepare/finalize consume.
    /// </summary>
    public class SupabasePlayerItemUseService : MonoBehaviour, IPlayerItemUseService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MonoBehaviour favorServiceBehaviour;
        [SerializeField] private MonoBehaviour coreConsumeServiceBehaviour;
        [SerializeField] private MonoBehaviour inventoryServiceBehaviour;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private bool enableDebugLogs = true;

        public void Configure(
            SupabaseConfigSO config,
            IFavorService favor = null,
            ICoreAssetConsumeService consume = null,
            IInventoryService inventory = null,
            SpeciesDatabaseSO speciesDb = null)
        {
            supabaseConfig = config;
            favorServiceBehaviour = favor as MonoBehaviour;
            coreConsumeServiceBehaviour = consume as MonoBehaviour;
            inventoryServiceBehaviour = inventory as MonoBehaviour;
            if (speciesDb != null)
                speciesDatabase = speciesDb;
        }

        public async Task<PlayerItemUseResult> UseItemAsync(
            string playerProfileId,
            string walletAddress,
            PlayerItemUseRequest request)
        {
            if (supabaseConfig == null)
                return PlayerItemUseResult.Failed("supabase_config_missing");
            if (request == null || string.IsNullOrWhiteSpace(request.itemId))
                return PlayerItemUseResult.Failed("item_id_required");

            if (!PlayerItemCatalogSeed.TryGet(request.itemId, out PlayerItemDefinition def) || def == null)
                return PlayerItemUseResult.Failed($"unknown_item:{request.itemId}");

            string profileId = FirstNonEmpty(
                playerProfileId,
                GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "");
            string wallet = FirstNonEmpty(
                walletAddress,
                GameSessionContext.Instance != null ? GameSessionContext.Instance.WalletAddress : "");

            if (def.ownershipKind == PlayerItemOwnershipKind.CoreCollectible)
                return await UseCoreCollectibleAsync(profileId, wallet, request, def);

            // Stride Tonic is client-side. profile_use_field_item only accepts catalog
            // rows that already exist in Supabase, and this change does not add one.
            if (def.effectFamily == PlayerItemEffectFamily.MovementSpeedBoost)
                return PlayerItemUseResult.Failed("movement_speed_boost_not_server_backed");

            if (!IsServerBackedFieldOrFavor(def.effectFamily))
                return PlayerItemUseResult.Failed("item_use_not_supported_yet");

            if (FieldItemHealUtility.NeedsPartyTarget(def.effectFamily) &&
                string.IsNullOrWhiteSpace(request.targetChainlingId))
            {
                return PlayerItemUseResult.Failed("target_required");
            }

            try
            {
                string json = JsonUtility.ToJson(new FieldItemUseRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                    item_id = def.itemId,
                    target_chainling_id = request.targetChainlingId ?? "",
                    idempotency_key = $"field-item-{Guid.NewGuid():N}",
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_use_field_item",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_use_field_item returned {response.statusCode}: {response.body}");
                    string rpcError = TryExtractError(response.body);
                    return PlayerItemUseResult.Failed(
                        !string.IsNullOrWhiteSpace(rpcError)
                            ? rpcError
                            : $"field_item_use_http_{response.statusCode}");
                }

                FieldItemUseResponse payload = JsonUtility.FromJson<FieldItemUseResponse>(response.body);
                if (payload == null || !payload.success)
                {
                    return PlayerItemUseResult.Failed(
                        payload != null && !string.IsNullOrWhiteSpace(payload.error)
                            ? payload.error
                            : "field_item_use_invalid_payload");
                }

                InventorySnapshot inventory = BuildInventoryFromPayload(payload, wallet);
                List<FieldItemHpPatch> hpPatches = BuildHpPatches(payload.hp_patches);
                ApplyHpPatchesToCachedRosters(hpPatches);
                int favorScore = ApplyFavorFromPayload(payload);

                InvalidateInventoryCache();

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[SupabasePlayerItemUseService] Used {def.itemId} family={payload.effect_family} patches={hpPatches.Count}",
                        this);
                }

                return PlayerItemUseResult.Ok(inventory, favorScore, hpPatches: hpPatches);
            }
            catch (Exception ex)
            {
                LogWarning($"UseItemAsync failed: {ex.Message}");
                return PlayerItemUseResult.Failed("field_item_use_exception");
            }
        }

        private Task<PlayerItemUseResult> UseCoreCollectibleAsync(
            string profileId,
            string wallet,
            PlayerItemUseRequest request,
            PlayerItemDefinition def)
        {
            // Pass 2 Bindery/Archive rites apply game effects after consume.
            return Task.FromResult(PlayerItemUseResult.Failed("core_item_rite_not_supported_yet"));
        }

        private static bool IsServerBackedFieldOrFavor(PlayerItemEffectFamily family)
        {
            switch (family)
            {
                case PlayerItemEffectFamily.HealSingle:
                case PlayerItemEffectFamily.HealParty:
                case PlayerItemEffectFamily.Revive:
                case PlayerItemEffectFamily.StatusClear:
                case PlayerItemEffectFamily.FavorBoost:
                    return true;
                default:
                    return false;
            }
        }

        private int ApplyFavorFromPayload(FieldItemUseResponse payload)
        {
            if (payload == null || payload.favor_points <= 0 || payload.favor_duration_battles <= 0)
                return FavorRuntime.CurrentScore;

            IFavorService favor = favorServiceBehaviour as IFavorService
                ?? GetComponent<FavorService>()
                ?? FavorRuntime.Active;
            if (favor == null)
            {
                FavorService created = gameObject.AddComponent<FavorService>();
                favorServiceBehaviour = created;
                favor = created;
            }

            favor.SetAmpouleFavor(payload.favor_points, payload.favor_duration_battles);
            return favor.CurrentFavorScore;
        }

        private void ApplyHpPatchesToCachedRosters(List<FieldItemHpPatch> patches)
        {
            if (patches == null || patches.Count == 0)
                return;

            SupabasePlayerRosterProvider[] rosterProviders = FindObjectsOfType<SupabasePlayerRosterProvider>(true);
            for (int p = 0; p < rosterProviders.Length; p++)
            {
                PlayerRosterSnapshot roster = rosterProviders[p] != null ? rosterProviders[p].CachedRoster : null;
                if (roster == null)
                    continue;

                for (int i = 0; i < patches.Count; i++)
                {
                    FieldItemHpPatch patch = patches[i];
                    if (patch == null || string.IsNullOrWhiteSpace(patch.chainlingId))
                        continue;
                    ChainlingRosterHpMutation.PatchEntryHp(roster, patch.chainlingId, patch.currentHp);
                }
            }
        }

        private void InvalidateInventoryCache()
        {
            if (inventoryServiceBehaviour is CoreBagItemInventoryService core)
                core.ClearItemInventoryCache();
        }

        private InventorySnapshot BuildInventoryFromPayload(FieldItemUseResponse payload, string wallet)
        {
            var snapshot = new InventorySnapshot
            {
                connectivity = MenuConnectivityState.Ready,
                ramBalance = payload.ram_balance,
                cluster = FirstNonEmpty(payload.cluster, "devnet"),
                walletPublicKey = FirstNonEmpty(payload.wallet_public_key, wallet),
                stacks = new List<MenuInventoryItemStack>(),
                consumableIds = new List<string>(),
                cosmeticAssetIds = new List<string>(),
            };

            ItemStackPayload[] rows = payload.stacks;
            if (rows == null)
                return snapshot;

            for (int i = 0; i < rows.Length; i++)
            {
                ItemStackPayload row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.item_id) || row.quantity <= 0)
                    continue;

                MenuInventoryItemStack stack = ResolveStack(row);
                snapshot.stacks.Add(stack);
                snapshot.consumableIds.Add(stack.itemId);
            }

            return snapshot;
        }

        private static MenuInventoryItemStack ResolveStack(ItemStackPayload row)
        {
            string itemId = row.item_id.Trim();
            PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition def);

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

        private static List<FieldItemHpPatch> BuildHpPatches(HpPatchPayload[] rows)
        {
            var patches = new List<FieldItemHpPatch>();
            if (rows == null)
                return patches;

            for (int i = 0; i < rows.Length; i++)
            {
                HpPatchPayload row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.chainling_id))
                    continue;

                patches.Add(new FieldItemHpPatch
                {
                    chainlingId = row.chainling_id.Trim(),
                    currentHp = Mathf.Max(0, row.current_hp),
                    healedAmount = Mathf.Max(0, row.healed_amount),
                });
            }

            return patches;
        }

        private static string TryExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "";
            try
            {
                FieldItemUseResponse payload = JsonUtility.FromJson<FieldItemUseResponse>(body);
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
                Debug.LogWarning($"[SupabasePlayerItemUseService] {message}", this);
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : (fallback ?? "").Trim();
        }

        [Serializable]
        private class FieldItemUseRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string item_id;
            public string target_chainling_id;
            public string idempotency_key;
        }

        [Serializable]
        private class FieldItemUseResponse
        {
            public bool success;
            public string error;
            public string player_profile_id;
            public string item_id;
            public string effect_family;
            public HpPatchPayload[] hp_patches;
            public int favor_points;
            public int favor_duration_battles;
            public int remaining_quantity;
            public long ram_balance;
            public ItemStackPayload[] stacks;
            public string wallet_public_key;
            public string cluster;
        }

        [Serializable]
        private class HpPatchPayload
        {
            public string chainling_id;
            public int current_hp;
            public int healed_amount;
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
