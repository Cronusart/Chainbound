/// <summary>
/// Single home for the player-facing account copy shown on the game menu's status label.
///
/// Account failures arrive as raw service strings: the Supabase gateway wraps an Edge
/// Function response as "&lt;function&gt; returned &lt;status&gt;: &lt;body&gt;", so the
/// machine error code is still embedded in the text. Matching those codes here keeps
/// backend vocabulary out of the menu controllers and out of the player's face, and gives
/// one place to edit wording or localize later.
/// </summary>
public static class MainMenuAccountStatusCopy
{
    public const string NoRestorableProfile = "NO RESTORABLE PROFILES FOUND";
    public const string RestoreUnavailable = "PROFILE RESTORE UNAVAILABLE ON THIS BUILD";
    public const string WalletProfileRecoveryFailed = "WALLET PROFILE RECOVERY FAILED - TRY AGAIN";
    public const string RestoreRequestExpired = "RESTORE REQUEST EXPIRED - TRY AGAIN";
    public const string RestoreProofRejected = "WALLET SIGNATURE WAS REJECTED";
    public const string ProfileServiceOffline = "PROFILE SERVICE OFFLINE";
    public const string WalletRequestCancelled = "WALLET REQUEST CANCELLED";
    public const string WalletRequestTimedOut = "WALLET REQUEST TIMED OUT - TRY AGAIN";
    public const string WalletUnavailable = "NO COMPATIBLE SOLANA WALLET FOUND";
    public const string WalletBusy = "A WALLET REQUEST IS ALREADY OPEN";
    public const string ClientUpdateRequired = "UPDATE REQUIRED TO LINK A WALLET";
    public const string BetaAccessRequired = "THIS WALLET IS NOT APPROVED FOR THE CLOSED BETA";
    public const string WrongWalletForProfile = "CONNECT THE WALLET ALREADY LINKED TO THIS PROFILE";
    public const string WalletLinkRace = "WALLET LINK CHANGED - REFRESH AND TRY AGAIN";
    public const string GenericFailure = "PROFILE REQUEST FAILED - TRY AGAIN";

    /// <summary>
    /// Translates a service error into status-label copy. Messages that are already
    /// player-readable pass through unchanged; anything still carrying a service shape is
    /// replaced so raw backend text never reaches the label.
    /// </summary>
    public static string Resolve(string serviceError)
    {
        if (string.IsNullOrWhiteSpace(serviceError))
            return GenericFailure;

        string probe = serviceError.ToLowerInvariant();

        // Backend error codes are exact contract strings, so they are matched first.
        if (probe.Contains("wallet_profile_not_found"))
            return NoRestorableProfile;
        if (probe.Contains("beta_not_allowlisted"))
            return BetaAccessRequired;
        if (probe.Contains("wallet_mismatch_for_profile") || probe.Contains("profile_wallet_mismatch"))
            return WrongWalletForProfile;
        if (probe.Contains("wallet_link_race_conflict"))
            return WalletLinkRace;
        if (probe.Contains("wallet_recovery is unavailable") || probe.Contains("wallet provider is not available"))
            return RestoreUnavailable;
        if (probe.Contains("wallet_profile_has_different_auth_identity") ||
            probe.Contains("wallet_profile_recovery_failed") ||
            probe.Contains("wallet_profile_recovery_target_mismatch"))
            return WalletProfileRecoveryFailed;
        if (probe.Contains("challenge_not_available") || probe.Contains("challenge_mismatch_or_expired"))
            return RestoreRequestExpired;
        if (probe.Contains("invalid_proof") || probe.Contains("missing_wallet_proof"))
            return RestoreProofRejected;
        if (probe.Contains("authenticated_profile_not_found"))
            return ProfileServiceOffline;
        if (probe.Contains("no_wallet_found") || probe.Contains("no compatible wallet") || probe.Contains("no mobile wallet adapter bridge"))
            return WalletUnavailable;
        if (probe.Contains("request_in_progress"))
            return WalletBusy;
        if (probe.Contains("legacy_wallet_link_disabled"))
            return ClientUpdateRequired;

        // Wallet-side outcomes surface as prose from the adapter rather than as codes.
        if (probe.Contains("cancel"))
            return WalletRequestCancelled;
        if (probe.Contains("timed out") || probe.Contains("timeout"))
            return WalletRequestTimedOut;

        return LooksMachineGenerated(probe) ? GenericFailure : serviceError;
    }

    private static bool LooksMachineGenerated(string probe)
    {
        return probe.Contains(" returned ") || probe.Contains("{") || probe.Contains("_");
    }
}
