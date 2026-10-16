using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Chainbound.Combat
{
    /// <summary>
    /// Dedicated client for Supabase anonymous authentication. It intentionally
    /// does not use <see cref="SupabaseClient"/>, whose URL policy is restricted
    /// to Edge Functions. Android refresh credentials are encrypted by Android
    /// Keystore through the small native bridge; editor storage is development-only.
    /// </summary>
    public sealed class SupabaseAnonymousAuthSessionProvider : MonoBehaviour, IAccountAuthSessionProvider
    {
        private const string LegacySessionStorageKey = "signaljam.supabase.anonymous.v1";
        private const string ScopedSessionStoragePrefix = "signaljam.supabase.anonymous.v2";
        private const int RefreshSkewSeconds = 45;

        [SerializeField] private bool enableDebugLogs;

        private readonly object sessionGate = new object();
        private CachedSession cached;
        private Task<AccountAuthSessionResult> sessionOperation;

#if UNITY_INCLUDE_TESTS
        public static Func<bool, bool, Task<AccountAuthSessionResult>> TestSessionOperationOverride;

        public static void ResetTestHooks()
        {
            TestSessionOperationOverride = null;
        }
#endif

        public bool HasCachedSession => !string.IsNullOrWhiteSpace(cached.refresh_token) || !string.IsNullOrWhiteSpace(cached.access_token);
        public string AccessToken => cached.access_token ?? "";
        public string AuthUserId => cached.user_id ?? "";

        public Task<AccountAuthSessionResult> BootstrapAsync(SupabaseConfigSO config)
        {
            return GetOrStartSessionOperation(config, forceRefresh: false, allowCreate: true);
        }

        public Task<AccountAuthSessionResult> RefreshSessionAsync(SupabaseConfigSO config, bool forceRefresh = false)
        {
            return GetOrStartSessionOperation(config, forceRefresh, allowCreate: false);
        }

        private Task<AccountAuthSessionResult> GetOrStartSessionOperation(
            SupabaseConfigSO config,
            bool forceRefresh,
            bool allowCreate)
        {
            lock (sessionGate)
            {
                if (sessionOperation != null && !sessionOperation.IsCompleted)
                    return sessionOperation;

                sessionOperation = RunSessionOperationAsync(config, forceRefresh, allowCreate);
                return sessionOperation;
            }
        }

        private async Task<AccountAuthSessionResult> RunSessionOperationAsync(
            SupabaseConfigSO config,
            bool forceRefresh,
            bool allowCreate)
        {
            config = PlatformContext.ResolveSupabaseConfig(config);
            if (config == null || string.IsNullOrWhiteSpace(config.supabaseUrl) || string.IsNullOrWhiteSpace(config.supabaseAnonKey))
                return AccountAuthSessionResult.Failed("Profile service configuration is unavailable.");

#if UNITY_INCLUDE_TESTS
            if (TestSessionOperationOverride != null)
                return await TestSessionOperationOverride(forceRefresh, allowCreate);
#endif

            // Preserve an unexpired process-memory JWT when callers bootstrap
            // more than once during the same app session.
            if (!forceRefresh && !string.IsNullOrWhiteSpace(cached.access_token) && !IsExpiringSoon(cached))
            {
                SupabaseAuthSessionContext.AccessToken = cached.access_token;
                return Success(restored: true);
            }

            if (string.IsNullOrWhiteSpace(cached.refresh_token))
                cached = LoadCachedSession();
            bool hadCachedSession = HasCachedSession;

            if (!string.IsNullOrWhiteSpace(cached.refresh_token))
            {
                AuthResponse refresh = await SendAuthRequestAsync(
                    config,
                    "/auth/v1/token?grant_type=refresh_token",
                    JsonUtility.ToJson(new RefreshRequest { refresh_token = cached.refresh_token }));
                if (refresh.success)
                    return StoreAndReturn(refresh.payload, restored: true);

                // Never replace an existing anonymous identity merely because its
                // refresh failed. Doing so would strand the player's profile behind
                // a newly-created auth user. Keep the refresh credential for a later
                // retry and surface the reconnect failure instead.
                return AccountAuthSessionResult.Failed(BuildFailureMessage(refresh, hadCachedSession: true));
            }

            if (!allowCreate)
                return AccountAuthSessionResult.Failed("No refreshable profile session is available.");

            // Only a genuinely fresh install may create a new anonymous identity.
            AuthResponse signup = await SendAuthRequestAsync(config, "/auth/v1/signup", "{}");
            if (!signup.success)
                return AccountAuthSessionResult.Failed(BuildFailureMessage(signup, hadCachedSession));

            return StoreAndReturn(signup.payload, restored: false);
        }

        public void ClearSession()
        {
            cached = default;
            SupabaseAuthSessionContext.AccessToken = "";
            SecureCredentialStore.Delete(SessionStorageKey);
            if (PlatformContext.ResolveOwnershipEnvironment() == OwnershipDeploymentEnvironment.DevnetOwnership)
                SecureCredentialStore.Delete(LegacySessionStorageKey);
        }

        private AccountAuthSessionResult StoreAndReturn(AuthResponseDto response, bool restored)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.access_token) || response.user == null || string.IsNullOrWhiteSpace(response.user.id))
                return AccountAuthSessionResult.Failed("Profile service returned an incomplete anonymous session.");

            cached = new CachedSession
            {
                access_token = response.access_token,
                refresh_token = response.refresh_token ?? "",
                user_id = response.user.id,
                expires_at_unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Mathf.Max(0, response.expires_in),
            };
            SupabaseAuthSessionContext.AccessToken = cached.access_token;
            // The access JWT is process-memory only. Persist just the refresh
            // credential (and non-authoritative subject metadata) so Android
            // Keystore storage cannot become a bearer-token cache.
            SecureCredentialStore.Set(SessionStorageKey, JsonUtility.ToJson(new CachedSession
            {
                refresh_token = cached.refresh_token,
                user_id = cached.user_id,
                expires_at_unix = 0,
            }));
            return Success(restored);
        }

        private AccountAuthSessionResult Success(bool restored)
        {
            return new AccountAuthSessionResult
            {
                success = true,
                accessToken = cached.access_token ?? "",
                authUserId = cached.user_id ?? "",
                restored = restored,
            };
        }

        private static bool IsExpiringSoon(CachedSession session)
        {
            return session.expires_at_unix <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + RefreshSkewSeconds;
        }

        private CachedSession LoadCachedSession()
        {
            string json = SecureCredentialStore.Get(SessionStorageKey);
            if (string.IsNullOrWhiteSpace(json) &&
                PlatformContext.ResolveOwnershipEnvironment() == OwnershipDeploymentEnvironment.DevnetOwnership)
            {
                json = SecureCredentialStore.Get(LegacySessionStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    SecureCredentialStore.Set(SessionStorageKey, json);
                    SecureCredentialStore.Delete(LegacySessionStorageKey);
                }
            }
            if (string.IsNullOrWhiteSpace(json))
                return default;

            CachedSession stored = JsonUtility.FromJson<CachedSession>(json);
            // Remove tokens written by the pre-hardened format immediately.
            // They are never trusted or used after an application restart.
            if (!string.IsNullOrWhiteSpace(stored.access_token))
            {
                stored.access_token = "";
                stored.expires_at_unix = 0;
                SecureCredentialStore.Set(SessionStorageKey, JsonUtility.ToJson(stored));
            }
            return stored;
        }

        private string SessionStorageKey => PlatformContext.BuildScopedStorageKey(ScopedSessionStoragePrefix);

        private async Task<AuthResponse> SendAuthRequestAsync(SupabaseConfigSO config, string relativePath, string jsonBody)
        {
            string url = config.supabaseUrl.TrimEnd('/') + relativePath;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || !uri.AbsolutePath.StartsWith("/auth/v1/", StringComparison.Ordinal))
                return AuthResponse.Failed(0, "invalid_auth_url", "The Supabase Auth URL is invalid.");

            try
            {
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? "{}"));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = Mathf.Max(1, config.requestTimeoutSeconds);
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("apikey", config.supabaseAnonKey);

                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
                    {
                        AuthResponse failure = ParseFailure(request.responseCode, request.downloadHandler != null ? request.downloadHandler.text : "");
                        if (enableDebugLogs || config.enableDebugLogs)
                        {
                            string code = string.IsNullOrWhiteSpace(failure.errorCode) ? "unknown" : failure.errorCode;
                            string message = string.IsNullOrWhiteSpace(failure.errorMessage) ? "No response message." : failure.errorMessage;
                            Debug.LogWarning($"[SupabaseAnonymousAuth] Auth request failed ({failure.responseCode}, {code}): {message}");
                        }
                        return failure;
                    }

                    return new AuthResponse
                    {
                        success = true,
                        payload = JsonUtility.FromJson<AuthResponseDto>(request.downloadHandler.text),
                    };
                }
            }
            catch (Exception exception)
            {
                if (enableDebugLogs || config.enableDebugLogs)
                    Debug.LogWarning($"[SupabaseAnonymousAuth] Auth request failed: {exception.Message}");
                return AuthResponse.Failed(0, "network_error", "The authentication request could not reach Supabase.");
            }
        }

        private static AuthResponse ParseFailure(long responseCode, string responseBody)
        {
            string code = "";
            string message = "";

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    AuthErrorDto error = JsonUtility.FromJson<AuthErrorDto>(responseBody);
                    if (error != null)
                    {
                        code = FirstNonEmpty(error.code, error.error_code, error.error);
                        message = FirstNonEmpty(error.message, error.msg, error.error_description);
                    }
                }
                catch (ArgumentException)
                {
                    // Preserve the HTTP status even when a proxy returns non-JSON.
                }
            }

            return AuthResponse.Failed(responseCode, code, message);
        }

        private static string BuildFailureMessage(AuthResponse failure, bool hadCachedSession)
        {
            string code = (failure.errorCode ?? "").Trim().ToLowerInvariant();
            string detail = (failure.errorMessage ?? "").Trim().ToLowerInvariant();

            if (code == "anonymous_provider_disabled" ||
                (detail.Contains("anonymous") && detail.Contains("disabled")))
            {
                return "Anonymous profile sign-in is disabled in Supabase Auth. Enable Anonymous Sign-Ins for this project and retry.";
            }

            if (failure.responseCode == 429)
                return "Profile sign-in is temporarily rate-limited. Wait a moment and retry.";

            if (failure.responseCode == 401 || failure.responseCode == 403)
                return "Profile authentication was rejected. Verify the Supabase URL and public API key.";

            if (failure.responseCode >= 500)
                return "Profile authentication is temporarily unavailable. Retry shortly.";

            if (failure.responseCode == 0)
            {
                return hadCachedSession
                    ? "Profile session could not be refreshed. Check your connection and retry."
                    : "Profile service could not be reached. Check your connection and retry.";
            }

            return hadCachedSession
                ? "Profile session could not be refreshed because authentication was rejected."
                : "Profile sign-in was rejected by the authentication service.";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return "";

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }

            return "";
        }

        [Serializable] private struct CachedSession { public string access_token; public string refresh_token; public string user_id; public long expires_at_unix; }
        [Serializable] private struct RefreshRequest { public string refresh_token; }
        [Serializable] private class AuthResponseDto { public string access_token; public string refresh_token; public int expires_in; public AuthUserDto user; }
        [Serializable] private class AuthUserDto { public string id; }
        [Serializable] private class AuthErrorDto { public string code; public string error_code; public string error; public string message; public string msg; public string error_description; }
        private struct AuthResponse
        {
            public bool success;
            public AuthResponseDto payload;
            public long responseCode;
            public string errorCode;
            public string errorMessage;

            public static AuthResponse Failed(long responseCode, string errorCode, string errorMessage)
            {
                return new AuthResponse
                {
                    success = false,
                    responseCode = responseCode,
                    errorCode = errorCode ?? "",
                    errorMessage = errorMessage ?? "",
                };
            }
        }
    }

    /// <summary>In-memory bearer shared by authenticated Edge Function callers.</summary>
    public static class SupabaseAuthSessionContext
    {
        public static string AccessToken { get; internal set; } = "";
    }

    /// <summary>Shared secure credential facade (Android Keystore-backed in device builds).</summary>
    public static class SecureCredentialStore
    {
        private const string EditorPrefix = "SignalJam_DevOnly_Secure_";

        public static string Get(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.signaljam.solana.mwa.SignalJamSecureStorage"))
                return store.CallStatic<string>("get", activity, key) ?? "";
#elif UNITY_EDITOR
            return PlayerPrefs.GetString(EditorPrefix + key, "");
#else
            return "";
#endif
        }

        public static void Set(string key, string value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.signaljam.solana.mwa.SignalJamSecureStorage"))
                store.CallStatic("set", activity, key, value ?? "");
#elif UNITY_EDITOR
            PlayerPrefs.SetString(EditorPrefix + key, value ?? "");
            PlayerPrefs.Save();
#endif
        }

        public static void Delete(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass("com.signaljam.solana.mwa.SignalJamSecureStorage"))
                store.CallStatic("delete", activity, key);
#elif UNITY_EDITOR
            PlayerPrefs.DeleteKey(EditorPrefix + key);
            PlayerPrefs.Save();
#endif
        }
    }
}
