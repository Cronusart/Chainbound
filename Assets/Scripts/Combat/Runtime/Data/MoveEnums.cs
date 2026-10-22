namespace Chainbound.Combat
{
    /// <summary>
    /// Ownership Spine move classification. Known moves are non-NFT records with no
    /// modifiers; Stamped moves are trusted Metaplex Core assets with one or two modifiers.
    /// </summary>
    public enum MoveTier
    {
        Known = 0,
        Stamped = 1,
    }

    /// <summary>
    /// Move categories.
    /// Matches COMBAT_03A Section 2.
    /// </summary>
    public enum MoveCategory
    {
        DamagingMelee,
        DamagingRanged,
        StatusUtility,
        Support,
        Passive,
    }

    /// <summary>
    /// Targeting mode for a move.
    /// </summary>
    public enum MoveTargeting
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self,
        Field,          // Affects the whole field (both sides)
        RandomEnemy,
    }

    /// <summary>
    /// Contact type for damage moves (affects Backlash triggers, etc.).
    /// </summary>
    public enum ContactType
    {
        Contact,        // Physical contact (triggers Backlash)
        Projectile,     // Ranged projectile (mitigated by Firewall)
        None,           // Non-damaging or special
    }

    /// <summary>
    /// Rarity tier. Affects availability/complexity, NOT raw power.
    /// Matches COMBAT_03A Section 6.1 and COMBAT_04 rarity tiers.
    /// </summary>
    public enum RarityTier
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic,
    }

    /// <summary>
    /// Modifier scope: whether a modifier applies per-move or per-Chainling.
    /// Matches COMBAT_05 Active/Passive distinction.
    /// </summary>
    public enum ModifierScope
    {
        Active,     // Applies to the specific move it's attached to
        Passive,    // Applies globally while the modified move is equipped
    }

    /// <summary>
    /// Modifier category from COMBAT_05.
    /// </summary>
    public enum ModifierCategory
    {
        POT,    // Potency (accuracy, power)
        EFF,    // Efficiency (AP refund, bonus action)
        INIT,   // Initiative / turn-order effects
        AUG,    // Augmentation (extra hit, AoE)
        TRX,    // Transformation (type infusion, stat substitution)
        TRG,    // Trigger (on-hit, on-crit, on-KO, conditional)
        SYN,    // Synergy (team/field effects)
        DUR,    // Durability (passive MaxHP%, defensive sustain)
    }

    /// <summary>
    /// Per-move slot grouping for mutually exclusive modifier families.
    /// </summary>
    public enum ModifierSlotType
    {
        None,
        INIT,
        ActionEconomy,
        Targeting,
        Barrier,
        Durability,
        StatSubstitution,
    }

    /// <summary>
    /// Trigger conditions for modifier effects.
    /// </summary>
    public enum ModifierTriggerCondition
    {
        None,
        OnHit,
        OnCrit,
        OnMiss,
        OnKO,
        OnAllyKO,
        OnLowHP,
        OnStatusApplied,
        EndOfTurn,
        OnSwapIn,
        OnSwapOut,
        OnEscape,
        OnHPBelow30, // Backward-compatible legacy value; prefer OnLowHP.
        OnBattleStart,
        OnIncomingHit,
    }

    /// <summary>
    /// Lock state for a move NFT in the context of PvP wager escrow.
    /// Unlocked moves can be freely equipped/unequipped/traded.
    /// Escrowed moves are locked for the duration of a PvP match.
    /// </summary>
    public enum MoveLockState
    {
        Unlocked,   // Free to equip, unequip, trade, burn
        Escrowed,   // Locked in escrow for an active PvP wager match
    }

    /// <summary>
    /// Selection policy for active-condition moves.
    /// </summary>
    public enum MoveSelectionPolicy
    {
        EffectOnly, // Move is selectable; condition only gates conditional effects.
        Gate,       // Move is only selectable when condition is met.
    }

    /// <summary>
    /// Runtime condition types for active moves.
    /// </summary>
    public enum ActiveConditionType
    {
        None,
        SelfHPBelowPercent,
    }

    /// <summary>
    /// Declares the primary timing bucket for move-authored effects.
    /// Runtime truth is still determined by the executor that consumes the effect.
    /// </summary>
    public enum MoveEffectTimingBucket
    {
        UIEligibility,
        ResolveTimePreHit,
        ResolveTimeUtility,
        PostHit,
        TurnEnd,
        ReactivePassive,
    }

    /// <summary>
    /// Controls when utility payloads on damaging moves are executed.
    /// Utility and support moves always execute their utility payloads in the utility resolver.
    /// </summary>
    public enum MoveUtilityExecutionTiming
    {
        UtilityOnly,
        ResolveBeforeFirstHit,
        AfterEachConfirmedHit,
        AfterMoveComplete,
    }

    /// <summary>
    /// Combines legacy active conditions with resolve-time move predicates.
    /// </summary>
    public enum MoveConditionalEffectPolicy
    {
        LegacyAndResolveConditions,
        LegacyActiveConditionOnly,
        ResolveConditionsOnly,
        LegacyOrResolveConditions,
        AlwaysApply,
    }

    public enum MoveResolveConditionSubject
    {
        Self,
        Target,
    }

    public enum MoveResolveConditionType
    {
        None,
        HPBelowPercent,
        HPAtOrBelowPercent,
        HPAbovePercent,
        APAtLeast,
        APAtMost,
        HasStatus,
        MissingStatus,
        StageAtLeast,
        StageAtMost,
        SpeedHigherThanOther,
        BarrierActive,
        NoBarrier,
        DebuffCountAtLeast,
        StatusCountAtLeast,
        NoBuffs,
        BuffedPartnerCountAtLeast,
        StageAtLeastOtherStage,
        WasHitLastTurn,
        WasDamagedLastTurn,
        WasNotDamagedLastTurn,
        DidNotUseDamagingMoveThisTurn,
        AnyFoeAPGreaterThanSelf,
        StageHigherThanOtherStage,
    }

    /// <summary>
    /// Stage-bearing combat stats, including derived Accuracy/Evasion stages.
    /// Kept separate from CoreStat because Accuracy and Evasion are not base stats.
    /// </summary>
    public enum CombatStageStat
    {
        MeleeAtk,
        MeleeDef,
        RangedAtk,
        RangedDef,
        Speed,
        Accuracy,
        Evasion,
        CritRate,
    }

    public enum UtilityContactFilter
    {
        Any,
        Contact,
        Projectile,
        None,
    }

    public enum ArmedWindowTrigger
    {
        None,
        FoeKO,
        EnemyMiss,
        IncomingDebuff,
        IncomingHit,
        OwnerTurnStart,
    }

    /// <summary>
    /// Typed utility effect opcodes for Support/StatusUtility execution.
    /// </summary>
    public enum UtilityEffectType
    {
        MitigationWindow,
        GainApOnDamageIfSpeedHigher,
        FullAbsorbBarrier,
        GainAP,
        DrainAP,
        StealAP,
        TransferAP,
        RefundAP,
        NextMoveAPSurcharge,
        TurnStartPendingStatus,
        DamageThresholdBarrier,
        WallHitRetaliation,
        /// <summary>Cleanse up to N General debuffs. Params: maxCount:chance:targetOverride.</summary>
        Cleanse,
        /// <summary>Bonus heal % MaxHP when user Speed &gt; any active foe. Params: bonusPercent.</summary>
        BonusHealPercentIfSpeedHigher,
        /// <summary>Always-on passive incoming-damage mitigation metadata.</summary>
        IncomingDamageMitigation,
        /// <summary>Apply a deterministic stage change with optional cap.</summary>
        ChangeStage,
        /// <summary>Apply a seeded random stage/stat change.</summary>
        RandomStageChange,
        /// <summary>Arm a serializable event window consumed by resolver hooks.</summary>
        ArmUtilityWindow,
        /// <summary>Bank current AP and release it at the owner's next turn start.</summary>
        BankAPUntilNextTurn,
        /// <summary>Gain AP when the action ledger records enough distinct targets hit.</summary>
        GainApIfDistinctTargetsHitAtLeast,
        /// <summary>Persistent field aura applied to later swap-ins.</summary>
        FieldEntryStageChange,
        /// <summary>Passive AP discount metadata while above an HP threshold.</summary>
        MoveAPDiscountWhileHPAbove,
        /// <summary>Heal for a percentage of HP damage actually dealt.</summary>
        LifestealPercent,
        /// <summary>Heal when a selected stage is at or above the authored threshold.</summary>
        HealPercentIfStageAtLeast,
        /// <summary>Add 1 turn to the ally's shortest General duration buff. Params: stackKey.</summary>
        ExtendBuff,
        /// <summary>Raise each living ally's lowest core stage below +6 by 1. Params: stackKey.</summary>
        RaiseLowestStage,
        /// <summary>Apply a ChemistryOnly status from an explicitly authored utility. Params: statusId:turns:chance:target:stackKey.</summary>
        ApplyChemistryStatus,
        /// <summary>Remove up to N General buffs, shortest duration first. Params: maxCount:chance:target:stackKey.</summary>
        StripBuffs,
        /// <summary>Remove all General buffs and debuffs. Params: chance:target:stackKey.</summary>
        StripAllStatuses,
        /// <summary>Strip buffs and grant the source a stage per removed buff. Params: maxCount:chance:target:stat:amount:stackKey.</summary>
        StripBuffsThenStage,
        /// <summary>Heal the source for a percent of MaxHP per General debuff on the target. Params: percentPerDebuff:chance:stackKey.</summary>
        HealSelfPerTargetDebuff,
        /// <summary>One-hit Wall/Barrier mitigation. Params: percent:category:turns:stackKey.</summary>
        BlockWindow,
        /// <summary>Reflect a percent of damage prevented by the paired BlockWindow. Params: percent:stackKey.</summary>
        BlockReflect,
    }

    public enum BlockKind
    {
        None,
        Wall,
        Barrier,
    }

    public enum BlockTier
    {
        None,
        Lower,
        Mid,
        Greater,
    }

    public enum PassiveDisplayState
    {
        Active,
        Armed,
        Locked,
        Used,
    }

    /// <summary>
    /// Trigger conditions for passive moves.
    /// </summary>
    public enum PassiveTriggerCondition
    {
        None,               // No trigger (always active or manual activation)
        HPBelowThreshold,   // Triggers when HP drops below threshold percentage
        OnDamageTaken,      // Triggers when taking damage
        OnKO,               // Triggers when an enemy is KO'd
        OnAllyKO,           // Triggers when an ally is KO'd
        OnMiss,              // Triggers when an enemy misses an attack
        EndOfTurn,          // Triggers at end of turn
        TurnStart,
        OnHit,
        OnCrit,
        OnAllyHit,
        OnBuffExpired,
        OnAction,
    }
}
