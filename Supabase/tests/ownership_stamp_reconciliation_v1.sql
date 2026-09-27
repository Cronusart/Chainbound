BEGIN;

SELECT plan(9);

SELECT ok(
    to_regprocedure('public.ownership_confirm_stamp_atomic(uuid,text,uuid,text,jsonb,numeric,integer)') IS NOT NULL,
    'reconcilable atomic Stamp settlement exists'
);

SELECT ok(
    NOT has_function_privilege(
        'authenticated',
        'public.ownership_confirm_stamp_atomic(uuid,text,uuid,text,jsonb,numeric,integer)',
        'EXECUTE'
    ),
    'authenticated clients cannot directly settle a Stamp'
);

SELECT ok(
    to_regclass('public.ux_ownership_stamp_one_intent_per_known') IS NOT NULL,
    'one-intent-per-Known partial unique index exists'
);

SELECT ok(
    to_regclass('public.ux_ownership_stamp_one_pending_per_profile') IS NOT NULL,
    'one-pending-intent-per-profile partial unique index exists'
);

INSERT INTO public.player_profiles (
    player_profile_id, platform_origin, last_platform_seen, ram_balance
) VALUES ('26130000-0000-0000-0000-000000000001', 'test', 'test', 100);

INSERT INTO public.ownership_known_moves (
    known_id, player_profile_id, idempotency_key, move_id, onchain_move_id,
    element, base_power, generator_version, pool_hash
) VALUES
    ('26130000-0000-0000-0000-000000000101', '26130000-0000-0000-0000-000000000001',
     'known-one', 'baseline_jab', 1, 1, 10, 1, repeat('a', 64)),
    ('26130000-0000-0000-0000-000000000102', '26130000-0000-0000-0000-000000000001',
     'known-two', 'baseline_volley', 2, 1, 10, 1, repeat('a', 64)),
    ('26130000-0000-0000-0000-000000000103', '26130000-0000-0000-0000-000000000001',
     'known-three', 'pressure_point', 3, 1, 10, 1, repeat('a', 64));

INSERT INTO public.ownership_stamp_operations (
    operation_id, player_profile_id, known_id, wallet_address, idempotency_key,
    idempotency_hash, modifier_count, modifier_ids, rarity, generator_version,
    pool_hash, onchain_instance_id, ticket_address, stamped_asset_address,
    transaction_base64, recent_blockhash, expires_at, status, signature
) VALUES (
    '26130000-0000-0000-0000-000000000201',
    '26130000-0000-0000-0000-000000000001',
    '26130000-0000-0000-0000-000000000101',
    'wallet-one', 'stamp-one', repeat('b', 64), 1, ARRAY['10.1-a'], 0, 1,
    repeat('a', 64), 1, 'ticket-one', 'asset-one', 'AA==', 'blockhash-one',
    now() - interval '1 hour', 'das_pending', 'signature-one'
);

SELECT is(
    (
        public.ownership_confirm_stamp_atomic(
            '26130000-0000-0000-0000-000000000001',
            'stamp-one',
            '26130000-0000-0000-0000-000000000101',
            'signature-one',
            '{"assetAddress":"asset-one"}'::jsonb,
            1,
            75
        )->>'ram_balance'
    ),
    '25',
    'DAS-pending intent settles after its transaction envelope expired'
);

SELECT is(
    (SELECT status FROM public.ownership_stamp_operations
      WHERE operation_id = '26130000-0000-0000-0000-000000000201'),
    'confirmed',
    'reconciled operation becomes confirmed'
);

SELECT ok(
    (SELECT consumed_at IS NOT NULL FROM public.ownership_known_moves
      WHERE known_id = '26130000-0000-0000-0000-000000000101'),
    'Known is consumed exactly when settlement commits'
);

SELECT is(
    (
        public.ownership_confirm_stamp_atomic(
            '26130000-0000-0000-0000-000000000001',
            'stamp-one',
            '26130000-0000-0000-0000-000000000101',
            'signature-one',
            '{"assetAddress":"asset-one"}'::jsonb,
            1,
            75
        )->>'replayed'
    ),
    'true',
    'reconciliation replay cannot debit RAM twice'
);

INSERT INTO public.ownership_stamp_operations (
    operation_id, player_profile_id, known_id, wallet_address, idempotency_key,
    idempotency_hash, modifier_count, modifier_ids, rarity, generator_version,
    pool_hash, onchain_instance_id, ticket_address, stamped_asset_address,
    transaction_base64, recent_blockhash, expires_at, status
) VALUES (
    '26130000-0000-0000-0000-000000000202',
    '26130000-0000-0000-0000-000000000001',
    '26130000-0000-0000-0000-000000000102',
    'wallet-one', 'stamp-two', repeat('c', 64), 1, ARRAY['10.1-b'], 0, 1,
    repeat('a', 64), 2, 'ticket-two', 'asset-two', 'AA==', 'blockhash-two',
    now() + interval '5 minutes', 'prepared'
);

SELECT throws_ok(
    $$INSERT INTO public.ownership_stamp_operations (
        operation_id, player_profile_id, known_id, wallet_address, idempotency_key,
        idempotency_hash, modifier_count, modifier_ids, rarity, generator_version,
        pool_hash, onchain_instance_id, ticket_address, stamped_asset_address,
        transaction_base64, recent_blockhash, expires_at, status
      ) VALUES (
        '26130000-0000-0000-0000-000000000203',
        '26130000-0000-0000-0000-000000000001',
        '26130000-0000-0000-0000-000000000103',
        'wallet-one', 'stamp-three', repeat('d', 64), 1, ARRAY['10.1-c'], 0, 1,
        repeat('a', 64), 3, 'ticket-three', 'asset-three', 'AA==', 'blockhash-three',
        now() + interval '5 minutes', 'prepared'
      )$$,
    '23505',
    NULL,
    'a profile cannot preview a second unresolved Stamp intent'
);

SELECT * FROM finish();
ROLLBACK;
