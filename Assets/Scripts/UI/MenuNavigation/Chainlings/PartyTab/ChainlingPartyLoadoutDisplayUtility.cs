using System.Collections.Generic;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPartyLoadoutDisplayUtility
    {
        public const string VariantLabel = "Variant";
        public const string AuraLabel = "Aura";
        public const string PartyColumnLabel = "Party Column";
        public const string IdleLabel = "Idle";
        public const string CombatFxLabel = "Combat FX";

        private static readonly SlotBinding[] DrillDownSlots =
        {
            new SlotBinding(VariantLabel, identity => identity != null ? identity.visualVariantId : ChainlingVisualIdentity.BaseVariantId, ResolveVariantLabel),
            new SlotBinding(AuraLabel, identity => identity != null ? identity.auraId : ChainlingVisualIdentity.NoneId, ResolveAuraLabel),
            new SlotBinding("HUD Card", identity => identity != null ? identity.hudCardVariantId : ChainlingVisualIdentity.DefaultId, ResolveHudCardLabel),
            new SlotBinding(PartyColumnLabel, identity => identity != null ? identity.partyColumnVariantId : ChainlingVisualIdentity.DefaultId, ResolvePartyColumnLabel),
            new SlotBinding("Idle Loop", identity => identity != null ? identity.idleStyleId : ChainlingVisualIdentity.NoneId, ResolveIdleLoopLabel),
            new SlotBinding("Idle Flourish", identity => identity != null ? identity.idleFlourishId : ChainlingVisualIdentity.NoneId, ResolveIdleFlourishLabel),
            new SlotBinding("Melee", identity => identity != null ? identity.meleeAttackStyleId : ChainlingVisualIdentity.NoneId, ResolveMeleeLabel),
            new SlotBinding("Ranged", identity => identity != null ? identity.rangedAttackStyleId : ChainlingVisualIdentity.NoneId, ResolveRangedLabel),
            new SlotBinding("Hit Reaction", identity => identity != null ? identity.hitReactionStyleId : ChainlingVisualIdentity.NoneId, ResolveHitReactionLabel),
            new SlotBinding("Swap In", identity => identity != null ? identity.swapInFxId : ChainlingVisualIdentity.DefaultId, ResolveSwapInLabel),
            new SlotBinding("Swap Out", identity => identity != null ? identity.swapOutFxId : ChainlingVisualIdentity.DefaultId, ResolveSwapOutLabel),
            new SlotBinding("KO", identity => identity != null ? identity.koFxId : ChainlingVisualIdentity.NoneId, ResolveKoLabel),
            new SlotBinding("Victory", identity => identity != null ? identity.victoryPoseId : ChainlingVisualIdentity.DefaultId, ResolveVictoryLabel),
        };

        private static readonly SlotBinding[] CombatFxSlots =
        {
            new SlotBinding("Melee", identity => identity != null ? identity.meleeAttackStyleId : ChainlingVisualIdentity.NoneId, ResolveMeleeLabel),
            new SlotBinding("Ranged", identity => identity != null ? identity.rangedAttackStyleId : ChainlingVisualIdentity.NoneId, ResolveRangedLabel),
            new SlotBinding("Hit Reaction", identity => identity != null ? identity.hitReactionStyleId : ChainlingVisualIdentity.NoneId, ResolveHitReactionLabel),
            new SlotBinding("Swap In", identity => identity != null ? identity.swapInFxId : ChainlingVisualIdentity.DefaultId, ResolveSwapInLabel),
            new SlotBinding("Swap Out", identity => identity != null ? identity.swapOutFxId : ChainlingVisualIdentity.DefaultId, ResolveSwapOutLabel),
            new SlotBinding("KO", identity => identity != null ? identity.koFxId : ChainlingVisualIdentity.NoneId, ResolveKoLabel),
            new SlotBinding("Victory", identity => identity != null ? identity.victoryPoseId : ChainlingVisualIdentity.DefaultId, ResolveVictoryLabel),
        };

        public static void PopulateSummary(ChainlingPartyColumnSummary summary, ChainlingPartyColumnContext ctx)
        {
            summary.lines.Clear();
            if (!ctx.HasFilledChainling)
                return;

            ChainlingVisualIdentity identity = ctx.entry.snapshot.visualIdentity;

            summary.lines.Add(new ChainlingPartyDisplayLine(
                VariantLabel,
                ResolveVariantLabel(ctx, identity != null ? identity.visualVariantId : ChainlingVisualIdentity.BaseVariantId)));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                AuraLabel,
                ResolveAuraLabel(ctx, identity != null ? identity.auraId : ChainlingVisualIdentity.NoneId)));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                PartyColumnLabel,
                ResolvePartyColumnLabel(
                    ctx,
                    identity != null ? identity.partyColumnVariantId : ChainlingVisualIdentity.DefaultId)));
            summary.lines.Add(new ChainlingPartyDisplayLine(IdleLabel, FormatIdleSummary(ctx, identity)));
            summary.lines.Add(new ChainlingPartyDisplayLine(
                CombatFxLabel,
                FormatCombatFxFooter(ctx, identity),
                true));

            summary.supportsDrillDown = true;
        }

        public static void PopulateDrillDown(ChainlingPartyColumnDrillDown drillDown, ChainlingPartyColumnContext ctx)
        {
            drillDown.kind = ChainlingPartyDrillDownKind.CosmeticPreview;
            drillDown.cosmeticSlots.Clear();
            if (!ctx.HasFilledChainling)
                return;

            for (int i = 0; i < DrillDownSlots.Length; i++)
                drillDown.cosmeticSlots.Add(BuildSlotRow(ctx, DrillDownSlots[i]));
        }

        private static string FormatIdleSummary(ChainlingPartyColumnContext ctx, ChainlingVisualIdentity identity)
        {
            string loopId = identity != null ? identity.idleStyleId : ChainlingVisualIdentity.NoneId;
            string flourishId = identity != null ? identity.idleFlourishId : ChainlingVisualIdentity.NoneId;
            bool hasLoop = !IsDefaultOrNone(loopId, null);
            bool hasFlourish = !IsDefaultOrNone(flourishId, null);

            if (!hasLoop && !hasFlourish)
                return "Stock";

            string loopLabel = hasLoop ? ResolveIdleLoopLabel(ctx, loopId) : null;
            string flourishLabel = hasFlourish ? ResolveIdleFlourishLabel(ctx, flourishId) : null;

            if (hasLoop && hasFlourish)
                return $"{loopLabel} · {flourishLabel}";
            if (hasLoop)
                return loopLabel;
            return flourishLabel;
        }

        private static string FormatCombatFxFooter(ChainlingPartyColumnContext ctx, ChainlingVisualIdentity identity)
        {
            int equipped = 0;
            for (int i = 0; i < CombatFxSlots.Length; i++)
            {
                SlotBinding binding = CombatFxSlots[i];
                string id = binding.idSelector(identity);
                string defaultId = ResolveDefaultIdForCombatSlot(binding.slotLabel);
                if (!IsDefaultOrNone(id, defaultId))
                    equipped++;
            }

            return equipped <= 0 ? "Stock" : $"{equipped} equipped";
        }

        private static string ResolveDefaultIdForCombatSlot(string slotLabel)
        {
            switch (slotLabel)
            {
                case "Swap In":
                case "Swap Out":
                case "Victory":
                    return ChainlingVisualIdentity.DefaultId;
                default:
                    return null;
            }
        }

        private static ChainlingPartyCosmeticSlotRow BuildSlotRow(ChainlingPartyColumnContext ctx, SlotBinding binding)
        {
            ChainlingVisualIdentity identity = ctx.entry.snapshot.visualIdentity;
            string rawId = binding.idSelector(identity);
            return new ChainlingPartyCosmeticSlotRow
            {
                slotLabel = binding.slotLabel,
                equippedLabel = binding.labelResolver(ctx, rawId),
            };
        }

        private static string ResolveVariantLabel(ChainlingPartyColumnContext ctx, string id)
        {
            if (IsDefaultOrNone(id, ChainlingVisualIdentity.BaseVariantId))
                return "Default";

            if (ctx.visualDatabase != null && ctx.entry != null && ctx.entry.snapshot != null)
            {
                CombatVisualProfileSO profile = ctx.visualDatabase.GetProfile(ctx.entry.snapshot.speciesId, id);
                if (profile != null && !string.IsNullOrWhiteSpace(profile.displayName))
                    return profile.displayName;
            }

            return FormatEquipped(id);
        }

        private static string ResolveAuraLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetAura(id) : null, id);

        private static string ResolveHudCardLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(
                ctx.identityDatabase != null ? ctx.identityDatabase.GetHudCardVariant(id) : null,
                id,
                ChainlingVisualIdentity.DefaultId);

        private static string ResolvePartyColumnLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(
                ctx.identityDatabase != null ? ctx.identityDatabase.GetPartyColumnVariant(id) : null,
                id,
                ChainlingVisualIdentity.DefaultId);

        private static string ResolveIdleLoopLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetIdleStyle(id) : null, id);

        private static string ResolveIdleFlourishLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetIdleFlourish(id) : null, id);

        private static string ResolveMeleeLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetMeleeAttackStyle(id) : null, id);

        private static string ResolveRangedLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetRangedAttackStyle(id) : null, id);

        private static string ResolveHitReactionLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetHitReactionStyle(id) : null, id);

        private static string ResolveSwapOutLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(
                ctx.identityDatabase != null ? ctx.identityDatabase.GetSwapOutFx(id) : null,
                id,
                ChainlingVisualIdentity.DefaultId);

        private static string ResolveKoLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(ctx.identityDatabase != null ? ctx.identityDatabase.GetKoFx(id) : null, id);

        private static string ResolveVictoryLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(
                ctx.identityDatabase != null ? ctx.identityDatabase.GetVictoryPose(id) : null,
                id,
                ChainlingVisualIdentity.DefaultId);

        private static string ResolveSwapInLabel(ChainlingPartyColumnContext ctx, string id) =>
            ResolveDefinitionLabel(
                ctx.identityDatabase != null ? ctx.identityDatabase.GetSwapInFx(id) : null,
                id,
                ChainlingVisualIdentity.DefaultId);

        private static string ResolveDefinitionLabel(
            ChainlingVisualDefinitionSO definition,
            string id,
            string defaultId = null)
        {
            if (IsDefaultOrNone(id, defaultId))
                return defaultId != null ? "Default" : "None";

            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
                return definition.DisplayName;

            return FormatEquipped(id);
        }

        private static bool IsDefaultOrNone(string id, string defaultId)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                string.Equals(id, ChainlingVisualIdentity.NoneId, System.StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(defaultId) &&
                   string.Equals(id, defaultId, System.StringComparison.Ordinal);
        }

        private static string FormatEquipped(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "None" : id;
        }

        private readonly struct SlotBinding
        {
            public readonly string slotLabel;
            public readonly System.Func<ChainlingVisualIdentity, string> idSelector;
            public readonly System.Func<ChainlingPartyColumnContext, string, string> labelResolver;

            public SlotBinding(
                string slotLabel,
                System.Func<ChainlingVisualIdentity, string> idSelector,
                System.Func<ChainlingPartyColumnContext, string, string> labelResolver)
            {
                this.slotLabel = slotLabel;
                this.idSelector = idSelector;
                this.labelResolver = labelResolver;
            }
        }
    }
}
