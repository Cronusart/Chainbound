using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Client-side mirror of the on-chain PlayerProfile PDA.
    /// PDA seed: ["player_profile", owner_pubkey]
    /// RAM is authoritative here (not in MoveVault). Only bounded program
    /// instructions may mutate RAM fields (burn, upgrade spend, vending spend).
    /// </summary>
    [Serializable]
    public class PlayerProfileData
    {
        /// <summary>Owner wallet public key (base58).</summary>
        public string owner;

        /// <summary>Schema version for migration.</summary>
        public int schemaVersion = 2;

        /// <summary>Slot when this profile was first created.</summary>
        public ulong createdAtSlot;

        /// <summary>Total PvE + PvP wins.</summary>
        public uint wins;

        /// <summary>Total PvE + PvP losses.</summary>
        public uint losses;

        /// <summary>Last known scene index (optional; 0 = unset).</summary>
        public uint lastKnownSceneId;

        /// <summary>Last known quantized position (i16 x 3). (0,0,0) = unset.</summary>
        public short lastKnownPosX;
        public short lastKnownPosY;
        public short lastKnownPosZ;

        /// <summary>Active party chainling IDs (up to 3). 0 = empty slot.</summary>
        public ulong[] activePartyChainlingIds;

        // â”€â”€ RAM System (authoritative, on-chain) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Current RAM balance. Non-transferable. Only program instructions can mutate.</summary>
        public uint ramBalance;

        /// <summary>RAM earned in the current daily window. Capped at RAM_DAILY_CAP.</summary>
        public ushort dailyRamEarned;

        /// <summary>Unix timestamp of the last daily RAM reset (UTC midnight boundary).</summary>
        public long lastRamResetTimestamp;

        /// <summary>Lifetime total RAM earned (tracking/analytics).</summary>
        public ulong ramLifetimeEarned;

        // â”€â”€ Daily Vending Counters (mirror future on-chain fields) â”€â”€â”€â”€â”€â”€â”€

        public ushort vendingRollsToday;
        public ushort vendingUpgOrdinalToday;
        public ushort vendingUpgRuneToday;
        public ushort vendingRerollsToday;

        // â”€â”€ PDA Metadata â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>PDA bump seed (stored on-chain for Anchor has_one validation).</summary>
        public byte bump;

        /// <summary>PDA address for this profile (base58). Derived, not stored on-chain.</summary>
        public string pdaAddress;

        public PlayerProfileData()
        {
            activePartyChainlingIds = new ulong[3];
            schemaVersion = 2;
        }

        // â”€â”€ RAM Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>RAM that can still be earned today before hitting the daily cap.</summary>
        public int RemainingDailyRam => Math.Max(0, CombatConstants.RAM_DAILY_CAP - dailyRamEarned);

        /// <summary>Whether the daily RAM cap has been reached.</summary>
        public bool IsDailyRamCapped => dailyRamEarned >= CombatConstants.RAM_DAILY_CAP;

        /// <summary>Whether the player can afford a given RAM cost.</summary>
        public bool CanAffordRam(int cost) => ramBalance >= (uint)cost;

        /// <summary>
        /// Check if the daily reset timestamp is stale and dailyRamEarned should reset.
        /// Uses UTC midnight as the reset boundary.
        /// </summary>
        public bool NeedsDailyReset(long currentUnixTimestamp)
        {
            var lastReset = DateTimeOffset.FromUnixTimeSeconds(lastRamResetTimestamp);
            var now = DateTimeOffset.FromUnixTimeSeconds(currentUnixTimestamp);
            return lastReset.UtcDateTime.Date < now.UtcDateTime.Date;
        }

        /// <summary>Reset the daily RAM counter.</summary>
        public void ResetDaily(long currentUnixTimestamp)
        {
            dailyRamEarned = 0;
            vendingRollsToday = 0;
            vendingUpgOrdinalToday = 0;
            vendingUpgRuneToday = 0;
            vendingRerollsToday = 0;
            lastRamResetTimestamp = currentUnixTimestamp;
        }

        /// <summary>
        /// Quantize a world position into i16 x 3 for on-chain storage.
        /// Multiplies by 100 and clamps to short range.
        /// </summary>
        public void SetQuantizedPosition(float x, float y, float z)
        {
            lastKnownPosX = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)(x * 100f)));
            lastKnownPosY = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)(y * 100f)));
            lastKnownPosZ = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)(z * 100f)));
        }

        /// <summary>
        /// Dequantize stored position back to world coordinates.
        /// </summary>
        public UnityEngine.Vector3 GetDequantizedPosition()
        {
            return new UnityEngine.Vector3(
                lastKnownPosX / 100f,
                lastKnownPosY / 100f,
                lastKnownPosZ / 100f
            );
        }
    }
}
