-- The capacity wrapper added an ordinal-loadout upsert for a locked capture.
-- `loadouts` is intentionally creation-audited only; it has no `updated_at`
-- column. Recreate the wrapper without assuming a timestamp column so the
-- capture grant, first move, event finalization, and battle checkpoint remain
-- one atomic transaction.
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

    -- The capture implementation made both records inside this transaction.
    -- Equip its authority-selected ordinal in the first move slot. `loadouts`
    -- owns `created_at` only, so an upsert must not write a nonexistent
    -- timestamp column.
    IF p_outcome = 'locked' AND
       NULLIF(v_result->>'chainling_id', '') IS NOT NULL AND
       NULLIF(v_result->>'move_item_id', '') IS NOT NULL THEN
        v_chainling_id := (v_result->>'chainling_id')::UUID;
        v_move_item_id := (v_result->>'move_item_id')::UUID;
        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_chainling_id, 0, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO UPDATE
        SET move_item_id = EXCLUDED.move_item_id;

        v_result := v_result || jsonb_build_object('ordinal_equipped', true);
        UPDATE public.profile_capture_events
        SET result_json = v_result
        WHERE attempt_id = p_attempt_id AND player_profile_id = p_profile_id;
    END IF;

    RETURN v_result;
END;
$$;

-- Reassert the existing internal-only surface after replacing the function.
REVOKE ALL ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.authoritative_finalize_capture_attempt_service(
    UUID, UUID, TEXT, INTEGER, INTEGER, JSONB, BOOLEAN, TEXT, TEXT, INTEGER, JSONB
) TO service_role;
