-- Wild capture grants a Known move only when the client rolled one from the
-- species progression pool. An empty move id is a gate miss: no move_items row,
-- no equipped loadout slot, and no ordinal-acquired increment.

CREATE OR REPLACE FUNCTION public.profile_capture_wild_tx(
    p_profile_id UUID,
    p_species_id TEXT,
    p_idempotency_key TEXT,
    p_ordinal_move_id TEXT,
    p_current_hp INTEGER,
    p_max_hp INTEGER,
    p_base_capture_rate INTEGER,
    p_status_modifier DOUBLE PRECISION DEFAULT 1.0,
    p_device_modifier DOUBLE PRECISION DEFAULT 1.0,
    p_favor_score INTEGER DEFAULT 0,
    p_stars INTEGER DEFAULT 0,
    p_level INTEGER DEFAULT 1,
    p_ivs JSONB DEFAULT '[0,0,0,0,0,0]'::jsonb,
    p_entangled BOOLEAN DEFAULT false,
    p_mutated BOOLEAN DEFAULT false,
    p_visual_variant_id TEXT DEFAULT 'base',
    p_entangled_type TEXT DEFAULT 'none',
    p_mutation_id TEXT DEFAULT 'none'
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_idempotency_key TEXT;
    v_species_id TEXT;
    v_move_id TEXT;
    v_existing JSONB;
    v_current_hp INTEGER;
    v_max_hp INTEGER;
    v_base_rate INTEGER;
    v_status_mod DOUBLE PRECISION;
    v_device_mod DOUBLE PRECISION;
    v_favor INTEGER;
    v_hp_ratio DOUBLE PRECISION;
    v_hp_bonus DOUBLE PRECISION;
    v_effective_rate DOUBLE PRECISION;
    v_probability DOUBLE PRECISION;
    v_favor_t DOUBLE PRECISION;
    v_favor_bonus DOUBLE PRECISION;
    v_roll DOUBLE PRECISION;
    v_success BOOLEAN;
    v_party_count INTEGER;
    v_party_slot INTEGER;
    v_is_party BOOLEAN;
    v_chainling_id UUID;
    v_move_item_id UUID;
    v_stars INTEGER;
    v_level INTEGER;
    v_ivs JSONB;
    v_species_was_new BOOLEAN;
    v_result JSONB;
    v_ordinals_acquired INTEGER;
    v_visual_identity JSONB := jsonb_build_object(
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
    );
    v_visual_variant TEXT := 'base';
    v_entangled_type TEXT := 'none';
    v_mutation_id TEXT := 'none';
BEGIN
    v_idempotency_key := NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '');
    IF v_idempotency_key IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required';
    END IF;

    v_species_id := NULLIF(TRIM(COALESCE(p_species_id, '')), '');
    IF v_species_id IS NULL THEN
        RAISE EXCEPTION 'species_id_required';
    END IF;

    v_move_id := NULLIF(TRIM(COALESCE(p_ordinal_move_id, '')), '');

    v_visual_variant := lower(NULLIF(TRIM(COALESCE(p_visual_variant_id, '')), ''));
    IF v_visual_variant IS NULL OR v_visual_variant IN ('entangled', 'mutated') THEN
        v_visual_variant := 'base';
    END IF;

    IF COALESCE(p_entangled, false) THEN
        v_entangled_type := COALESCE(NULLIF(TRIM(COALESCE(p_entangled_type, '')), ''), 'none');
    ELSE
        v_entangled_type := 'none';
    END IF;

    IF COALESCE(p_mutated, false) THEN
        v_mutation_id := COALESCE(NULLIF(TRIM(COALESCE(p_mutation_id, '')), ''), 'mutation_default');
        IF lower(v_mutation_id) = 'none' THEN
            v_mutation_id := 'mutation_default';
        END IF;
        v_visual_identity := jsonb_set(v_visual_identity, '{aura_id}', to_jsonb(v_mutation_id), true);
    ELSE
        v_mutation_id := 'none';
    END IF;

    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    SELECT result_json
    INTO v_existing
    FROM public.profile_capture_events
    WHERE idempotency_key = v_idempotency_key;

    IF FOUND THEN
        RETURN COALESCE(v_existing, '{}'::jsonb) || jsonb_build_object('already_applied', true);
    END IF;

    v_max_hp := GREATEST(1, COALESCE(p_max_hp, 1));
    v_current_hp := GREATEST(0, LEAST(v_max_hp, COALESCE(p_current_hp, v_max_hp)));
    v_base_rate := GREATEST(0, LEAST(100, COALESCE(p_base_capture_rate, 45)));
    v_status_mod := GREATEST(0.0, COALESCE(p_status_modifier, 1.0));
    v_device_mod := GREATEST(0.0, COALESCE(p_device_modifier, 1.0));
    v_favor := GREATEST(0, COALESCE(p_favor_score, 0));
    v_stars := GREATEST(0, LEAST(5, COALESCE(p_stars, 0)));
    v_level := GREATEST(1, COALESCE(p_level, 1));
    v_ivs := COALESCE(p_ivs, '[0,0,0,0,0,0]'::jsonb);

    -- Mirrors CaptureRateCalculator + FavorConfigSO v1 defaults.
    IF v_current_hp <= 0 THEN
        v_probability := 1.0;
    ELSE
        v_hp_ratio := LEAST(1.0, GREATEST(0.0, v_current_hp::DOUBLE PRECISION / v_max_hp::DOUBLE PRECISION));
        v_hp_bonus := LEAST(1.0, GREATEST(0.0, 1.0 - v_hp_ratio));
        v_effective_rate := v_base_rate * (0.5 + 0.5 * v_hp_bonus);
        v_effective_rate := v_effective_rate * v_status_mod * v_device_mod;
        v_probability := LEAST(1.0, GREATEST(0.0, v_effective_rate / 100.0));
        v_favor_t := v_favor::DOUBLE PRECISION / (v_favor::DOUBLE PRECISION + 100.0);
        v_favor_bonus := 0.08 * v_favor_t;
        v_probability := LEAST(1.0, GREATEST(0.0, v_probability + v_favor_bonus));
    END IF;

    v_roll := random();
    v_success := v_roll < v_probability;

    IF NOT v_success THEN
        v_result := jsonb_build_object(
            'success', false,
            'reason', 'capture_failed',
            'probability', v_probability,
            'roll', v_roll,
            'species_id', v_species_id,
            'already_applied', false
        );

        INSERT INTO public.profile_capture_events (idempotency_key, player_profile_id, result_json)
        VALUES (v_idempotency_key, p_profile_id, v_result);

        RETURN v_result;
    END IF;

    SELECT COUNT(*)::INTEGER
    INTO v_party_count
    FROM public.chainlings
    WHERE player_profile_id = p_profile_id
      AND COALESCE(is_party_member, false) = true;

    IF v_party_count < 3 THEN
        v_is_party := true;
        SELECT COALESCE(MIN(s.slot), v_party_count)
        INTO v_party_slot
        FROM generate_series(0, 2) AS s(slot)
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.chainlings c
            WHERE c.player_profile_id = p_profile_id
              AND COALESCE(c.is_party_member, false) = true
              AND c.party_slot = s.slot
        );
    ELSE
        v_is_party := false;
        v_party_slot := NULL;
    END IF;

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
        visual_identity,
        entangled_flag,
        entangled_type,
        mutation_id,
        current_hp
    ) VALUES (
        p_profile_id,
        v_species_id,
        v_is_party,
        v_party_slot,
        v_level,
        0,
        v_ivs,
        '[0,0,0,0,0,0]'::jsonb,
        v_stars,
        0,
        1,
        v_visual_variant,
        v_visual_identity,
        COALESCE(p_entangled, false),
        v_entangled_type,
        v_mutation_id,
        v_max_hp
    )
    RETURNING id INTO v_chainling_id;

    v_ordinals_acquired := 0;
    IF v_move_id IS NOT NULL THEN
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
            'wild_capture'
        )
        RETURNING id INTO v_move_item_id;

        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_chainling_id, 0, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO UPDATE
        SET move_item_id = EXCLUDED.move_item_id;

        v_ordinals_acquired := 1;
    END IF;

    SELECT NOT EXISTS (
        SELECT 1
        FROM public.chainlings c
        WHERE c.player_profile_id = p_profile_id
          AND c.species_id = v_species_id
          AND c.id <> v_chainling_id
    )
    INTO v_species_was_new;

    INSERT INTO public.player_lifetime_stats (
        player_profile_id,
        total_captures,
        unique_species_captured,
        entangled_captures,
        total_ordinals_acquired
    ) VALUES (
        p_profile_id,
        1,
        CASE WHEN v_species_was_new THEN 1 ELSE 0 END,
        CASE WHEN COALESCE(p_entangled, false) THEN 1 ELSE 0 END,
        v_ordinals_acquired
    )
    ON CONFLICT (player_profile_id) DO UPDATE
    SET total_captures = public.player_lifetime_stats.total_captures + 1,
        unique_species_captured = public.player_lifetime_stats.unique_species_captured + EXCLUDED.unique_species_captured,
        entangled_captures = public.player_lifetime_stats.entangled_captures + EXCLUDED.entangled_captures,
        total_ordinals_acquired = public.player_lifetime_stats.total_ordinals_acquired + EXCLUDED.total_ordinals_acquired,
        updated_at = now();

    v_result := jsonb_build_object(
        'success', true,
        'reason', 'captured',
        'probability', v_probability,
        'roll', v_roll,
        'species_id', v_species_id,
        'chainling_id', v_chainling_id,
        'is_party_member', v_is_party,
        'party_slot', v_party_slot,
        'move_item_id', v_move_item_id,
        'move_id', v_move_id,
        'already_applied', false
    );

    INSERT INTO public.profile_capture_events (idempotency_key, player_profile_id, result_json)
    VALUES (v_idempotency_key, p_profile_id, v_result);

    RETURN v_result;
END;
$$;

GRANT EXECUTE ON FUNCTION public.profile_capture_wild_tx(
    UUID, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER,
    DOUBLE PRECISION, DOUBLE PRECISION, INTEGER, INTEGER, INTEGER,
    JSONB, BOOLEAN, BOOLEAN, TEXT, TEXT, TEXT
) TO service_role;
