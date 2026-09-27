BEGIN;

SELECT plan(24);

SELECT ok(
    to_regprocedure('public.profile_commit_starter_tx(uuid,text)') IS NULL,
    'starter commit exposes one unambiguous PostgREST signature'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.profile_commit_starter_tx(uuid,text,text)',
        'EXECUTE'
    ),
    'authenticated clients cannot invoke the security-definer starter transaction directly'
);

INSERT INTO public.player_profiles (
    player_profile_id,
    platform_origin,
    last_platform_seen
) VALUES (
    '20000000-0000-0000-0000-000000000001',
    'test',
    'test'
);

CREATE TEMP TABLE profile_native_results (
    name TEXT PRIMARY KEY,
    payload JSONB
);

INSERT INTO profile_native_results VALUES (
    'starter',
    public.profile_commit_starter_tx(
        '20000000-0000-0000-0000-000000000001',
        'gilarime',
        'Glimmer'
    )
);

INSERT INTO profile_native_results VALUES (
    'starter_duplicate',
    public.profile_commit_starter_tx(
        '20000000-0000-0000-0000-000000000001',
        'validape',
        'Overwrite Attempt'
    )
);

SELECT is(
    (SELECT payload->>'success' FROM profile_native_results WHERE name = 'starter'),
    'true',
    'starter commit succeeds without a wallet or legacy player'
);

SELECT is(
    (
        SELECT display_name
        FROM public.chainlings
        WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'
    ),
    'Glimmer',
    'starter commit persists the supplied chainling display name'
);

SELECT is(
    (
        SELECT display_name
        FROM public.chainlings
        WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'
    ),
    'Glimmer',
    'idempotent starter repeat never overwrites a set display name'
);

INSERT INTO public.player_profiles (
    player_profile_id,
    platform_origin,
    last_platform_seen
) VALUES (
    '20000000-0000-0000-0000-000000000002',
    'test',
    'test'
);

INSERT INTO profile_native_results VALUES (
    'starter_unnamed',
    public.profile_commit_starter_tx(
        '20000000-0000-0000-0000-000000000002',
        'gilarime',
        NULL
    )
);

INSERT INTO profile_native_results VALUES (
    'starter_backfill',
    public.profile_commit_starter_tx(
        '20000000-0000-0000-0000-000000000002',
        'validape',
        'Backfilled'
    )
);

SELECT is(
    (
        SELECT display_name
        FROM public.chainlings
        WHERE player_profile_id = '20000000-0000-0000-0000-000000000002'
    ),
    'Backfilled',
    'an older unnamed starter receives a display-name backfill'
);

SELECT is(
    (SELECT payload->>'species_id' FROM profile_native_results WHERE name = 'starter'),
    'gilarime',
    'starter commit stores the selected species'
);

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.chainlings WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '1',
    'starter commit creates exactly one profile-owned chainling'
);

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.move_items WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '1',
    'starter commit creates exactly one profile-owned move item'
);

SELECT is(
    (
        SELECT mi.move_id
        FROM public.move_items mi
        WHERE mi.player_profile_id = '20000000-0000-0000-0000-000000000001'
    ),
    'baseline_shot',
    'gilarime receives its authored baseline move'
);

SELECT is(
    (
        SELECT mi.move_id
        FROM public.loadouts l
        JOIN public.chainlings c ON c.id = l.chainling_id
        JOIN public.move_items mi ON mi.id = l.move_item_id
        WHERE c.player_profile_id = '20000000-0000-0000-0000-000000000001'
          AND l.slot = 0
    ),
    'baseline_shot',
    'baseline move is equipped in starter slot zero'
);

SELECT is(
    (SELECT payload->>'species_id' FROM profile_native_results WHERE name = 'starter_duplicate'),
    'gilarime',
    'idempotent starter repeats retain the first selected species'
);

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.chainlings WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '1',
    'duplicate starter commit does not create a second chainling'
);

INSERT INTO profile_native_results VALUES (
    'lifetime',
    public.profile_apply_pve_profile_lifetime_stats_tx(
        '20000000-0000-0000-0000-000000000001',
        1, 0, 0, 0, 2, 0, 1, 25, 0
    )
);

SELECT is(
    (SELECT battles_won::TEXT FROM public.player_lifetime_stats WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '1',
    'lifetime battle stats are profile-owned'
);

SELECT is(
    (SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '25',
    'battle RAM reward updates the canonical profile balance'
);

INSERT INTO profile_native_results VALUES (
    'defeat_penalty',
    public.profile_apply_pve_profile_lifetime_stats_tx(
        '20000000-0000-0000-0000-000000000001',
        0, 1, 0, 0, 0, 0, 0, 0, 5
    )
);

SELECT is(
    (SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '20',
    'PvE defeat RAM penalty reduces the canonical profile balance'
);

SELECT is(
    (SELECT total_ram_spent::TEXT FROM public.player_lifetime_stats WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '5',
    'PvE defeat RAM penalty contributes to lifetime RAM spent'
);

SELECT ok(
    to_regclass('public.players') IS NULL,
    'legacy players table is absent'
);

SELECT is(
    (
        SELECT COUNT(*)::TEXT
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND column_name = 'legacy_player_id'
    ),
    '0',
    'legacy_player_id is absent from the public schema'
);

SELECT is(
    (
        SELECT COUNT(*)::TEXT
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name IN (
              'chainlings',
              'move_items',
              'player_lifetime_stats',
              'event_log',
              'player_sessions',
              'vending_tickets',
              'player_achievements',
              'move_upgrade_events'
          )
          AND column_name = 'player_id'
    ),
    '0',
    'gameplay tables contain no player_id ownership columns'
);

SELECT ok(
    NOT EXISTS (
        SELECT 1
        FROM public.move_items mi
        LEFT JOIN public.player_profiles pp
          ON pp.player_profile_id = mi.player_profile_id
        WHERE pp.player_profile_id IS NULL
    ),
    'every move item owner resolves to a canonical profile'
);

DELETE FROM public.player_profiles
WHERE player_profile_id = '20000000-0000-0000-0000-000000000001';

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.chainlings WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '0',
    'profile deletion cascades through owned chainlings'
);

SELECT is(
    (SELECT COUNT(*)::TEXT FROM public.move_items WHERE player_profile_id = '20000000-0000-0000-0000-000000000001'),
    '0',
    'profile deletion cascades through owned move inventory'
);

SELECT is(
    (
        SELECT COUNT(*)::TEXT
        FROM public.loadouts l
        WHERE l.chainling_id = (
            SELECT (payload->>'chainling_id')::UUID
            FROM profile_native_results
            WHERE name = 'starter'
        )
    ),
    '0',
    'profile deletion leaves no loadout rows'
);

SELECT * FROM finish();
ROLLBACK;
