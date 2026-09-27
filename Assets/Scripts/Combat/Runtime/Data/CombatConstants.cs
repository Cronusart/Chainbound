namespace Chainbound.Combat
{
    /// <summary>
    /// Authoritative combat constants from the spec sheets.
    /// All values are immutable and sourced from locked specs.
    /// </summary>
    public static class CombatConstants
    {
        // â”€â”€ AP System (02C Â§1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int AP_PER_TURN = 2;
        public const int AP_CAP = 10;
        public const int AP_COST_DAMAGING_MIN = 1;
        public const int AP_COST_DAMAGING_MAX = 3;
        public const int AP_COST_UTILITY_MIN = 0;
        public const int AP_COST_UTILITY_MAX = 2;
        public const int AP_COST_SUPPORT_MIN = 2;
        public const int AP_COST_SUPPORT_MAX = 4;
        public const int AP_COST_PASSIVE = 0;

        // â”€â”€ Damage Formula (02C Â§3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const float RANDOM_ROLL_MIN = 0.85f;
        public const float RANDOM_ROLL_MAX = 1.0f;
        public const float STAB_MULTIPLIER = 1.2f;
        public const float CHEMISTRY_ADVANTAGE_FACTOR = 1.2f;
        public const float CHEMISTRY_DISADVANTAGE_FACTOR = 0.8f;

        /// <summary>Flat damage progression scaler (stars/rebirth use StatScaleMultiplier on stats). Temporarily 1.0 for PvP baseline.</summary>
        public const float PROGRESSION_DAMAGE_FACTOR = 1f;

        public const float CRIT_MULTIPLIER_DEFAULT = 1.5f;

        // â”€â”€ Stages (02B Â§6) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int STAGE_MIN = -6;
        public const int STAGE_MAX = 6;
        public const int STAGE_PASSIVE_CHANGE_CAP = 1;
        public const int STAGE_ACTIVE_CHANGE_CAP = 2;
        public const int STAGE_CHEMISTRY_CHANGE_CAP = 3;

        // â”€â”€ Levels (02B Â§5) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int LEVEL_MIN = 1;
        public const int LEVEL_MAX = 100;
        public const int MVP_LEVEL_CAP = 5;

        // â”€â”€ IVs (02B Â§4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int IV_MIN = -5;
        public const int IV_MAX = 5;

        // ── Star Progression & Rebirth (ECONOMY_01) ─────────────────────
        public const int STAR_MAX = 5;
        public const int REBIRTH_MAX = 5;
        /// <summary>Legacy name: one player-allocation count per Cycle (applied as flat, not IV merge).</summary>
        public const int REBIRTH_IV_BONUS_PER_REBIRTH = 1;
        public const int STAT_COUNT = 6;

        // ── Cycle combat flats (no multiplicative star/Cycle scale) ─────
        public const int CYCLE_AUTO_STAT_FLAT = 1;
        public const int CYCLE_PICK_STAT_FLAT = 1;
        /// <summary>MaxHP Cycle auto: percent of species baseMaxHP per Cycle. Later: ProgressionConfigSO.</summary>
        public const float CYCLE_AUTO_HP_PERCENT = 0.05f;
        /// <summary>MaxHP Cycle pick: percent of species baseMaxHP per pick point. Later: ProgressionConfigSO.</summary>
        public const float CYCLE_PICK_HP_PERCENT = 0.05f;

        // â”€â”€ Slot System (03A Â§1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int BASE_SLOTS = MoveLoadoutSlotRules.BaseMoveSlots;
        public const int SLOTS_PER_REBIRTH = MoveLoadoutSlotRules.SlotsPerRebirth;
        public const int SLOTS_PER_EVOLUTION = 0;
        public const int MAX_SLOTS = MoveLoadoutSlotRules.MaxMoveSlots;

        // â”€â”€ Mitigation (02A Â§8, 02C Â§8) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Per-source mitigation clamp. 1.0 = uncapped. Later: CombatResolverConfigSO.</summary>
        public const float MITIGATION_SINGLE_SOURCE_CAP = 1.0f;
        public const float MITIGATION_FLOOR = 0.10f;  // Minimum 10% of original damage always dealt
        /// <summary>One wall/shield per Chainling; new barrier replaces prior. Later: CombatResolverConfigSO.</summary>
        public const bool EXCLUSIVE_BARRIERS_AND_WALLS = true;
        /// <summary>Clamp on summed passive MaxHP% from DUR modifiers. Later: Lever Board.</summary>
        public const float MAX_PASSIVE_MAX_HP_BONUS_PERCENT = 0.50f;

        // â”€â”€ Triggered Event Defaults (02A Â§7) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const float DEFAULT_ON_HIT_CHANCE = 0.25f;
        public const float DEFAULT_ON_CRIT_CHANCE = 0.50f;
        public const float REKT_LOSE_ACTION_CHANCE = 0.25f;
        public const float GLITCHED_MIND_MISACT_CHANCE = 0.50f;
        public const float SLOWED_LOSE_ACTION_CHANCE = 0.25f;
        public const float FIREWALL_PROJECTILE_MITIGATION = 0.30f;
        public const int SLOWED_SPEED_STAGES = -2;
        public const int SHATTERED_DEFENSE_STAGES = -1;
        public const float SHATTERED_CURRENT_HP_DAMAGE = 0.08f;
        public const float DRIP_HEAL_PERCENT_MAX_HP = 0.06f;
        public const float CASCADE_FAILURE_MAX_HP_DAMAGE = 0.25f;
        public const float SPLINTER_DAMAGE_MULTIPLIER = 0.65f;
        public const float SPLINTER_SINGLE_TARGET_DAMAGE_MULTIPLIER = 1.15f;
        public const int RUGGED_TICK_CAP = 15;

        // â”€â”€ Recoil & Self-Damage (02A Â§7.5) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const float OVERLEVERAGED_RECOIL = 0.25f;       // 25% of damage dealt
        public const float OVERCLOCKED_RECOIL_HP = 0.08f;      // 8% MaxHP
        public const float FOMO_SELF_DOT_HP = 0.0625f;         // 1/16 MaxHP per turn

        // â”€â”€ Team Format (2v2 with bench) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Number of Chainlings active on the field per team at once.</summary>
        public const int ACTIVE_PER_TEAM = 2;
        /// <summary>Total Chainlings per team roster (2 active + 1 bench).</summary>
        public const int ROSTER_SIZE = 3;
        /// <summary>Bench capacity per team (roster minus active field slots).</summary>
        public const int BENCH_SLOTS_PER_TEAM = ROSTER_SIZE - ACTIVE_PER_TEAM;

        // â”€â”€ Type Count â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int TYPE_COUNT = 14;  // Number of canonical types (excluding None)

        // â”€â”€ RAM System (Move Tier Progression) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int RAM_DAILY_CAP = 100;
        public const int RAM_PER_KNOWN_BURN = 10;

        // â”€â”€ Vending Machine Costs (MVP Economy) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int RAM_COST_GET_KNOWN = 5;
        public const int RAM_COST_STAMP_KNOWN = 75;
        public const int RAM_COST_LEVEL5_FLAGSHIP_FINALIZE = 0;
        public const int VENDING_DAILY_CAP = 10;

        // â”€â”€ Move Vault â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int MAX_KNOWN_MOVES = 50;

        // â”€â”€ Chainling Vault â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int MAX_CHAINLINGS = 25;

        // â”€â”€ Equipped Move Registry (on-chain fixed array) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int EQUIPPED_SLOTS_ON_CHAIN = MoveLoadoutSlotRules.OnChainStorageSlots;

        // â”€â”€ Ranked Constraints â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // â”€â”€ On-Chain Fees â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const float REROLL_SOL_FEE = 0.01f;
    }
}
