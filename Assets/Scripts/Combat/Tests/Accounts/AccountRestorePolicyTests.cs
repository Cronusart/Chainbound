using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class AccountRestorePolicyTests
    {
        [Test]
        public void SolanaDappStoreAlwaysUsesWalletWhenAvailable()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.SolanaDappStore);

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.SolanaDappStore,
                capabilities,
                identityCanRestore: true,
                walletAvailable: true);

            Assert.AreEqual(AccountRestoreRoute.Wallet, route);
        }

        [Test]
        public void SolanaDappStoreReportsNoneWhenWalletIsUnavailable()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.SolanaDappStore);

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.SolanaDappStore,
                capabilities,
                identityCanRestore: true,
                walletAvailable: false);

            Assert.AreEqual(AccountRestoreRoute.None, route);
        }

        [Test]
        public void GooglePlayUsesPlatformIdentityWhenItCanRestore()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.GooglePlay);

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.GooglePlay,
                capabilities,
                identityCanRestore: true,
                walletAvailable: true);

            Assert.AreEqual(AccountRestoreRoute.PlatformIdentity, route);
        }

        [Test]
        public void GooglePlayDoesNotFallBackToWalletWhenIdentityCannotRestore()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.GooglePlay);

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.GooglePlay,
                capabilities,
                identityCanRestore: false,
                walletAvailable: true);

            Assert.AreEqual(AccountRestoreRoute.None, route);
        }

        [Test]
        public void IosUsesPlatformIdentityWhenItCanRestore()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.ForBuild(BuildPlatform.IOSAppStore);

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.IOSAppStore,
                capabilities,
                identityCanRestore: true,
                walletAvailable: true);

            Assert.AreEqual(AccountRestoreRoute.PlatformIdentity, route);
        }

        [Test]
        public void EditorUsesPlatformIdentityWhenAMockCanRestore()
        {
            PlatformCapabilities capabilities = PlatformCapabilities.EditorDefaults();

            AccountRestoreRoute route = AccountRestorePolicy.Resolve(
                BuildPlatform.Editor,
                capabilities,
                identityCanRestore: true,
                walletAvailable: true);

            Assert.AreEqual(AccountRestoreRoute.PlatformIdentity, route);
        }
    }
}
