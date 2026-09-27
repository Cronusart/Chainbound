using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public static class SupabaseProgressionFlagService
    {
        [Serializable]
        private class SetFlagRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string flag_key;
            public bool flag_value;
        }

        [Serializable]
        private class SetFlagResponse
        {
            public bool success;
            public string error;
        }

        public static async Task<MenuMutationResult> SetFlagAsync(
            SupabaseConfigSO config,
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string flagKey,
            bool value)
        {
            if (config == null)
                return MenuMutationResult.Failed("SupabaseConfigSO was not assigned.");

            if (string.IsNullOrWhiteSpace(flagKey))
                return MenuMutationResult.Failed("flag_key was empty.");

            string json = JsonUtility.ToJson(new SetFlagRequest
            {
                player_profile_id = playerProfileId != null ? playerProfileId.Trim() : "",
                wallet_address = walletAddress != null ? walletAddress.Trim() : "",
                auth_user_id = authUserId != null ? authUserId.Trim() : "",
                flag_key = flagKey.Trim(),
                flag_value = value,
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "profile_set_progression_flag", json);
            if (!response.IsSuccess)
                return MenuMutationResult.Failed($"profile_set_progression_flag_http_{response.statusCode}: {response.body}");

            SetFlagResponse payload = JsonUtility.FromJson<SetFlagResponse>(response.body);
            if (payload == null || !payload.success)
                return MenuMutationResult.Failed(payload != null && !string.IsNullOrWhiteSpace(payload.error)
                    ? payload.error
                    : "profile_set_progression_flag_invalid_payload");

            return MenuMutationResult.Ok();
        }
    }
}
