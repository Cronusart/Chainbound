-- Chainbound - server-authoritative passive Chainling recovery.
--
-- Recovery is lazy: authenticated roster/party reads obtain a finalized Solana
-- block timestamp in the Edge layer, then call this locked transaction before
-- returning snapshots. The client never supplies HP, max HP, elapsed time, or
-- recovery rates to this function.

ALTER TABLE public.chainlings
    ADD COLUMN IF NOT EXISTS passive_recovery_at TIMESTAMPTZ NULL;

-- Canonical combat vitals required to derive MaxHP on the server. Keep this
-- catalogue in sync with SpeciesDatabase.asset when species are added/retuned.
CREATE TABLE IF NOT EXISTS public.chainling_species_vitals (
    species_id TEXT PRIMARY KEY,
    base_max_hp INTEGER NOT NULL CHECK (base_max_hp > 0),
    catalog_version INTEGER NOT NULL DEFAULT 1 CHECK (catalog_version > 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (species_id = lower(btrim(species_id)))
);

INSERT INTO public.chainling_species_vitals (species_id, base_max_hp, catalog_version)
VALUES
    ('caw', 78, 1),
    ('cinislug', 115, 1),
    ('deghoul', 86, 1),
    ('ghosty', 82, 1),
    ('gilarime', 132, 1),
    ('kitsauto', 109, 1),
    ('kreechank', 104, 1),
    ('lad', 70, 1),
    ('snek', 90, 1),
    ('validape', 127, 1),
    ('whale', 128, 1)
ON CONFLICT (species_id) DO NOTHING;

ALTER TABLE public.chainling_species_vitals ENABLE ROW LEVEL SECURITY;
REVOKE ALL ON TABLE public.chainling_species_vitals FROM anon, authenticated;

-- Existing HP state has no reliable historical recovery anchor. Start its timer
-- at deployment (or at the last authenticated HP write), rather than granting a
-- retroactive full heal. New captures without an HP timestamp follow the same
-- safe first-read rule in the resolver below.
UPDATE public.chainlings
SET passive_recovery_at = COALESCE(hp_synced_at, now())
WHERE passive_recovery_at IS NULL;

CREATE INDEX IF NOT EXISTS chainlings_passive_recovery_party_idx
    ON public.chainlings (player_profile_id, passive_recovery_at)
    WHERE is_party_member = true
      AND current_hp IS NOT NULL
      AND current_hp > 0;

CREATE OR REPLACE FUNCTION public.profile_apply_chainling_passive_recovery_tx(
    p_profile_id UUID,
    p_authoritative_time TIMESTAMPTZ,
    p_interval_seconds INTEGER,
    p_heal_basis_points INTEGER,
    p_max_catchup_intervals INTEGER,
    p_party_only BOOLEAN
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_row RECORD;
    v_interval_seconds INTEGER;
    v_heal_basis_points INTEGER;
    v_max_catchup_intervals INTEGER;
    v_rebirth_count INTEGER;
    v_hp_pick_count INTEGER;
    v_hp_iv INTEGER;
    v_auto_numerator INTEGER;
    v_pick_numerator INTEGER;
    v_auto_quotient INTEGER;
    v_pick_quotient INTEGER;
    v_auto_bonus INTEGER;
    v_pick_bonus INTEGER;
    v_max_hp INTEGER;
    v_current_hp INTEGER;
    v_recovery_anchor TIMESTAMPTZ;
    v_elapsed_seconds BIGINT;
    v_completed_ticks INTEGER;
    v_heal_per_tick INTEGER;
    v_next_hp INTEGER;
    v_next_anchor TIMESTAMPTZ;
    v_updated_count INTEGER := 0;
BEGIN
    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RAISE EXCEPTION 'profile_not_found';
    END IF;

    -- The service-role Edge function supplies a finalized Solana block time. A
    -- bounded acceptance window catches accidental/malicious stale callers while
    -- allowing normal Solana finality and provider lag.
    IF p_authoritative_time IS NULL
       OR p_authoritative_time < now() - INTERVAL '1 hour'
       OR p_authoritative_time > now() + INTERVAL '5 minutes' THEN
        RETURN jsonb_build_object('applied', false, 'reason', 'invalid_authoritative_time', 'updated_chainlings', 0);
    END IF;

    v_interval_seconds := LEAST(86400, GREATEST(60, COALESCE(p_interval_seconds, 1800)));
    v_heal_basis_points := LEAST(10000, GREATEST(0, COALESCE(p_heal_basis_points, 0)));
    v_max_catchup_intervals := LEAST(1000, GREATEST(1, COALESCE(p_max_catchup_intervals, 1)));

    IF v_heal_basis_points = 0 THEN
        RETURN jsonb_build_object('applied', true, 'updated_chainlings', 0);
    END IF;

    FOR v_row IN
        SELECT
            c.id,
            c.current_hp,
            c.hp_synced_at,
            c.passive_recovery_at,
            c.ivs,
            c.rebirth_count,
            c.rebirth_iv_bonuses,
            v.base_max_hp
        FROM public.chainlings c
        INNER JOIN public.chainling_species_vitals v
            ON v.species_id = lower(btrim(c.species_id))
        WHERE c.player_profile_id = p_profile_id
          AND (NOT COALESCE(p_party_only, true) OR c.is_party_member = true)
        FOR UPDATE OF c
    LOOP
        -- This mirrors ChainlingProgressionUtility / ProgressionConfigSO v1:
        -- MaxHP = base + IV[0] + round-to-even(base * .05 * rebirth)
        --                    + round-to-even(base * .05 * HP-pick-count).
        v_rebirth_count := LEAST(5, GREATEST(0, COALESCE(v_row.rebirth_count, 0)));
        v_hp_pick_count := LEAST(5, GREATEST(0, COALESCE((v_row.rebirth_iv_bonuses ->> 0)::INTEGER, 0)));
        v_hp_iv := LEAST(10, GREATEST(-10, COALESCE((v_row.ivs ->> 0)::INTEGER, 0)));

        v_auto_numerator := v_row.base_max_hp * 5 * v_rebirth_count;
        v_pick_numerator := v_row.base_max_hp * 5 * v_hp_pick_count;
        v_auto_quotient := FLOOR(v_auto_numerator::NUMERIC / 100)::INTEGER;
        v_pick_quotient := FLOOR(v_pick_numerator::NUMERIC / 100)::INTEGER;
        v_auto_bonus := v_auto_quotient + CASE
            WHEN (v_auto_numerator % 100) * 2 > 100 THEN 1
            WHEN (v_auto_numerator % 100) * 2 = 100 AND (v_auto_quotient % 2) <> 0 THEN 1
            ELSE 0
        END;
        v_pick_bonus := v_pick_quotient + CASE
            WHEN (v_pick_numerator % 100) * 2 > 100 THEN 1
            WHEN (v_pick_numerator % 100) * 2 = 100 AND (v_pick_quotient % 2) <> 0 THEN 1
            ELSE 0
        END;
        v_max_hp := GREATEST(1, v_row.base_max_hp + v_hp_iv + v_auto_bonus + v_pick_bonus);

        -- NULL is the existing full-health sentinel. KO is intentionally not
        -- revived by passive recovery; defeat respawn owns that separate rule.
        IF v_row.current_hp IS NULL OR v_row.current_hp <= 0 THEN
            CONTINUE;
        END IF;

        -- Full (or legacy over-max) state earns no more healing. We deliberately
        -- do not lower an existing value here: session-only MaxHP effects are
        -- resolved elsewhere, while this resolver only caps the HP it adds.
        IF v_row.current_hp >= v_max_hp THEN
            CONTINUE;
        END IF;

        -- A missing anchor starts at this authoritative read. This prevents an
        -- old or newly captured row from receiving unearned retroactive healing.
        v_recovery_anchor := GREATEST(
            COALESCE(v_row.passive_recovery_at, p_authoritative_time),
            COALESCE(v_row.hp_synced_at, p_authoritative_time)
        );
        v_elapsed_seconds := FLOOR(EXTRACT(EPOCH FROM (p_authoritative_time - v_recovery_anchor)))::BIGINT;
        IF v_elapsed_seconds < v_interval_seconds THEN
            CONTINUE;
        END IF;

        v_completed_ticks := LEAST(
            v_max_catchup_intervals,
            FLOOR(v_elapsed_seconds::NUMERIC / v_interval_seconds)::INTEGER
        );
        IF v_completed_ticks <= 0 THEN
            CONTINUE;
        END IF;

        -- Ceil rounding makes each positive rate meaningful at low MaxHP while
        -- LEAST below guarantees that persistent HP never exceeds canonical max.
        v_heal_per_tick := GREATEST(1, (v_max_hp * v_heal_basis_points + 9999) / 10000);
        v_current_hp := GREATEST(0, v_row.current_hp);
        v_next_hp := LEAST(v_max_hp, v_current_hp + (v_heal_per_tick * v_completed_ticks));
        v_next_anchor := CASE
            WHEN v_next_hp >= v_max_hp THEN p_authoritative_time
            ELSE v_recovery_anchor + make_interval(secs => v_completed_ticks * v_interval_seconds)
        END;

        UPDATE public.chainlings
        SET current_hp = v_next_hp,
            passive_recovery_at = v_next_anchor,
            updated_at = now()
        WHERE id = v_row.id;
        v_updated_count := v_updated_count + 1;
    END LOOP;

    RETURN jsonb_build_object('applied', true, 'updated_chainlings', v_updated_count);
END;
$$;

REVOKE ALL ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) TO service_role;

COMMENT ON FUNCTION public.profile_apply_chainling_passive_recovery_tx(UUID, TIMESTAMPTZ, INTEGER, INTEGER, INTEGER, BOOLEAN) IS
'Service-role passive HP resolver. Timestamp and levers originate in Edge/server config; does not revive KO Chainlings or exceed canonical MaxHP.';;
