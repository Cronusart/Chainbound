-- Signal Jam 3D — Player Lifetime Stats
-- Separate table for player-level analytics, quest progress, and battlepass milestones.
-- Additive migration: safe to re-run (IF NOT EXISTS).

-- ─── Table ────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS player_lifetime_stats (
    player_id       UUID PRIMARY KEY REFERENCES players(id),

    -- Battle stats
    battles_won     INT NOT NULL DEFAULT 0,
    battles_lost    INT NOT NULL DEFAULT 0,
    battles_drawn   INT NOT NULL DEFAULT 0,
    battles_fled    INT NOT NULL DEFAULT 0,

    -- KO stats
    total_kos_scored    INT NOT NULL DEFAULT 0,
    total_kos_taken     INT NOT NULL DEFAULT 0,

    -- Capture stats
    total_captures          INT NOT NULL DEFAULT 0,
    entangled_captures      INT NOT NULL DEFAULT 0,

    -- Progression stats
    total_rebirths          INT NOT NULL DEFAULT 0,
    total_star_ups          INT NOT NULL DEFAULT 0,
    total_mastery_cycles    INT NOT NULL DEFAULT 0,

    -- Economy stats
    total_ram_earned        BIGINT NOT NULL DEFAULT 0,
    total_ram_spent         BIGINT NOT NULL DEFAULT 0,
    total_ordinals_acquired INT NOT NULL DEFAULT 0,
    total_inscriptions_created INT NOT NULL DEFAULT 0,

    -- Species collection
    unique_species_captured INT NOT NULL DEFAULT 0,

    -- Timestamps
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- ─── Auto-update updated_at trigger ──────────────────────────────────────────

CREATE OR REPLACE FUNCTION update_player_lifetime_stats_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_player_lifetime_stats_updated_at ON player_lifetime_stats;
CREATE TRIGGER trg_player_lifetime_stats_updated_at
    BEFORE UPDATE ON player_lifetime_stats
    FOR EACH ROW
    EXECUTE FUNCTION update_player_lifetime_stats_updated_at();
-- ─── RLS ─────────────────────────────────────────────────────────────────────
-- All access goes through Edge Functions using the service-role key.

ALTER TABLE player_lifetime_stats ENABLE ROW LEVEL SECURITY;
