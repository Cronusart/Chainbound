using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lever board for casual matchmaking and the online turn deadline.
    /// The PvP turn limit lives here so the shared resolver board can stay unwired for wild battles.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchmakingConfig", menuName = "Chainbound/Combat/Config/Matchmaking Config")]
    public class MatchmakingConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int configVersion = 1;

        [Header("Queues")]
        public bool casualEnabled = true;
        public bool rankedEnabled;
        public bool requireWalletForCasual;
        public bool requireWalletForRanked = true;

        [Header("Queue and turns")]
        [Min(1)] public int queueTimeoutSeconds = 120;
        [Min(1)] public int turnForfeitMissedTurns = 2;
        [Tooltip("Seconds a seat has to submit each casual turn. Owned here, not on CombatResolverConfigSO.")]
        [Min(1)] public int turnActionTimeLimitSeconds = 60;
        [Tooltip("0 requires an exact Application.version match between paired clients.")]
        [Min(0)] public int maxAppVersionSkew;
        public string defaultCasualRulesetId = "pvp_casual";
        [Min(0)] public int rematchCooldownSeconds = 30;

        [Header("Later phases (stored now so the hash stays stable)")]
        [Min(1)] public int lobbyMaxPlayers = 16;
        [Min(0)] public int ratingBandStart = 100;
        [Min(0)] public int widenPerSecond = 1;
        [Min(0)] public int bandCap = 400;
        [Tooltip("Seconds after the first forensic hash before a one-sided result becomes disputed.")]
        [Min(1)] public int resultWindowSeconds = 45;
        [Tooltip("Seconds a created match may stay unlocked before it is cancelled.")]
        [Min(1)] public int lockDeadlineSeconds = 30;

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashMatchmakingConfig(this);

        public static MatchmakingConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<MatchmakingConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 1;
            casualEnabled = true;
            rankedEnabled = false;
            requireWalletForCasual = false;
            requireWalletForRanked = true;
            queueTimeoutSeconds = 120;
            turnForfeitMissedTurns = 2;
            turnActionTimeLimitSeconds = 60;
            maxAppVersionSkew = 0;
            defaultCasualRulesetId = "pvp_casual";
            rematchCooldownSeconds = 30;
            lobbyMaxPlayers = 16;
            ratingBandStart = 100;
            widenPerSecond = 1;
            bandCap = 400;
            resultWindowSeconds = 45;
            lockDeadlineSeconds = 30;
        }

        public static bool AcceptsServerBoard(MatchmakingConfigSO baked, MatchmakingConfigSO probe, string claimedHash)
        {
            if (baked == null || probe == null || string.IsNullOrWhiteSpace(claimedHash))
                return false;

            string bakedHash = baked.ComputeConfigHash();
            if (!string.Equals(bakedHash, claimedHash, System.StringComparison.OrdinalIgnoreCase))
                return false;

            return string.Equals(probe.ComputeConfigHash(), claimedHash, System.StringComparison.OrdinalIgnoreCase);
        }

        public void ApplyServerOverride(
            int version,
            bool casual,
            bool ranked,
            bool walletCasual,
            bool walletRanked,
            int queueTimeout,
            int forfeitMisses,
            int turnLimit,
            int versionSkew,
            string rulesetId,
            int rematchCooldown,
            int lobbyMax,
            int ratingStart,
            int widen,
            int cap,
            int resultWindow,
            int lockDeadline)
        {
            configVersion = Mathf.Max(1, version);
            casualEnabled = casual;
            rankedEnabled = ranked;
            requireWalletForCasual = walletCasual;
            requireWalletForRanked = walletRanked;
            queueTimeoutSeconds = Mathf.Max(1, queueTimeout);
            turnForfeitMissedTurns = Mathf.Max(1, forfeitMisses);
            turnActionTimeLimitSeconds = Mathf.Max(1, turnLimit);
            maxAppVersionSkew = Mathf.Max(0, versionSkew);
            defaultCasualRulesetId = string.IsNullOrWhiteSpace(rulesetId) ? "pvp_casual" : rulesetId.Trim();
            rematchCooldownSeconds = Mathf.Max(0, rematchCooldown);
            lobbyMaxPlayers = Mathf.Max(1, lobbyMax);
            ratingBandStart = Mathf.Max(0, ratingStart);
            widenPerSecond = Mathf.Max(0, widen);
            bandCap = Mathf.Max(0, cap);
            resultWindowSeconds = Mathf.Max(1, resultWindow);
            lockDeadlineSeconds = Mathf.Max(1, lockDeadline);
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            queueTimeoutSeconds = Mathf.Max(1, queueTimeoutSeconds);
            turnForfeitMissedTurns = Mathf.Max(1, turnForfeitMissedTurns);
            turnActionTimeLimitSeconds = Mathf.Max(1, turnActionTimeLimitSeconds);
            maxAppVersionSkew = Mathf.Max(0, maxAppVersionSkew);
            rematchCooldownSeconds = Mathf.Max(0, rematchCooldownSeconds);
            lobbyMaxPlayers = Mathf.Max(1, lobbyMaxPlayers);
            ratingBandStart = Mathf.Max(0, ratingBandStart);
            widenPerSecond = Mathf.Max(0, widenPerSecond);
            bandCap = Mathf.Max(0, bandCap);
            resultWindowSeconds = Mathf.Max(1, resultWindowSeconds);
            lockDeadlineSeconds = Mathf.Max(1, lockDeadlineSeconds);
            if (string.IsNullOrWhiteSpace(defaultCasualRulesetId))
                defaultCasualRulesetId = "pvp_casual";
        }
    }
}
