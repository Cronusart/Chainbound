-- Known instances remember whether they were rolled, learned, or granted.
-- Canonical remote migration version: 20260928121841.
-- Existing rows stay candy_roll. Stamp consume, RAM, and VRF are unchanged.

ALTER TABLE public.ownership_known_moves
    ADD COLUMN IF NOT EXISTS acquisition TEXT NOT NULL DEFAULT 'candy_roll';

ALTER TABLE public.ownership_known_moves
    DROP CONSTRAINT IF EXISTS ownership_known_moves_acquisition_valid;

ALTER TABLE public.ownership_known_moves
    ADD CONSTRAINT ownership_known_moves_acquisition_valid
    CHECK (acquisition IN ('candy_roll', 'progression_learn', 'event_grant'));

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
        element, base_power, generator_version, pool_hash, acquisition
    ) VALUES (
        p_player_profile_id, p_idempotency_key, p_move_id, p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash, 'candy_roll'
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

CREATE OR REPLACE FUNCTION public.ownership_grant_event_known_atomic(
    p_player_profile_id UUID,
    p_idempotency_key TEXT,
    p_move_id TEXT,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT,
    p_source TEXT DEFAULT 'event_grant'
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_existing public.ownership_known_moves%ROWTYPE;
    v_inserted public.ownership_known_moves%ROWTYPE;
    v_move_item_id UUID;
    v_source TEXT := COALESCE(NULLIF(TRIM(p_source), ''), 'event_grant');
BEGIN
    IF p_player_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_player_profile_id
    ) THEN
        RAISE EXCEPTION 'player_profile_not_found' USING ERRCODE = 'P0001';
    END IF;

    IF NULLIF(TRIM(COALESCE(p_move_id, '')), '') IS NULL THEN
        RAISE EXCEPTION 'move_id_required' USING ERRCODE = 'P0001';
    END IF;

    IF NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '') IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required' USING ERRCODE = 'P0001';
    END IF;

    SELECT *
      INTO v_existing
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = TRIM(p_idempotency_key);

    IF FOUND THEN
        SELECT id
          INTO v_move_item_id
          FROM public.move_items
         WHERE player_profile_id = p_player_profile_id
           AND move_id = v_existing.move_id
           AND tier = 'ordinal'::public.move_tier
           AND source = v_source
         ORDER BY created_at ASC
         LIMIT 1;

        RETURN jsonb_build_object(
            'replayed', TRUE,
            'known', to_jsonb(v_existing),
            'move_item_id', v_move_item_id
        );
    END IF;

    INSERT INTO public.ownership_known_moves (
        player_profile_id, idempotency_key, move_id, onchain_move_id,
        element, base_power, generator_version, pool_hash, acquisition
    ) VALUES (
        p_player_profile_id, TRIM(p_idempotency_key), TRIM(p_move_id), p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash, 'event_grant'
    )
    RETURNING * INTO v_inserted;

    INSERT INTO public.move_items (
        player_profile_id,
        tier,
        move_id,
        modifier1_id,
        modifier2_id,
        locked,
        exported_asset_id,
        source
    ) VALUES (
        p_player_profile_id,
        'ordinal'::public.move_tier,
        TRIM(p_move_id),
        NULL,
        NULL,
        false,
        NULL,
        v_source
    )
    RETURNING id INTO v_move_item_id;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'known', to_jsonb(v_inserted),
        'move_item_id', v_move_item_id
    );
END;
$$;

DROP FUNCTION IF EXISTS public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER
);

CREATE OR REPLACE FUNCTION public.profile_grant_progression_ordinal_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_move_id TEXT,
    p_source_event_id TEXT,
    p_idempotency_key TEXT,
    p_event_type TEXT,
    p_star_index INTEGER,
    p_mastery_cycle_index INTEGER,
    p_battle_seed INTEGER,
    p_onchain_move_id INTEGER,
    p_element INTEGER,
    p_base_power INTEGER,
    p_generator_version INTEGER,
    p_pool_hash TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_event public.chainling_stat_events%ROWTYPE;
    v_known public.ownership_known_moves%ROWTYPE;
    v_move_item_id UUID;
    v_idempotency_key TEXT;
    v_move_id TEXT;
    v_owned_copy_count INTEGER := 0;
BEGIN
    v_move_id := NULLIF(TRIM(COALESCE(p_move_id, '')), '');
    IF v_move_id IS NULL THEN
        RAISE EXCEPTION 'move_id_required';
    END IF;

    v_idempotency_key := NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '');
    IF v_idempotency_key IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required';
    END IF;

    IF p_generator_version IS NULL OR p_generator_version <= 0 THEN
        RAISE EXCEPTION 'invalid_generator_version';
    END IF;

    IF p_pool_hash IS NULL OR p_pool_hash !~ '^[0-9a-f]{64}$' THEN
        RAISE EXCEPTION 'invalid_pool_hash';
    END IF;

    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    SELECT *
    INTO v_chainling
    FROM public.chainlings
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'chainling_not_found_or_not_owned';
    END IF;

    INSERT INTO public.chainling_stat_events (
        idempotency_key,
        player_profile_id,
        chainling_id,
        event_type,
        metric_key,
        delta_json
    ) VALUES (
        v_idempotency_key,
        p_profile_id,
        p_chainling_id,
        'progression_ordinal_grant',
        'pve.progression_moves_granted',
        jsonb_build_object(
            'move_id', v_move_id,
            'source_event_id', COALESCE(p_source_event_id, ''),
            'event_type', COALESCE(p_event_type, ''),
            'star_index', GREATEST(0, COALESCE(p_star_index, 0)),
            'mastery_cycle_index', GREATEST(0, COALESCE(p_mastery_cycle_index, 0)),
            'battle_seed', COALESCE(p_battle_seed, 0)
        )
    )
    ON CONFLICT (idempotency_key) DO NOTHING
    RETURNING * INTO v_event;

    IF NOT FOUND THEN
        SELECT *
        INTO v_event
        FROM public.chainling_stat_events
        WHERE idempotency_key = v_idempotency_key;

        IF NOT FOUND OR
           v_event.player_profile_id IS DISTINCT FROM p_profile_id OR
           v_event.chainling_id IS DISTINCT FROM p_chainling_id OR
           v_event.event_type IS DISTINCT FROM 'progression_ordinal_grant' OR
           COALESCE(v_event.delta_json ->> 'move_id', '') IS DISTINCT FROM v_move_id THEN
            RAISE EXCEPTION 'idempotency_key_conflict';
        END IF;

        IF COALESCE(v_event.delta_json ->> 'move_item_id', '') <> '' THEN
            v_move_item_id := (v_event.delta_json ->> 'move_item_id')::UUID;
        END IF;

        SELECT *
          INTO v_known
          FROM public.ownership_known_moves
         WHERE player_profile_id = p_profile_id
           AND idempotency_key = v_idempotency_key;

        SELECT COUNT(*)::INTEGER
        INTO v_owned_copy_count
        FROM public.move_items
        WHERE player_profile_id = p_profile_id
          AND move_id = v_move_id
          AND tier = 'ordinal'::public.move_tier;

        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_item_id', v_move_item_id,
            'known_id', v_known.known_id,
            'move_id', v_move_id,
            'owned_copy_count', v_owned_copy_count
        );
    END IF;

    INSERT INTO public.move_items (
        player_profile_id,
        tier,
        move_id,
        modifier1_id,
        modifier2_id,
        locked,
        exported_asset_id,
        source
    ) VALUES (
        p_profile_id,
        'ordinal'::public.move_tier,
        v_move_id,
        NULL,
        NULL,
        false,
        NULL,
        'progression_grant'
    )
    RETURNING id INTO v_move_item_id;

    INSERT INTO public.ownership_known_moves (
        player_profile_id,
        idempotency_key,
        move_id,
        onchain_move_id,
        element,
        base_power,
        generator_version,
        pool_hash,
        acquisition
    ) VALUES (
        p_profile_id,
        v_idempotency_key,
        v_move_id,
        p_onchain_move_id,
        p_element,
        p_base_power,
        p_generator_version,
        p_pool_hash,
        'progression_learn'
    )
    RETURNING * INTO v_known;

    UPDATE public.chainling_stat_events
    SET delta_json = delta_json || jsonb_build_object(
        'move_item_id', v_move_item_id,
        'known_id', v_known.known_id
    )
    WHERE id = v_event.id;

    UPDATE public.chainlings
    SET pve_progression_moves_granted = pve_progression_moves_granted + 1,
        service_record_version = 1,
        updated_at = now()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id;

    SELECT COUNT(*)::INTEGER
    INTO v_owned_copy_count
    FROM public.move_items
    WHERE player_profile_id = p_profile_id
      AND move_id = v_move_id
      AND tier = 'ordinal'::public.move_tier;

    RETURN jsonb_build_object(
        'success', true,
        'already_applied', false,
        'move_item_id', v_move_item_id,
        'known_id', v_known.known_id,
        'move_id', v_move_id,
        'owned_copy_count', v_owned_copy_count
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, TEXT
) TO service_role;
