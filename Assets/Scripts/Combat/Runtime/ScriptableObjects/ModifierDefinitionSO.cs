using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// ScriptableObject defining a single move modifier.
    /// Modifiers are referenced by section IDs (e.g., "Ã‚Â§2.1-a"), not names.
    /// Matches COMBAT_05_StampedMoveModifiers_v1.2.
    /// </summary>
    [CreateAssetMenu(fileName = "Modifier_", menuName = "Chainbound/Combat/Modifiers/Modifier Definition")]
    public class ModifierDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Section ID from Spec 05 (e.g., '2.1-a'). Stable reference key.")]
        public string sectionId;

        [Tooltip("Player-facing name. Section ID stays the stable key.")]
        public string displayName;

        [TextArea(2, 4)]
        public string effectDescription;

        [Header("Classification")]
        public ModifierCategory category;
        public ModifierScope scope;
        public RarityTier minimumRarity;

        [Header("Effect Values")]
        [Tooltip("Minimum effect value (e.g., +5% accuracy Ã¢â€ â€™ 5).")]
        public float effectMin;

        [Tooltip("Maximum effect value (e.g., +8% accuracy Ã¢â€ â€™ 8).")]
        public float effectMax;

        [Header("Drop Weight")]
        [Tooltip("Within-tier relative weight for vending candidate pools. Default 1.")]
        [Min(0)]
        public int weight = 1;

        [Header("Restrictions")]
        [Tooltip("Can only be applied to Stamped moves of these categories.")]
        public MoveCategory[] allowedCategories;

        [Tooltip("Cannot be combined with these modifier section IDs.")]
        public string[] forbiddenCombinations;

        [Tooltip("Extra slot cost when this modifier is applied.")]
        public int slotCostIncrease;

        [Tooltip("Max move power this modifier can be attached to. 0 = no cap.")]
        public int powerCap;

        [Tooltip("Max AP cost this modifier can be attached to. 0 = no cap.")]
        public int apCostCap;

        [Tooltip("Move tags that disqualify this modifier (e.g., PRIOR|MULTI).")]
        public string[] forbiddenTags;

        [Tooltip("Optional per-Stamped slot grouping for one-per-type limits.")]
        public ModifierSlotType slotType;

        [Header("Type Infusion")]
        [Tooltip("For TRX type-infusion modifiers: if true, the modifier changes the move's type.")]
        public bool isTypeInfusion;

        [Header("Stat Substitution")]
        [Tooltip("For TRX stat-sub modifiers: if true, replaces the attack stat used.")]
        public bool isStatSubstitution;

        [Tooltip("The stat to substitute (only relevant if isStatSubstitution).")]
        public CoreStat substitutionStat;

        [Header("Trigger")]
        [Tooltip("For TRG modifiers: the trigger condition.")]
        public ModifierTriggerCondition triggerCondition;

        [Tooltip("HP threshold (%) used when triggerCondition is OnLowHP. Defaults to 30.")]
        [Range(0f, 100f)]
        public float lowHpThresholdPercent = 30f;

        [Tooltip("Trigger chance (0-1).")]
        [Range(0f, 1f)]
        public float triggerChance;

        [Header("Notes")]
        [TextArea(2, 4)]
        public string designNotes;
        /// <summary>Player-facing label; falls back to sectionId when unset.</summary>
        public string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(displayName) ? sectionId : displayName.Trim();
        }
    }
}
