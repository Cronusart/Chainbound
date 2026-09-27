using System.Collections.Generic;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class BattleOutcomePresentationTests
    {
        [Test]
        public void LastWildCapture_IsCaptureNotDraw()
        {
            BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
            {
                winnerTeamIndex = -1,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
            });

            Assert.AreEqual(BattleOutcomeKind.Capture, presentation.Kind);
            Assert.AreEqual("CAPTURE", presentation.BannerTitle);
            Assert.AreEqual("CAPTURE", presentation.ReportTitle);
            Assert.IsTrue(presentation.ShowRewardsPage);
            Assert.IsTrue(presentation.DidCapture);
        }

        [Test]
        public void KoAfterPriorCapture_IsVictoryWithCaptureFlag()
        {
            BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
            {
                winnerTeamIndex = 0,
                completionReason = BattleCompletionReason.Standard,
                captureAttempted = true,
                captureSucceeded = true,
            });

            Assert.AreEqual(BattleOutcomeKind.Victory, presentation.Kind);
            Assert.AreEqual("VICTORY!!!", presentation.BannerTitle);
            Assert.AreEqual("VICTORY", presentation.ReportTitle);
            Assert.IsTrue(presentation.DidCapture);
            Assert.IsTrue(presentation.ShowRewardsPage);
        }

        [Test]
        public void Flee_IsWithdrawnAndSkipsRewards()
        {
            BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
            {
                winnerTeamIndex = 1,
                wasFled = true,
            });

            Assert.AreEqual(BattleOutcomeKind.Withdrawn, presentation.Kind);
            Assert.AreEqual("WITHDRAWN", presentation.BannerTitle);
            Assert.IsFalse(presentation.ShowRewardsPage);
        }

        [Test]
        public void TrueDraw_IsDraw()
        {
            BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
            {
                winnerTeamIndex = -2,
            });

            Assert.AreEqual(BattleOutcomeKind.Draw, presentation.Kind);
            Assert.AreEqual("DRAW", presentation.BannerTitle);
            Assert.AreEqual("DRAW", presentation.ReportTitle);
        }

        [Test]
        public void PlayerLoss_IsDefeat()
        {
            BattleOutcomePresentation presentation = BattleOutcomePresentation.Resolve(new BattleResult
            {
                winnerTeamIndex = 1,
            });

            Assert.AreEqual(BattleOutcomeKind.Defeat, presentation.Kind);
            Assert.AreEqual("DEFEAT", presentation.BannerTitle);
        }

        [Test]
        public void ResolveCapturedDisplayName_UsesCaptureRecords()
        {
            var result = new BattleResult
            {
                captureAttempted = true,
                captureSucceeded = true,
                captures =
                {
                    new BattleCaptureRecord
                    {
                        displayName = "Validape",
                        subjectLabel = "wild mutated Validape",
                        durationSeconds = 3.5f,
                        succeeded = true,
                    },
                    new BattleCaptureRecord
                    {
                        displayName = "Kitsauto",
                        subjectLabel = "wild Kitsauto",
                        durationSeconds = 6.1f,
                        succeeded = true,
                    },
                },
            };

            Assert.AreEqual("Validape and Kitsauto", BattleOutcomePresentation.ResolveCapturedDisplayName(result));
            List<BattleCaptureRecord> successes = BattleOutcomePresentation.GetSuccessfulCaptures(result);
            Assert.AreEqual(2, successes.Count);
            Assert.AreEqual(
                "Captured: wild mutated Validape in 3.5 seconds",
                CaptureResultCopy.FormatBattleReportCaptureLine(successes[0]));
        }
    }
}
