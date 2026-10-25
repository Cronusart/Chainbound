using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Read-only zone signal log built from <see cref="EncounterZoneSO"/> assets.
    /// Starters are listed under Loom Lab and left out of wild rows.
    /// Weights, move pools, and variant tables are not copied onto the public rows.
    /// </summary>
    public sealed class EncounterCatalogIndex
    {
        public const string LabZoneId = "loom_lab";
        public const string Often = "Often";
        public const string Sometimes = "Sometimes";
        public const string Rare = "Rare";

        const float OftenShare = 0.25f;
        const float SometimesShare = 0.10f;

        static readonly string[] PreferredZoneOrder =
        {
            "starter_grove_grass",
            "jungle_bush",
            "link1_grass",
            "loomville_grass",
        };

        public IReadOnlyList<EncounterCatalogZone> Zones { get; }

        EncounterCatalogIndex(List<EncounterCatalogZone> zones)
        {
            Zones = zones ?? new List<EncounterCatalogZone>();
        }

        public static EncounterCatalogIndex Build(EncounterCatalogSourceSO source)
        {
            if (source == null)
                return Build((IReadOnlyList<EncounterZoneSO>)null, null);

            return Build(source.zones, source.speciesDatabase);
        }

        public static EncounterCatalogIndex Build(IReadOnlyList<EncounterZoneSO> zones, SpeciesDatabaseSO speciesDatabase)
        {
            speciesDatabase?.WarmLookup();
            var built = new List<EncounterCatalogZone>();
            var seenZoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (zones != null)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    EncounterZoneSO zone = zones[i];
                    if (zone == null)
                        continue;

                    string zoneId = string.IsNullOrWhiteSpace(zone.zoneId) ? zone.name : zone.zoneId.Trim();
                    if (string.IsNullOrWhiteSpace(zoneId) || !seenZoneIds.Add(zoneId))
                        continue;

                    built.Add(BuildZone(zone, zoneId, speciesDatabase));
                }
            }

            built.Sort(CompareZones);
            EncounterCatalogZone lab = BuildLab(speciesDatabase);
            if (lab != null)
                built.Insert(0, lab);

            return new EncounterCatalogIndex(built);
        }

        public List<EncounterCatalogSpecies> FindSpecies(string speciesId)
        {
            var matches = new List<EncounterCatalogSpecies>();
            if (string.IsNullOrWhiteSpace(speciesId))
                return matches;

            for (int i = 0; i < Zones.Count; i++)
            {
                EncounterCatalogZone zone = Zones[i];
                if (zone?.species == null)
                    continue;

                for (int s = 0; s < zone.species.Count; s++)
                {
                    EncounterCatalogSpecies slot = zone.species[s];
                    if (slot != null && string.Equals(slot.speciesId, speciesId, StringComparison.OrdinalIgnoreCase))
                        matches.Add(slot);
                }
            }

            return matches;
        }

        public List<string> ZoneDisplayNamesFor(string speciesId)
        {
            List<EncounterCatalogSpecies> slots = FindSpecies(speciesId);
            var names = new List<string>();
            for (int i = 0; i < slots.Count; i++)
            {
                string name = slots[i].zoneDisplayName;
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                    names.Add(name);
            }

            return names;
        }

        public static string DisplayNameForZone(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return "Route";

            switch (zoneId.Trim().ToLowerInvariant())
            {
                case "starter_grove_grass":
                    return "Starter Grove";
                case "jungle_bush":
                    return "Jungle";
                case "link1_grass":
                    return "Link 1";
                case "loomville_grass":
                    return "Loomville";
                case LabZoneId:
                    return "Loom Lab";
                default:
                    return TitleCase(zoneId);
            }
        }

        public static string FormatTime(int allowedSegments)
        {
            // Bit values match TimeSegmentMask. The public signature stays an int so
            // combat tests can call this without referencing the time-of-day assembly.
            if (allowedSegments == (int)TimeSegmentMask.None || allowedSegments == (int)TimeSegmentMask.All)
                return "Any";
            if (allowedSegments == (int)TimeSegmentMask.Day)
                return "Day";
            if (allowedSegments == (int)TimeSegmentMask.Night)
                return "Night";
            if (allowedSegments == (int)TimeSegmentMask.Dawn)
                return "Dawn";
            if (allowedSegments == (int)TimeSegmentMask.Dusk)
                return "Dusk";
            return "Any";
        }

        /// <summary>
        /// Coarse word from this entry's share of the zone total. The number itself stays off the page.
        /// Share of at least 25% is Often, at least 10% is Sometimes, otherwise Rare.
        /// </summary>
        public static string FormatFrequency(int weight, int zoneTotalWeight)
        {
            if (weight <= 0 || zoneTotalWeight <= 0)
                return Rare;

            float share = weight / (float)zoneTotalWeight;
            if (share >= OftenShare)
                return Often;
            if (share >= SometimesShare)
                return Sometimes;
            return Rare;
        }

        public static string FormatStarBand(int baseStars, int starVariance)
        {
            int min = Mathf.Clamp(baseStars, 0, 5);
            int max = Mathf.Clamp(baseStars + Mathf.Max(0, starVariance), 0, 5);
            if (min == max)
                return "Stars " + min;
            return "Stars " + min + "-" + max;
        }

        public static string FormatVolatility(int baseCaptureRate)
        {
            switch (CaptureChallengeResolver.MapLegacyBaseRate(baseCaptureRate))
            {
                case CaptureVolatilityProfile.Calm:
                    return "Calm";
                case CaptureVolatilityProfile.Standard:
                    return "Standard";
                case CaptureVolatilityProfile.Volatile:
                    return "Volatile";
                default:
                    return "Extreme";
            }
        }

        public static string FirstSentence(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "";

            string trimmed = description.Trim();
            int dot = trimmed.IndexOf('.');
            if (dot < 0)
                return trimmed.Length <= 90 ? trimmed : trimmed.Substring(0, 90).TrimEnd() + "…";

            return trimmed.Substring(0, dot + 1).Trim();
        }

        static EncounterCatalogZone BuildZone(EncounterZoneSO zone, string zoneId, SpeciesDatabaseSO speciesDatabase)
        {
            var catalogZone = new EncounterCatalogZone
            {
                zoneId = zoneId,
                displayName = DisplayNameForZone(zoneId),
                isLabOrigin = false,
            };

            List<ZoneEncounterEntry> merged = MergeEntries(zone);
            var kept = new List<ZoneEncounterEntry>();
            var seenSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < merged.Count; i++)
            {
                ZoneEncounterEntry entry = merged[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                    continue;
                if (!seenSpecies.Add(entry.speciesId))
                    continue;
                if (IsStarter(speciesDatabase, entry.speciesId))
                    continue;
                kept.Add(entry);
            }

            int total = 0;
            for (int i = 0; i < kept.Count; i++)
                total += Mathf.Max(0, kept[i].weight);

            for (int i = 0; i < kept.Count; i++)
                catalogZone.species.Add(ToSpecies(kept[i], catalogZone, total));

            return catalogZone;
        }

        static EncounterCatalogZone BuildLab(SpeciesDatabaseSO speciesDatabase)
        {
            if (speciesDatabase == null)
                return null;

            List<SpeciesDefinitionSO> starters = speciesDatabase.GetStarters();
            if (starters == null || starters.Count == 0)
                return null;

            var lab = new EncounterCatalogZone
            {
                zoneId = LabZoneId,
                displayName = DisplayNameForZone(LabZoneId),
                isLabOrigin = true,
            };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<SpeciesDefinitionSO>();
            for (int i = 0; i < starters.Count; i++)
            {
                SpeciesDefinitionSO starter = starters[i];
                if (starter == null || string.IsNullOrWhiteSpace(starter.speciesId))
                    continue;
                if (seen.Add(starter.speciesId))
                    rows.Add(starter);
            }

            if (rows.Count == 0)
                return null;

            for (int i = 0; i < rows.Count; i++)
            {
                lab.species.Add(new EncounterCatalogSpecies
                {
                    speciesId = rows[i].speciesId.Trim(),
                    zoneId = lab.zoneId,
                    zoneDisplayName = lab.displayName,
                    isLabOrigin = true,
                    weight = 1,
                    zoneTotalWeight = rows.Count,
                    allowedSegments = 0,
                    baseCaptureRate = RateForProfile(rows[i].defaultCaptureProfile),
                    baseStars = 0,
                    starVariance = 0,
                });
            }

            return lab;
        }

        static List<ZoneEncounterEntry> MergeEntries(EncounterZoneSO zone)
        {
            var merged = new List<ZoneEncounterEntry>();
            var indexBySpecies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (zone.entryMergeMode == EncounterEntryMergeMode.Extend &&
                zone.area != null &&
                zone.area.entries != null)
            {
                AddEntries(merged, indexBySpecies, zone.area.entries, replace: false);
            }

            if (zone.entries != null)
                AddEntries(merged, indexBySpecies, zone.entries, replace: true);

            return merged;
        }

        static void AddEntries(
            List<ZoneEncounterEntry> merged,
            Dictionary<string, int> indexBySpecies,
            List<ZoneEncounterEntry> source,
            bool replace)
        {
            for (int i = 0; i < source.Count; i++)
            {
                ZoneEncounterEntry entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
                    continue;

                if (indexBySpecies.TryGetValue(entry.speciesId, out int existing))
                {
                    if (replace)
                        merged[existing] = entry;
                    continue;
                }

                indexBySpecies.Add(entry.speciesId, merged.Count);
                merged.Add(entry);
            }
        }

        static EncounterCatalogSpecies ToSpecies(ZoneEncounterEntry entry, EncounterCatalogZone zone, int totalWeight)
        {
            return new EncounterCatalogSpecies
            {
                speciesId = entry.speciesId.Trim(),
                zoneId = zone.zoneId,
                zoneDisplayName = zone.displayName,
                isLabOrigin = false,
                weight = Mathf.Max(0, entry.weight),
                zoneTotalWeight = totalWeight,
                allowedSegments = (int)entry.allowedSegments,
                baseCaptureRate = entry.baseCaptureRate,
                baseStars = entry.baseStars,
                starVariance = entry.starVariance,
            };
        }

        static bool IsStarter(SpeciesDatabaseSO speciesDatabase, string speciesId)
        {
            if (speciesDatabase == null || string.IsNullOrWhiteSpace(speciesId))
                return false;

            SpeciesDefinitionSO species = speciesDatabase.GetById(speciesId);
            return species != null && species.isStarter;
        }

        static int RateForProfile(CaptureVolatilityProfile profile)
        {
            switch (profile)
            {
                case CaptureVolatilityProfile.Calm:
                    return 70;
                case CaptureVolatilityProfile.Volatile:
                    return 20;
                case CaptureVolatilityProfile.Extreme:
                    return 0;
                default:
                    return 40;
            }
        }

        static int CompareZones(EncounterCatalogZone a, EncounterCatalogZone b)
        {
            int rank = ZoneRank(a.zoneId).CompareTo(ZoneRank(b.zoneId));
            if (rank != 0)
                return rank;
            return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
        }

        static int ZoneRank(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return 100;

            string key = zoneId.Trim().ToLowerInvariant();
            for (int i = 0; i < PreferredZoneOrder.Length; i++)
            {
                if (key == PreferredZoneOrder[i])
                    return i;
            }

            return 100;
        }

        static string TitleCase(string zoneId)
        {
            string[] parts = zoneId.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                string lower = parts[i].ToLowerInvariant();
                parts[i] = char.ToUpperInvariant(lower[0]) + (lower.Length > 1 ? lower.Substring(1) : "");
            }

            return string.Join(" ", parts);
        }
    }

    public sealed class EncounterCatalogZone
    {
        public string zoneId;
        public string displayName;
        public bool isLabOrigin;
        public List<EncounterCatalogSpecies> species = new List<EncounterCatalogSpecies>();
    }

    public sealed class EncounterCatalogSpecies
    {
        public string speciesId;
        public string zoneId;
        public string zoneDisplayName;
        public bool isLabOrigin;
        public int weight;
        public int zoneTotalWeight;
        public int allowedSegments;
        public int baseCaptureRate;
        public int baseStars;
        public int starVariance;
    }
}
