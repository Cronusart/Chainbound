-- Test-only baseline for the focused Bindery Phase 1 migration suite.
--
-- The repository migration history starts after the dashboard-created MVP
-- schema. Supply only the legacy relations required by ownership Bind
-- coordination. Never push this fixture to a hosted project.

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
    unlinked_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE public.chainlings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    species_id TEXT NOT NULL,
    is_party_member BOOLEAN NOT NULL DEFAULT FALSE,
    party_slot SMALLINT,
    level INTEGER NOT NULL DEFAULT 1,
    xp BIGINT NOT NULL DEFAULT 0,
    ivs JSONB NOT NULL DEFAULT '[]'::jsonb,
    rebirth_iv_bonuses JSONB NOT NULL DEFAULT '[]'::jsonb,
    stars INTEGER NOT NULL DEFAULT 0,
    rebirth_count INTEGER NOT NULL DEFAULT 0,
    evolution_stage INTEGER NOT NULL DEFAULT 1,
    visual_variant_id TEXT NOT NULL DEFAULT 'base',
    visual_identity JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.player_lifetime_stats (
    player_profile_id UUID PRIMARY KEY REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    total_ram_spent BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
