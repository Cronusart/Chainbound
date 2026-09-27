using System.Collections;
using UnityEngine;

/// <summary>
/// Step that starts a quest.
/// </summary>
[CreateAssetMenu(fileName = "Step_StartQuest", menuName = "Chainbound/Narrative/Steps/Start Quest")]
public class Step_StartQuest : SequenceStep
{
    [Header("Quest")]
    [Tooltip("Quest definition to start")]
    [SerializeField] private QuestDefinitionSO questDefinition;
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (questDefinition == null)
        {
            Debug.LogError("[Step_StartQuest] Quest definition is null!");
            onComplete?.Invoke(StepOutcome.Failed("Quest definition is null"));
            yield break;
        }
        
        QuestService questService = QuestService.Instance;
        if (questService == null)
        {
            Debug.LogError("[Step_StartQuest] QuestService not found!");
            onComplete?.Invoke(StepOutcome.Failed("QuestService not found"));
            yield break;
        }
        
        questService.StartQuest(questDefinition.QuestId, questDefinition);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_StartQuest] Started quest: {questDefinition.QuestId}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (questDefinition == null)
        {
            Debug.LogWarning("[Step_StartQuest] Quest definition is not assigned!");
            return false;
        }
        return true;
    }
}

