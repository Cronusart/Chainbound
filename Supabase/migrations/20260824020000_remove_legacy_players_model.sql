-- Chainbound - remove the pre-release wallet-era players ownership model.
--
-- The hosted project was verified to contain no public.players rows before this
-- migration. Every gameplay-owned table now uses player_profile_id exclusively.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.players) THEN
        RAISE EXCEPTION
            'profile_native_cutover_requires_empty_players_table';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public.chainlings
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.move_items
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.player_lifetime_stats
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.event_log
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.player_sessions
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.vending_tickets
        WHERE player_profile_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM public.player_achievements
        WHERE player_profile_id IS NULL
    ) THEN
        RAISE EXCEPTION
            'profile_native_cutover_found_unowned_gameplay_rows';
    END IF;
END $$;
-- Retire profile-native overloads' wallet-era predecessors before removing
-- columns used by their stored transaction bodies.
DROP FUNCTION IF EXISTS public.profile_rebirth_chainling_tx(
    UUID, UUID, UUID, INTEGER, INTEGER
);
DROP FUNCTION IF EXISTS public.profile_upgrade_move_item_tx(
    UUID, UUID, UUID, public.move_tier, TEXT, TEXT, INTEGER, TEXT
);
DROP FUNCTION IF EXISTS public.profile_apply_pve_player_lifetime_stats_tx(
    UUID, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, INTEGER, BIGINT
);
-- Remove constraints whose key includes the old player_id column. This avoids
-- relying on historical auto-generated constraint names from the MVP schema.
DO $$
DECLARE
    v_constraint RECORD;
BEGIN
    FOR v_constraint IN
        SELECT c.conrelid::regclass AS table_name, c.conname
        FROM pg_constraint c
        JOIN pg_attribute a
          ON a.attrelid = c.conrelid
         AND a.attnum = ANY(c.conkey)
        WHERE c.connamespace = 'public'::regnamespace
          AND a.attname = 'player_id'
          AND c.conrelid IN (
              'public.chainlings'::regclass,
              'public.move_items'::regclass,
              'public.player_lifetime_stats'::regclass,
              'public.event_log'::regclass,
              'public.player_sessions'::regclass,
              'public.vending_tickets'::regclass,
              'public.player_achievements'::regclass,
              'public.move_upgrade_events'::regclass
          )
    LOOP
        EXECUTE format(
            'ALTER TABLE %s DROP CONSTRAINT %I',
            v_constraint.table_name,
            v_constraint.conname
        );
    END LOOP;
END $$;
ALTER TABLE public.chainlings
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.move_items
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.player_lifetime_stats
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.event_log
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.player_sessions
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.vending_tickets
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.player_achievements
    ALTER COLUMN player_profile_id SET NOT NULL,
    DROP COLUMN player_id;
ALTER TABLE public.move_upgrade_events
    DROP COLUMN player_id;
ALTER TABLE public.player_lifetime_stats
    ADD CONSTRAINT player_lifetime_stats_pkey PRIMARY KEY (player_profile_id);
ALTER TABLE public.chainlings
    DROP CONSTRAINT IF EXISTS chainlings_player_profile_id_fkey;
ALTER TABLE public.chainlings
    ADD CONSTRAINT chainlings_player_profile_id_fkey
    FOREIGN KEY (player_profile_id)
    REFERENCES public.player_profiles(player_profile_id)
    ON DELETE CASCADE;
ALTER TABLE public.move_items
    VALIDATE CONSTRAINT move_items_player_profile_id_fkey;
ALTER TABLE public.player_lifetime_stats
    VALIDATE CONSTRAINT player_lifetime_stats_player_profile_id_fkey;
ALTER TABLE public.event_log
    VALIDATE CONSTRAINT event_log_player_profile_id_fkey;
ALTER TABLE public.player_sessions
    VALIDATE CONSTRAINT player_sessions_player_profile_id_fkey;
ALTER TABLE public.vending_tickets
    VALIDATE CONSTRAINT vending_tickets_player_profile_id_fkey;
ALTER TABLE public.player_achievements
    VALIDATE CONSTRAINT player_achievements_player_profile_id_fkey;
-- legacy_player_id was never populated in this pre-release environment. Remove
-- both the generated UNIQUE constraint and the explicit historical index.
DO $$
DECLARE
    v_constraint RECORD;
BEGIN
    FOR v_constraint IN
        SELECT c.conname
        FROM pg_constraint c
        JOIN pg_attribute a
          ON a.attrelid = c.conrelid
         AND a.attnum = ANY(c.conkey)
        WHERE c.conrelid = 'public.player_profiles'::regclass
          AND a.attname = 'legacy_player_id'
    LOOP
        EXECUTE format(
            'ALTER TABLE public.player_profiles DROP CONSTRAINT %I',
            v_constraint.conname
        );
    END LOOP;
END $$;
DROP INDEX IF EXISTS public.ux_player_profiles_legacy_player_id;
ALTER TABLE public.player_profiles
    DROP COLUMN legacy_player_id;
DROP TABLE public.players;
DROP FUNCTION IF EXISTS public.players_set_wallet_lower();
COMMENT ON COLUMN public.player_profiles.player_profile_id IS
    'Canonical owner identifier for account, narrative, roster, inventory, economy, and gameplay state.';
COMMENT ON COLUMN public.move_items.player_profile_id IS
    'Canonical profile owner. Wallets are optional linked identities and never gameplay owners.';
