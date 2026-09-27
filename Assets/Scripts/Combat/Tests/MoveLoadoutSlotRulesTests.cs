using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class MoveLoadoutSlotRulesTests
    {
        [Test]
        public void ResolveMaxMoveSlots_UsesRebirthFormula()
        {
            Assert.AreEqual(4, MoveLoadoutSlotRules.ResolveMaxMoveSlots(0));
            Assert.AreEqual(5, MoveLoadoutSlotRules.ResolveMaxMoveSlots(1));
            Assert.AreEqual(9, MoveLoadoutSlotRules.ResolveMaxMoveSlots(5));
            Assert.AreEqual(9, MoveLoadoutSlotRules.ResolveMaxMoveSlots(99));
            Assert.AreEqual(4, MoveLoadoutSlotRules.ResolveMaxMoveSlots(-2));
        }

        [Test]
        public void ShouldScrollMoveList_OnlyAfterFourVisibleRows()
        {
            Assert.IsFalse(MoveLoadoutSlotRules.ShouldScrollMoveList(0));
            Assert.IsFalse(MoveLoadoutSlotRules.ShouldScrollMoveList(4));
            Assert.IsTrue(MoveLoadoutSlotRules.ShouldScrollMoveList(5));
            Assert.AreEqual(4, MoveLoadoutSlotRules.VisibleRowsBeforeScroll);
        }

        [Test]
        public void ChainlingLoadout_RejectsThirdPassive()
        {
            var loadout = new ChainlingLoadout { rebirthCount = 5 };
            loadout.equipped.Add(CreateMove("passive_a", MoveCategory.Passive, MoveTier.Stamped));
            loadout.equipped.Add(CreateMove("passive_b", MoveCategory.Passive, MoveTier.Stamped));

            LoadoutValidationResult result = loadout.CanEquip(CreateMove("passive_c", MoveCategory.Passive, MoveTier.Stamped));

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(LoadoutValidationError.PassivePerChainlingLimit, result.error);
        }

        [Test]
        public void ChainlingLoadout_AllowsMultipleStampedMoves()
        {
            var lowRebirth = new ChainlingLoadout { rebirthCount = 0 };
            lowRebirth.equipped.Add(CreateMove("stamped_a", MoveCategory.DamagingMelee, MoveTier.Stamped));

            LoadoutValidationResult lowResult = lowRebirth.CanEquip(CreateMove("stamped_b", MoveCategory.DamagingRanged, MoveTier.Stamped));

            Assert.IsTrue(lowResult.isValid);
        }

        [Test]
        public void CombatFactory_KeepsMoreThanFourMovesWhenRebirthAllows()
        {
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "loadout_test";
            species.displayName = "Loadout Test";
            species.baseMaxHP = 100;
            species.baseMeleeAtk = 10;
            species.baseMeleeDef = 10;
            species.baseRangedAtk = 10;
            species.baseRangedDef = 10;
            species.baseSpeed = 10;

            var moves = new List<MoveDefinitionSO>();
            for (int i = 0; i < 6; i++)
                moves.Add(CreateDefinition($"move_{i}"));

            ChainlingInstance instance = CombatFactory.CreateChainling(
                species,
                stars: 0,
                rebirthCount: 3,
                evolutionStage: 1,
                equippedMoves: moves);

            Assert.AreEqual(7, instance.maxSlots);
            Assert.AreEqual(6, instance.moves.Count);
        }

        private static MoveInstance CreateMove(string id, MoveCategory category, MoveTier tier)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                tier = tier,
                slotCostOverride = -1,
            };
        }

        private static MoveDefinitionSO CreateDefinition(string id)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = id;
            move.displayName = id;
            move.category = MoveCategory.DamagingMelee;
            move.baseType = ChainlingType.Gas;
            move.rarity = RarityTier.Common;
            move.targeting = MoveTargeting.SingleEnemy;
            move.contactType = ContactType.Contact;
            move.power = 10;
            move.apCost = 1;
            move.accuracy = 100;
            return move;
        }
    }
}
