-- Account for the configurable PvE defeat RAM cost in the same transaction that
-- records the battle lifetime totals. The requested amount is capped to the
-- player's current balance so a defeat can never make RAM negative.

DROP FUNCTION IF EXISTS public.profile_apply_pve_profile_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT
);

CREATE FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    p_profile_id UUID,
    p_battles_won INTEGER,
    p_battles_lost INTEGER,
    p_battles_drawn INTEGER,
    p_battles_fled INTEGER,
    p_total_kos_scored INTEGER,
    p_total_kos_taken INTEGER,
    p_total_star_ups INTEGER,
    p_total_ram_earned BIGINT,
    p_total_ram_spent BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_row public.player_lifetime_stats%ROWTYPE;
    v_current_ram BIGINT;
    v_ram_earned BIGINT := GREATEST(0, COALESCE(p_total_ram_earned, 0));
    v_actual_ram_spent BIGINT;
    v_ram_balance INTEGER;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'profile_not_found');
    END IF;

    SELECT GREATEST(0, COALESCE(ram_balance, 0))::BIGINT
    INTO v_current_ram
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'profile_not_found');
    END IF;

    v_actual_ram_spent := LEAST(v_current_ram, GREATEST(0, COALESCE(p_total_ram_spent, 0)));

    INSERT INTO public.player_lifetime_stats (
        player_profile_id,
        battles_won,
        battles_lost,
        battles_drawn,
        battles_fled,
        total_kos_scored,
        total_kos_taken,
        total_star_ups,
        total_ram_earned,
        total_ram_spent
    ) VALUES (
        p_profile_id,
        GREATEST(0, COALESCE(p_battles_won, 0)),
        GREATEST(0, COALESCE(p_battles_lost, 0)),
        GREATEST(0, COALESCE(p_battles_drawn, 0)),
        GREATEST(0, COALESCE(p_battles_fled, 0)),
        GREATEST(0, COALESCE(p_total_kos_scored, 0)),
        GREATEST(0, COALESCE(p_total_kos_taken, 0)),
        GREATEST(0, COALESCE(p_total_star_ups, 0)),
        v_ram_earned,
        v_actual_ram_spent
    )
    ON CONFLICT (player_profile_id) DO UPDATE
    SET battles_won = public.player_lifetime_stats.battles_won + EXCLUDED.battles_won,
        battles_lost = public.player_lifetime_stats.battles_lost + EXCLUDED.battles_lost,
        battles_drawn = public.player_lifetime_stats.battles_drawn + EXCLUDED.battles_drawn,
        battles_fled = public.player_lifetime_stats.battles_fled + EXCLUDED.battles_fled,
        total_kos_scored = public.player_lifetime_stats.total_kos_scored + EXCLUDED.total_kos_scored,
        total_kos_taken = public.player_lifetime_stats.total_kos_taken + EXCLUDED.total_kos_taken,
        total_star_ups = public.player_lifetime_stats.total_star_ups + EXCLUDED.total_star_ups,
        total_ram_earned = public.player_lifetime_stats.total_ram_earned + EXCLUDED.total_ram_earned,
        total_ram_spent = public.player_lifetime_stats.total_ram_spent + EXCLUDED.total_ram_spent,
        updated_at = now()
    RETURNING * INTO v_row;

    UPDATE public.player_profiles
    SET ram_balance = LEAST(
            2147483647::BIGINT,
            GREATEST(0::BIGINT, ram_balance::BIGINT + v_ram_earned - v_actual_ram_spent)
        )::INTEGER
    WHERE player_profile_id = p_profile_id
    RETURNING ram_balance INTO v_ram_balance;

    RETURN jsonb_build_object(
        'applied', true,
        'player_profile_id', v_row.player_profile_id,
        'ram_earned', v_ram_earned,
        'ram_spent', v_actual_ram_spent,
        'ram_balance', COALESCE(v_ram_balance, 0)
    );
END;
$$;

GRANT EXECUTE ON FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT, BIGINT
) TO service_role;
