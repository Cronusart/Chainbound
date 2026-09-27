using System.Security.Cryptography;
using System.Text.Json;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class ManifestCatalog
{
    private readonly ILogger<ManifestCatalog> _logger;
    private readonly GameplayManifest _manifest;
    private readonly CaptureBalanceManifest _capture;

    public ManifestCatalog(ServiceOptions options, ILogger<ManifestCatalog> logger)
    {
        _logger = logger;
        (_manifest, ManifestHash) = LoadGameplay(options.GameplayManifestPath);
        _capture = LoadCapture(options.CaptureBalancePath);
        ConfigHash = _capture.ConfigHash;
    }

    public string ManifestHash { get; }
    public string ConfigHash { get; }
    public CaptureBalanceData Balance => _capture.Balance.Normalized();
    public bool IsReady => _manifest.Encounters.Count > 0 && !string.IsNullOrWhiteSpace(ConfigHash);

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
            return (manifest, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
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

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
