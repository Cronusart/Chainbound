using System;

namespace Chainbound.Combat
{
    /// <summary>Zero-modifier Known move stored in the authoritative MoveVault PDA.</summary>
    [Serializable]
    public struct KnownMoveVaultEntry
    {
        public ulong instanceId;
        public ushort moveId;
        public byte element;
        public ushort basePower;
        public uint generatorVersion;
        public byte[] poolHash;

        public KnownMoveVaultEntry(
            ulong instanceId,
            ushort moveId,
            byte element,
            ushort basePower,
            uint generatorVersion,
            byte[] poolHash)
        {
            this.instanceId = instanceId;
            this.moveId = moveId;
            this.element = element;
            this.basePower = basePower;
            this.generatorVersion = generatorVersion;
            this.poolHash = poolHash;
        }
    }
}
