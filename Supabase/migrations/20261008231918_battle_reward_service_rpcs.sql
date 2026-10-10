-- Service-only authority and classification routines for Postgres-canonical
-- RAM v2.  Positive credit settlement is added after ram_daily_earnings in the
-- following migration; there is deliberately no Solana reward saga.

CREATE OR REPLACE FUNCTION public.battle_reward_get_service(p_battle_id UUID)
RETURNS JSONB
LANGUAGE sql
SECURITY DEFINER
SET search_path = ''
STABLE
AS $$
    SELECT CASE WHEN s.settlement_id IS NULL THEN NULL ELSE jsonb_build_object(
        'status', s.status,
        'battleId', s.battle_id,
        'settlementId', s.settlement_id,
        'requestedRam', s.requested_amount,
        'appliedRam', s.applied_amount,
        'reason', s.reason,
        'rewardTier', s.reward_tier,
        'rewardConfigVersion', s.reward_config_version,
        'rewardConfigHash', s.reward_config_hash,
        'dailyEarned', s.daily_earned,
        'dailyCap', s.daily_cap,
        'balanceAfter', s.balance_after,
        'alreadyApplied', true
    ) END
      FROM public.battle_reward_settlements AS s
     WHERE s.battle_id = p_battle_id;
$$;

CREATE OR REPLACE FUNCTION public.authoritative_begin_battle_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_start_command_id TEXT,
    p_battle_kind TEXT,
    p_seed BIGINT,
    p_manifest_hash TEXT,
    p_config_hash TEXT,
    p_encounter_id TEXT,
    p_reward_tier TEXT,
    p_reward_config_version INTEGER,
    p_reward_config_hash TEXT,
    p_state_json JSONB,
    p_equipment_snapshot JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_existing public.authoritative_pve_battles%ROWTYPE;
BEGIN
    IF NULLIF(btrim(p_start_command_id), '') IS NULL
       OR NULLIF(btrim(p_encounter_id), '') IS NULL
       OR p_reward_tier NOT IN ('none', 'standard', 'elite', 'boss') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_battle_authority_stamp');
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.battle_reward_configs
         WHERE config_version = p_reward_config_version
           AND config_hash = p_reward_config_hash
           AND enabled
    ) THEN
        RETURN jsonb_build_object('ok', false, 'code', 'reward_config_not_allowed');
    END IF;

    PERFORM 1 FROM public.player_profiles
     WHERE player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found'); END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended(p_profile_id::TEXT, 0));
    SELECT * INTO v_existing FROM public.authoritative_pve_battles
     WHERE player_profile_id = p_profile_id
       AND status IN ('active', 'capturing', 'service_interrupted')
     FOR UPDATE;
    IF FOUND THEN
        IF v_existing.start_command_id = p_start_command_id THEN
            IF v_existing.encounter_id IS DISTINCT FROM p_encounter_id
               OR v_existing.reward_tier IS DISTINCT FROM p_reward_tier
               OR v_existing.reward_config_version IS DISTINCT FROM p_reward_config_version
               OR v_existing.reward_config_hash IS DISTINCT FROM p_reward_config_hash THEN
                RETURN jsonb_build_object('ok', false, 'code', 'battle_authority_stamp_conflict');
            END IF;
            RETURN jsonb_build_object(
                'ok', true, 'already_applied', true,
                'battle_id', v_existing.battle_id,
                'state_version', v_existing.state_version
            );
        END IF;
        RETURN jsonb_build_object(
            'ok', false, 'code', 'battle_already_active',
            'battle_id', v_existing.battle_id,
            'state_version', v_existing.state_version
        );
    END IF;

    INSERT INTO public.authoritative_pve_battles (
        battle_id, player_profile_id, start_command_id, status, battle_kind,
        state_version, seed, manifest_hash, config_hash, encounter_id,
        reward_tier, reward_config_version, reward_config_hash,
        state_json, equipment_snapshot, expires_at
    ) VALUES (
        p_battle_id, p_profile_id, p_start_command_id, 'active', p_battle_kind,
        0, p_seed, p_manifest_hash, p_config_hash, p_encounter_id,
        p_reward_tier, p_reward_config_version, p_reward_config_hash,
        COALESCE(p_state_json, '{}'::jsonb), COALESCE(p_equipment_snapshot, '{}'::jsonb),
        to_timestamp(p_expires_at_unix_milliseconds / 1000.0)
    );
    RETURN jsonb_build_object('ok', true, 'battle_id', p_battle_id, 'state_version', 0);
END;
$$;

CREATE OR REPLACE FUNCTION public.battle_reward_stamp_terminal_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_terminal_outcome TEXT,
    p_terminal_reason TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE v_battle public.authoritative_pve_battles%ROWTYPE;
BEGIN
    IF p_terminal_outcome NOT IN ('victory', 'capture', 'defeat', 'draw', 'flee', 'forfeit', 'abandon')
       OR NULLIF(btrim(p_terminal_reason), '') IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_terminal_outcome');
    END IF;
    PERFORM 1 FROM public.player_profiles
     WHERE player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found'); END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found'); END IF;
    IF v_battle.status NOT IN ('completed', 'lost', 'abandoned') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_terminal');
    END IF;
    IF (p_terminal_outcome = 'victory' AND v_battle.status <> 'completed')
       OR (p_terminal_outcome IN ('forfeit', 'abandon') AND v_battle.status <> 'abandoned') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'terminal_status_mismatch');
    END IF;
    IF v_battle.terminal_outcome IS NOT NULL THEN
        IF v_battle.terminal_outcome IS DISTINCT FROM p_terminal_outcome THEN
            RETURN jsonb_build_object('ok', false, 'code', 'terminal_outcome_conflict');
        END IF;
        RETURN jsonb_build_object('ok', true, 'already_applied', true);
    END IF;
    UPDATE public.authoritative_pve_battles
       SET terminal_outcome = p_terminal_outcome,
           terminal_reason = p_terminal_reason,
           terminal_at = now(),
           updated_at = now()
     WHERE battle_id = p_battle_id;
    RETURN jsonb_build_object('ok', true, 'already_applied', false);
END;
$$;

CREATE OR REPLACE FUNCTION public.battle_reward_classify_service(
    p_battle_id UUID,
    p_gate_reason TEXT DEFAULT NULL
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_battle public.authoritative_pve_battles%ROWTYPE;
    v_existing public.battle_reward_settlements%ROWTYPE;
    v_config public.battle_reward_configs%ROWTYPE;
    v_requested BIGINT;
    v_balance BIGINT;
    v_reason TEXT;
BEGIN
    IF p_gate_reason IS NOT NULL
       AND p_gate_reason NOT IN ('rewards_disabled', 'shadow_mode', 'not_allowlisted', 'plausibility_rejected') THEN
        RAISE EXCEPTION 'invalid_reward_gate_reason';
    END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id;
    IF NOT FOUND THEN RAISE EXCEPTION 'battle_not_found'; END IF;
    SELECT ram_balance INTO v_balance FROM public.player_profiles
     WHERE player_profile_id = v_battle.player_profile_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'profile_not_found'; END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id FOR UPDATE;
    SELECT * INTO v_existing FROM public.battle_reward_settlements
     WHERE battle_id = p_battle_id FOR UPDATE;
    IF FOUND THEN RETURN public.battle_reward_get_service(p_battle_id); END IF;
    IF v_battle.status NOT IN ('completed', 'lost', 'abandoned')
       OR v_battle.terminal_outcome IS NULL THEN RAISE EXCEPTION 'battle_not_terminal'; END IF;

    SELECT * INTO v_config FROM public.battle_reward_configs
     WHERE config_version = v_battle.reward_config_version
       AND config_hash = v_battle.reward_config_hash AND enabled;
    IF NOT FOUND THEN RAISE EXCEPTION 'reward_config_not_allowed'; END IF;
    v_requested := CASE v_battle.reward_tier
        WHEN 'standard' THEN v_config.standard_amount
        WHEN 'elite' THEN v_config.elite_amount
        WHEN 'boss' THEN v_config.boss_amount
        ELSE NULL
    END;
    IF v_requested IS NULL THEN RAISE EXCEPTION 'reward_tier_not_allowed'; END IF;
    v_reason := COALESCE(
        p_gate_reason,
        CASE WHEN v_battle.terminal_outcome <> 'victory' THEN v_battle.terminal_outcome END
    );
    IF v_reason IS NULL THEN
        RETURN jsonb_build_object(
            'status', 'eligible', 'battleId', p_battle_id,
            'requestedRam', v_requested, 'appliedRam', 0,
            'reason', 'eligible', 'rewardTier', v_battle.reward_tier,
            'rewardConfigVersion', v_battle.reward_config_version,
            'rewardConfigHash', v_battle.reward_config_hash,
            'balanceAfter', v_balance, 'alreadyApplied', false
        );
    END IF;

    INSERT INTO public.battle_reward_settlements(
        battle_id, player_profile_id, terminal_outcome, reward_tier,
        reward_config_version, reward_config_hash, requested_amount,
        applied_amount, status, reason, balance_after, settled_at
    ) VALUES (
        p_battle_id, v_battle.player_profile_id, v_battle.terminal_outcome,
        v_battle.reward_tier, v_battle.reward_config_version, v_battle.reward_config_hash,
        v_requested, 0, 'zero', v_reason, v_balance, now()
    );
    RETURN public.battle_reward_get_service(p_battle_id);
END;
$$;

-- Action-step terminal stamping is kept in the same transaction that first
-- changes the battle to completed/lost.  The old overload remains available
-- during rolling deployment but cannot produce a reward without a stamp.
CREATE OR REPLACE FUNCTION public.authoritative_commit_action_step_service(
    p_battle_id UUID,
    p_profile_id UUID,
    p_command_id TEXT,
    p_expected_version INTEGER,
    p_new_state_version INTEGER,
    p_new_status TEXT,
    p_new_state_json JSONB,
    p_request_json JSONB,
    p_expires_at_unix_milliseconds BIGINT,
    p_terminal_outcome TEXT,
    p_terminal_reason TEXT
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
    IF NULLIF(btrim(p_command_id), '') IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'code', 'command_id_required');
    END IF;
    IF p_new_status NOT IN ('active', 'completed', 'lost') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_battle_status');
    END IF;
    IF (p_new_status = 'active' AND p_terminal_outcome IS NOT NULL)
       OR (p_new_status <> 'active' AND (
            p_terminal_outcome NOT IN ('victory', 'defeat', 'draw', 'flee')
            OR NULLIF(btrim(p_terminal_reason), '') IS NULL
       ))
       OR (p_terminal_outcome = 'victory' AND p_new_status <> 'completed') THEN
        RETURN jsonb_build_object('ok', false, 'code', 'invalid_terminal_transition');
    END IF;

    PERFORM 1 FROM public.player_profiles
     WHERE player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found'); END IF;
    SELECT * INTO v_battle FROM public.authoritative_pve_battles
     WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id FOR UPDATE;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'battle_not_found'); END IF;
    SELECT result_json INTO v_existing FROM public.authoritative_pve_commands
     WHERE battle_id = p_battle_id AND command_id = p_command_id;
    IF FOUND THEN RETURN v_existing || jsonb_build_object('already_applied', true); END IF;
    IF v_battle.status <> 'active' OR COALESCE(v_battle.state_json->>'phase', '') = 'capturing' THEN
        RETURN jsonb_build_object('ok', false, 'code', 'battle_not_ready');
    END IF;
    IF v_battle.state_version <> p_expected_version
       OR p_new_state_version <> p_expected_version + 1 THEN
        RETURN jsonb_build_object('ok', false, 'code', 'state_version_conflict', 'state_version', v_battle.state_version);
    END IF;
    v_result := jsonb_build_object(
        'ok', true, 'battle_id', p_battle_id,
        'state_version', p_new_state_version, 'already_applied', false
    );
    UPDATE public.authoritative_pve_battles
       SET state_version = p_new_state_version,
           status = p_new_status,
           state_json = COALESCE(p_new_state_json, '{}'::jsonb),
           expires_at = to_timestamp(p_expires_at_unix_milliseconds / 1000.0),
           checkpointed_at = now(),
           completed_at = CASE WHEN p_new_status IN ('completed', 'lost') THEN now() ELSE NULL END,
           terminal_outcome = CASE WHEN p_new_status = 'active' THEN NULL ELSE p_terminal_outcome END,
           terminal_reason = CASE WHEN p_new_status = 'active' THEN NULL ELSE p_terminal_reason END,
           terminal_at = CASE WHEN p_new_status = 'active' THEN NULL ELSE now() END,
           updated_at = now()
     WHERE battle_id = p_battle_id AND player_profile_id = p_profile_id
       AND state_version = p_expected_version;
    IF NOT FOUND THEN RETURN jsonb_build_object('ok', false, 'code', 'state_version_conflict'); END IF;
    INSERT INTO public.authoritative_pve_commands(
        battle_id, command_id, player_profile_id, expected_version,
        result_version, command_kind, request_json, result_json
    ) VALUES (
        p_battle_id, p_command_id, p_profile_id, p_expected_version,
        p_new_state_version, 'action_step', COALESCE(p_request_json, '{}'::jsonb), v_result
    );
    RETURN v_result;
END;
$$;

REVOKE ALL ON FUNCTION public.battle_reward_get_service(UUID) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_begin_battle_service(UUID, UUID, TEXT, TEXT, BIGINT, TEXT, TEXT, TEXT, TEXT, INTEGER, TEXT, JSONB, JSONB, BIGINT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.battle_reward_stamp_terminal_service(UUID, UUID, TEXT, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.battle_reward_classify_service(UUID, TEXT) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_commit_action_step_service(UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT, TEXT, TEXT) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.battle_reward_get_service(UUID) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_begin_battle_service(UUID, UUID, TEXT, TEXT, BIGINT, TEXT, TEXT, TEXT, TEXT, INTEGER, TEXT, JSONB, JSONB, BIGINT) TO service_role;
GRANT EXECUTE ON FUNCTION public.battle_reward_stamp_terminal_service(UUID, UUID, TEXT, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.battle_reward_classify_service(UUID, TEXT) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_commit_action_step_service(UUID, UUID, TEXT, INTEGER, INTEGER, TEXT, JSONB, JSONB, BIGINT, TEXT, TEXT) TO service_role;

COMMENT ON FUNCTION public.battle_reward_classify_service(UUID, TEXT) IS
    'Creates immutable zero receipts for gated/non-victory outcomes or returns an eligible server-owned victory without accepting an amount.';
