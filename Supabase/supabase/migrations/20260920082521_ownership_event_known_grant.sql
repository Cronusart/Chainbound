-- One-time account Known grants (boss victories, events). Idempotent per profile + key.
-- Canonical remote migration version: 20260920082521.
-- Writes ownership_known_moves (candy Stamp path) and move_items (party inventory path).

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
        element, base_power, generator_version, pool_hash
    ) VALUES (
        p_player_profile_id, TRIM(p_idempotency_key), TRIM(p_move_id), p_onchain_move_id,
        p_element, p_base_power, p_generator_version, p_pool_hash
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

REVOKE ALL ON FUNCTION public.ownership_grant_event_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_grant_event_known_atomic(
    UUID, TEXT, TEXT, INTEGER, INTEGER, INTEGER, INTEGER, TEXT, TEXT
) TO service_role;
