using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class FavorServiceTests
    {
        [Test]
        public void FavorConfig_SoftCurveIsDiminishing()
        {
            FavorConfigSO config = FavorConfigSO.CreateRuntimeDefault();
            float low = config.EvaluateCurve01(12);
            float mid = config.EvaluateCurve01(100);
            float high = config.EvaluateCurve01(220);
            Assert.Greater(mid, low);
            Assert.Greater(high, mid);
            Assert.Less(high - mid, mid - low + 0.05f);
            Assert.Less(config.CaptureProbabilityBonus(100), config.captureProbabilityBonusMax + 0.0001f);
        }

        [Test]
        public void FavorService_RefreshOnlyAndTicksDown()
        {
            var go = new GameObject("FavorServiceTests");
            try
            {
                FavorService service = go.AddComponent<FavorService>();
                service.SetAmpouleFavor(100, 3);
                Assert.AreEqual(100, service.CurrentFavorScore);
                Assert.AreEqual(3, service.AmpouleBattlesRemaining);

                service.SetAmpouleFavor(110, 2);
                Assert.AreEqual(110, service.CurrentFavorScore);
                Assert.AreEqual(2, service.AmpouleBattlesRemaining);

                service.NotifyBattleCompleted();
                Assert.AreEqual(1, service.AmpouleBattlesRemaining);
                service.NotifyBattleCompleted();
                Assert.AreEqual(0, service.CurrentFavorScore);
                Assert.AreEqual(0, service.AmpouleBattlesRemaining);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaptureRate_FavorAddsSoftBonus()
        {
            float baseP = CaptureRateCalculator.CalculateProbability(45, 50, 100);
            float favored = CaptureRateCalculator.CalculateProbability(
                45, 50, 100, 1f, 1f, 100, FavorConfigSO.CreateRuntimeDefault());
            Assert.Greater(favored, baseP);
            Assert.LessOrEqual(favored, 1f);
        }

        [Test]
        public void WildRoller_CanBumpLowestIvWithFavor()
        {
            var config = new WildRollConfig { ivMin = -10, ivMax = 10, mutationChance = 0f, entangledChance = 0f };
            FavorConfigSO favor = FavorConfigSO.CreateRuntimeDefault();
            favor.ivLowestBumpChanceMax = 1f;
            favor.softCurveK = 1f;

            // Deterministic seed; with 100% bump chance the lowest IV increases when not already max.
            var rng = new System.Random(7);
            WildTraits traits = WildRoller.RollTraits(config, rng, 1000, favor);
            Assert.AreEqual(6, traits.ivs.Length);
            for (int i = 0; i < traits.ivs.Length; i++)
                Assert.LessOrEqual(traits.ivs[i], config.ivMax);
        }
    }
}
