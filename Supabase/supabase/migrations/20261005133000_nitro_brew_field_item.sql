-- Nitro Brew: profile bag catalog row and a field-use branch that only decrements the stack.
-- Movement speed is applied by the client after a successful use.

INSERT INTO public.chainbound_item_catalog (
    item_id, display_name, description, category, effect_family, ownership_kind,
    max_stack, usable_in_menu, usable_in_battle, bindery_only, use_ram_cost
) VALUES (
    'nitro_brew',
    'Nitro Brew',
    'A short-burn motor brew. Movement quickens, then settles back to your normal pace.',
    'field',
    'movement_speed_boost',
    'profile_stack',
    99,
    true,
    false,
    false,
    0
)
ON CONFLICT (item_id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    category = EXCLUDED.category,
    effect_family = EXCLUDED.effect_family,
    ownership_kind = EXCLUDED.ownership_kind,
    max_stack = EXCLUDED.max_stack,
    usable_in_menu = EXCLUDED.usable_in_menu,
    usable_in_battle = EXCLUDED.usable_in_battle,
    bindery_only = EXCLUDED.bindery_only,
    use_ram_cost = EXCLUDED.use_ram_cost,
    enabled = true,
    updated_at = now();
CREATE OR REPLACE FUNCTION public.profile_use_field_item_tx(
    p_profile_id UUID,
    p_item_id TEXT,
    p_target_chainling_id UUID DEFAULT NULL,
    p_idempotency_key TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_catalog public.chainbound_item_catalog%ROWTYPE;
    v_stack public.chainbound_profile_item_stacks%ROWTYPE;
    v_chainling public.chainlings%ROWTYPE;
    v_heal INT := 0;
    v_current INT;
    v_next INT;
    v_patches JSONB := '[]'::jsonb;
    v_party RECORD;
    v_any_healed BOOLEAN := false;
    v_favor_points INT := 0;
    v_favor_battles INT := 0;
    v_event_chainling UUID;
BEGIN
    SELECT * INTO v_catalog
    FROM public.chainbound_item_catalog
    WHERE item_id = TRIM(p_item_id)
      AND enabled = true;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'unknown_item';
    END IF;

    IF v_catalog.category NOT IN ('field', 'favor') THEN
        RAISE EXCEPTION 'item_not_field_or_favor';
    END IF;

    SELECT * INTO v_stack
    FROM public.chainbound_profile_item_stacks
    WHERE player_profile_id = p_profile_id
      AND item_id = TRIM(p_item_id)
    FOR UPDATE;

    IF NOT FOUND OR COALESCE(v_stack.quantity, 0) <= 0 THEN
        RAISE EXCEPTION 'item_not_in_inventory';
    END IF;

    -- Heal amounts mirrored from PlayerItemCatalogSeed.
    v_heal := CASE TRIM(p_item_id)
        WHEN 'chainling_patch' THEN 30
        WHEN 'chainling_update' THEN 70
        WHEN 'chainling_reformat' THEN 9999
        WHEN 'system_flush' THEN 40
        WHEN 'party_patch' THEN 15
        ELSE 0
    END;

    v_favor_points := CASE TRIM(p_item_id)
        WHEN 'favor_boost_s' THEN 100
        WHEN 'favor_boost_m' THEN 100
        WHEN 'favor_boost_l' THEN 110
        ELSE 0
    END;
    v_favor_battles := CASE TRIM(p_item_id)
        WHEN 'favor_boost_s' THEN 3
        WHEN 'favor_boost_m' THEN 6
        WHEN 'favor_boost_l' THEN 10
        ELSE 0
    END;

    v_event_chainling := p_target_chainling_id;
    IF v_event_chainling IS NULL THEN
        SELECT c.id INTO v_event_chainling
        FROM public.chainlings c
        WHERE c.player_profile_id = p_profile_id
        ORDER BY COALESCE(c.is_party_member, false) DESC, COALESCE(c.party_slot, 999), c.id
        LIMIT 1;
    END IF;

    IF COALESCE(NULLIF(TRIM(p_idempotency_key), ''), '') <> '' AND v_event_chainling IS NOT NULL THEN
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
            v_event_chainling,
            'field_item_use',
            TRIM(p_item_id),
            jsonb_build_object('item_id', TRIM(p_item_id))
        )
        ON CONFLICT (idempotency_key) DO NOTHING;

        IF NOT FOUND THEN
            RETURN jsonb_build_object('applied', false, 'error', 'idempotent_replay');
        END IF;
    END IF;

    IF v_catalog.effect_family IN ('heal_single', 'status_clear') THEN
        IF p_target_chainling_id IS NULL THEN
            RAISE EXCEPTION 'target_required';
        END IF;

        SELECT * INTO v_chainling
        FROM public.chainlings
        WHERE id = p_target_chainling_id
          AND player_profile_id = p_profile_id
        FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'target_not_in_roster';
        END IF;

        v_current := COALESCE(v_chainling.current_hp, 999999);
        IF COALESCE(v_chainling.current_hp, 1) <= 0 THEN
            RAISE EXCEPTION 'target_fainted_use_revive';
        END IF;
        IF v_chainling.current_hp IS NULL THEN
            RAISE EXCEPTION 'already_full_hp';
        END IF;

        v_next := v_current + v_heal;
        IF v_next <= v_current THEN
            RAISE EXCEPTION 'already_full_hp';
        END IF;

        UPDATE public.chainlings
        SET current_hp = v_next,
            hp_synced_at = now()
        WHERE id = v_chainling.id;

        v_patches := jsonb_build_array(jsonb_build_object(
            'chainling_id', v_chainling.id,
            'current_hp', v_next,
            'healed_amount', v_heal
        ));

    ELSIF v_catalog.effect_family = 'revive' THEN
        IF p_target_chainling_id IS NULL THEN
            RAISE EXCEPTION 'target_required';
        END IF;

        SELECT * INTO v_chainling
        FROM public.chainlings
        WHERE id = p_target_chainling_id
          AND player_profile_id = p_profile_id
        FOR UPDATE;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'target_not_in_roster';
        END IF;

        IF COALESCE(v_chainling.current_hp, 1) > 0 THEN
            RAISE EXCEPTION 'target_not_fainted';
        END IF;

        v_next := GREATEST(1, 50);

        UPDATE public.chainlings
        SET current_hp = v_next,
            hp_synced_at = now()
        WHERE id = v_chainling.id;

        v_patches := jsonb_build_array(jsonb_build_object(
            'chainling_id', v_chainling.id,
            'current_hp', v_next,
            'healed_amount', v_next
        ));

    ELSIF v_catalog.effect_family = 'heal_party' THEN
        FOR v_party IN
            SELECT c.*
            FROM public.chainlings c
            WHERE c.player_profile_id = p_profile_id
              AND COALESCE(c.is_party_member, false) = true
            FOR UPDATE OF c
        LOOP
            IF v_party.current_hp IS NULL OR v_party.current_hp <= 0 THEN
                CONTINUE;
            END IF;
            v_next := v_party.current_hp + v_heal;
            UPDATE public.chainlings
            SET current_hp = v_next,
                hp_synced_at = now()
            WHERE id = v_party.id;
            v_patches := v_patches || jsonb_build_array(jsonb_build_object(
                'chainling_id', v_party.id,
                'current_hp', v_next,
                'healed_amount', v_heal
            ));
            v_any_healed := true;
        END LOOP;

        IF NOT v_any_healed THEN
            RAISE EXCEPTION 'party_already_full_hp';
        END IF;

    ELSIF v_catalog.effect_family = 'favor_boost' THEN
        NULL;
    ELSIF v_catalog.effect_family = 'movement_speed_boost' THEN
        NULL;
    ELSE
        RAISE EXCEPTION 'unsupported_field_effect';
    END IF;

    UPDATE public.chainbound_profile_item_stacks
    SET quantity = quantity - 1,
        updated_at = now()
    WHERE id = v_stack.id;

    DELETE FROM public.chainbound_profile_item_stacks
    WHERE id = v_stack.id
      AND quantity <= 0;

    RETURN jsonb_build_object(
        'applied', true,
        'item_id', TRIM(p_item_id),
        'effect_family', v_catalog.effect_family,
        'hp_patches', v_patches,
        'favor_points', v_favor_points,
        'favor_duration_battles', v_favor_battles,
        'remaining_quantity', GREATEST(0, v_stack.quantity - 1)
    );
END;
$$;
REVOKE ALL ON FUNCTION public.profile_use_field_item_tx(UUID, TEXT, UUID, TEXT) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.profile_use_field_item_tx(UUID, TEXT, UUID, TEXT) TO service_role;
