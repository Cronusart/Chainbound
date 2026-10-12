CREATE OR REPLACE FUNCTION public.pvp_party_problem(p_party jsonb)
RETURNS text
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_count integer;
    v_fainted integer;
BEGIN
    v_count := COALESCE(jsonb_array_length(p_party -> 'chainlings'), 0);
    IF v_count < 1 THEN
        RETURN 'empty_party';
    END IF;
    IF v_count > 3 THEN
        RETURN 'party_too_large';
    END IF;

    SELECT COUNT(*) INTO v_fainted
    FROM jsonb_array_elements(p_party -> 'chainlings') AS elem
    WHERE COALESCE((elem ->> 'currentHp')::integer, -1) BETWEEN 0 AND 0;

    IF v_fainted = v_count THEN
        RETURN 'party_fainted';
    END IF;
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_winner_seat(p_team integer)
RETURNS text
LANGUAGE sql
IMMUTABLE
SET search_path = ''
AS $$
    SELECT CASE p_team
        WHEN 0 THEN 'a'
        WHEN 1 THEN 'b'
        WHEN -2 THEN 'draw'
        ELSE NULL
    END;
$$;

CREATE OR REPLACE FUNCTION public.pvp_apply_result_window(p_match_id uuid)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_window integer;
BEGIN
    SELECT * INTO v_match
    FROM public.pvp_matches
    WHERE match_id = p_match_id
    FOR UPDATE;
    IF NOT FOUND OR v_match.state NOT IN ('locked', 'in_progress') THEN
        RETURN;
    END IF;
    IF v_match.forensic_hash_a IS NULL AND v_match.forensic_hash_b IS NULL THEN
        RETURN;
    END IF;

    v_window := GREATEST(1, COALESCE(v_match.result_window_seconds, 45));
    IF v_match.forensic_hash_a IS NOT NULL AND v_match.forensic_hash_b IS NOT NULL THEN
        IF v_match.forensic_hash_a = v_match.forensic_hash_b
           AND v_match.winner_claim_a IS NOT NULL
           AND v_match.winner_claim_a = v_match.winner_claim_b
           AND v_match.winner_claim_a IN (-2, 0, 1) THEN
            UPDATE public.pvp_matches
            SET state = 'completed',
                winner_seat = public.pvp_winner_seat(winner_claim_a),
                turn_deadline_at = NULL,
                result_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = p_match_id;
        ELSE
            UPDATE public.pvp_matches
            SET state = 'disputed',
                winner_seat = NULL,
                turn_deadline_at = NULL,
                result_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = p_match_id;
        END IF;
        RETURN;
    END IF;

    IF v_match.result_deadline_at IS NULL THEN
        UPDATE public.pvp_matches
        SET result_deadline_at = pg_catalog.now() + make_interval(secs => v_window),
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id;
        RETURN;
    END IF;

    IF v_match.result_deadline_at <= pg_catalog.now() THEN
        UPDATE public.pvp_matches
        SET state = 'disputed',
            winner_seat = NULL,
            turn_deadline_at = NULL,
            result_deadline_at = NULL,
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id;
    END IF;
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
    FOR v_match IN
        SELECT *
        FROM public.pvp_matches
        WHERE state IN ('locked', 'in_progress')
          AND (
            (p_match_id IS NOT NULL AND match_id = p_match_id)
            OR (
              p_match_id IS NULL
              AND (
                (turn_deadline_at IS NOT NULL AND turn_deadline_at <= pg_catalog.now())
                OR forensic_hash_a IS NOT NULL
                OR forensic_hash_b IS NOT NULL
              )
            )
          )
        FOR UPDATE
    LOOP
        IF v_match.forensic_hash_a IS NOT NULL OR v_match.forensic_hash_b IS NOT NULL THEN
            PERFORM public.pvp_apply_result_window(v_match.match_id);
            CONTINUE;
        END IF;

        IF v_match.turn_deadline_at IS NULL OR v_match.turn_deadline_at > pg_catalog.now() THEN
            CONTINUE;
        END IF;

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
                winner_seat = NULL,
                consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        ELSIF v_miss_a >= v_limit THEN
            UPDATE public.pvp_matches
            SET state = 'forfeit_a',
                winner_seat = 'b',
                consecutive_misses_a = v_miss_a,
                consecutive_misses_b = v_miss_b,
                turn_deadline_at = NULL,
                updated_at = pg_catalog.now()
            WHERE match_id = v_match.match_id;
        ELSIF v_miss_b >= v_limit THEN
            UPDATE public.pvp_matches
            SET state = 'forfeit_b',
                winner_seat = 'a',
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

REVOKE ALL ON FUNCTION public.pvp_party_problem(jsonb) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_winner_seat(integer) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_apply_result_window(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_sweep_timeouts_service(uuid) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.pvp_party_problem(jsonb) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_winner_seat(integer) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_apply_result_window(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_sweep_timeouts_service(uuid) TO service_role;
