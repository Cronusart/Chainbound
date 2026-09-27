using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct MoveWallPresentation
    {
        [Tooltip("Stable wall SFX catalog ID. Empty falls back to the wall source tag, then the catalog default.")]
        public string wallSfxId;

        [Tooltip("Billboard sprite for the persistent wall. Null uses the global wall visual default.")]
        public Sprite wallSprite;

        [Tooltip("Optional material override for the wall sprite.")]
        public Material wallMaterial;

        [Tooltip("Optional size multiplier on CombatWallVisualProfileSO.spriteSize. Zero uses profile defaultScale.")]
        public Vector2 wallSpriteScale;

        [Tooltip("Optional tint. Alpha 0 means no tint override.")]
        public Color wallTint;
    }

    /// <summary>
    /// ScriptableObject defining a single move.
    /// Matches COMBAT_03A1_StampedMoves_BaselineList_v1.2.
    /// Intrinsic properties are immutable (03A §6).
    /// This SO defines base move data shared across all tiers (Known/Stamped/Stamped).
    /// </summary>
    [CreateAssetMenu(fileName = "Move_", menuName = "Chainbound/Combat/Moves/Move Definition")]
    public class MoveDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable ID for data references (not display name).")]
        public string moveId;
        public string displayName;

        [TextArea(2, 4)]
        public string description;

        [Header("Classification")]
        public MoveCategory category;
        public ChainlingType baseType;  // None = typeless
        public RarityTier rarity;

        [Header("Targeting")]
        public MoveTargeting targeting;
        public ContactType contactType;

        [Header("Intrinsic Properties (Immutable)")]
        [Tooltip("Base power. 0 for non-damaging Stamped moves.")]
        public int power;

        [Tooltip("AP cost to use.")]
        public int apCost;

        [Tooltip("Base accuracy (0-100). 0 for auto-hit or non-damaging.")]
        public int accuracy;

        [Header("Multi-Hit")]
        [Tooltip("Minimum number of hits (1 = single hit).")]
        public int hitCountMin = 1;

        [Tooltip("Maximum number of hits (same as min for fixed-count).")]
        public int hitCountMax = 1;

        [Header("Recharge")]
        [Tooltip("Turns before reuse after use (0 = no recharge). See 02C §1.2.")]
        public int rechargeTurns;

        [Header("Status Effects")]
        [Tooltip("Statuses this move can apply (must be General origin only).")]
        public List<MoveStatusEffectDef> statusEffects = new List<MoveStatusEffectDef>();

        [Header("Stage Changes")]
        [Tooltip("Stat stage changes this move applies.")]
        public List<MoveStageChangeDef> stageChanges = new List<MoveStageChangeDef>();

        [Header("Tags")]
        [Tooltip("Special behavior tags for extensibility (e.g., 'APMAN', 'TRIG', 'WALL').")]
        public List<string> tags = new List<string>();

        [Header("Active Move Conditions")]
        [Tooltip("Selection policy for active-condition moves.")]
        public MoveSelectionPolicy selectionPolicy = MoveSelectionPolicy.EffectOnly;

        [Tooltip("Condition type for active move gating/conditional effects.")]
        public ActiveConditionType activeConditionType = ActiveConditionType.None;

        [Tooltip("Threshold percent used by active conditions (e.g., SelfHPBelowPercent).")]
        [Range(0f, 100f)]
        public float activeConditionThresholdPercent;

        [Header("Utility Effects")]
        [Tooltip("Typed utility effects executed when this move resolves.")]
        public List<UtilityEffectPayload> utilityEffects = new List<UtilityEffectPayload>();

        [Header("Resolve-Time Tuning")]
        [Tooltip("Resolver/calculator knobs evaluated from live combat state when this move resolves. Defaults preserve legacy behavior.")]
        public MoveResolveProfile resolveProfile = new MoveResolveProfile();

        [Header("Passive Trigger (Passive Stamped moves only)")]
        [Tooltip("Trigger condition for passive Stamped moves. None = no trigger (always active or manual).")]
        public PassiveTriggerCondition triggerCondition;

        [Tooltip("HP threshold percentage (0-100) for HP-based triggers. Only used if triggerCondition is HPBelowThreshold.")]
        [Range(0f, 100f)]
        public float hpThresholdPercent;

        [Tooltip("If true, this passive can only trigger once per battle.")]
        public bool triggerOncePerBattle;

        [Header("Healing")]
        [Tooltip("Heal as percentage of target's MaxHP (0 = not a heal or use flat). E.g., 25 = heal 25% MaxHP.")]
        [Range(0, 100)]
        public int healPercentMaxHP;

        [Header("Full-Absorb Barrier (HP Sacrifice)")]
        [Tooltip("HP sacrifice as % of MaxHP when using full-absorb barrier moves. Only applies to moves with FULL_ABSORB_WALL tag. 0 = no sacrifice.")]
        [Range(0f, 100f)]
        public float hpCostPercent;

        [Header("Presentation")]
        [Tooltip("Optional move-specific frame presentation layered on top of the acting species profile track during this move.")]
        public CombatPresentationTrackSO presentationTrack;

        public MoveWallPresentation wallPresentation;

        [Header("Fallback SFX")]
        [System.Obsolete("Use CombatVFXLibrary phaseStartSound instead.", false)]
        [HideInInspector]
        [Tooltip("Optional SoundEventSO used when the move has no frame-authored cast audio.")]
        public UnityEngine.Object castSound;

        [System.Obsolete("Use CombatVFXLibrary phaseStartSound instead.", false)]
        [HideInInspector]
        [Tooltip("Optional SoundEventSO used when the move has no frame-authored release audio.")]
        public UnityEngine.Object releaseSound;

        [System.Obsolete("Use CombatVFXLibrary phaseStartSound instead.", false)]
        [HideInInspector]
        [Tooltip("Optional SoundEventSO used when the move has no frame-authored travel audio.")]
        public UnityEngine.Object travelSound;

        [HideInInspector]
        [Tooltip("Optional SoundEventSO used as a move identity accent at impact when the move has no frame-authored audio.")]
        public UnityEngine.Object impactAccentSound;

        [Header("Notes")]
        [TextArea(2, 6)]
        [Tooltip("Design notes or spec references. Not used at runtime.")]
        public string designNotes;

        private void OnValidate()
        {
            bool isWallMove = CombatWallStateUtility.MoveDefinitionPlacesWall(this);
            bool hasWallPresentationData = !string.IsNullOrWhiteSpace(wallPresentation.wallSfxId)
                || wallPresentation.wallSprite != null
                || wallPresentation.wallMaterial != null
                || wallPresentation.wallSpriteScale != Vector2.zero
                || wallPresentation.wallTint.a > 0f;

            if (isWallMove && string.IsNullOrWhiteSpace(wallPresentation.wallSfxId))
            {
                Debug.LogWarning(
                    $"[MoveDefinitionSO] Wall move '{name}' is missing wallPresentation.wallSfxId.",
                    this);
            }
            else if (!isWallMove && hasWallPresentationData)
            {
                Debug.LogWarning(
                    $"[MoveDefinitionSO] '{name}' contains orphan wall presentation data but is not a wall move.",
                    this);
            }

            if (castSound != null || releaseSound != null || travelSound != null || impactAccentSound != null)
            {
                Debug.LogWarning(
                    $"[MoveDefinitionSO] Legacy fallback SFX remain assigned on '{name}'. Migrate them to CombatVFXLibrary.",
                    this);
            }

            if (impactAccentSound != null && presentationTrack != null && presentationTrack.HasAudioEvents())
            {
                Debug.LogWarning(
                    $"[MoveDefinitionSO] '{name}' has a legacy impactAccentSound and frame-authored audio. " +
                    "The accent is intentionally suppressed so frame-track audio remains authoritative.",
                    this);
            }
        }
    }

    /// <summary>
    /// Status effect definition for a move.
    /// </summary>
    [System.Serializable]
    public struct MoveStatusEffectDef
    {
        public StatusId statusId;
        public int duration;

        [Range(0f, 1f)]
        public float chance;

        [Tooltip("Override targeting for this specific status effect.")]
        public bool overrideTargeting;
        public MoveTargeting targetOverride;

        public ChainlingType parameterType;
    }

    /// <summary>
    /// Stage change definition for a move.
    /// </summary>
    [System.Serializable]
    public struct MoveStageChangeDef
    {
        public CoreStat stat;
        public int stages;

        [Range(0f, 1f)]
        public float chance;

        public bool targetsSelf;
    }
}
