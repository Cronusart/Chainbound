using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class FieldItemHealUtilityTests
    {
        [Test]
        public void SingleHeal_RaisesHpAndRejectsFull()
        {
            SpeciesDefinitionSO species = CreateSpecies(100);
            var snapshot = CreateSnapshot(40);

            Assert.IsTrue(FieldItemHealUtility.TryApplySingleHeal(
                snapshot, species, 30, out FieldItemHpPatch patch, out string error));
            Assert.AreEqual("", error);
            Assert.AreEqual(70, snapshot.currentHp);
            Assert.AreEqual(30, patch.healedAmount);

            snapshot.currentHp = ChainlingVitalsUtility.BuildMaxHp(snapshot, species);
            Assert.IsFalse(FieldItemHealUtility.TryApplySingleHeal(
                snapshot, species, 10, out _, out error));
            Assert.AreEqual("already_full_hp", error);
        }

        [Test]
        public void Revive_OnlyWhenFainted()
        {
            SpeciesDefinitionSO species = CreateSpecies(100);
            var snapshot = CreateSnapshot(20);
            FieldItemConfigSO config = FieldItemConfigSO.CreateRuntimeDefault();

            Assert.IsFalse(FieldItemHealUtility.TryApplyRevive(snapshot, species, config, out _, out string error));
            Assert.AreEqual("target_not_fainted", error);

            snapshot.currentHp = 0;
            Assert.IsTrue(FieldItemHealUtility.TryApplyRevive(snapshot, species, config, out FieldItemHpPatch patch, out error));
            Assert.AreEqual("", error);
            Assert.Greater(patch.currentHp, 0);
            Assert.LessOrEqual(patch.currentHp, ChainlingVitalsUtility.BuildMaxHp(snapshot, species));
        }

        [Test]
        public void PartyHeal_SkipsFullAndFainted()
        {
            SpeciesDefinitionSO species = CreateSpecies(100);
            var damaged = CreateSnapshot(10, "a");
            var fainted = CreateSnapshot(0, "b");
            var full = CreateSnapshot(ChainlingVitalsConstants.UnsyncedCurrentHp, "c");

            Assert.IsTrue(FieldItemHealUtility.TryApplyPartyHeal(
                new List<ChainlingSnapshot> { damaged, fainted, full },
                _ => species,
                15,
                out List<FieldItemHpPatch> patches,
                out string error));
            Assert.AreEqual("", error);
            Assert.AreEqual(1, patches.Count);
            Assert.AreEqual(25, damaged.currentHp);
            Assert.AreEqual(0, fainted.currentHp);
        }

        private static ChainlingSnapshot CreateSnapshot(int currentHp, string id = "c1")
        {
            return new ChainlingSnapshot
            {
                backendChainlingId = id,
                speciesId = "validape",
                stars = 0,
                rebirthCount = 0,
                currentHp = currentHp,
            };
        }

        private static SpeciesDefinitionSO CreateSpecies(int baseMaxHp)
        {
            var species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "validape";
            species.baseMaxHP = baseMaxHp;
            species.baseMeleeAtk = 10;
            species.baseMeleeDef = 10;
            species.baseRangedAtk = 10;
            species.baseRangedDef = 10;
            species.baseSpeed = 10;
            return species;
        }
    }
}
