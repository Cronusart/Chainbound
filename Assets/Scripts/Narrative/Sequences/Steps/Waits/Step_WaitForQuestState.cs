using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that waits for a quest to reach a specific state.
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForQuestState", menuName = "Chainbound/Narrative/Steps/Waits/Wait For Quest State")]
public class Step_WaitForQuestState : SequenceStep
{
    [Header("Quest")]
    [Tooltip("Quest ID to wait for")]
    [SerializeField] private string questId = "";
    
    [Header("Target State")]
    [Tooltip("State to wait for")]
    [SerializeField] private QuestTargetState targetState = QuestTargetState.Started;
    
    [Header("Timeout")]
    [Tooltip("Timeout in seconds (0 = no timeout)")]
    [SerializeField] private float timeoutSeconds = 0f;
    
    [Tooltip("Label to jump to on timeout (optional)")]
    [SerializeField] private string onTimeoutLabel = "";
    
    public enum QuestTargetState
    {
        Started,
        Active,  // Same as Started (for clarity)
        Completed,
        ObjectiveComplete
    }
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogError("[Step_WaitForQuestState] Quest ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Quest ID is empty"));
            yield break;
        }
        
        QuestService questService = QuestService.Instance;
        if (questService == null)
        {
            Debug.LogError("[Step_WaitForQuestState] QuestService not found!");
            onComplete?.Invoke(StepOutcome.Failed("QuestService not found"));
            yield break;
        }
        
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_WaitForQuestState] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForQuestState] Waiting for quest '{questId}' to reach state: {targetState}");
        }
        
        // Check if already in target state
        bool alreadyReached = CheckQuestState(questService);
        if (alreadyReached)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_WaitForQuestState] Quest already in target state: {questId}");
            }
            onComplete?.Invoke(StepOutcome.Success());
            yield break;
        }
        
        // Subscribe to quest events
        bool stateReached = false;
        System.Action<string> onQuestStarted = (id) =>
        {
            if (id == questId && targetState == QuestTargetState.Started)
            {
                stateReached = true;
            }
        };
        System.Action<string> onQuestCompleted = (id) =>
        {
            if (id == questId && (targetState == QuestTargetState.Completed || targetState == QuestTargetState.ObjectiveComplete))
            {
                stateReached = true;
            }
        };
        
        questService.OnQuestStarted += onQuestStarted;
        questService.OnQuestCompleted += onQuestCompleted;
        
        // Wait for state or timeout
        float elapsed = 0f;
        while (!stateReached && (timeoutSeconds <= 0f || elapsed < timeoutSeconds))
        {
            // Also check state each frame (in case event was missed)
            if (CheckQuestState(questService))
            {
                stateReached = true;
                break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Unsubscribe
        questService.OnQuestStarted -= onQuestStarted;
        questService.OnQuestCompleted -= onQuestCompleted;
        
        if (stateReached)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_WaitForQuestState] Quest reached target state: {questId}");
            }
            onComplete?.Invoke(StepOutcome.Success());
        }
        else
        {
            // Timeout
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[Step_WaitForQuestState] Timeout waiting for quest state: {questId}");
            }
            
            if (!string.IsNullOrEmpty(onTimeoutLabel))
            {
                runner.JumpToLabel(onTimeoutLabel);
                onComplete?.Invoke(StepOutcome.Success("Timeout - jumped to label"));
            }
            else
            {
                onComplete?.Invoke(StepOutcome.Failed($"Timeout waiting for quest state: {questId}"));
            }
        }
    }
    
    private bool CheckQuestState(QuestService questService)
    {
        switch (targetState)
        {
            case QuestTargetState.Started:
            case QuestTargetState.Active:
                return questService.IsQuestActive(questId);
            case QuestTargetState.Completed:
            case QuestTargetState.ObjectiveComplete:
                return questService.IsQuestComplete(questId);
            default:
                return false;
        }
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogWarning("[Step_WaitForQuestState] Quest ID is not set!");
            return false;
        }
        return true;
    }
}

