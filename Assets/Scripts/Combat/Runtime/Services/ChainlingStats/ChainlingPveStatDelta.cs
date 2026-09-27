using System;

namespace Chainbound.Combat
{
    [Serializable]
    public class ChainlingPveStatDelta
    {
        public int totalVictories;
        public long xpEarnedLifetime;
        public int progressionMovesGranted;
        public long damageDealt;
        public long damageTaken;
        public long damageMitigated;
        public long healingTotal;
        public int enemyKos;

        public bool IsEmpty =>
            totalVictories == 0 &&
            xpEarnedLifetime == 0 &&
            progressionMovesGranted == 0 &&
            damageDealt == 0 &&
            damageTaken == 0 &&
            damageMitigated == 0 &&
            healingTotal == 0 &&
            enemyKos == 0;

        public void Add(ChainlingPveStatDelta other)
        {
            if (other == null)
                return;

            totalVictories += Math.Max(0, other.totalVictories);
            xpEarnedLifetime += Math.Max(0, other.xpEarnedLifetime);
            progressionMovesGranted += Math.Max(0, other.progressionMovesGranted);
            damageDealt += Math.Max(0, other.damageDealt);
            damageTaken += Math.Max(0, other.damageTaken);
            damageMitigated += Math.Max(0, other.damageMitigated);
            healingTotal += Math.Max(0, other.healingTotal);
            enemyKos += Math.Max(0, other.enemyKos);
        }

        public ChainlingPveStatDelta Clone()
        {
            return new ChainlingPveStatDelta
            {
                totalVictories = totalVictories,
                xpEarnedLifetime = xpEarnedLifetime,
                progressionMovesGranted = progressionMovesGranted,
                damageDealt = damageDealt,
                damageTaken = damageTaken,
                damageMitigated = damageMitigated,
                healingTotal = healingTotal,
                enemyKos = enemyKos,
            };
        }
    }
}
