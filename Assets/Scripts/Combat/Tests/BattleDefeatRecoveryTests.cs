using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class BattleDefeatRecoveryTests
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
        public void ApplyToPveDefeat_RevivesOnlyKoParticipantsAtHalfBattleMaxHp()
        {
            var ko = new BattleParticipant { wasKOd = true, endCurrentHp = 0, endMaxHp = 101 };
            var survivor = new BattleParticipant { wasKOd = false, endCurrentHp = 24, endMaxHp = 100 };
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
                playerParticipants = new List<BattleParticipant> { ko, survivor },
            };

            int recovered = BattleDefeatRecoveryUtility.ApplyToPveDefeat(result, _config);

            Assert.AreEqual(1, recovered);
            Assert.AreEqual(51, ko.endCurrentHp);
            Assert.IsTrue(ko.wasKOd, "The battle result must retain the KO fact for reward and telemetry rules.");
            Assert.AreEqual(24, survivor.endCurrentHp);
        }

        [Test]
        public void ApplyToPveDefeat_ClampsPositiveFractionToLivingHp()
        {
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant { wasKOd = true, endMaxHp = 1 },
                },
            };

            int recovered = BattleDefeatRecoveryUtility.ApplyToPveDefeat(result, _config);

            Assert.AreEqual(1, recovered);
            Assert.AreEqual(1, result.playerParticipants[0].endCurrentHp);
        }

        [Test]
        public void ApplyToPveDefeat_ZeroRecoveryFractionDoesNotRevive()
        {
            _config.defeatKoRespawnHpFraction = 0f;
            var participant = new BattleParticipant { wasKOd = true, endCurrentHp = 0, endMaxHp = 100 };
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 1,
                playerParticipants = new List<BattleParticipant> { participant },
            };

            Assert.AreEqual(0, BattleDefeatRecoveryUtility.ApplyToPveDefeat(result, _config));
            Assert.AreEqual(0, participant.endCurrentHp);
        }

        [TestCase(BattleType.PvE, 0, false)]
        [TestCase(BattleType.PvE, -2, false)]
        [TestCase(BattleType.PvE, 1, true)]
        [TestCase(BattleType.PvP, 1, false)]
        public void ApplyToPveDefeat_DoesNotReviveOutsideAnOrdinaryPveLoss(
            BattleType battleType,
            int winnerTeamIndex,
            bool wasFled)
        {
            var participant = new BattleParticipant { wasKOd = true, endCurrentHp = 0, endMaxHp = 100 };
            var result = new BattleResult
            {
                battleType = battleType,
                winnerTeamIndex = winnerTeamIndex,
                wasFled = wasFled,
                playerParticipants = new List<BattleParticipant> { participant },
            };

            Assert.AreEqual(0, BattleDefeatRecoveryUtility.ApplyToPveDefeat(result, _config));
            Assert.AreEqual(0, participant.endCurrentHp);
        }
    }
}
