using System.Collections.Generic;
using System.Linq;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;

public class Link1EncounterContentTests
{
    private const string ZonePath = "Assets/Data/Combat/Encounters/Link1/EncounterZone_Link1_Grass.asset";

    private static readonly HashSet<string> StarterSpeciesIds = new HashSet<string>
    {
        "validape",
        "gilarime",
        "kitsauto",
    };

    [Test]
    public void Resolver_UsesDayAndNightTablesWithoutStarters()
    {
        EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(ZonePath);
        Assert.That(zone, Is.Not.Null);
        Assert.That(zone.ResolveCombatStageId(), Is.EqualTo(CombatStageIds.Link1));

        EffectiveEncounterTable dawn = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Dawn);
        EffectiveEncounterTable day = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Day);
        EffectiveEncounterTable dusk = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Dusk);
        EffectiveEncounterTable night = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Night);

        string[] daySpecies = { "cinislug", "kodiax" };
        string[] nightSpecies = { "cinislug", "kodiax", "marrowhead", "minefolk" };
        Assert.That(dawn.entries.Select(entry => entry.speciesId), Is.EquivalentTo(daySpecies));
        Assert.That(day.entries.Select(entry => entry.speciesId), Is.EquivalentTo(daySpecies));
        Assert.That(dusk.entries.Select(entry => entry.speciesId), Is.EquivalentTo(nightSpecies));
        Assert.That(night.entries.Select(entry => entry.speciesId), Is.EquivalentTo(nightSpecies));
        Assert.That(WeightOf(night, "minefolk"), Is.EqualTo(100f));
        Assert.That(WeightOf(day, "cinislug"), Is.EqualTo(450f));

        IEnumerable<string> allResolvedSpecies = day.entries
            .Concat(night.entries)
            .Select(entry => entry.speciesId);
        Assert.That(allResolvedSpecies.Any(StarterSpeciesIds.Contains), Is.False);
        Assert.That(allResolvedSpecies, Does.Not.Contain("cofi_lootwak"));
    }

    private static float WeightOf(EffectiveEncounterTable table, string speciesId)
    {
        return table.entries.First(entry => entry.speciesId == speciesId).weight;
    }
}
