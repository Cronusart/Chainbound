namespace Chainbound.BattleService;

public sealed class ServiceOptions
{
    public string SupabaseUrl { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/') ?? "";
    public string SupabaseAnonKey { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY") ?? "";
    public string SupabaseServiceRoleKey { get; init; } = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? "";
    public string SolanaRpcEndpoint { get; init; } = Environment.GetEnvironmentVariable("SOLANA_RPC_ENDPOINT") ?? "https://api.devnet.solana.com";
    public string GameplayManifestPath { get; init; } = Environment.GetEnvironmentVariable("GAMEPLAY_MANIFEST_PATH") ?? "manifests/gameplay-manifest.json";
    public string CaptureBalancePath { get; init; } = Environment.GetEnvironmentVariable("CAPTURE_BALANCE_PATH") ?? "manifests/capture-balance.json";
    public bool AuthoritativePveCaptureEnabled { get; init; } = ReadBool("AUTHORITATIVE_PVE_CAPTURE_ENABLED", false);
    public TimeSpan BattleResumeWindow { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaximumTimeExtrapolation { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan DisconnectedHoldRetention { get; init; } = TimeSpan.FromMilliseconds(250);
    public int CorrectionRateHz { get; init; } = 10;
    public int AbandonmentRamPenaltyFlat { get; init; } = ReadInt("BATTLE_ABANDONMENT_RAM_PENALTY", 5, 0, int.MaxValue);
    public int DefeatKoRespawnHpBasisPoints { get; init; } = ReadInt("BATTLE_DEFEAT_RESPAWN_HP_BP", 5000, 0, 10000);

    public bool HasAuthoritySecrets =>
        !string.IsNullOrWhiteSpace(SupabaseUrl) &&
        !string.IsNullOrWhiteSpace(SupabaseAnonKey) &&
        !string.IsNullOrWhiteSpace(SupabaseServiceRoleKey);

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
}
