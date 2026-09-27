namespace Chainbound.Combat
{
    /// <summary>
    /// Picks the profile-recovery mechanism for a build from platform capabilities,
    /// not from whichever provider happened to be serialized on the scene. A Seeker
    /// dApp Store build never uses Play/Apple identity, even if a stub provider is
    /// still wired for another store's scene.
    /// </summary>
    public static class AccountRestorePolicy
    {
        public static AccountRestoreRoute Resolve(
            BuildPlatform platform,
            PlatformCapabilities capabilities,
            bool identityCanRestore,
            bool walletAvailable)
        {
            if (platform == BuildPlatform.SolanaDappStore)
                return walletAvailable ? AccountRestoreRoute.Wallet : AccountRestoreRoute.None;

            if (capabilities.SupportsRecoverablePlatformIdentity(platform) && identityCanRestore)
                return AccountRestoreRoute.PlatformIdentity;

            return AccountRestoreRoute.None;
        }
    }
}
