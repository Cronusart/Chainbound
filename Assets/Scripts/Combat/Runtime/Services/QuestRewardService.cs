using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class QuestRewardResult
    {
        public bool ok;
        public bool alreadyApplied;
        public long ramBalance;
        public long ramBalanceVersion;

        /// <summary>RAM the reward asks for, and what the 100/UTC-day earn cap let through.</summary>
        public long requestedRam;
        public long appliedRam;

        /// <summary><c>applied</c>, <c>daily_cap_partial</c>, <c>daily_cap</c> or <c>no_ram</c>.</summary>
        public string reason;
        public string error;

        public static QuestRewardResult Ok(
            bool alreadyApplied,
            long ramBalance,
            long ramBalanceVersion,
            long requestedRam = 0,
            long appliedRam = 0,
            string reason = "")
        {
            return new QuestRewardResult
            {
                ok = true,
                alreadyApplied = alreadyApplied,
                ramBalance = ramBalance,
                ramBalanceVersion = ramBalanceVersion,
                requestedRam = requestedRam,
                appliedRam = appliedRam,
                reason = reason ?? "",
                error = "",
            };
        }

        public static QuestRewardResult Failed(string message)
        {
            return new QuestRewardResult
            {
                ok = false,
                alreadyApplied = false,
                error = message ?? "",
            };
        }
    }

    /// <summary>
    /// Posts a catalog reward id to <c>profile_apply_quest_reward</c>.
    /// The server owns the consumed item, granted item, and RAM amount.
    /// </summary>
    public sealed class QuestRewardService : MonoBehaviour
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public static QuestRewardService EnsureInstance()
        {
            QuestRewardService existing = FindFirstObjectByType<QuestRewardService>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            Transform host = ResolveHost();
            QuestRewardService created = host.gameObject.AddComponent<QuestRewardService>();
            created.EnsureConfig();
            return created;
        }

        public async Task<QuestRewardResult> ApplyAsync(string rewardId)
        {
            EnsureConfig();
            if (supabaseConfig == null)
                return QuestRewardResult.Failed("supabase_config_missing");

            string id = (rewardId ?? "").Trim();
            if (id.Length == 0)
                return QuestRewardResult.Failed("reward_id_required");

            string profileId = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.PlayerProfileId ?? "").Trim()
                : "";
            string wallet = GameSessionContext.Instance != null
                ? (GameSessionContext.Instance.WalletAddress ?? "").Trim()
                : "";

            try
            {
                string json = JsonUtility.ToJson(new QuestRewardRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                    reward_id = id,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_apply_quest_reward",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"profile_apply_quest_reward returned {response.statusCode}: {response.body}");
                    string rpcError = TryExtractError(response.body);
                    return QuestRewardResult.Failed(
                        !string.IsNullOrWhiteSpace(rpcError)
                            ? rpcError
                            : $"quest_reward_http_{response.statusCode}");
                }

                QuestRewardResponse payload = JsonUtility.FromJson<QuestRewardResponse>(response.body);
                if (payload == null || !payload.success || !payload.ok)
                {
                    return QuestRewardResult.Failed(
                        payload != null && !string.IsNullOrWhiteSpace(payload.error)
                            ? payload.error
                            : "quest_reward_invalid_payload");
                }

                InvalidateInventoryCache();
                PlayerProfileReadStore.ResolveOrCreate().MarkInventoryStale(profileId, wallet);
                if (payload.ram_balance_version > 0)
                {
                    PlayerRamBalanceStore.TryApplyAuthoritative(
                        profileId,
                        payload.ram_balance,
                        payload.ram_balance_version);
                }

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[QuestRewardService] Applied '{id}' (already_applied={payload.already_applied}, ram={payload.ram_balance}, applied_ram={payload.applied_ram}/{payload.requested_ram}, reason={payload.reason}).",
                        this);
                }

                return QuestRewardResult.Ok(
                    payload.already_applied,
                    payload.ram_balance,
                    payload.ram_balance_version,
                    payload.requested_ram,
                    payload.applied_ram,
                    payload.reason);
            }
            catch (Exception ex)
            {
                LogWarning($"ApplyAsync failed: {ex.Message}");
                return QuestRewardResult.Failed("quest_reward_exception");
            }
        }

        private void EnsureConfig()
        {
            if (supabaseConfig == null)
                supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
        }

        private static void InvalidateInventoryCache()
        {
            CoreBagItemInventoryService[] bags = FindObjectsByType<CoreBagItemInventoryService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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

            GameObject go = new GameObject("QuestRewardService");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            return go.transform;
        }

        private static string TryExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "";

            try
            {
                QuestRewardResponse payload = JsonUtility.FromJson<QuestRewardResponse>(body);
                return payload != null ? payload.error : "";
            }
            catch
            {
                return "";
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[QuestRewardService] {message}", this);
        }

        [Serializable]
        private class QuestRewardRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string reward_id;
        }

        [Serializable]
        private class QuestRewardResponse
        {
            public bool success;
            public bool ok;
            public bool already_applied;
            public long ram_balance;
            public long ram_balance_version;
            public long requested_ram;
            public long applied_ram;
            public string reason;
            public string error;
        }
    }
}
