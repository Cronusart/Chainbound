using System;
using UnityEngine;

/// <summary>
/// Shared handling for player display-name commits.
/// Chainling and starter names are intentionally not unique.
/// Offline commits cannot see other players; the next online
/// profile_set_display_name call is authoritative and re-prompts on name_taken.
/// </summary>
public static class PlayerNameCommitFlow
{
    public const string NameTakenCode = "name_taken";
    public const string NameTakenMessage = "That name is already taken. Try another.";

    public static bool ShouldRePrompt(PlayerNameCommitResult result)
    {
        if (result.Success)
            return false;

        if (string.Equals(result.ErrorCode, NameTakenCode, StringComparison.OrdinalIgnoreCase))
            return true;

        return result.ErrorMessage != null &&
               result.ErrorMessage.IndexOf(NameTakenCode, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static PlayerNameCommitResult FromEdgeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return PlayerNameCommitResult.FromError("Display name commit returned an empty response.");

        EdgeNameResponse payload = null;
        try
        {
            payload = JsonUtility.FromJson<EdgeNameResponse>(body);
        }
        catch (Exception)
        {
            payload = null;
        }

        string error = payload != null ? payload.error ?? "" : "";
        if (string.Equals(error, NameTakenCode, StringComparison.OrdinalIgnoreCase))
            return PlayerNameCommitResult.FromError(NameTakenMessage, NameTakenCode);

        if (!string.IsNullOrWhiteSpace(error))
            return PlayerNameCommitResult.FromError(error, error);

        return PlayerNameCommitResult.FromError(body);
    }

    [Serializable]
    private class EdgeNameResponse
    {
        public bool success;
        public string error;
        public string display_name;
    }
}
