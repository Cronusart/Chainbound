-- Test-only prerequisite schema for the RAM shop suites.
--
-- Unlike ram_v2_minimal_prerequisites.sql, this fixture leaves every ram_*
-- table to the real shop migrations so the suites exercise the deployed
-- ram_purchase_orders / ram_payment_attempts definitions. Apply it, then in order:
--   20261003113101_ram_shop_unified_ledger.sql
--   20261008083113_ram_shop_r01_devnet_hardening.sql
--   20261008231904_ram_reward_lock_order.sql
--   20261008231910_battle_reward_authority_schema.sql
--   20261008231918_battle_reward_service_rpcs.sql
--   20261009010622_ram_v2_postgres_canonical_earn.sql
--   20261009012120_ram_reward_atomic_terminal_wrappers.sql
--   20261009143000_ram_v2_pre_rollout_hardening.sql
--   the R2 shop migrations (20261028*)
-- Never push this fixture to a hosted project.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pgtap;

CREATE TABLE public.player_profiles (
    player_profile_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    platform_origin TEXT NOT NULL DEFAULT 'unknown',
    last_platform_seen TEXT NOT NULL DEFAULT 'unknown',
    ram_balance BIGINT NOT NULL DEFAULT 0 CHECK (ram_balance BETWEEN 0 AND 4294967295),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.profile_wallets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_provider TEXT NOT NULL DEFAULT 'other',
    wallet_public_key TEXT NOT NULL,
    linked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    unlinked_at TIMESTAMPTZ NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    metadata_json JSONB NOT NULL DEFAULT '{}'::JSONB
);

CREATE TABLE public.profile_identities (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    identity_type TEXT NOT NULL DEFAULT 'guest',
    provider TEXT NOT NULL DEFAULT 'supabase_anonymous',
    provider_subject TEXT NOT NULL,
    auth_user_id TEXT NULL,
    is_primary BOOLEAN NOT NULL DEFAULT true,
    linked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    revoked_at TIMESTAMPTZ NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::JSONB
);
CREATE UNIQUE INDEX ux_profile_identities_active_auth_user
    ON public.profile_identities(auth_user_id) WHERE revoked_at IS NULL;


CREATE TABLE public.authoritative_pve_battles (
    battle_id UUID PRIMARY KEY,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    start_command_id TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('active', 'capturing', 'completed', 'lost', 'abandoned', 'service_interrupted')),
    battle_kind TEXT NOT NULL CHECK (battle_kind IN ('wild', 'boss', 'trainer', 'scripted')),
    state_version INTEGER NOT NULL DEFAULT 0 CHECK (state_version >= 0),
    seed BIGINT NOT NULL,
    manifest_hash TEXT NOT NULL,
    config_hash TEXT NOT NULL,
    state_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    equipment_snapshot JSONB NOT NULL DEFAULT '{}'::jsonb,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    checkpointed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (player_profile_id, start_command_id)
);

CREATE TABLE public.authoritative_pve_commands (
    battle_id UUID NOT NULL REFERENCES public.authoritative_pve_battles(battle_id) ON DELETE CASCADE,
    command_id TEXT NOT NULL,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    expected_version INTEGER NOT NULL,
    result_version INTEGER NOT NULL,
    command_kind TEXT NOT NULL,
    request_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    result_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (battle_id, command_id)
);

CREATE TABLE public.profile_capture_events (
    attempt_id UUID PRIMARY KEY,
    battle_id UUID NULL REFERENCES public.authoritative_pve_battles(battle_id) ON DELETE SET NULL,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE
);


DO $fixture_cluster$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET chainbound.cluster = ''devnet''', current_database());
    PERFORM set_config('chainbound.cluster', 'devnet', false);
END
$fixture_cluster$;
