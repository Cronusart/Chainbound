using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Authenticated Supabase adapter for profile narrative snapshots and
/// idempotent quest/checkpoint mutations.
/// </summary>
public sealed class SupabaseNarrativeStateRepository :
    IQuestStateRepository,
    ISequenceCheckpointRepository,
    INarrativeStateSnapshotRepository
{
    private readonly SupabaseConfigSO config;

    public SupabaseNarrativeStateRepository(SupabaseConfigSO config = null)
    {
        this.config = config != null ? config : Resources.Load<SupabaseConfigSO>("SupabaseConfig");
    }

    public async Task<NarrativeStateSnapshot> LoadSnapshotAsync(NarrativeProfileIdentity identity)
    {
        EnsureConfigured();
        string json = JsonUtility.ToJson(IdentityRequest.From(identity));
        SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
            config,
            "profile_get_narrative_state",
            json);

        if (!response.IsSuccess)
            throw new InvalidOperationException($"profile_get_narrative_state_http_{response.statusCode}: {response.body}");

        SnapshotResponse payload = JsonUtility.FromJson<SnapshotResponse>(response.body);
        if (payload == null || !payload.success)
            throw new InvalidOperationException(payload != null ? payload.error : "invalid_narrative_snapshot_payload");

        return payload.ToRuntime();
    }

    public async Task<IReadOnlyList<QuestStateRecord>> LoadQuestStatesAsync(NarrativeProfileIdentity identity)
    {
        NarrativeStateSnapshot snapshot = await LoadSnapshotAsync(identity);
        return snapshot.questStates;
    }

    public async Task<IReadOnlyList<SequenceCheckpointRecord>> LoadSequenceCheckpointsAsync(
        NarrativeProfileIdentity identity)
    {
        NarrativeStateSnapshot snapshot = await LoadSnapshotAsync(identity);
        return snapshot.sequenceCheckpoints;
    }

    public async Task<NarrativeRepositoryResult> ApplyQuestStateAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation?.questState == null)
            return NarrativeRepositoryResult.Failed("quest_state_mutation_missing");

        EnsureConfigured();
        var request = QuestMutationRequest.From(identity, mutation);
        SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
            config,
            "profile_apply_quest_state",
            JsonUtility.ToJson(request));

        QuestMutationResponse payload = string.IsNullOrWhiteSpace(response.body)
            ? null
            : JsonUtility.FromJson<QuestMutationResponse>(response.body);
        if (!response.IsSuccess || payload == null || !payload.success)
        {
            string error = payload != null && !string.IsNullOrWhiteSpace(payload.error)
                ? payload.error
                : $"profile_apply_quest_state_http_{response.statusCode}";
            return NarrativeRepositoryResult.Failed(error, response.statusCode == 409);
        }

        return NarrativeRepositoryResult.Ok(payload.questState != null ? payload.questState.ToRuntime() : null);
    }

    public async Task<NarrativeRepositoryResult> ApplySequenceCheckpointAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation?.sequenceCheckpoint == null)
            return NarrativeRepositoryResult.Failed("sequence_checkpoint_mutation_missing");

        EnsureConfigured();
        var request = SequenceMutationRequest.From(identity, mutation);
        SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
            config,
            "profile_apply_sequence_checkpoint",
            JsonUtility.ToJson(request));

        SequenceMutationResponse payload = string.IsNullOrWhiteSpace(response.body)
            ? null
            : JsonUtility.FromJson<SequenceMutationResponse>(response.body);
        if (!response.IsSuccess || payload == null || !payload.success)
        {
            string error = payload != null && !string.IsNullOrWhiteSpace(payload.error)
                ? payload.error
                : $"profile_apply_sequence_checkpoint_http_{response.statusCode}";
            return NarrativeRepositoryResult.Failed(error, response.statusCode == 409);
        }

        return NarrativeRepositoryResult.Ok(
            sequenceCheckpoint: payload.sequenceCheckpoint != null ? payload.sequenceCheckpoint.ToRuntime() : null);
    }

    private void EnsureConfigured()
    {
        if (config == null)
            throw new InvalidOperationException("SupabaseConfigSO was not available for narrative persistence.");
    }

    [Serializable]
    private class IdentityRequest
    {
        public string player_profile_id;
        public string wallet_address;
        public string auth_user_id;

        public static IdentityRequest From(NarrativeProfileIdentity identity)
        {
            return new IdentityRequest
            {
                player_profile_id = identity != null ? identity.playerProfileId ?? "" : "",
                wallet_address = identity != null ? identity.walletAddress ?? "" : "",
                auth_user_id = identity != null ? identity.authUserId ?? "" : ""
            };
        }
    }

    [Serializable]
    private sealed class QuestMutationRequest : IdentityRequest
    {
        public string mutationId;
        public long expectedRevision;
        public QuestStateWireDto questState;

        public static QuestMutationRequest From(NarrativeProfileIdentity identity, NarrativeStateMutation mutation)
        {
            IdentityRequest baseRequest = IdentityRequest.From(identity);
            return new QuestMutationRequest
            {
                player_profile_id = baseRequest.player_profile_id,
                wallet_address = baseRequest.wallet_address,
                auth_user_id = baseRequest.auth_user_id,
                mutationId = mutation.mutationId,
                expectedRevision = Math.Max(0L, mutation.expectedRevision),
                questState = QuestStateWireDto.FromRuntime(mutation.questState)
            };
        }
    }

    [Serializable]
    private sealed class SequenceMutationRequest : IdentityRequest
    {
        public string mutationId;
        public long expectedRevision;
        public SequenceCheckpointWireDto sequenceCheckpoint;

        public static SequenceMutationRequest From(NarrativeProfileIdentity identity, NarrativeStateMutation mutation)
        {
            IdentityRequest baseRequest = IdentityRequest.From(identity);
            return new SequenceMutationRequest
            {
                player_profile_id = baseRequest.player_profile_id,
                wallet_address = baseRequest.wallet_address,
                auth_user_id = baseRequest.auth_user_id,
                mutationId = mutation.mutationId,
                expectedRevision = Math.Max(0L, mutation.expectedRevision),
                sequenceCheckpoint = SequenceCheckpointWireDto.FromRuntime(mutation.sequenceCheckpoint)
            };
        }
    }

    [Serializable]
    private sealed class SnapshotResponse
    {
        public bool success;
        public string error;
        public string playerProfileId;
        public int schemaVersion = 1;
        public List<QuestStateWireDto> questStates = new List<QuestStateWireDto>();
        public List<SequenceCheckpointWireDto> sequenceCheckpoints = new List<SequenceCheckpointWireDto>();

        public NarrativeStateSnapshot ToRuntime()
        {
            var snapshot = new NarrativeStateSnapshot
            {
                schemaVersion = Mathf.Max(1, schemaVersion),
                playerProfileId = playerProfileId ?? ""
            };

            if (questStates != null)
            {
                for (int i = 0; i < questStates.Count; i++)
                {
                    if (questStates[i] != null)
                        snapshot.questStates.Add(questStates[i].ToRuntime());
                }
            }

            if (sequenceCheckpoints != null)
            {
                for (int i = 0; i < sequenceCheckpoints.Count; i++)
                {
                    if (sequenceCheckpoints[i] != null)
                        snapshot.sequenceCheckpoints.Add(sequenceCheckpoints[i].ToRuntime());
                }
            }

            return snapshot;
        }
    }

    [Serializable]
    private sealed class QuestMutationResponse
    {
        public bool success;
        public string error;
        public QuestStateWireDto questState;
    }

    [Serializable]
    private sealed class SequenceMutationResponse
    {
        public bool success;
        public string error;
        public SequenceCheckpointWireDto sequenceCheckpoint;
    }

    [Serializable]
    private sealed class QuestStateWireDto
    {
        public string questId;
        public string lifecycle;
        public List<ObjectiveProgressRecord> objectiveProgress = new List<ObjectiveProgressRecord>();
        public QuestResolutionRecord resolution = new QuestResolutionRecord();
        public long revision;
        public int contentVersion = 1;
        public string updatedAtUtc;

        public static QuestStateWireDto FromRuntime(QuestStateRecord state)
        {
            return new QuestStateWireDto
            {
                questId = state != null ? state.questId ?? "" : "",
                lifecycle = state != null
                    ? NarrativeStateSerializationUtility.LifecycleToWire(state.lifecycle)
                    : "unknown",
                objectiveProgress = state?.objectiveProgress ?? new List<ObjectiveProgressRecord>(),
                resolution = state?.resolution ?? new QuestResolutionRecord(),
                revision = state != null ? state.revision : 0,
                contentVersion = state != null ? Mathf.Max(1, state.contentVersion) : 1,
                updatedAtUtc = state != null ? state.updatedAtUtc ?? "" : ""
            };
        }

        public QuestStateRecord ToRuntime()
        {
            return new QuestStateRecord
            {
                questId = questId ?? "",
                lifecycle = NarrativeStateSerializationUtility.LifecycleFromWire(lifecycle),
                objectiveProgress = objectiveProgress ?? new List<ObjectiveProgressRecord>(),
                resolution = resolution ?? new QuestResolutionRecord(),
                revision = Math.Max(0L, revision),
                contentVersion = Mathf.Max(1, contentVersion),
                updatedAtUtc = updatedAtUtc ?? ""
            };
        }
    }

    [Serializable]
    private sealed class SequenceCheckpointWireDto
    {
        public string sequenceId;
        public string status;
        public string checkpointId;
        public string checkpointLabel;
        public string lastCompletedLabel;
        public int checkpointStepIndex = -1;
        public string sceneId;
        public string spawnId;
        public List<NarrativeStringValue> persistentVariables = new List<NarrativeStringValue>();
        public List<NarrativeFlagValue> railFlagSnapshot = new List<NarrativeFlagValue>();
        public List<string> completedStepGuids = new List<string>();
        public List<string> playedGateKeys = new List<string>();
        public long revision;
        public int sequenceVersion = 1;
        public string updatedAtUtc;

        public static SequenceCheckpointWireDto FromRuntime(SequenceCheckpointRecord checkpoint)
        {
            return new SequenceCheckpointWireDto
            {
                sequenceId = checkpoint != null ? checkpoint.sequenceId ?? "" : "",
                status = checkpoint != null
                    ? NarrativeStateSerializationUtility.CheckpointStatusToWire(checkpoint.status)
                    : "active",
                checkpointId = checkpoint != null ? checkpoint.checkpointId ?? "" : "",
                checkpointLabel = checkpoint != null ? checkpoint.checkpointLabel ?? "" : "",
                lastCompletedLabel = checkpoint != null ? checkpoint.lastCompletedLabel ?? "" : "",
                checkpointStepIndex = checkpoint != null ? checkpoint.checkpointStepIndex : -1,
                sceneId = checkpoint != null ? checkpoint.sceneId ?? "" : "",
                spawnId = checkpoint != null ? checkpoint.spawnId ?? "" : "",
                persistentVariables = checkpoint?.persistentVariables ?? new List<NarrativeStringValue>(),
                railFlagSnapshot = checkpoint?.railFlagSnapshot ?? new List<NarrativeFlagValue>(),
                completedStepGuids = checkpoint?.completedStepGuids ?? new List<string>(),
                playedGateKeys = checkpoint?.playedGateKeys ?? new List<string>(),
                revision = checkpoint != null ? checkpoint.revision : 0,
                sequenceVersion = checkpoint != null ? Mathf.Max(1, checkpoint.sequenceVersion) : 1,
                updatedAtUtc = checkpoint != null ? checkpoint.updatedAtUtc ?? "" : ""
            };
        }

        public SequenceCheckpointRecord ToRuntime()
        {
            return new SequenceCheckpointRecord
            {
                sequenceId = sequenceId ?? "",
                status = NarrativeStateSerializationUtility.CheckpointStatusFromWire(status),
                checkpointId = checkpointId ?? "",
                checkpointLabel = checkpointLabel ?? "",
                lastCompletedLabel = lastCompletedLabel ?? "",
                checkpointStepIndex = checkpointStepIndex,
                sceneId = sceneId ?? "",
                spawnId = spawnId ?? "",
                persistentVariables = persistentVariables ?? new List<NarrativeStringValue>(),
                railFlagSnapshot = railFlagSnapshot ?? new List<NarrativeFlagValue>(),
                completedStepGuids = completedStepGuids ?? new List<string>(),
                playedGateKeys = playedGateKeys ?? new List<string>(),
                revision = Math.Max(0L, revision),
                sequenceVersion = Mathf.Max(1, sequenceVersion),
                updatedAtUtc = updatedAtUtc ?? ""
            };
        }
    }
}
