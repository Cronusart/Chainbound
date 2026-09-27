using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class BattleDefeatRamPenaltyTests
    {
        private PostBattleRewardsConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = PostBattleRewardsConfigSO.CreateRuntimeDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
                Object.DestroyImmediate(_config);
        }

        [Test]
        public void Calculate_PveLoss_AppliesFlatPenaltyWithoutRewards()
        {
            var summary = new StandardBattleRewardCalculator(_config).Calculate(new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
            });

            Assert.AreEqual(5, summary.ramPenalty);
            Assert.AreEqual(0, summary.ramEarned);
            Assert.AreEqual(0, summary.chainlingRewards.Count);
        }

        [Test]
        public void Calculate_FleeOrDraw_NeverAppliesPenalty()
        {
            var calculator = new StandardBattleRewardCalculator(_config);

            BattleRewardSummary fled = calculator.Calculate(new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
                wasFled = true,
            });
            BattleRewardSummary draw = calculator.Calculate(new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = -2,
            });

            Assert.AreEqual(0, fled.ramPenalty);
            Assert.AreEqual(0, draw.ramPenalty);
        }

        [Test]
        public void Calculate_PveLoss_UsesLevelScalingWhenEnabled()
        {
            _config.defeatRamPenaltyFlat = 5;
            _config.defeatRamPenaltyPerPlayerLevel = 2;

            var summary = new StandardBattleRewardCalculator(_config).Calculate(new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
                playerLevel = 3,
            });

            Assert.AreEqual(11, summary.ramPenalty);
        }

        [Test]
        public void Calculate_CaptureTerminal_DoesNotApplyPenaltyAndStillCalculates()
        {
            _config.capturedReceivesXp = true;
            var summary = new StandardBattleRewardCalculator(_config).Calculate(new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = -1,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
                totalEnemyKOs = 1,
                playerParticipants =
                {
                    new BattleParticipant { displayName = "Validape", speciesId = "validape", masteryXp = 0 }
                }
            });

            Assert.AreEqual(0, summary.ramPenalty);
            Assert.Greater(summary.chainlingRewards.Count, 0);
        }
    }
}
