CREATE OR REPLACE FUNCTION public.profile_set_chainling_display_name_tx(
    p_profile_id UUID,
    p_chainling_id UUID,
    p_display_name TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_name TEXT := NULLIF(BTRIM(COALESCE(p_display_name, '')), '');
BEGIN
    IF p_profile_id IS NULL OR p_chainling_id IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_and_chainling_required');
    END IF;

    IF v_name IS NULL OR char_length(v_name) < 1 OR char_length(v_name) > 16
       OR v_name !~ '^[[:alpha:]][[:alpha:] ''-]*$' THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
    END IF;

    UPDATE public.chainlings
    SET display_name = v_name,
        updated_at = NOW()
    WHERE id = p_chainling_id
      AND player_profile_id = p_profile_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'chainling_not_found');
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'chainling_id', p_chainling_id,
        'display_name', v_name
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_set_chainling_display_name_tx(UUID, UUID, TEXT)
FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_set_chainling_display_name_tx(UUID, UUID, TEXT)
TO service_role;

COMMENT ON FUNCTION public.profile_set_chainling_display_name_tx(UUID, UUID, TEXT) IS
'Service-role owner-scoped Chainling nickname mutation. Call through profile_set_chainling_display_name.';
