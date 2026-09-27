using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// A specific move equipped on a Chainling during combat.
    /// References a MoveDefinition (from SO data) plus runtime modifiers.
    /// Classification determines ownership and modifier availability:
    ///   Known = profile/PDA record, no modifiers, non-transferable
    ///   Stamped = trusted Metaplex Core asset, one or two modifiers
    /// </summary>
    [Serializable]
    public class MoveInstance
    {
        /// <summary>Unique instance ID for logging/targeting.</summary>
        public string instanceId;

        /// <summary>Reference to the definition's stable ID (e.g., from MoveDatabaseSO).</summary>
        public string definitionId;

        /// <summary>Cached display name (from definition).</summary>
        public string displayName;

        // â”€â”€ Tier â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Move classification: Known or Stamped.</summary>
        public MoveTier tier;

        /// <summary>Category of this move.</summary>
        public MoveCategory category;

        /// <summary>Base element type (None = typeless).</summary>
        public ChainlingType baseType;

        /// <summary>Targeting mode.</summary>
        public MoveTargeting targeting;

        /// <summary>Contact type for damaging moves.</summary>
        public ContactType contactType;

        /// <summary>Base power (0 for non-damaging).</summary>
        public int basePower;

        /// <summary>AP cost.</summary>
        public int apCost;

        /// <summary>Base accuracy (0-100).</summary>
        public int baseAccuracy;

        /// <summary>Hit count for multi-hit moves (1 = single hit).</summary>
        public int hitCount;

        /// <summary>Maximum hit count for variable multi-hit (e.g., 2-4 hits).</summary>
        public int hitCountMax;

        /// <summary>Rarity tier of this move.</summary>
        public RarityTier rarity;

        /// <summary>Recharge turns required after use (0 = no recharge).</summary>
        public int rechargeTurns;

        /// <summary>Status effects this move can apply (General only).</summary>
        public List<MoveStatusEffect> statusEffects;

        /// <summary>Stage changes this move applies.</summary>
        public List<MoveStageChange> stageChanges;

        /// <summary>Runtime modifier instances (populated from tier-appropriate data).</summary>
        public List<ModifierInstance> modifiers;

        /// <summary>Special behavior flags (encoded as string tags for extensibility).</summary>
        public List<string> tags;

        /// <summary>Selection policy for active-condition moves.</summary>
        public MoveSelectionPolicy selectionPolicy;

        /// <summary>Condition type for active move gating / conditional effects.</summary>
        public ActiveConditionType activeConditionType;

        /// <summary>Threshold percent used by active conditions.</summary>
        public float activeConditionThresholdPercent;

        /// <summary>Typed utility effects executed when this move resolves.</summary>
        public List<UtilityEffectPayload> utilityEffects;

        /// <summary>Move-authored resolve-time tuning copied from the definition.</summary>
        public MoveResolveProfile resolveProfile;

        // â”€â”€ Passive Trigger Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Trigger condition for passive moves.</summary>
        public PassiveTriggerCondition triggerCondition;

        /// <summary>HP threshold percentage (0-100) for HP-based triggers.</summary>
        public float hpThresholdPercent;

        /// <summary>If true, this passive can only trigger once per battle.</summary>
        public bool triggerOncePerBattle;

        /// <summary>Heal as percentage of target MaxHP (0 = not a %-heal). E.g., 25 = 25% MaxHP.</summary>
        public int healPercentMaxHP;

        /// <summary>HP sacrifice as % of MaxHP for full-absorb barrier moves (FULL_ABSORB_WALL tag). 0 = no sacrifice.</summary>
        public float hpCostPercent;

        // â”€â”€ Chain / NFT Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// On-chain address for this move.
        /// Known: null (stored in the authoritative profile/PDA).
        /// Stamped: Metaplex Core asset address.
        /// </summary>
        public string mintAddress;

        /// <summary>
        /// Modifier definition section IDs attached to this instance (e.g., "2.1-a", "6.1-d").
        /// Parallel to the <see cref="modifiers"/> list but stored as stable string IDs
        /// for chain serialization and cross-referencing with COMBAT_05.
        /// </summary>
        public string[] modifierIds;

        // â”€â”€ Tier-Specific Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Modifier 1 section ID. Required for Stamped and empty for Known.
        /// </summary>
        public string modifier1SectionId;

        /// <summary>
        /// Optional second modifier section ID for a Stamped move.
        /// </summary>
        public string modifier2SectionId;

        /// <summary>
        /// Historical reroll counter retained only as serialized gameplay state.
        /// </summary>
        public int rerollCount;

        /// <summary>
        /// Unix timestamp when this move was stamped on-chain. Zero for Known.
        /// </summary>
        public long mintTimestamp;

        /// <summary>
        /// Override for the slot cost of this move instance.
        /// -1 means "use the base slot cost from the definition".
        /// Values >= 0 are explicit overrides (e.g., from AUG/TRX modifiers adding +1).
        /// </summary>
        public int slotCostOverride;

        /// <summary>
        /// Schema version for future migration of on-chain data format.
        /// Increment when the serialized layout changes.
        /// </summary>
        public int schemaVersion;

        /// <summary>
        /// Current lock state for PvP escrow.
        /// Escrowed moves cannot be unequipped, traded, or burned.
        /// </summary>
        public MoveLockState lockState;

        public MoveInstance()
        {
            instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            tier = MoveTier.Known;
            statusEffects = new List<MoveStatusEffect>();
            stageChanges = new List<MoveStageChange>();
            modifiers = new List<ModifierInstance>();
            tags = new List<string>();
            utilityEffects = new List<UtilityEffectPayload>();
            resolveProfile = new MoveResolveProfile();
            modifierIds = Array.Empty<string>();
            slotCostOverride = -1;
            schemaVersion = 2;
            lockState = MoveLockState.Unlocked;
            selectionPolicy = MoveSelectionPolicy.EffectOnly;
            activeConditionType = ActiveConditionType.None;
            activeConditionThresholdPercent = 0f;
        }

        /// <summary>Whether this move has any modifiers based on its tier.</summary>
        public bool HasModifiers => tier == MoveTier.Stamped;

        /// <summary>Whether this move is tradeable (only Stamped moves and Stamped moves).</summary>
        public bool IsTradeable => tier == MoveTier.Stamped;

        /// <summary>Whether this move is eligible for ranked play.</summary>
        public bool IsRankedEligible => tier == MoveTier.Stamped;
    }

    /// <summary>
    /// A status effect that a move can apply.
    /// </summary>
    [Serializable]
    public struct MoveStatusEffect
    {
        public StatusId statusId;
        public int duration;
        public float chance;  // 0-1; 1 = guaranteed
        public MoveTargeting targetOverride;  // If different from move targeting

        /// <summary>Parameterized type (for type-specific statuses like coatings).</summary>
        public ChainlingType parameterType;
    }

    /// <summary>
    /// A stage change that a move applies.
    /// </summary>
    [Serializable]
    public struct MoveStageChange
    {
        public CoreStat stat;
        public int stages;  // Positive = buff, negative = debuff
        public float chance;  // 0-1; 1 = guaranteed
        public bool targetsSelf;
    }

    /// <summary>
    /// A modifier instance attached to a move.
    /// </summary>
    [Serializable]
    public class ModifierInstance
    {
        /// <summary>Unique instance ID.</summary>
        public string instanceId;

        /// <summary>Reference to the modifier definition's section ID (e.g., "2.1-a").</summary>
        public string definitionSectionId;

        /// <summary>Modifier category (POT/EFF/AUG/TRX/TRG/SYN).</summary>
        public ModifierCategory category;

        /// <summary>Active (per-move) or Passive (per-Chainling).</summary>
        public ModifierScope scope;

        /// <summary>Rarity tier of this modifier.</summary>
        public RarityTier rarity;

        /// <summary>Effect magnitude (interpreted per modifier type).</summary>
        public float effectValue;

        /// <summary>
        /// True when effectValue was fixed by the stamp derivation. False preserves legacy
        /// definitions whose ranges are intentionally resolved by older runtime behavior.
        /// </summary>
        public bool effectValueLocked;

        /// <summary>Secondary value if needed (e.g., min-max range).</summary>
        public float effectValueMax;

        /// <summary>For type infusion modifiers: the target type.</summary>
        public ChainlingType infusionType;

        /// <summary>For stat substitution: which stat to use instead.</summary>
        public CoreStat substitutionStat;

        /// <summary>For TRG modifiers: the trigger condition (e.g., OnSwapIn, OnSwapOut).</summary>
        public ModifierTriggerCondition triggerCondition;

        public ModifierInstance()
        {
            instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
