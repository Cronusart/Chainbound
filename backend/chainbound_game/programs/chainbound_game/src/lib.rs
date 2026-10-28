#![allow(clippy::too_many_arguments)] // Anchor instruction ABIs intentionally expose explicit arguments.

use crate::instruction::FulfillEncounter as FulfillEncounterInstruction;
use crate::instruction::FulfillRoll as FulfillRollInstruction;
use anchor_lang::prelude::*;
use ephemeral_rollups_sdk::anchor::{vrf, vrf_callback};
use ephemeral_rollups_sdk::vrf::{
    consts::DEFAULT_QUEUE,
    instructions::{create_request_scoped_randomness_ix, RequestRandomnessParams},
    types::SerializableAccountMeta,
};
use solana_sha256_hasher::hashv;
use mpl_core::{
    accounts::{BaseAssetV1, BaseCollectionV1},
    instructions::{BurnV1CpiBuilder, CreateV2CpiBuilder},
    types::{
        Attribute, Attributes, DataState, Plugin, PluginAuthority, PluginAuthorityPair,
        UpdateAuthority,
    },
};

declare_id!("9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ");

const MAX_KNOWN_MOVES: usize = 50;
const MAX_CHAINLINGS: usize = 25;
const EQUIPPED_SLOTS_ON_CHAIN: usize = 16;
const MAX_MODIFIER_ID_LEN: usize = 32;
const MAX_ASSET_NAME_LEN: usize = 64;
const MAX_ASSET_URI_LEN: usize = 200;
const MAX_SPECIES_ID_LEN: usize = 64;
const MAX_VISUAL_VARIANT_ID_LEN: usize = 64;
const MAX_ENTANGLED_TYPE_LEN: usize = 32;
const MAX_MUTATION_IDS: usize = 4;
const MAX_MUTATION_ID_LEN: usize = 32;
const MAX_CHAINLING_KNOWN_MOVE_IDS: usize = 16;
const COMPONENT_STAMPED_MOVE: u8 = 0;
const COMPONENT_COSMETIC: u8 = 1;
const KNOWN_ROLL_RAM_COST: u32 = 5;
const RAM_RESERVATION_KIND_KNOWN: u8 = 0;
const RAM_RESERVATION_KIND_STAMP: u8 = 1;
const RAM_RESERVATION_KIND_BIND: u8 = 2;
const RAM_RESERVATION_KIND_PROFILE_SPEND: u8 = 3;
const RAM_RECONCILE_CREDIT: u8 = 0;
const RAM_RECONCILE_DEBIT: u8 = 1;
const SECONDS_PER_DAY: i64 = 86_400;
const ENCOUNTER_PENDING: u8 = 0;
const ENCOUNTER_FULFILLED: u8 = 1;
const ENCOUNTER_CONSUMED: u8 = 2;
const ROLL_KIND_KNOWN: u8 = 1;
const ROLL_KIND_STAMP: u8 = 2;
const ROLL_PENDING: u8 = 0;
const ROLL_FULFILLED: u8 = 1;
const ROLL_CONSUMED: u8 = 2;
const ROLL_CANCELLED: u8 = 3;
const ROLL_TIER_COUNT: usize = 6;
const ROLL_TIER_UNSET: u8 = u8::MAX;
const ROLL_PPM_TOTAL: u32 = 1_000_000;
// About an hour of Devnet slots; the retention window is still an open design decision.
const ROLL_CLOSE_DELAY_SLOTS: u64 = 9_000;
// About a minute: well past the measured p90 fulfill of ~2 s, so only a stuck request is cancelled.
const ROLL_CANCEL_DELAY_SLOTS: u64 = 150;
// Encounter requests keep the same retention as rolls before their rent can be reclaimed.
const ENCOUNTER_CLOSE_DELAY_SLOTS: u64 = ROLL_CLOSE_DELAY_SLOTS;
const ROLL_SEED_DOMAIN: &[u8] = b"chainbound:roll:v1";

#[program]
pub mod chainbound_game {
    use super::*;

    pub fn initialize(
        ctx: Context<Initialize>,
        result_authority: Pubkey,
        schema_version: u16,
        ram_daily_cap: u16,
        ram_per_burn: u32,
        instruction_flags: u64,
    ) -> Result<()> {
        require!(
            result_authority != Pubkey::default(),
            ChainboundError::InvalidAuthority
        );
        require!(
            ram_per_burn <= u16::MAX as u32,
            ChainboundError::InvalidConfig
        );
        let config = &mut ctx.accounts.config;
        config.program_authority = ctx.accounts.program_authority.key();
        config.result_authority = result_authority;
        config.schema_version = schema_version;
        config.ram_daily_cap = ram_daily_cap;
        config.ram_per_burn = ram_per_burn;
        config.instruction_flags = instruction_flags;
        config.bump = ctx.bumps.config;
        Ok(())
    }

    pub fn update_game_config(
        ctx: Context<UpdateGameConfig>,
        result_authority: Pubkey,
        schema_version: u16,
        ram_daily_cap: u16,
        ram_per_burn: u32,
        instruction_flags: u64,
    ) -> Result<()> {
        require!(
            result_authority != Pubkey::default(),
            ChainboundError::InvalidAuthority
        );
        require!(
            ram_per_burn <= u16::MAX as u32,
            ChainboundError::InvalidConfig
        );
        let config = &mut ctx.accounts.config;
        config.result_authority = result_authority;
        config.schema_version = schema_version;
        config.ram_daily_cap = ram_daily_cap;
        config.ram_per_burn = ram_per_burn;
        config.instruction_flags = instruction_flags;
        Ok(())
    }

    /// Step one of handing config control to a new authority (for example a multisig vault).
    /// Nothing changes until the new authority accepts, so a mistyped key can't lock the config.
    pub fn propose_program_authority(
        ctx: Context<ProposeProgramAuthority>,
        new_authority: Pubkey,
    ) -> Result<()> {
        validate_authority_proposal(&ctx.accounts.game_config.program_authority, &new_authority)?;
        let transfer = &mut ctx.accounts.authority_transfer;
        transfer.current_authority = ctx.accounts.program_authority.key();
        transfer.pending_authority = new_authority;
        transfer.proposed_at_slot = Clock::get()?.slot;
        transfer.bump = ctx.bumps.authority_transfer;
        emit!(ProgramAuthorityProposed {
            current_authority: transfer.current_authority,
            pending_authority: new_authority,
        });
        Ok(())
    }

    /// Step two: the pending authority signs to take control. The proposal's rent goes back to
    /// the authority that paid for it.
    pub fn accept_program_authority(ctx: Context<AcceptProgramAuthority>) -> Result<()> {
        let config = &mut ctx.accounts.game_config;
        let previous_authority = config.program_authority;
        config.program_authority = ctx.accounts.pending_authority.key();
        emit!(ProgramAuthorityTransferred {
            previous_authority,
            new_authority: config.program_authority,
        });
        Ok(())
    }

    /// Withdraws an unaccepted proposal so a different authority can be proposed.
    pub fn cancel_program_authority_transfer(
        ctx: Context<CancelProgramAuthorityTransfer>,
    ) -> Result<()> {
        emit!(ProgramAuthorityTransferCancelled {
            current_authority: ctx.accounts.program_authority.key(),
            pending_authority: ctx.accounts.authority_transfer.pending_authority,
        });
        Ok(())
    }

    pub fn create_profile(ctx: Context<CreateProfile>) -> Result<()> {
        let profile = &mut ctx.accounts.profile;
        profile.owner = ctx.accounts.authority.key();
        profile.schema_version = 2;
        profile.created_at_slot = Clock::get()?.slot;
        profile.wins = 0;
        profile.losses = 0;
        profile.last_known_scene_id = 0;
        profile.last_known_pos_x = 0;
        profile.last_known_pos_y = 0;
        profile.last_known_pos_z = 0;
        profile.ram_balance = 0;
        profile.daily_ram_earned = 0;
        profile.last_ram_reset_timestamp = Clock::get()?.unix_timestamp;
        profile.ram_lifetime_earned = 0;
        profile.bump = ctx.bumps.profile;
        Ok(())
    }

    pub fn update_wins_losses(ctx: Context<AuthorityProfileMutation>, is_win: bool) -> Result<()> {
        let profile = &mut ctx.accounts.profile;
        if is_win {
            profile.wins = profile
                .wins
                .checked_add(1)
                .ok_or(ChainboundError::ArithmeticOverflow)?;
        } else {
            profile.losses = profile
                .losses
                .checked_add(1)
                .ok_or(ChainboundError::ArithmeticOverflow)?;
        }
        Ok(())
    }

    pub fn update_position(
        ctx: Context<ProfileMutation>,
        scene_id: u32,
        x: i16,
        y: i16,
        z: i16,
    ) -> Result<()> {
        let profile = &mut ctx.accounts.profile;
        profile.last_known_scene_id = scene_id;
        profile.last_known_pos_x = x;
        profile.last_known_pos_y = y;
        profile.last_known_pos_z = z;
        Ok(())
    }

    pub fn spend_ram(ctx: Context<AuthorityProfileMutation>, amount: u32) -> Result<()> {
        let profile = &mut ctx.accounts.profile;
        require!(
            profile.ram_balance >= amount,
            ChainboundError::InsufficientRam
        );
        profile.ram_balance = profile
            .ram_balance
            .checked_sub(amount)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        Ok(())
    }

    /// Credits RAM under the configured daily cap. Profile creation intentionally grants zero;
    /// only the pinned result authority can authorize a grant, and the receipt makes retries unique.
    pub fn grant_ram(ctx: Context<GrantRam>, idempotency_key: [u8; 32], amount: u32) -> Result<()> {
        let now = Clock::get()?.unix_timestamp;
        apply_capped_ram_grant(
            &mut ctx.accounts.profile,
            ctx.accounts.config.ram_daily_cap,
            amount,
            now,
        )?;

        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = ctx.accounts.owner.key();
        receipt.idempotency_key = idempotency_key;
        receipt.amount = amount;
        receipt.balance_after = ctx.accounts.profile.ram_balance;
        receipt.utc_day = now.div_euclid(SECONDS_PER_DAY);
        receipt.result_authority = ctx.accounts.result_authority.key();
        receipt.bump = ctx.bumps.receipt;

        emit!(RamGranted {
            owner: receipt.owner,
            amount,
            balance_after: receipt.balance_after,
            utc_day: receipt.utc_day,
            idempotency_key,
        });
        Ok(())
    }

    /// Creates the shared entitlement reservation required by every production RAM spend.
    /// The result authority pays for and controls the reservation; the player cannot mint one.
    pub fn create_ram_reservation(
        ctx: Context<CreateRamReservation>,
        operation_hash: [u8; 32],
        amount: u32,
        kind: u8,
        expires_at: i64,
    ) -> Result<()> {
        let now = Clock::get()?.unix_timestamp;
        require!(amount > 0, ChainboundError::InvalidRamAmount);
        require!(expires_at > now, ChainboundError::ReservationExpired);
        require!(
            kind <= RAM_RESERVATION_KIND_PROFILE_SPEND,
            ChainboundError::InvalidReservationKind
        );
        require!(
            ctx.accounts.profile.ram_balance >= amount,
            ChainboundError::InsufficientRam
        );

        let reservation = &mut ctx.accounts.reservation;
        reservation.owner = ctx.accounts.owner.key();
        reservation.operation_hash = operation_hash;
        reservation.amount = amount;
        reservation.kind = kind;
        reservation.expires_at = expires_at;
        reservation.consumed = false;
        reservation.result_authority = ctx.accounts.result_authority.key();
        reservation.bump = ctx.bumps.reservation;

        emit!(RamReserved {
            owner: reservation.owner,
            operation_hash,
            amount,
            kind,
            expires_at,
        });
        Ok(())
    }

    pub fn cancel_ram_reservation(
        ctx: Context<CancelRamReservation>,
        operation_hash: [u8; 32],
    ) -> Result<()> {
        require!(
            ctx.accounts.reservation.operation_hash == operation_hash,
            ChainboundError::IdempotencyMismatch
        );
        require!(
            !ctx.accounts.reservation.consumed,
            ChainboundError::ReservationAlreadyConsumed
        );
        emit!(RamReservationCancelled {
            owner: ctx.accounts.reservation.owner,
            operation_hash,
        });
        Ok(())
    }

    /// Credits verified purchases without touching earned-RAM counters or the daily cap.
    pub fn credit_purchased_ram(
        ctx: Context<CreditPurchasedRam>,
        order_hash: [u8; 32],
        amount: u32,
    ) -> Result<()> {
        let balance_before = ctx.accounts.profile.ram_balance;
        apply_uncapped_ram_credit(&mut ctx.accounts.profile, amount)?;

        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = ctx.accounts.owner.key();
        receipt.order_hash = order_hash;
        receipt.amount = amount;
        receipt.balance_before = balance_before;
        receipt.balance_after = ctx.accounts.profile.ram_balance;
        receipt.result_authority = ctx.accounts.result_authority.key();
        receipt.bump = ctx.bumps.receipt;

        emit!(PurchasedRamCredited {
            owner: receipt.owner,
            order_hash,
            amount,
            balance_before,
            balance_after: receipt.balance_after,
        });
        Ok(())
    }

    /// Retires on-chain purchasing power before a profile-native gameplay mutation settles.
    pub fn debit_reserved_ram(
        ctx: Context<DebitReservedRam>,
        operation_hash: [u8; 32],
        amount: u32,
        kind: u8,
    ) -> Result<()> {
        consume_ram_reservation(
            &mut ctx.accounts.reservation,
            ctx.accounts.owner.key(),
            operation_hash,
            amount,
            kind,
            Clock::get()?.unix_timestamp,
        )?;
        let balance_before = ctx.accounts.profile.ram_balance;
        apply_ram_debit(&mut ctx.accounts.profile, amount)?;

        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = ctx.accounts.owner.key();
        receipt.operation_hash = operation_hash;
        receipt.amount = amount;
        receipt.kind = kind;
        receipt.balance_before = balance_before;
        receipt.balance_after = ctx.accounts.profile.ram_balance;
        receipt.result_authority = ctx.accounts.result_authority.key();
        receipt.bump = ctx.bumps.receipt;

        emit!(ReservedRamDebited {
            owner: receipt.owner,
            operation_hash,
            amount,
            kind,
            balance_before,
            balance_after: receipt.balance_after,
        });
        Ok(())
    }

    /// Applies a one-time operator-approved delta for a frozen legacy reconciliation case.
    pub fn reconcile_ram_delta(
        ctx: Context<ReconcileRamDelta>,
        case_hash: [u8; 32],
        direction: u8,
        amount: u32,
    ) -> Result<()> {
        require!(
            direction == RAM_RECONCILE_CREDIT || direction == RAM_RECONCILE_DEBIT,
            ChainboundError::InvalidReconciliationDirection
        );
        let balance_before = ctx.accounts.profile.ram_balance;
        if direction == RAM_RECONCILE_CREDIT {
            apply_uncapped_ram_credit(&mut ctx.accounts.profile, amount)?;
        } else {
            apply_ram_debit(&mut ctx.accounts.profile, amount)?;
        }

        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = ctx.accounts.owner.key();
        receipt.case_hash = case_hash;
        receipt.direction = direction;
        receipt.amount = amount;
        receipt.balance_before = balance_before;
        receipt.balance_after = ctx.accounts.profile.ram_balance;
        receipt.result_authority = ctx.accounts.result_authority.key();
        receipt.bump = ctx.bumps.receipt;

        emit!(RamDeltaReconciled {
            owner: receipt.owner,
            case_hash,
            direction,
            amount,
            balance_before,
            balance_after: receipt.balance_after,
        });
        Ok(())
    }

    pub fn create_move_vault(ctx: Context<CreateMoveVault>) -> Result<()> {
        let vault = &mut ctx.accounts.vault;
        vault.owner = ctx.accounts.authority.key();
        vault.known_capacity = MAX_KNOWN_MOVES as u8;
        vault.known_count = 0;
        vault.known_moves = Vec::new();
        vault.next_instance_id = 1;
        vault.bump = ctx.bumps.vault;
        Ok(())
    }

    /// Authoritative Academy roll. The player wallet is not a signer: the result authority
    /// writes one zero-modifier Known instance and an immutable replay receipt.
    pub fn roll_known_move(
        ctx: Context<RollKnownMove>,
        idempotency_key: [u8; 32],
        move_id: u16,
        element: u8,
        base_power: u16,
    ) -> Result<()> {
        consume_ram_reservation(
            &mut ctx.accounts.reservation,
            ctx.accounts.owner.key(),
            idempotency_key,
            KNOWN_ROLL_RAM_COST,
            RAM_RESERVATION_KIND_KNOWN,
            Clock::get()?.unix_timestamp,
        )?;
        require!(
            ctx.accounts.profile.ram_balance >= KNOWN_ROLL_RAM_COST,
            ChainboundError::InsufficientRam
        );
        ctx.accounts.profile.ram_balance = ctx
            .accounts
            .profile
            .ram_balance
            .checked_sub(KNOWN_ROLL_RAM_COST)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        let instance_id = grant_known_move(
            &mut ctx.accounts.vault,
            move_id,
            element,
            base_power,
            ctx.accounts.ownership_config.generator_version,
            ctx.accounts.ownership_config.pool_hash,
        )?;
        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = ctx.accounts.owner.key();
        receipt.idempotency_key = idempotency_key;
        receipt.known_instance_id = instance_id;
        receipt.move_id = move_id;
        receipt.generator_version = ctx.accounts.ownership_config.generator_version;
        receipt.pool_hash = ctx.accounts.ownership_config.pool_hash;
        receipt.bump = ctx.bumps.receipt;
        emit!(KnownMoveRolled {
            owner: receipt.owner,
            known_instance_id: instance_id,
            move_id
        });
        Ok(())
    }

    pub fn burn_known_move_for_ram(
        ctx: Context<AuthorityBurnKnownMoveForRam>,
        move_index: u8,
    ) -> Result<()> {
        let vault = &mut ctx.accounts.vault;
        let profile = &mut ctx.accounts.profile;
        let index = move_index as usize;
        let count = vault.known_count as usize;
        require!(index < count, ChainboundError::InvalidIndex);
        vault.known_moves.swap_remove(index);
        vault.known_count = vault
            .known_count
            .checked_sub(1)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        apply_capped_ram_grant(
            profile,
            ctx.accounts.config.ram_daily_cap,
            ctx.accounts.config.ram_per_burn,
            Clock::get()?.unix_timestamp,
        )?;
        Ok(())
    }

    pub fn initialize_chainling_vault(ctx: Context<CreateChainlingVault>) -> Result<()> {
        let vault = &mut ctx.accounts.vault;
        vault.owner = ctx.accounts.authority.key();
        vault.chainling_capacity = MAX_CHAINLINGS as u8;
        vault.chainling_count = 0;
        vault.chainlings = [ChainlingEntry::default(); MAX_CHAINLINGS];
        vault.next_instance_id = 1;
        vault.bump = ctx.bumps.vault;
        Ok(())
    }

    pub fn add_chainling_from_capture(
        ctx: Context<AuthorityChainlingVaultMutation>,
        chainling_id: u16,
        level: u16,
        ivs_seed: u64,
    ) -> Result<()> {
        let vault = &mut ctx.accounts.vault;
        let index = vault.chainling_count as usize;
        require!(index < MAX_CHAINLINGS, ChainboundError::VaultFull);
        vault.chainlings[index] = ChainlingEntry {
            chainling_id,
            instance_id: vault.next_instance_id,
            level,
            xp: 0,
            ivs_seed,
            flags: 0,
            pending_level_reward: 0,
            pending_reward_seed: 0,
            claimed_reward_bits: 0,
        };
        vault.chainling_count = vault
            .chainling_count
            .checked_add(1)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        vault.next_instance_id = vault
            .next_instance_id
            .checked_add(1)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        Ok(())
    }

    pub fn remove_chainling(
        ctx: Context<AuthorityChainlingVaultMutation>,
        index: u8,
    ) -> Result<()> {
        let vault = &mut ctx.accounts.vault;
        let index = index as usize;
        let count = vault.chainling_count as usize;
        require!(index < count, ChainboundError::InvalidIndex);
        if count > 1 && index != count - 1 {
            vault.chainlings[index] = vault.chainlings[count - 1];
        }
        vault.chainlings[count - 1] = ChainlingEntry::default();
        vault.chainling_count = vault
            .chainling_count
            .checked_sub(1)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        Ok(())
    }

    pub fn initialize_equipped_registry(
        ctx: Context<CreateEquippedRegistry>,
        chainling_instance_id: u32,
    ) -> Result<()> {
        let registry = &mut ctx.accounts.registry;
        registry.owner = ctx.accounts.authority.key();
        registry.chainling_instance_id = chainling_instance_id;
        registry.slots = [EquippedMoveSlot::default(); EQUIPPED_SLOTS_ON_CHAIN];
        registry.bump = ctx.bumps.registry;
        Ok(())
    }

    pub fn set_equipped_slot(
        ctx: Context<AuthorityEquippedRegistryMutation>,
        slot_index: u8,
        tier: u8,
        source_kind: u8,
        known_index: u8,
        asset_id: [u8; 32],
        move_id: u16,
        mod_cache: u64,
    ) -> Result<()> {
        let registry = &mut ctx.accounts.registry;
        let index = slot_index as usize;
        require!(
            index < EQUIPPED_SLOTS_ON_CHAIN,
            ChainboundError::InvalidIndex
        );
        registry.slots[index] = EquippedMoveSlot {
            tier,
            source_kind,
            known_index,
            asset_id,
            move_id,
            mod_cache,
        };
        Ok(())
    }

    pub fn create_chainling(
        ctx: Context<AuthorityCreateChainlingAccount>,
        chainling_id: u64,
        species_id: String,
        level: u16,
        ivs: [i8; 6],
        ivs_seed: u64,
        mutated_type_flag: u8,
        entangled_flag: u8,
        base_visual_variant_id: String,
        entangled_type: String,
        mutation_ids: Vec<String>,
        known_move_ids: Vec<u16>,
    ) -> Result<()> {
        require!(
            mutation_ids.len() <= MAX_MUTATION_IDS,
            ChainboundError::TooManyMutations
        );
        require!(
            known_move_ids.len() <= MAX_CHAINLING_KNOWN_MOVE_IDS,
            ChainboundError::TooManyKnownMoves
        );

        let account = &mut ctx.accounts.chainling;
        account.owner = ctx.accounts.player.key();
        account.chainling_id = chainling_id;
        account.species_id = bounded_string(species_id, MAX_SPECIES_ID_LEN)?;
        account.level = level;
        account.ivs = ivs;
        account.ivs_seed = ivs_seed;
        account.mutated_type_flag = mutated_type_flag != 0;
        account.entangled_flag = entangled_flag != 0;
        account.base_visual_variant_id =
            bounded_string(base_visual_variant_id, MAX_VISUAL_VARIANT_ID_LEN)?;
        account.entangled_type = bounded_string(entangled_type, MAX_ENTANGLED_TYPE_LEN)?;
        account.mutation_ids = mutation_ids
            .into_iter()
            .map(|value| bounded_string(value, MAX_MUTATION_ID_LEN))
            .collect::<Result<Vec<_>>>()?;
        account.known_move_ids = known_move_ids;
        account.is_bound = false;
        account.archive_asset = Pubkey::default();
        account.bump = ctx.bumps.chainling;
        Ok(())
    }

    pub fn initialize_ownership_config(
        ctx: Context<InitializeOwnershipConfig>,
        schema_version: u16,
        moves_collection: Pubkey,
        archives_collection: Pubkey,
        covers_collection: Pubkey,
        cosmetics_collection: Pubkey,
        stamp_ram_cost: u32,
        bind_ram_cost: u32,
        generator_version: u32,
        pool_hash: [u8; 32],
    ) -> Result<()> {
        validate_ownership_config_keys(
            moves_collection,
            archives_collection,
            covers_collection,
            cosmetics_collection,
        )?;
        let config = &mut ctx.accounts.ownership_config;
        config.schema_version = schema_version;
        config.moves_collection = moves_collection;
        config.archives_collection = archives_collection;
        config.covers_collection = covers_collection;
        config.cosmetics_collection = cosmetics_collection;
        config.stamp_ram_cost = stamp_ram_cost;
        config.bind_ram_cost = bind_ram_cost;
        config.generator_version = generator_version;
        config.pool_hash = pool_hash;
        config.bump = ctx.bumps.ownership_config;
        Ok(())
    }

    pub fn update_ownership_config(
        ctx: Context<UpdateOwnershipConfig>,
        schema_version: u16,
        moves_collection: Pubkey,
        archives_collection: Pubkey,
        covers_collection: Pubkey,
        cosmetics_collection: Pubkey,
        stamp_ram_cost: u32,
        bind_ram_cost: u32,
        generator_version: u32,
        pool_hash: [u8; 32],
    ) -> Result<()> {
        validate_ownership_config_keys(
            moves_collection,
            archives_collection,
            covers_collection,
            cosmetics_collection,
        )?;
        let config = &mut ctx.accounts.ownership_config;
        config.schema_version = schema_version;
        config.moves_collection = moves_collection;
        config.archives_collection = archives_collection;
        config.covers_collection = covers_collection;
        config.cosmetics_collection = cosmetics_collection;
        config.stamp_ram_cost = stamp_ram_cost;
        config.bind_ram_cost = bind_ram_cost;
        config.generator_version = generator_version;
        config.pool_hash = pool_hash;
        Ok(())
    }

    /// Result-authority/VRF crank records the immutable modifier result. The owner wallet
    /// performs the actual Stamp in a later atomic transaction.
    pub fn prepare_stamp_ticket(
        ctx: Context<PrepareStampTicket>,
        idempotency_key: [u8; 32],
        known_instance_id: u64,
        modifier_ids: Vec<String>,
        rarity: u8,
        asset_name: String,
        asset_uri: String,
        expires_at: i64,
    ) -> Result<()> {
        require!(
            modifier_ids.len() == 1 || modifier_ids.len() == 2,
            ChainboundError::InvalidModifierCount
        );
        require!(
            modifier_ids.iter().all(|value| !value.trim().is_empty()),
            ChainboundError::InvalidModifier
        );
        if modifier_ids.len() == 2 {
            require!(
                modifier_ids[0] != modifier_ids[1],
                ChainboundError::DuplicateModifier
            );
        }
        let now = Clock::get()?.unix_timestamp;
        require!(expires_at > now, ChainboundError::TicketExpired);

        let known = find_known_move(&ctx.accounts.vault, known_instance_id)?;
        let mut stored_modifiers = [String::new(), String::new()];
        for (index, modifier_id) in modifier_ids.into_iter().enumerate() {
            stored_modifiers[index] = bounded_string(modifier_id, MAX_MODIFIER_ID_LEN)?;
        }

        let ticket = &mut ctx.accounts.ticket;
        ticket.owner = ctx.accounts.owner.key();
        ticket.idempotency_key = idempotency_key;
        ticket.known_instance_id = known_instance_id;
        ticket.move_id = known.move_id;
        ticket.modifier_count = if stored_modifiers[1].is_empty() { 1 } else { 2 };
        ticket.modifier_ids = stored_modifiers;
        ticket.rarity = rarity;
        ticket.asset_name = bounded_string(asset_name, MAX_ASSET_NAME_LEN)?;
        ticket.asset_uri = bounded_string(asset_uri, MAX_ASSET_URI_LEN)?;
        ticket.expires_at = expires_at;
        ticket.consumed = false;
        ticket.stamped_asset = Pubkey::default();
        ticket.generator_version = ctx.accounts.ownership_config.generator_version;
        ticket.pool_hash = ctx.accounts.ownership_config.pool_hash;
        ticket.bump = ctx.bumps.ticket;
        Ok(())
    }

    /// Records a Stamp ticket for a profile-owned Known move. The Known exists
    /// only in the authenticated Postgres profile until this wallet-signed
    /// transaction creates the Core asset; no wallet MoveVault row is required.
    pub fn prepare_profile_stamp_ticket(
        ctx: Context<PrepareProfileStampTicket>,
        idempotency_key: [u8; 32],
        known_reference: u64,
        move_id: u16,
        modifier_ids: Vec<String>,
        rarity: u8,
        asset_name: String,
        asset_uri: String,
        expires_at: i64,
    ) -> Result<()> {
        require!(known_reference != 0, ChainboundError::InvalidKnownReference);
        require!(
            modifier_ids.len() == 1 || modifier_ids.len() == 2,
            ChainboundError::InvalidModifierCount
        );
        require!(
            modifier_ids.iter().all(|value| !value.trim().is_empty()),
            ChainboundError::InvalidModifier
        );
        if modifier_ids.len() == 2 {
            require!(
                modifier_ids[0] != modifier_ids[1],
                ChainboundError::DuplicateModifier
            );
        }
        let now = Clock::get()?.unix_timestamp;
        require!(expires_at > now, ChainboundError::TicketExpired);

        let mut stored_modifiers = [String::new(), String::new()];
        for (index, modifier_id) in modifier_ids.into_iter().enumerate() {
            stored_modifiers[index] = bounded_string(modifier_id, MAX_MODIFIER_ID_LEN)?;
        }

        let ticket = &mut ctx.accounts.ticket;
        ticket.owner = ctx.accounts.owner.key();
        ticket.idempotency_key = idempotency_key;
        ticket.known_instance_id = known_reference;
        ticket.move_id = move_id;
        ticket.modifier_count = if stored_modifiers[1].is_empty() { 1 } else { 2 };
        ticket.modifier_ids = stored_modifiers;
        ticket.rarity = rarity;
        ticket.asset_name = bounded_string(asset_name, MAX_ASSET_NAME_LEN)?;
        ticket.asset_uri = bounded_string(asset_uri, MAX_ASSET_URI_LEN)?;
        ticket.expires_at = expires_at;
        ticket.consumed = false;
        ticket.stamped_asset = Pubkey::default();
        ticket.generator_version = ctx.accounts.ownership_config.generator_version;
        ticket.pool_hash = ctx.accounts.ownership_config.pool_hash;
        ticket.bump = ctx.bumps.ticket;
        Ok(())
    }

    /// Atomically consumes one Known instance and RAM and creates its Stamped Core asset.
    pub fn stamp_known_move(ctx: Context<StampKnownMove>, idempotency_key: [u8; 32]) -> Result<()> {
        let owner_key = ctx.accounts.owner.key();
        let now = Clock::get()?.unix_timestamp;
        let ticket = &ctx.accounts.ticket;

        consume_ram_reservation(
            &mut ctx.accounts.reservation,
            owner_key,
            idempotency_key,
            ctx.accounts.ownership_config.stamp_ram_cost,
            RAM_RESERVATION_KIND_STAMP,
            now,
        )?;

        require!(ticket.owner == owner_key, ChainboundError::InvalidAuthority);
        require!(
            ticket.idempotency_key == idempotency_key,
            ChainboundError::IdempotencyMismatch
        );
        require!(!ticket.consumed, ChainboundError::TicketAlreadyConsumed);
        require!(ticket.expires_at >= now, ChainboundError::TicketExpired);
        require!(
            ticket.modifier_count == 1 || ticket.modifier_count == 2,
            ChainboundError::InvalidModifierCount
        );
        require!(
            ticket.generator_version == ctx.accounts.ownership_config.generator_version
                && ticket.pool_hash == ctx.accounts.ownership_config.pool_hash,
            ChainboundError::StaleConfiguration
        );
        require!(
            !ticket.modifier_ids[0].is_empty()
                && (ticket.modifier_count == 1 || !ticket.modifier_ids[1].is_empty()),
            ChainboundError::InvalidModifier
        );
        if ticket.modifier_count == 2 {
            require!(
                ticket.modifier_ids[0] != ticket.modifier_ids[1],
                ChainboundError::DuplicateModifier
            );
        }
        require!(
            ctx.accounts.profile.ram_balance >= ctx.accounts.ownership_config.stamp_ram_cost,
            ChainboundError::InsufficientRam
        );

        validate_collection(
            &ctx.accounts.moves_collection,
            ctx.accounts.ownership_config.moves_collection,
            ctx.accounts.core_authority.key(),
        )?;

        let known = find_known_move(&ctx.accounts.vault, ticket.known_instance_id)?;
        require!(
            known.move_id == ticket.move_id,
            ChainboundError::KnownMoveMismatch
        );
        require!(
            known.generator_version == ticket.generator_version
                && known.pool_hash == ticket.pool_hash,
            ChainboundError::StaleConfiguration
        );

        let attributes = stamped_attributes(&ctx.accounts.ownership_config, ticket);
        let known_instance_id = ticket.known_instance_id;
        let move_id = ticket.move_id;
        let modifier_count = ticket.modifier_count;

        remove_known_by_instance(&mut ctx.accounts.vault, known_instance_id)?;
        ctx.accounts.profile.ram_balance = ctx
            .accounts
            .profile
            .ram_balance
            .checked_sub(ctx.accounts.ownership_config.stamp_ram_cost)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        ctx.accounts.ticket.consumed = true;
        ctx.accounts.ticket.stamped_asset = ctx.accounts.stamped_asset.key();

        let core_program_info = ctx.accounts.core_program.to_account_info();
        let stamped_asset_info = ctx.accounts.stamped_asset.to_account_info();
        let moves_collection_info = ctx.accounts.moves_collection.to_account_info();
        let core_authority_info = ctx.accounts.core_authority.to_account_info();
        let owner_info = ctx.accounts.owner.to_account_info();
        let system_program_info = ctx.accounts.system_program.to_account_info();
        let asset_bump = [ctx.bumps.stamped_asset];
        let authority_bump = [ctx.bumps.core_authority];
        let asset_seeds: &[&[u8]] = &[
            b"stamped_asset",
            owner_key.as_ref(),
            idempotency_key.as_ref(),
            &asset_bump,
        ];
        let authority_seeds: &[&[u8]] = &[b"core_authority", &authority_bump];
        let signer_seeds: &[&[&[u8]]] = &[asset_seeds, authority_seeds];

        CreateV2CpiBuilder::new(&core_program_info)
            .asset(&stamped_asset_info)
            .collection(Some(&moves_collection_info))
            .authority(Some(&core_authority_info))
            .payer(&owner_info)
            .owner(Some(&owner_info))
            // Core derives the asset update authority from the collection. Passing both
            // accounts is rejected by CreateV2 as ConflictingAuthority (custom error 29).
            .update_authority(None)
            .system_program(&system_program_info)
            .data_state(DataState::AccountState)
            .name(ctx.accounts.ticket.asset_name.clone())
            .uri(ctx.accounts.ticket.asset_uri.clone())
            .plugins(vec![PluginAuthorityPair {
                plugin: Plugin::Attributes(Attributes {
                    attribute_list: attributes,
                }),
                authority: Some(PluginAuthority::UpdateAuthority),
            }])
            .invoke_signed(signer_seeds)?;

        emit!(StampedMoveCreated {
            owner: owner_key,
            known_instance_id,
            move_id,
            modifier_count,
            asset: ctx.accounts.stamped_asset.key(),
            idempotency_key,
        });
        Ok(())
    }

    /// Atomically turns a profile-owned Known into a wallet-owned Core asset.
    /// Postgres is the canonical RAM ledger for this path, so the program only
    /// requires the wallet signature and the result-authority-issued ticket.
    pub fn stamp_profile_known_move(
        ctx: Context<StampProfileKnownMove>,
        idempotency_key: [u8; 32],
    ) -> Result<()> {
        let owner_key = ctx.accounts.owner.key();
        let now = Clock::get()?.unix_timestamp;
        let ticket = &ctx.accounts.ticket;

        require!(ticket.owner == owner_key, ChainboundError::InvalidAuthority);
        require!(
            ticket.idempotency_key == idempotency_key,
            ChainboundError::IdempotencyMismatch
        );
        require!(
            ticket.known_instance_id != 0,
            ChainboundError::InvalidKnownReference
        );
        require!(!ticket.consumed, ChainboundError::TicketAlreadyConsumed);
        require!(ticket.expires_at >= now, ChainboundError::TicketExpired);
        require!(
            ticket.modifier_count == 1 || ticket.modifier_count == 2,
            ChainboundError::InvalidModifierCount
        );
        require!(
            ticket.generator_version == ctx.accounts.ownership_config.generator_version
                && ticket.pool_hash == ctx.accounts.ownership_config.pool_hash,
            ChainboundError::StaleConfiguration
        );
        require!(
            !ticket.modifier_ids[0].is_empty()
                && (ticket.modifier_count == 1 || !ticket.modifier_ids[1].is_empty()),
            ChainboundError::InvalidModifier
        );
        if ticket.modifier_count == 2 {
            require!(
                ticket.modifier_ids[0] != ticket.modifier_ids[1],
                ChainboundError::DuplicateModifier
            );
        }

        validate_collection(
            &ctx.accounts.moves_collection,
            ctx.accounts.ownership_config.moves_collection,
            ctx.accounts.core_authority.key(),
        )?;

        let attributes = profile_stamped_attributes(&ctx.accounts.ownership_config, ticket);
        let known_instance_id = ticket.known_instance_id;
        let move_id = ticket.move_id;
        let modifier_count = ticket.modifier_count;
        ctx.accounts.ticket.consumed = true;
        ctx.accounts.ticket.stamped_asset = ctx.accounts.stamped_asset.key();

        let core_program_info = ctx.accounts.core_program.to_account_info();
        let stamped_asset_info = ctx.accounts.stamped_asset.to_account_info();
        let moves_collection_info = ctx.accounts.moves_collection.to_account_info();
        let core_authority_info = ctx.accounts.core_authority.to_account_info();
        let owner_info = ctx.accounts.owner.to_account_info();
        let system_program_info = ctx.accounts.system_program.to_account_info();
        let asset_bump = [ctx.bumps.stamped_asset];
        let authority_bump = [ctx.bumps.core_authority];
        let asset_seeds: &[&[u8]] = &[
            b"stamped_asset",
            owner_key.as_ref(),
            idempotency_key.as_ref(),
            &asset_bump,
        ];
        let authority_seeds: &[&[u8]] = &[b"core_authority", &authority_bump];
        let signer_seeds: &[&[&[u8]]] = &[asset_seeds, authority_seeds];

        CreateV2CpiBuilder::new(&core_program_info)
            .asset(&stamped_asset_info)
            .collection(Some(&moves_collection_info))
            .authority(Some(&core_authority_info))
            .payer(&owner_info)
            .owner(Some(&owner_info))
            .update_authority(None)
            .system_program(&system_program_info)
            .data_state(DataState::AccountState)
            .name(ctx.accounts.ticket.asset_name.clone())
            .uri(ctx.accounts.ticket.asset_uri.clone())
            .plugins(vec![PluginAuthorityPair {
                plugin: Plugin::Attributes(Attributes {
                    attribute_list: attributes,
                }),
                authority: Some(PluginAuthority::UpdateAuthority),
            }])
            .invoke_signed(signer_seeds)?;

        emit!(StampedMoveCreated {
            owner: owner_key,
            known_instance_id,
            move_id,
            modifier_count,
            asset: ctx.accounts.stamped_asset.key(),
            idempotency_key,
        });
        Ok(())
    }

    /// Publishes one Chainling Archive and atomically consumes its cover plus the selected
    /// Stamped move or cosmetic skin Core asset.
    pub fn bind_chainling(
        ctx: Context<BindChainling>,
        chainling_id: u64,
        idempotency_key: [u8; 32],
        component_kind: u8,
        archive_name: String,
        archive_uri: String,
    ) -> Result<()> {
        consume_ram_reservation(
            &mut ctx.accounts.reservation,
            ctx.accounts.owner.key(),
            idempotency_key,
            ctx.accounts.ownership_config.bind_ram_cost,
            RAM_RESERVATION_KIND_BIND,
            Clock::get()?.unix_timestamp,
        )?;
        require!(
            component_kind == COMPONENT_STAMPED_MOVE || component_kind == COMPONENT_COSMETIC,
            ChainboundError::InvalidComponentKind
        );
        require!(
            ctx.accounts.chainling.chainling_id == chainling_id,
            ChainboundError::ChainlingMismatch
        );
        require!(
            !ctx.accounts.chainling.is_bound,
            ChainboundError::AlreadyBound
        );
        require!(
            ctx.accounts.profile.ram_balance >= ctx.accounts.ownership_config.bind_ram_cost,
            ChainboundError::InsufficientRam
        );
        require!(
            ctx.accounts.cover.key() != ctx.accounts.component.key(),
            ChainboundError::DuplicateComponent
        );

        let owner_key = ctx.accounts.owner.key();
        let core_authority_key = ctx.accounts.core_authority.key();
        validate_collection(
            &ctx.accounts.cover_collection,
            ctx.accounts.ownership_config.covers_collection,
            core_authority_key,
        )?;
        validate_collection(
            &ctx.accounts.moves_collection,
            ctx.accounts.ownership_config.moves_collection,
            core_authority_key,
        )?;
        validate_collection(
            &ctx.accounts.cosmetics_collection,
            ctx.accounts.ownership_config.cosmetics_collection,
            core_authority_key,
        )?;
        validate_collection(
            &ctx.accounts.archives_collection,
            ctx.accounts.ownership_config.archives_collection,
            core_authority_key,
        )?;
        validate_asset_membership(
            &ctx.accounts.cover,
            owner_key,
            ctx.accounts.cover_collection.key(),
        )?;

        let expected_component_collection = if component_kind == COMPONENT_STAMPED_MOVE {
            ctx.accounts.moves_collection.key()
        } else {
            ctx.accounts.cosmetics_collection.key()
        };
        validate_asset_membership(
            &ctx.accounts.component,
            owner_key,
            expected_component_collection,
        )?;

        let archive_name = bounded_string(archive_name, MAX_ASSET_NAME_LEN)?;
        let archive_uri = bounded_string(archive_uri, MAX_ASSET_URI_LEN)?;
        let attributes = archive_attributes(
            &ctx.accounts.ownership_config,
            &ctx.accounts.chainling,
            ctx.accounts.cover.key(),
            ctx.accounts.component.key(),
            component_kind,
        );

        ctx.accounts.profile.ram_balance = ctx
            .accounts
            .profile
            .ram_balance
            .checked_sub(ctx.accounts.ownership_config.bind_ram_cost)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        ctx.accounts.chainling.is_bound = true;
        ctx.accounts.chainling.archive_asset = ctx.accounts.archive_asset.key();

        let receipt = &mut ctx.accounts.receipt;
        receipt.owner = owner_key;
        receipt.chainling_id = chainling_id;
        receipt.idempotency_key = idempotency_key;
        receipt.cover_asset = ctx.accounts.cover.key();
        receipt.component_asset = ctx.accounts.component.key();
        receipt.component_kind = component_kind;
        receipt.archive_asset = ctx.accounts.archive_asset.key();
        receipt.completed = true;
        receipt.bump = ctx.bumps.receipt;

        let core_program_info = ctx.accounts.core_program.to_account_info();
        let owner_info = ctx.accounts.owner.to_account_info();
        let system_program_info = ctx.accounts.system_program.to_account_info();
        let cover_info = ctx.accounts.cover.to_account_info();
        let component_info = ctx.accounts.component.to_account_info();
        let cover_collection_info = ctx.accounts.cover_collection.to_account_info();
        let component_collection_info = if component_kind == COMPONENT_STAMPED_MOVE {
            ctx.accounts.moves_collection.to_account_info()
        } else {
            ctx.accounts.cosmetics_collection.to_account_info()
        };

        BurnV1CpiBuilder::new(&core_program_info)
            .asset(&cover_info)
            .collection(Some(&cover_collection_info))
            .payer(&owner_info)
            .authority(Some(&owner_info))
            .system_program(Some(&system_program_info))
            .invoke()?;
        BurnV1CpiBuilder::new(&core_program_info)
            .asset(&component_info)
            .collection(Some(&component_collection_info))
            .payer(&owner_info)
            .authority(Some(&owner_info))
            .system_program(Some(&system_program_info))
            .invoke()?;

        let archive_asset_info = ctx.accounts.archive_asset.to_account_info();
        let archives_collection_info = ctx.accounts.archives_collection.to_account_info();
        let core_authority_info = ctx.accounts.core_authority.to_account_info();
        let chainling_id_bytes = chainling_id.to_le_bytes();
        let asset_bump = [ctx.bumps.archive_asset];
        let authority_bump = [ctx.bumps.core_authority];
        let asset_seeds: &[&[u8]] = &[
            b"archive_asset",
            owner_key.as_ref(),
            chainling_id_bytes.as_ref(),
            &asset_bump,
        ];
        let authority_seeds: &[&[u8]] = &[b"core_authority", &authority_bump];
        let signer_seeds: &[&[&[u8]]] = &[asset_seeds, authority_seeds];

        CreateV2CpiBuilder::new(&core_program_info)
            .asset(&archive_asset_info)
            .collection(Some(&archives_collection_info))
            .authority(Some(&core_authority_info))
            .payer(&owner_info)
            .owner(Some(&owner_info))
            .update_authority(Some(&core_authority_info))
            .system_program(&system_program_info)
            .data_state(DataState::AccountState)
            .name(archive_name)
            .uri(archive_uri)
            .plugins(vec![PluginAuthorityPair {
                plugin: Plugin::Attributes(Attributes {
                    attribute_list: attributes,
                }),
                authority: Some(PluginAuthority::UpdateAuthority),
            }])
            .invoke_signed(signer_seeds)?;

        emit!(ChainlingBound {
            owner: owner_key,
            chainling_id,
            archive_asset: ctx.accounts.archive_asset.key(),
            cover_asset: ctx.accounts.cover.key(),
            component_asset: ctx.accounts.component.key(),
            component_kind,
            idempotency_key,
        });
        Ok(())
    }

    /// Requests base-layer MagicBlock VRF randomness for one bounded request seed.
    pub fn request_encounter(ctx: Context<RequestEncounter>, request_seed: [u8; 32]) -> Result<()> {
        let request = &mut ctx.accounts.encounter_request;
        request.status = ENCOUNTER_PENDING;
        request.randomness = [0; 32];
        request.owner = ctx.accounts.payer.key();
        request.request_seed = request_seed;
        request.requested_at_slot = Clock::get()?.slot;
        request.fulfilled_at_slot = 0;
        request.bump = ctx.bumps.encounter_request;

        let ix = create_request_scoped_randomness_ix(RequestRandomnessParams {
            payer: ctx.accounts.payer.key(),
            oracle_queue: ctx.accounts.oracle_queue.key(),
            callback_program_id: crate::ID,
            callback_discriminator: FulfillEncounterInstruction::DISCRIMINATOR.to_vec(),
            caller_seed: request_seed,
            accounts_metas: Some(vec![SerializableAccountMeta {
                pubkey: ctx.accounts.encounter_request.key(),
                is_signer: false,
                is_writable: true,
            }]),
            ..Default::default()
        });
        ctx.accounts
            .invoke_signed_vrf(&ctx.accounts.payer.to_account_info(), &ix)?;
        Ok(())
    }

    /// MagicBlock callback. The scoped identity injected by `vrf_callback` proves the caller.
    pub fn fulfill_encounter(ctx: Context<FulfillEncounter>, randomness: [u8; 32]) -> Result<()> {
        ctx.accounts
            .encounter_request
            .fulfill(randomness, Clock::get()?.slot)
    }

    /// Marks fulfilled bytes consumed exactly once after the game has read them.
    pub fn consume_encounter_randomness(ctx: Context<ConsumeEncounterRandomness>) -> Result<()> {
        ctx.accounts.encounter_request.consume()
    }

    /// Returns a consumed encounter request's rent to the player who paid it, after the
    /// retention window. Anyone may send it (for example a server sweep); the rent can only go
    /// to the request's owner.
    pub fn close_encounter_request(ctx: Context<CloseEncounterRequest>) -> Result<()> {
        ctx.accounts
            .encounter_request
            .ensure_closable(Clock::get()?.slot)
    }

    /// Creates the published tier odds for one roll kind. Thresholds are cumulative parts per
    /// million, Common..Mythic.
    pub fn initialize_roll_odds(
        ctx: Context<InitializeRollOdds>,
        kind: u8,
        cumulative_ppm: [u32; ROLL_TIER_COUNT],
        pool_hash: [u8; 32],
    ) -> Result<()> {
        validate_roll_kind(kind)?;
        validate_cumulative_ppm(&cumulative_ppm)?;
        let odds = &mut ctx.accounts.roll_odds;
        odds.kind = kind;
        odds.odds_version = 1;
        odds.cumulative_ppm = cumulative_ppm;
        odds.pool_hash = pool_hash;
        odds.bump = ctx.bumps.roll_odds;
        emit!(RollOddsChanged {
            kind,
            odds_version: odds.odds_version,
            cumulative_ppm,
            pool_hash,
        });
        Ok(())
    }

    /// Replaces the odds for new requests. Pending requests keep the snapshot they were made with.
    pub fn update_roll_odds(
        ctx: Context<UpdateRollOdds>,
        cumulative_ppm: [u32; ROLL_TIER_COUNT],
        pool_hash: [u8; 32],
    ) -> Result<()> {
        validate_cumulative_ppm(&cumulative_ppm)?;
        let odds = &mut ctx.accounts.roll_odds;
        odds.odds_version = odds
            .odds_version
            .checked_add(1)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        odds.cumulative_ppm = cumulative_ppm;
        odds.pool_hash = pool_hash;
        emit!(RollOddsChanged {
            kind: odds.kind,
            odds_version: odds.odds_version,
            cumulative_ppm,
            pool_hash,
        });
        Ok(())
    }

    /// Requests VRF bytes for one paid operation (Known roll or Stamp). One PDA per operation, so
    /// a second request for the same operation fails at init.
    pub fn request_roll(
        ctx: Context<RequestRoll>,
        kind: u8,
        operation_hash: [u8; 32],
        subject_hash: [u8; 32],
    ) -> Result<()> {
        validate_roll_kind(kind)?;
        let odds = &ctx.accounts.roll_odds;
        let request = &mut ctx.accounts.roll_request;
        request.status = ROLL_PENDING;
        request.tier = ROLL_TIER_UNSET;
        request.randomness = [0; 32];
        request.kind = kind;
        request.operation_hash = operation_hash;
        request.subject_hash = subject_hash;
        request.odds_version = odds.odds_version;
        request.cumulative_ppm = odds.cumulative_ppm;
        request.pool_hash = odds.pool_hash;
        request.requested_at_slot = Clock::get()?.slot;
        request.fulfilled_at_slot = 0;
        request.consumed_at_slot = 0;
        request.result_hash = [0; 32];
        request.bump = ctx.bumps.roll_request;

        let caller_seed = roll_caller_seed(
            kind,
            &operation_hash,
            &subject_hash,
            request.odds_version,
            &request.pool_hash,
        );
        emit!(RollRequested {
            kind,
            operation_hash,
            subject_hash,
            odds_version: request.odds_version,
            caller_seed,
        });

        let ix = create_request_scoped_randomness_ix(RequestRandomnessParams {
            payer: ctx.accounts.result_authority.key(),
            oracle_queue: ctx.accounts.oracle_queue.key(),
            callback_program_id: crate::ID,
            callback_discriminator: FulfillRollInstruction::DISCRIMINATOR.to_vec(),
            caller_seed,
            accounts_metas: Some(vec![SerializableAccountMeta {
                pubkey: ctx.accounts.roll_request.key(),
                is_signer: false,
                is_writable: true,
            }]),
            ..Default::default()
        });
        ctx.accounts
            .invoke_signed_vrf(&ctx.accounts.result_authority.to_account_info(), &ix)?;
        Ok(())
    }

    /// MagicBlock callback. Stores the bytes and draws the rarity tier on-chain from the odds
    /// snapshot taken at request time.
    pub fn fulfill_roll(ctx: Context<FulfillRoll>, randomness: [u8; 32]) -> Result<()> {
        let request = &mut ctx.accounts.roll_request;
        request.fulfill(randomness, Clock::get()?.slot)?;
        emit!(RollFulfilled {
            kind: request.kind,
            operation_hash: request.operation_hash,
            tier: request.tier,
            odds_version: request.odds_version,
        });
        Ok(())
    }

    /// Records the hash of the settled result exactly once.
    pub fn consume_roll(ctx: Context<ConsumeRoll>, result_hash: [u8; 32]) -> Result<()> {
        let request = &mut ctx.accounts.roll_request;
        request.consume(result_hash, Clock::get()?.slot)?;
        emit!(RollConsumed {
            kind: request.kind,
            operation_hash: request.operation_hash,
            tier: request.tier,
            result_hash,
        });
        Ok(())
    }

    /// Cancels a request the oracle never fulfilled. Only a Pending request qualifies, so a
    /// cancel can never discard randomness that was already drawn; a late callback then fails on
    /// the status check. The server retries under a new operation hash or releases the RAM.
    pub fn cancel_roll(ctx: Context<CancelRoll>) -> Result<()> {
        let request = &mut ctx.accounts.roll_request;
        request.cancel(Clock::get()?.slot)?;
        emit!(RollCancelled {
            kind: request.kind,
            operation_hash: request.operation_hash,
            requested_at_slot: request.requested_at_slot,
            cancelled_at_slot: request.consumed_at_slot,
        });
        Ok(())
    }

    /// Returns a consumed or cancelled request's rent once the retention window has passed.
    /// Events keep the audit trail.
    pub fn close_roll(ctx: Context<CloseRoll>) -> Result<()> {
        ctx.accounts
            .roll_request
            .ensure_closable(Clock::get()?.slot)
    }

    pub fn apply_xp_and_maybe_level_up(
        _ctx: Context<ResultAuthorityOnly>,
        _chainling_instance_id: u32,
        _xp_delta: u32,
    ) -> Result<()> {
        err!(ChainboundError::ContractNotImplemented)
    }
}

#[derive(Accounts)]
pub struct Initialize<'info> {
    #[account(
        init,
        payer = program_authority,
        space = 8 + GameConfig::LEN,
        seeds = [b"game_config"],
        bump
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub program_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct UpdateGameConfig<'info> {
    #[account(
        mut,
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    pub program_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct ProposeProgramAuthority<'info> {
    #[account(
        init,
        payer = program_authority,
        space = 8 + AuthorityTransfer::LEN,
        seeds = [b"authority_transfer"],
        bump
    )]
    pub authority_transfer: Account<'info, AuthorityTransfer>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub program_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct AcceptProgramAuthority<'info> {
    #[account(
        mut,
        close = current_authority,
        seeds = [b"authority_transfer"],
        bump = authority_transfer.bump,
        constraint = authority_transfer.pending_authority == pending_authority.key()
            @ ChainboundError::InvalidAuthorityTransfer,
        constraint = authority_transfer.current_authority == game_config.program_authority
            @ ChainboundError::InvalidAuthorityTransfer
    )]
    pub authority_transfer: Account<'info, AuthorityTransfer>,
    #[account(mut, seeds = [b"game_config"], bump = game_config.bump)]
    pub game_config: Account<'info, GameConfig>,
    pub pending_authority: Signer<'info>,
    /// CHECK: Only receives the proposal's rent; pinned to the authority that paid it.
    #[account(mut, address = authority_transfer.current_authority @ ChainboundError::InvalidAuthorityTransfer)]
    pub current_authority: UncheckedAccount<'info>,
}

#[derive(Accounts)]
pub struct CancelProgramAuthorityTransfer<'info> {
    #[account(
        mut,
        close = program_authority,
        seeds = [b"authority_transfer"],
        bump = authority_transfer.bump
    )]
    pub authority_transfer: Account<'info, AuthorityTransfer>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub program_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct CreateProfile<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + PlayerProfile::LEN,
        seeds = [b"player_profile", authority.key().as_ref()],
        bump
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(mut)]
    pub authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ProfileMutation<'info> {
    #[account(
        mut,
        seeds = [b"player_profile", authority.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(mut)]
    pub authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct AuthorityProfileMutation<'info> {
    #[account(
        mut,
        seeds = [b"player_profile", profile.owner.as_ref()],
        bump = profile.bump
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct GrantRam<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + RamGrantReceipt::LEN,
        seeds = [b"ram_grant_receipt", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub receipt: Account<'info, RamGrantReceipt>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    /// CHECK: Bound to the validated PlayerProfile owner and receipt PDA seed.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(operation_hash: [u8; 32])]
pub struct CreateRamReservation<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + RamReservation::LEN,
        seeds = [b"ram_reservation", owner.key().as_ref(), operation_hash.as_ref()],
        bump
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    /// CHECK: Bound to the validated PlayerProfile and reservation PDA seed.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(operation_hash: [u8; 32])]
pub struct CancelRamReservation<'info> {
    #[account(
        mut,
        close = result_authority,
        seeds = [b"ram_reservation", reservation.owner.as_ref(), operation_hash.as_ref()],
        bump = reservation.bump,
        constraint = reservation.result_authority == result_authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(order_hash: [u8; 32])]
pub struct CreditPurchasedRam<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + RamPurchaseReceipt::LEN,
        seeds = [b"ram_purchase_receipt", owner.key().as_ref(), order_hash.as_ref()],
        bump
    )]
    pub receipt: Account<'info, RamPurchaseReceipt>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    /// CHECK: Bound to the validated PlayerProfile and receipt PDA seed.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(operation_hash: [u8; 32])]
pub struct DebitReservedRam<'info> {
    #[account(
        mut,
        seeds = [b"ram_reservation", owner.key().as_ref(), operation_hash.as_ref()],
        bump = reservation.bump,
        constraint = reservation.owner == owner.key() @ ChainboundError::InvalidAuthority,
        constraint = reservation.result_authority == result_authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        init,
        payer = result_authority,
        space = 8 + RamDebitReceipt::LEN,
        seeds = [b"ram_debit_receipt", owner.key().as_ref(), operation_hash.as_ref()],
        bump
    )]
    pub receipt: Account<'info, RamDebitReceipt>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    /// CHECK: Bound to the validated PlayerProfile and operation PDA seeds.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(case_hash: [u8; 32])]
pub struct ReconcileRamDelta<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + RamReconciliationReceipt::LEN,
        seeds = [b"ram_reconcile_receipt", owner.key().as_ref(), case_hash.as_ref()],
        bump
    )]
    pub receipt: Account<'info, RamReconciliationReceipt>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    /// CHECK: Bound to the validated PlayerProfile and receipt PDA seed.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct CreateMoveVault<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + MoveVault::LEN,
        seeds = [b"move_vault", authority.key().as_ref()],
        bump
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(mut)]
    pub authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct MoveVaultMutation<'info> {
    #[account(
        mut,
        seeds = [b"move_vault", authority.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(mut)]
    pub authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct RollKnownMove<'info> {
    #[account(
        mut,
        seeds = [b"ram_reservation", owner.key().as_ref(), idempotency_key.as_ref()],
        bump = reservation.bump,
        constraint = reservation.owner == owner.key() @ ChainboundError::InvalidAuthority,
        constraint = reservation.result_authority == result_authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        mut,
        seeds = [b"move_vault", owner.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    /// CHECK: Bound to the vault owner and receipt seed. The wallet intentionally does not sign.
    pub owner: UncheckedAccount<'info>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    #[account(
        init,
        payer = result_authority,
        space = 8 + KnownRollReceipt::LEN,
        seeds = [b"known_roll_receipt", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub receipt: Account<'info, KnownRollReceipt>,
    #[account(seeds = [b"game_config"], bump = game_config.bump, has_one = result_authority @ ChainboundError::InvalidAuthority)]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[vrf]
#[derive(Accounts)]
#[instruction(request_seed: [u8; 32])]
pub struct RequestEncounter<'info> {
    #[account(
        init,
        payer = payer,
        space = 8 + EncounterRequest::LEN,
        seeds = [b"encounter_request", payer.key().as_ref(), request_seed.as_ref()],
        bump
    )]
    pub encounter_request: Account<'info, EncounterRequest>,
    #[account(mut)]
    pub payer: Signer<'info>,
    /// CHECK: Pinned to MagicBlock's base-layer queue for Solana Devnet/mainnet.
    #[account(mut, address = DEFAULT_QUEUE)]
    pub oracle_queue: AccountInfo<'info>,
}

#[vrf_callback]
#[derive(Accounts)]
pub struct FulfillEncounter<'info> {
    #[account(mut)]
    pub encounter_request: Account<'info, EncounterRequest>,
}

#[derive(Accounts)]
pub struct ConsumeEncounterRandomness<'info> {
    #[account(
        mut,
        seeds = [b"encounter_request", owner.key().as_ref(), encounter_request.request_seed.as_ref()],
        bump = encounter_request.bump,
        has_one = owner @ ChainboundError::InvalidAuthority
    )]
    pub encounter_request: Account<'info, EncounterRequest>,
    pub owner: Signer<'info>,
}

#[derive(Accounts)]
pub struct CloseEncounterRequest<'info> {
    #[account(
        mut,
        close = owner,
        seeds = [b"encounter_request", owner.key().as_ref(), encounter_request.request_seed.as_ref()],
        bump = encounter_request.bump,
        has_one = owner @ ChainboundError::InvalidAuthority
    )]
    pub encounter_request: Account<'info, EncounterRequest>,
    /// CHECK: Pinned by `has_one` and the PDA seeds; it only receives the rent it paid.
    #[account(mut)]
    pub owner: UncheckedAccount<'info>,
}

#[derive(Accounts)]
#[instruction(kind: u8)]
pub struct InitializeRollOdds<'info> {
    #[account(
        init,
        payer = program_authority,
        space = 8 + RollOdds::LEN,
        seeds = [b"roll_odds", kind.to_le_bytes().as_ref()],
        bump
    )]
    pub roll_odds: Account<'info, RollOdds>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub program_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct UpdateRollOdds<'info> {
    #[account(
        mut,
        seeds = [b"roll_odds", roll_odds.kind.to_le_bytes().as_ref()],
        bump = roll_odds.bump
    )]
    pub roll_odds: Account<'info, RollOdds>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    pub program_authority: Signer<'info>,
}

#[vrf]
#[derive(Accounts)]
#[instruction(kind: u8, operation_hash: [u8; 32])]
pub struct RequestRoll<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + RollRequest::LEN,
        seeds = [b"roll_request", kind.to_le_bytes().as_ref(), operation_hash.as_ref()],
        bump
    )]
    pub roll_request: Account<'info, RollRequest>,
    #[account(
        seeds = [b"roll_odds", kind.to_le_bytes().as_ref()],
        bump = roll_odds.bump
    )]
    pub roll_odds: Account<'info, RollOdds>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    /// CHECK: Pinned to MagicBlock's base-layer queue for Solana Devnet/mainnet.
    #[account(mut, address = DEFAULT_QUEUE)]
    pub oracle_queue: AccountInfo<'info>,
}

#[vrf_callback]
#[derive(Accounts)]
pub struct FulfillRoll<'info> {
    #[account(mut)]
    pub roll_request: Account<'info, RollRequest>,
}

#[derive(Accounts)]
pub struct ConsumeRoll<'info> {
    #[account(
        mut,
        seeds = [
            b"roll_request",
            roll_request.kind.to_le_bytes().as_ref(),
            roll_request.operation_hash.as_ref()
        ],
        bump = roll_request.bump
    )]
    pub roll_request: Account<'info, RollRequest>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct CancelRoll<'info> {
    #[account(
        mut,
        seeds = [
            b"roll_request",
            roll_request.kind.to_le_bytes().as_ref(),
            roll_request.operation_hash.as_ref()
        ],
        bump = roll_request.bump
    )]
    pub roll_request: Account<'info, RollRequest>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct CloseRoll<'info> {
    #[account(
        mut,
        close = result_authority,
        seeds = [
            b"roll_request",
            roll_request.kind.to_le_bytes().as_ref(),
            roll_request.operation_hash.as_ref()
        ],
        bump = roll_request.bump
    )]
    pub roll_request: Account<'info, RollRequest>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct BurnKnownMoveForRam<'info> {
    #[account(
        mut,
        seeds = [b"move_vault", authority.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(
        mut,
        seeds = [b"player_profile", authority.key().as_ref()],
        bump = profile.bump
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(mut)]
    pub authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct AuthorityBurnKnownMoveForRam<'info> {
    #[account(mut, seeds = [b"move_vault", vault.owner.as_ref()], bump = vault.bump)]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(mut, seeds = [b"player_profile", vault.owner.as_ref()], bump = profile.bump)]
    pub profile: Account<'info, PlayerProfile>,
    #[account(seeds = [b"game_config"], bump = config.bump, has_one = result_authority @ ChainboundError::InvalidAuthority)]
    pub config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct CreateChainlingVault<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + ChainlingVault::LEN,
        seeds = [b"chainling_vault", authority.key().as_ref()],
        bump
    )]
    pub vault: Account<'info, ChainlingVault>,
    #[account(mut)]
    pub authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ChainlingVaultMutation<'info> {
    #[account(
        mut,
        seeds = [b"chainling_vault", authority.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Account<'info, ChainlingVault>,
    #[account(mut)]
    pub authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct AuthorityChainlingVaultMutation<'info> {
    #[account(mut, seeds = [b"chainling_vault", vault.owner.as_ref()], bump = vault.bump)]
    pub vault: Account<'info, ChainlingVault>,
    #[account(seeds = [b"game_config"], bump = config.bump, has_one = result_authority @ ChainboundError::InvalidAuthority)]
    pub config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(chainling_instance_id: u32)]
pub struct CreateEquippedRegistry<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + EquippedMoveRegistry::LEN,
        seeds = [b"equipped_moves", authority.key().as_ref(), &chainling_instance_id.to_le_bytes()],
        bump
    )]
    pub registry: Account<'info, EquippedMoveRegistry>,
    #[account(mut)]
    pub authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct EquippedRegistryMutation<'info> {
    #[account(
        mut,
        seeds = [b"equipped_moves", authority.key().as_ref(), &registry.chainling_instance_id.to_le_bytes()],
        bump = registry.bump,
        constraint = registry.owner == authority.key() @ ChainboundError::InvalidAuthority
    )]
    pub registry: Account<'info, EquippedMoveRegistry>,
    #[account(mut)]
    pub authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct AuthorityEquippedRegistryMutation<'info> {
    #[account(
        mut,
        seeds = [b"equipped_moves", registry.owner.as_ref(), &registry.chainling_instance_id.to_le_bytes()],
        bump = registry.bump
    )]
    pub registry: Account<'info, EquippedMoveRegistry>,
    #[account(seeds = [b"game_config"], bump = config.bump, has_one = result_authority @ ChainboundError::InvalidAuthority)]
    pub config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(chainling_id: u64)]
pub struct CreateChainlingAccount<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + ChainlingAccount::LEN,
        seeds = [b"chainling_account", authority.key().as_ref(), &chainling_id.to_le_bytes()],
        bump
    )]
    pub chainling: Account<'info, ChainlingAccount>,
    #[account(mut)]
    pub authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(chainling_id: u64)]
pub struct AuthorityCreateChainlingAccount<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + ChainlingAccount::LEN,
        seeds = [b"chainling_account", player.key().as_ref(), &chainling_id.to_le_bytes()],
        bump
    )]
    pub chainling: Account<'info, ChainlingAccount>,
    /// CHECK: Address is used as the owner and PDA seed; it need not sign a server-issued result.
    pub player: UncheckedAccount<'info>,
    #[account(seeds = [b"game_config"], bump = config.bump, has_one = result_authority @ ChainboundError::InvalidAuthority)]
    pub config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct InitializeOwnershipConfig<'info> {
    #[account(
        init,
        payer = program_authority,
        space = 8 + OwnershipConfig::LEN,
        seeds = [b"ownership_config"],
        bump
    )]
    pub ownership_config: Account<'info, OwnershipConfig>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub program_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct UpdateOwnershipConfig<'info> {
    #[account(mut, seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = program_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    pub program_authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct PrepareStampTicket<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + StampTicket::LEN,
        seeds = [b"stamp_ticket", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub ticket: Account<'info, StampTicket>,
    #[account(
        seeds = [b"move_vault", owner.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    /// CHECK: The address is validated against the MoveVault owner and becomes the ticket owner.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct PrepareProfileStampTicket<'info> {
    #[account(
        init,
        payer = result_authority,
        space = 8 + StampTicket::LEN,
        seeds = [b"stamp_ticket", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub ticket: Account<'info, StampTicket>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    /// CHECK: The authenticated backend binds this address to the profile's active wallet.
    pub owner: UncheckedAccount<'info>,
    #[account(
        seeds = [b"game_config"],
        bump = game_config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub game_config: Account<'info, GameConfig>,
    #[account(mut)]
    pub result_authority: Signer<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct StampKnownMove<'info> {
    #[account(mut)]
    pub owner: Signer<'info>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(
        mut,
        seeds = [b"ram_reservation", owner.key().as_ref(), idempotency_key.as_ref()],
        bump = reservation.bump,
        constraint = reservation.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        mut,
        seeds = [b"move_vault", owner.key().as_ref()],
        bump = vault.bump,
        constraint = vault.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub vault: Box<Account<'info, MoveVault>>,
    #[account(
        mut,
        seeds = [b"stamp_ticket", owner.key().as_ref(), idempotency_key.as_ref()],
        bump = ticket.bump,
        constraint = ticket.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub ticket: Account<'info, StampTicket>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    /// CHECK: The key and signer authority are enforced by this program's PDA seeds.
    #[account(seeds = [b"core_authority"], bump)]
    pub core_authority: UncheckedAccount<'info>,
    /// CHECK: Metaplex Core creates this PDA; it must be empty before the CPI.
    #[account(
        mut,
        seeds = [b"stamped_asset", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub stamped_asset: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub moves_collection: UncheckedAccount<'info>,
    /// CHECK: Fixed address prevents arbitrary CPI substitution.
    #[account(address = mpl_core::ID)]
    pub core_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(idempotency_key: [u8; 32])]
pub struct StampProfileKnownMove<'info> {
    #[account(mut)]
    pub owner: Signer<'info>,
    #[account(
        mut,
        seeds = [b"stamp_ticket", owner.key().as_ref(), idempotency_key.as_ref()],
        bump = ticket.bump,
        constraint = ticket.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub ticket: Account<'info, StampTicket>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    /// CHECK: The key and signer authority are enforced by this program's PDA seeds.
    #[account(seeds = [b"core_authority"], bump)]
    pub core_authority: UncheckedAccount<'info>,
    /// CHECK: Metaplex Core creates this PDA; it must be empty before the CPI.
    #[account(
        mut,
        seeds = [b"stamped_asset", owner.key().as_ref(), idempotency_key.as_ref()],
        bump
    )]
    pub stamped_asset: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub moves_collection: UncheckedAccount<'info>,
    /// CHECK: Fixed address prevents arbitrary CPI substitution.
    #[account(address = mpl_core::ID)]
    pub core_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(chainling_id: u64, idempotency_key: [u8; 32])]
pub struct BindChainling<'info> {
    #[account(mut)]
    pub owner: Signer<'info>,
    #[account(
        mut,
        seeds = [b"player_profile", owner.key().as_ref()],
        bump = profile.bump,
        constraint = profile.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub profile: Account<'info, PlayerProfile>,
    #[account(
        mut,
        seeds = [b"ram_reservation", owner.key().as_ref(), idempotency_key.as_ref()],
        bump = reservation.bump,
        constraint = reservation.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub reservation: Account<'info, RamReservation>,
    #[account(
        mut,
        seeds = [b"chainling_account", owner.key().as_ref(), &chainling_id.to_le_bytes()],
        bump = chainling.bump,
        constraint = chainling.owner == owner.key() @ ChainboundError::InvalidAuthority
    )]
    pub chainling: Account<'info, ChainlingAccount>,
    #[account(seeds = [b"ownership_config"], bump = ownership_config.bump)]
    pub ownership_config: Account<'info, OwnershipConfig>,
    /// CHECK: The key and signer authority are enforced by this program's PDA seeds.
    #[account(seeds = [b"core_authority"], bump)]
    pub core_authority: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; asset data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub cover: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; asset data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub component: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub cover_collection: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub moves_collection: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub cosmetics_collection: UncheckedAccount<'info>,
    /// CHECK: Owner is constrained to Metaplex Core; collection data is parsed and validated.
    #[account(mut, owner = mpl_core::ID)]
    pub archives_collection: UncheckedAccount<'info>,
    /// CHECK: Metaplex Core creates this PDA; it must be empty before the CPI.
    #[account(
        mut,
        seeds = [b"archive_asset", owner.key().as_ref(), &chainling_id.to_le_bytes()],
        bump
    )]
    pub archive_asset: UncheckedAccount<'info>,
    #[account(
        init,
        payer = owner,
        space = 8 + BindReceipt::LEN,
        seeds = [b"bind_receipt", owner.key().as_ref(), &chainling_id.to_le_bytes()],
        bump
    )]
    pub receipt: Account<'info, BindReceipt>,
    /// CHECK: Fixed address prevents arbitrary CPI substitution.
    #[account(address = mpl_core::ID)]
    pub core_program: UncheckedAccount<'info>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct ResultAuthorityOnly<'info> {
    #[account(
        seeds = [b"game_config"],
        bump = config.bump,
        has_one = result_authority @ ChainboundError::InvalidAuthority
    )]
    pub config: Account<'info, GameConfig>,
    pub result_authority: Signer<'info>,
}

#[account]
pub struct GameConfig {
    pub program_authority: Pubkey,
    pub result_authority: Pubkey,
    pub schema_version: u16,
    pub ram_daily_cap: u16,
    pub ram_per_burn: u32,
    pub instruction_flags: u64,
    pub bump: u8,
}

impl GameConfig {
    pub const LEN: usize = 32 + 32 + 2 + 2 + 4 + 8 + 1;
}

/// A pending `program_authority` handoff. Kept outside `GameConfig` so the config layout and
/// size never change.
#[account]
pub struct AuthorityTransfer {
    pub current_authority: Pubkey,
    pub pending_authority: Pubkey,
    pub proposed_at_slot: u64,
    pub bump: u8,
}

impl AuthorityTransfer {
    pub const LEN: usize = 32 + 32 + 8 + 1;
}

fn validate_authority_proposal(current: &Pubkey, proposed: &Pubkey) -> Result<()> {
    require!(
        *proposed != Pubkey::default() && proposed != current,
        ChainboundError::InvalidAuthorityTransfer
    );
    Ok(())
}

#[account]
pub struct PlayerProfile {
    pub owner: Pubkey,
    pub schema_version: u16,
    pub created_at_slot: u64,
    pub wins: u32,
    pub losses: u32,
    pub last_known_scene_id: u32,
    pub last_known_pos_x: i16,
    pub last_known_pos_y: i16,
    pub last_known_pos_z: i16,
    pub ram_balance: u32,
    pub daily_ram_earned: u16,
    pub last_ram_reset_timestamp: i64,
    pub ram_lifetime_earned: u64,
    pub bump: u8,
}

impl PlayerProfile {
    pub const LEN: usize = 32 + 2 + 8 + 4 + 4 + 4 + 2 + 2 + 2 + 4 + 2 + 8 + 8 + 1;
}

#[account]
pub struct RamGrantReceipt {
    pub owner: Pubkey,
    pub idempotency_key: [u8; 32],
    pub amount: u32,
    pub balance_after: u32,
    pub utc_day: i64,
    pub result_authority: Pubkey,
    pub bump: u8,
}

impl RamGrantReceipt {
    pub const LEN: usize = 32 + 32 + 4 + 4 + 8 + 32 + 1;
}

#[account]
pub struct RamReservation {
    pub owner: Pubkey,
    pub operation_hash: [u8; 32],
    pub amount: u32,
    pub kind: u8,
    pub expires_at: i64,
    pub consumed: bool,
    pub result_authority: Pubkey,
    pub bump: u8,
}

impl RamReservation {
    pub const LEN: usize = 32 + 32 + 4 + 1 + 8 + 1 + 32 + 1;
}

#[account]
pub struct RamPurchaseReceipt {
    pub owner: Pubkey,
    pub order_hash: [u8; 32],
    pub amount: u32,
    pub balance_before: u32,
    pub balance_after: u32,
    pub result_authority: Pubkey,
    pub bump: u8,
}

impl RamPurchaseReceipt {
    pub const LEN: usize = 32 + 32 + 4 + 4 + 4 + 32 + 1;
}

#[account]
pub struct RamDebitReceipt {
    pub owner: Pubkey,
    pub operation_hash: [u8; 32],
    pub amount: u32,
    pub kind: u8,
    pub balance_before: u32,
    pub balance_after: u32,
    pub result_authority: Pubkey,
    pub bump: u8,
}

impl RamDebitReceipt {
    pub const LEN: usize = 32 + 32 + 4 + 1 + 4 + 4 + 32 + 1;
}

#[account]
pub struct RamReconciliationReceipt {
    pub owner: Pubkey,
    pub case_hash: [u8; 32],
    pub direction: u8,
    pub amount: u32,
    pub balance_before: u32,
    pub balance_after: u32,
    pub result_authority: Pubkey,
    pub bump: u8,
}

impl RamReconciliationReceipt {
    pub const LEN: usize = 32 + 32 + 1 + 4 + 4 + 4 + 32 + 1;
}

#[account]
pub struct EncounterRequest {
    // Keep status/randomness first so Unity can poll the compact prefix without decoding the
    // remainder of the Anchor account.
    pub status: u8,
    pub randomness: [u8; 32],
    pub owner: Pubkey,
    pub request_seed: [u8; 32],
    pub requested_at_slot: u64,
    pub fulfilled_at_slot: u64,
    pub bump: u8,
}

impl EncounterRequest {
    pub const LEN: usize = 1 + 32 + 32 + 32 + 8 + 8 + 1;

    fn fulfill(&mut self, randomness: [u8; 32], fulfilled_at_slot: u64) -> Result<()> {
        require!(
            self.status == ENCOUNTER_PENDING,
            ChainboundError::InvalidEncounterStatus
        );
        self.randomness = randomness;
        self.fulfilled_at_slot = fulfilled_at_slot;
        self.status = ENCOUNTER_FULFILLED;
        Ok(())
    }

    fn consume(&mut self) -> Result<()> {
        require!(
            self.status == ENCOUNTER_FULFILLED,
            ChainboundError::InvalidEncounterStatus
        );
        self.status = ENCOUNTER_CONSUMED;
        Ok(())
    }

    /// The layout has no consume slot, so retention counts from fulfillment.
    fn ensure_closable(&self, current_slot: u64) -> Result<()> {
        require!(
            self.status == ENCOUNTER_CONSUMED,
            ChainboundError::InvalidEncounterStatus
        );
        let open_at = self
            .fulfilled_at_slot
            .checked_add(ENCOUNTER_CLOSE_DELAY_SLOTS)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        require!(
            current_slot >= open_at,
            ChainboundError::EncounterCloseTooEarly
        );
        Ok(())
    }
}

#[account]
pub struct RollOdds {
    pub kind: u8,
    pub odds_version: u32,
    pub cumulative_ppm: [u32; ROLL_TIER_COUNT],
    pub pool_hash: [u8; 32],
    pub bump: u8,
}

impl RollOdds {
    pub const LEN: usize = 1 + 4 + (4 * ROLL_TIER_COUNT) + 32 + 1;
}

#[account]
pub struct RollRequest {
    // Status, tier and randomness first so clients can poll a compact prefix.
    pub status: u8,
    pub tier: u8,
    pub randomness: [u8; 32],
    pub kind: u8,
    pub operation_hash: [u8; 32],
    pub subject_hash: [u8; 32],
    pub odds_version: u32,
    pub cumulative_ppm: [u32; ROLL_TIER_COUNT],
    pub pool_hash: [u8; 32],
    pub requested_at_slot: u64,
    pub fulfilled_at_slot: u64,
    pub consumed_at_slot: u64,
    pub result_hash: [u8; 32],
    pub bump: u8,
}

impl RollRequest {
    pub const LEN: usize =
        1 + 1 + 32 + 1 + 32 + 32 + 4 + (4 * ROLL_TIER_COUNT) + 32 + 8 + 8 + 8 + 32 + 1;

    fn fulfill(&mut self, randomness: [u8; 32], fulfilled_at_slot: u64) -> Result<()> {
        require!(
            self.status == ROLL_PENDING,
            ChainboundError::InvalidRollStatus
        );
        self.randomness = randomness;
        self.tier = roll_tier(&randomness, &self.cumulative_ppm)?;
        self.fulfilled_at_slot = fulfilled_at_slot;
        self.status = ROLL_FULFILLED;
        Ok(())
    }

    fn consume(&mut self, result_hash: [u8; 32], consumed_at_slot: u64) -> Result<()> {
        require!(
            self.status == ROLL_FULFILLED,
            ChainboundError::InvalidRollStatus
        );
        require!(result_hash != [0; 32], ChainboundError::InvalidRollResult);
        self.result_hash = result_hash;
        self.consumed_at_slot = consumed_at_slot;
        self.status = ROLL_CONSUMED;
        Ok(())
    }

    /// Records the cancel slot in `consumed_at_slot`, which doubles as the settled slot that the
    /// retention window counts from. Randomness and tier stay unset.
    fn cancel(&mut self, current_slot: u64) -> Result<()> {
        require!(
            self.status == ROLL_PENDING,
            ChainboundError::InvalidRollStatus
        );
        let cancel_at = self
            .requested_at_slot
            .checked_add(ROLL_CANCEL_DELAY_SLOTS)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        require!(current_slot >= cancel_at, ChainboundError::RollCancelTooEarly);
        self.consumed_at_slot = current_slot;
        self.status = ROLL_CANCELLED;
        Ok(())
    }

    fn ensure_closable(&self, current_slot: u64) -> Result<()> {
        require!(
            self.status == ROLL_CONSUMED || self.status == ROLL_CANCELLED,
            ChainboundError::InvalidRollStatus
        );
        let open_at = self
            .consumed_at_slot
            .checked_add(ROLL_CLOSE_DELAY_SLOTS)
            .ok_or(ChainboundError::ArithmeticOverflow)?;
        require!(current_slot >= open_at, ChainboundError::RollCloseTooEarly);
        Ok(())
    }
}

fn validate_roll_kind(kind: u8) -> Result<()> {
    require!(
        kind == ROLL_KIND_KNOWN || kind == ROLL_KIND_STAMP,
        ChainboundError::InvalidRollKind
    );
    Ok(())
}

/// Every tier must be reachable (strictly increasing) and the last threshold must close the range.
fn validate_cumulative_ppm(cumulative_ppm: &[u32; ROLL_TIER_COUNT]) -> Result<()> {
    let mut previous = 0u32;
    for threshold in cumulative_ppm.iter() {
        require!(*threshold > previous, ChainboundError::InvalidRollOdds);
        previous = *threshold;
    }
    require!(previous == ROLL_PPM_TOTAL, ChainboundError::InvalidRollOdds);
    Ok(())
}

/// Tier = first index whose cumulative threshold exceeds `u64_le(bytes 0..8) % 1_000_000`.
/// The modulo bias is at most 1e6 / 2^64 per outcome.
fn roll_tier(randomness: &[u8; 32], cumulative_ppm: &[u32; ROLL_TIER_COUNT]) -> Result<u8> {
    let mut head = [0u8; 8];
    head.copy_from_slice(&randomness[0..8]);
    let draw = (u64::from_le_bytes(head) % ROLL_PPM_TOTAL as u64) as u32;
    for (tier, threshold) in cumulative_ppm.iter().enumerate() {
        if draw < *threshold {
            return Ok(tier as u8);
        }
    }
    err!(ChainboundError::InvalidRollOdds)
}

/// Binds the oracle input to the operation, the subject and the odds version.
fn roll_caller_seed(
    kind: u8,
    operation_hash: &[u8; 32],
    subject_hash: &[u8; 32],
    odds_version: u32,
    pool_hash: &[u8; 32],
) -> [u8; 32] {
    hashv(&[
        ROLL_SEED_DOMAIN,
        &[kind],
        operation_hash,
        subject_hash,
        &odds_version.to_le_bytes(),
        pool_hash,
    ])
    .to_bytes()
}

#[account]
pub struct MoveVault {
    pub owner: Pubkey,
    pub known_capacity: u8,
    pub known_count: u8,
    pub known_moves: Vec<KnownMove>,
    pub next_instance_id: u64,
    pub bump: u8,
}

impl MoveVault {
    pub const LEN: usize = 32 + 1 + 1 + 4 + (KnownMove::LEN * MAX_KNOWN_MOVES) + 8 + 1;
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default)]
pub struct KnownMove {
    pub instance_id: u64,
    pub move_id: u16,
    pub element: u8,
    pub base_power: u16,
    pub generator_version: u32,
    pub pool_hash: [u8; 32],
}

impl KnownMove {
    pub const LEN: usize = 8 + 2 + 1 + 2 + 4 + 32;
}

#[account]
pub struct ChainlingVault {
    pub owner: Pubkey,
    pub chainling_capacity: u8,
    pub chainling_count: u8,
    pub chainlings: [ChainlingEntry; MAX_CHAINLINGS],
    pub next_instance_id: u32,
    pub bump: u8,
}

impl ChainlingVault {
    pub const LEN: usize = 32 + 1 + 1 + (ChainlingEntry::LEN * MAX_CHAINLINGS) + 4 + 1;
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default)]
pub struct ChainlingEntry {
    pub chainling_id: u16,
    pub instance_id: u32,
    pub level: u16,
    pub xp: u32,
    pub ivs_seed: u64,
    pub flags: u16,
    pub pending_level_reward: u8,
    pub pending_reward_seed: u64,
    pub claimed_reward_bits: u8,
}

impl ChainlingEntry {
    pub const LEN: usize = 2 + 4 + 2 + 4 + 8 + 2 + 1 + 8 + 1;
}

#[account]
pub struct EquippedMoveRegistry {
    pub owner: Pubkey,
    pub chainling_instance_id: u32,
    pub slots: [EquippedMoveSlot; EQUIPPED_SLOTS_ON_CHAIN],
    pub bump: u8,
}

impl EquippedMoveRegistry {
    pub const LEN: usize = 32 + 4 + (EquippedMoveSlot::LEN * EQUIPPED_SLOTS_ON_CHAIN) + 1;
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default)]
pub struct EquippedMoveSlot {
    pub tier: u8,
    pub source_kind: u8,
    pub known_index: u8,
    pub asset_id: [u8; 32],
    pub move_id: u16,
    pub mod_cache: u64,
}

impl EquippedMoveSlot {
    pub const LEN: usize = 1 + 1 + 1 + 32 + 2 + 8;
}

#[account]
pub struct ChainlingAccount {
    pub owner: Pubkey,
    pub chainling_id: u64,
    pub species_id: String,
    pub level: u16,
    pub ivs: [i8; 6],
    pub ivs_seed: u64,
    pub mutated_type_flag: bool,
    pub entangled_flag: bool,
    pub base_visual_variant_id: String,
    pub entangled_type: String,
    pub mutation_ids: Vec<String>,
    pub known_move_ids: Vec<u16>,
    pub is_bound: bool,
    pub archive_asset: Pubkey,
    pub bump: u8,
}

impl ChainlingAccount {
    pub const LEN: usize = 32
        + 8
        + 4
        + MAX_SPECIES_ID_LEN
        + 2
        + 6
        + 8
        + 1
        + 1
        + 4
        + MAX_VISUAL_VARIANT_ID_LEN
        + 4
        + MAX_ENTANGLED_TYPE_LEN
        + 4
        + (MAX_MUTATION_IDS * (4 + MAX_MUTATION_ID_LEN))
        + 4
        + (MAX_CHAINLING_KNOWN_MOVE_IDS * 2)
        + 1
        + 32
        + 1;
}

#[account]
pub struct OwnershipConfig {
    pub schema_version: u16,
    pub moves_collection: Pubkey,
    pub archives_collection: Pubkey,
    pub covers_collection: Pubkey,
    pub cosmetics_collection: Pubkey,
    pub stamp_ram_cost: u32,
    pub bind_ram_cost: u32,
    pub generator_version: u32,
    pub pool_hash: [u8; 32],
    pub bump: u8,
}

impl OwnershipConfig {
    pub const LEN: usize = 2 + (32 * 4) + 4 + 4 + 4 + 32 + 1;
}

#[account]
pub struct StampTicket {
    pub owner: Pubkey,
    pub idempotency_key: [u8; 32],
    pub known_instance_id: u64,
    pub move_id: u16,
    pub modifier_count: u8,
    pub modifier_ids: [String; 2],
    pub rarity: u8,
    pub asset_name: String,
    pub asset_uri: String,
    pub expires_at: i64,
    pub consumed: bool,
    pub stamped_asset: Pubkey,
    pub generator_version: u32,
    pub pool_hash: [u8; 32],
    pub bump: u8,
}

#[account]
pub struct KnownRollReceipt {
    pub owner: Pubkey,
    pub idempotency_key: [u8; 32],
    pub known_instance_id: u64,
    pub move_id: u16,
    pub generator_version: u32,
    pub pool_hash: [u8; 32],
    pub bump: u8,
}

impl KnownRollReceipt {
    pub const LEN: usize = 32 + 32 + 8 + 2 + 4 + 32 + 1;
}

impl StampTicket {
    pub const LEN: usize = 32
        + 32
        + 8
        + 2
        + 1
        + (2 * (4 + MAX_MODIFIER_ID_LEN))
        + 1
        + 4
        + MAX_ASSET_NAME_LEN
        + 4
        + MAX_ASSET_URI_LEN
        + 8
        + 1
        + 32
        + 4
        + 32
        + 1;
}

#[account]
pub struct BindReceipt {
    pub owner: Pubkey,
    pub chainling_id: u64,
    pub idempotency_key: [u8; 32],
    pub cover_asset: Pubkey,
    pub component_asset: Pubkey,
    pub component_kind: u8,
    pub archive_asset: Pubkey,
    pub completed: bool,
    pub bump: u8,
}

impl BindReceipt {
    pub const LEN: usize = 32 + 8 + 32 + 32 + 32 + 1 + 32 + 1 + 1;
}

#[event]
pub struct StampedMoveCreated {
    pub owner: Pubkey,
    pub known_instance_id: u64,
    pub move_id: u16,
    pub modifier_count: u8,
    pub asset: Pubkey,
    pub idempotency_key: [u8; 32],
}

#[event]
pub struct KnownMoveRolled {
    pub owner: Pubkey,
    pub known_instance_id: u64,
    pub move_id: u16,
}

#[event]
pub struct RollOddsChanged {
    pub kind: u8,
    pub odds_version: u32,
    pub cumulative_ppm: [u32; ROLL_TIER_COUNT],
    pub pool_hash: [u8; 32],
}

#[event]
pub struct RollRequested {
    pub kind: u8,
    pub operation_hash: [u8; 32],
    pub subject_hash: [u8; 32],
    pub odds_version: u32,
    pub caller_seed: [u8; 32],
}

#[event]
pub struct RollFulfilled {
    pub kind: u8,
    pub operation_hash: [u8; 32],
    pub tier: u8,
    pub odds_version: u32,
}

#[event]
pub struct RollConsumed {
    pub kind: u8,
    pub operation_hash: [u8; 32],
    pub tier: u8,
    pub result_hash: [u8; 32],
}

#[event]
pub struct RollCancelled {
    pub kind: u8,
    pub operation_hash: [u8; 32],
    pub requested_at_slot: u64,
    pub cancelled_at_slot: u64,
}

#[event]
pub struct ProgramAuthorityProposed {
    pub current_authority: Pubkey,
    pub pending_authority: Pubkey,
}

#[event]
pub struct ProgramAuthorityTransferred {
    pub previous_authority: Pubkey,
    pub new_authority: Pubkey,
}

#[event]
pub struct ProgramAuthorityTransferCancelled {
    pub current_authority: Pubkey,
    pub pending_authority: Pubkey,
}

#[event]
pub struct RamGranted {
    pub owner: Pubkey,
    pub amount: u32,
    pub balance_after: u32,
    pub utc_day: i64,
    pub idempotency_key: [u8; 32],
}

#[event]
pub struct RamReserved {
    pub owner: Pubkey,
    pub operation_hash: [u8; 32],
    pub amount: u32,
    pub kind: u8,
    pub expires_at: i64,
}

#[event]
pub struct RamReservationCancelled {
    pub owner: Pubkey,
    pub operation_hash: [u8; 32],
}

#[event]
pub struct PurchasedRamCredited {
    pub owner: Pubkey,
    pub order_hash: [u8; 32],
    pub amount: u32,
    pub balance_before: u32,
    pub balance_after: u32,
}

#[event]
pub struct ReservedRamDebited {
    pub owner: Pubkey,
    pub operation_hash: [u8; 32],
    pub amount: u32,
    pub kind: u8,
    pub balance_before: u32,
    pub balance_after: u32,
}

#[event]
pub struct RamDeltaReconciled {
    pub owner: Pubkey,
    pub case_hash: [u8; 32],
    pub direction: u8,
    pub amount: u32,
    pub balance_before: u32,
    pub balance_after: u32,
}

#[event]
pub struct ChainlingBound {
    pub owner: Pubkey,
    pub chainling_id: u64,
    pub archive_asset: Pubkey,
    pub cover_asset: Pubkey,
    pub component_asset: Pubkey,
    pub component_kind: u8,
    pub idempotency_key: [u8; 32],
}

fn grant_known_move(
    vault: &mut Account<MoveVault>,
    move_id: u16,
    element: u8,
    base_power: u16,
    generator_version: u32,
    pool_hash: [u8; 32],
) -> Result<u64> {
    let index = vault.known_count as usize;
    require!(index < MAX_KNOWN_MOVES, ChainboundError::VaultFull);
    let instance_id = vault.next_instance_id;
    require!(instance_id != 0, ChainboundError::ArithmeticOverflow);
    vault.known_moves.push(KnownMove {
        instance_id,
        move_id,
        element,
        base_power,
        generator_version,
        pool_hash,
    });
    vault.known_count = vault
        .known_count
        .checked_add(1)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    vault.next_instance_id = vault
        .next_instance_id
        .checked_add(1)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    Ok(instance_id)
}

fn apply_capped_ram_grant(
    profile: &mut PlayerProfile,
    daily_cap: u16,
    amount: u32,
    now: i64,
) -> Result<()> {
    require!(amount > 0, ChainboundError::InvalidRamGrant);
    let amount_u16 = u16::try_from(amount).map_err(|_| error!(ChainboundError::InvalidRamGrant))?;
    let current_day = now.div_euclid(SECONDS_PER_DAY);
    let recorded_day = profile.last_ram_reset_timestamp.div_euclid(SECONDS_PER_DAY);
    let earned_before = if current_day == recorded_day {
        profile.daily_ram_earned
    } else {
        0
    };
    let earned_after = earned_before
        .checked_add(amount_u16)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    require!(
        earned_after <= daily_cap,
        ChainboundError::RamDailyCapExceeded
    );
    let balance_after = profile
        .ram_balance
        .checked_add(amount)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    let lifetime_after = profile
        .ram_lifetime_earned
        .checked_add(amount as u64)
        .ok_or(ChainboundError::ArithmeticOverflow)?;

    profile.ram_balance = balance_after;
    profile.daily_ram_earned = earned_after;
    profile.ram_lifetime_earned = lifetime_after;
    if current_day != recorded_day {
        profile.last_ram_reset_timestamp = now;
    }
    Ok(())
}

fn apply_uncapped_ram_credit(profile: &mut PlayerProfile, amount: u32) -> Result<()> {
    require!(amount > 0, ChainboundError::InvalidRamAmount);
    profile.ram_balance = profile
        .ram_balance
        .checked_add(amount)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    Ok(())
}

fn apply_ram_debit(profile: &mut PlayerProfile, amount: u32) -> Result<()> {
    require!(amount > 0, ChainboundError::InvalidRamAmount);
    require!(
        profile.ram_balance >= amount,
        ChainboundError::InsufficientRam
    );
    profile.ram_balance = profile
        .ram_balance
        .checked_sub(amount)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    Ok(())
}

fn consume_ram_reservation(
    reservation: &mut RamReservation,
    owner: Pubkey,
    operation_hash: [u8; 32],
    amount: u32,
    kind: u8,
    now: i64,
) -> Result<()> {
    require_keys_eq!(reservation.owner, owner, ChainboundError::InvalidAuthority);
    require!(
        reservation.operation_hash == operation_hash,
        ChainboundError::IdempotencyMismatch
    );
    require!(
        reservation.amount == amount,
        ChainboundError::ReservationAmountMismatch
    );
    require!(
        reservation.kind == kind,
        ChainboundError::InvalidReservationKind
    );
    require!(
        !reservation.consumed,
        ChainboundError::ReservationAlreadyConsumed
    );
    require!(
        reservation.expires_at >= now,
        ChainboundError::ReservationExpired
    );
    reservation.consumed = true;
    Ok(())
}

fn find_known_move(vault: &MoveVault, instance_id: u64) -> Result<KnownMove> {
    vault
        .known_moves
        .iter()
        .find(|known| known.instance_id == instance_id)
        .copied()
        .ok_or_else(|| error!(ChainboundError::KnownMoveNotFound))
}

fn remove_known_by_instance(vault: &mut MoveVault, instance_id: u64) -> Result<KnownMove> {
    let index = vault
        .known_moves
        .iter()
        .position(|known| known.instance_id == instance_id)
        .ok_or_else(|| error!(ChainboundError::KnownMoveNotFound))?;
    let removed = vault.known_moves.swap_remove(index);
    vault.known_count = vault
        .known_count
        .checked_sub(1)
        .ok_or(ChainboundError::ArithmeticOverflow)?;
    Ok(removed)
}

fn validate_ownership_config_keys(
    moves_collection: Pubkey,
    archives_collection: Pubkey,
    covers_collection: Pubkey,
    cosmetics_collection: Pubkey,
) -> Result<()> {
    let keys = [
        moves_collection,
        archives_collection,
        covers_collection,
        cosmetics_collection,
    ];
    require!(
        keys.iter().all(|key| *key != Pubkey::default()),
        ChainboundError::InvalidConfig
    );
    for left in 0..keys.len() {
        for right in (left + 1)..keys.len() {
            require!(keys[left] != keys[right], ChainboundError::InvalidConfig);
        }
    }
    Ok(())
}

fn validate_collection(
    collection: &UncheckedAccount,
    expected_key: Pubkey,
    expected_update_authority: Pubkey,
) -> Result<()> {
    require_keys_eq!(
        collection.key(),
        expected_key,
        ChainboundError::WrongCollection
    );
    let collection_info = collection.to_account_info();
    let collection_data = BaseCollectionV1::try_from(&collection_info)
        .map_err(|_| error!(ChainboundError::InvalidCoreAccount))?;
    require_keys_eq!(
        collection_data.update_authority,
        expected_update_authority,
        ChainboundError::InvalidCollectionAuthority
    );
    Ok(())
}

fn validate_asset_membership(
    asset: &UncheckedAccount,
    expected_owner: Pubkey,
    expected_collection: Pubkey,
) -> Result<()> {
    let asset_info = asset.to_account_info();
    let asset_data = BaseAssetV1::try_from(&asset_info)
        .map_err(|_| error!(ChainboundError::InvalidCoreAccount))?;
    require_keys_eq!(
        asset_data.owner,
        expected_owner,
        ChainboundError::AssetOwnerMismatch
    );
    match asset_data.update_authority {
        UpdateAuthority::Collection(collection) => {
            require_keys_eq!(
                collection,
                expected_collection,
                ChainboundError::WrongCollection
            );
        }
        _ => return err!(ChainboundError::WrongCollection),
    }
    Ok(())
}

fn attribute(key: &str, value: String) -> Attribute {
    Attribute {
        key: key.to_string(),
        value,
    }
}

fn stamped_attributes(config: &OwnershipConfig, ticket: &StampTicket) -> Vec<Attribute> {
    let mut values = vec![
        attribute("cb.schema_version", config.schema_version.to_string()),
        attribute("cb.kind", "stamped_move".to_string()),
        attribute("cb.move_tier", "stamped".to_string()),
        attribute("cb.move_id", ticket.move_id.to_string()),
        attribute("cb.known_instance_id", ticket.known_instance_id.to_string()),
        attribute("cb.modifier_count", ticket.modifier_count.to_string()),
        attribute("cb.modifier_1", ticket.modifier_ids[0].clone()),
        attribute("cb.rarity", ticket.rarity.to_string()),
        attribute("cb.generator_version", ticket.generator_version.to_string()),
        attribute("cb.pool_hash", hex_bytes(&ticket.pool_hash)),
    ];
    if ticket.modifier_count == 2 {
        values.push(attribute("cb.modifier_2", ticket.modifier_ids[1].clone()));
    }
    values
}

fn profile_stamped_attributes(config: &OwnershipConfig, ticket: &StampTicket) -> Vec<Attribute> {
    let mut values = stamped_attributes(config, ticket);
    values.push(attribute("cb.known_source", "profile".to_string()));
    values
}

fn archive_attributes(
    config: &OwnershipConfig,
    chainling: &ChainlingAccount,
    cover_asset: Pubkey,
    component_asset: Pubkey,
    component_kind: u8,
) -> Vec<Attribute> {
    let mutation_ids = chainling.mutation_ids.join(",");
    let known_move_ids = chainling
        .known_move_ids
        .iter()
        .map(u16::to_string)
        .collect::<Vec<_>>()
        .join(",");
    vec![
        attribute("cb.schema_version", config.schema_version.to_string()),
        attribute("cb.kind", "chainling_archive".to_string()),
        attribute("cb.chainling_id", chainling.chainling_id.to_string()),
        attribute("cb.species_id", chainling.species_id.clone()),
        attribute("cb.level", chainling.level.to_string()),
        attribute("cb.ivs", format!("{:?}", chainling.ivs)),
        attribute("cb.ivs_seed", chainling.ivs_seed.to_string()),
        attribute("cb.entangled", chainling.entangled_flag.to_string()),
        attribute("cb.entangled_type", chainling.entangled_type.clone()),
        attribute(
            "cb.base_visual_variant_id",
            chainling.base_visual_variant_id.clone(),
        ),
        attribute("cb.mutated", chainling.mutated_type_flag.to_string()),
        attribute("cb.mutation_ids", mutation_ids),
        attribute("cb.known_move_ids", known_move_ids),
        attribute("cb.book_cover_asset", cover_asset.to_string()),
        attribute("cb.bind_component_asset", component_asset.to_string()),
        attribute(
            "cb.bind_component_kind",
            if component_kind == COMPONENT_STAMPED_MOVE {
                "stamped_move".to_string()
            } else {
                "cosmetic".to_string()
            },
        ),
    ]
}

fn bounded_string(value: String, max_len: usize) -> Result<String> {
    require!(value.len() <= max_len, ChainboundError::StringTooLong);
    Ok(value)
}

fn hex_bytes(value: &[u8]) -> String {
    value.iter().map(|byte| format!("{byte:02x}")).collect()
}

#[error_code]
pub enum ChainboundError {
    #[msg("Arithmetic overflow.")]
    ArithmeticOverflow,
    #[msg("Insufficient RAM.")]
    InsufficientRam,
    #[msg("RAM grant amount must be positive and fit the configured accounting fields.")]
    InvalidRamGrant,
    #[msg("RAM grant would exceed the configured daily cap.")]
    RamDailyCapExceeded,
    #[msg("RAM amount must be positive.")]
    InvalidRamAmount,
    #[msg("RAM reservation kind is invalid for this operation.")]
    InvalidReservationKind,
    #[msg("RAM reservation amount does not match the operation.")]
    ReservationAmountMismatch,
    #[msg("RAM reservation has expired.")]
    ReservationExpired,
    #[msg("RAM reservation was already consumed.")]
    ReservationAlreadyConsumed,
    #[msg("RAM reconciliation direction must be credit or debit.")]
    InvalidReconciliationDirection,
    #[msg("The target vault is full.")]
    VaultFull,
    #[msg("Invalid index.")]
    InvalidIndex,
    #[msg("Invalid authority for this account.")]
    InvalidAuthority,
    #[msg("String exceeds the account's bounded storage.")]
    StringTooLong,
    #[msg("This contract entrypoint is declared but not implemented yet.")]
    ContractNotImplemented,
    #[msg("Game configuration values are invalid.")]
    InvalidConfig,
    #[msg("The Known move instance does not exist or was already consumed.")]
    KnownMoveNotFound,
    #[msg("The profile-owned Known reference is invalid.")]
    InvalidKnownReference,
    #[msg("The ticket move no longer matches the selected Known instance.")]
    KnownMoveMismatch,
    #[msg("Stamped moves require exactly one or two modifiers.")]
    InvalidModifierCount,
    #[msg("Modifier identifiers must be non-empty.")]
    InvalidModifier,
    #[msg("Stamped modifier identifiers must be distinct.")]
    DuplicateModifier,
    #[msg("The ticket was produced for a different ownership configuration.")]
    StaleConfiguration,
    #[msg("This ticket has expired.")]
    TicketExpired,
    #[msg("This ticket was already consumed.")]
    TicketAlreadyConsumed,
    #[msg("The idempotency key does not match this receipt or ticket.")]
    IdempotencyMismatch,
    #[msg("The supplied Core collection is not the configured trusted collection.")]
    WrongCollection,
    #[msg("The supplied Core collection is not controlled by the program authority PDA.")]
    InvalidCollectionAuthority,
    #[msg("The supplied Core asset is not owned by the signing wallet.")]
    AssetOwnerMismatch,
    #[msg("The supplied Metaplex Core account could not be decoded.")]
    InvalidCoreAccount,
    #[msg("Bind component kind must be a Stamped move or cosmetic skin.")]
    InvalidComponentKind,
    #[msg("The cover and selected third component must be different assets.")]
    DuplicateComponent,
    #[msg("The Chainling does not match the requested Bind.")]
    ChainlingMismatch,
    #[msg("This Chainling is already Bound.")]
    AlreadyBound,
    #[msg("Capture supplied too many mutation IDs.")]
    TooManyMutations,
    #[msg("Capture supplied too many Known move IDs.")]
    TooManyKnownMoves,
    #[msg("Encounter randomness is not in the required lifecycle state.")]
    InvalidEncounterStatus,
    #[msg("Roll kind must be Known (1) or Stamp (2).")]
    InvalidRollKind,
    #[msg("Roll odds must be six strictly increasing thresholds ending at 1,000,000.")]
    InvalidRollOdds,
    #[msg("Roll request is not in the required lifecycle state.")]
    InvalidRollStatus,
    #[msg("Roll result hash must be non-zero.")]
    InvalidRollResult,
    #[msg("Roll request retention window has not passed.")]
    RollCloseTooEarly,
    #[msg("Roll request is still inside its fulfillment window and cannot be cancelled yet.")]
    RollCancelTooEarly,
    #[msg("Authority transfer target is invalid, or does not match the pending proposal.")]
    InvalidAuthorityTransfer,
    #[msg("Encounter request retention window has not passed.")]
    EncounterCloseTooEarly,
}

#[cfg(test)]
mod tests {
    use super::*;

    fn zero_ram_profile(timestamp: i64) -> PlayerProfile {
        PlayerProfile {
            owner: Pubkey::new_unique(),
            schema_version: 2,
            created_at_slot: 1,
            wins: 0,
            losses: 0,
            last_known_scene_id: 0,
            last_known_pos_x: 0,
            last_known_pos_y: 0,
            last_known_pos_z: 0,
            ram_balance: 0,
            daily_ram_earned: 0,
            last_ram_reset_timestamp: timestamp,
            ram_lifetime_earned: 0,
            bump: 1,
        }
    }

    fn test_vault() -> MoveVault {
        let known_moves = vec![
            KnownMove {
                instance_id: 10,
                move_id: 7,
                element: 2,
                base_power: 45,
                generator_version: 1,
                pool_hash: [9; 32],
            },
            KnownMove {
                instance_id: 11,
                move_id: 7,
                element: 2,
                base_power: 45,
                generator_version: 1,
                pool_hash: [9; 32],
            },
        ];
        MoveVault {
            owner: Pubkey::new_unique(),
            known_capacity: MAX_KNOWN_MOVES as u8,
            known_count: 2,
            known_moves,
            next_instance_id: 12,
            bump: 1,
        }
    }

    #[test]
    fn fresh_profile_starts_at_zero_and_can_receive_only_the_daily_cap() {
        let now = 1_800_000_000;
        let mut profile = zero_ram_profile(now);
        assert_eq!(profile.ram_balance, 0);
        assert_eq!(profile.daily_ram_earned, 0);
        assert_eq!(profile.ram_lifetime_earned, 0);

        apply_capped_ram_grant(&mut profile, 100, 100, now).unwrap();
        assert_eq!(profile.ram_balance, 100);
        assert_eq!(profile.daily_ram_earned, 100);
        assert_eq!(profile.ram_lifetime_earned, 100);
        assert!(apply_capped_ram_grant(&mut profile, 100, 1, now).is_err());
    }

    #[test]
    fn capped_grant_rejects_zero_and_resets_daily_counter_on_next_utc_day() {
        let day_one = 1_800_000_000;
        let day_two = day_one + SECONDS_PER_DAY;
        let mut profile = zero_ram_profile(day_one);

        assert!(apply_capped_ram_grant(&mut profile, 100, 0, day_one).is_err());
        apply_capped_ram_grant(&mut profile, 100, 100, day_one).unwrap();
        apply_capped_ram_grant(&mut profile, 100, 100, day_two).unwrap();

        assert_eq!(profile.ram_balance, 200);
        assert_eq!(profile.daily_ram_earned, 100);
        assert_eq!(profile.ram_lifetime_earned, 200);
        assert_eq!(profile.last_ram_reset_timestamp, day_two);
    }

    #[test]
    fn stamp_consumes_only_the_selected_known_instance() {
        let mut vault = test_vault();
        let removed = remove_known_by_instance(&mut vault, 10).unwrap();
        assert_eq!(removed.instance_id, 10);
        assert_eq!(vault.known_count, 1);
        assert_eq!(vault.known_moves[0].instance_id, 11);
        assert!(remove_known_by_instance(&mut vault, 10).is_err());
    }

    #[test]
    fn encounter_randomness_transitions_pending_fulfilled_consumed_once() {
        let mut request = EncounterRequest {
            status: ENCOUNTER_PENDING,
            randomness: [0; 32],
            owner: Pubkey::new_unique(),
            request_seed: [7; 32],
            requested_at_slot: 10,
            fulfilled_at_slot: 0,
            bump: 1,
        };

        request.fulfill([9; 32], 12).unwrap();
        assert_eq!(request.status, ENCOUNTER_FULFILLED);
        assert_eq!(request.randomness, [9; 32]);
        assert_eq!(request.fulfilled_at_slot, 12);
        assert!(request.fulfill([8; 32], 13).is_err());

        request.consume().unwrap();
        assert_eq!(request.status, ENCOUNTER_CONSUMED);
        assert!(request.consume().is_err());
    }

    // Ultra curve: 64 / 25 / 9 / 1.7 / 0.28 / 0.02 percent.
    const ULTRA_CUMULATIVE_PPM: [u32; ROLL_TIER_COUNT] =
        [640_000, 890_000, 980_000, 997_000, 999_800, 1_000_000];

    fn bytes_with_draw(draw: u64) -> [u8; 32] {
        let mut bytes = [0xAB; 32];
        bytes[0..8].copy_from_slice(&draw.to_le_bytes());
        bytes
    }

    fn test_roll_request() -> RollRequest {
        RollRequest {
            status: ROLL_PENDING,
            tier: ROLL_TIER_UNSET,
            randomness: [0; 32],
            kind: ROLL_KIND_KNOWN,
            operation_hash: [3; 32],
            subject_hash: [4; 32],
            odds_version: 1,
            cumulative_ppm: ULTRA_CUMULATIVE_PPM,
            pool_hash: [5; 32],
            requested_at_slot: 100,
            fulfilled_at_slot: 0,
            consumed_at_slot: 0,
            result_hash: [0; 32],
            bump: 1,
        }
    }

    #[test]
    fn roll_odds_validation_requires_strictly_increasing_thresholds_ending_at_one_million() {
        assert!(validate_cumulative_ppm(&ULTRA_CUMULATIVE_PPM).is_ok());
        assert!(validate_cumulative_ppm(&[0, 1, 2, 3, 4, 1_000_000]).is_err());
        assert!(validate_cumulative_ppm(&[10, 10, 20, 30, 40, 1_000_000]).is_err());
        assert!(validate_cumulative_ppm(&[10, 20, 30, 40, 50, 999_999]).is_err());
        assert!(validate_cumulative_ppm(&[10, 20, 30, 40, 50, 1_000_001]).is_err());
        assert!(validate_roll_kind(ROLL_KIND_KNOWN).is_ok());
        assert!(validate_roll_kind(ROLL_KIND_STAMP).is_ok());
        assert!(validate_roll_kind(0).is_err());
        assert!(validate_roll_kind(3).is_err());
    }

    #[test]
    fn roll_tier_boundaries_follow_cumulative_thresholds() {
        let cases: [(u64, u8); 13] = [
            (0, 0),
            (639_999, 0),
            (640_000, 1),
            (889_999, 1),
            (890_000, 2),
            (979_999, 2),
            (980_000, 3),
            (996_999, 3),
            (997_000, 4),
            (999_799, 4),
            (999_800, 5),
            (999_999, 5),
            // Only the value mod 1,000,000 matters.
            (3 * 1_000_000 + 999_800, 5),
        ];
        for (draw, expected) in cases {
            assert_eq!(
                roll_tier(&bytes_with_draw(draw), &ULTRA_CUMULATIVE_PPM).unwrap(),
                expected,
                "draw {draw}"
            );
        }
        // u64::MAX % 1_000_000 = 551_615, which is Common.
        assert_eq!(
            roll_tier(&bytes_with_draw(u64::MAX), &ULTRA_CUMULATIVE_PPM).unwrap(),
            0
        );
    }

    #[test]
    fn roll_request_draws_tier_on_fulfill_and_transitions_once() {
        let mut request = test_roll_request();
        request.fulfill(bytes_with_draw(999_850), 103).unwrap();
        assert_eq!(request.status, ROLL_FULFILLED);
        assert_eq!(request.tier, 5);
        assert_eq!(request.fulfilled_at_slot, 103);
        assert!(request.fulfill(bytes_with_draw(0), 104).is_err());
        assert_eq!(request.tier, 5);

        assert!(request.consume([0; 32], 110).is_err());
        request.consume([9; 32], 110).unwrap();
        assert_eq!(request.status, ROLL_CONSUMED);
        assert_eq!(request.result_hash, [9; 32]);
        assert!(request.consume([8; 32], 111).is_err());
    }

    #[test]
    fn roll_close_requires_consumed_and_retention_window() {
        let mut request = test_roll_request();
        assert!(request.ensure_closable(u64::MAX).is_err());
        request.fulfill(bytes_with_draw(1), 101).unwrap();
        assert!(request.ensure_closable(u64::MAX).is_err());
        request.consume([7; 32], 200).unwrap();
        assert!(request
            .ensure_closable(200 + ROLL_CLOSE_DELAY_SLOTS - 1)
            .is_err());
        assert!(request.ensure_closable(200 + ROLL_CLOSE_DELAY_SLOTS).is_ok());
    }

    #[test]
    fn roll_cancel_only_from_pending_after_the_fulfillment_window() {
        let mut request = test_roll_request();
        let cancel_at = request.requested_at_slot + ROLL_CANCEL_DELAY_SLOTS;
        assert!(request.cancel(cancel_at - 1).is_err());
        request.cancel(cancel_at).unwrap();
        assert_eq!(request.status, ROLL_CANCELLED);
        assert_eq!(request.tier, ROLL_TIER_UNSET);
        assert_eq!(request.randomness, [0; 32]);
        assert_eq!(request.consumed_at_slot, cancel_at);

        // A late oracle callback, a consume and a second cancel all fail.
        assert!(request.fulfill(bytes_with_draw(999_900), cancel_at + 1).is_err());
        assert!(request.consume([9; 32], cancel_at + 1).is_err());
        assert!(request.cancel(cancel_at + 1).is_err());
        assert_eq!(request.randomness, [0; 32]);

        assert!(request
            .ensure_closable(cancel_at + ROLL_CLOSE_DELAY_SLOTS - 1)
            .is_err());
        assert!(request
            .ensure_closable(cancel_at + ROLL_CLOSE_DELAY_SLOTS)
            .is_ok());
    }

    #[test]
    fn roll_cancel_cannot_discard_drawn_randomness() {
        let mut fulfilled = test_roll_request();
        fulfilled.fulfill(bytes_with_draw(5), 101).unwrap();
        assert!(fulfilled.cancel(u64::MAX).is_err());
        assert_eq!(fulfilled.status, ROLL_FULFILLED);

        let mut consumed = test_roll_request();
        consumed.fulfill(bytes_with_draw(5), 101).unwrap();
        consumed.consume([1; 32], 102).unwrap();
        assert!(consumed.cancel(u64::MAX).is_err());
        assert_eq!(consumed.status, ROLL_CONSUMED);
    }

    #[test]
    fn authority_proposal_rejects_default_and_current_keys() {
        let current = Pubkey::new_unique();
        let next = Pubkey::new_unique();
        assert!(validate_authority_proposal(&current, &next).is_ok());
        assert!(validate_authority_proposal(&current, &current).is_err());
        assert!(validate_authority_proposal(&current, &Pubkey::default()).is_err());
    }

    #[test]
    fn encounter_close_requires_consumed_and_retention_window() {
        let mut request = EncounterRequest {
            status: ENCOUNTER_PENDING,
            randomness: [0; 32],
            owner: Pubkey::new_unique(),
            request_seed: [1; 32],
            requested_at_slot: 10,
            fulfilled_at_slot: 0,
            bump: 1,
        };
        assert!(request.ensure_closable(u64::MAX).is_err());
        request.fulfill([2; 32], 20).unwrap();
        assert!(request.ensure_closable(u64::MAX).is_err());
        request.consume().unwrap();
        assert!(request
            .ensure_closable(20 + ENCOUNTER_CLOSE_DELAY_SLOTS - 1)
            .is_err());
        assert!(request
            .ensure_closable(20 + ENCOUNTER_CLOSE_DELAY_SLOTS)
            .is_ok());
    }

    #[test]
    fn roll_caller_seed_binds_every_input() {
        let base = roll_caller_seed(1, &[1; 32], &[2; 32], 1, &[3; 32]);
        assert_ne!(base, roll_caller_seed(2, &[1; 32], &[2; 32], 1, &[3; 32]));
        assert_ne!(base, roll_caller_seed(1, &[9; 32], &[2; 32], 1, &[3; 32]));
        assert_ne!(base, roll_caller_seed(1, &[1; 32], &[9; 32], 1, &[3; 32]));
        assert_ne!(base, roll_caller_seed(1, &[1; 32], &[2; 32], 2, &[3; 32]));
        assert_ne!(base, roll_caller_seed(1, &[1; 32], &[2; 32], 1, &[9; 32]));
        assert_eq!(base, roll_caller_seed(1, &[1; 32], &[2; 32], 1, &[3; 32]));
    }

    #[test]
    fn archive_metadata_contains_capture_base_variant_and_known_moves() {
        let owner = Pubkey::new_unique();
        let chainling = ChainlingAccount {
            owner,
            chainling_id: 99,
            species_id: "mossling".to_string(),
            level: 3,
            ivs: [-1, 2, 3, 4, 5, 6],
            ivs_seed: 1234,
            mutated_type_flag: true,
            entangled_flag: true,
            base_visual_variant_id: "mossling_variant_03".to_string(),
            entangled_type: "verdant".to_string(),
            mutation_ids: vec!["thorned".to_string()],
            known_move_ids: vec![7, 8],
            is_bound: false,
            archive_asset: Pubkey::default(),
            bump: 1,
        };
        let config = OwnershipConfig {
            schema_version: 1,
            moves_collection: Pubkey::new_unique(),
            archives_collection: Pubkey::new_unique(),
            covers_collection: Pubkey::new_unique(),
            cosmetics_collection: Pubkey::new_unique(),
            stamp_ram_cost: 75,
            bind_ram_cost: 250,
            generator_version: 1,
            pool_hash: [7; 32],
            bump: 1,
        };

        let attrs = archive_attributes(
            &config,
            &chainling,
            Pubkey::new_unique(),
            Pubkey::new_unique(),
            COMPONENT_STAMPED_MOVE,
        );
        assert!(attrs.iter().any(|item| {
            item.key == "cb.base_visual_variant_id" && item.value == "mossling_variant_03"
        }));
        assert!(attrs
            .iter()
            .any(|item| item.key == "cb.known_move_ids" && item.value == "7,8"));
        assert!(attrs
            .iter()
            .any(|item| item.key == "cb.kind" && item.value == "chainling_archive"));
        assert!(attrs.iter().any(|item| {
            item.key == "cb.bind_component_kind" && item.value == "stamped_move"
        }));
        assert!(!attrs.iter().any(|item| {
            item.value == "bound_chainling" || item.value == "cosmetic_skin"
        }));

        let cosmetic_attrs = archive_attributes(
            &config,
            &chainling,
            Pubkey::new_unique(),
            Pubkey::new_unique(),
            COMPONENT_COSMETIC,
        );
        assert!(cosmetic_attrs
            .iter()
            .any(|item| item.key == "cb.bind_component_kind" && item.value == "cosmetic"));
        assert!(!cosmetic_attrs.iter().any(|item| {
            item.value == "bound_chainling" || item.value == "cosmetic_skin"
        }));
    }

    #[test]
    fn ownership_collections_must_be_nonzero_and_distinct() {
        let moves = Pubkey::new_unique();
        assert!(validate_ownership_config_keys(
            moves,
            Pubkey::new_unique(),
            Pubkey::new_unique(),
            Pubkey::new_unique()
        )
        .is_ok());
        assert!(validate_ownership_config_keys(
            moves,
            moves,
            Pubkey::new_unique(),
            Pubkey::new_unique()
        )
        .is_err());
    }

    #[test]
    fn purchased_ram_is_uncapped_and_does_not_change_earned_counters() {
        let now = 1_800_000_000;
        let mut profile = zero_ram_profile(now);
        profile.ram_balance = 10;
        profile.daily_ram_earned = 7;
        profile.ram_lifetime_earned = 55;

        apply_uncapped_ram_credit(&mut profile, 500).unwrap();

        assert_eq!(profile.ram_balance, 510);
        assert_eq!(profile.daily_ram_earned, 7);
        assert_eq!(profile.ram_lifetime_earned, 55);
        assert_eq!(profile.last_ram_reset_timestamp, now);
        assert!(apply_uncapped_ram_credit(&mut profile, 0).is_err());
    }

    #[test]
    fn purchased_ram_credit_rejects_overflow() {
        let mut profile = zero_ram_profile(1_800_000_000);
        profile.ram_balance = u32::MAX;
        assert!(apply_uncapped_ram_credit(&mut profile, 1).is_err());
    }

    #[test]
    fn reservation_consumes_once_and_requires_exact_operation() {
        let owner = Pubkey::new_unique();
        let authority = Pubkey::new_unique();
        let operation_hash = [3; 32];
        let mut reservation = RamReservation {
            owner,
            operation_hash,
            amount: 75,
            kind: RAM_RESERVATION_KIND_STAMP,
            expires_at: 2_000,
            consumed: false,
            result_authority: authority,
            bump: 1,
        };

        assert!(consume_ram_reservation(
            &mut reservation,
            owner,
            operation_hash,
            74,
            RAM_RESERVATION_KIND_STAMP,
            1_000,
        )
        .is_err());
        assert!(consume_ram_reservation(
            &mut reservation,
            owner,
            operation_hash,
            75,
            RAM_RESERVATION_KIND_STAMP,
            1_000,
        )
        .is_ok());
        assert!(reservation.consumed);
        assert!(consume_ram_reservation(
            &mut reservation,
            owner,
            operation_hash,
            75,
            RAM_RESERVATION_KIND_STAMP,
            1_000,
        )
        .is_err());
    }

    #[test]
    fn reservation_rejects_expired_and_wrong_kind() {
        let owner = Pubkey::new_unique();
        let operation_hash = [4; 32];
        let base = RamReservation {
            owner,
            operation_hash,
            amount: 5,
            kind: RAM_RESERVATION_KIND_KNOWN,
            expires_at: 100,
            consumed: false,
            result_authority: Pubkey::new_unique(),
            bump: 1,
        };
        let mut wrong_kind = RamReservation { ..base };
        assert!(consume_ram_reservation(
            &mut wrong_kind,
            owner,
            operation_hash,
            5,
            RAM_RESERVATION_KIND_BIND,
            10,
        )
        .is_err());

        let mut expired = RamReservation { ..base };
        assert!(consume_ram_reservation(
            &mut expired,
            owner,
            operation_hash,
            5,
            RAM_RESERVATION_KIND_KNOWN,
            101,
        )
        .is_err());
    }
}
