-- Case-insensitive uniqueness for player display names claimed after this migration.
-- Chainling and starter names are not covered.
--
-- Cutover (rule a):
--   Existing rows are grandfathered. display_name_claimed_at stays NULL, so
--   today's case-insensitive duplicates remain and this migration can apply
--   on Devnet without rewriting display_name, wallets, RAM, or auth.
--   A later profile_set_display_name call is a new claim when the requested
--   name differs from that profile's current name. New claims are rejected
--   with name_taken when ANY other non-blank display_name matches ignoring
--   case, including grandfathered rows. The same profile may always re-set
--   its own current name.
--   display_name_claimed_at is stamped only when the writer is the sole
--   holder, which is what the partial unique index covers. A grandfathered
--   profile that re-saves a name still shared with other grandfathered rows
--   keeps a NULL stamp so those duplicates never enter the index.
--
-- This migration does not change existing display_name values.

ALTER TABLE public.player_profiles
    ADD COLUMN IF NOT EXISTS display_name_claimed_at TIMESTAMPTZ NULL;

COMMENT ON COLUMN public.player_profiles.display_name_claimed_at IS
    'Set when profile_set_display_name claims a name no other profile holds. NULL is grandfathered: the existing display_name was not rewritten and is outside the unique index. New claims are still blocked by every non-blank name.';

-- Replace the draft full unique index if a local database created it.
-- Dropping the index does not change row data.
DROP INDEX IF EXISTS public.ux_player_profiles_display_name_ci;

CREATE UNIQUE INDEX ux_player_profiles_display_name_ci
    ON public.player_profiles (lower(btrim(display_name)))
    WHERE display_name_claimed_at IS NOT NULL
      AND display_name IS NOT NULL
      AND btrim(display_name) <> '';

COMMENT ON INDEX public.ux_player_profiles_display_name_ci IS
    'One enforced player display name ignoring case. Grandfathered rows (display_name_claimed_at NULL) are excluded so existing Devnet duplicates do not block this index.';

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
    v_current TEXT;
    v_saved TEXT;
    v_key TEXT;
    v_current_key TEXT;
    v_same_name BOOLEAN;
    v_sole_holder BOOLEAN;
BEGIN
    IF v_name IS NULL OR char_length(v_name) < 1 OR char_length(v_name) > 16 THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_display_name');
    END IF;

    SELECT display_name
    INTO v_current
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    v_key := lower(v_name);
    v_current_key := lower(btrim(COALESCE(v_current, '')));
    v_same_name := v_current_key <> '' AND v_current_key = v_key;

    -- Serialize claims of this normalized name. The partial unique index
    -- still reports name_taken if two enforced writes race past the lock.
    PERFORM pg_advisory_xact_lock(hashtextextended('player_display_name:' || v_key, 0));

    v_sole_holder := NOT EXISTS (
        SELECT 1
        FROM public.player_profiles
        WHERE player_profile_id <> p_profile_id
          AND display_name IS NOT NULL
          AND btrim(display_name) <> ''
          AND lower(btrim(display_name)) = v_key
    );

    -- Rule (a): a different name is blocked by every existing holder,
    -- grandfathered or enforced. Re-setting this profile's own name is not.
    IF NOT v_same_name AND NOT v_sole_holder THEN
        RETURN jsonb_build_object('success', false, 'error', 'name_taken');
    END IF;

    UPDATE public.player_profiles
    SET display_name = v_name,
        display_name_claimed_at = CASE
            WHEN v_sole_holder THEN COALESCE(display_name_claimed_at, now())
            ELSE display_name_claimed_at
        END
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
    'Sets a player display name. New claims are name_taken when any other non-blank name matches ignoring case, including grandfathered rows. The same profile may re-set its own name. Stamps display_name_claimed_at only for the sole holder. Does not apply to chainling names.';
