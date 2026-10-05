-- Case-insensitive player display names. Does not touch chainling names.
BEGIN;

SELECT plan(7);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES
    ('20000000-0000-0000-0000-000000000001', 'test', 'test'),
    ('20000000-0000-0000-0000-000000000002', 'test', 'test');

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000001',
        'Nova'
    ) ->> 'success')::boolean,
    true,
    'first profile commits Nova'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000002',
        ' NOVA '
    ) ->> 'error'),
    'name_taken',
    'a different profile cannot take the same name ignoring case and surrounding space'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000001',
        'nova'
    ) ->> 'display_name'),
    'nova',
    'the owning profile can re-set its own name, including a case change'
);

SELECT is(
    (SELECT display_name FROM public.player_profiles WHERE player_profile_id = '20000000-0000-0000-0000-000000000002'),
    NULL,
    'the rejected profile keeps a null display name'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000002',
        'Nona'
    ) ->> 'success')::boolean,
    true,
    'a distinct name is accepted'
);

SELECT ok(
    EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND indexname = 'ux_player_profiles_display_name_ci'
    ),
    'case-insensitive unique index exists'
);

SELECT throws_ok(
    $$ INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen, display_name)
       VALUES ('20000000-0000-0000-0000-000000000003', 'test', 'test', 'NONA') $$,
    '23505',
    NULL,
    'the unique index rejects a case-variant insert that bypasses the RPC'
);

SELECT * FROM finish();
ROLLBACK;
