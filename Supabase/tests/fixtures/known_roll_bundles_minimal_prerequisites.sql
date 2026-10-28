-- Test-only baseline for the focused Known-roll bundle suite.
--
-- The repository migration history starts after the dashboard-created MVP
-- schema. Supply only the relations the profile-native Known roll migrations
-- touch, then apply, in order:
--   20261015042331_profile_owned_known_rolls.sql
--   20261019150751_player_lifetime_candy_stats.sql
--   20261027120000_known_roll_bundles.sql
-- and run tests/known_roll_bundles_v1.sql. Never push this fixture to a hosted project.

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pgtap;

CREATE SEQUENCE public.ram_balance_version_sequence AS BIGINT;

CREATE TABLE public.player_profiles (
    player_profile_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    platform_origin TEXT NOT NULL DEFAULT 'unknown',
    last_platform_seen TEXT NOT NULL DEFAULT 'unknown',
    ram_balance BIGINT NOT NULL DEFAULT 0 CHECK (ram_balance BETWEEN 0 AND 4294967295),
    ram_balance_version BIGINT NOT NULL DEFAULT nextval('public.ram_balance_version_sequence'),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.bump_ram_balance_version()
RETURNS TRIGGER
LANGUAGE plpgsql
SET search_path = ''
AS $$
BEGIN
    IF NEW.ram_balance IS DISTINCT FROM OLD.ram_balance THEN
        NEW.ram_balance_version := nextval('public.ram_balance_version_sequence');
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER player_profiles_bump_ram_balance_version
BEFORE UPDATE OF ram_balance ON public.player_profiles
FOR EACH ROW
EXECUTE FUNCTION public.bump_ram_balance_version();

CREATE TABLE public.profile_wallets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_public_key TEXT NOT NULL
);

CREATE TABLE public.player_lifetime_stats (
    player_profile_id UUID PRIMARY KEY REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.ownership_known_moves (
    known_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    idempotency_key TEXT NOT NULL,
    move_id TEXT NOT NULL,
    onchain_move_id INTEGER NOT NULL CHECK (onchain_move_id BETWEEN 0 AND 65535),
    element INTEGER NOT NULL CHECK (element BETWEEN 0 AND 255),
    base_power INTEGER NOT NULL CHECK (base_power BETWEEN 0 AND 65535),
    generator_version INTEGER NOT NULL CHECK (generator_version > 0),
    pool_hash TEXT NOT NULL CHECK (pool_hash ~ '^[0-9a-f]{64}$'),
    onchain_instance_id NUMERIC(20, 0),
    consumed_at TIMESTAMPTZ,
    acquisition TEXT NOT NULL DEFAULT 'candy_roll',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_known_moves_idempotency UNIQUE (player_profile_id, idempotency_key)
);

CREATE TABLE public.ownership_stamp_operations (
    operation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    status TEXT NOT NULL DEFAULT 'prepared'
);

CREATE TYPE public.move_tier AS ENUM ('ordinal', 'inscription');

CREATE TABLE public.move_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    tier public.move_tier NOT NULL,
    move_id TEXT NOT NULL,
    modifier1_id TEXT,
    modifier2_id TEXT,
    locked BOOLEAN NOT NULL DEFAULT FALSE,
    exported_asset_id TEXT,
    source TEXT,
    ownership_known_id UUID REFERENCES public.ownership_known_moves(known_id) ON DELETE RESTRICT,
    CONSTRAINT move_items_ownership_known_id_key UNIQUE (ownership_known_id)
);

CREATE TABLE public.ram_ledger_operations (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    operation_id UUID NOT NULL DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    wallet_address TEXT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    operation_kind TEXT NOT NULL,
    direction TEXT NOT NULL CHECK (direction IN ('credit', 'debit')),
    amount BIGINT NOT NULL CHECK (amount > 0 AND amount <= 4294967295),
    state TEXT NOT NULL DEFAULT 'reserved'
        CHECK (state IN ('reserved', 'chain_submitted', 'chain_confirmed', 'settled', 'released', 'failed', 'manual_review')),
    metadata_json JSONB NOT NULL DEFAULT '{}'::JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL,
    UNIQUE (cluster, operation_id)
);
