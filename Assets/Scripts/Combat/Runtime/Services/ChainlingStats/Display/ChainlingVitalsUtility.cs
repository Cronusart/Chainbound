using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Sentinel for <see cref="ChainlingSnapshot.currentHp"/> when no HP has been persisted (DB NULL = full HP).
    /// </summary>
    public static class ChainlingVitalsConstants
    {
        public const int UnsyncedCurrentHp = -1;
    }

    public readonly struct ChainlingVitalsDisplay
    {
        public ChainlingVitalsDisplay(int currentHp, int maxHp, float fill01, bool isFainted, string label)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Fill01 = fill01;
            IsFainted = isFainted;
            Label = label ?? "";
        }

        public int CurrentHp { get; }
        public int MaxHp { get; }
        public float Fill01 { get; }
        public bool IsFainted { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Resolves persisted HP for combat hydration and menu vitals display.
    /// </summary>
    public static class ChainlingVitalsUtility
    {
        public static bool HasPersistedCurrentHp(ChainlingSnapshot snapshot)
        {
            return snapshot != null && snapshot.currentHp >= 0;
        }

        public static int BuildMaxHp(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            return ChainlingAttributeDisplayUtility.BuildMaxHp(snapshot, species);
        }

        public static int ResolveEffectiveCurrentHp(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            int maxHp = BuildMaxHp(snapshot, species);
            if (maxHp <= 0)
                return 0;

            if (!HasPersistedCurrentHp(snapshot))
                return maxHp;

            return Mathf.Clamp(snapshot.currentHp, 0, maxHp);
        }

        /// <summary>
        /// Resolves a vital received from the authoritative battle state. A fresh profile can
        /// carry the shared unsynced sentinel until the client has calculated this Chainling's
        /// effective battle max HP; it therefore starts at full HP rather than fainted.
        /// </summary>
        public static int ResolveAuthoritativeCheckpointCurrentHp(int currentHp, int effectiveMaxHp)
        {
            int maxHp = Mathf.Max(1, effectiveMaxHp);
            return currentHp == ChainlingVitalsConstants.UnsyncedCurrentHp
                ? maxHp
                : Mathf.Clamp(currentHp, 0, maxHp);
        }

        public static ChainlingVitalsDisplay BuildDisplay(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            int maxHp = BuildMaxHp(snapshot, species);
            int currentHp = ResolveEffectiveCurrentHp(snapshot, species);
            float fill01 = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
            bool isFainted = maxHp > 0 && currentHp <= 0;
            string label = maxHp > 0 ? $"{currentHp} / {maxHp}" : "—";
            return new ChainlingVitalsDisplay(currentHp, maxHp, fill01, isFainted, label);
        }

        public static int MapDbCurrentHp(int? dbValue)
        {
            return dbValue.HasValue ? Mathf.Max(0, dbValue.Value) : ChainlingVitalsConstants.UnsyncedCurrentHp;
        }

        public static int MapDbCurrentHp(int dbValue, bool hasValue)
        {
            return hasValue ? Mathf.Max(0, dbValue) : ChainlingVitalsConstants.UnsyncedCurrentHp;
        }
    }
}
