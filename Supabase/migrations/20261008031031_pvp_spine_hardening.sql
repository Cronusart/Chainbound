-- M0.1 spine hardening. Devnet only. Does not edit the applied 20261007233709 spine.
-- Pairing, result windows, and the global sweep live in pvp_matchmaker_service.

ALTER TABLE public.matchmaking_queue
    ADD COLUMN IF NOT EXISTS resolver_hash text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS ruleset_hash text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS build_stamp text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS auth_user_id text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS turn_limit_seconds integer NOT NULL DEFAULT 60,
    ADD COLUMN IF NOT EXISTS forfeit_miss_threshold integer NOT NULL DEFAULT 2,
    ADD COLUMN IF NOT EXISTS lock_deadline_seconds integer NOT NULL DEFAULT 30,
    ADD COLUMN IF NOT EXISTS result_window_seconds integer NOT NULL DEFAULT 45;

ALTER TABLE public.pvp_matches
    ADD COLUMN IF NOT EXISTS resolver_hash text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS ruleset_hash text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS build_stamp text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS hp_policy text NOT NULL DEFAULT 'full_heal',
    ADD COLUMN IF NOT EXISTS winner_seat text,
    ADD COLUMN IF NOT EXISTS winner_claim_a integer,
    ADD COLUMN IF NOT EXISTS winner_claim_b integer,
    ADD COLUMN IF NOT EXISTS result_deadline_at timestamptz,
    ADD COLUMN IF NOT EXISTS lock_deadline_at timestamptz,
    ADD COLUMN IF NOT EXISTS result_window_seconds integer NOT NULL DEFAULT 45,
    ADD COLUMN IF NOT EXISTS auth_user_a text NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS auth_user_b text NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS idx_pvp_matches_player_a_created
    ON public.pvp_matches (player_a, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_pvp_matches_player_b_created
    ON public.pvp_matches (player_b, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_matchmaking_queue_pair_pin
    ON public.matchmaking_queue (
        queue_type, ruleset_id, config_hash, app_version, resolver_hash, ruleset_hash, build_stamp, enqueued_at
    )
    WHERE status = 'waiting';
