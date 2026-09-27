using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Structured combat event log.
    /// Legacy events are kept for existing presenters/UI.
    /// Forensic events are stored in a typed envelope for export and transcript generation.
    /// </summary>
    public class CombatEventLog
    {
        private readonly List<CombatEvent> _events;
        private readonly int _seed;
        private readonly Dictionary<string, string> _nameRegistry;

        private readonly string _battleId;
        private readonly List<CombatLogEvent> _forensicEvents;
        private readonly Dictionary<string, CombatEntityRef> _entityRefs;
        private CombatRng _rng;
        private int _forensicSequence;
        private int _contextTurn = -1;
        private int _contextAction = -1;
        private BattleResult _battleEndedResult;
        private string _team0Name;
        private string _team1Name;
        private string _rulesetId;
        private string _configHash;

        public IReadOnlyList<CombatEvent> Events => _events;
        public int Seed => _seed;
        public int EventCount => _events.Count;
        public string BattleId => _battleId;
        public IReadOnlyList<CombatLogEvent> ForensicEvents => _forensicEvents;
        public int ForensicEventCount => _forensicEvents.Count;
        public string RulesetId => _rulesetId;
        public string ConfigHash => _configHash;

        public CombatEventLog(int seed)
        {
            _seed = seed;
            _events = new List<CombatEvent>(256);
            _nameRegistry = new Dictionary<string, string>();

            _battleId = Guid.NewGuid().ToString("N");
            _forensicEvents = new List<CombatLogEvent>(512);
            _entityRefs = new Dictionary<string, CombatEntityRef>(64);
            _forensicSequence = 0;
        }

        public void AttachRng(CombatRng rng)
        {
            _rng = rng;
        }

        public void SetForensicContext(int turnIndex, int actionIndex)
        {
            _contextTurn = turnIndex;
            _contextAction = actionIndex;
        }

        public void SyncEntityRefs(CombatState state)
        {
            _entityRefs.Clear();
            if (state == null) return;
            SyncTeamRefs(state.team0);
            SyncTeamRefs(state.team1);
        }

        public void SetConfigStampMetadata(string rulesetId, string configHash)
        {
            _rulesetId = rulesetId ?? string.Empty;
            _configHash = configHash ?? string.Empty;
        }

        public CombatEntityRef ResolveEntityRef(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            if (_entityRefs.TryGetValue(instanceId, out var found))
                return CloneEntityRef(found);

            return new CombatEntityRef
            {
                teamId = -1,
                slotId = -1,
                instanceId = instanceId,
            };
        }

        public void LogForensic(
            CombatForensicEventType eventType,
            object payload = null,
            string actorInstanceId = null,
            List<string> targetInstanceIds = null,
            int? turnIndex = null,
            int? actionIndex = null)
        {
            if (!CombatLogSettings.EnableForensicLogging)
                return;

            var evt = new CombatLogEvent
            {
                sequenceIndex = _forensicSequence++,
                battleId = _battleId,
                turnIndex = turnIndex ?? _contextTurn,
                actionIndex = actionIndex ?? _contextAction,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                eventType = eventType.ToString(),
                actorRef = ResolveEntityRef(actorInstanceId),
                rngSeed = _seed,
                rngStepIndex = _rng != null ? _rng.CallCount : 0,
                payloadJson = SerializePayload(payload),
            };

            if (targetInstanceIds != null && targetInstanceIds.Count > 0)
            {
                for (int i = 0; i < targetInstanceIds.Count; i++)
                {
                    var targetRef = ResolveEntityRef(targetInstanceIds[i]);
                    if (targetRef != null)
                        evt.targets.Add(targetRef);
                }
            }

            AppendForensic(evt);
        }

        public List<CombatLogEvent> GetForensicEventsSnapshot()
        {
            return new List<CombatLogEvent>(_forensicEvents);
        }

        // Name Registry -------------------------------------------------------

        /// <summary>Register a display name for an instance ID (Chainling or move).</summary>
        public void RegisterName(string instanceId, string displayName)
        {
            if (!string.IsNullOrEmpty(instanceId))
                _nameRegistry[instanceId] = displayName;
        }

        /// <summary>Resolve an instance ID to its display name, or return the ID if unregistered.</summary>
        public string Name(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return "?";
            return _nameRegistry.TryGetValue(instanceId, out var name) ? name : instanceId;
        }

        // Core Legacy Logging -------------------------------------------------

        /// <summary>Add an event to the legacy log.</summary>
        public void Log(CombatEvent evt)
        {
            evt.timestamp = _events.Count;
            _events.Add(evt);
        }

        /// <summary>Shorthand: log a simple message event.</summary>
        public void LogMessage(CombatEventType type, string message, int turn = -1)
        {
            Log(new CombatEvent
            {
                eventType = type,
                turn = turn,
                message = message,
            });
        }

        /// <summary>Log a damage event with legacy context.</summary>
        /// <param name="hpAfter">Remaining HP of target after damage (0 = omit from message).</param>
        /// <param name="maxHp">Max HP of target for display (0 = omit from message).</param>
        public void LogDamage(int turn, string attackerId, string defenderId,
            string moveId, int damage, bool isCrit, float chemMultiplier,
            int hpAfter = 0, int maxHp = 0, string metadataJson = null)
        {
            string atkName = Name(attackerId);
            string defName = Name(defenderId);
            string moveName = Name(moveId);

            string hpSuffix = (maxHp > 0 && hpAfter >= 0)
                ? $" ({hpAfter}/{maxHp})"
                : string.Empty;

            Log(new CombatEvent
            {
                eventType = CombatEventType.DamageDealt,
                turn = turn,
                sourceId = attackerId,
                targetId = defenderId,
                moveId = moveId,
                intValue = damage,
                boolValue = isCrit,
                floatValue = chemMultiplier,
                message = $"Hit {defName} for {damage} damage!" +
                          (isCrit ? " (Crit!)" : "") +
                          (Math.Abs(chemMultiplier - 1f) > 0.0001f ? $" (chem x{chemMultiplier:F2})" : "") +
                          hpSuffix,
                metadataJson = metadataJson
            });
        }

        /// <summary>Log when a barrier fully absorbs damage.</summary>
        public void LogBarrierBlocked(int turn, string attackerId, string defenderId, string moveId, string metadataJson = null)
        {
            string atkName = Name(attackerId);
            string defName = Name(defenderId);
            Log(new CombatEvent
            {
                eventType = CombatEventType.BarrierBlocked,
                turn = turn,
                sourceId = attackerId,
                targetId = defenderId,
                moveId = moveId,
                message = $"{atkName}'s attack was blocked by {defName}'s barrier!",
                metadataJson = metadataJson
            });
        }

        public void LogWallExpired(int turn, string targetId, string sourceTag, string metadataJson = null)
        {
            string defName = Name(targetId);
            Log(new CombatEvent
            {
                eventType = CombatEventType.WallExpired,
                turn = turn,
                sourceId = string.Empty,
                targetId = targetId,
                moveId = sourceTag ?? string.Empty,
                message = $"{defName}'s wall expired.",
                metadataJson = metadataJson
            });
        }

        public void LogKO(
            int turn,
            string targetId,
            string sourceId = "",
            string moveId = "",
            int teamIndex = -1,
            int slotIndex = -1)
        {
            string targetName = Name(targetId);
            Log(new CombatEvent
            {
                eventType = CombatEventType.ChainlingKO,
                turn = turn,
                sourceId = sourceId ?? string.Empty,
                targetId = targetId,
                moveId = moveId ?? string.Empty,
                intValue = slotIndex,
                intValue2 = teamIndex,
                message = $"{targetName} fainted!"
            });
        }

        /// <summary>Log when an attack misses (accuracy check failed).</summary>
        public void LogMiss(int turn, string attackerId, string targetId, string moveId)
        {
            string atkName = Name(attackerId);
            string defName = Name(targetId);
            Log(new CombatEvent
            {
                eventType = CombatEventType.Miss,
                turn = turn,
                sourceId = attackerId,
                targetId = targetId,
                moveId = moveId,
                message = $"{atkName} missed {defName}!",
            });
        }

        /// <summary>Log when defender dodges (evasion check succeeded).</summary>
        public void LogDodge(int turn, string attackerId, string targetId, string moveId)
        {
            string atkName = Name(attackerId);
            string defName = Name(targetId);
            Log(new CombatEvent
            {
                eventType = CombatEventType.Dodge,
                turn = turn,
                sourceId = attackerId,
                targetId = targetId,
                moveId = moveId,
                message = $"{defName} dodged {atkName}'s attack!",
            });
        }

        /// <summary>Log a targeting lifecycle event.</summary>
        public void LogTargeting(int turn, CombatEventType type, string sourceId, string targetId,
            string moveId, string message)
        {
            Log(new CombatEvent
            {
                eventType = type,
                turn = turn,
                sourceId = sourceId,
                targetId = targetId,
                moveId = moveId,
                message = message,
            });
        }

        public void LogDetailedEvent(CombatEventType type, int turn, string message, string metadataJson,
            string sourceId = "", string targetId = "", string moveId = "")
        {
            Log(new CombatEvent
            {
                eventType = type,
                turn = turn,
                sourceId = sourceId,
                targetId = targetId,
                moveId = moveId,
                message = message,
                metadataJson = metadataJson
            });
        }

        /// <summary>Log a status application event.</summary>
        public void LogStatus(int turn, CombatEventType type, string targetId,
            StatusId statusId, int duration, string sourceId = "")
        {
            Log(new CombatEvent
            {
                eventType = type,
                turn = turn,
                sourceId = sourceId,
                targetId = targetId,
                statusId = statusId,
                intValue = duration,
                message = $"{type}: {statusId} on {Name(targetId)} (dur={duration})",
            });
        }

        /// <summary>Log an AP change event.</summary>
        public void LogAP(int turn, CombatEventType type, string chainlingId, int amount, int newTotal,
            string apMutationSource = null)
        {
            Log(new CombatEvent
            {
                eventType = type,
                turn = turn,
                targetId = chainlingId,
                intValue = amount,
                intValue2 = newTotal,
                apMutationSource = apMutationSource ?? string.Empty,
                message = $"{Name(chainlingId)} AP {(amount >= 0 ? "+" : "")}{amount} -> {newTotal}",
            });
        }

        /// <summary>Log a stage change event.</summary>
        public void LogStageChange(int turn, string targetId, CoreStat stat, int change, int newStage)
        {
            Log(new CombatEvent
            {
                eventType = CombatEventType.StageChanged,
                turn = turn,
                targetId = targetId,
                coreStat = stat,
                intValue = change,
                intValue2 = newStage,
                message = $"{Name(targetId)} {stat} {(change >= 0 ? "+" : "")}{change} -> stage {newStage}",
            });
        }

        // Queries -------------------------------------------------------------

        /// <summary>Get all legacy events for a specific turn.</summary>
        public List<CombatEvent> GetEventsForTurn(int turn)
        {
            var result = new List<CombatEvent>();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].turn == turn) result.Add(_events[i]);
            }
            return result;
        }

        /// <summary>Get the last N legacy events.</summary>
        public List<CombatEvent> GetLastEvents(int count)
        {
            int start = Math.Max(0, _events.Count - count);
            return _events.GetRange(start, _events.Count - start);
        }

        // Export --------------------------------------------------------------

        /// <summary>Export the legacy log as JSON string.</summary>
        public string ExportToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"seed\": {_seed},");
            sb.AppendLine($"  \"eventCount\": {_events.Count},");

            sb.AppendLine("  \"names\": {");
            int nameIdx = 0;
            foreach (var kv in _nameRegistry)
            {
                sb.Append($"    \"{kv.Key}\": \"{EscapeJson(kv.Value)}\"");
                nameIdx++;
                sb.AppendLine(nameIdx < _nameRegistry.Count ? "," : "");
            }
            sb.AppendLine("  },");

            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                sb.Append("    {");
                sb.Append($"\"t\":{e.timestamp},\"turn\":{e.turn},\"type\":\"{e.eventType}\"");
                if (!string.IsNullOrEmpty(e.sourceId)) sb.Append($",\"src\":\"{e.sourceId}\"");
                if (!string.IsNullOrEmpty(e.targetId)) sb.Append($",\"tgt\":\"{e.targetId}\"");
                if (!string.IsNullOrEmpty(e.moveId)) sb.Append($",\"insc\":\"{e.moveId}\"");
                if (e.statusId != StatusId.None) sb.Append($",\"status\":\"{e.statusId}\"");
                if (e.coreStat != default) sb.Append($",\"stat\":\"{e.coreStat}\"");
                if (e.intValue != 0) sb.Append($",\"val\":{e.intValue}");
                if (e.intValue2 != 0) sb.Append($",\"val2\":{e.intValue2}");
                if (Math.Abs(e.floatValue) > 0.001f) sb.Append($",\"fval\":{e.floatValue:F3}");
                if (e.boolValue) sb.Append(",\"flag\":true");
                if (!string.IsNullOrEmpty(e.message)) sb.Append($",\"msg\":\"{EscapeJson(e.message)}\"");
                if (!string.IsNullOrEmpty(e.metadataJson)) sb.Append($",\"meta\":{e.metadataJson}");
                sb.Append("}");
                if (i < _events.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Export forensic events as JSON string.</summary>
        public string ExportForensicJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"schemaVersion\": 1,");
            sb.AppendLine($"  \"battleId\": \"{EscapeJson(_battleId)}\",");
            sb.AppendLine($"  \"seed\": {_seed},");
            sb.AppendLine($"  \"rngFinalStep\": {(_rng != null ? _rng.CallCount : 0)},");
            sb.AppendLine($"  \"eventCount\": {_forensicEvents.Count},");
            AppendTypeIdToNameJson(sb);
            AppendBattleMetadataJson(sb);
            sb.AppendLine("  \"events\": [");

            for (int i = 0; i < _forensicEvents.Count; i++)
            {
                var e = _forensicEvents[i];
                sb.Append("    {");
                sb.Append($"\"sequenceIndex\":{e.sequenceIndex}");
                sb.Append($",\"battleId\":\"{EscapeJson(e.battleId)}\"");
                sb.Append($",\"turnIndex\":{e.turnIndex}");
                sb.Append($",\"actionIndex\":{e.actionIndex}");
                sb.Append($",\"timestampUtc\":\"{EscapeJson(e.timestampUtc)}\"");
                sb.Append($",\"eventType\":\"{EscapeJson(e.eventType)}\"");
                sb.Append(",\"actorRef\":");
                AppendEntityRefJson(sb, e.actorRef);
                sb.Append(",\"targets\":");
                AppendTargetsJson(sb, e.targets);
                sb.Append($",\"rngSeed\":{e.rngSeed}");
                sb.Append($",\"rngStepIndex\":{e.rngStepIndex}");
                sb.Append(",\"payload\":");
                sb.Append(string.IsNullOrEmpty(e.payloadJson) ? "{}" : e.payloadJson);
                sb.Append("}");
                if (i < _forensicEvents.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Append BattleEnded event and store metadata for export.</summary>
        public void LogBattleEnded(BattleResult result, string team0Name, string team1Name)
        {
            if (result == null) return;
            _battleEndedResult = result;
            _team0Name = team0Name ?? "Team0";
            _team1Name = team1Name ?? "Team1";
            if (!string.IsNullOrWhiteSpace(result.rulesetId))
                _rulesetId = result.rulesetId;
            if (!string.IsNullOrWhiteSpace(result.configHash))
                _configHash = result.configHash;

            var payload = new BattleEndedPayload
            {
                winnerTeamIndex = result.winnerTeamIndex,
                turnCount = result.turnCount,
                durationSeconds = result.durationSeconds,
                battleType = result.battleType.ToString(),
                endReason = result.wasFled ? "Fled" : "BattleComplete",
                team0Name = _team0Name,
                team1Name = _team1Name,
                participantCountPerTeam = CombatConstants.ROSTER_SIZE,
                rulesetId = !string.IsNullOrWhiteSpace(result.rulesetId) ? result.rulesetId : _rulesetId,
                configHash = !string.IsNullOrWhiteSpace(result.configHash) ? result.configHash : _configHash,
            };
            LogForensic(CombatForensicEventType.BattleEnded, payload, null, null, -1, -1);
        }

        /// <summary>Clear all legacy + forensic events.</summary>
        public void Clear()
        {
            _events.Clear();
            _forensicEvents.Clear();
            _forensicSequence = 0;
            _contextTurn = -1;
            _contextAction = -1;
            _battleEndedResult = null;
            _team0Name = null;
            _team1Name = null;
            _rulesetId = null;
            _configHash = null;
        }

        private void AppendForensic(CombatLogEvent evt)
        {
            _forensicEvents.Add(evt);
            int maxEvents = Math.Max(32, CombatLogSettings.MaxEventsKept);
            if (_forensicEvents.Count > maxEvents)
                _forensicEvents.RemoveAt(0);
        }

        private void SyncTeamRefs(TeamState team)
        {
            if (team == null || team.chainlings == null)
                return;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var chainling = team.chainlings[i];
                if (chainling == null || string.IsNullOrEmpty(chainling.instanceId))
                    continue;

                int slotId = ResolveCurrentSlot(team, i);
                _entityRefs[chainling.instanceId] = new CombatEntityRef
                {
                    teamId = team.teamIndex,
                    slotId = slotId,
                    instanceId = chainling.instanceId,
                };
            }
        }

        private static int ResolveCurrentSlot(TeamState team, int rosterIndex)
        {
            if (team != null && team.activeIndices != null)
            {
                for (int s = 0; s < team.activeIndices.Count; s++)
                {
                    if (team.activeIndices[s] == rosterIndex)
                        return s;
                }
            }

            return -1; // Not in active slots (benched or KO'd)
        }

        private static CombatEntityRef CloneEntityRef(CombatEntityRef src)
        {
            if (src == null) return null;
            return new CombatEntityRef
            {
                teamId = src.teamId,
                slotId = src.slotId,
                instanceId = src.instanceId,
            };
        }

        private static string SerializePayload(object payload)
        {
            if (payload == null)
                return "{}";
            if (payload is string payloadJson)
                return string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;
            return JsonUtility.ToJson(payload);
        }

        private static void AppendTypeIdToNameJson(StringBuilder sb)
        {
            var map = CombatLogTypeNames.BuildTypeIdToNameMap();
            sb.Append("  \"typeIdToName\": {");
            bool first = true;
            foreach (var kvp in map)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key}\":\"{EscapeJson(kvp.Value)}\"");
                first = false;
            }
            sb.AppendLine("},");
        }

        private void AppendBattleMetadataJson(StringBuilder sb)
        {
            string rulesetId = !string.IsNullOrWhiteSpace(_battleEndedResult?.rulesetId)
                ? _battleEndedResult.rulesetId
                : (_rulesetId ?? string.Empty);
            string configHash = !string.IsNullOrWhiteSpace(_battleEndedResult?.configHash)
                ? _battleEndedResult.configHash
                : (_configHash ?? string.Empty);

            if (_battleEndedResult == null)
            {
                sb.AppendLine("  \"battleType\": \"Unknown\",");
                sb.AppendLine("  \"team0Name\": \"\",");
                sb.AppendLine("  \"team1Name\": \"\",");
                sb.AppendLine("  \"participantCountPerTeam\": 3,");
                sb.AppendLine($"  \"rulesetId\": \"{EscapeJson(rulesetId)}\",");
                sb.AppendLine($"  \"configHash\": \"{EscapeJson(configHash)}\",");
                return;
            }
            sb.AppendLine($"  \"battleType\": \"{EscapeJson(_battleEndedResult.battleType.ToString())}\",");
            sb.AppendLine($"  \"team0Name\": \"{EscapeJson(_team0Name ?? "")}\",");
            sb.AppendLine($"  \"team1Name\": \"{EscapeJson(_team1Name ?? "")}\",");
            sb.AppendLine($"  \"participantCountPerTeam\": {CombatConstants.ROSTER_SIZE},");
            sb.AppendLine($"  \"rulesetId\": \"{EscapeJson(rulesetId)}\",");
            sb.AppendLine($"  \"configHash\": \"{EscapeJson(configHash)}\",");
        }

        private static void AppendEntityRefJson(StringBuilder sb, CombatEntityRef entityRef)
        {
            if (entityRef == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append("{");
            sb.Append($"\"teamId\":{entityRef.teamId},");
            sb.Append($"\"slotId\":{entityRef.slotId},");
            sb.Append($"\"instanceId\":\"{EscapeJson(entityRef.instanceId)}\"");
            sb.Append("}");
        }

        private static void AppendTargetsJson(StringBuilder sb, List<CombatEntityRef> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[");
            for (int i = 0; i < targets.Count; i++)
            {
                AppendEntityRefJson(sb, targets[i]);
                if (i < targets.Count - 1)
                    sb.Append(",");
            }
            sb.Append("]");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }

    /// <summary>
    /// A single legacy combat event entry.
    /// </summary>
    [Serializable]
    public struct CombatEvent
    {
        public int timestamp;
        public int turn;
        public CombatEventType eventType;

        public string sourceId;
        public string targetId;
        public string moveId;

        public StatusId statusId;
        public CoreStat coreStat;
        public ChainlingType chainlingType;

        public int intValue;
        public int intValue2;
        public float floatValue;
        public bool boolValue;

        public string message;
        public string metadataJson;

        /// <summary>Presentation category for AP events (see <see cref="CombatApMutationSource"/>).</summary>
        public string apMutationSource;
    }
}
