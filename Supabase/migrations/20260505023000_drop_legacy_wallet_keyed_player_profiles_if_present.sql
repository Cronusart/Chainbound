-- Signal Jam 3D - remove only the legacy wallet-keyed player_profiles table.
-- The account refactor recreates public.player_profiles as the canonical
-- player_profile_id table in the later account identity migration.

DO $$
BEGIN
    IF to_regclass('public.player_profiles') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'player_profiles'
             AND column_name = 'wallet_address'
       )
    THEN
        DROP TABLE public.player_profiles;
    END IF;
END $$;
