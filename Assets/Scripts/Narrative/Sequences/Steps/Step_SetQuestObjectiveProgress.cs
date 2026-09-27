using System.Collections;
using UnityEngine;

/// <summary>
/// Records progress on a single quest objective.
/// Use this as a milestone marker in linear sequences — drop one of these
/// after each key beat so the quest log stays in sync with the sequence.
///
/// Two modes:
///   - completeObjective = true  → calls SetObjectiveProgress(targetAmount) so
///     QuestService.IsComplete returns true for this objective.
///   - completeObjective = false → calls AddObjectiveProgress(addAmount) for
///     granular / counter-style objectives (collect N items, etc.).
/// </summary>
[CreateAssetMenu(fileName = "Step_SetQuestObjectiveProgress",
    menuName = "Chainbound/Narrative/Steps/Set Quest Objective Progress")]
public class Step_SetQuestObjectiveProgress : SequenceStep
{
    [Header("Quest")]
    [Tooltip("Quest ID — must match QuestDefinitionSO.questId (e.g. 'chainbound_intro').")]
    [SerializeField] private string questId = "";

    [Tooltip("Objective ID — must match one of the QuestObjective.objectiveId values.")]
    [SerializeField] private string objectiveId = "";

    [Header("Progress")]
    [Tooltip("When true, calls SetObjectiveProgress(targetAmount) to mark this objective complete. " +
             "When false, calls AddObjectiveProgress(addAmount).")]
    [SerializeField] private bool completeObjective = true;

    [Tooltip("Used only when completeObjective = false. Adds this amount to objective progress.")]
    [SerializeField] private int addAmount = 1;

    [Tooltip("When true and QuestService.IsQuestComplete after this update, also calls CompleteQuest.")]
    [SerializeField] private bool autoCompleteQuest = false;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId))
        {
            Debug.LogError("[Step_SetQuestObjectiveProgress] questId or objectiveId is empty!");
            onComplete?.Invoke(StepOutcome.Failed("questId or objectiveId empty"));
            yield break;
        }

        QuestService qs = QuestService.Instance;
        if (qs == null)
        {
            Debug.LogWarning("[Step_SetQuestObjectiveProgress] QuestService not found — skipping.");
            onComplete?.Invoke(StepOutcome.Success("QuestService not found — skipped."));
            yield return null;
            yield break;
        }

        if (!qs.IsQuestActive(questId))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[Step_SetQuestObjectiveProgress] Quest '{questId}' is not active. " +
                                 "Progress not recorded.");
            onComplete?.Invoke(StepOutcome.Success("Quest not active — skipped."));
            yield return null;
            yield break;
        }

        if (completeObjective)
        {
            // Resolve target amount from the runtime state's objective dictionary.
            // Use Mathf.Max(1, ...) to guard against targetAmount=0 definitions,
            // which would otherwise never satisfy IsComplete (currentProgress >= targetAmount).
            QuestState state = qs.GetQuestState(questId);
            int target = 1;
            if (state != null && state.objectives.TryGetValue(objectiveId, out QuestObjective runtimeObj))
                target = Mathf.Max(1, runtimeObj.targetAmount);

            qs.SetObjectiveProgress(questId, objectiveId, target);
        }
        else
        {
            qs.AddObjectiveProgress(questId, objectiveId, addAmount);
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_SetQuestObjectiveProgress] quest={questId} obj={objectiveId} " +
                      $"complete={completeObjective} add={addAmount}");

        // SetObjectiveProgress already completes the quest when all objectives are met.
        // Only force CompleteQuest if still Active (e.g. edge cases / partial definitions).
        if (autoCompleteQuest && qs.IsQuestActive(questId))
        {
            qs.CompleteQuest(questId);
            if (enableDebugLogs)
                Debug.Log($"[Step_SetQuestObjectiveProgress] Auto-completed quest '{questId}'.");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        bool valid = base.Validate();

        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[Step_SetQuestObjectiveProgress] questId is not set!");
            valid = false;
        }

        if (string.IsNullOrEmpty(objectiveId))
        {
            Debug.LogWarning("[Step_SetQuestObjectiveProgress] objectiveId is not set!");
            valid = false;
        }

        return valid;
    }
}
