-- Chainbound — Competitive ladder & seasons groundwork (dark).
-- Additive only: no UI ranks, no rating writes from PvE outcomes.
-- See Docs/Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md (G1).

-- ─── Seasons catalog ─────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.competitive_seasons (
    season_id           TEXT PRIMARY KEY,
    display_name        TEXT NOT NULL,
    starts_at           TIMESTAMPTZ NOT NULL,
    ends_at             TIMESTAMPTZ NOT NULL,
    state               TEXT NOT NULL DEFAULT 'draft'
                        CHECK (state IN ('draft', 'scheduled', 'active', 'soft_freeze', 'ended', 'archived')),
    eligible_modes      TEXT[] NOT NULL DEFAULT ARRAY['ranked_pvp']::TEXT[],
    ruleset_version     TEXT NOT NULL DEFAULT 'v1',
    ui_flag             TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_competitive_seasons_state_dates
    ON public.competitive_seasons (state, starts_at, ends_at);
ALTER TABLE public.competitive_seasons ENABLE ROW LEVEL SECURITY;
-- ─── Per-Chainling seasonal ladder rows ──────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.chainling_season_ladder (
    chainling_id        UUID NOT NULL REFERENCES public.chainlings(id) ON DELETE CASCADE,
    season_id           TEXT NOT NULL REFERENCES public.competitive_seasons(season_id) ON DELETE CASCADE,
    bracket_key         TEXT NOT NULL DEFAULT 'open',
    rating              NUMERIC(12, 4) NOT NULL DEFAULT 1000,
    season_wins         INT NOT NULL DEFAULT 0,
    season_losses       INT NOT NULL DEFAULT 0,
    season_matches      INT NOT NULL DEFAULT 0,
    percentile          NUMERIC(6, 3),
    rank_exact          INT,
    population_n        INT NOT NULL DEFAULT 0,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chainling_id, season_id, bracket_key)
);
CREATE INDEX IF NOT EXISTS idx_chainling_season_ladder_bracket_rating
    ON public.chainling_season_ladder (season_id, bracket_key, rating DESC);
CREATE INDEX IF NOT EXISTS idx_chainling_season_ladder_chainling
    ON public.chainling_season_ladder (chainling_id, season_id);
ALTER TABLE public.chainling_season_ladder ENABLE ROW LEVEL SECURITY;
-- ─── Optional career percentile cache (species-scoped brag; batch-filled later) ─

CREATE TABLE IF NOT EXISTS public.chainling_career_percentiles (
    chainling_id                    UUID PRIMARY KEY REFERENCES public.chainlings(id) ON DELETE CASCADE,
    species_id                      TEXT NOT NULL DEFAULT '',
    pct_pve_damage_dealt_species    NUMERIC(6, 3),
    pct_pve_damage_taken_species    NUMERIC(6, 3),
    pct_pve_total_victories_species NUMERIC(6, 3),
    population_n                    INT NOT NULL DEFAULT 0,
    updated_at                      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_chainling_career_percentiles_species
    ON public.chainling_career_percentiles (species_id);
ALTER TABLE public.chainling_career_percentiles ENABLE ROW LEVEL SECURITY;
-- Access is via Edge Functions / service role only (no public policies yet).;
