using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

namespace Chainbound.Hub
{
    public sealed class HubPresenceCard
    {
        public bool Success;
        public string UgsPlayerId;
        public string DisplayName;
        public string CosmeticIdsJson;
    }

    public static class HubPresenceClient
    {
        public static async Task<HubPresenceCard> FetchAsync(string ugsPlayerId)
        {
            var empty = new HubPresenceCard
            {
                UgsPlayerId = ugsPlayerId ?? string.Empty,
                DisplayName = "Trainer",
                CosmeticIdsJson = "[]",
            };
            if (string.IsNullOrWhiteSpace(ugsPlayerId))
                return empty;

            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return empty;

            try
            {
                string json = JsonUtility.ToJson(new HubCardBody
                {
                    player_profile_id = session.PlayerProfileId,
                    ugs_player_id = ugsPlayerId.Trim(),
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_presence_card", json);
                HubCardDto dto = string.IsNullOrWhiteSpace(response.body)
                    ? null
                    : JsonUtility.FromJson<HubCardDto>(response.body);
                if (dto == null || !dto.success)
                    return empty;

                return new HubPresenceCard
                {
                    Success = true,
                    UgsPlayerId = ugsPlayerId.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(dto.display_name) ? "Trainer" : dto.display_name.Trim(),
                    CosmeticIdsJson = string.IsNullOrWhiteSpace(dto.cosmetic_ids_json) ? "[]" : dto.cosmetic_ids_json,
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] presence card failed: " + ex.Message);
                return empty;
            }
        }

        [Serializable]
        private class HubCardBody
        {
            public string player_profile_id;
            public string ugs_player_id;
        }

        [Serializable]
        private class HubCardDto
        {
            public bool success;
            public string display_name;
            public string cosmetic_ids_json;
            public string error;
        }
    }
}
