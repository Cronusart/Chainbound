using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatIvRangeTests
    {
        [Test]
        public void NewFactoryRollsStayWithinLockedIvRange()
        {
            Assert.AreEqual(-5, CombatConstants.IV_MIN);
            Assert.AreEqual(5, CombatConstants.IV_MAX);

            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            try
            {
                species.speciesId = "iv-test";
                species.displayName = "IV Test";
                species.baseMaxHP = 100;
                species.baseMeleeAtk = 50;
                species.baseMeleeDef = 50;
                species.baseRangedAtk = 50;
                species.baseRangedDef = 50;
                species.baseSpeed = 50;

                var observed = new HashSet<int>();
                for (int seed = 0; seed < 128; seed++)
                {
                    ChainlingInstance chainling = CombatFactory.CreateChainling(
                        species,
                        stars: 1,
                        rebirthCount: 0,
                        evolutionStage: 0,
                        equippedMoves: null,
                        rng: new CombatRng(seed));
                    int[] ivs =
                    {
                        chainling.ivMaxHP,
                        chainling.ivMeleeAtk,
                        chainling.ivMeleeDef,
                        chainling.ivRangedAtk,
                        chainling.ivRangedDef,
                        chainling.ivSpeed,
                    };
                    for (int i = 0; i < ivs.Length; i++)
                    {
                        Assert.That(ivs[i], Is.InRange(CombatConstants.IV_MIN, CombatConstants.IV_MAX));
                        observed.Add(ivs[i]);
                    }
                }

                Assert.Contains(CombatConstants.IV_MIN, new List<int>(observed));
                Assert.Contains(CombatConstants.IV_MAX, new List<int>(observed));
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void SnapshotCopyClampsToLockedIvRangeWithoutMutatingSource()
        {
            int[] source = { -99, -5, -1, 1, 5, 99 };

            int[] copy = ChainlingSnapshotStatUtility.CopyIvs(source);

            CollectionAssert.AreEqual(new[] { -5, -5, -1, 1, 5, 5 }, copy);
            CollectionAssert.AreEqual(new[] { -99, -5, -1, 1, 5, 99 }, source);
        }
    }
}
