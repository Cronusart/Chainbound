using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum WalletOperationKind
    {
        ReadOnlyLocal,
        BackendProfileRead,
        OwnershipSensitiveRead,
        BackendProtectedWrite,
        OnChainTransaction
    }

    public enum WalletOperationGuardStatus
    {
        Ready,
        ProfileRequired,
        ReconnectRequired,
        WalletMismatch,
        ProfileMismatch,
        ProofExpired,
        UserCancelled,
        BackendRejected,
        UnsupportedPlatform
    }

    [Serializable]
    public class WalletOperationRequest
    {
        public WalletOperationKind operationKind = WalletOperationKind.BackendProtectedWrite;
        public string actionName = "ownership action";
        public string playerProfileId = "";
        public string walletPublicKey = "";
        public string clientVersion = "";
        public bool requireFreshProof = true;
    }

    public class WalletOperationGuardResult
    {
        public bool success;
        public WalletOperationGuardStatus status = WalletOperationGuardStatus.Ready;
        public string message = "";
        public PlayerAccountSession session;

        public static WalletOperationGuardResult Ready(PlayerAccountSession session)
        {
            return new WalletOperationGuardResult
            {
                success = true,
                status = WalletOperationGuardStatus.Ready,
                session = session,
            };
        }

        public static WalletOperationGuardResult Failed(WalletOperationGuardStatus status, string message, PlayerAccountSession session = null)
        {
            return new WalletOperationGuardResult
            {
                success = false,
                status = status,
                message = message ?? status.ToString(),
                session = session,
            };
        }
    }

    public interface IWalletOperationGuard
    {
        Task<WalletOperationGuardResult> EnsureVerifiedWalletAsync(WalletOperationRequest request);
        void MarkWalletDisconnected();
    }

    /// <summary>
    /// Shared guard for wallet-sensitive operations. Normal gameplay can continue
    /// when a wallet disconnects; protected operations call this to reconnect and
    /// verify the same backend-linked wallet before proceeding.
    /// </summary>
    public class WalletOperationGuard : MonoBehaviour, IWalletOperationGuard
    {
        [SerializeField] private AccountSessionService accountSessionService;
        [SerializeField] private MonoBehaviour walletProvider;
        [SerializeField] private bool enableDebugLogs = true;

        private IWalletProvider boundWalletProvider;

        private void OnEnable()
        {
            BindWalletProvider();
        }

        private void OnDisable()
        {
            UnbindWalletProvider();
        }

        public async Task<WalletOperationGuardResult> EnsureVerifiedWalletAsync(WalletOperationRequest request)
        {
            WalletOperationRequest resolvedRequest = request ?? new WalletOperationRequest();
            AccountSessionService service = ResolveAccountSessionService();
            PlayerAccountSession current = service != null ? service.CurrentSession : PlayerAccountSession.Empty();

            if (resolvedRequest.operationKind == WalletOperationKind.ReadOnlyLocal)
                return WalletOperationGuardResult.Ready(current);

            string expectedProfile = FirstNonEmpty(resolvedRequest.playerProfileId, current.PlayerProfileId);
            string expectedWallet = FirstNonEmpty(resolvedRequest.walletPublicKey, current.WalletPublicKey);

            if (string.IsNullOrWhiteSpace(expectedProfile))
                return WalletOperationGuardResult.Failed(WalletOperationGuardStatus.ProfileRequired, "Player profile is required.", current);

            if (!string.Equals(current.PlayerProfileId, expectedProfile, StringComparison.Ordinal))
                return WalletOperationGuardResult.Failed(WalletOperationGuardStatus.ProfileMismatch, "Active session profile does not match the requested profile.", current);

            if (resolvedRequest.operationKind == WalletOperationKind.BackendProfileRead && !resolvedRequest.requireFreshProof)
                return WalletOperationGuardResult.Ready(current);

            bool walletMissing = string.IsNullOrWhiteSpace(current.WalletPublicKey) ||
                                 string.IsNullOrWhiteSpace(expectedWallet) ||
                                 !current.HasLinkedWallet;
            bool walletMismatch = !string.IsNullOrWhiteSpace(current.WalletPublicKey) &&
                                  !string.IsNullOrWhiteSpace(expectedWallet) &&
                                  !string.Equals(current.WalletPublicKey, expectedWallet, StringComparison.Ordinal);
            bool proofExpired = resolvedRequest.requireFreshProof && !current.HasFreshWalletProof;

            if (!walletMissing && !walletMismatch && !proofExpired && service != null && service.HasVerifiedWalletConnection)
                return WalletOperationGuardResult.Ready(current);

            if (walletMismatch)
                return WalletOperationGuardResult.Failed(WalletOperationGuardStatus.WalletMismatch, "Connected wallet does not match the active profile wallet.", current);

            if (service == null)
                return WalletOperationGuardResult.Failed(WalletOperationGuardStatus.UnsupportedPlatform, "Account session service is not available.", current);

            if (enableDebugLogs)
                Debug.Log($"[WalletOperationGuard] Revalidating wallet for {resolvedRequest.operationKind}. missing={walletMissing} proofExpired={proofExpired}", this);

            IProfileWalletAccessCoordinator coordinator = ProfileWalletAccessRegistry.Current;
            if (coordinator == null)
                return WalletOperationGuardResult.Failed(WalletOperationGuardStatus.UnsupportedPlatform, "Wallet access is not available.", current);

            WalletAccessResult access = await coordinator.EnsureAccessAsync(new WalletAccessRequest
            {
                reason = WalletAccessReason.OwnershipAction,
                actionName = resolvedRequest.actionName,
                requireFreshProof = resolvedRequest.requireFreshProof,
                allowOfflineContinuation = false,
            });

            PlayerAccountSession refreshed = service.CurrentSession;
            if (access == null || access.kind != WalletAccessResultKind.Ready || refreshed == null || !refreshed.hasSession || refreshed.fetchError)
            {
                string error = access != null && !string.IsNullOrWhiteSpace(access.message)
                    ? access.message
                    : refreshed != null ? refreshed.errorMessage : "Wallet revalidation failed.";
                WalletOperationGuardStatus status = access != null && access.kind == WalletAccessResultKind.WrongWallet
                    ? WalletOperationGuardStatus.WalletMismatch
                    : access != null && (access.kind == WalletAccessResultKind.Cancelled || access.kind == WalletAccessResultKind.ContinuedOffline)
                        ? WalletOperationGuardStatus.UserCancelled
                        : IsUserCancellation(error)
                    ? WalletOperationGuardStatus.UserCancelled
                    : WalletOperationGuardStatus.BackendRejected;
                return WalletOperationGuardResult.Failed(
                    status,
                    error,
                    refreshed);
            }

            if (!string.Equals(refreshed.PlayerProfileId, expectedProfile, StringComparison.Ordinal))
            {
                return WalletOperationGuardResult.Failed(
                    WalletOperationGuardStatus.ProfileMismatch,
                    "Reconnected wallet resolved to a different player profile.",
                    refreshed);
            }

            if (!string.IsNullOrWhiteSpace(expectedWallet) &&
                !string.Equals(refreshed.WalletPublicKey, expectedWallet, StringComparison.Ordinal))
            {
                return WalletOperationGuardResult.Failed(
                    WalletOperationGuardStatus.WalletMismatch,
                    "Reconnected wallet does not match the active profile wallet.",
                    refreshed);
            }

            if (resolvedRequest.requireFreshProof && !service.HasVerifiedWalletConnection)
            {
                return WalletOperationGuardResult.Failed(
                    WalletOperationGuardStatus.ProofExpired,
                    "Wallet proof is not fresh.",
                    refreshed);
            }

            return WalletOperationGuardResult.Ready(refreshed);
        }

        public void MarkWalletDisconnected()
        {
            AccountSessionService service = ResolveAccountSessionService();
            PlayerAccountSession session = service != null ? service.CurrentSession : null;
            if (session == null)
                return;

            session.isWalletConnected = false;
            session.walletProofVerifiedAt = "";
            session.walletProofExpiresAt = "";
            service.ClearRuntimeWalletAuthorization();
        }

        private AccountSessionService ResolveAccountSessionService()
        {
            AccountSessionService authority = AccountSessionService.Instance;
            if (authority != null)
            {
                accountSessionService = authority;
                return accountSessionService;
            }

            if (accountSessionService == null)
                accountSessionService = GetComponent<AccountSessionService>();
            if (accountSessionService == null)
                accountSessionService = FindObjectOfType<AccountSessionService>();
            return accountSessionService;
        }

        private void BindWalletProvider()
        {
            IWalletProvider provider = ResolveWalletProvider();
            if (ReferenceEquals(provider, boundWalletProvider))
                return;

            UnbindWalletProvider();
            boundWalletProvider = provider;
            if (boundWalletProvider != null)
                boundWalletProvider.OnWalletDisconnected += HandleWalletDisconnected;
        }

        private void UnbindWalletProvider()
        {
            if (boundWalletProvider != null)
                boundWalletProvider.OnWalletDisconnected -= HandleWalletDisconnected;
            boundWalletProvider = null;
        }

        private IWalletProvider ResolveWalletProvider()
        {
            if (walletProvider is IWalletProvider explicitProvider)
                return explicitProvider;

            walletProvider = GetComponent<SolanaWalletAdapterProvider>();
            if (walletProvider is IWalletProvider localProvider)
                return localProvider;

            walletProvider = FindObjectOfType<SolanaWalletAdapterProvider>();
            return walletProvider as IWalletProvider;
        }

        private void HandleWalletDisconnected()
        {
            if (enableDebugLogs)
                Debug.Log("[WalletOperationGuard] Wallet disconnected; protected operations will require reconnect.", this);

            MarkWalletDisconnected();
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : (fallback ?? "").Trim();
        }

        private static bool IsUserCancellation(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            string normalized = message.ToUpperInvariant();
            return normalized.Contains("CANCEL") || normalized.Contains("TIMED OUT") || normalized.Contains("TIMEOUT") || normalized.Contains("USER_REJECT");
        }
    }
}
