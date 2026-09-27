using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Profile-scoped narrative bootstrap and write-through coordinator. Gameplay
/// systems consume hydrated in-memory state; only this coordinator talks to the
/// local snapshot/outbox and remote persistence repositories.
/// </summary>
public sealed class NarrativeProfileStateCoordinator : MonoBehaviour
{
    private static NarrativeProfileStateCoordinator instance;

    [Header("Catalogs")]
    [SerializeField] private QuestCatalogSO questCatalog;
    [SerializeField] private SequenceCatalogSO sequenceCatalog;

    [Header("Diagnostics")]
    [SerializeField] private bool enableDebugLogs;

    private LocalNarrativeStateRepository localRepository;
    private SupabaseNarrativeStateRepository remoteRepository;
    private NarrativeProfileIdentity currentIdentity;
    private QuestService questService;
    private SequenceRunner sequenceRunner;
    private bool subscriptionsActive;
    private bool suppressRuntimeMutations;
    private bool flushInProgress;
    private Task activeHydration;

    public static NarrativeProfileStateCoordinator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NarrativeProfileStateCoordinator>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("NarrativeProfileStateCoordinator");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<NarrativeProfileStateCoordinator>();
                    if (services == null)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    public bool IsReady { get; private set; }
    public NarrativeProfileIdentity CurrentIdentity => currentIdentity;
    public event Action<bool> OnReadyChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        questCatalog = questCatalog != null
            ? questCatalog
            : Resources.Load<QuestCatalogSO>("Narrative/QuestCatalog");
        sequenceCatalog = sequenceCatalog != null
            ? sequenceCatalog
            : Resources.Load<SequenceCatalogSO>("Narrative/SequenceCatalog");
        localRepository = new LocalNarrativeStateRepository();
        remoteRepository = new SupabaseNarrativeStateRepository();
    }

    private void OnDestroy()
    {
        UnsubscribeRuntime();
        if (instance == this)
            instance = null;
    }

    public Task HydrateFromCurrentSessionAsync(bool forceOffline = false)
    {
        GameSessionContext session = GameSessionContext.Instance;
        var identity = new NarrativeProfileIdentity
        {
            playerProfileId = session != null ? session.PlayerProfileId : "",
            walletAddress = session != null ? session.WalletAddress : "",
            authUserId = session != null ? session.AuthUserId : ""
        };
        return HydrateProfileAsync(identity, forceOffline);
    }

    public Task HydrateProfileAsync(NarrativeProfileIdentity identity, bool forceOffline = false)
    {
        string requestedCacheIdentity = identity != null ? identity.CacheIdentity : "offline_default";
        if (activeHydration != null && !activeHydration.IsCompleted &&
            currentIdentity != null && currentIdentity.CacheIdentity == requestedCacheIdentity)
        {
            return activeHydration;
        }

        activeHydration = HydrateInternalAsync(identity ?? new NarrativeProfileIdentity(), forceOffline);
        return activeHydration;
    }

    public bool ResumeHydratedSequence(string sequenceId = null)
    {
        return IsReady && sequenceRunner != null && sequenceRunner.ResumeRestoredCheckpoint(sequenceId);
    }

    public async Task FlushOutboxAsync()
    {
        if (flushInProgress || currentIdentity == null || !CanUseRemote())
            return;

        flushInProgress = true;
        try
        {
            while (true)
            {
                NarrativeMutationQueue queue = localRepository.LoadOutbox(currentIdentity);
                NarrativeStateMutation mutation = queue.mutations != null
                    ? queue.mutations.FirstOrDefault(item => item != null)
                    : null;
                if (mutation == null)
                    break;

                NarrativeRepositoryResult result;
                try
                {
                    result = mutation.kind == NarrativeMutationKind.QuestState
                        ? await remoteRepository.ApplyQuestStateAsync(currentIdentity, mutation)
                        : await remoteRepository.ApplySequenceCheckpointAsync(currentIdentity, mutation);
                }
                catch (Exception ex)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[NarrativeProfileStateCoordinator] Outbox sync deferred: {ex.Message}");
                    break;
                }

                if (!result.Success)
                {
                    if (result.Conflict)
                    {
                        if (await ReconcileConflictAsync(mutation))
                            continue;
                    }
                    else if (enableDebugLogs)
                        Debug.LogWarning($"[NarrativeProfileStateCoordinator] Outbox sync deferred: {result.Error}");
                    break;
                }

                localRepository.AcknowledgeMutation(currentIdentity, mutation.mutationId);
                if (result.QuestState != null)
                    localRepository.ReplaceQuestFromServer(currentIdentity, result.QuestState);
                if (result.SequenceCheckpoint != null)
                    localRepository.ReplaceSequenceFromServer(currentIdentity, result.SequenceCheckpoint);
            }
        }
        finally
        {
            flushInProgress = false;
        }
    }

    private async Task HydrateInternalAsync(NarrativeProfileIdentity identity, bool forceOffline)
    {
        SetReady(false);
        currentIdentity = identity;
        questService = QuestService.Instance;
        _ = SequenceDirector.Instance;
        _ = SequenceCatalogAutoStart.Instance;
        sequenceRunner = SequenceRunner.Instance;
        questService.BeginProfileHydration();
        UnsubscribeRuntime();

        NarrativeStateSnapshot snapshot = null;
        bool useRemote = !forceOffline && CanUseRemote();
        if (useRemote)
        {
            try
            {
                snapshot = await remoteRepository.LoadSnapshotAsync(identity);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NarrativeProfileStateCoordinator] Remote hydration failed; using profile local state. {ex.Message}");
            }
        }

        snapshot = snapshot != null
            ? localRepository.MergeRemoteSnapshot(identity, snapshot)
            : await localRepository.LoadSnapshotAsync(identity);

        // Durable non-quest world flags share the same gameplay readiness
        // boundary so scene objects never interpret unhydrated state as false.
        await ProfileWorldFlagStateService.Instance.HydrateProfileAsync(identity, forceOffline);
        await SanctuaryStateService.Instance.HydrateProfileAsync(identity, forceOffline);

        await ApplyIntroCompatibilityAsync(snapshot);
        snapshot = await localRepository.LoadSnapshotAsync(identity);

        suppressRuntimeMutations = true;
        try
        {
            questService.RestoreSnapshot(snapshot.questStates, questCatalog);
            RestoreActiveSequence(snapshot.sequenceCheckpoints);
        }
        finally
        {
            suppressRuntimeMutations = false;
        }

        SubscribeRuntime();
        SetReady(true);

        if (useRemote)
            await FlushOutboxAsync();
    }

    private async Task ApplyIntroCompatibilityAsync(NarrativeStateSnapshot snapshot)
    {
        GameSessionContext session = GameSessionContext.Instance;
        if (session == null || snapshot == null || questCatalog == null)
            return;

        bool introIsCompleted = session.IntroComplete || snapshot.questStates.Any(item =>
            item != null && item.questId == "chainbound_intro" &&
            item.lifecycle == QuestLifecycleState.Completed);
        if (!introIsCompleted)
            return;

        if (!snapshot.questStates.Any(item => item != null && item.questId == "chainbound_intro") &&
            questCatalog.TryGet("chainbound_intro", out QuestDefinitionSO introDefinition))
        {
            bool hasStarter = !string.IsNullOrWhiteSpace(session.StarterSpeciesId);
            var intro = new QuestStateRecord
            {
                questId = "chainbound_intro",
                lifecycle = QuestLifecycleState.Completed,
                revision = 1,
                contentVersion = introDefinition.ContentVersion,
                updatedAtUtc = DateTime.UtcNow.ToString("O"),
                resolution = QuestResolutionRecord.Create(
                    hasStarter ? "chainling_bound" : "legacy_complete",
                    hasStarter
                        ? new Dictionary<string, string> { ["starter_species_id"] = session.StarterSpeciesId }
                        : null)
            };
            await AcceptLocalQuestMutationAsync(intro, 0);
        }

        if (!snapshot.questStates.Any(item => item != null && item.questId == "coffee_and_beer") &&
            questCatalog.TryGet("coffee_and_beer", out QuestDefinitionSO coffeeDefinition))
        {
            var coffee = new QuestStateRecord
            {
                questId = "coffee_and_beer",
                lifecycle = QuestLifecycleState.Available,
                revision = 1,
                contentVersion = coffeeDefinition.ContentVersion,
                updatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            await AcceptLocalQuestMutationAsync(coffee, 0);
        }
    }

    private void RestoreActiveSequence(IReadOnlyList<SequenceCheckpointRecord> checkpoints)
    {
        if (sequenceRunner == null || sequenceCatalog == null || checkpoints == null)
            return;

        SequenceCheckpointRecord selected = checkpoints
            .Where(item => item != null && item.status == SequenceCheckpointStatus.Active)
            .OrderByDescending(item => ParseUtc(item.updatedAtUtc))
            .ThenByDescending(item => item.revision)
            .FirstOrDefault();

        if (selected != null && !sequenceRunner.RestoreCheckpoint(selected, sequenceCatalog))
            Debug.LogWarning($"[NarrativeProfileStateCoordinator] Active sequence '{selected.sequenceId}' could not be restored from SequenceCatalogSO.");
    }

    private void SubscribeRuntime()
    {
        if (subscriptionsActive)
            return;

        if (questService != null)
            questService.OnQuestStateRecordChanged += HandleQuestStateChanged;
        if (sequenceRunner != null)
            sequenceRunner.OnPersistentStateChanged += HandleSequenceStateChanged;
        subscriptionsActive = true;
    }

    private void UnsubscribeRuntime()
    {
        if (!subscriptionsActive)
            return;

        if (questService != null)
            questService.OnQuestStateRecordChanged -= HandleQuestStateChanged;
        if (sequenceRunner != null)
            sequenceRunner.OnPersistentStateChanged -= HandleSequenceStateChanged;
        subscriptionsActive = false;
    }

    private async void HandleQuestStateChanged(QuestStateRecord record)
    {
        if (suppressRuntimeMutations || !IsReady || record == null)
            return;

        await AcceptLocalQuestMutationAsync(record, Math.Max(0L, record.revision - 1L));
        _ = FlushOutboxAsync();
    }

    private async void HandleSequenceStateChanged(SequenceCheckpointRecord record)
    {
        if (suppressRuntimeMutations || !IsReady || record == null)
            return;

        var mutation = new NarrativeStateMutation
        {
            mutationId = Guid.NewGuid().ToString("N"),
            kind = NarrativeMutationKind.SequenceCheckpoint,
            expectedRevision = Math.Max(0L, record.revision - 1L),
            sequenceCheckpoint = record.Clone()
        };
        NarrativeRepositoryResult accepted = await localRepository.ApplySequenceCheckpointAsync(currentIdentity, mutation);
        if (!accepted.Success)
        {
            Debug.LogError($"[NarrativeProfileStateCoordinator] Local sequence checkpoint was not accepted: {accepted.Error}");
            return;
        }

        _ = FlushOutboxAsync();
    }

    private async Task AcceptLocalQuestMutationAsync(QuestStateRecord record, long expectedRevision)
    {
        var mutation = new NarrativeStateMutation
        {
            mutationId = Guid.NewGuid().ToString("N"),
            kind = NarrativeMutationKind.QuestState,
            expectedRevision = Math.Max(0L, expectedRevision),
            questState = record.Clone()
        };
        NarrativeRepositoryResult accepted = await localRepository.ApplyQuestStateAsync(currentIdentity, mutation);
        if (!accepted.Success)
            Debug.LogError($"[NarrativeProfileStateCoordinator] Local quest state was not accepted: {accepted.Error}");
    }

    private async Task<bool> ReconcileConflictAsync(NarrativeStateMutation conflictedMutation)
    {
        try
        {
            NarrativeStateSnapshot remote = await remoteRepository.LoadSnapshotAsync(currentIdentity);
            if (conflictedMutation.kind == NarrativeMutationKind.QuestState && conflictedMutation.questState != null)
            {
                QuestStateRecord serverState = remote.questStates.FirstOrDefault(item =>
                    item != null && item.questId == conflictedMutation.questState.questId);
                if (serverState != null && serverState.lifecycle == QuestLifecycleState.Completed &&
                    conflictedMutation.questState.lifecycle != QuestLifecycleState.Completed)
                {
                    localRepository.AcknowledgeMutation(currentIdentity, conflictedMutation.mutationId);
                    localRepository.ReplaceQuestFromServer(currentIdentity, serverState);
                    RestoreQuestRuntimeFromLocal();
                    return true;
                }

                if (serverState != null && serverState.lifecycle == QuestLifecycleState.Completed)
                {
                    if (QuestResolutionsEqual(serverState, conflictedMutation.questState))
                    {
                        localRepository.AcknowledgeMutation(currentIdentity, conflictedMutation.mutationId);
                        localRepository.ReplaceQuestFromServer(currentIdentity, serverState);
                        RestoreQuestRuntimeFromLocal();
                        return true;
                    }

                    Debug.LogError($"[NarrativeProfileStateCoordinator] Immutable quest resolution conflict for '{serverState.questId}'. Server state was preserved.");
                    localRepository.AcknowledgeMutation(currentIdentity, conflictedMutation.mutationId);
                    localRepository.ReplaceQuestFromServer(currentIdentity, serverState);
                    RestoreQuestRuntimeFromLocal();
                    return true;
                }

                long serverRevision = serverState != null ? serverState.revision : 0L;
                conflictedMutation.expectedRevision = Math.Max(0L, serverRevision);
                conflictedMutation.questState.revision = conflictedMutation.expectedRevision + 1L;
                localRepository.ReplacePendingMutation(currentIdentity, conflictedMutation);
                return true;
            }

            if (conflictedMutation.kind == NarrativeMutationKind.SequenceCheckpoint &&
                conflictedMutation.sequenceCheckpoint != null)
            {
                SequenceCheckpointRecord serverState = remote.sequenceCheckpoints.FirstOrDefault(item =>
                    item != null && item.sequenceId == conflictedMutation.sequenceCheckpoint.sequenceId);
                if (serverState != null && serverState.status == SequenceCheckpointStatus.Completed &&
                    conflictedMutation.sequenceCheckpoint.status == SequenceCheckpointStatus.Active)
                {
                    localRepository.AcknowledgeMutation(currentIdentity, conflictedMutation.mutationId);
                    localRepository.ReplaceSequenceFromServer(currentIdentity, serverState);
                    sequenceRunner?.ClearPersistentState(serverState.sequenceId);
                    return true;
                }

                long serverRevision = serverState != null ? serverState.revision : 0L;
                conflictedMutation.expectedRevision = Math.Max(0L, serverRevision);
                conflictedMutation.sequenceCheckpoint.revision = conflictedMutation.expectedRevision + 1L;
                localRepository.ReplacePendingMutation(currentIdentity, conflictedMutation);
                return true;
            }

            Debug.LogWarning($"[NarrativeProfileStateCoordinator] Narrative mutation '{conflictedMutation.mutationId}' requires a later retry after revision reconciliation.");
            return false;
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[NarrativeProfileStateCoordinator] Conflict reconciliation deferred: {ex.Message}");
            return false;
        }
    }

    private static bool QuestResolutionsEqual(QuestStateRecord first, QuestStateRecord second)
    {
        if (first?.resolution == null || second?.resolution == null ||
            !string.Equals(first.resolution.resolutionId, second.resolution.resolutionId, StringComparison.Ordinal))
            return false;

        var firstContext = first.resolution.context ?? new List<NarrativeStringValue>();
        var secondContext = second.resolution.context ?? new List<NarrativeStringValue>();
        if (firstContext.Count != secondContext.Count)
            return false;
        for (int i = 0; i < firstContext.Count; i++)
        {
            NarrativeStringValue entry = firstContext[i];
            NarrativeStringValue match = secondContext.FirstOrDefault(candidate =>
                candidate != null && entry != null && candidate.key == entry.key);
            if (entry == null || match == null || match.value != entry.value)
                return false;
        }

        return true;
    }

    private void RestoreQuestRuntimeFromLocal()
    {
        if (questService == null || currentIdentity == null)
            return;

        NarrativeStateSnapshot local = localRepository.LoadSnapshot(currentIdentity);
        suppressRuntimeMutations = true;
        try
        {
            questService.RestoreSnapshot(local.questStates, questCatalog);
        }
        finally
        {
            suppressRuntimeMutations = false;
        }
    }

    private bool CanUseRemote()
    {
        return currentIdentity != null &&
            !string.IsNullOrWhiteSpace(currentIdentity.playerProfileId) &&
            !string.IsNullOrWhiteSpace(SupabaseAuthSessionContext.AccessToken);
    }

    private void SetReady(bool value)
    {
        if (IsReady == value)
            return;
        IsReady = value;
        OnReadyChanged?.Invoke(value);
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTime.TryParse(value, out DateTime parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;
    }
}
