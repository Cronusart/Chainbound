-- Rail presence snapshot on sequence checkpoints. Durable world flags stay on
-- progression_state; this column only stores sequence-rail keys captured at
-- Step_SetCheckpoint so continue-game can rewind later rails.

ALTER TABLE public.profile_sequence_checkpoints
    ADD COLUMN IF NOT EXISTS rail_flag_snapshot JSONB NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE public.profile_sequence_checkpoints
    DROP CONSTRAINT IF EXISTS profile_sequence_checkpoints_rail_flag_snapshot_object;

ALTER TABLE public.profile_sequence_checkpoints
    ADD CONSTRAINT profile_sequence_checkpoints_rail_flag_snapshot_object
    CHECK (jsonb_typeof(rail_flag_snapshot) = 'object');

CREATE OR REPLACE FUNCTION public.profile_sequence_checkpoint_json(p_row public.profile_sequence_checkpoints)
RETURNS JSONB
LANGUAGE sql
STABLE
AS $$
    SELECT jsonb_build_object(
        'sequenceId', p_row.sequence_id,
        'status', p_row.status,
        'checkpointId', p_row.checkpoint_id,
        'checkpointLabel', p_row.checkpoint_label,
        'lastCompletedLabel', p_row.last_completed_label,
        'checkpointStepIndex', p_row.checkpoint_step_index,
        'sceneId', p_row.scene_id,
        'spawnId', p_row.spawn_id,
        'persistentVariables', p_row.persistent_variables,
        'railFlagSnapshot', p_row.rail_flag_snapshot,
        'completedStepGuids', p_row.completed_step_guids,
        'playedGateKeys', p_row.played_gate_keys,
        'revision', p_row.revision,
        'sequenceVersion', p_row.sequence_version,
        'updatedAtUtc', to_char(p_row.updated_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
    );
$$;

DROP FUNCTION IF EXISTS public.profile_apply_sequence_checkpoint_tx(
    UUID, TEXT, BIGINT, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, TEXT, TEXT, JSONB, JSONB, JSONB, INTEGER
);

CREATE FUNCTION public.profile_apply_sequence_checkpoint_tx(
    p_profile_id UUID,
    p_mutation_id TEXT,
    p_expected_revision BIGINT,
    p_sequence_id TEXT,
    p_status TEXT,
    p_checkpoint_id TEXT,
    p_checkpoint_label TEXT,
    p_last_completed_label TEXT,
    p_checkpoint_step_index INTEGER,
    p_scene_id TEXT,
    p_spawn_id TEXT,
    p_persistent_variables JSONB,
    p_completed_step_guids JSONB,
    p_played_gate_keys JSONB,
    p_rail_flag_snapshot JSONB,
    p_sequence_version INTEGER
)
RETURNS JSONB
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
DECLARE
    v_existing public.profile_sequence_checkpoints%ROWTYPE;
    v_saved public.profile_sequence_checkpoints%ROWTYPE;
    v_prior_result JSONB;
    v_result JSONB;
    v_current_revision BIGINT := 0;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::text, '') || ':sequence:' || COALESCE(p_sequence_id, ''),
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

    IF COALESCE(p_sequence_id, '') !~ '^[a-z][a-z0-9_]{1,95}$'
       OR p_status NOT IN ('active', 'completed', 'abandoned') THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_sequence_state');
    END IF;

    IF jsonb_typeof(COALESCE(p_persistent_variables, '{}'::jsonb)) <> 'object'
       OR jsonb_typeof(COALESCE(p_rail_flag_snapshot, '{}'::jsonb)) <> 'object'
       OR jsonb_typeof(COALESCE(p_completed_step_guids, '[]'::jsonb)) <> 'array'
       OR jsonb_typeof(COALESCE(p_played_gate_keys, '[]'::jsonb)) <> 'array' THEN
        RETURN jsonb_build_object('success', false, 'error', 'invalid_json_shape');
    END IF;

    SELECT * INTO v_existing
    FROM public.profile_sequence_checkpoints
    WHERE player_profile_id = p_profile_id
      AND sequence_id = p_sequence_id
    FOR UPDATE;

    IF FOUND THEN
        v_current_revision := v_existing.revision;
        IF v_existing.status = 'completed' AND p_status = 'active' THEN
            RETURN jsonb_build_object(
                'success', false,
                'error', 'completed_sequence_immutable',
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

    INSERT INTO public.profile_sequence_checkpoints (
        player_profile_id,
        sequence_id,
        status,
        checkpoint_id,
        checkpoint_label,
        last_completed_label,
        checkpoint_step_index,
        scene_id,
        spawn_id,
        persistent_variables,
        completed_step_guids,
        played_gate_keys,
        rail_flag_snapshot,
        revision,
        sequence_version
    ) VALUES (
        p_profile_id,
        p_sequence_id,
        p_status,
        COALESCE(p_checkpoint_id, ''),
        COALESCE(p_checkpoint_label, ''),
        COALESCE(p_last_completed_label, ''),
        COALESCE(p_checkpoint_step_index, -1),
        COALESCE(p_scene_id, ''),
        COALESCE(p_spawn_id, ''),
        COALESCE(p_persistent_variables, '{}'::jsonb),
        COALESCE(p_completed_step_guids, '[]'::jsonb),
        COALESCE(p_played_gate_keys, '[]'::jsonb),
        COALESCE(p_rail_flag_snapshot, '{}'::jsonb),
        v_current_revision + 1,
        GREATEST(1, COALESCE(p_sequence_version, 1))
    )
    ON CONFLICT (player_profile_id, sequence_id) DO UPDATE
    SET status = EXCLUDED.status,
        checkpoint_id = EXCLUDED.checkpoint_id,
        checkpoint_label = EXCLUDED.checkpoint_label,
        last_completed_label = EXCLUDED.last_completed_label,
        checkpoint_step_index = EXCLUDED.checkpoint_step_index,
        scene_id = EXCLUDED.scene_id,
        spawn_id = EXCLUDED.spawn_id,
        persistent_variables = EXCLUDED.persistent_variables,
        completed_step_guids = EXCLUDED.completed_step_guids,
        played_gate_keys = EXCLUDED.played_gate_keys,
        rail_flag_snapshot = EXCLUDED.rail_flag_snapshot,
        revision = EXCLUDED.revision,
        sequence_version = EXCLUDED.sequence_version
    RETURNING * INTO v_saved;

    v_result := jsonb_build_object(
        'success', true,
        'applied', true,
        'sequenceCheckpoint', public.profile_sequence_checkpoint_json(v_saved)
    );

    INSERT INTO public.profile_narrative_mutations (
        player_profile_id, mutation_id, mutation_kind, result_json
    ) VALUES (p_profile_id, p_mutation_id, 'sequence_checkpoint', v_result);

    RETURN v_result;
END;
$$;

GRANT EXECUTE ON FUNCTION public.profile_apply_sequence_checkpoint_tx(
    UUID, TEXT, BIGINT, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, TEXT, TEXT, JSONB, JSONB, JSONB, JSONB, INTEGER
) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_sequence_checkpoint_json(public.profile_sequence_checkpoints) TO service_role;
