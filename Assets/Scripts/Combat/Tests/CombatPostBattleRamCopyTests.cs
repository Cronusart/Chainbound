using Chainbound.Combat.Core;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatPostBattleRamCopyTests
    {
        [Test]
        public void FormatRamTicker_Victory_ReportsAppliedReceipt()
        {
            Assert.AreEqual(
                "+12 RAM — granted",
                CombatPostBattleReportController.FormatRamTicker(false, Settled(12, 12, 12, 100)));
        }

        [Test]
        public void FormatRamTicker_ZeroOrDefeat_DoesNotPromiseAGrant()
        {
            Assert.AreEqual("RAM unchanged this battle", CombatPostBattleReportController.FormatRamTicker(false, Zero("capture")));
            Assert.AreEqual("No RAM lost this battle", CombatPostBattleReportController.FormatRamTicker(true, Zero("defeat")));
        }

        [Test]
        public void FormatRamTicker_DoesNotUseSettlementJargon()
        {
            string copy = CombatPostBattleReportController.FormatRamTicker(false, Settled(5, 5, 5, 100));
            StringAssert.DoesNotContain("authoritative", copy.ToLowerInvariant());
            StringAssert.DoesNotContain("settlement", copy.ToLowerInvariant());
        }

        [Test]
        public void FormatRamTicker_DailyCap_ReportsPartialOrZeroTruthfully()
        {
            Assert.AreEqual(
                "+5 RAM — daily cap reached",
                CombatPostBattleReportController.FormatRamTicker(false, Settled(25, 5, 100, 100)));
            Assert.AreEqual(
                "Daily RAM cap reached",
                CombatPostBattleReportController.FormatRamTicker(false, Settled(5, 0, 100, 100, "daily_cap_reached")));
        }

        [Test]
        public void FormatRamTicker_MissingReceipt_DoesNotShowLocalPreview()
        {
            string copy = CombatPostBattleReportController.FormatRamTicker(false, 12);
            Assert.AreEqual("RAM reward unavailable — check your balance", copy);
            StringAssert.DoesNotContain("12", copy);
        }

        [Test]
        public void FormatRewardsIntro_VictoryWithPreviewRam_DoesNotPromiseGrant()
        {
            var summary = new BattleRewardSummary
            {
                ramEarned = 8,
                chainlingRewards = { new ChainlingXpReward { xpEarned = 20 } },
            };

            string copy = CombatPostBattleReportController.FormatRewardsIntro(
                false,
                BattleOutcomeKind.Victory,
                string.Empty,
                summary);

            StringAssert.DoesNotContain("grants RAM", copy);
            StringAssert.Contains("overworld", copy);
            StringAssert.DoesNotContain("client-resolved", copy);
        }

        [Test]
        public void FormatDefeatCopy_DropsClientResolvedJargon()
        {
            string copy = CombatPostBattleReportController.FormatDefeatCopy(1, 50);
            StringAssert.Contains("Confirm returns you to your recovery point.", copy);
            StringAssert.DoesNotContain("client-resolved", copy);
            StringAssert.DoesNotContain("settled", copy);
        }

        private static BattleRewardSettlement Settled(
            long requested,
            long applied,
            long dailyEarned,
            long dailyCap,
            string reason = "granted")
        {
            return new BattleRewardSettlement
            {
                Status = applied > 0 ? BattleRewardStatus.Settled : BattleRewardStatus.Zero,
                RequestedRam = requested,
                AppliedRam = applied,
                DailyEarned = dailyEarned,
                DailyCap = dailyCap,
                BalanceAfter = applied,
                Reason = reason,
            };
        }

        private static BattleRewardSettlement Zero(string reason)
        {
            return new BattleRewardSettlement
            {
                Status = BattleRewardStatus.Zero,
                Reason = reason,
                BalanceAfter = 0,
            };
        }
    }
}
