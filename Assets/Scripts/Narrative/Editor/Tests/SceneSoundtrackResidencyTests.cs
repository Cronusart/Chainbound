using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public sealed class SceneSoundtrackResidencyTests
{
    private const string CatalogPath = "Assets/Data/Audio/Music/SceneSoundtrackCatalog.asset";

    [Test]
    public void AdditiveUnmappedLoad_DoesNotApplySoundtrack()
    {
        Assert.That(
            MusicConductor.ShouldApplySceneLoadedSoundtrack(LoadSceneMode.Additive, sceneHasCatalogEntry: false),
            Is.False);
    }

    [Test]
    public void AdditiveMappedLoad_DoesNotApplySoundtrackUntilChunkBecomesActive()
    {
        Assert.That(
            MusicConductor.ShouldApplySceneLoadedSoundtrack(LoadSceneMode.Additive, sceneHasCatalogEntry: true),
            Is.False);
    }

    [Test]
    public void SingleUnmappedLoad_AppliesSoundtrackSoHardSwapsCanStopMusic()
    {
        Assert.That(
            MusicConductor.ShouldApplySceneLoadedSoundtrack(LoadSceneMode.Single, sceneHasCatalogEntry: false),
            Is.True);
    }

    [Test]
    public void Catalog_Link1SharesLoomvilleCueWithoutForceRestart()
    {
        SceneSoundtrackCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SceneSoundtrackCatalogSO>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);

        Assert.That(catalog.TryGetEntry("Loomville", out SceneSoundtrackCatalogSO.SceneSoundtrackEntry loomville), Is.True);
        Assert.That(catalog.TryGetEntry("Link1", out SceneSoundtrackCatalogSO.SceneSoundtrackEntry link1), Is.True);

        Assert.That(loomville.cue, Is.SameAs(link1.cue));
        Assert.That(loomville.forceRestart, Is.False);
        Assert.That(link1.forceRestart, Is.False);
    }

    [Test]
    public void EqualPowerCrossfade_MaintainsConstantCombinedPower()
    {
        MethodInfo method = typeof(MusicConductor).GetMethod(
            "EvaluateEqualPowerGains",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        Vector2 start = (Vector2)method.Invoke(null, new object[] { 0f });
        Vector2 middle = (Vector2)method.Invoke(null, new object[] { 0.5f });
        Vector2 end = (Vector2)method.Invoke(null, new object[] { 1f });

        Assert.That(start.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(start.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(end.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(end.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(middle.x * middle.x + middle.y * middle.y, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Catalog_OverworldTransitionsUseThreeSecondFade()
    {
        SceneSoundtrackCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SceneSoundtrackCatalogSO>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);

        foreach (string sceneName in new[] { "Loomville", "Link1", "Jungle", "Academy_Interior" })
        {
            Assert.That(catalog.TryGetEntry(sceneName, out SceneSoundtrackCatalogSO.SceneSoundtrackEntry entry), Is.True, sceneName);
            Assert.That(entry.fadeSeconds, Is.EqualTo(3f), sceneName);
        }
    }

    [Test]
    public void Catalog_AcademySharesJungleCueWithoutRestart()
    {
        SceneSoundtrackCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SceneSoundtrackCatalogSO>(CatalogPath);
        Assert.That(catalog.TryGetEntry("Jungle", out SceneSoundtrackCatalogSO.SceneSoundtrackEntry jungle), Is.True);
        Assert.That(catalog.TryGetEntry("Academy_Interior", out SceneSoundtrackCatalogSO.SceneSoundtrackEntry academy), Is.True);
        Assert.That(academy.cue, Is.SameAs(jungle.cue));
        Assert.That(academy.forceRestart, Is.False);
    }
}
