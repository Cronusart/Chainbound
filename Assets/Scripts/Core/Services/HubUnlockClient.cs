using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

namespace Chainbound.Hub
{
    public static class HubUnlockClient
    {
        public static async Task<bool> UnlockAsync()
        {
            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return false;

            try
            {
                string json = JsonUtility.ToJson(new HubProfileBody
                {
                    player_profile_id = session.PlayerProfileId,
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_unlock", json);
                HubResultDto dto = string.IsNullOrWhiteSpace(response.body)
                    ? null
                    : JsonUtility.FromJson<HubResultDto>(response.body);
                return dto != null && dto.success;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] unlock failed: " + ex.Message);
                return false;
            }
        }

        public static async Task<bool> LinkIdentityAsync(string ugsPlayerId)
        {
            if (string.IsNullOrWhiteSpace(ugsPlayerId))
                return false;

            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return false;

            try
            {
                string json = JsonUtility.ToJson(new HubLinkBody
                {
                    player_profile_id = session.PlayerProfileId,
                    ugs_player_id = ugsPlayerId.Trim(),
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_link_identity", json);
                HubResultDto dto = string.IsNullOrWhiteSpace(response.body)
                    ? null
                    : JsonUtility.FromJson<HubResultDto>(response.body);
                return dto != null && dto.success;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] identity link failed: " + ex.Message);
                return false;
            }
        }

        [Serializable]
        private class HubProfileBody
        {
            public string player_profile_id;
        }

        [Serializable]
        private class HubLinkBody
        {
            public string player_profile_id;
            public string ugs_player_id;
        }

        [Serializable]
        private class HubResultDto
        {
            public bool success;
            public string error;
        }
    }
}
