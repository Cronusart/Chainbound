using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Fills capture-target identity used by Core: species profile plus variant/mutation ranks.
    /// </summary>
    public static class CaptureDifficultyContextBuilder
    {
        public static void ApplyTo(
            CaptureTargetContext target,
            ChainlingInstance source,
            CombatVisualDatabaseSO visualDatabase)
        {
            if (target == null || source == null)
                return;

            target.HasSpeciesCaptureProfile = source.hasCaptureProfile;
            target.SpeciesCaptureProfile = source.captureProfile;
            target.IsMutated = source.isMutated;
            target.IsEntangled = source.isEntangled;
            target.MutationRarityRank = Mathf.Clamp(source.captureMutationRarityRank, 0, 5);
            target.VariantRarityRank = ResolveVariantRarityRank(source, visualDatabase);
        }

        private static int ResolveVariantRarityRank(ChainlingInstance source, CombatVisualDatabaseSO visualDatabase)
        {
            if (visualDatabase == null || source == null)
                return 0;

            string variantId = source.visualIdentity != null
                ? source.visualIdentity.visualVariantId
                : ChainlingVisualIdentity.BaseVariantId;
            if (!visualDatabase.TryGetCatalogRarity(source.speciesId, variantId, out RarityTier rarity))
                return 0;

            return Mathf.Clamp((int)rarity, 0, 5);
        }
    }
}
