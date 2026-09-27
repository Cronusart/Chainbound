using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// A single captured Chainling stored in the on-chain ChainlingVault.
    /// MVP minimal struct -- expanded fields added later.
    /// </summary>
    [Serializable]
    public struct ChainlingEntry
    {
        /// <summary>Species ID from the Chainling database.</summary>
        public ushort chainlingId;

        /// <summary>Unique per-wallet instance counter (monotonic, assigned on capture).</summary>
        public uint instanceId;

        /// <summary>Current level (1-100).</summary>
        public ushort level;

        /// <summary>Current XP within the level.</summary>
        public uint xp;

        /// <summary>
        /// Deterministic IV seed. Derived from encounter data, NOT VRF.
        /// Treated as non-critical for MVP per spec.
        /// </summary>
        public ulong ivsSeed;

        /// <summary>Bitfield for optional flags (reserved for future use).</summary>
        public ushort flags;

        // â”€â”€ Level-Up Reward State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Pending level reward: 0 = none, 1-5 = level that triggered the reward.</summary>
        public byte pendingLevelReward;

        /// <summary>Deterministic seed for move selection (program-derived, not client).</summary>
        public ulong pendingRewardSeed;

        /// <summary>Bitfield: bit N set = level N reward already claimed (bits 1-5 used for MVP).</summary>
        public byte claimedRewardBits;

        public bool HasPendingReward => pendingLevelReward > 0;
        public bool IsLevelClaimed(int level) => (claimedRewardBits & (1 << level)) != 0;
    }

    /// <summary>
    /// Client-side mirror of the on-chain ChainlingVault PDA.
    /// One per wallet; stores up to MAX_CHAINLINGS captured Chainlings.
    /// PDA seed: ["chainling_vault", owner_pubkey]
    /// On-chain layout: fixed-size array [ChainlingEntry; MAX_CHAINLINGS].
    /// </summary>
    [Serializable]
    public class ChainlingVaultData
    {
        /// <summary>Owner wallet public key (base58).</summary>
        public string owner;

        /// <summary>On-chain capacity (MVP = MAX_CHAINLINGS). Future: upgradeable via RAM.</summary>
        public byte chainlingCapacity;

        /// <summary>Current count of stored chainlings.</summary>
        public byte chainlingCount;

        /// <summary>Captured chainlings in the vault.</summary>
        public List<ChainlingEntry> chainlings;

        /// <summary>Monotonically increasing ID assigned to each new capture.</summary>
        public uint nextInstanceId;

        /// <summary>PDA bump seed.</summary>
        public byte bump;

        /// <summary>PDA address (base58). Derived client-side, not stored on-chain.</summary>
        public string pdaAddress;

        public ChainlingVaultData()
        {
            chainlings = new List<ChainlingEntry>();
            chainlingCapacity = (byte)CombatConstants.MAX_CHAINLINGS;
        }

        public int ChainlingCount => chainlings?.Count ?? 0;
        public bool CanAddChainling => ChainlingCount < chainlingCapacity;
    }
}
