using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatRuntimeFixTests
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
        public void ApServiceBlocksThreadLockGainsAndLogsBlockedGain()
        {
            var log = new CombatEventLog(7);
            var service = new CombatApService(log);
            var target = CreateChainling("locked", 1, 10);
            target.statuses.Add(new StatusInstance(StatusId.ThreadLock, StatusClass.Duration, StatusOrigin.General, 1, "test"));

            int gained = service.Gain(target, 3, 1, "TestGain");

            Assert.AreEqual(0, gained);
            Assert.AreEqual(1, target.currentAP);
            Assert.IsTrue(log.Events.Any(e => e.eventType == CombatEventType.APGainBlocked && e.targetId == target.instanceId));
        }

        [Test]
        public void FrozenAssetsBlocksVoluntarySwapButImmovableDoesNot()
        {
            var eligibility = new CombatActionEligibilityService();
            var actor = CreateChainling("actor", 0, 10);
            var bench = CreateChainling("bench", 0, 10);
            var team = CreateTeam(0, actor, bench);
            team.activeIndices.Clear();
            team.activeIndices.Add(0);

            actor.statuses.Add(new StatusInstance(StatusId.FrozenAssets, StatusClass.Duration, StatusOrigin.General, 1, "test"));
            Assert.AreEqual(0, eligibility.GetAvailableSwapTargets(team, actor, null).Count);

            actor.statuses.Clear();
            actor.statuses.Add(new StatusInstance(StatusId.Immovable, StatusClass.Duration, StatusOrigin.General, 1, "test"));
            CollectionAssert.AreEqual(new[] { 1 }, eligibility.GetAvailableSwapTargets(team, actor, null));
        }

        [Test]
        public void NextMoveApSurchargeAppliesToSelectionCost()
        {
            var eligibility = new CombatActionEligibilityService();
            var actor = CreateChainling("surcharged", 2, 10);
            var move = CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 2, 40, 100);

            Assert.IsTrue(eligibility.EvaluateMoveSelection(actor, move, new CombatState()).canSelect);

            actor.nextMoveApSurcharge = 1;
            Assert.AreEqual(3, eligibility.GetEffectiveApCost(actor, move));
            Assert.IsFalse(eligibility.EvaluateMoveSelection(actor, move, new CombatState()).canSelect);

            actor.currentAP = 3;
            Assert.IsTrue(eligibility.EvaluateMoveSelection(actor, move, new CombatState()).canSelect);
        }

        [Test]
        public void ResolverRechecksApAfterEarlierDrainAndFailsSelectedMove()
        {
            var resolver = CreateResolver();
            var drainer = CreateChainling("drainer", 0, 100);
            var victim = CreateChainling("victim", 0, 10);

            drainer.moves.Add(CreateMove("ap_sap", MoveCategory.StatusUtility, MoveTargeting.SingleEnemy, 0, 0, 0,
                new UtilityEffectPayload
                {
                    effectType = UtilityEffectType.DrainAP,
                    apGain = 2,
                    targetOverride = MoveTargeting.SingleEnemy,
                    chance = 1f,
                }));
            victim.moves.Add(CreateMove("costly_strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 2, 40, 100));

            var state = resolver.InitializeBattle(
                CreateTeam(0, drainer),
                CreateTeam(1, victim));

            resolver.BeginTurn(state);
            Assert.AreEqual(2, victim.currentAP);

            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 1, 0) },
                new List<CombatAction> { CombatAction.UseMove(1, 0, 0, 0) });

            Assert.AreSame(drainer, queue[0].actor);
            resolver.ExecuteActionStep(state, queue[0]);
            Assert.AreEqual(0, victim.currentAP);

            resolver.ExecuteActionStep(state, queue[1]);

            Assert.IsTrue(victim.hasActedThisTurn);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ActionFailed &&
                e.sourceId == victim.instanceId &&
                e.message.Contains("NO AP")));
        }

        [Test]
        public void ApDonationTransfersPaidCostToActivePartnerSlot()
        {
            var resolver = CreateResolver();
            var donor = CreateChainling("donor", 0, 100);
            var partner = CreateChainling("partner", 0, 10);
            var enemy = CreateChainling("enemy", 0, 5);

            donor.moves.Add(CreateMove("ap_donation", MoveCategory.Support, MoveTargeting.SingleAlly, 2, 0, 0));

            var state = resolver.InitializeBattle(
                CreateTeam(0, donor, partner),
                CreateTeam(1, enemy));

            resolver.BeginTurn(state);
            Assert.AreEqual(2, donor.currentAP);
            Assert.AreEqual(2, partner.currentAP);

            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1) });

            Assert.AreSame(donor, queue[0].actor);
            resolver.ExecuteActionStep(state, queue[0]);

            Assert.AreEqual(0, donor.currentAP);
            Assert.AreEqual(4, partner.currentAP);
            Assert.IsTrue(donor.apDonationUsedThisTurn);
        }

        [Test]
        public void ApServiceTagsMoveCostAndDrainSourcesSeparately()
        {
            var log = new CombatEventLog(11);
            var service = new CombatApService(log);
            var actor = CreateChainling("actor", 4, 10);

            Assert.IsTrue(service.TrySpendCost(actor, 2, 1, "Strike", out _));
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APSpent &&
                e.apMutationSource == CombatApMutationSource.MoveCost));

            actor.currentAP = 3;
            service.Drain(actor, 2, 1, "DrainEffect");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APSpent &&
                e.apMutationSource == CombatApMutationSource.Drain));
        }

        [Test]
        public void ApServiceTagsGainRefundStealAndBlockedSources()
        {
            var log = new CombatEventLog(13);
            var service = new CombatApService(log);
            var source = CreateChainling("source", 1, 10);
            var target = CreateChainling("target", 3, 10);

            service.Gain(source, 2, 1, "GainEffect");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APGained &&
                e.apMutationSource == CombatApMutationSource.Gain));

            service.Refund(source, 1, 1, "RefundEffect");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APRefunded &&
                e.apMutationSource == CombatApMutationSource.Refund));

            service.Steal(source, target, 2, 1, "StealEffect");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APStolen &&
                e.apMutationSource == CombatApMutationSource.Steal));

            target.statuses.Add(new StatusInstance(StatusId.ThreadLock, StatusClass.Duration, StatusOrigin.General, 1, "test"));
            service.Gain(target, 2, 1, "BlockedGain");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APGainBlocked &&
                e.apMutationSource == CombatApMutationSource.Blocked));
        }

        [Test]
        public void ApServiceTagsTurnRegenSourceForBaselineApGrant()
        {
            var log = new CombatEventLog(14);
            var service = new CombatApService(log);
            var chainling = CreateChainling("regen_target", 0, 10);

            service.Gain(chainling, CombatConstants.AP_PER_TURN, 2, "TurnRegen");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APGained &&
                e.apMutationSource == CombatApMutationSource.TurnRegen));

            chainling.statuses.Add(new StatusInstance(StatusId.ThreadLock, StatusClass.Duration, StatusOrigin.General, 1, "test"));
            service.Gain(chainling, CombatConstants.AP_PER_TURN, 2, "TurnRegen");
            Assert.IsTrue(log.Events.Any(e =>
                e.eventType == CombatEventType.APGainBlocked &&
                e.apMutationSource == CombatApMutationSource.TurnRegen));
        }

        [Test]
        public void TwoTurnRechargeTicksOncePerActiveEndOfTurn()
        {
            var resolver = CreateResolver();
            var actor = CreateChainling("actor", 0, 20);
            var enemy = CreateChainling("enemy", 0, 10);
            MoveInstance move = CreateMove(
                "two_turn_recharge",
                MoveCategory.StatusUtility,
                MoveTargeting.Self,
                0,
                0,
                100);
            move.rechargeTurns = 2;
            actor.moves.Add(move);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, enemy));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            StatusInstance recharge = actor.statuses.Single(s => s.statusId == StatusId.Recharging);
            Assert.AreEqual(1, recharge.turnsRemaining);
            Assert.AreEqual(move.instanceId, recharge.rechargingMoveId);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.IsFalse(actor.statuses.Any(s => s.statusId == StatusId.Recharging));
            Assert.AreEqual(1, resolver.Log.Events.Count(e =>
                e.eventType == CombatEventType.MoveRechargeComplete));
        }

        [Test]
        public void BenchRechargeTicksWhileOtherStatusesStayPaused()
        {
            var resolver = CreateResolver();
            var lead = CreateChainling("lead", 0, 20);
            var partner = CreateChainling("partner", 0, 18);
            var bench = CreateChainling("bench", 0, 15);
            var enemy = CreateChainling("enemy", 0, 10);
            TeamState allies = CreateTeam(0, lead, partner, bench);
            CombatState state = resolver.InitializeBattle(allies, CreateTeam(1, enemy));

            bench.statuses.Add(new StatusInstance(
                StatusId.Recharging,
                StatusClass.Consumed,
                StatusOrigin.General,
                2,
                "bench_move")
            {
                rechargingMoveId = "bench_move",
            });
            bench.statuses.Add(new StatusInstance(
                StatusId.ThreadLock,
                StatusClass.Duration,
                StatusOrigin.General,
                2,
                "bench_lock"));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            StatusInstance recharge = bench.statuses.Single(s => s.statusId == StatusId.Recharging);
            StatusInstance threadLock = bench.statuses.Single(s => s.statusId == StatusId.ThreadLock);
            Assert.AreEqual(1, recharge.turnsRemaining);
            Assert.AreEqual(2, threadLock.turnsRemaining);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.IsFalse(bench.statuses.Any(s => s.statusId == StatusId.Recharging));
            Assert.AreEqual(2, bench.statuses.Single(s => s.statusId == StatusId.ThreadLock).turnsRemaining);
        }

        [Test]
        public void RandomEnemySelectsBothLivingOpposingActivesAcrossSeeds()
        {
            var selectedTargetIds = new HashSet<string>();

            for (int seed = 1; seed <= 32; seed++)
            {
                var matrix = CreateAsset<TypeChemistryMatrixSO>();
                StatusCatalogSO catalog = CreateStatusCatalog();
                var resolver = new CombatResolver(matrix, catalog, seed, true);
                var actor = CreateChainling("actor", 0, 20);
                var foeA = CreateChainling("foe-a", 0, 10);
                var foeB = CreateChainling("foe-b", 0, 9);
                MoveInstance move = CreateMove(
                    "random_ping",
                    MoveCategory.DamagingRanged,
                    MoveTargeting.RandomEnemy,
                    0,
                    1,
                    100);
                move.resolveProfile = new MoveResolveProfile
                {
                    accuracy = new MoveAccuracyTuning { forceHit = true },
                };
                actor.moves.Add(move);

                CombatState state = resolver.InitializeBattle(
                    CreateTeam(0, actor),
                    CreateTeam(1, foeA, foeB));
                resolver.ExecuteTurn(
                    state,
                    CombatAction.UseMove(0, 0, 1, 0),
                    CombatAction.Pass(1));

                selectedTargetIds.Add(resolver.Log.Events.Single(e =>
                    e.eventType == CombatEventType.DamageDealt).targetId);
            }

            CollectionAssert.AreEquivalent(new[] { "foe-a", "foe-b" }, selectedTargetIds);
        }

        [Test]
        public void BattlePacingProfileDefaultsAreReadable()
        {
            var profile = CreateAsset<CombatBattlePacingProfileSO>();

            Assert.GreaterOrEqual(profile.postSelectionConfirmDelay, 0f);
            Assert.GreaterOrEqual(profile.damageHold, 0f);
            Assert.GreaterOrEqual(profile.apFeedbackHold, 0f);
            Assert.GreaterOrEqual(profile.turnStartApGrantHold, 0f);
            Assert.IsTrue(profile.batchTurnStartApHudRefresh);
            Assert.IsFalse(profile.showMoveCostApFeedback);
            Assert.AreNotEqual(default, profile.apGainColor);
            Assert.AreNotEqual(default, profile.apDrainColor);
        }

        [Test]
        public void NextActionStatusSurvivesTheGrantingTurnTick()
        {
            var surging = new StatusInstance(StatusId.Surging, StatusClass.Consumed, StatusOrigin.General, 1, "crit_feed");
            surging.HoldThroughGrantTurn(4);

            Assert.IsTrue(surging.Tick(4));
            Assert.IsFalse(surging.IsExpired);

            Assert.IsFalse(surging.Tick(5));
            Assert.IsTrue(surging.IsExpired);
        }

        [Test]
        public void RestOfTurnStatusExpiresOnTheGrantingTurnTick()
        {
            var swarm = new StatusInstance(StatusId.BotSwarm, StatusClass.Duration, StatusOrigin.General, 1, "bot_swarm");

            Assert.IsFalse(swarm.Tick(4));
            Assert.IsTrue(swarm.IsExpired);
        }

        [Test]
        public void SoftHandsInverseScalingUsesDefenseAsPivot()
        {
            Assert.AreEqual(50f, DamageCalculator.ApplyInverseAttackScaling(50f, 50f), 0.01f);
            Assert.Less(DamageCalculator.ApplyInverseAttackScaling(80f, 50f), 50f);
            Assert.Greater(DamageCalculator.ApplyInverseAttackScaling(30f, 50f), 50f);
        }

        /// <summary>
        /// Play Mode smoke checks (manual):
        /// confirm delay after selection, melee/ranged impact hold, AP gain/drain outline flash,
        /// status/stage labels, passive/counter labels, wall raised/blocked/broken anchor popups, NO AP nudge.
        /// </summary>
        [Test]
        public void PassiveDisplayStateReflectsThresholdsAndUsedState()
        {
            var eligibility = new CombatActionEligibilityService();
            var actor = CreateChainling("passive_user", 0, 10);
            var passive = CreateMove("desperation_surge", MoveCategory.Passive, MoveTargeting.Self, 0, 0, 0);
            passive.triggerCondition = PassiveTriggerCondition.HPBelowThreshold;
            passive.hpThresholdPercent = 50f;

            actor.currentHP = 20;
            Assert.AreEqual(PassiveDisplayState.Active, eligibility.GetPassiveDisplayState(actor, passive));

            actor.currentHP = 80;
            Assert.AreEqual(PassiveDisplayState.Armed, eligibility.GetPassiveDisplayState(actor, passive));

            passive.triggerOncePerBattle = true;
            actor.triggeredPassiveIds.Add(passive.instanceId);
            Assert.AreEqual(PassiveDisplayState.Used, eligibility.GetPassiveDisplayState(actor, passive));
        }

        private CombatResolver CreateResolver()
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            var catalog = CreateStatusCatalog();
            return new CombatResolver(matrix, catalog, 1234, true);
        }

        private StatusCatalogSO CreateStatusCatalog()
        {
            var catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.Add(CreateStatusDefinition(StatusId.ThreadLock, "Thread Lock", true));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.FrozenAssets, "Frozen Assets", true));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.Immovable, "Immovable", false));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.Recharging, "Recharging", false));
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO CreateStatusDefinition(StatusId id, string displayName, bool debuff)
        {
            var def = CreateAsset<StatusDefinitionSO>();
            def.statusId = id;
            def.displayName = displayName;
            def.statusClass = StatusClass.Duration;
            def.origin = StatusOrigin.General;
            def.defaultDuration = 1;
            def.isDebuff = debuff;
            return def;
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
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.None,
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
