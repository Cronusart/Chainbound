using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public interface IChainlingDisplayNameService
    {
        Task<MenuMutationResult> SetDisplayNameAsync(
            string playerProfileId,
            string chainlingBackendId,
            string displayName);
    }

    /// <summary>Profile-first mutation for an owned Chainling's nickname.</summary>
    public sealed class SupabaseChainlingDisplayNameService : MonoBehaviour, IChainlingDisplayNameService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public void Configure(SupabaseConfigSO config)
        {
            if (config != null)
                supabaseConfig = config;
        }

        public async Task<MenuMutationResult> SetDisplayNameAsync(
            string playerProfileId,
            string chainlingBackendId,
            string displayName)
        {
            string profileId = playerProfileId != null ? playerProfileId.Trim() : string.Empty;
            string chainlingId = chainlingBackendId != null ? chainlingBackendId.Trim() : string.Empty;
            string resolvedName = ChainlingDisplayNameRules.Sanitize(displayName);

            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            if (supabaseConfig == null)
                return MenuMutationResult.Failed("SupabaseConfigSO was not assigned.");
            if (string.IsNullOrWhiteSpace(profileId))
                return MenuMutationResult.Failed("Player profile id was empty.");
            if (string.IsNullOrWhiteSpace(chainlingId))
                return MenuMutationResult.Failed("Captured Chainling id was empty.");
            if (!ChainlingDisplayNameRules.IsPersistenceValid(resolvedName))
                return MenuMutationResult.Failed("Chainling name was invalid.");

            try
            {
                string json = JsonUtility.ToJson(new SetNameRequest
                {
                    player_profile_id = profileId,
                    chainling_id = chainlingId,
                    display_name = resolvedName,
                });

                const string functionName = "profile_set_chainling_display_name";
                SupabaseClient.Response response =
                    await SupabaseClient.PostJsonAsync(supabaseConfig, functionName, json);
                if (!response.IsSuccess)
                    return Fail($"{functionName} returned {response.statusCode}: {response.body}");

                SetNameResponse payload = JsonUtility.FromJson<SetNameResponse>(response.body);
                if (payload == null || !payload.success)
                    return Fail(payload != null ? payload.error : $"{functionName} returned invalid JSON.");

                if (enableDebugLogs)
                    Debug.Log($"[SupabaseChainlingDisplayNameService] Named Chainling {chainlingId} '{resolvedName}'.");
                return MenuMutationResult.Ok();
            }
            catch (Exception exception)
            {
                return Fail(exception.Message);
            }
        }

        private MenuMutationResult Fail(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabaseChainlingDisplayNameService] {message}", this);
            return MenuMutationResult.Failed(message);
        }

        [Serializable]
        private sealed class SetNameRequest
        {
            public string player_profile_id;
            public string chainling_id;
            public string display_name;
        }

        [Serializable]
        private sealed class SetNameResponse
        {
            public bool success;
            public string error;
        }
    }
}
