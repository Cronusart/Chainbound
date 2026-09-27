using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class DefenseBalanceSystemsTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void CycleHp_UsesFivePercentOfSpeciesBase()
        {
            // base 120, Cycle 3, 2 MaxHP picks → auto +18, pick +12
            int[] picks = { 2, 0, 0, 0, 0, 0 };
            Assert.AreEqual(18, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, 3, 120));
            Assert.AreEqual(12, ChainlingProgressionUtility.GetCyclePickFlat(CoreStat.MaxHP, picks, 120));
            Assert.AreEqual(150, ChainlingProgressionUtility.ComputeProgressedStat(120, 0, CoreStat.MaxHP, 3, picks));
        }

        [Test]
        public void Mitigation_SingleSourceAboveLegacyCap_IsNotClampedToThirtyPercent()
        {
            var defender = CombatFactory.CreateTestChainling("Tank", ChainlingType.Node, hp: 200, mDef: 40);
            defender.AddMitigationSource(0.5f, turnsRemaining: 2, hitsRemaining: -1, sourceTag: "heavy", stackKey: "heavy");
            Assert.AreEqual(0.5f, defender.GetTotalMitigation(), 0.001f);
        }

        [Test]
        public void Mitigation_MultiSourceProduct_StacksWithoutPerSourceThirtyCap()
        {
            var defender = CombatFactory.CreateTestChainling("Tank", ChainlingType.Node, hp: 200);
            defender.AddMitigationSource(0.4f, 2, -1, "a", "a");
            defender.AddMitigationSource(0.4f, 2, -1, "b", "b");

            // 1 - (0.6 * 0.6) = 0.64
            Assert.AreEqual(0.64f, defender.GetTotalMitigation(), 0.001f);
        }

        [Test]
        public void MitigationConstants_UncappedWithFloor()
        {
            Assert.AreEqual(0.10f, CombatConstants.MITIGATION_FLOOR, 0.0001f);
            Assert.AreEqual(1.0f, CombatConstants.MITIGATION_SINGLE_SOURCE_CAP, 0.0001f);
            Assert.IsTrue(CombatConstants.EXCLUSIVE_BARRIERS_AND_WALLS);
        }

        [Test]
        public void AddBarrier_ReplacesPriorBarrierWhenExclusive()
        {
            var unit = CombatFactory.CreateTestChainling("Wall", ChainlingType.Frozen);
            bool firstReplace = unit.AddBarrier(BarrierType.SingleHit, 1, 0, 3, "first");
            Assert.IsFalse(firstReplace);
            Assert.AreEqual(1, unit.barriers.Count);

            bool replaced = unit.AddBarrier(BarrierType.DamageThreshold, 0, 40, 2, "second");
            Assert.IsTrue(replaced);
            Assert.AreEqual(1, unit.barriers.Count);
            Assert.AreEqual(BarrierType.DamageThreshold, unit.barriers[0].barrierType);
            Assert.AreEqual("second", unit.barriers[0].sourceTag);
        }

        [Test]
        public void PassiveMaxHpUtility_SumsAndClampsPercentBonus()
        {
            var chainling = CombatFactory.CreateTestChainling("Dur", ChainlingType.Bot, hp: 100);
            chainling.moves.Add(new MoveInstance
            {
                definitionId = "passive_a",
                category = MoveCategory.Passive,
                modifier1SectionId = "8.1-e",
            });
            chainling.moves.Add(new MoveInstance
            {
                definitionId = "passive_b",
                category = MoveCategory.Passive,
                modifier1SectionId = "8.1-d",
            });

            var catalog = CreateCatalog(
                Durability("8.1-d", 18f, 25f),
                Durability("8.1-e", 25f, 33f));

            int bonus = PassiveMaxHpUtility.ComputeFlatBonus(chainling, catalog);
            Assert.AreEqual(50, bonus); // 50% clamp of base 100
        }

        [Test]
        public void OnHitLifesteal_FamilySection_HealsAttacker()
        {
            var catalog = CreateCatalog(Modifier(
                "6.1-e",
                ModifierCategory.TRG,
                "Heal 8-12% of damage dealt as HP",
                100f,
                100f,
                ModifierTriggerCondition.OnHit));

            var resolver = CreateResolver(catalog);
            var attacker = CombatFactory.CreateTestChainling("Leech", ChainlingType.Burned, hp: 100, mAtk: 80, spd: 30);
            var target = CombatFactory.CreateTestChainling("Prey", ChainlingType.Node, hp: 200, mDef: 20, spd: 10);
            attacker.currentAP = 3;
            attacker.currentHP = 40;
            attacker.displayHP = 40;
            var move = CombatFactory.CreateTestMove("bite", MoveCategory.DamagingMelee, ChainlingType.Burned, power: 70, apCost: 1, accuracy: 100);
            move.modifier1SectionId = "6.1-e";
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            int hpBefore = attacker.currentHP;
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.Greater(attacker.currentHP, hpBefore);
            Assert.IsTrue(resolver.Log.Events.Any(e => e.eventType == CombatEventType.HealApplied));
        }

        private CombatResolver CreateResolver(ModifierCatalogSO catalog)
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            var statusCatalog = CreateAsset<StatusCatalogSO>();
            statusCatalog.BuildLookup();
            return new CombatResolver(matrix, statusCatalog, seed: 42, chemistryStrictMode: true, modifierCatalog: catalog);
        }

        private ModifierCatalogSO CreateCatalog(params ModifierDefinitionSO[] defs)
        {
            var catalog = CreateAsset<ModifierCatalogSO>();
            catalog.modifiers.AddRange(defs);
            catalog.BuildLookup();
            return catalog;
        }

        private ModifierDefinitionSO Durability(string sectionId, float min, float max)
        {
            var def = CreateAsset<ModifierDefinitionSO>();
            def.sectionId = sectionId;
            def.category = ModifierCategory.DUR;
            def.scope = ModifierScope.Passive;
            def.effectMin = min;
            def.effectMax = max;
            def.slotType = ModifierSlotType.Durability;
            def.allowedCategories = new[] { MoveCategory.Passive };
            return def;
        }

        private ModifierDefinitionSO Modifier(
            string sectionId,
            ModifierCategory category,
            string description,
            float effectMin,
            float effectMax,
            ModifierTriggerCondition trigger)
        {
            var def = CreateAsset<ModifierDefinitionSO>();
            def.sectionId = sectionId;
            def.category = category;
            def.scope = ModifierScope.Active;
            def.effectDescription = description;
            def.effectMin = effectMin;
            def.effectMax = effectMax;
            def.triggerCondition = trigger;
            def.triggerChance = 1f;
            def.allowedCategories = new[] { MoveCategory.DamagingMelee, MoveCategory.DamagingRanged };
            return def;
        }

        private static TeamState CreateTeam(int index, params ChainlingInstance[] units)
        {
            var team = new TeamState(index, $"team{index}");
            team.chainlings.AddRange(units);
            for (int i = 0; i < units.Length; i++)
            {
                units[i].instanceId = $"t{index}_{i}";
                units[i].isAlive = true;
            }
            return team;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }
    }
}
