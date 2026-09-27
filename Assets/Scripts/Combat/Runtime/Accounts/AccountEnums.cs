using System;

namespace Chainbound.Combat
{
    public enum BuildPlatform
    {
        Editor,
        GooglePlay,
        IOSAppStore,
        SolanaDappStore
    }

    public enum PlatformIdentityType
    {
        None,
        Guest,
        GooglePlay,
        Apple,
        Seeker
    }

    public enum AuthIdentityType
    {
        None,
        Guest,
        SupabaseAuth,
        GooglePlay,
        Apple,
        Seeker
    }

    public enum WalletProviderType
    {
        None,
        Phantom,
        Solflare,
        SeedVault,
        Other
    }

    public enum AccountLinkState
    {
        None,
        Unlinked,
        LinkPending,
        Linked,
        Revoked,
        Conflict
    }

    public enum LastSignInMethod
    {
        None,
        Guest,
        GooglePlay,
        Apple,
        Seeker,
        Wallet,
        EditorMock
    }

    /// <summary>
    /// How this build can recover an existing profile. Restore is one player action,
    /// but the mechanism is store-specific: Play/Apple resume a platform identity,
    /// while a Solana dApp Store (Seeker) build has no store identity and must prove
    /// wallet ownership instead.
    /// </summary>
    public enum AccountRestoreRoute
    {
        None = 0,
        PlatformIdentity = 1,
        Wallet = 2,
    }

    public enum AccountLinkError
    {
        None,
        MissingCurrentProfile,
        MissingCandidate,
        ExplicitConsentRequired,
        IdentityAlreadyLinkedToCurrentProfile,
        IdentityLinkedToDifferentProfile,
        WalletAlreadyLinkedToCurrentProfile,
        WalletLinkedToDifferentProfile,
        ProviderUnsupported,
        InvalidProof,
        UserCancelled,
        BackendRejected,
        Unknown
    }

    public enum AccountLinkEventType
    {
        GuestStarted,
        IdentityRestoreRequested,
        IdentityLinkRequested,
        WalletConnectRequested,
        WalletLinkRequested,
        WalletUnlinkRequested,
        LinkConflict,
        LinkSucceeded,
        LinkFailed
    }

    public enum PurchaseSourceType
    {
        None,
        GooglePlayBilling,
        AppleAppStoreIap,
        SolanaWalletTransaction,
        RewardedAd,
        ManualGrant,
        Promo,
        Test
    }

    public enum AdRewardSourceType
    {
        None,
        RewardedVideo,
        Offerwall,
        Debug
    }

    [Flags]
    public enum EntitlementStatusFlags
    {
        None = 0,
        PendingValidation = 1 << 0,
        Validated = 1 << 1,
        Granted = 1 << 2,
        Rejected = 1 << 3,
        Refunded = 1 << 4,
        Revoked = 1 << 5
    }
}
