using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class ChainlingPlayerTerms
    {
        /// <summary>Player-facing label at rebirthCount == REBIRTH_MAX (final Cycle-up).</summary>
        public const string MaxCycleLabel = "Max Cycle";

        private static readonly string[] CycleNumerals = { "I", "II", "III", "IV", "V" };

        /// <summary>
        /// Visual / summary tier index: rebirth 0-4 → Cycle I-V (0-4), rebirth 5 → Max Cycle (5).
        /// Typography for Max Cycle reuses Cycle V assets.
        /// </summary>
        public static int ResolveCycleTier(int rebirthCount)
        {
            return ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount);
        }

        public static bool IsMaxCycle(int rebirthCount)
        {
            return ChainlingProgressionUtility.NormalizeRebirthCount(rebirthCount) >=
                CombatConstants.REBIRTH_MAX;
        }

        /// <summary>
        /// Typography index for cycle VFX (0-4). Max Cycle shares Cycle V presentation.
        /// </summary>
        public static int ResolveCycleVisualTier(int rebirthCount)
        {
            int tier = ResolveCycleTier(rebirthCount);
            return Mathf.Clamp(tier, 0, CycleNumerals.Length - 1);
        }

        public static string FormatCycleLabel(int rebirthCount)
        {
            if (IsMaxCycle(rebirthCount))
                return MaxCycleLabel;

            return $"Cycle {FormatCycleNumeral(rebirthCount)}";
        }

        public static string FormatCycleNumeral(int rebirthCount)
        {
            if (IsMaxCycle(rebirthCount))
                return "Max";

            int tier = ResolveCycleVisualTier(rebirthCount);
            return CycleNumerals[tier];
        }

        public static ArchiveBindingState ResolveArchiveState(ChainlingSnapshot snapshot)
        {
            if (snapshot == null)
                return ArchiveBindingState.Dossier;

            return !string.IsNullOrWhiteSpace(snapshot.nftMintAddress) || snapshot.archivedAtUnix > 0
                ? ArchiveBindingState.Anchored
                : ArchiveBindingState.Dossier;
        }
    }
}
