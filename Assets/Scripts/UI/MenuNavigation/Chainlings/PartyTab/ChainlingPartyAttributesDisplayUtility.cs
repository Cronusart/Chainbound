using System.Collections.Generic;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyAttributesDisplayUtility
    {
        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            summary.attributeAxes.Clear();
            summary.attributeProgressionFooter = "";
            if (!ctx.HasFilledChainling)
                return;

            ChainlingSnapshot snapshot = ctx.entry.snapshot;
            summary.primaryType = ctx.species != null ? ctx.species.primaryType : ChainlingType.None;

            IReadOnlyList<ChainlingAttributeRadarAxis> axes =
                ChainlingAttributeDisplayUtility.BuildPartyRadarAxes(
                    snapshot,
                    ctx.species,
                    ctx.speciesDatabase);
            for (int i = 0; i < axes.Count; i++)
                summary.attributeAxes.Add(axes[i]);

            summary.attributeProgressionFooter =
                $"*{snapshot.stars}/{CombatConstants.STAR_MAX}  {ChainlingPlayerTerms.FormatCycleLabel(snapshot.rebirthCount)}";

            // Fallback text lines when AttributesViewLayout is not present on the prefab.
            IReadOnlyList<ChainlingAttributeStatValue> stats =
                ChainlingAttributeDisplayUtility.BuildPartyStatValues(snapshot, ctx.species);
            if (stats.Count >= 5)
            {
                summary.lines.Add(new ChainlingPartyDisplayLine(
                    $"{stats[0].Label} / {stats[1].Label}",
                    $"{stats[0].Total} / {stats[1].Total}"));
                summary.lines.Add(new ChainlingPartyDisplayLine(
                    $"{stats[2].Label} / {stats[3].Label}",
                    $"{stats[2].Total} / {stats[3].Total}"));
                summary.lines.Add(new ChainlingPartyDisplayLine(stats[4].Label, stats[4].Total.ToString()));
            }

            summary.lines.Add(new ChainlingPartyDisplayLine(
                "Progression",
                summary.attributeProgressionFooter,
                true));
            summary.supportsDrillDown = true;
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.AttributeBreakdown;
            drillDown.attributeSections.Clear();
            if (!ctx.HasFilledChainling)
                return;

            List<ChainlingAttributeBreakdownSection> sections =
                ChainlingAttributeDisplayUtility.BuildBreakdownSections(ctx.entry.snapshot, ctx.species);

            for (int i = 0; i < sections.Count; i++)
            {
                ChainlingAttributeBreakdownSection section = sections[i];
                drillDown.attributeSections.Add(new ChainlingPartyAttributeBreakdownSection
                {
                    label = section.label,
                    total = section.total,
                    baseValue = section.baseValue,
                    ivBonus = section.ivBonus,
                    cycleAutoFlat = section.cycleAutoFlat,
                    cyclePickFlat = section.cyclePickFlat,
                });
            }
        }
    }
}
