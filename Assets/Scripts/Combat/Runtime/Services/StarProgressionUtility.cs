using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public static class StarProgressionUtility
    {
        public static int GetXpForStar(int starIndex, int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);
            if (starIndex < 0)
                return 0;

            if (starIndex >= config.starXpThresholds.Length)
                return ScaleXp(config.masteryBaseXp, rebirthCount, config);

            return ScaleXp(config.starXpThresholds[starIndex], rebirthCount, config);
        }

        public static int GetTotalXpForMaxStar(int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);

            int total = 0;
            for (int i = 0; i < config.starXpThresholds.Length; i++)
                total += GetXpForStar(i, rebirthCount, config);

            return total;
        }

        public static int GetStarCount(int totalXp, int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);

            int remainingXp = Mathf.Max(0, totalXp);
            int stars = 0;

            for (int i = 0; i < config.starXpThresholds.Length; i++)
            {
                int required = GetXpForStar(i, rebirthCount, config);
                if (remainingXp < required)
                    break;

                remainingXp -= required;
                stars++;
            }

            return stars;
        }

        public static float GetMilestoneFill01(int totalXp, int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);
            int clampedXp = Mathf.Max(0, totalXp);
            int currentStars = GetStarCount(clampedXp, rebirthCount, config);
            int required = GetXpForStar(currentStars, rebirthCount, config);
            if (required <= 0)
                return 1f;

            if (currentStars < config.starXpThresholds.Length)
            {
                int spent = 0;
                for (int i = 0; i < currentStars; i++)
                    spent += GetXpForStar(i, rebirthCount, config);
                return Mathf.Clamp01((clampedXp - spent) / (float)required);
            }

            int overflowXp = Mathf.Max(0, clampedXp - GetTotalXpForMaxStar(rebirthCount, config));
            int intoCycle = overflowXp % required;
            return Mathf.Clamp01(intoCycle / (float)required);
        }

        public static void GetMilestoneSegment(
            int totalXp,
            int rebirthCount,
            ProgressionConfigSO config,
            out int earned,
            out int required,
            out float fill01)
        {
            config = ResolveConfig(config);
            fill01 = GetMilestoneFill01(totalXp, rebirthCount, config);
            int stars = GetStarCount(totalXp, rebirthCount, config);
            required = GetXpForStar(stars, rebirthCount, config);
            if (required <= 0)
            {
                earned = Mathf.Max(0, totalXp);
                required = 0;
                fill01 = 1f;
                return;
            }

            earned = Mathf.Clamp(Mathf.RoundToInt(fill01 * required), 0, required);
        }

        public static int GetXpToNextMilestone(int totalXp, int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);
            int clampedXp = Mathf.Max(0, totalXp);
            int maxStarXp = GetTotalXpForMaxStar(rebirthCount, config);
            int currentStars = GetStarCount(clampedXp, rebirthCount, config);

            if (currentStars < config.starXpThresholds.Length)
            {
                int requiredTotal = 0;
                for (int i = 0; i <= currentStars; i++)
                    requiredTotal += GetXpForStar(i, rebirthCount, config);

                return Mathf.Max(0, requiredTotal - clampedXp);
            }

            int masteryXp = GetXpForStar(config.starXpThresholds.Length, rebirthCount, config);
            if (masteryXp <= 0)
                return 0;

            int overflowXp = Mathf.Max(0, clampedXp - maxStarXp);
            int progressIntoCycle = overflowXp % masteryXp;
            return progressIntoCycle == 0 ? masteryXp : masteryXp - progressIntoCycle;
        }

        public static float GetRebirthMultiplier(int rebirthCount, ProgressionConfigSO config)
        {
            config = ResolveConfig(config);
            return Mathf.Pow(config.rebirthXpMultiplierBase, Mathf.Max(0, rebirthCount));
        }

        public static List<int> DetectStarUps(int currentXp, int deltaXp, int rebirthCount, ProgressionConfigSO config)
        {
            var starUps = new List<int>();
            if (deltaXp <= 0)
                return starUps;

            int previousStars = GetStarCount(currentXp, rebirthCount, config);
            int newStars = GetStarCount(currentXp + deltaXp, rebirthCount, config);

            for (int star = previousStars + 1; star <= newStars; star++)
                starUps.Add(star);

            return starUps;
        }

        public static List<int> DetectMasteryCycles(int currentXp, int deltaXp, int rebirthCount, ProgressionConfigSO config)
        {
            var cycles = new List<int>();
            if (deltaXp <= 0)
                return cycles;

            config = ResolveConfig(config);
            int maxStarXp = GetTotalXpForMaxStar(rebirthCount, config);
            int masteryXp = GetXpForStar(config.starXpThresholds.Length, rebirthCount, config);
            if (masteryXp <= 0)
                return cycles;

            int previousOverflow = Mathf.Max(0, currentXp - maxStarXp);
            int newOverflow = Mathf.Max(0, currentXp + deltaXp - maxStarXp);
            int previousCycles = previousOverflow / masteryXp;
            int newCycles = newOverflow / masteryXp;

            for (int cycle = previousCycles + 1; cycle <= newCycles; cycle++)
                cycles.Add(cycle);

            return cycles;
        }

        private static int ScaleXp(int baseXp, int rebirthCount, ProgressionConfigSO config)
        {
            float multiplier = GetRebirthMultiplier(rebirthCount, config);
            return Mathf.Max(1, Mathf.RoundToInt(baseXp * multiplier));
        }

        private static ProgressionConfigSO ResolveConfig(ProgressionConfigSO config)
        {
            return config != null ? config : BalanceConfigRuntime.Progression;
        }
    }
}
