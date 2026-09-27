using System.Collections.Generic;
using System.Threading.Tasks;

public interface IQuestStateRepository
{
    Task<IReadOnlyList<QuestStateRecord>> LoadQuestStatesAsync(NarrativeProfileIdentity identity);
    Task<NarrativeRepositoryResult> ApplyQuestStateAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation);
}

public interface ISequenceCheckpointRepository
{
    Task<IReadOnlyList<SequenceCheckpointRecord>> LoadSequenceCheckpointsAsync(NarrativeProfileIdentity identity);
    Task<NarrativeRepositoryResult> ApplySequenceCheckpointAsync(
        NarrativeProfileIdentity identity,
        NarrativeStateMutation mutation);
}

public interface INarrativeStateSnapshotRepository
{
    Task<NarrativeStateSnapshot> LoadSnapshotAsync(NarrativeProfileIdentity identity);
}
