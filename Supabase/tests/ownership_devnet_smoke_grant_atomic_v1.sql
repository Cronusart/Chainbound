BEGIN;

SELECT plan(7);

SELECT ok(
    to_regprocedure(
        'public.ownership_confirm_devnet_smoke_grant_atomic(uuid,uuid,text,integer,integer,numeric,numeric)'
    ) IS NOT NULL,
    'atomic smoke grant settlement function exists'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.ownership_confirm_devnet_smoke_grant_atomic(uuid,uuid,text,integer,integer,numeric,numeric)',
        'EXECUTE'
    ),
    'authenticated clients cannot directly settle a smoke grant'
);

INSERT INTO public.player_profiles (
    player_profile_id, platform_origin, last_platform_seen, ram_balance
) VALUES
    ('26000000-0000-0000-0000-000000000001', 'test', 'test', 0),
    ('26000000-0000-0000-0000-000000000002', 'test', 'test', 1);

INSERT INTO public.ownership_ram_grant_operations (
    operation_id,
    player_profile_id,
    wallet_address,
    idempotency_key,
    idempotency_hash,
    amount,
    player_profile_address,
    move_vault_address,
    receipt_address,
    transaction_base64,
    recent_blockhash
) VALUES
    (
        '26000000-0000-0000-0000-000000000101',
        '26000000-0000-0000-0000-000000000001',
        'wallet-one',
        'grant-one',
        repeat('a', 64),
        100,
        'profile-pda-one',
        'vault-pda-one',
        'receipt-pda-one',
        'AA==',
        'blockhash-one'
    ),
    (
        '26000000-0000-0000-0000-000000000102',
        '26000000-0000-0000-0000-000000000002',
        'wallet-two',
        'grant-two',
        repeat('b', 64),
        100,
        'profile-pda-two',
        'vault-pda-two',
        'receipt-pda-two',
        'AA==',
        'blockhash-two'
    );

CREATE TEMP TABLE smoke_grant_results (payload JSONB);

INSERT INTO smoke_grant_results VALUES (
    public.ownership_confirm_devnet_smoke_grant_atomic(
        '26000000-0000-0000-0000-000000000001',
        '26000000-0000-0000-0000-000000000101',
        'signature-one',
        100,
        100,
        100,
        1
    )
);

SELECT is(
    (SELECT payload->>'ram_balance' FROM smoke_grant_results LIMIT 1),
    '100',
    'settlement returns the verified mirror balance'
);

SELECT is(
    (SELECT ram_balance::TEXT FROM public.player_profiles
      WHERE player_profile_id = '26000000-0000-0000-0000-000000000001'),
    '100',
    'chain-verified RAM is written to the canonical profile mirror'
);

SELECT is(
    (SELECT status FROM public.ownership_ram_grant_operations
      WHERE operation_id = '26000000-0000-0000-0000-000000000101'),
    'confirmed',
    'audit operation is confirmed in the same transaction'
);

SELECT is(
    (
        public.ownership_confirm_devnet_smoke_grant_atomic(
            '26000000-0000-0000-0000-000000000001',
            '26000000-0000-0000-0000-000000000101',
            'signature-one',
            100,
            100,
            100,
            1
        )->>'replayed'
    ),
    'true',
    'confirmed settlement replays idempotently'
);

SELECT throws_ok(
    $$SELECT public.ownership_confirm_devnet_smoke_grant_atomic(
        '26000000-0000-0000-0000-000000000002',
        '26000000-0000-0000-0000-000000000102',
        'signature-two',
        100,
        100,
        100,
        1
    )$$,
    'P0001',
    'server_ram_balance_not_fresh',
    'a non-fresh server mirror fails closed'
);

SELECT * FROM finish();
ROLLBACK;
