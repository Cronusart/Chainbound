-- Idempotent profile bag grants and consumes for quest items.
-- The edge function resolves the caller; this transaction owns the stack math.

INSERT INTO public.chainbound_item_catalog (
    item_id, display_name, description, category, effect_family, ownership_kind,
    max_stack, usable_in_menu, usable_in_battle, bindery_only, use_ram_cost
) VALUES
    (
        'coffee_beans',
        'Coffee Beans',
        'Zeb''s recovered bean bag. Reynard brews these for Toby.',
        'quest',
        'none',
        'profile_stack',
        1,
        false,
        false,
        false,
        0
    ),
    (
        'cold_beer',
        'Cold Beer',
        'A cold beer from Momo. Toby wants it with his coffee, still hot.',
        'quest',
        'none',
        'profile_stack',
        1,
        false,
        false,
        false,
        0
    ),
    (
        'tobys_delivery',
        'Toby''s Delivery',
        'Two brewed blacks and a cold beer, packed for Professor Toby.',
        'quest',
        'none',
        'profile_stack',
        1,
        false,
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

CREATE TABLE IF NOT EXISTS public.chainbound_profile_item_mutations (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    idempotency_key TEXT NOT NULL,
    grants JSONB NOT NULL DEFAULT '[]'::jsonb,
    consumes JSONB NOT NULL DEFAULT '[]'::jsonb,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_profile_item_mutations_key_nonempty CHECK (length(trim(idempotency_key)) > 0),
    CONSTRAINT chainbound_profile_item_mutations_pk PRIMARY KEY (player_profile_id, idempotency_key)
);

ALTER TABLE public.chainbound_profile_item_mutations ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.chainbound_profile_item_mutations FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.chainbound_profile_item_mutations TO service_role;

CREATE OR REPLACE FUNCTION public.profile_apply_profile_item_delta_tx(
    p_profile_id UUID,
    p_idempotency_key TEXT,
    p_grants JSONB DEFAULT '[]'::jsonb,
    p_consumes JSONB DEFAULT '[]'::jsonb
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_key TEXT := trim(COALESCE(p_idempotency_key, ''));
    v_grants JSONB := COALESCE(p_grants, '[]'::jsonb);
    v_consumes JSONB := COALESCE(p_consumes, '[]'::jsonb);
    v_line JSONB;
    v_item_id TEXT;
    v_quantity INT;
    v_max INT;
    v_catalog public.chainbound_item_catalog%ROWTYPE;
    v_have INT;
    v_inserted UUID;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_id_required');
    END IF;

    IF length(v_key) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'idempotency_key_required');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_not_found');
    END IF;

    IF jsonb_typeof(v_grants) IS DISTINCT FROM 'array'
        OR jsonb_typeof(v_consumes) IS DISTINCT FROM 'array' THEN
        RETURN jsonb_build_object('ok', false, 'error', 'delta_must_be_arrays');
    END IF;

    IF jsonb_array_length(v_grants) = 0 AND jsonb_array_length(v_consumes) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'delta_required');
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.chainbound_profile_item_mutations
        WHERE player_profile_id = p_profile_id
          AND idempotency_key = v_key
    ) THEN
        RETURN jsonb_build_object('ok', true, 'already_applied', true);
    END IF;

    FOR v_line IN SELECT value FROM jsonb_array_elements(v_consumes)
    LOOP
        v_item_id := trim(COALESCE(v_line->>'item_id', ''));
        v_quantity := COALESCE((v_line->>'quantity')::int, 0);
        IF length(v_item_id) = 0 OR v_quantity <= 0 THEN
            RETURN jsonb_build_object('ok', false, 'error', 'invalid_consume');
        END IF;

        SELECT * INTO v_catalog
        FROM public.chainbound_item_catalog
        WHERE item_id = v_item_id;

        IF NOT FOUND OR v_catalog.enabled IS NOT TRUE THEN
            RETURN jsonb_build_object('ok', false, 'error', 'unknown_item', 'item_id', v_item_id);
        END IF;

        IF v_catalog.ownership_kind IS DISTINCT FROM 'profile_stack' THEN
            RETURN jsonb_build_object('ok', false, 'error', 'core_item_not_grantable', 'item_id', v_item_id);
        END IF;
    END LOOP;

    FOR v_line IN SELECT value FROM jsonb_array_elements(v_grants)
    LOOP
        v_item_id := trim(COALESCE(v_line->>'item_id', ''));
        v_quantity := COALESCE((v_line->>'quantity')::int, 0);
        IF length(v_item_id) = 0 OR v_quantity <= 0 THEN
            RETURN jsonb_build_object('ok', false, 'error', 'invalid_grant');
        END IF;

        SELECT * INTO v_catalog
        FROM public.chainbound_item_catalog
        WHERE item_id = v_item_id;

        IF NOT FOUND OR v_catalog.enabled IS NOT TRUE THEN
            RETURN jsonb_build_object('ok', false, 'error', 'unknown_item', 'item_id', v_item_id);
        END IF;

        IF v_catalog.ownership_kind IS DISTINCT FROM 'profile_stack' THEN
            RETURN jsonb_build_object('ok', false, 'error', 'core_item_not_grantable', 'item_id', v_item_id);
        END IF;
    END LOOP;

    FOR v_item_id, v_quantity IN
        SELECT item_id, SUM(quantity)::int
        FROM (
            SELECT trim(value->>'item_id') AS item_id,
                   (value->>'quantity')::int AS quantity
            FROM jsonb_array_elements(v_consumes)
        ) lines
        GROUP BY item_id
    LOOP
        SELECT quantity INTO v_have
        FROM public.chainbound_profile_item_stacks
        WHERE player_profile_id = p_profile_id
          AND item_id = v_item_id
        FOR UPDATE;

        IF COALESCE(v_have, 0) < v_quantity THEN
            RETURN jsonb_build_object(
                'ok', false,
                'error', 'insufficient_quantity',
                'item_id', v_item_id
            );
        END IF;
    END LOOP;

    INSERT INTO public.chainbound_profile_item_mutations (
        player_profile_id,
        idempotency_key,
        grants,
        consumes
    )
    VALUES (p_profile_id, v_key, v_grants, v_consumes)
    ON CONFLICT (player_profile_id, idempotency_key) DO NOTHING
    RETURNING player_profile_id INTO v_inserted;

    IF v_inserted IS NULL THEN
        RETURN jsonb_build_object('ok', true, 'already_applied', true);
    END IF;

    FOR v_item_id, v_quantity IN
        SELECT item_id, SUM(quantity)::int
        FROM (
            SELECT trim(value->>'item_id') AS item_id,
                   (value->>'quantity')::int AS quantity
            FROM jsonb_array_elements(v_consumes)
        ) lines
        GROUP BY item_id
    LOOP
        UPDATE public.chainbound_profile_item_stacks
        SET quantity = quantity - v_quantity,
            updated_at = now()
        WHERE player_profile_id = p_profile_id
          AND item_id = v_item_id;

        DELETE FROM public.chainbound_profile_item_stacks
        WHERE player_profile_id = p_profile_id
          AND item_id = v_item_id
          AND quantity <= 0;
    END LOOP;

    FOR v_item_id, v_quantity IN
        SELECT item_id, SUM(quantity)::int
        FROM (
            SELECT trim(value->>'item_id') AS item_id,
                   (value->>'quantity')::int AS quantity
            FROM jsonb_array_elements(v_grants)
        ) lines
        GROUP BY item_id
    LOOP
        SELECT max_stack INTO v_max
        FROM public.chainbound_item_catalog
        WHERE item_id = v_item_id;

        INSERT INTO public.chainbound_profile_item_stacks (player_profile_id, item_id, quantity)
        VALUES (p_profile_id, v_item_id, LEAST(v_max, v_quantity))
        ON CONFLICT (player_profile_id, item_id) DO UPDATE
        SET quantity = LEAST(
                v_max,
                public.chainbound_profile_item_stacks.quantity + EXCLUDED.quantity
            ),
            updated_at = now();
    END LOOP;

    RETURN jsonb_build_object('ok', true, 'already_applied', false);
END;
$$;

REVOKE ALL ON FUNCTION public.profile_apply_profile_item_delta_tx(UUID, TEXT, JSONB, JSONB) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.profile_apply_profile_item_delta_tx(UUID, TEXT, JSONB, JSONB) TO service_role;
