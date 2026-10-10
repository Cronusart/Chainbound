-- RAM v2 phase 1.6: fail-closed rollout authority, replay-safe reserves,
-- active linked-wallet accounts, monotonic balances/receipts, and retirement
-- of chain-mirror account freezing.

CREATE TABLE IF NOT EXISTS public.battle_reward_settings (
    singleton BOOLEAN PRIMARY KEY DEFAULT true CHECK (singleton),
    mode TEXT NOT NULL DEFAULT 'off' CHECK (mode IN ('off', 'shadow', 'grant')),
    cluster TEXT NOT NULL DEFAULT 'disabled'
        CHECK (cluster IN ('disabled', 'devnet', 'mainnet-beta')),
    profile_allowlist UUID[] NOT NULL DEFAULT '{}'::UUID[],
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.battle_reward_settings(singleton, mode, cluster, profile_allowlist)
VALUES (true, 'off', 'disabled', '{}'::UUID[])
ON CONFLICT (singleton) DO NOTHING;

-- Only the explicitly marked Devnet receives a usable cluster seed. Mode still
-- starts off and requires a separate operator action to enter shadow/grant.
DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        UPDATE public.battle_reward_settings
           SET cluster = 'devnet', mode = 'off', updated_at = now()
         WHERE singleton;
    END IF;
END
$devnet_guard$;

ALTER TABLE public.battle_reward_settings ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.battle_reward_settings FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.battle_reward_settings TO service_role;

-- Every server response that carries a RAM balance also carries this one
-- comparable version. A profile update that does not change RAM leaves the
-- version alone; every RAM mutation advances it exactly once.
CREATE SEQUENCE public.ram_balance_version_sequence AS BIGINT;
ALTER TABLE public.player_profiles
    ADD COLUMN ram_balance_version BIGINT NOT NULL
        DEFAULT nextval('public.ram_balance_version_sequence');

ALTER TABLE public.ram_purchase_orders
    ADD COLUMN balance_version BIGINT NULL
        CHECK (balance_version IS NULL OR balance_version > 0);

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

REVOKE ALL ON SEQUENCE public.ram_balance_version_sequence FROM PUBLIC, anon, authenticated;
GRANT USAGE, SELECT ON SEQUENCE public.ram_balance_version_sequence TO service_role;
REVOKE ALL ON FUNCTION public.bump_ram_balance_version() FROM PUBLIC, anon, authenticated;

-- A wallet link is session state; this column is the permanent ownership
-- binding. It is write-once and globally unique, so unlinking cannot make a
-- wallet available to another profile or let a profile swap wallets.
ALTER TABLE public.player_profiles
    ADD COLUMN bound_wallet_address TEXT NULL;

CREATE UNIQUE INDEX player_profiles_bound_wallet_address_key
    ON public.player_profiles(bound_wallet_address)
    WHERE bound_wallet_address IS NOT NULL;

CREATE OR REPLACE FUNCTION public.preserve_bound_wallet_address()
RETURNS TRIGGER
LANGUAGE plpgsql
SET search_path = ''
AS $$
BEGIN
    IF OLD.bound_wallet_address IS NOT NULL
       AND NEW.bound_wallet_address IS DISTINCT FROM OLD.bound_wallet_address THEN
        RAISE EXCEPTION 'wallet_binding_is_permanent';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER player_profiles_preserve_bound_wallet
BEFORE UPDATE OF bound_wallet_address ON public.player_profiles
FOR EACH ROW EXECUTE FUNCTION public.preserve_bound_wallet_address();

REVOKE ALL ON FUNCTION public.preserve_bound_wallet_address() FROM PUBLIC, anon, authenticated;

-- Backfill only histories with exactly one wallet per profile and exactly one
-- profile per wallet. Existing ledger rows must agree. Ambiguous histories are
-- intentionally left NULL for operator review rather than guessed.
DO $devnet_bound_wallet_backfill$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        WITH unambiguous_profiles AS (
            SELECT player_profile_id, MIN(wallet_public_key) AS wallet_public_key
              FROM public.profile_wallets
             GROUP BY player_profile_id
            HAVING COUNT(DISTINCT wallet_public_key) = 1
        ), unambiguous_wallets AS (
            SELECT wallet_public_key
              FROM public.profile_wallets
             GROUP BY wallet_public_key
            HAVING COUNT(DISTINCT player_profile_id) = 1
        )
        UPDATE public.player_profiles AS p
           SET bound_wallet_address = candidate.wallet_public_key
          FROM unambiguous_profiles AS candidate
          JOIN unambiguous_wallets AS wallet
            ON wallet.wallet_public_key = candidate.wallet_public_key
         WHERE p.player_profile_id = candidate.player_profile_id
           AND p.bound_wallet_address IS NULL
           AND NOT EXISTS (
               SELECT 1 FROM public.ram_ledger_accounts AS account
                WHERE account.player_profile_id = p.player_profile_id
                  AND account.wallet_address <> candidate.wallet_public_key
           );
    END IF;
END
$devnet_bound_wallet_backfill$;

CREATE OR REPLACE FUNCTION public.wallet_resolve_bound_profile_service(
    p_wallet_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_profile_id UUID;
BEGIN
    IF NULLIF(BTRIM(p_wallet_address), '') IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_wallet_public_key');
    END IF;
    SELECT player_profile_id INTO v_profile_id
      FROM public.player_profiles
     WHERE bound_wallet_address = p_wallet_address;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', true, 'status', 'no_profile');
    END IF;
    RETURN jsonb_build_object(
        'success', true, 'status', 'profile_found', 'player_profile_id', v_profile_id
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.wallet_bind_profile_service(
    p_player_profile_id UUID,
    p_wallet_address TEXT,
    p_wallet_provider TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_profile_bound TEXT;
    v_wallet_profile UUID;
    v_active_wallet TEXT;
    v_link_id UUID;
    v_first_lock TEXT := LEAST('profile:' || p_player_profile_id::TEXT, 'wallet:' || p_wallet_address);
    v_second_lock TEXT := GREATEST('profile:' || p_player_profile_id::TEXT, 'wallet:' || p_wallet_address);
BEGIN
    IF p_player_profile_id IS NULL OR NULLIF(BTRIM(p_wallet_address), '') IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_wallet_binding');
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(v_first_lock, 0));
    PERFORM pg_advisory_xact_lock(hashtextextended(v_second_lock, 0));

    SELECT bound_wallet_address INTO v_profile_bound
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;
    IF v_profile_bound IS NOT NULL AND v_profile_bound <> p_wallet_address THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_already_bound_to_profile');
    END IF;

    SELECT player_profile_id INTO v_wallet_profile
      FROM public.player_profiles
     WHERE bound_wallet_address = p_wallet_address
     FOR UPDATE;
    IF FOUND AND v_wallet_profile <> p_player_profile_id THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_bound_to_other_profile');
    END IF;

    SELECT wallet_public_key INTO v_active_wallet
      FROM public.profile_wallets
     WHERE player_profile_id = p_player_profile_id
       AND is_active AND unlinked_at IS NULL
     FOR UPDATE;
    IF FOUND AND v_active_wallet <> p_wallet_address THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_already_bound_to_profile');
    END IF;

    IF v_profile_bound IS NULL THEN
        UPDATE public.player_profiles
           SET bound_wallet_address = p_wallet_address
         WHERE player_profile_id = p_player_profile_id
           AND bound_wallet_address IS NULL;
    END IF;

    SELECT id INTO v_link_id
      FROM public.profile_wallets
     WHERE player_profile_id = p_player_profile_id
       AND wallet_public_key = p_wallet_address
     ORDER BY linked_at DESC, id DESC
     LIMIT 1
     FOR UPDATE;
    IF v_link_id IS NULL THEN
        INSERT INTO public.profile_wallets(
            player_profile_id, wallet_provider, wallet_public_key,
            is_active, unlinked_at, metadata_json
        ) VALUES (
            p_player_profile_id, COALESCE(NULLIF(BTRIM(p_wallet_provider), ''), 'other'),
            p_wallet_address, true, NULL, jsonb_build_object('source', 'wallet_connect_verified')
        );
    ELSE
        UPDATE public.profile_wallets
           SET wallet_provider = COALESCE(NULLIF(BTRIM(p_wallet_provider), ''), wallet_provider),
               is_active = true, unlinked_at = NULL,
               metadata_json = COALESCE(metadata_json, '{}'::JSONB)
                   || jsonb_build_object('reactivated_at', now())
         WHERE id = v_link_id;
    END IF;

    RETURN jsonb_build_object(
        'success', true, 'status', 'profile_found',
        'player_profile_id', p_player_profile_id, 'wallet_public_key', p_wallet_address
    );
EXCEPTION WHEN unique_violation THEN
    RETURN jsonb_build_object('success', false, 'error', 'wallet_bound_to_other_profile');
END;
$$;

CREATE OR REPLACE FUNCTION public.wallet_create_bound_profile_service(
    p_auth_user_id TEXT,
    p_current_profile_id UUID,
    p_wallet_address TEXT,
    p_wallet_provider TEXT,
    p_active_platform TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_identity public.profile_identities%ROWTYPE;
    v_existing_profile UUID;
    v_new_profile_id UUID;
    v_first_lock TEXT := LEAST('profile:' || p_current_profile_id::TEXT, 'wallet:' || p_wallet_address);
    v_second_lock TEXT := GREATEST('profile:' || p_current_profile_id::TEXT, 'wallet:' || p_wallet_address);
BEGIN
    IF NULLIF(BTRIM(p_auth_user_id), '') IS NULL OR p_current_profile_id IS NULL
       OR NULLIF(BTRIM(p_wallet_address), '') IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_wallet_binding');
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(v_first_lock, 0));
    PERFORM pg_advisory_xact_lock(hashtextextended(v_second_lock, 0));

    SELECT * INTO v_identity
      FROM public.profile_identities
     WHERE auth_user_id = p_auth_user_id AND revoked_at IS NULL
     FOR UPDATE;
    IF NOT FOUND OR v_identity.player_profile_id <> p_current_profile_id THEN
        RETURN jsonb_build_object('success', false, 'error', 'authenticated_profile_mismatch');
    END IF;

    SELECT player_profile_id INTO v_existing_profile
      FROM public.player_profiles
     WHERE bound_wallet_address = p_wallet_address
     FOR UPDATE;
    IF FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_bound_to_other_profile');
    END IF;

    INSERT INTO public.player_profiles(
        platform_origin, last_platform_seen, bound_wallet_address
    ) VALUES (
        COALESCE(NULLIF(BTRIM(p_active_platform), ''), 'unknown'),
        COALESCE(NULLIF(BTRIM(p_active_platform), ''), 'unknown'), p_wallet_address
    ) RETURNING player_profile_id INTO v_new_profile_id;

    INSERT INTO public.profile_wallets(
        player_profile_id, wallet_provider, wallet_public_key,
        is_active, unlinked_at, metadata_json
    ) VALUES (
        v_new_profile_id, COALESCE(NULLIF(BTRIM(p_wallet_provider), ''), 'other'),
        p_wallet_address, true, NULL,
        jsonb_build_object('source', 'wallet_connect_verified_new_game')
    );

    UPDATE public.profile_identities
       SET player_profile_id = v_new_profile_id,
           metadata_json = COALESCE(metadata_json, '{}'::JSONB)
               || jsonb_build_object('source', 'verified_wallet_new_game',
                                     'previous_profile_id', p_current_profile_id)
     WHERE id = v_identity.id;

    RETURN jsonb_build_object(
        'success', true, 'status', 'profile_created',
        'player_profile_id', v_new_profile_id, 'wallet_public_key', p_wallet_address
    );
EXCEPTION WHEN unique_violation THEN
    RETURN jsonb_build_object('success', false, 'error', 'wallet_bound_to_other_profile');
END;
$$;

REVOKE ALL ON FUNCTION public.wallet_resolve_bound_profile_service(TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.wallet_bind_profile_service(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.wallet_create_bound_profile_service(TEXT, UUID, TEXT, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.wallet_resolve_bound_profile_service(TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.wallet_bind_profile_service(UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.wallet_create_bound_profile_service(TEXT, UUID, TEXT, TEXT, TEXT) TO service_role;

CREATE SEQUENCE public.battle_reward_receipt_sequence AS BIGINT;
REVOKE ALL ON SEQUENCE public.battle_reward_receipt_sequence FROM PUBLIC, anon, authenticated;
GRANT USAGE, SELECT ON SEQUENCE public.battle_reward_receipt_sequence TO service_role;

ALTER TABLE public.battle_reward_settlements
    ADD COLUMN encounter_id TEXT NULL,
    ADD COLUMN is_narrative BOOLEAN NOT NULL DEFAULT false,
    ADD COLUMN balance_before BIGINT NULL
        CHECK (balance_before IS NULL OR balance_before BETWEEN 0 AND 4294967295),
    ADD COLUMN balance_version BIGINT NOT NULL DEFAULT 0
        CHECK (balance_version >= 0),
    ADD COLUMN receipt_sequence BIGINT NOT NULL
        DEFAULT nextval('public.battle_reward_receipt_sequence');

UPDATE public.battle_reward_settlements
   SET balance_before = COALESCE(balance_after - applied_amount, balance_after, 0)
 WHERE balance_before IS NULL;
ALTER TABLE public.battle_reward_settlements ALTER COLUMN balance_before SET NOT NULL;

ALTER TABLE public.authoritative_pve_battles
    ADD COLUMN reward_gate_reason TEXT NULL
        CHECK (reward_gate_reason IS NULL OR reward_gate_reason IN ('rewards_disabled', 'replay_not_eligible')),
    DROP CONSTRAINT authoritative_pve_battles_reward_tier_check,
    ADD CONSTRAINT authoritative_pve_battles_reward_tier_check
        CHECK (reward_tier IS NULL OR reward_tier IN ('none', 'standard', 'elite', 'boss')),
    DROP CONSTRAINT authoritative_pve_battles_reward_config_version_check,
    ADD CONSTRAINT authoritative_pve_battles_reward_config_version_check CHECK (
        reward_config_version IS NULL
        OR (reward_tier = 'none' AND reward_config_version = 0)
        OR (reward_tier IN ('standard', 'elite', 'boss') AND reward_config_version > 0)
    );

ALTER TABLE public.battle_reward_settlements
    DROP CONSTRAINT battle_reward_settlements_reward_tier_check,
    ADD CONSTRAINT battle_reward_settlements_reward_tier_check
        CHECK (reward_tier IN ('none', 'standard', 'elite', 'boss')),
    DROP CONSTRAINT battle_reward_settlements_reward_config_version_check,
    ADD CONSTRAINT battle_reward_settlements_reward_config_version_check
        CHECK (reward_config_version >= 0),
    ADD CONSTRAINT battle_reward_balance_equation CHECK (
        balance_after IS NULL OR balance_after = balance_before + applied_amount
    ),
    ADD CONSTRAINT battle_reward_outcome_amount_consistency CHECK (
        terminal_outcome = 'victory' OR applied_amount = 0
    );

CREATE UNIQUE INDEX battle_reward_narrative_once_idx
    ON public.battle_reward_settlements(player_profile_id, encounter_id)
    WHERE is_narrative AND status = 'settled' AND applied_amount > 0;

-- Chain snapshots are diagnostic only. They can no longer freeze the canonical
-- Postgres account or prevent earn/spend paths.
ALTER TABLE public.ram_reconciliation_cases
    DROP CONSTRAINT ram_reconciliation_cases_status_check,
    ADD CONSTRAINT ram_reconciliation_cases_status_check
        CHECK (status IN ('open', 'approved', 'chain_submitted', 'settled', 'rejected', 'superseded_v2'));

CREATE OR REPLACE FUNCTION public.ram_ledger_register_audit(
    p_player_profile_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT,
    p_chain_balance BIGINT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_profile_balance BIGINT;
    v_account public.ram_ledger_accounts%ROWTYPE;
BEGIN
    SELECT ram_balance INTO v_profile_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
       AND bound_wallet_address = p_wallet_address
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_scope_mismatch'; END IF;
    IF p_chain_balance < 0 OR p_chain_balance > 4294967295 THEN
        RAISE EXCEPTION 'invalid_chain_balance';
    END IF;

    INSERT INTO public.ram_ledger_accounts(
        player_profile_id, wallet_address, cluster, status,
        profile_balance_snapshot, chain_balance_snapshot, last_audited_at, updated_at
    ) VALUES (
        p_player_profile_id, p_wallet_address, p_cluster, 'active',
        v_profile_balance, p_chain_balance, now(), now()
    )
    ON CONFLICT DO NOTHING;

    SELECT * INTO v_account
      FROM public.ram_ledger_accounts
     WHERE player_profile_id = p_player_profile_id
       AND wallet_address = p_wallet_address
       AND cluster = p_cluster;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_scope_mismatch'; END IF;

    UPDATE public.ram_ledger_accounts
       SET status = 'active',
           profile_balance_snapshot = v_profile_balance,
           chain_balance_snapshot = p_chain_balance,
           last_audited_at = now(),
           updated_at = now()
     WHERE player_profile_id = p_player_profile_id;

    RETURN jsonb_build_object(
        'status', 'active',
        'canonical', 'postgres',
        'mismatchObserved', v_profile_balance <> p_chain_balance,
        'profile_balance', v_profile_balance,
        'chain_balance', p_chain_balance
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_ledger_ensure_account(
    p_player_profile_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_profile_balance BIGINT;
    v_bound_wallet TEXT;
    v_account public.ram_ledger_accounts%ROWTYPE;
BEGIN
    IF p_player_profile_id IS NULL
       OR NULLIF(BTRIM(p_wallet_address), '') IS NULL
       OR p_cluster NOT IN ('devnet', 'mainnet-beta') THEN
        RAISE EXCEPTION 'invalid_ram_ledger_scope';
    END IF;

    SELECT p.ram_balance, p.bound_wallet_address
      INTO v_profile_balance, v_bound_wallet
      FROM public.player_profiles AS p
     WHERE p.player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    IF v_bound_wallet IS DISTINCT FROM p_wallet_address THEN
        RAISE EXCEPTION 'ram_ledger_scope_mismatch';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.profile_wallets AS w
         WHERE w.player_profile_id = p_player_profile_id
           AND w.wallet_public_key = p_wallet_address
           AND w.is_active AND w.unlinked_at IS NULL
    ) THEN
        RAISE EXCEPTION 'wallet_not_linked_to_profile';
    END IF;

    INSERT INTO public.ram_ledger_accounts(
        player_profile_id, wallet_address, cluster, status,
        profile_balance_snapshot, chain_balance_snapshot,
        last_audited_at, updated_at
    ) VALUES (
        p_player_profile_id, p_wallet_address, p_cluster, 'active',
        v_profile_balance, NULL, NULL, now()
    )
    ON CONFLICT DO NOTHING;

    SELECT * INTO v_account
      FROM public.ram_ledger_accounts
     WHERE player_profile_id = p_player_profile_id
       AND wallet_address = p_wallet_address
       AND cluster = p_cluster;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_scope_mismatch'; END IF;

    RETURN jsonb_build_object(
        'status', v_account.status,
        'canonical', 'postgres',
        'profile_balance', v_profile_balance,
        'wallet_address', v_account.wallet_address,
        'cluster', v_account.cluster
    );
END;
$$;

-- The old all-ones wallet is a real linked Devnet profile, not an orphan.
-- More generally, activate every linked legacy row and create any missing
-- account without consulting Solana or opening a reconciliation case.
DO $devnet_ledger_accounts$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        UPDATE public.ram_ledger_accounts AS a
           SET status = 'active', updated_at = now()
          FROM public.profile_wallets AS w
         WHERE w.player_profile_id = a.player_profile_id
           AND w.wallet_public_key = a.wallet_address
           AND EXISTS (
               SELECT 1 FROM public.player_profiles AS bound_profile
                WHERE bound_profile.player_profile_id = a.player_profile_id
                  AND bound_profile.bound_wallet_address = a.wallet_address
           )
           AND w.is_active
           AND w.unlinked_at IS NULL
           AND a.cluster = 'devnet'
           AND a.status <> 'active';

        INSERT INTO public.ram_ledger_accounts(
            player_profile_id, wallet_address, cluster, status,
            profile_balance_snapshot, chain_balance_snapshot,
            last_audited_at, updated_at
        )
        SELECT p.player_profile_id, w.wallet_public_key, 'devnet', 'active',
               p.ram_balance, NULL, NULL, now()
          FROM public.player_profiles AS p
          JOIN public.profile_wallets AS w
            ON w.player_profile_id = p.player_profile_id
           AND w.wallet_public_key = p.bound_wallet_address
           AND w.is_active
           AND w.unlinked_at IS NULL
          LEFT JOIN public.ram_ledger_accounts AS a
            ON a.player_profile_id = p.player_profile_id
         WHERE a.player_profile_id IS NULL
        ON CONFLICT DO NOTHING;
    END IF;
END
$devnet_ledger_accounts$;

CREATE OR REPLACE FUNCTION public.ram_shop_settle_purchase(
    p_order_id UUID,
    p_credit_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_order public.ram_purchase_orders%ROWTYPE;
    v_balance BIGINT;
    v_balance_version BIGINT;
BEGIN
    SELECT * INTO v_order FROM public.ram_purchase_orders
     WHERE order_id = p_order_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status = 'settled' THEN
        RETURN jsonb_build_object(
            'replayed', TRUE, 'status', 'settled',
            'balance_after', v_order.balance_after,
            'balance_version', v_order.balance_version
        );
    END IF;
    IF v_order.status NOT IN ('payment_verified', 'credit_submitted') THEN
        RAISE EXCEPTION 'payment_not_verified';
    END IF;
    PERFORM 1 FROM public.ram_ledger_accounts
     WHERE player_profile_id = v_order.player_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_active'; END IF;

    UPDATE public.player_profiles
       SET ram_balance = ram_balance + v_order.ram_amount
     WHERE player_profile_id = v_order.player_profile_id
       AND ram_balance + v_order.ram_amount <= 4294967295
     RETURNING ram_balance, ram_balance_version INTO v_balance, v_balance_version;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;

    UPDATE public.ram_purchase_orders SET
        status = 'settled', credit_signature = p_credit_signature,
        credit_receipt_address = p_receipt_address,
        balance_after = v_balance, balance_version = v_balance_version,
        settled_at = now(), lease_owner = NULL, lease_expires_at = NULL, updated_at = now()
     WHERE id = v_order.id;
    RETURN jsonb_build_object(
        'replayed', FALSE, 'status', 'settled',
        'balance_after', v_balance, 'balance_version', v_balance_version
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_ledger_reserve_operation(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT,
    p_operation_kind TEXT,
    p_amount BIGINT,
    p_expires_at TIMESTAMPTZ,
    p_metadata_json JSONB DEFAULT '{}'::JSONB
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_balance BIGINT;
    v_reserved BIGINT;
    v_operation public.ram_ledger_operations;
    v_reservation public.ram_ledger_reservations;
    v_metadata JSONB := COALESCE(p_metadata_json, '{}'::JSONB);
BEGIN
    IF p_operation_id IS NULL OR p_amount <= 0 OR p_amount > 4294967295
       OR p_expires_at <= now() OR NULLIF(BTRIM(p_operation_kind), '') IS NULL THEN
        RAISE EXCEPTION 'invalid_reservation';
    END IF;

    PERFORM 1
      FROM public.ram_ledger_accounts
     WHERE player_profile_id = p_player_profile_id
       AND wallet_address = p_wallet_address
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_active'; END IF;

    SELECT ram_balance
      INTO v_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;

    SELECT *
      INTO v_operation
      FROM public.ram_ledger_operations
     WHERE cluster = p_cluster
       AND operation_id = p_operation_id
     FOR UPDATE;

    IF FOUND THEN
        IF v_operation.player_profile_id IS DISTINCT FROM p_player_profile_id
           OR v_operation.wallet_address IS DISTINCT FROM p_wallet_address
           OR v_operation.operation_kind IS DISTINCT FROM p_operation_kind
           OR v_operation.direction IS DISTINCT FROM 'debit'
           OR v_operation.amount IS DISTINCT FROM p_amount
           OR v_operation.metadata_json IS DISTINCT FROM v_metadata THEN
            RAISE EXCEPTION 'operation_id_conflict';
        END IF;

        SELECT * INTO v_reservation
          FROM public.ram_ledger_reservations
         WHERE operation_id = v_operation.id
         FOR UPDATE;
        IF NOT FOUND THEN RAISE EXCEPTION 'operation_reservation_missing'; END IF;
        IF v_operation.state IN ('released', 'failed', 'manual_review')
           OR v_reservation.state = 'released' THEN
            RAISE EXCEPTION 'operation_terminal';
        END IF;

        IF v_operation.state = 'reserved'
           AND v_reservation.state = 'active'
           AND v_reservation.expires_at <= now() THEN
            SELECT COALESCE(SUM(r.amount), 0)
              INTO v_reserved
              FROM public.ram_ledger_reservations r
              JOIN public.ram_ledger_operations o ON o.id = r.operation_id
             WHERE o.player_profile_id = p_player_profile_id
               AND r.operation_id <> v_operation.id
               AND r.state = 'active'
               AND r.expires_at > now();
            IF v_balance - v_reserved < p_amount THEN RAISE EXCEPTION 'insufficient_ram'; END IF;
            UPDATE public.ram_ledger_reservations
               SET expires_at = p_expires_at
             WHERE operation_id = v_operation.id;
        END IF;

        SELECT COALESCE(SUM(r.amount), 0)
          INTO v_reserved
          FROM public.ram_ledger_reservations r
          JOIN public.ram_ledger_operations o ON o.id = r.operation_id
         WHERE o.player_profile_id = p_player_profile_id
           AND r.state = 'active'
           AND r.expires_at > now();

        RETURN jsonb_build_object(
            'operation_id', v_operation.operation_id,
            'state', v_operation.state,
            'amount', v_operation.amount,
            'available_balance', GREATEST(0, v_balance - v_reserved),
            'replayed', TRUE
        );
    END IF;

    SELECT COALESCE(SUM(r.amount), 0)
      INTO v_reserved
      FROM public.ram_ledger_reservations r
      JOIN public.ram_ledger_operations o ON o.id = r.operation_id
     WHERE o.player_profile_id = p_player_profile_id
       AND r.state = 'active'
       AND r.expires_at > now();

    IF v_balance - v_reserved < p_amount THEN RAISE EXCEPTION 'insufficient_ram'; END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, metadata_json
    ) VALUES (
        p_operation_id, p_player_profile_id, p_wallet_address, p_cluster,
        p_operation_kind, 'debit', p_amount, v_metadata
    )
    RETURNING * INTO v_operation;

    INSERT INTO public.ram_ledger_reservations(operation_id, amount, expires_at)
    VALUES (v_operation.id, p_amount, p_expires_at);

    RETURN jsonb_build_object(
        'operation_id', v_operation.operation_id,
        'state', v_operation.state,
        'amount', v_operation.amount,
        'available_balance', v_balance - v_reserved - p_amount,
        'replayed', FALSE
    );
END;
$$;

-- Supersede all chain-mirror reconciliation cases on the marked Devnet. This
-- includes H68e while remaining idempotent and inert on Mainnet Smoke.
DO $devnet_h68$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        UPDATE public.ram_reconciliation_cases
           SET status = 'superseded_v2',
               reason = concat_ws('; ', NULLIF(reason, ''), 'Postgres canonical under RAM v2'),
               settled_at = COALESCE(settled_at, now()),
               updated_at = now()
         WHERE status IN ('open', 'approved', 'chain_submitted');
        UPDATE public.ram_ledger_accounts
           SET status = 'active', updated_at = now()
         WHERE status = 'frozen_reconciliation';
    END IF;
END
$devnet_h68$;

CREATE OR REPLACE FUNCTION public.chainbound_reward_cluster_is_safe(p_cluster TEXT)
RETURNS BOOLEAN LANGUAGE sql IMMUTABLE PARALLEL SAFE
SET search_path = ''
AS $$ SELECT p_cluster = 'devnet' $$;
REVOKE ALL ON FUNCTION public.chainbound_reward_cluster_is_safe(TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.chainbound_reward_cluster_is_safe(TEXT) TO service_role;

CREATE OR REPLACE FUNCTION public.authoritative_begin_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_start_command_id TEXT,
    p_battle_kind TEXT,
    p_seed BIGINT,
    p_manifest_hash TEXT,
    p_config_hash TEXT,
    p_encounter_id TEXT,
    p_reward_tier TEXT,
    p_reward_config_version INTEGER,
    p_reward_config_hash TEXT,
    p_state_json JSONB,
    p_equipment_snapshot JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_existing public.authoritative_pve_battles%ROWTYPE;
    v_reward_tier TEXT := p_reward_tier;
    v_reward_config_version INTEGER := p_reward_config_version;
    v_reward_config_hash TEXT := p_reward_config_hash;
    v_reward_gate_reason TEXT;
BEGIN
    IF NULLIF(btrim(p_start_command_id), '') IS NULL
       OR NULLIF(btrim(p_encounter_id), '') IS NULL
       OR p_reward_tier NOT IN ('none', 'standard', 'elite', 'boss') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_battle_authority_stamp');
    END IF;

    -- A no-reward battle has one canonical stamp regardless of which catalog
    -- version the caller currently has. Normalize before replay comparison so
    -- retries remain idempotent across BattleService and database callers.
    IF v_reward_tier = 'none' THEN
        v_reward_config_version := 0;
        v_reward_config_hash := repeat('0', 64);
        v_reward_gate_reason := 'replay_not_eligible';
    END IF;

    -- Battle availability is independent of the reward faucet. When the
    -- supplied reward configuration is absent or disabled, persist an
    -- explicit zero-tier stamp instead of rejecting battle creation.
    IF v_reward_tier <> 'none' AND NOT EXISTS (
        SELECT 1 FROM public.battle_reward_configs
         WHERE config_version = p_reward_config_version
           AND config_hash = p_reward_config_hash
           AND enabled
    ) THEN
        v_reward_tier := 'none';
        v_reward_config_version := 0;
        v_reward_config_hash := repeat('0', 64);
        v_reward_gate_reason := 'rewards_disabled';
    END IF;

    PERFORM 1 FROM public.player_profiles
     WHERE player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found'); END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(p_profile_id::TEXT, 0));
    SELECT * INTO v_existing FROM public.authoritative_pve_battles
     WHERE player_profile_id = p_profile_id
       AND status IN ('active', 'capturing', 'service_interrupted')
     FOR UPDATE;
    IF FOUND THEN
        IF v_existing.start_command_id = p_start_command_id THEN
            IF v_existing.encounter_id IS DISTINCT FROM p_encounter_id
               OR v_existing.reward_tier IS DISTINCT FROM v_reward_tier
               OR v_existing.reward_config_version IS DISTINCT FROM v_reward_config_version
               OR v_existing.reward_config_hash IS DISTINCT FROM v_reward_config_hash THEN
                RETURN jsonb_build_object('ok', false, 'code', 'battle_authority_stamp_conflict');
            END IF;
            RETURN jsonb_build_object(
                'ok', true, 'already_applied', true,
                'battle_id', v_existing.battle_id,
                'state_version', v_existing.state_version
            );
        END IF;
        RETURN jsonb_build_object(
            'ok', false, 'code', 'battle_already_active',
            'battle_id', v_existing.battle_id,
            'state_version', v_existing.state_version
        );
    END IF;

    INSERT INTO public.authoritative_pve_battles (
        battle_id, player_profile_id, start_command_id, status, battle_kind,
        state_version, seed, manifest_hash, config_hash, encounter_id,
        reward_tier, reward_config_version, reward_config_hash, reward_gate_reason,
        state_json, equipment_snapshot, expires_at
    ) VALUES (
        p_battle_id, p_profile_id, p_start_command_id, 'active', p_battle_kind,
        0, p_seed, p_manifest_hash, p_config_hash, p_encounter_id,
        v_reward_tier, v_reward_config_version, v_reward_config_hash, v_reward_gate_reason,
        COALESCE(p_state_json, '{}'::jsonb), COALESCE(p_equipment_snapshot, '{}'::jsonb),
        to_timestamp(p_expires_at_unix_milliseconds / 1000.0)
    );
    RETURN jsonb_build_object('ok', true, 'battle_id', p_battle_id, 'state_version', 0);
END;
$$;

CREATE OR REPLACE FUNCTION public.battle_reward_get_service(p_battle_id UUID)
RETURNS JSONB LANGUAGE sql SECURITY DEFINER SET search_path = '' STABLE
AS $$
    SELECT CASE WHEN s.settlement_id IS NULL THEN NULL ELSE jsonb_build_object(
        'status', s.status, 'battleId', s.battle_id, 'settlementId', s.settlement_id,
        'terminalOutcome', s.terminal_outcome,
        'requestedRam', s.requested_amount, 'appliedRam', s.applied_amount,
        'reason', s.reason, 'rewardTier', s.reward_tier,
        'rewardConfigVersion', s.reward_config_version,
        'rewardConfigHash', s.reward_config_hash,
        'dailyEarned', s.daily_earned, 'dailyCap', s.daily_cap,
        'balanceBefore', s.balance_before, 'balanceAfter', s.balance_after,
        'balanceVersion', s.balance_version,
        'receiptSequence', s.receipt_sequence,
        'settledAt', to_char(s.settled_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        'alreadyApplied', true
    ) END
      FROM public.battle_reward_settlements s
     WHERE s.battle_id = p_battle_id;
$$;

CREATE OR REPLACE FUNCTION public.battle_reward_classify_service(
    p_battle_id UUID,
    p_gate_reason TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_config public.battle_reward_configs%ROWTYPE;
    v_balance BIGINT;
    v_balance_version BIGINT;
    v_requested BIGINT := 0;
    v_tier TEXT;
    v_config_version INTEGER;
    v_config_hash TEXT;
    v_is_narrative BOOLEAN;
BEGIN
    IF p_gate_reason IS NOT NULL AND p_gate_reason NOT IN
        ('rewards_disabled', 'shadow_mode', 'not_allowlisted', 'plausibility_rejected') THEN
        RAISE EXCEPTION 'invalid_reward_gate_reason';
    END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;
    SELECT ram_balance, ram_balance_version INTO v_balance, v_balance_version FROM public.player_profiles
     WHERE player_profile_id = v_battle.player_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id FOR UPDATE;
    IF EXISTS (SELECT 1 FROM public.battle_reward_settlements WHERE battle_id = p_battle_id) THEN
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', true);
    END IF;
    IF v_battle.status NOT IN ('completed', 'lost', 'abandoned')
       OR v_battle.terminal_outcome IS NULL THEN
        RAISE EXCEPTION 'battle_not_terminal';
    END IF;

    v_tier := COALESCE(v_battle.reward_tier, 'none');
    v_config_version := COALESCE(v_battle.reward_config_version, 0);
    v_config_hash := COALESCE(v_battle.reward_config_hash, repeat('0', 64));
    v_is_narrative := v_battle.battle_kind IN ('boss', 'trainer', 'scripted');
    SELECT * INTO v_config FROM public.battle_reward_configs
     WHERE config_version = v_battle.reward_config_version
       AND config_hash = v_battle.reward_config_hash;
    IF FOUND THEN
        v_requested := CASE v_tier
            WHEN 'standard' THEN v_config.standard_amount
            WHEN 'elite' THEN v_config.elite_amount
            WHEN 'boss' THEN v_config.boss_amount ELSE 0 END;
    END IF;

    IF p_gate_reason IS NULL THEN
        RETURN jsonb_build_object(
            'status', 'eligible', 'battleId', p_battle_id,
            'terminalOutcome', v_battle.terminal_outcome,
            'requestedRam', v_requested, 'appliedRam', 0,
            'reason', 'eligible', 'rewardTier', v_tier,
            'rewardConfigVersion', v_config_version,
            'rewardConfigHash', v_config_hash,
            'balanceBefore', v_balance, 'balanceAfter', v_balance,
            'balanceVersion', v_balance_version,
            'alreadyApplied', false
        );
    END IF;

    INSERT INTO public.battle_reward_settlements(
        battle_id, player_profile_id, encounter_id, is_narrative,
        terminal_outcome, reward_tier, reward_config_version, reward_config_hash,
        requested_amount, applied_amount, status, reason,
        balance_before, balance_after, balance_version, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, v_battle.encounter_id, v_is_narrative,
        v_battle.terminal_outcome, v_tier, v_config_version, v_config_hash,
        v_requested, 0, 'zero', p_gate_reason,
        v_balance, v_balance, v_balance_version, now()
    );
    RETURN public.battle_reward_get_service(p_battle_id)
        || jsonb_build_object('alreadyApplied', false);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_battle_reward_settle_service(p_battle_id UUID)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_existing public.battle_reward_settlements%ROWTYPE;
    v_config public.battle_reward_configs%ROWTYPE;
    v_settings public.battle_reward_settings%ROWTYPE;
    v_daily public.ram_daily_earnings%ROWTYPE;
    v_operation public.ram_ledger_operations%ROWTYPE;
    v_balance_before BIGINT;
    v_balance_after BIGINT;
    v_balance_version BIGINT;
    v_requested BIGINT := 0;
    v_applied BIGINT := 0;
    v_tier TEXT;
    v_config_version INTEGER;
    v_config_hash TEXT;
    v_zero_reason TEXT;
    v_day DATE := (now() AT TIME ZONE 'UTC')::DATE;
    v_daily_cap CONSTANT BIGINT := 100;
    v_is_narrative BOOLEAN;
BEGIN
    SELECT * INTO v_battle FROM public.authoritative_pve_battles WHERE battle_id = p_battle_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;

    -- Global order: profile -> battle -> settlement -> daily -> operation.
    SELECT ram_balance, ram_balance_version INTO v_balance_before, v_balance_version FROM public.player_profiles
     WHERE player_profile_id = v_battle.player_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id FOR UPDATE;
    SELECT * INTO v_existing FROM public.battle_reward_settlements
     WHERE battle_id = p_battle_id FOR UPDATE;
    IF FOUND THEN
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', true);
    END IF;
    IF v_battle.status NOT IN ('completed', 'lost', 'abandoned')
       OR v_battle.terminal_outcome IS NULL THEN
        RAISE EXCEPTION 'battle_not_terminal';
    END IF;

    v_tier := COALESCE(v_battle.reward_tier, 'none');
    v_config_version := COALESCE(v_battle.reward_config_version, 0);
    v_config_hash := COALESCE(v_battle.reward_config_hash, repeat('0', 64));
    v_is_narrative := v_battle.battle_kind IN ('boss', 'trainer', 'scripted');

    IF v_battle.reward_tier IS NULL THEN
        v_zero_reason := 'legacy_battle';
    ELSIF v_tier = 'none' THEN
        v_zero_reason := COALESCE(v_battle.reward_gate_reason, 'replay_not_eligible');
    ELSE
        SELECT * INTO v_config FROM public.battle_reward_configs
         WHERE config_version = v_battle.reward_config_version
           AND config_hash = v_battle.reward_config_hash AND enabled;
        IF NOT FOUND THEN
            v_zero_reason := 'rewards_disabled';
        ELSE
            v_requested := CASE v_tier
                WHEN 'standard' THEN v_config.standard_amount
                WHEN 'elite' THEN v_config.elite_amount
                WHEN 'boss' THEN v_config.boss_amount ELSE 0 END;
        END IF;
    END IF;

    IF v_zero_reason IS NULL AND v_battle.terminal_outcome <> 'victory' THEN
        v_zero_reason := v_battle.terminal_outcome;
    END IF;
    IF v_zero_reason IS NULL AND v_is_narrative AND EXISTS (
        SELECT 1 FROM public.battle_reward_settlements s
         WHERE s.player_profile_id = v_battle.player_profile_id
           AND s.encounter_id = v_battle.encounter_id
           AND s.is_narrative AND s.status = 'settled' AND s.applied_amount > 0
    ) THEN
        v_zero_reason := 'replay_not_eligible';
    END IF;

    SELECT * INTO v_settings FROM public.battle_reward_settings WHERE singleton;
    IF v_zero_reason IS NULL AND NOT FOUND THEN v_zero_reason := 'settings_unavailable'; END IF;
    IF v_zero_reason IS NULL AND v_settings.mode = 'off' THEN v_zero_reason := 'rewards_disabled'; END IF;
    IF v_zero_reason IS NULL AND v_settings.mode = 'shadow' THEN v_zero_reason := 'shadow_mode'; END IF;
    IF v_zero_reason IS NULL AND NOT public.chainbound_reward_cluster_is_safe(v_settings.cluster) THEN
        v_zero_reason := 'unsafe_cluster';
    END IF;
    IF v_zero_reason IS NULL AND NOT (v_battle.player_profile_id = ANY(v_settings.profile_allowlist)) THEN
        v_zero_reason := 'not_allowlisted';
    END IF;

    IF v_zero_reason IS NOT NULL THEN
        INSERT INTO public.battle_reward_settlements(
            battle_id, player_profile_id, encounter_id, is_narrative,
            terminal_outcome, reward_tier, reward_config_version, reward_config_hash,
            requested_amount, applied_amount, status, reason,
            balance_before, balance_after, balance_version, settled_at
        ) VALUES (
            p_battle_id, v_battle.player_profile_id, v_battle.encounter_id, v_is_narrative,
            v_battle.terminal_outcome, v_tier, v_config_version, v_config_hash,
            v_requested, 0, 'zero', v_zero_reason,
            v_balance_before, v_balance_before, v_balance_version, now()
        );
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', false);
    END IF;

    INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
    VALUES (v_battle.player_profile_id, v_day, 0)
    ON CONFLICT (player_profile_id, utc_day) DO NOTHING;
    SELECT * INTO v_daily FROM public.ram_daily_earnings
     WHERE player_profile_id = v_battle.player_profile_id AND utc_day = v_day FOR UPDATE;
    SELECT * INTO v_operation FROM public.ram_ledger_operations
     WHERE cluster = v_settings.cluster AND operation_id = p_battle_id FOR UPDATE;
    IF FOUND THEN RAISE EXCEPTION 'battle_reward_operation_conflict'; END IF;

    v_applied := LEAST(v_requested, GREATEST(0, v_daily_cap - v_daily.earned));
    IF v_applied = 0 THEN
        INSERT INTO public.battle_reward_settlements(
            battle_id, player_profile_id, encounter_id, is_narrative,
            terminal_outcome, reward_tier, reward_config_version, reward_config_hash,
            requested_amount, applied_amount, status, reason, utc_day, daily_earned,
            daily_cap, balance_before, balance_after, balance_version, settled_at
        ) VALUES (
            p_battle_id, v_battle.player_profile_id, v_battle.encounter_id, v_is_narrative,
            v_battle.terminal_outcome, v_tier, v_config_version, v_config_hash,
            v_requested, 0, 'zero', 'daily_cap', v_day, v_daily.earned,
            v_daily_cap, v_balance_before, v_balance_before, v_balance_version, now()
        );
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', false);
    END IF;
    IF v_balance_before + v_applied > 4294967295 THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, state, metadata_json, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, NULL, v_settings.cluster,
        'battle_reward', 'credit', v_applied, 'settled',
        jsonb_build_object('battle_id', p_battle_id, 'terminal_outcome', v_battle.terminal_outcome,
            'reward_tier', v_tier, 'reward_config_version', v_config_version,
            'reward_config_hash', v_config_hash, 'utc_day', v_day), now()
    ) RETURNING * INTO v_operation;
    UPDATE public.ram_daily_earnings SET earned = earned + v_applied, updated_at = now()
     WHERE player_profile_id = v_battle.player_profile_id AND utc_day = v_day RETURNING * INTO v_daily;
    UPDATE public.player_profiles SET ram_balance = ram_balance + v_applied
     WHERE player_profile_id = v_battle.player_profile_id
     RETURNING ram_balance, ram_balance_version INTO v_balance_after, v_balance_version;

    INSERT INTO public.battle_reward_settlements(
        battle_id, player_profile_id, encounter_id, is_narrative,
        terminal_outcome, reward_tier, reward_config_version, reward_config_hash,
        requested_amount, applied_amount, status, reason, ram_ledger_operation_id,
        utc_day, daily_earned, daily_cap, balance_before, balance_after, balance_version, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, v_battle.encounter_id, v_is_narrative,
        v_battle.terminal_outcome, v_tier, v_config_version, v_config_hash,
        v_requested, v_applied, 'settled', 'granted', v_operation.id,
        v_day, v_daily.earned, v_daily_cap, v_balance_before, v_balance_after, v_balance_version, now()
    );
    RETURN public.battle_reward_get_service(p_battle_id)
        || jsonb_build_object('alreadyApplied', false);
END;
$$;

-- The standalone terminal stamp is obsolete: terminal state is stamped only by
-- the same transaction that commits a terminal command/capture/forfeit.
DROP FUNCTION public.battle_reward_stamp_terminal_service(UUID, UUID, TEXT, TEXT);

REVOKE ALL ON FUNCTION public.battle_reward_get_service(UUID) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.battle_reward_classify_service(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_battle_reward_settle_service(UUID) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_register_audit(UUID, TEXT, TEXT, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_ensure_account(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.battle_reward_get_service(UUID) TO service_role;
GRANT EXECUTE ON FUNCTION public.battle_reward_classify_service(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_battle_reward_settle_service(UUID) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_register_audit(UUID, TEXT, TEXT, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_ensure_account(UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB) TO service_role;

COMMENT ON TABLE public.battle_reward_settings IS
    'Singleton database rollout authority. Defaults off; grant is Devnet plus explicit profile allowlist only.';
COMMENT ON FUNCTION public.ram_ledger_register_audit(UUID, TEXT, TEXT, BIGINT) IS
    'Diagnostic chain snapshot only. Postgres is canonical and a mismatch never freezes the account.';
COMMENT ON FUNCTION public.ram_ledger_ensure_account(UUID, TEXT, TEXT) IS
    'Service-only idempotent creation of an active Postgres-canonical RAM account for a verified active wallet link.';
