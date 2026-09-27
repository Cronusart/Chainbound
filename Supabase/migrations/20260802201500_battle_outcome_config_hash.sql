-- Chainbound — Persist competitive config stamp alongside PvE battle outcome events.
-- No dedicated battles/outcomes table exists; chainling_stat_events is the outcome sink.
-- config_hash is also carried in delta_json from the edge function for forward compatibility.

ALTER TABLE public.chainling_stat_events
    ADD COLUMN IF NOT EXISTS config_hash TEXT;
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
    v_config_hash TEXT;
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

    v_config_hash := NULLIF(TRIM(COALESCE(p_delta_json ->> 'config_hash', '')), '');

    IF COALESCE(NULLIF(TRIM(p_idempotency_key), ''), '') <> '' THEN
        INSERT INTO public.chainling_stat_events (
            idempotency_key,
            player_profile_id,
            chainling_id,
            event_type,
            metric_key,
            delta_json,
            config_hash
        )
        VALUES (
            TRIM(p_idempotency_key),
            p_profile_id,
            p_chainling_id,
            'pve_battle_outcome',
            NULL,
            COALESCE(p_delta_json, '{}'::jsonb),
            v_config_hash
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
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_battle_outcome_tx(UUID, UUID, JSONB, TEXT) TO service_role;
