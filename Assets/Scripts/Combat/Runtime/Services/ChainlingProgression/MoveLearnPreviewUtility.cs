using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class MoveLearnPreviewRow
    {
        public string moveId;
        public string moveName;
        public string typeLabel;
        public string rarityLabel;
        public ProgressionPoolTag poolTag;
        public float learnPercent;
        public bool learned;
        public int ownedCopyCount;
        public bool isEventMove;
    }

    public sealed class MoveLearnSummaryPreview
    {
        public string gateLabel;
        public int gatePercent;
        public bool isMastery;
        public bool poolConfigured;
        public string topMoveName;
        public float topMovePercent;
        public bool hasTopMove;
        public string xpMilestoneSuffix;
    }

    public static class MoveLearnPreviewUtility
    {
        public const string UnconfiguredPoolLabel = "—";

        public static int GetNextStarGatePercent(
            SpeciesDefinitionSO species,
            int currentStars,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null,
            IReadOnlyDictionary<string, int> ownedMoveCounts = null)
        {
            int nextStar = ResolveNextStar(currentStars);
            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                nextStar,
                isEntangled,
                ownedMoveIds,
                eventCatalog);

            return ChainlingProgressionRollUtility.GetStarGatePercent(
                nextStar,
                isEntangled,
                tables,
                pool.starGateBonusPercent);
        }

        public static int GetMasteryGatePercent(
            SpeciesDefinitionSO species,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null)
        {
            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                CombatConstants.STAR_MAX,
                isEntangled,
                ownedMoveIds,
                eventCatalog);

            return ChainlingProgressionRollUtility.GetMasteryGatePercent(
                isEntangled,
                tables,
                pool.starGateBonusPercent);
        }

        public static float GetMoveLearnPercent(
            SpeciesDefinitionSO species,
            int currentStars,
            string moveId,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null)
        {
            if (species == null || string.IsNullOrWhiteSpace(moveId))
                return 0f;

            ProgressionGrantEventType eventType = ResolvePreviewEventType(currentStars);
            int starIndex = ResolvePoolStarIndex(currentStars, eventType);
            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                ownedMoveIds,
                eventCatalog);

            int totalWeight = pool.TotalWeight;
            if (totalWeight <= 0)
                return 0f;

            ResolvedProgressionMoveEntry entry = FindEntry(pool, moveId);
            if (entry == null || entry.effectiveWeight <= 0)
                return 0f;

            int gatePercent = ResolveGatePercent(
                eventType,
                starIndex,
                isEntangled,
                tables,
                pool.starGateBonusPercent);

            return gatePercent * (entry.effectiveWeight / (float)totalWeight);
        }

        public static MoveLearnSummaryPreview BuildSummaryPreview(
            SpeciesDefinitionSO species,
            int currentStars,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null,
            IReadOnlyDictionary<string, int> ownedMoveCounts = null)
        {
            int stars = ChainlingProgressionUtility.NormalizeStars(currentStars);
            ProgressionGrantEventType eventType = ResolvePreviewEventType(stars);
            bool isMastery = eventType == ProgressionGrantEventType.MasteryCycle;
            int starIndex = ResolvePoolStarIndex(stars, eventType);
            bool poolConfigured = HasConfiguredPool(species, starIndex, eventCatalog);

            ChainlingProgressionResolvedPool eligiblePool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                ownedMoveIds,
                eventCatalog);

            int gatePercent = poolConfigured
                ? ResolveGatePercent(eventType, starIndex, isEntangled, tables, eligiblePool.starGateBonusPercent)
                : 0;

            var summary = new MoveLearnSummaryPreview
            {
                gateLabel = isMastery ? "Mastery" : "Next star",
                gatePercent = gatePercent,
                isMastery = isMastery,
                poolConfigured = poolConfigured,
                xpMilestoneSuffix = isMastery ? "to mastery" : "to next star",
            };

            List<MoveLearnPreviewRow> rows = BuildPreviewRows(
                species,
                stars,
                isEntangled,
                tables,
                eventCatalog,
                ownedMoveIds,
                ownedMoveCounts);

            for (int i = 0; i < rows.Count; i++)
            {
                MoveLearnPreviewRow row = rows[i];
                if (row == null || row.learnPercent <= 0f)
                    continue;

                summary.hasTopMove = true;
                summary.topMoveName = row.moveName;
                summary.topMovePercent = row.learnPercent;
                break;
            }

            return summary;
        }

        public static string FormatSummaryGateLine(MoveLearnSummaryPreview summary)
        {
            if (summary == null)
                return $"{UnconfiguredPoolLabel}";

            if (!summary.poolConfigured)
                return "Pool not configured";

            return $"{summary.gateLabel}: {summary.gatePercent}%";
        }

        public static List<MoveLearnPreviewRow> BuildPreviewRows(
            SpeciesDefinitionSO species,
            int currentStars,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null,
            IReadOnlyDictionary<string, int> ownedMoveCounts = null)
        {
            ProgressionGrantEventType eventType = ResolvePreviewEventType(currentStars);
            return BuildPreviewRows(
                species,
                currentStars,
                isEntangled,
                eventType,
                tables,
                eventCatalog,
                ownedMoveIds,
                ownedMoveCounts);
        }

        public static List<MoveLearnPreviewRow> BuildPreviewRows(
            SpeciesDefinitionSO species,
            int currentStars,
            bool isEntangled,
            ProgressionGrantEventType eventType,
            ChainlingProgressionRewardTablesSO tables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IReadOnlyCollection<string> ownedMoveIds = null,
            IReadOnlyDictionary<string, int> ownedMoveCounts = null)
        {
            var rows = new List<MoveLearnPreviewRow>();
            if (species == null)
                return rows;

            int starIndex = ResolvePoolStarIndex(currentStars, eventType);
            HashSet<string> owned = BuildOwnedSet(ownedMoveIds);
            ChainlingProgressionResolvedPool eligiblePool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                owned,
                eventCatalog);
            ChainlingProgressionResolvedPool displayPool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                null,
                eventCatalog);

            int gatePercent = ResolveGatePercent(
                eventType,
                starIndex,
                isEntangled,
                tables,
                eligiblePool.starGateBonusPercent);
            int eligibleWeight = eligiblePool.TotalWeight;

            for (int i = 0; i < displayPool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry display = displayPool.entries[i];
                if (display == null || string.IsNullOrWhiteSpace(display.moveId))
                    continue;

                ResolvedProgressionMoveEntry eligible = FindEntry(eligiblePool, display.moveId);
                int ownedCopies = ResolveOwnedCopyCount(display.moveId, owned, ownedMoveCounts);
                bool learned = ownedCopies > 0;
                float percent = eligible != null && eligibleWeight > 0
                    ? gatePercent * (eligible.effectiveWeight / (float)eligibleWeight)
                    : 0f;

                rows.Add(new MoveLearnPreviewRow
                {
                    moveId = display.moveId,
                    moveName = display.displayName,
                    typeLabel = display.move != null ? MoveSelectDisplayUtility.FormatTypeName(display.move.baseType) : "",
                    rarityLabel = display.move != null ? display.move.rarity.ToString() : "",
                    poolTag = display.poolTag,
                    learnPercent = percent,
                    learned = learned,
                    ownedCopyCount = ownedCopies,
                    isEventMove = display.isEventMove,
                });
            }

            rows.Sort(CompareRows);
            return rows;
        }

        public static bool HasConfiguredPool(
            SpeciesDefinitionSO species,
            int starIndex,
            ChainlingProgressionEventCatalogSO eventCatalog)
        {
            if (species == null)
                return false;

            ChainlingProgressionResolvedPool displayPool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                false,
                null,
                eventCatalog);

            return displayPool.entries != null && displayPool.entries.Count > 0;
        }

        public static ProgressionGrantEventType ResolvePreviewEventType(int currentStars)
        {
            return ChainlingProgressionUtility.NormalizeStars(currentStars) >= CombatConstants.STAR_MAX
                ? ProgressionGrantEventType.MasteryCycle
                : ProgressionGrantEventType.StarUp;
        }

        public static ChainlingProgressionResolvedPool ResolvePreviewPool(
            SpeciesDefinitionSO species,
            int currentStars,
            bool isEntangled,
            ChainlingProgressionEventCatalogSO eventCatalog)
        {
            int stars = ChainlingProgressionUtility.NormalizeStars(currentStars);
            int starIndex = ResolvePoolStarIndex(stars, ResolvePreviewEventType(stars));
            return ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                null,
                eventCatalog);
        }

        public static string FormatPercent(float percent)
        {
            if (percent <= 0f)
                return "0%";
            if (percent < 1f)
                return "<1%";

            return $"{Mathf.RoundToInt(percent)}%";
        }

        private static int ResolveGatePercent(
            ProgressionGrantEventType eventType,
            int starIndex,
            bool isEntangled,
            ChainlingProgressionRewardTablesSO tables,
            int bonusPercent)
        {
            return eventType == ProgressionGrantEventType.MasteryCycle
                ? ChainlingProgressionRollUtility.GetMasteryGatePercent(isEntangled, tables, bonusPercent)
                : ChainlingProgressionRollUtility.GetStarGatePercent(starIndex, isEntangled, tables, bonusPercent);
        }

        private static int ResolvePoolStarIndex(int currentStars, ProgressionGrantEventType eventType)
        {
            if (eventType == ProgressionGrantEventType.MasteryCycle)
                return CombatConstants.STAR_MAX;

            return ResolveNextStar(currentStars);
        }

        private static int ResolveNextStar(int currentStars)
        {
            return Mathf.Clamp(ChainlingProgressionUtility.NormalizeStars(currentStars) + 1, 1, CombatConstants.STAR_MAX);
        }

        private static ResolvedProgressionMoveEntry FindEntry(ChainlingProgressionResolvedPool pool, string moveId)
        {
            if (pool?.entries == null || string.IsNullOrWhiteSpace(moveId))
                return null;

            string normalized = moveId.Trim();
            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry != null && string.Equals(entry.moveId, normalized, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static HashSet<string> BuildOwnedSet(IReadOnlyCollection<string> ownedMoveIds)
        {
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (ownedMoveIds == null)
                return owned;

            foreach (string moveId in ownedMoveIds)
            {
                if (!string.IsNullOrWhiteSpace(moveId))
                    owned.Add(moveId.Trim());
            }

            return owned;
        }

        private static int ResolveOwnedCopyCount(
            string moveId,
            HashSet<string> owned,
            IReadOnlyDictionary<string, int> ownedMoveCounts)
        {
            string normalized = moveId != null ? moveId.Trim() : "";
            if (ownedMoveCounts != null &&
                ownedMoveCounts.TryGetValue(normalized, out int count))
            {
                return Mathf.Max(0, count);
            }

            return owned != null && owned.Contains(normalized) ? 1 : 0;
        }

        private static int CompareRows(MoveLearnPreviewRow a, MoveLearnPreviewRow b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int percentCompare = b.learnPercent.CompareTo(a.learnPercent);
            if (percentCompare != 0)
                return percentCompare;

            return string.CompareOrdinal(a.moveName, b.moveName);
        }
    }
}
