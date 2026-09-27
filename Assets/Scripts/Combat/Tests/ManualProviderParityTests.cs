using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class ManualProviderParityTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Builder_WritesCyclePicksAndHonorsZeroIvs()
        {
            var entry = new ManualChainlingEntry
            {
                speciesId = "validape",
                stars = 3,
                rebirthCount = 2,
                pickMaxHP = 1,
                pickMeleeAtk = 1,
                ivMaxHP = 0,
                ivMeleeAtk = 0,
                ivMeleeDef = 0,
                ivRangedAtk = 0,
                ivRangedDef = 0,
                ivSpeed = 0,
            };

            ChainlingSnapshot snap = ManualChainlingSnapshotBuilder.Build(entry, logPickMismatch: false);
            Assert.AreEqual(3, snap.stars);
            Assert.AreEqual(2, snap.rebirthCount);
            Assert.AreEqual(0, snap.level);
            Assert.AreEqual(6, snap.ivs.Length);
            Assert.AreEqual(0, snap.ivs[0]);
            Assert.AreEqual(1, snap.rebirthIvBonuses[0]);
            Assert.AreEqual(1, snap.rebirthIvBonuses[1]);
        }

        [Test]
        public void Builder_TruncatesExcessCyclePicks()
        {
            var entry = new ManualChainlingEntry
            {
                speciesId = "validape",
                stars = 1,
                rebirthCount = 1,
                pickMaxHP = 2,
                pickMeleeAtk = 2,
            };

            ChainlingSnapshot snap = ManualChainlingSnapshotBuilder.Build(entry, logPickMismatch: false);
            int sum = 0;
            for (int i = 0; i < snap.rebirthIvBonuses.Length; i++)
                sum += snap.rebirthIvBonuses[i];
            Assert.AreEqual(1, sum);
        }

        [Test]
        public void SpeciesDefinition_BaseCombatStatTotal_ExcludesMaxHp()
        {
            var species = CreateAsset<SpeciesDefinitionSO>();
            species.baseMaxHP = 999;
            species.baseMeleeAtk = 10;
            species.baseMeleeDef = 20;
            species.baseRangedAtk = 30;
            species.baseRangedDef = 40;
            species.baseSpeed = 50;
            Assert.AreEqual(150, species.BaseCombatStatTotal);
        }

        [Test]
        public void RosterSlotUtility_HonorsAllZeroAuthoredIvs()
        {
            SpeciesDatabaseSO speciesDb = CreateSpeciesDb("validape", baseMaxHp: 100);
            MoveDatabaseSO moveDb = CreateAsset<MoveDatabaseSO>();

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
                    },
                },
            };

            TeamState team = RosterSlotUtility.BuildTeamFromParty(
                0,
                party,
                speciesDb,
                moveDb,
                battleSeed: 99,
                rng: new CombatRng(99));

            Assert.AreEqual(1, team.chainlings.Count);
            ChainlingInstance c = team.chainlings[0];
            Assert.AreEqual(0, c.ivMaxHP);
            Assert.AreEqual(0, c.ivMeleeAtk);
            Assert.AreEqual(0, c.ivMeleeDef);
            Assert.AreEqual(0, c.ivRangedAtk);
            Assert.AreEqual(0, c.ivRangedDef);
            Assert.AreEqual(0, c.ivSpeed);
        }

        [Test]
        public void RosterSlotUtility_EmptySnapshotUsesOnlyAuthoredInitialLoadout()
        {
            SpeciesDatabaseSO speciesDb = CreateSpeciesDb("starter", baseMaxHp: 100);
            SpeciesDefinitionSO species = speciesDb.GetById("starter");
            MoveDefinitionSO baseline = CreateMove("baseline_striker", 40);
            MoveDefinitionSO exclusive = CreateMove("exclusive_finisher", 120);
            species.initialMoves = new List<MoveDefinitionSO> { baseline };
            species.exclusiveMoves = new List<MoveDefinitionSO> { exclusive };
            species.learnableMoves = new List<MoveDefinitionSO> { baseline, exclusive };

            MoveDatabaseSO moveDb = CreateAsset<MoveDatabaseSO>();
            moveDb.moves = new List<MoveDefinitionSO> { baseline, exclusive };
            moveDb.BuildLookup();

            var party = new PartySnapshot
            {
                chainlings = new List<ChainlingSnapshot>
                {
                    new ChainlingSnapshot
                    {
                        speciesId = "starter",
                        moveIds = new List<string>(),
                        ivs = new[] { 0, 0, 0, 0, 0, 0 },
                    },
                },
            };

            TeamState team = RosterSlotUtility.BuildTeamFromParty(
                0,
                party,
                speciesDb,
                moveDb,
                battleSeed: 7,
                rng: new CombatRng(7));

            Assert.AreEqual(1, team.chainlings.Count);
            Assert.AreEqual(1, team.chainlings[0].moves.Count);
            Assert.AreEqual("baseline_striker", team.chainlings[0].moves[0].definitionId);
        }

        [Test]
        public void Builder_CyclePicksContributeToMaxHpViaProgressionConfig()
        {
            SpeciesDatabaseSO speciesDb = CreateSpeciesDb("tank", baseMaxHp: 100);
            MoveDatabaseSO moveDb = CreateAsset<MoveDatabaseSO>();

            var entry = new ManualChainlingEntry
            {
                speciesId = "tank",
                stars = 2,
                rebirthCount = 1,
                pickMaxHP = 1,
            };
            ChainlingSnapshot snap = ManualChainlingSnapshotBuilder.Build(entry, logPickMismatch: false);

            var party = new PartySnapshot
            {
                chainlings = new List<ChainlingSnapshot> { snap },
            };

            TeamState team = RosterSlotUtility.BuildTeamFromParty(
                0,
                party,
                speciesDb,
                moveDb,
                battleSeed: 1,
                rng: new CombatRng(1));

            // base 100 + auto 5% + pick 5% = 110
            Assert.AreEqual(110, team.chainlings[0].EffectiveMaxHP);
        }

        private SpeciesDatabaseSO CreateSpeciesDb(string speciesId, int baseMaxHp)
        {
            var speciesDb = CreateAsset<SpeciesDatabaseSO>();
            var species = CreateAsset<SpeciesDefinitionSO>();
            species.speciesId = speciesId;
            species.displayName = speciesId;
            species.baseMaxHP = baseMaxHp;
            species.baseMeleeAtk = 10;
            species.baseMeleeDef = 10;
            species.baseRangedAtk = 10;
            species.baseRangedDef = 10;
            species.baseSpeed = 10;
            speciesDb.species = new List<SpeciesDefinitionSO> { species };
            speciesDb.BuildLookup();
            return speciesDb;
        }

        private MoveDefinitionSO CreateMove(string moveId, int power)
        {
            MoveDefinitionSO move = CreateAsset<MoveDefinitionSO>();
            move.moveId = moveId;
            move.displayName = moveId;
            move.category = MoveCategory.DamagingMelee;
            move.targeting = MoveTargeting.SingleEnemy;
            move.contactType = ContactType.Contact;
            move.power = power;
            move.apCost = 1;
            move.accuracy = 100;
            return move;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }
    }
}
