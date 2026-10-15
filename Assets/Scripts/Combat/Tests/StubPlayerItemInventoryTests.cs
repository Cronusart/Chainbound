using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class StubPlayerItemInventoryTests
    {
        [Test]
        public async Task Grant_BackpackSeed_InsertsOnceThenAlreadyClaimed()
        {
            var go = new GameObject("StubPlayerItemInventoryTests");
            try
            {
                StubPlayerItemInventory stub = go.AddComponent<StubPlayerItemInventory>();

                WorldPickupGrantResult first = await stub.GrantWorldPickupAsync("world.pickup.link1.backpack_01");
                Assert.That(first.ok, Is.True);
                Assert.That(first.alreadyClaimed, Is.False);
                Assert.That(first.itemId, Is.EqualTo("chainling_patch"));
                Assert.That(first.quantity, Is.EqualTo(1));
                Assert.That(first.remainingQuantity, Is.EqualTo(1));

                WorldPickupGrantResult retry = await stub.GrantWorldPickupAsync("world.pickup.link1.backpack_01");
                Assert.That(retry.ok, Is.True);
                Assert.That(retry.alreadyClaimed, Is.True);
                Assert.That(retry.remainingQuantity, Is.EqualTo(1));

                WorldPickupGrantResult patch = await stub.GrantWorldPickupAsync(
                    "world.pickup.loomville_mart.open_crate_patch");
                Assert.That(patch.ok, Is.True);
                Assert.That(patch.itemId, Is.EqualTo("chainling_patch"));
                Assert.That(patch.alreadyClaimed, Is.False);
                Assert.That(patch.remainingQuantity, Is.EqualTo(2));

                WorldPickupGrantResult orb = await stub.GrantWorldPickupAsync(
                    "world.pickup.loomville_mart.open_crate_orb");
                Assert.That(orb.ok, Is.True);
                Assert.That(orb.itemId, Is.EqualTo("signal_orb"));
                Assert.That(orb.alreadyClaimed, Is.False);
                Assert.That(orb.remainingQuantity, Is.EqualTo(1));

                WorldPickupGrantResult linkOrb = await stub.GrantWorldPickupAsync(
                    "world.pickup.link1.backpack_02");
                Assert.That(linkOrb.ok, Is.True);
                Assert.That(linkOrb.itemId, Is.EqualTo("signal_orb"));
                Assert.That(linkOrb.alreadyClaimed, Is.False);
                Assert.That(linkOrb.remainingQuantity, Is.EqualTo(2));

                InventorySnapshot snapshot = await stub.FetchInventoryAsync("editor-profile", "editor-wallet");
                MenuInventoryItemStack orbStack = snapshot.stacks.Find(stack => stack.itemId == "signal_orb");
                Assert.That(orbStack, Is.Not.Null);
                Assert.That(orbStack.quantity, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task Grant_UnknownSpawn_Fails()
        {
            var go = new GameObject("StubPlayerItemInventoryUnknown");
            try
            {
                StubPlayerItemInventory stub = go.AddComponent<StubPlayerItemInventory>();
                WorldPickupGrantResult result = await stub.GrantWorldPickupAsync("world.pickup.missing");
                Assert.That(result.ok, Is.False);
                Assert.That(result.error, Is.EqualTo("unknown_spawn"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
