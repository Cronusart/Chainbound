using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages the ChainlingVault PDA: captured chainling storage.
    /// PDA seed: ["chainling_vault", owner_pubkey]
    /// </summary>
    public interface IChainlingVaultService
    {
        ChainlingVaultData CachedVault { get; }
        bool IsVaultLoaded { get; }

        string DeriveVaultPda(string walletPubkey);
        Task<VaultOperationResult> CreateVaultAsync(string walletPubkey);
        Task<ChainlingVaultData> LoadVaultAsync(string walletPubkey);

        /// <summary>
        /// Add a captured chainling. Assigns the next instance ID.
        /// Returns the assigned ChainlingEntry with instanceId populated.
        /// </summary>
        Task<ChainlingCaptureResult> AddChainlingFromCaptureAsync(ushort chainlingId, ushort level, ulong ivsSeed);

        Task<VaultOperationResult> RemoveChainlingAsync(int index);
        void ClearCache();
    }

    public class ChainlingCaptureResult
    {
        public bool success;
        public string errorMessage;
        public ChainlingEntry entry;
        public TxResult txResult;
    }
}
