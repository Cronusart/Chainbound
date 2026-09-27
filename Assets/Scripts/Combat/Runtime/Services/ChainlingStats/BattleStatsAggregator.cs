using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Chainbound.Combat
{
    public static class BattleStatsAggregator
    {
        public static List<ChainlingBattleStatContribution> AggregateCombatStats(
            IReadOnlyList<CombatEvent> events,
            BattleResult result)
        {
            var output = new Dictionary<string, ChainlingBattleStatContribution>(StringComparer.Ordinal);
            if (events == null || result == null || result.battleType != BattleType.PvE)
                return new List<ChainlingBattleStatContribution>();

            var byInstanceId = BuildParticipantMap(result.playerParticipants);
            if (byInstanceId.Count == 0)
                return new List<ChainlingBattleStatContribution>();

            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent evt = events[i];
                switch (evt.eventType)
                {
                    case CombatEventType.DamageDealt:
                    case CombatEventType.HPDotDamage:
                    case CombatEventType.HPRecoilDamage:
                    case CombatEventType.PassiveDamageDealt:
                        ApplyDamage(evt, byInstanceId, output);
                        ApplyMitigation(evt, byInstanceId, output);
                        break;
                    case CombatEventType.BarrierBlocked:
                        ApplyMitigation(evt, byInstanceId, output);
                        break;
                    case CombatEventType.HealApplied:
                        ApplyHealing(evt, byInstanceId, output);
                        break;
                    case CombatEventType.ChainlingKO:
                        ApplyKo(evt, byInstanceId, output);
                        break;
                }
            }

            return SortedContributions(output);
        }

        public static BattleOutcomeBundle BuildOutcomeBundle(
            BattleResult result,
            BattleRewardSummary rewardSummary,
            IEnumerable<ChainlingBattleStatContribution> combatContributions = null)
        {
            var merged = new Dictionary<string, ChainlingBattleStatContribution>(StringComparer.Ordinal);

            if (result == null || result.battleType != BattleType.PvE)
                return new BattleOutcomeBundle(result, rewardSummary, Array.Empty<ChainlingBattleStatContribution>());

            IEnumerable<ChainlingBattleStatContribution> source = combatContributions;
            if (source == null)
                source = result.pveStatContributions;
            if (source == null)
                source = Array.Empty<ChainlingBattleStatContribution>();

            foreach (var contribution in source)
            {
                if (contribution == null || string.IsNullOrWhiteSpace(contribution.chainlingBackendId))
                    continue;

                GetOrCreate(merged, contribution.chainlingBackendId, contribution.instanceId, contribution.displayName)
                    .delta.Add(contribution.delta);
                GetOrCreate(merged, contribution.chainlingBackendId, contribution.instanceId, contribution.displayName)
                    .legacyTimesKod += Math.Max(0, contribution.legacyTimesKod);
            }

            MergeRewards(result, rewardSummary, merged);

            return new BattleOutcomeBundle(
                result,
                rewardSummary,
                SortedContributions(merged),
                result.forensicHash,
                result.configHash);
        }

        public static string ComputeLegacyEventHash(IReadOnlyList<CombatEvent> events, int seed)
        {
            if (events == null || events.Count == 0)
                return string.Empty;

            using (SHA256 sha = SHA256.Create())
            {
                var builder = new StringBuilder();
                builder.Append(seed);
                builder.Append('|');

                for (int i = 0; i < events.Count; i++)
                {
                    CombatEvent evt = events[i];
                    builder.Append(evt.timestamp).Append(':')
                        .Append(evt.turn).Append(':')
                        .Append(evt.eventType).Append(':')
                        .Append(evt.sourceId).Append(':')
                        .Append(evt.targetId).Append(':')
                        .Append(evt.moveId).Append(':')
                        .Append(evt.intValue).Append(':')
                        .Append(evt.intValue2).Append(':')
                        .Append(evt.metadataJson).Append('|');
                }

                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                byte[] hash = sha.ComputeHash(bytes);
                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        private static void MergeRewards(
            BattleResult result,
            BattleRewardSummary rewardSummary,
            Dictionary<string, ChainlingBattleStatContribution> merged)
        {
            if (rewardSummary?.chainlingRewards == null)
                return;

            bool grantVictory = result.PlayerWon && !result.wasFled;

            for (int i = 0; i < rewardSummary.chainlingRewards.Count; i++)
            {
                ChainlingXpReward reward = rewardSummary.chainlingRewards[i];
                if (reward == null || string.IsNullOrWhiteSpace(reward.backendChainlingId))
                    continue;

                var contribution = GetOrCreate(
                    merged,
                    reward.backendChainlingId,
                    reward.instanceId,
                    reward.displayName);

                int xpEarned = Math.Max(0, reward.xpEarned);
                contribution.delta.xpEarnedLifetime += xpEarned;

                if (grantVictory && xpEarned > 0)
                    contribution.delta.totalVictories += 1;

                contribution.delta.progressionMovesGranted += CountAppliedProgressionGrants(reward);
            }
        }

        private static int CountAppliedProgressionGrants(ChainlingXpReward reward)
        {
            if (reward?.progressionGrants == null)
                return 0;

            int count = 0;
            for (int i = 0; i < reward.progressionGrants.Count; i++)
            {
                ProgressionGrantResult grant = reward.progressionGrants[i];
                if (grant != null && grant.grantSucceeded && !grant.alreadyApplied)
                    count++;
            }

            return count;
        }

        private static Dictionary<string, BattleParticipant> BuildParticipantMap(IReadOnlyList<BattleParticipant> participants)
        {
            var map = new Dictionary<string, BattleParticipant>(StringComparer.Ordinal);
            if (participants == null)
                return map;

            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipant participant = participants[i];
                if (participant == null ||
                    string.IsNullOrWhiteSpace(participant.instanceId) ||
                    string.IsNullOrWhiteSpace(participant.backendChainlingId))
                {
                    continue;
                }

                map[participant.instanceId] = participant;
            }

            return map;
        }

        private static void ApplyDamage(
            CombatEvent evt,
            Dictionary<string, BattleParticipant> byInstanceId,
            Dictionary<string, ChainlingBattleStatContribution> output)
        {
            int amount = Math.Max(0, evt.intValue);
            if (amount <= 0)
                return;

            if (!string.IsNullOrWhiteSpace(evt.sourceId) &&
                byInstanceId.TryGetValue(evt.sourceId, out BattleParticipant source))
            {
                GetOrCreate(output, source).delta.damageDealt += amount;
            }

            if (!string.IsNullOrWhiteSpace(evt.targetId) &&
                byInstanceId.TryGetValue(evt.targetId, out BattleParticipant target))
            {
                GetOrCreate(output, target).delta.damageTaken += amount;
            }
        }

        private static void ApplyMitigation(
            CombatEvent evt,
            Dictionary<string, BattleParticipant> byInstanceId,
            Dictionary<string, ChainlingBattleStatContribution> output)
        {
            if (string.IsNullOrWhiteSpace(evt.targetId) ||
                !byInstanceId.TryGetValue(evt.targetId, out BattleParticipant target))
            {
                return;
            }

            if (!WallInterceptPayload.TryFromEvent(evt, out WallInterceptPayload payload))
                return;

            int absorbed = Math.Max(0, payload.damageAbsorbed);
            if (absorbed <= 0 && payload.barrierFullyBlocked)
                absorbed = Math.Max(0, payload.incomingDamage - payload.finalHpDamage);

            if (absorbed > 0)
                GetOrCreate(output, target).delta.damageMitigated += absorbed;
        }

        private static void ApplyHealing(
            CombatEvent evt,
            Dictionary<string, BattleParticipant> byInstanceId,
            Dictionary<string, ChainlingBattleStatContribution> output)
        {
            int amount = Math.Max(0, evt.intValue);
            if (amount <= 0 ||
                string.IsNullOrWhiteSpace(evt.sourceId) ||
                !byInstanceId.TryGetValue(evt.sourceId, out BattleParticipant source))
            {
                return;
            }

            GetOrCreate(output, source).delta.healingTotal += amount;
        }

        private static void ApplyKo(
            CombatEvent evt,
            Dictionary<string, BattleParticipant> byInstanceId,
            Dictionary<string, ChainlingBattleStatContribution> output)
        {
            if (!string.IsNullOrWhiteSpace(evt.sourceId) &&
                byInstanceId.TryGetValue(evt.sourceId, out BattleParticipant source) &&
                !byInstanceId.ContainsKey(evt.targetId ?? string.Empty))
            {
                GetOrCreate(output, source).delta.enemyKos += 1;
            }

            if (!string.IsNullOrWhiteSpace(evt.targetId) &&
                byInstanceId.TryGetValue(evt.targetId, out BattleParticipant target))
            {
                GetOrCreate(output, target).legacyTimesKod += 1;
            }
        }

        private static ChainlingBattleStatContribution GetOrCreate(
            Dictionary<string, ChainlingBattleStatContribution> map,
            BattleParticipant participant)
        {
            return GetOrCreate(map, participant.backendChainlingId, participant.instanceId, participant.displayName);
        }

        private static ChainlingBattleStatContribution GetOrCreate(
            Dictionary<string, ChainlingBattleStatContribution> map,
            string backendChainlingId,
            string instanceId,
            string displayName)
        {
            string key = backendChainlingId ?? string.Empty;
            if (!map.TryGetValue(key, out ChainlingBattleStatContribution contribution))
            {
                contribution = new ChainlingBattleStatContribution
                {
                    chainlingBackendId = key,
                    instanceId = instanceId ?? string.Empty,
                    displayName = displayName ?? string.Empty,
                    delta = new ChainlingPveStatDelta(),
                };
                map[key] = contribution;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(contribution.instanceId))
                    contribution.instanceId = instanceId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(contribution.displayName))
                    contribution.displayName = displayName ?? string.Empty;
            }

            return contribution;
        }

        private static List<ChainlingBattleStatContribution> SortedContributions(
            Dictionary<string, ChainlingBattleStatContribution> map)
        {
            var list = new List<ChainlingBattleStatContribution>();
            foreach (var pair in map)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    list.Add(pair.Value.Clone());
            }

            list.Sort((a, b) => string.CompareOrdinal(a.chainlingBackendId, b.chainlingBackendId));
            return list;
        }
    }
}
