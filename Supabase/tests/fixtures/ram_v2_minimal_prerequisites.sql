-- Test-only prerequisite schema for the focused RAM v2 migration suites.
--
-- The repository's historical migration chain intentionally starts after the
-- original MVP schema (see Supabase/migrations/README.md), so a blank local
-- database cannot apply every historical migration. This fixture provides
-- only the pre-existing relations used by the RAM v2 migrations. It is not a
-- production baseline and must never be pushed to a hosted project.

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

CREATE TABLE public.ram_purchase_orders (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    catalog_version TEXT NOT NULL DEFAULT 'test',
    sku TEXT NOT NULL DEFAULT 'test',
    bundle_name TEXT NOT NULL DEFAULT 'test',
    ram_amount BIGINT NOT NULL CHECK (ram_amount > 0 AND ram_amount <= 4294967295),
    tender TEXT NOT NULL DEFAULT 'SOL',
    payment_atomic_amount NUMERIC(20, 0) NOT NULL DEFAULT 1,
    mint_address TEXT NULL,
    decimals SMALLINT NOT NULL DEFAULT 9,
    token_program TEXT NULL,
    destination TEXT NOT NULL DEFAULT '11111111111111111111111111111111',
    reference_address TEXT NOT NULL DEFAULT '11111111111111111111111111111111',
    expected_message_base64 TEXT NOT NULL DEFAULT 'test',
    expected_message_hash TEXT NOT NULL DEFAULT repeat('0', 64),
    unsigned_transaction_base64 TEXT NOT NULL DEFAULT 'test',
    recent_blockhash TEXT NOT NULL DEFAULT 'test',
    last_valid_block_height BIGINT NOT NULL DEFAULT 1,
    estimated_fee_lamports NUMERIC(20, 0) NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'quoted',
    payment_signature TEXT NULL,
    credit_signature TEXT NULL,
    credit_receipt_address TEXT NULL,
    balance_after BIGINT NULL,
    quote_expires_at TIMESTAMPTZ NOT NULL DEFAULT now() + interval '5 minutes',
    payment_verified_at TIMESTAMPTZ NULL,
    settled_at TIMESTAMPTZ NULL,
    reconcile_after TIMESTAMPTZ NOT NULL DEFAULT now(),
    lease_owner TEXT NULL,
    lease_expires_at TIMESTAMPTZ NULL,
    failure_code TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

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

CREATE TABLE public.ram_ledger_accounts (
    player_profile_id UUID PRIMARY KEY REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    status TEXT NOT NULL DEFAULT 'pending_audit'
        CHECK (status IN ('pending_audit', 'active', 'frozen_reconciliation')),
    profile_balance_snapshot BIGINT NULL CHECK (profile_balance_snapshot IS NULL OR profile_balance_snapshot >= 0),
    chain_balance_snapshot BIGINT NULL CHECK (chain_balance_snapshot IS NULL OR chain_balance_snapshot >= 0),
    last_audited_at TIMESTAMPTZ NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (cluster, wallet_address)
);

CREATE TABLE public.ram_ledger_operations (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    operation_id UUID NOT NULL DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    operation_kind TEXT NOT NULL,
    direction TEXT NOT NULL CHECK (direction IN ('credit', 'debit')),
    amount BIGINT NOT NULL CHECK (amount > 0 AND amount <= 4294967295),
    state TEXT NOT NULL DEFAULT 'reserved'
        CHECK (state IN ('reserved', 'chain_submitted', 'chain_confirmed', 'settled', 'released', 'failed', 'manual_review')),
    chain_signature TEXT NULL,
    receipt_address TEXT NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL,
    UNIQUE (cluster, operation_id),
    UNIQUE (cluster, chain_signature),
    UNIQUE (cluster, receipt_address)
);

CREATE TABLE public.ram_ledger_reservations (
    operation_id BIGINT PRIMARY KEY REFERENCES public.ram_ledger_operations(id) ON DELETE CASCADE,
    amount BIGINT NOT NULL CHECK (amount > 0),
    state TEXT NOT NULL DEFAULT 'active' CHECK (state IN ('active', 'consumed', 'released')),
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE public.ram_reconciliation_cases (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    case_id UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    wallet_address TEXT NOT NULL,
    profile_balance BIGINT NOT NULL CHECK (profile_balance >= 0),
    chain_balance BIGINT NOT NULL CHECK (chain_balance >= 0),
    status TEXT NOT NULL DEFAULT 'open',
    direction TEXT NULL,
    amount BIGINT NULL,
    reason TEXT NULL,
    approved_by TEXT NULL,
    chain_signature TEXT NULL,
    receipt_address TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL,
    CONSTRAINT ram_reconciliation_cases_status_check
        CHECK (status IN ('open', 'approved', 'chain_submitted', 'settled', 'rejected'))
);

CREATE UNIQUE INDEX ram_reconciliation_open_profile_idx
    ON public.ram_reconciliation_cases(player_profile_id)
    WHERE status IN ('open', 'approved', 'chain_submitted');

-- Subsequent migration sessions see the same marker used by the hosted
-- Devnet rollout guard. The fixture also provides one pre-v2 balance and one
-- legacy mismatch case so the data migration can be asserted rather than
-- merely parsed.
DO $fixture_cluster$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET chainbound.cluster = ''devnet''', current_database());
    PERFORM set_config('chainbound.cluster', 'devnet', false);
END
$fixture_cluster$;

INSERT INTO public.player_profiles(
    player_profile_id, platform_origin, last_platform_seen, ram_balance
) VALUES (
    '34000000-0000-4000-8000-000000000001', 'test', 'test', 90
);
INSERT INTO public.profile_wallets(
    player_profile_id, wallet_public_key, linked_at, is_active
) VALUES (
    '34000000-0000-4000-8000-000000000001',
    '22222222222222222222222222222222', now(), true
);
INSERT INTO public.ram_ledger_accounts(
    player_profile_id, wallet_address, cluster, status,
    profile_balance_snapshot, chain_balance_snapshot, last_audited_at
) VALUES (
    '34000000-0000-4000-8000-000000000001',
    '22222222222222222222222222222222', 'devnet',
    'frozen_reconciliation', 90, 0, now()
);
INSERT INTO public.ram_reconciliation_cases(
    player_profile_id, cluster, wallet_address, profile_balance,
    chain_balance, status, reason
) VALUES (
    '34000000-0000-4000-8000-000000000001', 'devnet',
    '22222222222222222222222222222222', 90, 0, 'open',
    'legacy chain mirror mismatch'
);

ALTER TABLE public.ram_ledger_accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_ledger_operations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_ledger_reservations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_reconciliation_cases ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.ram_ledger_accounts FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_ledger_operations FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_ledger_reservations FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_reconciliation_cases FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_accounts TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_operations TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_reservations TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_reconciliation_cases TO service_role;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO service_role;
