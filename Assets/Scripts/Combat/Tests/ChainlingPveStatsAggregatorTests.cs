using System.Collections.Generic;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class ChainlingPveStatsAggregatorTests
    {
        [Test]
        public void AggregateCombatStats_AttributesPveEventTotals()
        {
            BattleResult result = CreateResult(BattleType.PvE, playerWon: true);
            var events = new List<CombatEvent>
            {
                new CombatEvent
                {
                    eventType = CombatEventType.DamageDealt,
                    sourceId = "ally_a",
                    targetId = "enemy_a",
                    intValue = 12,
                },
                new CombatEvent
                {
                    eventType = CombatEventType.PassiveDamageDealt,
                    sourceId = "ally_a",
                    targetId = "enemy_a",
                    intValue = 4,
                },
                new CombatEvent
                {
                    eventType = CombatEventType.DamageDealt,
                    sourceId = "enemy_a",
                    targetId = "ally_a",
                    intValue = 5,
                    metadataJson = WallJson(WallInterceptMode.WallMitigated, incomingDamage: 12, finalDamage: 5, absorbed: 7),
                },
                new CombatEvent
                {
                    eventType = CombatEventType.BarrierBlocked,
                    sourceId = "enemy_a",
                    targetId = "ally_b",
                    intValue = 0,
                    metadataJson = WallJson(WallInterceptMode.WallBlocked, incomingDamage: 9, finalDamage: 0, absorbed: 9),
                },
                new CombatEvent
                {
                    eventType = CombatEventType.HealApplied,
                    sourceId = "ally_b",
                    targetId = "ally_a",
                    intValue = 6,
                },
                new CombatEvent
                {
                    eventType = CombatEventType.ChainlingKO,
                    sourceId = "ally_a",
                    targetId = "enemy_a",
                },
                new CombatEvent
                {
                    eventType = CombatEventType.ChainlingKO,
                    sourceId = "enemy_a",
                    targetId = "ally_b",
                },
            };

            List<ChainlingBattleStatContribution> contributions = BattleStatsAggregator.AggregateCombatStats(events, result);

            ChainlingBattleStatContribution allyA = Find(contributions, "chain_a");
            Assert.AreEqual(16, allyA.delta.damageDealt);
            Assert.AreEqual(5, allyA.delta.damageTaken);
            Assert.AreEqual(7, allyA.delta.damageMitigated);
            Assert.AreEqual(1, allyA.delta.enemyKos);

            ChainlingBattleStatContribution allyB = Find(contributions, "chain_b");
            Assert.AreEqual(6, allyB.delta.healingTotal);
            Assert.AreEqual(9, allyB.delta.damageMitigated);
            Assert.AreEqual(1, allyB.legacyTimesKod);
        }

        [Test]
        public void AggregateCombatStats_SkipsPvPResults()
        {
            BattleResult result = CreateResult(BattleType.PvP, playerWon: true);
            var events = new List<CombatEvent>
            {
                new CombatEvent
                {
                    eventType = CombatEventType.DamageDealt,
                    sourceId = "ally_a",
                    targetId = "enemy_a",
                    intValue = 99,
                },
            };

            List<ChainlingBattleStatContribution> contributions = BattleStatsAggregator.AggregateCombatStats(events, result);

            Assert.AreEqual(0, contributions.Count);
        }

        [Test]
        public void BuildOutcomeBundle_MergesRewardsAndVictoryCredit()
        {
            BattleResult result = CreateResult(BattleType.PvE, playerWon: true);
            var combatContribution = new ChainlingBattleStatContribution
            {
                chainlingBackendId = "chain_a",
                instanceId = "ally_a",
                displayName = "Ally A",
                delta = new ChainlingPveStatDelta { damageDealt = 10 },
            };
            var rewards = new BattleRewardSummary
            {
                playerWon = true,
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        instanceId = "ally_a",
                        backendChainlingId = "chain_a",
                        displayName = "Ally A",
                        xpEarned = 100,
                        previousStars = 1,
                        newStars = 2,
                        gainedStar = true,
                    },
                    new ChainlingXpReward
                    {
                        instanceId = "ally_b",
                        backendChainlingId = "chain_b",
                        displayName = "Ally B",
                        xpEarned = 0,
                    },
                },
            };

            BattleOutcomeBundle bundle = BattleStatsAggregator.BuildOutcomeBundle(
                result,
                rewards,
                new[] { combatContribution });

            ChainlingBattleStatContribution allyA = Find(bundle.chainlingContributions, "chain_a");
            Assert.AreEqual(10, allyA.delta.damageDealt);
            Assert.AreEqual(100, allyA.delta.xpEarnedLifetime);
            Assert.AreEqual(1, allyA.delta.totalVictories);

            ChainlingBattleStatContribution allyB = Find(bundle.chainlingContributions, "chain_b");
            Assert.AreEqual(0, allyB.delta.totalVictories);
            Assert.AreEqual(0, allyB.delta.xpEarnedLifetime);
        }

        [Test]
        public void BuildOutcomeBundle_DoesNotGrantVictoryForFlee()
        {
            BattleResult result = CreateResult(BattleType.PvE, playerWon: true);
            result.wasFled = true;
            var rewards = new BattleRewardSummary
            {
                playerWon = true,
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        instanceId = "ally_a",
                        backendChainlingId = "chain_a",
                        displayName = "Ally A",
                        xpEarned = 25,
                    },
                },
            };

            BattleOutcomeBundle bundle = BattleStatsAggregator.BuildOutcomeBundle(result, rewards);

            ChainlingBattleStatContribution allyA = Find(bundle.chainlingContributions, "chain_a");
            Assert.AreEqual(25, allyA.delta.xpEarnedLifetime);
            Assert.AreEqual(0, allyA.delta.totalVictories);
        }

        private static BattleResult CreateResult(BattleType battleType, bool playerWon)
        {
            return new BattleResult
            {
                battleType = battleType,
                winnerTeamIndex = playerWon ? 0 : 1,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        instanceId = "ally_a",
                        backendChainlingId = "chain_a",
                        displayName = "Ally A",
                    },
                    new BattleParticipant
                    {
                        instanceId = "ally_b",
                        backendChainlingId = "chain_b",
                        displayName = "Ally B",
                    },
                },
            };
        }

        private static ChainlingBattleStatContribution Find(
            IReadOnlyList<ChainlingBattleStatContribution> contributions,
            string backendId)
        {
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i].chainlingBackendId == backendId)
                    return contributions[i];
            }

            Assert.Fail($"Contribution for {backendId} was not found.");
            return null;
        }

        private static string WallJson(
            WallInterceptMode mode,
            int incomingDamage,
            int finalDamage,
            int absorbed)
        {
            return WallInterceptPayload.Create(
                hitIndex: 1,
                totalHits: 1,
                mode: mode,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: true,
                bypassBarrier: false,
                bypassReason: "",
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 0.5f,
                incomingDamage: incomingDamage,
                finalHpDamage: finalDamage,
                barrierFullyBlocked: mode == WallInterceptMode.WallBlocked,
                mitigationSourcesExpired: 0,
                barriersExpired: 0,
                wallExpiredThisStep: false,
                partialPenetration: false,
                wallSourceTag: "",
                damageAbsorbed: absorbed).ToJson();
        }
    }
}
