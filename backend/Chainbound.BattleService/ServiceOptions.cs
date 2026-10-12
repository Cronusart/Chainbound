namespace Chainbound.BattleService;

public sealed class ServiceOptions
{
    public string SupabaseUrl { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/') ?? "";
    public string SupabaseAnonKey { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";
    public string SupabaseServiceRoleKey { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? "";
    public string SolanaRpcEndpoint { get; init; } = Environment.GetEnvironmentVariable("SOLANA_RPC_ENDPOINT") ?? "https://api.devnet.solana.com";
    public string GameplayManifestPath { get; init; } = Environment.GetEnvironmentVariable("GAMEPLAY_MANIFEST_PATH") ?? "manifests/gameplay-manifest.json";
    public string CaptureBalancePath { get; init; } = Environment.GetEnvironmentVariable("CAPTURE_BALANCE_PATH") ?? "manifests/capture-balance.json";
    public string BattleRewardsPath { get; init; } = Environment.GetEnvironmentVariable("BATTLE_REWARDS_PATH") ?? "manifests/battle-rewards.json";
    public string BattleRewardMode { get; init; } = ReadRewardMode();
    public string BattleRewardServiceSecret { get; init; } = Environment.GetEnvironmentVariable("BATTLE_REWARD_SERVICE_SECRET") ?? "";
    public string BattleRewardSettleUrl { get; init; } = Environment.GetEnvironmentVariable("BATTLE_REWARD_SETTLE_URL")?.Trim() ?? "";
    public TimeSpan BattleRewardTimeout { get; init; } = TimeSpan.FromSeconds(ReadInt("BATTLE_REWARD_TIMEOUT_SECONDS", 8, 1, 15));
    public TimeSpan BattleRewardRetryCooldown { get; init; } = TimeSpan.FromSeconds(ReadInt("BATTLE_REWARD_RETRY_COOLDOWN_SECONDS", 5, 1, 60));
    public bool AuthoritativePveCaptureEnabled { get; init; } = ReadBool("AUTHORITATIVE_PVE_CAPTURE_ENABLED", false);
    public TimeSpan BattleResumeWindow { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan CaptureArmGrace { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan MaximumTimeExtrapolation { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan DisconnectedHoldRetention { get; init; } = TimeSpan.FromMilliseconds(250);
    public int CorrectionRateHz { get; init; } = 10;
    public int AbandonmentRamPenaltyFlat { get; init; } = 0;
    public int DefeatKoRespawnHpBasisPoints { get; init; } = ReadInt("BATTLE_DEFEAT_RESPAWN_HP_BP", 5000, 0, 10000);

    public bool HasAuthoritySecrets =>
        !string.IsNullOrWhiteSpace(SupabaseUrl) &&
        !string.IsNullOrWhiteSpace(SupabaseAnonKey) &&
        !string.IsNullOrWhiteSpace(SupabaseServiceRoleKey);

    public bool HasBattleRewardCoordinator =>
        HasAuthoritySecrets &&
        !string.IsNullOrWhiteSpace(BattleRewardServiceSecret) &&
        Uri.TryCreate(ResolvedBattleRewardSettleUrl, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    public string ResolvedBattleRewardSettleUrl => !string.IsNullOrWhiteSpace(BattleRewardSettleUrl)
        ? BattleRewardSettleUrl
        : $"{SupabaseUrl}/functions/v1/battle_reward_settle";

    private static bool ReadBool(string key, bool fallback)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        return bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }

    private static int ReadInt(string key, int fallback, int minimum, int maximum)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out int parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
    }

    private static string ReadRewardMode()
    {
        // The database settings row is the rollout authority. An unset value
        // passes through to it; explicit off/shadow values remain available as
        // stricter deployment-level emergency controls.
        string mode = (Environment.GetEnvironmentVariable("BATTLE_REWARD_MODE") ?? "grant").Trim().ToLowerInvariant();
        return mode is "off" or "shadow" or "grant" ? mode : "invalid";
    }
}
