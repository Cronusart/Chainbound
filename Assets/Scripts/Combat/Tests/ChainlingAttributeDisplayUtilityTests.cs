using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingAttributeDisplayUtilityTests
    {
        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ChainlingAttributeDisplayUtility.ClearCatalogRadarRangeCache();
#endif
        }

        [Test]
        public void BuildPartyStatValues_UsesCycleFlats()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(stars: 5, rebirth: 1, ivOffset: 2, pickMatk: 1);
            SpeciesDefinitionSO species = CreateSpecies("a", 20, 18, 15, 14, 22);

            var values = ChainlingAttributeDisplayUtility.BuildPartyStatValues(snapshot, species);

            Assert.AreEqual(5, values.Count);
            // 20 + IV2 + auto1 + pick1 = 24 (stars ignored)
            Assert.AreEqual(24, values[0].Total);
            Assert.AreEqual("MATK", values[0].Label);
        }

        [Test]
        public void BuildBreakdownSections_ShowBaseIvCycleAutoAndPick()
        {
            ChainlingSnapshot snapshot = CreateSnapshot(stars: 2, rebirth: 1, ivOffset: 1, pickMatk: 1);
            SpeciesDefinitionSO species = CreateSpecies("a", 20, 18, 15, 14, 22);

            var sections = ChainlingAttributeDisplayUtility.BuildBreakdownSections(snapshot, species);
            var values = ChainlingAttributeDisplayUtility.BuildPartyStatValues(snapshot, species);

            Assert.AreEqual(5, sections.Count);
            Assert.AreEqual(1, sections[0].cycleAutoFlat);
            Assert.AreEqual(1, sections[0].cyclePickFlat);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(values[i].Total, sections[i].total, sections[i].label);
        }

        [Test]
        public void BuildMaxHp_AppliesAutoAndPickFlats()
        {
            var snapshot = new ChainlingSnapshot
            {
                speciesId = "a",
                stars = 5,
                rebirthCount = 1,
                ivs = new[] { 0, 0, 0, 0, 0, 0 },
                rebirthIvBonuses = new[] { 1, 0, 0, 0, 0, 0 },
            };
            SpeciesDefinitionSO species = CreateSpecies("a", 20, 18, 15, 14, 22);

            // 100 + 0 + auto 5% + pick 5% = 110
            Assert.AreEqual(110, ChainlingAttributeDisplayUtility.BuildMaxHp(snapshot, species));
        }

        [Test]
        public void BuildPartyRadarAxes_UsesVisualVertexOrder()
        {
            var axes = ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(
                CreateSnapshot(stars: 1, rebirth: 0, ivOffset: 2),
                CreateSpecies("a", 20, 18, 15, 14, 22));

            Assert.AreEqual(5, axes.Count);
            Assert.AreEqual("SPD", axes[0].Label);
            Assert.AreEqual("MDEF", axes[1].Label);
            Assert.AreEqual("MATK", axes[2].Label);
            Assert.AreEqual("RATK", axes[3].Label);
            Assert.AreEqual("RDEF", axes[4].Label);
        }

        [Test]
        public void BuildCatalogRadarRange_UsesFlatCeilAndQuarterFloor_IgnoresZeroBases()
        {
            SpeciesDefinitionSO ghost = CreateSpecies("ghost", 0, 0, 0, 0, 0);
            SpeciesDefinitionSO low = CreateSpecies("low", 20, 22, 18, 19, 21);
            SpeciesDefinitionSO high = CreateSpecies("high", 70, 60, 65, 55, 75);
            SpeciesDatabaseSO db = CreateDatabase(ghost, low, high);

            ChainlingRadarCatalogRange range =
                ChainlingAttributeDisplayUtility.BuildCatalogRadarRange(db, floorFactor: 0.25f);

            Assert.IsTrue(range.IsValid);

            int expectedCeil = ChainlingAttributeDisplayUtility.ComputeRadarRim(
                75,
                CombatConstants.REBIRTH_MAX);
            Assert.AreEqual(expectedCeil, Mathf.RoundToInt(range.Ceil));

            int expectedMinPossible = Mathf.Max(1, 18 + CombatConstants.IV_MIN);
            Assert.AreEqual(expectedMinPossible * 0.25f, range.Floor, 0.01f);
        }

        [Test]
        public void BuildPartyRadarAxes_CatalogNormalize_HigherTotalHigherFill()
        {
            SpeciesDefinitionSO species = CreateSpecies("high", 70, 60, 65, 55, 75);
            SpeciesDatabaseSO db = CreateDatabase(species);

            var lowIv = new ChainlingSnapshot
            {
                speciesId = "high",
                stars = 2,
                rebirthCount = 1,
                ivs = new[] { 0, 0, 0, 0, 0, 0 },
                rebirthIvBonuses = new int[6],
            };
            var highMatk = new ChainlingSnapshot
            {
                speciesId = "high",
                stars = 2,
                rebirthCount = 1,
                ivs = new[] { 0, 10, 0, 0, 0, 0 },
                rebirthIvBonuses = new int[6],
            };

            var lowAxes = ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(lowIv, species, db);
            var highAxes = ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(highMatk, species, db);
            int matk = IndexOfLabel(highAxes, "MATK");

            Assert.Greater(highAxes[matk].Fill01, lowAxes[matk].Fill01);
            Assert.Less(highAxes[matk].Fill01, 1f);
        }

        [Test]
        public void ComputeRadarRim_UsesIvMaxPlusCycleAutoAndAllPicks()
        {
            int rebirth = 2;
            int rim = ChainlingAttributeDisplayUtility.ComputeRadarRim(20, rebirth);
            int expected = 20 + CombatConstants.IV_MAX
                + rebirth * CombatConstants.CYCLE_AUTO_STAT_FLAT
                + rebirth * CombatConstants.CYCLE_PICK_STAT_FLAT;
            Assert.AreEqual(expected, rim);
        }

        private static int IndexOfLabel(IReadOnlyList<ChainlingAttributeRadarAxis> axes, string label)
        {
            for (int i = 0; i < axes.Count; i++)
            {
                if (axes[i].Label == label)
                    return i;
            }

            Assert.Fail($"Missing radar axis '{label}'");
            return -1;
        }

        private static ChainlingSnapshot CreateSnapshot(
            int stars,
            int rebirth,
            int ivOffset,
            int pickMatk = 0)
        {
            return new ChainlingSnapshot
            {
                speciesId = "validape",
                stars = stars,
                rebirthCount = rebirth,
                ivs = new[] { ivOffset, ivOffset, ivOffset, ivOffset, ivOffset, ivOffset },
                rebirthIvBonuses = new[] { 0, pickMatk, 0, 0, 0, 0 },
            };
        }

        private static SpeciesDefinitionSO CreateSpecies(
            string id,
            int matk,
            int mdef,
            int ratk,
            int rdef,
            int spd)
        {
            var species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.displayName = id;
            species.baseMaxHP = 100;
            species.baseMeleeAtk = matk;
            species.baseMeleeDef = mdef;
            species.baseRangedAtk = ratk;
            species.baseRangedDef = rdef;
            species.baseSpeed = spd;
            return species;
        }

        private static SpeciesDatabaseSO CreateDatabase(params SpeciesDefinitionSO[] species)
        {
            var db = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            db.species = new List<SpeciesDefinitionSO>(species);
            return db;
        }
    }
}
