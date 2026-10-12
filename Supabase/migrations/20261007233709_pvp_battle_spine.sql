-- Casual PvP battle spine. Service-role edge functions are the only writers.
-- Clients do not receive table grants. Combat state is not stored here; the turn log is.

CREATE TABLE IF NOT EXISTS public.matchmaking_queue (
    queue_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id uuid NOT NULL REFERENCES public.player_profiles (player_profile_id) ON DELETE CASCADE,
    queue_type text NOT NULL DEFAULT 'casual' CHECK (queue_type = 'casual'),
    ruleset_id text NOT NULL,
    config_hash text NOT NULL,
    app_version text NOT NULL,
    enqueued_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    status text NOT NULL DEFAULT 'waiting' CHECK (status IN ('waiting', 'matched', 'cancelled', 'expired')),
    match_id uuid NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_matchmaking_queue_one_waiting
    ON public.matchmaking_queue (profile_id)
    WHERE status = 'waiting';

CREATE INDEX IF NOT EXISTS idx_matchmaking_queue_pair
    ON public.matchmaking_queue (queue_type, ruleset_id, config_hash, app_version, enqueued_at)
    WHERE status = 'waiting';

CREATE INDEX IF NOT EXISTS idx_matchmaking_queue_profile
    ON public.matchmaking_queue (profile_id, enqueued_at DESC);

CREATE TABLE IF NOT EXISTS public.pvp_matches (
    match_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    queue_type text NOT NULL DEFAULT 'casual' CHECK (queue_type = 'casual'),
    ruleset_id text NOT NULL,
    config_hash text NOT NULL,
    app_version text NOT NULL,
    seed integer NOT NULL CHECK (seed > 0 AND seed < 2147483648),
    player_a uuid NOT NULL REFERENCES public.player_profiles (player_profile_id),
    player_b uuid NOT NULL REFERENCES public.player_profiles (player_profile_id),
    party_a jsonb,
    party_b jsonb,
    state text NOT NULL CHECK (state IN (
        'created', 'locked', 'in_progress', 'completed', 'disputed', 'forfeit_a', 'forfeit_b', 'cancelled'
    )),
    turn_index integer NOT NULL DEFAULT 0,
    consecutive_misses_a integer NOT NULL DEFAULT 0,
    consecutive_misses_b integer NOT NULL DEFAULT 0,
    forensic_hash_a text,
    forensic_hash_b text,
    turn_deadline_at timestamptz,
    turn_limit_seconds integer NOT NULL DEFAULT 60 CHECK (turn_limit_seconds > 0),
    forfeit_miss_threshold integer NOT NULL DEFAULT 2 CHECK (forfeit_miss_threshold > 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pvp_matches_distinct_players CHECK (player_a <> player_b)
);

CREATE INDEX IF NOT EXISTS idx_pvp_matches_player_a_open
    ON public.pvp_matches (player_a, created_at DESC)
    WHERE state IN ('created', 'locked', 'in_progress');

CREATE INDEX IF NOT EXISTS idx_pvp_matches_player_b_open
    ON public.pvp_matches (player_b, created_at DESC)
    WHERE state IN ('created', 'locked', 'in_progress');

CREATE TABLE IF NOT EXISTS public.pvp_match_turns (
    match_id uuid NOT NULL REFERENCES public.pvp_matches (match_id) ON DELETE CASCADE,
    turn_index integer NOT NULL CHECK (turn_index >= 1),
    seat text NOT NULL CHECK (seat IN ('a', 'b')),
    action_json jsonb NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    committed boolean NOT NULL DEFAULT false,
    PRIMARY KEY (match_id, turn_index, seat)
);

ALTER TABLE public.matchmaking_queue ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pvp_matches ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pvp_match_turns ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.matchmaking_queue FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.pvp_matches FROM PUBLIC, anon, authenticated;
REVOKE ALL ON TABLE public.pvp_match_turns FROM PUBLIC, anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.matchmaking_queue TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.pvp_matches TO service_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.pvp_match_turns TO service_role;

CREATE OR REPLACE FUNCTION public.pvp_positive_seed()
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_bytes bytea := extensions.gen_random_bytes(4);
    v_seed integer;
BEGIN
    v_seed := pg_catalog.get_byte(v_bytes, 0)
        + pg_catalog.get_byte(v_bytes, 1) * 256
        + pg_catalog.get_byte(v_bytes, 2) * 65536
        + (pg_catalog.get_byte(v_bytes, 3) % 128) * 16777216;
    IF v_seed < 1 THEN
        v_seed := 1;
    END IF;
    RETURN v_seed;
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_sweep_timeouts_service(p_match_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_submitted_a boolean;
    v_submitted_b boolean;
    v_miss_a integer;
    v_miss_b integer;
    v_limit integer;
BEGIN
    UPDATE public.matchmaking_queue
    SET status = 'expired'
    WHERE status = 'waiting'
      AND expires_at <= pg_catalog.now();

    FOR v_match IN
        SELECT *
        FROM public.pvp_matches
        WHERE state IN ('locked', 'in_progress')
          AND turn_deadline_at IS NOT NULL
          AND turn_deadline_at <= pg_catalog.now()
          AND (p_match_id IS NULL OR match_id = p_match_id)
        FOR UPDATE
    LOOP
        SELECT EXISTS (
            SELECT 1 FROM public.pvp_match_turns
            WHERE match_id = v_match.match_id
              AND turn_index = v_match.turn_index
              AND seat = 'a'
        ) INTO v_submitted_a;
        SELECT EXISTS (
            SELECT 1 FROM public.pvp_match_turns
            WHERE match_id = v_match.match_id
              AND turn_index = v_match.turn_index
              AND seat = 'b'
        ) INTO v_submitted_b;

        v_miss_a := CASE WHEN v_submitted_a THEN v_match.consecutive_misses_a ELSE v_match.consecutive_misses_a + 1 END;
        v_miss_b := CASE WHEN v_submitted_b THEN v_match.consecutive_misses_b ELSE v_match.consecutive_misses_b + 1 END;
        v_limit := GREATEST(1, v_match.forfeit_miss_threshold);

        IF v_miss_a >= v_limit AND v_miss_b >= v_limit THEN
            UPDATE public.pvp_matches
            SET state = 'cancelled',
                consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        ELSIF v_miss_a >= v_limit THEN
            UPDATE public.pvp_matches
            SET state = 'forfeit_a',
                consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        ELSIF v_miss_b >= v_limit THEN
            UPDATE public.pvp_matches
            SET state = 'forfeit_b',
                consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        ELSE
            UPDATE public.pvp_matches
            SET consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = pg_catalog.now() + make_interval(secs => v_match.turn_limit_seconds),
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        END IF;
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_snapshot_service(p_profile_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_queue public.matchmaking_queue%ROWTYPE;
    v_seat text := '';
    v_committed integer := 0;
    v_actions_a text := '';
    v_actions_b text := '';
    v_found boolean := false;
BEGIN
    PERFORM public.pvp_sweep_timeouts_service(NULL);

    SELECT * INTO v_match
    FROM public.pvp_matches
    WHERE (player_a = p_profile_id OR player_b = p_profile_id)
      AND state IN ('created', 'locked', 'in_progress')
    ORDER BY created_at DESC
    LIMIT 1;
    v_found := FOUND;

    IF NOT v_found THEN
        SELECT * INTO v_queue
        FROM public.matchmaking_queue
        WHERE profile_id = p_profile_id
          AND status = 'waiting'
          AND expires_at > pg_catalog.now()
        ORDER BY enqueued_at DESC
        LIMIT 1;
        IF FOUND THEN
            RETURN jsonb_build_object(
                'success', true,
                'error', '',
                'phase', 'waiting',
                'match_id', '',
                'seat', '',
                'seed', 0,
                'ruleset_id', v_queue.ruleset_id,
                'state', 'waiting',
                'turn_index', 0,
                'party_a_json', '',
                'party_b_json', '',
                'committed_turn_index', 0,
                'actions_a_json', '',
                'actions_b_json', '',
                'forensic_hash_a', '',
                'forensic_hash_b', '',
                'player_a', '',
                'player_b', '',
                'paired', false
            );
        END IF;

        SELECT * INTO v_match
        FROM public.pvp_matches
        WHERE player_a = p_profile_id OR player_b = p_profile_id
        ORDER BY created_at DESC
        LIMIT 1;
        v_found := FOUND;
    END IF;

    IF NOT v_found THEN
        RETURN jsonb_build_object(
            'success', true,
            'error', '',
            'phase', 'idle',
            'match_id', '',
            'seat', '',
            'seed', 0,
            'ruleset_id', '',
            'state', '',
            'turn_index', 0,
            'party_a_json', '',
            'party_b_json', '',
            'committed_turn_index', 0,
            'actions_a_json', '',
            'actions_b_json', '',
            'forensic_hash_a', '',
            'forensic_hash_b', '',
            'player_a', '',
            'player_b', '',
            'paired', false
        );
    END IF;

    v_seat := CASE WHEN v_match.player_a = p_profile_id THEN 'a' WHEN v_match.player_b = p_profile_id THEN 'b' ELSE '' END;
    v_committed := GREATEST(0, v_match.turn_index - 1);
    IF v_committed >= 1 THEN
        SELECT COALESCE(action_json::text, '') INTO v_actions_a
        FROM public.pvp_match_turns
        WHERE match_id = v_match.match_id AND turn_index = v_committed AND seat = 'a' AND committed;
        SELECT COALESCE(action_json::text, '') INTO v_actions_b
        FROM public.pvp_match_turns
        WHERE match_id = v_match.match_id AND turn_index = v_committed AND seat = 'b' AND committed;
        IF v_actions_a IS NULL OR v_actions_b IS NULL OR v_actions_a = '' OR v_actions_b = '' THEN
            v_committed := 0;
            v_actions_a := '';
            v_actions_b := '';
        END IF;
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'error', '',
        'phase', 'matched',
        'match_id', v_match.match_id::text,
        'seat', v_seat,
        'seed', v_match.seed,
        'ruleset_id', v_match.ruleset_id,
        'state', v_match.state,
        'turn_index', v_match.turn_index,
        'party_a_json', COALESCE(v_match.party_a::text, ''),
        'party_b_json', COALESCE(v_match.party_b::text, ''),
        'committed_turn_index', v_committed,
        'actions_a_json', COALESCE(v_actions_a, ''),
        'actions_b_json', COALESCE(v_actions_b, ''),
        'forensic_hash_a', COALESCE(v_match.forensic_hash_a, ''),
        'forensic_hash_b', COALESCE(v_match.forensic_hash_b, ''),
        'player_a', v_match.player_a::text,
        'player_b', v_match.player_b::text,
        'paired', true
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_enqueue_service(
    p_profile_id uuid,
    p_queue_type text,
    p_ruleset_id text,
    p_config_hash text,
    p_app_version text,
    p_queue_timeout_seconds integer,
    p_turn_limit_seconds integer,
    p_forfeit_misses integer
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_opponent public.matchmaking_queue%ROWTYPE;
    v_match_id uuid;
    v_seed integer;
    v_timeout integer := GREATEST(1, COALESCE(p_queue_timeout_seconds, 120));
    v_snapshot jsonb;
BEGIN
    PERFORM 1
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found', 'phase', 'idle', 'paired', false);
    END IF;

    PERFORM public.pvp_sweep_timeouts_service(NULL);

    IF EXISTS (
        SELECT 1 FROM public.pvp_matches
        WHERE (player_a = p_profile_id OR player_b = p_profile_id)
          AND state IN ('created', 'locked', 'in_progress')
    ) THEN
        v_snapshot := public.pvp_snapshot_service(p_profile_id);
        RETURN v_snapshot || jsonb_build_object('success', false, 'error', 'match_in_progress');
    END IF;

    IF EXISTS (
        SELECT 1 FROM public.matchmaking_queue
        WHERE profile_id = p_profile_id
          AND status = 'waiting'
          AND expires_at > pg_catalog.now()
    ) THEN
        v_snapshot := public.pvp_snapshot_service(p_profile_id);
        RETURN v_snapshot || jsonb_build_object('success', true, 'error', '', 'paired', false);
    END IF;

    SELECT * INTO v_opponent
    FROM public.matchmaking_queue
    WHERE status = 'waiting'
      AND profile_id <> p_profile_id
      AND queue_type = p_queue_type
      AND ruleset_id = p_ruleset_id
      AND config_hash = p_config_hash
      AND app_version = p_app_version
      AND expires_at > pg_catalog.now()
    ORDER BY enqueued_at
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    IF FOUND THEN
        v_match_id := pg_catalog.gen_random_uuid();
        v_seed := public.pvp_positive_seed();
        INSERT INTO public.pvp_matches (
            match_id, queue_type, ruleset_id, config_hash, app_version, seed,
            player_a, player_b, state, turn_index, turn_limit_seconds, forfeit_miss_threshold
        ) VALUES (
            v_match_id, p_queue_type, p_ruleset_id, p_config_hash, p_app_version, v_seed,
            v_opponent.profile_id, p_profile_id, 'created', 0,
            GREATEST(1, COALESCE(p_turn_limit_seconds, 60)),
            GREATEST(1, COALESCE(p_forfeit_misses, 2))
        );
        UPDATE public.matchmaking_queue
        SET status = 'matched', match_id = v_match_id
        WHERE queue_id = v_opponent.queue_id;

        INSERT INTO public.matchmaking_queue (
            profile_id, queue_type, ruleset_id, config_hash, app_version, enqueued_at, expires_at, status, match_id
        ) VALUES (
            p_profile_id, p_queue_type, p_ruleset_id, p_config_hash, p_app_version,
            pg_catalog.now(), pg_catalog.now(), 'matched', v_match_id
        );

        v_snapshot := public.pvp_snapshot_service(p_profile_id);
        RETURN v_snapshot || jsonb_build_object('success', true, 'error', '', 'paired', true);
    END IF;

    BEGIN
        INSERT INTO public.matchmaking_queue (
            profile_id, queue_type, ruleset_id, config_hash, app_version, expires_at, status
        ) VALUES (
            p_profile_id, p_queue_type, p_ruleset_id, p_config_hash, p_app_version,
            pg_catalog.now() + make_interval(secs => v_timeout), 'waiting'
        );
    EXCEPTION
        WHEN unique_violation THEN
            v_snapshot := public.pvp_snapshot_service(p_profile_id);
            RETURN v_snapshot || jsonb_build_object('success', true, 'error', '', 'paired', false);
    END;

    v_snapshot := public.pvp_snapshot_service(p_profile_id);
    RETURN v_snapshot || jsonb_build_object('success', true, 'error', '', 'paired', false);
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_cancel_queue_service(p_profile_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_updated integer;
    v_snapshot jsonb;
BEGIN
    UPDATE public.matchmaking_queue
    SET status = 'cancelled'
    WHERE profile_id = p_profile_id
      AND status = 'waiting';
    GET DIAGNOSTICS v_updated = ROW_COUNT;
    v_snapshot := public.pvp_snapshot_service(p_profile_id);
    IF v_updated = 0 THEN
        RETURN v_snapshot || jsonb_build_object('success', false, 'error', 'not_waiting');
    END IF;
    RETURN v_snapshot || jsonb_build_object('success', true, 'error', '');
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_lock_parties_service(
    p_match_id uuid,
    p_actor_id uuid,
    p_party_a jsonb,
    p_party_b jsonb
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_count_a integer;
    v_count_b integer;
    v_snapshot jsonb;
BEGIN
    SELECT * INTO v_match
    FROM public.pvp_matches
    WHERE match_id = p_match_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'match_not_found', 'phase', 'idle', 'paired', false);
    END IF;
    IF v_match.player_a <> p_actor_id AND v_match.player_b <> p_actor_id THEN
        RETURN jsonb_build_object('success', false, 'error', 'not_participant', 'phase', 'idle', 'paired', false);
    END IF;

    IF v_match.party_a IS NOT NULL AND v_match.party_b IS NOT NULL AND v_match.state <> 'created' THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
    END IF;

    v_count_a := COALESCE(jsonb_array_length(p_party_a -> 'chainlings'), 0);
    v_count_b := COALESCE(jsonb_array_length(p_party_b -> 'chainlings'), 0);
    IF v_count_a < 1 OR v_count_b < 1 THEN
        UPDATE public.pvp_matches
        SET state = 'cancelled', updated_at = pg_catalog.now(), turn_deadline_at = NULL
        WHERE match_id = p_match_id
          AND state = 'created';
        v_snapshot := public.pvp_snapshot_service(p_actor_id);
        RETURN v_snapshot || jsonb_build_object('success', false, 'error', 'empty_party');
    END IF;

    UPDATE public.pvp_matches
    SET party_a = p_party_a,
        party_b = p_party_b,
        state = 'locked',
        turn_index = 1,
        turn_deadline_at = pg_catalog.now() + make_interval(secs => turn_limit_seconds),
        updated_at = pg_catalog.now()
    WHERE match_id = p_match_id
      AND state = 'created';

    RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_submit_turn_service(
    p_match_id uuid,
    p_actor_id uuid,
    p_turn_index integer,
    p_action jsonb
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_seat text;
    v_existing jsonb;
    v_both boolean;
BEGIN
    PERFORM public.pvp_sweep_timeouts_service(p_match_id);

    SELECT * INTO v_match
    FROM public.pvp_matches
    WHERE match_id = p_match_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'match_not_found', 'phase', 'idle', 'paired', false);
    END IF;

    v_seat := CASE
        WHEN v_match.player_a = p_actor_id THEN 'a'
        WHEN v_match.player_b = p_actor_id THEN 'b'
        ELSE ''
    END;
    IF v_seat = '' THEN
        RETURN jsonb_build_object('success', false, 'error', 'not_participant', 'phase', 'idle', 'paired', false);
    END IF;

    IF v_match.state NOT IN ('locked', 'in_progress') THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'match_not_active');
    END IF;
    IF p_turn_index IS DISTINCT FROM v_match.turn_index THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'turn_mismatch');
    END IF;

    SELECT action_json INTO v_existing
    FROM public.pvp_match_turns
    WHERE match_id = p_match_id AND turn_index = p_turn_index AND seat = v_seat;
    IF FOUND THEN
        IF v_existing IS DISTINCT FROM p_action THEN
            RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'turn_conflict');
        END IF;
    ELSE
        INSERT INTO public.pvp_match_turns (match_id, turn_index, seat, action_json, committed)
        VALUES (p_match_id, p_turn_index, v_seat, p_action, false);
    END IF;

    SELECT COUNT(*) = 2 INTO v_both
    FROM public.pvp_match_turns
    WHERE match_id = p_match_id AND turn_index = p_turn_index;

    IF v_both THEN
        UPDATE public.pvp_match_turns
        SET committed = true
        WHERE match_id = p_match_id AND turn_index = p_turn_index;
        UPDATE public.pvp_matches
        SET turn_index = turn_index + 1,
            consecutive_misses_a = 0,
            consecutive_misses_b = 0,
            state = 'in_progress',
            turn_deadline_at = pg_catalog.now() + make_interval(secs => turn_limit_seconds),
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id;
    END IF;

    RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_submit_result_service(
    p_match_id uuid,
    p_actor_id uuid,
    p_forensic_hash text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_seat text;
    v_hash text := NULLIF(btrim(COALESCE(p_forensic_hash, '')), '');
    v_a text;
    v_b text;
BEGIN
    IF v_hash IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'hash_required', 'phase', 'idle', 'paired', false);
    END IF;

    SELECT * INTO v_match
    FROM public.pvp_matches
    WHERE match_id = p_match_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'match_not_found', 'phase', 'idle', 'paired', false);
    END IF;

    v_seat := CASE
        WHEN v_match.player_a = p_actor_id THEN 'a'
        WHEN v_match.player_b = p_actor_id THEN 'b'
        ELSE ''
    END;
    IF v_seat = '' THEN
        RETURN jsonb_build_object('success', false, 'error', 'not_participant', 'phase', 'idle', 'paired', false);
    END IF;

    IF v_match.state IN ('forfeit_a', 'forfeit_b', 'cancelled', 'completed', 'disputed') THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
    END IF;
    IF v_match.state NOT IN ('locked', 'in_progress') THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'match_not_active');
    END IF;

    IF v_seat = 'a' THEN
        UPDATE public.pvp_matches
        SET forensic_hash_a = COALESCE(forensic_hash_a, v_hash),
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id
          AND (forensic_hash_a IS NULL OR forensic_hash_a = v_hash);
    ELSE
        UPDATE public.pvp_matches
        SET forensic_hash_b = COALESCE(forensic_hash_b, v_hash),
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id
          AND (forensic_hash_b IS NULL OR forensic_hash_b = v_hash);
    END IF;

    IF NOT FOUND THEN
        RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'hash_conflict');
    END IF;

    SELECT forensic_hash_a, forensic_hash_b INTO v_a, v_b
    FROM public.pvp_matches
    WHERE match_id = p_match_id;

    IF v_a IS NOT NULL AND v_b IS NOT NULL THEN
        UPDATE public.pvp_matches
        SET state = CASE WHEN v_a = v_b THEN 'completed' ELSE 'disputed' END,
            turn_deadline_at = NULL,
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id
          AND state IN ('locked', 'in_progress');
    END IF;

    RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
END;
$$;

REVOKE ALL ON FUNCTION public.pvp_positive_seed() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_sweep_timeouts_service(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_snapshot_service(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, integer, integer, integer) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_cancel_queue_service(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_lock_parties_service(uuid, uuid, jsonb, jsonb) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_submit_turn_service(uuid, uuid, integer, jsonb) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_submit_result_service(uuid, uuid, text) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.pvp_positive_seed() TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_sweep_timeouts_service(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_snapshot_service(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_enqueue_service(uuid, text, text, text, text, integer, integer, integer) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_cancel_queue_service(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_lock_parties_service(uuid, uuid, jsonb, jsonb) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_submit_turn_service(uuid, uuid, integer, jsonb) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_submit_result_service(uuid, uuid, text) TO service_role;
