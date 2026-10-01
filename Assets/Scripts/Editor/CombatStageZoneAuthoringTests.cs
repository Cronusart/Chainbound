using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatStageZoneAuthoringTests
{
    [SetUp]
    public void ClearStageRequest()
    {
        CombatStageRequest.TryConsumePending(out _);
        CombatStageRequest.ClearActive(null);
    }

    [Test]
    public void Normalize_Empty_UsesLoomville()
    {
        Assert.That(CombatStageIds.Normalize(null), Is.EqualTo(CombatStageIds.Loomville));
        Assert.That(CombatStageIds.Normalize("  "), Is.EqualTo(CombatStageIds.Loomville));
        Assert.That(CombatStageIds.Normalize(" Link1 "), Is.EqualTo(CombatStageIds.Link1));
    }

    [Test]
    public void Zone_EmptyStage_ResolvesToLoomville()
    {
        EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
        try
        {
            zone.combatStageId = "";
            Assert.That(zone.ResolveCombatStageId(), Is.EqualTo(CombatStageIds.Loomville));
            zone.combatStageId = "jungle";
            Assert.That(zone.ResolveCombatStageId(), Is.EqualTo(CombatStageIds.Jungle));
        }
        finally
        {
            Object.DestroyImmediate(zone);
        }
    }

    [Test]
    public void PendingStage_IsConsumedOnce()
    {
        CombatStageRequest.SetPending("link1");
        Assert.That(CombatStageRequest.TryConsumePending(out string stageId), Is.True);
        Assert.That(stageId, Is.EqualTo(CombatStageIds.Link1));
        Assert.That(CombatStageRequest.TryConsumePending(out _), Is.False);
    }

    [Test]
    public void EncounterZones_CarryCombatStageIds()
    {
        AssertStage("Assets/Data/Combat/Encounters/Loomville/EncounterZone_Loomville_Grass.asset", CombatStageIds.Loomville);
        AssertStage("Assets/Data/Combat/Encounters/Link1/EncounterZone_Link1_Grass.asset", CombatStageIds.Link1);
        AssertStage("Assets/Data/Combat/Encounters/EncounterZone_StarterGrove_Grass.asset", CombatStageIds.Loomville);
    }

    [Test]
    public void StageIds_ResolveAtmosphereCatalogProfiles()
    {
        AtmosphericCatalogSO catalog = AssetDatabase.LoadAssetAtPath<AtmosphericCatalogSO>(
            "Assets/Data/World/AtmosphericCatalog_Overworld.asset");
        Assert.That(catalog, Is.Not.Null);

        AtmosphericProfileSO loomville = catalog.Resolve(CombatStageIds.Loomville);
        AtmosphericProfileSO link1 = catalog.Resolve(CombatStageIds.Link1);
        AtmosphericProfileSO jungle = catalog.Resolve(CombatStageIds.Jungle);
        Assert.That(loomville, Is.Not.Null);
        Assert.That(link1, Is.Not.Null);
        Assert.That(jungle, Is.Not.Null);
        Assert.That(jungle, Is.Not.SameAs(loomville));
    }

    private static void AssertStage(string path, string expectedStageId)
    {
        EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(path);
        Assert.That(zone, Is.Not.Null, path);
        Assert.That(zone.ResolveCombatStageId(), Is.EqualTo(expectedStageId), path);
    }
}
