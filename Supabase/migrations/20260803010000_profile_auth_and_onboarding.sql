-- JWT subject is the sole authority for profile-first Edge Functions. This index
-- makes anonymous-auth profile bootstrap idempotent under concurrent launches.
CREATE UNIQUE INDEX IF NOT EXISTS ux_profile_identities_active_auth_user
    ON public.profile_identities (auth_user_id)
    WHERE revoked_at IS NULL AND auth_user_id IS NOT NULL AND auth_user_id <> '';
-- The cinematic checkpoint intentionally lives in the data-driven progression
-- state so other onboarding milestones can use the same profile shape.
UPDATE public.player_profiles
SET progression_state = COALESCE(progression_state, '{}'::jsonb)
WHERE progression_state IS NULL;
