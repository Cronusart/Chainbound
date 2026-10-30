using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class DailyLoginClaimResult
    {
        public bool ok;
        public bool alreadyClaimed;
        public long ramAmount;
        public string itemId;
        public int itemQuantity;
        public long ramBalance;
        public long ramBalanceVersion;
        public int streakAfter;

        /// <summary><c>stack_full</c>, <c>not_unlocked</c>, <c>disabled</c>, <c>cluster_unavailable</c> or a transport error.</summary>
        public string error;

        public static DailyLoginClaimResult Failed(string message)
        {
            return new DailyLoginClaimResult
            {
                ok = false,
                itemId = "",
                error = message ?? "",
            };
        }
    }

    /// <summary>
    /// Daily gift state for the signed-in profile (Docs/Specs/Economy/DAILY_LOGIN_REWARDS_v1.md).
    /// The server owns eligibility, the UTC day, the RAM amount and the item. This
    /// service only mirrors <c>profile_get_daily_login_status</c> and posts
    /// <c>profile_claim_daily_login</c>; presenters listen to its events.
    /// The phone clock never grants the gift: reaching <see cref="NextClaimAtUtc"/>
    /// only requests a fresh status.
    /// </summary>
    public sealed class DailyLoginRewardService : MonoBehaviour
    {
        public const string StatusFunctionName = "profile_get_daily_login_status";
        public const string ClaimFunctionName = "profile_claim_daily_login";
        public const string StackFullError = "stack_full";
        public const string NotUnlockedError = "not_unlocked";
        public const string DisabledError = "disabled";

        private const float TimerRefreshCooldownSeconds = 30f;

        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        private Task<bool> refreshTask;
        private Task<DailyLoginClaimResult> claimTask;
        private int stateGeneration;
        private string statusProfileId = "";
        private bool timerRefreshRequested;
        private float nextTimerRefreshAllowedAt;
        private bool applicationWasInterrupted;
        private AccountSessionService subscribedSessions;

        /// <summary>A status payload has been confirmed by the server. False after a failed fetch.</summary>
        public bool StatusLoaded { get; private set; }
        public bool Enabled { get; private set; }
        public bool Unlocked { get; private set; }

        /// <summary>True only when the server said this profile can claim today. Gift boxes follow this.</summary>
        public bool CanClaim { get; private set; }

        public DateTime ServerNowUtc { get; private set; }

        /// <summary>Start of the next UTC day once today is claimed; null while claimable or locked.</summary>
        public DateTime? NextClaimAtUtc { get; private set; }

        /// <summary><c>serverNow - DateTime.UtcNow</c> when the last payload arrived.</summary>
        public TimeSpan UtcOffset { get; private set; }

        public long RamAmount { get; private set; }
        public string ItemId { get; private set; } = "";
        public int ItemQuantity { get; private set; }

        /// <summary>Stored for a later pass. v1 does not render it.</summary>
        public int CurrentStreak { get; private set; }

        public bool IsClaimInFlight => claimTask != null && !claimTask.IsCompleted;
        public DateTime EstimatedServerNowUtc => DateTime.UtcNow + UtcOffset;

        public event Action OnStatusChanged;

        /// <summary>First grant of a UTC day only. A replay never fires this.</summary>
        public event Action<DailyLoginClaimResult> OnClaimSucceeded;

        public event Action<string> OnClaimRejected;

        public static DailyLoginRewardService EnsureInstance()
        {
            DailyLoginRewardService existing = FindAnyObjectByType<DailyLoginRewardService>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            Transform host = ResolveHost();
            DailyLoginRewardService created = host.gameObject.AddComponent<DailyLoginRewardService>();
            created.EnsureConfig();
            return created;
        }

        public void SetConfig(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        /// <summary>Asks the server for today's status. Concurrent callers share one request.</summary>
        public Task<bool> RefreshAsync()
        {
            if (refreshTask != null && !refreshTask.IsCompleted)
                return refreshTask;

            refreshTask = RefreshCoreAsync();
            return refreshTask;
        }

        /// <summary>Claims today's gift. A second call while one is in flight returns the same request.</summary>
        public Task<DailyLoginClaimResult> ClaimAsync()
        {
            if (claimTask != null && !claimTask.IsCompleted)
                return claimTask;

            claimTask = ClaimCoreAsync();
            return claimTask;
        }

        private void OnEnable()
        {
            TrySubscribeSessions();
        }

        private void OnDisable()
        {
            if (subscribedSessions != null)
                subscribedSessions.OnSessionChanged -= HandleSessionChanged;
            subscribedSessions = null;
        }

        private void Update()
        {
            if (subscribedSessions == null)
                TrySubscribeSessions();

            if (!StatusLoaded || CanClaim || !NextClaimAtUtc.HasValue || timerRefreshRequested)
                return;
            if (Time.unscaledTime < nextTimerRefreshAllowedAt || EstimatedServerNowUtc < NextClaimAtUtc.Value)
                return;

            timerRefreshRequested = true;
            nextTimerRefreshAllowedAt = Time.unscaledTime + TimerRefreshCooldownSeconds;
            _ = RefreshAsync();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                applicationWasInterrupted = true;
                return;
            }

            RefreshAfterInterruption();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                applicationWasInterrupted = true;
                return;
            }

            RefreshAfterInterruption();
        }

        private void RefreshAfterInterruption()
        {
            if (!applicationWasInterrupted)
                return;

            applicationWasInterrupted = false;
            if (!string.IsNullOrEmpty(ResolveProfileId()))
                _ = RefreshAsync();
        }

        private void TrySubscribeSessions()
        {
            AccountSessionService sessions = AccountSessionService.Instance;
            if (sessions == null || sessions == subscribedSessions)
                return;

            if (subscribedSessions != null)
                subscribedSessions.OnSessionChanged -= HandleSessionChanged;
            subscribedSessions = sessions;
            subscribedSessions.OnSessionChanged += HandleSessionChanged;
        }

        private void HandleSessionChanged(PlayerAccountSession session)
        {
            string profileId = session != null ? (session.PlayerProfileId ?? "").Trim() : "";
            bool profileChanged = !string.Equals(profileId, statusProfileId, StringComparison.Ordinal);
            if (profileChanged && (StatusLoaded || statusProfileId.Length > 0))
                ResetStatus();

            bool online = session != null && session.hasSession && !session.fetchError && profileId.Length > 0;
            if (online && (profileChanged || !StatusLoaded))
                _ = RefreshAsync();
        }

        private async Task<bool> RefreshCoreAsync()
        {
            EnsureConfig();
            int generation = stateGeneration;
            string profileId = ResolveProfileId();
            if (supabaseConfig == null)
            {
                MarkUnavailable(generation, "supabase_config_missing");
                return false;
            }

            try
            {
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    StatusFunctionName,
                    BuildRequestJson(profileId));

                if (!response.IsSuccess)
                {
                    MarkUnavailable(generation, $"{StatusFunctionName} returned {response.statusCode}: {response.body}");
                    return false;
                }

                DailyLoginResponse payload = JsonUtility.FromJson<DailyLoginResponse>(response.body);
                if (payload == null || !payload.success || !payload.ok)
                {
                    MarkUnavailable(generation, "daily_login_status_invalid_payload");
                    return false;
                }

                // A claim or profile switch landed while this request was out; its
                // state is newer than this payload.
                if (generation != stateGeneration)
                    return StatusLoaded;

                statusProfileId = profileId;
                StatusLoaded = true;
                Enabled = payload.enabled;
                Unlocked = payload.unlocked;
                CanClaim = payload.can_claim;
                RamAmount = payload.ram_amount;
                ItemId = payload.item_id ?? "";
                ItemQuantity = payload.item_quantity;
                CurrentStreak = payload.current_streak;
                ApplyServerClock(payload.server_now, payload.next_claim_at);
                OnStatusChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                MarkUnavailable(generation, $"RefreshAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task<DailyLoginClaimResult> ClaimCoreAsync()
        {
            EnsureConfig();
            if (supabaseConfig == null)
                return Reject(DailyLoginClaimResult.Failed("supabase_config_missing"));

            string profileId = ResolveProfileId();
            string wallet = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.WalletAddress ?? "").Trim()
                : "";

            try
            {
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    ClaimFunctionName,
                    BuildRequestJson(profileId));

                DailyLoginResponse payload = TryParse(response.body);
                if (!response.IsSuccess || payload == null || !payload.success || !payload.ok)
                {
                    string error = payload != null && !string.IsNullOrWhiteSpace(payload.error)
                        ? payload.error
                        : $"daily_login_http_{response.statusCode}";
                    LogWarning($"{ClaimFunctionName} returned {response.statusCode}: {response.body}");
                    ApplyRejection(error);
                    return Reject(DailyLoginClaimResult.Failed(error));
                }

                if (payload.ram_balance_version > 0)
                {
                    PlayerRamBalanceStore.TryApplyAuthoritative(
                        profileId,
                        payload.ram_balance,
                        payload.ram_balance_version);
                }

                if (!payload.already_claimed)
                {
                    InvalidateInventoryCache();
                    PlayerProfileReadStore.ResolveOrCreate().MarkInventoryStale(profileId, wallet);
                }

                var result = new DailyLoginClaimResult
                {
                    ok = true,
                    alreadyClaimed = payload.already_claimed,
                    ramAmount = payload.ram_amount,
                    itemId = payload.item_id ?? "",
                    itemQuantity = payload.item_quantity,
                    ramBalance = payload.ram_balance,
                    ramBalanceVersion = payload.ram_balance_version,
                    streakAfter = payload.streak_after,
                    error = "",
                };

                stateGeneration++;
                statusProfileId = profileId;
                StatusLoaded = true;
                Enabled = true;
                Unlocked = true;
                CanClaim = false;
                if (payload.streak_after > 0)
                    CurrentStreak = payload.streak_after;
                ApplyServerClock(payload.server_now, payload.next_claim_at);

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[DailyLoginRewardService] Claimed (already_claimed={payload.already_claimed}, ram={payload.ram_balance}, next={payload.next_claim_at}).",
                        this);
                }

                OnStatusChanged?.Invoke();
                if (!result.alreadyClaimed)
                    OnClaimSucceeded?.Invoke(result);
                return result;
            }
            catch (Exception ex)
            {
                LogWarning($"ClaimAsync failed: {ex.Message}");
                return Reject(DailyLoginClaimResult.Failed("daily_login_exception"));
            }
        }

        private void ApplyRejection(string error)
        {
            if (string.Equals(error, NotUnlockedError, StringComparison.Ordinal))
            {
                stateGeneration++;
                Unlocked = false;
                CanClaim = false;
                OnStatusChanged?.Invoke();
            }
            else if (string.Equals(error, DisabledError, StringComparison.Ordinal))
            {
                stateGeneration++;
                Enabled = false;
                CanClaim = false;
                OnStatusChanged?.Invoke();
            }

            // stack_full leaves the day unspent and the boxes up.
        }

        private DailyLoginClaimResult Reject(DailyLoginClaimResult result)
        {
            OnClaimRejected?.Invoke(result.error);
            return result;
        }

        private void ApplyServerClock(string serverNow, string nextClaimAt)
        {
            DateTime? previousNext = NextClaimAtUtc;
            if (TryParseUtc(serverNow, out DateTime now))
            {
                ServerNowUtc = now;
                UtcOffset = now - DateTime.UtcNow;
            }

            NextClaimAtUtc = TryParseUtc(nextClaimAt, out DateTime next) ? next : (DateTime?)null;
            if (NextClaimAtUtc != previousNext)
                timerRefreshRequested = false;
        }

        private void MarkUnavailable(int generation, string reason)
        {
            LogWarning(reason);
            if (generation != stateGeneration)
                return;

            bool changed = StatusLoaded || CanClaim;
            StatusLoaded = false;
            CanClaim = false;
            if (changed)
                OnStatusChanged?.Invoke();
        }

        private void ResetStatus()
        {
            stateGeneration++;
            statusProfileId = "";
            StatusLoaded = false;
            Enabled = false;
            Unlocked = false;
            CanClaim = false;
            NextClaimAtUtc = null;
            CurrentStreak = 0;
            timerRefreshRequested = false;
            OnStatusChanged?.Invoke();
        }

        private static bool TryParseUtc(string value, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset parsed))
                return false;
            utc = parsed.UtcDateTime;
            return true;
        }

        private static string ResolveProfileId()
        {
            string profileId = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.PlayerProfileId ?? "").Trim()
                : "";
            if (profileId.Length == 0 && AccountSessionService.Instance != null)
                profileId = (AccountSessionService.Instance.CurrentSession?.PlayerProfileId ?? "").Trim();
            return profileId;
        }

        private static string BuildRequestJson(string profileId)
        {
            // The edge function resolves the claimant from the JWT; a mismatched
            // profile id is rejected, never trusted.
            return JsonUtility.ToJson(new DailyLoginRequest { player_profile_id = profileId ?? "" });
        }

        private static DailyLoginResponse TryParse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                return JsonUtility.FromJson<DailyLoginResponse>(body);
            }
            catch
            {
                return null;
            }
        }

        private void EnsureConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
        }

        private static void InvalidateInventoryCache()
        {
            CoreBagItemInventoryService[] bags = FindObjectsByType<CoreBagItemInventoryService>(FindObjectsInactive.Include);
            for (int i = 0; i < bags.Length; i++)
            {
                if (bags[i] != null)
                    bags[i].ClearItemInventoryCache();
            }
        }

        private static Transform ResolveHost()
        {
            if (GameSessionContext.Instance != null)
                return GameSessionContext.Instance.transform;

            GameObject go = new GameObject("DailyLoginRewardService");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            return go.transform;
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[DailyLoginRewardService] {message}", this);
        }

        [Serializable]
        private class DailyLoginRequest
        {
            public string player_profile_id;
        }

        [Serializable]
        private class DailyLoginResponse
        {
            public bool success;
            public bool ok;
            public bool already_claimed;
            public bool enabled;
            public bool unlocked;
            public bool can_claim;
            public string utc_day;
            public string server_now;
            public string next_claim_at;
            public int current_streak;
            public long ram_amount;
            public string item_id;
            public int item_quantity;
            public long ram_balance;
            public long ram_balance_version;
            public int streak_after;
            public string error;
        }
    }
}
