using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class ManifestEncounterAuthorityTests
{
    [Theory]
    [InlineData("2026-01-01T10:47:00Z", "night")]
    [InlineData("2026-01-01T10:48:00Z", "dawn")]
    [InlineData("2026-01-01T13:12:00Z", "day")]
    [InlineData("2026-01-01T22:48:00Z", "dusk")]
    [InlineData("2026-01-02T01:12:00Z", "night")]
    public void Authority_time_uses_the_fixed_CST_encounter_segments(string timestamp, string expected)
    {
        long milliseconds = DateTimeOffset.Parse(timestamp).ToUnixTimeMilliseconds();

        Assert.Equal(expected, ManifestCatalog.ResolveTimeSegment(milliseconds));
    }

    [Fact]
    public void Zone_resolution_only_draws_from_the_authority_time_segment()
    {
        using TestCatalogFiles files = TestCatalogFiles.Create();
        ManifestCatalog catalog = files.CreateCatalog();

        ManifestEncounter day = catalog.ResolveEncounter(
            "jungle_bush",
            "",
            123,
            DateTimeOffset.Parse("2026-01-01T18:00:00Z").ToUnixTimeMilliseconds());
        ManifestEncounter night = catalog.ResolveEncounter(
            "jungle_bush",
            "",
            123,
            DateTimeOffset.Parse("2026-01-01T06:00:00Z").ToUnixTimeMilliseconds());

        Assert.Equal("day-creature", day.Targets.Single().SpeciesId);
        Assert.Equal("night-creature", night.Targets.Single().SpeciesId);
    }

    [Fact]
    public void Server_iv_roll_is_deterministic_bounded_and_not_the_manifest_zero_array()
    {
        var target = new ManifestTarget
        {
            TargetId = "jungle_bush:night:forkrook",
            RollIvs = true,
            IvMin = -5,
            IvMax = 5,
            Ivs = new int[6],
        };
        MethodInfo method = typeof(BattleSessionManager).GetMethod(
            "RollTargetIvs",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(BattleSessionManager), "RollTargetIvs");

        int[] first = (int[])method.Invoke(null, new object[] { target, (uint)1819909814, 0 })!;
        int[] second = (int[])method.Invoke(null, new object[] { target, (uint)1819909814, 0 })!;

        Assert.Equal(first, second);
        Assert.All(first, value => Assert.InRange(value, -5, 5));
        Assert.Contains(first, value => value != 0);
    }

    private sealed class TestCatalogFiles : IDisposable
    {
        private readonly string directory;

        private TestCatalogFiles(string directory)
        {
            this.directory = directory;
        }

        public static TestCatalogFiles Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"chainbound-manifest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var gameplay = new
            {
                manifestVersion = 2,
                encounters = new object[]
                {
                    Encounter("day", "day-creature"),
                    Encounter("night", "night-creature"),
                },
            };
            File.WriteAllText(
                Path.Combine(directory, "gameplay.json"),
                JsonSerializer.Serialize(gameplay));
            return new TestCatalogFiles(directory);
        }

        public ManifestCatalog CreateCatalog()
        {
            return new ManifestCatalog(
                new ServiceOptions
                {
                    GameplayManifestPath = Path.Combine(directory, "gameplay.json"),
                    CaptureBalancePath = Path.Combine(directory, "missing-capture.json"),
                    BattleRewardsPath = Path.Combine(directory, "missing-rewards.json"),
                },
                NullLogger<ManifestCatalog>.Instance);
        }

        public void Dispose()
        {
            Directory.Delete(directory, recursive: true);
        }

        private static object Encounter(string timeSegment, string speciesId)
        {
            return new
            {
                encounterId = $"zone:jungle_bush:{timeSegment}:{speciesId}",
                zoneId = "jungle_bush",
                timeSegment,
                weight = 1,
                targets = new object[]
                {
                    new { targetId = speciesId, speciesId },
                },
            };
        }
    }
}
