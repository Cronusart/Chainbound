using System.Collections.Generic;
using System.Linq;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEditor;

public class LoomvilleEncounterContentTests
{
    private const string ZonePath = "Assets/Data/Combat/Encounters/Loomville/EncounterZone_Loomville_Grass.asset";

    private static readonly HashSet<string> StarterSpeciesIds = new HashSet<string>
    {
        "validape",
        "gilarime",
        "kitsauto",
    };

    [Test]
    public void Resolver_UsesDistinctDayAndEveningTablesWithoutStarters()
    {
        EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(ZonePath);
        Assert.That(zone, Is.Not.Null);

        EffectiveEncounterTable day = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Day);
        EffectiveEncounterTable dusk = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Dusk);
        EffectiveEncounterTable night = EncounterTableResolver.Resolve(zone, new WildRollConfig(), TimeSegment.Night);

        Assert.That(day.entries.Select(entry => entry.speciesId), Is.EquivalentTo(new[]
        {
            "cinislug",
            "caw",
            "snek",
        }));
        Assert.That(dusk.entries.Select(entry => entry.speciesId), Is.EquivalentTo(new[]
        {
            "kreechank",
            "ghosty",
        }));
        Assert.That(night.entries.Select(entry => entry.speciesId), Is.EquivalentTo(new[]
        {
            "kreechank",
            "ghosty",
            "deghoul",
        }));

        IEnumerable<string> allResolvedSpecies = day.entries
            .Concat(dusk.entries)
            .Concat(night.entries)
            .Select(entry => entry.speciesId);
        Assert.That(allResolvedSpecies.Any(StarterSpeciesIds.Contains), Is.False);
    }

    [Test]
    public void EntriesUseOneProficiencyAlignedBaselineMove()
    {
        EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(ZonePath);
        Assert.That(zone, Is.Not.Null);

        var expectedMoves = new Dictionary<string, string>
        {
            { "cinislug", "baseline_shot" },
            { "caw", "baseline_shot" },
            { "snek", "baseline_shot" },
            { "kreechank", "baseline_striker" },
            { "ghosty", "baseline_shot" },
            { "deghoul", "baseline_striker" },
        };

        Assert.That(zone.entries.Count, Is.EqualTo(expectedMoves.Count));
        for (int i = 0; i < zone.entries.Count; i++)
        {
            ZoneEncounterEntry entry = zone.entries[i];
            Assert.That(entry.movePool, Is.Not.Null, entry.speciesId);
            Assert.That(entry.battleKitSizeMin, Is.EqualTo(1), entry.speciesId);
            Assert.That(entry.battleKitSizeMax, Is.EqualTo(1), entry.speciesId);
            Assert.That(entry.movePool.entries.Count, Is.EqualTo(1), entry.speciesId);
            Assert.That(entry.movePool.entries[0].moveId, Is.EqualTo(expectedMoves[entry.speciesId]), entry.speciesId);
        }
    }
}
