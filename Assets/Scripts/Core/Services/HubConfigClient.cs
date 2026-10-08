using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

namespace Chainbound.Hub
{
    public sealed class HubConfig
    {
        public bool Success;
        public bool MultiplayerUnlocked;
        public bool HubUnlocked;
        public bool QueueOpen;
        public string Error;
    }

    public static class HubConfigClient
    {
        public static HubConfig Current { get; private set; }

        public static async Task<HubConfig> RefreshAsync()
        {
            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
            {
                Current = new HubConfig { Error = "profile_required" };
                return Current;
            }

            try
            {
                string json = JsonUtility.ToJson(new HubProfileBody
                {
                    player_profile_id = session.PlayerProfileId,
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_config", json);
                if (string.IsNullOrWhiteSpace(response.body))
                {
                    Current = new HubConfig { Error = "server_unreachable" };
                    return Current;
                }

                HubConfigDto dto = JsonUtility.FromJson<HubConfigDto>(response.body);
                Current = dto == null
                    ? new HubConfig { Error = "server_unreachable" }
                    : new HubConfig
                    {
                        Success = dto.success,
                        MultiplayerUnlocked = dto.multiplayer_unlocked,
                        HubUnlocked = dto.hub_unlocked,
                        QueueOpen = dto.queue_open,
                        Error = dto.error,
                    };
                return Current;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] config failed: " + ex.Message);
                Current = new HubConfig { Error = "server_unreachable" };
                return Current;
            }
        }

        [Serializable]
        private class HubProfileBody
        {
            public string player_profile_id;
        }

        [Serializable]
        private class HubConfigDto
        {
            public bool success;
            public bool multiplayer_unlocked;
            public bool hub_unlocked;
            public bool queue_open;
            public string error;
        }
    }
}
