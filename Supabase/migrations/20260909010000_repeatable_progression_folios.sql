-- Chainbound - repeatable star/mastery folios.
-- Distinct progression events may award the same move definition as separate Known Move copies.
-- Replays of the same idempotency key always return the original grant without duplicating it.

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
    v_event public.chainling_stat_events%ROWTYPE;
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

    UPDATE public.chainling_stat_events
    SET delta_json = delta_json || jsonb_build_object('move_item_id', v_move_item_id)
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
        'move_id', v_move_id,
        'owned_copy_count', v_owned_copy_count
    );
END;
$$;

GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER
) TO service_role;
