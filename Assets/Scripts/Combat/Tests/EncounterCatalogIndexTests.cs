using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class EncounterCatalogIndexTests
    {
        [Test]
        public void Build_ExtendMergesAreaEntriesAndKeepsStartersOnTheLabRow()
        {
            SpeciesDefinitionSO starter = Species("gilarime", "Gilarime", true);
            SpeciesDefinitionSO wild = Species("kreechank", "Kreechank", false);
            SpeciesDatabaseSO database = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            database.species = new List<SpeciesDefinitionSO> { starter, wild };

            EncounterAreaSO area = ScriptableObject.CreateInstance<EncounterAreaSO>();
            area.entries = new List<ZoneEncounterEntry>
            {
                Entry("gilarime", 10),
                Entry("kreechank", 20),
            };

            EncounterZoneSO grove = ScriptableObject.CreateInstance<EncounterZoneSO>();
            grove.zoneId = "starter_grove_grass";
            grove.entryMergeMode = EncounterEntryMergeMode.Extend;
            grove.area = area;
            grove.entries = new List<ZoneEncounterEntry> { Entry("cinislug", 30) };

            try
            {
                EncounterCatalogIndex index = EncounterCatalogIndex.Build(new[] { grove }, database);
                Assert.AreEqual(EncounterCatalogIndex.LabZoneId, index.Zones[0].zoneId);
                Assert.AreEqual("Loom Lab", index.Zones[0].displayName);
                Assert.AreEqual(1, index.Zones[0].species.Count);
                Assert.AreEqual("gilarime", index.Zones[0].species[0].speciesId);

                EncounterCatalogZone wildZone = index.Zones[1];
                Assert.AreEqual("Starter Grove", wildZone.displayName);
                Assert.AreEqual(2, wildZone.species.Count);
                Assert.AreEqual("kreechank", wildZone.species[0].speciesId);
                Assert.AreEqual("cinislug", wildZone.species[1].speciesId);
            }
            finally
            {
                Object.DestroyImmediate(starter);
                Object.DestroyImmediate(wild);
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(area);
                Object.DestroyImmediate(grove);
            }
        }

        [Test]
        public void Build_ReplaceIgnoresTheAreaTable()
        {
            EncounterAreaSO area = ScriptableObject.CreateInstance<EncounterAreaSO>();
            area.entries = new List<ZoneEncounterEntry> { Entry("hidden", 50) };
            EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
            zone.zoneId = "loomville_grass";
            zone.entryMergeMode = EncounterEntryMergeMode.Replace;
            zone.area = area;
            zone.entries = new List<ZoneEncounterEntry>
            {
                Entry("kreechank", 450),
                Entry("crackett", 14),
            };

            try
            {
                EncounterCatalogIndex index = EncounterCatalogIndex.Build(new[] { zone }, null);
                Assert.AreEqual(1, index.Zones.Count);
                Assert.AreEqual("Loomville", index.Zones[0].displayName);
                Assert.AreEqual(2, index.Zones[0].species.Count);
                Assert.AreEqual(EncounterCatalogIndex.Often, EncounterCatalogIndex.FormatFrequency(450, 464));
                Assert.AreEqual(EncounterCatalogIndex.Rare, EncounterCatalogIndex.FormatFrequency(14, 464));
                Assert.IsFalse(index.FindSpecies("hidden").Count > 0);
            }
            finally
            {
                Object.DestroyImmediate(area);
                Object.DestroyImmediate(zone);
            }
        }

        [Test]
        public void FormatTimeAndVolatility_UseCoarseWords()
        {
            Assert.AreEqual("Any", EncounterCatalogIndex.FormatTime(0));
            Assert.AreEqual("Night", EncounterCatalogIndex.FormatTime(1));
            Assert.AreEqual("Day", EncounterCatalogIndex.FormatTime(4));
            Assert.AreEqual("Any", EncounterCatalogIndex.FormatTime(5));
            Assert.AreEqual("Any", EncounterCatalogIndex.FormatTime(15));
            Assert.AreEqual("Calm", EncounterCatalogIndex.FormatVolatility(70));
            Assert.AreEqual("Standard", EncounterCatalogIndex.FormatVolatility(55));
            Assert.AreEqual("Volatile", EncounterCatalogIndex.FormatVolatility(20));
            Assert.AreEqual("Extreme", EncounterCatalogIndex.FormatVolatility(10));
            Assert.AreEqual("Stars 1-3", EncounterCatalogIndex.FormatStarBand(1, 2));
        }

        static SpeciesDefinitionSO Species(string id, string name, bool starter)
        {
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.displayName = name;
            species.isStarter = starter;
            return species;
        }

        static ZoneEncounterEntry Entry(string id, int weight)
        {
            return new ZoneEncounterEntry
            {
                speciesId = id,
                weight = weight,
            };
        }
    }
}
