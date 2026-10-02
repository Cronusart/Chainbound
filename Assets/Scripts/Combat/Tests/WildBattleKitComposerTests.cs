using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class WildBattleKitComposerTests
    {
        [Test]
        public void Compose_KeepsBaselineAndOnlyTypedDamage_IgnoresTypelessProgressionHit()
        {
            MoveDefinitionSO jab = Move("baseline_jab", ChainlingType.None, MoveCategory.DamagingMelee);
            MoveDefinitionSO seam = Move("seam_cut", ChainlingType.Shard, MoveCategory.DamagingMelee);
            MoveDefinitionSO isolation = Move("isolation_tool", ChainlingType.Shard, MoveCategory.StatusUtility);
            SpeciesDefinitionSO species = Species("crackett", jab, seam, isolation);

            bool sawPlaystyle = false;
            bool sawTwoMoveKit = false;
            for (int seed = 1; seed <= 40; seed++)
            {
                List<string> kit = WildBattleKitComposer.Compose(
                    new[] { "baseline_striker", "baseline_shot" },
                    species,
                    0,
                    false,
                    new System.Random(seed),
                    null,
                    null);

                Assert.AreEqual("baseline_striker", kit[0]);
                Assert.Contains("seam_cut", kit);
                Assert.IsFalse(kit.Contains("baseline_jab"));
                Assert.IsFalse(kit.Contains("baseline_shot"));
                Assert.LessOrEqual(kit.Count, 3);
                if (kit.Count == 2)
                    sawTwoMoveKit = true;
                if (kit.Contains("isolation_tool"))
                {
                    sawPlaystyle = true;
                    Assert.AreEqual(3, kit.Count);
                }
            }

            Assert.IsTrue(sawPlaystyle);
            Assert.IsTrue(sawTwoMoveKit);
        }

        [Test]
        public void Compose_UsesSignatureWhenItIsTheOnlyTypedHit()
        {
            MoveDefinitionSO blow = Move("bulwark_blow", ChainlingType.Block, MoveCategory.DamagingMelee);
            MoveDefinitionSO stance = Move("immovable_stance", ChainlingType.Block, MoveCategory.StatusUtility);
            SpeciesDefinitionSO species = Species("blockwyrm", blow, stance);

            List<string> kit = WildBattleKitComposer.Compose(
                new[] { "baseline_striker" },
                species,
                0,
                false,
                new System.Random(2),
                null,
                null);

            Assert.Contains("baseline_striker", kit);
            Assert.Contains("bulwark_blow", kit);
            Assert.LessOrEqual(kit.Count, 3);
        }

        [Test]
        public void Compose_UsesStarFiveWeightForTheTypedSlot()
        {
            MoveDefinitionSO common = Move("stake_strike", ChainlingType.Staked, MoveCategory.DamagingMelee);
            MoveDefinitionSO signature = Move("yield_spike", ChainlingType.Staked, MoveCategory.DamagingMelee);
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "hopoch";
            species.progressionMovePool = new List<SpeciesProgressionMoveEntry>
            {
                Entry(common, 1, true, 0),
                Entry(signature, 0, true, 1),
            };

            List<string> early = WildBattleKitComposer.Compose(
                new[] { "baseline_striker" },
                species,
                0,
                false,
                new System.Random(1),
                null,
                null);
            List<string> atFive = WildBattleKitComposer.Compose(
                new[] { "baseline_striker" },
                species,
                5,
                false,
                new System.Random(1),
                null,
                null);

            CollectionAssert.AreEqual(new[] { "baseline_striker", "stake_strike" }, early);
            CollectionAssert.AreEqual(new[] { "baseline_striker", "yield_spike" }, atFive);
        }

        [Test]
        public void Compose_DoesNotReplaceTheSeparateKnownRoll()
        {
            MoveDefinitionSO common = Move("stake_strike", ChainlingType.Staked, MoveCategory.DamagingMelee);
            MoveDefinitionSO signature = Move("patience_passive", ChainlingType.Staked, MoveCategory.Passive);
            SpeciesDefinitionSO species = Species("hopoch", common, signature);
            ChainlingProgressionRewardTablesSO open = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            open.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };

            string known = ProgressionKnownMoveRoll.TryRoll(species, 0, false, new System.Random(1), open, null);
            List<string> kit = WildBattleKitComposer.Compose(
                new[] { "baseline_striker" },
                species,
                0,
                false,
                new System.Random(1),
                null,
                null);

            Assert.IsTrue(known == "stake_strike" || known == "patience_passive");
            Assert.AreEqual("baseline_striker", kit[0]);
            Assert.Contains("stake_strike", kit);
            Assert.AreNotEqual(string.Join(",", kit), known);
        }

        private static SpeciesDefinitionSO Species(string id, params MoveDefinitionSO[] moves)
        {
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = id;
            species.progressionMovePool = new List<SpeciesProgressionMoveEntry>();
            for (int i = 0; i < moves.Length; i++)
                species.progressionMovePool.Add(Entry(moves[i], 10, false, 10));
            return species;
        }

        private static SpeciesProgressionMoveEntry Entry(
            MoveDefinitionSO move,
            int weight,
            bool useStarFive,
            int weightAtStarFive)
        {
            return new SpeciesProgressionMoveEntry
            {
                move = move,
                weight = weight,
                useWeightAtStarFive = useStarFive,
                weightAtStarFive = weightAtStarFive,
            };
        }

        private static MoveDefinitionSO Move(string id, ChainlingType type, MoveCategory category)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = id;
            move.displayName = id;
            move.baseType = type;
            move.category = category;
            move.rarity = RarityTier.Common;
            return move;
        }
    }
}
