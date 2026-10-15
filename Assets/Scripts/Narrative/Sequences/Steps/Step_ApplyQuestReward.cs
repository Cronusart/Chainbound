using System;
using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Applies one server-catalog quest reward. The client sends the reward id only.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_ApplyQuestReward",
    menuName = "Chainbound/Narrative/Steps/Apply Quest Reward")]
public class Step_ApplyQuestReward : SequenceStep
{
    [SerializeField] private string rewardId = "";

    public string RewardId => rewardId != null ? rewardId.Trim() : "";

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrWhiteSpace(RewardId))
        {
            onComplete?.Invoke(StepOutcome.Failed("Quest reward id is empty.", GetOnFailLabel()));
            yield break;
        }

        QuestRewardService service = QuestRewardService.EnsureInstance();
        Task<QuestRewardResult> task = service.ApplyAsync(RewardId);
        while (task != null && !task.IsCompleted)
            yield return null;

        bool ok = task != null &&
                  task.Status == TaskStatus.RanToCompletion &&
                  task.Result != null &&
                  task.Result.ok;
        if (!ok)
        {
            string error = task != null && task.Result != null && !string.IsNullOrWhiteSpace(task.Result.error)
                ? task.Result.error
                : "quest_reward_failed";
            Debug.LogWarning($"[Step_ApplyQuestReward] '{RewardId}' failed: {error}");
            onComplete?.Invoke(StepOutcome.Failed(error, GetOnFailLabel()));
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log($"[Step_ApplyQuestReward] '{RewardId}' applied.", this);

        onComplete?.Invoke(StepOutcome.Success());
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(RewardId))
        {
            Debug.LogWarning("[Step_ApplyQuestReward] Reward id is empty.");
            return false;
        }

        return true;
    }
}
