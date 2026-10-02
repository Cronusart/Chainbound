using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatRewardPreparationHandoffTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void BattleController_PreparesAssignedRouterBeforePostBattleReport()
        {
            _root = new GameObject("CombatRewardPreparationHandoffTest");
            var controller = _root.AddComponent<CombatBattleController>();
            var recorder = _root.AddComponent<RecordingRewardPreparation>();
            var result = CreateVictoryResult();

            SetPrivateField(controller, "rewardsRouterOverride", recorder);
            InvokePrivate(controller, "PrepareRewardsBeforePostBattlePresentation", result);

            Assert.AreSame(result, recorder.PreparedResult);
        }

        [Test]
        public void RouterPreparation_SuppliesTheAuthoritativeSummaryToTheReport()
        {
            _root = new GameObject("CombatRewardPreparationReportTest");
            var report = _root.AddComponent<CombatPostBattleReportController>();
            var router = _root.AddComponent<CombatPostBattleRouter>();
            var result = CreateVictoryResult();

            SetPrivateField(router, "battleReport", report);
            PreparedBattleRewards prepared = router.PrepareRewards(result);
            PreparedBattleRewards received = GetPrivateField<PreparedBattleRewards>(report, "_preparedRewards");

            Assert.IsNotNull(prepared);
            Assert.IsNotNull(prepared.summary);
            Assert.AreSame(prepared, received);
            Assert.AreSame(result, received.result);
            Assert.AreEqual(5, received.summary.ramEarned);
        }

        private static BattleResult CreateVictoryResult()
        {
            return new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = 0,
                totalEnemyKOs = 1,
                encounterDifficulty = EncounterDifficulty.Standard,
            };
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{methodName} was not found.");
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} was not found.");
            return (T)field.GetValue(target);
        }

        private sealed class RecordingRewardPreparation : MonoBehaviour, ICombatRewardsRouter, ICombatRewardPreparation
        {
            public BattleResult PreparedResult { get; private set; }

            public PreparedBattleRewards PrepareRewards(BattleResult result)
            {
                PreparedResult = result;
                return new PreparedBattleRewards { result = result, summary = new BattleRewardSummary() };
            }

            public void RouteToRewards(BattleResult result)
            {
            }
        }
    }
}
