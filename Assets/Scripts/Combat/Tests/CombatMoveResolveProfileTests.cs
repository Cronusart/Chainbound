using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatMoveResolveProfileTests
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
        public void CombatFactoryCopiesResolveProfileFromMoveDefinition()
        {
            var definition = CreateAsset<MoveDefinitionSO>();
            definition.moveId = "profile_copy";
            definition.displayName = "Profile Copy";
            definition.category = MoveCategory.DamagingRanged;
            definition.targeting = MoveTargeting.SingleEnemy;
            definition.power = 40;
            definition.accuracy = 95;
            definition.resolveProfile.damage.finalDamageMultiplier = 1.5f;
            definition.resolveProfile.hitCount.flatHitBonus = 2;
            definition.resolveProfile.conditionalDamage.multiplier = 1.4f;
            definition.resolveProfile.conditionalDamage.randomMultipliers.Add(0.75f);
            definition.resolveProfile.conditionalDamage.randomMultipliers.Add(1.5f);
            definition.resolveProfile.moveEffectsOncePerTarget = true;
            definition.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.DamageThresholdBarrier,
                magnitude = 30f,
                reflectPercent = 0.25f,
                stageStat = CombatStageStat.Speed,
                stageAmount = -1,
                armedTrigger = ArmedWindowTrigger.EnemyMiss,
            });

            MoveInstance instance = CombatFactory.CreateMove(definition);

            Assert.AreNotSame(definition.resolveProfile, instance.resolveProfile);
            Assert.AreEqual(1.5f, instance.resolveProfile.damage.finalDamageMultiplier);
            Assert.AreEqual(2, instance.resolveProfile.hitCount.flatHitBonus);
            Assert.AreEqual(1.4f, instance.resolveProfile.conditionalDamage.multiplier);
            CollectionAssert.AreEqual(new[] { 0.75f, 1.5f }, instance.resolveProfile.conditionalDamage.randomMultipliers);
            Assert.IsTrue(instance.resolveProfile.moveEffectsOncePerTarget);
            Assert.AreEqual(0.25f, instance.utilityEffects[0].reflectPercent);
            Assert.AreEqual(CombatStageStat.Speed, instance.utilityEffects[0].stageStat);
            Assert.AreEqual(-1, instance.utilityEffects[0].stageAmount);
            Assert.AreEqual(ArmedWindowTrigger.EnemyMiss, instance.utilityEffects[0].armedTrigger);
        }

        [Test]
        public void DamageCalculatorUsesMoveAuthoredFormulaTuning()
        {
            ChainlingInstance attacker = CreateChainling("attacker", 0, 50);
            ChainlingInstance defender = CreateChainling("defender", 0, 50);
            MoveInstance baseline = CreateMove("baseline", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 50, 100);
            MoveInstance tuned = CreateMove("tuned", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 50, 100);
            tuned.resolveProfile.damage.useCustomRandomRollRange = true;
            tuned.resolveProfile.damage.randomRollMin = 1f;
            tuned.resolveProfile.damage.randomRollMax = 1f;
            tuned.resolveProfile.damage.flatPowerBonus = 50;
            tuned.resolveProfile.damage.finalDamageMultiplier = 2f;

            int baselineDamage = DamageCalculator.CalculateDamageDetailed(
                attacker,
                defender,
                baseline,
                new ChemistryResult(),
                false,
                new CombatRng(1),
                out _);

            int tunedDamage = DamageCalculator.CalculateDamageDetailed(
                attacker,
                defender,
                tuned,
                new ChemistryResult(),
                false,
                new CombatRng(1),
                out DamageComputationTrace trace);

            Assert.Greater(tunedDamage, baselineDamage);
            Assert.AreEqual(100, trace.basePower);
            Assert.AreEqual(2f, trace.tuningMultiplier);
        }

        [Test]
        public void AccuracyAndCritCanBeForcedFromResolveProfile()
        {
            ChainlingInstance attacker = CreateChainling("attacker", 0, 50);
            ChainlingInstance defender = CreateChainling("defender", 0, 50);
            MoveInstance move = CreateMove("wild", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 0, 10, 100);

            move.resolveProfile.accuracy.forceMiss = true;
            float hitChance = DamageCalculator.ComputeHitChance(attacker, defender, move, out _, out _);
            Assert.AreEqual(0f, hitChance);

            move.resolveProfile.accuracy.forceMiss = false;
            move.resolveProfile.accuracy.forceHit = true;
            hitChance = DamageCalculator.ComputeHitChance(attacker, defender, move, out _, out _);
            Assert.AreEqual(1f, hitChance);

            move.resolveProfile.crit.forceCrit = true;
            Assert.AreEqual(1f, DamageCalculator.ComputeCritRate(attacker, move));

            move.resolveProfile.crit.forceCrit = false;
            move.resolveProfile.crit.suppressCrits = true;
            Assert.AreEqual(0f, DamageCalculator.ComputeCritRate(attacker, move));
        }

        [Test]
        public void DamagingMoveExecutesMoveAuthoredUtilityAtResolveTime()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance attacker = CreateChainling("attacker", 0, 100);
            ChainlingInstance defender = CreateChainling("defender", 0, 10);
            MoveInstance move = CreateMove("battery_strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 5, 0,
                new UtilityEffectPayload
                {
                    effectType = UtilityEffectType.GainAP,
                    apGain = 1,
                    targetOverride = MoveTargeting.Self,
                    chance = 1f,
                });
            move.resolveProfile.utilityExecutionTiming = MoveUtilityExecutionTiming.ResolveBeforeFirstHit;
            attacker.moves.Add(move);

            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, attacker),
                CreateTeam(1, defender));
            resolver.BeginTurn(state);
            Assert.AreEqual(CombatConstants.AP_PER_TURN, attacker.currentAP);

            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 1, 0) },
                new List<CombatAction> { CombatAction.Pass(1) });

            resolver.ExecuteActionStep(state, queue.First(step => step.actor == attacker));

            Assert.AreEqual(CombatConstants.AP_PER_TURN + 1, attacker.currentAP);
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 0, true)]
        [TestCase(1, 1, false)]
        [TestCase(-1, -2, true)]
        public void StageHigherThanOtherStageUsesStrictComparison(int defense, int attack, bool expected)
        {
            ChainlingInstance actor = CreateChainling("actor", 0, 20);
            actor.SetStage(CoreStat.MeleeDef, defense);
            actor.SetStage(CoreStat.MeleeAtk, attack);
            var profile = new MoveResolveProfile
            {
                resolveConditions = new List<MoveResolveCondition>
                {
                    new MoveResolveCondition
                    {
                        subject = MoveResolveConditionSubject.Self,
                        conditionType = MoveResolveConditionType.StageHigherThanOtherStage,
                        stat = CoreStat.MeleeDef,
                        comparisonStat = CoreStat.MeleeAtk,
                    },
                },
            };

            MoveResolveConditionEvaluation result = MoveResolveProfileUtility.EvaluateConditions(
                profile, actor, CreateChainling("target", 0, 10));

            Assert.AreEqual(expected, result.passed);
            if (!expected)
                StringAssert.Contains("StageNotHigher", result.reason);
        }

        [Test]
        public void SureHitTuningIgnoresEvasionAndKeepsNinetyFivePercentFloor()
        {
            ChainlingInstance attacker = CreateChainling("attacker", 0, 20);
            ChainlingInstance defender = CreateChainling("defender", 0, 10);
            attacker.stageAccuracy = -2;
            defender.stageEvasion = 6;
            MoveInstance move = CreateMove("precision", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 0, 34, 100);
            move.resolveProfile.accuracy.ignoreEvasionStages = 6;
            move.resolveProfile.accuracy.minHitChance = 0.95f;

            float chance = DamageCalculator.ComputeHitChance(attacker, defender, move, out _, out _);

            Assert.GreaterOrEqual(chance, 0.95f);
            move.resolveProfile.accuracy.forceMiss = true;
            Assert.AreEqual(0f, DamageCalculator.ComputeHitChance(attacker, defender, move, out _, out _));
        }

        [TestCase(true, -1)]
        [TestCase(false, -3)]
        public void MultiHitMoveCanApplyStageChangesOncePerTarget(bool oncePerTarget, int expectedStage)
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance attacker = CreateChainling("attacker", 0, 100);
            ChainlingInstance defender = CreateChainling("defender", 0, 10);
            MoveInstance move = CreateMove("multi", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 0, 1, 100);
            move.hitCount = 3;
            move.hitCountMax = 3;
            move.resolveProfile.accuracy.forceHit = true;
            move.resolveProfile.moveEffectsOncePerTarget = oncePerTarget;
            move.stageChanges.Add(new MoveStageChange
            {
                stat = CoreStat.Speed,
                stages = -1,
                chance = 1f,
            });
            attacker.moves.Add(move);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));

            Assert.AreEqual(expectedStage, defender.stageSpeed);
        }

        private CombatResolver CreateResolver()
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            var catalog = CreateAsset<StatusCatalogSO>();
            catalog.BuildLookup();
            return new CombatResolver(matrix, catalog, 1234, true);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static ChainlingInstance CreateChainling(string id, int ap, int speed)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = ap,
                baseMaxHP = 100,
                currentHP = 100,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = speed,
                maxSlots = 4,
                isAlive = true,
            };
            chainling.SyncDisplayVitals();
            return chainling;
        }

        private static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
        {
            var team = new TeamState(teamIndex, $"Team {teamIndex}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static MoveInstance CreateMove(
            string id,
            MoveCategory category,
            MoveTargeting targeting,
            int apCost,
            int power,
            int accuracy,
            params UtilityEffectPayload[] utilityEffects)
        {
            var move = new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                targeting = targeting,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.Projectile,
                basePower = power,
                apCost = apCost,
                baseAccuracy = accuracy,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };

            if (utilityEffects != null)
                move.utilityEffects.AddRange(utilityEffects);

            return move;
        }
    }
}
