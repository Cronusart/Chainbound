BEGIN;

SELECT plan(13);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'test', 'test'),
    ('10000000-0000-0000-0000-000000000002', 'test', 'test');

CREATE TEMP TABLE narrative_results (name TEXT PRIMARY KEY, payload JSONB);

INSERT INTO narrative_results VALUES (
    'active',
    public.profile_apply_quest_state_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-mutation-active-001',
        0,
        'chainbound_intro',
        'active',
        '{"choose_starter":0}'::jsonb,
        NULL,
        '{}'::jsonb,
        1
    )
);

INSERT INTO narrative_results VALUES (
    'complete',
    public.profile_apply_quest_state_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-mutation-complete-002',
        1,
        'chainbound_intro',
        'completed',
        '{"choose_starter":1,"dispatch_to_reynard":1}'::jsonb,
        'chainling_bound',
        '{"starter_species_id":"kitsauto"}'::jsonb,
        1
    )
);

INSERT INTO narrative_results VALUES (
    'duplicate',
    public.profile_apply_quest_state_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-mutation-complete-002',
        1,
        'chainbound_intro',
        'completed',
        '{"choose_starter":1,"dispatch_to_reynard":1}'::jsonb,
        'chainling_bound',
        '{"starter_species_id":"kitsauto"}'::jsonb,
        1
    )
);

INSERT INTO narrative_results VALUES (
    'regression',
    public.profile_apply_quest_state_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-mutation-regress-003',
        2,
        'chainbound_intro',
        'active',
        '{"choose_starter":1}'::jsonb,
        NULL,
        '{}'::jsonb,
        1
    )
);

SELECT is((SELECT payload->>'success' FROM narrative_results WHERE name = 'active'), 'true', 'active quest mutation succeeds');
SELECT is((SELECT payload->'questState'->>'revision' FROM narrative_results WHERE name = 'active'), '1', 'first quest revision is one');
SELECT is((SELECT payload->>'success' FROM narrative_results WHERE name = 'complete'), 'true', 'completion transaction succeeds');
SELECT is((SELECT payload->'questState'->>'lifecycle' FROM narrative_results WHERE name = 'complete'), 'completed', 'completion stores terminal lifecycle');
SELECT is((SELECT payload->'questState'->>'resolutionId' FROM narrative_results WHERE name = 'complete'), 'chainling_bound', 'completion stores resolution id');
SELECT is((SELECT payload->'questState'->'resolutionContext'->>'starter_species_id' FROM narrative_results WHERE name = 'complete'), 'kitsauto', 'completion stores resolution context atomically');
SELECT is((SELECT payload->>'duplicate' FROM narrative_results WHERE name = 'duplicate'), 'true', 'duplicate mutation id is idempotent');
SELECT is((SELECT payload->>'error' FROM narrative_results WHERE name = 'regression'), 'completed_quest_immutable', 'completed quest cannot regress');
SELECT ok((SELECT intro_complete FROM public.player_profiles WHERE player_profile_id = '10000000-0000-0000-0000-000000000001'), 'intro compatibility mirror updates in completion transaction');
SELECT is((SELECT COUNT(*)::TEXT FROM public.profile_quest_states WHERE player_profile_id = '10000000-0000-0000-0000-000000000002'), '0', 'quest state is profile isolated');

INSERT INTO narrative_results VALUES (
    'checkpoint',
    public.profile_apply_sequence_checkpoint_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-checkpoint-active-004',
        0,
        'chainbound_intro_sequence',
        'active',
        'intro_before_choose_starter',
        'choosestarter',
        'choosestarter',
        76,
        'loom_lab',
        'loom_lab_entry',
        '{"starter_species_id":"kitsauto"}'::jsonb,
        '["step-guid-1"]'::jsonb,
        '["intro_gate"]'::jsonb,
        '{"intro.guardrail.house":false}'::jsonb,
        1
    )
);

INSERT INTO narrative_results VALUES (
    'checkpoint_stale',
    public.profile_apply_sequence_checkpoint_tx(
        '10000000-0000-0000-0000-000000000001',
        'test-checkpoint-stale-005',
        0,
        'chainbound_intro_sequence',
        'active',
        'intro_after_choose_starter',
        'choosestarter',
        'choosestarter',
        80,
        'loom_lab',
        'loom_lab_entry',
        '{}'::jsonb,
        '[]'::jsonb,
        '[]'::jsonb,
        '{}'::jsonb,
        1
    )
);

SELECT is((SELECT payload->'sequenceCheckpoint'->>'checkpointId' FROM narrative_results WHERE name = 'checkpoint'), 'intro_before_choose_starter', 'checkpoint id is authoritative state');
SELECT is((SELECT payload->'sequenceCheckpoint'->'persistentVariables'->>'starter_species_id' FROM narrative_results WHERE name = 'checkpoint'), 'kitsauto', 'checkpoint preserves persistent variables');
SELECT is((SELECT payload->'sequenceCheckpoint'->'railFlagSnapshot'->>'intro.guardrail.house' FROM narrative_results WHERE name = 'checkpoint'), 'false', 'checkpoint preserves rail flag snapshot');
SELECT is((SELECT payload->>'error' FROM narrative_results WHERE name = 'checkpoint_stale'), 'revision_conflict', 'stale checkpoint revision is rejected');

SELECT * FROM finish();
ROLLBACK;
