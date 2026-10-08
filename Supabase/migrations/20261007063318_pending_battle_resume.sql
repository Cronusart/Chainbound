-- Pending PvE battle resume: five-minute client window support, shared
-- timeout/forfeit settlement, and idempotent pre-presentation action checkpoints.

CREATE OR REPLACE FUNCTION public.authoritative_settle_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT,
    p_ram_penalty INTEGER,
    p_respawn_hp_basis_points INTEGER,
    p_end_reason TEXT,
    p_require_expired BOOLEAN
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_authority_time TIMESTAMPTZ := to_timestamp(p_authority_unix_milliseconds / 1000.0);
    v_respawn_bp INTEGER := LEAST(10000, GREATEST(0, COALESCE(p_respawn_hp_basis_points, 5000)));
    v_end_reason TEXT := CASE WHEN p_end_reason = 'Forfeited' THEN 'Forfeited' ELSE 'Abandoned' END;
    v_recovered_count INTEGER := 0;
    v_state_version INTEGER;
    v_economy JSONB := '{}'::jsonb;
BEGIN
    IF p_authority_unix_milliseconds IS NULL
       OR v_authority_time < now() - INTERVAL '1 hour'
       OR v_authority_time > now() + INTERVAL '5 minutes' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_authoritative_time');
    END IF;

    -- All battle mutation paths serialize profile first, then battle. This
    -- matches capture begin/finalize and prevents cross-path deadlocks.
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
            'state_version', v_battle.state_version,
            'end_reason', COALESCE(v_battle.state_json->>'endReason', v_end_reason),
            'recovered_chainlings', 0
        );
    END IF;
    IF v_battle.status NOT IN ('active', 'capturing', 'service_interrupted') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_already_finalized');
    END IF;
    IF COALESCE(p_require_expired, false) AND v_battle.expires_at > v_authority_time THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_expired');
    END IF;

    UPDATE public.profile_capture_events
    SET outcome = 'interrupted',
        finalized_at = COALESCE(finalized_at, now()),
        result_json = COALESCE(result_json, '{}'::jsonb) || jsonb_build_object(
            'success', false,
            'outcome', 'interrupted',
            'reason', CASE WHEN v_end_reason = 'Forfeited' THEN 'battle_forfeited' ELSE 'battle_abandoned' END
        )
    WHERE battle_id = p_battle_id AND outcome = 'pending';

    UPDATE public.authoritative_pve_battles
    SET status = 'abandoned',
        state_version = state_version + 1,
        state_json = jsonb_set(
            jsonb_set(COALESCE(state_json, '{}'::jsonb), '{phase}', '"complete"'::jsonb, true),
            '{endReason}', to_jsonb(v_end_reason), true
        ),
        checkpointed_at = now(),
        completed_at = now(),
        updated_at = now()
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    RETURNING state_version INTO v_state_version;

    IF v_respawn_bp > 0 THEN
        WITH source AS (
            SELECT
                c.id,
                v.base_max_hp,
                LEAST(5, GREATEST(0, COALESCE(c.rebirth_count, 0))) AS rebirth_count,
                LEAST(5, GREATEST(0, COALESCE((c.rebirth_iv_bonuses ->> 0)::INTEGER, 0))) AS hp_pick_count,
                LEAST(10, GREATEST(-10, COALESCE((c.ivs ->> 0)::INTEGER, 0))) AS hp_iv
            FROM public.chainlings AS c
            INNER JOIN public.chainling_species_vitals AS v
                ON v.species_id = lower(btrim(c.species_id))
            WHERE c.player_profile_id = p_profile_id
              AND COALESCE(c.is_party_member, false)
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
            UPDATE public.chainlings AS c
            SET current_hp = LEAST(
                    m.max_hp,
                    GREATEST(1, (m.max_hp * v_respawn_bp + 9999) / 10000)
                ),
                hp_synced_at = v_authority_time,
                passive_recovery_at = v_authority_time,
                updated_at = now()
            FROM maxima AS m
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
        'end_reason', v_end_reason,
        'recovered_chainlings', v_recovered_count,
        'economy', v_economy
    );
END;
$$;

-- Keep the legacy timeout RPC as a compatibility wrapper while both callers
-- move to the shared settlement entry point.
CREATE OR REPLACE FUNCTION public.authoritative_expire_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_authority_unix_milliseconds BIGINT,
    p_ram_penalty INTEGER,
    p_respawn_hp_basis_points INTEGER
)
RETURNS JSONB
LANGUAGE sql
SECURITY DEFINER
SET search_path = ''
AS $$
    SELECT public.authoritative_settle_battle_service(
        p_battle_id,
        p_profile_id,
        p_authority_unix_milliseconds,
        p_ram_penalty,
        p_respawn_hp_basis_points,
        'Abandoned',
        true
    );
$$;

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

    SELECT * INTO v_battle
    FROM public.authoritative_pve_battles
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found');
    END IF;

    SELECT result_json INTO v_existing
    FROM public.authoritative_pve_commands
    WHERE battle_id = p_battle_id AND command_id = p_command_id;
    IF FOUND THEN
        RETURN v_existing || jsonb_build_object('already_applied', true);
    END IF;

    IF v_battle.status <> 'active' OR COALESCE(v_battle.state_json->>'phase', '') = 'capturing' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_ready');
    END IF;
    IF v_battle.state_version <> p_expected_version OR p_new_state_version <> p_expected_version + 1 THEN
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
    WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
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

REVOKE ALL ON FUNCTION public.authoritative_settle_battle_service(
    UUID, UUID, BIGINT, INTEGER, INTEGER, TEXT, BOOLEAN
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_settle_battle_service(
    UUID, UUID, BIGINT, INTEGER, INTEGER, TEXT, BOOLEAN
) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) TO service_role;

COMMENT ON FUNCTION public.authoritative_settle_battle_service(
    UUID, UUID, BIGINT, INTEGER, INTEGER, TEXT, BOOLEAN
) IS 'Idempotently settles timeout or forfeit, interrupts capture, applies one PvE loss/RAM cost, and revives every equipped party Chainling.';
COMMENT ON FUNCTION public.authoritative_commit_action_step_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT
) IS 'Idempotently checkpoints client-resolved HP/AP before presentation and extends the five-minute resume window.';
COMMENT ON TABLE public.authoritative_pve_battles IS
    'Resumable authoritative PvE sessions. One open session per profile; every committed action extends a five-minute resume window.';
