-- Hub H0.1. Safe defaults only. Devnet flags are flipped in a separate statement after apply.
-- Dev exemption is ignored unless hub_settings.cluster is exactly 'devnet'.

ALTER TABLE public.hub_settings
    ADD COLUMN IF NOT EXISTS cluster text NOT NULL DEFAULT '';

ALTER TABLE public.hub_settings
    DROP CONSTRAINT IF EXISTS hub_settings_cluster_known;

ALTER TABLE public.hub_settings
    ADD CONSTRAINT hub_settings_cluster_known CHECK (cluster IN ('', 'devnet'));

UPDATE public.hub_settings
SET multiplayer_unlocked = false,
    dev_exempt_enabled = false,
    cluster = ''
WHERE settings_id = 1;

CREATE OR REPLACE FUNCTION public.hub_enqueue_allowed(p_profile_id uuid)
RETURNS text
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
DECLARE
    v_unlocked boolean := false;
    v_dev_enabled boolean := false;
    v_cluster text := '';
    v_hub boolean := false;
    v_exempt boolean := false;
BEGIN
    SELECT multiplayer_unlocked, dev_exempt_enabled, cluster
    INTO v_unlocked, v_dev_enabled, v_cluster
    FROM public.hub_settings
    WHERE settings_id = 1;

    IF NOT COALESCE(v_unlocked, false) THEN
        RETURN 'hub_locked';
    END IF;

    SELECT hub_unlocked, dev_exempt
    INTO v_hub, v_exempt
    FROM public.hub_profiles
    WHERE player_profile_id = p_profile_id;

    IF COALESCE(v_hub, false) THEN
        RETURN NULL;
    END IF;

    IF v_cluster = 'devnet'
       AND COALESCE(v_dev_enabled, false)
       AND COALESCE(v_exempt, false) THEN
        RETURN NULL;
    END IF;

    RETURN 'hub_locked';
END;
$$;

CREATE OR REPLACE FUNCTION public.hub_link_identity_service(
    p_profile_id uuid,
    p_supabase_user_id uuid,
    p_ugs_player_id text
)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = ''
AS $$
BEGIN
    IF p_supabase_user_id IS NULL OR p_ugs_player_id IS NULL OR length(btrim(p_ugs_player_id)) = 0 THEN
        RETURN jsonb_build_object('success', false, 'error', 'identity_required');
    END IF;

    PERFORM 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    BEGIN
        INSERT INTO public.hub_identities (supabase_user_id, player_profile_id, ugs_player_id)
        VALUES (p_supabase_user_id, p_profile_id, btrim(p_ugs_player_id))
        ON CONFLICT (supabase_user_id) DO UPDATE
        SET player_profile_id = EXCLUDED.player_profile_id,
            ugs_player_id = EXCLUDED.ugs_player_id,
            linked_at = pg_catalog.now();
    EXCEPTION
        WHEN unique_violation THEN
            RETURN jsonb_build_object('success', false, 'error', 'identity_in_use');
    END;

    RETURN jsonb_build_object('success', true, 'error', '');
END;
$$;

REVOKE ALL ON FUNCTION public.hub_enqueue_allowed(uuid) FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.hub_link_identity_service(uuid, uuid, text) FROM PUBLIC, anon, authenticated;

GRANT EXECUTE ON FUNCTION public.hub_enqueue_allowed(uuid) TO service_role;
GRANT EXECUTE ON FUNCTION public.hub_link_identity_service(uuid, uuid, text) TO service_role;
