-- Candy Machine Known rolls are profile-native inventory. A wallet and Solana
-- account are required only when a player later Stamps the move.

ALTER TABLE public.ownership_stamp_operations
    ADD COLUMN IF NOT EXISTS known_source TEXT NOT NULL DEFAULT 'legacy_onchain';

ALTER TABLE public.ownership_stamp_operations
    DROP CONSTRAINT IF EXISTS ownership_stamp_operations_known_source_valid;

ALTER TABLE public.ownership_stamp_operations
    ADD CONSTRAINT ownership_stamp_operations_known_source_valid
    CHECK (known_source IN ('legacy_onchain', 'profile'));

CREATE OR REPLACE FUNCTION public.ownership_roll_known_profile_atomic(
    p_operation_id UUID,
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT,
    p_ram_cost INTEGER
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_profile public.player_profiles%ROWTYPE;
    v_existing public.ownership_known_moves%ROWTYPE;
    v_known public.ownership_known_moves%ROWTYPE;
    v_operation public.ram_ledger_operations%ROWTYPE;
    v_move_item_id UUID;
    v_idempotency_key TEXT := NULLIF(BTRIM(COALESCE(p_idempotency_key, '')), '');
    v_move_id TEXT := NULLIF(BTRIM(COALESCE(p_move_id, '')), '');
BEGIN
    IF p_operation_id IS NULL OR p_player_profile_id IS NULL THEN
        RAISE EXCEPTION 'invalid_known_roll_identity';
    END IF;
    IF p_cluster NOT IN ('devnet', 'mainnet-beta') THEN
        RAISE EXCEPTION 'invalid_known_roll_cluster';
    END IF;
    IF v_idempotency_key IS NULL OR LENGTH(v_idempotency_key) > 128 THEN
        RAISE EXCEPTION 'invalid_idempotency_key';
    END IF;
    IF v_move_id IS NULL THEN RAISE EXCEPTION 'move_id_required'; END IF;
    IF p_ram_cost IS DISTINCT FROM 5 THEN RAISE EXCEPTION 'invalid_known_roll_ram_cost'; END IF;
    IF p_onchain_move_id NOT BETWEEN 0 AND 65535
       OR p_element NOT BETWEEN 0 AND 255
       OR p_base_power NOT BETWEEN 0 AND 65535
       OR p_generator_version IS NULL OR p_generator_version <= 0
       OR p_pool_hash IS NULL OR p_pool_hash !~ '^[0-9a-f]{64}$' THEN
        RAISE EXCEPTION 'invalid_known_roll_result';
    END IF;

    -- Profile is always the first lock for profile-native economy mutations.
    SELECT * INTO v_profile
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'player_profile_not_found'; END IF;

    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE cluster = p_cluster
       AND operation_id = p_operation_id
     FOR UPDATE;
    IF FOUND AND (
        v_operation.player_profile_id IS DISTINCT FROM p_player_profile_id
        OR v_operation.wallet_address IS NOT NULL
        OR v_operation.operation_kind IS DISTINCT FROM 'known_roll'
        OR v_operation.direction IS DISTINCT FROM 'debit'
        OR v_operation.amount IS DISTINCT FROM p_ram_cost
        OR v_operation.state IS DISTINCT FROM 'settled'
        OR COALESCE(v_operation.metadata_json ->> 'idempotency_key', '') IS DISTINCT FROM v_idempotency_key
    ) THEN
        RAISE EXCEPTION 'known_roll_operation_conflict';
    END IF;

    SELECT * INTO v_existing
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = v_idempotency_key
     FOR UPDATE;

    IF FOUND THEN
        IF v_existing.move_id IS DISTINCT FROM v_move_id
           OR v_existing.onchain_move_id IS DISTINCT FROM p_onchain_move_id
           OR v_existing.element IS DISTINCT FROM p_element
           OR v_existing.base_power IS DISTINCT FROM p_base_power
           OR v_existing.generator_version IS DISTINCT FROM p_generator_version
           OR v_existing.pool_hash IS DISTINCT FROM p_pool_hash
           OR v_existing.acquisition IS DISTINCT FROM 'candy_roll' THEN
            RAISE EXCEPTION 'known_roll_idempotency_conflict';
        END IF;

        INSERT INTO public.move_items(
            player_profile_id, tier, move_id, modifier1_id, modifier2_id,
            locked, exported_asset_id, source, ownership_known_id
        ) VALUES (
            p_player_profile_id, 'ordinal'::public.move_tier, v_move_id, NULL, NULL,
            FALSE, NULL, 'candy_roll', v_existing.known_id
        )
        ON CONFLICT (ownership_known_id) DO UPDATE
           SET move_id = EXCLUDED.move_id
        RETURNING id INTO v_move_item_id;

        RETURN jsonb_build_object(
            'replayed', TRUE,
            'known', to_jsonb(v_existing),
            'move_item_id', v_move_item_id,
            'ram_balance', v_profile.ram_balance,
            'ram_balance_version', v_profile.ram_balance_version,
            'ram_cost', p_ram_cost
        );
    END IF;

    IF v_operation.id IS NOT NULL THEN
        RAISE EXCEPTION 'known_roll_operation_conflict';
    END IF;
    IF v_profile.ram_balance < p_ram_cost THEN RAISE EXCEPTION 'insufficient_ram'; END IF;

    INSERT INTO public.ownership_known_moves(
        player_profile_id, idempotency_key, move_id, onchain_move_id,
        element, base_power, generator_version, pool_hash, acquisition,
        onchain_instance_id
    ) VALUES (
        p_player_profile_id, v_idempotency_key, v_move_id, p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash, 'candy_roll',
        NULL
    ) RETURNING * INTO v_known;

    INSERT INTO public.move_items(
        player_profile_id, tier, move_id, modifier1_id, modifier2_id,
        locked, exported_asset_id, source, ownership_known_id
    ) VALUES (
        p_player_profile_id, 'ordinal'::public.move_tier, v_move_id, NULL, NULL,
        FALSE, NULL, 'candy_roll', v_known.known_id
    ) RETURNING id INTO v_move_item_id;

    UPDATE public.player_profiles
       SET ram_balance = ram_balance - p_ram_cost
     WHERE player_profile_id = p_player_profile_id
     RETURNING * INTO v_profile;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, state, metadata_json, settled_at
    ) VALUES (
        p_operation_id, p_player_profile_id, NULL, p_cluster,
        'known_roll', 'debit', p_ram_cost, 'settled',
        jsonb_build_object(
            'source', 'profile_owned_known_roll',
            'idempotency_key', v_idempotency_key,
            'known_id', v_known.known_id,
            'move_item_id', v_move_item_id,
            'move_id', v_move_id,
            'generator_version', p_generator_version,
            'pool_hash', p_pool_hash
        ),
        now()
    );

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'known', to_jsonb(v_known),
        'move_item_id', v_move_item_id,
        'ram_balance', v_profile.ram_balance,
        'ram_balance_version', v_profile.ram_balance_version,
        'ram_cost', p_ram_cost
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_roll_known_profile_atomic(
    UUID, UUID, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_roll_known_profile_atomic(
    UUID, UUID, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) TO service_role;

-- Keep the older service RPC safe for any rolling Edge deployment. It now
-- delegates to the same profile-native transaction and never needs a wallet.
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
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_hash TEXT := md5(
        'profile-known-roll:' || COALESCE(p_player_profile_id::TEXT, '') || ':' ||
        BTRIM(COALESCE(p_idempotency_key, ''))
    );
    v_operation_id UUID;
    v_cluster TEXT;
BEGIN
    v_operation_id := (
        SUBSTR(v_hash, 1, 8) || '-' || SUBSTR(v_hash, 9, 4) || '-' ||
        SUBSTR(v_hash, 13, 4) || '-' || SUBSTR(v_hash, 17, 4) || '-' ||
        SUBSTR(v_hash, 21, 12)
    )::UUID;
    SELECT cluster INTO v_cluster
      FROM public.battle_reward_settings
     WHERE singleton;
    IF v_cluster IS NULL THEN RAISE EXCEPTION 'ownership_cluster_not_configured'; END IF;

    RETURN public.ownership_roll_known_profile_atomic(
        v_operation_id, p_player_profile_id, v_cluster, p_idempotency_key,
        p_move_id, p_onchain_move_id, p_element, p_base_power,
        p_generator_version, p_pool_hash, p_ram_cost
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_roll_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_roll_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, INTEGER
) TO service_role;

-- The legacy settlement stores a vault instance on the Known row. Profile-native
-- Stamp tickets use a deterministic receipt reference instead, so restore NULL
-- after the legacy settlement while retaining the reference on the operation.
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
    v_result JSONB;
    v_operation public.ownership_stamp_operations%ROWTYPE;
    v_move_item_id UUID;
    v_modifier1_id TEXT;
    v_modifier2_id TEXT;
BEGIN
    v_result := public.ownership_confirm_stamp_reserved_legacy(
        p_operation_id, p_player_profile_id, p_cluster, p_idempotency_key,
        p_known_id, p_signature, p_verified_asset_json, p_onchain_instance_id, p_receipt_address
    );

    SELECT * INTO v_operation
      FROM public.ownership_stamp_operations
     WHERE operation_id = p_operation_id
       AND player_profile_id = p_player_profile_id
       AND known_id = p_known_id
       AND status = 'confirmed'
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'confirmed_stamp_operation_not_found'; END IF;

    IF v_operation.known_source = 'profile' THEN
        UPDATE public.ownership_known_moves
           SET onchain_instance_id = NULL
         WHERE known_id = p_known_id
           AND player_profile_id = p_player_profile_id;
    END IF;

    SELECT catalog.modifier_id INTO v_modifier1_id
      FROM public.modifiers_catalog AS catalog
     WHERE catalog.modifier_id = public.normalize_modifier_id(v_operation.modifier_ids[1]);

    IF v_operation.modifier_count = 2 THEN
        SELECT catalog.modifier_id INTO v_modifier2_id
          FROM public.modifiers_catalog AS catalog
         WHERE catalog.modifier_id = public.normalize_modifier_id(v_operation.modifier_ids[2]);
    END IF;

    UPDATE public.move_items
       SET tier = 'inscription'::public.move_tier,
           modifier1_id = v_modifier1_id,
           modifier1_effect_value = CASE
               WHEN v_modifier1_id IS NULL THEN 0
               ELSE COALESCE(v_operation.modifier_effect_values[1], 0)
           END,
           modifier1_effect_value_locked = v_modifier1_id IS NOT NULL
               AND v_operation.modifier_effect_values[1] IS NOT NULL,
           modifier2_id = v_modifier2_id,
           modifier2_effect_value = CASE
               WHEN v_modifier2_id IS NULL THEN 0
               ELSE COALESCE(v_operation.modifier_effect_values[2], 0)
           END,
           modifier2_effect_value_locked = v_modifier2_id IS NOT NULL
               AND v_operation.modifier_effect_values[2] IS NOT NULL,
           exported_asset_id = v_operation.stamped_asset_address,
           stamp_operation_id = v_operation.operation_id
     WHERE player_profile_id = p_player_profile_id
       AND ownership_known_id = p_known_id
    RETURNING id INTO v_move_item_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'move_item_not_found_for_known'; END IF;

    RETURN v_result || jsonb_build_object('move_item_id', v_move_item_id);
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_confirm_stamp_reserved(
    UUID, UUID, TEXT, TEXT, UUID, TEXT, JSONB, NUMERIC, TEXT
) TO service_role;

