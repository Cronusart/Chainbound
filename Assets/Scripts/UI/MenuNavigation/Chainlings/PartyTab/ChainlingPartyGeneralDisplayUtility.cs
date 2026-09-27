using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyGeneralDisplayUtility
    {
        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            summary.xpBarFill01 = 0f;
            summary.xpLabel = "";
            summary.hasXpSegment = false;
            summary.xpSegmentEarned = 0;
            summary.xpSegmentSize = 0;
            summary.starCount = 0;
            if (!ctx.HasFilledChainling)
                return;

            ChainlingSnapshot snapshot = ctx.entry.snapshot;
            int stars = ChainlingProgressionUtility.NormalizeStars(snapshot.stars);
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            int masteryXp = Mathf.Max(0, ctx.entry.masteryXp);
            ProgressionConfigSO config = ctx.progressionConfig;
            StarProgressionUtility.GetMilestoneSegment(
                masteryXp,
                rebirth,
                config,
                out int segmentEarned,
                out int segmentSize,
                out float fill01);
            HashSet<string> owned = ChainlingProgressionOwnedMovesUtility.CollectFromSnapshotAndInventory(
                snapshot,
                ctx.moveInventory);
            Dictionary<string, int> ownedCounts = ChainlingProgressionOwnedMovesUtility.CountFromSnapshotAndInventory(
                snapshot,
                ctx.moveInventory);

            MoveLearnSummaryPreview learnSummary = MoveLearnPreviewUtility.BuildSummaryPreview(
                ctx.species,
                stars,
                ChainlingProgressionIdentityUtility.IsEntangled(snapshot),
                ctx.progressionRewardTables,
                ctx.progressionEventCatalog,
                owned,
                ownedCounts);

            summary.xpLabel = segmentSize > 0
                ? $"{segmentEarned} / {segmentSize} XP"
                : $"{masteryXp} XP";
            summary.hasXpSegment = segmentSize > 0;
            summary.xpSegmentEarned = segmentEarned;
            summary.xpSegmentSize = segmentSize;
            summary.lines.Add(new ChainlingPartyDisplayLine(
                "Type",
                summary.typeLabel));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                "Variant",
                summary.variantLabel));
            AppendMoveLearnOddsLines(summary.lines, learnSummary);
            summary.xpBarFill01 = fill01;
            summary.starCount = stars;
            summary.supportsDrillDown = true;
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.MoveLearnBreakdown;
            drillDown.moveLearnRows.Clear();
            if (!ctx.HasFilledChainling || ctx.species == null)
                return;

            HashSet<string> owned = ChainlingProgressionOwnedMovesUtility.CollectFromSnapshotAndInventory(
                ctx.entry.snapshot,
                ctx.moveInventory);
            Dictionary<string, int> ownedCounts = ChainlingProgressionOwnedMovesUtility.CountFromSnapshotAndInventory(
                ctx.entry.snapshot,
                ctx.moveInventory);
            int stars = ChainlingProgressionUtility.NormalizeStars(ctx.entry.snapshot.stars);
            List<MoveLearnPreviewRow> previewRows = MoveLearnPreviewUtility.BuildPreviewRows(
                ctx.species,
                stars,
                ChainlingProgressionIdentityUtility.IsEntangled(ctx.entry.snapshot),
                ctx.progressionRewardTables,
                ctx.progressionEventCatalog,
                owned,
                ownedCounts);

            if (previewRows.Count > 0)
            {
                for (int i = 0; i < previewRows.Count; i++)
                    AppendPreviewRow(drillDown, previewRows[i]);
                return;
            }

            AppendUnconfiguredFallbackRows(drillDown, ctx.species.learnableMoves, owned, ownedCounts, false);
            AppendUnconfiguredFallbackRows(drillDown, ctx.species.exclusiveMoves, owned, ownedCounts, true);
        }

        private static void AppendMoveLearnOddsLines(
            List<ChainlingPartyDisplayLine> lines,
            MoveLearnSummaryPreview learnSummary)
        {
            string gateLabel = learnSummary != null && !string.IsNullOrWhiteSpace(learnSummary.gateLabel)
                ? learnSummary.gateLabel
                : "Next-star folio";

            if (learnSummary == null || !learnSummary.poolConfigured)
            {
                lines.Add(new ChainlingPartyDisplayLine(
                    gateLabel + " chance",
                    MoveLearnPreviewUtility.FormatSummaryGateLine(learnSummary),
                    true));
                return;
            }

            lines.Add(new ChainlingPartyDisplayLine(
                learnSummary.isMastery ? "Five-star mastery folio chance" : "Next-star folio chance",
                $"{learnSummary.gatePercent}%",
                true));
        }

        private static void AppendPreviewRow(ChainlingPartyColumnDrillDown drillDown, MoveLearnPreviewRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.moveId))
                return;

            drillDown.moveLearnRows.Add(new ChainlingPartyMoveLearnRow
            {
                moveId = row.moveId,
                moveName = row.moveName,
                typeLabel = row.typeLabel,
                rarityLabel = row.rarityLabel,
                learnPercentLabel = FormatChanceAndOwnership(row.learnPercent, row.ownedCopyCount),
                learned = row.learned,
                ownedCopyCount = row.ownedCopyCount,
                isExclusive = row.poolTag == ProgressionPoolTag.Exclusive,
            });
        }

        private static void AppendUnconfiguredFallbackRows(
            ChainlingPartyColumnDrillDown drillDown,
            IReadOnlyList<MoveDefinitionSO> moves,
            HashSet<string> learned,
            IReadOnlyDictionary<string, int> ownedCounts,
            bool exclusive)
        {
            if (moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinitionSO move = moves[i];
                if (move == null || string.IsNullOrWhiteSpace(move.moveId))
                    continue;

                bool isLearned = learned.Contains(move.moveId);
                int ownedCopyCount = ownedCounts != null && ownedCounts.TryGetValue(move.moveId, out int count)
                    ? count
                    : isLearned ? 1 : 0;
                drillDown.moveLearnRows.Add(new ChainlingPartyMoveLearnRow
                {
                    moveId = move.moveId,
                    moveName = string.IsNullOrWhiteSpace(move.displayName) ? move.moveId : move.displayName,
                    typeLabel = MoveSelectDisplayUtility.FormatTypeName(move.baseType),
                    rarityLabel = move.rarity.ToString(),
                    learnPercentLabel = FormatChanceAndOwnership(-1f, ownedCopyCount),
                    learned = isLearned,
                    ownedCopyCount = ownedCopyCount,
                    isExclusive = exclusive,
                });
            }
        }

        private static string FormatChanceAndOwnership(float percent, int ownedCopyCount)
        {
            string chance = percent < 0f
                ? MoveLearnPreviewUtility.UnconfiguredPoolLabel
                : MoveLearnPreviewUtility.FormatPercent(percent);
            return ownedCopyCount > 0 ? $"{chance} · Owned ×{ownedCopyCount}" : chance;
        }
    }
}
