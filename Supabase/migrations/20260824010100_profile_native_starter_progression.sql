-- Chainbound - profile-native starter and progression move grants.

CREATE OR REPLACE FUNCTION public.profile_commit_starter_tx(
    p_profile_id UUID,
    p_species_id TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_initial RECORD;
    v_move_item_id UUID;
    v_created BOOLEAN := false;
    v_species_id TEXT := NULLIF(BTRIM(COALESCE(p_species_id, '')), '');
    v_move_ids JSONB := '[]'::jsonb;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::TEXT, '') || ':starter',
        0
    ));

    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    IF v_species_id IS NULL OR NOT EXISTS (
        SELECT 1
        FROM public.species_initial_loadouts
        WHERE species_id = v_species_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_starter_species');
    END IF;

    SELECT *
    INTO v_chainling
    FROM public.chainlings
    WHERE player_profile_id = p_profile_id
      AND is_party_member = true
      AND party_slot = 0
    LIMIT 1
    FOR UPDATE;

    IF NOT FOUND THEN
        INSERT INTO public.chainlings (
            player_profile_id,
            species_id,
            is_party_member,
            party_slot,
            level,
            xp,
            ivs,
            rebirth_iv_bonuses,
            stars,
            rebirth_count,
            evolution_stage,
            visual_variant_id,
            visual_identity
        ) VALUES (
            p_profile_id,
            v_species_id,
            true,
            0,
            1,
            0,
            '[0,0,0,0,0,0]'::jsonb,
            '[0,0,0,0,0,0]'::jsonb,
            0,
            0,
            1,
            'base',
            jsonb_build_object(
                'aura_id', 'none',
                'hud_card_variant_id', 'default',
                'swap_in_fx_id', 'default',
                'swap_out_fx_id', 'default',
                'ko_fx_id', 'none',
                'victory_pose_id', 'default',
                'idle_style_id', 'none',
                'idle_flourish_id', 'none',
                'melee_attack_style_id', 'none',
                'ranged_attack_style_id', 'none',
                'hit_reaction_style_id', 'none',
                'attacker_ko_style_id', 'none'
            )
        )
        RETURNING * INTO v_chainling;

        v_created := true;
    END IF;

    -- Idempotent repeats retain the first selected species. Missing authored
    -- initial slots are repaired without overwriting player-authored loadouts.
    FOR v_initial IN
        SELECT slot, move_id
        FROM public.species_initial_loadouts
        WHERE species_id = v_chainling.species_id
        ORDER BY slot
    LOOP
        IF EXISTS (
            SELECT 1
            FROM public.loadouts
            WHERE chainling_id = v_chainling.id
              AND slot = v_initial.slot
              AND move_item_id IS NOT NULL
        ) THEN
            CONTINUE;
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
            v_initial.move_id,
            NULL,
            NULL,
            false,
            NULL,
            'starter_initial'
        )
        RETURNING id INTO v_move_item_id;

        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_chainling.id, v_initial.slot, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO UPDATE
        SET move_item_id = COALESCE(public.loadouts.move_item_id, EXCLUDED.move_item_id);
    END LOOP;

    UPDATE public.player_profiles
    SET has_started_game = true,
        updated_at = now()
    WHERE player_profile_id = p_profile_id;

    SELECT COALESCE(jsonb_agg(mi.move_id ORDER BY l.slot), '[]'::jsonb)
    INTO v_move_ids
    FROM public.loadouts l
    JOIN public.move_items mi ON mi.id = l.move_item_id
    WHERE l.chainling_id = v_chainling.id;

    RETURN jsonb_build_object(
        'success', true,
        'player_profile_id', p_profile_id,
        'chainling_id', v_chainling.id,
        'species_id', v_chainling.species_id,
        'move_ids', v_move_ids,
        'created', v_created
    );
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT) TO service_role;
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
    WHERE player_profile_id = p_profile_id
      AND move_id = v_move_id
      AND tier = 'ordinal'::public.move_tier
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
GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(
    UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER
) TO service_role;
