using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    public static class CombatTextFeedBuilder
    {
        public static string BuildTranscript(IReadOnlyList<CombatLogEvent> events)
        {
            if (events == null || events.Count == 0)
                return string.Empty;

            var sb = new StringBuilder(events.Count * 48);
            int lastTurn = -1;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null) continue;

                if (evt.turnIndex > 0 && evt.turnIndex != lastTurn)
                {
                    lastTurn = evt.turnIndex;
                    sb.AppendLine($"Turn {lastTurn}");
                }

                string line = BuildLine(evt);
                if (!string.IsNullOrEmpty(line))
                    sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }

        private static string BuildLine(CombatLogEvent evt)
        {
            switch (evt.eventType)
            {
                case nameof(CombatForensicEventType.ActionChosen):
                {
                    var payload = Parse<ActionChosenPayload>(evt.payloadJson);
                    if (payload == null) return null;

                    if (payload.actionType == nameof(CombatActionType.UseMove))
                    {
                        string actor = string.IsNullOrEmpty(payload.actorDisplayName) ? Label(evt.actorRef) : payload.actorDisplayName;
                        return $"{actor} used {payload.moveName} ({payload.apCost} AP).";
                    }

                    if (payload.actionType == nameof(CombatActionType.Swap))
                    {
                        string actor = string.IsNullOrEmpty(payload.actorDisplayName) ? Label(evt.actorRef) : payload.actorDisplayName;
                        return $"{actor} prepared a swap.";
                    }

                    return null;
                }

                case nameof(CombatForensicEventType.TargetsResolved):
                {
                    var payload = Parse<TargetsResolvedPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    if (payload.resolvedTargetNames == null || payload.resolvedTargetNames.Count == 0)
                        return "Targets: none.";
                    return $"Targets: {string.Join(", ", payload.resolvedTargetNames)}.";
                }

                case nameof(CombatForensicEventType.DamageResolved):
                {
                    var payload = Parse<DamageResolvedPayload>(evt.payloadJson);
                    if (payload == null || payload.packet == null) return null;
                    string target = string.IsNullOrEmpty(payload.targetDisplayName) ? FirstTargetLabel(evt.targets) : payload.targetDisplayName;
                    string crit = payload.packet.wasCrit ? " (Crit!)" : string.Empty;
                    string hpRemaining = payload.maxHp > 0
                        ? $" ({payload.hpAfter}/{payload.maxHp})"
                        : string.Empty;
                    return $"Hit {target} for {payload.packet.finalDamage} damage!{crit}{hpRemaining}";
                }

                case nameof(CombatForensicEventType.KOResolved):
                {
                    var payload = Parse<KOResolvedPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    string target = string.IsNullOrEmpty(payload.targetDisplayName) ? FirstTargetLabel(evt.targets) : payload.targetDisplayName;
                    return $"{target} was KO'd!";
                }

                case nameof(CombatForensicEventType.SwapResolved):
                {
                    var payload = Parse<SwapResolvedPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    if (string.IsNullOrEmpty(payload.incomingDisplayName))
                        return "Swap resolved.";

                    int teamIndex = payload.incomingRef != null
                        ? payload.incomingRef.teamId
                        : (payload.outgoingRef != null ? payload.outgoingRef.teamId : 1);
                    string subject = CombatNarrationNaming.ResolveTeamSubject(payload.teamName, teamIndex);
                    return string.IsNullOrEmpty(subject)
                        ? $"Swapped in {payload.incomingDisplayName}."
                        : $"{subject} swapped in {payload.incomingDisplayName}.";
                }

                case nameof(CombatForensicEventType.StatusApplied):
                {
                    var payload = Parse<StatusEventPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    string target = string.IsNullOrEmpty(payload.targetDisplayName) ? FirstTargetLabel(evt.targets) : payload.targetDisplayName;
                    return $"{target} gained {payload.statusId}.";
                }

                case nameof(CombatForensicEventType.StatusRefreshed):
                {
                    var payload = Parse<StatusEventPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    string target = string.IsNullOrEmpty(payload.targetDisplayName) ? FirstTargetLabel(evt.targets) : payload.targetDisplayName;
                    return $"{target}'s {payload.statusId} was refreshed.";
                }

                case nameof(CombatForensicEventType.BattleEnded):
                {
                    var payload = Parse<BattleEndedPayload>(evt.payloadJson);
                    if (payload == null) return null;
                    if (string.Equals(payload.endReason, "Fled", StringComparison.Ordinal))
                        return $"Battle ended! {payload.team0Name ?? "Player"} fled after {payload.turnCount} turns.";

                    string winner = payload.winnerTeamIndex switch
                    {
                        0 => payload.team0Name ?? "Team 0",
                        1 => payload.team1Name ?? "Team 1",
                        -2 => "Draw",
                        _ => "Unknown"
                    };
                    return $"Battle ended! {winner} wins after {payload.turnCount} turns ({payload.durationSeconds:F1}s).";
                }
            }

            return null;
        }

        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string Label(CombatEntityRef entityRef)
        {
            if (entityRef == null || string.IsNullOrEmpty(entityRef.instanceId))
                return "Unknown";
            return entityRef.instanceId;
        }

        private static string FirstTargetLabel(List<CombatEntityRef> targets)
        {
            if (targets == null || targets.Count == 0)
                return "target";
            return Label(targets[0]);
        }
    }
}
