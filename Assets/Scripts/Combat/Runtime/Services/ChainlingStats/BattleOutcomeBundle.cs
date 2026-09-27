using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    [Serializable]
    public class BattleOutcomeBundle
    {
        public BattleResult result;
        public BattleRewardSummary rewardSummary;
        public List<ChainlingBattleStatContribution> chainlingContributions = new List<ChainlingBattleStatContribution>();
        public string forensicHash;
        public string configHash;

        public BattleOutcomeBundle()
        {
        }

        public BattleOutcomeBundle(
            BattleResult result,
            BattleRewardSummary rewardSummary,
            IEnumerable<ChainlingBattleStatContribution> contributions,
            string forensicHash = null,
            string configHash = null)
        {
            this.result = result;
            this.rewardSummary = rewardSummary;
            this.forensicHash = forensicHash ?? result?.forensicHash ?? string.Empty;
            this.configHash = configHash ?? result?.configHash ?? string.Empty;

            if (contributions == null)
                return;

            foreach (var contribution in contributions)
            {
                if (contribution != null)
                    chainlingContributions.Add(contribution.Clone());
            }
        }
    }
}
