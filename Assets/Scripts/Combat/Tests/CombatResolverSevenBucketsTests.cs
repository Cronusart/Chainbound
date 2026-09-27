using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatResolverSevenBucketsTests
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
        public void ResolveConditionCountsUseLiveTeamAndStatusCatalog()
        {
            StatusCatalogSO catalog = CreateStatusCatalog();
            ChainlingInstance source = CreateChainling("source");
            ChainlingInstance buffedPartner = CreateChainling("buffed_partner");
            ChainlingInstance plainPartner = CreateChainling("plain_partner");
            ChainlingInstance target = CreateChainling("target");
            buffedPartner.stageSpeed = 1;
            target.statuses.Add(new StatusInstance(StatusId.FUD, StatusClass.Duration, StatusOrigin.General, 2, "test"));
            target.statuses.Add(new StatusInstance(StatusId.ThreadLock, StatusClass.Duration, StatusOrigin.General, 2, "test"));

            TeamState sourceTeam = CreateTeam(0, source, buffedPartner, plainPartner);
            TeamState targetTeam = CreateTeam(1, target);
            sourceTeam.activeIndices.AddRange(new[] { 0, 1 });
            targetTeam.activeIndices.Add(0);
            var context = new MoveResolveEvaluationContext
            {
                sourceTeam = sourceTeam,
                targetTeam = targetTeam,
                statusCatalog = catalog,
                ledger = new TurnActionLedger(),
            };

            int buffedCount = MoveResolveProfileUtility.ResolveConditionCount(
                Condition(MoveResolveConditionSubject.Self, MoveResolveConditionType.BuffedPartnerCountAtLeast),
                source,
                target,
                context);
            int debuffCount = MoveResolveProfileUtility.ResolveConditionCount(
                Condition(MoveResolveConditionSubject.Target, MoveResolveConditionType.DebuffCountAtLeast),
                source,
                target,
                context);

            Assert.AreEqual(1, buffedCount);
            Assert.AreEqual(2, debuffCount);
        }

        [Test]
        public void ConditionalDamageUsesFinalRedirectedDefender()
        {
            int lowHpTargetDamage = ResolveConditionalStrike(redirectToFullHealthPartner: false, out CombatEventLog directLog);
            int interceptorDamage = ResolveConditionalStrike(redirectToFullHealthPartner: true, out CombatEventLog redirectLog);

            Assert.Greater(lowHpTargetDamage, interceptorDamage);
            Assert.IsFalse(directLog.Events.Any(e => e.message != null && e.message.Contains("conditional damage skipped")));
            Assert.IsTrue(redirectLog.Events.Any(e => e.message != null && e.message.Contains("conditional damage skipped")));
        }

        [Test]
        public void IncomingHitWindowDodgesCountersAndIsConsumed()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance attacker = CreateChainling("attacker", speed: 30);
            ChainlingInstance defender = CreateChainling("defender", speed: 20);
            MoveInstance strike = CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 50);
            MoveInstance feint = CreateMove("fox_feint", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            feint.triggerOncePerBattle = true;
            feint.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.ArmUtilityWindow,
                armedTrigger = ArmedWindowTrigger.IncomingHit,
                turnBudget = 999,
                hitBudget = 1,
                targetOverride = MoveTargeting.Self,
                counterDamagePercentMaxHP = 0.10f,
                stackKey = "fox_feint",
            });
            attacker.moves.Add(strike);
            defender.moves.Add(feint);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.UseMove(1, 0));
            int defenderHpBefore = defender.currentHP;
            int attackerHpBefore = attacker.currentHP;

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(defenderHpBefore, defender.currentHP);
            Assert.AreEqual(attackerHpBefore - 10, attacker.currentHP);
            Assert.AreEqual(0, defender.armedUtilityWindows.Count);
            Assert.IsTrue(defender.triggeredPassiveIds.Contains(feint.instanceId));
            Assert.IsFalse(new CombatActionEligibilityService().EvaluateMoveSelection(defender, feint, state).canSelect);
        }

        [Test]
        public void BankedApReturnsAtNextTurnStartWithBonus()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance banker = CreateChainling("banker", speed: 20);
            ChainlingInstance foe = CreateChainling("foe", speed: 10);
            MoveInstance reserve = CreateMove("ap_reserve", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            reserve.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.BankAPUntilNextTurn,
                threshold = 5,
                apGain = 1,
                targetOverride = MoveTargeting.Self,
                stackKey = "ap_reserve",
            });
            banker.moves.Add(reserve);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, banker), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(0, banker.currentAP);
            Assert.AreEqual(1, banker.armedUtilityWindows.Count);

            resolver.BeginTurn(state);

            // Baseline +2, then the banked 2 AP returns with its +1 bonus.
            Assert.AreEqual(5, banker.currentAP);
            Assert.AreEqual(0, banker.armedUtilityWindows.Count);
        }

        [Test]
        public void FieldEntryEffectFiresOnVoluntarySwapAndExpires()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance caster = CreateChainling("caster", speed: 30);
            ChainlingInstance enemyLead = CreateChainling("enemy_lead", speed: 20);
            ChainlingInstance enemyPartner = CreateChainling("enemy_partner", speed: 15);
            ChainlingInstance enemyBench = CreateChainling("enemy_bench", speed: 10);
            MoveInstance field = CreateMove("stasis_field", MoveCategory.StatusUtility, MoveTargeting.Field, 0, 0);
            field.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.FieldEntryStageChange,
                stageStat = CombatStageStat.Speed,
                stageAmount = -1,
                turnBudget = 2,
                chance = 1f,
                stackKey = "stasis_field",
            });
            caster.moves.Add(field);

            TeamState enemyTeam = CreateTeam(1, enemyLead, enemyPartner, enemyBench);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, caster), enemyTeam);
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(1, state.fieldEntryEffects.Count);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Swap(1, 2));

            Assert.AreEqual(-1, enemyBench.stageSpeed);
            Assert.AreEqual(0, state.fieldEntryEffects.Count);
        }

        [Test]
        public void DataDrivenTurnStartAndKoPassivesExecuteUtilityPayloads()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance attacker = CreateChainling("attacker", speed: 30);
            ChainlingInstance defender = CreateChainling("defender", speed: 10);
            defender.currentHP = 1;

            MoveInstance turnStart = CreateMove("turn_start_ap_gain", MoveCategory.Passive, MoveTargeting.Self, 0, 0);
            turnStart.triggerCondition = PassiveTriggerCondition.TurnStart;
            turnStart.utilityEffects.Add(GainApPayload("turn_start", 1));
            MoveInstance onKo = CreateMove("on_ko_gain", MoveCategory.Passive, MoveTargeting.Self, 0, 0);
            onKo.triggerCondition = PassiveTriggerCondition.OnKO;
            onKo.utilityEffects.Add(GainApPayload("on_ko", 1));
            attacker.moves.Add(CreateMove("finisher", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 200));
            attacker.moves.Add(turnStart);
            attacker.moves.Add(onKo);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsFalse(defender.isAlive);
            Assert.AreEqual(4, attacker.currentAP, "baseline +2, turn-start +1, KO +1");
            Assert.IsTrue(resolver.Log.Events.Any(e => e.eventType == CombatEventType.PassiveTriggered && e.moveId == onKo.instanceId));
        }

        [Test]
        public void EndOfTurnStatusKoTriggersDataDrivenOnKoPassive()
        {
            StatusCatalogSO catalog = CreateStatusCatalog();
            StatusDefinitionSO dot = catalog.Get(StatusId.FUD);
            dot.hasEndOfTurnDamage = true;
            dot.endOfTurnDamagePercent = 1f;

            CombatResolver resolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 4321, true);
            ChainlingInstance attacker = CreateChainling("attacker", speed: 30);
            ChainlingInstance defender = CreateChainling("defender", speed: 10);
            defender.currentHP = 1;
            defender.statuses.Add(new StatusInstance(
                StatusId.FUD, StatusClass.Duration, StatusOrigin.General, 2, "test"));

            MoveInstance onKo = CreateMove("eot_on_ko_gain", MoveCategory.Passive, MoveTargeting.Self, 0, 0);
            onKo.triggerCondition = PassiveTriggerCondition.OnKO;
            onKo.utilityEffects.Add(GainApPayload("eot_on_ko", 1));
            attacker.moves.Add(onKo);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.IsFalse(defender.isAlive);
            Assert.AreEqual(3, attacker.currentAP, "baseline +2, end-of-turn KO +1");
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.PassiveTriggered && e.moveId == onKo.instanceId));
        }

        [Test]
        public void RandomStageOpcodeIsSeedDeterministic()
        {
            int[] first = ExecuteRandomStage(seed: 7731);
            int[] second = ExecuteRandomStage(seed: 7731);

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(1, first.Sum(), "exactly one stage should rise by one");
        }

        [Test]
        public void GlitchedMindChoosesALegalActionInsteadOfDroppingTheTurn()
        {
            CombatResolverConfigSO config = CreateAsset<CombatResolverConfigSO>();
            config.glitchedMindMisactChance = 1f;
            CombatResolver resolver = CreateResolver(config: config);
            ChainlingInstance actor = CreateChainling("glitched", speed: 30);
            ChainlingInstance foe = CreateChainling("foe", speed: 10);
            actor.statuses.Add(new StatusInstance(StatusId.GlitchedMind, StatusClass.Duration, StatusOrigin.General, 2, "test"));
            actor.moves.Add(CreateMove("legal_move", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 0, 10));

            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsTrue(actor.hasActedThisTurn);
            Assert.IsTrue(resolver.Log.Events.Any(e => e.message != null && e.message.Contains("mis-acted as")));
        }

        private int ResolveConditionalStrike(bool redirectToFullHealthPartner, out CombatEventLog log)
        {
            CombatResolver resolver = CreateResolver(seed: 991);
            ChainlingInstance attacker = CreateChainling("attacker", maxHp: 500, speed: 30);
            ChainlingInstance target = CreateChainling("target", maxHp: 500, speed: 10);
            target.currentHP = 150;
            ChainlingInstance interceptor = CreateChainling("interceptor", maxHp: 500, speed: 5);
            MoveInstance move = CreateMove("conditional_strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 40);
            move.resolveProfile.damage.useCustomRandomRollRange = true;
            move.resolveProfile.damage.randomRollMin = 1f;
            move.resolveProfile.damage.randomRollMax = 1f;
            move.resolveProfile.conditionalDamage.multiplier = 1.5f;
            move.resolveProfile.conditionalDamage.conditions.Add(
                Condition(MoveResolveConditionSubject.Target, MoveResolveConditionType.HPBelowPercent, 40f));
            attacker.moves.Add(move);

            TeamState defendingTeam = redirectToFullHealthPartner
                ? CreateTeam(1, target, interceptor)
                : CreateTeam(1, target);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), defendingTeam);
            if (redirectToFullHealthPartner)
            {
                target.statuses.Add(new StatusInstance(StatusId.DamageIntercept, StatusClass.Consumed, StatusOrigin.General, 1, "test"));
                target.redirectInterceptor = interceptor;
            }

            int before = redirectToFullHealthPartner ? interceptor.currentHP : target.currentHP;
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));
            int after = redirectToFullHealthPartner ? interceptor.currentHP : target.currentHP;
            log = resolver.Log;
            return before - after;
        }

        private int[] ExecuteRandomStage(int seed)
        {
            var log = new CombatEventLog(seed);
            var ap = new CombatApService(log);
            var executor = new UtilityEffectExecutor(log, ap);
            ChainlingInstance source = CreateChainling("random_source");
            MoveInstance move = CreateMove("random_stage", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            move.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.RandomStageChange,
                stageAmount = 1,
                stageAmountMax = 1,
                targetOverride = MoveTargeting.Self,
                chance = 1f,
            });
            executor.Execute(source, source, move, 1, true, new CombatRng(seed));
            return new[]
            {
                source.stageMeleeAtk,
                source.stageMeleeDef,
                source.stageRangedAtk,
                source.stageRangedDef,
                source.stageSpeed,
            };
        }

        private CombatResolver CreateResolver(int seed = 1234, CombatResolverConfigSO config = null)
        {
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            return new CombatResolver(matrix, CreateStatusCatalog(), seed, true, resolverConfig: config);
        }

        private StatusCatalogSO CreateStatusCatalog()
        {
            StatusCatalogSO catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.Add(CreateStatusDefinition(StatusId.FUD, isDebuff: true));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.ThreadLock, isDebuff: true));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.Surging, isBuff: true));
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO CreateStatusDefinition(StatusId id, bool isDebuff = false, bool isBuff = false)
        {
            StatusDefinitionSO definition = CreateAsset<StatusDefinitionSO>();
            definition.statusId = id;
            definition.displayName = id.ToString();
            definition.statusClass = StatusClass.Duration;
            definition.origin = StatusOrigin.General;
            definition.defaultDuration = 2;
            definition.isDebuff = isDebuff;
            definition.isBuff = isBuff;
            return definition;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static MoveResolveCondition Condition(
            MoveResolveConditionSubject subject,
            MoveResolveConditionType type,
            float threshold = 0f)
        {
            return new MoveResolveCondition
            {
                subject = subject,
                conditionType = type,
                threshold = threshold,
                statusId = StatusId.None,
                stat = CoreStat.MeleeAtk,
                comparisonStat = CoreStat.MeleeAtk,
            };
        }

        private static UtilityEffectPayload GainApPayload(string key, int amount)
        {
            return new UtilityEffectPayload
            {
                effectType = UtilityEffectType.GainAP,
                apGain = amount,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
                stackKey = key,
            };
        }

        private static ChainlingInstance CreateChainling(
            string id,
            int maxHp = 100,
            int speed = 20)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = 0,
                baseMaxHP = maxHp,
                currentHP = maxHp,
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
            int power)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                targeting = targeting,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.Projectile,
                basePower = power,
                apCost = apCost,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }
    }
}
