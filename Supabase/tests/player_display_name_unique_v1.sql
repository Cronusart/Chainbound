-- Grandfathered player display names stay in place. New claims are unique.
-- Does not touch chainling names, wallets, or RAM.
BEGIN;

SELECT plan(20);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen, display_name)
VALUES
    ('20000000-0000-0000-0000-000000000001', 'test', 'test', 'tonic'),
    ('20000000-0000-0000-0000-000000000002', 'test', 'test', 'Tonic');

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES
    ('20000000-0000-0000-0000-000000000003', 'test', 'test'),
    ('20000000-0000-0000-0000-000000000004', 'test', 'test');

SELECT is(
    (SELECT count(*)
     FROM public.player_profiles
     WHERE lower(btrim(display_name)) = 'tonic'
       AND display_name_claimed_at IS NULL),
    2::bigint,
    'grandfathered case-insensitive duplicates are stored with a null claim stamp'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000003',
        ' TONIC '
    ) ->> 'error'),
    'name_taken',
    'a new claim is blocked by a grandfathered name'
);

SELECT is(
    (SELECT display_name
     FROM public.player_profiles
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000003'),
    NULL,
    'the rejected new claim does not write a display name'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000001',
        'TONIC'
    ) ->> 'display_name'),
    'TONIC',
    'a grandfathered profile can re-set its own duplicated name'
);

SELECT is(
    (SELECT display_name_claimed_at IS NULL
     FROM public.player_profiles
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    true,
    're-saving a shared grandfathered name does not enter the unique index'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000003',
        'Nova'
    ) ->> 'success')::boolean,
    true,
    'a new claim of an unused name succeeds'
);

SELECT is(
    (SELECT display_name_claimed_at IS NOT NULL
     FROM public.player_profiles
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000003'),
    true,
    'a successful new claim is stamped for the unique index'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000004',
        ' nova '
    ) ->> 'error'),
    'name_taken',
    'a second new claim of that name is rejected'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000003',
        'nova'
    ) ->> 'display_name'),
    'nova',
    'the owning profile can re-set its own name, including a case change'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000002',
        'Pebble'
    ) ->> 'success')::boolean,
    true,
    'a grandfathered profile can claim a free new name'
);

SELECT is(
    (SELECT display_name
     FROM public.player_profiles
     WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    'TONIC',
    'the other grandfathered duplicate keeps its display name'
);

SELECT ok(
    EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND indexname = 'ux_player_profiles_display_name_ci'
          AND indexdef ILIKE '%display_name_claimed_at IS NOT NULL%'
    ),
    'the unique index covers only claimed names'
);

SELECT throws_ok(
    $$ INSERT INTO public.player_profiles (
           player_profile_id, platform_origin, last_platform_seen, display_name, display_name_claimed_at
       )
       VALUES (
           '20000000-0000-0000-0000-000000000005', 'test', 'test', 'NOVA', now()
       ) $$,
    '23505',
    NULL,
    'two enforced rows cannot hold the same name ignoring case'
);

SELECT lives_ok(
    $$ INSERT INTO public.player_profiles (
           player_profile_id, platform_origin, last_platform_seen, display_name
       )
       VALUES (
           '20000000-0000-0000-0000-000000000006', 'test', 'test', 'NOVA'
       ) $$,
    'a grandfathered insert is outside the unique index'
);

SELECT is(
    public.display_name_characters_allowed('Nyx_2'),
    true,
    'digits and underscore are allowed'
);

SELECT is(
    public.display_name_characters_allowed('A.B'),
    true,
    'an interior period is allowed'
);

SELECT is(
    public.display_name_characters_allowed('<size>'),
    false,
    'text mesh pro tags are rejected'
);

SELECT is(
    public.display_name_characters_allowed('Nyx.'),
    false,
    'a trailing special is rejected'
);

SELECT is(
    public.display_name_characters_allowed('Abcdefghijklm'),
    true,
    'a 13-character name is inside the 16-character saved-name limit'
);

SELECT is(
    (public.profile_set_display_name_tx(
        '20000000-0000-0000-0000-000000000004',
        '<size=40>Nyx'
    ) ->> 'error'),
    'invalid_display_name',
    'a direct call cannot store a text mesh pro tag'
);

SELECT * FROM finish();
ROLLBACK;
