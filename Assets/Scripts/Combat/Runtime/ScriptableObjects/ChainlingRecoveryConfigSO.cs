using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Lever board for server-authoritative passive Chainling HP recovery.
    /// The client uses this board for presentation only; Supabase applies health changes
    /// against the matching server board and a Solana-derived timestamp.
    /// </summary>
    [CreateAssetMenu(fileName = "ChainlingRecoveryConfig", menuName = "Chainbound/Combat/Config/Chainling Recovery Config")]
    public sealed class ChainlingRecoveryConfigSO : ScriptableObject
    {
        public const int BasisPoints = 10000;

        [Header("Identity")]
        [Min(1)] public int configVersion = 1;

        [Header("Recovery")]
        [Tooltip("Disabling this stops new passive recovery while preserving all saved HP.")]
        public bool enabled = true;
        [Tooltip("Elapsed time required for one recovery tick.")]
        [Min(60)] public int intervalSeconds = 1800;
        [Tooltip("Max-HP fraction restored per completed tick. 0.10 = 10%.")]
        [Range(0f, 1f)] public float maxHpFractionPerInterval = 0.10f;
        [Tooltip("Maximum completed ticks applied in one authoritative read. Prevents unbounded catch-up work.")]
        [Min(1)] public int maxCatchupIntervals = 48;
        [Tooltip("When enabled, only the active party recovers passively.")]
        public bool partyMembersOnly = true;

        public int HealBasisPoints => Mathf.Clamp(Mathf.RoundToInt(maxHpFractionPerInterval * BasisPoints), 0, BasisPoints);

        /// <summary>Returns a ceil-rounded tick amount so a valid, damaged Chainling always progresses.</summary>
        public int ResolveHealPerInterval(int maxHp)
        {
            if (!enabled || maxHp <= 0 || HealBasisPoints <= 0)
                return 0;

            return Mathf.Max(1, Mathf.CeilToInt(maxHp * (HealBasisPoints / (float)BasisPoints)));
        }

        public string ComputeConfigHash() => BalanceConfigHashUtility.HashChainlingRecoveryConfig(this);

        public static ChainlingRecoveryConfigSO CreateRuntimeDefault()
        {
            var config = CreateInstance<ChainlingRecoveryConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.ResetToDefaults();
            return config;
        }

        public void ResetToDefaults()
        {
            configVersion = 1;
            enabled = true;
            intervalSeconds = 1800;
            maxHpFractionPerInterval = 0.10f;
            maxCatchupIntervals = 48;
            partyMembersOnly = true;
        }

        public void ApplyServerOverride(
            int version,
            bool isEnabled,
            int interval,
            int healBasisPoints,
            int catchupIntervals,
            bool partyOnly)
        {
            configVersion = Mathf.Max(1, version);
            enabled = isEnabled;
            intervalSeconds = Mathf.Clamp(interval, 60, 86400);
            maxHpFractionPerInterval = Mathf.Clamp01(healBasisPoints / (float)BasisPoints);
            maxCatchupIntervals = Mathf.Clamp(catchupIntervals, 1, 1000);
            partyMembersOnly = partyOnly;
        }

        private void OnValidate()
        {
            configVersion = Mathf.Max(1, configVersion);
            intervalSeconds = Mathf.Clamp(intervalSeconds, 60, 86400);
            maxHpFractionPerInterval = Mathf.Clamp01(maxHpFractionPerInterval);
            maxCatchupIntervals = Mathf.Clamp(maxCatchupIntervals, 1, 1000);
        }
    }
}
