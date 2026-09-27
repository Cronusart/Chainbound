using System;

namespace Chainbound.Combat
{
    [Serializable]
    public class ChainlingPveServiceRecord
    {
        public const int CurrentVersion = 1;

        public int serviceRecordVersion = CurrentVersion;
        public int totalVictories;
        public long xpEarnedLifetime;
        public int progressionMovesGranted;
        public long damageDealt;
        public long damageTaken;
        public long damageMitigated;
        public long healingTotal;
        public int enemyKos;

        public long GetMetricValue(string metricKey)
        {
            switch (metricKey)
            {
                case ChainlingMetricIds.PveTotalVictories:
                    return totalVictories;
                case ChainlingMetricIds.PveXpEarnedLifetime:
                    return xpEarnedLifetime;
                case ChainlingMetricIds.PveProgressionMovesGranted:
                    return progressionMovesGranted;
                case ChainlingMetricIds.PveDamageDealt:
                    return damageDealt;
                case ChainlingMetricIds.PveDamageTaken:
                    return damageTaken;
                case ChainlingMetricIds.PveDamageMitigated:
                    return damageMitigated;
                case ChainlingMetricIds.PveHealingTotal:
                    return healingTotal;
                case ChainlingMetricIds.PveEnemyKos:
                    return enemyKos;
                default:
                    return 0;
            }
        }

        public void Apply(ChainlingPveStatDelta delta)
        {
            if (delta == null)
                return;

            totalVictories = AddNonNegative(totalVictories, delta.totalVictories);
            xpEarnedLifetime = AddNonNegative(xpEarnedLifetime, delta.xpEarnedLifetime);
            progressionMovesGranted = AddNonNegative(progressionMovesGranted, delta.progressionMovesGranted);
            damageDealt = AddNonNegative(damageDealt, delta.damageDealt);
            damageTaken = AddNonNegative(damageTaken, delta.damageTaken);
            damageMitigated = AddNonNegative(damageMitigated, delta.damageMitigated);
            healingTotal = AddNonNegative(healingTotal, delta.healingTotal);
            enemyKos = AddNonNegative(enemyKos, delta.enemyKos);
        }

        public static ChainlingPveServiceRecord CloneOrDefault(ChainlingPveServiceRecord source)
        {
            if (source == null)
                return new ChainlingPveServiceRecord();

            return new ChainlingPveServiceRecord
            {
                serviceRecordVersion = source.serviceRecordVersion > 0 ? source.serviceRecordVersion : CurrentVersion,
                totalVictories = Math.Max(0, source.totalVictories),
                xpEarnedLifetime = Math.Max(0, source.xpEarnedLifetime),
                progressionMovesGranted = Math.Max(0, source.progressionMovesGranted),
                damageDealt = Math.Max(0, source.damageDealt),
                damageTaken = Math.Max(0, source.damageTaken),
                damageMitigated = Math.Max(0, source.damageMitigated),
                healingTotal = Math.Max(0, source.healingTotal),
                enemyKos = Math.Max(0, source.enemyKos),
            };
        }

        private static int AddNonNegative(int current, int delta)
        {
            return Math.Max(0, current) + Math.Max(0, delta);
        }

        private static long AddNonNegative(long current, long delta)
        {
            return Math.Max(0, current) + Math.Max(0, delta);
        }
    }
}
