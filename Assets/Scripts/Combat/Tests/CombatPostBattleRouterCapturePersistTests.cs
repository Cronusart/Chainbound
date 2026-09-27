using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatPostBattleRouterCapturePersistTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator CaptureTerminal_PersistsInsteadOfSkipping()
        {
            _root = new GameObject("PostBattleRouterCaptureTest");
            var persistence = _root.AddComponent<RecordingBattleRewardPersistence>();
            var router = _root.AddComponent<CombatPostBattleRouter>();
            SetPrivateField(router, "rewardPersistenceOverride", persistence);

            bool completed = false;
            bool returned = false;
            router.OnPostBattleComplete += () => completed = true;
            router.OnReturnToOverworld += _ => returned = true;

            yield return null;

            var result = new BattleResult
            {
                battleType = BattleType.PvE,
                winnerTeamIndex = -1,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
                totalEnemyKOs = 1,
            };

            router.RouteToRewards(result);

            float elapsed = 0f;
            while (!completed && elapsed < 2f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(completed, "Router should complete capture persistence.");
            Assert.IsTrue(persistence.PersistCalled, "Capture must persist rewards instead of CompleteFlow skip.");
            Assert.IsTrue(returned);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private sealed class RecordingBattleRewardPersistence : MonoBehaviour, IBattleRewardPersistence
        {
            public bool PersistCalled { get; private set; }
            public bool IsAvailable => true;

            public System.Threading.Tasks.Task<bool> PersistRewardsAsync(BattleRewardSummary summary, string walletAddress)
            {
                PersistCalled = true;
                return System.Threading.Tasks.Task.FromResult(true);
            }

            public System.Threading.Tasks.Task<bool> PersistOutcomeAsync(BattleOutcomeBundle outcome, string walletAddress)
            {
                PersistCalled = true;
                return System.Threading.Tasks.Task.FromResult(true);
            }
        }
    }
}
