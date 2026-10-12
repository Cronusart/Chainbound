-- Reservation-backed Bind preparation and replay-safe settlement.
-- Canonical remote migration version: 20261006110715.
-- The approved source registry is intentionally empty in production migrations.

CREATE TABLE public.ownership_bind_sources (
    source_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    chainling_id UUID NOT NULL REFERENCES public.chainlings(id) ON DELETE CASCADE,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    onchain_chainling_id NUMERIC(20, 0) NOT NULL CHECK (
        onchain_chainling_id BETWEEN 0 AND 18446744073709551615
    ),
    cover_asset_address TEXT NOT NULL,
    component_asset_address TEXT NOT NULL,
    component_kind SMALLINT NOT NULL CHECK (component_kind IN (0, 1)),
    archive_name TEXT NOT NULL CHECK (octet_length(archive_name) BETWEEN 1 AND 64),
    archive_uri TEXT NOT NULL CHECK (archive_uri LIKE 'https://%' AND octet_length(archive_uri) <= 200),
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    consumed_at TIMESTAMPTZ,
    consumed_operation_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_bind_source_assets_distinct CHECK (cover_asset_address <> component_asset_address),
    CONSTRAINT ownership_bind_source_profile_chainling UNIQUE (player_profile_id, chainling_id),
    CONSTRAINT ownership_bind_source_chain UNIQUE (cluster, wallet_address, onchain_chainling_id),
    CONSTRAINT ownership_bind_source_cover UNIQUE (cluster, cover_asset_address),
    CONSTRAINT ownership_bind_source_component UNIQUE (cluster, component_asset_address)
);

CREATE TABLE public.ownership_bind_operations (
    operation_id UUID PRIMARY KEY,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    source_id UUID NOT NULL REFERENCES public.ownership_bind_sources(source_id) ON DELETE RESTRICT,
    chainling_id UUID NOT NULL REFERENCES public.chainlings(id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    idempotency_key TEXT NOT NULL CHECK (octet_length(idempotency_key) BETWEEN 1 AND 128),
    onchain_chainling_id NUMERIC(20, 0) NOT NULL,
    cover_asset_address TEXT NOT NULL,
    component_asset_address TEXT NOT NULL,
    component_kind SMALLINT NOT NULL CHECK (component_kind IN (0, 1)),
    archive_name TEXT NOT NULL,
    archive_uri TEXT NOT NULL,
    reservation_address TEXT NOT NULL,
    chainling_address TEXT NOT NULL,
    archive_asset_address TEXT NOT NULL,
    receipt_address TEXT NOT NULL,
    transaction_base64 TEXT NOT NULL,
    recent_blockhash TEXT NOT NULL,
    last_valid_block_height NUMERIC(20, 0) NOT NULL CHECK (last_valid_block_height >= 0),
    estimated_fee_lamports NUMERIC(20, 0) NOT NULL CHECK (estimated_fee_lamports >= 0),
    expires_at TIMESTAMPTZ NOT NULL,
    status TEXT NOT NULL DEFAULT 'prepared' CHECK (
        status IN ('prepared', 'submitted', 'chain_confirmed', 'settled', 'failed')
    ),
    signature TEXT,
    failure_code TEXT,
    submitted_at TIMESTAMPTZ,
    chain_confirmed_at TIMESTAMPTZ,
    settled_at TIMESTAMPTZ,
    last_reconciled_at TIMESTAMPTZ,
    reconcile_after TIMESTAMPTZ NOT NULL DEFAULT now(),
    lease_owner TEXT,
    lease_expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_bind_operation_idempotency UNIQUE (player_profile_id, idempotency_key),
    CONSTRAINT ownership_bind_operation_receipt UNIQUE (cluster, receipt_address),
    CONSTRAINT ownership_bind_operation_archive UNIQUE (cluster, archive_asset_address)
);

CREATE UNIQUE INDEX ownership_bind_operation_signature_unique
    ON public.ownership_bind_operations(cluster, signature)
    WHERE signature IS NOT NULL;
CREATE UNIQUE INDEX ownership_bind_operation_live_source_unique
    ON public.ownership_bind_operations(source_id)
    WHERE status <> 'failed';
CREATE INDEX ownership_bind_operation_recovery
    ON public.ownership_bind_operations(status, updated_at)
    WHERE status IN ('prepared', 'submitted', 'chain_confirmed');

CREATE TABLE public.ownership_chainling_bindings (
    binding_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    operation_id UUID NOT NULL UNIQUE REFERENCES public.ownership_bind_operations(operation_id) ON DELETE RESTRICT,
    source_id UUID NOT NULL UNIQUE REFERENCES public.ownership_bind_sources(source_id) ON DELETE RESTRICT,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    chainling_id UUID NOT NULL UNIQUE REFERENCES public.chainlings(id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    onchain_chainling_id NUMERIC(20, 0) NOT NULL,
    cover_asset_address TEXT NOT NULL,
    component_asset_address TEXT NOT NULL,
    component_kind SMALLINT NOT NULL CHECK (component_kind IN (0, 1)),
    archive_asset_address TEXT NOT NULL,
    receipt_address TEXT NOT NULL,
    signature TEXT NOT NULL,
    bound_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_chainling_binding_chain UNIQUE (cluster, wallet_address, onchain_chainling_id),
    CONSTRAINT ownership_chainling_binding_archive UNIQUE (cluster, archive_asset_address),
    CONSTRAINT ownership_chainling_binding_receipt UNIQUE (cluster, receipt_address),
    CONSTRAINT ownership_chainling_binding_signature UNIQUE (cluster, signature)
);

ALTER TABLE public.ownership_bind_sources
    ADD CONSTRAINT ownership_bind_source_consumed_operation_fk
    FOREIGN KEY (consumed_operation_id)
    REFERENCES public.ownership_bind_operations(operation_id)
    ON DELETE RESTRICT;

ALTER TABLE public.ownership_bind_sources ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ownership_bind_operations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ownership_chainling_bindings ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.ownership_bind_sources FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ownership_bind_operations FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ownership_chainling_bindings FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ownership_bind_sources TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ownership_bind_operations TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.ownership_chainling_bindings TO service_role;

CREATE OR REPLACE FUNCTION public.ownership_prepare_bind_operation(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT,
    p_source_id UUID,
    p_idempotency_key TEXT,
    p_expires_at TIMESTAMPTZ,
    p_reservation_address TEXT,
    p_chainling_address TEXT,
    p_archive_asset_address TEXT,
    p_receipt_address TEXT,
    p_transaction_base64 TEXT,
    p_recent_blockhash TEXT,
    p_last_valid_block_height NUMERIC,
    p_estimated_fee_lamports NUMERIC
) RETURNS public.ownership_bind_operations
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_source public.ownership_bind_sources%ROWTYPE;
    v_existing public.ownership_bind_operations%ROWTYPE;
    v_result public.ownership_bind_operations%ROWTYPE;
BEGIN
    IF p_operation_id IS NULL OR p_source_id IS NULL OR p_expires_at <= now()
       OR NULLIF(BTRIM(p_idempotency_key), '') IS NULL
       OR octet_length(p_idempotency_key) > 128
       OR NULLIF(BTRIM(p_transaction_base64), '') IS NULL THEN
        RAISE EXCEPTION 'invalid_bind_prepare';
    END IF;

    SELECT * INTO v_existing
      FROM public.ownership_bind_operations
     WHERE operation_id = p_operation_id
        OR (player_profile_id = p_player_profile_id AND idempotency_key = p_idempotency_key)
     ORDER BY created_at
     LIMIT 1
     FOR UPDATE;
    IF FOUND THEN
        IF v_existing.operation_id IS DISTINCT FROM p_operation_id
           OR v_existing.player_profile_id IS DISTINCT FROM p_player_profile_id
           OR v_existing.wallet_address IS DISTINCT FROM p_wallet_address
           OR v_existing.cluster IS DISTINCT FROM p_cluster
           OR v_existing.source_id IS DISTINCT FROM p_source_id THEN
            RAISE EXCEPTION 'bind_operation_conflict';
        END IF;
        IF v_existing.status <> 'prepared' OR v_existing.signature IS NOT NULL THEN
            RETURN v_existing;
        END IF;
    END IF;

    SELECT * INTO v_source
      FROM public.ownership_bind_sources
     WHERE source_id = p_source_id
     FOR UPDATE;
    IF NOT FOUND OR NOT v_source.enabled THEN RAISE EXCEPTION 'bind_source_not_available'; END IF;
    IF v_source.consumed_at IS NOT NULL
       OR v_source.player_profile_id IS DISTINCT FROM p_player_profile_id
       OR v_source.wallet_address IS DISTINCT FROM p_wallet_address
       OR v_source.cluster IS DISTINCT FROM p_cluster THEN
        RAISE EXCEPTION 'bind_source_mismatch';
    END IF;

    PERFORM public.ram_ledger_reserve_operation(
        p_operation_id,
        p_player_profile_id,
        p_wallet_address,
        p_cluster,
        'bind',
        250,
        p_expires_at,
        jsonb_build_object(
            'idempotency_key', p_idempotency_key,
            'source_id', p_source_id,
            'chainling_id', v_source.chainling_id,
            'onchain_chainling_id', v_source.onchain_chainling_id,
            'cover_asset_address', v_source.cover_asset_address,
            'component_asset_address', v_source.component_asset_address,
            'component_kind', v_source.component_kind
        )
    );

    IF v_existing.operation_id IS NOT NULL THEN
        UPDATE public.ownership_bind_operations
           SET reservation_address = p_reservation_address,
               chainling_address = p_chainling_address,
               archive_asset_address = p_archive_asset_address,
               receipt_address = p_receipt_address,
               transaction_base64 = p_transaction_base64,
               recent_blockhash = p_recent_blockhash,
               last_valid_block_height = p_last_valid_block_height,
               estimated_fee_lamports = p_estimated_fee_lamports,
               expires_at = p_expires_at,
               failure_code = NULL,
               updated_at = now()
         WHERE operation_id = p_operation_id
         RETURNING * INTO v_result;
        RETURN v_result;
    END IF;

    INSERT INTO public.ownership_bind_operations(
        operation_id, player_profile_id, source_id, chainling_id,
        wallet_address, cluster, idempotency_key, onchain_chainling_id,
        cover_asset_address, component_asset_address, component_kind,
        archive_name, archive_uri, reservation_address, chainling_address,
        archive_asset_address, receipt_address, transaction_base64,
        recent_blockhash, last_valid_block_height, estimated_fee_lamports,
        expires_at
    ) VALUES (
        p_operation_id, p_player_profile_id, p_source_id, v_source.chainling_id,
        p_wallet_address, p_cluster, p_idempotency_key, v_source.onchain_chainling_id,
        v_source.cover_asset_address, v_source.component_asset_address, v_source.component_kind,
        v_source.archive_name, v_source.archive_uri, p_reservation_address, p_chainling_address,
        p_archive_asset_address, p_receipt_address, p_transaction_base64,
        p_recent_blockhash, p_last_valid_block_height, p_estimated_fee_lamports,
        p_expires_at
    ) RETURNING * INTO v_result;
    RETURN v_result;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_mark_bind_submitted(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_signature TEXT
) RETURNS public.ownership_bind_operations
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_operation public.ownership_bind_operations%ROWTYPE;
BEGIN
    IF NULLIF(BTRIM(p_signature), '') IS NULL THEN RAISE EXCEPTION 'signature_required'; END IF;
    SELECT * INTO v_operation
      FROM public.ownership_bind_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_operation_not_found'; END IF;
    IF v_operation.status = 'settled' THEN RETURN v_operation; END IF;
    IF v_operation.status NOT IN ('prepared', 'submitted', 'chain_confirmed') THEN
        RAISE EXCEPTION 'bind_operation_not_submittable';
    END IF;
    IF v_operation.signature IS NOT NULL AND v_operation.signature IS DISTINCT FROM p_signature THEN
        RAISE EXCEPTION 'bind_signature_conflict';
    END IF;
    PERFORM public.ram_ledger_mark_chain_submitted(p_operation_id, p_cluster, p_signature);
    UPDATE public.ownership_bind_operations
       SET status = CASE WHEN status = 'prepared' THEN 'submitted' ELSE status END,
           signature = COALESCE(signature, p_signature),
           submitted_at = COALESCE(submitted_at, now()),
           last_reconciled_at = now(),
           reconcile_after = now(),
           updated_at = now()
     WHERE operation_id = p_operation_id
     RETURNING * INTO v_operation;
    RETURN v_operation;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_settle_bind_reserved(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_signature TEXT,
    p_receipt_address TEXT,
    p_archive_asset_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ownership_bind_operations%ROWTYPE;
    v_source public.ownership_bind_sources%ROWTYPE;
    v_existing public.ownership_chainling_bindings%ROWTYPE;
    v_settlement JSONB;
    v_balance BIGINT;
    v_now TIMESTAMPTZ := now();
BEGIN
    IF NULLIF(BTRIM(p_signature), '') IS NULL
       OR NULLIF(BTRIM(p_receipt_address), '') IS NULL
       OR NULLIF(BTRIM(p_archive_asset_address), '') IS NULL THEN
        RAISE EXCEPTION 'verified_bind_evidence_required';
    END IF;
    SELECT * INTO v_operation
      FROM public.ownership_bind_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_operation_not_found'; END IF;
    IF v_operation.receipt_address IS DISTINCT FROM p_receipt_address
       OR v_operation.archive_asset_address IS DISTINCT FROM p_archive_asset_address THEN
        RAISE EXCEPTION 'bind_receipt_mismatch';
    END IF;

    SELECT * INTO v_existing
      FROM public.ownership_chainling_bindings
     WHERE operation_id = p_operation_id
     FOR UPDATE;
    IF FOUND THEN
        IF v_operation.status <> 'settled' THEN RAISE EXCEPTION 'bind_row_precedes_ledger_settlement'; END IF;
        SELECT ram_balance INTO v_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_player_profile_id;
        RETURN jsonb_build_object('replayed', TRUE, 'ram_balance', v_balance, 'binding', to_jsonb(v_existing));
    END IF;
    IF v_operation.status NOT IN ('prepared', 'submitted', 'chain_confirmed') THEN
        RAISE EXCEPTION 'bind_operation_not_reconcilable';
    END IF;

    SELECT * INTO v_source
      FROM public.ownership_bind_sources
     WHERE source_id = v_operation.source_id
     FOR UPDATE;
    IF NOT FOUND OR v_source.consumed_at IS NOT NULL THEN RAISE EXCEPTION 'bind_source_consumed'; END IF;

    v_settlement := public.ram_ledger_settle_spend(
        p_operation_id, p_cluster, p_signature, p_receipt_address
    );

    INSERT INTO public.ownership_chainling_bindings(
        operation_id, source_id, player_profile_id, chainling_id,
        wallet_address, cluster, onchain_chainling_id, cover_asset_address,
        component_asset_address, component_kind, archive_asset_address,
        receipt_address, signature, bound_at
    ) VALUES (
        v_operation.operation_id, v_operation.source_id, v_operation.player_profile_id,
        v_operation.chainling_id, v_operation.wallet_address, v_operation.cluster,
        v_operation.onchain_chainling_id, v_operation.cover_asset_address,
        v_operation.component_asset_address, v_operation.component_kind,
        p_archive_asset_address, p_receipt_address, p_signature, v_now
    ) RETURNING * INTO v_existing;

    UPDATE public.ownership_bind_sources
       SET consumed_at = v_now, consumed_operation_id = p_operation_id, updated_at = v_now
     WHERE source_id = v_operation.source_id;
    UPDATE public.ownership_bind_operations
       SET status = 'settled', signature = p_signature,
           submitted_at = COALESCE(submitted_at, v_now),
           chain_confirmed_at = COALESCE(chain_confirmed_at, v_now),
           settled_at = v_now, last_reconciled_at = v_now,
           reconcile_after = v_now, lease_owner = NULL, lease_expires_at = NULL,
           failure_code = NULL, updated_at = v_now
     WHERE operation_id = p_operation_id;
    INSERT INTO public.player_lifetime_stats(player_profile_id, total_ram_spent)
    VALUES (p_player_profile_id, 250)
    ON CONFLICT (player_profile_id) DO UPDATE
      SET total_ram_spent = public.player_lifetime_stats.total_ram_spent + 250,
          updated_at = v_now;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'ram_balance', (v_settlement ->> 'balance_after')::BIGINT,
        'binding', to_jsonb(v_existing)
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_release_bind_operation(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_failure_code TEXT
) RETURNS public.ownership_bind_operations
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_operation public.ownership_bind_operations%ROWTYPE;
BEGIN
    SELECT * INTO v_operation
      FROM public.ownership_bind_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'bind_operation_not_found'; END IF;
    IF v_operation.status IN ('settled', 'failed') THEN RETURN v_operation; END IF;
    PERFORM public.ram_ledger_release_operation(p_operation_id, p_cluster, p_failure_code);
    UPDATE public.ownership_bind_operations
       SET status = 'failed', failure_code = p_failure_code,
           last_reconciled_at = now(), reconcile_after = now(),
           lease_owner = NULL, lease_expires_at = NULL, updated_at = now()
     WHERE operation_id = p_operation_id
     RETURNING * INTO v_operation;
    RETURN v_operation;
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_bind_claim_reconcile_batch(
    p_worker_id TEXT,
    p_lease_seconds INTEGER DEFAULT 30,
    p_limit INTEGER DEFAULT 20
) RETURNS SETOF public.ownership_bind_operations
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    IF NULLIF(BTRIM(p_worker_id), '') IS NULL
       OR p_lease_seconds < 1 OR p_limit < 1 OR p_limit > 100 THEN
        RAISE EXCEPTION 'invalid_worker_request';
    END IF;
    RETURN QUERY
    WITH claimed AS (
        SELECT operation_id
          FROM public.ownership_bind_operations
         WHERE status IN ('prepared', 'submitted', 'chain_confirmed')
           AND reconcile_after <= now()
           AND (lease_expires_at IS NULL OR lease_expires_at <= now())
         ORDER BY reconcile_after, created_at
         FOR UPDATE SKIP LOCKED
         LIMIT p_limit
    )
    UPDATE public.ownership_bind_operations operation
       SET lease_owner = p_worker_id,
           lease_expires_at = now() + make_interval(secs => p_lease_seconds),
           updated_at = now()
      FROM claimed
     WHERE operation.operation_id = claimed.operation_id
    RETURNING operation.*;
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_prepare_bind_operation(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, TIMESTAMPTZ, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, NUMERIC, NUMERIC
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_mark_bind_submitted(UUID, UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_settle_bind_reserved(UUID, UUID, TEXT, TEXT, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_release_bind_operation(UUID, UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_bind_claim_reconcile_batch(TEXT, INTEGER, INTEGER)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_prepare_bind_operation(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, TIMESTAMPTZ, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, NUMERIC, NUMERIC
) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_mark_bind_submitted(UUID, UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_settle_bind_reserved(UUID, UUID, TEXT, TEXT, TEXT, TEXT)
    TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_release_bind_operation(UUID, UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_bind_claim_reconcile_batch(TEXT, INTEGER, INTEGER) TO service_role;
