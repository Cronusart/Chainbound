using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Marks a player's intro as complete in Supabase and local PlayerPrefs.
    /// Prefers the canonical profile endpoint when GameSessionContext has a PlayerProfileId.
    /// Call from the intro sequence when it finishes.
    /// </summary>
    public static class SupabaseIntroCompleteHelper
    {
        private const string PREFS_KEY = "SignalJam_IntroQuestComplete";

        [Serializable]
        private struct SetIntroRequest
        {
            public string player_profile_id;
        }

        /// <summary>
        /// Fire-and-forget: marks intro_complete=true in Supabase and writes
        /// the PlayerPrefs flag. Logs errors but never throws.
        /// </summary>
        public static async void SetIntroComplete(SupabaseConfigSO config, string walletAddress)
        {
            // Always set locally first so the flag is available even if the
            // network call fails.
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();

            if (config == null)
            {
                Debug.LogWarning("[SupabaseIntroHelper] No config — skipped remote update.");
                return;
            }

            string playerProfileId = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";

            if (string.IsNullOrWhiteSpace(playerProfileId))
            {
                Debug.LogWarning("[SupabaseIntroHelper] No authenticated player profile id - skipped remote update.");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(new SetIntroRequest
                {
                    player_profile_id = playerProfileId,
                });

                const string functionName = "profile_set_intro_complete";

                var response = await SupabaseClient.PostJsonAsync(config, functionName, json);

                if (response.IsSuccess)
                {
                    Debug.Log($"[SupabaseIntroHelper] intro_complete=true persisted for profile={playerProfileId}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[SupabaseIntroHelper] {functionName} failed ({response.statusCode}): {response.body}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SupabaseIntroHelper] Exception: {ex.Message}");
            }
        }
    }
}
