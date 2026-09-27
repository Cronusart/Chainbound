using System.Threading.Tasks;
using Chainbound.Combat;

public struct PlayerNameCommitResult
{
    public bool Success;
    public string DisplayName;
    public string ErrorMessage;

    public static PlayerNameCommitResult FromSuccess(string displayName)
    {
        return new PlayerNameCommitResult
        {
            Success = true,
            DisplayName = displayName ?? "",
            ErrorMessage = "",
        };
    }

    public static PlayerNameCommitResult FromError(string errorMessage)
    {
        return new PlayerNameCommitResult
        {
            Success = false,
            DisplayName = "",
            ErrorMessage = errorMessage ?? "Unknown error.",
        };
    }
}

/// <summary>
/// Persists the player's chosen display name to the active backend profile.
/// </summary>
public interface IPlayerNameService
{
    Task<PlayerNameCommitResult> CommitDisplayNameAsync(
        string walletAddress,
        string displayName,
        SupabaseConfigSO configOverride = null);
}
