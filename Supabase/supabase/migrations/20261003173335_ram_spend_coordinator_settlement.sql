-- Replay-safe settlement for reservation-backed RAM spends.
-- Service-only functions deliberately use an empty search_path and lock the
-- ledger/profile rows before making an idempotent state transition.

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
       AND status = 'active'
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

CREATE OR REPLACE FUNCTION public.ram_ledger_mark_chain_submitted(
    p_operation_id UUID,
    p_cluster TEXT,
    p_chain_signature TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_operation public.ram_ledger_operations;
BEGIN
    IF NULLIF(BTRIM(p_chain_signature), '') IS NULL THEN RAISE EXCEPTION 'signature_required'; END IF;
    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    IF v_operation.state = 'settled' THEN
        RETURN jsonb_build_object('state', v_operation.state, 'replayed', TRUE);
    END IF;
    IF v_operation.state NOT IN ('reserved', 'chain_submitted', 'chain_confirmed') THEN
        RAISE EXCEPTION 'invalid_operation_state';
    END IF;
    IF v_operation.chain_signature IS NOT NULL
       AND v_operation.chain_signature IS DISTINCT FROM p_chain_signature THEN
        RAISE EXCEPTION 'operation_signature_conflict';
    END IF;
    UPDATE public.ram_ledger_operations
       SET state = CASE WHEN state = 'reserved' THEN 'chain_submitted' ELSE state END,
           chain_signature = COALESCE(chain_signature, p_chain_signature),
           updated_at = now()
     WHERE id = v_operation.id;
    RETURN jsonb_build_object('state', 'chain_submitted', 'replayed', v_operation.chain_signature IS NOT NULL);
END;
$$;

CREATE OR REPLACE FUNCTION public.ownership_settle_known_reserved(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_chain_signature TEXT,
    p_receipt_address TEXT,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_onchain_instance_id NUMERIC,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ram_ledger_operations;
    v_existing public.ownership_known_moves%ROWTYPE;
    v_inserted public.ownership_known_moves%ROWTYPE;
    v_settlement JSONB;
    v_balance BIGINT;
BEGIN
    IF NULLIF(BTRIM(p_chain_signature), '') IS NULL
       OR NULLIF(BTRIM(p_receipt_address), '') IS NULL
       OR NULLIF(BTRIM(p_idempotency_key), '') IS NULL THEN
        RAISE EXCEPTION 'invalid_known_settlement';
    END IF;

    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    IF v_operation.player_profile_id IS DISTINCT FROM p_player_profile_id
       OR v_operation.operation_kind IS DISTINCT FROM 'known_roll'
       OR v_operation.direction IS DISTINCT FROM 'debit'
       OR v_operation.amount <> 5
       OR COALESCE(v_operation.metadata_json ->> 'idempotency_key', '') IS DISTINCT FROM p_idempotency_key THEN
        RAISE EXCEPTION 'known_operation_mismatch';
    END IF;

    SELECT * INTO v_existing
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = p_idempotency_key
     FOR UPDATE;
    IF FOUND THEN
        IF v_existing.move_id IS DISTINCT FROM p_move_id
           OR v_existing.onchain_move_id IS DISTINCT FROM p_onchain_move_id
           OR v_existing.onchain_instance_id IS DISTINCT FROM p_onchain_instance_id THEN
            RAISE EXCEPTION 'known_idempotency_conflict';
        END IF;
        IF v_operation.state <> 'settled' THEN
            RAISE EXCEPTION 'known_row_precedes_ledger_settlement';
        END IF;
        SELECT ram_balance INTO v_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_player_profile_id;
        RETURN jsonb_build_object(
            'replayed', TRUE,
            'known', to_jsonb(v_existing),
            'ram_balance', v_balance,
            'ram_cost', 5
        );
    END IF;

    v_settlement := public.ram_ledger_settle_spend(
        p_operation_id,
        p_cluster,
        p_chain_signature,
        p_receipt_address
    );

    INSERT INTO public.ownership_known_moves(
        player_profile_id, idempotency_key, move_id, onchain_move_id,
        onchain_instance_id, element, base_power, generator_version,
        pool_hash, acquisition
    ) VALUES (
        p_player_profile_id, p_idempotency_key, p_move_id, p_onchain_move_id,
        p_onchain_instance_id, p_element, p_base_power, p_generator_version,
        p_pool_hash, 'candy_roll'
    )
    RETURNING * INTO v_inserted;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'known', to_jsonb(v_inserted),
        'ram_balance', (v_settlement ->> 'balance_after')::BIGINT,
        'ram_cost', 5
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_ledger_mark_chain_submitted(UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_settle_known_reserved(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_roll_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) FROM service_role;

GRANT EXECUTE ON FUNCTION public.ram_ledger_reserve_operation(UUID, UUID, TEXT, TEXT, TEXT, BIGINT, TIMESTAMPTZ, JSONB)
    TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_ledger_mark_chain_submitted(UUID, TEXT, TEXT)
    TO service_role;
GRANT EXECUTE ON FUNCTION public.ownership_settle_known_reserved(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, NUMERIC, INTEGER, INTEGER, INTEGER, TEXT
) TO service_role;

CREATE OR REPLACE FUNCTION public.ram_ledger_release_operation(
    p_operation_id UUID,
    p_cluster TEXT,
    p_failure_code TEXT DEFAULT NULL
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ram_ledger_operations;
BEGIN
    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'operation_not_found'; END IF;
    IF v_operation.state = 'settled' THEN
        RETURN jsonb_build_object('released', FALSE, 'state', 'settled', 'replayed', TRUE);
    END IF;
    IF v_operation.state = 'released' THEN
        RETURN jsonb_build_object('released', TRUE, 'state', 'released', 'replayed', TRUE);
    END IF;
    IF v_operation.state NOT IN ('reserved', 'chain_submitted') THEN
        RAISE EXCEPTION 'operation_not_releasable';
    END IF;
    IF v_operation.state = 'chain_submitted' AND NULLIF(BTRIM(p_failure_code), '') IS NULL THEN
        RAISE EXCEPTION 'verified_failure_required';
    END IF;

    UPDATE public.ram_ledger_reservations
       SET state = 'released'
     WHERE operation_id = v_operation.id
       AND state = 'active';

    UPDATE public.ram_ledger_operations
       SET state = 'released',
           metadata_json = metadata_json || jsonb_build_object('failure_code', p_failure_code),
           updated_at = now()
     WHERE id = v_operation.id;

    RETURN jsonb_build_object('released', TRUE, 'state', 'released', 'replayed', FALSE);
END;
$$;

REVOKE ALL ON FUNCTION public.ram_ledger_release_operation(UUID, TEXT, TEXT)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_ledger_release_operation(UUID, TEXT, TEXT)
    TO service_role;

CREATE OR REPLACE FUNCTION public.ownership_confirm_stamp_reserved(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_idempotency_key TEXT,
    p_known_id UUID,
    p_signature TEXT,
    p_verified_asset_json JSONB,
    p_onchain_instance_id NUMERIC,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ownership_stamp_operations%ROWTYPE;
    v_ledger public.ram_ledger_operations%ROWTYPE;
    v_known public.ownership_known_moves%ROWTYPE;
    v_settlement JSONB;
    v_ram_balance BIGINT;
    v_now TIMESTAMPTZ := now();
BEGIN
    IF NULLIF(BTRIM(p_signature), '') IS NULL
       OR NULLIF(BTRIM(p_receipt_address), '') IS NULL
       OR p_verified_asset_json IS NULL THEN
        RAISE EXCEPTION 'verified_stamp_evidence_required';
    END IF;

    SELECT * INTO v_operation
      FROM public.ownership_stamp_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND idempotency_key = p_idempotency_key
     FOR UPDATE;
    IF NOT FOUND OR v_operation.known_id <> p_known_id THEN
        RAISE EXCEPTION 'stamp_operation_not_found';
    END IF;

    SELECT * INTO v_ledger
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id
       AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND
       OR v_ledger.player_profile_id IS DISTINCT FROM p_player_profile_id
       OR v_ledger.operation_kind IS DISTINCT FROM 'stamp'
       OR v_ledger.direction IS DISTINCT FROM 'debit'
       OR v_ledger.amount <> 75 THEN
        RAISE EXCEPTION 'stamp_ledger_operation_mismatch';
    END IF;

    IF v_operation.status = 'confirmed' THEN
        IF v_ledger.state <> 'settled' THEN RAISE EXCEPTION 'stamp_ledger_not_settled'; END IF;
        SELECT ram_balance INTO v_ram_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_player_profile_id;
        RETURN jsonb_build_object(
            'replayed', TRUE,
            'ram_balance', v_ram_balance,
            'signature', v_operation.signature,
            'asset', v_operation.verified_asset_json
        );
    END IF;

    IF v_operation.status NOT IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending') THEN
        RAISE EXCEPTION 'stamp_operation_not_reconcilable';
    END IF;

    SELECT * INTO v_known
      FROM public.ownership_known_moves
     WHERE known_id = p_known_id
       AND player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND OR v_known.consumed_at IS NOT NULL THEN
        RAISE EXCEPTION 'known_not_found_or_consumed';
    END IF;

    v_settlement := public.ram_ledger_settle_spend(
        p_operation_id,
        p_cluster,
        p_signature,
        p_receipt_address
    );

    UPDATE public.ownership_known_moves
       SET consumed_at = v_now,
           onchain_instance_id = p_onchain_instance_id
     WHERE known_id = p_known_id;

    UPDATE public.ownership_stamp_operations
       SET status = 'confirmed',
           signature = p_signature,
           verified_asset_json = p_verified_asset_json,
           chain_confirmed_at = COALESCE(chain_confirmed_at, v_now),
           last_reconciled_at = v_now,
           confirmed_at = v_now,
           failure_code = NULL,
           updated_at = v_now
     WHERE operation_id = p_operation_id;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'ram_balance', (v_settlement ->> 'balance_after')::BIGINT,
        'signature', p_signature,
        'asset', p_verified_asset_json
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_atomic(
    UUID, TEXT, UUID, TEXT, JSONB, NUMERIC, INTEGER
) FROM service_role;
GRANT EXECUTE ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) TO service_role;

CREATE UNIQUE INDEX IF NOT EXISTS ram_ledger_one_open_rebirth_per_chainling_idx
    ON public.ram_ledger_operations(
        player_profile_id,
        (metadata_json ->> 'chainling_id')
    )
    WHERE operation_kind = 'rebirth'
      AND state IN ('reserved', 'chain_submitted', 'chain_confirmed');

CREATE OR REPLACE FUNCTION public.profile_prepare_rebirth_operation(
    p_operation_id UUID,
    p_profile_id UUID,
    p_wallet_address TEXT,
    p_cluster TEXT,
    p_chainling_id UUID,
    p_stat_index INTEGER,
    p_cost INTEGER,
    p_expires_at TIMESTAMPTZ,
    p_metadata_json JSONB
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_reserved JSONB;
BEGIN
    IF p_stat_index < 0 OR p_stat_index > 5 THEN RAISE EXCEPTION 'invalid_stat'; END IF;
    IF p_cost <= 0 THEN RAISE EXCEPTION 'invalid_rebirth_cost'; END IF;

    v_reserved := public.ram_ledger_reserve_operation(
        p_operation_id,
        p_profile_id,
        p_wallet_address,
        p_cluster,
        'rebirth',
        p_cost,
        p_expires_at,
        p_metadata_json
    );

    -- A caller may have lost the successful settlement response.  In that
    -- case the gameplay row has already been reset and must not be validated
    -- as though this were a new rebirth.
    IF COALESCE(v_reserved ->> 'state', '') = 'settled' THEN
        RETURN v_reserved;
    END IF;

    SELECT * INTO v_chainling
      FROM public.chainlings
     WHERE id = p_chainling_id
       AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'chainling_not_found_or_not_owned'; END IF;
    IF COALESCE(v_chainling.stars, 0) < 5 THEN RAISE EXCEPTION 'rebirth_requires_star_5'; END IF;
    IF COALESCE(v_chainling.rebirth_count, 0) >= 5 THEN RAISE EXCEPTION 'rebirth_max_reached'; END IF;

    RETURN v_reserved || jsonb_build_object(
        'rebirth_count', COALESCE(v_chainling.rebirth_count, 0),
        'stars', COALESCE(v_chainling.stars, 0)
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_prepare_rebirth_operation(
    UUID, UUID, TEXT, TEXT, UUID, INTEGER, INTEGER, TIMESTAMPTZ, JSONB
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_prepare_rebirth_operation(
    UUID, UUID, TEXT, TEXT, UUID, INTEGER, INTEGER, TIMESTAMPTZ, JSONB
) TO service_role;

CREATE OR REPLACE FUNCTION public.profile_rebirth_chainling_reserved_tx(
    p_operation_id UUID,
    p_profile_id UUID,
    p_cluster TEXT,
    p_chainling_id UUID,
    p_stat_index INTEGER,
    p_cost INTEGER,
    p_chain_signature TEXT,
    p_receipt_address TEXT
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_operation public.ram_ledger_operations%ROWTYPE;
    v_chainling public.chainlings%ROWTYPE;
    v_bonuses INTEGER[] := ARRAY[0, 0, 0, 0, 0, 0];
    v_index INTEGER;
    v_settlement JSONB;
    v_ram_balance BIGINT;
    v_replayed BOOLEAN := FALSE;
BEGIN
    IF p_stat_index < 0 OR p_stat_index > 5 THEN RAISE EXCEPTION 'invalid_stat'; END IF;
    IF p_cost <= 0 THEN RAISE EXCEPTION 'invalid_rebirth_cost'; END IF;

    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE operation_id = p_operation_id AND cluster = p_cluster
     FOR UPDATE;
    IF NOT FOUND
       OR v_operation.player_profile_id IS DISTINCT FROM p_profile_id
       OR v_operation.operation_kind IS DISTINCT FROM 'rebirth'
       OR v_operation.direction IS DISTINCT FROM 'debit'
       OR v_operation.amount IS DISTINCT FROM p_cost::BIGINT
       OR COALESCE(v_operation.metadata_json ->> 'chainling_id', '') IS DISTINCT FROM p_chainling_id::TEXT
       OR COALESCE(v_operation.metadata_json ->> 'stat_index', '') IS DISTINCT FROM p_stat_index::TEXT THEN
        RAISE EXCEPTION 'rebirth_operation_mismatch';
    END IF;

    SELECT * INTO v_chainling
      FROM public.chainlings
     WHERE id = p_chainling_id
       AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'chainling_not_found_or_not_owned'; END IF;

    IF v_operation.state = 'settled' THEN
        v_replayed := TRUE;
        SELECT ram_balance INTO v_ram_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_profile_id;
    ELSE
        IF COALESCE(v_chainling.stars, 0) < 5 THEN RAISE EXCEPTION 'rebirth_requires_star_5'; END IF;
        IF COALESCE(v_chainling.rebirth_count, 0) >= 5 THEN RAISE EXCEPTION 'rebirth_max_reached'; END IF;

        FOR v_index IN 0..5 LOOP
            v_bonuses[v_index + 1] := COALESCE((v_chainling.rebirth_iv_bonuses ->> v_index)::INTEGER, 0);
        END LOOP;
        v_bonuses[p_stat_index + 1] := v_bonuses[p_stat_index + 1] + 1;

        v_settlement := public.ram_ledger_settle_spend(
            p_operation_id, p_cluster, p_chain_signature, p_receipt_address
        );
        v_ram_balance := (v_settlement ->> 'balance_after')::BIGINT;

        UPDATE public.chainlings
           SET rebirth_count = COALESCE(rebirth_count, 0) + 1,
               stars = 0,
               mastery_xp = 0,
               rebirth_iv_bonuses = to_jsonb(v_bonuses),
               current_hp = NULL,
               hp_synced_at = NULL,
               updated_at = now()
         WHERE id = p_chainling_id
           AND player_profile_id = p_profile_id
         RETURNING * INTO v_chainling;

        INSERT INTO public.player_lifetime_stats(player_profile_id, total_rebirths, total_ram_spent)
        VALUES (p_profile_id, 1, p_cost)
        ON CONFLICT (player_profile_id) DO UPDATE
          SET total_rebirths = public.player_lifetime_stats.total_rebirths + 1,
              total_ram_spent = public.player_lifetime_stats.total_ram_spent + p_cost,
              updated_at = now();
    END IF;

    RETURN jsonb_build_object(
        'replayed', v_replayed,
        'ram_balance', v_ram_balance,
        'chainling', jsonb_build_object(
            'backend_chainling_id', v_chainling.id,
            'species_id', v_chainling.species_id,
            'stars', COALESCE(v_chainling.stars, 0),
            'rebirth_count', COALESCE(v_chainling.rebirth_count, 0),
            'evolution_stage', COALESCE(v_chainling.evolution_stage, 1),
            'display_name', COALESCE(v_chainling.display_name, ''),
            'ivs', COALESCE(v_chainling.ivs, '[]'::jsonb),
            'rebirth_iv_bonuses', COALESCE(v_chainling.rebirth_iv_bonuses, '[0,0,0,0,0,0]'::jsonb),
            'entangled_flag', COALESCE(v_chainling.entangled_flag, false),
            'visual_variant_id', COALESCE(v_chainling.visual_variant_id, 'base'),
            'visual_identity', COALESCE(v_chainling.visual_identity, '{}'::jsonb),
            'current_hp', -1
        )
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_rebirth_chainling_reserved_tx(
    UUID, UUID, TEXT, UUID, INTEGER, INTEGER, TEXT, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_rebirth_chainling_tx(UUID, UUID, INTEGER, INTEGER)
    FROM service_role;
GRANT EXECUTE ON FUNCTION public.profile_rebirth_chainling_reserved_tx(
    UUID, UUID, TEXT, UUID, INTEGER, INTEGER, TEXT, TEXT
) TO service_role;
