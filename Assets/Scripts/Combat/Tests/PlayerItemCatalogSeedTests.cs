using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class PlayerItemCatalogSeedTests
    {
        [Test]
        public void Seed_ContainsFieldCaptureFavorBinderyAndArchiveItems()
        {
            Assert.IsTrue(PlayerItemCatalogSeed.TryGet("chainling_update", out PlayerItemDefinition patch));
            Assert.AreEqual(PlayerItemCategory.Field, patch.category);

            Assert.IsTrue(PlayerItemCatalogSeed.TryGet("signal_orb", out PlayerItemDefinition orb));
            Assert.AreEqual(PlayerItemCategory.Capture, orb.category);

            Assert.IsTrue(PlayerItemCatalogSeed.TryGet("favor_boost_s", out PlayerItemDefinition boost));
            Assert.AreEqual(PlayerItemEffectFamily.FavorBoost, boost.effectFamily);
            Assert.AreEqual(PlayerItemCategory.Favor, boost.category);
            Assert.Greater(boost.favorPoints, 0);
            Assert.Greater(boost.favorDurationBattles, 0);

            Assert.IsFalse(PlayerItemCatalogSeed.TryGet("clarity_ampoule_s", out _));

            Assert.IsTrue(PlayerItemCatalogSeed.TryGet("stamp_solvent", out PlayerItemDefinition solvent));
            Assert.AreEqual(PlayerItemEffectFamily.StampErase, solvent.effectFamily);

            Assert.IsTrue(PlayerItemCatalogSeed.TryGet("archive_severance", out PlayerItemDefinition severance));
            Assert.AreEqual(PlayerItemOwnershipKind.CoreCollectible, severance.ownershipKind);
            Assert.AreEqual(PlayerItemEffectFamily.UnbindArchiveBook, severance.effectFamily);
        }

        [Test]
        public void CategoryKeys_ParseLegacyClarityAsFavor()
        {
            Assert.IsTrue(PlayerItemCategoryKeys.TryParse("clarity", out PlayerItemCategory category));
            Assert.AreEqual(PlayerItemCategory.Favor, category);
            Assert.AreEqual("favor", PlayerItemCategoryKeys.ToKey(PlayerItemCategory.Favor));
            Assert.AreEqual("favor_boost", PlayerItemCategoryKeys.ToKey(PlayerItemEffectFamily.FavorBoost));
        }

        [Test]
        public void CreateStack_CopiesCategoryAndOwnershipFromSeed()
        {
            MenuInventoryItemStack stack = PlayerItemCatalogSeed.CreateStack(
                "archive_key",
                1,
                "CoreAsset1111111111111111111111111111111");

            Assert.AreEqual("archive", stack.categoryKey);
            Assert.AreEqual(PlayerItemOwnershipKind.CoreCollectible, stack.ownershipKind);
            Assert.AreEqual("CoreAsset1111111111111111111111111111111", stack.coreAssetId);
        }

        [Test]
        public void Harness_SeedsBagInventoryWithCoreBackedArchiveTools()
        {
            var go = new UnityEngine.GameObject("PlayerItemCatalogSeedTests_Harness");
            try
            {
                ChainlingsMenuTestHarness harness = go.AddComponent<ChainlingsMenuTestHarness>();
                harness.ApplyGenerationSettings(
                    newSeed: 42,
                    rosterSize: 6,
                    partySize: CombatConstants.ROSTER_SIZE,
                    moveInventorySize: 48,
                    regenerateOnEnableFlag: false,
                    unlockCosmetics: true,
                    includeRebirthReady: true,
                    allowEntangled: true);
                harness.RegenerateTestRoster();

                InventorySnapshot bag = harness.FetchInventoryAsync("profile", "wallet").GetAwaiter().GetResult();
                Assert.NotNull(bag);
                Assert.Greater(bag.ramBalance, 0);
                Assert.NotNull(bag.stacks);
                Assert.Greater(bag.stacks.Count, 0);

                bool sawCore = false;
                bool sawProfile = false;
                bool sawFavor = false;
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    MenuInventoryItemStack stack = bag.stacks[i];
                    if (stack == null)
                        continue;
                    if (stack.ownershipKind == PlayerItemOwnershipKind.CoreCollectible &&
                        !string.IsNullOrWhiteSpace(stack.coreAssetId))
                    {
                        sawCore = true;
                    }

                    if (stack.ownershipKind == PlayerItemOwnershipKind.ProfileStack && stack.quantity > 0)
                        sawProfile = true;

                    if (stack.itemId != null && stack.itemId.StartsWith("favor_boost"))
                        sawFavor = true;
                }

                Assert.IsTrue(sawProfile, "Expected profile-stack consumables in harness bag.");
                Assert.IsTrue(sawCore, "Expected Core-backed archive tools in harness bag.");
                Assert.IsTrue(sawFavor, "Expected Favor Boost stacks in harness bag.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
