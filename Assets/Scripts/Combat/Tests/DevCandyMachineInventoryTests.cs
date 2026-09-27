using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class DevCandyMachineInventoryTests
    {
        [Test]
        public void GrantRamAndKnown_AreLocalOnly()
        {
            DevCandyMachineInventory inventory = CreateInventory(ramCostKnown: 0, ramCostStamp: 75);
            inventory.GrantRam(200);
            KnownMoveRecord granted = inventory.GrantKnown("slash");
            Assert.AreEqual(200, inventory.RamBalance);
            Assert.AreEqual("slash", granted.moveId);
            Assert.AreEqual(0, granted.modifierCount);
            Assert.AreEqual(1, inventory.CachedKnown.Count);
        }

        [Test]
        public async Task GetKnown_SpendsRamAndAddsKnown()
        {
            DevCandyMachineInventory inventory = CreateInventory(ramCostKnown: 10, ramCostStamp: 75);
            inventory.GrantRam(50);
            KnownMoveRecord rolled = await inventory.GetKnownAsync("roll-1");
            Assert.AreEqual(40, inventory.RamBalance);
            Assert.AreEqual(1, inventory.CachedKnown.Count);
            Assert.AreEqual(0, rolled.modifierCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(rolled.moveId));
        }

        [Test]
        public async Task Stamp_ConsumesKnownAndAttachesModifiers()
        {
            DevCandyMachineInventory inventory = CreateInventory(ramCostKnown: 0, ramCostStamp: 75);
            inventory.GrantRam(100);
            KnownMoveRecord known = inventory.GrantKnown("slash");
            StampTransactionReview review = await inventory.ReviewStampAsync(new StampMoveRequest
            {
                knownInstanceId = known.instanceId,
                requestedModifierCount = 2,
                idempotencyKey = "stamp-1",
            });
            Assert.IsTrue(review.simulationSucceeded);
            Assert.AreEqual(75, review.ramCost);

            StampMoveResult result = await inventory.StampMoveAsync(new StampMoveRequest
            {
                knownInstanceId = known.instanceId,
                requestedModifierCount = 2,
                idempotencyKey = "stamp-1",
            });
            Assert.IsTrue(result.success);
            Assert.AreEqual(0, inventory.CachedKnown.Count);
            Assert.AreEqual(1, inventory.CachedStamped.Count);
            Assert.AreEqual(25, inventory.RamBalance);
            Assert.AreEqual(2, result.verifiedAsset.modifierCount);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.verifiedAsset.modifier1Id));
            Assert.IsTrue(result.verifiedAsset.modifier1EffectValueLocked);
            Assert.That(result.verifiedAsset.modifier1EffectValue, Is.InRange(5f, 8f));
        }

        [Test]
        public void GetKnown_FailsWithoutEnoughRam()
        {
            DevCandyMachineInventory inventory = CreateInventory(ramCostKnown: 25, ramCostStamp: 75);
            Assert.Throws<System.InvalidOperationException>(() => inventory.GetKnownAsync("roll-1"));
        }

        private static DevCandyMachineInventory CreateInventory(int ramCostKnown, int ramCostStamp)
        {
            VendingConfigSO config = VendingConfigSO.CreateRuntimeDefault();
            config.ramCostGetKnown = ramCostKnown;
            config.ramCostStampKnown = ramCostStamp;
            config.rarityTierWeightsPercent = new[] { 100f, 0f, 0f, 0f, 0f, 0f };

            MoveDefinitionSO slash = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            slash.moveId = "slash";
            slash.displayName = "Slash";
            slash.category = MoveCategory.DamagingMelee;
            slash.rarity = RarityTier.Common;
            MoveDefinitionSO beam = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            beam.moveId = "beam";
            beam.displayName = "Beam";
            beam.category = MoveCategory.DamagingRanged;
            beam.rarity = RarityTier.Rare;
            MoveDatabaseSO moveDatabase = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            moveDatabase.moves = new List<MoveDefinitionSO> { slash, beam };
            moveDatabase.BuildLookup();

            var modifiers = new List<ModifierDefinitionSO>
            {
                CreateModifier("2.1-a", 5f, 8f),
                CreateModifier("2.2-b", 5f, 8f),
                CreateModifier("3.1-c", 5f, 8f),
            };
            ModifierCatalogSO modifierCatalog = ScriptableObject.CreateInstance<ModifierCatalogSO>();
            modifierCatalog.modifiers = modifiers;
            modifierCatalog.BuildLookup();
            return new DevCandyMachineInventory(config, moveDatabase, modifierCatalog, 4);
        }

        private static ModifierDefinitionSO CreateModifier(string id, float min, float max)
        {
            ModifierDefinitionSO modifier = ScriptableObject.CreateInstance<ModifierDefinitionSO>();
            modifier.sectionId = id;
            modifier.effectMin = min;
            modifier.effectMax = max;
            modifier.minimumRarity = RarityTier.Common;
            modifier.weight = 1;
            modifier.allowedCategories = new[] { MoveCategory.DamagingMelee, MoveCategory.DamagingRanged };
            return modifier;
        }
    }
}
