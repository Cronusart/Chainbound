using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class SupabaseAuthRefreshTests
    {
        private GameObject root;
        private SupabaseConfigSO config;

        [SetUp]
        public void SetUp()
        {
            SupabaseClient.ResetTestHooks();
            SupabaseAnonymousAuthSessionProvider.ResetTestHooks();
            config = ScriptableObject.CreateInstance<SupabaseConfigSO>();
            config.supabaseUrl = "https://test-project.supabase.co";
            config.supabaseAnonKey = "test-public-key";
            config.enableDebugLogs = false;
            config.maxRetries = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SupabaseClient.ResetTestHooks();
            SupabaseAnonymousAuthSessionProvider.ResetTestHooks();
            if (root != null)
            {
                AccountSessionService sessions = root.GetComponent<AccountSessionService>();
                if (sessions != null && ReferenceEquals(AccountSessionService.Instance, sessions))
                    InvokeLifecycle(sessions, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
            }
            if (config != null)
                UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public async Task EdgeRequest_InvalidJwt_RefreshesAndRetriesOnce()
        {
            root = new GameObject(nameof(EdgeRequest_InvalidJwt_RefreshesAndRetriesOnce));
            AccountSessionService sessions = root.AddComponent<AccountSessionService>();
            InvokeLifecycle(sessions, "Awake");
            FakeAuthSessionProvider auth = root.AddComponent<FakeAuthSessionProvider>();
            sessions.Configure(authenticatedSessionProvider: auth, config: config);

            var sentTokens = new List<string>();
            SupabaseClient.TestPostOverride = (_, _, _, token, _) =>
            {
                sentTokens.Add(token);
                return Task.FromResult(sentTokens.Count == 1
                    ? new SupabaseClient.Response
                    {
                        statusCode = 401,
                        body = "{\"code\":\"UNAUTHORIZED_ASYMMETRIC_JWT\",\"message\":\"Invalid JWT\"}",
                    }
                    : new SupabaseClient.Response { statusCode = 200, body = "{\"success\":true}" });
            };

            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                config,
                "profile_get_chainlings",
                "{}");

            Assert.IsTrue(response.IsSuccess, $"status={response.statusCode} body={response.body}");
            CollectionAssert.AreEqual(new[] { "old-access-token", "new-access-token" }, sentTokens);
            Assert.AreEqual(1, auth.ProactiveCalls);
            Assert.AreEqual(1, auth.ForcedCalls);
        }

        [Test]
        public async Task RefreshProvider_ConcurrentCallersShareOneRotatingRequest()
        {
            root = new GameObject(nameof(RefreshProvider_ConcurrentCallersShareOneRotatingRequest));
            SupabaseAnonymousAuthSessionProvider provider = root.AddComponent<SupabaseAnonymousAuthSessionProvider>();
            var completion = new TaskCompletionSource<AccountAuthSessionResult>();
            int operationCount = 0;
            SupabaseAnonymousAuthSessionProvider.TestSessionOperationOverride = (_, _) =>
            {
                operationCount++;
                return completion.Task;
            };

            Task<AccountAuthSessionResult> first = provider.RefreshSessionAsync(config, forceRefresh: true);
            Task<AccountAuthSessionResult> second = provider.RefreshSessionAsync(config, forceRefresh: true);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, operationCount);

            completion.SetResult(Success("rotated-access-token"));
            Assert.AreEqual("rotated-access-token", (await first).accessToken);
            Assert.AreEqual("rotated-access-token", (await second).accessToken);
        }

        private static AccountAuthSessionResult Success(string accessToken)
        {
            return new AccountAuthSessionResult
            {
                success = true,
                accessToken = accessToken,
                authUserId = "auth-user",
                restored = true,
            };
        }

        private static void InvokeLifecycle(AccountSessionService service, string methodName)
        {
            MethodInfo method = typeof(AccountSessionService).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"AccountSessionService.{methodName} was not found.");
            method.Invoke(service, null);
        }

        private sealed class FakeAuthSessionProvider : MonoBehaviour, IAccountAuthSessionProvider
        {
            public int ProactiveCalls { get; private set; }
            public int ForcedCalls { get; private set; }
            public bool HasCachedSession => true;
            public string AccessToken => "old-access-token";
            public string AuthUserId => "auth-user";

            public Task<AccountAuthSessionResult> BootstrapAsync(SupabaseConfigSO value)
            {
                return Task.FromResult(Success("old-access-token"));
            }

            public Task<AccountAuthSessionResult> RefreshSessionAsync(SupabaseConfigSO value, bool forceRefresh = false)
            {
                if (forceRefresh)
                {
                    ForcedCalls++;
                    return Task.FromResult(Success("new-access-token"));
                }

                ProactiveCalls++;
                return Task.FromResult(Success("old-access-token"));
            }

            public void ClearSession()
            {
            }
        }
    }
}
