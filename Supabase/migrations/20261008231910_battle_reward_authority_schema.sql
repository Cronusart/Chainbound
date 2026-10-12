-- Immutable battle authority stamps and the domain receipt for immediate RAM
-- rewards.  This is deliberately separate from ram_ledger_operations because
-- zero-value outcomes are valid receipts but not currency movements.

ALTER TABLE public.authoritative_pve_battles
    ADD COLUMN IF NOT EXISTS encounter_id TEXT NULL,
    ADD COLUMN IF NOT EXISTS reward_tier TEXT NULL,
    ADD COLUMN IF NOT EXISTS reward_config_version INTEGER NULL,
    ADD COLUMN IF NOT EXISTS reward_config_hash TEXT NULL,
    ADD COLUMN IF NOT EXISTS terminal_outcome TEXT NULL,
    ADD COLUMN IF NOT EXISTS terminal_reason TEXT NULL,
    ADD COLUMN IF NOT EXISTS terminal_at TIMESTAMPTZ NULL;

ALTER TABLE public.authoritative_pve_battles
    DROP CONSTRAINT IF EXISTS authoritative_pve_battles_reward_tier_check,
    ADD CONSTRAINT authoritative_pve_battles_reward_tier_check
        CHECK (reward_tier IS NULL OR reward_tier IN ('standard', 'elite', 'boss')),
    DROP CONSTRAINT IF EXISTS authoritative_pve_battles_reward_config_version_check,
    ADD CONSTRAINT authoritative_pve_battles_reward_config_version_check
        CHECK (reward_config_version IS NULL OR reward_config_version > 0),
    DROP CONSTRAINT IF EXISTS authoritative_pve_battles_reward_config_hash_check,
    ADD CONSTRAINT authoritative_pve_battles_reward_config_hash_check
        CHECK (reward_config_hash IS NULL OR reward_config_hash ~ '^[0-9a-f]{64}$'),
    DROP CONSTRAINT IF EXISTS authoritative_pve_battles_terminal_outcome_check,
    ADD CONSTRAINT authoritative_pve_battles_terminal_outcome_check
        CHECK (terminal_outcome IS NULL OR terminal_outcome IN
            ('victory', 'capture', 'defeat', 'draw', 'flee', 'forfeit', 'abandon')),
    DROP CONSTRAINT IF EXISTS authoritative_pve_battles_terminal_consistency_check,
    ADD CONSTRAINT authoritative_pve_battles_terminal_consistency_check
        CHECK ((terminal_outcome IS NULL) = (terminal_at IS NULL));

-- The database allowlist makes an exported reward hash executable without
-- accepting amounts from BattleService or Edge.  A future config is enabled
-- by a migration after its manifest hash has been reviewed.
CREATE TABLE public.battle_reward_configs (
    config_version INTEGER PRIMARY KEY CHECK (config_version > 0),
    config_hash TEXT NOT NULL UNIQUE CHECK (config_hash ~ '^[0-9a-f]{64}$'),
    standard_amount BIGINT NOT NULL CHECK (standard_amount BETWEEN 1 AND 4294967295),
    elite_amount BIGINT NOT NULL CHECK (elite_amount BETWEEN 1 AND 4294967295),
    boss_amount BIGINT NOT NULL CHECK (boss_amount BETWEEN 1 AND 4294967295),
    enabled BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.battle_reward_configs(
    config_version, config_hash, standard_amount, elite_amount, boss_amount, enabled
) VALUES (
    3, '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
    5, 15, 25, false
) ON CONFLICT (config_version) DO NOTHING;

-- Enabling executable economy configuration is Devnet-only. Applying this
-- migration anywhere else creates inert schema, never a live reward faucet.
DO $devnet_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        UPDATE public.battle_reward_configs
           SET enabled = true
         WHERE config_version = 3
           AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';
    ELSE
        RAISE NOTICE 'RAM v2 reward config remains disabled outside Devnet';
    END IF;
END
$devnet_guard$;

CREATE TABLE public.battle_reward_settlements (
    settlement_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    battle_id UUID NOT NULL UNIQUE
        REFERENCES public.authoritative_pve_battles(battle_id) ON DELETE RESTRICT,
    player_profile_id UUID NOT NULL
        REFERENCES public.player_profiles(player_profile_id) ON DELETE RESTRICT,
    terminal_outcome TEXT NOT NULL CHECK (terminal_outcome IN
        ('victory', 'capture', 'defeat', 'draw', 'flee', 'forfeit', 'abandon')),
    reward_tier TEXT NOT NULL CHECK (reward_tier IN ('standard', 'elite', 'boss')),
    reward_config_version INTEGER NOT NULL CHECK (reward_config_version > 0),
    reward_config_hash TEXT NOT NULL CHECK (reward_config_hash ~ '^[0-9a-f]{64}$'),
    requested_amount BIGINT NOT NULL CHECK (requested_amount BETWEEN 0 AND 4294967295),
    applied_amount BIGINT NOT NULL DEFAULT 0
        CHECK (applied_amount BETWEEN 0 AND requested_amount),
    status TEXT NOT NULL CHECK (status IN ('zero', 'settled', 'failed')),
    reason TEXT NOT NULL CHECK (length(btrim(reason)) > 0),
    ram_ledger_operation_id BIGINT NULL UNIQUE
        REFERENCES public.ram_ledger_operations(id) ON DELETE RESTRICT,
    utc_day DATE NULL,
    daily_earned BIGINT NULL CHECK (daily_earned IS NULL OR daily_earned BETWEEN 0 AND 100),
    daily_cap BIGINT NOT NULL DEFAULT 100 CHECK (daily_cap = 100),
    balance_after BIGINT NULL CHECK (balance_after IS NULL OR balance_after BETWEEN 0 AND 4294967295),
    failure_code TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    settled_at TIMESTAMPTZ NULL,
    CONSTRAINT battle_reward_zero_consistency CHECK (
        status <> 'zero' OR (
            applied_amount = 0 AND ram_ledger_operation_id IS NULL
        )
    ),
    CONSTRAINT battle_reward_settled_consistency CHECK (
        status <> 'settled' OR (
            applied_amount > 0 AND ram_ledger_operation_id IS NOT NULL
            AND utc_day IS NOT NULL AND daily_earned IS NOT NULL
            AND balance_after IS NOT NULL AND settled_at IS NOT NULL
        )
    )
);

CREATE INDEX battle_reward_profile_history_idx
    ON public.battle_reward_settlements(player_profile_id, created_at DESC);

ALTER TABLE public.battle_reward_settlements ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.battle_reward_configs ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.battle_reward_configs FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.battle_reward_settlements FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.battle_reward_configs TO service_role;
GRANT SELECT, INSERT, UPDATE ON TABLE public.battle_reward_settlements TO service_role;

COMMENT ON TABLE public.battle_reward_settlements IS
    'Service-only immutable receipts for terminal PvE RAM reward classification and Postgres settlement.';

CREATE TABLE public.battle_reward_settings (
    singleton BOOLEAN PRIMARY KEY DEFAULT true CHECK (singleton),
    mode TEXT NOT NULL DEFAULT 'off' CHECK (mode IN ('off', 'shadow', 'grant')),
    cluster TEXT NOT NULL DEFAULT 'disabled'
        CHECK (cluster IN ('disabled', 'devnet', 'mainnet-beta')),
    profile_allowlist UUID[] NOT NULL DEFAULT '{}'::UUID[],
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
INSERT INTO public.battle_reward_settings(singleton, mode, cluster, profile_allowlist)
VALUES (true, 'off', 'disabled', '{}'::UUID[])
ON CONFLICT (singleton) DO NOTHING;
DO $devnet_settings_guard$
DECLARE
    v_is_devnet BOOLEAN := COALESCE(current_setting('chainbound.cluster', true), '') = 'devnet';
BEGIN
    IF NOT v_is_devnet AND to_regclass('public.hub_settings') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.hub_settings WHERE cluster = ''devnet'')'
            INTO v_is_devnet;
    END IF;
    IF v_is_devnet THEN
        UPDATE public.battle_reward_settings SET cluster = 'devnet', mode = 'off', updated_at = now()
         WHERE singleton;
    END IF;
END
$devnet_settings_guard$;
ALTER TABLE public.battle_reward_settings ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.battle_reward_settings FROM PUBLIC, anon, authenticated;
GRANT SELECT ON TABLE public.battle_reward_settings TO service_role;
