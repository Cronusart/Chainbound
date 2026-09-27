-- Chainbound - starter naming is part of the idempotent starter commit.
-- A two-argument compatibility wrapper is replaced at the end so older
-- callers also route into this authoritative implementation.

CREATE OR REPLACE FUNCTION public.profile_commit_starter_tx(
    p_profile_id UUID,
    p_species_id TEXT,
    p_display_name TEXT DEFAULT NULL
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
    v_display_name TEXT := NULL;
    v_move_ids JSONB := '[]'::jsonb;
BEGIN
    IF p_display_name IS NOT NULL THEN
        v_display_name := NULLIF(BTRIM(p_display_name), '');
        IF v_display_name IS NULL OR char_length(v_display_name) < 1 OR char_length(v_display_name) > 16 THEN
            RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
        END IF;
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::TEXT, '') || ':starter',
        0
    ));

    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    IF v_species_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.species_initial_loadouts WHERE species_id = v_species_id
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
            display_name,
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
            v_display_name,
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
    ELSIF v_chainling.display_name IS NULL AND v_display_name IS NOT NULL THEN
        -- A retry may repair an older starter that was committed before this
        -- feature. Deliberately never overwrite an existing player name.
        UPDATE public.chainlings
        SET display_name = v_display_name,
            updated_at = now()
        WHERE id = v_chainling.id
          AND display_name IS NULL
        RETURNING * INTO v_chainling;
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
            SELECT 1 FROM public.loadouts
            WHERE chainling_id = v_chainling.id
              AND slot = v_initial.slot
              AND move_item_id IS NOT NULL
        ) THEN
            CONTINUE;
        END IF;

        INSERT INTO public.move_items (
            player_profile_id, tier, move_id, modifier1_id, modifier2_id,
            locked, exported_asset_id, source
        ) VALUES (
            p_profile_id, 'ordinal'::public.move_tier, v_initial.move_id,
            NULL, NULL, false, NULL, 'starter_initial'
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
        'display_name', COALESCE(v_chainling.display_name, ''),
        'move_ids', v_move_ids,
        'created', v_created
    );
END;
$$;

GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) TO service_role;

CREATE OR REPLACE FUNCTION public.profile_commit_starter_tx(
    p_profile_id UUID,
    p_species_id TEXT
)
RETURNS JSONB
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
AS $$
    SELECT public.profile_commit_starter_tx(p_profile_id, p_species_id, NULL);
$$;

GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT) TO service_role;
