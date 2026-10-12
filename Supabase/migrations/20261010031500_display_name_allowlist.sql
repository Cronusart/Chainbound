-- Shared character allowlist for player, Chainling, and starter display names.
-- Saved names stay 1–16. A name starts with a letter or digit and cannot end
-- on a space or special. Case-insensitive uniqueness is unchanged.

CREATE OR REPLACE FUNCTION public.display_name_code_is_letter_or_digit(p_code INT)
RETURNS BOOLEAN
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT p_code BETWEEN 48 AND 57
        OR p_code BETWEEN 65 AND 90
        OR p_code BETWEEN 97 AND 122
        OR p_code BETWEEN 192 AND 214
        OR p_code BETWEEN 216 AND 246
        OR p_code BETWEEN 248 AND 255;
$$;

CREATE OR REPLACE FUNCTION public.display_name_code_is_special(p_code INT)
RETURNS BOOLEAN
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT p_code IN (32, 39, 45, 46, 95);
$$;

CREATE OR REPLACE FUNCTION public.display_name_characters_allowed(p_name TEXT)
RETURNS BOOLEAN
LANGUAGE plpgsql
IMMUTABLE
AS $$
DECLARE
    v_len INT;
    v_index INT;
    v_code INT;
BEGIN
    IF p_name IS NULL THEN
        RETURN FALSE;
    END IF;

    v_len := char_length(p_name);
    IF v_len < 1 OR v_len > 16 OR position('  ' IN p_name) > 0 THEN
        RETURN FALSE;
    END IF;

    v_code := ascii(substring(p_name FROM 1 FOR 1));
    IF NOT public.display_name_code_is_letter_or_digit(v_code) THEN
        RETURN FALSE;
    END IF;

    v_code := ascii(substring(p_name FROM v_len FOR 1));
    IF public.display_name_code_is_special(v_code) THEN
        RETURN FALSE;
    END IF;

    FOR v_index IN 1..v_len LOOP
        v_code := ascii(substring(p_name FROM v_index FOR 1));
        IF NOT public.display_name_code_is_letter_or_digit(v_code)
           AND NOT public.display_name_code_is_special(v_code) THEN
            RETURN FALSE;
        END IF;
    END LOOP;

    RETURN TRUE;
END;
$$;

REVOKE ALL ON FUNCTION public.display_name_characters_allowed(TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.display_name_code_is_letter_or_digit(INT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.display_name_code_is_special(INT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.display_name_characters_allowed(TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.display_name_code_is_letter_or_digit(INT) TO service_role;
GRANT EXECUTE ON FUNCTION public.display_name_code_is_special(INT) TO service_role;

CREATE OR REPLACE FUNCTION public.profile_set_display_name_tx(
    p_profile_id UUID,
    p_display_name TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_name TEXT := NULLIF(btrim(COALESCE(p_display_name, '')), '');
    v_current TEXT;
    v_saved TEXT;
    v_key TEXT;
    v_current_key TEXT;
    v_same_name BOOLEAN;
    v_sole_holder BOOLEAN;
BEGIN
    IF NOT public.display_name_characters_allowed(v_name) THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
    END IF;

    SELECT display_name
    INTO v_current
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    v_key := lower(v_name);
    v_current_key := lower(btrim(COALESCE(v_current, '')));
    v_same_name := v_current_key <> '' AND v_current_key = v_key;

    -- Serialize claims of this normalized name. The partial unique index
    -- still reports name_taken if two enforced writes race past the lock.
    PERFORM pg_advisory_xact_lock(hashtextextended('player_display_name:' || v_key, 0));

    v_sole_holder := NOT EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id <> p_profile_id
          AND display_name IS NOT NULL
          AND btrim(display_name) <> ''
          AND lower(btrim(display_name)) = v_key
    );

    -- Rule (a): a different name is blocked by every existing holder,
    -- grandfathered or enforced. Re-setting this profile's own name is not.
    IF NOT v_same_name AND NOT v_sole_holder THEN
        RETURN jsonb_build_object('success', false, 'error', 'name_taken');
    END IF;

    UPDATE public.player_profiles
    SET display_name = v_name,
        display_name_claimed_at = CASE
            WHEN v_sole_holder THEN COALESCE(display_name_claimed_at, now())
            ELSE display_name_claimed_at
        END
    WHERE player_profile_id = p_profile_id
    RETURNING display_name INTO v_saved;

    IF v_saved IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'player_profile_id', p_profile_id,
        'display_name', v_saved
    );
EXCEPTION
    WHEN unique_violation THEN
        RETURN jsonb_build_object('success', false, 'error', 'name_taken');
END;
$$;

REVOKE ALL ON FUNCTION public.profile_set_display_name_tx(UUID, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_set_display_name_tx(UUID, TEXT) TO service_role;

CREATE OR REPLACE FUNCTION public.profile_set_chainling_display_name_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_display_name TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_name TEXT := NULLIF(BTRIM(COALESCE(p_display_name, '')), '');
BEGIN
    IF p_profile_id IS NULL OR p_chainling_id IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_and_chainling_required');
    END IF;

    IF NOT public.display_name_characters_allowed(v_name) THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
    END IF;

    UPDATE public.chainlings
    SET display_name = v_name,
        updated_at = NOW()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'chainling_not_found');
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'chainling_id', p_chainling_id,
        'display_name', v_name
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_set_chainling_display_name_tx(UUID, UUID, TEXT)
FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_set_chainling_display_name_tx(UUID, UUID, TEXT)
TO service_role;

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
        IF NOT public.display_name_characters_allowed(v_display_name) THEN
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
        UPDATE public.chainlings
        SET display_name = v_display_name,
            updated_at = now()
        WHERE id = v_chainling.id
          AND display_name IS NULL
        RETURNING * INTO v_chainling;
    END IF;

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

REVOKE ALL ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT, TEXT) TO service_role;

NOTIFY pgrst, 'reload schema';
