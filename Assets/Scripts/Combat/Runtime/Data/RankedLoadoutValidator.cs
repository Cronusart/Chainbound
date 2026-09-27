using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Validates that a Chainling's loadout is eligible for ranked play.
    /// Rules:
    ///   - Known moves are not eligible for ranked play.
    ///   - Stamped moves are eligible for ranked play.
    ///
    /// Friendly matches skip validation entirely.
    /// </summary>
    public static class RankedLoadoutValidator
    {
        /// <summary>
        /// Validate a single Chainling's equipped moves for ranked eligibility.
        /// Returns the first validation failure found, or Valid if all moves pass.
        /// </summary>
        public static LoadoutValidationResult ValidateForRanked(ChainlingInstance chainling)
        {
            if (chainling == null)
                return LoadoutValidationResult.Fail(
                    LoadoutValidationError.KnownNotAllowedInRanked,
                    "Chainling is null.");

            for (int i = 0; i < chainling.moves.Count; i++)
            {
                var move = chainling.moves[i];

                if (move.tier == MoveTier.Known)
                {
                    return LoadoutValidationResult.Fail(
                        LoadoutValidationError.KnownNotAllowedInRanked,
                        $"Move '{move.displayName}' is Known and cannot be used in ranked. " +
                        "Stamp the move first.");
                }
            }

            return LoadoutValidationResult.Valid;
        }

        /// <summary>
        /// Validate an entire team roster for ranked eligibility.
        /// Checks every Chainling in the roster.
        /// </summary>
        public static LoadoutValidationResult ValidateTeamForRanked(List<ChainlingInstance> roster)
        {
            if (roster == null || roster.Count == 0)
                return LoadoutValidationResult.Fail(
                    LoadoutValidationError.KnownNotAllowedInRanked,
                    "Roster is empty.");

            for (int i = 0; i < roster.Count; i++)
            {
                var result = ValidateForRanked(roster[i]);
                if (!result.isValid)
                    return result;
            }

            return LoadoutValidationResult.Valid;
        }
    }
}
