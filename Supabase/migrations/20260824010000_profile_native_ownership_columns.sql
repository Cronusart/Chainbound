-- Chainbound - profile-native gameplay ownership, phase 1.
--
-- Adds the canonical player_profile_id ownership columns and moves the active
-- profile economy fields onto player_profiles. Legacy columns remain during
-- this phase so Edge Functions can be deployed before the destructive cleanup.

ALTER TABLE public.player_profiles
    ADD COLUMN IF NOT EXISTS ram_balance INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS vending_upg_ord_today INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS vending_upg_rune_today INTEGER NOT NULL DEFAULT 0;
-- Preserve any pre-release compatibility data if a non-empty environment ever
-- applies this migration. The current hosted project has no public.players rows.
UPDATE public.player_profiles pp
SET ram_balance = GREATEST(pp.ram_balance, COALESCE(p.ram_balance, 0)),
    vending_upg_ord_today = GREATEST(pp.vending_upg_ord_today, COALESCE(p.vending_upg_ord_today, 0)),
    vending_upg_rune_today = GREATEST(pp.vending_upg_rune_today, COALESCE(p.vending_upg_rune_today, 0))
FROM public.players p
WHERE p.id = pp.legacy_player_id;
ALTER TABLE public.chainlings
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.move_items
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.player_lifetime_stats
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.event_log
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.player_sessions
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.vending_tickets
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.player_achievements
    ADD COLUMN IF NOT EXISTS player_profile_id UUID;
ALTER TABLE public.move_upgrade_events
    ALTER COLUMN player_id DROP NOT NULL;
-- Backfill through either side of the temporary compatibility mapping.
UPDATE public.chainlings c
SET player_profile_id = COALESCE(c.player_profile_id, p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE c.player_id = p.id
  AND c.player_profile_id IS NULL;
UPDATE public.move_items mi
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE mi.player_id = p.id
  AND mi.player_profile_id IS NULL;
UPDATE public.player_lifetime_stats s
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE s.player_id = p.id
  AND s.player_profile_id IS NULL;
UPDATE public.event_log e
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE e.player_id = p.id
  AND e.player_profile_id IS NULL;
UPDATE public.player_sessions s
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE s.player_id = p.id
  AND s.player_profile_id IS NULL;
UPDATE public.vending_tickets t
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE t.player_id = p.id
  AND t.player_profile_id IS NULL;
UPDATE public.player_achievements a
SET player_profile_id = COALESCE(p.player_profile_id, pp.player_profile_id)
FROM public.players p
LEFT JOIN public.player_profiles pp ON pp.legacy_player_id = p.id
WHERE a.player_id = p.id
  AND a.player_profile_id IS NULL;
CREATE INDEX IF NOT EXISTS idx_move_items_player_profile_id
    ON public.move_items (player_profile_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_player_lifetime_stats_player_profile_id
    ON public.player_lifetime_stats (player_profile_id);
CREATE INDEX IF NOT EXISTS idx_event_log_player_profile_id
    ON public.event_log (player_profile_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_player_sessions_player_profile_id
    ON public.player_sessions (player_profile_id, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_vending_tickets_player_profile_id
    ON public.vending_tickets (player_profile_id, created_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS ux_player_achievements_profile_achievement
    ON public.player_achievements (player_profile_id, achievement_id)
    WHERE player_profile_id IS NOT NULL;
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'move_items_player_profile_id_fkey'
          AND conrelid = 'public.move_items'::regclass
    ) THEN
        ALTER TABLE public.move_items
            ADD CONSTRAINT move_items_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'player_lifetime_stats_player_profile_id_fkey'
          AND conrelid = 'public.player_lifetime_stats'::regclass
    ) THEN
        ALTER TABLE public.player_lifetime_stats
            ADD CONSTRAINT player_lifetime_stats_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'event_log_player_profile_id_fkey'
          AND conrelid = 'public.event_log'::regclass
    ) THEN
        ALTER TABLE public.event_log
            ADD CONSTRAINT event_log_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'player_sessions_player_profile_id_fkey'
          AND conrelid = 'public.player_sessions'::regclass
    ) THEN
        ALTER TABLE public.player_sessions
            ADD CONSTRAINT player_sessions_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'vending_tickets_player_profile_id_fkey'
          AND conrelid = 'public.vending_tickets'::regclass
    ) THEN
        ALTER TABLE public.vending_tickets
            ADD CONSTRAINT vending_tickets_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'player_achievements_player_profile_id_fkey'
          AND conrelid = 'public.player_achievements'::regclass
    ) THEN
        ALTER TABLE public.player_achievements
            ADD CONSTRAINT player_achievements_player_profile_id_fkey
            FOREIGN KEY (player_profile_id)
            REFERENCES public.player_profiles(player_profile_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;
END $$;
