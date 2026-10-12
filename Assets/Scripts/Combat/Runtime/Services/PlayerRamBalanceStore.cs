using System;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Process-local projection of the Postgres-canonical RAM balance. Every source is
    /// ordered by the same server-issued ram_balance_version.
    /// </summary>
    public static class PlayerRamBalanceStore
    {
        public readonly struct ReadToken
        {
            internal ReadToken(string playerProfileId, long revision)
            {
                PlayerProfileId = playerProfileId;
                Revision = revision;
            }

            public string PlayerProfileId { get; }
            internal long Revision { get; }
        }

        private static string currentProfileId = "";
        private static long currentBalance;
        private static long revision;
        private static long currentBalanceVersion;
        private static bool hasValue;

        public static event Action<string, long> BalanceChanged;

        public static ReadToken BeginProfileRead(string playerProfileId)
        {
            string profileId = Normalize(playerProfileId);
            if (!string.IsNullOrWhiteSpace(profileId) &&
                hasValue &&
                !string.Equals(currentProfileId, profileId, StringComparison.Ordinal))
            {
                currentProfileId = profileId;
                currentBalance = 0;
                currentBalanceVersion = 0;
                hasValue = false;
                revision++;
            }
            return new ReadToken(profileId, revision);
        }

        public static bool TryApplyProfileRead(ReadToken token, long balance, long balanceVersion)
        {
            string profileId = token.PlayerProfileId;
            if (string.IsNullOrWhiteSpace(profileId) || token.Revision != revision)
                return false;
            if (hasValue && !string.Equals(currentProfileId, profileId, StringComparison.Ordinal))
                return false;

            return TryApplyAuthoritative(profileId, balance, balanceVersion);
        }

        public static bool ApplySettlement(string playerProfileId, BattleRewardSettlement settlement)
        {
            if (settlement == null || !settlement.BalanceAfter.HasValue)
                return false;
            if (!string.Equals(settlement.Status, BattleRewardStatus.Settled, StringComparison.Ordinal) &&
                !string.Equals(settlement.Status, BattleRewardStatus.Zero, StringComparison.Ordinal))
            {
                return false;
            }

            string profileId = Normalize(playerProfileId);
            if (string.IsNullOrWhiteSpace(profileId))
                return false;
            return TryApplyAuthoritative(profileId, settlement.BalanceAfter.Value, settlement.BalanceVersion);
        }

        public static bool TryApplyAuthoritative(string playerProfileId, long balance, long balanceVersion)
        {
            string profileId = Normalize(playerProfileId);
            if (string.IsNullOrWhiteSpace(profileId) || balanceVersion <= 0)
                return false;
            if (hasValue && string.Equals(currentProfileId, profileId, StringComparison.Ordinal))
            {
                if (balanceVersion < currentBalanceVersion)
                    return false;
                if (balanceVersion == currentBalanceVersion)
                    return currentBalance == Math.Max(0, balance);
            }

            Apply(profileId, balance);
            currentBalanceVersion = balanceVersion;
            return true;
        }

        public static bool TryGet(string playerProfileId, out long balance)
        {
            string profileId = Normalize(playerProfileId);
            if (hasValue && string.Equals(currentProfileId, profileId, StringComparison.Ordinal))
            {
                balance = currentBalance;
                return true;
            }

            balance = 0;
            return false;
        }

        public static void Clear()
        {
            currentProfileId = "";
            currentBalance = 0;
            hasValue = false;
            currentBalanceVersion = 0;
            revision++;
        }

        private static void Apply(string playerProfileId, long balance)
        {
            long normalizedBalance = Math.Max(0, balance);
            bool changed = !hasValue ||
                           !string.Equals(currentProfileId, playerProfileId, StringComparison.Ordinal) ||
                           currentBalance != normalizedBalance;
            currentProfileId = playerProfileId;
            currentBalance = normalizedBalance;
            hasValue = true;
            revision++;
            if (changed)
                BalanceChanged?.Invoke(currentProfileId, currentBalance);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }
}
