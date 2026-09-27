-- Ownership Spine Phase 1 Devnet bootstrap audit and atomic Stamp settlement.
-- This migration does not grant RAM. Chain-side grants remain result-authority
-- transactions and profile creation continues to start at zero RAM.

CREATE TABLE IF NOT EXISTS public.ownership_ram_grant_operations (
    operation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    wallet_address TEXT NOT NULL,
    idempotency_key TEXT NOT NULL,
    idempotency_hash TEXT NOT NULL CHECK (idempotency_hash ~ '^[0-9a-f]{64}$'),
    amount INTEGER NOT NULL CHECK (amount = 100),
    player_profile_address TEXT NOT NULL,
    move_vault_address TEXT NOT NULL,
    receipt_address TEXT NOT NULL,
    transaction_base64 TEXT NOT NULL,
    recent_blockhash TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'prepared' CHECK (status IN ('prepared', 'confirmed', 'failed')),
    signature TEXT,
    balance_after INTEGER,
    daily_ram_earned INTEGER,
    lifetime_ram_earned NUMERIC(20, 0),
    utc_day NUMERIC(20, 0),
    confirmed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_ram_grant_idempotency UNIQUE (player_profile_id, idempotency_key),
    CONSTRAINT ownership_ram_grant_receipt_unique UNIQUE (receipt_address)
);

CREATE INDEX IF NOT EXISTS idx_ownership_ram_grant_wallet
    ON public.ownership_ram_grant_operations (wallet_address, created_at DESC);

ALTER TABLE public.ownership_ram_grant_operations ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON public.ownership_ram_grant_operations FROM anon, authenticated;

-- Settle the server-side half of a confirmed first Stamp exactly once. The
-- on-chain transaction has already charged 5 for materialization and 75 for
-- the first Stamp; the server mirror already charged the Known roll's 5, so
-- confirmation debits only the pinned first-Stamp cost of 75.
CREATE OR REPLACE FUNCTION public.ownership_confirm_stamp_atomic(
    p_player_profile_id UUID,
    p_idempotency_key TEXT,
    p_known_id UUID,
    p_signature TEXT,
    p_verified_asset_json JSONB,
    p_onchain_instance_id NUMERIC,
    p_ram_cost INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_operation public.ownership_stamp_operations%ROWTYPE;
    v_known public.ownership_known_moves%ROWTYPE;
    v_ram_balance INTEGER;
    v_now TIMESTAMPTZ := now();
BEGIN
    IF p_ram_cost <> 75 THEN
        RAISE EXCEPTION 'invalid_first_stamp_ram_cost' USING ERRCODE = 'P0001';
    END IF;

    SELECT * INTO v_operation
      FROM public.ownership_stamp_operations
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = p_idempotency_key
     FOR UPDATE;

    IF NOT FOUND OR v_operation.known_id <> p_known_id THEN
        RAISE EXCEPTION 'stamp_operation_not_found' USING ERRCODE = 'P0001';
    END IF;

    IF v_operation.status = 'confirmed' THEN
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

    IF v_operation.status <> 'prepared' THEN
        RAISE EXCEPTION 'stamp_operation_not_prepared' USING ERRCODE = 'P0001';
    END IF;

    SELECT * INTO v_known
      FROM public.ownership_known_moves
     WHERE known_id = p_known_id
       AND player_profile_id = p_player_profile_id
     FOR UPDATE;

    IF NOT FOUND OR v_known.consumed_at IS NOT NULL THEN
        RAISE EXCEPTION 'known_not_found_or_consumed' USING ERRCODE = 'P0001';
    END IF;

    UPDATE public.player_profiles
       SET ram_balance = ram_balance - p_ram_cost
     WHERE player_profile_id = p_player_profile_id
       AND ram_balance >= p_ram_cost
     RETURNING ram_balance INTO v_ram_balance;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'insufficient_ram' USING ERRCODE = 'P0001';
    END IF;

    UPDATE public.ownership_known_moves
       SET consumed_at = v_now,
           onchain_instance_id = p_onchain_instance_id
     WHERE known_id = p_known_id;

    UPDATE public.ownership_stamp_operations
       SET status = 'confirmed',
           signature = p_signature,
           verified_asset_json = p_verified_asset_json,
           confirmed_at = v_now,
           updated_at = v_now
     WHERE operation_id = v_operation.operation_id;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'ram_balance', v_ram_balance,
        'signature', p_signature,
        'asset', p_verified_asset_json
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_atomic(
    UUID, TEXT, UUID, TEXT, JSONB, NUMERIC, INTEGER
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_confirm_stamp_atomic(
    UUID, TEXT, UUID, TEXT, JSONB, NUMERIC, INTEGER
) TO service_role;

