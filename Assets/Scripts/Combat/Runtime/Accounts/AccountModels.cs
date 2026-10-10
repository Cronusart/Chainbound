using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public enum WalletConnectMode
    {
        Resolve,
        Link,
        Create,
    }

    [Serializable]
    public class PlayerProfileRef
    {
        public string playerProfileId = "";
        public string authUserId = "";
        public string displayName = "";
        public string platformOrigin = "";
        public string lastPlatformSeen = "";
        public string dataResidency = "";
        public bool hasStartedGame;
        /// <summary>
        /// The menu sprite cinematic has completed (or been deliberately skipped).
        /// Kept separately from the quest so an interrupted intro deterministically replays it.
        /// </summary>
        public bool introCinematicComplete;
        public bool introComplete;
        public bool isNewProfile;

        public bool HasProfileId => !string.IsNullOrWhiteSpace(playerProfileId);
    }

    [Serializable]
    public class AuthIdentityRef
    {
        public AuthIdentityType identityType = AuthIdentityType.None;
        public string provider = "";
        public string providerSubject = "";
        public string authUserId = "";
        public bool isPrimary;
        public string linkedAt = "";
        public string revokedAt = "";
    }

    [Serializable]
    public class PlatformIdentityRef
    {
        public PlatformIdentityType platformIdentityType = PlatformIdentityType.None;
        public string platformIdentitySubject = "";
        public string provider = "";
        public bool isPrimary;
        public string linkedAt = "";
        public string revokedAt = "";
    }

    [Serializable]
    public class WalletIdentityRef
    {
        public WalletProviderType walletProviderType = WalletProviderType.None;
        public string walletPublicKey = "";
        public bool isActive;
        public string linkedAt = "";
        public string unlinkedAt = "";
    }

    [Serializable]
    public class PlayerAccountSession
    {
        public bool hasSession;
        public PlayerProfileRef profile = new PlayerProfileRef();
        public AuthIdentityRef authIdentity = new AuthIdentityRef();
        public PlatformIdentityRef platformIdentity = new PlatformIdentityRef();
        public WalletIdentityRef walletIdentity = new WalletIdentityRef();
        public AccountLinkState accountLinkState = AccountLinkState.None;
        public LastSignInMethod lastSignInMethod = LastSignInMethod.None;
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public bool isGuestSession;
        public bool isRecoverable;
        public bool isWalletConnected;
        public bool fetchError;
        public string errorMessage = "";
        public string walletProofVerifiedAt = "";
        public string walletProofExpiresAt = "";
        public string walletResolutionStatus = "";

        public string PlayerProfileId => profile != null ? profile.playerProfileId : "";
        public string WalletPublicKey => walletIdentity != null ? walletIdentity.walletPublicKey : "";
        /// <summary>
        /// Persistent backend association. This remains true when the wallet app is
        /// disconnected or the runtime proof has expired.
        /// </summary>
        public bool HasLinkedWallet =>
            walletIdentity != null &&
            walletIdentity.isActive &&
            !string.IsNullOrWhiteSpace(walletIdentity.walletPublicKey);

        /// <summary>
        /// Runtime proof state reported by the session payload. Call
        /// AccountSessionService.HasVerifiedWalletConnection when adapter state must
        /// also be checked.
        /// </summary>
        public bool HasVerifiedWalletConnection =>
            HasLinkedWallet &&
            isWalletConnected &&
            HasFreshWalletProof;

        public bool HasFreshWalletProof
        {
            get
            {
                if (string.IsNullOrWhiteSpace(walletProofExpiresAt))
                    return false;

                return DateTime.TryParse(walletProofExpiresAt, out DateTime expiresAt) &&
                       expiresAt.ToUniversalTime() > DateTime.UtcNow;
            }
        }

        public static PlayerAccountSession Empty()
        {
            return new PlayerAccountSession
            {
                hasSession = false,
                accountLinkState = AccountLinkState.None,
                errorMessage = "",
            };
        }

        public static PlayerAccountSession FromGuest(string playerProfileId, BuildPlatform platform)
        {
            return new PlayerAccountSession
            {
                hasSession = true,
                isGuestSession = true,
                isRecoverable = false,
                accountLinkState = AccountLinkState.Unlinked,
                lastSignInMethod = LastSignInMethod.Guest,
                activePlatformBuild = platform,
                profile = new PlayerProfileRef
                {
                    playerProfileId = playerProfileId ?? "",
                    platformOrigin = platform.ToString(),
                    lastPlatformSeen = platform.ToString(),
                    isNewProfile = true,
                },
                platformIdentity = new PlatformIdentityRef
                {
                    platformIdentityType = PlatformIdentityType.Guest,
                    provider = "local_install",
                    isPrimary = true,
                },
                authIdentity = new AuthIdentityRef
                {
                    identityType = AuthIdentityType.Guest,
                    provider = "local_install",
                    isPrimary = true,
                },
            };
        }
    }

    [Serializable]
    public class AccountSignInRequest
    {
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public PlatformCapabilities capabilities;
        public string clientVersion = "";
        public string deviceInstallId = "";
        public bool explicitConsent;
    }

    [Serializable]
    public class IdentitySignInResult
    {
        public bool success;
        public AuthIdentityType authIdentityType = AuthIdentityType.None;
        public PlatformIdentityType platformIdentityType = PlatformIdentityType.None;
        public string provider = "";
        public string providerSubject = "";
        public string authUserId = "";
        public string errorMessage = "";

        public static IdentitySignInResult Failed(string message)
        {
            return new IdentitySignInResult { success = false, errorMessage = message ?? "Identity sign-in failed." };
        }
    }

    [Serializable]
    public class AccountAuthSessionResult
    {
        public bool success;
        public string accessToken = "";
        public string authUserId = "";
        public string errorMessage = "";
        public bool restored;

        public static AccountAuthSessionResult Failed(string message)
        {
            return new AccountAuthSessionResult { success = false, errorMessage = message ?? "Anonymous authentication failed." };
        }
    }

    [Serializable]
    public class WalletConnectResult
    {
        public bool success;
        public bool userCancelled;
        public WalletProviderType providerType = WalletProviderType.None;
        public string walletPublicKey = "";
        public string errorMessage = "";

        public static WalletConnectResult Failed(string message)
        {
            return new WalletConnectResult { success = false, errorMessage = message ?? "Wallet connection failed." };
        }
    }

    [Serializable]
    public class AccountStartGuestRequest
    {
        public string guestInstallId = "";
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public string clientVersion = "";
    }

    [Serializable]
    public class AccountBeginGameRequest
    {
        public string playerProfileId = "";
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public string clientVersion = "";
    }

    [Serializable]
    public class AccountOnboardingStateRequest
    {
        public string playerProfileId = "";
        public bool introCinematicComplete;
    }

    [Serializable]
    public class AccountRestoreIdentityRequest
    {
        public AuthIdentityType authIdentityType = AuthIdentityType.None;
        public PlatformIdentityType platformIdentityType = PlatformIdentityType.None;
        public string provider = "";
        public string providerSubject = "";
        public string authUserId = "";
        public string verificationToken = "";
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
    }

    [Serializable]
    public class AccountLinkIdentityRequest : AccountRestoreIdentityRequest
    {
        public string playerProfileId = "";
        public bool explicitConsent;
    }

    [Serializable]
    public class AccountLinkWalletRequest
    {
        public string playerProfileId = "";
        public WalletProviderType walletProviderType = WalletProviderType.None;
        public string walletPublicKey = "";
        public string signedMessage = "";
        public string signature = "";
        public bool explicitConsent;
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
    }

    [Serializable]
    public class AccountUnlinkWalletRequest
    {
        public string playerProfileId = "";
        public string walletPublicKey = "";
        public bool explicitConsent;
    }

    [Serializable]
    public class AccountGetSessionRequest
    {
        public string playerProfileId = "";
        public string authUserId = "";
        public string walletPublicKey = "";
    }

    [Serializable]
    public class AccountWalletAuthChallengeRequest
    {
        public string playerProfileId = "";
        public WalletProviderType walletProviderType = WalletProviderType.None;
        public string walletPublicKey = "";
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public string clientVersion = "";
        public string deviceId = "";
        public string cluster = "";
    }

    [Serializable]
    public class WalletAuthChallengeResult
    {
        public bool success;
        public string errorMessage = "";
        public string nonceId = "";
        public string walletPublicKey = "";
        public string walletProvider = "";
        public string cluster = "";
        public string message = "";
        public string issuedAt = "";
        public string expiresAt = "";

        public static WalletAuthChallengeResult Failed(string message)
        {
            return new WalletAuthChallengeResult
            {
                success = false,
                errorMessage = message ?? "Wallet auth challenge failed.",
            };
        }
    }

    [Serializable]
    public class AccountWalletVerifiedConnectRequest
    {
        public WalletConnectMode mode = WalletConnectMode.Link;
        public string playerProfileId = "";
        public WalletProviderType walletProviderType = WalletProviderType.None;
        public string walletPublicKey = "";
        public string nonceId = "";
        public string signedMessage = "";
        public string signature = "";
        public BuildPlatform activePlatformBuild = BuildPlatform.Editor;
        public string clientVersion = "";
        public string deviceId = "";
    }

    [Serializable]
    public class AccountLinkResult
    {
        public bool success;
        public AccountLinkState state = AccountLinkState.None;
        public AccountLinkError error = AccountLinkError.None;
        public string playerProfileId = "";
        public string message = "";

        public static AccountLinkResult Success(string playerProfileId, AccountLinkState state = AccountLinkState.Linked, string message = "")
        {
            return new AccountLinkResult
            {
                success = true,
                state = state,
                error = AccountLinkError.None,
                playerProfileId = playerProfileId ?? "",
                message = message ?? "",
            };
        }

        public static AccountLinkResult Failed(AccountLinkError error, string message, AccountLinkState state = AccountLinkState.Conflict)
        {
            return new AccountLinkResult
            {
                success = false,
                state = state,
                error = error,
                message = message ?? "Account link failed.",
            };
        }
    }

    [Serializable]
    public class AccountLinkCollisionContext
    {
        public string currentPlayerProfileId = "";
        public string existingLinkedPlayerProfileId = "";
        public bool existingLinkActive;
        public bool explicitConsent;
    }

    [Serializable]
    public class ProfileOperationResult
    {
        public bool success;
        public string playerProfileId = "";
        public string displayName = "";
        public string backendChainlingId = "";
        public string speciesId = "";
        public string errorMessage = "";

        public static ProfileOperationResult Failed(string message)
        {
            return new ProfileOperationResult { success = false, errorMessage = message ?? "Profile operation failed." };
        }
    }

    [Serializable]
    public class EntitlementGrantRequest
    {
        public string playerProfileId = "";
        public PurchaseSourceType sourceType = PurchaseSourceType.None;
        public BuildPlatform sourcePlatform = BuildPlatform.Editor;
        public string sourceTransactionId = "";
        public string skuOrProductId = "";
        public long amount;
        public string currencyType = "";
        public string metadataJson = "";
    }

    [Serializable]
    public class EntitlementGrantResult
    {
        public bool success;
        public string ledgerId = "";
        public EntitlementStatusFlags status = EntitlementStatusFlags.None;
        public string errorMessage = "";
    }

    [Serializable]
    public class RewardGrantRequest
    {
        public string playerProfileId = "";
        public AdRewardSourceType sourceType = AdRewardSourceType.None;
        public BuildPlatform sourcePlatform = BuildPlatform.Editor;
        public string placementId = "";
        public string rewardId = "";
        public long amount;
        public string currencyType = "";
        public string metadataJson = "";
    }

    [Serializable]
    public class ProductCatalogEntry
    {
        public string productId = "";
        public string displayName = "";
        public PurchaseSourceType sourceType = PurchaseSourceType.None;
        public long amount;
        public string currencyType = "";
        public List<string> platformProductIds = new List<string>();
    }
}
