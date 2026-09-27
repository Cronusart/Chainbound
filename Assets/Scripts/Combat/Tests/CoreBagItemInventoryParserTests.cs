using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CoreBagItemInventoryParserTests
    {
        [Test]
        public void GuestPayload_WithoutWallet_ParsesProfileStacksIntoCategories()
        {
            const string json =
                "{\"success\":true,\"player_profile_id\":\"guest-profile\",\"wallet_public_key\":\"\"," +
                "\"cluster\":\"devnet\",\"ram_balance\":0,\"stacks\":[" +
                "{\"item_id\":\"chainling_patch\",\"quantity\":2,\"category\":\"field\",\"display_name\":\"Chainling Patch\"}," +
                "{\"item_id\":\"signal_orb\",\"quantity\":1,\"category\":\"capture\",\"display_name\":\"Signal Orb\"}" +
                "]}";

            InventorySnapshot snapshot = CoreBagItemInventoryService.BuildSnapshotFromJson(json, "");

            Assert.That(snapshot.lastError, Is.Empty);
            Assert.That(snapshot.connectivity, Is.EqualTo(MenuConnectivityState.Ready));
            Assert.That(snapshot.stacks, Is.Not.Null);
            Assert.That(snapshot.stacks.Count, Is.EqualTo(2));
            Assert.That(snapshot.walletPublicKey, Is.Empty);

            Assert.That(snapshot.stacks[0].itemId, Is.EqualTo("chainling_patch"));
            Assert.That(snapshot.stacks[0].quantity, Is.EqualTo(2));
            Assert.That(snapshot.stacks[0].categoryKey, Is.EqualTo(PlayerItemCategoryKeys.Field));

            Assert.That(snapshot.stacks[1].itemId, Is.EqualTo("signal_orb"));
            Assert.That(snapshot.stacks[1].quantity, Is.EqualTo(1));
            Assert.That(snapshot.stacks[1].categoryKey, Is.EqualTo(PlayerItemCategoryKeys.Capture));
        }

        [Test]
        public void FailedPayload_SurfacesWalletErrorInsteadOfSilentEmptyBag()
        {
            InventorySnapshot snapshot = CoreBagItemInventoryService.BuildSnapshotFromJson(
                "{\"success\":false,\"error\":\"active_wallet_required\"}",
                "");

            Assert.That(snapshot.lastError, Is.EqualTo("active_wallet_required"));
            Assert.That(snapshot.connectivity, Is.EqualTo(MenuConnectivityState.Error));
            Assert.That(snapshot.stacks, Is.Not.Null);
            Assert.That(snapshot.stacks.Count, Is.EqualTo(0));
        }
    }
}
