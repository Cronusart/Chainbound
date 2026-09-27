using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class StubPlayerItemUseFieldHealTests
    {
        [Test]
        public void UseTonic_HealsDamagedPartyMemberAndConsumesStack()
        {
            var go = new GameObject("StubPlayerItemUseFieldHealTests");
            try
            {
                ChainlingsMenuTestHarness harness = go.AddComponent<ChainlingsMenuTestHarness>();
                SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
                species.speciesId = "validape";
                species.displayName = "Validape";
                species.baseMaxHP = 100;
                species.baseMeleeAtk = 10;
                species.baseMeleeDef = 10;
                species.baseRangedAtk = 10;
                species.baseRangedDef = 10;
                species.baseSpeed = 10;

                SpeciesDatabaseSO db = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
                db.species.Add(species);
                db.BuildLookup();
                harness.Configure(db, null, null, null);
                harness.ApplyGenerationSettings(
                    newSeed: 11,
                    rosterSize: 3,
                    partySize: 3,
                    moveInventorySize: 8,
                    regenerateOnEnableFlag: false,
                    unlockCosmetics: false,
                    includeRebirthReady: false,
                    allowEntangled: false);
                harness.RegenerateTestRoster();

                PlayerRosterSnapshot roster = harness.CachedRoster;
                Assert.NotNull(roster);
                Assert.Greater(roster.chainlings.Count, 0);

                PlayerRosterEntry entry = roster.chainlings[0];
                entry.isPartyMember = true;
                entry.snapshot.speciesId = "validape";
                entry.snapshot.currentHp = 20;
                string targetId = entry.snapshot.backendChainlingId;
                Assert.IsFalse(string.IsNullOrWhiteSpace(targetId));

                InventorySnapshot bag = harness.FetchInventoryAsync("p", "w").GetAwaiter().GetResult();
                int beforeQty = CountItem(bag, "chainling_update");
                Assert.Greater(beforeQty, 0);

                StubPlayerItemUseService use = go.AddComponent<StubPlayerItemUseService>();
                FavorService favor = go.AddComponent<FavorService>();
                use.Configure(harness, favor, null, harness, db);

                PlayerItemUseResult result = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest
                    {
                        itemId = "chainling_update",
                        targetChainlingId = targetId,
                    }).GetAwaiter().GetResult();

                Assert.IsTrue(result.success, result.error);
                Assert.AreEqual(90, entry.snapshot.currentHp);
                Assert.AreEqual(beforeQty - 1, CountItem(result.inventory, "chainling_update"));

                entry.snapshot.currentHp = ChainlingVitalsUtility.BuildMaxHp(entry.snapshot, species);
                int qtyAfterHeal = CountItem(result.inventory, "chainling_update");
                PlayerItemUseResult rejected = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest
                    {
                        itemId = "chainling_update",
                        targetChainlingId = targetId,
                    }).GetAwaiter().GetResult();

                Assert.IsFalse(rejected.success);
                Assert.AreEqual("already_full_hp", rejected.error);

                // Working inventory should be unchanged after a rejected use.
                PlayerItemUseResult favorUse = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest { itemId = "favor_boost_s" }).GetAwaiter().GetResult();
                Assert.IsTrue(favorUse.success, favorUse.error);
                Assert.AreEqual(qtyAfterHeal, CountItem(favorUse.inventory, "chainling_update"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UseFavorBoost_SetsScoreWithoutTarget()
        {
            var go = new GameObject("StubPlayerItemUseFavorTests");
            try
            {
                StubInventoryService inventory = go.AddComponent<StubInventoryService>();
                StubPlayerItemUseService use = go.AddComponent<StubPlayerItemUseService>();
                FavorService favor = go.AddComponent<FavorService>();
                use.Configure(inventory, favor);

                PlayerItemUseResult result = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest { itemId = "favor_boost_s" }).GetAwaiter().GetResult();

                Assert.IsTrue(result.success, result.error);
                Assert.AreEqual(100, result.favorScore);
                Assert.AreEqual(100, favor.CurrentFavorScore);
                Assert.AreEqual(3, favor.AmpouleBattlesRemaining);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static int CountItem(InventorySnapshot bag, string itemId)
        {
            if (bag?.stacks == null)
                return 0;
            int total = 0;
            for (int i = 0; i < bag.stacks.Count; i++)
            {
                if (bag.stacks[i] != null && bag.stacks[i].itemId == itemId)
                    total += bag.stacks[i].quantity;
            }
            return total;
        }
    }
}
