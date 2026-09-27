using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Chainbound.Combat
{
    public static class SupabaseSanctuaryLatchService
    {
        [Serializable]
        private class SetLatchResponse
        {
            public bool success;
            public string error;
        }

        public static async Task<MenuMutationResult> SetLatchAsync(
            SupabaseConfigSO config,
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string sanctuaryJson)
        {
            if (config == null)
                return MenuMutationResult.Failed("SupabaseConfigSO was not assigned.");

            JObject sanctuary;
            try
            {
                sanctuary = string.IsNullOrWhiteSpace(sanctuaryJson)
                    ? new JObject()
                    : JObject.Parse(sanctuaryJson);
            }
            catch (Exception ex)
            {
                return MenuMutationResult.Failed($"invalid_sanctuary_payload: {ex.Message}");
            }

            var body = new JObject
            {
                ["player_profile_id"] = playerProfileId != null ? playerProfileId.Trim() : "",
                ["wallet_address"] = walletAddress != null ? walletAddress.Trim() : "",
                ["auth_user_id"] = authUserId != null ? authUserId.Trim() : "",
                ["sanctuary"] = sanctuary
            };

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                config,
                "profile_set_sanctuary_latch",
                body.ToString(Newtonsoft.Json.Formatting.None));
            if (!response.IsSuccess)
                return MenuMutationResult.Failed($"profile_set_sanctuary_latch_http_{response.statusCode}: {response.body}");

            SetLatchResponse payload = JsonUtility.FromJson<SetLatchResponse>(response.body);
            if (payload == null || !payload.success)
            {
                return MenuMutationResult.Failed(payload != null && !string.IsNullOrWhiteSpace(payload.error)
                    ? payload.error
                    : "profile_set_sanctuary_latch_invalid_payload");
            }

            return MenuMutationResult.Ok();
        }
    }
}
