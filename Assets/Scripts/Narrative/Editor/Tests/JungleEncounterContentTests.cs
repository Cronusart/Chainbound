using System.Collections.Generic;
using System.Linq;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;

public class JungleEncounterContentTests
{
    private const string ZonePath = "Assets/Data/Combat/Encounters/Jungle/EncounterZone_Jungle_Bush.asset";

    private static readonly HashSet<string> StarterSpeciesIds = new HashSet<string>
    {
        "validape",
        "gilarime",
        "kitsauto",
    };

    [Test]
    public void Resolver_UsesDayDuskAndNightTablesWithoutStartersOrBoss()
    {
        EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(ZonePath);
        Assert.That(zone, Is.Not.Null);
        Assert.That(zone.ResolveCombatStageId(), Is.EqualTo(CombatStageIds.Jungle));
        Assert.That(zone.difficulty, Is.Not.EqualTo(EncounterDifficulty.Boss));

        EffectiveEncounterTable dawn = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Dawn);
        EffectiveEncounterTable day = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Day);
        EffectiveEncounterTable dusk = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Dusk);
        EffectiveEncounterTable night = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Night);

        string[] daySpecies = { "rattlestake", "forkrook", "hopoch" };
        string[] nightSpecies = { "rattlestake", "forkrook", "hopoch", "deghoul", "blockwyrm" };
        Assert.That(dawn.entries.Select(entry => entry.speciesId), Is.EquivalentTo(daySpecies));
        Assert.That(day.entries.Select(entry => entry.speciesId), Is.EquivalentTo(daySpecies));
        Assert.That(dusk.entries.Select(entry => entry.speciesId), Is.EquivalentTo(nightSpecies));
        Assert.That(night.entries.Select(entry => entry.speciesId), Is.EquivalentTo(nightSpecies));
        Assert.That(WeightOf(night, "blockwyrm"), Is.EqualTo(6f));
        Assert.That(WeightOf(night, "deghoul"), Is.EqualTo(450f));
        Assert.That(night.entries.Select(entry => entry.speciesId), Does.Not.Contain("humpacked"));

        IEnumerable<string> allResolvedSpecies = day.entries
            .Concat(dusk.entries)
            .Concat(night.entries)
            .Select(entry => entry.speciesId);
        Assert.That(allResolvedSpecies.Any(StarterSpeciesIds.Contains), Is.False);
        Assert.That(allResolvedSpecies, Does.Not.Contain("cofi_lootwak"));
        Assert.That(allResolvedSpecies, Does.Not.Contain("humpacked"));
    }

    private static float WeightOf(EffectiveEncounterTable table, string speciesId)
    {
        return table.entries.First(entry => entry.speciesId == speciesId).weight;
    }
}
