using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingVitalsUtilityTests
    {
        [Test]
        public void ResolveEffectiveCurrentHp_Unsynced_ReturnsMaxHp()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(ChainlingVitalsConstants.UnsyncedCurrentHp);
            SpeciesDefinitionSO species = CreateSpecies(baseMaxHp: 120);

            Assert.AreEqual(120, ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, species));
        }

        [Test]
        public void ResolveEffectiveCurrentHp_Zero_IsFainted()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(0);
            SpeciesDefinitionSO species = CreateSpecies(baseMaxHp: 100);
            ChainlingVitalsDisplay display = ChainlingVitalsUtility.BuildDisplay(snapshot, species);

            Assert.AreEqual(0, display.CurrentHp);
            Assert.IsTrue(display.IsFainted);
            Assert.AreEqual(0f, display.Fill01, 0.001f);
        }

        [Test]
        public void ResolveEffectiveCurrentHp_AboveMax_ClampsToMax()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(150);
            SpeciesDefinitionSO species = CreateSpecies(baseMaxHp: 100);

            Assert.AreEqual(100, ChainlingVitalsUtility.ResolveEffectiveCurrentHp(snapshot, species));
        }

        [Test]
        public void ResolveAuthoritativeCheckpointCurrentHp_Unsynced_ReturnsEffectiveMaxHp()
        {
            int resolved = ChainlingVitalsUtility.ResolveAuthoritativeCheckpointCurrentHp(
                ChainlingVitalsConstants.UnsyncedCurrentHp,
                156);

            Assert.AreEqual(156, resolved);
        }

        [Test]
        public void ResolveAuthoritativeCheckpointCurrentHp_Zero_RemainsFainted()
        {
            int resolved = ChainlingVitalsUtility.ResolveAuthoritativeCheckpointCurrentHp(0, 156);

            Assert.AreEqual(0, resolved);
        }

        [Test]
        public void ResolveAuthoritativeCheckpointCurrentHp_AboveMax_ClampsToEffectiveMaxHp()
        {
            int resolved = ChainlingVitalsUtility.ResolveAuthoritativeCheckpointCurrentHp(200, 156);

            Assert.AreEqual(156, resolved);
        }

        [Test]
        public void BuildDisplay_PartialHp_ComputesFillRatio()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(50);
            SpeciesDefinitionSO species = CreateSpecies(baseMaxHp: 100);
            ChainlingVitalsDisplay display = ChainlingVitalsUtility.BuildDisplay(snapshot, species);

            Assert.AreEqual(0.5f, display.Fill01, 0.001f);
            Assert.AreEqual("50 / 100", display.Label);
            Assert.IsFalse(display.IsFainted);
        }

        private static ChainlingSnapshot CreateSnapshot(int currentHp)
        {
            return new ChainlingSnapshot
            {
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
