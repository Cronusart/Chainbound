using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Validates and executes move equip/unequip operations
    /// against a Chainling's <see cref="ChainlingLoadout"/>.
    ///
    /// This service owns the team-wide Mythic limit tracking and
    /// delegates per-Chainling validation to <see cref="ChainlingLoadout"/>.
    /// </summary>
    public interface IMoveEquipService
    {
        /// <summary>
        /// Check whether a move can be equipped on a specific Chainling.
        /// Validates compatibility, slot limits, Mythic caps, and modifier stacking.
        /// </summary>
        LoadoutValidationResult CanEquip(ChainlingLoadout loadout, MoveInstance move);

        /// <summary>
        /// Equip a move. Validates first, then adds to the loadout.
        /// Returns the validation result (check <see cref="LoadoutValidationResult.isValid"/>).
        /// </summary>
        LoadoutValidationResult Equip(ChainlingLoadout loadout, MoveInstance move);

        /// <summary>
        /// Check whether a move can be unequipped from a specific Chainling.
        /// Escrowed moves cannot be unequipped.
        /// </summary>
        LoadoutValidationResult CanUnequip(ChainlingLoadout loadout, MoveInstance move);

        /// <summary>
        /// Unequip a move. Validates first, then removes from the loadout.
        /// Returns the validation result.
        /// </summary>
        LoadoutValidationResult Unequip(ChainlingLoadout loadout, MoveInstance move);

        /// <summary>
        /// Register a loadout into the team-wide Mythic tracking.
        /// Must be called for each Chainling loadout on team roster changes.
        /// </summary>
        void RegisterLoadout(ChainlingLoadout loadout);

        /// <summary>
        /// Unregister a loadout from team-wide tracking.
        /// </summary>
        void UnregisterLoadout(ChainlingLoadout loadout);

        /// <summary>
        /// Get all currently registered loadouts (for team-wide queries).
        /// </summary>
        IReadOnlyList<ChainlingLoadout> GetRegisteredLoadouts();
    }
}
