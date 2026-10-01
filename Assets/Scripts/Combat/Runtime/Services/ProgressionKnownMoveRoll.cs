using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Rolls the Known move a captured Chainling can already have equipped.
    /// Uses the species progression pool and the same star-gate percent as a folio.
    /// A star-0 wild uses the first-star gate. An empty result is a miss, not a fallback move.
    /// </summary>
    public static class ProgressionKnownMoveRoll
    {
        public static string TryRoll(
            SpeciesDefinitionSO species,
            int stars,
            bool isEntangled,
            System.Random rng,
            ChainlingProgressionRewardTablesSO tables,
            ProgressionConfigSO progressionConfig)
        {
            if (species == null)
                return "";

            int starIndex = Mathf.Clamp(Mathf.Max(1, stars), 1, CombatConstants.STAR_MAX);
            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                isEntangled,
                null,
                null,
                progressionConfig);

            if (pool == null || pool.TotalWeight <= 0)
                return "";

            if (!ChainlingProgressionRollUtility.TryRollStarGate(
                    starIndex,
                    isEntangled,
                    tables,
                    pool.starGateBonusPercent,
                    rng))
            {
                return "";
            }

            ResolvedProgressionMoveEntry move = ChainlingProgressionRollUtility.RollMoveFromPool(pool, rng);
            return move != null && !string.IsNullOrWhiteSpace(move.moveId) ? move.moveId : "";
        }
    }
}
