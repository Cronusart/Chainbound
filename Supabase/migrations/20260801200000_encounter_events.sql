-- Chainbound — Server-wide encounter & reward live-ops events.
-- Consumed read-only by SupabaseEncounterEventsProvider via get_encounter_events.
-- See Docs/Specs/Combat/ENCOUNTER_EVENTS_SUPABASE.md

-- ─── Events table ────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.encounter_events (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    starts_at           TIMESTAMPTZ NOT NULL,
    ends_at             TIMESTAMPTZ NOT NULL,
    enabled             BOOLEAN NOT NULL DEFAULT true,
    payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT encounter_events_name_not_blank
        CHECK (length(btrim(name)) > 0),
    CONSTRAINT encounter_events_window_ordered
        CHECK (ends_at > starts_at),
    -- The Unity client deserializes payload into EncounterModifierDto; a non-object
    -- (array/scalar/null) would silently produce an empty modifier.
    CONSTRAINT encounter_events_payload_is_object
        CHECK (jsonb_typeof(payload) = 'object')
);
-- get_encounter_events filters enabled rows by an instant inside [starts_at, ends_at].
-- Partial index keeps only live-eligible rows, which is the only set ever queried.
CREATE INDEX IF NOT EXISTS idx_encounter_events_active_window
    ON public.encounter_events (starts_at, ends_at)
    WHERE enabled;
DROP TRIGGER IF EXISTS trg_encounter_events_touch_updated_at
    ON public.encounter_events;
CREATE TRIGGER trg_encounter_events_touch_updated_at
    BEFORE UPDATE ON public.encounter_events
    FOR EACH ROW
    EXECUTE FUNCTION public.chainbound_touch_updated_at();
-- ─── Access control ──────────────────────────────────────────────────────────

-- RLS on with no policies: only service_role (which bypasses RLS) can read/write.
-- Live-ops levers must never be client-readable or client-writable.
ALTER TABLE public.encounter_events ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.encounter_events FROM anon, authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.encounter_events TO service_role;
