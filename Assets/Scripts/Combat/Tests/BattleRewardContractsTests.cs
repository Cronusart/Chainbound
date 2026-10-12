using System;
using Chainbound.Combat.Core;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class BattleRewardContractsTests
    {
        [Test]
        public void ValidatorAcceptsBalancedVictoryReceipt()
        {
            Guid battleId = Guid.NewGuid();
            Assert.IsTrue(BattleRewardReceiptValidator.IsValid(new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Settled,
                BattleId = battleId.ToString("D"),
                SettlementId = Guid.NewGuid().ToString("D"),
                TerminalOutcome = BattleTerminalOutcome.Victory,
                RequestedRam = 25,
                AppliedRam = 5,
                BalanceBefore = 95,
                BalanceAfter = 100,
                BalanceVersion = 9,
                ReceiptSequence = 7,
                SettledAt = "2026-10-09T12:00:00.000Z",
            }, battleId));
        }

        [Test]
        public void ValidatorRejectsImpossibleBalanceOrOutcome()
        {
            Guid battleId = Guid.NewGuid();
            var receipt = new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Settled,
                BattleId = battleId.ToString("D"),
                SettlementId = Guid.NewGuid().ToString("D"),
                TerminalOutcome = BattleTerminalOutcome.Defeat,
                RequestedRam = 5,
                AppliedRam = 5,
                BalanceBefore = 0,
                BalanceAfter = 10,
                BalanceVersion = 10,
                ReceiptSequence = 8,
                SettledAt = "2026-10-09T12:00:00.000Z",
            };

            Assert.IsFalse(BattleRewardReceiptValidator.IsValid(receipt, battleId));
        }
    }
}
