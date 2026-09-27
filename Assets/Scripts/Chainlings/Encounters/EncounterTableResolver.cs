using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Flattened, modifier-applied encounter table ready for seeded rolls.
    /// </summary>
    public class EffectiveEncounterTable
    {
        public string areaId = "";
        public string zoneId = "";
        public TimeSegment segment;
        public float encounterChance = 0.15f;
        public EncounterDifficulty difficulty = EncounterDifficulty.Standard;
        public int minPartySize = 1;
        public int maxPartySize = 1;
        public WildRollConfig wildConfig = new WildRollConfig();
        public float ivBumpChanceOverride = -1f;
        public int favorBonus;
        public UnityEngine.Object bossIntroDialogueAsset;
        public List<WeightedMutationEntry> mutationPool = new List<WeightedMutationEntry>();
        public List<EffectiveEncounterEntry> entries = new List<EffectiveEncounterEntry>();
        public List<string> activeModifierIds = new List<string>();
        public string debugResolvedSummary = "";
    }

    public class EffectiveEncounterEntry
    {
        public string speciesId;
        public float weight;
        public int baseCaptureRate = 45;
        public int baseStars;
        public int starVariance;
        public int battleKitSizeMin = 1;
        public int battleKitSizeMax = 2;
        public List<WeightedMoveEntry> movePool = new List<WeightedMoveEntry>();
        public List<WeightedVariantEntry> variantPool = new List<WeightedVariantEntry>();
        public Dictionary<string, float> moveWeightMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> variantWeightMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pure C# resolver: Area -> Zone -> TimeSegment -> Modifiers -> EffectiveEncounterTable.
    /// </summary>
    public static class EncounterTableResolver
    {
        public static EffectiveEncounterTable Resolve(
            EncounterZoneSO zone,
            WildRollConfig fallbackWildConfig,
            TimeSegment segment,
            EncounterModifierService modifierService = null,
            long unixUtc = 0)
        {
            var table = new EffectiveEncounterTable
            {
                segment = segment,
                wildConfig = CloneWild(fallbackWildConfig),
            };

            if (zone == null)
            {
                table.debugResolvedSummary = "zone=null";
                return table;
            }

            EncounterAreaSO area = zone.area;
            table.zoneId = zone.zoneId ?? "";
            table.areaId = area != null ? (area.areaId ?? "") : "";
            table.bossIntroDialogueAsset = zone.bossIntroDialogueAsset;

            // Chance
            float baseChance = zone.encounterChance >= 0f
                ? zone.encounterChance
                : (area != null ? area.encounterChance : 0.15f);
            table.encounterChance = Mathf.Clamp01(baseChance);

            // Difficulty
            table.difficulty = zone.inheritDifficulty && area != null
                ? area.difficulty
                : zone.difficulty;

            // Party size
            int minParty = zone.minEncounterPartySize >= 0
                ? zone.minEncounterPartySize
                : (area != null ? area.minEncounterPartySize : 1);
            int maxParty = zone.maxEncounterPartySize >= 0
                ? zone.maxEncounterPartySize
                : (area != null ? area.maxEncounterPartySize : 1);
            table.minPartySize = Mathf.Clamp(minParty, 1, 2);
            table.maxPartySize = Mathf.Clamp(maxParty, 1, 2);
            if (table.maxPartySize < table.minPartySize)
                table.maxPartySize = table.minPartySize;

            // Wild traits
            if (area != null && area.wildTraits != null)
                table.wildConfig = CloneWild(area.wildTraits);
            ApplyZoneTraitOverrides(table.wildConfig, zone);

            float[] variantTier = ResolveVariantTierMultipliers(zone, area);
            float[] mutationTier = ResolveMutationTierMultipliers(zone, area);
            BuildMutationPool(table, zone, area, mutationTier);

            // Merge entries
            var merged = MergeEntries(area, zone);
            for (int i = 0; i < merged.Count; i++)
            {
                var src = merged[i];
                if (src == null || string.IsNullOrWhiteSpace(src.speciesId))
                    continue;
                if (!src.IsSegmentAllowed(segment))
                    continue;

                float segMult = src.GetSegmentWeightMultiplier(segment);
                if (segMult <= 0f)
                    continue;

                var effective = new EffectiveEncounterEntry
                {
                    speciesId = src.speciesId.Trim(),
                    weight = Mathf.Max(0f, src.weight * segMult),
                    baseCaptureRate = src.baseCaptureRate,
                    baseStars = src.baseStars,
                    starVariance = src.starVariance,
                    battleKitSizeMin = src.battleKitSizeMin,
                    battleKitSizeMax = src.battleKitSizeMax,
                };

                CopyMovePool(src, zone, area, effective);
                CopyVariantPool(src, zone, area, effective, variantTier);

                if (effective.weight > 0f)
                    table.entries.Add(effective);
            }

            ApplyModifiers(table, modifierService, unixUtc);
            // Re-apply mutation pool after MutationChance live-ops scalar may have changed.
            BuildMutationPool(table, zone, area, mutationTier);
            table.debugResolvedSummary = BuildSummary(table);
            return table;
        }

        private static void ApplyModifiers(
            EffectiveEncounterTable table,
            EncounterModifierService modifierService,
            long unixUtc)
        {
            if (modifierService == null)
                modifierService = EncounterModifierService.Instance;
            if (modifierService == null)
                return;

            if (unixUtc <= 0)
                unixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var active = modifierService.GetActiveModifiers(
                table.areaId, table.zoneId, table.segment, table.difficulty, unixUtc);

            for (int m = 0; m < active.Count; m++)
            {
                var mod = active[m];
                if (mod == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(mod.modifierId))
                    table.activeModifierIds.Add(mod.modifierId);
                if (mod.ops == null)
                    continue;

                for (int o = 0; o < mod.ops.Count; o++)
                {
                    var op = mod.ops[o];
                    if (op == null)
                        continue;
                    ApplyTableOp(table, mod, op);
                }
            }
        }

        private static void ApplyTableOp(
            EffectiveEncounterTable table,
            EncounterModifier mod,
            EncounterModifierOp op)
        {
            switch (op.opType)
            {
                case EncounterModifierOpType.EncounterChance:
                    table.encounterChance = Mathf.Clamp01(
                        EncounterModifierService.ApplyOp(table.encounterChance, op.mode, op.value));
                    break;

                case EncounterModifierOpType.MutationChance:
                    table.wildConfig.mutationChance = Mathf.Clamp01(
                        EncounterModifierService.ApplyOp(table.wildConfig.mutationChance, op.mode, op.value));
                    break;

                case EncounterModifierOpType.EntangledChance:
                    table.wildConfig.entangledChance = Mathf.Clamp01(
                        EncounterModifierService.ApplyOp(table.wildConfig.entangledChance, op.mode, op.value));
                    break;

                case EncounterModifierOpType.IvBumpChance:
                    float currentBump = table.ivBumpChanceOverride >= 0f ? table.ivBumpChanceOverride : 0f;
                    table.ivBumpChanceOverride = Mathf.Clamp01(
                        EncounterModifierService.ApplyOp(currentBump, op.mode, op.value));
                    break;

                case EncounterModifierOpType.IvMin:
                    table.wildConfig.ivMin = Mathf.RoundToInt(
                        EncounterModifierService.ApplyOp(table.wildConfig.ivMin, op.mode, op.value));
                    break;

                case EncounterModifierOpType.IvMax:
                    table.wildConfig.ivMax = Mathf.RoundToInt(
                        EncounterModifierService.ApplyOp(table.wildConfig.ivMax, op.mode, op.value));
                    break;

                case EncounterModifierOpType.FavorBonus:
                    if (op.mode == EncounterModifierOpMode.Set)
                        table.favorBonus = Mathf.RoundToInt(op.value);
                    else if (op.mode == EncounterModifierOpMode.Multiply)
                        table.favorBonus = Mathf.RoundToInt(table.favorBonus * op.value);
                    else
                        table.favorBonus += Mathf.RoundToInt(op.value);
                    break;

                case EncounterModifierOpType.InjectSpecies:
                    if (string.IsNullOrWhiteSpace(op.targetId))
                        break;
                    if (!mod.MatchesScope(table.areaId, table.zoneId, op.targetId, table.segment, table.difficulty) &&
                        (mod.speciesIds == null || mod.speciesIds.Count == 0 ||
                         !ListContains(mod.speciesIds, op.targetId)))
                    {
                        // Still allow inject when species scope empty or matches target.
                        if (mod.speciesIds != null && mod.speciesIds.Count > 0 &&
                            !ListContains(mod.speciesIds, op.targetId))
                            break;
                    }
                    table.entries.Add(new EffectiveEncounterEntry
                    {
                        speciesId = op.targetId.Trim(),
                        weight = Mathf.Max(1, op.injectWeight),
                        baseCaptureRate = op.injectCaptureRate,
                        baseStars = op.injectBaseStars,
                        battleKitSizeMin = 1,
                        battleKitSizeMax = 1,
                    });
                    break;

                case EncounterModifierOpType.InjectVariant:
                    if (string.IsNullOrWhiteSpace(op.targetId))
                        break;
                    for (int i = 0; i < table.entries.Count; i++)
                    {
                        var entry = table.entries[i];
                        if (!mod.MatchesScope(table.areaId, table.zoneId, entry.speciesId, table.segment, table.difficulty))
                            continue;
                        entry.variantPool.Add(new WeightedVariantEntry
                        {
                            visualVariantId = op.targetId.Trim(),
                            weight = Mathf.Max(1, op.injectWeight),
                        });
                    }
                    break;

                case EncounterModifierOpType.SpeciesWeight:
                case EncounterModifierOpType.CaptureRate:
                case EncounterModifierOpType.MoveWeight:
                case EncounterModifierOpType.VariantWeight:
                    ApplyEntryScopedOp(table, mod, op);
                    break;
            }
        }

        private static void ApplyEntryScopedOp(
            EffectiveEncounterTable table,
            EncounterModifier mod,
            EncounterModifierOp op)
        {
            for (int i = 0; i < table.entries.Count; i++)
            {
                var entry = table.entries[i];
                if (!mod.MatchesScope(table.areaId, table.zoneId, entry.speciesId, table.segment, table.difficulty))
                    continue;

                switch (op.opType)
                {
                    case EncounterModifierOpType.SpeciesWeight:
                        if (!string.IsNullOrWhiteSpace(op.targetId) &&
                            !string.Equals(op.targetId.Trim(), entry.speciesId, StringComparison.OrdinalIgnoreCase))
                            break;
                        entry.weight = Mathf.Max(0f,
                            EncounterModifierService.ApplyOp(entry.weight, op.mode, op.value));
                        break;

                    case EncounterModifierOpType.CaptureRate:
                        if (!string.IsNullOrWhiteSpace(op.targetId) &&
                            !string.Equals(op.targetId.Trim(), entry.speciesId, StringComparison.OrdinalIgnoreCase))
                            break;
                        entry.baseCaptureRate = Mathf.Clamp(
                            Mathf.RoundToInt(EncounterModifierService.ApplyOp(entry.baseCaptureRate, op.mode, op.value)),
                            0, 100);
                        break;

                    case EncounterModifierOpType.MoveWeight:
                        if (string.IsNullOrWhiteSpace(op.targetId))
                            break;
                        ApplyWeightMultiplier(entry.moveWeightMultipliers, op.targetId, op.mode, op.value);
                        break;

                    case EncounterModifierOpType.VariantWeight:
                        if (string.IsNullOrWhiteSpace(op.targetId))
                            break;
                        ApplyWeightMultiplier(entry.variantWeightMultipliers, op.targetId, op.mode, op.value);
                        break;
                }
            }
        }

        private static void ApplyWeightMultiplier(
            Dictionary<string, float> map,
            string targetId,
            EncounterModifierOpMode mode,
            float value)
        {
            string key = targetId.Trim();
            float current = map.TryGetValue(key, out float existing) ? existing : 1f;
            map[key] = Mathf.Max(0f, EncounterModifierService.ApplyOp(current, mode, value));
        }

        private static List<ZoneEncounterEntry> MergeEntries(EncounterAreaSO area, EncounterZoneSO zone)
        {
            var result = new List<ZoneEncounterEntry>();
            if (zone.entryMergeMode == EncounterEntryMergeMode.Replace)
            {
                if (zone.entries != null)
                    result.AddRange(zone.entries);
                return result;
            }

            // Extend: area first, then zone overrides by speciesId.
            var bySpecies = new Dictionary<string, ZoneEncounterEntry>(StringComparer.OrdinalIgnoreCase);
            if (area?.entries != null)
            {
                for (int i = 0; i < area.entries.Count; i++)
                {
                    var e = area.entries[i];
                    if (e == null || string.IsNullOrWhiteSpace(e.speciesId))
                        continue;
                    bySpecies[e.speciesId.Trim()] = e;
                }
            }

            if (zone.entries != null)
            {
                for (int i = 0; i < zone.entries.Count; i++)
                {
                    var e = zone.entries[i];
                    if (e == null || string.IsNullOrWhiteSpace(e.speciesId))
                        continue;
                    bySpecies[e.speciesId.Trim()] = e;
                }
            }

            result.AddRange(bySpecies.Values);
            return result;
        }

        private static void CopyMovePool(
            ZoneEncounterEntry src,
            EncounterZoneSO zone,
            EncounterAreaSO area,
            EffectiveEncounterEntry dest)
        {
            if (src.movePool != null && src.movePool.entries != null && src.movePool.entries.Count > 0)
            {
                dest.movePool.AddRange(src.movePool.entries);
                return;
            }
            if (src.inlineMovePool != null && src.inlineMovePool.Count > 0)
            {
                dest.movePool.AddRange(src.inlineMovePool);
                return;
            }
            if (zone.defaultMovePool != null && zone.defaultMovePool.entries != null)
            {
                dest.movePool.AddRange(zone.defaultMovePool.entries);
                return;
            }
            if (area != null && area.defaultMovePool != null && area.defaultMovePool.entries != null)
                dest.movePool.AddRange(area.defaultMovePool.entries);
        }

        private static void CopyVariantPool(
            ZoneEncounterEntry src,
            EncounterZoneSO zone,
            EncounterAreaSO area,
            EffectiveEncounterEntry dest,
            float[] variantTier)
        {
            WildVariantRarityTableSO table =
                src.variantRarityTable != null
                    ? src.variantRarityTable
                    : (zone.defaultVariantRarityTable != null
                        ? zone.defaultVariantRarityTable
                        : area != null ? area.defaultVariantRarityTable : null);

            if (table != null)
            {
                // Per-variant live-ops multipliers apply later in WildVariantRoller.
                dest.variantPool.AddRange(table.BuildWeightedPool(variantTier, null));
                if (dest.variantPool.Count > 0)
                    return;
            }

            if (src.variantPool != null && src.variantPool.entries != null && src.variantPool.entries.Count > 0)
            {
                dest.variantPool.AddRange(src.variantPool.entries);
                return;
            }
            if (src.inlineVariantPool != null && src.inlineVariantPool.Count > 0)
            {
                dest.variantPool.AddRange(src.inlineVariantPool);
                return;
            }
            if (zone.defaultVariantPool != null && zone.defaultVariantPool.entries != null)
            {
                dest.variantPool.AddRange(zone.defaultVariantPool.entries);
                return;
            }
            if (area != null && area.defaultVariantPool != null && area.defaultVariantPool.entries != null)
                dest.variantPool.AddRange(area.defaultVariantPool.entries);

            if (dest.variantPool.Count == 0)
            {
                dest.variantPool.Add(new WeightedVariantEntry
                {
                    visualVariantId = ChainlingVisualIdentity.BaseVariantId,
                    weight = 100,
                });
            }
        }

        private static void BuildMutationPool(
            EffectiveEncounterTable table,
            EncounterZoneSO zone,
            EncounterAreaSO area,
            float[] mutationTier)
        {
            table.mutationPool.Clear();
            WildMutationRarityTableSO mutationTable =
                zone.mutationRarityTable != null
                    ? zone.mutationRarityTable
                    : area != null ? area.mutationRarityTable : null;

            float nonNoneScalar = Mathf.Clamp01(table.wildConfig != null ? table.wildConfig.mutationChance : 0f);
            // When a rarity table is present, mutationChance scales non-none weights.
            // Treat default 0.047 as relative mass; convert to a multiplier that keeps Common none dominant:
            // use max(mutationChance, 0) as direct scalar (table author includes none weight).
            if (mutationTable != null)
            {
                // Scale non-none by mutationChance / 0.047 so default table weights match authored intent,
                // while still allowing zone override of mutationChance as a lever.
                float relative = nonNoneScalar <= 0f
                    ? 0f
                    : nonNoneScalar / 0.047f;
                table.mutationPool.AddRange(mutationTable.BuildWeightedPool(mutationTier, relative));
            }

            if (table.mutationPool.Count == 0)
            {
                // Legacy fallback: boolean mutationChance with a single generic mutation id.
                int noneWeight = 1000;
                int mutWeight = Mathf.Max(0, Mathf.RoundToInt(1000f * nonNoneScalar));
                table.mutationPool.Add(new WeightedMutationEntry
                {
                    mutationId = ChainlingVisualIdentity.NoneId,
                    weight = Mathf.Max(1, noneWeight - mutWeight),
                });
                if (mutWeight > 0)
                {
                    table.mutationPool.Add(new WeightedMutationEntry
                    {
                        mutationId = "mutation_default",
                        weight = mutWeight,
                    });
                }
            }
        }

        private static float[] ResolveVariantTierMultipliers(EncounterZoneSO zone, EncounterAreaSO area)
        {
            float[] parent = area != null && area.variantTierMultipliers != null
                ? area.variantTierMultipliers.ToArray()
                : null;
            float[] child = zone != null && zone.variantTierMultipliers != null
                ? zone.variantTierMultipliers.ToArray()
                : null;
            return EncounterRarityLeverUtility.MergeTierMultipliers(parent, child);
        }

        private static float[] ResolveMutationTierMultipliers(EncounterZoneSO zone, EncounterAreaSO area)
        {
            float[] parent = area != null && area.mutationTierMultipliers != null
                ? area.mutationTierMultipliers.ToArray()
                : null;
            float[] child = zone != null && zone.mutationTierMultipliers != null
                ? zone.mutationTierMultipliers.ToArray()
                : null;
            return EncounterRarityLeverUtility.MergeTierMultipliers(parent, child);
        }

        private static void ApplyZoneTraitOverrides(WildRollConfig config, EncounterZoneSO zone)
        {
            if (config == null || zone == null)
                return;
            if (zone.mutationChanceOverride >= 0f)
                config.mutationChance = zone.mutationChanceOverride;
            if (zone.entangledChanceOverride >= 0f)
                config.entangledChance = zone.entangledChanceOverride;
            if (zone.ivMinOverride != int.MinValue)
                config.ivMin = zone.ivMinOverride;
            if (zone.ivMaxOverride != int.MinValue)
                config.ivMax = zone.ivMaxOverride;
        }

        private static WildRollConfig CloneWild(WildRollConfig source)
        {
            if (source == null)
                return new WildRollConfig();
            var clone = new WildRollConfig
            {
                mutationChance = source.mutationChance,
                entangledChance = source.entangledChance,
                ivMin = source.ivMin,
                ivMax = source.ivMax,
                entangledTypePool = new List<ChainlingType>(),
            };
            if (source.entangledTypePool != null)
                clone.entangledTypePool.AddRange(source.entangledTypePool);
            return clone;
        }

        private static bool ListContains(List<string> ids, string value)
        {
            if (ids == null || string.IsNullOrWhiteSpace(value))
                return false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]) &&
                    string.Equals(ids[i].Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string BuildSummary(EffectiveEncounterTable table)
        {
            var sb = new StringBuilder();
            sb.Append($"area={table.areaId} zone={table.zoneId} seg={table.segment} ");
            sb.Append($"chance={table.encounterChance:F3} favorBonus={table.favorBonus} ");
            sb.Append($"mut={table.wildConfig.mutationChance:F3} ent={table.wildConfig.entangledChance:F3} ");
            sb.Append($"entries=[");
            for (int i = 0; i < table.entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(table.entries[i].speciesId).Append(':').Append(table.entries[i].weight.ToString("0.##"));
            }
            sb.Append("] mods=[").Append(string.Join(",", table.activeModifierIds)).Append(']');
            return sb.ToString();
        }
    }
}
