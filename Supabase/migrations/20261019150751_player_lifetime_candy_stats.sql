-- Lifetime candy-machine rolls and stamps. Backfill from the ownership rows
-- that already record those actions, then count only new transitions.

ALTER TABLE public.player_lifetime_stats
    ADD COLUMN IF NOT EXISTS candy_machine_rolls INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS candy_machine_stamps INTEGER NOT NULL DEFAULT 0;

INSERT INTO public.player_lifetime_stats (player_profile_id, candy_machine_rolls)
SELECT player_profile_id, COUNT(*)::integer
  FROM public.ownership_known_moves
 WHERE acquisition = 'candy_roll'
 GROUP BY player_profile_id
ON CONFLICT (player_profile_id) DO UPDATE
    SET candy_machine_rolls = EXCLUDED.candy_machine_rolls;

INSERT INTO public.player_lifetime_stats (player_profile_id, candy_machine_stamps)
SELECT player_profile_id, COUNT(*)::integer
  FROM public.ownership_stamp_operations
 WHERE status = 'confirmed'
 GROUP BY player_profile_id
ON CONFLICT (player_profile_id) DO UPDATE
    SET candy_machine_stamps = EXCLUDED.candy_machine_stamps;

CREATE OR REPLACE FUNCTION public.player_lifetime_stats_count_candy_roll()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
BEGIN
    INSERT INTO public.player_lifetime_stats (player_profile_id, candy_machine_rolls)
    VALUES (NEW.player_profile_id, 1)
    ON CONFLICT (player_profile_id) DO UPDATE
        SET candy_machine_rolls = public.player_lifetime_stats.candy_machine_rolls + 1;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION public.player_lifetime_stats_count_candy_stamp()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = ''
AS $$
BEGIN
    INSERT INTO public.player_lifetime_stats (player_profile_id, candy_machine_stamps)
    VALUES (NEW.player_profile_id, 1)
    ON CONFLICT (player_profile_id) DO UPDATE
        SET candy_machine_stamps = public.player_lifetime_stats.candy_machine_stamps + 1;
    RETURN NEW;
END;
$$;

REVOKE ALL ON FUNCTION public.player_lifetime_stats_count_candy_roll() FROM PUBLIC, anon, authenticated;
REVOKE ALL ON FUNCTION public.player_lifetime_stats_count_candy_stamp() FROM PUBLIC, anon, authenticated;

DROP TRIGGER IF EXISTS ownership_known_moves_count_candy_roll ON public.ownership_known_moves;
CREATE TRIGGER ownership_known_moves_count_candy_roll
    AFTER INSERT ON public.ownership_known_moves
    FOR EACH ROW
    WHEN (NEW.acquisition = 'candy_roll')
    EXECUTE FUNCTION public.player_lifetime_stats_count_candy_roll();

DROP TRIGGER IF EXISTS ownership_stamp_operations_count_candy_stamp ON public.ownership_stamp_operations;
CREATE TRIGGER ownership_stamp_operations_count_candy_stamp
    AFTER UPDATE OF status ON public.ownership_stamp_operations
    FOR EACH ROW
    WHEN (NEW.status = 'confirmed' AND OLD.status IS DISTINCT FROM 'confirmed')
    EXECUTE FUNCTION public.player_lifetime_stats_count_candy_stamp();
