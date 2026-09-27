using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public enum WalletAccessReason
    {
        ResumeProfile,
        OwnershipAction,
        ManualHud,
    }

    public enum WalletAccessResultKind
    {
        Ready,
        ContinuedOffline,
        Cancelled,
        WrongWallet,
        Failed,
    }

    [Serializable]
    public sealed class WalletAccessRequest
    {
        public WalletAccessReason reason = WalletAccessReason.OwnershipAction;
        public string actionName = "ownership action";
        public bool requireFreshProof = true;
        public bool allowOfflineContinuation;
    }

    public sealed class WalletAccessResult
    {
        public WalletAccessResultKind kind;
        public string message = "";
        public PlayerAccountSession session;

        public bool IsReady => kind == WalletAccessResultKind.Ready;

        public static WalletAccessResult From(
            WalletAccessResultKind kind,
            string message = "",
            PlayerAccountSession session = null)
        {
            return new WalletAccessResult
            {
                kind = kind,
                message = message ?? "",
                session = session,
            };
        }
    }

    public interface IProfileWalletAccessCoordinator
    {
        Task<WalletAccessResult> EnsureAccessAsync(WalletAccessRequest request);
    }

    public static class ProfileWalletAccessRegistry
    {
        public static IProfileWalletAccessCoordinator Current { get; private set; }

        public static void Register(IProfileWalletAccessCoordinator coordinator)
        {
            Current = coordinator;
        }

        public static void Unregister(IProfileWalletAccessCoordinator coordinator)
        {
            if (ReferenceEquals(Current, coordinator))
                Current = null;
        }
    }
}
