-- Chainbound - keep the temporary intro_complete profile mirror without
-- touching the removed wallet-era players model.

CREATE OR REPLACE FUNCTION public.profile_apply_quest_state_tx(
    p_profile_id UUID,
    p_mutation_id TEXT,
    p_expected_revision BIGINT,
    p_quest_id TEXT,
    p_lifecycle TEXT,
    p_objective_progress JSONB,
    p_resolution_id TEXT,
    p_resolution_context JSONB,
    p_content_version INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_existing public.profile_quest_states%ROWTYPE;
    v_saved public.profile_quest_states%ROWTYPE;
    v_prior_result JSONB;
    v_result JSONB;
    v_current_revision BIGINT := 0;
    v_normalized_resolution_id TEXT := NULLIF(BTRIM(COALESCE(p_resolution_id, '')), '');
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::TEXT, '') || ':quest:' || COALESCE(p_quest_id, ''),
        0
    ));

    SELECT result_json INTO v_prior_result
    FROM public.profile_narrative_mutations
    WHERE player_profile_id = p_profile_id
      AND mutation_id = p_mutation_id;

    IF FOUND THEN
        RETURN v_prior_result || jsonb_build_object('duplicate', true);
    END IF;

    IF p_profile_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM public.player_profiles WHERE player_profile_id = p_profile_id
    ) THEN
        RETURN jsonb_build_object('success', false, 'error', 'profile_not_found');
    END IF;

    IF COALESCE(p_mutation_id, '') !~ '^[A-Za-z0-9_-]{8,128}$' THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_mutation_id');
    END IF;

    IF COALESCE(p_quest_id, '') !~ '^[a-z][a-z0-9_]{1,95}$' THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_quest_id');
    END IF;

    IF p_lifecycle NOT IN ('locked', 'available', 'active', 'completed', 'failed') THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_lifecycle');
    END IF;

    IF jsonb_typeof(COALESCE(p_objective_progress, '{}'::jsonb)) <> 'object'
       OR jsonb_typeof(COALESCE(p_resolution_context, '{}'::jsonb)) <> 'object' THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_json_shape');
    END IF;

    IF p_lifecycle = 'completed' AND v_normalized_resolution_id IS NULL THEN
        RETURN jsonb_build_object('success', false, 'error', 'completed_resolution_required');
    END IF;

    SELECT * INTO v_existing
    FROM public.profile_quest_states
    WHERE player_profile_id = p_profile_id
      AND quest_id = p_quest_id
    FOR UPDATE;

    IF FOUND THEN
        v_current_revision := v_existing.revision;

        IF v_existing.lifecycle = 'completed' THEN
            IF p_lifecycle = 'completed'
               AND COALESCE(v_existing.resolution_id, '') = COALESCE(v_normalized_resolution_id, '')
               AND v_existing.resolution_context = COALESCE(p_resolution_context, '{}'::jsonb) THEN
                v_result := jsonb_build_object(
                    'success', true,
                    'applied', false,
                    'terminalNoop', true,
                    'questState', public.profile_quest_state_json(v_existing)
                );
                INSERT INTO public.profile_narrative_mutations (
                    player_profile_id, mutation_id, mutation_kind, result_json
                ) VALUES (p_profile_id, p_mutation_id, 'quest_state', v_result);
                RETURN v_result;
            END IF;

            RETURN jsonb_build_object(
                'success', false,
                'error', 'completed_quest_immutable',
                'currentRevision', v_existing.revision
            );
        END IF;
    END IF;

    IF GREATEST(0, COALESCE(p_expected_revision, 0)) <> v_current_revision THEN
        RETURN jsonb_build_object(
            'success', false,
            'error', 'revision_conflict',
            'currentRevision', v_current_revision
        );
    END IF;

    INSERT INTO public.profile_quest_states (
        player_profile_id,
        quest_id,
        lifecycle,
        objective_progress,
        resolution_id,
        resolution_context,
        revision,
        content_version,
        resolved_at
    ) VALUES (
        p_profile_id,
        p_quest_id,
        p_lifecycle,
        COALESCE(p_objective_progress, '{}'::jsonb),
        v_normalized_resolution_id,
        COALESCE(p_resolution_context, '{}'::jsonb),
        v_current_revision + 1,
        GREATEST(1, COALESCE(p_content_version, 1)),
        CASE WHEN p_lifecycle = 'completed' THEN now() ELSE NULL END
    )
    ON CONFLICT (player_profile_id, quest_id) DO UPDATE
    SET lifecycle = EXCLUDED.lifecycle,
        objective_progress = EXCLUDED.objective_progress,
        resolution_id = EXCLUDED.resolution_id,
        resolution_context = EXCLUDED.resolution_context,
        revision = EXCLUDED.revision,
        content_version = EXCLUDED.content_version,
        resolved_at = COALESCE(public.profile_quest_states.resolved_at, EXCLUDED.resolved_at)
    RETURNING * INTO v_saved;

    IF p_quest_id = 'chainbound_intro' AND p_lifecycle = 'completed' THEN
        UPDATE public.player_profiles
        SET intro_complete = true,
            has_started_game = true,
            updated_at = now()
        WHERE player_profile_id = p_profile_id;
    END IF;

    v_result := jsonb_build_object(
        'success', true,
        'applied', true,
        'questState', public.profile_quest_state_json(v_saved)
    );

    INSERT INTO public.profile_narrative_mutations (
        player_profile_id, mutation_id, mutation_kind, result_json
    ) VALUES (p_profile_id, p_mutation_id, 'quest_state', v_result);

    RETURN v_result;
END;
$$;
GRANT EXECUTE ON FUNCTION public.profile_apply_quest_state_tx(
    UUID, TEXT, BIGINT, TEXT, TEXT, JSONB, TEXT, JSONB, INTEGER
) TO service_role;
