-- Devnet-only hub presence gate. Do not apply to Mainnet Smoke.
-- hub_unlocked is not a client-writable progression flag.

CREATE TABLE IF NOT EXISTS public.hub_settings (
    settings_id integer PRIMARY KEY,
    multiplayer_unlocked boolean NOT NULL DEFAULT false,
    dev_exempt_enabled boolean NOT NULL DEFAULT false,
    CONSTRAINT hub_settings_singleton CHECK (settings_id = 1)
);

INSERT INTO public.hub_settings (settings_id, multiplayer_unlocked, dev_exempt_enabled)
VALUES (1, true, true)
ON CONFLICT (settings_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS public.hub_profiles (
    player_profile_id uuid PRIMARY KEY REFERENCES public.player_profiles (player_profile_id) ON DELETE CASCADE,
    hub_unlocked boolean NOT NULL DEFAULT false,
    dev_exempt boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT pg_catalog.now()
);

CREATE TABLE IF NOT EXISTS public.hub_identities (
    supabase_user_id uuid PRIMARY KEY,
    player_profile_id uuid NOT NULL REFERENCES public.player_profiles (player_profile_id) ON DELETE CASCADE,
    ugs_player_id text NOT NULL,
    linked_at timestamptz NOT NULL DEFAULT pg_catalog.now(),
    CONSTRAINT hub_identities_ugs_nonempty CHECK (length(btrim(ugs_player_id)) > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_hub_identities_ugs
    ON public.hub_identities (ugs_player_id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_hub_identities_profile
    ON public.hub_identities (player_profile_id);

ALTER TABLE public.hub_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.hub_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.hub_identities ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.hub_settings FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.hub_profiles FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.hub_identities FROM PUBLIC, anon, authenticated;

GRANT ALL ON TABLE public.hub_settings TO service_role;
GRANT ALL ON TABLE public.hub_profiles TO service_role;
GRANT ALL ON TABLE public.hub_identities TO service_role;

CREATE OR REPLACE FUNCTION public.hub_enqueue_allowed(p_profile_id uuid)
RETURNS text
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_unlocked boolean := false;
    v_dev_enabled boolean := false;
    v_hub boolean := false;
    v_exempt boolean := false;
BEGIN
    SELECT multiplayer_unlocked, dev_exempt_enabled
    INTO v_unlocked, v_dev_enabled
    FROM public.hub_settings
    WHERE settings_id = 1;

    IF NOT COALESCE(v_unlocked, false) THEN
        RETURN 'hub_locked';
    END IF;

    SELECT hub_unlocked, dev_exempt
    INTO v_hub, v_exempt
    FROM public.hub_profiles
    WHERE player_profile_id = p_profile_id;

    IF COALESCE(v_hub, false) THEN
        RETURN NULL;
    END IF;

    IF COALESCE(v_dev_enabled, false) AND COALESCE(v_exempt, false) THEN
        RETURN NULL;
    END IF;

    RETURN 'hub_locked';
END;
$$;

CREATE OR REPLACE FUNCTION public.hub_config_service(p_profile_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_unlocked boolean := false;
    v_hub boolean := false;
    v_gate text;
BEGIN
    SELECT multiplayer_unlocked INTO v_unlocked
    FROM public.hub_settings
    WHERE settings_id = 1;

    SELECT hub_unlocked INTO v_hub
    FROM public.hub_profiles
    WHERE player_profile_id = p_profile_id;

    v_gate := public.hub_enqueue_allowed(p_profile_id);
    RETURN jsonb_build_object(
        'success', true,
        'error', '',
        'multiplayer_unlocked', COALESCE(v_unlocked, false),
        'hub_unlocked', COALESCE(v_hub, false),
        'queue_open', v_gate IS NULL
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.hub_unlock_service(p_profile_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_unlocked boolean := false;
BEGIN
    PERFORM 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    SELECT multiplayer_unlocked INTO v_unlocked
    FROM public.hub_settings
    WHERE settings_id = 1;

    IF NOT COALESCE(v_unlocked, false) THEN
        RETURN jsonb_build_object('success', false, 'error', 'hub_locked');
    END IF;

    INSERT INTO public.hub_profiles (player_profile_id, hub_unlocked, updated_at)
    VALUES (p_profile_id, true, pg_catalog.now())
    ON CONFLICT (player_profile_id) DO UPDATE
    SET hub_unlocked = true,
        updated_at = pg_catalog.now();

    RETURN jsonb_build_object('success', true, 'error', '');
END;
$$;

CREATE OR REPLACE FUNCTION public.hub_link_identity_service(
    p_profile_id uuid,
    p_supabase_user_id uuid,
    p_ugs_player_id text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    IF p_supabase_user_id IS NULL OR p_ugs_player_id IS NULL OR length(btrim(p_ugs_player_id)) = 0 THEN
        RETURN jsonb_build_object('success', false, 'error', 'identity_required');
    END IF;

    PERFORM 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    INSERT INTO public.hub_identities (supabase_user_id, player_profile_id, ugs_player_id)
    VALUES (p_supabase_user_id, p_profile_id, btrim(p_ugs_player_id))
    ON CONFLICT (supabase_user_id) DO UPDATE
    SET player_profile_id = EXCLUDED.player_profile_id,
        ugs_player_id = EXCLUDED.ugs_player_id,
        linked_at = pg_catalog.now();

    RETURN jsonb_build_object('success', true, 'error', '');
END;
$$;

CREATE OR REPLACE FUNCTION public.hub_presence_card_service(p_ugs_player_id text)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_profile uuid;
    v_name text := 'Trainer';
    v_cosmetics text := '[]';
BEGIN
    IF p_ugs_player_id IS NULL OR length(btrim(p_ugs_player_id)) = 0 THEN
        RETURN jsonb_build_object(
            'success', true,
            'error', '',
            'display_name', 'Trainer',
            'cosmetic_ids_json', '[]'
        );
    END IF;

    SELECT player_profile_id INTO v_profile
    FROM public.hub_identities
    WHERE ugs_player_id = btrim(p_ugs_player_id);

    IF v_profile IS NOT NULL THEN
        SELECT COALESCE(NULLIF(btrim(display_name), ''), 'Trainer')
        INTO v_name
        FROM public.player_profiles
        WHERE player_profile_id = v_profile;

        SELECT COALESCE(jsonb_agg(DISTINCT catalog.cosmetic_id)::text, '[]')
        INTO v_cosmetics
        FROM public.chainbound_core_cosmetic_ownership_cache owned
        JOIN public.chainbound_cosmetic_catalog catalog
          ON catalog.cosmetic_id = owned.cosmetic_id
        WHERE owned.player_profile_id = v_profile
          AND owned.verification_status = 'verified';
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'error', '',
        'display_name', COALESCE(v_name, 'Trainer'),
        'cosmetic_ids_json', COALESCE(v_cosmetics, '[]')
    );
END;
$$;

REVOKE ALL ON FUNCTION public.hub_enqueue_allowed(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.hub_config_service(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.hub_unlock_service(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.hub_link_identity_service(uuid, uuid, text) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.hub_presence_card_service(text) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.hub_enqueue_allowed(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.hub_config_service(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.hub_unlock_service(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.hub_link_identity_service(uuid, uuid, text) TO service_role;
GRANT EXECUTE ON FUNCTION public.hub_presence_card_service(text) TO service_role;

CREATE OR REPLACE FUNCTION public.pvp_enqueue_service(
    p_profile_id uuid,
    p_queue_type text,
    p_ruleset_id text,
    p_config_hash text,
    p_app_version text,
    p_resolver_hash text,
    p_ruleset_hash text,
    p_build_stamp text,
    p_auth_user_id text,
    p_queue_timeout_seconds integer,
    p_turn_limit_seconds integer,
    p_forfeit_misses integer,
    p_rematch_cooldown_seconds integer,
    p_lock_deadline_seconds integer,
    p_result_window_seconds integer
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_timeout integer := GREATEST(1, COALESCE(p_queue_timeout_seconds, 120));
    v_cooldown integer := GREATEST(0, COALESCE(p_rematch_cooldown_seconds, 0));
    v_snapshot jsonb;
    v_gate text;
BEGIN
    PERFORM 1
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found', 'phase', 'idle', 'paired', false);
    END IF;

    v_gate := public.hub_enqueue_allowed(p_profile_id);
    IF v_gate IS NOT NULL THEN
        RETURN jsonb_build_object('success', false, 'error', v_gate, 'phase', 'idle', 'paired', false);
    END IF;

    IF EXISTS (
        SELECT 1 FROM public.pvp_matches
        WHERE (player_a = p_profile_id OR player_b = p_profile_id)
          AND state IN ('created', 'locked', 'in_progress')
    ) THEN
        RETURN public.pvp_snapshot_service(p_profile_id) || jsonb_build_object('success', true, 'error', '');
    END IF;

    IF v_cooldown > 0 AND EXISTS (
        SELECT 1 FROM public.pvp_matches
        WHERE (player_a = p_profile_id OR player_b = p_profile_id)
          AND state IN ('completed', 'disputed', 'forfeit_a', 'forfeit_b', 'cancelled')
          AND updated_at > pg_catalog.now() - make_interval(secs => v_cooldown)
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'enqueue_cooldown', 'phase', 'idle', 'paired', false);
    END IF;

    IF EXISTS (
        SELECT 1 FROM public.matchmaking_queue
        WHERE profile_id = p_profile_id
          AND status = 'waiting'
          AND expires_at > pg_catalog.now()
    ) THEN
        RETURN public.pvp_snapshot_service(p_profile_id) || jsonb_build_object('success', true, 'error', '', 'paired', false);
    END IF;

    BEGIN
        INSERT INTO public.matchmaking_queue (
            profile_id, queue_type, ruleset_id, config_hash, app_version,
            resolver_hash, ruleset_hash, build_stamp, auth_user_id,
            turn_limit_seconds, forfeit_miss_threshold, lock_deadline_seconds, result_window_seconds,
            expires_at, status
        ) VALUES (
            p_profile_id, p_queue_type, p_ruleset_id, p_config_hash, p_app_version,
            COALESCE(p_resolver_hash, ''), COALESCE(p_ruleset_hash, ''), COALESCE(p_build_stamp, ''), COALESCE(p_auth_user_id, ''),
            GREATEST(1, COALESCE(p_turn_limit_seconds, 60)),
            GREATEST(1, COALESCE(p_forfeit_misses, 2)),
            GREATEST(1, COALESCE(p_lock_deadline_seconds, 30)),
            GREATEST(1, COALESCE(p_result_window_seconds, 45)),
            pg_catalog.now() + make_interval(secs => v_timeout), 'waiting'
        );
    EXCEPTION
        WHEN unique_violation THEN
            RETURN public.pvp_snapshot_service(p_profile_id) || jsonb_build_object('success', true, 'error', '', 'paired', false);
    END;

    v_snapshot := public.pvp_snapshot_service(p_profile_id);
    RETURN v_snapshot || jsonb_build_object('success', true, 'error', '', 'paired', false);
END;
$$;

REVOKE ALL ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, text, text, text, text, integer, integer, integer, integer, integer, integer) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, text, text, text, text, integer, integer, integer, integer, integer, integer) TO service_role;
