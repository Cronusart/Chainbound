using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class StampModifierPipelineTests
    {
        [Test]
        public void IllegalHosts_AreRejectedByCentralValidation()
        {
            MoveDefinitionSO spiteCounter = CreateMove("spite_counter", "Spite Counter", MoveCategory.Passive);
            MoveDefinitionSO evasionManeuver = CreateMove("evasion_maneuver", "Evasion Maneuver", MoveCategory.StatusUtility);
            ModifierDefinitionSO powerVariance = CreateModifier("10.1-c", 20f, 30f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged);
            ModifierDefinitionSO lifesteal = CreateModifier("6.1-d", 5f, 8f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged);

            Assert.IsFalse(ModifierValidationService.CanApply(powerVariance, spiteCounter).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(lifesteal, evasionManeuver).isValid);
        }

        [Test]
        public void PassA_IllegalHosts_AreRejected()
        {
            MoveDefinitionSO damaging = CreateMove("slash", "Slash", MoveCategory.DamagingMelee);
            MoveDefinitionSO utility = CreateMove("glitch", "Glitch", MoveCategory.StatusUtility);
            MoveDefinitionSO support = CreateMove("mend", "Mend", MoveCategory.Support);
            MoveDefinitionSO passive = CreateMove("spite_counter", "Spite Counter", MoveCategory.Passive);

            ModifierDefinitionSO firstDibs = CreateModifier(
                "1.3-a", 5f, 12f, MoveCategory.StatusUtility, MoveCategory.Support, MoveCategory.Passive);
            firstDibs.slotType = ModifierSlotType.INIT;
            firstDibs.forbiddenCombinations = new[] { "1.1-a", "1.2-a", "1.3-b", "4.3-a", "4.3-b" };
            ModifierDefinitionSO quietPriority = CreateModifier("1.3-b", 5f, 8f, MoveCategory.StatusUtility);
            quietPriority.slotType = ModifierSlotType.INIT;
            ModifierDefinitionSO patchKit = CreateModifier("6.8-a", 5f, 12f, MoveCategory.StatusUtility, MoveCategory.Support);
            ModifierDefinitionSO stickyMirror = CreateModifier("6.8-d", 5f, 15f, MoveCategory.Passive);
            ModifierDefinitionSO ignoreWalls = CreateModifier(
                "4.8-a", 0f, 0f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged);
            ModifierDefinitionSO sharedBuff = CreateModifier("7.3-a", 0f, 0f, MoveCategory.StatusUtility, MoveCategory.Support);
            ModifierDefinitionSO sparseFocus = CreateModifier("2.7-a", 3f, 8f);

            Assert.IsFalse(ModifierValidationService.CanApply(firstDibs, damaging).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(firstDibs, utility).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(firstDibs, passive).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(quietPriority, support).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(quietPriority, passive).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(quietPriority, utility).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(patchKit, damaging).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(patchKit, passive).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(patchKit, support).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(stickyMirror, utility).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(stickyMirror, passive).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(ignoreWalls, utility).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(ignoreWalls, damaging).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(sharedBuff, damaging).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(sharedBuff, utility).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(sparseFocus, damaging).isValid);
            Assert.IsTrue(ModifierValidationService.CanApply(sparseFocus, utility).isValid);

            ModifierDefinitionSO hairTrigger = CreateModifier(
                "1.1-a", 8f, 12f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged);
            hairTrigger.slotType = ModifierSlotType.INIT;
            Assert.IsFalse(ModifierValidationService.CanApply(
                firstDibs,
                utility,
                existingModifierSectionIds: new[] { "1.1-a" }).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(
                firstDibs,
                utility,
                CreateCatalog(hairTrigger),
                new[] { "1.1-a" }).isValid);
        }

        [Test]
        public void StatSubstitution_AllowsOneOnDamagingMovesOnly()
        {
            ModifierDefinitionSO ironFist = CreateModifier("5.2-a", 0f, 0f, MoveCategory.DamagingMelee);
            ironFist.slotType = ModifierSlotType.StatSubstitution;
            ModifierDefinitionSO fleetStrike = CreateModifier("5.2-b", 0f, 0f, MoveCategory.DamagingMelee);
            fleetStrike.slotType = ModifierSlotType.StatSubstitution;
            MoveDefinitionSO melee = CreateMove("jab", "Jab", MoveCategory.DamagingMelee);
            MoveDefinitionSO utility = CreateMove("cleanse", "Cleanse", MoveCategory.StatusUtility);

            Assert.IsTrue(ModifierValidationService.CanApply(ironFist, melee).isValid);
            Assert.IsFalse(ModifierValidationService.CanApply(ironFist, utility).isValid);

            ModifierValidationResult second = ModifierValidationService.CanApply(
                fleetStrike,
                melee,
                CreateCatalog(ironFist),
                new[] { "5.2-a" });
            Assert.IsFalse(second.isValid);
            StringAssert.Contains("StatSubstitution", second.reason);
        }

        [Test]
        public void RevealFormatter_SubstitutesLockedTokenN()
        {
            ModifierDefinitionSO definition = CreateModifier("1.3-a", 5f, 12f, MoveCategory.StatusUtility);
            definition.displayName = "First Dibs";
            definition.effectDescription = "{n}% chance to act first within speed tier";

            string formatted = CandyMachineResultView.FormatModifierEffect("1.3-a", definition, 8f, true);

            StringAssert.Contains("First Dibs", formatted);
            StringAssert.Contains("8%", formatted);
            StringAssert.DoesNotContain("{n}", formatted);
        }

        [Test]
        public void SameSeed_ProducesSameModifierAndLockedValue()
        {
            TestData data = CreateDamagingTestData();
            byte[] seed = Encoding.UTF8.GetBytes("same-seed");

            Assert.IsTrue(Generate(data, seed, out IReadOnlyList<StampedModifierRoll> first, out string firstError), firstError);
            Assert.IsTrue(Generate(data, seed, out IReadOnlyList<StampedModifierRoll> second, out string secondError), secondError);
            Assert.AreEqual(first[0].sectionId, second[0].sectionId);
            Assert.AreEqual(first[0].effectValue, second[0].effectValue);
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentLockedValuesWithinInclusiveRange()
        {
            TestData data = CreateDamagingTestData(singleModifier: true);
            var observed = new HashSet<float>();
            for (int i = 0; i < 16; i++)
            {
                Assert.IsTrue(Generate(
                    data,
                    Encoding.UTF8.GetBytes($"seed-{i}"),
                    out IReadOnlyList<StampedModifierRoll> rolls,
                    out string error), error);
                Assert.AreEqual("6.1-d", rolls[0].sectionId);
                Assert.That(rolls[0].effectValue, Is.InRange(5f, 8f));
                observed.Add(rolls[0].effectValue);
            }
            Assert.Greater(observed.Count, 1);
        }

        [Test]
        public void EmptyEligiblePool_FailsClearlyWithoutIllegalFallback()
        {
            MoveDefinitionSO move = CreateMove("spite_counter", "Spite Counter", MoveCategory.Passive);
            ModifierCatalogSO catalog = CreateCatalog(
                CreateModifier("10.1-c", 20f, 30f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged));
            VendingConfigSO config = CommonOnlyConfig();

            bool success = MoveModifierGenerator.TryGenerateStampedModifiersFromVrf(
                Encoding.UTF8.GetBytes("seed"),
                2,
                MoveDatabaseSO.HashMoveId(move.moveId),
                Encoding.UTF8.GetBytes("owner"),
                Encoding.UTF8.GetBytes("context"),
                catalog,
                move,
                1,
                out _,
                out string reason,
                vendingConfig: config);

            Assert.IsFalse(success);
            StringAssert.Contains("No eligible modifier", reason);
        }

        [Test]
        public void InvalidAttemptBudget_FailsClearly()
        {
            TestData data = CreateDamagingTestData(singleModifier: true);
            bool success = MoveModifierGenerator.TryGenerateStampedModifiersFromVrf(
                Encoding.UTF8.GetBytes("seed"),
                2,
                MoveDatabaseSO.HashMoveId(data.move.moveId),
                Encoding.UTF8.GetBytes("owner"),
                Encoding.UTF8.GetBytes("context"),
                data.catalog,
                data.move,
                1,
                out _,
                out string reason,
                maxAttempts: 0,
                vendingConfig: data.config);

            Assert.IsFalse(success);
            StringAssert.Contains("Max attempts", reason);
        }

        [Test]
        public void RevealFormatter_UsesLockedValueInsteadOfRawRange()
        {
            ModifierDefinitionSO definition = CreateModifier("6.1-d", 5f, 8f, MoveCategory.DamagingMelee);
            definition.effectDescription = "Heal 5-8% of damage dealt as HP";

            string formatted = CandyMachineResultView.FormatModifierEffect("6.1-d", definition, 6.25f, true);

            StringAssert.Contains("6.25%", formatted);
            StringAssert.DoesNotContain("5-8%", formatted);
        }

        [Test]
        public void PersistedLockedValue_BecomesRuntimeModifierValue()
        {
            MoveDefinitionSO move = CreateMove("slash", "Slash", MoveCategory.DamagingMelee);
            MoveDatabaseSO database = CreateDatabase(move);
            var item = new MenuMoveItem
            {
                moveItemId = "stamped-1",
                moveId = move.moveId,
                tier = MoveTier.Stamped,
                modifier1SectionId = "6.1-d",
                modifier1EffectValue = 6.75f,
                modifier1EffectValueLocked = true,
            };

            MoveInstance instance = item.ToMoveInstance(database);

            Assert.AreEqual(1, instance.modifiers.Count);
            Assert.AreEqual("6.1-d", instance.modifiers[0].definitionSectionId);
            Assert.AreEqual(6.75f, instance.modifiers[0].effectValue);
            Assert.IsTrue(instance.modifiers[0].effectValueLocked);
            Assert.AreEqual(instance.modifiers[0].effectValue, instance.modifiers[0].effectValueMax);
        }

        [Test]
        public async Task DevInventory_UsesValidationAndLocksEffectValue()
        {
            MoveDefinitionSO host = CreateMove("spite_counter", "Spite Counter", MoveCategory.Passive);
            MoveDatabaseSO database = CreateDatabase(host);
            ModifierDefinitionSO illegal = CreateModifier("10.1-c", 20f, 30f, MoveCategory.DamagingMelee, MoveCategory.DamagingRanged);
            ModifierDefinitionSO legal = CreateModifier("8.1-a", 5f, 10f, MoveCategory.Passive);
            ModifierCatalogSO catalog = CreateCatalog(illegal, legal);
            var inventory = new DevCandyMachineInventory(CommonOnlyConfig(), database, catalog, 99);
            inventory.GrantRam(1000);
            KnownMoveRecord known = inventory.GrantKnown(host.moveId);

            StampMoveResult result = await inventory.StampMoveAsync(new StampMoveRequest
            {
                knownInstanceId = known.instanceId,
                requestedModifierCount = 1,
                idempotencyKey = "validation-lock-test",
            });

            Assert.IsTrue(result.success, result.error);
            Assert.AreEqual("8.1-a", result.verifiedAsset.modifier1Id);
            Assert.IsTrue(result.verifiedAsset.modifier1EffectValueLocked);
            Assert.That(result.verifiedAsset.modifier1EffectValue, Is.InRange(5f, 10f));
        }

        private static bool Generate(
            TestData data,
            byte[] seed,
            out IReadOnlyList<StampedModifierRoll> rolls,
            out string error)
        {
            return MoveModifierGenerator.TryGenerateStampedModifiersFromVrf(
                seed,
                2,
                MoveDatabaseSO.HashMoveId(data.move.moveId),
                Encoding.UTF8.GetBytes("owner"),
                Encoding.UTF8.GetBytes("context"),
                data.catalog,
                data.move,
                1,
                out rolls,
                out error,
                vendingConfig: data.config);
        }

        private static TestData CreateDamagingTestData(bool singleModifier = false)
        {
            MoveDefinitionSO move = CreateMove("slash", "Slash", MoveCategory.DamagingMelee);
            ModifierDefinitionSO ranged = CreateModifier("6.1-d", 5f, 8f, MoveCategory.DamagingMelee);
            ModifierCatalogSO catalog = singleModifier
                ? CreateCatalog(ranged)
                : CreateCatalog(ranged, CreateModifier("2.1-a", 10f, 20f, MoveCategory.DamagingMelee));
            return new TestData(move, catalog, CommonOnlyConfig());
        }

        private static MoveDefinitionSO CreateMove(string id, string name, MoveCategory category)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = id;
            move.displayName = name;
            move.category = category;
            move.tags = new List<string>();
            return move;
        }

        private static ModifierDefinitionSO CreateModifier(
            string id,
            float min,
            float max,
            params MoveCategory[] allowed)
        {
            ModifierDefinitionSO modifier = ScriptableObject.CreateInstance<ModifierDefinitionSO>();
            modifier.sectionId = id;
            modifier.effectMin = min;
            modifier.effectMax = max;
            modifier.minimumRarity = RarityTier.Common;
            modifier.weight = 1;
            modifier.allowedCategories = allowed;
            return modifier;
        }

        private static ModifierCatalogSO CreateCatalog(params ModifierDefinitionSO[] definitions)
        {
            ModifierCatalogSO catalog = ScriptableObject.CreateInstance<ModifierCatalogSO>();
            catalog.modifiers = new List<ModifierDefinitionSO>(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        private static MoveDatabaseSO CreateDatabase(params MoveDefinitionSO[] definitions)
        {
            MoveDatabaseSO database = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            database.moves = new List<MoveDefinitionSO>(definitions);
            database.BuildLookup();
            return database;
        }

        private static VendingConfigSO CommonOnlyConfig()
        {
            VendingConfigSO config = VendingConfigSO.CreateRuntimeDefault();
            config.rarityTierWeightsPercent = new[] { 100f, 0f, 0f, 0f, 0f, 0f };
            config.ramCostStampKnown = 0;
            return config;
        }

        private readonly struct TestData
        {
            public readonly MoveDefinitionSO move;
            public readonly ModifierCatalogSO catalog;
            public readonly VendingConfigSO config;

            public TestData(MoveDefinitionSO move, ModifierCatalogSO catalog, VendingConfigSO config)
            {
                this.move = move;
                this.catalog = catalog;
                this.config = config;
            }
        }
    }
}
