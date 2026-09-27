using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public static class BattleIntroRequestContextUtility
    {
        public static void ApplyOpponentContext(ref BattleIntroRequest request, CombatState state)
        {
            request.leadOpponentSpeciesId = string.Empty;
            request.leadOpponentVisualVariantId = ChainlingVisualIdentity.BaseVariantId;
            request.opponentSpeciesIds = Array.Empty<string>();
            request.hasEntangledOpponent = false;
            request.opponentActiveCount = 0;

            if (state?.team1 == null)
                return;

            var speciesIds = new List<string>(2);
            bool foundLead = false;

            for (int slot = 0; slot < 2; slot++)
            {
                ChainlingInstance opponent = state.team1.GetActiveInSlot(slot);
                if (opponent == null || !opponent.isAlive)
                    continue;

                request.opponentActiveCount++;

                string speciesId = NormalizeId(opponent.speciesId);
                string variantId = ResolveVisualVariantId(opponent);

                if (!string.IsNullOrEmpty(speciesId))
                    speciesIds.Add(speciesId);

                if (!foundLead)
                {
                    request.leadOpponentSpeciesId = speciesId;
                    request.leadOpponentVisualVariantId = variantId;
                    foundLead = true;
                }

                if (opponent.isEntangled)
                    request.hasEntangledOpponent = true;
            }

            request.opponentSpeciesIds = speciesIds.ToArray();
        }

        public static string ResolveVisualVariantId(ChainlingInstance chainling)
        {
            return ChainlingVisualIdentity.NormalizeId(
                chainling?.visualIdentity?.visualVariantId,
                ChainlingVisualIdentity.BaseVariantId);
        }

        /// <summary>Legacy helper: entangled is no longer encoded in visualVariantId.</summary>
        public static bool IsEntangledVariant(string visualVariantId)
        {
            return false;
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }
    }
}
