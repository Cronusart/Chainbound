-- Close a cinematic authoritative battle without a party revive, RAM change, or reward.

CREATE OR REPLACE FUNCTION public.authoritative_dismiss_scripted_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_authority_time TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_state_version INTEGER;
BEGIN
    IF p_authority_unix_milliseconds IS NULL
       OR v_authority_time < now() - INTERVAL '1 hour'
       OR v_authority_time > now() + INTERVAL '5 minutes' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_authoritative_time');
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
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found');
    END IF;

    IF v_battle.status = 'abandoned' THEN
        RETURN jsonb_build_object(
            'ok', true,
            'already_applied', true,
            'state_version', v_battle.state_version
        );
    END IF;
    IF v_battle.status NOT IN ('active', 'capturing', 'service_interrupted') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_already_finalized');
    END IF;

    UPDATE public.profile_capture_events
    SET outcome = 'interrupted',
        finalized_at = COALESCE(finalized_at, now()),
        result_json = COALESCE(result_json, '{}'::jsonb) || jsonb_build_object(
            'success', false,
            'outcome', 'interrupted',
            'reason', 'scripted_dismiss'
        )
    WHERE battle_id = p_battle_id AND outcome = 'pending';

    UPDATE public.authoritative_pve_battles
    SET status = 'abandoned',
        state_version = state_version + 1,
        state_json = jsonb_set(
            jsonb_set(COALESCE(state_json, '{}'::jsonb), '{phase}', '"complete"'::jsonb, true),
            '{endReason}', '"ScriptedDismiss"'::jsonb, true
        ),
        checkpointed_at = now(),
        completed_at = now(),
        updated_at = now()
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    RETURNING state_version INTO v_state_version;

    RETURN jsonb_build_object(
        'ok', true,
        'already_applied', false,
        'state_version', v_state_version,
        'end_reason', 'ScriptedDismiss'
    );
END;
$$;

REVOKE ALL ON FUNCTION public.authoritative_dismiss_scripted_battle_service(UUID, UUID, BIGINT)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_dismiss_scripted_battle_service(UUID, UUID, BIGINT)
    TO service_role;

COMMENT ON FUNCTION public.authoritative_dismiss_scripted_battle_service(UUID, UUID, BIGINT) IS
    'Closes a scripted cinematic battle. Does not change party HP, RAM, or rewards.';
