using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class DailyLoginRewardServiceTests
    {
        private const string ClaimableStatus =
            "{\"success\":true,\"ok\":true,\"enabled\":true,\"unlocked\":true,\"can_claim\":true," +
            "\"utc_day\":\"2026-10-30\",\"server_now\":\"2026-10-30T12:00:00.000Z\",\"next_claim_at\":\"\"," +
            "\"current_streak\":2,\"ram_amount\":10,\"item_id\":\"signal_orb\",\"item_quantity\":1,\"error\":\"\"}";

        private const string ClaimedStatus =
            "{\"success\":true,\"ok\":true,\"enabled\":true,\"unlocked\":true,\"can_claim\":false," +
            "\"utc_day\":\"2026-10-30\",\"server_now\":\"2026-10-30T12:00:00.000Z\"," +
            "\"next_claim_at\":\"2026-10-31T00:00:00.000Z\",\"current_streak\":3,\"ram_amount\":10," +
            "\"item_id\":\"signal_orb\",\"item_quantity\":1,\"error\":\"\"}";

        private const string FirstClaim =
            "{\"success\":true,\"ok\":true,\"already_claimed\":false,\"utc_day\":\"2026-10-30\"," +
            "\"server_now\":\"2026-10-30T12:00:00.000Z\",\"next_claim_at\":\"2026-10-31T00:00:00.000Z\"," +
            "\"ram_balance\":110,\"ram_balance_version\":7,\"ram_amount\":10,\"item_id\":\"signal_orb\"," +
            "\"item_quantity\":1,\"streak_after\":3,\"error\":\"\"}";

        private const string ReplayClaim =
            "{\"success\":true,\"ok\":true,\"already_claimed\":true,\"utc_day\":\"2026-10-30\"," +
            "\"server_now\":\"2026-10-30T12:00:05.000Z\",\"next_claim_at\":\"2026-10-31T00:00:00.000Z\"," +
            "\"ram_balance\":110,\"ram_balance_version\":7,\"ram_amount\":10,\"item_id\":\"signal_orb\"," +
            "\"item_quantity\":1,\"streak_after\":3,\"error\":\"\"}";

        private GameObject root;
        private SupabaseConfigSO config;
        private DailyLoginRewardService service;
        private readonly List<string> calls = new List<string>();
        private readonly Dictionary<string, Queue<Func<Task<SupabaseClient.Response>>>> responses =
            new Dictionary<string, Queue<Func<Task<SupabaseClient.Response>>>>();

        [SetUp]
        public void SetUp()
        {
            SupabaseClient.ResetTestHooks();
            PlayerRamBalanceStore.Clear();
            calls.Clear();
            responses.Clear();

            config = ScriptableObject.CreateInstance<SupabaseConfigSO>();
            config.supabaseUrl = "https://test-project.supabase.co";
            config.supabaseAnonKey = "test-public-key";
            config.enableDebugLogs = false;
            config.maxRetries = 0;

            root = new GameObject(nameof(DailyLoginRewardServiceTests));
            AccountSessionService sessions = root.AddComponent<AccountSessionService>();
            InvokeLifecycle(sessions, "Awake");
            FakeAuthSessionProvider auth = root.AddComponent<FakeAuthSessionProvider>();
            sessions.Configure(authenticatedSessionProvider: auth, config: config);

            service = root.AddComponent<DailyLoginRewardService>();
            service.SetConfig(config);

            SupabaseClient.TestPostOverride = (_, functionName, _, _, _) =>
            {
                calls.Add(functionName);
                if (responses.TryGetValue(functionName, out Queue<Func<Task<SupabaseClient.Response>>> queue) &&
                    queue.Count > 0)
                    return queue.Dequeue()();
                return Task.FromResult(new SupabaseClient.Response { statusCode = 200, body = "{}" });
            };
        }

        [TearDown]
        public void TearDown()
        {
            SupabaseClient.ResetTestHooks();
            PlayerRamBalanceStore.Clear();
            if (root != null)
            {
                AccountSessionService sessions = root.GetComponent<AccountSessionService>();
                if (sessions != null && ReferenceEquals(AccountSessionService.Instance, sessions))
                    InvokeLifecycle(sessions, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
            }

            PlayerProfileReadStore[] stores = UnityEngine.Object.FindObjectsByType<PlayerProfileReadStore>(
                FindObjectsInactive.Include);
            for (int i = 0; i < stores.Length; i++)
                UnityEngine.Object.DestroyImmediate(stores[i].gameObject);

            if (config != null)
                UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public async Task Refresh_ClaimableStatus_ShowsGiftWithServerLabels()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);

            Assert.IsTrue(await service.RefreshAsync());

            Assert.IsTrue(service.StatusLoaded);
            Assert.IsTrue(service.Unlocked);
            Assert.IsTrue(service.CanClaim);
            Assert.IsFalse(service.NextClaimAtUtc.HasValue, "next_claim_at is empty while claimable");
            Assert.AreEqual(10, service.RamAmount);
            Assert.AreEqual("signal_orb", service.ItemId);
            Assert.AreEqual(1, service.ItemQuantity);
            Assert.AreEqual(2, service.CurrentStreak);
            Assert.AreEqual(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc), service.ServerNowUtc);
        }

        [Test]
        public async Task Refresh_ClaimedStatus_CountsDownToNextUtcMidnight()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimedStatus);

            await service.RefreshAsync();

            Assert.IsTrue(service.StatusLoaded);
            Assert.IsFalse(service.CanClaim);
            Assert.IsTrue(service.NextClaimAtUtc.HasValue);
            Assert.AreEqual(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), service.NextClaimAtUtc.Value);
            Assert.AreEqual(DateTimeKind.Utc, service.NextClaimAtUtc.Value.Kind);
        }

        [Test]
        public async Task Refresh_FailedFetch_HidesConfirmedGift()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            Enqueue(DailyLoginRewardService.StatusFunctionName, 500, "{\"success\":false,\"error\":\"boom\"}");
            await service.RefreshAsync();
            int statusEvents = 0;
            service.OnStatusChanged += () => statusEvents++;

            Assert.IsFalse(await service.RefreshAsync());

            Assert.IsFalse(service.StatusLoaded);
            Assert.IsFalse(service.CanClaim, "an unconfirmed gift is never shown");
            Assert.AreEqual(1, statusEvents);
        }

        [Test]
        public async Task Claim_FirstGrantCelebratesOnce_ReplayOnlyRefreshes()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            Enqueue(DailyLoginRewardService.ClaimFunctionName, 200, FirstClaim);
            Enqueue(DailyLoginRewardService.ClaimFunctionName, 200, ReplayClaim);
            await service.RefreshAsync();
            var celebrated = new List<DailyLoginClaimResult>();
            service.OnClaimSucceeded += celebrated.Add;

            DailyLoginClaimResult first = await service.ClaimAsync();
            DailyLoginClaimResult replay = await service.ClaimAsync();

            Assert.IsTrue(first.ok);
            Assert.IsFalse(first.alreadyClaimed);
            Assert.IsTrue(replay.ok);
            Assert.IsTrue(replay.alreadyClaimed);
            Assert.AreEqual(1, celebrated.Count, "a replay must not play the grant again");
            Assert.AreEqual(10, celebrated[0].ramAmount);
            Assert.AreEqual("signal_orb", celebrated[0].itemId);
            Assert.AreEqual(1, celebrated[0].itemQuantity);
            Assert.IsFalse(service.CanClaim);
            Assert.AreEqual(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), service.NextClaimAtUtc.Value);
        }

        [Test]
        public async Task Claim_StackFull_RejectsAndLeavesGiftUp()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            Enqueue(
                DailyLoginRewardService.ClaimFunctionName,
                409,
                "{\"success\":false,\"ok\":false,\"already_claimed\":false,\"error\":\"stack_full\",\"item_id\":\"signal_orb\"}");
            await service.RefreshAsync();
            var rejected = new List<string>();
            int celebrated = 0;
            service.OnClaimRejected += rejected.Add;
            service.OnClaimSucceeded += _ => celebrated++;

            DailyLoginClaimResult result = await service.ClaimAsync();

            Assert.IsFalse(result.ok);
            Assert.AreEqual(DailyLoginRewardService.StackFullError, result.error);
            CollectionAssert.AreEqual(new[] { DailyLoginRewardService.StackFullError }, rejected);
            Assert.AreEqual(0, celebrated);
            Assert.IsTrue(service.CanClaim, "stack_full does not spend the day");
        }

        [Test]
        public async Task Claim_NotUnlocked_HidesGift()
        {
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            Enqueue(
                DailyLoginRewardService.ClaimFunctionName,
                409,
                "{\"success\":false,\"ok\":false,\"error\":\"not_unlocked\"}");
            await service.RefreshAsync();

            await service.ClaimAsync();

            Assert.IsFalse(service.Unlocked);
            Assert.IsFalse(service.CanClaim);
        }

        [Test]
        public async Task Claim_DoubleTapWhileInFlight_SendsOneRequest()
        {
            var pending = new TaskCompletionSource<SupabaseClient.Response>();
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            responses[DailyLoginRewardService.ClaimFunctionName] = new Queue<Func<Task<SupabaseClient.Response>>>(
                new Func<Task<SupabaseClient.Response>>[] { () => pending.Task });
            await service.RefreshAsync();
            int celebrated = 0;
            service.OnClaimSucceeded += _ => celebrated++;

            Task<DailyLoginClaimResult> first = service.ClaimAsync();
            Task<DailyLoginClaimResult> second = service.ClaimAsync();

            Assert.AreSame(first, second);
            Assert.IsTrue(service.IsClaimInFlight);
            pending.SetResult(new SupabaseClient.Response { statusCode = 200, body = FirstClaim });
            await first;

            Assert.AreEqual(1, calls.FindAll(name => name == DailyLoginRewardService.ClaimFunctionName).Count);
            Assert.AreEqual(1, celebrated);
        }

        [Test]
        public async Task Refresh_StaleStatusAfterClaim_DoesNotBringBoxesBack()
        {
            var pendingStatus = new TaskCompletionSource<SupabaseClient.Response>();
            Enqueue(DailyLoginRewardService.StatusFunctionName, 200, ClaimableStatus);
            responses[DailyLoginRewardService.StatusFunctionName].Enqueue(() => pendingStatus.Task);
            Enqueue(DailyLoginRewardService.ClaimFunctionName, 200, FirstClaim);
            await service.RefreshAsync();

            Task<bool> staleRefresh = service.RefreshAsync();
            await service.ClaimAsync();
            pendingStatus.SetResult(new SupabaseClient.Response { statusCode = 200, body = ClaimableStatus });
            await staleRefresh;

            Assert.IsFalse(service.CanClaim, "a status read that started before the claim is discarded");
        }

        private void Enqueue(string functionName, int statusCode, string body)
        {
            if (!responses.TryGetValue(functionName, out Queue<Func<Task<SupabaseClient.Response>>> queue))
            {
                queue = new Queue<Func<Task<SupabaseClient.Response>>>();
                responses[functionName] = queue;
            }

            queue.Enqueue(() => Task.FromResult(new SupabaseClient.Response { statusCode = statusCode, body = body }));
        }

        private static void InvokeLifecycle(AccountSessionService sessions, string methodName)
        {
            MethodInfo method = typeof(AccountSessionService).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"AccountSessionService.{methodName} was not found.");
            method.Invoke(sessions, null);
        }

        private sealed class FakeAuthSessionProvider : MonoBehaviour, IAccountAuthSessionProvider
        {
            public bool HasCachedSession => true;
            public string AccessToken => "test-access-token";
            public string AuthUserId => "auth-user";

            public Task<AccountAuthSessionResult> BootstrapAsync(SupabaseConfigSO value)
            {
                return Task.FromResult(Success());
            }

            public Task<AccountAuthSessionResult> RefreshSessionAsync(SupabaseConfigSO value, bool forceRefresh = false)
            {
                return Task.FromResult(Success());
            }

            public void ClearSession()
            {
            }

            private static AccountAuthSessionResult Success()
            {
                return new AccountAuthSessionResult
                {
                    success = true,
                    accessToken = "test-access-token",
                    authUserId = "auth-user",
                    restored = true,
                };
            }
        }
    }
}
