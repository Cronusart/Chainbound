BEGIN;

SELECT plan(15);

INSERT INTO public.player_profiles(
    player_profile_id, platform_origin, last_platform_seen, ram_balance
) VALUES
    ('39000000-0000-4000-8000-000000000001', 'test', 'test', 20),
    ('39000000-0000-4000-8000-000000000002', 'test', 'test', 0);

CREATE TEMP TABLE known_roll_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO known_roll_results VALUES (
    'first',
    public.ownership_roll_known_profile_atomic(
        '39000000-0000-4000-8000-000000000101',
        '39000000-0000-4000-8000-000000000001',
        'devnet', 'walletless-roll-1', 'baseline_jab', 47232, 0, 20, 1,
        repeat('a', 64), 5
    )
);

SELECT is((SELECT payload ->> 'replayed' FROM known_roll_results WHERE name = 'first'), 'false',
          'first walletless Known roll is applied');
SELECT is((SELECT payload ->> 'ram_balance' FROM known_roll_results WHERE name = 'first'), '15',
          'profile RAM is debited atomically');
SELECT ok((SELECT (payload ->> 'ram_balance_version')::BIGINT > 0 FROM known_roll_results WHERE name = 'first'),
          'the authoritative RAM version is returned');
SELECT is((SELECT COUNT(*) FROM public.profile_wallets
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'), 0::BIGINT,
          'the rolling profile has no linked wallet');
SELECT is((SELECT COUNT(*) FROM public.ownership_known_moves
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'), 1::BIGINT,
          'one profile-owned Known provenance row is created');
SELECT is((SELECT COUNT(*) FROM public.move_items
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'
              AND ownership_known_id IS NOT NULL), 1::BIGINT,
          'one linked playable move item is created');
SELECT is((SELECT onchain_instance_id::TEXT FROM public.ownership_known_moves
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'), NULL::TEXT,
          'profile-owned Known has no wallet vault instance before Stamp');
SELECT is((SELECT wallet_address FROM public.ram_ledger_operations
            WHERE operation_id = '39000000-0000-4000-8000-000000000101'
              AND cluster = 'devnet'), NULL::TEXT,
          'the settled Known spend is profile-scoped rather than wallet-scoped');
SELECT is((SELECT state || ':' || operation_kind FROM public.ram_ledger_operations
            WHERE operation_id = '39000000-0000-4000-8000-000000000101'
              AND cluster = 'devnet'), 'settled:known_roll',
          'the walletless debit retains an auditable settled ledger operation');

INSERT INTO known_roll_results VALUES (
    'replay',
    public.ownership_roll_known_profile_atomic(
        '39000000-0000-4000-8000-000000000101',
        '39000000-0000-4000-8000-000000000001',
        'devnet', 'walletless-roll-1', 'baseline_jab', 47232, 0, 20, 1,
        repeat('a', 64), 5
    )
);
SELECT is((SELECT payload ->> 'replayed' FROM known_roll_results WHERE name = 'replay'), 'true',
          'retry returns the same Known without another debit');
SELECT is((SELECT ram_balance FROM public.player_profiles
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'), 15::BIGINT,
          'retry leaves RAM unchanged');
SELECT is((SELECT COUNT(*) FROM public.move_items
            WHERE player_profile_id = '39000000-0000-4000-8000-000000000001'
              AND ownership_known_id IS NOT NULL), 1::BIGINT,
          'retry does not duplicate the move item');

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_profile_atomic(
        '39000000-0000-4000-8000-000000000101',
        '39000000-0000-4000-8000-000000000001',
        'devnet', 'walletless-roll-1', 'baseline_slam', 1, 0, 40, 1,
        repeat('a', 64), 5
    )$$,
    'known_roll_idempotency_conflict',
    'an idempotency replay cannot change the rolled result'
);

SELECT throws_ok(
    $$SELECT public.ownership_roll_known_profile_atomic(
        '39000000-0000-4000-8000-000000000102',
        '39000000-0000-4000-8000-000000000002',
        'devnet', 'walletless-roll-empty', 'baseline_jab', 47232, 0, 20, 1,
        repeat('a', 64), 5
    )$$,
    'insufficient_ram',
    'insufficient profile RAM creates no Known or move item'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.ownership_roll_known_profile_atomic(uuid,uuid,text,text,text,integer,integer,integer,integer,text,integer)',
        'EXECUTE'
    ),
    'authenticated callers cannot bypass the walletless Known Edge service'
);

SELECT * FROM finish();
ROLLBACK;
