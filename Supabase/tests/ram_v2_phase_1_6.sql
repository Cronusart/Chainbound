BEGIN;

SELECT plan(21);

INSERT INTO public.player_profiles(
  player_profile_id, platform_origin, last_platform_seen, ram_balance, bound_wallet_address
)
VALUES
  ('36000000-0000-4000-8000-000000000001', 'test', 'test', 80, '33333333333333333333333333333333'),
  ('36000000-0000-4000-8000-000000000002', 'test', 'test', 80, '44444444444444444444444444444444'),
  ('36000000-0000-4000-8000-000000000003', 'test', 'test', 10, '55555555555555555555555555555555'),
  ('36000000-0000-4000-8000-000000000004', 'test', 'test', 0, NULL);

INSERT INTO public.profile_wallets(player_profile_id, wallet_public_key, is_active)
VALUES
  ('36000000-0000-4000-8000-000000000001', '33333333333333333333333333333333', true),
  ('36000000-0000-4000-8000-000000000002', '44444444444444444444444444444444', true),
  ('36000000-0000-4000-8000-000000000003', '55555555555555555555555555555555', true);

SELECT is(
  public.ram_ledger_ensure_account(
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet'
  )->>'status',
  'active',
  'ensure_account creates an active Postgres-canonical account'
);
SELECT lives_ok(
  $$SELECT public.ram_ledger_ensure_account(
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet')$$,
  'ensure_account is idempotent'
);
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_ledger_accounts
    WHERE player_profile_id = '36000000-0000-4000-8000-000000000001'),
  '1',
  'ensure_account keeps one account row'
);
SELECT is(
  (SELECT profile_balance_snapshot::TEXT FROM public.ram_ledger_accounts
    WHERE player_profile_id = '36000000-0000-4000-8000-000000000001'),
  '80',
  'new account snapshots the current Postgres opening balance'
);

UPDATE public.ram_ledger_accounts
   SET status = 'pending_audit'
 WHERE player_profile_id = '36000000-0000-4000-8000-000000000001';

CREATE TEMP TABLE reserve_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO reserve_results VALUES
  ('first', public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet', 'stamp', 75,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB)),
  ('retry', public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet', 'stamp', 75,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB));

SELECT is((SELECT payload->>'replayed' FROM reserve_results WHERE name = 'first'), 'false',
          'first reserve is not a replay');
SELECT is((SELECT payload->>'replayed' FROM reserve_results WHERE name = 'retry'), 'true',
          '80 balance can replay its existing 75 hold without requiring 150');
SELECT is((SELECT payload->>'available_balance' FROM reserve_results WHERE name = 'retry'), '5',
          'replay reports balance net of its one existing hold');

SELECT throws_ok(
  $$SELECT public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet', 'bind', 75,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB)$$,
  'operation_id_conflict',
  'same operation id with a different kind is rejected before balance checks'
);
SELECT throws_ok(
  $$SELECT public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet', 'stamp', 74,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB)$$,
  'operation_id_conflict',
  'same operation id with a different amount is rejected before balance checks'
);

SELECT public.ram_ledger_ensure_account(
  '36000000-0000-4000-8000-000000000002',
  '44444444444444444444444444444444', 'devnet');
SELECT throws_ok(
  $$SELECT public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000002',
    '44444444444444444444444444444444', 'devnet', 'stamp', 75,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB)$$,
  'operation_id_conflict',
  'same operation id with a different profile is rejected before balance checks'
);

UPDATE public.ram_ledger_operations SET state = 'released'
 WHERE cluster = 'devnet' AND operation_id = '36000000-0000-4000-8000-000000000101';
UPDATE public.ram_ledger_reservations SET state = 'consumed'
 WHERE operation_id = (SELECT id FROM public.ram_ledger_operations
                         WHERE cluster = 'devnet'
                           AND operation_id = '36000000-0000-4000-8000-000000000101');
SELECT throws_ok(
  $$SELECT public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000101',
    '36000000-0000-4000-8000-000000000001',
    '33333333333333333333333333333333', 'devnet', 'stamp', 75,
    now() + interval '10 minutes', '{"scope":"phase-1.6"}'::JSONB)$$,
  'operation_terminal',
  'released operation with its consumed hold is terminal'
);

INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, metadata_json
) VALUES (
  '36000000-0000-4000-8000-000000000102',
  '36000000-0000-4000-8000-000000000002',
  '44444444444444444444444444444444', 'devnet',
  'bind', 'debit', 75, 'reserved', '{"expired":true}'::JSONB
);
INSERT INTO public.ram_ledger_reservations(operation_id, amount, state, expires_at)
SELECT id, 75, 'active', now() - interval '1 minute'
  FROM public.ram_ledger_operations
 WHERE cluster = 'devnet' AND operation_id = '36000000-0000-4000-8000-000000000102';
SELECT is(
  public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000102',
    '36000000-0000-4000-8000-000000000002',
    '44444444444444444444444444444444', 'devnet', 'bind', 75,
    now() + interval '10 minutes', '{"expired":true}'::JSONB)->>'replayed',
  'true',
  'expired matching hold renews as an idempotent replay'
);
SELECT ok(
  (SELECT expires_at > now() FROM public.ram_ledger_reservations
    WHERE operation_id = (SELECT id FROM public.ram_ledger_operations
                            WHERE cluster = 'devnet'
                              AND operation_id = '36000000-0000-4000-8000-000000000102')),
  'expired hold receives the new expiry'
);

SELECT throws_ok(
  $$SELECT public.ram_ledger_reserve_operation(
    '36000000-0000-4000-8000-000000000103',
    '36000000-0000-4000-8000-000000000003',
    '55555555555555555555555555555555', 'devnet', 'stamp', 5,
    now() + interval '10 minutes', '{}'::JSONB)$$,
  'ram_ledger_not_active',
  'missing ledger scope preserves ram_ledger_not_active'
);
SELECT ok(
  (SELECT prosecdef FROM pg_proc
    WHERE oid = 'public.ram_ledger_reserve_operation(uuid,uuid,text,text,text,bigint,timestamptz,jsonb)'::regprocedure),
  'reserve_operation remains SECURITY DEFINER'
);
SELECT ok(
  NOT has_function_privilege('authenticated', 'public.ram_ledger_ensure_account(uuid,text,text)', 'EXECUTE'),
  'authenticated cannot create ledger accounts'
);
SELECT ok(
  has_function_privilege('service_role', 'public.ram_ledger_ensure_account(uuid,text,text)', 'EXECUTE'),
  'service role can ensure ledger accounts'
);

UPDATE public.battle_reward_configs SET enabled = false WHERE config_version = 3;
CREATE TEMP TABLE begin_disabled_result(payload JSONB);
INSERT INTO begin_disabled_result VALUES (public.authoritative_begin_battle_service(
  '36000000-0000-4000-8000-000000000201',
  '36000000-0000-4000-8000-000000000004', 'disabled-reward-start', 'wild', 1,
  repeat('a', 64), repeat('b', 64), 'disabled_reward', 'standard', 3,
  '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
  '{}', '{}', (extract(epoch FROM now() + interval '5 minutes') * 1000)::BIGINT
));
SELECT is((SELECT payload->>'ok' FROM begin_disabled_result), 'true',
          'battle starts when reward config is disabled');
SELECT is(
  (SELECT reward_tier || ':' || reward_config_version::TEXT
     FROM public.authoritative_pve_battles
    WHERE battle_id = '36000000-0000-4000-8000-000000000201'),
  'none:0',
  'disabled reward config stamps an explicit zero tier'
);

SELECT ok(
  (SELECT ram_balance_version > 0 FROM public.player_profiles
    WHERE player_profile_id = '36000000-0000-4000-8000-000000000001'),
  'every profile has a positive server balance version'
);
SELECT is(
  (SELECT status FROM public.ram_ledger_accounts
    WHERE wallet_address = '22222222222222222222222222222222'),
  'active',
  'Devnet backfill activates the linked legacy placeholder account'
);

SELECT * FROM finish();
ROLLBACK;
