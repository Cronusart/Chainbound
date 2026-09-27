-- Signal Jam 3D — Chainlings: progression, player-facing, and analytics columns
-- Additive migration: safe to re-run (IF NOT EXISTS / ADD COLUMN IF NOT EXISTS).
-- Targets the existing `chainlings` table.

-- ─── Progression Columns (ECONOMY_01: stars, rebirth, mastery) ───────────────

ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS stars SMALLINT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS rebirth_count SMALLINT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS rebirth_iv_bonuses JSONB NOT NULL DEFAULT '[0,0,0,0,0,0]';
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS mastery_xp INT NOT NULL DEFAULT 0;
-- ─── Player-Facing Columns ───────────────────────────────────────────────────

ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS display_name TEXT;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS entangled_flag BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS evolution_stage SMALLINT NOT NULL DEFAULT 1;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();
-- ─── Per-Chainling Analytics ─────────────────────────────────────────────────

ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS battles_won INT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS battles_lost INT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS kos_scored INT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS times_kod INT NOT NULL DEFAULT 0;
ALTER TABLE chainlings ADD COLUMN IF NOT EXISTS times_captured INT NOT NULL DEFAULT 0;
-- ─── Auto-update updated_at trigger ──────────────────────────────────────────

CREATE OR REPLACE FUNCTION update_chainlings_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_chainlings_updated_at ON chainlings;
CREATE TRIGGER trg_chainlings_updated_at
    BEFORE UPDATE ON chainlings
    FOR EACH ROW
    EXECUTE FUNCTION update_chainlings_updated_at();
-- ─── RLS ─────────────────────────────────────────────────────────────────────
-- All access goes through Edge Functions using the service-role key.

ALTER TABLE chainlings ENABLE ROW LEVEL SECURITY;
