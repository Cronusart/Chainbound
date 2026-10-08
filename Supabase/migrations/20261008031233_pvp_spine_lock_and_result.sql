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
    v_problem text;
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

    v_problem := public.pvp_party_problem(p_party_a);
    IF v_problem IS NULL THEN
        v_problem := public.pvp_party_problem(p_party_b);
    END IF;
    IF v_problem IS NOT NULL THEN
        UPDATE public.pvp_matches
        SET state = 'cancelled', winner_seat = NULL, updated_at = pg_catalog.now(), turn_deadline_at = NULL
        WHERE match_id = p_match_id
          AND state = 'created';
        v_snapshot := public.pvp_snapshot_service(p_actor_id);
        RETURN v_snapshot || jsonb_build_object('success', false, 'error', v_problem);
    END IF;

    UPDATE public.pvp_matches
    SET party_a = p_party_a,
        party_b = p_party_b,
        hp_policy = 'full_heal',
        state = 'locked',
        turn_index = 1,
        turn_deadline_at = pg_catalog.now() + make_interval(secs => turn_limit_seconds),
        lock_deadline_at = NULL,
        updated_at = pg_catalog.now()
    WHERE match_id = p_match_id
      AND state = 'created';

    RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
END;
$$;

DROP FUNCTION IF EXISTS public.pvp_submit_result_service(uuid, uuid, text);

CREATE OR REPLACE FUNCTION public.pvp_submit_result_service(
    p_match_id uuid,
    p_actor_id uuid,
    p_forensic_hash text,
    p_winner_team integer
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
BEGIN
    IF v_hash IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'hash_required', 'phase', 'idle', 'paired', false);
    END IF;
    IF p_winner_team IS NULL OR p_winner_team NOT IN (-2, 0, 1) THEN
        RETURN jsonb_build_object('success', false, 'error', 'winner_illegal', 'phase', 'idle', 'paired', false);
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
        IF v_match.forensic_hash_a IS NOT NULL AND v_match.forensic_hash_a IS DISTINCT FROM v_hash THEN
            RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'hash_conflict');
        END IF;
        IF v_match.winner_claim_a IS NOT NULL AND v_match.winner_claim_a IS DISTINCT FROM p_winner_team THEN
            RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'hash_conflict');
        END IF;
        UPDATE public.pvp_matches
        SET forensic_hash_a = v_hash,
            winner_claim_a = p_winner_team,
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id;
    ELSE
        IF v_match.forensic_hash_b IS NOT NULL AND v_match.forensic_hash_b IS DISTINCT FROM v_hash THEN
            RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'hash_conflict');
        END IF;
        IF v_match.winner_claim_b IS NOT NULL AND v_match.winner_claim_b IS DISTINCT FROM p_winner_team THEN
            RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', false, 'error', 'hash_conflict');
        END IF;
        UPDATE public.pvp_matches
        SET forensic_hash_b = v_hash,
            winner_claim_b = p_winner_team,
            updated_at = pg_catalog.now()
        WHERE match_id = p_match_id;
    END IF;

    PERFORM public.pvp_apply_result_window(p_match_id);
    RETURN public.pvp_snapshot_service(p_actor_id) || jsonb_build_object('success', true, 'error', '');
END;
$$;

REVOKE ALL ON FUNCTION public.pvp_lock_parties_service(uuid, uuid, jsonb, jsonb) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.pvp_submit_result_service(uuid, uuid, text, integer) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.pvp_lock_parties_service(uuid, uuid, jsonb, jsonb) TO service_role;
GRANT EXECUTE ON FUNCTION public.pvp_submit_result_service(uuid, uuid, text, integer) TO service_role;

DO $cron$
DECLARE
    v_jobid bigint;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        FOR v_jobid IN SELECT jobid FROM cron.job WHERE jobname = 'pvp_matchmaker' LOOP
            PERFORM cron.unschedule(v_jobid);
        END LOOP;
        BEGIN
            PERFORM cron.schedule('pvp_matchmaker', '10 seconds', 'SELECT public.pvp_matchmaker_service()');
        EXCEPTION WHEN OTHERS THEN
            PERFORM cron.schedule('pvp_matchmaker', '* * * * *', 'SELECT public.pvp_matchmaker_service()');
        END;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'pvp_matchmaker cron skipped: %', SQLERRM;
END
$cron$;
