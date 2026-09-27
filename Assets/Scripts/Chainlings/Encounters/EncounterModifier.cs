using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum EncounterModifierOpMode
    {
        Multiply = 0,
        Add = 1,
        Set = 2,
    }

    public enum EncounterModifierOpType
    {
        EncounterChance = 0,
        SpeciesWeight = 1,
        MutationChance = 2,
        EntangledChance = 3,
        IvBumpChance = 4,
        IvMin = 5,
        IvMax = 6,
        MoveWeight = 7,
        VariantWeight = 8,
        CaptureRate = 9,
        FavorBonus = 10,
        InjectSpecies = 11,
        InjectVariant = 12,
        RewardRamMultiplier = 13,
        RewardXpMultiplier = 14,
        RewardRareDropChance = 15,
        RewardBonusItem = 16,
    }

    [Serializable]
    public class EncounterModifierOp
    {
        public EncounterModifierOpType opType = EncounterModifierOpType.SpeciesWeight;
        [Tooltip("Target id when relevant (speciesId, moveId, variantId, itemId). Empty = all.")]
        public string targetId = "";
        public EncounterModifierOpMode mode = EncounterModifierOpMode.Multiply;
        public float value = 1f;

        [Tooltip("For InjectSpecies / InjectVariant ops: weight of the injected row.")]
        public int injectWeight = 10;

        [Tooltip("For InjectSpecies: optional base capture rate.")]
        [Range(0, 100)]
        public int injectCaptureRate = 45;

        [Tooltip("For InjectSpecies: optional base stars.")]
        [Range(0, 5)]
        public int injectBaseStars;
    }

    /// <summary>
    /// Universal modifier used by local design assets and server-wide events.
    /// Scope selectors empty = ALL. Time window 0/0 = always.
    /// </summary>
    [Serializable]
    public class EncounterModifier
    {
        public string modifierId = "";
        public string displayName = "";

        [Header("Scope (empty = ALL)")]
        public List<string> areaIds = new List<string>();
        public List<string> zoneIds = new List<string>();
        public List<string> speciesIds = new List<string>();
        public TimeSegmentMask segments = TimeSegmentMask.None;

        [Tooltip("When true, difficulty filter is applied.")]
        public bool filterByDifficulty;
        public EncounterDifficulty difficulty = EncounterDifficulty.Standard;

        [Header("Time Window (UTC unix seconds; 0 = open)")]
        public long startUnixUtc;
        public long endUnixUtc;

        [Header("Ops")]
        public List<EncounterModifierOp> ops = new List<EncounterModifierOp>();

        public bool IsActiveAt(long unixUtc)
        {
            if (startUnixUtc > 0 && unixUtc < startUnixUtc)
                return false;
            if (endUnixUtc > 0 && unixUtc > endUnixUtc)
                return false;
            return true;
        }

        public bool MatchesScope(
            string areaId,
            string zoneId,
            string speciesId,
            TimeSegment segment,
            EncounterDifficulty difficultyTier)
        {
            if (!MatchesList(areaIds, areaId))
                return false;
            if (!MatchesList(zoneIds, zoneId))
                return false;
            if (!MatchesList(speciesIds, speciesId))
                return false;
            if (!TimeOfDayPredicateUtility.MatchesSegments(segments, segment))
                return false;
            if (filterByDifficulty && difficulty != difficultyTier)
                return false;
            return true;
        }

        private static bool MatchesList(List<string> ids, string value)
        {
            if (ids == null || ids.Count == 0)
                return true;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]) &&
                    string.Equals(ids[i].Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public interface IEncounterModifierSource
    {
        IReadOnlyList<EncounterModifier> GetModifiers();
    }
}
