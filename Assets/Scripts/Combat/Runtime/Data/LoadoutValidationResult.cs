using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Structured result from a loadout validation check (CanEquip / CanUnequip).
    /// Provides a machine-readable error code plus a human-readable message for UI.
    /// </summary>
    [Serializable]
    public struct LoadoutValidationResult
    {
        /// <summary>True if the operation is allowed.</summary>
        public bool isValid;

        /// <summary>Error code (None if valid).</summary>
        public LoadoutValidationError error;

        /// <summary>Human-readable explanation of the failure (empty if valid).</summary>
        public string message;

        /// <summary>Convenience: a successful validation result.</summary>
        public static LoadoutValidationResult Valid =>
            new LoadoutValidationResult { isValid = true, error = LoadoutValidationError.None, message = "" };

        /// <summary>Create a failed validation result.</summary>
        public static LoadoutValidationResult Fail(LoadoutValidationError error, string message) =>
            new LoadoutValidationResult { isValid = false, error = error, message = message };
    }

    /// <summary>
    /// Machine-readable error codes for loadout validation failures.
    /// </summary>
    public enum LoadoutValidationError
    {
        None,

        /// <summary>No more move slots available on this Chainling.</summary>
        NoSlotsAvailable,

        /// <summary>This move is already equipped (no duplicates allowed).</summary>
        DuplicateMove,

        /// <summary>This Chainling already has 2 Mythic Stamped moves (per-Chainling cap).</summary>
        MythicPerChainlingLimit,

        /// <summary>The team already has 4 Mythic Stamped moves (per-team cap).</summary>
        MythicPerTeamLimit,

        /// <summary>The team already has 2 Mythic passive Stamped moves (per-team cap).</summary>
        MythicPassivePerTeamLimit,

        /// <summary>The move's type is not in this Chainling's eligible pools.</summary>
        TypeIncompatible,

        /// <summary>The move is escrowed and cannot be unequipped.</summary>
        MoveEscrowed,

        /// <summary>Modifier stacking rules would be violated by equipping this move.</summary>
        ModifierStackingViolation,

        /// <summary>The move is a species-exclusive that does not belong to this species.</summary>
        ExclusiveSpeciesMismatch,

        /// <summary>The global stat-substitution cap (2 per Chainling) would be exceeded.</summary>
        StatSubstitutionCapExceeded,

        /// <summary>The Chainling already has the maximum allowed passive moves equipped.</summary>
        PassivePerChainlingLimit,

        /// <summary>The Chainling already has the maximum allowed Stamped-tier moves equipped.</summary>
        StampedPerChainlingLimit,

        /// <summary>Known-tier moves are not allowed in ranked play.</summary>
        KnownNotAllowedInRanked,

        /// <summary>Too many Stamped-tier moves on this Chainling for ranked play.</summary>
        StampedPerChainlingRankedLimit,
    }
}
