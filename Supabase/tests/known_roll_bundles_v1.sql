BEGIN;

SELECT plan(29);

INSERT INTO public.player_profiles(
    player_profile_id, platform_origin, last_platform_seen, ram_balance
) VALUES
    ('39100000-0000-4000-8000-000000000001', 'test', 'test', 60),
    ('39100000-0000-4000-8000-000000000002', 'test', 'test', 45),
    ('39100000-0000-4000-8000-000000000003', 'test', 'test', 20),
    ('39100000-0000-4000-8000-000000000004', 'test', 'test', 100);

-- Profile 4 holds 48 live Knowns and one consumed (Stamped) Known.
INSERT INTO public.ownership_known_moves(
    player_profile_id, idempotency_key, move_id, onchain_move_id, element,
    base_power, generator_version, pool_hash, consumed_at
)
SELECT '39100000-0000-4000-8000-000000000004', 'seed-' || i, 'baseline_jab', 47232, 0, 20, 1,
       repeat('a', 64), CASE WHEN i = 48 THEN now() END
  FROM generate_series(0, 48) AS i;

-- n ordered pulls with operation ids <base>..<base + n - 1>.
CREATE FUNCTION pg_temp.pulls(p_base INTEGER, p_count INTEGER, p_move_id TEXT DEFAULT 'baseline_jab')
RETURNS JSONB LANGUAGE sql AS $$
    SELECT jsonb_agg(jsonb_build_object(
        'operation_id', format('39100000-0000-4000-8000-%s', lpad((p_base + i)::TEXT, 12, '0'))::UUID,
        'move_id', p_move_id,
        'onchain_move_id', 47232,
        'element', 0,
        'base_power', 20
    ) ORDER BY i)
      FROM generate_series(0, p_count - 1) AS i;
$$;

CREATE TEMP TABLE bundle_results(name TEXT PRIMARY KEY, payload JSONB);
CREATE TEMP TABLE versions(name TEXT PRIMARY KEY, version BIGINT);
INSERT INTO versions
SELECT 'before', last_value FROM public.ram_balance_version_sequence;

INSERT INTO bundle_results VALUES (
    'first',
    public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000001', 'devnet', 'bundle-a',
        pg_temp.pulls(100, 10), 1, repeat('a', 64), 5
    )
);

SELECT is((SELECT payload ->> 'replayed' FROM bundle_results WHERE name = 'first'), 'false',
          'a fresh ten-pull bundle is applied');
SELECT is((SELECT jsonb_array_length(payload -> 'knowns') FROM bundle_results WHERE name = 'first'), 10,
          'the bundle returns one Known per pull');
SELECT is((SELECT payload ->> 'ram_cost' FROM bundle_results WHERE name = 'first'), '50',
          'the bundle costs 5 RAM per pull');
SELECT is((SELECT ram_balance FROM public.player_profiles
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'), 10::BIGINT,
          'profile RAM is debited by the whole bundle');
SELECT is((SELECT last_value FROM public.ram_balance_version_sequence)
          - (SELECT version FROM versions WHERE name = 'before'), 1::BIGINT,
          'the bundle advances the RAM version exactly once');
SELECT is((SELECT (payload ->> 'ram_balance_version')::BIGINT FROM bundle_results WHERE name = 'first'),
          (SELECT ram_balance_version FROM public.player_profiles
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'),
          'the returned RAM version is the committed one');
SELECT is((SELECT array_agg(idempotency_key ORDER BY created_at, idempotency_key)
             FROM public.ownership_known_moves
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'),
          (SELECT array_agg('bundle-a#' || i ORDER BY 'bundle-a#' || i) FROM generate_series(0, 9) AS i),
          'each pull is a Known row keyed <bundle key>#<index>');
SELECT is((SELECT array_agg(value ->> 'idempotency_key' ORDER BY ordinality)
             FROM bundle_results, jsonb_array_elements(payload -> 'knowns') WITH ORDINALITY
            WHERE name = 'first'),
          (SELECT array_agg('bundle-a#' || i ORDER BY i) FROM generate_series(0, 9) AS i),
          'knowns are returned in pull order');
SELECT is((SELECT COUNT(*) FROM public.move_items
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'
              AND ownership_known_id IS NOT NULL), 10::BIGINT,
          'each pull gets one linked playable move item');
SELECT is((SELECT COUNT(*) FROM public.ram_ledger_operations
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'
              AND cluster = 'devnet' AND state = 'settled' AND operation_kind = 'known_roll'
              AND amount = 5 AND wallet_address IS NULL), 10::BIGINT,
          'each pull keeps a settled single-roll-shaped ledger operation');
SELECT is((SELECT array_agg((metadata_json ->> 'bundle_index')::INTEGER ORDER BY operation_id)
             FROM public.ram_ledger_operations
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'
              AND metadata_json ->> 'bundle_idempotency_key' = 'bundle-a'),
          ARRAY[0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
          'ledger operations record the bundle key and pull index');
SELECT is((SELECT candy_machine_rolls FROM public.player_lifetime_stats
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'), 10,
          'lifetime candy rolls count every pull');

INSERT INTO bundle_results VALUES (
    'replay',
    public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000001', 'devnet', 'bundle-a',
        pg_temp.pulls(100, 10), 1, repeat('a', 64), 5
    )
);
SELECT is((SELECT payload ->> 'replayed' FROM bundle_results WHERE name = 'replay'), 'true',
          'retrying the bundle key replays it');
SELECT is((SELECT payload -> 'knowns' FROM bundle_results WHERE name = 'replay'),
          (SELECT payload -> 'knowns' FROM bundle_results WHERE name = 'first'),
          'the replay returns the identical Known rows in the same order');
SELECT is((SELECT payload -> 'move_item_ids' FROM bundle_results WHERE name = 'replay'),
          (SELECT payload -> 'move_item_ids' FROM bundle_results WHERE name = 'first'),
          'the replay returns the same move items');
SELECT is((SELECT ram_balance FROM public.player_profiles
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'), 10::BIGINT,
          'the replay does not debit again');
SELECT is((SELECT COUNT(*) FROM public.ownership_known_moves
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000001'), 10::BIGINT,
          'the replay writes no new Known rows');

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000001', 'devnet', 'bundle-a',
        pg_temp.pulls(100, 5), 1, repeat('a', 64), 5
    )$$,
    'known_roll_bundle_conflict',
    'a retry cannot trim a settled bundle'
);
SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000001', 'devnet', 'bundle-a',
        pg_temp.pulls(100, 10, 'baseline_slam'), 1, repeat('a', 64), 5
    )$$,
    'known_roll_bundle_conflict',
    'a retry cannot change a rolled result'
);

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000002', 'devnet', 'bundle-short',
        pg_temp.pulls(300, 10), 1, repeat('a', 64), 5
    )$$,
    'insufficient_ram',
    'a bundle the profile cannot afford in full fails'
);
SELECT is((SELECT ram_balance::TEXT || ':' ||
                  (SELECT COUNT(*) FROM public.ownership_known_moves
                    WHERE player_profile_id = '39100000-0000-4000-8000-000000000002')::TEXT || ':' ||
                  (SELECT COUNT(*) FROM public.ram_ledger_operations
                    WHERE player_profile_id = '39100000-0000-4000-8000-000000000002')::TEXT
             FROM public.player_profiles
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000002'), '45:0:0',
          'an unaffordable bundle writes no pull and debits nothing, even though 9 of 10 pulls were affordable');

SELECT NULL FROM public.ownership_roll_known_profile_atomic(
    '39100000-0000-4000-8000-000000000400',
    '39100000-0000-4000-8000-000000000003',
    'devnet', 'shared-key', 'baseline_jab', 47232, 0, 20, 1,
    repeat('a', 64), 5
);
SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000003', 'devnet', 'shared-key',
        pg_temp.pulls(500, 2), 1, repeat('a', 64), 5
    )$$,
    'known_roll_idempotency_conflict',
    'a key already spent on a single roll cannot start a bundle'
);

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000004', 'devnet', 'over-capacity',
        pg_temp.pulls(800, 5), 1, repeat('a', 64), 5
    )$$,
    'inventory_full free_slots=2 required=5',
    'a bundle that does not fit the 50-Known inventory is refused'
);
SELECT is((SELECT ram_balance FROM public.player_profiles
            WHERE player_profile_id = '39100000-0000-4000-8000-000000000004'), 100::BIGINT,
          'a refused over-capacity bundle debits nothing');
SELECT is((SELECT payload ->> 'replayed'
             FROM (SELECT public.ownership_roll_known_bundle_profile_atomic(
                     '39100000-0000-4000-8000-000000000004', 'devnet', 'fills-inventory',
                     pg_temp.pulls(850, 2), 1, repeat('a', 64), 5
                  ) AS payload) AS rolled), 'false',
          'consumed Knowns free their slot, so a bundle can fill the inventory exactly');

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000003', 'devnet', 'one-pull',
        pg_temp.pulls(600, 1), 1, repeat('a', 64), 5
    )$$,
    'invalid_pull_count',
    'a one-pull request uses the single-roll RPC'
);
SELECT throws_ok(
    $$SELECT public.ownership_roll_known_bundle_profile_atomic(
        '39100000-0000-4000-8000-000000000003', 'devnet', 'too-many',
        pg_temp.pulls(700, 21), 1, repeat('a', 64), 5
    )$$,
    'invalid_pull_count',
    'bundles are capped at 20 pulls'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.ownership_roll_known_bundle_profile_atomic(uuid,text,text,jsonb,integer,text,integer)',
        'EXECUTE'
    ),
    'authenticated callers cannot bypass the Known roll Edge service'
);
SELECT ok(
    NOT has_table_privilege('authenticated', 'public.ownership_known_roll_bundles', 'SELECT'),
    'bundle records are not readable by clients'
);

SELECT * FROM finish();
ROLLBACK;
