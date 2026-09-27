using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Profile-isolated deterministic repository for Edit Mode tests and local
/// simulations. It enforces the same revision, idempotency, and Completed quest
/// immutability guarantees as the backend transaction.
/// </summary>
public sealed class InMemoryNarrativeStateRepository :
    IQuestStateRepository,
    ISequenceCheckpointRepository,
    INarrativeStateSnapshotRepository
{
    private readonly Dictionary<string, NarrativeStateSnapshot> snapshots =
        new Dictionary<string, NarrativeStateSnapshot>(StringComparer.Ordinal);
    private readonly Dictionary<string, NarrativeRepositoryResult> mutationResults =
        new Dictionary<string, NarrativeRepositoryResult>(StringComparer.Ordinal);

    public InMemoryNarrativeStateRepository(NarrativeStateSnapshot initial = null)
    {
        if (initial != null)
        {
            string key = !string.IsNullOrWhiteSpace(initial.playerProfileId)
                ? initial.playerProfileId
                : "offline_default";
            snapshots[key] = initial.Clone();
        }
    }

    public Task<NarrativeStateSnapshot> LoadSnapshotAsync(NarrativeProfileIdentity identity)
    {
        return Task.FromResult(GetSnapshot(identity).Clone());
    }

    public async Task<IReadOnlyList<QuestStateRecord>> LoadQuestStatesAsync(NarrativeProfileIdentity identity)
    {
        return (await LoadSnapshotAsync(identity)).questStates;
    }

    public async Task<IReadOnlyList<SequenceCheckpointRecord>> LoadSequenceCheckpointsAsync(
        NarrativeProfileIdentity identity)
    {
        return (await LoadSnapshotAsync(identity)).sequenceCheckpoints;
    }

    public Task<NarrativeRepositoryResult> ApplyQuestStateAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation?.questState == null || string.IsNullOrWhiteSpace(mutation.mutationId))
            return Task.FromResult(NarrativeRepositoryResult.Failed("quest_state_mutation_missing"));

        string mutationKey = MutationKey(identity, mutation.mutationId);
        if (mutationResults.TryGetValue(mutationKey, out NarrativeRepositoryResult prior))
            return Task.FromResult(prior);

        NarrativeStateSnapshot snapshot = GetSnapshot(identity);
        QuestStateRecord existing = snapshot.questStates.FirstOrDefault(item =>
            item != null && item.questId == mutation.questState.questId);
        long currentRevision = existing != null ? existing.revision : 0L;

        if (existing != null && existing.lifecycle == QuestLifecycleState.Completed)
        {
            if (mutation.questState.lifecycle == QuestLifecycleState.Completed &&
                ResolutionEquals(existing.resolution, mutation.questState.resolution))
            {
                NarrativeRepositoryResult terminalNoop = NarrativeRepositoryResult.Ok(existing.Clone());
                mutationResults[mutationKey] = terminalNoop;
                return Task.FromResult(terminalNoop);
            }

            return Task.FromResult(NarrativeRepositoryResult.Failed("completed_quest_immutable", true));
        }

        if (Math.Max(0L, mutation.expectedRevision) != currentRevision)
            return Task.FromResult(NarrativeRepositoryResult.Failed("revision_conflict", true));

        QuestStateRecord saved = mutation.questState.Clone();
        saved.revision = currentRevision + 1L;
        Upsert(snapshot.questStates, saved, item => item.questId);
        NarrativeRepositoryResult result = NarrativeRepositoryResult.Ok(saved.Clone());
        mutationResults[mutationKey] = result;
        return Task.FromResult(result);
    }

    public Task<NarrativeRepositoryResult> ApplySequenceCheckpointAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation)
    {
        if (mutation?.sequenceCheckpoint == null || string.IsNullOrWhiteSpace(mutation.mutationId))
            return Task.FromResult(NarrativeRepositoryResult.Failed("sequence_checkpoint_mutation_missing"));

        string mutationKey = MutationKey(identity, mutation.mutationId);
        if (mutationResults.TryGetValue(mutationKey, out NarrativeRepositoryResult prior))
            return Task.FromResult(prior);

        NarrativeStateSnapshot snapshot = GetSnapshot(identity);
        SequenceCheckpointRecord existing = snapshot.sequenceCheckpoints.FirstOrDefault(item =>
            item != null && item.sequenceId == mutation.sequenceCheckpoint.sequenceId);
        long currentRevision = existing != null ? existing.revision : 0L;
        if (Math.Max(0L, mutation.expectedRevision) != currentRevision)
            return Task.FromResult(NarrativeRepositoryResult.Failed("revision_conflict", true));

        SequenceCheckpointRecord saved = mutation.sequenceCheckpoint.Clone();
        saved.revision = currentRevision + 1L;
        Upsert(snapshot.sequenceCheckpoints, saved, item => item.sequenceId);
        NarrativeRepositoryResult result = NarrativeRepositoryResult.Ok(sequenceCheckpoint: saved.Clone());
        mutationResults[mutationKey] = result;
        return Task.FromResult(result);
    }

    private NarrativeStateSnapshot GetSnapshot(NarrativeProfileIdentity identity)
    {
        string key = identity != null ? identity.CacheIdentity : "offline_default";
        if (!snapshots.TryGetValue(key, out NarrativeStateSnapshot snapshot))
        {
            snapshot = new NarrativeStateSnapshot
            {
                playerProfileId = identity != null ? identity.playerProfileId ?? "" : ""
            };
            snapshots[key] = snapshot;
        }

        return snapshot;
    }

    private static void Upsert<T>(List<T> items, T incoming, Func<T, string> idSelector)
    {
        string id = idSelector(incoming);
        int index = items.FindIndex(item => item != null && idSelector(item) == id);
        if (index >= 0)
            items[index] = incoming;
        else
            items.Add(incoming);
    }

    private static bool ResolutionEquals(QuestResolutionRecord first, QuestResolutionRecord second)
    {
        if (first == null || second == null || first.resolutionId != second.resolutionId)
            return false;
        List<NarrativeStringValue> firstContext = first.context ?? new List<NarrativeStringValue>();
        List<NarrativeStringValue> secondContext = second.context ?? new List<NarrativeStringValue>();
        return firstContext.Count == secondContext.Count && firstContext.All(entry =>
            entry != null && secondContext.Any(candidate =>
                candidate != null && candidate.key == entry.key && candidate.value == entry.value));
    }

    private static string MutationKey(NarrativeProfileIdentity identity, string mutationId)
    {
        return $"{(identity != null ? identity.CacheIdentity : "offline_default")}:{mutationId}";
    }
}
