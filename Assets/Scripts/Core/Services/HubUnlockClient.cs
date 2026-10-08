using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

namespace Chainbound.Hub
{
    public sealed class HubLinkResult
    {
        public bool Success;
        public string Ticket = string.Empty;
        public string Error = string.Empty;
    }

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

        public static async Task<HubLinkResult> LinkIdentityAsync(string ugsPlayerId)
        {
            if (string.IsNullOrWhiteSpace(ugsPlayerId))
                return Failed("identity_required");

            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return Failed("profile_required");

            try
            {
                string json = JsonUtility.ToJson(new HubLinkBody
                {
                    player_profile_id = session.PlayerProfileId,
                    ugs_player_id = ugsPlayerId.Trim(),
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_link_identity", json);
                if (response.statusCode == 409)
                {
                    Debug.LogWarning("[Hub] that trainer id is already linked.");
                    return Failed("identity_in_use");
                }

                HubLinkDto dto = string.IsNullOrWhiteSpace(response.body)
                    ? null
                    : JsonUtility.FromJson<HubLinkDto>(response.body);
                if (dto == null || !dto.success || string.IsNullOrWhiteSpace(dto.ticket))
                    return Failed(dto != null ? dto.error : "ticket_unavailable");

                return new HubLinkResult
                {
                    Success = true,
                    Ticket = dto.ticket,
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] identity link failed: " + ex.Message);
                return Failed("server_unreachable");
            }
        }

        public static async Task<string> VerifyTicketAsync(string ticket)
        {
            if (string.IsNullOrWhiteSpace(ticket))
                return null;

            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig();
            PlayerAccountSession session = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession
                : null;
            if (config == null || session == null || string.IsNullOrWhiteSpace(session.PlayerProfileId))
                return null;

            try
            {
                string json = JsonUtility.ToJson(new HubVerifyBody
                {
                    player_profile_id = session.PlayerProfileId,
                    ticket = ticket,
                });
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "hub_verify_ticket", json);
                HubVerifyDto dto = string.IsNullOrWhiteSpace(response.body)
                    ? null
                    : JsonUtility.FromJson<HubVerifyDto>(response.body);
                if (response.statusCode == 401 || dto == null || !dto.success || string.IsNullOrWhiteSpace(dto.ugs_player_id))
                    return null;

                return dto.ugs_player_id.Trim();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] ticket verify failed: " + ex.Message);
                return null;
            }
        }

        static HubLinkResult Failed(string error)
        {
            return new HubLinkResult { Error = error ?? string.Empty };
        }

        [Serializable]
        class HubProfileBody
        {
            public string player_profile_id;
        }

        [Serializable]
        class HubLinkBody
        {
            public string player_profile_id;
            public string ugs_player_id;
        }

        [Serializable]
        class HubVerifyBody
        {
            public string player_profile_id;
            public string ticket;
        }

        [Serializable]
        class HubResultDto
        {
            public bool success;
            public string error;
        }

        [Serializable]
        class HubLinkDto
        {
            public bool success;
            public string error;
            public string ticket;
        }

        [Serializable]
        class HubVerifyDto
        {
            public bool success;
            public string error;
            public string ugs_player_id;
        }
    }
}
