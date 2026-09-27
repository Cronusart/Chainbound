using System.Collections.Generic;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingPartyReadModelTests
    {
        [Test]
        public void BuildSummary_EmptySlot_IsEmptyWithoutDrillDown()
        {
            var ctx = new ChainlingPartyColumnContext { slotIndex = 0 };
            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Stats, ctx);
            ChainlingPartyColumnDrillDown drillDown =
                ChainlingPartyColumnViewModelBuilder.BuildDrillDown(ChainlingPartyViewMode.Stats, ctx);

            Assert.IsTrue(summary.isEmpty);
            Assert.AreEqual("Assign at a PC.", summary.emptyMessage);
            Assert.AreEqual(ChainlingPartyDrillDownKind.None, drillDown.kind);
        }

        [Test]
        public void BuildSummary_StatsView_ReadsPveServiceRecord()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.pveServiceRecord.totalVictories = 12;
            ctx.entry.pveServiceRecord.enemyKos = 4;
            ctx.entry.pveServiceRecord.xpEarnedLifetime = 900;
            ctx.entry.pveServiceRecord.damageDealt = 34965;
            ctx.entry.pveServiceRecord.damageTaken = 15760;
            ctx.entry.battleStats = new ChainlingBattleStats { timesKod = 17 };

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Stats, ctx);

            Assert.IsFalse(summary.isEmpty);
            Assert.AreEqual(0, summary.topBadges.Count);
            Assert.AreEqual(34965, summary.damageDealt);
            Assert.AreEqual(15760, summary.damageTaken);
            Assert.IsTrue(ContainsLine(summary.lines, "Victories", "12"));
            Assert.IsTrue(ContainsLine(summary.lines, "Enemy KOs", "4"));
            Assert.IsTrue(ContainsLine(summary.lines, "Damage dealt", "34,965"));
            Assert.IsTrue(ContainsLine(summary.lines, "Damage taken", "15,760"));
            Assert.IsTrue(ContainsLine(summary.lines, "XP earned", "900"));
            Assert.IsTrue(ContainsLine(summary.lines, "Times KO'd", "17"));
            Assert.AreEqual("Victories", summary.lines[0].Label);
            Assert.AreEqual("Enemy KOs", summary.lines[1].Label);
            Assert.AreEqual("Damage dealt", summary.lines[2].Label);
            Assert.AreEqual("Damage taken", summary.lines[3].Label);
            Assert.AreEqual("XP earned", summary.lines[4].Label);
            Assert.AreEqual("Times KO'd", summary.lines[5].Label);
        }

        [Test]
        public void BuildDrillDown_StatsView_UnlocksFirstBloodBadge()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.pveServiceRecord.enemyKos = 1;
            ctx.badgeCatalog = CreateBadgeCatalog();

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Stats, ctx);
            ChainlingPartyColumnDrillDown drillDown =
                ChainlingPartyColumnViewModelBuilder.BuildDrillDown(ChainlingPartyViewMode.Stats, ctx);

            Assert.AreEqual(0, summary.topBadges.Count);
            Assert.Greater(drillDown.badges.Count, 0);
            Assert.AreEqual("first_blood", drillDown.badges[0].BadgeId);
            Assert.IsTrue(drillDown.badges[0].Unlocked);
        }

        [Test]
        public void BuildSummary_LoadoutView_ShowsLookCardOnly()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.visualIdentity = ChainlingVisualIdentity.CreateDefault();
            ctx.entry.snapshot.visualIdentity.meleeAttackStyleId = "melee_slash_01";
            ctx.entry.snapshot.visualIdentity.koFxId = "ko_burst_01";

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Loadout, ctx);
            ChainlingPartyColumnDrillDown drillDown =
                ChainlingPartyColumnViewModelBuilder.BuildDrillDown(ChainlingPartyViewMode.Loadout, ctx);

            Assert.IsFalse(summary.isEmpty);
            Assert.AreEqual(5, summary.lines.Count);
            Assert.AreEqual("Variant", summary.lines[0].Label);
            Assert.AreEqual("Aura", summary.lines[1].Label);
            Assert.AreEqual("Party Column", summary.lines[2].Label);
            Assert.AreEqual("Idle", summary.lines[3].Label);
            Assert.AreEqual("Combat FX", summary.lines[4].Label);
            Assert.AreEqual("2 equipped", summary.lines[4].Value);
            Assert.IsFalse(ContainsLabel(summary.lines, "Melee"));
            Assert.IsFalse(ContainsLabel(summary.lines, "HUD Card"));
            Assert.GreaterOrEqual(drillDown.cosmeticSlots.Count, 10);
        }

        [Test]
        public void BuildSummary_LoadoutView_FormatsIdleLoopAndFlourish()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.visualIdentity = ChainlingVisualIdentity.CreateDefault();
            ctx.entry.snapshot.visualIdentity.idleStyleId = "idle_cool";
            ctx.entry.snapshot.visualIdentity.idleFlourishId = "flourish_spin";

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Loadout, ctx);

            Assert.IsTrue(ContainsLine(summary.lines, "Idle", "idle_cool · flourish_spin"));
            Assert.IsTrue(ContainsLine(summary.lines, "Combat FX", "Stock"));
        }

        [Test]
        public void BuildSummary_GeneralView_ExposesStarCount()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.stars = 3;
            ctx.entry.snapshot.rebirthCount = 2;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);

            Assert.AreEqual(3, summary.starCount);
            Assert.AreEqual(2, summary.cycleTier);
            Assert.AreEqual("Cycle III", summary.cycleLabel);
            Assert.IsFalse(ContainsLine(summary.lines, "Stars", "3/5"));
            Assert.IsFalse(ContainsLabel(summary.lines, "Rebirth"));
            Assert.IsFalse(ContainsValueFragment(summary.lines, "Rb"));
        }

        [Test]
        public void BuildSummary_GeneralView_FormatsMaxCycleAtRebirthCap()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.rebirthCount = CombatConstants.REBIRTH_MAX;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);

            Assert.AreEqual(CombatConstants.REBIRTH_MAX, summary.cycleTier);
            Assert.AreEqual(ChainlingPlayerTerms.MaxCycleLabel, summary.cycleLabel);
            Assert.AreEqual("Cycle I", ChainlingPlayerTerms.FormatCycleLabel(0));
            Assert.AreEqual("Cycle V", ChainlingPlayerTerms.FormatCycleLabel(4));
        }

        [Test]
        public void BuildSummary_AttributesView_ExposesRadarAxes()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.stars = 2;
            ctx.entry.snapshot.rebirthCount = 1;
            ctx.entry.snapshot.ivs = new[] { 0, 4, 2, 1, 3, 5 };

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Attributes, ctx);

            Assert.IsFalse(summary.isEmpty);
            Assert.AreEqual(ChainlingType.Meme, summary.primaryType);
            Assert.AreEqual(5, summary.attributeAxes.Count);
            Assert.AreEqual("SPD", summary.attributeAxes[0].Label);
            Assert.AreEqual("MATK", summary.attributeAxes[2].Label);
            Assert.Greater(summary.attributeAxes[2].Total, 0);
            Assert.GreaterOrEqual(summary.attributeAxes[2].Fill01, 0f);
            Assert.LessOrEqual(summary.attributeAxes[2].Fill01, 1f);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.attributeProgressionFooter));
        }

        [Test]
        public void BuildSummary_GeneralView_ProducesXpBarFill()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.masteryXp = 0;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);

            Assert.IsFalse(summary.isEmpty);
            Assert.AreEqual(0f, summary.xpBarFill01, 0.001f);
            Assert.AreEqual("0 / 450 XP", summary.xpLabel);
            Assert.IsTrue(ContainsLine(summary.lines, "Type", "Meme"));
            Assert.IsTrue(ContainsLine(summary.lines, "Variant", "Default"));
            Assert.IsTrue(ContainsLabel(summary.lines, "Next-star folio chance"));
            Assert.IsFalse(ContainsLabel(summary.lines, "Form"));

            ctx.entry.masteryXp = 99999;
            summary = ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);
            Assert.Greater(summary.xpBarFill01, 0f);
            Assert.IsFalse(string.IsNullOrWhiteSpace(summary.xpLabel));
        }

        [Test]
        public void BuildSummary_GeneralView_ResolvesArchiveAndEntangledState()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.nftMintAddress = "CoreAsset111111111111111111111111111111111";
            ctx.entry.snapshot.archivedAtUnix = 1782560000L;
            ctx.entry.snapshot.isEntangled = true;
            ctx.entry.snapshot.entangledType = ChainlingType.Meme;
            ctx.entry.snapshot.visualIdentity.visualVariantId = ChainlingVisualIdentity.BaseVariantId;
            ctx.entry.snapshot.visualIdentity.Normalize();

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);

            Assert.AreEqual(ArchiveBindingState.Anchored, summary.archiveState);
            Assert.IsTrue(summary.isEntangled);
            Assert.AreEqual("Entangled Meme", summary.typeLabel);
            Assert.AreEqual("Default", summary.variantLabel);
            Assert.IsTrue(ContainsLine(summary.lines, "Type", "Entangled Meme"));
            Assert.IsTrue(ContainsLine(summary.lines, "Variant", "Default"));
        }

        [Test]
        public void BuildSummary_FilledColumn_ProducesVitalsFromPersistedHp()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.currentHp = 40;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Moves, ctx);

            Assert.NotNull(summary.vitals);
            Assert.AreEqual(40, summary.vitals.currentHp);
            Assert.AreEqual(100, summary.vitals.maxHp);
            Assert.AreEqual(0.4f, summary.vitals.fill01, 0.001f);
            Assert.AreEqual("40 / 100", summary.vitals.label);
        }

        [Test]
        public void BuildSummary_MovesView_UsesEquippedOnlyRowsAndRebirthCap()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.rebirthCount = 3;
            ctx.entry.snapshot.moveIds = new List<string>
            {
                "baseline_jab",
                "",
                "second_move",
                "third_move",
                "fourth_move",
                "fifth_move",
            };

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Moves, ctx);

            Assert.AreEqual(7, summary.moveMaxSlots);
            Assert.AreEqual(5, summary.moveEquippedCount);
            Assert.AreEqual(5, summary.moveRows.Count);
            Assert.AreEqual("second_move", summary.moveRows[1].moveName);
            Assert.IsFalse(ContainsLine(summary.lines, "Slot 2", "No moves equipped."));
        }

        [Test]
        public void BuildSummary_UnsyncedHp_UsesFullMaxInVitals()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.entry.snapshot.currentHp = ChainlingVitalsConstants.UnsyncedCurrentHp;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.General, ctx);

            Assert.NotNull(summary.vitals);
            Assert.AreEqual(100, summary.vitals.currentHp);
            Assert.AreEqual(100, summary.vitals.maxHp);
            Assert.AreEqual(1f, summary.vitals.fill01, 0.001f);
        }

        [Test]
        public void BuildDrillDown_AttributesView_ProducesFiveSections()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ChainlingPartyColumnDrillDown drillDown =
                ChainlingPartyColumnViewModelBuilder.BuildDrillDown(ChainlingPartyViewMode.Attributes, ctx);

            Assert.AreEqual(ChainlingPartyDrillDownKind.AttributeBreakdown, drillDown.kind);
            Assert.AreEqual(5, drillDown.attributeSections.Count);
        }

        [Test]
        public void BuildSummary_EntryView_PopulatesRoleDescriptionAndStarterEncounters()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.species.isStarter = true;
            ctx.species.description = "A starter signal ape.";
            ctx.species.combatRoleOverride = SpeciesCombatRole.MeleeStriker;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Detail, ctx);

            Assert.IsFalse(summary.isEmpty);
            Assert.AreEqual("Validape", summary.speciesDisplayName);
            Assert.AreEqual(ChainlingType.Meme, summary.primaryType);
            Assert.AreEqual(SpeciesCombatRole.MeleeStriker, summary.combatRole);
            Assert.AreEqual("Melee Striker", summary.combatRoleLabel);
            Assert.AreEqual("A starter signal ape.", summary.descriptionHook);
            Assert.AreEqual("Starter · Loom Lab", summary.encountersSummaryLine);
            Assert.IsTrue(ContainsLine(summary.lines, "Species", "Validape"));
            Assert.IsTrue(ContainsLine(summary.lines, "Preferred type", "Meme"));
            Assert.IsTrue(ContainsLine(summary.lines, "Preferred role", "Melee Striker"));
            Assert.IsTrue(summary.supportsDrillDown);
        }

        [Test]
        public void FormatPreferredTypeValue_JoinsSecondaryWithoutEntangledPrefix()
        {
            Assert.AreEqual(
                "Meme",
                ChainlingPartyDetailDisplayUtility.FormatPreferredTypeValue(
                    ChainlingType.Meme,
                    ChainlingType.None));
            Assert.AreEqual(
                "Meme · Bot",
                ChainlingPartyDetailDisplayUtility.FormatPreferredTypeValue(
                    ChainlingType.Meme,
                    ChainlingType.Bot));
        }

        [Test]
        public void BuildSummary_EntryView_FillsSignatureFromFlagshipMove()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            var flagship = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            flagship.moveId = "flagship_slam";
            flagship.displayName = "Flagship Slam";
            flagship.baseType = ChainlingType.Meme;
            flagship.category = MoveCategory.DamagingMelee;
            flagship.power = 85;
            flagship.rarity = RarityTier.Rare;
            ctx.species.flagshipMove = flagship;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Detail, ctx);

            Assert.NotNull(summary.signatureMove);
            Assert.IsFalse(summary.signatureMove.isEmpty);
            Assert.AreEqual("Flagship Slam", summary.signatureMove.moveName);
            Assert.AreEqual("85", summary.signatureMove.statLabel);
            Assert.AreEqual(ChainlingType.Meme, summary.signatureMove.moveType);
            Assert.AreEqual(RarityTier.Rare, summary.signatureMove.rarity);
        }

        [Test]
        public void BuildSummary_EntryView_EmptyFlagship_UsesEmptySignatureRow()
        {
            ChainlingPartyColumnContext ctx = CreateFilledContext();
            ctx.species.flagshipMove = null;

            ChainlingPartyColumnSummary summary =
                ChainlingPartyColumnViewModelBuilder.BuildSummary(ChainlingPartyViewMode.Detail, ctx);

            Assert.NotNull(summary.signatureMove);
            Assert.IsTrue(summary.signatureMove.isEmpty);
            Assert.AreEqual("No signature authored", summary.signatureMove.moveName);
            Assert.AreEqual("Origins unlogged", summary.encountersSummaryLine);
        }

        [Test]
        public void ViewModeLabels_DetailDisplaysAsEntry()
        {
            Assert.AreEqual("Entry", ChainlingPartyViewModeLabels.GetDisplayName(ChainlingPartyViewMode.Detail));
            Assert.AreEqual("Moves", ChainlingPartyViewModeLabels.GetDisplayName(ChainlingPartyViewMode.Moves));
        }

        private static bool ContainsLine(List<ChainlingPartyDisplayLine> lines, string label, string value)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Label == label && lines[i].Value == value)
                    return true;
            }

            return false;
        }

        private static bool ContainsLabel(List<ChainlingPartyDisplayLine> lines, string label)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Label == label)
                    return true;
            }

            return false;
        }

        private static bool ContainsValueFragment(List<ChainlingPartyDisplayLine> lines, string fragment)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!string.IsNullOrEmpty(lines[i].Value) && lines[i].Value.Contains(fragment))
                    return true;
            }

            return false;
        }

        private static ChainlingPartyColumnContext CreateFilledContext()
        {
            var species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "validape";
            species.displayName = "Validape";
            species.description = "A starter signal ape.";
            species.baseMeleeAtk = 20;
            species.baseMeleeDef = 18;
            species.baseRangedAtk = 15;
            species.baseRangedDef = 14;
            species.baseSpeed = 22;
            species.baseMaxHP = 100;
            species.primaryType = ChainlingType.Meme;

            var entry = new PlayerRosterEntry
            {
                masteryXp = 25,
                snapshot = new ChainlingSnapshot
                {
                    speciesId = "validape",
                    stars = 1,
                    rebirthCount = 0,
                    moveIds = new List<string> { "baseline_jab" },
                    visualIdentity = ChainlingVisualIdentity.CreateDefault(),
                },
                pveServiceRecord = new ChainlingPveServiceRecord(),
            };

            return new ChainlingPartyColumnContext
            {
                entry = entry,
                species = species,
                slotIndex = 0,
                progressionConfig = ProgressionConfigSO.CreateRuntimeDefault(),
            };
        }

        private static ChainlingBadgeCatalogSO CreateBadgeCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<ChainlingBadgeCatalogSO>();
            var firstBlood = ScriptableObject.CreateInstance<ChainlingBadgeDefinitionSO>();
            firstBlood.badgeId = "first_blood";
            firstBlood.displayName = "First Blood";
            firstBlood.metricKey = ChainlingMetricIds.PveEnemyKos;
            firstBlood.threshold = 1;

            SetCatalogBadges(catalog, new List<ChainlingBadgeDefinitionSO> { firstBlood });
            return catalog;
        }

        private static void SetCatalogBadges(ChainlingBadgeCatalogSO catalog, List<ChainlingBadgeDefinitionSO> badges)
        {
            var field = typeof(ChainlingBadgeCatalogSO).GetField(
                "badges",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(catalog, badges);
        }
    }
}
