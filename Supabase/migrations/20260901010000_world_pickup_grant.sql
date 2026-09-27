-- Unique world pickup bag grants. Server owns spawn rewards; Unity sends spawn_id only.
-- Claims are the bag fact. Profile world flags remain the scene-presence fact.

CREATE TABLE IF NOT EXISTS public.chainbound_world_pickup_spawns (
    spawn_id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    quantity INT NOT NULL DEFAULT 1,
    enabled BOOLEAN NOT NULL DEFAULT true,
    notes TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_world_pickup_spawns_id_nonempty CHECK (length(trim(spawn_id)) > 0),
    CONSTRAINT chainbound_world_pickup_spawns_qty_positive CHECK (quantity > 0)
);
CREATE INDEX IF NOT EXISTS idx_chainbound_world_pickup_spawns_item
    ON public.chainbound_world_pickup_spawns (item_id)
    WHERE enabled = true;
DROP TRIGGER IF EXISTS trg_chainbound_world_pickup_spawns_touch_updated_at
    ON public.chainbound_world_pickup_spawns;
CREATE TRIGGER trg_chainbound_world_pickup_spawns_touch_updated_at
    BEFORE UPDATE ON public.chainbound_world_pickup_spawns
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();

CREATE TABLE IF NOT EXISTS public.chainbound_world_pickup_claims (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    spawn_id TEXT NOT NULL REFERENCES public.chainbound_world_pickup_spawns(spawn_id),
    item_id TEXT NOT NULL,
    quantity INT NOT NULL,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_world_pickup_claims_qty_positive CHECK (quantity > 0),
    CONSTRAINT chainbound_world_pickup_claims_unique UNIQUE (player_profile_id, spawn_id)
);
CREATE INDEX IF NOT EXISTS idx_chainbound_world_pickup_claims_profile
    ON public.chainbound_world_pickup_claims (player_profile_id);

ALTER TABLE public.chainbound_world_pickup_spawns ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.chainbound_world_pickup_claims ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.chainbound_world_pickup_spawns FROM PUBLIC;
REVOKE ALL ON TABLE public.chainbound_world_pickup_claims FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.chainbound_world_pickup_spawns TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.chainbound_world_pickup_claims TO service_role;

INSERT INTO public.chainbound_world_pickup_spawns (spawn_id, item_id, quantity, enabled, notes)
VALUES (
    'world.pickup.link1.backpack_01',
    'chainling_patch',
    1,
    true,
    'Link 1 backpack unique pickup'
)
ON CONFLICT (spawn_id) DO UPDATE SET
    item_id = EXCLUDED.item_id,
    quantity = EXCLUDED.quantity,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();

CREATE OR REPLACE FUNCTION public.profile_grant_world_pickup_tx(
    p_profile_id UUID,
    p_spawn_id TEXT,
    p_idempotency_key TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_spawn_id TEXT := TRIM(COALESCE(p_spawn_id, ''));
    v_spawn public.chainbound_world_pickup_spawns%ROWTYPE;
    v_catalog public.chainbound_item_catalog%ROWTYPE;
    v_claim public.chainbound_world_pickup_claims%ROWTYPE;
    v_remaining INT := 0;
    v_next_qty INT := 0;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_id_required');
    END IF;

    IF length(v_spawn_id) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'spawn_id_required');
    END IF;

    SELECT * INTO v_spawn
    FROM public.chainbound_world_pickup_spawns
    WHERE spawn_id = v_spawn_id
    FOR UPDATE;

    IF NOT FOUND OR v_spawn.enabled IS NOT TRUE THEN
        RETURN jsonb_build_object('ok', false, 'error', 'unknown_spawn');
    END IF;

    SELECT * INTO v_catalog
    FROM public.chainbound_item_catalog
    WHERE item_id = v_spawn.item_id;

    IF NOT FOUND OR v_catalog.enabled IS NOT TRUE THEN
        RETURN jsonb_build_object('ok', false, 'error', 'unknown_item');
    END IF;

    IF v_catalog.ownership_kind IS DISTINCT FROM 'profile_stack' THEN
        RETURN jsonb_build_object('ok', false, 'error', 'core_item_not_grantable');
    END IF;

    SELECT * INTO v_claim
    FROM public.chainbound_world_pickup_claims
    WHERE player_profile_id = p_profile_id
      AND spawn_id = v_spawn.spawn_id;

    IF FOUND THEN
        SELECT COALESCE(quantity, 0) INTO v_remaining
        FROM public.chainbound_profile_item_stacks
        WHERE player_profile_id = p_profile_id
          AND item_id = v_spawn.item_id;

        RETURN jsonb_build_object(
            'ok', true,
            'already_claimed', true,
            'item_id', v_spawn.item_id,
            'quantity', v_spawn.quantity,
            'remaining_quantity', COALESCE(v_remaining, 0)
        );
    END IF;

    INSERT INTO public.chainbound_profile_item_stacks (player_profile_id, item_id, quantity)
    VALUES (p_profile_id, v_spawn.item_id, v_spawn.quantity)
    ON CONFLICT (player_profile_id, item_id) DO UPDATE
    SET quantity = LEAST(
            v_catalog.max_stack,
            public.chainbound_profile_item_stacks.quantity + EXCLUDED.quantity
        ),
        updated_at = now();

    SELECT quantity INTO v_next_qty
    FROM public.chainbound_profile_item_stacks
    WHERE player_profile_id = p_profile_id
      AND item_id = v_spawn.item_id;

    INSERT INTO public.chainbound_world_pickup_claims (
        player_profile_id,
        spawn_id,
        item_id,
        quantity
    )
    VALUES (
        p_profile_id,
        v_spawn.spawn_id,
        v_spawn.item_id,
        v_spawn.quantity
    );

    RETURN jsonb_build_object(
        'ok', true,
        'already_claimed', false,
        'item_id', v_spawn.item_id,
        'quantity', v_spawn.quantity,
        'remaining_quantity', COALESCE(v_next_qty, v_spawn.quantity)
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_grant_world_pickup_tx(UUID, TEXT, TEXT) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.profile_grant_world_pickup_tx(UUID, TEXT, TEXT) TO service_role;
