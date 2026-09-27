using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// A single equipped move slot in the on-chain EquippedMoveRegistry.
    /// Supports all three tiers: Known (vault index), Stamped (legacy compressed asset asset),
    /// or Stamped (Core NFT asset).
    /// </summary>
    [Serializable]
    public struct EquippedMoveSlot
    {
        /// <summary>Move tier: 0=Known, 1=Stamped(legacy compressed asset), 2=Stamped(Core).</summary>
        public byte tier;

        /// <summary>Source kind: 0=KnownIndex, 1=NFTAssetId.</summary>
        public byte sourceKind;

        /// <summary>Index into MoveVault.Known moves when sourceKind == 0.</summary>
        public byte knownIndex;

        /// <summary>NFT asset public key (32 bytes) when sourceKind == 1.</summary>
        public byte[] assetId;

        /// <summary>Cached move_id for quick lookup (not authoritative for NFTs).</summary>
        public ushort moveId;

        /// <summary>Optional modifier cache (not authoritative). Packed representation.</summary>
        public ulong modCache;

        /// <summary>Whether this slot is occupied.</summary>
        public bool IsEmpty => moveId == 0 && tier == 0 && sourceKind == 0 && knownIndex == 0;

        public static EquippedMoveSlot Empty => default;

        public static EquippedMoveSlot FromKnown(byte knownIdx, ushort moveId)
        {
            return new EquippedMoveSlot
            {
                tier = (byte)MoveTier.Known,
                sourceKind = 0,
                knownIndex = knownIdx,
                assetId = new byte[32],
                moveId = moveId,
                modCache = 0,
            };
        }

        public static EquippedMoveSlot FromNft(MoveTier nftTier, byte[] nftAssetId, ushort moveId, ulong modCache = 0)
        {
            var slot = new EquippedMoveSlot
            {
                tier = (byte)nftTier,
                sourceKind = 1,
                knownIndex = 0,
                moveId = moveId,
                modCache = modCache,
            };
            slot.assetId = new byte[32];
            if (nftAssetId != null)
            {
                int len = Math.Min(nftAssetId.Length, 32);
                Array.Copy(nftAssetId, slot.assetId, len);
            }
            return slot;
        }
    }

    /// <summary>
    /// Client-side mirror of the on-chain EquippedMoveRegistry PDA.
    /// One per chainling instance; persists equipped loadout on-chain.
    /// PDA seed: ["equipped_moves", owner_pubkey, chainling_instance_id_le_bytes]
    /// On-chain layout: fixed-size array [EquippedMoveSlot; EQUIPPED_SLOTS_ON_CHAIN].
    /// </summary>
    [Serializable]
    public class EquippedMoveRegistryData
    {
        /// <summary>Owner wallet public key (base58).</summary>
        public string owner;

        /// <summary>The ChainlingEntry.instanceId this registry belongs to.</summary>
        public uint chainlingInstanceId;

        /// <summary>Equipped move slots (fixed size on-chain).</summary>
        public EquippedMoveSlot[] slots;

        /// <summary>PDA bump seed.</summary>
        public byte bump;

        /// <summary>PDA address (base58). Derived client-side, not stored on-chain.</summary>
        public string pdaAddress;

        public EquippedMoveRegistryData()
        {
            slots = new EquippedMoveSlot[CombatConstants.EQUIPPED_SLOTS_ON_CHAIN];
        }

        /// <summary>Number of non-empty slots.</summary>
        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                if (slots == null) return 0;
                for (int i = 0; i < slots.Length; i++)
                    if (!slots[i].IsEmpty) count++;
                return count;
            }
        }
    }
}
