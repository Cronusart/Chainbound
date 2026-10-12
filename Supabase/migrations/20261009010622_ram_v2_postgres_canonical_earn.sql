-- RAM v2: Postgres is canonical.  Earnings settle in one transaction and do
-- not require a wallet, Solana profile, signer, receipt PDA, or reconciler.

ALTER TABLE public.ram_ledger_operations
    ALTER COLUMN wallet_address DROP NOT NULL;

CREATE TABLE public.ram_daily_earnings (
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    utc_day DATE NOT NULL,
    earned BIGINT NOT NULL DEFAULT 0 CHECK (earned BETWEEN 0 AND 100),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, utc_day)
);

CREATE TABLE public.ram_v2_migration_audit (
    snapshot_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    profile_balance BIGINT NOT NULL,
    settled_credit_total BIGINT NOT NULL,
    settled_debit_total BIGINT NOT NULL,
    opening_delta BIGINT NOT NULL,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (player_profile_id)
);

-- Cache the marker for the data-migration statements below. A temp table is
-- deliberately used instead of a transaction-local custom setting: migration
-- runners are allowed to autocommit each statement, which would clear a
-- set_config(..., true) value before the following INSERT.
DROP TABLE IF EXISTS pg_temp.ram_v2_devnet_guard;
CREATE TEMP TABLE ram_v2_devnet_guard(is_devnet BOOLEAN NOT NULL);
DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    INSERT INTO pg_temp.ram_v2_devnet_guard(is_devnet) VALUES (v_is_devnet);
    IF NOT v_is_devnet THEN
        RAISE NOTICE 'RAM v2 opening balances skipped outside Devnet';
    END IF;
END
$devnet_guard$;

INSERT INTO public.ram_v2_migration_audit(
    player_profile_id, profile_balance, settled_credit_total,
    settled_debit_total, opening_delta
)
SELECT
    p.player_profile_id,
    p.ram_balance,
    COALESCE(SUM(o.amount) FILTER (
        WHERE o.state = 'settled' AND o.direction = 'credit'
    ), 0),
    COALESCE(SUM(o.amount) FILTER (
        WHERE o.state = 'settled' AND o.direction = 'debit'
    ), 0),
    p.ram_balance
      - COALESCE(SUM(o.amount) FILTER (
            WHERE o.state = 'settled' AND o.direction = 'credit'
        ), 0)
      + COALESCE(SUM(o.amount) FILTER (
            WHERE o.state = 'settled' AND o.direction = 'debit'
        ), 0)
FROM public.player_profiles AS p
LEFT JOIN public.ram_ledger_operations AS o
  ON o.player_profile_id = p.player_profile_id
WHERE (SELECT is_devnet FROM pg_temp.ram_v2_devnet_guard)
GROUP BY p.player_profile_id, p.ram_balance;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.ram_v2_migration_audit WHERE opening_delta < 0) THEN
        RAISE EXCEPTION 'ram_v2_negative_opening_delta_requires_operator_review';
    END IF;
END;
$$;

INSERT INTO public.ram_ledger_operations(
    operation_id, player_profile_id, wallet_address, cluster,
    operation_kind, direction, amount, state, metadata_json,
    created_at, updated_at, settled_at
)
SELECT
    (
        substr(md5('ram-v2-opening:' || a.player_profile_id::TEXT), 1, 8) || '-' ||
        substr(md5('ram-v2-opening:' || a.player_profile_id::TEXT), 9, 4) || '-' ||
        '5' || substr(md5('ram-v2-opening:' || a.player_profile_id::TEXT), 14, 3) || '-' ||
        '8' || substr(md5('ram-v2-opening:' || a.player_profile_id::TEXT), 18, 3) || '-' ||
        substr(md5('ram-v2-opening:' || a.player_profile_id::TEXT), 21, 12)
    )::UUID,
    a.player_profile_id,
    w.wallet_public_key,
    (SELECT cluster FROM public.battle_reward_settings WHERE singleton),
    'opening_balance',
    'credit',
    a.opening_delta,
    'settled',
    jsonb_build_object(
        'source', 'ram_v2_migration',
        'snapshot_id', a.snapshot_id,
        'profile_balance', a.profile_balance
    ),
    a.captured_at,
    a.captured_at,
    a.captured_at
FROM public.ram_v2_migration_audit AS a
LEFT JOIN LATERAL (
    SELECT wallet_public_key
      FROM public.profile_wallets
     WHERE player_profile_id = a.player_profile_id
       AND is_active AND unlinked_at IS NULL
     ORDER BY linked_at DESC
     LIMIT 1
) AS w ON true
WHERE a.opening_delta > 0;

DROP TABLE pg_temp.ram_v2_devnet_guard;

ALTER TABLE public.ram_daily_earnings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.ram_v2_migration_audit ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.ram_daily_earnings FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.ram_v2_migration_audit FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE ON TABLE public.ram_daily_earnings TO service_role;
GRANT SELECT ON TABLE public.ram_v2_migration_audit TO service_role;

CREATE OR REPLACE FUNCTION public.ram_battle_reward_settle_service(p_battle_id UUID)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_existing public.battle_reward_settlements%ROWTYPE;
    v_config public.battle_reward_configs%ROWTYPE;
    v_daily public.ram_daily_earnings%ROWTYPE;
    v_operation public.ram_ledger_operations%ROWTYPE;
    v_profile_balance BIGINT;
    v_requested BIGINT;
    v_applied BIGINT;
    v_day DATE := (now() AT TIME ZONE 'UTC')::DATE;
    v_daily_cap CONSTANT BIGINT := 100;
    v_cluster TEXT;
BEGIN
    SELECT cluster INTO v_cluster FROM public.battle_reward_settings WHERE singleton;
    SELECT * INTO v_battle
      FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;

    -- Global order: profile -> battle -> settlement -> daily -> operation.
    SELECT ram_balance INTO v_profile_balance
      FROM public.player_profiles
     WHERE player_profile_id = v_battle.player_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    SELECT * INTO v_battle
      FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id
     FOR UPDATE;
    SELECT * INTO v_existing
      FROM public.battle_reward_settlements
     WHERE battle_id = p_battle_id
     FOR UPDATE;
    IF FOUND THEN
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', true);
    END IF;

    IF v_battle.status NOT IN ('completed', 'lost', 'abandoned')
       OR v_battle.terminal_outcome IS NULL THEN
        RAISE EXCEPTION 'battle_not_terminal';
    END IF;
    SELECT * INTO v_config
      FROM public.battle_reward_configs
     WHERE config_version = v_battle.reward_config_version
       AND config_hash = v_battle.reward_config_hash
       AND enabled;
    IF NOT FOUND THEN RAISE EXCEPTION 'reward_config_not_allowed'; END IF;
    v_requested := CASE v_battle.reward_tier
        WHEN 'standard' THEN v_config.standard_amount
        WHEN 'elite' THEN v_config.elite_amount
        WHEN 'boss' THEN v_config.boss_amount
        ELSE NULL
    END;
    IF v_requested IS NULL THEN RAISE EXCEPTION 'reward_tier_not_allowed'; END IF;

    IF v_battle.terminal_outcome <> 'victory' THEN
        INSERT INTO public.battle_reward_settlements(
            battle_id, player_profile_id, terminal_outcome, reward_tier,
            reward_config_version, reward_config_hash, requested_amount,
            applied_amount, status, reason, balance_after, settled_at
        ) VALUES (
            p_battle_id, v_battle.player_profile_id, v_battle.terminal_outcome,
            v_battle.reward_tier, v_battle.reward_config_version, v_battle.reward_config_hash,
            v_requested, 0, 'zero', v_battle.terminal_outcome, v_profile_balance, now()
        );
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', false);
    END IF;

    INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
    VALUES (v_battle.player_profile_id, v_day, 0)
    ON CONFLICT (player_profile_id, utc_day) DO NOTHING;
    SELECT * INTO v_daily
      FROM public.ram_daily_earnings
     WHERE player_profile_id = v_battle.player_profile_id
       AND utc_day = v_day
     FOR UPDATE;

    SELECT * INTO v_operation
      FROM public.ram_ledger_operations
     WHERE cluster = v_cluster AND operation_id = p_battle_id
     FOR UPDATE;
    IF FOUND THEN RAISE EXCEPTION 'battle_reward_operation_conflict'; END IF;

    v_applied := LEAST(v_requested, GREATEST(0, v_daily_cap - v_daily.earned));
    IF v_applied = 0 THEN
        INSERT INTO public.battle_reward_settlements(
            battle_id, player_profile_id, terminal_outcome, reward_tier,
            reward_config_version, reward_config_hash, requested_amount,
            applied_amount, status, reason, utc_day, daily_earned,
            daily_cap, balance_after, settled_at
        ) VALUES (
            p_battle_id, v_battle.player_profile_id, v_battle.terminal_outcome,
            v_battle.reward_tier, v_battle.reward_config_version, v_battle.reward_config_hash,
            v_requested, 0, 'zero', 'daily_cap', v_day, v_daily.earned,
            v_daily_cap, v_profile_balance, now()
        );
        RETURN public.battle_reward_get_service(p_battle_id)
            || jsonb_build_object('alreadyApplied', false);
    END IF;
    IF v_profile_balance + v_applied > 4294967295 THEN
        RAISE EXCEPTION 'ram_balance_overflow';
    END IF;

    INSERT INTO public.ram_ledger_operations(
        operation_id, player_profile_id, wallet_address, cluster,
        operation_kind, direction, amount, state, metadata_json, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, NULL, v_cluster,
        'battle_reward', 'credit', v_applied, 'settled',
        jsonb_build_object(
            'battle_id', p_battle_id,
            'terminal_outcome', v_battle.terminal_outcome,
            'reward_tier', v_battle.reward_tier,
            'reward_config_version', v_battle.reward_config_version,
            'reward_config_hash', v_battle.reward_config_hash,
            'utc_day', v_day
        ),
        now()
    ) RETURNING * INTO v_operation;

    UPDATE public.ram_daily_earnings
       SET earned = earned + v_applied, updated_at = now()
     WHERE player_profile_id = v_battle.player_profile_id AND utc_day = v_day
     RETURNING * INTO v_daily;
    UPDATE public.player_profiles
       SET ram_balance = ram_balance + v_applied
     WHERE player_profile_id = v_battle.player_profile_id
     RETURNING ram_balance INTO v_profile_balance;

    INSERT INTO public.battle_reward_settlements(
        battle_id, player_profile_id, terminal_outcome, reward_tier,
        reward_config_version, reward_config_hash, requested_amount,
        applied_amount, status, reason, ram_ledger_operation_id,
        utc_day, daily_earned, daily_cap, balance_after, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, v_battle.terminal_outcome,
        v_battle.reward_tier, v_battle.reward_config_version, v_battle.reward_config_hash,
        v_requested, v_applied, 'settled', 'granted', v_operation.id,
        v_day, v_daily.earned, v_daily_cap, v_profile_balance, now()
    );

    RETURN public.battle_reward_get_service(p_battle_id)
        || jsonb_build_object('alreadyApplied', false);
END;
$$;

CREATE OR REPLACE FUNCTION public.ram_v2_audit_service(p_profile_id UUID DEFAULT NULL)
RETURNS TABLE(
    player_profile_id UUID,
    profile_balance BIGINT,
    ledger_balance BIGINT,
    balanced BOOLEAN
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = ''
STABLE
AS $$
    SELECT
        p.player_profile_id,
        p.ram_balance,
        COALESCE(SUM(CASE
            WHEN o.state = 'settled' AND o.direction = 'credit' THEN o.amount
            WHEN o.state = 'settled' AND o.direction = 'debit' THEN -o.amount
            ELSE 0
        END), 0)::BIGINT AS ledger_balance,
        p.ram_balance = COALESCE(SUM(CASE
            WHEN o.state = 'settled' AND o.direction = 'credit' THEN o.amount
            WHEN o.state = 'settled' AND o.direction = 'debit' THEN -o.amount
            ELSE 0
        END), 0) AS balanced
      FROM public.player_profiles AS p
      LEFT JOIN public.ram_ledger_operations AS o
        ON o.player_profile_id = p.player_profile_id
     WHERE p_profile_id IS NULL OR p.player_profile_id = p_profile_id
     GROUP BY p.player_profile_id, p.ram_balance;
$$;

REVOKE ALL ON FUNCTION public.ram_battle_reward_settle_service(UUID) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.ram_v2_audit_service(UUID) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ram_battle_reward_settle_service(UUID) TO service_role;
GRANT EXECUTE ON FUNCTION public.ram_v2_audit_service(UUID) TO service_role;

COMMENT ON FUNCTION public.ram_battle_reward_settle_service(UUID) IS
    'Idempotently classifies and immediately settles one terminal PvE RAM reward in Postgres using the database UTC clock and a 100/day earn cap.';
