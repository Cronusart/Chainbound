using System.Collections.Generic;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class ChainlingHpPersistenceTests
    {
        [Test]
        public void BuildRequest_MergesBenchedParticipantHpWithoutContributions()
        {
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                seed = 42,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        backendChainlingId = "active-id",
                        instanceId = "inst-active",
                        endCurrentHp = 55,
                        endMaxHp = 100,
                    },
                    new BattleParticipant
                    {
                        backendChainlingId = "bench-id",
                        instanceId = "inst-bench",
                        endCurrentHp = 100,
                        endMaxHp = 100,
                    },
                },
            };

            var outcome = new BattleOutcomeBundle(
                result,
                new BattleRewardSummary(),
                new List<ChainlingBattleStatContribution>
                {
                    new ChainlingBattleStatContribution
                    {
                        chainlingBackendId = "active-id",
                        instanceId = "inst-active",
                        delta = new ChainlingPveStatDelta { damageDealt = 10 },
                    },
                });

            SupabaseBattleOutcomePersistence.OutcomeRequest request =
                SupabaseBattleOutcomePersistence.BuildRequest(outcome, "wallet");

            Assert.AreEqual(2, request.chainlings.Count);

            SupabaseBattleOutcomePersistence.ChainlingOutcomePayload active = FindPayload(request.chainlings, "active-id");
            SupabaseBattleOutcomePersistence.ChainlingOutcomePayload bench = FindPayload(request.chainlings, "bench-id");

            Assert.IsTrue(active.sync_current_hp);
            Assert.AreEqual(55, active.current_hp);
            Assert.AreEqual(10, active.damage_dealt);

            Assert.IsTrue(bench.sync_current_hp);
            Assert.AreEqual(100, bench.current_hp);
            Assert.AreEqual(0, bench.damage_dealt);
        }

        [Test]
        public void BuildRequest_DefeatParticipant_PersistsZeroHp()
        {
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        backendChainlingId = "fainted-id",
                        endCurrentHp = 0,
                        endMaxHp = 90,
                    },
                },
            };

            var outcome = new BattleOutcomeBundle(result, new BattleRewardSummary(), new List<ChainlingBattleStatContribution>());
            SupabaseBattleOutcomePersistence.OutcomeRequest request =
                SupabaseBattleOutcomePersistence.BuildRequest(outcome, "");

            Assert.AreEqual(1, request.chainlings.Count);
            Assert.IsTrue(request.chainlings[0].sync_current_hp);
            Assert.AreEqual(0, request.chainlings[0].current_hp);
        }

        [Test]
        public void BuildRequest_RecoveredDefeatParticipant_PersistsRecoveryHp()
        {
            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        backendChainlingId = "recovered-id",
                        wasKOd = true,
                        endCurrentHp = 45,
                        endMaxHp = 90,
                    },
                },
            };

            var outcome = new BattleOutcomeBundle(result, new BattleRewardSummary(), new List<ChainlingBattleStatContribution>());
            SupabaseBattleOutcomePersistence.OutcomeRequest request =
                SupabaseBattleOutcomePersistence.BuildRequest(outcome, "");

            Assert.AreEqual(1, request.chainlings.Count);
            Assert.IsTrue(request.chainlings[0].sync_current_hp);
            Assert.AreEqual(45, request.chainlings[0].current_hp);
        }

        private static SupabaseBattleOutcomePersistence.ChainlingOutcomePayload FindPayload(
            List<SupabaseBattleOutcomePersistence.ChainlingOutcomePayload> payloads,
            string backendId)
        {
            for (int i = 0; i < payloads.Count; i++)
            {
                if (payloads[i].chainling_id == backendId)
                    return payloads[i];
            }

            Assert.Fail($"Missing payload for {backendId}");
            return null;
        }
    }
}
