-- Cycle flat progression contract (comment + function recreate).
-- Storage model unchanged: rebirth_iv_bonuses are allocation COUNTS (sum = rebirth_count).
-- Unity/combat applies flats:
--   auto from rebirth_count: +1 combat stats, +5 MaxHP per Cycle
--   pick from rebirth_iv_bonuses[i]: +1 combat / +5 MaxHP (index 0) per point
-- Base ivs stay wild-rolled; clients must NOT merge bonuses into ivs.

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
    -- p_stat_index: 0=MaxHP, 1=MeleeAtk, 2=MeleeDef, 3=RangedAtk, 4=RangedDef, 5=Speed
    -- Increment is one allocation COUNT. MaxHP application (×5) is client/combat-side only.
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

    -- Store allocation count (+1). Do not apply MaxHP ×5 here.
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

    -- Payload: ivs = base only; rebirth_iv_bonuses = allocation counts (not applied flats).
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
COMMENT ON FUNCTION public.profile_rebirth_chainling_tx(UUID, UUID, UUID, INTEGER, INTEGER) IS
'Cycle (rebirth) transaction. Increments rebirth_count, resets stars/mastery, and adds one allocation COUNT to rebirth_iv_bonuses[stat]. Bonuses are counts (sum = rebirth_count), not merged into ivs. Combat applies auto flats from rebirth_count (+1 combat / +5 MaxHP) and pick flats from counts (+1 combat / +5 MaxHP for index 0).';
GRANT EXECUTE ON FUNCTION public.profile_rebirth_chainling_tx(UUID, UUID, UUID, INTEGER, INTEGER) TO service_role;
