using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rolls a wild battle kit (N moves, at least one damaging) and exactly one
    /// known move from that kit for capture persistence.
    /// </summary>
    public static class WildMoveRoller
    {
        public struct Result
        {
            public List<string> battleMoveIds;
            public string knownMoveId;
        }

        public static Result Roll(
            IReadOnlyList<WeightedMoveEntry> pool,
            int kitSizeMin,
            int kitSizeMax,
            System.Random rng,
            MoveDatabaseSO moveDb,
            IReadOnlyDictionary<string, float> moveWeightMultipliers = null)
        {
            var result = new Result
            {
                battleMoveIds = new List<string>(),
                knownMoveId = "",
            };

            if (rng == null)
                rng = new System.Random();

            var working = BuildWorkingPool(pool, moveWeightMultipliers);
            if (working.Count == 0)
                return result;

            int min = Mathf.Clamp(kitSizeMin, 1, 4);
            int max = Mathf.Clamp(kitSizeMax, 1, 4);
            if (max < min) max = min;
            int kitSize = min == max ? min : min + rng.Next(max - min + 1);
            kitSize = Mathf.Min(kitSize, working.Count);

            var picked = new List<WeightedMoveEntry>(kitSize);
            var remaining = new List<WeightedMoveEntry>(working);

            for (int i = 0; i < kitSize && remaining.Count > 0; i++)
            {
                var chosen = WeightedPick(remaining, rng);
                if (chosen == null)
                    break;
                picked.Add(chosen);
                remaining.Remove(chosen);
            }

            EnsureDamagingMove(picked, remaining, working, rng, moveDb);

            for (int i = 0; i < picked.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(picked[i].moveId))
                    result.battleMoveIds.Add(picked[i].moveId.Trim());
            }

            if (result.battleMoveIds.Count > 0)
            {
                int knownIndex = rng.Next(result.battleMoveIds.Count);
                result.knownMoveId = result.battleMoveIds[knownIndex];
            }

            return result;
        }

        private static List<WeightedMoveEntry> BuildWorkingPool(
            IReadOnlyList<WeightedMoveEntry> pool,
            IReadOnlyDictionary<string, float> multipliers)
        {
            var working = new List<WeightedMoveEntry>();
            if (pool == null)
                return working;

            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.moveId) || entry.weight <= 0)
                    continue;

                float mult = 1f;
                if (multipliers != null &&
                    multipliers.TryGetValue(entry.moveId.Trim(), out float m))
                {
                    mult = Mathf.Max(0f, m);
                }

                int scaled = Mathf.Max(0, Mathf.RoundToInt(entry.weight * mult));
                if (scaled <= 0)
                    continue;

                working.Add(new WeightedMoveEntry
                {
                    moveId = entry.moveId.Trim(),
                    weight = scaled,
                });
            }

            return working;
        }

        private static void EnsureDamagingMove(
            List<WeightedMoveEntry> picked,
            List<WeightedMoveEntry> remaining,
            List<WeightedMoveEntry> fullPool,
            System.Random rng,
            MoveDatabaseSO moveDb)
        {
            if (HasDamaging(picked, moveDb))
                return;

            // Prefer swapping in a damaging move from remaining, else from full pool.
            WeightedMoveEntry damageCandidate = FindDamaging(remaining, moveDb)
                ?? FindDamaging(fullPool, moveDb);

            if (damageCandidate == null)
                return;

            if (picked.Count == 0)
            {
                picked.Add(damageCandidate);
                return;
            }

            // Replace a non-damaging slot.
            for (int i = 0; i < picked.Count; i++)
            {
                if (!IsDamaging(picked[i], moveDb))
                {
                    picked[i] = damageCandidate;
                    return;
                }
            }

            picked[rng.Next(picked.Count)] = damageCandidate;
        }

        private static bool HasDamaging(List<WeightedMoveEntry> entries, MoveDatabaseSO moveDb)
        {
            if (entries == null)
                return false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsDamaging(entries[i], moveDb))
                    return true;
            }
            return false;
        }

        private static WeightedMoveEntry FindDamaging(List<WeightedMoveEntry> entries, MoveDatabaseSO moveDb)
        {
            if (entries == null)
                return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsDamaging(entries[i], moveDb))
                    return entries[i];
            }
            return null;
        }

        public static bool IsDamaging(WeightedMoveEntry entry, MoveDatabaseSO moveDb)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.moveId))
                return false;
            if (moveDb == null)
                return true; // Without a DB, cannot verify; treat as acceptable.

            MoveDefinitionSO def = moveDb.GetById(entry.moveId);
            if (def == null)
                return false;

            return def.category == MoveCategory.DamagingMelee
                   || def.category == MoveCategory.DamagingRanged
                   || def.power > 0;
        }

        private static WeightedMoveEntry WeightedPick(List<WeightedMoveEntry> entries, System.Random rng)
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
                total += Mathf.Max(0, entries[i].weight);
            if (total <= 0)
                return entries.Count > 0 ? entries[0] : null;

            int roll = rng.Next(total);
            int cumulative = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                cumulative += Mathf.Max(0, entries[i].weight);
                if (roll < cumulative)
                    return entries[i];
            }
            return entries[entries.Count - 1];
        }
    }
}
