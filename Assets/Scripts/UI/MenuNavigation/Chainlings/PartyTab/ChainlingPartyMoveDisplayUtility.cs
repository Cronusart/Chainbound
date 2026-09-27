using System.Collections.Generic;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyMoveDisplayUtility
    {
        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            summary.moveRows.Clear();
            summary.moveEquippedCount = 0;
            summary.moveMaxSlots = 0;
            if (!ctx.HasFilledChainling)
                return;

            int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(ctx.entry.snapshot.rebirthCount);
            List<ChainlingPartyMoveModifierRow> rows = BuildEquippedRows(ctx, maxSlots);
            summary.moveRows.AddRange(BuildMoveRowSummaries(ctx, maxSlots));
            summary.moveEquippedCount = summary.moveRows.Count;
            summary.moveMaxSlots = maxSlots;
            summary.supportsDrillDown = rows.Count > 0;

            for (int i = 0; i < summary.moveRows.Count; i++)
            {
                ChainlingPartyMoveRowSummary row = summary.moveRows[i];
                if (row == null || row.isEmpty)
                    continue;

                summary.lines.Add(new ChainlingPartyDisplayLine(
                    $"Slot {i + 1}",
                    $"{row.moveName} - {row.statLabel}"));
            }

            if (summary.lines.Count == 0)
                summary.lines.Add(new ChainlingPartyDisplayLine("", "No moves equipped.", true));
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.MoveModifierSheet;
            drillDown.moveModifierRows.Clear();
            if (!ctx.HasFilledChainling)
                return;

            int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(ctx.entry.snapshot.rebirthCount);
            drillDown.moveModifierRows.AddRange(BuildEquippedRows(ctx, maxSlots));
        }

        private static List<ChainlingPartyMoveRowSummary> BuildMoveRowSummaries(
            ChainlingPartyColumnContext ctx,
            int maxSlots)
        {
            var summaries = new List<ChainlingPartyMoveRowSummary>(maxSlots);
            ChainlingSnapshot snapshot = ctx.entry.snapshot;
            if (snapshot.moveIds == null || snapshot.moveIds.Count == 0)
                return summaries;

            for (int i = 0; i < snapshot.moveIds.Count && summaries.Count < maxSlots; i++)
            {
                string moveId = snapshot.moveIds[i];
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                MoveDefinitionSO def = ctx.moveDatabase != null ? ctx.moveDatabase.GetById(moveId) : null;
                summaries.Add(new ChainlingPartyMoveRowSummary
                {
                    moveName = ResolveMoveName(def, moveId),
                    statLabel = PartyTabMoveDisplayUtility.FormatStatLabel(def),
                    moveType = def != null ? def.baseType : ChainlingType.None,
                    rarity = def != null ? def.rarity : RarityTier.Common,
                });
            }

            return summaries;
        }

        private static List<ChainlingPartyMoveModifierRow> BuildEquippedRows(
            ChainlingPartyColumnContext ctx,
            int maxSlots)
        {
            var rows = new List<ChainlingPartyMoveModifierRow>(maxSlots);
            ChainlingSnapshot snapshot = ctx.entry.snapshot;
            if (snapshot.moveIds == null || snapshot.moveIds.Count == 0)
                return rows;

            for (int i = 0; i < snapshot.moveIds.Count && rows.Count < maxSlots; i++)
            {
                string moveId = snapshot.moveIds[i];
                if (string.IsNullOrWhiteSpace(moveId))
                    continue;

                MoveDefinitionSO def = ctx.moveDatabase != null ? ctx.moveDatabase.GetById(moveId) : null;
                rows.Add(new ChainlingPartyMoveModifierRow
                {
                    slotIndex = i,
                    moveName = ResolveMoveName(def, moveId),
                    typeLabel = def != null ? MoveSelectDisplayUtility.FormatTypeName(def.baseType) : "Unknown",
                    categoryLabel = def != null ? def.category.ToString() : "Move",
                    modifier1Label = ResolveModifierLabel(ctx.modifierCatalog, GetModifierId(snapshot.modifier1SectionIds, i)),
                    modifier2Label = ResolveModifierLabel(ctx.modifierCatalog, GetModifierId(snapshot.modifier2SectionIds, i)),
                });
            }

            return rows;
        }

        private static string ResolveMoveName(MoveDefinitionSO def, string moveId)
        {
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return def.displayName;
            return string.IsNullOrWhiteSpace(moveId) ? "Unknown Move" : moveId;
        }

        private static string GetModifierId(IList<string> ids, int index)
        {
            if (ids == null || index < 0 || index >= ids.Count)
                return "";
            return ids[index] ?? "";
        }

        private static string ResolveModifierLabel(ModifierCatalogSO catalog, string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
                return "None";

            if (catalog != null)
            {
                ModifierDefinitionSO def = catalog.GetBySectionId(sectionId);
                if (def != null)
                    return def.GetDisplayName();
            }

            return sectionId;
        }
    }
}
