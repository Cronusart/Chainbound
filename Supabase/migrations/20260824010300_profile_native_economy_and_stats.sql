-- Chainbound - profile-native rebirth, move upgrades, and lifetime stats.

CREATE OR REPLACE FUNCTION public.profile_rebirth_chainling_tx(
    p_profile_id UUID,
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

    UPDATE public.player_profiles
    SET ram_balance = ram_balance - p_cost
    WHERE player_profile_id = p_profile_id
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

    INSERT INTO public.player_lifetime_stats (player_profile_id, total_rebirths, total_ram_spent)
    VALUES (p_profile_id, 1, p_cost)
    ON CONFLICT (player_profile_id) DO UPDATE
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
GRANT EXECUTE ON FUNCTION public.profile_rebirth_chainling_tx(
    UUID, UUID, INTEGER, INTEGER
) TO service_role;
CREATE OR REPLACE FUNCTION public.profile_upgrade_move_item_tx(
    p_profile_id UUID,
    p_source_move_item_id UUID,
    p_target_tier public.move_tier,
    p_modifier1_id TEXT,
    p_modifier2_id TEXT,
    p_ram_cost INTEGER,
    p_idempotency_key TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_source public.move_items%ROWTYPE;
    v_new_move_item_id UUID;
    v_idempotency_key TEXT;
    v_ram_balance INTEGER;
    v_existing_result UUID;
    v_modifier1 TEXT;
    v_modifier2 TEXT;
BEGIN
    v_idempotency_key := NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '');
    IF v_idempotency_key IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required';
    END IF;

    IF p_ram_cost IS NULL OR p_ram_cost < 0 THEN
        RAISE EXCEPTION 'invalid_ram_cost';
    END IF;

    IF p_target_tier NOT IN ('inscription'::public.move_tier, 'rune'::public.move_tier) THEN
        RAISE EXCEPTION 'invalid_target_tier';
    END IF;

    SELECT result_move_item_id
    INTO v_existing_result
    FROM public.move_upgrade_events
    WHERE idempotency_key = v_idempotency_key
      AND player_profile_id = p_profile_id;

    IF FOUND AND v_existing_result IS NOT NULL THEN
        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_item_id', v_existing_result,
            'target_tier', p_target_tier::TEXT
        );
    END IF;

    SELECT *
    INTO v_source
    FROM public.move_items
    WHERE id = p_source_move_item_id
      AND player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'move_item_not_found_or_not_owned';
    END IF;

    IF COALESCE(v_source.locked, false) THEN
        RAISE EXCEPTION 'move_item_locked';
    END IF;

    IF EXISTS (SELECT 1 FROM public.loadouts WHERE move_item_id = v_source.id) THEN
        RAISE EXCEPTION 'move_item_equipped';
    END IF;

    IF p_target_tier = 'inscription'::public.move_tier THEN
        IF v_source.tier <> 'ordinal'::public.move_tier THEN
            RAISE EXCEPTION 'invalid_source_tier_for_stamp';
        END IF;

        v_modifier1 := NULLIF(TRIM(COALESCE(p_modifier1_id, '')), '');
        IF v_modifier1 IS NULL THEN
            RAISE EXCEPTION 'modifier1_required';
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM public.modifiers_catalog
            WHERE modifier_id = v_modifier1 AND enabled = true
        ) THEN
            RAISE EXCEPTION 'modifier1_not_in_catalog';
        END IF;
        v_modifier2 := NULL;
    ELSE
        IF v_source.tier <> 'inscription'::public.move_tier THEN
            RAISE EXCEPTION 'invalid_source_tier_for_bind';
        END IF;

        v_modifier1 := COALESCE(
            NULLIF(TRIM(COALESCE(p_modifier1_id, '')), ''),
            NULLIF(TRIM(COALESCE(v_source.modifier1_id, '')), '')
        );
        v_modifier2 := NULLIF(TRIM(COALESCE(p_modifier2_id, '')), '');

        IF v_modifier1 IS NULL THEN RAISE EXCEPTION 'modifier1_required'; END IF;
        IF v_modifier2 IS NULL THEN RAISE EXCEPTION 'modifier2_required'; END IF;
        IF v_modifier2 = v_modifier1 THEN RAISE EXCEPTION 'modifier2_must_differ'; END IF;

        IF NOT EXISTS (
            SELECT 1 FROM public.modifiers_catalog
            WHERE modifier_id = v_modifier2 AND enabled = true
        ) THEN
            RAISE EXCEPTION 'modifier2_not_in_catalog';
        END IF;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.moves_catalog
        WHERE move_id = v_source.move_id AND enabled = true
    ) THEN
        RAISE EXCEPTION 'move_not_in_catalog';
    END IF;

    INSERT INTO public.move_upgrade_events (
        idempotency_key,
        player_profile_id,
        source_move_item_id,
        target_tier,
        ram_cost
    ) VALUES (
        v_idempotency_key,
        p_profile_id,
        v_source.id,
        p_target_tier,
        p_ram_cost
    )
    ON CONFLICT (idempotency_key) DO NOTHING;

    IF NOT FOUND THEN
        SELECT result_move_item_id
        INTO v_existing_result
        FROM public.move_upgrade_events
        WHERE idempotency_key = v_idempotency_key
          AND player_profile_id = p_profile_id;

        IF v_existing_result IS NOT NULL THEN
            RETURN jsonb_build_object(
                'success', true,
                'already_applied', true,
                'move_item_id', v_existing_result,
                'target_tier', p_target_tier::TEXT
            );
        END IF;

        RAISE EXCEPTION 'upgrade_event_conflict';
    END IF;

    IF p_ram_cost > 0 THEN
        UPDATE public.player_profiles
        SET ram_balance = ram_balance - p_ram_cost
        WHERE player_profile_id = p_profile_id
          AND ram_balance >= p_ram_cost
        RETURNING ram_balance INTO v_ram_balance;

        IF NOT FOUND THEN
            DELETE FROM public.move_upgrade_events WHERE idempotency_key = v_idempotency_key;
            RAISE EXCEPTION 'insufficient_ram';
        END IF;
    ELSE
        SELECT ram_balance INTO v_ram_balance
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;
    END IF;

    DELETE FROM public.move_items
    WHERE id = v_source.id
      AND player_profile_id = p_profile_id;

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
        p_target_tier,
        v_source.move_id,
        v_modifier1,
        v_modifier2,
        false,
        NULL,
        CASE
            WHEN p_target_tier = 'inscription'::public.move_tier THEN 'archive_dispenser_stamp'
            ELSE 'archive_dispenser_bind'
        END
    )
    RETURNING id INTO v_new_move_item_id;

    UPDATE public.move_upgrade_events
    SET result_move_item_id = v_new_move_item_id
    WHERE idempotency_key = v_idempotency_key;

    IF p_target_tier = 'inscription'::public.move_tier THEN
        UPDATE public.player_profiles
        SET vending_upg_ord_today = vending_upg_ord_today + 1
        WHERE player_profile_id = p_profile_id;

        INSERT INTO public.player_lifetime_stats (
            player_profile_id, total_inscriptions_created, total_ram_spent
        ) VALUES (p_profile_id, 1, p_ram_cost)
        ON CONFLICT (player_profile_id) DO UPDATE
        SET total_inscriptions_created = public.player_lifetime_stats.total_inscriptions_created + 1,
            total_ram_spent = public.player_lifetime_stats.total_ram_spent + p_ram_cost,
            updated_at = now();
    ELSE
        UPDATE public.player_profiles
        SET vending_upg_rune_today = vending_upg_rune_today + 1
        WHERE player_profile_id = p_profile_id;

        INSERT INTO public.player_lifetime_stats (player_profile_id, total_ram_spent)
        VALUES (p_profile_id, p_ram_cost)
        ON CONFLICT (player_profile_id) DO UPDATE
        SET total_ram_spent = public.player_lifetime_stats.total_ram_spent + p_ram_cost,
            updated_at = now();
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'already_applied', false,
        'move_item_id', v_new_move_item_id,
        'move_id', v_source.move_id,
        'target_tier', p_target_tier::TEXT,
        'modifier1_id', v_modifier1,
        'modifier2_id', COALESCE(v_modifier2, ''),
        'ram_balance', COALESCE(v_ram_balance, 0)
    );
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM public.move_upgrade_events
        WHERE idempotency_key = v_idempotency_key
          AND result_move_item_id IS NULL;
        RAISE;
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_upgrade_move_item_tx(
    UUID, UUID, public.move_tier, TEXT, TEXT, INTEGER, TEXT
) TO service_role;
CREATE OR REPLACE FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    p_profile_id UUID,
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
    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'profile_not_found');
    END IF;

    INSERT INTO public.player_lifetime_stats (
        player_profile_id,
        battles_won,
        battles_lost,
        battles_drawn,
        battles_fled,
        total_kos_scored,
        total_kos_taken,
        total_star_ups,
        total_ram_earned
    ) VALUES (
        p_profile_id,
        GREATEST(0, COALESCE(p_battles_won, 0)),
        GREATEST(0, COALESCE(p_battles_lost, 0)),
        GREATEST(0, COALESCE(p_battles_drawn, 0)),
        GREATEST(0, COALESCE(p_battles_fled, 0)),
        GREATEST(0, COALESCE(p_total_kos_scored, 0)),
        GREATEST(0, COALESCE(p_total_kos_taken, 0)),
        GREATEST(0, COALESCE(p_total_star_ups, 0)),
        GREATEST(0, COALESCE(p_total_ram_earned, 0))
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
        updated_at = now()
    RETURNING * INTO v_row;

    UPDATE public.player_profiles
    SET ram_balance = LEAST(
            2147483647::BIGINT,
            ram_balance::BIGINT + GREATEST(0, COALESCE(p_total_ram_earned, 0))
        )::INTEGER
    WHERE player_profile_id = p_profile_id;

    RETURN jsonb_build_object('applied', true, 'player_profile_id', v_row.player_profile_id);
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_apply_pve_profile_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT
) TO service_role;
