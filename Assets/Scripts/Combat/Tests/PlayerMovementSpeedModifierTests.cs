using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class PlayerMovementSpeedModifierTests
    {
        [SetUp]
        public void ResetHosts()
        {
            PlayerMovementSpeedModifierRuntime.ClearHosts();
        }

        [TearDown]
        public void ClearHosts()
        {
            PlayerMovementSpeedModifierRuntime.ClearHosts();
        }

        [Test]
        public void Consumable_IsFifteenPercentForThirtySeconds()
        {
            Assert.AreEqual(0.15f, PlayerMovementSpeedConsumable.BonusFraction);
            Assert.AreEqual(30f, PlayerMovementSpeedConsumable.DurationSeconds);
            Assert.AreEqual("nitro_brew", PlayerMovementSpeedConsumable.ItemId);

            Assert.IsTrue(PlayerItemCatalogSeed.TryGet(PlayerMovementSpeedConsumable.ItemId, out PlayerItemDefinition tonic));
            Assert.AreEqual(PlayerItemEffectFamily.MovementSpeedBoost, tonic.effectFamily);
            Assert.AreEqual(PlayerItemCategory.Field, tonic.category);
            Assert.IsTrue(tonic.usableInMenu);
            Assert.IsFalse(tonic.usableInBattle);

            string label = InventoryItemEffectCopy.FormatEffectLabel(tonic);
            StringAssert.Contains("15%", label);
            StringAssert.Contains("30", label);
            Assert.AreEqual(
                InventoryItemCeremonyId.SelfConfirm,
                InventoryItemCeremonyResolver.Resolve(tonic));
        }

        [Test]
        public void Expiry_RestoresBaselineWhileAnotherSourceStays()
        {
            var pipeline = new PlayerMovementSpeedModifierPipeline();
            const float now = 0f;
            const float wearable = 0.10f;

            Assert.IsTrue(pipeline.SetSource(
                "wearable.example_boots",
                PlayerMovementSpeedModifierKind.Wearable,
                wearable,
                0f,
                now));
            float withWearable = pipeline.SpeedMultiplier;

            Assert.IsTrue(pipeline.SetSource(
                PlayerMovementSpeedConsumable.SourceId,
                PlayerMovementSpeedModifierKind.Consumable,
                PlayerMovementSpeedConsumable.BonusFraction,
                PlayerMovementSpeedConsumable.DurationSeconds,
                now));

            float boosted = pipeline.SpeedMultiplier;
            Assert.AreEqual(1f + wearable + PlayerMovementSpeedConsumable.BonusFraction, boosted, 0.000001f);
            Assert.AreEqual(2, pipeline.SourceCount);

            Assert.AreEqual(0, pipeline.Expire(PlayerMovementSpeedConsumable.DurationSeconds - 0.01f));
            Assert.AreEqual(boosted, pipeline.SpeedMultiplier, 0.000001f);

            Assert.AreEqual(1, pipeline.Expire(PlayerMovementSpeedConsumable.DurationSeconds));
            Assert.AreEqual(withWearable, pipeline.SpeedMultiplier, 0f);
            Assert.AreEqual(1f + wearable, pipeline.SpeedMultiplier, 0.000001f);
            Assert.IsFalse(pipeline.TryGetSource(PlayerMovementSpeedConsumable.SourceId, out _));
            Assert.IsTrue(pipeline.TryGetSource("wearable.example_boots", out PlayerMovementSpeedModifierState boots));
            Assert.IsFalse(boots.Expires);
            Assert.AreEqual(wearable, boots.BonusFraction, 0f);
        }

        [Test]
        public void SameSource_RefreshesInsteadOfStacking()
        {
            var pipeline = new PlayerMovementSpeedModifierPipeline();
            pipeline.SetSource(
                PlayerMovementSpeedConsumable.SourceId,
                PlayerMovementSpeedModifierKind.Consumable,
                PlayerMovementSpeedConsumable.BonusFraction,
                PlayerMovementSpeedConsumable.DurationSeconds,
                0f);
            pipeline.SetSource(
                PlayerMovementSpeedConsumable.SourceId,
                PlayerMovementSpeedModifierKind.Consumable,
                PlayerMovementSpeedConsumable.BonusFraction,
                PlayerMovementSpeedConsumable.DurationSeconds,
                10f);

            Assert.AreEqual(1, pipeline.SourceCount);
            Assert.AreEqual(1f + PlayerMovementSpeedConsumable.BonusFraction, pipeline.SpeedMultiplier, 0.000001f);
            Assert.AreEqual(0, pipeline.Expire(30f));
            Assert.AreEqual(1, pipeline.Expire(40f));
            Assert.AreEqual(1f, pipeline.SpeedMultiplier, 0f);
        }

        [Test]
        public void CancelExpiring_DropsBrewAndKeepsPartyPassive()
        {
            var pipeline = new PlayerMovementSpeedModifierPipeline();
            pipeline.SetSource(
                "party.passive.example",
                PlayerMovementSpeedModifierKind.PartyPassive,
                0.05f,
                0f,
                0f);
            pipeline.SetSource(
                PlayerMovementSpeedConsumable.SourceId,
                PlayerMovementSpeedModifierKind.Consumable,
                PlayerMovementSpeedConsumable.BonusFraction,
                PlayerMovementSpeedConsumable.DurationSeconds,
                0f);

            float withPassive = 1.05f;
            Assert.AreEqual(withPassive + PlayerMovementSpeedConsumable.BonusFraction, pipeline.SpeedMultiplier, 0.000001f);
            Assert.AreEqual(1, pipeline.RemoveExpiringSources());
            Assert.AreEqual(withPassive, pipeline.SpeedMultiplier, 0.000001f);
            pipeline.Clear();
            Assert.AreEqual(1f, pipeline.SpeedMultiplier, 0f);
            Assert.AreEqual(0, pipeline.SourceCount);
        }

        [Test]
        public void UseNitroBrew_AppliesThenExpiresWithoutConsumingOnFailure()
        {
            var go = new GameObject("NitroBrewUse");
            var host = new RecordingHost();
            try
            {
                StubInventoryService inventory = go.AddComponent<StubInventoryService>();
                StubPlayerItemUseService use = go.AddComponent<StubPlayerItemUseService>();
                use.Configure(inventory);

                PlayerItemUseResult missing = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest { itemId = PlayerMovementSpeedConsumable.ItemId }).GetAwaiter().GetResult();
                Assert.IsFalse(missing.success);
                Assert.AreEqual("movement_speed_host_missing", missing.error);

                PlayerMovementSpeedModifierRuntime.Register(host);
                host.Pipeline.SetSource(
                    "wearable.example_boots",
                    PlayerMovementSpeedModifierKind.Wearable,
                    0.10f,
                    0f,
                    0f);
                float withBoots = host.SpeedMultiplier;

                PlayerItemUseResult used = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest { itemId = PlayerMovementSpeedConsumable.ItemId }).GetAwaiter().GetResult();
                Assert.IsTrue(used.success, used.error);
                Assert.AreEqual(1, CountItem(used.inventory, PlayerMovementSpeedConsumable.ItemId));
                Assert.AreEqual(withBoots + PlayerMovementSpeedConsumable.BonusFraction, host.SpeedMultiplier, 0.000001f);
                Assert.IsTrue(host.Pipeline.TryGetSource(PlayerMovementSpeedConsumable.SourceId, out PlayerMovementSpeedModifierState tonic));
                Assert.AreEqual(PlayerMovementSpeedConsumable.BonusFraction, tonic.BonusFraction, 0f);
                Assert.AreEqual(PlayerMovementSpeedConsumable.DurationSeconds, tonic.ExpiresAt, 0f);

                PlayerItemUseResult refreshed = use.UseItemAsync(
                    "p",
                    "w",
                    new PlayerItemUseRequest { itemId = PlayerMovementSpeedConsumable.ItemId }).GetAwaiter().GetResult();
                Assert.IsTrue(refreshed.success, refreshed.error);
                Assert.AreEqual(0, CountItem(refreshed.inventory, PlayerMovementSpeedConsumable.ItemId));
                Assert.AreEqual(2, host.Pipeline.SourceCount);
                Assert.AreEqual(withBoots + PlayerMovementSpeedConsumable.BonusFraction, host.SpeedMultiplier, 0.000001f);

                host.Now = PlayerMovementSpeedConsumable.DurationSeconds;
                Assert.AreEqual(withBoots, host.SpeedMultiplier, 0f);
            }
            finally
            {
                PlayerMovementSpeedModifierRuntime.ClearHosts();
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
                MenuInventoryItemStack stack = bag.stacks[i];
                if (stack != null && stack.itemId == itemId)
                    total += stack.quantity;
            }

            return total;
        }

        private sealed class RecordingHost : IPlayerMovementSpeedModifierHost
        {
            public readonly PlayerMovementSpeedModifierPipeline Pipeline = new PlayerMovementSpeedModifierPipeline();
            public float Now;

            public void SetSource(
                string sourceId,
                PlayerMovementSpeedModifierKind kind,
                float bonusFraction,
                float durationSeconds)
            {
                Pipeline.SetSource(sourceId, kind, bonusFraction, durationSeconds, Now);
            }

            public bool RemoveSource(string sourceId)
            {
                return Pipeline.RemoveSource(sourceId);
            }

            public void Clear()
            {
                Pipeline.Clear();
            }

            public float SpeedMultiplier
            {
                get
                {
                    Pipeline.Expire(Now);
                    return Pipeline.SpeedMultiplier;
                }
            }
        }
    }
}
