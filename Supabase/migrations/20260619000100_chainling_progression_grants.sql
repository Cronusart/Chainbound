-- Chainbound - Chainling star/mastery progression ordinal grants.
-- Supabase-primary inventory grant path for editor-configured progression pools.

CREATE OR REPLACE FUNCTION public.profile_grant_progression_ordinal_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_move_id TEXT,
    p_source_event_id TEXT,
    p_idempotency_key TEXT,
    p_event_type TEXT,
    p_star_index INTEGER,
    p_mastery_cycle_index INTEGER,
    p_battle_seed INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_profile public.player_profiles%ROWTYPE;
    v_move_item_id UUID;
    v_existing_move_item_id UUID;
    v_idempotency_key TEXT;
    v_move_id TEXT;
BEGIN
    v_move_id := NULLIF(TRIM(COALESCE(p_move_id, '')), '');
    IF v_move_id IS NULL THEN
        RAISE EXCEPTION 'move_id_required';
    END IF;

    v_idempotency_key := NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '');
    IF v_idempotency_key IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required';
    END IF;

    SELECT *
    INTO v_profile
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    IF v_profile.legacy_player_id IS NULL THEN
        RAISE EXCEPTION 'legacy_player_id_required';
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
    )
    VALUES (
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
    ON CONFLICT (idempotency_key) DO NOTHING;

    IF NOT FOUND THEN
        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_id', v_move_id
        );
    END IF;

    SELECT id
    INTO v_existing_move_item_id
    FROM public.move_items
    WHERE player_id = v_profile.legacy_player_id
      AND move_id = v_move_id
      AND COALESCE(locked, false) = false
    ORDER BY id
    LIMIT 1;

    IF FOUND THEN
        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_item_id', v_existing_move_item_id,
            'move_id', v_move_id
        );
    END IF;

    INSERT INTO public.move_items (
        player_id,
        tier,
        move_id,
        modifier1_id,
        modifier2_id,
        locked,
        exported_asset_id
    )
    VALUES (
        v_profile.legacy_player_id,
        'ordinal',
        v_move_id,
        NULL,
        NULL,
        false,
        NULL
    )
    RETURNING id INTO v_move_item_id;

    UPDATE public.chainlings
    SET pve_progression_moves_granted = pve_progression_moves_granted + 1,
        service_record_version = 1,
        updated_at = now()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id;

    RETURN jsonb_build_object(
        'success', true,
        'already_applied', false,
        'move_item_id', v_move_item_id,
        'move_id', v_move_id
    );
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER) TO service_role;
