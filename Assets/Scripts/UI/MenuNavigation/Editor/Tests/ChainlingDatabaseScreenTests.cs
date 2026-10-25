using System.Collections.Generic;
using Chainbound.Combat;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor.Tests
{
    public class ChainlingDatabaseScreenTests
    {
        [Test]
        public void Screen_UnknownDoesNotOpenAndSeenHidesTheFlagship()
        {
            SpeciesDatabaseSO database = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            SpeciesDefinitionSO unknown = Species(database, "unknownling", "Unknownling");
            SpeciesDefinitionSO seen = Species(database, "seenling", "Seenling");
            SpeciesDefinitionSO caught = Species(database, "caughtling", "Caughtling");
            EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
            zone.zoneId = "loomville_grass";
            zone.entryMergeMode = EncounterEntryMergeMode.Replace;
            zone.entries = new List<ZoneEncounterEntry>
            {
                new ZoneEncounterEntry { speciesId = "unknownling", weight = 10, baseCaptureRate = 55 },
                new ZoneEncounterEntry { speciesId = "seenling", weight = 10, baseCaptureRate = 55 },
                new ZoneEncounterEntry { speciesId = "caughtling", weight = 10, baseCaptureRate = 55 },
            };

            GameObject host = new GameObject("database", typeof(RectTransform), typeof(ChainlingDatabaseScreen));
            ChainlingDatabaseScreen screen = host.GetComponent<ChainlingDatabaseScreen>();
            try
            {
                screen.BindForTests(
                    new[] { zone },
                    database,
                    new ChainlingDexSnapshot
                    {
                        seenSpeciesIds = new List<string> { "seenling" },
                        caughtSpeciesIds = new List<string> { "caughtling" },
                    },
                    new HashSet<string> { "caughtling" });

                Assert.AreEqual("Unregistered", Row(screen, "SpeciesRow_00"));
                Assert.AreEqual("Seenling", Row(screen, "SpeciesRow_01"));
                Assert.AreEqual("Caughtling", Row(screen, "SpeciesRow_02"));
                Assert.AreEqual("Unregistered", Text(screen, "TopRight/Name"));
                Assert.IsNull(screen.transform.Find("TopRight/UnknownMark").GetComponent<Image>().sprite);
                Assert.AreEqual(Navigation.Mode.Explicit, ButtonAt(screen, "ZoneRow_00").navigation.mode);
                Assert.GreaterOrEqual(ButtonAt(screen, "ZoneRow_00").GetComponent<LayoutElement>().minHeight, 60f);

                ButtonAt(screen, "SpeciesRow_00").onClick.Invoke();
                Assert.IsFalse(screen.transform.Find("DatabaseSheet").gameObject.activeSelf);
                Assert.AreEqual(ChainlingDatabaseEntryBuilder.NoFieldTrace, Text(screen, "DialogueLowerCenter/Line1"));
                Assert.IsFalse(Text(screen, "TopRight/Name").Contains("Unknownling"));

                ButtonAt(screen, "SpeciesRow_01").onClick.Invoke();
                Assert.IsTrue(screen.transform.Find("DatabaseSheet").gameObject.activeSelf);
                string seenBody = Text(screen, "DatabaseSheet/Body");
                StringAssert.Contains("Meme", seenBody);
                StringAssert.Contains("Loomville", seenBody);
                Assert.IsFalse(seenBody.Contains("Signal Slam"));
                Assert.IsFalse(seenBody.Contains("Standard"));

                ButtonAt(screen, "SpeciesRow_02").onClick.Invoke();
                string caughtBody = Text(screen, "DatabaseSheet/Body");
                StringAssert.Contains("Standard", caughtBody);
                StringAssert.Contains(caught.description, caughtBody);
                StringAssert.Contains("Signal Slam", caughtBody);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(unknown.flagshipMove);
                Object.DestroyImmediate(seen.flagshipMove);
                Object.DestroyImmediate(caught.flagshipMove);
                Object.DestroyImmediate(unknown);
                Object.DestroyImmediate(seen);
                Object.DestroyImmediate(caught);
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(zone);
            }
        }

        static SpeciesDefinitionSO Species(SpeciesDatabaseSO database, string id, string name)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.displayName = "Signal Slam";
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.displayName = name;
            species.primaryType = ChainlingType.Meme;
            species.description = "It clicks in the grass. The rest of the file stays off the field note.";
            species.flagshipMove = move;
            database.species.Add(species);
            return species;
        }

        static string Row(ChainlingDatabaseScreen screen, string rowName)
        {
            return Text(screen, ChainlingDatabaseLayout.SpeciesContentPath + "/" + rowName + "/Label");
        }

        static Button ButtonAt(ChainlingDatabaseScreen screen, string rowName)
        {
            string path = rowName.StartsWith("Zone")
                ? ChainlingDatabaseLayout.ZoneContentPath + "/" + rowName
                : ChainlingDatabaseLayout.SpeciesContentPath + "/" + rowName;
            return screen.transform.Find(path).GetComponent<Button>();
        }

        static string Text(ChainlingDatabaseScreen screen, string path)
        {
            return screen.transform.Find(path).GetComponent<TMP_Text>().text;
        }
    }
}
