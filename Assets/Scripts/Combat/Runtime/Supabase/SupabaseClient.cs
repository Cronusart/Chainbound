using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lightweight HTTP client for calling Supabase Edge Functions from Unity.
    /// Uses UnityWebRequest under the hood - no external dependencies.
    /// </summary>
    public static class SupabaseClient
    {
        private const int MaxAuditEntries = 64;

        private static readonly object AuditLock = new object();
        private static readonly List<string> RequestAuditUrls = new List<string>(MaxAuditEntries);

        public struct Response
        {
            public int statusCode;
            public string body;
            public bool IsSuccess => statusCode >= 200 && statusCode < 300;
        }

        /// <summary>
        /// Clear captured Supabase request URLs used by dev self-tests.
        /// </summary>
        public static void ClearRequestAudit()
        {
            lock (AuditLock)
            {
                RequestAuditUrls.Clear();
            }
        }

        /// <summary>
        /// Snapshot of recently captured Supabase request URLs.
        /// </summary>
        public static IReadOnlyList<string> GetRequestAuditSnapshot()
        {
            lock (AuditLock)
            {
                return RequestAuditUrls.ToArray();
            }
        }

        /// <summary>
        /// True only when URL targets Supabase Edge Functions under /functions/v1.
        /// </summary>
        public static bool IsAllowedEdgeFunctionUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            string path = uri.AbsolutePath.ToLowerInvariant();

            if (path.Contains("/rest/v1") || path.Contains("/auth/v1"))
                return false;

            return path == "/functions/v1" || path.StartsWith("/functions/v1/");
        }

        /// <summary>
        /// POST JSON to a Supabase Edge Function and return the response.
        /// Retries on transient DNS/connection failures (e.g. "Cannot resolve destination host").
        /// </summary>
        public static async Task<Response> PostJsonAsync(
            SupabaseConfigSO config,
            string functionName,
            string jsonBody)
        {
            return await PostJsonAsync(config, functionName, jsonBody, SupabaseAuthSessionContext.AccessToken);
        }

        /// <summary>
        /// POST JSON to an Edge Function with a user access token. The anon key is
        /// still supplied as the Supabase API key, but it is never used as the
        /// Authorization bearer for profile-authorized requests.
        /// </summary>
        public static async Task<Response> PostJsonAsync(
            SupabaseConfigSO config,
            string functionName,
            string jsonBody,
            string accessToken)
        {
            config = PlatformContext.ResolveSupabaseConfig(config);
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (functionName != null && functionName.StartsWith("profile_", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(accessToken))
            {
                return new Response
                {
                    statusCode = 0,
                    body = "Authenticated profile request requires a Supabase user session."
                };
            }

            string url = BuildFunctionUrl(config, functionName);
            RecordRequestUrl(url);

            if (!IsAllowedEdgeFunctionUrl(url))
            {
                string error = $"[SupabaseClient] Blocked non-Edge Supabase URL: {url}";
                Debug.LogError(error);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException(error);
#else
                return new Response { statusCode = 0, body = error };
#endif
            }

            int maxRetries = Mathf.Max(0, config.maxRetries);
            int attempt = 0;

            while (true)
            {
                attempt++;
                if (config.enableDebugLogs && attempt > 1)
                    Debug.Log($"[SupabaseClient] POST {url} (attempt {attempt}/{maxRetries + 1})");

                if (attempt == 1 && config.enableDebugLogs)
                    Debug.Log($"[SupabaseClient] POST {url}");

                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

                using var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Content-Type", "application/json");
                string bearer = string.IsNullOrWhiteSpace(accessToken)
                    ? config.supabaseAnonKey
                    : accessToken.Trim();
                request.SetRequestHeader("Authorization", $"Bearer {bearer}");
                request.SetRequestHeader("apikey", config.supabaseAnonKey);

                request.timeout = config.requestTimeoutSeconds;

                var op = request.SendWebRequest();

                while (!op.isDone)
                    await Task.Yield();

                var response = new Response
                {
                    statusCode = (int)request.responseCode,
                    body = request.downloadHandler?.text ?? ""
                };

                if (config.enableDebugLogs)
                {
                    Debug.Log($"[SupabaseClient] {response.statusCode} <- {functionName}" +
                              (response.IsSuccess ? "" : $" | {response.body}"));
                }

                bool isConnectionError = request.result == UnityWebRequest.Result.ConnectionError ||
                                       request.result == UnityWebRequest.Result.DataProcessingError;

                if (isConnectionError)
                {
                    Debug.LogWarning($"[SupabaseClient] Network error calling {functionName}: {request.error}");

                    if (attempt <= maxRetries)
                    {
                        float delaySeconds = Mathf.Pow(1.5f, attempt - 1);
                        if (config.enableDebugLogs)
                            Debug.Log($"[SupabaseClient] Retrying in {delaySeconds:F1}s...");
                        await Task.Delay((int)(delaySeconds * 1000));
                        continue;
                    }
                }

                return response;
            }
        }

        private static string BuildFunctionUrl(SupabaseConfigSO config, string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("functionName is required.", nameof(functionName));

            string normalizedBase = config.ResolvedEdgeFunctionBaseUrl.TrimEnd('/');
            string normalizedFunction = functionName.Trim('/');
            return $"{normalizedBase}/{normalizedFunction}";
        }

        private static void RecordRequestUrl(string url)
        {
            lock (AuditLock)
            {
                if (RequestAuditUrls.Count >= MaxAuditEntries)
                    RequestAuditUrls.RemoveAt(0);

                RequestAuditUrls.Add(url);
            }
        }
    }
}
