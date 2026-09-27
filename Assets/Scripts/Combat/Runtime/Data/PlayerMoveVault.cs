using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Client-side mirror of the on-chain MoveVault PDA.
    /// Stores Known moves only (no NFT inventory). RAM is in PlayerProfile.
    /// PDA seed: ["move_vault", owner_pubkey]
    /// On-chain layout: fixed-size Known array plus a monotonic instance counter.
    /// </summary>
    [Serializable]
    public class PlayerMoveVault
    {
        /// <summary>Owner wallet public key (base58).</summary>
        public string owner;

        public byte knownCapacity;

        public byte knownCount;

        public List<KnownMoveVaultEntry> knownMoves;

        public ulong nextInstanceId;

        /// <summary>PDA bump seed.</summary>
        public byte bump;

        /// <summary>PDA address of this vault (derived, not stored on-chain).</summary>
        public string pdaAddress;

        /// <summary>Schema version for future migration.</summary>
        public int schemaVersion;

        public PlayerMoveVault()
        {
            knownMoves = new List<KnownMoveVaultEntry>();
            knownCapacity = (byte)CombatConstants.MAX_KNOWN_MOVES;
            schemaVersion = 2;
        }

        public int KnownCount => knownMoves?.Count ?? 0;

        public bool CanAddKnown => KnownCount < knownCapacity;
    }
}
