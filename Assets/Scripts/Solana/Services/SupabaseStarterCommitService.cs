using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Supabase-backed starter commit service.
/// </summary>
public class SupabaseStarterCommitService : MonoBehaviour, IStarterCommitService
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

    public async Task<StarterCommitResult> CommitStarterAsync(
        string walletAddress,
        string starterSpeciesId,
        SupabaseConfigSO configOverride = null,
        string displayName = null)
    {
        string trimmedSpeciesId = starterSpeciesId != null ? starterSpeciesId.Trim() : "";
        string trimmedDisplayName = displayName != null ? displayName.Trim() : "";
        string playerProfileId = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : "";
        SupabaseConfigSO resolvedConfig = configOverride != null ? configOverride : supabaseConfig;

        if (string.IsNullOrWhiteSpace(playerProfileId))
            return StarterCommitResult.FromError("Authenticated player profile id is required.");

        if (string.IsNullOrWhiteSpace(trimmedSpeciesId))
            return StarterCommitResult.FromError("Starter species id was empty.");

        if (resolvedConfig == null)
            return StarterCommitResult.FromError("SupabaseConfigSO was not assigned.");

        try
        {
            string requestJson = JsonUtility.ToJson(new CommitStarterRequest
            {
                player_profile_id = playerProfileId,
                starter_species_id = trimmedSpeciesId,
                display_name = string.IsNullOrWhiteSpace(trimmedDisplayName) ? null : trimmedDisplayName,
            });

            const string functionName = "profile_commit_starter";

            var response = await SupabaseClient.PostJsonAsync(resolvedConfig, functionName, requestJson);
            if (!response.IsSuccess)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[SupabaseStarterCommitService] {functionName} returned {response.statusCode}: {response.body}");
                }

                return StarterCommitResult.FromError(
                    $"{functionName} returned {response.statusCode}: {response.body}");
            }

            CommitStarterResponse payload = JsonUtility.FromJson<CommitStarterResponse>(response.body);
            string confirmedSpeciesId = payload != null && !string.IsNullOrWhiteSpace(payload.species_id)
                ? payload.species_id.Trim()
                : trimmedSpeciesId;
            string backendChainlingId = payload != null ? payload.chainling_id ?? "" : "";

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[SupabaseStarterCommitService] Starter committed: species={confirmedSpeciesId} chainling={backendChainlingId}");
            }

            return StarterCommitResult.FromSuccess(confirmedSpeciesId, backendChainlingId);
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[SupabaseStarterCommitService] Exception: {ex.Message}");

            return StarterCommitResult.FromError(ex.Message);
        }
    }

    [Serializable]
    private class CommitStarterRequest
    {
        public string player_profile_id;
        public string starter_species_id;
        public string display_name;
    }

    [Serializable]
    private class CommitStarterResponse
    {
        public string chainling_id;
        public string species_id;
        public string error;
    }
}
