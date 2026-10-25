using System;
using System.Collections.Generic;
using System.Text;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation
{
    public enum ChainlingDatabaseTier
    {
        Unregistered = 0,
        Encountered = 1,
        Captured = 2,
    }

    /// <summary>
    /// Field list for one zone slot.
    /// Unregistered keeps identity off the page.
    /// Encountered is a field note.
    /// Captured adds the rest of the file, still without weights, move pools, or variant odds.
    /// </summary>
    public sealed class ChainlingDatabaseEntryView
    {
        public ChainlingDatabaseTier tier;
        public string rowLabel = "";
        public string name = "";
        public string typeLabel = "";
        public string zoneLabel = "";
        public string timeLabel = "";
        public string frequencyLabel = "";
        public string habitatLine = "";
        public string description = "";
        public string roleLabel = "";
        public string flagshipMoveName = "";
        public string starBand = "";
        public string volatilityLabel = "";
        public string otherZones = "";
        public bool showPortrait;
        public bool canOpenSheet;
        public bool canOpenDossier;
        public string lowerTitle = "";
        public string lowerLine1 = "";
        public string lowerLine2 = "";
        public string prompt = "";
        public string sheetTitle = "";
        public string sheetBody = "";
        public string sheetPrompt = "";
    }

    public static class ChainlingDatabaseEntryBuilder
    {
        public const string UnregisteredLabel = "Unregistered";
        public const string NoFieldTrace = "No field trace";
        public const string FileComplete = "File complete";
        public const string PromptBack = "B Back";
        public const string PromptRead = "A Read    B Back";
        public const string PromptDossier = "A Open dossier    B Back";

        public static ChainlingDatabaseTier Resolve(string speciesId, ChainlingDexSnapshot dex, ISet<string> ownedSpeciesIds)
        {
            if (Contains(ownedSpeciesIds, speciesId) || Contains(dex != null ? dex.caughtSpeciesIds : null, speciesId))
                return ChainlingDatabaseTier.Captured;
            if (Contains(dex != null ? dex.seenSpeciesIds : null, speciesId))
                return ChainlingDatabaseTier.Encountered;
            return ChainlingDatabaseTier.Unregistered;
        }

        public static ChainlingDatabaseEntryView Build(
            EncounterCatalogSpecies slot,
            SpeciesDefinitionSO species,
            ChainlingDatabaseTier tier,
            IReadOnlyList<string> otherZoneNames,
            bool owned)
        {
            var view = new ChainlingDatabaseEntryView { tier = tier };
            if (slot == null || tier == ChainlingDatabaseTier.Unregistered)
            {
                view.rowLabel = UnregisteredLabel;
                view.lowerTitle = UnregisteredLabel;
                view.lowerLine1 = NoFieldTrace;
                view.prompt = PromptBack;
                view.sheetPrompt = PromptBack;
                return view;
            }

            view.name = DisplayName(species, slot.speciesId);
            view.rowLabel = view.name;
            view.typeLabel = species != null
                ? MoveSelectDisplayUtility.FormatTypeName(species.primaryType)
                : "";
            view.zoneLabel = slot.zoneDisplayName ?? "";
            view.timeLabel = EncounterCatalogIndex.FormatTime(slot.allowedSegments);
            view.frequencyLabel = EncounterCatalogIndex.FormatFrequency(slot.weight, slot.zoneTotalWeight);
            view.habitatLine = EncounterCatalogIndex.FirstSentence(species != null ? species.description : "");
            view.showPortrait = true;
            view.canOpenSheet = true;
            view.lowerTitle = view.name;
            view.lowerLine2 = view.habitatLine;
            view.prompt = PromptRead;
            view.sheetTitle = view.name;
            view.sheetPrompt = PromptBack;

            if (tier == ChainlingDatabaseTier.Captured)
            {
                view.description = species != null && species.description != null ? species.description.Trim() : "";
                view.roleLabel = SpeciesCombatRoleResolver.FormatLabel(SpeciesCombatRoleResolver.Resolve(species));
                view.flagshipMoveName = FlagshipName(species);
                view.starBand = EncounterCatalogIndex.FormatStarBand(slot.baseStars, slot.starVariance);
                view.volatilityLabel = EncounterCatalogIndex.FormatVolatility(slot.baseCaptureRate);
                view.otherZones = JoinOthers(slot.zoneDisplayName, otherZoneNames);
                view.canOpenDossier = owned;
                view.lowerLine1 = FileComplete;
                if (owned)
                    view.sheetPrompt = PromptDossier;
            }
            else
            {
                view.lowerLine1 = "Logged after a " + view.zoneLabel + " encounter";
            }

            view.sheetBody = BuildSheet(view);
            return view;
        }

        static string BuildSheet(ChainlingDatabaseEntryView view)
        {
            var builder = new StringBuilder();
            AppendLine(builder, view.typeLabel);
            string where = JoinParts(view.zoneLabel, view.timeLabel, view.frequencyLabel);
            AppendLine(builder, where);
            AppendLine(builder, view.habitatLine);
            if (view.tier != ChainlingDatabaseTier.Captured)
                return builder.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(view.description))
            {
                builder.AppendLine();
                builder.AppendLine(view.description);
            }

            AppendLine(builder, view.roleLabel);
            AppendLine(builder, view.flagshipMoveName);
            AppendLine(builder, view.starBand);
            AppendLine(builder, view.volatilityLabel);
            if (!string.IsNullOrWhiteSpace(view.otherZones))
                builder.AppendLine("Also in " + view.otherZones);

            return builder.ToString().Trim();
        }

        static string DisplayName(SpeciesDefinitionSO species, string speciesId)
        {
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName.Trim();
            return speciesId ?? "";
        }

        static string FlagshipName(SpeciesDefinitionSO species)
        {
            MoveDefinitionSO move = species != null ? species.flagshipMove : null;
            if (move == null)
                return "";
            if (!string.IsNullOrWhiteSpace(move.displayName))
                return move.displayName.Trim();
            return move.moveId ?? "";
        }

        static string JoinOthers(string currentZone, IReadOnlyList<string> otherZoneNames)
        {
            if (otherZoneNames == null || otherZoneNames.Count == 0)
                return "";

            var names = new List<string>();
            bool skippedCurrent = false;
            for (int i = 0; i < otherZoneNames.Count; i++)
            {
                string name = otherZoneNames[i];
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (!skippedCurrent && string.Equals(name, currentZone, StringComparison.OrdinalIgnoreCase))
                {
                    skippedCurrent = true;
                    continue;
                }

                if (!names.Contains(name))
                    names.Add(name);
            }

            return names.Count == 0 ? "" : string.Join(", ", names);
        }

        static string JoinParts(string zone, string time, string frequency)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(zone))
                parts.Add(zone);
            if (!string.IsNullOrWhiteSpace(time))
                parts.Add(time);
            if (!string.IsNullOrWhiteSpace(frequency))
                parts.Add(frequency);
            return string.Join(" · ", parts);
        }

        static void AppendLine(StringBuilder builder, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                builder.AppendLine(value);
        }

        static bool Contains(IEnumerable<string> ids, string speciesId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(speciesId))
                return false;

            foreach (string id in ids)
            {
                if (string.Equals(id, speciesId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
