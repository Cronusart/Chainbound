using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Canonical inventory surface for owned move instances.
    /// Move tiers: Known (PDA), Stamped (legacy compressed asset), Stamped (Core NFT).
    /// </summary>
    public interface IMoveInventoryService
    {
        /// <summary>
        /// Get the cached list of all owned move instances.
        /// </summary>
        IReadOnlyList<MoveInstance> GetOwnedMoves();

        /// <summary>
        /// Look up a move by internal instance ID.
        /// </summary>
        MoveInstance GetByInstanceId(string instanceId);

        /// <summary>
        /// Look up a move by mint/asset address.
        /// </summary>
        MoveInstance GetByMintAddress(string mintAddress);

        /// <summary>
        /// Refresh local cache from chain-backed sources.
        /// </summary>
        Task RefreshFromChainAsync();

        /// <summary>
        /// Add/update a move in the local cache.
        /// </summary>
        void AddToCache(MoveInstance moveInstance);

        /// <summary>
        /// Remove a move from the local cache by instance ID.
        /// </summary>
        void RemoveFromCache(string instanceId);
    }
}
