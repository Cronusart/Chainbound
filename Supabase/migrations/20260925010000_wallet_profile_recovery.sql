-- A verified wallet proof may recover the profile already bound to that wallet
-- onto the caller's current Supabase auth identity. The Edge Function verifies
-- the Ed25519 proof before invoking this service-role-only transaction.

CREATE OR REPLACE FUNCTION public.profile_recover_by_wallet_tx(
    p_auth_user_id TEXT,
    p_current_profile_id UUID,
    p_wallet_public_key TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE
    v_current_identity public.profile_identities%ROWTYPE;
    v_target_profile_id UUID;
    v_revoked_identity_count INTEGER := 0;
BEGIN
    IF NULLIF(BTRIM(COALESCE(p_auth_user_id, '')), '') IS NULL
       OR p_current_profile_id IS NULL
       OR NULLIF(BTRIM(COALESCE(p_wallet_public_key, '')), '') IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_recovery_arguments_invalid');
    END IF;

    -- Serialize all recovery attempts for this wallet while keeping the lock
    -- scoped to this short transaction.
    PERFORM pg_advisory_xact_lock(hashtextextended(
        'wallet-profile-recovery:' || p_wallet_public_key,
        0
    ));

    SELECT wallet.player_profile_id
    INTO v_target_profile_id
    FROM public.profile_wallets AS wallet
    WHERE wallet.wallet_public_key = p_wallet_public_key
      AND wallet.is_active = true
      AND wallet.unlinked_at IS NULL
    FOR UPDATE;

    IF v_target_profile_id IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'wallet_profile_not_found');
    END IF;

    SELECT identity.*
    INTO v_current_identity
    FROM public.profile_identities AS identity
    WHERE identity.auth_user_id = p_auth_user_id
      AND identity.revoked_at IS NULL
    FOR UPDATE;

    IF v_current_identity.id IS NULL
       OR v_current_identity.player_profile_id <> p_current_profile_id THEN
        RETURN jsonb_build_object('success', false, 'error', 'authenticated_profile_mismatch');
    END IF;

    IF v_target_profile_id = p_current_profile_id THEN
        UPDATE public.profile_identities
        SET is_primary = (id = v_current_identity.id)
        WHERE player_profile_id = v_target_profile_id
          AND revoked_at IS NULL;

        RETURN jsonb_build_object(
            'success', true,
            'recovered', false,
            'player_profile_id', v_target_profile_id,
            'previous_profile_id', p_current_profile_id,
            'revoked_identity_count', 0
        );
    END IF;

    -- Possession of the linked wallet rotates profile access to this install.
    -- Revoking these mappings prevents stale anonymous JWT subjects from
    -- continuing to resolve the recovered profile. Supabase auth users are not
    -- deleted; only their profile authority is revoked.
    UPDATE public.profile_identities
    SET
        revoked_at = now(),
        is_primary = false,
        metadata_json = COALESCE(metadata_json, '{}'::jsonb) || jsonb_build_object(
            'revoked_by', 'verified_wallet_profile_recovery',
            'replacement_auth_user_id', p_auth_user_id
        )
    WHERE player_profile_id = v_target_profile_id
      AND revoked_at IS NULL
      AND (auth_user_id IS NULL OR auth_user_id <> p_auth_user_id);
    GET DIAGNOSTICS v_revoked_identity_count = ROW_COUNT;

    UPDATE public.profile_identities
    SET
        player_profile_id = v_target_profile_id,
        is_primary = true,
        metadata_json = COALESCE(metadata_json, '{}'::jsonb) || jsonb_build_object(
            'source', 'verified_wallet_profile_recovery',
            'previous_profile_id', p_current_profile_id
        )
    WHERE id = v_current_identity.id
      AND revoked_at IS NULL;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'wallet profile recovery identity update lost its target'
            USING ERRCODE = '40001';
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'recovered', true,
        'player_profile_id', v_target_profile_id,
        'previous_profile_id', p_current_profile_id,
        'revoked_identity_count', v_revoked_identity_count
    );
END;
$$;

REVOKE ALL ON FUNCTION public.profile_recover_by_wallet_tx(TEXT, UUID, TEXT) FROM PUBLIC;
REVOKE ALL ON FUNCTION public.profile_recover_by_wallet_tx(TEXT, UUID, TEXT) FROM anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_recover_by_wallet_tx(TEXT, UUID, TEXT) TO service_role;

COMMENT ON FUNCTION public.profile_recover_by_wallet_tx(TEXT, UUID, TEXT) IS
'Service-role-only profile authority handoff invoked after verified Solana wallet proof. Never merges or deletes gameplay profiles.';
