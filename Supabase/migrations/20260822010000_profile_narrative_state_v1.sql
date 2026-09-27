-- Chainbound - persistent profile narrative state v1.
-- Quest lifecycle/objectives/resolutions and resumable sequence checkpoints are
-- profile-scoped, revisioned, and mutated through idempotent service-role RPCs.

CREATE TABLE IF NOT EXISTS public.profile_quest_states (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    quest_id TEXT NOT NULL,
    lifecycle TEXT NOT NULL,
    objective_progress JSONB NOT NULL DEFAULT '{}'::jsonb,
    resolution_id TEXT NULL,
    resolution_context JSONB NOT NULL DEFAULT '{}'::jsonb,
    revision BIGINT NOT NULL DEFAULT 0,
    content_version INTEGER NOT NULL DEFAULT 1,
    resolved_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, quest_id),
    CONSTRAINT profile_quest_states_quest_id_format
        CHECK (quest_id ~ '^[a-z][a-z0-9_]{1,95}$'),
    CONSTRAINT profile_quest_states_lifecycle
        CHECK (lifecycle IN ('locked', 'available', 'active', 'completed', 'failed')),
    CONSTRAINT profile_quest_states_objectives_object
        CHECK (jsonb_typeof(objective_progress) = 'object'),
    CONSTRAINT profile_quest_states_resolution_context_object
        CHECK (jsonb_typeof(resolution_context) = 'object'),
    CONSTRAINT profile_quest_states_completed_resolution
        CHECK (lifecycle <> 'completed' OR NULLIF(BTRIM(COALESCE(resolution_id, '')), '') IS NOT NULL),
    CONSTRAINT profile_quest_states_revision_nonnegative CHECK (revision >= 0),
    CONSTRAINT profile_quest_states_content_version_positive CHECK (content_version >= 1)
);
CREATE INDEX IF NOT EXISTS idx_profile_quest_states_profile_lifecycle
    ON public.profile_quest_states (player_profile_id, lifecycle);
CREATE TABLE IF NOT EXISTS public.profile_sequence_checkpoints (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    sequence_id TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'active',
    checkpoint_id TEXT NOT NULL DEFAULT '',
    checkpoint_label TEXT NOT NULL DEFAULT '',
    last_completed_label TEXT NOT NULL DEFAULT '',
    checkpoint_step_index INTEGER NOT NULL DEFAULT -1,
    scene_id TEXT NOT NULL DEFAULT '',
    spawn_id TEXT NOT NULL DEFAULT '',
    persistent_variables JSONB NOT NULL DEFAULT '{}'::jsonb,
    completed_step_guids JSONB NOT NULL DEFAULT '[]'::jsonb,
    played_gate_keys JSONB NOT NULL DEFAULT '[]'::jsonb,
    revision BIGINT NOT NULL DEFAULT 0,
    sequence_version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, sequence_id),
    CONSTRAINT profile_sequence_checkpoints_sequence_id_format
        CHECK (sequence_id ~ '^[a-z][a-z0-9_]{1,95}$'),
    CONSTRAINT profile_sequence_checkpoints_status
        CHECK (status IN ('active', 'completed', 'abandoned')),
    CONSTRAINT profile_sequence_checkpoints_variables_object
        CHECK (jsonb_typeof(persistent_variables) = 'object'),
    CONSTRAINT profile_sequence_checkpoints_step_guids_array
        CHECK (jsonb_typeof(completed_step_guids) = 'array'),
    CONSTRAINT profile_sequence_checkpoints_gate_keys_array
        CHECK (jsonb_typeof(played_gate_keys) = 'array'),
    CONSTRAINT profile_sequence_checkpoints_revision_nonnegative CHECK (revision >= 0),
    CONSTRAINT profile_sequence_checkpoints_version_positive CHECK (sequence_version >= 1)
);
CREATE INDEX IF NOT EXISTS idx_profile_sequence_checkpoints_profile_status
    ON public.profile_sequence_checkpoints (player_profile_id, status);
CREATE TABLE IF NOT EXISTS public.profile_narrative_mutations (
    player_profile_id UUID NOT NULL REFERENCES public.player_profiles(player_profile_id) ON DELETE CASCADE,
    mutation_id TEXT NOT NULL,
    mutation_kind TEXT NOT NULL,
    result_json JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (player_profile_id, mutation_id),
    CONSTRAINT profile_narrative_mutations_id_format
        CHECK (mutation_id ~ '^[A-Za-z0-9_-]{8,128}$'),
    CONSTRAINT profile_narrative_mutations_kind
        CHECK (mutation_kind IN ('quest_state', 'sequence_checkpoint')),
    CONSTRAINT profile_narrative_mutations_result_object
        CHECK (jsonb_typeof(result_json) = 'object')
);
CREATE INDEX IF NOT EXISTS idx_profile_narrative_mutations_profile_created
    ON public.profile_narrative_mutations (player_profile_id, created_at DESC);
ALTER TABLE public.profile_quest_states ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_sequence_checkpoints ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.profile_narrative_mutations ENABLE ROW LEVEL SECURITY;
CREATE OR REPLACE FUNCTION public.chainbound_touch_narrative_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;
DROP TRIGGER IF EXISTS trg_profile_quest_states_touch ON public.profile_quest_states;
CREATE TRIGGER trg_profile_quest_states_touch
    BEFORE UPDATE ON public.profile_quest_states
    FOR EACH ROW EXECUTE FUNCTION public.chainbound_touch_narrative_updated_at();
DROP TRIGGER IF EXISTS trg_profile_sequence_checkpoints_touch ON public.profile_sequence_checkpoints;
CREATE TRIGGER trg_profile_sequence_checkpoints_touch
    BEFORE UPDATE ON public.profile_sequence_checkpoints
    FOR EACH ROW EXECUTE FUNCTION public.chainbound_touch_narrative_updated_at();
CREATE OR REPLACE FUNCTION public.profile_quest_state_json(p_row public.profile_quest_states)
RETURNS JSONB
LANGUAGE sql
STABLE
AS $$
    SELECT jsonb_build_object(
        'questId', p_row.quest_id,
        'lifecycle', p_row.lifecycle,
        'objectiveProgress', p_row.objective_progress,
        'resolutionId', COALESCE(p_row.resolution_id, ''),
        'resolutionContext', p_row.resolution_context,
        'revision', p_row.revision,
        'contentVersion', p_row.content_version,
        'resolvedAtUtc', COALESCE(to_char(p_row.resolved_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'), ''),
        'updatedAtUtc', to_char(p_row.updated_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
    );
$$;
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
    -- Serialize every mutation for this profile quest, including the first write
    -- where no row exists yet for SELECT ... FOR UPDATE to lock. This keeps
    -- cross-device revision checks authoritative while the mutation ledger still
    -- provides request-level idempotency.
    PERFORM pg_advisory_xact_lock(hashtextextended(
        COALESCE(p_profile_id::text, '') || ':quest:' || COALESCE(p_quest_id, ''),
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
            has_started_game = true
        WHERE player_profile_id = p_profile_id;

        IF EXISTS (
            SELECT 1 FROM public.player_profiles
            WHERE player_profile_id = p_profile_id AND legacy_player_id IS NOT NULL
        ) THEN
            UPDATE public.players p
            SET intro_complete = true
            FROM public.player_profiles pp
            WHERE pp.player_profile_id = p_profile_id
              AND pp.legacy_player_id = p.id;
        END IF;
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
        'completedStepGuids', p_row.completed_step_guids,
        'playedGateKeys', p_row.played_gate_keys,
        'revision', p_row.revision,
        'sequenceVersion', p_row.sequence_version,
        'updatedAtUtc', to_char(p_row.updated_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
    );
$$;
CREATE OR REPLACE FUNCTION public.profile_apply_sequence_checkpoint_tx(
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
    -- Sequence checkpoint revisions need the same first-write serialization as
    -- quest state; distinct devices may checkpoint the same sequence concurrently.
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
GRANT EXECUTE ON FUNCTION public.profile_apply_quest_state_tx(
    UUID, TEXT, BIGINT, TEXT, TEXT, JSONB, TEXT, JSONB, INTEGER
) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_apply_sequence_checkpoint_tx(
    UUID, TEXT, BIGINT, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, TEXT, TEXT, JSONB, JSONB, JSONB, INTEGER
) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_quest_state_json(public.profile_quest_states) TO service_role;
GRANT EXECUTE ON FUNCTION public.profile_sequence_checkpoint_json(public.profile_sequence_checkpoints) TO service_role;
-- Compatibility backfill. Historical resolution context is intentionally not
-- guessed; future clients provide it when completing a quest.
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
)
SELECT
    pp.player_profile_id,
    'chainbound_intro',
    'completed',
    '{}'::jsonb,
    CASE WHEN starter.match_count = 1 THEN 'chainling_bound' ELSE 'legacy_complete' END,
    CASE
        WHEN starter.match_count = 1
            THEN jsonb_build_object('starter_species_id', starter.species_id)
        ELSE '{}'::jsonb
    END,
    1,
    1,
    COALESCE(pp.updated_at, now())
FROM public.player_profiles pp
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::INTEGER AS match_count,
        MIN(NULLIF(BTRIM(c.species_id), '')) AS species_id
    FROM public.chainlings c
    WHERE c.is_party_member = true
      AND c.party_slot = 0
      AND NULLIF(BTRIM(c.species_id), '') IS NOT NULL
      AND (
          c.player_profile_id = pp.player_profile_id
          OR (c.player_profile_id IS NULL AND c.player_id = pp.legacy_player_id)
      )
) starter ON true
WHERE pp.intro_complete = true
ON CONFLICT (player_profile_id, quest_id) DO NOTHING;
INSERT INTO public.profile_quest_states (
    player_profile_id,
    quest_id,
    lifecycle,
    objective_progress,
    resolution_id,
    resolution_context,
    revision,
    content_version
)
SELECT
    pp.player_profile_id,
    'coffee_and_beer',
    'available',
    '{}'::jsonb,
    NULL,
    '{}'::jsonb,
    1,
    1
FROM public.player_profiles pp
WHERE pp.intro_complete = true
ON CONFLICT (player_profile_id, quest_id) DO NOTHING;
