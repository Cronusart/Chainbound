using UnityEngine;

/// <summary>
/// Runtime record for a single quest id: lifecycle + definition + optional progress state.
/// </summary>
public sealed class QuestRuntimeEntry
{
    public QuestLifecycleState Lifecycle { get; set; } = QuestLifecycleState.Unknown;

    public QuestDefinitionSO Definition { get; set; }

    /// <summary>
    /// Objective progress; non-null when the quest has been started at least once (Active, Completed, Failed).
    /// </summary>
    public QuestState State { get; set; }

    /// <summary>Durable outcome data. Populated when Lifecycle is Completed.</summary>
    public QuestResolutionRecord Resolution { get; set; } = new QuestResolutionRecord();

    /// <summary>Monotonic profile-state revision used by persistence adapters.</summary>
    public long Revision { get; set; }

    public static QuestState BuildStateFromDefinition(string questId, QuestDefinitionSO questDef)
    {
        if (questDef == null)
            return null;

        var state = new QuestState
        {
            questId = questId,
            questDefinition = questDef,
            objectives = new System.Collections.Generic.Dictionary<string, QuestObjective>()
        };

        foreach (var obj in questDef.Objectives)
        {
            state.objectives[obj.objectiveId] = new QuestObjective
            {
                objectiveId = obj.objectiveId,
                description = obj.description,
                targetAmount = obj.targetAmount,
                currentProgress = 0
            };
        }

        return state;
    }
}
