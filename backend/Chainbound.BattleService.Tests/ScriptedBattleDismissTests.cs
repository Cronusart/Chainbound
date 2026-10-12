using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class ScriptedBattleDismissTests
{
    [Fact]
    public void Intro_creature_is_the_scripted_manifest_encounter()
    {
        string manifestDirectory = FindManifestDirectory();
        var catalog = new ManifestCatalog(
            new ServiceOptions
            {
                GameplayManifestPath = Path.Combine(manifestDirectory, "gameplay-manifest.json"),
                CaptureBalancePath = Path.Combine(manifestDirectory, "capture-balance.json"),
                BattleRewardsPath = Path.Combine(manifestDirectory, "battle-rewards.json"),
            },
            NullLogger<ManifestCatalog>.Instance);

        Assert.True(catalog.IsScriptedEncounter("intro_first_creature"));
        Assert.False(catalog.IsScriptedEncounter("loomville_grass"));
        Assert.False(catalog.IsScriptedEncounter(""));
    }

    private static string FindManifestDirectory()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "backend",
                "Chainbound.BattleService",
                "manifests");
            if (File.Exists(Path.Combine(candidate, "gameplay-manifest.json")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Gameplay manifests were not found above the test output.");
    }
}
