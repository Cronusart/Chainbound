-- Ownership Spine Phase 1 authority persistence.
-- Canonical Known rows live here; historical move_items normalization remains
-- isolated to profile_get_move_inventory / MoveTierMigrationAdapter.

CREATE TABLE IF NOT EXISTS public.ownership_known_moves (
    known_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    idempotency_key TEXT NOT NULL,
    move_id TEXT NOT NULL,
    onchain_move_id INTEGER NOT NULL CHECK (onchain_move_id BETWEEN 0 AND 65535),
    element INTEGER NOT NULL CHECK (element BETWEEN 0 AND 255),
    base_power INTEGER NOT NULL CHECK (base_power BETWEEN 0 AND 65535),
    generator_version INTEGER NOT NULL CHECK (generator_version > 0),
    pool_hash TEXT NOT NULL CHECK (pool_hash ~ '^[0-9a-f]{64}$'),
    onchain_instance_id NUMERIC(20, 0),
    consumed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_known_moves_idempotency UNIQUE (player_profile_id, idempotency_key),
    CONSTRAINT ownership_known_moves_onchain_instance_valid CHECK (
        onchain_instance_id IS NULL OR onchain_instance_id BETWEEN 0 AND 18446744073709551615
    )
);

CREATE INDEX IF NOT EXISTS idx_ownership_known_moves_active
    ON public.ownership_known_moves (player_profile_id, created_at)
    WHERE consumed_at IS NULL;

CREATE TABLE IF NOT EXISTS public.ownership_stamp_operations (
    operation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    known_id UUID NOT NULL REFERENCES public.ownership_known_moves(known_id) ON DELETE RESTRICT,
    wallet_address TEXT NOT NULL,
    idempotency_key TEXT NOT NULL,
    idempotency_hash TEXT NOT NULL CHECK (idempotency_hash ~ '^[0-9a-f]{64}$'),
    modifier_count INTEGER NOT NULL CHECK (modifier_count IN (1, 2)),
    modifier_ids TEXT[] NOT NULL,
    rarity INTEGER NOT NULL CHECK (rarity BETWEEN 0 AND 5),
    generator_version INTEGER NOT NULL CHECK (generator_version > 0),
    pool_hash TEXT NOT NULL CHECK (pool_hash ~ '^[0-9a-f]{64}$'),
    onchain_instance_id NUMERIC(20, 0) NOT NULL,
    ticket_address TEXT NOT NULL,
    stamped_asset_address TEXT NOT NULL,
    transaction_base64 TEXT NOT NULL,
    recent_blockhash TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    status TEXT NOT NULL DEFAULT 'prepared' CHECK (status IN ('prepared', 'confirmed', 'expired', 'failed')),
    signature TEXT,
    verified_asset_json JSONB,
    confirmed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_stamp_operations_idempotency UNIQUE (player_profile_id, idempotency_key),
    CONSTRAINT ownership_stamp_operations_modifier_shape CHECK (
        cardinality(modifier_ids) = modifier_count
        AND modifier_ids[1] IS NOT NULL
        AND btrim(modifier_ids[1]) <> ''
        AND (modifier_count = 1 OR (modifier_ids[2] IS NOT NULL AND btrim(modifier_ids[2]) <> '' AND modifier_ids[1] <> modifier_ids[2]))
    )
);

CREATE INDEX IF NOT EXISTS idx_ownership_stamp_operations_inventory
    ON public.ownership_stamp_operations (player_profile_id, status, confirmed_at DESC);

ALTER TABLE public.ownership_known_moves ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ownership_stamp_operations ENABLE ROW LEVEL SECURITY;

-- No client policies are intentional. Edge Functions use the service role and
-- establish profile authority from the caller's validated Supabase JWT.
REVOKE ALL ON public.ownership_known_moves FROM anon, authenticated;
REVOKE ALL ON public.ownership_stamp_operations FROM anon, authenticated;

-- The Phase 1 Known roll costs exactly 5 RAM. Keep the balance mutation and Known
-- insertion in one database transaction so retries cannot debit twice and concurrent
-- requests cannot mint a free Known.
CREATE OR REPLACE FUNCTION public.ownership_roll_known_atomic(
    p_player_profile_id UUID,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT,
    p_ram_cost INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_existing public.ownership_known_moves%ROWTYPE;
    v_inserted public.ownership_known_moves%ROWTYPE;
    v_ram_balance INTEGER;
BEGIN
    IF p_ram_cost <> 5 THEN
        RAISE EXCEPTION 'invalid_known_roll_ram_cost' USING ERRCODE = 'P0001';
    END IF;

    SELECT ram_balance
      INTO v_ram_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'player_profile_not_found' USING ERRCODE = 'P0001';
    END IF;

    SELECT *
      INTO v_existing
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = p_idempotency_key;

    IF FOUND THEN
        RETURN jsonb_build_object(
            'replayed', TRUE,
            'known', to_jsonb(v_existing),
            'ram_balance', v_ram_balance,
            'ram_cost', p_ram_cost
        );
    END IF;

    IF v_ram_balance < p_ram_cost THEN
        RAISE EXCEPTION 'insufficient_ram' USING ERRCODE = 'P0001';
    END IF;

    UPDATE public.player_profiles
       SET ram_balance = ram_balance - p_ram_cost
     WHERE player_profile_id = p_player_profile_id
     RETURNING ram_balance INTO v_ram_balance;

    INSERT INTO public.ownership_known_moves (
        player_profile_id, idempotency_key, move_id, onchain_move_id,
        element, base_power, generator_version, pool_hash
    ) VALUES (
        p_player_profile_id, p_idempotency_key, p_move_id, p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash
    )
    RETURNING * INTO v_inserted;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'known', to_jsonb(v_inserted),
        'ram_balance', v_ram_balance,
        'ram_cost', p_ram_cost
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_roll_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_roll_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) TO service_role;
