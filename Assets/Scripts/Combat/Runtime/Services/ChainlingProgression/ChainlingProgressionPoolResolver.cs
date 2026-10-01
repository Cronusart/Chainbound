using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class ResolvedProgressionMoveEntry
    {
        public MoveDefinitionSO move;
        public string moveId;
        public string displayName;
        public ProgressionPoolTag poolTag;
        public int baseWeight;
        public int effectiveWeight;
        public string sourceEventId;
        public bool isEventMove;
    }

    public sealed class ChainlingProgressionResolvedPool
    {
        public string speciesId;
        public int starIndex;
        public int starGateBonusPercent;
        public List<ResolvedProgressionMoveEntry> entries = new List<ResolvedProgressionMoveEntry>();

        public int TotalWeight
        {
            get
            {
                int total = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    ResolvedProgressionMoveEntry entry = entries[i];
                    if (entry != null)
                        total += Mathf.Max(0, entry.effectiveWeight);
                }

                return total;
            }
        }
    }

    public static class ChainlingProgressionPoolResolver
    {
        public static ChainlingProgressionResolvedPool Resolve(
            SpeciesDefinitionSO species,
            int starIndex,
            bool isEntangled,
            IReadOnlyCollection<string> ownedMoveIds,
            ChainlingProgressionEventCatalogSO eventCatalog,
            ProgressionConfigSO progressionConfig = null)
        {
            var result = new ChainlingProgressionResolvedPool
            {
                speciesId = species != null ? species.speciesId : "",
                starIndex = Mathf.Clamp(starIndex, 1, CombatConstants.STAR_MAX),
            };

            if (species == null)
                return result;

            ProgressionConfigSO resolvedProgression = progressionConfig != null
                ? progressionConfig
                : BalanceConfigRuntime.Progression;
            var owned = BuildOwnedSet(ownedMoveIds);
            var map = new Dictionary<string, ResolvedProgressionMoveEntry>(StringComparer.Ordinal);
            var order = new List<ResolvedProgressionMoveEntry>();

            AddEntries(species.progressionMovePool, result.starIndex, owned, "", false, resolvedProgression, map, order);
            ApplyEventPatches(species.speciesId, result.starIndex, owned, eventCatalog, resolvedProgression, result, map, order);

            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] != null && order[i].effectiveWeight > 0)
                    result.entries.Add(order[i]);
            }

            return result;
        }

        public static ChainlingProgressionResolvedPool Resolve(
            SpeciesDatabaseSO speciesDatabase,
            string speciesId,
            int starIndex,
            bool isEntangled,
            IReadOnlyCollection<string> ownedMoveIds,
            ChainlingProgressionEventCatalogSO eventCatalog,
            ProgressionConfigSO progressionConfig = null)
        {
            SpeciesDefinitionSO species = speciesDatabase != null ? speciesDatabase.GetById(speciesId) : null;
            return Resolve(species, starIndex, isEntangled, ownedMoveIds, eventCatalog, progressionConfig);
        }

        private static void ApplyEventPatches(
            string speciesId,
            int starIndex,
            HashSet<string> ownedMoveIds,
            ChainlingProgressionEventCatalogSO eventCatalog,
            ProgressionConfigSO progressionConfig,
            ChainlingProgressionResolvedPool result,
            Dictionary<string, ResolvedProgressionMoveEntry> map,
            List<ResolvedProgressionMoveEntry> order)
        {
            IReadOnlyList<ChainlingProgressionEventSO> events = eventCatalog != null ? eventCatalog.Events : null;
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                ChainlingProgressionEventSO progressionEvent = events[i];
                if (progressionEvent == null || !progressionEvent.enabled || progressionEvent.patches == null)
                    continue;

                for (int j = 0; j < progressionEvent.patches.Count; j++)
                {
                    SpeciesProgressionEventPatch patch = progressionEvent.patches[j];
                    if (patch == null || !PatchAppliesToSpecies(patch, speciesId))
                        continue;

                    result.starGateBonusPercent += patch.starGateBonusPercent;
                    ApplyWeightMultipliers(patch.weightMultipliers, map);
                    AddEntries(patch.addedMoves, starIndex, ownedMoveIds, progressionEvent.eventId, true, progressionConfig, map, order);
                }
            }
        }

        private static void AddEntries(
            IReadOnlyList<SpeciesProgressionMoveEntry> entries,
            int starIndex,
            HashSet<string> ownedMoveIds,
            string sourceEventId,
            bool isEventMove,
            ProgressionConfigSO progressionConfig,
            Dictionary<string, ResolvedProgressionMoveEntry> map,
            List<ResolvedProgressionMoveEntry> order)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                SpeciesProgressionMoveEntry source = entries[i];
                if (!IsEligible(source, starIndex, ownedMoveIds))
                    continue;

                string moveId = source.move.moveId.Trim();
                float rarityWeight = progressionConfig != null
                    ? progressionConfig.GetMoveLearnRarityWeight(source.move.rarity)
                    : 1f;
                int authoredWeight = source.weight;
                if (starIndex >= CombatConstants.STAR_MAX && source.useWeightAtStarFive)
                    authoredWeight = source.weightAtStarFive;
                int weight = Mathf.Max(0, Mathf.RoundToInt(authoredWeight * rarityWeight));
                if (weight <= 0)
                    continue;

                if (!map.TryGetValue(moveId, out ResolvedProgressionMoveEntry resolved))
                {
                    resolved = new ResolvedProgressionMoveEntry
                    {
                        move = source.move,
                        moveId = moveId,
                        displayName = string.IsNullOrWhiteSpace(source.move.displayName) ? moveId : source.move.displayName,
                        poolTag = source.poolTag,
                        baseWeight = weight,
                        effectiveWeight = weight,
                        sourceEventId = sourceEventId ?? "",
                        isEventMove = isEventMove,
                    };
                    map[moveId] = resolved;
                    order.Add(resolved);
                }
                else
                {
                    resolved.baseWeight += weight;
                    resolved.effectiveWeight += weight;
                    if (isEventMove)
                    {
                        resolved.isEventMove = true;
                        resolved.sourceEventId = sourceEventId ?? "";
                    }
                }
            }
        }

        private static void ApplyWeightMultipliers(
            IReadOnlyList<WeightMultiplierPatch> multipliers,
            Dictionary<string, ResolvedProgressionMoveEntry> map)
        {
            if (multipliers == null || map == null)
                return;

            for (int i = 0; i < multipliers.Count; i++)
            {
                WeightMultiplierPatch patch = multipliers[i];
                string moveId = patch != null && patch.moveId != null ? patch.moveId.Trim() : "";
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                if (map.TryGetValue(moveId, out ResolvedProgressionMoveEntry entry))
                    entry.effectiveWeight = Mathf.Max(0, Mathf.RoundToInt(entry.effectiveWeight * Mathf.Max(0f, patch.multiplier)));
            }
        }

        private static bool IsEligible(
            SpeciesProgressionMoveEntry entry,
            int starIndex,
            HashSet<string> ownedMoveIds)
        {
            if (entry == null || entry.move == null || string.IsNullOrWhiteSpace(entry.move.moveId))
                return false;

            int minStar = Mathf.Clamp(entry.minStar, 0, CombatConstants.STAR_MAX);
            int maxStar = entry.maxStar <= 0
                ? CombatConstants.STAR_MAX
                : Mathf.Clamp(entry.maxStar, 0, CombatConstants.STAR_MAX);

            if (minStar > 0 && starIndex < minStar)
                return false;
            if (maxStar > 0 && starIndex > maxStar)
                return false;

            return !entry.excludeIfOwned || ownedMoveIds == null || !ownedMoveIds.Contains(entry.move.moveId);
        }

        private static bool PatchAppliesToSpecies(SpeciesProgressionEventPatch patch, string speciesId)
        {
            if (patch == null)
                return false;

            if (string.IsNullOrWhiteSpace(patch.speciesId))
                return true;

            return string.Equals(patch.speciesId.Trim(), speciesId ?? "", StringComparison.Ordinal);
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
    }
}
