-- Candy Machine multi-pull bundles. One request rolls N profile-native Known
-- moves in a single transaction: the RAM for every pull is debited together or
-- not at all, and a retried bundle key replays the whole bundle.
--
-- Each pull keeps the single-roll shape (one ownership_known_moves row, one
-- move_items row, one settled 'known_roll' ledger operation of 5 RAM) under the
-- per-pull key '<bundle key>#<index>', so inventory, ledger reads and lifetime
-- stats need no bundle awareness. The bundle row pins pull_count so a retry can
-- never top up or trim a bundle that already settled.

CREATE TABLE IF NOT EXISTS public.ownership_known_roll_bundles (
    bundle_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    idempotency_key TEXT NOT NULL CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    cluster TEXT NOT NULL CHECK (cluster IN ('devnet', 'mainnet-beta')),
    pull_count SMALLINT NOT NULL CHECK (pull_count BETWEEN 2 AND 20),
    ram_cost_per_pull INTEGER NOT NULL CHECK (ram_cost_per_pull > 0),
    ram_cost_total INTEGER NOT NULL,
    known_ids UUID[] NOT NULL,
    operation_ids UUID[] NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ownership_known_roll_bundles_idempotency UNIQUE (player_profile_id, idempotency_key),
    CONSTRAINT ownership_known_roll_bundles_cost_total CHECK (ram_cost_total = pull_count * ram_cost_per_pull),
    CONSTRAINT ownership_known_roll_bundles_known_ids_count CHECK (cardinality(known_ids) = pull_count),
    CONSTRAINT ownership_known_roll_bundles_operation_ids_count CHECK (cardinality(operation_ids) = pull_count)
);

ALTER TABLE public.ownership_known_roll_bundles ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.ownership_known_roll_bundles FROM PUBLIC, anon, authenticated;

-- p_pulls is the Edge-derived result for each pull, in pull order:
--   [{ "operation_id", "move_id", "onchain_move_id", "element", "base_power" }, ...]
-- The per-pull idempotency key is derived here, not trusted from the caller.
CREATE OR REPLACE FUNCTION public.ownership_roll_known_bundle_profile_atomic(
    p_player_profile_id UUID,
    p_cluster TEXT,
    p_idempotency_key TEXT,
    p_pulls JSONB,
    p_generator_version INTEGER,
    p_pool_hash TEXT,
    p_ram_cost_per_pull INTEGER
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_profile public.player_profiles%ROWTYPE;
    v_bundle public.ownership_known_roll_bundles%ROWTYPE;
    v_known public.ownership_known_moves%ROWTYPE;
    v_idempotency_key TEXT := NULLIF(BTRIM(COALESCE(p_idempotency_key, '')), '');
    v_pull_count INTEGER;
    v_known_count INTEGER;
    v_ram_cost_total INTEGER;
    v_pull JSONB;
    v_index INTEGER;
    v_pull_key TEXT;
    v_pull_keys TEXT[] := ARRAY[]::TEXT[];
    v_operation_id UUID;
    v_operation_ids UUID[] := ARRAY[]::UUID[];
    v_move_id TEXT;
    v_onchain_move_id INTEGER;
    v_element INTEGER;
    v_base_power INTEGER;
    v_move_item_id UUID;
    v_known_ids UUID[] := ARRAY[]::UUID[];
    v_move_item_ids UUID[] := ARRAY[]::UUID[];
    v_knowns JSONB := '[]'::JSONB;
BEGIN
    IF p_player_profile_id IS NULL THEN RAISE EXCEPTION 'invalid_known_roll_identity'; END IF;
    IF p_cluster NOT IN ('devnet', 'mainnet-beta') THEN RAISE EXCEPTION 'invalid_known_roll_cluster'; END IF;
    IF v_idempotency_key IS NULL OR LENGTH(v_idempotency_key) > 128 THEN
        RAISE EXCEPTION 'invalid_idempotency_key';
    END IF;
    IF p_ram_cost_per_pull IS DISTINCT FROM 5 THEN RAISE EXCEPTION 'invalid_known_roll_ram_cost'; END IF;
    IF p_generator_version IS NULL OR p_generator_version <= 0
       OR p_pool_hash IS NULL OR p_pool_hash !~ '^[0-9a-f]{64}$' THEN
        RAISE EXCEPTION 'invalid_known_roll_result';
    END IF;
    IF p_pulls IS NULL OR jsonb_typeof(p_pulls) <> 'array' THEN RAISE EXCEPTION 'invalid_known_roll_bundle'; END IF;
    v_pull_count := jsonb_array_length(p_pulls);
    IF v_pull_count NOT BETWEEN 2 AND 20 THEN RAISE EXCEPTION 'invalid_pull_count'; END IF;
    v_ram_cost_total := v_pull_count * p_ram_cost_per_pull;

    FOR v_index IN 0 .. v_pull_count - 1 LOOP
        v_pull := p_pulls -> v_index;
        IF jsonb_typeof(v_pull) <> 'object' THEN RAISE EXCEPTION 'invalid_known_roll_bundle'; END IF;
        v_operation_id := NULLIF(v_pull ->> 'operation_id', '')::UUID;
        IF v_operation_id IS NULL OR v_operation_id = ANY(v_operation_ids) THEN
            RAISE EXCEPTION 'invalid_known_roll_bundle';
        END IF;
        v_operation_ids := v_operation_ids || v_operation_id;
        v_pull_keys := v_pull_keys || (v_idempotency_key || '#' || v_index::TEXT);
    END LOOP;

    -- Profile is always the first lock for profile-native economy mutations.
    -- A concurrent retry of the same bundle waits here, then replays below.
    SELECT * INTO v_profile
      FROM public.player_profiles
     WHERE player_profile_id = p_player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'player_profile_not_found'; END IF;

    SELECT * INTO v_bundle
      FROM public.ownership_known_roll_bundles
     WHERE player_profile_id = p_player_profile_id
       AND idempotency_key = v_idempotency_key;

    IF FOUND THEN
        IF v_bundle.pull_count IS DISTINCT FROM v_pull_count::SMALLINT
           OR v_bundle.cluster IS DISTINCT FROM p_cluster
           OR v_bundle.ram_cost_per_pull IS DISTINCT FROM p_ram_cost_per_pull
           OR v_bundle.operation_ids IS DISTINCT FROM v_operation_ids THEN
            RAISE EXCEPTION 'known_roll_bundle_conflict';
        END IF;

        FOR v_index IN 0 .. v_pull_count - 1 LOOP
            v_pull := p_pulls -> v_index;
            SELECT * INTO v_known
              FROM public.ownership_known_moves
             WHERE known_id = v_bundle.known_ids[v_index + 1]
               AND player_profile_id = p_player_profile_id;
            IF NOT FOUND
               OR v_known.idempotency_key IS DISTINCT FROM v_pull_keys[v_index + 1]
               OR v_known.move_id IS DISTINCT FROM NULLIF(BTRIM(v_pull ->> 'move_id'), '')
               OR v_known.onchain_move_id IS DISTINCT FROM (v_pull ->> 'onchain_move_id')::INTEGER
               OR v_known.element IS DISTINCT FROM (v_pull ->> 'element')::INTEGER
               OR v_known.base_power IS DISTINCT FROM (v_pull ->> 'base_power')::INTEGER
               OR v_known.generator_version IS DISTINCT FROM p_generator_version
               OR v_known.pool_hash IS DISTINCT FROM p_pool_hash THEN
                RAISE EXCEPTION 'known_roll_bundle_conflict';
            END IF;
            -- Read, never re-create: a replay must not restore an item the
            -- player has since spent.
            SELECT id INTO v_move_item_id
              FROM public.move_items
             WHERE ownership_known_id = v_known.known_id;
            v_knowns := v_knowns || jsonb_build_array(to_jsonb(v_known));
            v_move_item_ids := v_move_item_ids || v_move_item_id;
        END LOOP;

        RETURN jsonb_build_object(
            'replayed', TRUE,
            'bundle_id', v_bundle.bundle_id,
            'pull_count', v_pull_count,
            'knowns', v_knowns,
            'move_item_ids', to_jsonb(v_move_item_ids),
            'operation_ids', to_jsonb(v_operation_ids),
            'ram_balance', v_profile.ram_balance,
            'ram_balance_version', v_profile.ram_balance_version,
            'ram_cost', v_bundle.ram_cost_total,
            'ram_cost_per_pull', v_bundle.ram_cost_per_pull
        );
    END IF;

    -- A key already spent as a single roll, or per-pull rows/operations with no
    -- bundle row, mean the caller is reusing a key. Fail closed; never merge.
    PERFORM 1
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND (idempotency_key = v_idempotency_key OR idempotency_key = ANY(v_pull_keys));
    IF FOUND THEN RAISE EXCEPTION 'known_roll_idempotency_conflict'; END IF;
    PERFORM 1
      FROM public.ram_ledger_operations
     WHERE cluster = p_cluster
       AND operation_id = ANY(v_operation_ids);
    IF FOUND THEN RAISE EXCEPTION 'known_roll_operation_conflict'; END IF;

    -- A roll never drops a move: refuse the whole bundle when the Known
    -- inventory (unconsumed rows, capped at MAX_KNOWN_MOVES = 50 in Unity and
    -- the program vault) cannot hold every pull. A replay above is exempt.
    SELECT COUNT(*) INTO v_known_count
      FROM public.ownership_known_moves
     WHERE player_profile_id = p_player_profile_id
       AND consumed_at IS NULL;
    IF v_known_count + v_pull_count > 50 THEN
        RAISE EXCEPTION 'inventory_full free_slots=% required=%', GREATEST(0, 50 - v_known_count), v_pull_count;
    END IF;

    -- All-or-nothing: the whole bundle is priced before any pull is written.
    IF v_profile.ram_balance < v_ram_cost_total THEN RAISE EXCEPTION 'insufficient_ram'; END IF;

    FOR v_index IN 0 .. v_pull_count - 1 LOOP
        v_pull := p_pulls -> v_index;
        v_pull_key := v_pull_keys[v_index + 1];
        v_operation_id := v_operation_ids[v_index + 1];
        v_move_id := NULLIF(BTRIM(COALESCE(v_pull ->> 'move_id', '')), '');
        v_onchain_move_id := (v_pull ->> 'onchain_move_id')::INTEGER;
        v_element := (v_pull ->> 'element')::INTEGER;
        v_base_power := (v_pull ->> 'base_power')::INTEGER;
        IF v_move_id IS NULL THEN RAISE EXCEPTION 'move_id_required'; END IF;
        IF v_onchain_move_id IS NULL OR v_onchain_move_id NOT BETWEEN 0 AND 65535
           OR v_element IS NULL OR v_element NOT BETWEEN 0 AND 255
           OR v_base_power IS NULL OR v_base_power NOT BETWEEN 0 AND 65535 THEN
            RAISE EXCEPTION 'invalid_known_roll_result';
        END IF;

        INSERT INTO public.ownership_known_moves(
            player_profile_id, idempotency_key, move_id, onchain_move_id,
            element, base_power, generator_version, pool_hash, acquisition,
            onchain_instance_id
        ) VALUES (
            p_player_profile_id, v_pull_key, v_move_id, v_onchain_move_id,
            v_element, v_base_power, p_generator_version, p_pool_hash, 'candy_roll',
            NULL
        ) RETURNING * INTO v_known;

        INSERT INTO public.move_items(
            player_profile_id, tier, move_id, modifier1_id, modifier2_id,
            locked, exported_asset_id, source, ownership_known_id
        ) VALUES (
            p_player_profile_id, 'ordinal'::public.move_tier, v_move_id, NULL, NULL,
            FALSE, NULL, 'candy_roll', v_known.known_id
        ) RETURNING id INTO v_move_item_id;

        INSERT INTO public.ram_ledger_operations(
            operation_id, player_profile_id, wallet_address, cluster,
            operation_kind, direction, amount, state, metadata_json, settled_at
        ) VALUES (
            v_operation_id, p_player_profile_id, NULL, p_cluster,
            'known_roll', 'debit', p_ram_cost_per_pull, 'settled',
            jsonb_build_object(
                'source', 'profile_owned_known_roll',
                'idempotency_key', v_pull_key,
                'bundle_idempotency_key', v_idempotency_key,
                'bundle_index', v_index,
                'bundle_size', v_pull_count,
                'known_id', v_known.known_id,
                'move_item_id', v_move_item_id,
                'move_id', v_move_id,
                'generator_version', p_generator_version,
                'pool_hash', p_pool_hash
            ),
            now()
        );

        v_known_ids := v_known_ids || v_known.known_id;
        v_move_item_ids := v_move_item_ids || v_move_item_id;
        v_knowns := v_knowns || jsonb_build_array(to_jsonb(v_known));
    END LOOP;

    -- One debit for the whole bundle, so ram_balance_version moves once.
    UPDATE public.player_profiles
       SET ram_balance = ram_balance - v_ram_cost_total
     WHERE player_profile_id = p_player_profile_id
     RETURNING * INTO v_profile;

    INSERT INTO public.ownership_known_roll_bundles(
        player_profile_id, idempotency_key, cluster, pull_count,
        ram_cost_per_pull, ram_cost_total, known_ids, operation_ids
    ) VALUES (
        p_player_profile_id, v_idempotency_key, p_cluster, v_pull_count,
        p_ram_cost_per_pull, v_ram_cost_total, v_known_ids, v_operation_ids
    ) RETURNING * INTO v_bundle;

    RETURN jsonb_build_object(
        'replayed', FALSE,
        'bundle_id', v_bundle.bundle_id,
        'pull_count', v_pull_count,
        'knowns', v_knowns,
        'move_item_ids', to_jsonb(v_move_item_ids),
        'operation_ids', to_jsonb(v_operation_ids),
        'ram_balance', v_profile.ram_balance,
        'ram_balance_version', v_profile.ram_balance_version,
        'ram_cost', v_ram_cost_total,
        'ram_cost_per_pull', p_ram_cost_per_pull
    );
END;
$$;

REVOKE ALL ON FUNCTION public.ownership_roll_known_bundle_profile_atomic(
    UUID, TEXT, TEXT, JSONB, INTEGER, TEXT, INTEGER
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ownership_roll_known_bundle_profile_atomic(
    UUID, TEXT, TEXT, JSONB, INTEGER, TEXT, INTEGER
) TO service_role;
