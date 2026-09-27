-- Durable Stamp intents and crash-safe reconciliation.
-- A transaction blockhash may expire, but the authoritative Stamp outcome does not.

ALTER TABLE public.ownership_stamp_operations
    ADD COLUMN IF NOT EXISTS submitted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS chain_confirmed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS last_reconciled_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS failure_code TEXT;

ALTER TABLE public.ownership_stamp_operations
    DROP CONSTRAINT IF EXISTS ownership_stamp_operations_status_check;

UPDATE public.ownership_stamp_operations
   SET status = 'prepared',
       failure_code = COALESCE(failure_code, 'transaction_envelope_expired'),
       updated_at = now()
 WHERE status = 'expired';

ALTER TABLE public.ownership_stamp_operations
    ADD CONSTRAINT ownership_stamp_operations_status_check CHECK (
        status IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending', 'confirmed', 'failed')
    );

-- Pre-release data may contain several prepared attempts for one Known. Preserve
-- the earliest intent and retire later attempts without deleting audit history.
WITH ranked AS (
    SELECT operation_id,
           row_number() OVER (
               PARTITION BY player_profile_id, known_id
               ORDER BY (status = 'confirmed') DESC, created_at ASC, operation_id ASC
           ) AS intent_rank
      FROM public.ownership_stamp_operations
     WHERE status IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending', 'confirmed')
)
UPDATE public.ownership_stamp_operations AS operation
   SET status = 'failed',
       failure_code = 'superseded_duplicate_known_intent',
       updated_at = now()
  FROM ranked
 WHERE operation.operation_id = ranked.operation_id
   AND ranked.intent_rank > 1;

-- Only one unresolved Stamp may exist per profile. This prevents previewing a
-- set of Known moves and then signing only the most desirable outcome.
WITH ranked AS (
    SELECT operation_id,
           row_number() OVER (
               PARTITION BY player_profile_id
               ORDER BY
                   CASE status
                       WHEN 'chain_confirmed' THEN 0
                       WHEN 'das_pending' THEN 1
                       WHEN 'submitted' THEN 2
                       ELSE 3
                   END,
                   created_at ASC,
                   operation_id ASC
           ) AS profile_rank
      FROM public.ownership_stamp_operations
     WHERE status IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending')
)
UPDATE public.ownership_stamp_operations AS operation
   SET status = 'failed',
       failure_code = 'superseded_duplicate_profile_intent',
       updated_at = now()
  FROM ranked
 WHERE operation.operation_id = ranked.operation_id
   AND ranked.profile_rank > 1;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ownership_stamp_one_intent_per_known
    ON public.ownership_stamp_operations (player_profile_id, known_id)
    WHERE status IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending', 'confirmed');

CREATE UNIQUE INDEX IF NOT EXISTS ux_ownership_stamp_one_pending_per_profile
    ON public.ownership_stamp_operations (player_profile_id)
    WHERE status IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending');

CREATE UNIQUE INDEX IF NOT EXISTS ux_ownership_stamp_asset
    ON public.ownership_stamp_operations (stamped_asset_address);

CREATE INDEX IF NOT EXISTS idx_ownership_stamp_reconciliation
    ON public.ownership_stamp_operations (status, updated_at)
    WHERE status IN ('submitted', 'chain_confirmed', 'das_pending');

-- Settle exactly once after the expected Core asset is independently verified.
-- Lock order is profile -> operation -> Known to keep concurrent settlement safe.
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

    IF NULLIF(btrim(p_signature), '') IS NULL OR p_verified_asset_json IS NULL THEN
        RAISE EXCEPTION 'verified_stamp_evidence_required' USING ERRCODE = 'P0001';
    END IF;

    SELECT ram_balance
      INTO v_ram_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'player_profile_not_found' USING ERRCODE = 'P0001';
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
        RETURN jsonb_build_object(
            'replayed', TRUE,
            'ram_balance', v_ram_balance,
            'signature', v_operation.signature,
            'asset', v_operation.verified_asset_json
        );
    END IF;

    IF v_operation.status NOT IN ('prepared', 'submitted', 'chain_confirmed', 'das_pending') THEN
        RAISE EXCEPTION 'stamp_operation_not_reconcilable' USING ERRCODE = 'P0001';
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
           chain_confirmed_at = COALESCE(chain_confirmed_at, v_now),
           last_reconciled_at = v_now,
           confirmed_at = v_now,
           failure_code = NULL,
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
