using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Service for managing the player's on-chain profile PDA.
    /// PDA seed: ["player_profile", owner_pubkey]
    /// RAM is authoritative in PlayerProfile (not MoveVault).
    /// </summary>
    public interface IProfileService
    {
        bool IsProfileLoaded { get; }
        PlayerProfileData CachedProfile { get; }

        string DerivePdaAddress(string walletPubkey);
        Task<bool> HasProfileAsync(string walletPubkey);
        Task<TxResult> CreateProfileAsync(string walletPubkey);
        Task<PlayerProfileData> LoadProfileAsync(string walletPubkey);
        Task<TxResult> UpdateWinsLossesAsync(bool isWin);
        Task<TxResult> UpdateLastPositionAsync(uint sceneId, float x, float y, float z);
        void ClearCache();

        // â”€â”€ RAM Operations (bounded; no generic setters) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Spend RAM via a bounded on-chain instruction (upgrade, vending, capacity).
        /// Returns false if the player cannot afford the cost.
        /// </summary>
        Task<TxResult> SpendRamAsync(int amount, string reason);

        /// <summary>
        /// Legacy compatibility seam. Standalone RAM credit is fail-closed because the
        /// burn_ordinal_for_ram instruction performs the burn and reward atomically.
        /// </summary>
        Task<TxResult> CreditRamFromBurnAsync(int amount);

        /// <summary>
        /// Checks and applies the daily RAM reset if the UTC day boundary has passed.
        /// Should be called on profile load and before any RAM mutation.
        /// </summary>
        void CheckAndApplyDailyReset();
    }
}
