-- Case-insensitive uniqueness for player profile display names.
-- Chainling and starter names are intentionally not covered.
--
-- This migration does not rename or delete existing rows. If two profiles
-- already share a name ignoring case, it raises and leaves the schema
-- unchanged so an operator can resolve those rows manually and re-run.

DO $$
DECLARE
    collision_count INTEGER := 0;
    collision_report TEXT := '';
    collision RECORD;
BEGIN
    FOR collision IN
        SELECT lower(btrim(display_name)) AS normalized_name,
               count(*) AS copies,
               string_agg(player_profile_id::text, ', ' ORDER BY created_at, player_profile_id::text) AS profile_ids
        FROM public.player_profiles
        WHERE display_name IS NOT NULL
          AND btrim(display_name) <> ''
        GROUP BY lower(btrim(display_name))
        HAVING count(*) > 1
    LOOP
        collision_count := collision_count + 1;
        collision_report := collision_report || format(
            E'\n  "%s" x%s profiles [%s]',
            collision.normalized_name,
            collision.copies,
            collision.profile_ids
        );
    END LOOP;

    IF collision_count > 0 THEN
        RAISE EXCEPTION
            'player_profiles has % case-insensitive display_name collision(s). This migration does not rename or delete existing rows. Resolve these rows manually, then re-run:%',
            collision_count,
            collision_report;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_player_profiles_display_name_ci
    ON public.player_profiles (lower(btrim(display_name)))
    WHERE display_name IS NOT NULL
      AND btrim(display_name) <> '';

CREATE OR REPLACE FUNCTION public.profile_set_display_name_tx(
    p_profile_id UUID,
    p_display_name TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_name TEXT := NULLIF(btrim(COALESCE(p_display_name, '')), '');
    v_saved TEXT;
BEGIN
    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    IF v_name IS NULL OR char_length(v_name) < 1 OR char_length(v_name) > 16 THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
    END IF;

    -- Serialize same-name commits. The unique index still catches a race
    -- that bypasses this lock, and the handler below reports name_taken.
    PERFORM pg_advisory_xact_lock(hashtextextended('player_display_name:' || lower(v_name), 0));

    IF EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id <> p_profile_id
          AND display_name IS NOT NULL
          AND lower(btrim(display_name)) = lower(v_name)
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'name_taken');
    END IF;

    UPDATE public.player_profiles
    SET display_name = v_name
    WHERE player_profile_id = p_profile_id
    RETURNING display_name INTO v_saved;

    IF v_saved IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    RETURN jsonb_build_object(
        'success', true,
        'player_profile_id', p_profile_id,
        'display_name', v_saved
    );
EXCEPTION
    WHEN unique_violation THEN
        RETURN jsonb_build_object('success', false, 'error', 'name_taken');
END;
$$;

REVOKE ALL ON FUNCTION public.profile_set_display_name_tx(UUID, TEXT) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.profile_set_display_name_tx(UUID, TEXT) TO service_role;

COMMENT ON FUNCTION public.profile_set_display_name_tx(UUID, TEXT) IS
    'Sets a player profile display name. Rejects case-insensitive collisions with name_taken. The same profile may re-set its own name. Does not apply to chainling names.';
