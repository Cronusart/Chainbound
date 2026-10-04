-- RAM shop catalog, verified-payment journal, and unified RAM settlement ledger.
-- Production intentionally starts with no catalog rows and checkout disabled.

ALTER TABLE public.player_profiles
    ALTER COLUMN ram_balance TYPE BIGINT USING ram_balance::BIGINT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'player_profiles_ram_balance_u32'
          AND conrelid = 'public.player_profiles'::regclass
    ) THEN
        ALTER TABLE public.player_profiles
            ADD CONSTRAINT player_profiles_ram_balance_u32
            CHECK (ram_balance >= 0 AND ram_balance <= 4294967295);
    END IF;
END $$;

CREATE TABLE public.ram_shop_settings (
    singleton BOOLEAN PRIMARY KEY DEFAULT TRUE CHECK (singleton),
    checkout_enabled BOOLEAN NOT NULL DEFAULT FALSE,
    quote_ttl_seconds INTEGER NULL CHECK (quote_ttl_seconds IS NULL OR quote_ttl_seconds > 0),
    maintenance_reason TEXT NOT NULL DEFAULT 'RAM bundles are not available yet.',
    settlement_commitment TEXT NOT NULL DEFAULT 'finalized'
        CHECK (settlement_commitment = 'finalized'),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.ram_shop_settings(singleton)
VALUES (TRUE)
ON CONFLICT (singleton) DO NOTHING;

CREATE TABLE public.ram_shop_catalog_versions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    version TEXT NOT NULL UNIQUE,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ram_shop_one_enabled_catalog_idx
    ON public.ram_shop_catalog_versions (enabled)
    WHERE enabled;

CREATE TABLE public.ram_shop_bundles (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    catalog_version_id BIGINT NOT NULL
        REFERENCES public.ram_shop_catalog_versions(id) ON DELETE CASCADE,
    sku TEXT NOT NULL,
    display_name TEXT NOT NULL,
    ram_amount BIGINT NOT NULL CHECK (ram_amount > 0 AND ram_amount <= 4294967295),
    sort_order INTEGER NOT NULL DEFAULT 0,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    approved_badge TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (catalog_version_id, sku)
);

CREATE INDEX ram_shop_bundles_catalog_idx
    ON public.ram_shop_bundles (catalog_version_id, enabled, sort_order, id);

CREATE TABLE public.ram_shop_tender_prices (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bundle_id BIGINT NOT NULL REFERENCES public.ram_shop_bundles(id) ON DELETE CASCADE,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    tender TEXT NOT NULL CHECK (tender IN ('SOL', 'USDC', 'SKR')),
    atomic_amount NUMERIC(20, 0) NOT NULL
        CHECK (atomic_amount > 0 AND atomic_amount <= 18446744073709551615),
    mint_address TEXT NULL,
    decimals SMALLINT NOT NULL CHECK (decimals BETWEEN 0 AND 18),
    token_program TEXT NULL,
    destination TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (bundle_id, cluster, tender),
    CHECK (
        (tender = 'SOL' AND mint_address IS NULL AND token_program IS NULL AND decimals = 9)
        OR
        (tender <> 'SOL' AND mint_address IS NOT NULL AND token_program IS NOT NULL)
    ),
    CHECK (
        tender <> 'SKR'
        OR cluster <> 'mainnet-beta'
        OR mint_address = 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3'
    )
);

CREATE INDEX ram_shop_tender_prices_lookup_idx
    ON public.ram_shop_tender_prices (bundle_id, cluster, tender)
    WHERE enabled;

CREATE TABLE public.ram_purchase_orders (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    catalog_version TEXT NOT NULL,
    sku TEXT NOT NULL,
    bundle_name TEXT NOT NULL,
    ram_amount BIGINT NOT NULL CHECK (ram_amount > 0 AND ram_amount <= 4294967295),
    tender TEXT NOT NULL CHECK (tender IN ('SOL', 'USDC', 'SKR')),
    payment_atomic_amount NUMERIC(20, 0) NOT NULL CHECK (payment_atomic_amount > 0),
    mint_address TEXT NULL,
    decimals SMALLINT NOT NULL CHECK (decimals BETWEEN 0 AND 18),
    token_program TEXT NULL,
    destination TEXT NOT NULL,
    reference_address TEXT NOT NULL,
    expected_message_base64 TEXT NOT NULL,
    expected_message_hash TEXT NOT NULL,
    unsigned_transaction_base64 TEXT NOT NULL,
    recent_blockhash TEXT NOT NULL,
    last_valid_block_height BIGINT NOT NULL CHECK (last_valid_block_height > 0),
    estimated_fee_lamports NUMERIC(20, 0) NOT NULL CHECK (estimated_fee_lamports >= 0),
    status TEXT NOT NULL DEFAULT 'quoted'
        CHECK (status IN (
            'quoted', 'submitted', 'payment_verified', 'credit_submitted', 'settled',
            'expired_unpaid', 'rejected', 'failed', 'manual_review'
        )),
    payment_signature TEXT NULL,
    credit_signature TEXT NULL,
    credit_receipt_address TEXT NULL,
    balance_after BIGINT NULL CHECK (balance_after IS NULL OR balance_after >= 0),
    quote_expires_at TIMESTAMPTZ NOT NULL,
    payment_verified_at TIMESTAMPTZ NULL,
    settled_at TIMESTAMPTZ NULL,
    reconcile_after TIMESTAMPTZ NOT NULL DEFAULT now(),
    lease_owner TEXT NULL,
    lease_expires_at TIMESTAMPTZ NULL,
    failure_code TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (cluster, reference_address),
    UNIQUE (cluster, payment_signature),
    UNIQUE (cluster, credit_receipt_address)
);

CREATE INDEX ram_purchase_orders_profile_created_idx
    ON public.ram_purchase_orders (player_profile_id, created_at DESC);
CREATE INDEX ram_purchase_orders_reconcile_idx
    ON public.ram_purchase_orders (status, reconcile_after, id)
    WHERE status IN ('submitted', 'payment_verified', 'credit_submitted');

CREATE TABLE public.ram_payment_attempts (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id BIGINT NOT NULL REFERENCES public.ram_purchase_orders(id) ON DELETE CASCADE,
    signature TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('submitted', 'verified', 'failed', 'unknown')),
    error_code TEXT NULL,
    observed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (order_id, signature),
    UNIQUE (signature)
);

CREATE INDEX ram_payment_attempts_order_idx ON public.ram_payment_attempts (order_id);

CREATE TABLE public.ram_ledger_accounts (
    player_profile_id UUID PRIMARY KEY
        REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
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
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    operation_kind TEXT NOT NULL,
    direction TEXT NOT NULL CHECK (direction IN ('credit', 'debit')),
    amount BIGINT NOT NULL CHECK (amount > 0 AND amount <= 4294967295),
    state TEXT NOT NULL DEFAULT 'reserved'
        CHECK (state IN ('reserved', 'chain_submitted', 'chain_confirmed', 'settled', 'released', 'failed', 'manual_review')),
    chain_signature TEXT NULL,
    receipt_address TEXT NULL,
    metadata_json JSONB NOT NULL DEFAULT '{}'::JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL,
    UNIQUE (cluster, operation_id),
    UNIQUE (cluster, chain_signature),
    UNIQUE (cluster, receipt_address)
);

CREATE INDEX ram_ledger_operations_profile_idx
    ON public.ram_ledger_operations (player_profile_id, created_at DESC);

CREATE TABLE public.ram_ledger_reservations (
    operation_id BIGINT PRIMARY KEY
        REFERENCES public.ram_ledger_operations(id) ON DELETE CASCADE,
    amount BIGINT NOT NULL CHECK (amount > 0),
    state TEXT NOT NULL DEFAULT 'active' CHECK (state IN ('active', 'consumed', 'released')),
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ram_ledger_reservations_active_idx
    ON public.ram_ledger_reservations (operation_id, expires_at)
    WHERE state = 'active';

CREATE TABLE public.ram_reconciliation_cases (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    case_id UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    wallet_address TEXT NOT NULL,
    profile_balance BIGINT NOT NULL CHECK (profile_balance >= 0),
    chain_balance BIGINT NOT NULL CHECK (chain_balance >= 0),
    status TEXT NOT NULL DEFAULT 'open'
        CHECK (status IN ('open', 'approved', 'chain_submitted', 'settled', 'rejected')),
    direction TEXT NULL CHECK (direction IS NULL OR direction IN ('credit_chain', 'debit_chain', 'credit_profile', 'debit_profile')),
    amount BIGINT NULL CHECK (amount IS NULL OR amount > 0),
    reason TEXT NULL,
    approved_by TEXT NULL,
    chain_signature TEXT NULL,
    receipt_address TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL
);

CREATE UNIQUE INDEX ram_reconciliation_open_profile_idx
    ON public.ram_reconciliation_cases (player_profile_id)
    WHERE status IN ('open', 'approved', 'chain_submitted');

INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
SELECT pp.player_profile_id, pw.wallet_public_key, 'devnet', 'pending_audit'
FROM public.player_profiles pp
JOIN public.profile_wallets pw ON pw.player_profile_id = pp.player_profile_id
WHERE pw.is_active = TRUE AND pw.unlinked_at IS NULL
ON CONFLICT (player_profile_id) DO NOTHING;

ALTER TABLE public.ram_shop_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_shop_catalog_versions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_shop_bundles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_shop_tender_prices ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_purchase_orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_payment_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_ledger_accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_ledger_operations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_ledger_reservations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_reconciliation_cases ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.ram_shop_settings FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_shop_catalog_versions FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_shop_bundles FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_shop_tender_prices FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_purchase_orders FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_payment_attempts FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_ledger_accounts FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_ledger_operations FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_ledger_reservations FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_reconciliation_cases FROM PUBLIC, anon, authenticated;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_shop_settings TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_shop_catalog_versions TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_shop_bundles TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_shop_tender_prices TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_purchase_orders TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_payment_attempts TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_accounts TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_operations TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_ledger_reservations TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ram_reconciliation_cases TO service_role;

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO service_role;

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
    v_status TEXT;
BEGIN
    SELECT ram_balance INTO v_profile_balance
    FROM public.player_profiles
    WHERE player_profile_id = p_player_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    IF p_chain_balance < 0 OR p_chain_balance > 4294967295 THEN
        RAISE EXCEPTION 'invalid_chain_balance';
    END IF;

    v_status := CASE WHEN v_profile_balance = p_chain_balance
        THEN 'active' ELSE 'frozen_reconciliation' END;
    INSERT INTO public.ram_ledger_accounts(
        player_profile_id, wallet_address, cluster, status,
        profile_balance_snapshot, chain_balance_snapshot, last_audited_at, updated_at
    ) VALUES (
        p_player_profile_id, p_wallet_address, p_cluster, v_status,
        v_profile_balance, p_chain_balance, now(), now()
    )
    ON CONFLICT (player_profile_id) DO UPDATE SET
        wallet_address = EXCLUDED.wallet_address,
        cluster = EXCLUDED.cluster,
        status = EXCLUDED.status,
        profile_balance_snapshot = EXCLUDED.profile_balance_snapshot,
        chain_balance_snapshot = EXCLUDED.chain_balance_snapshot,
        last_audited_at = EXCLUDED.last_audited_at,
        updated_at = now();

    IF v_status = 'frozen_reconciliation' THEN
        INSERT INTO public.ram_reconciliation_cases(
            player_profile_id, cluster, wallet_address, profile_balance, chain_balance
        ) VALUES (
            p_player_profile_id, p_cluster, p_wallet_address, v_profile_balance, p_chain_balance
        ) ON CONFLICT DO NOTHING;
    END IF;
    RETURN jsonb_build_object(
        'status', v_status,
        'profile_balance', v_profile_balance,
        'chain_balance', p_chain_balance
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
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_balance BIGINT;
    v_reserved BIGINT;
    v_operation public.ram_ledger_operations;
BEGIN
    IF p_amount <= 0 OR p_amount > 4294967295 OR p_expires_at <= now() THEN
        RAISE EXCEPTION 'invalid_reservation';
    END IF;
    PERFORM 1 FROM public.ram_ledger_accounts
    WHERE player_profile_id = p_player_profile_id
      AND wallet_address = p_wallet_address
      AND cluster = p_cluster
      AND status = 'active'
    FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_active'; END IF;

    SELECT ram_balance INTO v_balance FROM public.player_profiles
    WHERE player_profile_id = p_player_profile_id FOR UPDATE;
    SELECT COALESCE(sum(r.amount), 0) INTO v_reserved
    FROM public.ram_ledger_reservations r
    JOIN public.ram_ledger_operations o ON o.id = r.operation_id
    WHERE o.player_profile_id = p_player_profile_id
      AND r.state = 'active' AND r.expires_at > now();
    IF v_balance - v_reserved < p_amount THEN RAISE EXCEPTION 'insufficient_ram'; END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, metadata_json
    ) VALUES (
        p_operation_id, p_player_profile_id, p_wallet_address, p_cluster,
        p_operation_kind, 'debit', p_amount, COALESCE(p_metadata_json, '{}'::JSONB)
    )
    ON CONFLICT (cluster, operation_id) DO UPDATE SET operation_id = EXCLUDED.operation_id
    RETURNING * INTO v_operation;

    INSERT INTO public.ram_ledger_reservations(operation_id, amount, expires_at)
    VALUES (v_operation.id, p_amount, p_expires_at)
    ON CONFLICT (operation_id) DO NOTHING;

    RETURN jsonb_build_object(
        'operation_id', v_operation.operation_id,
        'state', v_operation.state,
        'amount', v_operation.amount,
        'available_balance', v_balance - v_reserved - p_amount
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_ledger_settle_spend(
    p_operation_id UUID,
    p_cluster TEXT,
    p_chain_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ram_ledger_operations;
    v_balance BIGINT;
BEGIN
    SELECT * INTO v_operation FROM public.ram_ledger_operations
    WHERE operation_id = p_operation_id AND cluster = p_cluster FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    IF v_operation.state = 'settled' THEN
        SELECT ram_balance INTO v_balance FROM public.player_profiles
        WHERE player_profile_id = v_operation.player_profile_id;
        RETURN jsonb_build_object('replayed', TRUE, 'balance_after', v_balance);
    END IF;
    IF v_operation.direction <> 'debit' OR v_operation.state NOT IN ('reserved', 'chain_submitted', 'chain_confirmed') THEN
        RAISE EXCEPTION 'invalid_operation_state';
    END IF;
    UPDATE public.player_profiles
    SET ram_balance = ram_balance - v_operation.amount
    WHERE player_profile_id = v_operation.player_profile_id
      AND ram_balance >= v_operation.amount
    RETURNING ram_balance INTO v_balance;
    IF NOT FOUND THEN RAISE EXCEPTION 'insufficient_ram'; END IF;

    UPDATE public.ram_ledger_reservations SET state = 'consumed'
    WHERE operation_id = v_operation.id AND state = 'active';
    UPDATE public.ram_ledger_operations SET
        state = 'settled', chain_signature = p_chain_signature,
        receipt_address = p_receipt_address, settled_at = now(), updated_at = now()
    WHERE id = v_operation.id;
    RETURN jsonb_build_object('replayed', FALSE, 'balance_after', v_balance);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_ledger_release_operation(
    p_operation_id UUID,
    p_cluster TEXT,
    p_failure_code TEXT DEFAULT NULL
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_id BIGINT;
BEGIN
    SELECT id INTO v_id FROM public.ram_ledger_operations
    WHERE operation_id = p_operation_id AND cluster = p_cluster FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    UPDATE public.ram_ledger_reservations SET state = 'released'
    WHERE operation_id = v_id AND state = 'active';
    UPDATE public.ram_ledger_operations SET
        state = 'released', metadata_json = metadata_json || jsonb_build_object('failure_code', p_failure_code),
        updated_at = now()
    WHERE id = v_id AND state = 'reserved';
    RETURN jsonb_build_object('released', TRUE);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_shop_record_submission(
    p_order_id UUID,
    p_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_order public.ram_purchase_orders;
BEGIN
    SELECT * INTO v_order FROM public.ram_purchase_orders
    WHERE order_id = p_order_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.payment_signature IS NOT NULL AND v_order.payment_signature <> p_signature THEN
        RAISE EXCEPTION 'order_signature_conflict';
    END IF;
    INSERT INTO public.ram_payment_attempts(order_id, signature, status)
    VALUES (v_order.id, p_signature, 'submitted')
    ON CONFLICT (order_id, signature) DO NOTHING;
    UPDATE public.ram_purchase_orders SET
        payment_signature = p_signature,
        status = CASE WHEN status = 'quoted' THEN 'submitted' ELSE status END,
        reconcile_after = now(), updated_at = now()
    WHERE id = v_order.id;
    RETURN jsonb_build_object('status', 'submitted', 'signature', p_signature);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_shop_mark_payment_verified(
    p_order_id UUID,
    p_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_order public.ram_purchase_orders;
BEGIN
    SELECT * INTO v_order FROM public.ram_purchase_orders
    WHERE order_id = p_order_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status IN ('payment_verified', 'credit_submitted', 'settled') THEN
        RETURN jsonb_build_object('replayed', TRUE, 'status', v_order.status);
    END IF;
    IF v_order.status <> 'submitted' OR v_order.payment_signature <> p_signature THEN
        RAISE EXCEPTION 'invalid_payment_state';
    END IF;
    UPDATE public.ram_payment_attempts SET status = 'verified'
    WHERE order_id = v_order.id AND signature = p_signature;
    UPDATE public.ram_purchase_orders SET
        status = 'payment_verified', payment_verified_at = now(),
        reconcile_after = now(), updated_at = now()
    WHERE id = v_order.id;
    RETURN jsonb_build_object('replayed', FALSE, 'status', 'payment_verified');
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_shop_mark_credit_submitted(
    p_order_id UUID,
    p_credit_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
DECLARE v_order public.ram_purchase_orders;
BEGIN
    SELECT * INTO v_order FROM public.ram_purchase_orders
    WHERE order_id = p_order_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status = 'settled' THEN
        RETURN jsonb_build_object('replayed', TRUE, 'status', 'settled');
    END IF;
    IF v_order.status NOT IN ('payment_verified', 'credit_submitted') THEN
        RAISE EXCEPTION 'payment_not_verified';
    END IF;
    UPDATE public.ram_purchase_orders SET
        status = 'credit_submitted', credit_signature = p_credit_signature,
        reconcile_after = now(), updated_at = now()
    WHERE id = v_order.id;
    RETURN jsonb_build_object('replayed', v_order.status = 'credit_submitted', 'status', 'credit_submitted');
END;
$$;

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
    v_order public.ram_purchase_orders;
    v_balance BIGINT;
BEGIN
    SELECT * INTO v_order FROM public.ram_purchase_orders
    WHERE order_id = p_order_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'order_not_found'; END IF;
    IF v_order.status = 'settled' THEN
        RETURN jsonb_build_object('replayed', TRUE, 'status', 'settled', 'balance_after', v_order.balance_after);
    END IF;
    IF v_order.status NOT IN ('payment_verified', 'credit_submitted') THEN
        RAISE EXCEPTION 'payment_not_verified';
    END IF;
    PERFORM 1 FROM public.ram_ledger_accounts
    WHERE player_profile_id = v_order.player_profile_id AND status = 'active' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_ledger_not_active'; END IF;

    UPDATE public.player_profiles SET ram_balance = ram_balance + v_order.ram_amount
    WHERE player_profile_id = v_order.player_profile_id
      AND ram_balance + v_order.ram_amount <= 4294967295
    RETURNING ram_balance INTO v_balance;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;

    UPDATE public.ram_purchase_orders SET
        status = 'settled', credit_signature = p_credit_signature,
        credit_receipt_address = p_receipt_address, balance_after = v_balance,
        settled_at = now(), lease_owner = NULL, lease_expires_at = NULL, updated_at = now()
    WHERE id = v_order.id;
    RETURN jsonb_build_object('replayed', FALSE, 'status', 'settled', 'balance_after', v_balance);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_shop_claim_reconcile_batch(
    p_worker_id TEXT,
    p_lease_seconds INTEGER DEFAULT 30,
    p_limit INTEGER DEFAULT 20
) RETURNS SETOF public.ram_purchase_orders
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
BEGIN
    IF p_worker_id IS NULL OR btrim(p_worker_id) = '' OR p_lease_seconds < 1 OR p_limit < 1 OR p_limit > 100 THEN
        RAISE EXCEPTION 'invalid_worker_request';
    END IF;
    RETURN QUERY
    UPDATE public.ram_purchase_orders o SET
        lease_owner = p_worker_id,
        lease_expires_at = now() + make_interval(secs => p_lease_seconds),
        updated_at = now()
    WHERE o.id IN (
        SELECT q.id FROM public.ram_purchase_orders q
        WHERE q.status IN ('submitted', 'payment_verified', 'credit_submitted')
          AND q.reconcile_after <= now()
          AND (q.lease_expires_at IS NULL OR q.lease_expires_at < now())
        ORDER BY q.reconcile_after, q.id
        LIMIT p_limit
        FOR UPDATE SKIP LOCKED
    )
    RETURNING o.*;
END;
$$;

REVOKE ALL ON FUNCTION public.ram_ledger_register_audit(UUID, TEXT, TEXT, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_settle_spend(UUID, TEXT, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_release_operation(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_record_submission(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_mark_credit_submitted(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_settle_purchase(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_shop_claim_reconcile_batch(TEXT, INTEGER, INTEGER) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.ram_ledger_register_audit(UUID, TEXT, TEXT, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_settle_spend(UUID, TEXT, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_release_operation(UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_record_submission(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_payment_verified(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_mark_credit_submitted(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_settle_purchase(UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_shop_claim_reconcile_batch(TEXT, INTEGER, INTEGER) TO service_role;

COMMENT ON TABLE public.ram_shop_settings IS
    'Service-only RAM checkout kill switch. No catalog rows are seeded by this migration.';
COMMENT ON FUNCTION public.ram_shop_claim_reconcile_batch(TEXT, INTEGER, INTEGER) IS
    'Leases due purchase rows for the Edge reconciler. Schedule the reconciler through pg_cron + pg_net using Vault-held credentials after deployment.';
