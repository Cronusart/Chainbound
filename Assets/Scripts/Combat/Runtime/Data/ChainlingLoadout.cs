using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages move equip/unequip for a single Chainling.
    /// Enforces slot, duplicate, passive, modifier-stacking, and escrow rules.
    ///
    /// Pure C# â€” no MonoBehaviour. Designed for constructor injection of team context.
    ///
    /// Slot formula:
    ///   Slots = min(9, 4 + rebirthCount).
    ///
    /// Loadout caps:
    ///   - Max 2 passive moves per Chainling.
    ///
    /// Modifier stacking (COMBAT_05 Section 8):
    ///   - Max 3 modifiers per move
    ///   - Max 1 slot-consuming modifier (AUG/TRX) per move
    ///   - Max 2 stat-substitution modifiers per Chainling
    ///   (Stacking within a single move is validated at modifier-attach time,
    ///    not at equip time; equip only checks global caps.)
    ///
    /// Ranked constraints:
    ///   - Known moves can be equipped but are not eligible for ranked matches.
    /// </summary>
    [Serializable]
    public class ChainlingLoadout
    {
        // â”€â”€ Configuration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Species ID of the owning Chainling.</summary>
        public string speciesId;

        /// <summary>Primary type of the owning Chainling.</summary>
        public ChainlingType primaryType;

        /// <summary>Secondary type (None if mono-type). Equip checks do not treat this as a second type.</summary>
        public ChainlingType secondaryType;

        /// <summary>Species asset used for exclusive and off-type equip checks. Null skips those checks.</summary>
        public SpeciesDefinitionSO speciesDefinition;

        /// <summary>Specimen entangled flag. Entangled type is per Chainling, not species.secondaryType.</summary>
        public bool isEntangled;

        /// <summary>Rolled entangled type when <see cref="isEntangled"/> is set.</summary>
        public ChainlingType entangledType = ChainlingType.None;

        /// <summary>Used to block exclusives that belong to a different species.</summary>
        public SpeciesDatabaseSO speciesDatabase;

        /// <summary>Current evolution stage (1-based). Does not determine move slot count.</summary>
        public int evolutionStage;

        /// <summary>Current rebirth count. Determines move slot count.</summary>
        public int rebirthCount;

        // â”€â”€ Equipped Moves â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Currently equipped moves (all tiers).</summary>
        public List<MoveInstance> equipped;

        // â”€â”€ Team Context (optional delegate for team-wide Mythic checks) â”€

        /// <summary>
        /// Callback that returns the total number of Mythic moves
        /// currently equipped across the entire team (excluding this Chainling).
        /// Set by the service layer that owns the full team roster.
        /// Returns 0 if no team context is available.
        /// </summary>
        [NonSerialized]
        public Func<int> getTeamMythicCount;

        /// <summary>
        /// Callback that returns the total number of Mythic passive Stamped moves
        /// currently equipped across the entire team (excluding this Chainling).
        /// </summary>
        [NonSerialized]
        public Func<int> getTeamMythicPassiveCount;

        // â”€â”€ Constants â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public const int MYTHIC_PER_CHAINLING_MAX = 2;
        public const int MYTHIC_PER_TEAM_MAX = 4;
        public const int MYTHIC_PASSIVE_PER_TEAM_MAX = 2;
        public const int STAT_SUB_PER_CHAINLING_MAX = 2;

        // â”€â”€ Constructor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public ChainlingLoadout()
        {
            equipped = new List<MoveInstance>();
            evolutionStage = 1;
            rebirthCount = 0;
            primaryType = ChainlingType.None;
            secondaryType = ChainlingType.None;
        }

        // â”€â”€ Computed Properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Maximum move slots for this Chainling at its current rebirth count.
        /// Formula: min(9, 4 + rebirthCount).
        /// </summary>
        public int MaxSlots => MoveLoadoutSlotRules.ResolveMaxMoveSlots(rebirthCount);

        /// <summary>
        /// Number of slots currently consumed, including modifier slot costs.
        /// AUG and TRX modifiers each consume +1 slot (COMBAT_05 Section 1.1).
        /// </summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < equipped.Count; i++)
                {
                    used += GetSlotCost(equipped[i]);
                }
                return used;
            }
        }

        /// <summary>Remaining available slots.</summary>
        public int AvailableSlots => MaxSlots - UsedSlots;

        // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Check whether a given move can be equipped.
        /// Returns a structured result with error code and message for UI.
        /// </summary>
        public LoadoutValidationResult CanEquip(MoveInstance move)
        {
            if (move == null)
                return LoadoutValidationResult.Fail(LoadoutValidationError.DuplicateMove,
                    "Move is null.");

            if (speciesDefinition != null)
            {
                LoadoutValidationResult eligibility = SpeciesMoveEquipRules.Evaluate(
                    speciesDefinition,
                    isEntangled,
                    entangledType,
                    move.definitionId,
                    move.baseType,
                    speciesDatabase);
                if (!eligibility.isValid)
                    return eligibility;
            }

            // 1. Duplicate check (COMBAT_04 Section 4.2: no duplicate Stamped moves)
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].definitionId == move.definitionId)
                    return LoadoutValidationResult.Fail(LoadoutValidationError.DuplicateMove,
                        $"Move '{move.displayName}' is already equipped.");
            }

            // 2. Slot check
            int cost = GetSlotCost(move);
            if (cost > AvailableSlots)
                return LoadoutValidationResult.Fail(LoadoutValidationError.NoSlotsAvailable,
                    $"Not enough slots. Need {cost}, have {AvailableSlots}.");

            // 3. Passive cap
            if (move.category == MoveCategory.Passive)
            {
                int currentPassiveCount = CountPassiveEquipped();
                if (currentPassiveCount >= MoveLoadoutSlotRules.MaxPassiveMoves)
                    return LoadoutValidationResult.Fail(LoadoutValidationError.PassivePerChainlingLimit,
                        $"Already have {MoveLoadoutSlotRules.MaxPassiveMoves} passive moves on this Chainling.");
            }

            // 4. Stat-substitution global cap (max 2 per Chainling, COMBAT_05 Section 5.2)
            int newStatSubCount = CountStatSubModifiers(move);
            if (newStatSubCount > 0)
            {
                int existingStatSubs = CountTotalStatSubModifiers();
                if (existingStatSubs + newStatSubCount > STAT_SUB_PER_CHAINLING_MAX)
                    return LoadoutValidationResult.Fail(LoadoutValidationError.StatSubstitutionCapExceeded,
                        $"Equipping this would exceed the stat-substitution cap of {STAT_SUB_PER_CHAINLING_MAX} per Chainling.");
            }

            return LoadoutValidationResult.Valid;
        }

        /// <summary>
        /// Equip a move after validation. Returns the validation result.
        /// If invalid, the move is NOT added.
        /// </summary>
        public LoadoutValidationResult Equip(MoveInstance move)
        {
            var result = CanEquip(move);
            if (!result.isValid)
                return result;

            equipped.Add(move);
            return LoadoutValidationResult.Valid;
        }

        /// <summary>
        /// Check whether a move can be unequipped.
        /// Escrowed Stamped moves cannot be removed.
        /// </summary>
        public LoadoutValidationResult CanUnequip(MoveInstance move)
        {
            if (move == null)
                return LoadoutValidationResult.Fail(LoadoutValidationError.DuplicateMove, "Move is null.");

            if (move.lockState == MoveLockState.Escrowed)
                return LoadoutValidationResult.Fail(LoadoutValidationError.MoveEscrowed,
                    $"'{move.displayName}' is escrowed in a PvP wager and cannot be unequipped.");

            bool found = false;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].instanceId == move.instanceId)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return LoadoutValidationResult.Fail(LoadoutValidationError.DuplicateMove,
                    "Move is not currently equipped.");

            return LoadoutValidationResult.Valid;
        }

        /// <summary>
        /// Unequip a move after validation. Returns the validation result.
        /// If invalid (e.g., escrowed), the move is NOT removed.
        /// </summary>
        public LoadoutValidationResult Unequip(MoveInstance move)
        {
            var result = CanUnequip(move);
            if (!result.isValid)
                return result;

            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].instanceId == move.instanceId)
                {
                    equipped.RemoveAt(i);
                    return LoadoutValidationResult.Valid;
                }
            }

            return LoadoutValidationResult.Fail(LoadoutValidationError.DuplicateMove,
                "Move not found during removal (unexpected).");
        }

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Validate modifier guardrails for the current equipped loadout.
        /// </summary>
        public ModifierValidationResult ValidateModifierRules(ModifierCatalogSO modifierCatalog)
        {
            return ModifierValidationService.ValidateLoadout(this, modifierCatalog);
        }

        /// <summary>
        /// Calculate the effective slot cost of a move.
        /// Base cost is 1. AUG/TRX modifiers add +1 each (COMBAT_05 Section 1.1).
        /// The slotCostOverride field, if >= 0, replaces the base cost entirely.
        /// </summary>
        public static int GetSlotCost(MoveInstance move)
        {
            int baseCost = move.slotCostOverride >= 0 ? move.slotCostOverride : 1;

            // Add +1 for each AUG or TRX modifier (slot-consuming categories)
            int modifierExtra = 0;
            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    var cat = move.modifiers[i].category;
                    if (cat == ModifierCategory.AUG || cat == ModifierCategory.TRX)
                        modifierExtra++;
                }
            }

            return baseCost + modifierExtra;
        }

        private int CountMythicEquipped()
        {
            int count = 0;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].rarity == RarityTier.Mythic)
                    count++;
            }
            return count;
        }

        private int CountMythicPassiveEquipped()
        {
            int count = 0;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i].rarity == RarityTier.Mythic &&
                    equipped[i].category == MoveCategory.Passive)
                    count++;
            }
            return count;
        }

        private int CountPassiveEquipped()
        {
            int count = 0;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] != null && equipped[i].category == MoveCategory.Passive)
                    count++;
            }

            return count;
        }

        /// <summary>Count stat-substitution modifiers on a single move.</summary>
        private static int CountStatSubModifiers(MoveInstance move)
        {
            int count = 0;
            if (move.modifiers != null)
            {
                for (int i = 0; i < move.modifiers.Count; i++)
                {
                    if (move.modifiers[i].category == ModifierCategory.TRX &&
                        move.modifiers[i].substitutionStat != CoreStat.MaxHP) // MaxHP = sentinel for "no substitution"
                        count++;
                }
            }
            return count;
        }

        /// <summary>Count total stat-substitution modifiers across all equipped moves.</summary>
        private int CountTotalStatSubModifiers()
        {
            int total = 0;
            for (int i = 0; i < equipped.Count; i++)
            {
                total += CountStatSubModifiers(equipped[i]);
            }
            return total;
        }
    }
}

