-- Lucky Crayon side conversation.
-- Academy backpack grants lucky_crayon. Fisher-Ape's reward row consumes it,
-- grants one signal_orb, and credits 15 RAM outside the battle daily cap.
-- Do not apply this file from the editor. Apply it to the project database
-- before the pickup or the reward can succeed for a real profile.

INSERT INTO public.chainbound_item_catalog (
    item_id, display_name, description, category, effect_family, ownership_kind,
    max_stack, usable_in_menu, usable_in_battle, bindery_only, use_ram_cost
) VALUES (
    'lucky_crayon',
    'Lucky Crayon',
    'Fisher-Ape''s lucky crayon. He says it catches the strong ones.',
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

INSERT INTO public.chainbound_world_pickup_spawns (spawn_id, item_id, quantity, enabled, notes)
VALUES (
    'world.pickup.academy_interior.lucky_crayon',
    'lucky_crayon',
    1,
    true,
    'Academy interior second-floor backpack. Grants Fisher-Ape''s lucky crayon.'
)
ON CONFLICT (spawn_id) DO UPDATE SET
    item_id = EXCLUDED.item_id,
    quantity = EXCLUDED.quantity,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();

CREATE TABLE IF NOT EXISTS public.chainbound_quest_rewards (
    reward_id TEXT PRIMARY KEY,
    consume_item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    consume_quantity INT NOT NULL CHECK (consume_quantity > 0),
    grant_item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    grant_quantity INT NOT NULL CHECK (grant_quantity > 0),
    ram_amount BIGINT NOT NULL CHECK (ram_amount >= 0 AND ram_amount <= 4294967295),
    enabled BOOLEAN NOT NULL DEFAULT true,
    notes TEXT NOT NULL DEFAULT '',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chainbound_quest_rewards_id_nonempty CHECK (length(trim(reward_id)) > 0)
);

ALTER TABLE public.chainbound_quest_rewards ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.chainbound_quest_rewards FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.chainbound_quest_rewards TO service_role;

INSERT INTO public.chainbound_quest_rewards (
    reward_id, consume_item_id, consume_quantity, grant_item_id, grant_quantity, ram_amount, enabled, notes
) VALUES (
    'fisher_ape_lucky_crayon',
    'lucky_crayon',
    1,
    'signal_orb',
    1,
    15,
    true,
    'Fisher-Ape thanks the player for his lucky crayon.'
)
ON CONFLICT (reward_id) DO UPDATE SET
    consume_item_id = EXCLUDED.consume_item_id,
    consume_quantity = EXCLUDED.consume_quantity,
    grant_item_id = EXCLUDED.grant_item_id,
    grant_quantity = EXCLUDED.grant_quantity,
    ram_amount = EXCLUDED.ram_amount,
    enabled = EXCLUDED.enabled,
    notes = EXCLUDED.notes,
    updated_at = now();

CREATE OR REPLACE FUNCTION public.profile_apply_quest_reward_tx(
    p_profile_id UUID,
    p_reward_id TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_reward_id TEXT := trim(COALESCE(p_reward_id, ''));
    v_reward public.chainbound_quest_rewards%ROWTYPE;
    v_key TEXT;
    v_delta JSONB;
    v_cluster TEXT;
    v_hex TEXT;
    v_operation_id UUID;
    v_balance BIGINT;
    v_version BIGINT;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_id_required');
    END IF;

    IF length(v_reward_id) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'reward_id_required');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_not_found');
    END IF;

    SELECT * INTO v_reward
    FROM public.chainbound_quest_rewards
    WHERE reward_id = v_reward_id;

    IF NOT FOUND OR v_reward.enabled IS NOT TRUE THEN
        RETURN jsonb_build_object('ok', false, 'error', 'unknown_reward', 'reward_id', v_reward_id);
    END IF;

    PERFORM 1
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;

    v_key := 'quest-reward:' || v_reward_id;

    IF EXISTS (
        SELECT 1
        FROM public.chainbound_profile_item_mutations
        WHERE player_profile_id = p_profile_id
          AND idempotency_key = v_key
    ) THEN
        SELECT ram_balance, ram_balance_version
        INTO v_balance, v_version
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;

        RETURN jsonb_build_object(
            'ok', true,
            'already_applied', true,
            'ram_balance', v_balance,
            'ram_balance_version', v_version
        );
    END IF;

    SELECT cluster INTO v_cluster
    FROM public.battle_reward_settings
    WHERE singleton;

    IF v_cluster IS NULL OR v_cluster NOT IN ('devnet', 'mainnet-beta') THEN
        RETURN jsonb_build_object('ok', false, 'error', 'quest_reward_cluster_unavailable');
    END IF;

    v_delta := public.profile_apply_profile_item_delta_tx(
        p_profile_id,
        v_key,
        jsonb_build_array(jsonb_build_object('item_id', v_reward.grant_item_id, 'quantity', v_reward.grant_quantity)),
        jsonb_build_array(jsonb_build_object('item_id', v_reward.consume_item_id, 'quantity', v_reward.consume_quantity))
    );

    IF COALESCE((v_delta->>'ok')::boolean, false) IS NOT TRUE THEN
        RETURN v_delta;
    END IF;

    IF COALESCE((v_delta->>'already_applied')::boolean, false) IS TRUE THEN
        SELECT ram_balance, ram_balance_version
        INTO v_balance, v_version
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;

        RETURN jsonb_build_object(
            'ok', true,
            'already_applied', true,
            'ram_balance', v_balance,
            'ram_balance_version', v_version
        );
    END IF;

    IF v_reward.ram_amount > 0 THEN
        v_hex := md5(p_profile_id::text || ':quest-reward:' || v_reward_id);
        v_operation_id := (
            substr(v_hex, 1, 8) || '-' ||
            substr(v_hex, 9, 4) || '-' ||
            substr(v_hex, 13, 4) || '-' ||
            substr(v_hex, 17, 4) || '-' ||
            substr(v_hex, 21, 12)
        )::uuid;

        INSERT INTO public.ram_ledger_operations(
            operation_id, player_profile_id, wallet_address, cluster,
            operation_kind, direction, amount, state, metadata_json, settled_at
        ) VALUES (
            v_operation_id, p_profile_id, NULL, v_cluster,
            'quest_reward', 'credit', v_reward.ram_amount, 'settled',
            jsonb_build_object('reward_id', v_reward_id, 'idempotency_key', v_key),
            now()
        );

        UPDATE public.player_profiles
        SET ram_balance = ram_balance + v_reward.ram_amount
        WHERE player_profile_id = p_profile_id
        RETURNING ram_balance, ram_balance_version
        INTO v_balance, v_version;
    ELSE
        SELECT ram_balance, ram_balance_version
        INTO v_balance, v_version
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;
    END IF;

    RETURN jsonb_build_object(
        'ok', true,
        'already_applied', false,
        'ram_balance', v_balance,
        'ram_balance_version', v_version
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_apply_quest_reward_tx(UUID, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_apply_quest_reward_tx(UUID, TEXT) TO service_role;
