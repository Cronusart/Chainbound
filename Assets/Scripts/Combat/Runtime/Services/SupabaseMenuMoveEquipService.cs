using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseMenuMoveEquipService : MonoBehaviour, IMenuMoveEquipService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<MenuMutationResult> EquipMoveAsync(string playerProfileId, string walletAddress, string chainlingId, int slot, string moveItemId)
        {
            if (supabaseConfig == null)
                return MenuMutationResult.Failed("SupabaseConfigSO was not assigned.");

            if (string.IsNullOrWhiteSpace(chainlingId))
                return MenuMutationResult.Failed("chainling_id_required");

            var session = GameSessionContext.Instance;
            string profileId = !string.IsNullOrWhiteSpace(playerProfileId)
                ? playerProfileId.Trim()
                : session != null ? session.PlayerProfileId : "";
            string wallet = !string.IsNullOrWhiteSpace(walletAddress)
                ? walletAddress.Trim()
                : session != null ? session.WalletAddress : "";
            string authUserId = session != null ? session.AuthUserId : "";

            string json = JsonUtility.ToJson(new ProfileSetLoadoutRequest
            {
                player_profile_id = profileId,
                wallet_address = wallet,
                auth_user_id = authUserId,
                chainling_id = chainlingId.Trim(),
                slot = slot,
                move_item_id = moveItemId != null ? moveItemId.Trim() : "",
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, "profile_set_loadout", json);
            if (!response.IsSuccess)
                return MenuMutationResult.Failed($"profile_set_loadout_http_{response.statusCode}: {response.body}");

            var envelope = JsonUtility.FromJson<ProfileSetLoadoutEnvelope>(response.body);
            if (envelope == null || !envelope.success)
            {
                string error = envelope != null && !string.IsNullOrEmpty(envelope.error)
                    ? envelope.error
                    : "profile_set_loadout_invalid_payload";
                return MenuMutationResult.Failed(error);
            }

            return MenuMutationResult.Ok();
        }

        [Serializable]
        private class ProfileSetLoadoutRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string chainling_id;
            public int slot;
            public string move_item_id;
        }

        [Serializable]
        private class ProfileSetLoadoutEnvelope
        {
            public bool success;
            public string error;
        }
    }
}
