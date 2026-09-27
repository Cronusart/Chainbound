-- Atomically settle a chain-verified devnet smoke RAM grant into the server
-- economy mirror. RPC verification happens before this short transaction;
-- this function owns the only write boundary for both the audit row and RAM.
-- Wrapped as one DO statement for compatibility with the linked project's
-- legacy CLI prepared-statement migration runner.
DO $migration$
BEGIN
EXECUTE $function$
CREATE OR REPLACE FUNCTION public.ownership_confirm_devnet_smoke_grant_atomic(
    p_player_profile_id UUID,
    p_operation_id UUID,
    p_signature TEXT,
    p_balance_after INTEGER,
    p_daily_ram_earned INTEGER,
    p_lifetime_ram_earned NUMERIC,
    p_utc_day NUMERIC
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $body$
DECLARE
    v_operation public.ownership_ram_grant_operations%ROWTYPE;
    v_ram_balance INTEGER;
    v_now TIMESTAMPTZ := now();
BEGIN
    IF p_balance_after < 0
       OR p_daily_ram_earned < 0
       OR p_lifetime_ram_earned < 0
       OR p_utc_day < 0 THEN
        RAISE EXCEPTION 'invalid_verified_ram_grant_values' USING ERRCODE = 'P0001';
    END IF;

    SELECT * INTO v_operation
      FROM public.ownership_ram_grant_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
     FOR UPDATE;

    IF NOT FOUND OR v_operation.amount <> 100 THEN
        RAISE EXCEPTION 'smoke_grant_operation_not_found' USING ERRCODE = 'P0001';
    END IF;

    IF v_operation.status = 'confirmed' THEN
        SELECT ram_balance INTO v_ram_balance
          FROM public.player_profiles
         WHERE player_profile_id = p_player_profile_id;
        RETURN jsonb_build_object(
            'replayed', TRUE,
            'ram_balance', v_ram_balance,
            'signature', v_operation.signature
        );
    END IF;

    IF v_operation.status <> 'prepared' THEN
        RAISE EXCEPTION 'smoke_grant_operation_not_prepared' USING ERRCODE = 'P0001';
    END IF;

    -- The smoke transaction is deliberately restricted to a fresh chain profile
    -- and grants the full fixed amount. A different result is never mirrored.
    IF p_balance_after <> v_operation.amount THEN
        RAISE EXCEPTION 'unexpected_verified_ram_balance' USING ERRCODE = 'P0001';
    END IF;

    UPDATE public.player_profiles
       SET ram_balance = p_balance_after
     WHERE player_profile_id = p_player_profile_id
       AND ram_balance = 0
     RETURNING ram_balance INTO v_ram_balance;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'server_ram_balance_not_fresh' USING ERRCODE = 'P0001';
    END IF;

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

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'ram_balance', v_ram_balance,
        'signature', p_signature
    );
END;
$body$
$function$;

EXECUTE 'REVOKE ALL ON FUNCTION public.ownership_confirm_devnet_smoke_grant_atomic(UUID, UUID, TEXT, INTEGER, INTEGER, NUMERIC, NUMERIC) FROM PUBLIC, anon, authenticated';
EXECUTE 'GRANT EXECUTE ON FUNCTION public.ownership_confirm_devnet_smoke_grant_atomic(UUID, UUID, TEXT, INTEGER, INTEGER, NUMERIC, NUMERIC) TO service_role';
END
$migration$;
