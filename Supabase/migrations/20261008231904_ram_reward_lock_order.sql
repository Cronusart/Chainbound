-- Establish the lock-order contract required by battle rewards before any
-- reward table exists.  Reward routines added by later migrations lock:
-- player_profiles -> authoritative_pve_battles ->
-- battle_reward_settlements -> ram_daily_earnings -> ram_ledger_operations.
--
-- The action checkpoint previously locked the battle first.  It does not yet
-- touch the ledger, but normalizing it now prevents a future battle->profile
-- inversion when terminal settlement hooks are added.

CREATE OR REPLACE FUNCTION public.authoritative_commit_action_step_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_command_id TEXT,
    p_expected_version INTEGER,
    p_new_state_version INTEGER,
    p_new_status TEXT,
    p_new_state_json JSONB,
    p_request_json JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_existing JSONB;
    v_result JSONB;
BEGIN
    IF length(btrim(COALESCE(p_command_id, ''))) = 0 THEN
        RETURN jsonb_build_object('ok', false, 'code', 'command_id_required');
    END IF;
    IF p_new_status NOT IN ('active', 'completed', 'lost') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_battle_status');
    END IF;

    PERFORM 1
      FROM public.player_profiles
     WHERE player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found');
    END IF;

    SELECT * INTO v_battle
      FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id
       AND player_profile_id = p_profile_id
     FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found');
    END IF;

    SELECT result_json INTO v_existing
      FROM public.authoritative_pve_commands
     WHERE battle_id = p_battle_id
       AND command_id = p_command_id;
    IF FOUND THEN
        RETURN v_existing || jsonb_build_object('already_applied', true);
    END IF;

    IF v_battle.status <> 'active'
       OR COALESCE(v_battle.state_json->>'phase', '') = 'capturing' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_ready');
    END IF;
    IF v_battle.state_version <> p_expected_version
       OR p_new_state_version <> p_expected_version + 1 THEN
        RETURN jsonb_build_object(
            'ok', false,
            'code', 'state_version_conflict',
            'state_version', v_battle.state_version
        );
    END IF;

    v_result := jsonb_build_object(
        'ok', true,
        'battle_id', p_battle_id,
        'state_version', p_new_state_version,
        'already_applied', false
    );

    UPDATE public.authoritative_pve_battles
       SET state_version = p_new_state_version,
           status = p_new_status,
           state_json = COALESCE(p_new_state_json, '{}'::jsonb),
           expires_at = to_timestamp(p_expires_at_unix_milliseconds / 1000.0),
           checkpointed_at = now(),
           completed_at = CASE WHEN p_new_status IN ('completed', 'lost') THEN now() ELSE NULL END,
           updated_at = now()
     WHERE battle_id = p_battle_id
       AND player_profile_id = p_profile_id
       AND state_version = p_expected_version;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'state_version_conflict');
    END IF;

    INSERT INTO public.authoritative_pve_commands (
        battle_id, command_id, player_profile_id, expected_version,
        result_version, command_kind, request_json, result_json
    ) VALUES (
        p_battle_id, p_command_id, p_profile_id, p_expected_version,
        p_new_state_version, 'action_step', COALESCE(p_request_json, '{}'::jsonb), v_result
    );

    RETURN v_result;
END;
$$;

REVOKE ALL ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) TO service_role;

COMMENT ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) IS 'Idempotently checkpoints a PvE action. Locks profile before battle to preserve the cross-domain RAM settlement lock order.';
