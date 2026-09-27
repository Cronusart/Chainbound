using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Mutation helpers for the canonical player party lineup backed by Supabase edge functions.
    /// Wire this from player menu UX once roster editing ships.
    /// </summary>
    public static class SupabasePartyLineupMutation
    {
        [Serializable]
        private class ProfileSetPartyRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public string[] party_chainling_ids;
        }

        [Serializable]
        private class ProfileSetPartyEnvelope
        {
            public bool success;
            public string error;
            public string player_profile_id;
        }

        /// <summary>
        /// Persists lineup order (<c>party_slot</c> 0 … n‑1). Requires owning every chainling on the resolved profile.
        /// </summary>
        public static async Task<(bool Ok, string ErrorMessage)> ProfileSetPartyAsync(
            SupabaseConfigSO config,
            string playerProfileId,
            string walletAddress,
            string authUserIdOrEmpty,
            string[] orderedPartyChainlingBackendIds)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (orderedPartyChainlingBackendIds == null)
                return (false, "party_chainling_ids_was_null");

            if (orderedPartyChainlingBackendIds.Length == 0 ||
                orderedPartyChainlingBackendIds.Length > CombatConstants.ROSTER_SIZE)
            {
                return (false,
                    $"party_chainling_ids_must_have_between_1_and_{CombatConstants.ROSTER_SIZE}_entries.");
            }

            string json = JsonUtility.ToJson(new ProfileSetPartyRequest
            {
                player_profile_id = playerProfileId != null ? playerProfileId.Trim() : "",
                wallet_address = walletAddress != null ? walletAddress.Trim() : "",
                auth_user_id = authUserIdOrEmpty != null ? authUserIdOrEmpty.Trim() : "",
                party_chainling_ids = orderedPartyChainlingBackendIds,
            });

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, "profile_set_party", json);
            if (!response.IsSuccess)
            {
                return (false,
                    $"profile_set_party_http_{response.statusCode}: {response.body}");
            }

            var envelope = JsonUtility.FromJson<ProfileSetPartyEnvelope>(response.body);
            if (envelope == null || !envelope.success)
            {
                string err = envelope != null && !string.IsNullOrEmpty(envelope.error)
                    ? envelope.error
                    : "profile_set_party_invalid_payload";
                return (false, err);
            }

            return (true, null);
        }
    }
}
