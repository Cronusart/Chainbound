using System;

namespace Chainbound.Combat
{
    public static class ChainlingMetricIds
    {
        public const string PveTotalVictories = "pve.total_victories";
        public const string PveXpEarnedLifetime = "pve.xp_earned_lifetime";
        public const string PveProgressionMovesGranted = "pve.progression_moves_granted";
        public const string PveDamageDealt = "pve.damage_dealt";
        public const string PveDamageTaken = "pve.damage_taken";
        public const string PveDamageMitigated = "pve.damage_mitigated";
        public const string PveHealingTotal = "pve.healing_total";
        public const string PveEnemyKos = "pve.enemy_kos";

        public static readonly string[] AllPve =
        {
            PveTotalVictories,
            PveXpEarnedLifetime,
            PveProgressionMovesGranted,
            PveDamageDealt,
            PveDamageTaken,
            PveDamageMitigated,
            PveHealingTotal,
            PveEnemyKos,
        };

        public static bool IsPveMetric(string metricKey)
        {
            if (string.IsNullOrWhiteSpace(metricKey))
                return false;

            for (int i = 0; i < AllPve.Length; i++)
            {
                if (string.Equals(AllPve[i], metricKey, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
