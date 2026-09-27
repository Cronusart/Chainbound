using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Result of a vault operation (burn, add, etc.).
    /// </summary>
    public class VaultOperationResult
    {
        public bool success;
        public string errorMessage;
        public PlayerMoveVault updatedVault;
        public TxResult txResult;
    }

    /// <summary>
    /// Reads the MoveVault PDA and performs owner-authorized Known salvage.
    /// RAM is NOT stored here; RAM mutations are delegated to IProfileService.
    /// PDA seed: ["move_vault", owner_pubkey]
    /// </summary>
    public interface IMoveVaultService
    {
        PlayerMoveVault CachedVault { get; }
        bool IsVaultLoaded { get; }

        string DeriveVaultPda(string walletPubkey);
        Task<VaultOperationResult> CreateVaultAsync(string walletPubkey);
        Task<PlayerMoveVault> LoadVaultAsync(string walletPubkey);
        void ClearCache();
    }
}
