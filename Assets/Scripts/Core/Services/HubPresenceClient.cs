using System;
using System.Collections.Generic;
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
        const float CacheSeconds = 30f;
        const float MinGapSeconds = 0.25f;

        static readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        static readonly Dictionary<string, Task<HubPresenceCard>> inFlight = new Dictionary<string, Task<HubPresenceCard>>(StringComparer.Ordinal);

        public static async Task<HubPresenceCard> FetchAsync(string ugsPlayerId)
        {
            var empty = Empty(ugsPlayerId);
            if (string.IsNullOrWhiteSpace(ugsPlayerId))
                return empty;

            string id = ugsPlayerId.Trim();
            if (cache.TryGetValue(id, out CacheEntry cached) && Time.unscaledTime < cached.ExpiresAt)
                return cached.Card;

            if (inFlight.TryGetValue(id, out Task<HubPresenceCard> pending))
                return await pending;

            if (cached.Card != null && Time.unscaledTime < cached.FetchedAt + MinGapSeconds)
                return cached.Card;

            Task<HubPresenceCard> task = FetchUncachedAsync(id);
            inFlight[id] = task;
            try
            {
                HubPresenceCard card = await task;
                cache[id] = new CacheEntry
                {
                    Card = card,
                    FetchedAt = Time.unscaledTime,
                    ExpiresAt = Time.unscaledTime + CacheSeconds,
                };
                return card;
            }
            finally
            {
                inFlight.Remove(id);
            }
        }

        static async Task<HubPresenceCard> FetchUncachedAsync(string ugsPlayerId)
        {
            var empty = Empty(ugsPlayerId);
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
                    ugs_player_id = ugsPlayerId,
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
                    UgsPlayerId = ugsPlayerId,
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

        static HubPresenceCard Empty(string ugsPlayerId)
        {
            return new HubPresenceCard
            {
                UgsPlayerId = ugsPlayerId ?? string.Empty,
                DisplayName = "Trainer",
                CosmeticIdsJson = "[]",
            };
        }

        struct CacheEntry
        {
            public HubPresenceCard Card;
            public float FetchedAt;
            public float ExpiresAt;
        }

        [Serializable]
        class HubCardBody
        {
            public string player_profile_id;
            public string ugs_player_id;
        }

        [Serializable]
        class HubCardDto
        {
            public bool success;
            public string display_name;
            public string cosmetic_ids_json;
            public string error;
        }
    }
}
