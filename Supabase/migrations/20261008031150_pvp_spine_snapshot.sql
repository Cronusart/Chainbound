CREATE OR REPLACE FUNCTION public.pvp_snapshot_service(p_profile_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_match public.pvp_matches%ROWTYPE;
    v_queue public.matchmaking_queue%ROWTYPE;
    v_open_id uuid;
    v_seat text := '';
    v_committed integer := 0;
    v_actions_a text := '';
    v_actions_b text := '';
    v_turns text := '';
    v_found boolean := false;
BEGIN
    SELECT open_matches.match_id INTO v_open_id
    FROM (
        (
            SELECT match_id, created_at
            FROM public.pvp_matches
            WHERE player_a = p_profile_id
              AND state IN ('created', 'locked', 'in_progress')
            ORDER BY created_at DESC
            LIMIT 1
        )
        UNION ALL
        (
            SELECT match_id, created_at
            FROM public.pvp_matches
            WHERE player_b = p_profile_id
              AND state IN ('created', 'locked', 'in_progress')
            ORDER BY created_at DESC
            LIMIT 1
        )
    ) AS open_matches
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_open_id IS NOT NULL THEN
        PERFORM public.pvp_sweep_timeouts_service(v_open_id);
        SELECT * INTO v_match
        FROM public.pvp_matches
        WHERE match_id = v_open_id;
        v_found := FOUND;
    END IF;

    IF NOT v_found THEN
        SELECT * INTO v_queue
        FROM public.matchmaking_queue
        WHERE profile_id = p_profile_id
          AND status IN ('waiting', 'expired')
        ORDER BY enqueued_at DESC
        LIMIT 1;
        IF FOUND AND (v_queue.status = 'expired' OR v_queue.expires_at <= pg_catalog.now()) THEN
            UPDATE public.matchmaking_queue
            SET status = 'expired'
            WHERE queue_id = v_queue.queue_id
              AND status = 'waiting';
            RETURN jsonb_build_object(
                'success', true,
                'error', '',
                'phase', 'expired',
                'match_id', '',
                'seat', '',
                'seed', 0,
                'ruleset_id', v_queue.ruleset_id,
                'state', 'expired',
                'turn_index', 0,
                'party_a_json', '',
                'party_b_json', '',
                'committed_turn_index', 0,
                'actions_a_json', '',
                'actions_b_json', '',
                'turns_json', '',
                'forensic_hash_a', '',
                'forensic_hash_b', '',
                'winner_seat', '',
                'hp_policy', '',
                'player_a', '',
                'player_b', '',
                'paired', false
            );
        END IF;
        IF FOUND AND v_queue.status = 'waiting' AND v_queue.expires_at > pg_catalog.now() THEN
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
                'turns_json', '',
                'forensic_hash_a', '',
                'forensic_hash_b', '',
                'winner_seat', '',
                'hp_policy', '',
                'player_a', '',
                'player_b', '',
                'paired', false
            );
        END IF;

        SELECT latest.* INTO v_match
        FROM (
            (
                SELECT *
                FROM public.pvp_matches
                WHERE player_a = p_profile_id
                ORDER BY created_at DESC
                LIMIT 1
            )
            UNION ALL
            (
                SELECT *
                FROM public.pvp_matches
                WHERE player_b = p_profile_id
                ORDER BY created_at DESC
                LIMIT 1
            )
        ) AS latest
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
            'turns_json', '',
            'forensic_hash_a', '',
            'forensic_hash_b', '',
            'winner_seat', '',
            'hp_policy', '',
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

    SELECT COALESCE(jsonb_build_object(
        'turns',
        COALESCE((
            SELECT jsonb_agg(jsonb_build_object(
                'turn_index', t.turn_index,
                'seat', t.seat,
                'actions', COALESCE(t.action_json -> 'actions', '[]'::jsonb)
            ) ORDER BY t.turn_index, t.seat)
            FROM public.pvp_match_turns t
            WHERE t.match_id = v_match.match_id
              AND t.committed
        ), '[]'::jsonb)
    )::text, '') INTO v_turns;

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
        'turns_json', COALESCE(v_turns, ''),
        'forensic_hash_a', COALESCE(v_match.forensic_hash_a, ''),
        'forensic_hash_b', COALESCE(v_match.forensic_hash_b, ''),
        'winner_seat', COALESCE(v_match.winner_seat, ''),
        'hp_policy', COALESCE(v_match.hp_policy, 'full_heal'),
        'player_a', v_match.player_a::text,
        'player_b', v_match.player_b::text,
        'paired', true
    );
END;
$$;

REVOKE ALL ON FUNCTION public.pvp_snapshot_service(uuid) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.pvp_snapshot_service(uuid) TO service_role;
