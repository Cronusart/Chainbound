using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public readonly struct ChainlingAttributeStatValue
    {
        public ChainlingAttributeStatValue(string label, int total)
        {
            Label = label ?? "";
            Total = total;
        }

        public string Label { get; }
        public int Total { get; }
    }

    /// <summary>
    /// One pentagon axis for party Attributes radar (final total + display fill01).
    /// </summary>
    public readonly struct ChainlingAttributeRadarAxis
    {
        public ChainlingAttributeRadarAxis(
            string label,
            int total,
            float fill01,
            float absoluteFill01,
            int baseValue,
            int ivIndex)
        {
            Label = label ?? "";
            Total = total;
            Fill01 = Mathf.Clamp01(fill01);
            AbsoluteFill01 = Mathf.Clamp01(absoluteFill01);
            BaseValue = baseValue;
            IvIndex = ivIndex;
        }

        public string Label { get; }
        public int Total { get; }
        /// <summary>Catalog-normalized fill against shared species-DB floor/ceil.</summary>
        public float Fill01 { get; }
        /// <summary>total / catalog ceil (no floor subtract) — debug / optional display.</summary>
        public float AbsoluteFill01 { get; }
        public int BaseValue { get; }
        public int IvIndex { get; }
    }

    /// <summary>
    /// Universal radar range derived from <see cref="SpeciesDatabaseSO"/> (not per-column autofit).
    /// </summary>
    public readonly struct ChainlingRadarCatalogRange
    {
        public static ChainlingRadarCatalogRange Invalid => default;

        public ChainlingRadarCatalogRange(float floor, float ceil)
        {
            Floor = Mathf.Max(0f, floor);
            Ceil = Mathf.Max(Floor + 0.0001f, ceil);
            IsValid = Ceil > Floor;
        }

        /// <summary>Display zero point (25% of catalog min possible by default).</summary>
        public float Floor { get; }

        /// <summary>Shared ceiling = highest theoretical rim across all catalog axes.</summary>
        public float Ceil { get; }

        public bool IsValid { get; }

        public float Normalize(int total)
        {
            if (!IsValid)
                return 0f;
            return Mathf.Clamp01((total - Floor) / (Ceil - Floor));
        }
    }

    public sealed class ChainlingAttributeBreakdownSection
    {
        public string label;
        public int total;
        public int baseValue;
        public int ivBonus;
        public int cycleAutoFlat;
        public int cyclePickFlat;
    }

    /// <summary>
    /// Shared combat stat formatting for menu overview and party attribute views.
    /// </summary>
    public static class ChainlingAttributeDisplayUtility
    {
        public const float DefaultCatalogFloorFactor = 0.25f;

        /// <summary>
        /// Table / drill-down order (not radar vertices).
        /// </summary>
        private static readonly string[] PartyStatLabels = { "MATK", "MDEF", "RATK", "RDEF", "SPD" };
        private static readonly int[] PartyStatBaseIndices = { 1, 2, 3, 4, 5 };
        private static readonly CoreStat[] PartyStatCore = {
            CoreStat.MeleeAtk, CoreStat.MeleeDef, CoreStat.RangedAtk, CoreStat.RangedDef, CoreStat.Speed
        };

        /// <summary>
        /// Radar vertex order with default graphic rotation (−90° = index 0 at bottom, then CCW):
        /// SPD bottom-center · MDEF bottom-right · MATK top-right · RATK top-left · RDEF bottom-left.
        /// </summary>
        public static readonly string[] PartyRadarAxisLabels = { "SPD", "MDEF", "MATK", "RATK", "RDEF" };

        private static readonly int[] PartyRadarIvIndices = { 5, 2, 1, 3, 4 };
        private static readonly int[] PartyRadarBaseSlots = { 4, 1, 0, 2, 3 };
        private static readonly CoreStat[] PartyRadarCore = {
            CoreStat.Speed, CoreStat.MeleeDef, CoreStat.MeleeAtk, CoreStat.RangedAtk, CoreStat.RangedDef
        };

        private static SpeciesDatabaseSO cachedCatalog;
        private static float cachedFloorFactor = -1f;
        private static ChainlingRadarCatalogRange cachedRange;

        public static int BuildMaxHp(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            return ComputeProgressed(snapshot, species != null ? species.baseMaxHP : 0, CoreStat.MaxHP, 0);
        }

        public static IReadOnlyList<ChainlingAttributeStatValue> BuildPartyStatValues(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species)
        {
            var values = new ChainlingAttributeStatValue[PartyStatLabels.Length];
            if (snapshot == null || species == null)
                return values;

            int[] bases = ResolvePartyBases(species);
            for (int i = 0; i < PartyStatLabels.Length; i++)
            {
                values[i] = new ChainlingAttributeStatValue(
                    PartyStatLabels[i],
                    ComputeProgressed(snapshot, bases[i], PartyStatCore[i], PartyStatBaseIndices[i]));
            }

            return values;
        }

        /// <summary>
        /// Party Attributes radar axes. Fill01 uses a shared catalog floor/ceil from
        /// <paramref name="speciesDatabase"/> (highest rim across all species/axes as ceil;
        /// 25% of lowest IV_MIN possible as floor). Falls back to per-axis rim if DB missing.
        /// </summary>
        public static IReadOnlyList<ChainlingAttributeRadarAxis> BuildPartyRadarAxes(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species,
            SpeciesDatabaseSO speciesDatabase = null,
            float catalogFloorFactor = DefaultCatalogFloorFactor)
        {
            var axes = new ChainlingAttributeRadarAxis[PartyRadarAxisLabels.Length];
            if (snapshot == null || species == null)
                return axes;

            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            int[] bases = ResolvePartyBases(species);

            ChainlingRadarCatalogRange range = GetOrBuildCatalogRadarRange(speciesDatabase, catalogFloorFactor);

            for (int i = 0; i < PartyRadarAxisLabels.Length; i++)
            {
                int baseSlot = PartyRadarBaseSlots[i];
                int ivIndex = PartyRadarIvIndices[i];
                int baseValue = bases[baseSlot];
                int total = ComputeProgressed(snapshot, baseValue, PartyRadarCore[i], ivIndex);
                int localRim = ComputeRadarRim(baseValue, rebirth);

                float absoluteFill = range.IsValid
                    ? (range.Ceil > 0f ? total / range.Ceil : 0f)
                    : (localRim > 0 ? total / (float)localRim : 0f);
                float fill01 = range.IsValid
                    ? range.Normalize(total)
                    : absoluteFill;

                axes[i] = new ChainlingAttributeRadarAxis(
                    PartyRadarAxisLabels[i],
                    total,
                    fill01,
                    absoluteFill,
                    baseValue,
                    ivIndex);
            }

            return axes;
        }

        /// <summary>
        /// Shared radar range from the species catalog.
        /// Ceil = max (base + IV_MAX + Cycle auto max + all picks into one combat stat).
        /// Floor = floorFactor × min max(1, base + IV_MIN), excluding zero/ghost bases.
        /// </summary>
        public static ChainlingRadarCatalogRange BuildCatalogRadarRange(
            SpeciesDatabaseSO database,
            float floorFactor = DefaultCatalogFloorFactor)
        {
            floorFactor = Mathf.Clamp01(floorFactor);
            if (database == null || database.species == null || database.species.Count == 0)
                return ChainlingRadarCatalogRange.Invalid;

            int maxRim = 0;
            int minPossible = int.MaxValue;
            bool found = false;

            for (int s = 0; s < database.species.Count; s++)
            {
                SpeciesDefinitionSO species = database.species[s];
                if (species == null)
                    continue;

                int[] bases = ResolvePartyBases(species);
                for (int i = 0; i < bases.Length; i++)
                {
                    int baseValue = bases[i];
                    if (baseValue <= 0)
                        continue;

                    found = true;
                    int rim = ComputeRadarRim(baseValue, CombatConstants.REBIRTH_MAX);
                    if (rim > maxRim)
                        maxRim = rim;

                    int lowTotal = Mathf.Max(1, baseValue + CombatConstants.IV_MIN);
                    if (lowTotal < minPossible)
                        minPossible = lowTotal;
                }
            }

            if (!found || maxRim <= 0 || minPossible == int.MaxValue)
                return ChainlingRadarCatalogRange.Invalid;

            float floor = minPossible * floorFactor;
            if (floor >= maxRim)
                floor = 0f;

            return new ChainlingRadarCatalogRange(floor, maxRim);
        }

        public static ChainlingRadarCatalogRange GetOrBuildCatalogRadarRange(
            SpeciesDatabaseSO database,
            float floorFactor = DefaultCatalogFloorFactor)
        {
            floorFactor = Mathf.Clamp01(floorFactor);
            if (database == null)
                return ChainlingRadarCatalogRange.Invalid;

            if (cachedCatalog == database &&
                Mathf.Abs(cachedFloorFactor - floorFactor) < 0.0001f &&
                cachedRange.IsValid)
            {
                return cachedRange;
            }

            cachedCatalog = database;
            cachedFloorFactor = floorFactor;
            cachedRange = BuildCatalogRadarRange(database, floorFactor);
            return cachedRange;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearCatalogRadarRangeCache()
        {
            cachedCatalog = null;
            cachedFloorFactor = -1f;
            cachedRange = ChainlingRadarCatalogRange.Invalid;
        }
#endif

        /// <summary>
        /// Theoretical max for one combat axis at a given Cycle count:
        /// base + IV_MAX + auto(+N) + all picks into that stat (+N).
        /// </summary>
        public static int ComputeRadarRim(int speciesBase, int rebirthCount)
        {
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount);
            int autoFlat = rebirth * CombatConstants.CYCLE_AUTO_STAT_FLAT;
            int pickFlat = rebirth * CombatConstants.CYCLE_PICK_STAT_FLAT;
            return Mathf.Max(1, speciesBase + CombatConstants.IV_MAX + autoFlat + pickFlat);
        }

        public static string BuildCompactStatsWithHp(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot == null || species == null)
                return "Stats unavailable until species data is assigned.";

            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            IReadOnlyList<ChainlingAttributeStatValue> partyStats = BuildPartyStatValues(snapshot, species);
            return
                $"Cycle flats +{rebirth} combat / +{ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, rebirth, species.baseMaxHP)} MaxHP auto\n" +
                $"HP {BuildMaxHp(snapshot, species)}   " +
                $"MATK {partyStats[0].Total}   MDEF {partyStats[1].Total}\n" +
                $"RATK {partyStats[2].Total}   RDEF {partyStats[3].Total}   SPD {partyStats[4].Total}";
        }

        public static List<ChainlingAttributeBreakdownSection> BuildBreakdownSections(
            ChainlingSnapshot snapshot,
            SpeciesDefinitionSO species)
        {
            var sections = new List<ChainlingAttributeBreakdownSection>();
            if (snapshot == null || species == null)
                return sections;

            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(snapshot.rebirthCount);
            int[] bonuses = ChainlingProgressionUtility.NormalizeRebirthIvBonuses(snapshot.rebirthIvBonuses);
            int[] bases = ResolvePartyBases(species);
            for (int i = 0; i < PartyStatLabels.Length; i++)
            {
                CoreStat stat = PartyStatCore[i];
                int iv = GetIv(snapshot.ivs, PartyStatBaseIndices[i]);
                sections.Add(new ChainlingAttributeBreakdownSection
                {
                    label = PartyStatLabels[i],
                    total = ComputeProgressed(snapshot, bases[i], stat, PartyStatBaseIndices[i]),
                    baseValue = bases[i],
                    ivBonus = iv,
                    cycleAutoFlat = ChainlingProgressionUtility.GetCycleAutoFlat(stat, rebirth, bases[i]),
                    cyclePickFlat = ChainlingProgressionUtility.GetCyclePickFlat(stat, bonuses, bases[i]),
                });
            }

            return sections;
        }

        private static int[] ResolvePartyBases(SpeciesDefinitionSO species)
        {
            return new[]
            {
                species.baseMeleeAtk,
                species.baseMeleeDef,
                species.baseRangedAtk,
                species.baseRangedDef,
                species.baseSpeed,
            };
        }

        private static int ComputeProgressed(
            ChainlingSnapshot snapshot,
            int baseValue,
            CoreStat stat,
            int ivIndex)
        {
            if (snapshot == null)
                return 1;

            int iv = GetIv(snapshot.ivs, ivIndex);
            return ChainlingProgressionUtility.ComputeProgressedStat(
                baseValue,
                iv,
                stat,
                snapshot.rebirthCount,
                snapshot.rebirthIvBonuses);
        }

        private static int GetIv(int[] ivs, int index)
        {
            return ivs != null && index >= 0 && index < ivs.Length ? ivs[index] : 0;
        }
    }
}
