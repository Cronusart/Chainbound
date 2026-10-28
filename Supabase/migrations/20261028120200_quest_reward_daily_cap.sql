-- RAM Shop R2 Task 4 (decision D3): quest rewards count toward the daily earn cap.
--
-- 20261014030000_fisher_ape_lucky_crayon credited a quest's ram_amount outside
-- ram_daily_earnings. RAM_00 section 3 counts quest RAM toward the same 100/UTC-day cap
-- as battle rewards, so the RAM part now uses the battle settlement rule:
--   applied = min(requested, 100 - earned today)
-- A positive applied amount writes one settled 'quest_reward' ledger row (with
-- requested/applied in metadata), bumps ram_daily_earnings and the balance.
-- Zero writes no ledger row (the ledger CHECK requires amount > 0) and reports
-- reason 'daily_cap'. The item consume/grant still applies either way, and a
-- capped one-time reward is reduced, not deferred (RAM_00 section 8).
--
-- Signature, idempotency per profile + reward_id, item handling and the legacy
-- response keys (ok, already_applied, ram_balance, ram_balance_version) are
-- unchanged. New keys: requestedRam, appliedRam, reason, balanceAfter.
--
-- Lock order (RAM_00 profile-first rule): player_profiles -> the reward's item
-- mutation (via profile_apply_profile_item_delta_tx) -> ram_daily_earnings ->
-- ram_ledger_operations.

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
    v_day DATE := (now() AT TIME ZONE 'UTC')::DATE;
    v_daily_cap CONSTANT BIGINT := 100;
    v_earned BIGINT;
    v_applied BIGINT := 0;
    v_reason TEXT;
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
    v_hex := md5(p_profile_id::text || ':quest-reward:' || v_reward_id);
    v_operation_id := (
        substr(v_hex, 1, 8) || '-' ||
        substr(v_hex, 9, 4) || '-' ||
        substr(v_hex, 13, 4) || '-' ||
        substr(v_hex, 17, 4) || '-' ||
        substr(v_hex, 21, 12)
    )::uuid;

    IF EXISTS (
        SELECT 1
        FROM public.chainbound_profile_item_mutations
        WHERE player_profile_id = p_profile_id
          AND idempotency_key = v_key
    ) THEN
        RETURN public.profile_quest_reward_replay(p_profile_id, v_operation_id, v_reward.ram_amount);
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
        RETURN public.profile_quest_reward_replay(p_profile_id, v_operation_id, v_reward.ram_amount);
    END IF;

    IF v_reward.ram_amount > 0 THEN
        INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
        VALUES (p_profile_id, v_day, 0)
        ON CONFLICT (player_profile_id, utc_day) DO NOTHING;
        SELECT earned INTO v_earned
        FROM public.ram_daily_earnings
        WHERE player_profile_id = p_profile_id AND utc_day = v_day
        FOR UPDATE;

        PERFORM 1 FROM public.ram_ledger_operations
        WHERE cluster = v_cluster AND operation_id = v_operation_id
        FOR UPDATE;
        IF FOUND THEN RAISE EXCEPTION 'quest_reward_operation_conflict'; END IF;

        v_applied := LEAST(v_reward.ram_amount, GREATEST(0, v_daily_cap - v_earned));
    END IF;

    IF v_applied > 0 THEN
        INSERT INTO public.ram_ledger_operations(
            operation_id, player_profile_id, wallet_address, cluster,
            operation_kind, direction, amount, state, metadata_json, settled_at
        ) VALUES (
            v_operation_id, p_profile_id, NULL, v_cluster,
            'quest_reward', 'credit', v_applied, 'settled',
            jsonb_build_object(
                'reward_id', v_reward_id,
                'idempotency_key', v_key,
                'requested', v_reward.ram_amount,
                'applied', v_applied,
                'utc_day', v_day
            ),
            now()
        );

        UPDATE public.ram_daily_earnings
        SET earned = earned + v_applied, updated_at = now()
        WHERE player_profile_id = p_profile_id AND utc_day = v_day;

        UPDATE public.player_profiles
        SET ram_balance = ram_balance + v_applied
        WHERE player_profile_id = p_profile_id
          AND ram_balance + v_applied <= 4294967295
        RETURNING ram_balance, ram_balance_version
        INTO v_balance, v_version;
        IF NOT FOUND THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;
        v_reason := CASE WHEN v_applied < v_reward.ram_amount THEN 'daily_cap_partial' ELSE 'applied' END;
    ELSE
        SELECT ram_balance, ram_balance_version
        INTO v_balance, v_version
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;
        v_reason := CASE WHEN v_reward.ram_amount > 0 THEN 'daily_cap' ELSE 'no_ram' END;
    END IF;

    RETURN jsonb_build_object(
        'ok', true,
        'already_applied', false,
        'ram_balance', v_balance,
        'ram_balance_version', v_version,
        'requestedRam', v_reward.ram_amount,
        'appliedRam', v_applied,
        'reason', v_reason,
        'balanceAfter', v_balance
    );
END;
$$;

-- Replay answer: what the first application actually credited, read back from
-- its ledger row (a capped-to-zero application has none).
CREATE OR REPLACE FUNCTION public.profile_quest_reward_replay(
    p_profile_id UUID,
    p_operation_id UUID,
    p_requested BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
STABLE
AS $$
DECLARE
    v_balance BIGINT;
    v_version BIGINT;
    v_applied BIGINT;
BEGIN
    SELECT ram_balance, ram_balance_version
    INTO v_balance, v_version
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id;

    SELECT amount INTO v_applied
    FROM public.ram_ledger_operations
    WHERE player_profile_id = p_profile_id
      AND operation_id = p_operation_id
      AND operation_kind = 'quest_reward'
      AND state = 'settled';

    RETURN jsonb_build_object(
        'ok', true,
        'already_applied', true,
        'ram_balance', v_balance,
        'ram_balance_version', v_version,
        'requestedRam', p_requested,
        'appliedRam', COALESCE(v_applied, 0),
        'reason', CASE
            WHEN v_applied IS NULL AND p_requested > 0 THEN 'daily_cap'
            WHEN v_applied IS NULL THEN 'no_ram'
            WHEN v_applied < p_requested THEN 'daily_cap_partial'
            ELSE 'applied'
        END,
        'balanceAfter', v_balance
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_quest_reward_replay(UUID, UUID, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_apply_quest_reward_tx(UUID, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_apply_quest_reward_tx(UUID, TEXT) TO service_role;
