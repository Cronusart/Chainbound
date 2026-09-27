-- Chainbound - Chainling persisted HP (battle-end sync).
-- See Docs/Specs/Combat/CHAINLING_HP_STATE.md

ALTER TABLE public.chainlings
    ADD COLUMN IF NOT EXISTS current_hp INT NULL,
    ADD COLUMN IF NOT EXISTS hp_synced_at TIMESTAMPTZ NULL;
CREATE OR REPLACE FUNCTION public.profile_apply_pve_battle_outcome_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_delta_json JSONB,
    p_idempotency_key TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_total_victories INT;
    v_xp_earned BIGINT;
    v_progression_moves INT;
    v_damage_dealt BIGINT;
    v_damage_taken BIGINT;
    v_damage_mitigated BIGINT;
    v_healing_total BIGINT;
    v_enemy_kos INT;
    v_legacy_times_kod INT;
    v_new_stars INT;
    v_current_hp INT;
    v_has_current_hp BOOLEAN;
BEGIN
    SELECT *
    INTO v_chainling
    FROM public.chainlings
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'chainling_not_found_or_not_owned';
    END IF;

    IF COALESCE(NULLIF(TRIM(p_idempotency_key), ''), '') <> '' THEN
        INSERT INTO public.chainling_stat_events (
            idempotency_key,
            player_profile_id,
            chainling_id,
            event_type,
            metric_key,
            delta_json
        )
        VALUES (
            TRIM(p_idempotency_key),
            p_profile_id,
            p_chainling_id,
            'pve_battle_outcome',
            NULL,
            COALESCE(p_delta_json, '{}'::jsonb)
        )
        ON CONFLICT (idempotency_key) DO NOTHING;

        IF NOT FOUND THEN
            RETURN jsonb_build_object(
                'applied', false,
                'chainling', public.chainling_pve_service_record_json(v_chainling)
            );
        END IF;
    END IF;

    v_total_victories := GREATEST(0, COALESCE((p_delta_json ->> 'total_victories')::INT, 0));
    v_xp_earned := GREATEST(0, COALESCE((p_delta_json ->> 'xp_earned_lifetime')::BIGINT, 0));
    v_progression_moves := GREATEST(0, COALESCE((p_delta_json ->> 'progression_moves_granted')::INT, 0));
    v_damage_dealt := GREATEST(0, COALESCE((p_delta_json ->> 'damage_dealt')::BIGINT, 0));
    v_damage_taken := GREATEST(0, COALESCE((p_delta_json ->> 'damage_taken')::BIGINT, 0));
    v_damage_mitigated := GREATEST(0, COALESCE((p_delta_json ->> 'damage_mitigated')::BIGINT, 0));
    v_healing_total := GREATEST(0, COALESCE((p_delta_json ->> 'healing_total')::BIGINT, 0));
    v_enemy_kos := GREATEST(0, COALESCE((p_delta_json ->> 'enemy_kos')::INT, 0));
    v_legacy_times_kod := GREATEST(0, COALESCE((p_delta_json ->> 'legacy_times_kod')::INT, 0));
    v_new_stars := LEAST(5, GREATEST(0, COALESCE((p_delta_json ->> 'new_stars')::INT, 0)));
    v_has_current_hp := p_delta_json ? 'current_hp';
    v_current_hp := GREATEST(0, COALESCE((p_delta_json ->> 'current_hp')::INT, 0));

    UPDATE public.chainlings
    SET pve_total_victories = pve_total_victories + v_total_victories,
        pve_xp_earned_lifetime = pve_xp_earned_lifetime + v_xp_earned,
        pve_progression_moves_granted = pve_progression_moves_granted + v_progression_moves,
        pve_damage_dealt = pve_damage_dealt + v_damage_dealt,
        pve_damage_taken = pve_damage_taken + v_damage_taken,
        pve_damage_mitigated = pve_damage_mitigated + v_damage_mitigated,
        pve_healing_total = pve_healing_total + v_healing_total,
        pve_enemy_kos = pve_enemy_kos + v_enemy_kos,
        service_record_version = 1,
        battles_won = battles_won + v_total_victories,
        kos_scored = kos_scored + v_enemy_kos,
        times_kod = times_kod + v_legacy_times_kod,
        mastery_xp = LEAST(2147483647, COALESCE(mastery_xp, 0)::BIGINT + v_xp_earned)::INT,
        stars = LEAST(5, GREATEST(COALESCE(stars, 0), v_new_stars)),
        current_hp = CASE WHEN v_has_current_hp THEN v_current_hp ELSE current_hp END,
        hp_synced_at = CASE WHEN v_has_current_hp THEN now() ELSE hp_synced_at END,
        updated_at = now()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id
    RETURNING * INTO v_chainling;

    RETURN jsonb_build_object(
        'applied', true,
        'chainling', public.chainling_pve_service_record_json(v_chainling)
    );
END;
$$;
CREATE OR REPLACE FUNCTION public.profile_rebirth_chainling_tx(
    p_profile_id UUID,
    p_legacy_player_id UUID,
    p_chainling_id UUID,
    p_stat_index INTEGER,
    p_cost INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_bonuses INTEGER[] := ARRAY[0, 0, 0, 0, 0, 0];
    v_index INTEGER;
    v_ram_balance INTEGER;
BEGIN
    IF p_stat_index < 0 OR p_stat_index > 5 THEN
        RAISE EXCEPTION 'invalid_stat';
    END IF;

    IF p_cost < 0 THEN
        RAISE EXCEPTION 'invalid_rebirth_cost';
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

    IF COALESCE(v_chainling.stars, 0) < 5 THEN
        RAISE EXCEPTION 'rebirth_requires_star_5';
    END IF;

    IF COALESCE(v_chainling.rebirth_count, 0) >= 5 THEN
        RAISE EXCEPTION 'rebirth_max_reached';
    END IF;

    FOR v_index IN 0..5 LOOP
        v_bonuses[v_index + 1] := COALESCE((v_chainling.rebirth_iv_bonuses ->> v_index)::INTEGER, 0);
    END LOOP;

    v_bonuses[p_stat_index + 1] := v_bonuses[p_stat_index + 1] + 1;

    UPDATE public.players
    SET ram_balance = ram_balance - p_cost
    WHERE id = p_legacy_player_id
      AND ram_balance >= p_cost
    RETURNING ram_balance INTO v_ram_balance;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'insufficient_ram';
    END IF;

    UPDATE public.chainlings
    SET rebirth_count = COALESCE(rebirth_count, 0) + 1,
        stars = 0,
        mastery_xp = 0,
        rebirth_iv_bonuses = to_jsonb(v_bonuses),
        current_hp = NULL,
        hp_synced_at = NULL,
        updated_at = now()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id
    RETURNING * INTO v_chainling;

    INSERT INTO public.player_lifetime_stats (player_id, total_rebirths, total_ram_spent)
    VALUES (p_legacy_player_id, 1, p_cost)
    ON CONFLICT (player_id) DO UPDATE
    SET total_rebirths = public.player_lifetime_stats.total_rebirths + 1,
        total_ram_spent = public.player_lifetime_stats.total_ram_spent + p_cost,
        updated_at = now();

    RETURN jsonb_build_object(
        'ram_balance', v_ram_balance,
        'chainling', jsonb_build_object(
            'backend_chainling_id', v_chainling.id,
            'species_id', v_chainling.species_id,
            'stars', COALESCE(v_chainling.stars, 0),
            'rebirth_count', COALESCE(v_chainling.rebirth_count, 0),
            'evolution_stage', COALESCE(v_chainling.evolution_stage, 1),
            'display_name', COALESCE(v_chainling.display_name, ''),
            'ivs', COALESCE(v_chainling.ivs, '[]'::jsonb),
            'rebirth_iv_bonuses', COALESCE(v_chainling.rebirth_iv_bonuses, '[0,0,0,0,0,0]'::jsonb),
            'entangled_flag', COALESCE(v_chainling.entangled_flag, false),
            'visual_variant_id', COALESCE(v_chainling.visual_variant_id, 'base'),
            'visual_identity', COALESCE(v_chainling.visual_identity, '{}'::jsonb),
            'current_hp', -1
        )
    );
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_battle_outcome_tx(UUID, UUID, JSONB, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_rebirth_chainling_tx(UUID, UUID, UUID, INTEGER, INTEGER) TO service_role;
