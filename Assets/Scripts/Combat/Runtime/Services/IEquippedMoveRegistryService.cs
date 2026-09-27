using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages EquippedMoveRegistry PDAs (one per chainling instance).
    /// PDA seed: ["equipped_moves", owner_pubkey, chainling_instance_id_le_bytes]
    /// </summary>
    public interface IEquippedMoveRegistryService
    {
        string DeriveRegistryPda(string walletPubkey, uint chainlingInstanceId);
        Task<VaultOperationResult> CreateRegistryAsync(string walletPubkey, uint chainlingInstanceId);
        Task<EquippedMoveRegistryData> LoadRegistryAsync(string walletPubkey, uint chainlingInstanceId);
        Task<VaultOperationResult> SetSlotAsync(string walletPubkey, uint chainlingInstanceId, int slotIndex, EquippedMoveSlot slot);
        Task<VaultOperationResult> ClearSlotAsync(string walletPubkey, uint chainlingInstanceId, int slotIndex);
        void ClearCache();
    }
}
