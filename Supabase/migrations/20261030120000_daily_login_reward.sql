-- Daily login gift v1 (Docs/Specs/Economy/DAILY_LOGIN_REWARDS_v1.md).
--
-- One claim per profile per fixed UTC calendar day, unlocked by The Professor's
-- Order (coffee_and_beer completed, or the Toby handoff flag). The gift is
-- 10 RAM + 1 signal_orb, owned by the daily_login_settings row.
--
-- The RAM is a settled 'daily_login' ledger credit outside the 100/UTC-day earn
-- cap, the same class as a shop purchase: this migration never reads or writes
-- the daily earn bucket (the guard test checks that the table name is absent).
--
-- Every reject happens before the first insert. After the claim row exists, a
-- failure raises, so the claim, the item mutation and the RAM credit roll back
-- together.
--
-- Lock order: player_profiles -> claim insert -> item delta (stacks) ->
-- ram_ledger_operations -> balance update.

CREATE TABLE IF NOT EXISTS public.daily_login_settings (
    singleton BOOLEAN PRIMARY KEY DEFAULT true,
    enabled BOOLEAN NOT NULL DEFAULT true,
    ram_amount BIGINT NOT NULL,
    item_id TEXT NOT NULL,
    item_quantity INTEGER NOT NULL,
    quest_id TEXT NOT NULL,
    unlock_flag_key TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT daily_login_settings_singleton CHECK (singleton),
    CONSTRAINT daily_login_settings_ram_amount CHECK (ram_amount > 0 AND ram_amount <= 4294967295),
    CONSTRAINT daily_login_settings_item_quantity CHECK (item_quantity > 0),
    CONSTRAINT daily_login_settings_item_id_nonempty CHECK (length(trim(item_id)) > 0)
);

INSERT INTO public.daily_login_settings(
    singleton, enabled, ram_amount, item_id, item_quantity, quest_id, unlock_flag_key
) VALUES (
    true, true, 10, 'signal_orb', 1, 'coffee_and_beer', 'world.coffee.toby_delivery_accepted'
)
ON CONFLICT (singleton) DO NOTHING;

CREATE TABLE IF NOT EXISTS public.profile_daily_login_claims (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    utc_day DATE NOT NULL,
    streak_after INTEGER NOT NULL,
    ram_amount BIGINT NOT NULL,
    item_id TEXT NOT NULL,
    item_quantity INTEGER NOT NULL,
    operation_id UUID NOT NULL,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, utc_day),
    CONSTRAINT profile_daily_login_claims_streak CHECK (streak_after >= 1),
    CONSTRAINT profile_daily_login_claims_ram_amount CHECK (ram_amount > 0)
);

CREATE TABLE IF NOT EXISTS public.profile_daily_login (
    player_profile_id UUID PRIMARY KEY REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    last_claim_utc_day DATE NOT NULL,
    current_streak INTEGER NOT NULL,
    longest_streak INTEGER NOT NULL,
    total_claims INTEGER NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT profile_daily_login_streaks CHECK (current_streak >= 1 AND longest_streak >= current_streak),
    CONSTRAINT profile_daily_login_total CHECK (total_claims >= 1)
);

ALTER TABLE public.daily_login_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_daily_login_claims ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_daily_login ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.daily_login_settings FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.profile_daily_login_claims FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.profile_daily_login FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.daily_login_settings TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.profile_daily_login_claims TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.profile_daily_login TO service_role;

-- Eligibility shared by status and claim: the whole quest completed, or the
-- Toby handoff flag stored as boolean true. A profile that only finished
-- fetch_coffee stays locked.
CREATE OR REPLACE FUNCTION public.profile_daily_login_unlocked(
    p_profile_id UUID,
    p_quest_id TEXT,
    p_flag_key TEXT
)
RETURNS BOOLEAN
LANGUAGE sql
SECURITY DEFINER
SET search_path = public
STABLE
AS $$
    SELECT EXISTS (
        SELECT 1
        FROM public.profile_quest_states
        WHERE player_profile_id = p_profile_id
          AND quest_id = p_quest_id
          AND lifecycle = 'completed'
    ) OR EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id
          AND jsonb_typeof(progression_state -> p_flag_key) = 'boolean'
          AND (progression_state ->> p_flag_key)::boolean
    );
$$;

CREATE OR REPLACE FUNCTION public.profile_claim_daily_login_tx(p_profile_id UUID)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_day DATE := (now() AT TIME ZONE 'UTC')::DATE;
    v_next_claim_at TIMESTAMPTZ := ((now() AT TIME ZONE 'UTC')::DATE + 1)::timestamp AT TIME ZONE 'UTC';
    v_settings public.daily_login_settings%ROWTYPE;
    v_cluster TEXT;
    v_claim public.profile_daily_login_claims%ROWTYPE;
    v_summary public.profile_daily_login%ROWTYPE;
    v_have INTEGER;
    v_max INTEGER;
    v_key TEXT;
    v_hex TEXT;
    v_operation_id UUID;
    v_delta JSONB;
    v_streak INTEGER;
    v_balance BIGINT;
    v_version BIGINT;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_id_required');
    END IF;

    SELECT * INTO v_settings FROM public.daily_login_settings WHERE singleton;
    IF NOT FOUND OR v_settings.enabled IS NOT TRUE THEN
        RETURN jsonb_build_object('ok', false, 'error', 'disabled');
    END IF;

    SELECT cluster INTO v_cluster
    FROM public.battle_reward_settings
    WHERE singleton;
    IF v_cluster IS NULL OR v_cluster NOT IN ('devnet', 'mainnet-beta') THEN
        RETURN jsonb_build_object('ok', false, 'error', 'cluster_unavailable');
    END IF;

    PERFORM 1
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_not_found');
    END IF;

    IF NOT public.profile_daily_login_unlocked(p_profile_id, v_settings.quest_id, v_settings.unlock_flag_key) THEN
        RETURN jsonb_build_object('ok', false, 'error', 'not_unlocked');
    END IF;

    SELECT * INTO v_claim
    FROM public.profile_daily_login_claims
    WHERE player_profile_id = p_profile_id AND utc_day = v_day;
    IF FOUND THEN
        SELECT ram_balance, ram_balance_version INTO v_balance, v_version
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id;
        RETURN jsonb_build_object(
            'ok', true,
            'already_claimed', true,
            'utc_day', v_day,
            'next_claim_at', v_next_claim_at,
            'server_now', now(),
            'ram_balance', v_balance,
            'ram_balance_version', v_version,
            'ram_amount', v_claim.ram_amount,
            'item_id', v_claim.item_id,
            'item_quantity', v_claim.item_quantity,
            'streak_after', v_claim.streak_after
        );
    END IF;

    -- A full stack would be clamped by the item delta and still return ok,
    -- spending the day for zero orbs. Refuse instead; the player can use one
    -- and claim later the same UTC day.
    SELECT max_stack INTO v_max
    FROM public.chainbound_item_catalog
    WHERE item_id = v_settings.item_id;
    SELECT quantity INTO v_have
    FROM public.chainbound_profile_item_stacks
    WHERE player_profile_id = p_profile_id AND item_id = v_settings.item_id;
    IF v_max IS NOT NULL AND COALESCE(v_have, 0) >= v_max THEN
        RETURN jsonb_build_object('ok', false, 'error', 'stack_full', 'item_id', v_settings.item_id);
    END IF;

    SELECT * INTO v_summary
    FROM public.profile_daily_login
    WHERE player_profile_id = p_profile_id;
    v_streak := CASE
        WHEN v_summary.last_claim_utc_day = v_day - 1 THEN v_summary.current_streak + 1
        ELSE 1
    END;

    v_key := 'daily-login:' || v_day::text;
    v_hex := md5(p_profile_id::text || ':daily-login:' || v_day::text);
    v_operation_id := (
        substr(v_hex, 1, 8) || '-' ||
        substr(v_hex, 9, 4) || '-' ||
        substr(v_hex, 13, 4) || '-' ||
        substr(v_hex, 17, 4) || '-' ||
        substr(v_hex, 21, 12)
    )::uuid;

    -- The primary key (player_profile_id, utc_day) is the double-claim lock.
    -- From here on, failures raise.
    INSERT INTO public.profile_daily_login_claims(
        player_profile_id, utc_day, streak_after, ram_amount, item_id, item_quantity, operation_id
    ) VALUES (
        p_profile_id, v_day, v_streak, v_settings.ram_amount, v_settings.item_id,
        v_settings.item_quantity, v_operation_id
    );

    v_delta := public.profile_apply_profile_item_delta_tx(
        p_profile_id,
        v_key,
        jsonb_build_array(jsonb_build_object('item_id', v_settings.item_id, 'quantity', v_settings.item_quantity)),
        '[]'::jsonb
    );
    IF COALESCE((v_delta->>'ok')::boolean, false) IS NOT TRUE THEN
        RAISE EXCEPTION 'daily_login_item_grant_failed: %', COALESCE(v_delta->>'error', 'unknown');
    END IF;
    IF COALESCE((v_delta->>'already_applied')::boolean, false) IS TRUE THEN
        RAISE EXCEPTION 'daily_login_item_mutation_conflict';
    END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, state, metadata_json, settled_at
    ) VALUES (
        v_operation_id, p_profile_id, NULL, v_cluster,
        'daily_login', 'credit', v_settings.ram_amount, 'settled',
        jsonb_build_object(
            'utc_day', v_day,
            'idempotency_key', v_key,
            'reward', 'daily_login',
            'streak_after', v_streak
        ),
        now()
    );

    UPDATE public.player_profiles
    SET ram_balance = ram_balance + v_settings.ram_amount
    WHERE player_profile_id = p_profile_id
      AND ram_balance + v_settings.ram_amount <= 4294967295
    RETURNING ram_balance, ram_balance_version
    INTO v_balance, v_version;
    IF NOT FOUND THEN RAISE EXCEPTION 'ram_balance_overflow'; END IF;

    INSERT INTO public.profile_daily_login(
        player_profile_id, last_claim_utc_day, current_streak, longest_streak, total_claims
    ) VALUES (
        p_profile_id, v_day, v_streak, v_streak, 1
    )
    ON CONFLICT (player_profile_id) DO UPDATE SET
        last_claim_utc_day = EXCLUDED.last_claim_utc_day,
        current_streak = EXCLUDED.current_streak,
        longest_streak = GREATEST(public.profile_daily_login.longest_streak, EXCLUDED.current_streak),
        total_claims = public.profile_daily_login.total_claims + 1,
        updated_at = now();

    RETURN jsonb_build_object(
        'ok', true,
        'already_claimed', false,
        'utc_day', v_day,
        'next_claim_at', v_next_claim_at,
        'server_now', now(),
        'ram_balance', v_balance,
        'ram_balance_version', v_version,
        'ram_amount', v_settings.ram_amount,
        'item_id', v_settings.item_id,
        'item_quantity', v_settings.item_quantity,
        'streak_after', v_streak
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.profile_get_daily_login_status_tx(p_profile_id UUID)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
STABLE
AS $$
DECLARE
    v_day DATE := (now() AT TIME ZONE 'UTC')::DATE;
    v_settings public.daily_login_settings%ROWTYPE;
    v_has_settings BOOLEAN;
    v_enabled BOOLEAN;
    v_unlocked BOOLEAN;
    v_claimed BOOLEAN;
    v_streak INTEGER;
BEGIN
    IF p_profile_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_id_required');
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('ok', false, 'error', 'profile_not_found');
    END IF;

    SELECT * INTO v_settings FROM public.daily_login_settings WHERE singleton;
    v_has_settings := FOUND;
    v_enabled := v_has_settings AND v_settings.enabled IS TRUE;
    v_unlocked := v_has_settings AND public.profile_daily_login_unlocked(
        p_profile_id, v_settings.quest_id, v_settings.unlock_flag_key
    );

    v_claimed := EXISTS (
        SELECT 1
        FROM public.profile_daily_login_claims
        WHERE player_profile_id = p_profile_id AND utc_day = v_day
    );

    SELECT current_streak INTO v_streak
    FROM public.profile_daily_login
    WHERE player_profile_id = p_profile_id;

    RETURN jsonb_build_object(
        'ok', true,
        'enabled', v_enabled,
        'unlocked', v_unlocked,
        'canClaim', v_enabled AND v_unlocked AND NOT v_claimed,
        'utcDay', v_day,
        'serverNow', now(),
        'nextClaimAt', CASE
            WHEN v_claimed THEN (v_day + 1)::timestamp AT TIME ZONE 'UTC'
            ELSE NULL
        END,
        'currentStreak', COALESCE(v_streak, 0),
        'ramAmount', v_settings.ram_amount,
        'itemId', v_settings.item_id,
        'itemQuantity', v_settings.item_quantity
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_daily_login_unlocked(UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_claim_daily_login_tx(UUID) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.profile_get_daily_login_status_tx(UUID) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_claim_daily_login_tx(UUID) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_get_daily_login_status_tx(UUID) TO service_role;
