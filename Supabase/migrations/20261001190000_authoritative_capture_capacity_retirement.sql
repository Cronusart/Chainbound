-- Chainbound - authoritative capture capacity and legacy-path retirement.
--
-- A profile-level row lock is the serialization boundary for all authoritative
-- capture attempts. The only production creator of a captured Chainling is the
-- authoritative battle finalizer. Existing rows and historical migrations stay
-- intact for audit/test-data continuity.

CREATE OR REPLACE FUNCTION public.chainling_profile_capacity()
RETURNS INTEGER
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
AS $$
    SELECT 25;
$$;

CREATE OR REPLACE FUNCTION public.chainling_party_capacity()
RETURNS INTEGER
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
AS $$
    SELECT 3;
$$;

CREATE OR REPLACE FUNCTION public.profile_chainling_capacity_summary_service(
    p_profile_id UUID
)
RETURNS JSONB
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = public
AS $$
    WITH counts AS (
        SELECT
            COUNT(*)::INTEGER AS owned_count,
            COUNT(*) FILTER (WHERE COALESCE(is_party_member, false))::INTEGER AS party_count
        FROM public.chainlings
        WHERE player_profile_id = p_profile_id
    )
    SELECT jsonb_build_object(
        'owned_count', counts.owned_count,
        'owned_capacity', public.chainling_profile_capacity(),
        'party_count', counts.party_count,
        'party_capacity', public.chainling_party_capacity(),
        'storage_count', GREATEST(0, counts.owned_count - counts.party_count),
        'storage_capacity', public.chainling_profile_capacity() - public.chainling_party_capacity(),
        'can_capture', counts.owned_count < public.chainling_profile_capacity()
    )
    FROM counts;
$$;

-- Preserve the original implementation as a private callee, then put the
-- profile lock and capacity invariant at the public service boundary.
ALTER FUNCTION public.authoritative_begin_capture_attempt_service(
    UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT
) RENAME TO authoritative_begin_capture_attempt_service_impl;

CREATE OR REPLACE FUNCTION public.authoritative_begin_capture_attempt_service(
    p_attempt_id UUID,
    p_battle_id UUID,
    p_profile_id UUID,
    p_command_id TEXT,
    p_expected_version INTEGER,
    p_orb_id TEXT,
    p_capture_profile_id TEXT,
    p_battle_config_hash TEXT,
    p_attempt_config_hash TEXT,
    p_target_snapshot JSONB,
    p_low_hp_threshold_bp INTEGER,
    p_new_state_json JSONB,
    p_expires_at_unix_milliseconds BIGINT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_existing JSONB;
    v_owned_count INTEGER;
BEGIN
    -- One lock order for begin/finalize: profile, then battle inside the impl.
    PERFORM 1 FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('ok', false, 'code', 'profile_not_found');
    END IF;

    -- Idempotency must win over a later full-roster check on a retry.
    SELECT result_json INTO v_existing
    FROM public.authoritative_pve_commands
    WHERE battle_id = p_battle_id AND command_id = p_command_id;
    IF FOUND THEN
        RETURN v_existing || jsonb_build_object('already_applied', true);
    END IF;

    SELECT COUNT(*)::INTEGER INTO v_owned_count
    FROM public.chainlings
    WHERE player_profile_id = p_profile_id;
    IF v_owned_count >= public.chainling_profile_capacity() THEN
        RETURN jsonb_build_object(
            'ok', false,
            'code', 'chainling_storage_full',
            'owned_count', v_owned_count,
            'owned_capacity', public.chainling_profile_capacity()
        );
    END IF;

    RETURN public.authoritative_begin_capture_attempt_service_impl(
        p_attempt_id, p_battle_id, p_profile_id, p_command_id, p_expected_version,
        p_orb_id, p_capture_profile_id, p_battle_config_hash, p_attempt_config_hash,
        p_target_snapshot, p_low_hp_threshold_bp, p_new_state_json,
        p_expires_at_unix_milliseconds
    );
END;
$$;

ALTER FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) RENAME TO authoritative_finalize_capture_attempt_service_impl;

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
    p_new_state_json JSONB
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_existing_outcome TEXT;
    v_existing_result JSONB;
    v_owned_count INTEGER;
    v_result JSONB;
    v_chainling_id UUID;
    v_move_item_id UUID;
BEGIN
    PERFORM 1 FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    -- Retried finalization returns its original grant before testing capacity.
    SELECT outcome, result_json
    INTO v_existing_outcome, v_existing_result
    FROM public.profile_capture_events
    WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id;
    IF FOUND AND v_existing_outcome <> 'pending' THEN
        RETURN COALESCE(v_existing_result, '{}'::jsonb) || jsonb_build_object('already_applied', true);
    END IF;

    IF p_outcome = 'locked' THEN
        SELECT COUNT(*)::INTEGER INTO v_owned_count
        FROM public.chainlings
        WHERE player_profile_id = p_profile_id;
        IF v_owned_count >= public.chainling_profile_capacity() THEN
            RAISE EXCEPTION 'chainling_storage_full';
        END IF;
    END IF;

    v_result := public.authoritative_finalize_capture_attempt_service_impl(
        p_attempt_id, p_profile_id, p_outcome, p_elapsed_milliseconds,
        p_final_meter_bp, p_history, p_perfect_lock, p_entangled_type,
        p_new_battle_status, p_new_state_version, p_new_state_json
    );

    -- The capture implementation has made both records in this transaction.
    -- Equip the authority-selected ordinal in the newly granted Chainling's
    -- first move slot before this finalizer returns success.
    IF p_outcome = 'locked' AND
       NULLIF(v_result->>'chainling_id', '') IS NOT NULL AND
       NULLIF(v_result->>'move_item_id', '') IS NOT NULL THEN
        v_chainling_id := (v_result->>'chainling_id')::UUID;
        v_move_item_id := (v_result->>'move_item_id')::UUID;
        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_chainling_id, 0, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO UPDATE
        SET move_item_id = EXCLUDED.move_item_id,
            updated_at = now();

        v_result := v_result || jsonb_build_object('ordinal_equipped', true);
        UPDATE public.profile_capture_events
        SET result_json = v_result
        WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id;
    END IF;

    RETURN v_result;
END;
$$;

-- The retired client-controlled RPC remains in migration history only. It is
-- not executable by any runtime role after this migration.
REVOKE ALL ON FUNCTION public.profile_capture_wild_tx(
    UUID, TEXT, TEXT, TEXT, INTEGER, INTEGER, INTEGER,
    DOUBLE PRECISION, DOUBLE PRECISION, INTEGER, INTEGER, INTEGER,
    JSONB, BOOLEAN, BOOLEAN, TEXT, TEXT, TEXT
) FROM PUBLIC, anon, authenticated, service_role;

REVOKE ALL ON FUNCTION public.profile_chainling_capacity_summary_service(UUID)
    FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_chainling_capacity_summary_service(UUID) TO service_role;

REVOKE ALL ON FUNCTION public.authoritative_begin_capture_attempt_service_impl(
    UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.authoritative_finalize_capture_attempt_service_impl(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) FROM PUBLIC, anon, authenticated, service_role;
REVOKE ALL ON FUNCTION public.authoritative_begin_capture_attempt_service(
    UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT
) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_begin_capture_attempt_service(
    UUID, UUID, UUID, TEXT, INTEGER, TEXT, TEXT, TEXT, TEXT, JSONB, INTEGER, JSONB, BIGINT
) TO service_role;
GRANT EXECUTE ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) TO service_role;

COMMENT ON FUNCTION public.chainling_profile_capacity() IS
    'Authoritative total captured-Chainling capacity for one gameplay profile.';
COMMENT ON FUNCTION public.profile_chainling_capacity_summary_service(UUID) IS
    'Server-derived roster capacity used by read-only roster and capture preflight UI.';
