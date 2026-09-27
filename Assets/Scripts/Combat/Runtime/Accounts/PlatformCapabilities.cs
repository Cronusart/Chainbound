using System;

namespace Chainbound.Combat
{
    [Serializable]
    public struct PlatformCapabilities
    {
        public bool supportsGooglePlayIdentity;
        public bool supportsAppleIdentity;
        public bool supportsPlayBilling;
        public bool supportsAppleIAP;
        public bool supportsSolanaWallet;
        public bool supportsSeedVault;
        public bool supportsRewardedAds;
        public bool supportsOnChainMinting;
        public bool supportsExternalWalletOnly;
        public bool supportsExperimentalCryptoCommerce;

        public static PlatformCapabilities EditorDefaults()
        {
            return new PlatformCapabilities
            {
                supportsGooglePlayIdentity = true,
                supportsAppleIdentity = true,
                supportsSolanaWallet = true,
                supportsSeedVault = true,
                supportsRewardedAds = true,
                supportsOnChainMinting = true,
                supportsExternalWalletOnly = true,
                supportsExperimentalCryptoCommerce = true,
            };
        }

        public static PlatformCapabilities ForBuild(BuildPlatform platform)
        {
            switch (platform)
            {
                case BuildPlatform.GooglePlay:
                    return new PlatformCapabilities
                    {
                        supportsGooglePlayIdentity = true,
                        supportsPlayBilling = true,
                        supportsSolanaWallet = true,
                        supportsRewardedAds = true,
                        supportsOnChainMinting = true,
                        supportsExternalWalletOnly = true,
                    };
                case BuildPlatform.IOSAppStore:
                    return new PlatformCapabilities
                    {
                        supportsAppleIdentity = true,
                        supportsAppleIAP = true,
                        supportsSolanaWallet = true,
                        supportsRewardedAds = true,
                        supportsOnChainMinting = true,
                        supportsExternalWalletOnly = true,
                    };
                case BuildPlatform.SolanaDappStore:
                    return new PlatformCapabilities
                    {
                        supportsSolanaWallet = true,
                        supportsSeedVault = true,
                        supportsOnChainMinting = true,
                        supportsExternalWalletOnly = true,
                        supportsExperimentalCryptoCommerce = true,
                    };
                default:
                    return EditorDefaults();
            }
        }

        public bool SupportsRecoverablePlatformIdentity(BuildPlatform platform)
        {
            return (platform == BuildPlatform.GooglePlay && supportsGooglePlayIdentity)
                || (platform == BuildPlatform.IOSAppStore && supportsAppleIdentity)
                || (platform == BuildPlatform.SolanaDappStore && supportsSolanaWallet)
                || platform == BuildPlatform.Editor;
        }

        public bool SupportsWalletProvider(WalletProviderType provider)
        {
            if (!supportsSolanaWallet)
                return false;

            return provider == WalletProviderType.SeedVault
                ? supportsSeedVault
                : provider != WalletProviderType.None;
        }
    }
}
