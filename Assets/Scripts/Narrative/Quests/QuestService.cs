using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Service for managing quest state.
/// Tracks lifecycle (locked / available / active / completed / failed), objectives, and progress.
/// </summary>
public class QuestService : MonoBehaviour
{
    private static QuestService instance;

    public static QuestService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<QuestService>();
                if (instance == null)
                {
                    if (!Application.isPlaying)
                        return null;
                    GameObject go = new GameObject("QuestService");
                    instance = go.AddComponent<QuestService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly Dictionary<string, QuestRuntimeEntry> questEntries = new Dictionary<string, QuestRuntimeEntry>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public event Action<string> OnQuestStarted;
    public event Action<string, string, int> OnObjectiveProgressChanged;
    public event Action<string> OnQuestCompleted;
    public event Action<string> OnQuestAvailable;
    public event Action<string> OnQuestFailed;
    public event Action<string, QuestLifecycleState, QuestLifecycleState> OnQuestLifecycleStateChanged;
    public event Action<QuestStateRecord> OnQuestStateRecordChanged;
    public event Action<bool> OnHydrationStateChanged;

    /// <summary>
    /// False only while a profile snapshot is actively being hydrated. It defaults
    /// true for scene/test compatibility until a coordinator begins hydration.
    /// </summary>
    public bool IsHydrated { get; private set; } = true;

    private void RaiseLifecycleChanged(string questId, QuestLifecycleState from, QuestLifecycleState to)
    {
        OnQuestLifecycleStateChanged?.Invoke(questId, from, to);
        if (enableDebugLogs)
            Debug.Log($"[QuestService] '{questId}' lifecycle: {from} -> {to}");
    }

    /// <summary>
    /// Start or activate a quest (creates runtime objectives). Idempotent if already Active with same definition.
    /// </summary>
    public void StartQuest(string questId, QuestDefinitionSO questDef)
    {
        if (string.IsNullOrEmpty(questId) || questDef == null)
        {
            Debug.LogWarning("[QuestService] Cannot start quest with null ID or definition!");
            return;
        }

        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry))
        {
            entry = new QuestRuntimeEntry
            {
                Definition = questDef,
                Lifecycle = QuestLifecycleState.Active,
                State = QuestRuntimeEntry.BuildStateFromDefinition(questId, questDef)
            };
            questEntries[questId] = entry;
            RaiseLifecycleChanged(questId, QuestLifecycleState.Unknown, QuestLifecycleState.Active);
            OnQuestStarted?.Invoke(questId);
            PublishStateChanged(questId, entry);
            if (enableDebugLogs)
                Debug.Log($"[QuestService] Started quest: {questId}");
            return;
        }

        switch (entry.Lifecycle)
        {
            case QuestLifecycleState.Available:
            case QuestLifecycleState.Locked:
                entry.Definition = questDef;
                entry.State = QuestRuntimeEntry.BuildStateFromDefinition(questId, questDef);
                var prev = entry.Lifecycle;
                entry.Lifecycle = QuestLifecycleState.Active;
                RaiseLifecycleChanged(questId, prev, QuestLifecycleState.Active);
                OnQuestStarted?.Invoke(questId);
                PublishStateChanged(questId, entry);
                if (enableDebugLogs)
                    Debug.Log($"[QuestService] Started quest from {prev}: {questId}");
                return;

            case QuestLifecycleState.Active:
                Debug.LogWarning($"[QuestService] Quest '{questId}' is already active!");
                return;

            case QuestLifecycleState.Completed:
            case QuestLifecycleState.Failed:
                Debug.LogWarning($"[QuestService] Quest '{questId}' is {entry.Lifecycle}; clear or reset before restarting.");
                return;

            default:
                entry.Definition = questDef;
                entry.State = QuestRuntimeEntry.BuildStateFromDefinition(questId, questDef);
                entry.Lifecycle = QuestLifecycleState.Active;
                RaiseLifecycleChanged(questId, QuestLifecycleState.Unknown, QuestLifecycleState.Active);
                OnQuestStarted?.Invoke(questId);
                PublishStateChanged(questId, entry);
                if (enableDebugLogs)
                    Debug.Log($"[QuestService] Started quest: {questId}");
                return;
        }
    }

    /// <summary>
    /// Register a quest as locked (not yet offerable).
    /// </summary>
    public void SetQuestLocked(string questId, QuestDefinitionSO questDef)
    {
        if (string.IsNullOrEmpty(questId) || questDef == null)
        {
            Debug.LogWarning("[QuestService] SetQuestLocked: invalid id or definition.");
            return;
        }

        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry))
        {
            entry = new QuestRuntimeEntry { Definition = questDef, Lifecycle = QuestLifecycleState.Locked };
            questEntries[questId] = entry;
            RaiseLifecycleChanged(questId, QuestLifecycleState.Unknown, QuestLifecycleState.Locked);
            PublishStateChanged(questId, entry);
            return;
        }

        if (entry.Lifecycle == QuestLifecycleState.Active)
        {
            Debug.LogWarning($"[QuestService] Cannot lock active quest '{questId}'.");
            return;
        }

        if (entry.Lifecycle == QuestLifecycleState.Completed)
        {
            Debug.LogWarning($"[QuestService] Cannot lock completed quest '{questId}'.");
            return;
        }

        var was = entry.Lifecycle;
        entry.Definition = questDef;
        entry.Lifecycle = QuestLifecycleState.Locked;
        RaiseLifecycleChanged(questId, was, QuestLifecycleState.Locked);
        PublishStateChanged(questId, entry);
    }

    /// <summary>
    /// Register a quest as available (offerable) without starting objectives.
    /// </summary>
    public void SetQuestAvailable(string questId, QuestDefinitionSO questDef)
    {
        if (string.IsNullOrEmpty(questId) || questDef == null)
        {
            Debug.LogWarning("[QuestService] SetQuestAvailable: invalid id or definition.");
            return;
        }

        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry))
        {
            entry = new QuestRuntimeEntry { Definition = questDef, Lifecycle = QuestLifecycleState.Available };
            questEntries[questId] = entry;
            RaiseLifecycleChanged(questId, QuestLifecycleState.Unknown, QuestLifecycleState.Available);
            OnQuestAvailable?.Invoke(questId);
            PublishStateChanged(questId, entry);
            return;
        }

        if (entry.Lifecycle == QuestLifecycleState.Active)
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is already active; SetQuestAvailable ignored.");
            return;
        }

        if (entry.Lifecycle == QuestLifecycleState.Completed)
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is completed; SetQuestAvailable ignored.");
            return;
        }

        var was = entry.Lifecycle;
        entry.Definition = questDef;
        entry.Lifecycle = QuestLifecycleState.Available;
        RaiseLifecycleChanged(questId, was, QuestLifecycleState.Available);
        OnQuestAvailable?.Invoke(questId);
        PublishStateChanged(questId, entry);
    }

    /// <summary>
    /// Mark an active quest as failed.
    /// </summary>
    public void FailQuest(string questId)
    {
        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry) || entry.Lifecycle != QuestLifecycleState.Active)
        {
            Debug.LogWarning($"[QuestService] FailQuest: '{questId}' is not active.");
            return;
        }

        entry.Lifecycle = QuestLifecycleState.Failed;
        RaiseLifecycleChanged(questId, QuestLifecycleState.Active, QuestLifecycleState.Failed);
        OnQuestFailed?.Invoke(questId);
        PublishStateChanged(questId, entry);
        if (enableDebugLogs)
            Debug.Log($"[QuestService] Quest failed: {questId}");
    }

    public QuestLifecycleState GetQuestLifecycleState(string questId)
    {
        if (string.IsNullOrEmpty(questId) || !questEntries.TryGetValue(questId, out var entry))
            return QuestLifecycleState.Unknown;
        return entry.Lifecycle;
    }

    public bool IsQuestAvailable(string questId)
    {
        return GetQuestLifecycleState(questId) == QuestLifecycleState.Available;
    }

    public void SetObjectiveProgress(string questId, string objectiveId, int progress)
    {
        if (!TryGetActiveEntry(questId, out QuestRuntimeEntry entry, out QuestState state))
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is not active!");
            return;
        }

        if (!state.objectives.TryGetValue(objectiveId, out QuestObjective objective))
        {
            Debug.LogWarning($"[QuestService] Objective '{objectiveId}' not found in quest '{questId}'!");
            return;
        }

        objective.currentProgress = Mathf.Clamp(progress, 0, objective.targetAmount);
        OnObjectiveProgressChanged?.Invoke(questId, objectiveId, objective.currentProgress);

        if (AreAllObjectivesComplete(state) &&
            (entry.Definition == null || entry.Definition.AutoCompleteWhenObjectivesComplete))
        {
            CompleteQuestInternal(questId, entry, fromObjectives: true, null, null);
        }
        else
        {
            PublishStateChanged(questId, entry);
        }

        if (enableDebugLogs)
            Debug.Log($"[QuestService] Objective '{objectiveId}' progress: {objective.currentProgress}/{objective.targetAmount}");
    }

    public void AddObjectiveProgress(string questId, string objectiveId, int amount)
    {
        if (!TryGetActiveEntry(questId, out _, out QuestState state))
            return;

        if (!state.objectives.TryGetValue(objectiveId, out QuestObjective objective))
            return;

        SetObjectiveProgress(questId, objectiveId, objective.currentProgress + amount);
    }

    /// <summary>
    /// Completes an active quest (force-complete allowed even if objectives incomplete).
    /// </summary>
    public void CompleteQuest(string questId)
    {
        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry))
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is not registered!");
            return;
        }

        if (entry.Lifecycle != QuestLifecycleState.Active)
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is not active (state={entry.Lifecycle})!");
            return;
        }

        CompleteQuestInternal(questId, entry, fromObjectives: false, null, null);
    }

    /// <summary>
    /// Completes a quest with an explicit, profile-persistent outcome and context.
    /// Returns false without mutating state when the resolution is invalid.
    /// </summary>
    public bool CompleteQuest(
        string questId,
        string resolutionId,
        IReadOnlyDictionary<string, string> resolutionContext)
    {
        if (!questEntries.TryGetValue(questId, out QuestRuntimeEntry entry) ||
            entry.Lifecycle != QuestLifecycleState.Active)
        {
            Debug.LogWarning($"[QuestService] Quest '{questId}' is not active and cannot be resolved.");
            return false;
        }

        return CompleteQuestInternal(
            questId,
            entry,
            fromObjectives: false,
            resolutionId,
            resolutionContext);
    }

    private bool CompleteQuestInternal(
        string questId,
        QuestRuntimeEntry entry,
        bool fromObjectives,
        string explicitResolutionId,
        IReadOnlyDictionary<string, string> explicitResolutionContext)
    {
        string resolutionId = string.IsNullOrWhiteSpace(explicitResolutionId)
            ? entry.Definition != null ? entry.Definition.DefaultResolutionId : "standard"
            : explicitResolutionId.Trim();

        IEnumerable<string> contextKeys = explicitResolutionContext != null
            ? explicitResolutionContext.Keys
            : Array.Empty<string>();
        if (entry.Definition != null &&
            !entry.Definition.ValidateResolution(resolutionId, contextKeys, out string validationError))
        {
            Debug.LogWarning($"[QuestService] {validationError}");
            return false;
        }

        var prev = entry.Lifecycle;
        entry.Lifecycle = QuestLifecycleState.Completed;
        entry.Resolution = QuestResolutionRecord.Create(
            resolutionId,
            explicitResolutionContext,
            DateTime.UtcNow.ToString("O"));
        RaiseLifecycleChanged(questId, prev, QuestLifecycleState.Completed);
        OnQuestCompleted?.Invoke(questId);
        PublishStateChanged(questId, entry);
        if (enableDebugLogs)
            Debug.Log($"[QuestService] Quest completed: {questId} (fromObjectives={fromObjectives})");
        return true;
    }

    private static bool AreAllObjectivesComplete(QuestState state)
    {
        if (state == null || state.objectives == null || state.objectives.Count == 0)
            return false;
        foreach (var objective in state.objectives.Values)
        {
            if (!objective.IsComplete)
                return false;
        }
        return true;
    }

    private bool TryGetActiveEntry(string questId, out QuestRuntimeEntry entry, out QuestState state)
    {
        entry = null;
        state = null;
        if (string.IsNullOrEmpty(questId) || !questEntries.TryGetValue(questId, out entry))
            return false;
        if (entry.Lifecycle != QuestLifecycleState.Active)
            return false;
        state = entry.State;
        return state != null;
    }

    public bool IsQuestActive(string questId)
    {
        return GetQuestLifecycleState(questId) == QuestLifecycleState.Active;
    }

    /// <summary>
    /// True when the quest finished successfully (lifecycle Completed).
    /// </summary>
    public bool IsQuestComplete(string questId)
    {
        return GetQuestLifecycleState(questId) == QuestLifecycleState.Completed;
    }

    public bool TryGetObjective(string questId, string objectiveId, out QuestObjective objective)
    {
        objective = null;
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId))
            return false;
        if (!TryGetActiveEntry(questId, out _, out QuestState state))
            return false;
        return state.objectives.TryGetValue(objectiveId, out objective);
    }

    /// <summary>
    /// Runtime objective state; non-null only when quest has been started at least once.
    /// </summary>
    public QuestState GetQuestState(string questId)
    {
        if (string.IsNullOrEmpty(questId) || !questEntries.TryGetValue(questId, out var entry))
            return null;
        return entry.State;
    }

    public bool TryGetResolution(string questId, out QuestResolutionRecord resolution)
    {
        resolution = null;
        if (string.IsNullOrWhiteSpace(questId) ||
            !questEntries.TryGetValue(questId, out QuestRuntimeEntry entry) ||
            entry.Lifecycle != QuestLifecycleState.Completed ||
            entry.Resolution == null ||
            !entry.Resolution.HasResolution)
        {
            return false;
        }

        resolution = entry.Resolution.Clone();
        return true;
    }

    public void BeginProfileHydration()
    {
        IsHydrated = false;
        OnHydrationStateChanged?.Invoke(false);
    }

    public void RestoreSnapshot(IReadOnlyList<QuestStateRecord> records, QuestCatalogSO catalog)
    {
        questEntries.Clear();

        if (records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                QuestStateRecord record = records[i];
                if (record == null || string.IsNullOrWhiteSpace(record.questId) ||
                    record.lifecycle == QuestLifecycleState.Unknown)
                {
                    continue;
                }

                if (catalog == null || !catalog.TryGet(record.questId, out QuestDefinitionSO definition))
                {
                    Debug.LogWarning($"[QuestService] Persisted quest '{record.questId}' is missing from QuestCatalogSO.");
                    continue;
                }

                QuestState state = record.lifecycle == QuestLifecycleState.Locked ||
                                   record.lifecycle == QuestLifecycleState.Available
                    ? null
                    : QuestRuntimeEntry.BuildStateFromDefinition(record.questId, definition);

                if (state != null && record.objectiveProgress != null)
                {
                    for (int objectiveIndex = 0; objectiveIndex < record.objectiveProgress.Count; objectiveIndex++)
                    {
                        ObjectiveProgressRecord progress = record.objectiveProgress[objectiveIndex];
                        if (progress != null && state.objectives.TryGetValue(progress.objectiveId, out QuestObjective objective))
                            objective.currentProgress = Mathf.Clamp(progress.currentProgress, 0, objective.targetAmount);
                    }
                }

                questEntries[record.questId] = new QuestRuntimeEntry
                {
                    Definition = definition,
                    Lifecycle = record.lifecycle,
                    State = state,
                    Resolution = record.resolution != null ? record.resolution.Clone() : new QuestResolutionRecord(),
                    Revision = Math.Max(0L, record.revision)
                };
            }
        }

        IsHydrated = true;
        OnHydrationStateChanged?.Invoke(true);
    }

    public NarrativeStateSnapshot ExportSnapshot(string playerProfileId = "")
    {
        var snapshot = new NarrativeStateSnapshot { playerProfileId = playerProfileId ?? "" };
        foreach (KeyValuePair<string, QuestRuntimeEntry> pair in questEntries)
            snapshot.questStates.Add(BuildRecord(pair.Key, pair.Value, incrementRevision: false));
        return snapshot;
    }

    /// <summary>
    /// Quest ids that are currently Active.
    /// </summary>
    public IReadOnlyList<string> GetActiveQuestIds()
    {
        return questEntries
            .Where(kv => kv.Value.Lifecycle == QuestLifecycleState.Active)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// All registered quest ids (any lifecycle except implicit unknown for missing keys).
    /// </summary>
    public IReadOnlyList<string> GetRegisteredQuestIds()
    {
        return questEntries.Keys.ToList();
    }

    private void PublishStateChanged(string questId, QuestRuntimeEntry entry)
    {
        if (entry == null)
            return;

        QuestStateRecord record = BuildRecord(questId, entry, incrementRevision: true);
        OnQuestStateRecordChanged?.Invoke(record);
    }

    private static QuestStateRecord BuildRecord(
        string questId,
        QuestRuntimeEntry entry,
        bool incrementRevision)
    {
        if (incrementRevision)
            entry.Revision = Math.Max(0L, entry.Revision) + 1L;

        var record = new QuestStateRecord
        {
            questId = questId ?? "",
            lifecycle = entry.Lifecycle,
            resolution = entry.Resolution != null ? entry.Resolution.Clone() : new QuestResolutionRecord(),
            revision = Math.Max(0L, entry.Revision),
            contentVersion = entry.Definition != null ? entry.Definition.ContentVersion : 1,
            updatedAtUtc = DateTime.UtcNow.ToString("O")
        };

        if (entry.State?.objectives != null)
        {
            foreach (KeyValuePair<string, QuestObjective> pair in entry.State.objectives)
            {
                if (pair.Value == null)
                    continue;
                record.objectiveProgress.Add(new ObjectiveProgressRecord
                {
                    objectiveId = pair.Key,
                    currentProgress = Mathf.Max(0, pair.Value.currentProgress)
                });
            }
            record.objectiveProgress.Sort((a, b) => string.CompareOrdinal(a.objectiveId, b.objectiveId));
        }

        return record;
    }
}

/// <summary>
/// Runtime quest state (objectives in progress).
/// </summary>
[System.Serializable]
public class QuestState
{
    public string questId;
    public QuestDefinitionSO questDefinition;
    public Dictionary<string, QuestObjective> objectives = new Dictionary<string, QuestObjective>();
}
