using UnityEngine;

/// <summary>
/// Condition that checks quest state.
/// </summary>
[CreateAssetMenu(fileName = "Condition_QuestState", menuName = "Chainbound/Narrative/Conditions/Quest State")]
public class ConditionQuestState : SequenceCondition
{
    [Header("Quest")]
    [Tooltip("Quest ID to check")]
    [SerializeField] private string questId = "";
    
    [Header("State Check")]
    [Tooltip("What state to check for")]
    [SerializeField] private QuestTargetState targetState = QuestTargetState.Started;
    
    public enum QuestTargetState
    {
        Started,
        Completed,
        Active
    }
    
    public override bool Evaluate(SequenceContext context)
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[ConditionQuestState] Quest ID is empty!");
            return false;
        }
        
        QuestService questService = QuestService.Instance;
        if (questService == null)
        {
            Debug.LogWarning("[ConditionQuestState] QuestService not found!");
            return false;
        }
        
        switch (targetState)
        {
            case QuestTargetState.Started:
            case QuestTargetState.Active:
                return questService.IsQuestActive(questId);
            case QuestTargetState.Completed:
                return questService.IsQuestComplete(questId);
            default:
                return false;
        }
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[ConditionQuestState] Quest ID is not set!");
            return false;
        }
        return true;
    }
}

