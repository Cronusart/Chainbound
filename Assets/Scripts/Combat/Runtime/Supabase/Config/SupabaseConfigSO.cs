using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "SupabaseConfig", menuName = "Chainbound/Platform/Supabase Config")]
    public class SupabaseConfigSO : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("Supabase project URL (e.g. https://xxxx.supabase.co).")]
        public string supabaseUrl = "https://jhzqcbrzkausrrtcavuu.supabase.co";

        [Tooltip("Supabase anon/public key. NEVER put the service role key here.")]
        public string supabaseAnonKey = "";

        [Header("Edge Functions")]
        [Tooltip("Base URL for Edge Functions. Leave empty to auto-derive from supabaseUrl.")]
        public string edgeFunctionBaseUrl = "";

        [Header("Requests")]
        [Tooltip("HTTP request timeout in seconds.")]
        public int requestTimeoutSeconds = 10;

        [Tooltip("Max retries for transient DNS/connection failures (e.g. 'Cannot resolve destination host'). 0 = no retries.")]
        public int maxRetries = 2;

        [Header("Debug")]
        [Tooltip("Log request URLs, status codes, and response bodies (never keys).")]
        public bool enableDebugLogs = true;

        /// <summary>
        /// Resolved Edge Function base URL. Falls back to {supabaseUrl}/functions/v1.
        /// </summary>
        public string ResolvedEdgeFunctionBaseUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(edgeFunctionBaseUrl))
                    return edgeFunctionBaseUrl.TrimEnd('/');
                return supabaseUrl.TrimEnd('/') + "/functions/v1";
            }
        }
    }
}
