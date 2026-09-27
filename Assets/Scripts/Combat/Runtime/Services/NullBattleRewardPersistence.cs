using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class NullBattleRewardPersistence : IBattleRewardPersistence
    {
        public bool IsAvailable => true;

        public Task<bool> PersistRewardsAsync(BattleRewardSummary summary, string walletAddress)
        {
            return PersistOutcomeAsync(new BattleOutcomeBundle(null, summary, null), walletAddress);
        }

        public Task<bool> PersistOutcomeAsync(BattleOutcomeBundle outcome, string walletAddress)
        {
            var builder = new StringBuilder();
            builder.Append($"[BattleRewards] Local-only persistence. wallet={walletAddress ?? "<none>"}");

            BattleRewardSummary summary = outcome != null ? outcome.rewardSummary : null;
            if (summary != null)
            {
                builder.Append($" ram={summary.ramEarned}");
                if (summary.chainlingRewards != null)
                {
                    for (int i = 0; i < summary.chainlingRewards.Count; i++)
                    {
                        var reward = summary.chainlingRewards[i];
                        if (reward == null)
                            continue;

                        builder.Append($" | {reward.displayName}:{reward.xpEarned}xp");
                    }
                }
            }

            if (outcome?.chainlingContributions != null && outcome.chainlingContributions.Count > 0)
                builder.Append($" pveContributions={outcome.chainlingContributions.Count}");

            Debug.Log(builder.ToString());
            return Task.FromResult(true);
        }
    }
}
