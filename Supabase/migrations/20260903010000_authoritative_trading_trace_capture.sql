-- Chainbound authoritative trading-trace capture foundation.
-- Service-role functions are the only mutation path. Clients retain read-only UI fallbacks.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- -----------------------------------------------------------------------------
-- Authoritative item metadata
-- -----------------------------------------------------------------------------

INSERT INTO public.chainbound_item_catalog (
    item_id, display_name, description, category, effect_family, ownership_kind,
    max_stack, usable_in_menu, usable_in_battle, bindery_only, metadata_json, schema_version, enabled
) VALUES
    ('signal_orb', 'Signal Orb', 'Neutral capture band and lock profile.', 'capture', 'capture_tool', 'profile_stack', 99, false, true, false,
        '{"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0},"entangles_on_success":false}'::jsonb, 2, true),
    ('lattice_orb', 'Lattice Orb', 'Widens the capture band by 25%, but reduces Lock Rate by 15%.', 'capture', 'capture_tool', 'profile_stack', 99, false, true, false,
        '{"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":2500,"lock_rate_bp":-1500,"capture_time_bp":0},"entangles_on_success":false}'::jsonb, 2, true),
    ('quick_orb', 'Quick Orb', 'Raises Lock Rate by 30%, but narrows the capture band by 20%.', 'capture', 'capture_tool', 'profile_stack', 99, false, true, false,
        '{"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":-2000,"lock_rate_bp":3000,"capture_time_bp":0},"entangles_on_success":false}'::jsonb, 2, true),
    ('entangle_orb', 'Entangle Orb', 'Narrows the band and timer. A successful Lock assigns an entangled type.', 'capture', 'capture_tool', 'profile_stack', 99, false, true, false,
        '{"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":-2000,"lock_rate_bp":0,"capture_time_bp":-2500},"entangles_on_success":true,"normal_vending":false,"static_pickup":false}'::jsonb, 2, true),
    ('signal_scanner_i', 'Signal Scanner I', 'Reveals the low-HP marker and one non-HP IV grade.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"head","capture_effect":{"scan":1,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('signal_scanner_iii', 'Signal Scanner III', 'Reveals the low-HP marker and three non-HP IV grades.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"head","capture_effect":{"scan":3,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('signal_scanner_v', 'Signal Scanner V', 'Reveals the low-HP marker and all five non-HP IV grades.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"head","capture_effect":{"scan":5,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('stability_visor', 'Stability Visor', 'Adds 15% Stability to reduce trace motion.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"head","capture_effect":{"scan":0,"stability_bp":1500,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('wideband_rig', 'Wideband Rig', 'Widens the capture band by 15%.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"body","capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":1500,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('lockdrive_rig', 'Lockdrive Rig', 'Raises Lock Rate by 15%.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"body","capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":1500,"capture_time_bp":0}}'::jsonb, 2, true),
    ('reserve_clock_rig', 'Reserve Clock Rig', 'Adds 15% capture time.', 'utility', 'utility_gear', 'profile_stack', 1, true, false, false,
        '{"utility_slot":"body","capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":1500}}'::jsonb, 2, true),
    ('band_expander_s', 'Band Expander S', 'Widens the capture band by 10% for 10 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":600,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":1000,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('band_expander_m', 'Band Expander M', 'Widens the capture band by 20% for 20 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":1200,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":2000,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('band_expander_l', 'Band Expander L', 'Widens the capture band by 30% for 30 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":1800,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":3000,"lock_rate_bp":0,"capture_time_bp":0}}'::jsonb, 2, true),
    ('lock_accelerant_s', 'Lock Accelerant S', 'Raises Lock Rate by 10% for 10 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":600,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":1000,"capture_time_bp":0}}'::jsonb, 2, true),
    ('lock_accelerant_m', 'Lock Accelerant M', 'Raises Lock Rate by 20% for 20 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":1200,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":2000,"capture_time_bp":0}}'::jsonb, 2, true),
    ('lock_accelerant_l', 'Lock Accelerant L', 'Raises Lock Rate by 30% for 30 real-time minutes.', 'utility', 'capture_boost', 'profile_stack', 99, true, false, false,
        '{"duration_seconds":1800,"capture_effect":{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":3000,"capture_time_bp":0}}'::jsonb, 2, true)
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
    metadata_json = EXCLUDED.metadata_json,
    schema_version = EXCLUDED.schema_version,
    enabled = EXCLUDED.enabled,
    updated_at = now();

-- -----------------------------------------------------------------------------
-- Profiles, target assignments, and live events
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.capture_balance_profiles (
    profile_id TEXT PRIMARY KEY,
    config_version INTEGER NOT NULL DEFAULT 1 CHECK (config_version > 0),
    difficulty TEXT NOT NULL CHECK (difficulty IN ('calm', 'standard', 'volatile', 'extreme')),
    motion_multiplier_bp INTEGER NOT NULL CHECK (motion_multiplier_bp >= 0),
    timer_multiplier_bp INTEGER NOT NULL CHECK (timer_multiplier_bp > 0),
    spike_chance_multiplier_bp INTEGER NOT NULL CHECK (spike_chance_multiplier_bp >= 0),
    typed_delta JSONB NOT NULL DEFAULT '{}'::jsonb,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.capture_balance_profiles (
    profile_id, config_version, difficulty, motion_multiplier_bp, timer_multiplier_bp,
    spike_chance_multiplier_bp, typed_delta, enabled
) VALUES
    ('calm', 1, 'calm', 7500, 11500, 7500, '{}'::jsonb, true),
    ('standard', 1, 'standard', 10000, 10000, 10000, '{}'::jsonb, true),
    ('volatile', 1, 'volatile', 13000, 8750, 13000, '{}'::jsonb, true),
    ('extreme', 1, 'extreme', 16000, 6500, 16000, '{}'::jsonb, true)
ON CONFLICT (profile_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS public.capture_profile_assignments (
    assignment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id TEXT NOT NULL REFERENCES public.capture_balance_profiles(profile_id),
    config_version INTEGER NOT NULL DEFAULT 1 CHECK (config_version > 0),
    species_id TEXT NULL,
    visual_variant_id TEXT NULL,
    mutation_id TEXT NULL,
    entangled_flag BOOLEAN NULL,
    entangled_type TEXT NULL,
    area_id TEXT NULL,
    zone_id TEXT NULL,
    difficulty_id TEXT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_capture_profile_assignments_lookup
    ON public.capture_profile_assignments (enabled, species_id, zone_id, priority DESC);
CREATE UNIQUE INDEX IF NOT EXISTS ux_capture_profile_assignments_exact
    ON public.capture_profile_assignments (
        COALESCE(species_id, ''), COALESCE(visual_variant_id, ''), COALESCE(mutation_id, ''),
        COALESCE(entangled_flag, false), (entangled_flag IS NULL), COALESCE(entangled_type, ''),
        COALESCE(area_id, ''), COALESCE(zone_id, ''), COALESCE(difficulty_id, ''), priority
    ) WHERE enabled = true;

CREATE TABLE IF NOT EXISTS public.capture_live_events (
    event_id TEXT PRIMARY KEY,
    config_version INTEGER NOT NULL DEFAULT 1 CHECK (config_version > 0),
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL CHECK (ends_at > starts_at),
    priority INTEGER NOT NULL DEFAULT 0,
    replacement_profile_id TEXT NULL REFERENCES public.capture_balance_profiles(profile_id),
    filters JSONB NOT NULL DEFAULT '{}'::jsonb,
    typed_delta JSONB NOT NULL DEFAULT '{}'::jsonb,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_capture_live_events_active
    ON public.capture_live_events (enabled, starts_at, ends_at, priority);

-- -----------------------------------------------------------------------------
-- Profile equipment, real-time boosts, sessions, commands, and ledger
-- -----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.profile_capture_equipment (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    slot TEXT NOT NULL CHECK (slot IN ('head', 'body')),
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    equipped_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, slot)
);

CREATE TABLE IF NOT EXISTS public.profile_capture_boosts (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    item_id TEXT NOT NULL REFERENCES public.chainbound_item_catalog(item_id),
    activated_at TIMESTAMPTZ NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, item_id),
    CHECK (expires_at > activated_at)
);
CREATE INDEX IF NOT EXISTS idx_profile_capture_boosts_expiry
    ON public.profile_capture_boosts (player_profile_id, expires_at);

CREATE TABLE IF NOT EXISTS public.authoritative_pve_battles (
    battle_id UUID PRIMARY KEY,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    start_command_id TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('active', 'capturing', 'completed', 'lost', 'abandoned', 'service_interrupted')),
    battle_kind TEXT NOT NULL CHECK (battle_kind IN ('wild', 'boss', 'trainer', 'scripted')),
    state_version INTEGER NOT NULL DEFAULT 0 CHECK (state_version >= 0),
    seed BIGINT NOT NULL,
    manifest_hash TEXT NOT NULL,
    config_hash TEXT NOT NULL,
    state_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    equipment_snapshot JSONB NOT NULL DEFAULT '{}'::jsonb,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    checkpointed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
ALTER TABLE public.authoritative_pve_battles
    ADD COLUMN IF NOT EXISTS start_command_id TEXT;
UPDATE public.authoritative_pve_battles
SET start_command_id = 'legacy:' || battle_id::TEXT
WHERE start_command_id IS NULL OR length(trim(start_command_id)) = 0;
ALTER TABLE public.authoritative_pve_battles
    ALTER COLUMN start_command_id SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_authoritative_pve_battle_start_command
    ON public.authoritative_pve_battles (player_profile_id, start_command_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_authoritative_pve_one_active_profile
    ON public.authoritative_pve_battles (player_profile_id)
    WHERE status IN ('active', 'capturing', 'service_interrupted');
CREATE INDEX IF NOT EXISTS idx_authoritative_pve_battle_expiry
    ON public.authoritative_pve_battles (status, expires_at);

CREATE TABLE IF NOT EXISTS public.authoritative_pve_commands (
    battle_id UUID NOT NULL REFERENCES public.authoritative_pve_battles(battle_id) ON DELETE CASCADE,
    command_id TEXT NOT NULL,
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    expected_version INTEGER NOT NULL,
    result_version INTEGER NOT NULL,
    command_kind TEXT NOT NULL,
    request_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    result_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (battle_id, command_id)
);

ALTER TABLE public.profile_capture_events
    ADD COLUMN IF NOT EXISTS attempt_id UUID,
    ADD COLUMN IF NOT EXISTS battle_id UUID REFERENCES public.authoritative_pve_battles(battle_id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS command_id TEXT,
    ADD COLUMN IF NOT EXISTS orb_id TEXT,
    ADD COLUMN IF NOT EXISTS capture_profile_id TEXT,
    ADD COLUMN IF NOT EXISTS config_hash TEXT,
    ADD COLUMN IF NOT EXISTS target_id TEXT,
    ADD COLUMN IF NOT EXISTS species_id TEXT,
    ADD COLUMN IF NOT EXISTS target_snapshot JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS hp_band TEXT,
    ADD COLUMN IF NOT EXISTS low_hp_threshold_bp INTEGER,
    ADD COLUMN IF NOT EXISTS elapsed_milliseconds INTEGER,
    ADD COLUMN IF NOT EXISTS lock_escape_history JSONB NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS outcome TEXT,
    ADD COLUMN IF NOT EXISTS alertness INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS grant_chainling_id UUID,
    ADD COLUMN IF NOT EXISTS grant_move_item_id UUID,
    ADD COLUMN IF NOT EXISTS accepted_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS finalized_at TIMESTAMPTZ;

UPDATE public.profile_capture_events
SET outcome = CASE
        WHEN COALESCE((result_json ->> 'success')::BOOLEAN, false) THEN 'legacy_locked'
        ELSE 'legacy_failed'
    END,
    finalized_at = COALESCE(finalized_at, created_at)
WHERE outcome IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_capture_events_attempt
    ON public.profile_capture_events (attempt_id) WHERE attempt_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_capture_events_one_pending_battle
    ON public.profile_capture_events (battle_id) WHERE outcome = 'pending';

ALTER TABLE public.player_lifetime_stats
    ADD COLUMN IF NOT EXISTS capture_attempts INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS capture_orbs_used INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS capture_timeouts INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS capture_targets_escaped INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS capture_perfect_locks INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS captures_above_low_hp INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS captures_by_orb JSONB NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS fastest_capture_milliseconds INTEGER,
    ADD COLUMN IF NOT EXISTS extreme_profile_captures INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS entangle_orb_captures INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS public.capture_achievement_catalog (
    achievement_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    description TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true
);
INSERT INTO public.capture_achievement_catalog (achievement_id, display_name, description) VALUES
    ('first_lock', 'First Lock', 'Capture your first Chainling with the trading trace.'),
    ('clean_signal', 'Clean Signal', 'Complete a perfect Lock without leaving the band.'),
    ('against_the_noise', 'Against the Noise', 'Capture a target above the low-HP threshold.'),
    ('extreme_hunter', 'Extreme Hunter', 'Capture an Extreme-profile target.'),
    ('entangler', 'Entangler', 'Complete a successful Entangle Orb capture.')
ON CONFLICT (achievement_id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    enabled = true;

CREATE TABLE IF NOT EXISTS public.profile_capture_achievements (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    achievement_id TEXT NOT NULL REFERENCES public.capture_achievement_catalog(achievement_id),
    attempt_id UUID NULL,
    unlocked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, achievement_id)
);

ALTER TABLE public.capture_balance_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.capture_profile_assignments ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.capture_live_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_capture_equipment ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_capture_boosts ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.authoritative_pve_battles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.authoritative_pve_commands ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.capture_achievement_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_capture_achievements ENABLE ROW LEVEL SECURITY;

-- -----------------------------------------------------------------------------
-- Helpers and service-only RPCs
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.capture_effect_zero()
RETURNS JSONB LANGUAGE sql IMMUTABLE AS $$
    SELECT '{"scan":0,"stability_bp":0,"band_width_bp":0,"lock_rate_bp":0,"capture_time_bp":0}'::jsonb
$$;

CREATE OR REPLACE FUNCTION public.capture_effect_add(p_left JSONB, p_right JSONB)
RETURNS JSONB LANGUAGE sql IMMUTABLE AS $$
    SELECT jsonb_build_object(
        'scan', GREATEST(COALESCE((p_left->>'scan')::INTEGER, 0), COALESCE((p_right->>'scan')::INTEGER, 0)),
        'stability_bp', COALESCE((p_left->>'stability_bp')::INTEGER, 0) + COALESCE((p_right->>'stability_bp')::INTEGER, 0),
        'band_width_bp', COALESCE((p_left->>'band_width_bp')::INTEGER, 0) + COALESCE((p_right->>'band_width_bp')::INTEGER, 0),
        'lock_rate_bp', COALESCE((p_left->>'lock_rate_bp')::INTEGER, 0) + COALESCE((p_right->>'lock_rate_bp')::INTEGER, 0),
        'capture_time_bp', COALESCE((p_left->>'capture_time_bp')::INTEGER, 0) + COALESCE((p_right->>'capture_time_bp')::INTEGER, 0)
    )
$$;

CREATE OR REPLACE FUNCTION public.capture_get_loadout_service(
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_now TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_head TEXT;
    v_body TEXT;
    v_gear JSONB := public.capture_effect_zero();
    v_boost JSONB := public.capture_effect_zero();
    v_row RECORD;
    v_boosts JSONB := '[]'::jsonb;
BEGIN
    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN RAISE EXCEPTION 'profile_not_found'; END IF;

    FOR v_row IN
        SELECT e.slot, e.item_id, c.metadata_json->'capture_effect' AS effect
        FROM public.profile_capture_equipment e
        JOIN public.chainbound_item_catalog c ON c.item_id = e.item_id
        WHERE e.player_profile_id = p_profile_id AND c.enabled = true
    LOOP
        IF v_row.slot = 'head' THEN v_head := v_row.item_id; ELSE v_body := v_row.item_id; END IF;
        v_gear := public.capture_effect_add(v_gear, COALESCE(v_row.effect, '{}'::jsonb));
    END LOOP;

    DELETE FROM public.profile_capture_boosts
    WHERE player_profile_id = p_profile_id AND expires_at <= v_now;

    FOR v_row IN
        SELECT b.item_id, b.expires_at, c.metadata_json->'capture_effect' AS effect
        FROM public.profile_capture_boosts b
        JOIN public.chainbound_item_catalog c ON c.item_id = b.item_id
        WHERE b.player_profile_id = p_profile_id AND b.expires_at > v_now AND c.enabled = true
        ORDER BY b.item_id
    LOOP
        v_boost := public.capture_effect_add(v_boost, COALESCE(v_row.effect, '{}'::jsonb));
        v_boosts := v_boosts || jsonb_build_array(jsonb_build_object(
            'item_id', v_row.item_id,
            'expires_at_unix_milliseconds', FLOOR(EXTRACT(EPOCH FROM v_row.expires_at) * 1000)::BIGINT,
            'effects', COALESCE(v_row.effect, public.capture_effect_zero())
        ));
    END LOOP;

    RETURN jsonb_build_object(
        'head_item_id', COALESCE(v_head, ''),
        'body_item_id', COALESCE(v_body, ''),
        'gear_effects', v_gear,
        'boost_effects', v_boost,
        'active_boosts', v_boosts,
        'authority_unix_milliseconds', p_authority_unix_milliseconds,
        'time_authority_fresh', true
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.capture_equip_utility_gear_service(
    p_profile_id UUID,
    p_slot TEXT,
    p_item_id TEXT,
    p_authority_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_slot TEXT := lower(trim(COALESCE(p_slot, '')));
    v_item_id TEXT := trim(COALESCE(p_item_id, ''));
    v_catalog public.chainbound_item_catalog%ROWTYPE;
BEGIN
    IF v_slot NOT IN ('head', 'body') THEN RAISE EXCEPTION 'invalid_utility_slot'; END IF;
    IF EXISTS (
        SELECT 1 FROM public.authoritative_pve_battles
        WHERE player_profile_id = p_profile_id
          AND status IN ('active', 'capturing', 'service_interrupted')
    ) THEN RAISE EXCEPTION 'equipment_locked_during_battle'; END IF;

    SELECT * INTO v_catalog FROM public.chainbound_item_catalog
    WHERE item_id = v_item_id AND enabled = true FOR SHARE;
    IF NOT FOUND OR v_catalog.effect_family <> 'utility_gear' OR
       COALESCE(v_catalog.metadata_json->>'utility_slot', '') <> v_slot
    THEN RAISE EXCEPTION 'invalid_utility_gear'; END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.chainbound_profile_item_stacks
        WHERE player_profile_id = p_profile_id AND item_id = v_item_id AND quantity > 0
    ) THEN RAISE EXCEPTION 'utility_gear_not_owned'; END IF;

    INSERT INTO public.profile_capture_equipment (player_profile_id, slot, item_id)
    VALUES (p_profile_id, v_slot, v_item_id)
    ON CONFLICT (player_profile_id, slot) DO UPDATE
    SET item_id = EXCLUDED.item_id, equipped_at = now(), updated_at = now();

    RETURN public.capture_get_loadout_service(p_profile_id, p_authority_unix_milliseconds);
END;
$$;

CREATE OR REPLACE FUNCTION public.capture_activate_boost_service(
    p_profile_id UUID,
    p_item_id TEXT,
    p_authority_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_now TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_item_id TEXT := trim(COALESCE(p_item_id, ''));
    v_duration INTEGER;
BEGIN
    SELECT COALESCE((metadata_json->>'duration_seconds')::INTEGER, 0)
    INTO v_duration
    FROM public.chainbound_item_catalog
    WHERE item_id = v_item_id AND effect_family = 'capture_boost' AND enabled = true
    FOR SHARE;
    IF NOT FOUND OR v_duration <= 0 THEN RAISE EXCEPTION 'invalid_capture_boost'; END IF;

    UPDATE public.chainbound_profile_item_stacks
    SET quantity = quantity - 1, updated_at = now()
    WHERE player_profile_id = p_profile_id AND item_id = v_item_id AND quantity > 0;
    IF NOT FOUND THEN RAISE EXCEPTION 'capture_boost_not_owned'; END IF;

    INSERT INTO public.profile_capture_boosts (
        player_profile_id, item_id, activated_at, expires_at
    ) VALUES (
        p_profile_id, v_item_id, v_now, v_now + make_interval(secs => v_duration)
    )
    ON CONFLICT (player_profile_id, item_id) DO UPDATE
    SET activated_at = LEAST(public.profile_capture_boosts.activated_at, v_now),
        expires_at = GREATEST(public.profile_capture_boosts.expires_at, v_now) + make_interval(secs => v_duration),
        updated_at = now();

    RETURN public.capture_get_loadout_service(p_profile_id, p_authority_unix_milliseconds);
END;
$$;

CREATE OR REPLACE FUNCTION public.capture_resolve_profile_service(
    p_target JSONB,
    p_authority_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_assignment RECORD;
    v_tie_count INTEGER;
    v_profile public.capture_balance_profiles%ROWTYPE;
    v_event RECORD;
    v_events JSONB := '[]'::jsonb;
    v_delta JSONB := public.capture_effect_zero();
    v_now TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_legacy_rate INTEGER := COALESCE((p_target->>'legacy_base_capture_rate')::INTEGER, 45);
BEGIN
    SELECT a.*,
           ((a.species_id IS NOT NULL)::INT + (a.visual_variant_id IS NOT NULL)::INT +
            (a.mutation_id IS NOT NULL)::INT + (a.entangled_flag IS NOT NULL)::INT +
            (a.entangled_type IS NOT NULL)::INT + (a.area_id IS NOT NULL)::INT +
            (a.zone_id IS NOT NULL)::INT + (a.difficulty_id IS NOT NULL)::INT) AS specificity
    INTO v_assignment
    FROM public.capture_profile_assignments a
    WHERE a.enabled = true
      AND (a.species_id IS NULL OR a.species_id = p_target->>'species_id')
      AND (a.visual_variant_id IS NULL OR a.visual_variant_id = p_target->>'visual_variant_id')
      AND (a.mutation_id IS NULL OR a.mutation_id = p_target->>'mutation_id')
      AND (a.entangled_flag IS NULL OR a.entangled_flag = COALESCE((p_target->>'entangled')::BOOLEAN, false))
      AND (a.entangled_type IS NULL OR a.entangled_type = p_target->>'entangled_type')
      AND (a.area_id IS NULL OR a.area_id = p_target->>'area_id')
      AND (a.zone_id IS NULL OR a.zone_id = p_target->>'zone_id')
      AND (a.difficulty_id IS NULL OR a.difficulty_id = p_target->>'difficulty_id')
    ORDER BY specificity DESC, a.priority DESC
    LIMIT 1;

    IF FOUND THEN
        SELECT COUNT(*) INTO v_tie_count
        FROM public.capture_profile_assignments a
        WHERE a.enabled = true
          AND a.priority = v_assignment.priority
          AND ((a.species_id IS NOT NULL)::INT + (a.visual_variant_id IS NOT NULL)::INT +
               (a.mutation_id IS NOT NULL)::INT + (a.entangled_flag IS NOT NULL)::INT +
               (a.entangled_type IS NOT NULL)::INT + (a.area_id IS NOT NULL)::INT +
               (a.zone_id IS NOT NULL)::INT + (a.difficulty_id IS NOT NULL)::INT) = v_assignment.specificity
          AND (a.species_id IS NULL OR a.species_id = p_target->>'species_id')
          AND (a.visual_variant_id IS NULL OR a.visual_variant_id = p_target->>'visual_variant_id')
          AND (a.mutation_id IS NULL OR a.mutation_id = p_target->>'mutation_id')
          AND (a.entangled_flag IS NULL OR a.entangled_flag = COALESCE((p_target->>'entangled')::BOOLEAN, false))
          AND (a.entangled_type IS NULL OR a.entangled_type = p_target->>'entangled_type')
          AND (a.area_id IS NULL OR a.area_id = p_target->>'area_id')
          AND (a.zone_id IS NULL OR a.zone_id = p_target->>'zone_id')
          AND (a.difficulty_id IS NULL OR a.difficulty_id = p_target->>'difficulty_id');
        IF v_tie_count > 1 THEN RAISE EXCEPTION 'capture_profile_assignment_tie'; END IF;
        SELECT * INTO v_profile FROM public.capture_balance_profiles
        WHERE profile_id = v_assignment.profile_id AND enabled = true;
    ELSE
        SELECT * INTO v_profile FROM public.capture_balance_profiles
        WHERE profile_id = CASE
            WHEN v_legacy_rate >= 70 THEN 'calm'
            WHEN v_legacy_rate >= 40 THEN 'standard'
            WHEN v_legacy_rate >= 20 THEN 'volatile'
            ELSE 'extreme'
        END AND enabled = true;
    END IF;
    IF NOT FOUND THEN RAISE EXCEPTION 'capture_profile_not_found'; END IF;

    FOR v_event IN
        SELECT * FROM public.capture_live_events e
        WHERE e.enabled = true AND e.starts_at <= v_now AND e.ends_at > v_now
          AND (NOT (e.filters ? 'species_id') OR e.filters->>'species_id' = p_target->>'species_id')
          AND (NOT (e.filters ? 'visual_variant_id') OR e.filters->>'visual_variant_id' = p_target->>'visual_variant_id')
          AND (NOT (e.filters ? 'mutation_id') OR e.filters->>'mutation_id' = p_target->>'mutation_id')
          AND (NOT (e.filters ? 'entangled') OR (e.filters->>'entangled')::BOOLEAN = COALESCE((p_target->>'entangled')::BOOLEAN, false))
          AND (NOT (e.filters ? 'entangled_type') OR e.filters->>'entangled_type' = p_target->>'entangled_type')
          AND (NOT (e.filters ? 'area_id') OR e.filters->>'area_id' = p_target->>'area_id')
          AND (NOT (e.filters ? 'zone_id') OR e.filters->>'zone_id' = p_target->>'zone_id')
          AND (NOT (e.filters ? 'difficulty_id') OR e.filters->>'difficulty_id' = p_target->>'difficulty_id')
        ORDER BY e.priority ASC, e.event_id ASC
    LOOP
        IF v_event.replacement_profile_id IS NOT NULL THEN
            SELECT * INTO v_profile FROM public.capture_balance_profiles
            WHERE profile_id = v_event.replacement_profile_id AND enabled = true;
            IF NOT FOUND THEN RAISE EXCEPTION 'capture_event_profile_not_found'; END IF;
        END IF;
        v_delta := public.capture_effect_add(v_delta, COALESCE(v_event.typed_delta->'capture_effect', '{}'::jsonb));
        v_events := v_events || jsonb_build_array(jsonb_build_object(
            'event_id', v_event.event_id, 'config_version', v_event.config_version,
            'priority', v_event.priority, 'typed_delta', v_event.typed_delta
        ));
    END LOOP;

    RETURN jsonb_build_object(
        'profile', jsonb_build_object(
            'profile_id', v_profile.profile_id,
            'config_version', v_profile.config_version,
            'difficulty', v_profile.difficulty,
            'motion_multiplier_bp', v_profile.motion_multiplier_bp,
            'timer_multiplier_bp', v_profile.timer_multiplier_bp,
            'spike_chance_multiplier_bp', v_profile.spike_chance_multiplier_bp,
            'typed_delta', v_profile.typed_delta
        ),
        'assignment_id', COALESCE(v_assignment.assignment_id::TEXT, ''),
        'assignment_version', COALESCE(v_assignment.config_version, 0),
        'event_effects', v_delta,
        'events', v_events
    );
END;
$$;

DROP FUNCTION IF EXISTS public.authoritative_begin_battle_service(
    UUID, UUID, TEXT, BIGINT, TEXT, TEXT, JSONB, JSONB, BIGINT
);
CREATE OR REPLACE FUNCTION public.authoritative_begin_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_start_command_id TEXT,
    p_battle_kind TEXT,
    p_seed BIGINT,
    p_manifest_hash TEXT,
    p_config_hash TEXT,
    p_state_json JSONB,
    p_equipment_snapshot JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE v_existing public.authoritative_pve_battles%ROWTYPE;
BEGIN
    IF length(trim(COALESCE(p_start_command_id, ''))) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'code', 'command_id_required');
    END IF;
    PERFORM pg_advisory_xact_lock(hashtextextended(p_profile_id::TEXT, 0));
    SELECT * INTO v_existing FROM public.authoritative_pve_battles
    WHERE player_profile_id = p_profile_id
      AND status IN ('active', 'capturing', 'service_interrupted')
    FOR UPDATE;
    IF FOUND THEN
        IF v_existing.start_command_id = p_start_command_id THEN
            RETURN jsonb_build_object(
                'ok', true,
                'already_applied', true,
                'battle_id', v_existing.battle_id,
                'state_version', v_existing.state_version
            );
        END IF;
        RETURN jsonb_build_object('ok', false, 'code', 'battle_already_active', 'battle_id', v_existing.battle_id, 'state_version', v_existing.state_version);
    END IF;

    INSERT INTO public.authoritative_pve_battles (
        battle_id, player_profile_id, start_command_id, status, battle_kind, state_version, seed,
        manifest_hash, config_hash, state_json, equipment_snapshot, expires_at
    ) VALUES (
        p_battle_id, p_profile_id, p_start_command_id, 'active', p_battle_kind, 0, p_seed,
        p_manifest_hash, p_config_hash, COALESCE(p_state_json, '{}'::jsonb),
        COALESCE(p_equipment_snapshot, '{}'::jsonb),
        to_timestamp(p_expires_at_unix_milliseconds / 1000.0)
    );
    RETURN jsonb_build_object('ok', true, 'battle_id', p_battle_id, 'state_version', 0);
END;
$$;

CREATE OR REPLACE FUNCTION public.authoritative_checkpoint_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_expected_version INTEGER,
    p_new_version INTEGER,
    p_status TEXT,
    p_state_json JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    UPDATE public.authoritative_pve_battles
    SET state_version = p_new_version,
        status = p_status,
        state_json = COALESCE(p_state_json, '{}'::jsonb),
        expires_at = to_timestamp(p_expires_at_unix_milliseconds / 1000.0),
        checkpointed_at = now(),
        completed_at = CASE WHEN p_status IN ('completed', 'lost', 'abandoned') THEN now() ELSE NULL END,
        updated_at = now()
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
      AND state_version = p_expected_version;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'state_version_conflict');
    END IF;
    RETURN jsonb_build_object('ok', true, 'state_version', p_new_version);
END;
$$;

CREATE OR REPLACE FUNCTION public.authoritative_record_command_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_command_id TEXT,
    p_expected_version INTEGER,
    p_result_version INTEGER,
    p_command_kind TEXT,
    p_request_json JSONB,
    p_result_json JSONB
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE v_existing JSONB;
BEGIN
    SELECT result_json INTO v_existing FROM public.authoritative_pve_commands
    WHERE battle_id = p_battle_id AND command_id = p_command_id;
    IF FOUND THEN RETURN v_existing || jsonb_build_object('already_applied', true); END IF;

    INSERT INTO public.authoritative_pve_commands (
        battle_id, command_id, player_profile_id, expected_version, result_version,
        command_kind, request_json, result_json
    ) VALUES (
        p_battle_id, p_command_id, p_profile_id, p_expected_version, p_result_version,
        p_command_kind, COALESCE(p_request_json, '{}'::jsonb), COALESCE(p_result_json, '{}'::jsonb)
    );
    RETURN COALESCE(p_result_json, '{}'::jsonb) || jsonb_build_object('already_applied', false);
END;
$$;

-- Finalize an unrecovered resume-window expiry as a real PvE loss. This is one
-- service-only transaction so a retry cannot charge RAM or revive the party
-- twice. The flat penalty and revive fraction are server-owned rollout levers;
-- a future player-level term can be added here without changing the client.
CREATE OR REPLACE FUNCTION public.authoritative_expire_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT,
    p_ram_penalty INTEGER,
    p_respawn_hp_basis_points INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_authority_time TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_respawn_hp_basis_points INTEGER := LEAST(10000, GREATEST(0, COALESCE(p_respawn_hp_basis_points, 5000)));
    v_recovered_count INTEGER := 0;
    v_state_version INTEGER;
    v_economy JSONB := '{}'::jsonb;
BEGIN
    IF p_authority_unix_milliseconds IS NULL
       OR v_authority_time < now() - INTERVAL '1 hour'
       OR v_authority_time > now() + INTERVAL '5 minutes' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_authoritative_time');
    END IF;

    SELECT * INTO v_battle
    FROM public.authoritative_pve_battles
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found');
    END IF;

    IF v_battle.status = 'abandoned' THEN
        RETURN jsonb_build_object(
            'ok', true,
            'already_applied', true,
            'state_version', v_battle.state_version,
            'recovered_chainlings', 0
        );
    END IF;
    IF v_battle.status NOT IN ('active', 'capturing', 'service_interrupted') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_already_finalized');
    END IF;
    IF v_battle.expires_at > v_authority_time THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_expired');
    END IF;

    UPDATE public.profile_capture_events
    SET outcome = 'interrupted',
        finalized_at = COALESCE(finalized_at, now()),
        result_json = COALESCE(result_json, '{}'::jsonb) || jsonb_build_object(
            'success', false,
            'outcome', 'interrupted',
            'reason', 'battle_abandoned'
        )
    WHERE battle_id = p_battle_id AND outcome = 'pending';

    UPDATE public.authoritative_pve_battles
    SET status = 'abandoned',
        state_version = state_version + 1,
        state_json = jsonb_set(
            jsonb_set(COALESCE(state_json, '{}'::jsonb), '{phase}', '"complete"'::jsonb, true),
            '{endReason}', '"Abandoned"'::jsonb, true
        ),
        checkpointed_at = now(),
        completed_at = now(),
        updated_at = now()
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    RETURNING state_version INTO v_state_version;

    IF v_respawn_hp_basis_points > 0 THEN
        WITH source AS (
            SELECT
                c.id,
                v.base_max_hp,
                LEAST(5, GREATEST(0, COALESCE(c.rebirth_count, 0))) AS rebirth_count,
                LEAST(5, GREATEST(0, COALESCE((c.rebirth_iv_bonuses ->> 0)::INTEGER, 0))) AS hp_pick_count,
                LEAST(10, GREATEST(-10, COALESCE((c.ivs ->> 0)::INTEGER, 0))) AS hp_iv
            FROM public.chainlings c
            INNER JOIN public.chainling_species_vitals v
                ON v.species_id = lower(btrim(c.species_id))
            WHERE c.player_profile_id = p_profile_id
              AND c.is_party_member = true
              AND c.current_hp <= 0
            FOR UPDATE OF c
        ), numerators AS (
            SELECT *,
                (base_max_hp::BIGINT * 5 * rebirth_count) AS auto_numerator,
                (base_max_hp::BIGINT * 5 * hp_pick_count) AS pick_numerator
            FROM source
        ), quotients AS (
            SELECT *,
                FLOOR(auto_numerator::NUMERIC / 100)::INTEGER AS auto_quotient,
                FLOOR(pick_numerator::NUMERIC / 100)::INTEGER AS pick_quotient
            FROM numerators
        ), maxima AS (
            SELECT id, GREATEST(1,
                base_max_hp + hp_iv +
                auto_quotient + CASE
                    WHEN (auto_numerator % 100) * 2 > 100 THEN 1
                    WHEN (auto_numerator % 100) * 2 = 100 AND (auto_quotient % 2) <> 0 THEN 1
                    ELSE 0
                END +
                pick_quotient + CASE
                    WHEN (pick_numerator % 100) * 2 > 100 THEN 1
                    WHEN (pick_numerator % 100) * 2 = 100 AND (pick_quotient % 2) <> 0 THEN 1
                    ELSE 0
                END
            ) AS max_hp
            FROM quotients
        ), recovered AS (
            UPDATE public.chainlings c
            SET current_hp = LEAST(
                    m.max_hp,
                    GREATEST(1, (m.max_hp * v_respawn_hp_basis_points + 9999) / 10000)
                ),
                hp_synced_at = v_authority_time,
                passive_recovery_at = v_authority_time,
                updated_at = now()
            FROM maxima m
            WHERE c.id = m.id
            RETURNING c.id
        )
        SELECT COUNT(*) INTO v_recovered_count FROM recovered;
    END IF;

    SELECT public.profile_apply_pve_profile_lifetime_stats_tx(
        p_profile_id,
        0, 1, 0, 0, 0, 0, 0,
        0::BIGINT,
        GREATEST(0, COALESCE(p_ram_penalty, 0))::BIGINT
    ) INTO v_economy;

    RETURN jsonb_build_object(
        'ok', true,
        'already_applied', false,
        'state_version', v_state_version,
        'recovered_chainlings', v_recovered_count,
        'economy', v_economy
    );
END;
$$;

DROP FUNCTION IF EXISTS public.authoritative_begin_capture_attempt_service(
    UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT
);
CREATE OR REPLACE FUNCTION public.authoritative_begin_capture_attempt_service(
    p_attempt_id UUID,
    p_battle_id UUID,
    p_profile_id UUID,
    p_command_id TEXT,
    p_expected_version INTEGER,
    p_orb_id TEXT,
    p_capture_profile_id TEXT,
    p_battle_config_hash TEXT,
    p_attempt_config_hash TEXT,
    p_target_snapshot JSONB,
    p_low_hp_threshold_bp INTEGER,
    p_new_state_json JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_existing JSONB;
    v_result JSONB;
BEGIN
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found'); END IF;

    SELECT result_json INTO v_existing FROM public.authoritative_pve_commands
    WHERE battle_id = p_battle_id AND command_id = p_command_id;
    IF FOUND THEN RETURN v_existing || jsonb_build_object('already_applied', true); END IF;

    IF v_battle.status <> 'active' THEN RETURN jsonb_build_object('ok', false, 'code', 'battle_not_ready'); END IF;
    IF v_battle.state_version <> p_expected_version THEN RETURN jsonb_build_object('ok', false, 'code', 'state_version_conflict', 'state_version', v_battle.state_version); END IF;
    IF v_battle.config_hash <> p_battle_config_hash THEN RETURN jsonb_build_object('ok', false, 'code', 'upgrade_required'); END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.chainbound_item_catalog
        WHERE item_id = p_orb_id AND effect_family = 'capture_tool' AND enabled = true
    ) THEN RETURN jsonb_build_object('ok', false, 'code', 'invalid_orb'); END IF;

    UPDATE public.chainbound_profile_item_stacks
    SET quantity = quantity - 1, updated_at = now()
    WHERE player_profile_id = p_profile_id AND item_id = p_orb_id AND quantity > 0;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'orb_not_owned'); END IF;

    INSERT INTO public.player_lifetime_stats (player_profile_id, capture_attempts, capture_orbs_used)
    VALUES (p_profile_id, 1, 1)
    ON CONFLICT (player_profile_id) DO UPDATE
    SET capture_attempts = public.player_lifetime_stats.capture_attempts + 1,
        capture_orbs_used = public.player_lifetime_stats.capture_orbs_used + 1,
        updated_at = now();

    v_result := jsonb_build_object(
        'ok', true, 'attempt_id', p_attempt_id, 'battle_id', p_battle_id,
        'state_version', p_expected_version + 1, 'orb_consumed', true
    );

    INSERT INTO public.profile_capture_events (
        idempotency_key, player_profile_id, attempt_id, battle_id, command_id,
        orb_id, capture_profile_id, config_hash, target_id, species_id,
        target_snapshot, hp_band, low_hp_threshold_bp, outcome, alertness,
        accepted_at, result_json
    ) VALUES (
        p_command_id, p_profile_id, p_attempt_id, p_battle_id, p_command_id,
        p_orb_id, p_capture_profile_id, p_attempt_config_hash,
        p_target_snapshot->>'target_id', p_target_snapshot->>'species_id',
        p_target_snapshot,
        CASE WHEN COALESCE((p_target_snapshot->>'hp_basis_points')::INTEGER, 10000) <= p_low_hp_threshold_bp THEN 'low' ELSE 'above_low' END,
        p_low_hp_threshold_bp, 'pending', COALESCE((p_target_snapshot->>'alertness')::INTEGER, 0),
        now(), v_result
    );

    UPDATE public.authoritative_pve_battles
    SET status = 'capturing', state_version = state_version + 1,
        state_json = p_new_state_json,
        expires_at = to_timestamp(p_expires_at_unix_milliseconds / 1000.0),
        checkpointed_at = now(), updated_at = now()
    WHERE battle_id = p_battle_id;

    INSERT INTO public.authoritative_pve_commands (
        battle_id, command_id, player_profile_id, expected_version, result_version,
        command_kind, request_json, result_json
    ) VALUES (
        p_battle_id, p_command_id, p_profile_id, p_expected_version, p_expected_version + 1,
        'capture', jsonb_build_object('orb_id', p_orb_id, 'target_id', p_target_snapshot->>'target_id'), v_result
    );

    RETURN v_result;
END;
$$;

CREATE OR REPLACE FUNCTION public.authoritative_finalize_capture_attempt_service(
    p_attempt_id UUID,
    p_profile_id UUID,
    p_outcome TEXT,
    p_elapsed_milliseconds INTEGER,
    p_final_meter_bp INTEGER,
    p_history JSONB,
    p_perfect_lock BOOLEAN,
    p_entangled_type TEXT,
    p_new_battle_status TEXT,
    p_new_state_version INTEGER,
    p_new_state_json JSONB
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_event public.profile_capture_events%ROWTYPE;
    v_target JSONB;
    v_result JSONB;
    v_chainling_id UUID;
    v_move_item_id UUID;
    v_party_count INTEGER;
    v_party_slot INTEGER;
    v_is_party BOOLEAN;
    v_species_new BOOLEAN := false;
    v_entangled BOOLEAN;
    v_entangled_type TEXT;
    v_mutated BOOLEAN;
    v_visual_identity JSONB := jsonb_build_object(
        'aura_id', 'none', 'hud_card_variant_id', 'default', 'swap_in_fx_id', 'default',
        'swap_out_fx_id', 'default', 'ko_fx_id', 'none', 'victory_pose_id', 'default',
        'idle_style_id', 'none', 'idle_flourish_id', 'none', 'melee_attack_style_id', 'none',
        'ranged_attack_style_id', 'none', 'hit_reaction_style_id', 'none', 'attacker_ko_style_id', 'none'
    );
    v_unlocked JSONB := '[]'::jsonb;
BEGIN
    IF p_outcome NOT IN ('locked', 'escaped', 'timed_out', 'interrupted') THEN
        RAISE EXCEPTION 'invalid_capture_outcome';
    END IF;
    -- Battle first, attempt second: expiry uses the same lock order so a capture
    -- result and the 15-minute abandonment transaction cannot cross-commit.
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
    WHERE battle_id = (
        SELECT battle_id FROM public.profile_capture_events
        WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id
    ) AND player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;
    SELECT * INTO v_event FROM public.profile_capture_events
    WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'capture_attempt_not_found'; END IF;
    IF v_event.outcome <> 'pending' THEN
        RETURN COALESCE(v_event.result_json, '{}'::jsonb) || jsonb_build_object('already_applied', true);
    END IF;
    IF v_battle.status NOT IN ('capturing', 'service_interrupted') THEN
        RAISE EXCEPTION 'battle_not_capturing';
    END IF;
    IF p_new_state_version <> v_battle.state_version + 1 THEN
        RAISE EXCEPTION 'state_version_conflict';
    END IF;
    v_target := v_event.target_snapshot;

    IF p_outcome = 'locked' THEN
        v_entangled := COALESCE((v_target->>'entangled')::BOOLEAN, false) OR v_event.orb_id = 'entangle_orb';
        v_entangled_type := CASE
            WHEN v_event.orb_id = 'entangle_orb' THEN COALESCE(NULLIF(trim(p_entangled_type), ''), 'none')
            WHEN v_entangled THEN COALESCE(NULLIF(v_target->>'entangled_type', ''), 'none')
            ELSE 'none'
        END;
        v_mutated := COALESCE((v_target->>'mutated')::BOOLEAN, false);
        IF v_mutated THEN
            v_visual_identity := jsonb_set(v_visual_identity, '{aura_id}', to_jsonb(COALESCE(NULLIF(v_target->>'mutation_id', ''), 'mutation_default')), true);
        END IF;

        SELECT COUNT(*)::INTEGER INTO v_party_count FROM public.chainlings
        WHERE player_profile_id = p_profile_id AND COALESCE(is_party_member, false) = true;
        IF v_party_count < 3 THEN
            v_is_party := true;
            SELECT COALESCE(MIN(s.slot), v_party_count) INTO v_party_slot
            FROM generate_series(0, 2) AS s(slot)
            WHERE NOT EXISTS (
                SELECT 1 FROM public.chainlings c
                WHERE c.player_profile_id = p_profile_id AND COALESCE(c.is_party_member, false) = true AND c.party_slot = s.slot
            );
        ELSE v_is_party := false; v_party_slot := NULL; END IF;

        INSERT INTO public.chainlings (
            player_profile_id, species_id, is_party_member, party_slot, level, xp, ivs,
            rebirth_iv_bonuses, stars, rebirth_count, evolution_stage, visual_variant_id,
            visual_identity, entangled_flag, entangled_type, mutation_id, current_hp
        ) VALUES (
            p_profile_id, v_target->>'species_id', v_is_party, v_party_slot,
            GREATEST(1, COALESCE((v_target->>'level')::INTEGER, 1)), 0,
            COALESCE(v_target->'ivs', '[0,0,0,0,0,0]'::jsonb), '[0,0,0,0,0,0]'::jsonb,
            GREATEST(0, LEAST(5, COALESCE((v_target->>'stars')::INTEGER, 0))), 0, 1,
            COALESCE(NULLIF(v_target->>'visual_variant_id', ''), 'base'), v_visual_identity,
            v_entangled, v_entangled_type,
            CASE WHEN v_mutated THEN COALESCE(NULLIF(v_target->>'mutation_id', ''), 'mutation_default') ELSE 'none' END,
            GREATEST(1, LEAST(
                GREATEST(1, COALESCE((v_target->>'max_hp')::INTEGER, 1)),
                COALESCE((v_target->>'current_hp')::INTEGER, 1)
            ))
        ) RETURNING id INTO v_chainling_id;

        INSERT INTO public.move_items (
            player_profile_id, tier, move_id, modifier1_id, modifier2_id, locked, exported_asset_id, source
        ) VALUES (
            p_profile_id, 'ordinal'::public.move_tier,
            COALESCE(NULLIF(v_target->>'ordinal_move_id', ''), 'baseline_jab'),
            NULL, NULL, false, NULL, 'authoritative_capture'
        ) RETURNING id INTO v_move_item_id;

        SELECT NOT EXISTS (
            SELECT 1 FROM public.chainlings c
            WHERE c.player_profile_id = p_profile_id AND c.species_id = v_target->>'species_id' AND c.id <> v_chainling_id
        ) INTO v_species_new;

        INSERT INTO public.player_lifetime_stats (
            player_profile_id, total_captures, unique_species_captured, entangled_captures,
            total_ordinals_acquired, capture_perfect_locks, captures_above_low_hp,
            captures_by_orb, fastest_capture_milliseconds, extreme_profile_captures,
            entangle_orb_captures
        ) VALUES (
            p_profile_id, 1, CASE WHEN v_species_new THEN 1 ELSE 0 END, CASE WHEN v_entangled THEN 1 ELSE 0 END,
            1, CASE WHEN p_perfect_lock THEN 1 ELSE 0 END, CASE WHEN v_event.hp_band = 'above_low' THEN 1 ELSE 0 END,
            jsonb_build_object(v_event.orb_id, 1), GREATEST(0, p_elapsed_milliseconds),
            CASE WHEN v_event.capture_profile_id = 'extreme' THEN 1 ELSE 0 END,
            CASE WHEN v_event.orb_id = 'entangle_orb' THEN 1 ELSE 0 END
        )
        ON CONFLICT (player_profile_id) DO UPDATE SET
            total_captures = public.player_lifetime_stats.total_captures + 1,
            unique_species_captured = public.player_lifetime_stats.unique_species_captured + EXCLUDED.unique_species_captured,
            entangled_captures = public.player_lifetime_stats.entangled_captures + EXCLUDED.entangled_captures,
            total_ordinals_acquired = public.player_lifetime_stats.total_ordinals_acquired + 1,
            capture_perfect_locks = public.player_lifetime_stats.capture_perfect_locks + EXCLUDED.capture_perfect_locks,
            captures_above_low_hp = public.player_lifetime_stats.captures_above_low_hp + EXCLUDED.captures_above_low_hp,
            captures_by_orb = jsonb_set(
                public.player_lifetime_stats.captures_by_orb,
                ARRAY[v_event.orb_id],
                to_jsonb(COALESCE((public.player_lifetime_stats.captures_by_orb->>v_event.orb_id)::INTEGER, 0) + 1), true),
            fastest_capture_milliseconds = CASE
                WHEN public.player_lifetime_stats.fastest_capture_milliseconds IS NULL THEN EXCLUDED.fastest_capture_milliseconds
                ELSE LEAST(public.player_lifetime_stats.fastest_capture_milliseconds, EXCLUDED.fastest_capture_milliseconds)
            END,
            extreme_profile_captures = public.player_lifetime_stats.extreme_profile_captures + EXCLUDED.extreme_profile_captures,
            entangle_orb_captures = public.player_lifetime_stats.entangle_orb_captures + EXCLUDED.entangle_orb_captures,
            updated_at = now();

        INSERT INTO public.profile_capture_achievements (player_profile_id, achievement_id, attempt_id)
        VALUES (p_profile_id, 'first_lock', p_attempt_id) ON CONFLICT DO NOTHING;
        IF p_perfect_lock THEN
            INSERT INTO public.profile_capture_achievements (player_profile_id, achievement_id, attempt_id)
            VALUES (p_profile_id, 'clean_signal', p_attempt_id) ON CONFLICT DO NOTHING;
        END IF;
        IF v_event.hp_band = 'above_low' THEN
            INSERT INTO public.profile_capture_achievements (player_profile_id, achievement_id, attempt_id)
            VALUES (p_profile_id, 'against_the_noise', p_attempt_id) ON CONFLICT DO NOTHING;
        END IF;
        IF v_event.capture_profile_id = 'extreme' THEN
            INSERT INTO public.profile_capture_achievements (player_profile_id, achievement_id, attempt_id)
            VALUES (p_profile_id, 'extreme_hunter', p_attempt_id) ON CONFLICT DO NOTHING;
        END IF;
        IF v_event.orb_id = 'entangle_orb' THEN
            INSERT INTO public.profile_capture_achievements (player_profile_id, achievement_id, attempt_id)
            VALUES (p_profile_id, 'entangler', p_attempt_id) ON CONFLICT DO NOTHING;
        END IF;

        SELECT COALESCE(jsonb_agg(achievement_id ORDER BY achievement_id), '[]'::jsonb)
        INTO v_unlocked FROM public.profile_capture_achievements
        WHERE player_profile_id = p_profile_id AND attempt_id = p_attempt_id;
    ELSE
        INSERT INTO public.player_lifetime_stats (
            player_profile_id, capture_timeouts, capture_targets_escaped
        ) VALUES (
            p_profile_id, CASE WHEN p_outcome = 'timed_out' THEN 1 ELSE 0 END,
            CASE WHEN p_outcome = 'escaped' THEN 1 ELSE 0 END
        )
        ON CONFLICT (player_profile_id) DO UPDATE SET
            capture_timeouts = public.player_lifetime_stats.capture_timeouts + EXCLUDED.capture_timeouts,
            capture_targets_escaped = public.player_lifetime_stats.capture_targets_escaped + EXCLUDED.capture_targets_escaped,
            updated_at = now();
    END IF;

    v_result := jsonb_build_object(
        'ok', true, 'attempt_id', p_attempt_id, 'outcome', p_outcome,
        'final_meter_bp', GREATEST(0, LEAST(10000, p_final_meter_bp)),
        'elapsed_milliseconds', GREATEST(0, p_elapsed_milliseconds),
        'chainling_id', COALESCE(v_chainling_id::TEXT, ''),
        'move_item_id', COALESCE(v_move_item_id::TEXT, ''),
        'is_party_member', COALESCE(v_is_party, false),
        'party_slot', v_party_slot,
        'entangled_type', CASE WHEN COALESCE(v_entangled, false) THEN COALESCE(v_entangled_type, 'none') ELSE 'none' END,
        'unlocked_achievement_ids', v_unlocked,
        'already_applied', false
    );

    UPDATE public.profile_capture_events
    SET outcome = p_outcome, elapsed_milliseconds = GREATEST(0, p_elapsed_milliseconds),
        lock_escape_history = COALESCE(p_history, '[]'::jsonb),
        grant_chainling_id = v_chainling_id, grant_move_item_id = v_move_item_id,
        finalized_at = now(), result_json = v_result
    WHERE attempt_id = p_attempt_id;

    UPDATE public.authoritative_pve_battles
    SET status = p_new_battle_status, state_version = p_new_state_version,
        state_json = COALESCE(p_new_state_json, '{}'::jsonb), checkpointed_at = now(),
        completed_at = CASE WHEN p_new_battle_status IN ('completed', 'lost', 'abandoned') THEN now() ELSE NULL END,
        updated_at = now()
    WHERE battle_id = v_event.battle_id AND player_profile_id = p_profile_id
      AND state_version = v_battle.state_version
      AND status IN ('capturing', 'service_interrupted');
    IF NOT FOUND THEN RAISE EXCEPTION 'state_version_conflict'; END IF;

    RETURN v_result;
END;
$$;

REVOKE ALL ON FUNCTION public.capture_get_loadout_service(UUID, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.capture_equip_utility_gear_service(UUID, TEXT, TEXT, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.capture_activate_boost_service(UUID, TEXT, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.capture_resolve_profile_service(JSONB, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_begin_battle_service(UUID, UUID, TEXT, TEXT, BIGINT, TEXT, TEXT, JSONB, JSONB, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_checkpoint_battle_service(UUID, UUID, INTEGER, INTEGER, TEXT, JSONB, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_record_command_service(UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_expire_battle_service(UUID, UUID, BIGINT, INTEGER, INTEGER) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_begin_capture_attempt_service(UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_finalize_capture_attempt_service(UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.capture_get_loadout_service(UUID, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.capture_equip_utility_gear_service(UUID, TEXT, TEXT, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.capture_activate_boost_service(UUID, TEXT, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.capture_resolve_profile_service(JSONB, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_begin_battle_service(UUID, UUID, TEXT, TEXT, BIGINT, TEXT, TEXT, JSONB, JSONB, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_checkpoint_battle_service(UUID, UUID, INTEGER, INTEGER, TEXT, JSONB, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_record_command_service(UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_expire_battle_service(UUID, UUID, BIGINT, INTEGER, INTEGER) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_begin_capture_attempt_service(UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_finalize_capture_attempt_service(UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB) TO service_role;

COMMENT ON TABLE public.authoritative_pve_battles IS
    'Resumable authoritative PvE sessions. One active session per profile; checkpoints expire after 15 minutes.';
COMMENT ON TABLE public.profile_capture_events IS
    'Canonical capture ledger. Accepted attempts consume Orbs here; finalization grants outcomes idempotently.';
COMMENT ON TABLE public.capture_live_events IS
    'Timed capture overrides applied in ascending priority; profile replacement occurs before typed additive deltas.';
