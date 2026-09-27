using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatCaptureTurnTests
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
        public void CaptureActionResolvesAfterAllCombatRegardlessOfSpeed()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance recorder = CreateChainling("recorder", speed: 90);
            ChainlingInstance partner = CreateChainling("partner", speed: 10);
            partner.moves.Add(CreateStrike("chip"));
            ChainlingInstance wild = CreateChainling("wild", speed: 50);
            wild.moves.Add(CreateStrike("claw"));
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, recorder, partner),
                CreateTeam(1, wild));

            resolver.BeginTurn(state);
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction>
                {
                    CombatAction.Capture(0, wild.instanceId, "signal_orb"),
                    CombatAction.UseMove(0, 0, 1, 0),
                },
                new List<CombatAction> { CombatAction.UseMove(1, 0, 0, 0) });

            Assert.AreEqual(CombatActionType.Capture, queue[queue.Count - 1].action.actionType);
            Assert.AreEqual(recorder.instanceId, queue[queue.Count - 1].actor.instanceId);
            Assert.IsTrue(queue.Take(queue.Count - 1).All(step =>
                step.action.actionType != CombatActionType.Capture));
        }

        [Test]
        public void SecondQueuedCaptureIsNormalizedToPass()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance lead = CreateChainling("lead", speed: 40);
            ChainlingInstance partner = CreateChainling("partner", speed: 30);
            ChainlingInstance wild = CreateChainling("wild", speed: 20);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, lead, partner),
                CreateTeam(1, wild));

            resolver.BeginTurn(state);
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction>
                {
                    CombatAction.Capture(0, wild.instanceId, "signal_orb"),
                    CombatAction.Capture(0, wild.instanceId, "lattice_orb"),
                },
                new List<CombatAction> { CombatAction.Pass(1) });

            int captureCount = queue.Count(step => step.action.actionType == CombatActionType.Capture);
            int passCount = queue.Count(step =>
                step.actingTeam != null &&
                step.actingTeam.teamIndex == 0 &&
                step.action.actionType == CombatActionType.Pass);
            Assert.AreEqual(1, captureCount);
            Assert.AreEqual(1, passCount);
        }

        [Test]
        public void CaptureIsCancelledWhenTargetIsKoedDuringCombat()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance recorder = CreateChainling("recorder", speed: 5);
            ChainlingInstance partner = CreateChainling("partner", speed: 80);
            partner.moves.Add(CreateStrike("finisher", power: 200));
            ChainlingInstance target = CreateChainling("target", maxHp: 8, speed: 10);
            ChainlingInstance otherWild = CreateChainling("other_wild", speed: 10);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, recorder, partner),
                CreateTeam(1, target, otherWild));

            resolver.BeginTurn(state);
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction>
                {
                    CombatAction.Capture(0, target.instanceId, "signal_orb"),
                    CombatAction.UseMove(0, 0, 1, 0),
                },
                new List<CombatAction>
                {
                    CombatAction.Pass(1),
                    CombatAction.Pass(1),
                });

            for (int i = 0; i < queue.Count; i++)
                resolver.ExecuteActionStep(state, queue[i]);

            Assert.IsFalse(target.isAlive);
            Assert.IsNull(state.pendingCaptureAttempt);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ActionFailed &&
                e.message != null &&
                e.message.Contains("no longer eligible")));
        }

        [Test]
        public void CaptureIsCancelledWhenRecorderIsKoedDuringCombat()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance recorder = CreateChainling("recorder", maxHp: 8, speed: 5);
            ChainlingInstance partner = CreateChainling("partner", speed: 20);
            partner.moves.Add(CreateStrike("nudge", power: 1));
            ChainlingInstance wild = CreateChainling("wild", speed: 90);
            wild.moves.Add(CreateStrike("crush", power: 200));
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, recorder, partner),
                CreateTeam(1, wild));

            resolver.BeginTurn(state);
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction>
                {
                    CombatAction.Capture(0, wild.instanceId, "signal_orb"),
                    CombatAction.UseMove(0, 0, 1, 0),
                },
                new List<CombatAction> { CombatAction.UseMove(1, 0, 0, 0) });

            for (int i = 0; i < queue.Count; i++)
                resolver.ExecuteActionStep(state, queue[i]);

            Assert.IsFalse(recorder.isAlive);
            Assert.IsNull(state.pendingCaptureAttempt);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ActionFailed &&
                e.message != null &&
                e.message.Contains("recorder is not eligible")));
        }

        [Test]
        public void CaptureIsCancelledWhenRecorderSkipsTheAction()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance recorder = CreateChainling("recorder", speed: 40);
            recorder.skippingNextTurn = true;
            ChainlingInstance wild = CreateChainling("wild", speed: 10);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, recorder),
                CreateTeam(1, wild));

            resolver.BeginTurn(state);
            recorder.skippingNextTurn = true;
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction> { CombatAction.Capture(0, wild.instanceId, "signal_orb") },
                new List<CombatAction> { CombatAction.Pass(1) });

            for (int i = 0; i < queue.Count; i++)
                resolver.ExecuteActionStep(state, queue[i]);

            Assert.IsNull(state.pendingCaptureAttempt);
            Assert.IsTrue(wild.isAlive);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ActionFailed &&
                e.message != null &&
                e.message.Contains("recorder is not eligible")));
        }

        [Test]
        public void EligibleCaptureLeavesPendingAttemptWithLiveHp()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance recorder = CreateChainling("recorder", speed: 5);
            ChainlingInstance partner = CreateChainling("partner", speed: 80);
            partner.moves.Add(CreateStrike("chip", power: 20));
            ChainlingInstance wild = CreateChainling("wild", maxHp: 100, speed: 10);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, recorder, partner),
                CreateTeam(1, wild));

            resolver.BeginTurn(state);
            List<CombatResolver.CombatActionStep> queue = resolver.BuildActionQueueForCurrentTurn(
                state,
                new List<CombatAction>
                {
                    CombatAction.Capture(0, wild.instanceId, "signal_orb"),
                    CombatAction.UseMove(0, 0, 1, 0),
                },
                new List<CombatAction> { CombatAction.Pass(1) });

            for (int i = 0; i < queue.Count; i++)
                resolver.ExecuteActionStep(state, queue[i]);

            Assert.IsNotNull(state.pendingCaptureAttempt);
            Assert.AreEqual(wild.instanceId, state.pendingCaptureAttempt.targetInstanceId);
            Assert.AreEqual("signal_orb", state.pendingCaptureAttempt.orbItemId);
            Assert.Less(wild.currentHP, wild.EffectiveMaxHP);
            Assert.IsTrue(wild.isAlive);
        }

        private CombatResolver CreateResolver()
        {
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            StatusCatalogSO catalog = CreateAsset<StatusCatalogSO>();
            catalog.BuildLookup();
            return new CombatResolver(matrix, catalog, 17, true);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static ChainlingInstance CreateChainling(string id, int maxHp = 100, int speed = 20)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = 10,
                baseMaxHP = maxHp,
                currentHP = maxHp,
                baseMeleeAtk = 80,
                baseMeleeDef = 40,
                baseRangedAtk = 80,
                baseRangedDef = 40,
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

        private static MoveInstance CreateStrike(string id, int power = 40)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                contactType = ContactType.Contact,
                basePower = power,
                apCost = 0,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }
    }
}
