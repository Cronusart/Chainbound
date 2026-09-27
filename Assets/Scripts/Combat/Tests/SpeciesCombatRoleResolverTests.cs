using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class SpeciesCombatRoleResolverTests
    {
        [Test]
        public void Resolve_OverrideWinsOverBaseStats()
        {
            SpeciesDefinitionSO species = CreateSpecies(
                meleeAtk: 40,
                meleeDef: 10,
                rangedAtk: 10,
                rangedDef: 10,
                speed: 10);
            species.combatRoleOverride = SpeciesCombatRole.Support;

            Assert.AreEqual(SpeciesCombatRole.Support, SpeciesCombatRoleResolver.Resolve(species));
            Assert.AreEqual("Support Warrior", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.Support));
        }

        [Test]
        public void Resolve_MeleeStriker_FromOffenseDominantBaseStats()
        {
            SpeciesDefinitionSO species = CreateSpecies(
                meleeAtk: 40,
                meleeDef: 12,
                rangedAtk: 10,
                rangedDef: 10,
                speed: 12);

            Assert.AreEqual(SpeciesCombatRole.MeleeStriker, SpeciesCombatRoleResolver.Resolve(species));
            Assert.AreEqual("Melee Striker", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.MeleeStriker));
        }

        [Test]
        public void Resolve_Speed_FromSpeedDominantBaseStats()
        {
            SpeciesDefinitionSO species = CreateSpecies(
                meleeAtk: 12,
                meleeDef: 12,
                rangedAtk: 12,
                rangedDef: 12,
                speed: 36);

            Assert.AreEqual(SpeciesCombatRole.Speed, SpeciesCombatRoleResolver.Resolve(species));
        }

        [Test]
        public void Resolve_Balanced_WhenNoAxisDominates()
        {
            SpeciesDefinitionSO species = CreateSpecies(
                meleeAtk: 18,
                meleeDef: 17,
                rangedAtk: 16,
                rangedDef: 17,
                speed: 18);

            Assert.AreEqual(SpeciesCombatRole.Balanced, SpeciesCombatRoleResolver.Resolve(species));
        }

        [Test]
        public void FormatLabel_CoversV1Set()
        {
            Assert.AreEqual("Melee Guardian", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.MeleeGuard));
            Assert.AreEqual("Ranged Striker", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.RangedStriker));
            Assert.AreEqual("Ranged Guardian", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.RangedGuard));
            Assert.AreEqual("Utility Disruptor", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.Disrupt));
            Assert.AreEqual("Speedy Striker", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.Speed));
            Assert.AreEqual("All-Rounder", SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRole.Balanced));
        }

        private static SpeciesDefinitionSO CreateSpecies(
            int meleeAtk,
            int meleeDef,
            int rangedAtk,
            int rangedDef,
            int speed)
        {
            var species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "test_species";
            species.displayName = "Test";
            species.baseMeleeAtk = meleeAtk;
            species.baseMeleeDef = meleeDef;
            species.baseRangedAtk = rangedAtk;
            species.baseRangedDef = rangedDef;
            species.baseSpeed = speed;
            species.baseMaxHP = 100;
            species.combatRoleOverride = SpeciesCombatRole.Unspecified;
            return species;
        }
    }
}
