-- Storage-only Chainling release. Party members and bound Chainlings stay.
-- Loadouts and the PvE service record cascade with the chainlings row.
-- Known move_items stay on the profile. profile_capture_events.grant_chainling_id
-- has no foreign key, so capture history keeps the old id.

CREATE OR REPLACE FUNCTION public.profile_release_chainling(
    p_profile_id UUID,
    p_chainling_id UUID
) RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_owner UUID;
    v_party BOOLEAN;
    v_deleted INTEGER;
BEGIN
    IF p_profile_id IS NULL OR p_chainling_id IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'chainling_id_required');
    END IF;

    SELECT player_profile_id, COALESCE(is_party_member, false)
      INTO v_owner, v_party
    FROM public.chainlings
    WHERE id = p_chainling_id;

    IF v_owner IS NULL THEN
        RETURN jsonb_build_object('ok', false, 'error', 'chainling_not_found');
    END IF;

    IF v_owner <> p_profile_id THEN
        RETURN jsonb_build_object('ok', false, 'error', 'chainling_not_owned');
    END IF;

    IF v_party THEN
        RETURN jsonb_build_object('ok', false, 'error', 'still_in_party');
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.ownership_chainling_bindings
        WHERE chainling_id = p_chainling_id
    ) OR EXISTS (
        SELECT 1
        FROM public.ownership_bind_operations
        WHERE chainling_id = p_chainling_id
          AND status <> 'failed'
    ) THEN
        RETURN jsonb_build_object('ok', false, 'error', 'bound_chainling');
    END IF;

    BEGIN
        DELETE FROM public.chainlings
        WHERE id = p_chainling_id
          AND player_profile_id = p_profile_id;
        GET DIAGNOSTICS v_deleted = ROW_COUNT;
    EXCEPTION
        WHEN foreign_key_violation THEN
            RETURN jsonb_build_object('ok', false, 'error', 'bound_chainling');
    END;

    IF v_deleted IS NULL OR v_deleted = 0 THEN
        RETURN jsonb_build_object('ok', false, 'error', 'chainling_not_found');
    END IF;

    RETURN jsonb_build_object('ok', true);
END;
$$;

REVOKE ALL ON FUNCTION public.profile_release_chainling(UUID, UUID) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_release_chainling(UUID, UUID) TO service_role;
