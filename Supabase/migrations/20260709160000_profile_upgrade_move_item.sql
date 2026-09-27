-- Chainbound - Archive Dispenser move tier upgrades (Known -> Stamped -> Bound).
-- Player-facing: profile_upgrade_move_item edge fn -> profile_upgrade_move_item_tx.

CREATE TABLE IF NOT EXISTS public.move_upgrade_events (
    idempotency_key TEXT PRIMARY KEY,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    player_id UUID NOT NULL REFERENCES public.players(id) ON DELETE CASCADE,
    source_move_item_id UUID NOT NULL,
    target_tier public.move_tier NOT NULL,
    result_move_item_id UUID REFERENCES public.move_items(id) ON DELETE SET NULL,
    ram_cost INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_move_upgrade_events_player_profile
    ON public.move_upgrade_events (player_profile_id, created_at DESC);
ALTER TABLE public.move_upgrade_events ENABLE ROW LEVEL SECURITY;
CREATE OR REPLACE FUNCTION public.profile_upgrade_move_item_tx(
    p_profile_id UUID,
    p_legacy_player_id UUID,
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
    WHERE idempotency_key = v_idempotency_key;

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
      AND player_id = p_legacy_player_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'move_item_not_found_or_not_owned';
    END IF;

    IF COALESCE(v_source.locked, false) THEN
        RAISE EXCEPTION 'move_item_locked';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.loadouts
        WHERE move_item_id = v_source.id
    ) THEN
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

        v_modifier1 := NULLIF(TRIM(COALESCE(p_modifier1_id, '')), '');
        IF v_modifier1 IS NULL THEN
            v_modifier1 := NULLIF(TRIM(COALESCE(v_source.modifier1_id, '')), '');
        END IF;

        IF v_modifier1 IS NULL THEN
            RAISE EXCEPTION 'modifier1_required';
        END IF;

        v_modifier2 := NULLIF(TRIM(COALESCE(p_modifier2_id, '')), '');
        IF v_modifier2 IS NULL THEN
            RAISE EXCEPTION 'modifier2_required';
        END IF;

        IF v_modifier2 = v_modifier1 THEN
            RAISE EXCEPTION 'modifier2_must_differ';
        END IF;

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
        player_id,
        source_move_item_id,
        target_tier,
        ram_cost
    )
    VALUES (
        v_idempotency_key,
        p_profile_id,
        p_legacy_player_id,
        v_source.id,
        p_target_tier,
        p_ram_cost
    )
    ON CONFLICT (idempotency_key) DO NOTHING;

    IF NOT FOUND THEN
        SELECT result_move_item_id
        INTO v_existing_result
        FROM public.move_upgrade_events
        WHERE idempotency_key = v_idempotency_key;

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
        UPDATE public.players
        SET ram_balance = ram_balance - p_ram_cost
        WHERE id = p_legacy_player_id
          AND ram_balance >= p_ram_cost
        RETURNING ram_balance INTO v_ram_balance;

        IF NOT FOUND THEN
            DELETE FROM public.move_upgrade_events WHERE idempotency_key = v_idempotency_key;
            RAISE EXCEPTION 'insufficient_ram';
        END IF;
    ELSE
        SELECT ram_balance INTO v_ram_balance
        FROM public.players
        WHERE id = p_legacy_player_id;
    END IF;

    DELETE FROM public.move_items
    WHERE id = v_source.id
      AND player_id = p_legacy_player_id;

    INSERT INTO public.move_items (
        player_id,
        tier,
        move_id,
        modifier1_id,
        modifier2_id,
        locked,
        exported_asset_id,
        source
    )
    VALUES (
        p_legacy_player_id,
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
        UPDATE public.players
        SET vending_upg_ord_today = COALESCE(vending_upg_ord_today, 0) + 1
        WHERE id = p_legacy_player_id;

        INSERT INTO public.player_lifetime_stats (player_id, total_inscriptions_created, total_ram_spent)
        VALUES (p_legacy_player_id, 1, p_ram_cost)
        ON CONFLICT (player_id) DO UPDATE
        SET total_inscriptions_created = public.player_lifetime_stats.total_inscriptions_created + 1,
            total_ram_spent = public.player_lifetime_stats.total_ram_spent + p_ram_cost,
            updated_at = now();
    ELSE
        UPDATE public.players
        SET vending_upg_rune_today = COALESCE(vending_upg_rune_today, 0) + 1
        WHERE id = p_legacy_player_id;

        INSERT INTO public.player_lifetime_stats (player_id, total_ram_spent)
        VALUES (p_legacy_player_id, p_ram_cost)
        ON CONFLICT (player_id) DO UPDATE
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
    UUID, UUID, UUID, public.move_tier, TEXT, TEXT, INTEGER, TEXT
) TO service_role;
-- Progression grants must populate required move_items.source on remote schema.
CREATE OR REPLACE FUNCTION public.profile_grant_progression_ordinal_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_move_id TEXT,
    p_source_event_id TEXT,
    p_idempotency_key TEXT,
    p_event_type TEXT,
    p_star_index INTEGER,
    p_mastery_cycle_index INTEGER,
    p_battle_seed INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_chainling public.chainlings%ROWTYPE;
    v_profile public.player_profiles%ROWTYPE;
    v_move_item_id UUID;
    v_existing_move_item_id UUID;
    v_idempotency_key TEXT;
    v_move_id TEXT;
BEGIN
    v_move_id := NULLIF(TRIM(COALESCE(p_move_id, '')), '');
    IF v_move_id IS NULL THEN
        RAISE EXCEPTION 'move_id_required';
    END IF;

    v_idempotency_key := NULLIF(TRIM(COALESCE(p_idempotency_key, '')), '');
    IF v_idempotency_key IS NULL THEN
        RAISE EXCEPTION 'idempotency_key_required';
    END IF;

    SELECT *
    INTO v_profile
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    IF v_profile.legacy_player_id IS NULL THEN
        RAISE EXCEPTION 'legacy_player_id_required';
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

    INSERT INTO public.chainling_stat_events (
        idempotency_key,
        player_profile_id,
        chainling_id,
        event_type,
        metric_key,
        delta_json
    )
    VALUES (
        v_idempotency_key,
        p_profile_id,
        p_chainling_id,
        'progression_ordinal_grant',
        'pve.progression_moves_granted',
        jsonb_build_object(
            'move_id', v_move_id,
            'source_event_id', COALESCE(p_source_event_id, ''),
            'event_type', COALESCE(p_event_type, ''),
            'star_index', GREATEST(0, COALESCE(p_star_index, 0)),
            'mastery_cycle_index', GREATEST(0, COALESCE(p_mastery_cycle_index, 0)),
            'battle_seed', COALESCE(p_battle_seed, 0)
        )
    )
    ON CONFLICT (idempotency_key) DO NOTHING;

    IF NOT FOUND THEN
        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_id', v_move_id
        );
    END IF;

    SELECT id
    INTO v_existing_move_item_id
    FROM public.move_items
    WHERE player_id = v_profile.legacy_player_id
      AND move_id = v_move_id
      AND tier = 'ordinal'::public.move_tier
      AND COALESCE(locked, false) = false
    ORDER BY id
    LIMIT 1;

    IF FOUND THEN
        RETURN jsonb_build_object(
            'success', true,
            'already_applied', true,
            'move_item_id', v_existing_move_item_id,
            'move_id', v_move_id
        );
    END IF;

    INSERT INTO public.move_items (
        player_id,
        tier,
        move_id,
        modifier1_id,
        modifier2_id,
        locked,
        exported_asset_id,
        source
    )
    VALUES (
        v_profile.legacy_player_id,
        'ordinal'::public.move_tier,
        v_move_id,
        NULL,
        NULL,
        false,
        NULL,
        'progression_grant'
    )
    RETURNING id INTO v_move_item_id;

    UPDATE public.chainlings
    SET pve_progression_moves_granted = pve_progression_moves_granted + 1,
        service_record_version = 1,
        updated_at = now()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id;

    RETURN jsonb_build_object(
        'success', true,
        'already_applied', false,
        'move_item_id', v_move_item_id,
        'move_id', v_move_id
    );
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_grant_progression_ordinal_tx(UUID, UUID, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER) TO service_role;
