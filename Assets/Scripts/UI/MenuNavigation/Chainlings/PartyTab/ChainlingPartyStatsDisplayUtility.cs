using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyStatsDisplayUtility
    {
        public const string VictoriesLabel = "Victories";
        public const string EnemyKosLabel = "Enemy KOs";
        public const string DamageDealtLabel = "Damage dealt";
        public const string DamageTakenLabel = "Damage taken";
        public const string XpEarnedLabel = "XP earned";
        public const string TimesKodLabel = "Times KO'd";

        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            summary.topBadges.Clear();
            summary.damageDealt = 0;
            summary.damageTaken = 0;
            if (!ctx.HasFilledChainling)
                return;

            ChainlingPveServiceRecord record = ChainlingPveServiceRecord.CloneOrDefault(ctx.entry.pveServiceRecord);
            ChainlingBattleStats legacy = ctx.entry.battleStats;
            int timesKod = legacy != null ? legacy.timesKod : 0;

            summary.damageDealt = record.damageDealt;
            summary.damageTaken = record.damageTaken;

            summary.lines.Add(new ChainlingPartyDisplayLine(VictoriesLabel, FormatStatNumber(record.totalVictories)));
            summary.lines.Add(new ChainlingPartyDisplayLine(EnemyKosLabel, FormatStatNumber(record.enemyKos)));
            summary.lines.Add(new ChainlingPartyDisplayLine(DamageDealtLabel, FormatStatNumber(record.damageDealt)));
            summary.lines.Add(new ChainlingPartyDisplayLine(DamageTakenLabel, FormatStatNumber(record.damageTaken)));
            summary.lines.Add(new ChainlingPartyDisplayLine(XpEarnedLabel, FormatStatNumber(record.xpEarnedLifetime)));
            summary.lines.Add(new ChainlingPartyDisplayLine(TimesKodLabel, FormatStatNumber(timesKod)));

            summary.supportsDrillDown = true;
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.FullStatSheet;
            drillDown.statLines.Clear();
            drillDown.badges.Clear();
            if (!ctx.HasFilledChainling)
                return;

            ChainlingPveServiceRecord record = ChainlingPveServiceRecord.CloneOrDefault(ctx.entry.pveServiceRecord);
            ChainlingBattleStats legacy = ctx.entry.battleStats;

            drillDown.statLines.Add(new ChainlingPartyDisplayLine("PvE victories", FormatStatNumber(record.totalVictories)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine("Lifetime XP earned", FormatStatNumber(record.xpEarnedLifetime)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                "Progression move grants",
                FormatStatNumber(record.progressionMovesGranted)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine(DamageDealtLabel, FormatStatNumber(record.damageDealt)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine(DamageTakenLabel, FormatStatNumber(record.damageTaken)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                "Damage mitigated",
                FormatStatNumber(record.damageMitigated)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine("Healing total", FormatStatNumber(record.healingTotal)));
            drillDown.statLines.Add(new ChainlingPartyDisplayLine(EnemyKosLabel, FormatStatNumber(record.enemyKos)));

            if (legacy != null)
            {
                drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                    "Legacy wins",
                    FormatStatNumber(legacy.battlesWon),
                    true));
                drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                    "Legacy losses",
                    FormatStatNumber(legacy.battlesLost),
                    true));
                drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                    TimesKodLabel,
                    FormatStatNumber(legacy.timesKod),
                    true));
                drillDown.statLines.Add(new ChainlingPartyDisplayLine(
                    "Times captured",
                    FormatStatNumber(legacy.timesCaptured),
                    true));
            }

            drillDown.badges.AddRange(BuildBadgeDisplays(record, ctx.badgeCatalog));
        }

        public static string FormatStatNumber(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        public static List<ChainlingPartyBadgeDisplay> BuildBadgeDisplays(
            ChainlingPveServiceRecord record,
            ChainlingBadgeCatalogSO catalog)
        {
            var displays = new List<ChainlingPartyBadgeDisplay>();
            IReadOnlyList<ChainlingBadgeDefinitionSO> definitions = catalog != null ? catalog.Definitions : null;
            if (definitions == null || definitions.Count == 0)
                return displays;

            List<string> unlockedIds = ChainlingBadgeEvaluator.EvaluateUnlockedBadgeIds(record, definitions);
            var unlockedSet = new HashSet<string>(unlockedIds);

            for (int i = 0; i < definitions.Count; i++)
            {
                ChainlingBadgeDefinitionSO definition = definitions[i];
                if (definition == null || !definition.IsValid)
                    continue;

                long current = record.GetMetricValue(definition.metricKey);
                bool unlocked = unlockedSet.Contains(definition.badgeId);
                float progress = definition.threshold > 0
                    ? UnityEngine.Mathf.Clamp01(current / (float)definition.threshold)
                    : 0f;

                displays.Add(new ChainlingPartyBadgeDisplay(
                    definition.badgeId,
                    definition.displayName,
                    definition.description,
                    definition.icon,
                    unlocked,
                    progress,
                    current,
                    definition.threshold));
            }

            return displays;
        }

        public static List<ChainlingPartyBadgeDisplay> SelectTopBadges(
            ChainlingPveServiceRecord record,
            ChainlingBadgeCatalogSO catalog,
            int maxCount)
        {
            List<ChainlingPartyBadgeDisplay> all = BuildBadgeDisplays(record, catalog);
            if (all.Count == 0 || maxCount <= 0)
                return new List<ChainlingPartyBadgeDisplay>();

            List<ChainlingPartyBadgeDisplay> unlocked = all.Where(b => b.Unlocked)
                .OrderByDescending(b => b.Threshold)
                .ThenBy(b => b.DisplayName)
                .ToList();

            if (unlocked.Count >= maxCount)
                return unlocked.Take(maxCount).ToList();

            var result = new List<ChainlingPartyBadgeDisplay>(unlocked);
            List<ChainlingPartyBadgeDisplay> locked = all.Where(b => !b.Unlocked)
                .OrderByDescending(b => b.Progress01)
                .ThenBy(b => b.Threshold)
                .ToList();

            for (int i = 0; i < locked.Count && result.Count < maxCount; i++)
                result.Add(locked[i]);

            return result;
        }
    }
}
