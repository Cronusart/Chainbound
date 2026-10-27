using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class LibraryCardAcquisitionTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void EquippedMoves_KeepAcquisitionAcrossBlankSlots_AndStampDoesNotChangeTheFace()
        {
            SpeciesDatabaseSO speciesDb = CreateSpeciesDb();
            MoveDefinitionSO learned = CreateMove("star_jab");
            MoveDefinitionSO candy = CreateMove("candy_shot");
            MoveDefinitionSO plain = CreateMove("plain_slam");
            MoveDatabaseSO moveDb = Create<MoveDatabaseSO>();
            moveDb.moves = new List<MoveDefinitionSO> { learned, candy, plain };
            moveDb.BuildLookup();

            var party = new PartySnapshot
            {
                chainlings = new List<ChainlingSnapshot>
                {
                    new ChainlingSnapshot
                    {
                        speciesId = "validape",
                        stars = 0,
                        rebirthCount = 0,
                        ivs = new[] { 0, 0, 0, 0, 0, 0 },
                        moveIds = new List<string> { "", "star_jab", "candy_shot", "plain_slam" },
                        moveAcquisitions = new List<string>
                        {
                            LibraryCardFaces.CandyRoll,
                            LibraryCardFaces.ProgressionLearn,
                            LibraryCardFaces.CandyRoll,
                        },
                    },
                },
            };

            TeamState team = RosterSlotUtility.BuildTeamFromParty(
                0,
                party,
                speciesDb,
                moveDb,
                battleSeed: 3,
                rng: new CombatRng(3));

            Assert.AreEqual(3, team.chainlings[0].moves.Count);
            MoveInstance learnedMove = team.chainlings[0].moves[0];
            MoveInstance candyMove = team.chainlings[0].moves[1];
            MoveInstance plainMove = team.chainlings[0].moves[2];

            learnedMove.tier = MoveTier.Stamped;
            learnedMove.modifier1SectionId = "2.1-a";

            Assert.AreEqual(LibraryCardFaces.ProgressionLearn, learnedMove.acquisition);
            Assert.IsTrue(LibraryCardFaces.IsLearned(learnedMove.acquisition));
            Assert.AreEqual(LibraryCardFaces.CandyRoll, candyMove.acquisition);
            Assert.IsFalse(LibraryCardFaces.IsLearned(candyMove.acquisition));
            Assert.IsFalse(LibraryCardFaces.IsLearned(plainMove.acquisition));
            Assert.IsFalse(LibraryCardFaces.IsLearned(LibraryCardFaces.EventGrant));
        }

        SpeciesDatabaseSO CreateSpeciesDb()
        {
            SpeciesDatabaseSO speciesDb = Create<SpeciesDatabaseSO>();
            SpeciesDefinitionSO species = Create<SpeciesDefinitionSO>();
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
            return speciesDb;
        }

        MoveDefinitionSO CreateMove(string moveId)
        {
            MoveDefinitionSO move = Create<MoveDefinitionSO>();
            move.moveId = moveId;
            move.displayName = moveId;
            move.category = MoveCategory.DamagingMelee;
            move.targeting = MoveTargeting.SingleEnemy;
            move.contactType = ContactType.Contact;
            move.power = 20;
            move.apCost = 1;
            move.accuracy = 100;
            return move;
        }

        T Create<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }
    }
}
