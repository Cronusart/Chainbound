using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Supabase-backed player display-name write service.
/// </summary>
public class SupabasePlayerNameService : MonoBehaviour, IPlayerNameService
{
    [Header("Supabase")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public void Configure(SupabaseConfigSO config)
    {
        if (config != null)
            supabaseConfig = config;
    }

    public async Task<PlayerNameCommitResult> CommitDisplayNameAsync(
        string walletAddress,
        string displayName,
        SupabaseConfigSO configOverride = null)
    {
        string trimmedName = displayName != null ? displayName.Trim() : "";
        string playerProfileId = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";
        SupabaseConfigSO resolvedConfig = configOverride != null ? configOverride : supabaseConfig;

        if (string.IsNullOrWhiteSpace(playerProfileId))
            return PlayerNameCommitResult.FromError("Authenticated player profile id is required.");

        if (string.IsNullOrWhiteSpace(trimmedName))
            return PlayerNameCommitResult.FromError("Display name was empty.");

        if (resolvedConfig == null)
            return PlayerNameCommitResult.FromError("SupabaseConfigSO was not assigned.");

        try
        {
            string requestJson = JsonUtility.ToJson(new SetDisplayNameRequest
            {
                player_profile_id = playerProfileId,
                display_name = trimmedName,
            });

            const string functionName = "profile_set_display_name";

            var response = await SupabaseClient.PostJsonAsync(resolvedConfig, functionName, requestJson);
            if (!response.IsSuccess)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[SupabasePlayerNameService] {functionName} returned {response.statusCode}: {response.body}");
                }

                PlayerNameCommitResult parsed = PlayerNameCommitFlow.FromEdgeBody(response.body);
                if (PlayerNameCommitFlow.ShouldRePrompt(parsed))
                    return parsed;

                return PlayerNameCommitResult.FromError(
                    $"{functionName} returned {response.statusCode}: {response.body}",
                    parsed.ErrorCode);
            }

            SetDisplayNameResponse payload = JsonUtility.FromJson<SetDisplayNameResponse>(response.body);
            string confirmedName = payload != null && !string.IsNullOrWhiteSpace(payload.display_name)
                ? payload.display_name.Trim()
                : trimmedName;

            if (enableDebugLogs)
                Debug.Log($"[SupabasePlayerNameService] Display name committed: {confirmedName}");

            return PlayerNameCommitResult.FromSuccess(confirmedName);
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabasePlayerNameService] Exception: {ex.Message}");

            return PlayerNameCommitResult.FromError(ex.Message);
        }
    }

    [Serializable]
    private class SetDisplayNameRequest
    {
        public string player_profile_id;
        public string display_name;
    }

    [Serializable]
    private class SetDisplayNameResponse
    {
        public bool success;
        public string display_name;
        public string error;
    }
}
