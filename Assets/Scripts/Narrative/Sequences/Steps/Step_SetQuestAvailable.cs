using System.Collections;
using UnityEngine;

/// <summary>
/// Marks a quest Available without starting it.
/// Use this to offer a follow-up quest after another quest completes; an NPC
/// interaction profile can then start it on first interact.
/// </summary>
[CreateAssetMenu(fileName = "Step_SetQuestAvailable", menuName = "Chainbound/Narrative/Steps/Set Quest Available")]
public class Step_SetQuestAvailable : SequenceStep
{
    [Header("Quest")]
    [Tooltip("Quest definition to mark Available.")]
    [SerializeField] private QuestDefinitionSO questDefinition;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (questDefinition == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("Quest definition is null."));
            yield break;
        }

        QuestService questService = QuestService.Instance;
        if (questService == null)
        {
            onComplete?.Invoke(StepOutcome.Failed("QuestService not found."));
            yield break;
        }

        questService.SetQuestAvailable(questDefinition.QuestId, questDefinition);

        if (enableDebugLogs)
            Debug.Log($"[Step_SetQuestAvailable] Quest available: {questDefinition.QuestId}");

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        bool valid = base.Validate();
        if (questDefinition == null)
        {
            Debug.LogWarning("[Step_SetQuestAvailable] Quest definition is not assigned!");
            valid = false;
        }

        return valid;
    }
}
