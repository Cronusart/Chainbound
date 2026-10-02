using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds a wild battle kit without touching the capture Known roll.
    /// Slot 1 is one damaging move from the zone wild pool.
    /// Slot 2 is one typed damaging move from the species progression pool.
    /// Slot 3 is a 50% roll of one non-damaging progression move.
    /// </summary>
    public static class WildBattleKitComposer
    {
        public const int MaxKitSize = 4;

        public static List<string> Compose(
            IReadOnlyList<string> baselineKit,
            SpeciesDefinitionSO species,
            int stars,
            bool isEntangled,
            System.Random rng,
            MoveDatabaseSO moveDatabase,
            ProgressionConfigSO progressionConfig)
        {
            var kit = new List<string>(3);
            string baseline = PickBaseline(baselineKit, moveDatabase);
            if (!string.IsNullOrEmpty(baseline))
                kit.Add(baseline);

            if (species == null)
                return kit;

            if (rng == null)
                rng = new System.Random();

            int starIndex = Mathf.Clamp(Mathf.Max(1, stars), 1, CombatConstants.STAR_MAX);
            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                null,
                null,
                progressionConfig);

            string typed = RollMatching(pool, rng, IsTypedDamage, kit);
            if (!string.IsNullOrEmpty(typed) && kit.Count < MaxKitSize)
                kit.Add(typed);

            if (HasMatch(pool, IsPlaystyle, kit) && rng.Next(2) == 0)
            {
                string playstyle = RollMatching(pool, rng, IsPlaystyle, kit);
                if (!string.IsNullOrEmpty(playstyle) && kit.Count < MaxKitSize)
                    kit.Add(playstyle);
            }

            return kit;
        }

        private static string PickBaseline(IReadOnlyList<string> baselineKit, MoveDatabaseSO moveDatabase)
        {
            if (baselineKit == null)
                return "";

            string first = "";
            for (int i = 0; i < baselineKit.Count; i++)
            {
                string moveId = baselineKit[i] != null ? baselineKit[i].Trim() : "";
                if (string.IsNullOrEmpty(moveId))
                    continue;
                if (string.IsNullOrEmpty(first))
                    first = moveId;
                if (moveDatabase == null || IsDamaging(moveDatabase.GetById(moveId)))
                    return moveId;
            }

            return first;
        }

        private static bool IsTypedDamage(ResolvedProgressionMoveEntry entry)
        {
            MoveDefinitionSO move = entry != null ? entry.move : null;
            return move != null && move.baseType != ChainlingType.None && IsDamaging(move);
        }

        private static bool IsPlaystyle(ResolvedProgressionMoveEntry entry)
        {
            MoveDefinitionSO move = entry != null ? entry.move : null;
            return move != null && !IsDamaging(move);
        }

        private static bool IsDamaging(MoveDefinitionSO move)
        {
            if (move == null)
                return false;

            return move.category == MoveCategory.DamagingMelee
                   || move.category == MoveCategory.DamagingRanged
                   || move.power > 0;
        }

        private static bool HasMatch(
            ChainlingProgressionResolvedPool pool,
            Func<ResolvedProgressionMoveEntry, bool> match,
            List<string> already)
        {
            if (pool == null || pool.entries == null)
                return false;

            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry == null || entry.effectiveWeight <= 0 || !match(entry))
                    continue;
                if (!Contains(already, entry.moveId))
                    return true;
            }

            return false;
        }

        private static string RollMatching(
            ChainlingProgressionResolvedPool pool,
            System.Random rng,
            Func<ResolvedProgressionMoveEntry, bool> match,
            List<string> already)
        {
            if (pool == null || pool.entries == null)
                return "";

            int total = 0;
            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry == null || entry.effectiveWeight <= 0 || !match(entry) || Contains(already, entry.moveId))
                    continue;
                total += entry.effectiveWeight;
            }

            if (total <= 0)
                return "";

            int roll = rng.Next(total);
            int cumulative = 0;
            for (int i = 0; i < pool.entries.Count; i++)
            {
                ResolvedProgressionMoveEntry entry = pool.entries[i];
                if (entry == null || entry.effectiveWeight <= 0 || !match(entry) || Contains(already, entry.moveId))
                    continue;
                cumulative += entry.effectiveWeight;
                if (roll < cumulative)
                    return entry.moveId;
            }

            return "";
        }

        private static bool Contains(List<string> ids, string moveId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(moveId))
                return false;

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], moveId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
