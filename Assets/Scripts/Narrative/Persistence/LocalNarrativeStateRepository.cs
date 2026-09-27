using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Profile-keyed local snapshot and ordered mutation outbox. Local writes are
/// synchronous so a process refresh cannot lose an accepted narrative change.
/// </summary>
public sealed class LocalNarrativeStateRepository :
    IQuestStateRepository,
    ISequenceCheckpointRepository,
    INarrativeStateSnapshotRepository
{
    private const string SnapshotPrefix = "SignalJam_NarrativeSnapshot_v1_";
    private const string OutboxPrefix = "SignalJam_NarrativeOutbox_v1_";
    private const string AliasPrefix = "SignalJam_NarrativeProfileAlias_v1_";

    public Task<NarrativeStateSnapshot> LoadSnapshotAsync(NarrativeProfileIdentity identity)
    {
        return Task.FromResult(LoadSnapshot(identity));
    }

    public Task<IReadOnlyList<QuestStateRecord>> LoadQuestStatesAsync(NarrativeProfileIdentity identity)
    {
        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        return Task.FromResult<IReadOnlyList<QuestStateRecord>>(snapshot.questStates);
    }

    public Task<IReadOnlyList<SequenceCheckpointRecord>> LoadSequenceCheckpointsAsync(
        NarrativeProfileIdentity identity)
    {
        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        return Task.FromResult<IReadOnlyList<SequenceCheckpointRecord>>(snapshot.sequenceCheckpoints);
    }

    public Task<NarrativeRepositoryResult> ApplyQuestStateAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation == null || mutation.questState == null)
            return Task.FromResult(NarrativeRepositoryResult.Failed("quest_state_mutation_missing"));

        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        UpsertQuest(snapshot, mutation.questState);
        SaveSnapshot(identity, snapshot);
        Enqueue(identity, mutation);
        return Task.FromResult(NarrativeRepositoryResult.Ok(mutation.questState.Clone()));
    }

    public Task<NarrativeRepositoryResult> ApplySequenceCheckpointAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation == null || mutation.sequenceCheckpoint == null)
            return Task.FromResult(NarrativeRepositoryResult.Failed("sequence_checkpoint_mutation_missing"));

        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        UpsertSequence(snapshot, mutation.sequenceCheckpoint);
        SaveSnapshot(identity, snapshot);
        Enqueue(identity, mutation);
        return Task.FromResult(NarrativeRepositoryResult.Ok(sequenceCheckpoint: mutation.sequenceCheckpoint.Clone()));
    }

    public NarrativeStateSnapshot MergeRemoteSnapshot(
        NarrativeProfileIdentity identity,
        NarrativeStateSnapshot remoteSnapshot)
    {
        NarrativeStateSnapshot merged = remoteSnapshot != null
            ? remoteSnapshot.Clone()
            : new NarrativeStateSnapshot();
        merged.playerProfileId = identity != null ? identity.playerProfileId ?? "" : "";

        NarrativeMutationQueue queue = LoadOutbox(identity);
        if (queue.mutations != null)
        {
            for (int i = 0; i < queue.mutations.Count; i++)
            {
                NarrativeStateMutation mutation = queue.mutations[i];
                if (mutation == null)
                    continue;

                if (mutation.kind == NarrativeMutationKind.QuestState && mutation.questState != null)
                    UpsertQuest(merged, mutation.questState);
                else if (mutation.kind == NarrativeMutationKind.SequenceCheckpoint && mutation.sequenceCheckpoint != null)
                    UpsertSequence(merged, mutation.sequenceCheckpoint);
            }
        }

        SaveSnapshot(identity, merged);
        return merged;
    }

    public NarrativeMutationQueue LoadOutbox(NarrativeProfileIdentity identity)
    {
        string json = PlayerPrefs.GetString(GetOutboxKey(identity), "");
        if (string.IsNullOrWhiteSpace(json))
            return new NarrativeMutationQueue();

        try
        {
            NarrativeMutationQueue queue = JsonUtility.FromJson<NarrativeMutationQueue>(json);
            return queue ?? new NarrativeMutationQueue();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalNarrativeStateRepository] Invalid outbox was discarded: {ex.Message}");
            return new NarrativeMutationQueue();
        }
    }

    public void AcknowledgeMutation(NarrativeProfileIdentity identity, string mutationId)
    {
        if (string.IsNullOrWhiteSpace(mutationId))
            return;

        NarrativeMutationQueue queue = LoadOutbox(identity);
        queue.mutations.RemoveAll(item => item != null && item.mutationId == mutationId);
        SaveOutbox(identity, queue);
    }

    public void ReplacePendingMutation(NarrativeProfileIdentity identity, NarrativeStateMutation mutation)
    {
        if (mutation == null || string.IsNullOrWhiteSpace(mutation.mutationId))
            return;

        NarrativeMutationQueue queue = LoadOutbox(identity);
        int index = queue.mutations.FindIndex(item =>
            item != null && string.Equals(item.mutationId, mutation.mutationId, StringComparison.Ordinal));
        if (index < 0)
            return;
        queue.mutations[index] = CloneMutation(mutation);
        SaveOutbox(identity, queue);
    }

    public void ReplaceQuestFromServer(NarrativeProfileIdentity identity, QuestStateRecord state)
    {
        if (state == null)
            return;

        if (LoadOutbox(identity).mutations.Exists(item =>
                item?.questState != null && item.questState.questId == state.questId))
            return;

        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        QuestStateRecord existing = snapshot.questStates.Find(item =>
            item != null && string.Equals(item.questId, state.questId, StringComparison.Ordinal));
        if (existing != null && existing.revision > state.revision &&
            existing.lifecycle != QuestLifecycleState.Completed)
            return;
        UpsertQuest(snapshot, state);
        SaveSnapshot(identity, snapshot);
    }

    public void ReplaceSequenceFromServer(NarrativeProfileIdentity identity, SequenceCheckpointRecord checkpoint)
    {
        if (checkpoint == null)
            return;

        if (LoadOutbox(identity).mutations.Exists(item =>
                item?.sequenceCheckpoint != null && item.sequenceCheckpoint.sequenceId == checkpoint.sequenceId))
            return;

        NarrativeStateSnapshot snapshot = LoadSnapshot(identity);
        SequenceCheckpointRecord existing = snapshot.sequenceCheckpoints.Find(item =>
            item != null && string.Equals(item.sequenceId, checkpoint.sequenceId, StringComparison.Ordinal));
        if (existing != null && existing.revision > checkpoint.revision)
            return;
        UpsertSequence(snapshot, checkpoint);
        SaveSnapshot(identity, snapshot);
    }

    public NarrativeStateSnapshot LoadSnapshot(NarrativeProfileIdentity identity)
    {
        string json = PlayerPrefs.GetString(GetSnapshotKey(identity), "");
        if (string.IsNullOrWhiteSpace(json))
        {
            return new NarrativeStateSnapshot
            {
                playerProfileId = identity != null ? identity.playerProfileId ?? "" : ""
            };
        }

        try
        {
            NarrativeStateSnapshot snapshot = JsonUtility.FromJson<NarrativeStateSnapshot>(json);
            if (snapshot == null)
                snapshot = new NarrativeStateSnapshot();
            snapshot.questStates ??= new List<QuestStateRecord>();
            snapshot.sequenceCheckpoints ??= new List<SequenceCheckpointRecord>();
            return snapshot;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalNarrativeStateRepository] Invalid snapshot was ignored: {ex.Message}");
            return new NarrativeStateSnapshot
            {
                playerProfileId = identity != null ? identity.playerProfileId ?? "" : ""
            };
        }
    }

    public void SaveSnapshot(NarrativeProfileIdentity identity, NarrativeStateSnapshot snapshot)
    {
        NarrativeStateSnapshot value = snapshot ?? new NarrativeStateSnapshot();
        value.playerProfileId = identity != null ? identity.playerProfileId ?? "" : "";
        PlayerPrefs.SetString(GetSnapshotKey(identity), JsonUtility.ToJson(value));
        PlayerPrefs.Save();
    }

    private void Enqueue(NarrativeProfileIdentity identity, NarrativeStateMutation mutation)
    {
        if (mutation == null || string.IsNullOrWhiteSpace(mutation.mutationId))
            return;

        NarrativeMutationQueue queue = LoadOutbox(identity);
        if (queue.mutations.Exists(item => item != null && item.mutationId == mutation.mutationId))
            return;

        queue.mutations.Add(CloneMutation(mutation));
        SaveOutbox(identity, queue);
    }

    private void SaveOutbox(NarrativeProfileIdentity identity, NarrativeMutationQueue queue)
    {
        PlayerPrefs.SetString(GetOutboxKey(identity), JsonUtility.ToJson(queue ?? new NarrativeMutationQueue()));
        PlayerPrefs.Save();
    }

    private static void UpsertQuest(NarrativeStateSnapshot snapshot, QuestStateRecord incoming)
    {
        if (snapshot == null || incoming == null || string.IsNullOrWhiteSpace(incoming.questId))
            return;

        snapshot.questStates ??= new List<QuestStateRecord>();
        int index = snapshot.questStates.FindIndex(item =>
            item != null && string.Equals(item.questId, incoming.questId, StringComparison.Ordinal));

        if (index >= 0)
        {
            QuestStateRecord existing = snapshot.questStates[index];
            if (existing.lifecycle == QuestLifecycleState.Completed && incoming.lifecycle != QuestLifecycleState.Completed)
                return;
            snapshot.questStates[index] = incoming.Clone();
        }
        else
        {
            snapshot.questStates.Add(incoming.Clone());
        }
    }

    private static void UpsertSequence(NarrativeStateSnapshot snapshot, SequenceCheckpointRecord incoming)
    {
        if (snapshot == null || incoming == null || string.IsNullOrWhiteSpace(incoming.sequenceId))
            return;

        snapshot.sequenceCheckpoints ??= new List<SequenceCheckpointRecord>();
        int index = snapshot.sequenceCheckpoints.FindIndex(item =>
            item != null && string.Equals(item.sequenceId, incoming.sequenceId, StringComparison.Ordinal));
        if (index >= 0)
            snapshot.sequenceCheckpoints[index] = incoming.Clone();
        else
            snapshot.sequenceCheckpoints.Add(incoming.Clone());
    }

    private static NarrativeStateMutation CloneMutation(NarrativeStateMutation mutation)
    {
        return new NarrativeStateMutation
        {
            mutationId = mutation.mutationId ?? "",
            kind = mutation.kind,
            expectedRevision = mutation.expectedRevision,
            questState = mutation.questState != null ? mutation.questState.Clone() : null,
            sequenceCheckpoint = mutation.sequenceCheckpoint != null ? mutation.sequenceCheckpoint.Clone() : null
        };
    }

    private static string GetSnapshotKey(NarrativeProfileIdentity identity)
    {
        return SnapshotPrefix + Hash128.Compute(ResolveCacheIdentity(identity));
    }

    private static string GetOutboxKey(NarrativeProfileIdentity identity)
    {
        return OutboxPrefix + Hash128.Compute(ResolveCacheIdentity(identity));
    }

    private static string ResolveCacheIdentity(NarrativeProfileIdentity identity)
    {
        if (identity == null)
            return "offline_default";

        if (!string.IsNullOrWhiteSpace(identity.playerProfileId))
        {
            string profileId = identity.playerProfileId.Trim();
            RegisterAlias("auth", identity.authUserId, profileId);
            RegisterAlias("wallet", identity.walletAddress, profileId);
            return profileId;
        }

        string authAlias = ReadAlias("auth", identity.authUserId);
        if (!string.IsNullOrWhiteSpace(authAlias))
            return authAlias;

        string walletAlias = ReadAlias("wallet", identity.walletAddress);
        if (!string.IsNullOrWhiteSpace(walletAlias))
            return walletAlias;

        return identity.CacheIdentity;
    }

    private static void RegisterAlias(string kind, string value, string profileId)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(profileId))
            return;
        PlayerPrefs.SetString(GetAliasKey(kind, value), profileId);
    }

    private static string ReadAlias(string kind, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : PlayerPrefs.GetString(GetAliasKey(kind, value), "");
    }

    private static string GetAliasKey(string kind, string value)
    {
        string normalized = value != null ? value.Trim() : "";
        return AliasPrefix + kind + "_" + Hash128.Compute(normalized);
    }
}
