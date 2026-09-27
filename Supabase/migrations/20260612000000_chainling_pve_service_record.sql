-- Chainbound - Chainling PvE service record.
-- Additive migration for battle-end PvE stat persistence and service-record reads.

ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_total_victories INT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_xp_earned_lifetime BIGINT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_progression_moves_granted INT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_damage_dealt BIGINT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_damage_taken BIGINT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_damage_mitigated BIGINT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_healing_total BIGINT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS pve_enemy_kos INT NOT NULL DEFAULT 0;
ALTER TABLE public.chainlings ADD COLUMN IF NOT EXISTS service_record_version SMALLINT NOT NULL DEFAULT 1;
CREATE TABLE IF NOT EXISTS public.chainling_stat_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key TEXT NOT NULL UNIQUE,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    chainling_id UUID NOT NULL REFERENCES public.chainlings(id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    metric_key TEXT,
    delta_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_chainling_stat_events_chainling_id
    ON public.chainling_stat_events (chainling_id, created_at DESC);
ALTER TABLE public.chainling_stat_events ENABLE ROW LEVEL SECURITY;
CREATE OR REPLACE FUNCTION public.chainling_pve_service_record_json(
    p_chainling public.chainlings
)
RETURNS JSONB
LANGUAGE sql
STABLE
SET search_path = public
AS $$
    SELECT jsonb_build_object(
        'backend_chainling_id', p_chainling.id,
        'service_record_version', COALESCE(p_chainling.service_record_version, 1),
        'total_victories', COALESCE(p_chainling.pve_total_victories, 0),
        'xp_earned_lifetime', COALESCE(p_chainling.pve_xp_earned_lifetime, 0),
        'progression_moves_granted', COALESCE(p_chainling.pve_progression_moves_granted, 0),
        'damage_dealt', COALESCE(p_chainling.pve_damage_dealt, 0),
        'damage_taken', COALESCE(p_chainling.pve_damage_taken, 0),
        'damage_mitigated', COALESCE(p_chainling.pve_damage_mitigated, 0),
        'healing_total', COALESCE(p_chainling.pve_healing_total, 0),
        'enemy_kos', COALESCE(p_chainling.pve_enemy_kos, 0),
        'mastery_xp', COALESCE(p_chainling.mastery_xp, 0),
        'stars', COALESCE(p_chainling.stars, 0)
    );
$$;
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
CREATE OR REPLACE FUNCTION public.profile_increment_chainling_metric_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_metric_key TEXT,
    p_amount INTEGER,
    p_idempotency_key TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_amount INTEGER;
BEGIN
    v_amount := GREATEST(0, COALESCE(p_amount, 0));
    IF v_amount <= 0 THEN
        RAISE EXCEPTION 'invalid_metric_amount';
    END IF;

    IF p_metric_key <> 'pve.progression_moves_granted' THEN
        RAISE EXCEPTION 'unsupported_metric_key';
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
            'metric_increment',
            p_metric_key,
            jsonb_build_object('amount', v_amount)
        )
        ON CONFLICT (idempotency_key) DO NOTHING;

        IF NOT FOUND THEN
            RETURN jsonb_build_object(
                'applied', false,
                'chainling', public.chainling_pve_service_record_json(v_chainling)
            );
        END IF;
    END IF;

    UPDATE public.chainlings
    SET pve_progression_moves_granted = pve_progression_moves_granted + v_amount,
        service_record_version = 1,
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
CREATE OR REPLACE FUNCTION public.profile_apply_pve_player_lifetime_stats_tx(
    p_legacy_player_id UUID,
    p_battles_won INTEGER,
    p_battles_lost INTEGER,
    p_battles_drawn INTEGER,
    p_battles_fled INTEGER,
    p_total_kos_scored INTEGER,
    p_total_kos_taken INTEGER,
    p_total_star_ups INTEGER,
    p_total_ram_earned BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_row public.player_lifetime_stats%ROWTYPE;
BEGIN
    IF p_legacy_player_id IS NULL THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'legacy_player_id_required');
    END IF;

    INSERT INTO public.player_lifetime_stats (
        player_id,
        battles_won,
        battles_lost,
        battles_drawn,
        battles_fled,
        total_kos_scored,
        total_kos_taken,
        total_star_ups,
        total_ram_earned
    )
    VALUES (
        p_legacy_player_id,
        GREATEST(0, COALESCE(p_battles_won, 0)),
        GREATEST(0, COALESCE(p_battles_lost, 0)),
        GREATEST(0, COALESCE(p_battles_drawn, 0)),
        GREATEST(0, COALESCE(p_battles_fled, 0)),
        GREATEST(0, COALESCE(p_total_kos_scored, 0)),
        GREATEST(0, COALESCE(p_total_kos_taken, 0)),
        GREATEST(0, COALESCE(p_total_star_ups, 0)),
        GREATEST(0, COALESCE(p_total_ram_earned, 0))
    )
    ON CONFLICT (player_id) DO UPDATE
    SET battles_won = public.player_lifetime_stats.battles_won + EXCLUDED.battles_won,
        battles_lost = public.player_lifetime_stats.battles_lost + EXCLUDED.battles_lost,
        battles_drawn = public.player_lifetime_stats.battles_drawn + EXCLUDED.battles_drawn,
        battles_fled = public.player_lifetime_stats.battles_fled + EXCLUDED.battles_fled,
        total_kos_scored = public.player_lifetime_stats.total_kos_scored + EXCLUDED.total_kos_scored,
        total_kos_taken = public.player_lifetime_stats.total_kos_taken + EXCLUDED.total_kos_taken,
        total_star_ups = public.player_lifetime_stats.total_star_ups + EXCLUDED.total_star_ups,
        total_ram_earned = public.player_lifetime_stats.total_ram_earned + EXCLUDED.total_ram_earned,
        updated_at = now()
    RETURNING * INTO v_row;

    RETURN jsonb_build_object('applied', true, 'player_id', v_row.player_id);
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_battle_outcome_tx(UUID, UUID, JSONB, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_increment_chainling_metric_tx(UUID, UUID, TEXT, INTEGER, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_player_lifetime_stats_tx(UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.chainling_pve_service_record_json(public.chainlings) TO service_role;
