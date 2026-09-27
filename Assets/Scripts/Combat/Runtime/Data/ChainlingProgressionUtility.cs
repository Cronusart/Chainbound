using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Star + Cycle (rebirth) progression helpers.
    /// Combat power uses additive Cycle flats; MaxHP Cycle uses percent of species base.
    /// </summary>
    public static class ChainlingProgressionUtility
    {
        public static int NormalizeStars(int stars)
        {
            return Clamp(stars, 0, CombatConstants.STAR_MAX);
        }

        public static int NormalizeRebirthCount(int rebirthCount)
        {
            return Clamp(rebirthCount, 0, CombatConstants.REBIRTH_MAX);
        }

        /// <summary>
        /// Legacy API: always returns 1. Combat uses <see cref="ComputeProgressedStat"/> (Cycle flats).
        /// </summary>
        public static float GetStatScaleMultiplier(int stars, int rebirthCount)
        {
            return 1f;
        }

        /// <summary>
        /// Auto Cycle bonus from rebirth_count: flat*n + round(speciesBase * percent * n) per board.
        /// </summary>
        public static int GetCycleAutoFlat(
            CoreStat stat,
            int rebirthCount,
            int speciesBase = 0,
            ProgressionConfigSO config = null)
        {
            int n = NormalizeRebirthCount(rebirthCount);
            if (n <= 0)
                return 0;

            config = config != null ? config : BalanceConfigRuntime.Progression;

            if (stat == CoreStat.MaxHP)
            {
                return (config.cycleAutoHpFlat * n)
                    + (int)Math.Round(speciesBase * config.cycleAutoHpPercent * n);
            }

            return (config.cycleAutoStatFlat * n)
                + (int)Math.Round(speciesBase * config.cycleAutoStatPercent * n);
        }

        /// <summary>
        /// Player Cycle pick bonuses from allocation counts in rebirth_iv_bonuses.
        /// flat*points + round(speciesBase * percent * points) per board.
        /// </summary>
        public static int GetCyclePickFlat(
            CoreStat stat,
            int[] rebirthIvBonuses,
            int speciesBase = 0,
            ProgressionConfigSO config = null)
        {
            int index = (int)stat;
            int points = GetBonusPoints(rebirthIvBonuses, index);
            if (points <= 0)
                return 0;

            config = config != null ? config : BalanceConfigRuntime.Progression;

            if (stat == CoreStat.MaxHP)
            {
                return (config.cyclePickHpFlat * points)
                    + (int)Math.Round(speciesBase * config.cyclePickHpPercent * points);
            }

            return (config.cyclePickStatFlat * points)
                + (int)Math.Round(speciesBase * config.cyclePickStatPercent * points);
        }

        /// <summary>
        /// Pre-stage combat total: base + baseIV + Cycle auto + Cycle pick.
        /// </summary>
        public static int ComputeProgressedStat(
            int speciesBase,
            int baseIv,
            CoreStat stat,
            int rebirthCount,
            int[] rebirthIvBonuses,
            ProgressionConfigSO config = null)
        {
            int autoFlat = GetCycleAutoFlat(stat, rebirthCount, speciesBase, config);
            int pickFlat = GetCyclePickFlat(stat, rebirthIvBonuses, speciesBase, config);
            return Math.Max(1, speciesBase + baseIv + autoFlat + pickFlat);
        }

        /// <summary>Copy or zero-fill a 6-length allocation array.</summary>
        public static int[] NormalizeRebirthIvBonuses(int[] rebirthIvBonuses)
        {
            var result = new int[CombatConstants.STAT_COUNT];
            if (rebirthIvBonuses == null)
                return result;

            int copy = Math.Min(rebirthIvBonuses.Length, CombatConstants.STAT_COUNT);
            for (int i = 0; i < copy; i++)
                result[i] = Math.Max(0, rebirthIvBonuses[i]);

            return result;
        }

        /// <summary>
        /// Initiative tie-break score after speed: higher stars, then higher rebirth.
        /// </summary>
        public static int GetProgressionTieBreakScore(int stars, int rebirthCount)
        {
            return (NormalizeStars(stars) * 100) + NormalizeRebirthCount(rebirthCount);
        }

        /// <summary>
        /// Resolve stars/rebirth from a snapshot. Migrates legacy snapshot.level to stars once when stars are unset.
        /// </summary>
        public static void ResolveSnapshotProgression(
            int snapshotStars,
            int snapshotRebirthCount,
            int legacySnapshotLevel,
            out int stars,
            out int rebirthCount)
        {
            stars = NormalizeStars(snapshotStars);
            rebirthCount = NormalizeRebirthCount(snapshotRebirthCount);

            if (stars == 0 && legacySnapshotLevel > 0)
                stars = ApproximateStarsFromLegacyLevel(legacySnapshotLevel);
        }

        /// <summary>
        /// Maps a deprecated legacy level (1–100) to an approximate star count for one-time import paths.
        /// </summary>
        public static int ApproximateStarsFromLegacyLevel(int legacyLevel)
        {
            int clampedLevel = Clamp(legacyLevel > 0 ? legacyLevel : 50, 1, 100);
            return NormalizeStars((int)Math.Round(clampedLevel / 20f));
        }

        public static int ApproximateStarVarianceFromLegacyVariance(int legacyVariance)
        {
            if (legacyVariance <= 0)
                return 0;

            if (legacyVariance <= 10)
                return 1;

            return 2;
        }

        private static int GetBonusPoints(int[] rebirthIvBonuses, int index)
        {
            if (rebirthIvBonuses == null || index < 0 || index >= rebirthIvBonuses.Length)
                return 0;

            return Math.Max(0, rebirthIvBonuses[index]);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
