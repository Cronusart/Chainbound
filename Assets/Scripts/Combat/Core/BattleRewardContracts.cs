#nullable enable
using System;

namespace Chainbound.Combat.Core
{
    public static class BattleRewardStatus
    {
        public const string Zero = "zero";
        public const string Pending = "pending";
        public const string Settled = "settled";
        public const string Failed = "failed";
        public const string Unavailable = "reward_unavailable";
    }

    public static class BattleTerminalOutcome
    {
        public const string Victory = "victory";
        public const string Capture = "capture";
        public const string Defeat = "defeat";
        public const string Draw = "draw";
        public const string Flee = "flee";
        public const string Forfeit = "forfeit";
        public const string Abandon = "abandon";
    }

    public static class BattleRewardTier
    {
        public const string None = "none";
        public const string Standard = "standard";
        public const string Elite = "elite";
        public const string Boss = "boss";
    }

    public static class BattleRewardReceiptValidator
    {
        public static bool IsValid(BattleRewardSettlement settlement, Guid expectedBattleId)
        {
            if (settlement == null ||
                !Guid.TryParse(settlement.BattleId, out Guid battleId) || battleId != expectedBattleId ||
                !Guid.TryParse(settlement.SettlementId, out _) ||
                settlement.RequestedRam < 0 || settlement.AppliedRam < 0 ||
                settlement.AppliedRam > settlement.RequestedRam ||
                !settlement.BalanceBefore.HasValue || !settlement.BalanceAfter.HasValue ||
                settlement.BalanceAfter.Value != settlement.BalanceBefore.Value + settlement.AppliedRam ||
                settlement.BalanceVersion <= 0 ||
                settlement.ReceiptSequence <= 0 || string.IsNullOrWhiteSpace(settlement.SettledAt) ||
                !IsTerminalOutcome(settlement.TerminalOutcome))
            {
                return false;
            }

            if (string.Equals(settlement.Status, BattleRewardStatus.Settled, StringComparison.Ordinal))
                return string.Equals(settlement.TerminalOutcome, BattleTerminalOutcome.Victory, StringComparison.Ordinal) &&
                       settlement.AppliedRam > 0;
            if (string.Equals(settlement.Status, BattleRewardStatus.Zero, StringComparison.Ordinal))
                return settlement.AppliedRam == 0;
            return string.Equals(settlement.Status, BattleRewardStatus.Failed, StringComparison.Ordinal) &&
                   settlement.AppliedRam == 0;
        }

        private static bool IsTerminalOutcome(string value)
        {
            return value == BattleTerminalOutcome.Victory || value == BattleTerminalOutcome.Capture ||
                   value == BattleTerminalOutcome.Defeat || value == BattleTerminalOutcome.Draw ||
                   value == BattleTerminalOutcome.Flee || value == BattleTerminalOutcome.Forfeit ||
                   value == BattleTerminalOutcome.Abandon;
        }
    }

    [Serializable]
    public sealed class BattleRewardSettlement
    {
        public string Status { get; set; } = BattleRewardStatus.Unavailable;
        public string BattleId { get; set; } = "";
        public string SettlementId { get; set; } = "";
        public string TerminalOutcome { get; set; } = "";
        public long RequestedRam { get; set; }
        public long AppliedRam { get; set; }
        public string Reason { get; set; } = "";
        public string RewardTier { get; set; } = BattleRewardTier.Standard;
        public int RewardConfigVersion { get; set; }
        public string RewardConfigHash { get; set; } = "";
        public long? DailyEarned { get; set; }
        public long? DailyCap { get; set; }
        public long? BalanceAfter { get; set; }
        public long? BalanceBefore { get; set; }
        public long BalanceVersion { get; set; }
        public long ReceiptSequence { get; set; }
        public string SettledAt { get; set; } = "";
        public bool AlreadyApplied { get; set; }

        public bool IsTerminal =>
            string.Equals(Status, BattleRewardStatus.Zero, StringComparison.Ordinal) ||
            string.Equals(Status, BattleRewardStatus.Settled, StringComparison.Ordinal) ||
            string.Equals(Status, BattleRewardStatus.Failed, StringComparison.Ordinal);

        public static BattleRewardSettlement Pending(string battleId, string reason = "settlement_pending")
        {
            return new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Pending,
                BattleId = battleId ?? "",
                Reason = reason ?? "settlement_pending",
            };
        }

        public static BattleRewardSettlement Unavailable(string battleId, string reason = "reward_unavailable")
        {
            return new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Unavailable,
                BattleId = battleId ?? "",
                Reason = reason ?? "reward_unavailable",
            };
        }
    }
}
