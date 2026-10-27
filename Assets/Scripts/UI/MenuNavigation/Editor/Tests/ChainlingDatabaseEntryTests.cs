using System.Collections.Generic;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor.Tests
{
    public class ChainlingDatabaseEntryTests
    {
        [Test]
        public void UnknownSlot_ShowsNoNameOrType()
        {
            EncounterCatalogSpecies slot = Slot("kreechank", "Loomville");
            SpeciesDefinitionSO species = Species("kreechank", "Kreechank");
            try
            {
                ChainlingDatabaseEntryView view = ChainlingDatabaseEntryBuilder.Build(
                    slot,
                    species,
                    ChainlingDatabaseTier.Unregistered,
                    new[] { "Loomville" },
                    owned: false);

                Assert.AreEqual(ChainlingDatabaseEntryBuilder.UnregisteredLabel, view.rowLabel);
                Assert.AreEqual("", view.name);
                Assert.AreEqual("", view.typeLabel);
                Assert.AreEqual("", view.zoneLabel);
                Assert.AreEqual("", view.flagshipMoveName);
                Assert.AreEqual("", view.description);
                Assert.AreEqual("", view.volatilityLabel);
                Assert.IsFalse(view.showPortrait);
                Assert.IsFalse(view.canOpenSheet);
                Assert.AreEqual(ChainlingDatabaseEntryBuilder.NoFieldTrace, view.lowerLine1);
                Assert.IsFalse(view.sheetBody.Contains("Kreechank"));
            }
            finally
            {
                Object.DestroyImmediate(species.flagshipMove);
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void SeenSlot_ShowsTypeAndZoneButNotTheFlagshipMove()
        {
            EncounterCatalogSpecies slot = Slot("kreechank", "Loomville");
            SpeciesDefinitionSO species = Species("kreechank", "Kreechank");
            try
            {
                ChainlingDatabaseEntryView view = ChainlingDatabaseEntryBuilder.Build(
                    slot,
                    species,
                    ChainlingDatabaseTier.Encountered,
                    new[] { "Loomville", "Jungle" },
                    owned: false);

                Assert.AreEqual("Kreechank", view.name);
                Assert.AreEqual("Meme", view.typeLabel);
                Assert.AreEqual("Loomville", view.zoneLabel);
                Assert.AreEqual("Any", view.timeLabel);
                Assert.AreEqual(EncounterCatalogIndex.Often, view.frequencyLabel);
                Assert.AreEqual("", view.flagshipMoveName);
                Assert.AreEqual("", view.description);
                Assert.AreEqual("", view.volatilityLabel);
                Assert.AreEqual("", view.roleLabel);
                Assert.AreEqual("", view.starBand);
                Assert.IsTrue(view.canOpenSheet);
                Assert.IsFalse(view.canOpenDossier);
                Assert.AreEqual("Logged after a Loomville encounter", view.lowerLine1);
                Assert.IsFalse(view.sheetBody.Contains("Signal Slam"));
                Assert.IsFalse(view.sheetBody.Contains("stays off"));
                Assert.IsFalse(view.sheetBody.Contains("450"));
                Assert.IsFalse(view.sheetBody.Contains("Jungle"));
            }
            finally
            {
                Object.DestroyImmediate(species.flagshipMove);
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void CaughtSlot_ShowsVolatilityWordAndDescription()
        {
            EncounterCatalogSpecies slot = Slot("kreechank", "Loomville");
            slot.baseStars = 1;
            slot.starVariance = 2;
            SpeciesDefinitionSO species = Species("kreechank", "Kreechank");
            try
            {
                ChainlingDatabaseEntryView view = ChainlingDatabaseEntryBuilder.Build(
                    slot,
                    species,
                    ChainlingDatabaseTier.Captured,
                    new[] { "Loomville", "Jungle" },
                    owned: true);

                Assert.AreEqual("Standard", view.volatilityLabel);
                Assert.AreEqual(species.description, view.description);
                Assert.AreEqual("Signal Slam", view.flagshipMoveName);
                Assert.AreEqual("Stars 1-3", view.starBand);
                Assert.AreEqual("Jungle", view.otherZones);
                Assert.IsTrue(view.canOpenDossier);
                Assert.AreEqual(ChainlingDatabaseEntryBuilder.FileComplete, view.lowerLine1);
                StringAssert.Contains("Standard", view.sheetBody);
                StringAssert.Contains(species.description, view.sheetBody);
                StringAssert.Contains("Signal Slam", view.sheetBody);
                Assert.IsFalse(view.sheetBody.Contains("450"));
            }
            finally
            {
                Object.DestroyImmediate(species.flagshipMove);
                Object.DestroyImmediate(species);
            }
        }

        static EncounterCatalogSpecies Slot(string id, string zone)
        {
            return new EncounterCatalogSpecies
            {
                speciesId = id,
                zoneId = "loomville_grass",
                zoneDisplayName = zone,
                weight = 450,
                zoneTotalWeight = 900,
                allowedSegments = 15,
                baseCaptureRate = 55,
            };
        }

        static SpeciesDefinitionSO Species(string id, string name)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = "signal_slam";
            move.displayName = "Signal Slam";
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.displayName = name;
            species.primaryType = ChainlingType.Meme;
            species.description = "It clicks in the grass. The rest of the file stays off the field note.";
            species.flagshipMove = move;
            return species;
        }
    }
}
