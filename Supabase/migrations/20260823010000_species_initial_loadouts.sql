-- Chainbound - data-authored initial species loadouts and atomic starter grants.
-- Initial loadouts are stable content IDs; profile-owned move items remain the
-- authoritative equipped state after acquisition.

CREATE TABLE IF NOT EXISTS public.species_initial_loadouts (
    species_id TEXT NOT NULL,
    slot SMALLINT NOT NULL,
    move_id TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (species_id, slot),
    CONSTRAINT species_initial_loadouts_species_id_format
        CHECK (species_id ~ '^[a-z][a-z0-9_]{1,95}$'),
    CONSTRAINT species_initial_loadouts_move_id_format
        CHECK (move_id ~ '^[a-z][a-z0-9_]{1,95}$'),
    CONSTRAINT species_initial_loadouts_slot
        CHECK (slot >= 0 AND slot < 16)
);
ALTER TABLE public.species_initial_loadouts ENABLE ROW LEVEL SECURITY;
INSERT INTO public.species_initial_loadouts (species_id, slot, move_id)
VALUES
    ('validape', 0, 'baseline_striker'),
    ('gilarime', 0, 'baseline_shot'),
    ('kitsauto', 0, 'baseline_jab')
ON CONFLICT (species_id, slot) DO UPDATE
SET move_id = EXCLUDED.move_id,
    updated_at = now();
CREATE OR REPLACE FUNCTION public.profile_commit_starter_tx(
    p_profile_id UUID,
    p_species_id TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_profile public.player_profiles%ROWTYPE;
    v_chainling public.chainlings%ROWTYPE;
    v_initial RECORD;
    v_move_item_id UUID;
    v_created BOOLEAN := false;
    v_species_id TEXT := NULLIF(BTRIM(COALESCE(p_species_id, '')), '');
    v_move_ids JSONB := '[]'::jsonb;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::TEXT, '') || ':starter',
        0
    ));

    SELECT *
    INTO v_profile
    FROM public.player_profiles
    WHERE player_profile_id = p_profile_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    IF v_profile.legacy_player_id IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'legacy_player_id_required');
    END IF;

    IF v_species_id IS NULL OR NOT EXISTS (
        SELECT 1
        FROM public.species_initial_loadouts
        WHERE species_id = v_species_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_starter_species');
    END IF;

    SELECT *
    INTO v_chainling
    FROM public.chainlings
    WHERE player_profile_id = p_profile_id
      AND is_party_member = true
      AND party_slot = 0
    LIMIT 1
    FOR UPDATE;

    IF NOT FOUND THEN
        INSERT INTO public.chainlings (
            player_profile_id,
            player_id,
            species_id,
            is_party_member,
            party_slot,
            level,
            xp,
            ivs,
            rebirth_iv_bonuses,
            stars,
            rebirth_count,
            evolution_stage,
            visual_variant_id,
            visual_identity
        ) VALUES (
            p_profile_id,
            v_profile.legacy_player_id,
            v_species_id,
            true,
            0,
            1,
            0,
            '[0,0,0,0,0,0]'::jsonb,
            '[0,0,0,0,0,0]'::jsonb,
            0,
            0,
            1,
            'base',
            jsonb_build_object(
                'aura_id', 'none',
                'hud_card_variant_id', 'default',
                'swap_in_fx_id', 'default',
                'swap_out_fx_id', 'default',
                'ko_fx_id', 'none',
                'victory_pose_id', 'default',
                'idle_style_id', 'none',
                'idle_flourish_id', 'none',
                'melee_attack_style_id', 'none',
                'ranged_attack_style_id', 'none',
                'hit_reaction_style_id', 'none',
                'attacker_ko_style_id', 'none'
            )
        )
        RETURNING * INTO v_chainling;

        v_created := true;
    END IF;

    -- Existing idempotent commits retain their originally selected species.
    -- Missing initial slots are repaired, but player-authored occupied slots are
    -- never overwritten.
    FOR v_initial IN
        SELECT slot, move_id
        FROM public.species_initial_loadouts
        WHERE species_id = v_chainling.species_id
        ORDER BY slot
    LOOP
        IF EXISTS (
            SELECT 1
            FROM public.loadouts
            WHERE chainling_id = v_chainling.id
              AND slot = v_initial.slot
              AND move_item_id IS NOT NULL
        ) THEN
            CONTINUE;
        END IF;

        INSERT INTO public.move_items (
            player_id,
            tier,
            move_id,
            modifier1_id,
            modifier2_id,
            locked,
            exported_asset_id,
            source
        ) VALUES (
            v_profile.legacy_player_id,
            'ordinal'::public.move_tier,
            v_initial.move_id,
            NULL,
            NULL,
            false,
            NULL,
            'starter_initial'
        )
        RETURNING id INTO v_move_item_id;

        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_chainling.id, v_initial.slot, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO UPDATE
        SET move_item_id = COALESCE(public.loadouts.move_item_id, EXCLUDED.move_item_id);
    END LOOP;

    UPDATE public.player_profiles
    SET has_started_game = true,
        updated_at = now()
    WHERE player_profile_id = p_profile_id;

    SELECT COALESCE(jsonb_agg(mi.move_id ORDER BY l.slot), '[]'::jsonb)
    INTO v_move_ids
    FROM public.loadouts l
    JOIN public.move_items mi ON mi.id = l.move_item_id
    WHERE l.chainling_id = v_chainling.id;

    RETURN jsonb_build_object(
        'success', true,
        'player_profile_id', p_profile_id,
        'playerId', v_profile.legacy_player_id,
        'chainling_id', v_chainling.id,
        'species_id', v_chainling.species_id,
        'move_ids', v_move_ids,
        'created', v_created
    );
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_commit_starter_tx(UUID, TEXT) TO service_role;
-- Repair initial loadouts for pre-migration starter profiles where the intro is
-- already complete. Existing occupied loadout slots are preserved.
DO $$
DECLARE
    v_candidate RECORD;
    v_move_item_id UUID;
BEGIN
    FOR v_candidate IN
        SELECT
            c.id AS chainling_id,
            p.legacy_player_id,
            sil.slot,
            sil.move_id
        FROM public.chainlings c
        JOIN public.player_profiles p
          ON p.player_profile_id = c.player_profile_id
        JOIN public.species_initial_loadouts sil
          ON sil.species_id = c.species_id
        WHERE c.is_party_member = true
          AND c.party_slot = 0
          AND p.legacy_player_id IS NOT NULL
          AND (
              COALESCE(p.intro_complete, false) = true
              OR EXISTS (
                  SELECT 1
                  FROM public.profile_quest_states qs
                  WHERE qs.player_profile_id = c.player_profile_id
                    AND qs.quest_id = 'chainbound_intro'
                    AND qs.lifecycle = 'completed'
                    AND COALESCE(qs.resolution_context ->> 'starter_species_id', c.species_id) = c.species_id
              )
          )
          AND NOT EXISTS (
              SELECT 1
              FROM public.loadouts l
              WHERE l.chainling_id = c.id
                AND l.slot = sil.slot
                AND l.move_item_id IS NOT NULL
          )
    LOOP
        INSERT INTO public.move_items (
            player_id,
            tier,
            move_id,
            modifier1_id,
            modifier2_id,
            locked,
            exported_asset_id,
            source
        ) VALUES (
            v_candidate.legacy_player_id,
            'ordinal'::public.move_tier,
            v_candidate.move_id,
            NULL,
            NULL,
            false,
            NULL,
            'starter_initial_backfill'
        )
        RETURNING id INTO v_move_item_id;

        INSERT INTO public.loadouts (chainling_id, slot, move_item_id)
        VALUES (v_candidate.chainling_id, v_candidate.slot, v_move_item_id)
        ON CONFLICT (chainling_id, slot) DO NOTHING;
    END LOOP;
END;
$$;
