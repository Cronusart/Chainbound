-- PvE results are still client-resolved. Preserve non-RAM statistics while
-- ignoring both requested RAM reward and penalty until an authoritative
-- battle verifier exists.

CREATE OR REPLACE FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
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
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_row public.player_lifetime_stats%ROWTYPE;
    v_ram_balance BIGINT;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'profile_not_found');
    END IF;
    SELECT ram_balance INTO v_ram_balance
      FROM public.player_profiles
     WHERE player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'profile_not_found');
    END IF;

    INSERT INTO public.player_lifetime_stats(
        player_profile_id, battles_won, battles_lost, battles_drawn, battles_fled,
        total_kos_scored, total_kos_taken, total_star_ups, total_ram_earned, total_ram_spent
    ) VALUES (
        p_profile_id,
        GREATEST(0, COALESCE(p_battles_won, 0)),
        GREATEST(0, COALESCE(p_battles_lost, 0)),
        GREATEST(0, COALESCE(p_battles_drawn, 0)),
        GREATEST(0, COALESCE(p_battles_fled, 0)),
        GREATEST(0, COALESCE(p_total_kos_scored, 0)),
        GREATEST(0, COALESCE(p_total_kos_taken, 0)),
        GREATEST(0, COALESCE(p_total_star_ups, 0)),
        0,
        0
    )
    ON CONFLICT (player_profile_id) DO UPDATE SET
        battles_won = public.player_lifetime_stats.battles_won + EXCLUDED.battles_won,
        battles_lost = public.player_lifetime_stats.battles_lost + EXCLUDED.battles_lost,
        battles_drawn = public.player_lifetime_stats.battles_drawn + EXCLUDED.battles_drawn,
        battles_fled = public.player_lifetime_stats.battles_fled + EXCLUDED.battles_fled,
        total_kos_scored = public.player_lifetime_stats.total_kos_scored + EXCLUDED.total_kos_scored,
        total_kos_taken = public.player_lifetime_stats.total_kos_taken + EXCLUDED.total_kos_taken,
        total_star_ups = public.player_lifetime_stats.total_star_ups + EXCLUDED.total_star_ups,
        updated_at = now()
    RETURNING * INTO v_row;

    RETURN jsonb_build_object(
        'applied', true,
        'player_profile_id', v_row.player_profile_id,
        'ram_earned', 0,
        'ram_spent', 0,
        'ram_balance', v_ram_balance
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT, BIGINT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT, BIGINT
) TO service_role;
