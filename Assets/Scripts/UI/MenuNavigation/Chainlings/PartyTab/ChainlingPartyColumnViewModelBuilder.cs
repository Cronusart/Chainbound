using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyColumnViewModelBuilder
    {
        public static ChainlingPartyColumnSummary BuildSummary(
            ChainlingPartyViewMode mode,
            ChainlingPartyColumnContext ctx)
        {
            if (ctx == null)
                return ChainlingPartyColumnSummary.Empty();

            if (!ctx.HasFilledChainling)
                return ChainlingPartyColumnSummary.Empty("Assign at a PC.");

            var summary = new ChainlingPartyColumnSummary();
            PopulateSharedSummary(summary, ctx);
            summary.vitals = ChainlingPartyVitalsDisplayUtility.Build(ctx);
            switch (mode)
            {
                case ChainlingPartyViewMode.General:
                    ChainlingPartyGeneralDisplayUtility.PopulateSummary(summary, ctx);
                    break;
                case ChainlingPartyViewMode.Attributes:
                    ChainlingPartyAttributesDisplayUtility.PopulateSummary(summary, ctx);
                    break;
                case ChainlingPartyViewMode.Moves:
                    ChainlingPartyMoveDisplayUtility.PopulateSummary(summary, ctx);
                    break;
                case ChainlingPartyViewMode.Detail:
                    ChainlingPartyDetailDisplayUtility.PopulateSummary(summary, ctx);
                    break;
                case ChainlingPartyViewMode.Loadout:
                    ChainlingPartyLoadoutDisplayUtility.PopulateSummary(summary, ctx);
                    break;
                case ChainlingPartyViewMode.Stats:
                    ChainlingPartyStatsDisplayUtility.PopulateSummary(summary, ctx);
                    break;
            }

            return summary;
        }

        private static void PopulateSharedSummary(
            ChainlingPartyColumnSummary summary,
            ChainlingPartyColumnContext ctx)
        {
            if (summary == null || ctx == null || !ctx.HasFilledChainling)
                return;

            ChainlingSnapshot snapshot = ctx.entry.snapshot;
            summary.cycleTier = ChainlingPlayerTerms.ResolveCycleTier(snapshot.rebirthCount);
            summary.cycleLabel = ChainlingPlayerTerms.FormatCycleLabel(snapshot.rebirthCount);
            summary.typeLabel = ChainlingVariantDisplayUtility.ResolveTypeLabel(ctx);
            summary.variantLabel = ChainlingVariantDisplayUtility.ResolveVariantLabel(ctx);
            summary.formLabel = summary.variantLabel;
            summary.isEntangled = ChainlingProgressionIdentityUtility.IsEntangled(snapshot);
            summary.archiveState = ChainlingPlayerTerms.ResolveArchiveState(snapshot);
        }

        public static ChainlingPartyColumnDrillDown BuildDrillDown(
            ChainlingPartyViewMode mode,
            ChainlingPartyColumnContext ctx)
        {
            if (ctx == null || !ctx.HasFilledChainling)
                return ChainlingPartyColumnDrillDown.None();

            var drillDown = new ChainlingPartyColumnDrillDown();
            switch (mode)
            {
                case ChainlingPartyViewMode.General:
                    ChainlingPartyGeneralDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
                case ChainlingPartyViewMode.Attributes:
                    ChainlingPartyAttributesDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
                case ChainlingPartyViewMode.Moves:
                    ChainlingPartyMoveDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
                case ChainlingPartyViewMode.Detail:
                    ChainlingPartyDetailDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
                case ChainlingPartyViewMode.Loadout:
                    ChainlingPartyLoadoutDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
                case ChainlingPartyViewMode.Stats:
                    ChainlingPartyStatsDisplayUtility.PopulateDrillDown(drillDown, ctx);
                    break;
            }

            ChainlingPartyColumnSummary summary = BuildSummary(mode, ctx);
            if (!summary.supportsDrillDown)
                return ChainlingPartyColumnDrillDown.None();

            return drillDown;
        }
    }
}
