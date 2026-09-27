using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "ChainlingBadgeDefinition", menuName = "Chainbound/Combat/Chainling Badge")]
    public class ChainlingBadgeDefinitionSO : ScriptableObject
    {
        public string badgeId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public string metricKey = ChainlingMetricIds.PveEnemyKos;
        public long threshold = 1;
        public BattleMode battleModeFilter = BattleMode.PvE;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(badgeId) &&
            ChainlingMetricIds.IsPveMetric(metricKey) &&
            threshold > 0;
    }
}
