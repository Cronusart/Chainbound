DROP FUNCTION IF EXISTS public.pvp_enqueue_service(uuid, text, text, text, text, integer, integer, integer);

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
BEGIN
    PERFORM 1
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found', 'phase', 'idle', 'paired', false);
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

CREATE OR REPLACE FUNCTION public.pvp_matchmaker_service()
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_left public.matchmaking_queue%ROWTYPE;
    v_right public.matchmaking_queue%ROWTYPE;
    v_skipped uuid[] := ARRAY[]::uuid[];
    v_match_id uuid;
    v_seed integer;
    v_paired integer := 0;
    v_guard integer := 0;
BEGIN
    UPDATE public.matchmaking_queue
    SET status = 'expired'
    WHERE status = 'waiting'
      AND expires_at <= pg_catalog.now();

    UPDATE public.pvp_matches
    SET state = 'cancelled',
        winner_seat = NULL,
        turn_deadline_at = NULL,
        updated_at = pg_catalog.now()
    WHERE state = 'created'
      AND lock_deadline_at IS NOT NULL
      AND lock_deadline_at <= pg_catalog.now();

    PERFORM public.pvp_sweep_timeouts_service(NULL);

    LOOP
        v_guard := v_guard + 1;
        IF v_guard > 100 THEN
            EXIT;
        END IF;

        SELECT * INTO v_left
        FROM public.matchmaking_queue
        WHERE status = 'waiting'
          AND expires_at > pg_catalog.now()
          AND NOT (queue_id = ANY (v_skipped))
        ORDER BY enqueued_at
        LIMIT 1
        FOR UPDATE SKIP LOCKED;
        EXIT WHEN NOT FOUND;

        SELECT * INTO v_right
        FROM public.matchmaking_queue
        WHERE status = 'waiting'
          AND queue_id <> v_left.queue_id
          AND profile_id <> v_left.profile_id
          AND expires_at > pg_catalog.now()
          AND queue_type = v_left.queue_type
          AND ruleset_id = v_left.ruleset_id
          AND config_hash = v_left.config_hash
          AND app_version = v_left.app_version
          AND resolver_hash = v_left.resolver_hash
          AND ruleset_hash = v_left.ruleset_hash
          AND build_stamp = v_left.build_stamp
          AND NOT (
            COALESCE(v_left.auth_user_id, '') <> ''
            AND auth_user_id = v_left.auth_user_id
          )
        ORDER BY enqueued_at
        LIMIT 1
        FOR UPDATE SKIP LOCKED;

        IF NOT FOUND THEN
            v_skipped := array_append(v_skipped, v_left.queue_id);
            CONTINUE;
        END IF;

        v_match_id := pg_catalog.gen_random_uuid();
        v_seed := public.pvp_positive_seed();
        INSERT INTO public.pvp_matches (
            match_id, queue_type, ruleset_id, config_hash, app_version,
            resolver_hash, ruleset_hash, build_stamp, seed,
            player_a, player_b, auth_user_a, auth_user_b,
            state, turn_index, turn_limit_seconds, forfeit_miss_threshold,
            result_window_seconds, lock_deadline_at, hp_policy
        ) VALUES (
            v_match_id, v_left.queue_type, v_left.ruleset_id, v_left.config_hash, v_left.app_version,
            v_left.resolver_hash, v_left.ruleset_hash, v_left.build_stamp, v_seed,
            v_left.profile_id, v_right.profile_id, COALESCE(v_left.auth_user_id, ''), COALESCE(v_right.auth_user_id, ''),
            'created', 0, v_left.turn_limit_seconds, v_left.forfeit_miss_threshold,
            v_left.result_window_seconds,
            pg_catalog.now() + make_interval(secs => GREATEST(1, v_left.lock_deadline_seconds)),
            'full_heal'
        );

        UPDATE public.matchmaking_queue
        SET status = 'matched', match_id = v_match_id
        WHERE queue_id IN (v_left.queue_id, v_right.queue_id);
        v_paired := v_paired + 1;
    END LOOP;

    RETURN jsonb_build_object('success', true, 'error', '', 'paired', v_paired);
END;
$$;

REVOKE ALL ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, text, text, text, text, integer, integer, integer, integer, integer, integer) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_matchmaker_service() FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, text, text, text, text, integer, integer, integer, integer, integer, integer) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_matchmaker_service() TO service_role;
