using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NarrativeStringValue
{
    public string key = "";
    public string value = "";

    public NarrativeStringValue() { }

    public NarrativeStringValue(string key, string value)
    {
        this.key = key != null ? key.Trim() : "";
        this.value = value != null ? value.Trim() : "";
    }
}

[Serializable]
public sealed class NarrativeFlagValue
{
    public string key = "";
    public bool value;

    public NarrativeFlagValue() { }

    public NarrativeFlagValue(string key, bool value)
    {
        this.key = key != null ? key.Trim() : "";
        this.value = value;
    }
}

[Serializable]
public sealed class ObjectiveProgressRecord
{
    public string objectiveId = "";
    public int currentProgress;
}

[Serializable]
public sealed class QuestResolutionRecord
{
    public string resolutionId = "";
    public List<NarrativeStringValue> context = new List<NarrativeStringValue>();
    public string resolvedAtUtc = "";

    public bool HasResolution => !string.IsNullOrWhiteSpace(resolutionId);

    public bool TryGetContextValue(string key, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(key) || context == null)
            return false;

        for (int i = 0; i < context.Count; i++)
        {
            NarrativeStringValue entry = context[i];
            if (entry != null && string.Equals(entry.key, key, StringComparison.Ordinal))
            {
                value = entry.value ?? "";
                return true;
            }
        }

        return false;
    }

    public static QuestResolutionRecord Create(
        string resolutionId,
        IReadOnlyDictionary<string, string> values,
        string resolvedAtUtc = "")
    {
        var record = new QuestResolutionRecord
        {
            resolutionId = resolutionId != null ? resolutionId.Trim() : "",
            resolvedAtUtc = resolvedAtUtc ?? ""
        };

        if (values == null)
            return record;

        foreach (KeyValuePair<string, string> pair in values)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
                record.context.Add(new NarrativeStringValue(pair.Key, pair.Value));
        }

        record.context.Sort((a, b) => string.CompareOrdinal(a != null ? a.key : "", b != null ? b.key : ""));
        return record;
    }

    public QuestResolutionRecord Clone()
    {
        var clone = new QuestResolutionRecord
        {
            resolutionId = resolutionId ?? "",
            resolvedAtUtc = resolvedAtUtc ?? ""
        };

        if (context != null)
        {
            for (int i = 0; i < context.Count; i++)
            {
                NarrativeStringValue entry = context[i];
                if (entry != null)
                    clone.context.Add(new NarrativeStringValue(entry.key, entry.value));
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class QuestStateRecord
{
    public string questId = "";
    public QuestLifecycleState lifecycle = QuestLifecycleState.Unknown;
    public List<ObjectiveProgressRecord> objectiveProgress = new List<ObjectiveProgressRecord>();
    public QuestResolutionRecord resolution = new QuestResolutionRecord();
    public long revision;
    public int contentVersion = 1;
    public string updatedAtUtc = "";

    public QuestStateRecord Clone()
    {
        var clone = new QuestStateRecord
        {
            questId = questId ?? "",
            lifecycle = lifecycle,
            resolution = resolution != null ? resolution.Clone() : new QuestResolutionRecord(),
            revision = revision,
            contentVersion = Mathf.Max(1, contentVersion),
            updatedAtUtc = updatedAtUtc ?? ""
        };

        if (objectiveProgress != null)
        {
            for (int i = 0; i < objectiveProgress.Count; i++)
            {
                ObjectiveProgressRecord entry = objectiveProgress[i];
                if (entry != null)
                {
                    clone.objectiveProgress.Add(new ObjectiveProgressRecord
                    {
                        objectiveId = entry.objectiveId ?? "",
                        currentProgress = Mathf.Max(0, entry.currentProgress)
                    });
                }
            }
        }

        return clone;
    }
}

public enum SequenceCheckpointStatus
{
    Active,
    Completed,
    Abandoned
}

[Serializable]
public sealed class SequenceCheckpointRecord
{
    public string sequenceId = "";
    public SequenceCheckpointStatus status = SequenceCheckpointStatus.Active;
    public string checkpointId = "";
    public string checkpointLabel = "";
    public string lastCompletedLabel = "";
    public int checkpointStepIndex = -1;
    public string sceneId = "";
    public string spawnId = "";
    public List<NarrativeStringValue> persistentVariables = new List<NarrativeStringValue>();
    public List<NarrativeFlagValue> railFlagSnapshot = new List<NarrativeFlagValue>();
    public List<string> completedStepGuids = new List<string>();
    public List<string> playedGateKeys = new List<string>();
    public long revision;
    public int sequenceVersion = 1;
    public string updatedAtUtc = "";

    public SequenceCheckpointRecord Clone()
    {
        var clone = new SequenceCheckpointRecord
        {
            sequenceId = sequenceId ?? "",
            status = status,
            checkpointId = checkpointId ?? "",
            checkpointLabel = checkpointLabel ?? "",
            lastCompletedLabel = lastCompletedLabel ?? "",
            checkpointStepIndex = checkpointStepIndex,
            sceneId = sceneId ?? "",
            spawnId = spawnId ?? "",
            revision = revision,
            sequenceVersion = Mathf.Max(1, sequenceVersion),
            updatedAtUtc = updatedAtUtc ?? ""
        };

        if (persistentVariables != null)
        {
            for (int i = 0; i < persistentVariables.Count; i++)
            {
                NarrativeStringValue entry = persistentVariables[i];
                if (entry != null)
                    clone.persistentVariables.Add(new NarrativeStringValue(entry.key, entry.value));
            }
        }

        if (railFlagSnapshot != null)
        {
            for (int i = 0; i < railFlagSnapshot.Count; i++)
            {
                NarrativeFlagValue entry = railFlagSnapshot[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.key))
                    clone.railFlagSnapshot.Add(new NarrativeFlagValue(entry.key, entry.value));
            }
        }

        if (completedStepGuids != null)
            clone.completedStepGuids.AddRange(completedStepGuids);
        if (playedGateKeys != null)
            clone.playedGateKeys.AddRange(playedGateKeys);

        return clone;
    }
}

[Serializable]
public sealed class NarrativeStateSnapshot
{
    public int schemaVersion = 1;
    public string playerProfileId = "";
    public List<QuestStateRecord> questStates = new List<QuestStateRecord>();
    public List<SequenceCheckpointRecord> sequenceCheckpoints = new List<SequenceCheckpointRecord>();

    public NarrativeStateSnapshot Clone()
    {
        var clone = new NarrativeStateSnapshot
        {
            schemaVersion = Mathf.Max(1, schemaVersion),
            playerProfileId = playerProfileId ?? ""
        };

        if (questStates != null)
        {
            for (int i = 0; i < questStates.Count; i++)
            {
                if (questStates[i] != null)
                    clone.questStates.Add(questStates[i].Clone());
            }
        }

        if (sequenceCheckpoints != null)
        {
            for (int i = 0; i < sequenceCheckpoints.Count; i++)
            {
                if (sequenceCheckpoints[i] != null)
                    clone.sequenceCheckpoints.Add(sequenceCheckpoints[i].Clone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class NarrativeProfileIdentity
{
    public string playerProfileId = "";
    public string walletAddress = "";
    public string authUserId = "";

    public string CacheIdentity
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(playerProfileId))
                return playerProfileId.Trim();
            if (!string.IsNullOrWhiteSpace(authUserId))
                return $"auth_{authUserId.Trim()}";
            if (!string.IsNullOrWhiteSpace(walletAddress))
                return $"wallet_{walletAddress.Trim()}";
            return "offline_default";
        }
    }
}

public enum NarrativeMutationKind
{
    QuestState,
    SequenceCheckpoint
}

[Serializable]
public sealed class NarrativeStateMutation
{
    public string mutationId = "";
    public NarrativeMutationKind kind;
    public long expectedRevision;
    public QuestStateRecord questState;
    public SequenceCheckpointRecord sequenceCheckpoint;
}

[Serializable]
public sealed class NarrativeMutationQueue
{
    public List<NarrativeStateMutation> mutations = new List<NarrativeStateMutation>();
}

public sealed class NarrativeRepositoryResult
{
    public bool Success { get; private set; }
    public bool Conflict { get; private set; }
    public string Error { get; private set; } = "";
    public QuestStateRecord QuestState { get; private set; }
    public SequenceCheckpointRecord SequenceCheckpoint { get; private set; }

    public static NarrativeRepositoryResult Ok(
        QuestStateRecord questState = null,
        SequenceCheckpointRecord sequenceCheckpoint = null)
    {
        return new NarrativeRepositoryResult
        {
            Success = true,
            QuestState = questState,
            SequenceCheckpoint = sequenceCheckpoint
        };
    }

    public static NarrativeRepositoryResult Failed(string error, bool conflict = false)
    {
        return new NarrativeRepositoryResult
        {
            Success = false,
            Conflict = conflict,
            Error = error ?? "narrative_repository_failure"
        };
    }
}

public static class NarrativeStateSerializationUtility
{
    public static string LifecycleToWire(QuestLifecycleState lifecycle)
    {
        switch (lifecycle)
        {
            case QuestLifecycleState.Locked: return "locked";
            case QuestLifecycleState.Available: return "available";
            case QuestLifecycleState.Active: return "active";
            case QuestLifecycleState.Completed: return "completed";
            case QuestLifecycleState.Failed: return "failed";
            default: return "unknown";
        }
    }

    public static QuestLifecycleState LifecycleFromWire(string value)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "locked": return QuestLifecycleState.Locked;
            case "available": return QuestLifecycleState.Available;
            case "active": return QuestLifecycleState.Active;
            case "completed": return QuestLifecycleState.Completed;
            case "failed": return QuestLifecycleState.Failed;
            default: return QuestLifecycleState.Unknown;
        }
    }

    public static string CheckpointStatusToWire(SequenceCheckpointStatus status)
    {
        switch (status)
        {
            case SequenceCheckpointStatus.Completed: return "completed";
            case SequenceCheckpointStatus.Abandoned: return "abandoned";
            default: return "active";
        }
    }

    public static SequenceCheckpointStatus CheckpointStatusFromWire(string value)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "completed": return SequenceCheckpointStatus.Completed;
            case "abandoned": return SequenceCheckpointStatus.Abandoned;
            default: return SequenceCheckpointStatus.Active;
        }
    }
}
