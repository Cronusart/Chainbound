using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum EncounterCadence
    {
        OnEnter,
        PerSecond,
    }

    /// <summary>
    /// How zone species entries combine with the parent area table.
    /// </summary>
    public enum EncounterEntryMergeMode
    {
        /// <summary>Zone entries append to (and may override by speciesId) the area table.</summary>
        Extend = 0,
        /// <summary>Zone entries fully replace the area table.</summary>
        Replace = 1,
    }

    [Serializable]
    public class WeightedMoveEntry
    {
        public string moveId;
        public int weight = 10;
    }

    [Serializable]
    public class WeightedVariantEntry
    {
        [Tooltip("Maps to ChainlingVisualIdentity.visualVariantId (e.g. base, chrome). Spawn odds only; rarity tier lives on the species visual set / rarity table.")]
        public string visualVariantId = ChainlingVisualIdentity.BaseVariantId;

        [Tooltip("Relative spawn weight for this skin id.")]
        [Min(0)]
        public int weight = 100;
    }

    /// <summary>
    /// Per-tier weight multipliers for rarity tables. Negative = inherit parent/default (1).
    /// Order: Common, Uncommon, Rare, Epic, Legendary, Mythic.
    /// </summary>
    [Serializable]
    public class EncounterRarityTierMultipliers
    {
        public float common = -1f;
        public float uncommon = -1f;
        public float rare = -1f;
        public float epic = -1f;
        public float legendary = -1f;
        public float mythic = -1f;

        public float[] ToArray()
        {
            return new[] { common, uncommon, rare, epic, legendary, mythic };
        }
    }

    [Serializable]
    public class EncounterSegmentWeight
    {
        public TimeSegment segment = TimeSegment.Day;
        [Range(0f, 5f)]
        public float weight = 1f;
    }

    [Serializable]
    public class ZoneEncounterEntry
    {
        [Tooltip("Species definition ID (matches SpeciesDefinitionSO.speciesId).")]
        public string speciesId;

        [Tooltip("Relative weight. Higher = more common.")]
        public int weight = 10;

        [Tooltip("Allowed time-of-day segments. None = all segments.")]
        public TimeSegmentMask allowedSegments = TimeSegmentMask.None;

        [Tooltip("Optional per-segment weight multipliers (default 1 when absent).")]
        public List<EncounterSegmentWeight> segmentWeights = new List<EncounterSegmentWeight>();

        [Tooltip("Legacy 0–100 mapper to a capture volatility profile (not a catch %). ≥70 Calm, ≥40 Standard, ≥20 Volatile, else Extreme. Mutated / entangled / variant do not change this. See Docs/Specs/Combat/CAPTURE_DIFFICULTY_COMPOSITION_v1.md.")]
        [Range(0, 100)]
        public int baseCaptureRate = 45;

        [Tooltip("Star progression baseline (0-5).")]
        [Range(0, 5)]
        public int baseStars;

        [Tooltip("+/- star variance from baseStars.")]
        [Range(0, 2)]
        public int starVariance;

        [Header("Wild Moves")]
        [Tooltip("Optional shared move pool. When set, used instead of inlineMovePool.")]
        public WildMovePoolSO movePool;

        [Tooltip("Inline weighted move pool when movePool is null.")]
        public List<WeightedMoveEntry> inlineMovePool = new List<WeightedMoveEntry>();

        [Tooltip("Battle kit size min (inclusive).")]
        [Range(1, 4)]
        public int battleKitSizeMin = 1;

        [Tooltip("Battle kit size max (inclusive).")]
        [Range(1, 4)]
        public int battleKitSizeMax = 2;

        [Header("Variants")]
        [Tooltip("Preferred: rarity table of skin ids + spawn weights. Rarity is a tier label; weight is the numeric odds.")]
        public WildVariantRarityTableSO variantRarityTable;

        [Tooltip("Optional shared id+weight pool when no rarity table is set.")]
        public WildVariantPoolSO variantPool;

        [Tooltip("Inline id+weight pool when table/pool are unset.")]
        public List<WeightedVariantEntry> inlineVariantPool = new List<WeightedVariantEntry>();

        public float GetSegmentWeightMultiplier(TimeSegment segment)
        {
            if (segmentWeights == null || segmentWeights.Count == 0)
                return 1f;

            for (int i = 0; i < segmentWeights.Count; i++)
            {
                if (segmentWeights[i].segment == segment)
                    return Mathf.Max(0f, segmentWeights[i].weight);
            }

            return 1f;
        }

        public bool IsSegmentAllowed(TimeSegment segment)
        {
            return TimeOfDayPredicateUtility.MatchesSegments(allowedSegments, segment);
        }
    }

    /// <summary>
    /// Configures encounter parameters for a trigger zone: species weights,
    /// time-of-day gating, encounter chance, and cadence.
    /// Optionally inherits defaults from an <see cref="EncounterAreaSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterZone_", menuName = "Chainbound/Combat/Encounters/Encounter Zone")]
    public class EncounterZoneSO : ScriptableObject
    {
        [Header("Identity")]
        public string zoneId;

        [Tooltip("Scenery id inside the shared combat scene (loomville, link1, jungle). Empty uses loomville.")]
        public string combatStageId;

        [Tooltip("Optional parent area that supplies base table and defaults.")]
        public EncounterAreaSO area;

        /// <summary>
        /// Stage activated in the shared combat scene. Empty authored values use <see cref="CombatStageIds.Default"/>.
        /// </summary>
        public string ResolveCombatStageId()
        {
            return CombatStageIds.Normalize(combatStageId);
        }

        [Header("Encounter Chance")]
        [Tooltip("Probability of an encounter per roll. Negative = inherit from area (or 0.15).")]
        public float encounterChance = -1f;

        [Header("Cadence")]
        public EncounterCadence cadence = EncounterCadence.OnEnter;

        [Tooltip("Seconds between rolls when cadence is PerSecond.")]
        public float cadenceIntervalSeconds = 3f;

        [Header("Reward Difficulty")]
        [Tooltip("When inheritDifficulty is true, area difficulty is used.")]
        public bool inheritDifficulty = true;
        public EncounterDifficulty difficulty = EncounterDifficulty.Standard;

        [Header("Boss / Quest Intro")]
        [Tooltip("Optional dialogue asset injected into the combat boss intro bridge when this zone resolves as a boss encounter.")]
        public UnityEngine.Object bossIntroDialogueAsset;

        [Header("Party Size")]
        [Tooltip("Minimum number of wild chainlings (1 or 2). Negative = inherit.")]
        public int minEncounterPartySize = -1;

        [Tooltip("Maximum number of wild chainlings (1 or 2). Negative = inherit.")]
        public int maxEncounterPartySize = -1;

        [Header("Species Table")]
        public EncounterEntryMergeMode entryMergeMode = EncounterEntryMergeMode.Extend;
        public List<ZoneEncounterEntry> entries = new List<ZoneEncounterEntry>();

        [Header("Wild Trait Overrides (optional)")]
        [Tooltip("Scalar on non-none mutation weights (and legacy chance). Negative = inherit area/global.")]
        public float mutationChanceOverride = -1f;

        [Tooltip("Override entangled chance for this zone. Negative = inherit area/global.")]
        public float entangledChanceOverride = -1f;

        [Tooltip("Override IV min. Use int.MinValue to inherit.")]
        public int ivMinOverride = int.MinValue;

        [Tooltip("Override IV max. Use int.MinValue to inherit.")]
        public int ivMaxOverride = int.MinValue;

        [Header("Rarity Levers (optional)")]
        [Tooltip("Default variant rarity table when an entry has none.")]
        public WildVariantRarityTableSO defaultVariantRarityTable;

        [Tooltip("Per-tier multipliers for variant rarity tables. Negative = inherit.")]
        public EncounterRarityTierMultipliers variantTierMultipliers = new EncounterRarityTierMultipliers();

        [Tooltip("Mutation FX rarity table for this zone (inherits area when null).")]
        public WildMutationRarityTableSO mutationRarityTable;

        [Tooltip("Per-tier multipliers for mutation rarity tables. Negative = inherit.")]
        public EncounterRarityTierMultipliers mutationTierMultipliers = new EncounterRarityTierMultipliers();

        [Header("Default Pools (optional)")]
        [Tooltip("Fallback move pool when an entry has none.")]
        public WildMovePoolSO defaultMovePool;

        [Tooltip("Fallback variant pool when an entry has none.")]
        public WildVariantPoolSO defaultVariantPool;

        [Header("Capture Known Move")]
        [Tooltip("Species database used to roll a captured Chainling's starting Known move from that species' progression pool.")]
        public SpeciesDatabaseSO speciesDatabase;

        [Tooltip("Star-gate odds for that capture roll. Empty uses the built-in 10/20/30/40/50 table.")]
        public ChainlingProgressionRewardTablesSO progressionRewardTables;

        [Tooltip("Rarity weights applied to progression pool entries during folio and capture rolls. Empty treats every rarity weight as 1.")]
        public ProgressionConfigSO progressionConfig;
    }
}
