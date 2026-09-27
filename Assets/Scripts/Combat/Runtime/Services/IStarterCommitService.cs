using System.Threading.Tasks;
using Chainbound.Combat;

public struct StarterCommitResult
{
    public bool Success;
    public string SpeciesId;
    public string BackendChainlingId;
    public string ErrorMessage;

    public static StarterCommitResult FromSuccess(string speciesId, string backendChainlingId)
    {
        return new StarterCommitResult
        {
            Success = true,
            SpeciesId = speciesId ?? "",
            BackendChainlingId = backendChainlingId ?? "",
            ErrorMessage = "",
        };
    }

    public static StarterCommitResult FromError(string errorMessage)
    {
        return new StarterCommitResult
        {
            Success = false,
            SpeciesId = "",
            BackendChainlingId = "",
            ErrorMessage = errorMessage ?? "Unknown error.",
        };
    }
}

/// <summary>
/// Commits the player's chosen starter to the active backend profile.
/// </summary>
public interface IStarterCommitService
{
    Task<StarterCommitResult> CommitStarterAsync(
        string walletAddress,
        string starterSpeciesId,
        SupabaseConfigSO configOverride = null,
        string displayName = null);
}
