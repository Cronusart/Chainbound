using System.Collections.Generic;

namespace Chainbound.Combat
{
    public static class ChainlingBadgeEvaluator
    {
        public static List<string> EvaluateUnlockedBadgeIds(
            ChainlingPveServiceRecord record,
            IReadOnlyList<ChainlingBadgeDefinitionSO> definitions,
            BattleMode battleMode = BattleMode.PvE)
        {
            var unlocked = new List<string>();
            if (record == null || definitions == null)
                return unlocked;

            for (int i = 0; i < definitions.Count; i++)
            {
                ChainlingBadgeDefinitionSO definition = definitions[i];
                if (definition == null || !definition.IsValid)
                    continue;

                if (definition.battleModeFilter != battleMode)
                    continue;

                if (record.GetMetricValue(definition.metricKey) >= definition.threshold)
                    unlocked.Add(definition.badgeId);
            }

            return unlocked;
        }
    }
}
