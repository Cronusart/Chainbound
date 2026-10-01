using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class SpeciesMovePoolRulesTests
    {
        [Test]
        public void Equip_AllowsTypelessPrimaryEntangledAndOffType_BlocksSecondaryAndForeignExclusive()
        {
            MoveDefinitionSO typeless = Move("typeless_slam", ChainlingType.None);
            MoveDefinitionSO mined = Move("mined_strike", ChainlingType.Mined);
            MoveDefinitionSO gas = Move("gas_shot", ChainlingType.Gas);
            MoveDefinitionSO gasOther = Move("gas_other", ChainlingType.Gas);
            MoveDefinitionSO node = Move("node_jab", ChainlingType.Node);
            MoveDefinitionSO exclusive = Move("starter_only", ChainlingType.Node);

            SpeciesDefinitionSO kodiax = Species("kodiax", ChainlingType.Mined);
            kodiax.secondaryType = ChainlingType.Gas;
            kodiax.offTypeMoveExceptions = new List<MoveDefinitionSO> { gas };

            SpeciesDefinitionSO validape = Species("validape", ChainlingType.Node);
            validape.exclusiveMoves = new List<MoveDefinitionSO> { exclusive };

            SpeciesDatabaseSO database = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            database.species = new List<SpeciesDefinitionSO> { kodiax, validape };

            Assert.IsTrue(CanEquip(kodiax, false, ChainlingType.None, typeless, database).isValid);
            Assert.IsTrue(CanEquip(kodiax, false, ChainlingType.None, mined, database).isValid);
            Assert.IsFalse(CanEquip(kodiax, false, ChainlingType.None, node, database).isValid);
            Assert.IsTrue(CanEquip(kodiax, true, ChainlingType.Node, node, database).isValid);
            Assert.IsTrue(CanEquip(kodiax, false, ChainlingType.None, gas, database).isValid);
            Assert.IsFalse(CanEquip(kodiax, false, ChainlingType.None, gasOther, database).isValid);
            Assert.AreEqual(
                LoadoutValidationError.ExclusiveSpeciesMismatch,
                CanEquip(kodiax, true, ChainlingType.Node, exclusive, database).error);
            Assert.IsTrue(CanEquip(validape, false, ChainlingType.None, exclusive, database).isValid);

            SpeciesDefinitionSO meme = Species("ghost", ChainlingType.Meme);
            Assert.IsTrue(CanEquip(meme, false, ChainlingType.None, mined, null).isValid);
        }

        [Test]
        public void CaptureRoll_UsesStarGateAndStarFiveWeight()
        {
            MoveDefinitionSO common = Move("common_shot", ChainlingType.Mined);
            MoveDefinitionSO signature = Move("signature_crash", ChainlingType.Mined);
            SpeciesDefinitionSO species = Species("kodiax", ChainlingType.Mined);
            species.progressionMovePool = new List<SpeciesProgressionMoveEntry>
            {
                new SpeciesProgressionMoveEntry
                {
                    move = common,
                    weight = 0,
                    useWeightAtStarFive = true,
                    weightAtStarFive = 1,
                },
                new SpeciesProgressionMoveEntry
                {
                    move = signature,
                    weight = 1,
                    useWeightAtStarFive = true,
                    weightAtStarFive = 0,
                    isSignature = true,
                },
            };

            ChainlingProgressionRewardTablesSO open = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            open.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };

            string early = ProgressionKnownMoveRoll.TryRoll(species, 0, false, new System.Random(1), open, null);
            Assert.AreEqual("signature_crash", early);

            string atFive = ProgressionKnownMoveRoll.TryRoll(species, 5, false, new System.Random(1), open, null);
            Assert.AreEqual("common_shot", atFive);

            ChainlingProgressionRewardTablesSO closed = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            closed.starKnownMoveChanceStandard = new[] { 0, 100, 100, 100, 100 };
            string missed = ProgressionKnownMoveRoll.TryRoll(species, 0, false, new System.Random(1), closed, null);
            Assert.AreEqual("", missed);
        }

        private static LoadoutValidationResult CanEquip(
            SpeciesDefinitionSO species,
            bool isEntangled,
            ChainlingType entangledType,
            MoveDefinitionSO move,
            SpeciesDatabaseSO database)
        {
            return SpeciesMoveEquipRules.Evaluate(
                species,
                isEntangled,
                entangledType,
                move.moveId,
                move.baseType,
                database);
        }

        private static SpeciesDefinitionSO Species(string id, ChainlingType type)
        {
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.primaryType = type;
            species.progressionMovePool = new List<SpeciesProgressionMoveEntry>();
            species.offTypeMoveExceptions = new List<MoveDefinitionSO>();
            species.exclusiveMoves = new List<MoveDefinitionSO>();
            return species;
        }

        private static MoveDefinitionSO Move(string id, ChainlingType type)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = id;
            move.displayName = id;
            move.baseType = type;
            move.rarity = RarityTier.Common;
            move.category = MoveCategory.DamagingMelee;
            return move;
        }
    }
}
