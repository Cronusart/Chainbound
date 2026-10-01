-- Close inherited MVP Data API exposure before the Mainnet smoke gate opens.
--
-- Chainbound clients use JWT-verified Edge Functions. Those functions and the
-- authoritative battle service use the service-role key for database access;
-- Unity does not query these tables or RPCs directly.

ALTER TABLE public.achievements ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.event_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.modifiers_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.move_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.moves_catalog ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.player_achievements ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.player_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.vending_tickets ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE
    public.achievements,
    public.event_log,
    public.modifiers_catalog,
    public.move_items,
    public.moves_catalog,
    public.player_achievements,
    public.player_sessions,
    public.vending_tickets
FROM PUBLIC, anon, authenticated;

GRANT ALL ON TABLE
    public.achievements,
    public.event_log,
    public.modifiers_catalog,
    public.move_items,
    public.moves_catalog,
    public.player_achievements,
    public.player_sessions,
    public.vending_tickets
TO service_role;

-- PostgreSQL grants function execution to PUBLIC by default. Keep all public
-- schema RPCs service-only; Edge Functions remain the authenticated boundary.
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO service_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC, anon, authenticated;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT EXECUTE ON FUNCTIONS TO service_role;

-- These helpers use only arguments, NEW, and pg_catalog built-ins, so an empty
-- search_path is safe and prevents object-shadowing attacks.
ALTER FUNCTION public.capture_effect_add(jsonb, jsonb) SET search_path = '';
ALTER FUNCTION public.capture_effect_zero() SET search_path = '';
ALTER FUNCTION public.chainbound_touch_narrative_updated_at() SET search_path = '';
ALTER FUNCTION public.chainbound_touch_updated_at() SET search_path = '';
ALTER FUNCTION public.players_set_updated_at() SET search_path = '';
ALTER FUNCTION public.profile_quest_state_json(public.profile_quest_states) SET search_path = '';
ALTER FUNCTION public.profile_sequence_checkpoint_json(public.profile_sequence_checkpoints) SET search_path = '';
ALTER FUNCTION public.signaljam_touch_updated_at() SET search_path = '';
ALTER FUNCTION public.update_chainlings_updated_at() SET search_path = '';
ALTER FUNCTION public.update_player_lifetime_stats_updated_at() SET search_path = '';
ALTER FUNCTION public.update_updated_at_column() SET search_path = '';

-- Each column is already the table primary key, so the extra unique indexes
-- add write amplification without adding a distinct access path.
DROP INDEX IF EXISTS public.ux_player_lifetime_stats_player_profile_id;
DROP INDEX IF EXISTS public.ux_player_profiles_profile_id;
