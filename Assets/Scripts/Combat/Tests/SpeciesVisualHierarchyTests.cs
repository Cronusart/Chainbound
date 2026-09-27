using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class SpeciesVisualHierarchyTests
    {
        [Test]
        public void Normalize_DoesNotRewriteSkinIdWhenEntangled()
        {
            var identity = new ChainlingVisualIdentity { visualVariantId = "chrome" };
            identity.Normalize(entangledFlag: true);
            Assert.AreEqual("chrome", identity.visualVariantId);
        }

        [Test]
        public void Normalize_LegacyEntangledSkinBecomesBase()
        {
            var identity = new ChainlingVisualIdentity
            {
                visualVariantId = ChainlingVisualIdentity.EntangledVariantId,
            };
            identity.Normalize(entangledFlag: true);
            Assert.AreEqual(ChainlingVisualIdentity.BaseVariantId, identity.visualVariantId);
        }

        [Test]
        public void Normalize_NonBodySentinelsBecomeBase()
        {
            var identity = new ChainlingVisualIdentity
            {
                visualVariantId = ChainlingVisualIdentity.DefaultId,
            };
            identity.Normalize();
            Assert.AreEqual(ChainlingVisualIdentity.BaseVariantId, identity.visualVariantId);

            identity.visualVariantId = ChainlingVisualIdentity.NoneId;
            identity.Normalize();
            Assert.AreEqual(ChainlingVisualIdentity.BaseVariantId, identity.visualVariantId);

            Assert.IsFalse(ChainlingVisualIdentity.IsBodySkinVariantId(ChainlingVisualIdentity.DefaultId));
            Assert.IsFalse(ChainlingVisualIdentity.IsBodySkinVariantId(ChainlingVisualIdentity.NoneId));
            Assert.IsFalse(ChainlingVisualIdentity.IsBodySkinVariantId(ChainlingVisualIdentity.EntangledVariantId));
            Assert.IsTrue(ChainlingVisualIdentity.IsBodySkinVariantId("chrome"));
            Assert.AreEqual(
                ChainlingVisualIdentity.BaseVariantId,
                ChainlingVisualIdentity.NormalizeBodySkinVariantId(ChainlingVisualIdentity.DefaultId));
        }

        [Test]
        public void VisualDatabase_CollectsBodyVariantIdsFromSetThenFlatEntries()
        {
            var set = ScriptableObject.CreateInstance<ChainlingSpeciesVisualSetSO>();
            set.speciesId = "validape";
            set.variants = new List<SpeciesVisualVariantEntry>
            {
                new SpeciesVisualVariantEntry
                {
                    variantId = ChainlingVisualIdentity.BaseVariantId,
                    rarity = RarityTier.Common,
                },
                new SpeciesVisualVariantEntry
                {
                    variantId = "chrome",
                    displayName = "Chrome",
                    rarity = RarityTier.Rare,
                },
            };

            var database = ScriptableObject.CreateInstance<CombatVisualDatabaseSO>();
            database.EditorAddOrReplaceVisualSet(set);
            database.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
            {
                speciesId = "validape",
                visualVariantId = "shadow",
                entangledType = ChainlingType.None,
                profile = ScriptableObject.CreateInstance<CombatVisualProfileSO>(),
            });
            database.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
            {
                speciesId = "validape",
                visualVariantId = "chrome",
                entangledType = ChainlingType.Burned,
                profile = ScriptableObject.CreateInstance<CombatVisualProfileSO>(),
            });
            database.RebuildLookup();

            List<string> ids = database.CollectBodyVariantIds("validape");
            Assert.AreEqual(3, ids.Count);
            Assert.Contains(ChainlingVisualIdentity.BaseVariantId, ids);
            Assert.Contains("chrome", ids);
            Assert.Contains("shadow", ids);
            Assert.IsTrue(database.TryGetCatalogRarity("validape", "chrome", out RarityTier rarity));
            Assert.AreEqual(RarityTier.Rare, rarity);
            Assert.IsTrue(database.ContainsBodyVariant("validape", "chrome"));
            Assert.IsFalse(database.ContainsBodyVariant("validape", "missing"));
        }

        [Test]
        public void IsEntangled_UsesSnapshotFlagOnly()
        {
            var snapshot = new ChainlingSnapshot
            {
                isEntangled = true,
                visualIdentity = new ChainlingVisualIdentity
                {
                    visualVariantId = "chrome",
                },
            };

            Assert.IsTrue(ChainlingProgressionIdentityUtility.IsEntangled(snapshot));
            Assert.IsFalse(ChainlingProgressionIdentityUtility.IsEntangled(snapshot.visualIdentity));
        }

        [Test]
        public void VisualDatabase_ResolvesVariantTimesTypeWithFallback()
        {
            var baseProfile = ScriptableObject.CreateInstance<CombatVisualProfileSO>();
            var burnedProfile = ScriptableObject.CreateInstance<CombatVisualProfileSO>();
            var database = ScriptableObject.CreateInstance<CombatVisualDatabaseSO>();
            database.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
            {
                speciesId = "kitsauto",
                visualVariantId = "chrome",
                entangledType = ChainlingType.None,
                profile = baseProfile,
            });
            database.EditorAddOrReplaceEntry(new CombatVisualDatabaseSO.Entry
            {
                speciesId = "kitsauto",
                visualVariantId = "chrome",
                entangledType = ChainlingType.Burned,
                profile = burnedProfile,
            });
            database.RebuildLookup();

            Assert.AreSame(burnedProfile, database.GetProfile("kitsauto", "chrome", ChainlingType.Burned));
            Assert.AreSame(baseProfile, database.GetProfile("kitsauto", "chrome", ChainlingType.Frozen));
            Assert.AreSame(baseProfile, database.GetProfile("kitsauto", "chrome", ChainlingType.None));
        }

        [Test]
        public void WildVariantRoller_PreservesSkinWhenEntangled()
        {
            var pool = new List<WeightedVariantEntry>
            {
                new WeightedVariantEntry { visualVariantId = "chrome", weight = 100 },
            };

            ChainlingVisualIdentity identity = WildVariantRoller.Roll(pool, new System.Random(1), isEntangled: true);
            Assert.AreEqual("chrome", identity.visualVariantId);
        }

        [Test]
        public void WildRoller_EntangledPicksTypeFromPool()
        {
            var config = new WildRollConfig
            {
                entangledChance = 1f,
                mutationChance = 0f,
                entangledTypePool = new List<ChainlingType> { ChainlingType.Burned },
            };

            WildTraits traits = WildRoller.RollTraits(config, new System.Random(7));
            Assert.IsTrue(traits.isEntangled);
            Assert.AreEqual(ChainlingType.Burned, traits.entangledType);
            Assert.IsFalse(traits.isMutated);
        }

        [Test]
        public void MutationAndVariantAndEntangled_CanStack()
        {
            var variantTable = ScriptableObject.CreateInstance<WildVariantRarityTableSO>();
            variantTable.entries = new List<WildVariantRarityEntry>
            {
                new WildVariantRarityEntry
                {
                    visualVariantId = "chrome",
                    rarity = RarityTier.Rare,
                    weight = 100,
                },
            };

            var mutationTable = ScriptableObject.CreateInstance<WildMutationRarityTableSO>();
            mutationTable.entries = new List<WildMutationRarityEntry>
            {
                new WildMutationRarityEntry
                {
                    mutationId = ChainlingVisualIdentity.NoneId,
                    rarity = RarityTier.Common,
                    weight = 1,
                },
                new WildMutationRarityEntry
                {
                    mutationId = "glow_mut",
                    rarity = RarityTier.Rare,
                    weight = 1000,
                },
            };

            var rng = new System.Random(42);
            var config = new WildRollConfig
            {
                entangledChance = 1f,
                mutationChance = 0.047f,
                entangledTypePool = new List<ChainlingType> { ChainlingType.Node },
            };

            WildTraits traits = WildRoller.RollTraits(config, rng);
            List<WeightedVariantEntry> variantPool = variantTable.BuildWeightedPool();
            ChainlingVisualIdentity visual = WildVariantRoller.Roll(variantPool, rng, traits.isEntangled);
            List<WeightedMutationEntry> mutationPool = mutationTable.BuildWeightedPool(null, 1f);
            string mutationId = WildMutationRoller.Roll(mutationPool, rng);
            traits.mutationId = mutationId;
            traits.isMutated = mutationId != ChainlingVisualIdentity.NoneId;
            if (traits.isMutated)
                visual.auraId = mutationId;

            Assert.IsTrue(traits.isEntangled);
            Assert.AreEqual(ChainlingType.Node, traits.entangledType);
            Assert.AreEqual("chrome", visual.visualVariantId);
            Assert.IsTrue(traits.isMutated);
            Assert.AreEqual("glow_mut", visual.auraId);
        }

        [Test]
        public void VariantTierMultipliers_ScaleRareWeights()
        {
            var table = ScriptableObject.CreateInstance<WildVariantRarityTableSO>();
            table.entries = new List<WildVariantRarityEntry>
            {
                new WildVariantRarityEntry
                {
                    visualVariantId = "base",
                    rarity = RarityTier.Common,
                    weight = 100,
                },
                new WildVariantRarityEntry
                {
                    visualVariantId = "chrome",
                    rarity = RarityTier.Rare,
                    weight = 10,
                },
            };

            float[] multipliers = EncounterRarityLeverUtility.NormalizeTierMultipliers(
                new[] { 1f, 1f, 10f, 1f, 1f, 1f });
            List<WeightedVariantEntry> pool = table.BuildWeightedPool(multipliers);

            WeightedVariantEntry chrome = pool.Find(e => e.visualVariantId == "chrome");
            Assert.IsNotNull(chrome);
            Assert.AreEqual(100, chrome.weight);
        }
    }
}
