using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingRosterHpHydrationTests
    {
        [Test]
        public void BuildTeamFromParty_AppliesPersistedCurrentHp()
        {
            var speciesDb = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            var moveDb = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            var species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "validape";
            species.displayName = "Validape";
            species.baseMaxHP = 100;
            species.baseMeleeAtk = 10;
            species.baseMeleeDef = 10;
            species.baseRangedAtk = 10;
            species.baseRangedDef = 10;
            species.baseSpeed = 10;
            speciesDb.species = new List<SpeciesDefinitionSO> { species };
            speciesDb.BuildLookup();

            var party = new PartySnapshot
            {
                chainlings = new List<ChainlingSnapshot>
                {
                    new ChainlingSnapshot
                    {
                        speciesId = "validape",
                        stars = 0,
                        rebirthCount = 0,
                        currentHp = 42,
                        ivs = new[] { 0, 0, 0, 0, 0, 0 },
                    },
                },
            };

            TeamState team = RosterSlotUtility.BuildTeamFromParty(
                0,
                party,
                speciesDb,
                moveDb,
                battleSeed: 1,
                rng: new CombatRng(1));

            Assert.AreEqual(1, team.chainlings.Count);
            Assert.AreEqual(42, team.chainlings[0].currentHP);
            Assert.AreEqual(42, team.chainlings[0].displayHP);
        }
    }
}
