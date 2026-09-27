using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Tunables for out-of-battle field consumables (revive fraction, etc.).
    /// Heal amounts stay on <see cref="PlayerItemDefinition"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "FieldItemConfig", menuName = "Chainbound/Combat/Config/Field Item Config")]
    public class FieldItemConfigSO : ScriptableObject
    {
        [Header("Revive")]
        [Range(0.05f, 1f)]
        [Tooltip("Fraction of max HP restored by Chainling Reboot when fainted.")]
        public float reviveHpFraction = 0.5f;

        [Tooltip("Minimum HP after revive (clamped to max).")]
        public int reviveMinHp = 1;

        public int ResolveReviveHp(int maxHp)
        {
            if (maxHp <= 0)
                return 0;

            int fromFraction = Mathf.RoundToInt(maxHp * Mathf.Clamp01(reviveHpFraction));
            return Mathf.Clamp(Mathf.Max(reviveMinHp, fromFraction), 1, maxHp);
        }

        public static FieldItemConfigSO CreateRuntimeDefault()
        {
            FieldItemConfigSO config = CreateInstance<FieldItemConfigSO>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }
    }
}
