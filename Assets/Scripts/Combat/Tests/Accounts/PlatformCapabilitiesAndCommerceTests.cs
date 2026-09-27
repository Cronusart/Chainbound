using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class PlatformCapabilitiesAndCommerceTests
    {
        [Test]
        public void GooglePlayCapabilitiesExposePlayIdentityAndBilling()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.GooglePlay);

            Assert.IsTrue(capabilities.supportsGooglePlayIdentity);
            Assert.IsTrue(capabilities.supportsPlayBilling);
            Assert.IsTrue(capabilities.supportsSolanaWallet);
            Assert.IsFalse(capabilities.supportsAppleIdentity);
        }

        [Test]
        public void IosCapabilitiesDoNotAssumeSeedVaultTransport()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.IOSAppStore);

            Assert.IsTrue(capabilities.supportsAppleIdentity);
            Assert.IsTrue(capabilities.supportsAppleIAP);
            Assert.IsTrue(capabilities.supportsSolanaWallet);
            Assert.IsFalse(capabilities.supportsSeedVault);
        }

        [Test]
        public void SolanaDappStoreCapabilitiesPreferSeedVaultAndCryptoCommerce()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.SolanaDappStore);

            Assert.IsTrue(capabilities.supportsSeedVault);
            Assert.IsTrue(capabilities.supportsOnChainMinting);
            Assert.IsTrue(capabilities.supportsExperimentalCryptoCommerce);
            Assert.IsFalse(capabilities.supportsPlayBilling);
            Assert.IsFalse(capabilities.supportsGooglePlayIdentity);
            Assert.IsTrue(capabilities.SupportsRecoverablePlatformIdentity(BuildPlatform.SolanaDappStore));
        }

        [Test]
        public void RewardGrantMapsToEntitlementLedgerRequest()
        {
            var reward = new RewardGrantRequest
            {
                playerProfileId = "profile-a",
                sourcePlatform = BuildPlatform.GooglePlay,
                placementId = "daily-video",
                rewardId = "soft_currency_small",
                amount = 25,
                currencyType = "ram",
                metadataJson = "{\"test\":true}",
            };

            EntitlementGrantRequest request = EntitlementGrantMapper.FromReward(reward);

            Assert.AreEqual("profile-a", request.playerProfileId);
            Assert.AreEqual(PurchaseSourceType.RewardedAd, request.sourceType);
            Assert.AreEqual(BuildPlatform.GooglePlay, request.sourcePlatform);
            Assert.AreEqual("daily-video", request.sourceTransactionId);
            Assert.AreEqual("soft_currency_small", request.skuOrProductId);
            Assert.AreEqual(25, request.amount);
            Assert.AreEqual("ram", request.currencyType);
        }
    }
}
