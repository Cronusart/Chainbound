using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseMoveInventoryProvider : MonoBehaviour, IMoveInventoryProvider
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public MoveInventorySnapshot CachedInventory { get; private set; }
        public string LastError { get; private set; }

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<MoveInventorySnapshot> FetchMoveInventoryAsync(string playerProfileId, string walletAddress)
        {
            LastError = "";
            CachedInventory = null;

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

            try
            {
                string json = JsonUtility.ToJson(new MoveInventoryRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, "profile_get_move_inventory", json);
                if (!response.IsSuccess)
                    return Fail($"profile_get_move_inventory returned {response.statusCode}: {response.body}");

                MoveInventoryResponse payload = JsonUtility.FromJson<MoveInventoryResponse>(response.body);
                if (payload == null)
                    return Fail("profile_get_move_inventory returned an invalid payload.");

                var snapshot = new MoveInventorySnapshot
                {
                    playerProfileId = payload.player_profile_id,
                };

                Dictionary<string, MenuMoveLoadoutSlot> equippedByMoveItemId = new Dictionary<string, MenuMoveLoadoutSlot>(StringComparer.Ordinal);
                List<LoadoutPayload> loadouts = payload.loadouts ?? new List<LoadoutPayload>();
                for (int i = 0; i < loadouts.Count; i++)
                {
                    LoadoutPayload loadout = loadouts[i];
                    if (loadout == null || string.IsNullOrWhiteSpace(loadout.move_item_id))
                        continue;

                    var slot = new MenuMoveLoadoutSlot
                    {
                        chainlingId = loadout.chainling_id,
                        slot = loadout.slot,
                        moveItemId = loadout.move_item_id,
                    };
                    snapshot.loadouts.Add(slot);
                    equippedByMoveItemId[slot.moveItemId] = slot;
                }

                List<MoveItemPayload> moves = payload.moves ?? new List<MoveItemPayload>();
                for (int i = 0; i < moves.Count; i++)
                {
                    MoveItemPayload payloadMove = moves[i];
                    if (payloadMove == null || string.IsNullOrWhiteSpace(payloadMove.move_id))
                        continue;

                    string moveItemId = payloadMove.move_item_id;
                    equippedByMoveItemId.TryGetValue(moveItemId ?? "", out MenuMoveLoadoutSlot equippedSlot);

                    snapshot.moves.Add(new MenuMoveItem
                    {
                        moveItemId = moveItemId,
                        moveId = payloadMove.move_id,
                        tier = MoveTierMigrationAdapter.FromStoredValue(
                            payloadMove.tier, payloadMove.modifier1_id, payloadMove.modifier2_id),
                        modifier1SectionId = payloadMove.modifier1_id,
                        modifier1EffectValue = payloadMove.modifier1_effect_value,
                        modifier1EffectValueLocked = payloadMove.modifier1_effect_value_locked,
                        modifier2SectionId = payloadMove.modifier2_id,
                        modifier2EffectValue = payloadMove.modifier2_effect_value,
                        modifier2EffectValueLocked = payloadMove.modifier2_effect_value_locked,
                        locked = payloadMove.locked,
                        exportedAssetId = payloadMove.exported_asset_id,
                        equippedChainlingId = equippedSlot != null ? equippedSlot.chainlingId : "",
                        equippedSlot = equippedSlot != null ? equippedSlot.slot : -1,
                    });
                }

                CachedInventory = snapshot;
                if (enableDebugLogs)
                    Debug.Log($"[SupabaseMoveInventoryProvider] Loaded {snapshot.moves.Count} move item(s).", this);

                return CachedInventory;
            }
            catch (Exception ex)
            {
                return Fail($"FetchMoveInventoryAsync exception: {ex.Message}");
            }
        }

        private MoveInventorySnapshot Fail(string reason)
        {
            LastError = reason;
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabaseMoveInventoryProvider] {reason}", this);
            return null;
        }

        [Serializable]
        private class MoveInventoryRequest
        {
            public string player_profile_id;
            public string wallet_address;
        }

        [Serializable]
        private class MoveInventoryResponse
        {
            public string player_profile_id;
            public List<MoveItemPayload> moves = new List<MoveItemPayload>();
            public List<LoadoutPayload> loadouts = new List<LoadoutPayload>();
        }

        [Serializable]
        private class MoveItemPayload
        {
            public string move_item_id;
            public string tier;
            public string move_id;
            public string modifier1_id;
            public float modifier1_effect_value;
            public bool modifier1_effect_value_locked;
            public string modifier2_id;
            public float modifier2_effect_value;
            public bool modifier2_effect_value_locked;
            public bool locked;
            public string exported_asset_id;
        }

        [Serializable]
        private class LoadoutPayload
        {
            public string chainling_id;
            public int slot;
            public string move_item_id;
        }
    }
}
