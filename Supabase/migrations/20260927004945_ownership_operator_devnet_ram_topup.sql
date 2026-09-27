-- One-time, operator-only Devnet recovery settlement. It reconciles an
-- already verified chain grant into the server mirror and cannot be used by
-- player roles or as a general economy top-up primitive.
CREATE OR REPLACE FUNCTION public.ownership_reconcile_operator_devnet_ram_topup_atomic(
    p_player_profile_id UUID,
    p_operation_id UUID,
    p_signature TEXT,
    p_chain_balance_before INTEGER,
    p_balance_after INTEGER,
    p_daily_ram_earned INTEGER,
    p_lifetime_ram_earned NUMERIC,
    p_utc_day NUMERIC
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_operation public.ownership_ram_grant_operations%ROWTYPE;
    v_ram_balance INTEGER;
    v_now TIMESTAMPTZ := now();
BEGIN
    IF p_chain_balance_before < 0 OR p_balance_after < 0
       OR p_daily_ram_earned < 0 OR p_daily_ram_earned > 100
       OR p_lifetime_ram_earned < 0 OR p_utc_day < 0 THEN
        RAISE EXCEPTION 'invalid_operator_topup_values' USING ERRCODE = 'P0001';
    END IF;

    SELECT * INTO v_operation
      FROM public.ownership_ram_grant_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
     FOR UPDATE;

    IF NOT FOUND OR v_operation.amount <> 100 THEN
        RAISE EXCEPTION 'operator_topup_operation_not_found' USING ERRCODE = 'P0001';
    END IF;

    IF v_operation.status = 'confirmed' THEN
        IF v_operation.signature <> p_signature THEN
            RAISE EXCEPTION 'operator_topup_signature_mismatch' USING ERRCODE = 'P0001';
        END IF;
        SELECT ram_balance INTO v_ram_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_player_profile_id;
        RETURN jsonb_build_object('replayed', TRUE, 'ram_balance', v_ram_balance, 'signature', v_operation.signature);
    END IF;

    IF v_operation.status <> 'prepared' THEN
        RAISE EXCEPTION 'operator_topup_operation_not_prepared' USING ERRCODE = 'P0001';
    END IF;

    IF p_balance_after <> p_chain_balance_before + v_operation.amount THEN
        RAISE EXCEPTION 'operator_topup_unexpected_chain_delta' USING ERRCODE = 'P0001';
    END IF;

    SELECT ram_balance INTO v_ram_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'operator_topup_profile_not_found' USING ERRCODE = 'P0001';
    END IF;
    IF v_ram_balance > p_chain_balance_before THEN
        RAISE EXCEPTION 'operator_topup_server_ram_exceeds_chain' USING ERRCODE = 'P0001';
    END IF;

    UPDATE public.player_profiles
       SET ram_balance = p_balance_after
     WHERE player_profile_id = p_player_profile_id;

    UPDATE public.ownership_ram_grant_operations
       SET status = 'confirmed',
           signature = p_signature,
           balance_after = p_balance_after,
           daily_ram_earned = p_daily_ram_earned,
           lifetime_ram_earned = p_lifetime_ram_earned,
           utc_day = p_utc_day,
           confirmed_at = v_now,
           updated_at = v_now
     WHERE operation_id = v_operation.operation_id;

    RETURN jsonb_build_object('replayed', FALSE, 'ram_balance', p_balance_after, 'signature', p_signature);
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_reconcile_operator_devnet_ram_topup_atomic(
    UUID, UUID, TEXT, INTEGER, INTEGER, INTEGER, NUMERIC, NUMERIC
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_reconcile_operator_devnet_ram_topup_atomic(
    UUID, UUID, TEXT, INTEGER, INTEGER, INTEGER, NUMERIC, NUMERIC
) TO service_role;
