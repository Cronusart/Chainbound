-- Keep the immutable terminal reward stamp in the same transaction that first
-- finalizes a capture, forfeit, or abandonment. Action-step finalization uses
-- the matching overload added in 20261008231918.

CREATE OR REPLACE FUNCTION public.authoritative_settle_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT,
    p_ram_penalty INTEGER,
    p_respawn_hp_basis_points INTEGER,
    p_end_reason TEXT,
    p_require_expired BOOLEAN,
    p_terminal_outcome TEXT,
    p_terminal_reason TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_existing_outcome TEXT;
BEGIN
    IF p_terminal_outcome IS NULL OR p_terminal_outcome NOT IN ('forfeit', 'abandon') THEN
        RAISE EXCEPTION 'invalid_terminal_outcome';
    END IF;
    IF NULLIF(btrim(p_terminal_reason), '') IS NULL THEN
        RAISE EXCEPTION 'terminal_reason_required';
    END IF;
    IF (COALESCE(p_end_reason, '') = 'Forfeited') <> (p_terminal_outcome = 'forfeit') THEN
        RAISE EXCEPTION 'terminal_outcome_mismatch';
    END IF;

    v_result := public.authoritative_settle_battle_service(
        p_battle_id,
        p_profile_id,
        p_authority_unix_milliseconds,
        p_ram_penalty,
        p_respawn_hp_basis_points,
        p_end_reason,
        p_require_expired
    );
    IF COALESCE((v_result->>'ok')::BOOLEAN, false) IS NOT TRUE THEN
        RETURN v_result;
    END IF;

    SELECT terminal_outcome INTO v_existing_outcome
      FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;
    IF v_existing_outcome IS NOT NULL AND v_existing_outcome <> p_terminal_outcome THEN
        RAISE EXCEPTION 'terminal_outcome_conflict';
    END IF;

    UPDATE public.authoritative_pve_battles
       SET terminal_outcome = COALESCE(terminal_outcome, p_terminal_outcome),
           terminal_reason = COALESCE(terminal_reason, NULLIF(btrim(p_terminal_reason), ''), p_end_reason),
           terminal_at = COALESCE(terminal_at, now()),
           updated_at = now()
     WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id;
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
    p_new_state_json JSONB,
    p_terminal_outcome TEXT,
    p_terminal_reason TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_result JSONB;
    v_battle_id UUID;
    v_existing_outcome TEXT;
BEGIN
    IF p_terminal_outcome IS NOT NULL AND p_terminal_outcome NOT IN ('capture', 'defeat') THEN
        RAISE EXCEPTION 'invalid_terminal_outcome';
    END IF;
    IF p_terminal_outcome IS NOT NULL AND NULLIF(btrim(p_terminal_reason), '') IS NULL THEN
        RAISE EXCEPTION 'terminal_reason_required';
    END IF;
    IF p_terminal_outcome = 'capture' AND p_outcome <> 'locked' THEN
        RAISE EXCEPTION 'terminal_outcome_mismatch';
    END IF;
    IF p_terminal_outcome = 'defeat' AND p_outcome <> 'escaped' THEN
        RAISE EXCEPTION 'terminal_outcome_mismatch';
    END IF;
    IF (p_new_battle_status IN ('completed', 'lost')) <> (p_terminal_outcome IS NOT NULL) THEN
        RAISE EXCEPTION 'terminal_outcome_required';
    END IF;

    v_result := public.authoritative_finalize_capture_attempt_service(
        p_attempt_id,
        p_profile_id,
        p_outcome,
        p_elapsed_milliseconds,
        p_final_meter_bp,
        p_history,
        p_perfect_lock,
        p_entangled_type,
        p_new_battle_status,
        p_new_state_version,
        p_new_state_json
    );
    IF p_terminal_outcome IS NULL THEN
        RETURN v_result;
    END IF;

    SELECT battle_id INTO v_battle_id
      FROM public.profile_capture_events
     WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'capture_attempt_not_found'; END IF;
    SELECT terminal_outcome INTO v_existing_outcome
      FROM public.authoritative_pve_battles
     WHERE battle_id = v_battle_id AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;
    IF v_existing_outcome IS NOT NULL AND v_existing_outcome <> p_terminal_outcome THEN
        RAISE EXCEPTION 'terminal_outcome_conflict';
    END IF;

    UPDATE public.authoritative_pve_battles
       SET terminal_outcome = COALESCE(terminal_outcome, p_terminal_outcome),
           terminal_reason = COALESCE(terminal_reason, NULLIF(btrim(p_terminal_reason), ''), p_outcome),
           terminal_at = COALESCE(terminal_at, now()),
           updated_at = now()
     WHERE battle_id = v_battle_id AND player_profile_id = p_profile_id;
    RETURN v_result;
END;
$$;

REVOKE ALL ON FUNCTION public.authoritative_settle_battle_service(
    UUID, UUID, BIGINT, INTEGER, INTEGER, TEXT, BOOLEAN, TEXT, TEXT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB, TEXT, TEXT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_settle_battle_service(
    UUID, UUID, BIGINT, INTEGER, INTEGER, TEXT, BOOLEAN, TEXT, TEXT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB, TEXT, TEXT
) TO service_role;
