using System.Security.Cryptography;
using System.Text.Json;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class ManifestCatalog
{
    private readonly ILogger<ManifestCatalog> _logger;
    private readonly GameplayManifest _manifest;
    private readonly CaptureBalanceManifest _capture;
    private readonly BattleRewardsManifest _rewards;

    public ManifestCatalog(ServiceOptions options, ILogger<ManifestCatalog> logger)
    {
        _logger = logger;
        (_manifest, ManifestHash) = LoadGameplay(options.GameplayManifestPath);
        _capture = LoadCapture(options.CaptureBalancePath);
        ConfigHash = _capture.ConfigHash;
        (_rewards, RewardConfigHash) = LoadRewards(options.BattleRewardsPath);
    }

    public string ManifestHash { get; }
    public string ConfigHash { get; }
    public string RewardConfigHash { get; }
    public int RewardConfigVersion => _rewards.ConfigVersion;
    public CaptureBalanceData Balance => _capture.Balance.Normalized();
    public bool IsReady =>
        _manifest.Encounters.Count > 0 &&
        !string.IsNullOrWhiteSpace(ConfigHash) &&
        IsRewardManifestValid(_rewards) &&
        !string.IsNullOrWhiteSpace(RewardConfigHash);

    public int ResolveRewardAmount(string rewardTier)
    {
        return (rewardTier ?? "").Trim().ToLowerInvariant() switch
        {
            BattleRewardTier.Standard => _rewards.Rewards.Standard,
            BattleRewardTier.Elite => _rewards.Rewards.Elite,
            BattleRewardTier.Boss => _rewards.Rewards.Boss,
            _ => throw new AuthorityException("invalid_reward_tier", "The encounter reward tier is not supported."),
        };
    }

    public ManifestEncounter ResolveEncounter(string zoneId, string narrativeEncounterId, uint seed)
    {
        IEnumerable<ManifestEncounter> candidates = _manifest.Encounters;
        if (!string.IsNullOrWhiteSpace(narrativeEncounterId))
            candidates = candidates.Where(x => string.Equals(x.EncounterId, narrativeEncounterId, StringComparison.Ordinal));
        else
            candidates = candidates.Where(x => string.Equals(x.ZoneId, zoneId, StringComparison.Ordinal));

        List<ManifestEncounter> rows = candidates.Where(x => x.Targets.Count > 0 && x.Weight > 0).ToList();
        if (rows.Count == 0)
            throw new AuthorityException("encounter_not_found", "The authoritative manifest has no matching encounter.", 404);

        int total = rows.Sum(x => Math.Max(0, x.Weight));
        var rng = new DeterministicCaptureRng(seed);
        int roll = rng.NextInt(0, Math.Max(1, total));
        for (int i = 0; i < rows.Count; i++)
        {
            roll -= Math.Max(0, rows[i].Weight);
            if (roll < 0)
                return rows[i];
        }
        return rows[^1];
    }

    private (BattleRewardsManifest Manifest, string Hash) LoadRewards(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            BattleRewardsManifest manifest = JsonSerializer.Deserialize<BattleRewardsManifest>(bytes, JsonDefaults.Options)
                ?? new BattleRewardsManifest();
            if (!IsRewardManifestValid(manifest))
                throw new InvalidDataException("Battle reward manifest failed schema/value validation.");
            return (manifest, CanonicalJsonHash.Compute(bytes));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Battle reward manifest was unavailable at {Path}.", path);
            return (new BattleRewardsManifest(), "");
        }
    }

    private static bool IsRewardManifestValid(BattleRewardsManifest manifest)
    {
        return manifest.SchemaVersion == 1 &&
            manifest.ConfigVersion > 0 &&
            manifest.Rewards != null &&
            manifest.Rewards.Standard > 0 &&
            manifest.Rewards.Elite > 0 &&
            manifest.Rewards.Boss > 0;
    }

    public string SelectEntangledType(uint seed)
    {
        List<WeightedEntangledType> pool = _capture.EntangledTypes
            .Where(x => !string.IsNullOrWhiteSpace(x.TypeId) && x.Weight > 0)
            .ToList();
        if (pool.Count == 0)
        {
            pool = new List<WeightedEntangledType>
            {
                new() { TypeId = "node", Weight = 1 },
                new() { TypeId = "gas", Weight = 1 },
                new() { TypeId = "block", Weight = 1 },
                new() { TypeId = "encrypted", Weight = 1 },
                new() { TypeId = "burned", Weight = 1 },
                new() { TypeId = "frozen", Weight = 1 },
                new() { TypeId = "forked", Weight = 1 },
                new() { TypeId = "mined", Weight = 1 },
                new() { TypeId = "staked", Weight = 1 },
                new() { TypeId = "compressed", Weight = 1 },
                new() { TypeId = "bot", Weight = 1 },
                new() { TypeId = "glitched", Weight = 1 },
                new() { TypeId = "shard", Weight = 1 },
            };
        }

        int total = pool.Sum(x => x.Weight);
        var rng = new DeterministicCaptureRng(seed ^ 0xE17A9E1Eu);
        int roll = rng.NextInt(0, total);
        foreach (WeightedEntangledType entry in pool)
        {
            roll -= entry.Weight;
            if (roll < 0) return entry.TypeId.Trim().ToLowerInvariant();
        }
        return pool[^1].TypeId.Trim().ToLowerInvariant();
    }

    private (GameplayManifest Manifest, string Hash) LoadGameplay(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            GameplayManifest manifest = JsonSerializer.Deserialize<GameplayManifest>(bytes, JsonDefaults.Options) ?? new GameplayManifest();
            return (manifest, CanonicalJsonHash.Compute(bytes));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Gameplay manifest was unavailable at {Path}.", path);
            return (new GameplayManifest(), "");
        }
    }

    private CaptureBalanceManifest LoadCapture(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            CaptureBalanceManifest manifest = JsonSerializer.Deserialize<CaptureBalanceManifest>(bytes, JsonDefaults.Options) ?? new CaptureBalanceManifest();
            manifest.Balance ??= CaptureBalanceData.CreateDefault();
            return manifest;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Capture balance manifest was unavailable at {Path}.", path);
            return new CaptureBalanceManifest { ConfigHash = "", Balance = CaptureBalanceData.CreateDefault() };
        }
    }
}

public static class CanonicalJsonHash
{
    public static string Compute(byte[] json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteCanonical(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
