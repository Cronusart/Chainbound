-- RAM Shop R2 Task 2: two sessions settling one order at the same instant
-- credit it once and write one ledger row. Like battle_reward_concurrency_v2,
-- this suite commits its fixture before opening independent dblink sessions,
-- runs as the container's supabase_admin role, and deletes every row it made.

CREATE EXTENSION IF NOT EXISTS dblink;

SELECT plan(5);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES ('43000000-0000-4000-8000-000000000001', 'test', 'test', 0);
INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
VALUES ('43000000-0000-4000-8000-000000000001', 'wallet-concurrent', 'devnet', 'active');
INSERT INTO public.ram_purchase_orders(
  order_id, player_profile_id, wallet_address, cluster, catalog_version, sku,
  bundle_name, ram_amount, tender, payment_atomic_amount, decimals, destination,
  reference_address, expected_message_base64, expected_message_hash,
  unsigned_transaction_base64, recent_blockhash, last_valid_block_height,
  estimated_fee_lamports, quote_expires_at, status, payment_signature
) VALUES (
  '43000000-0000-4000-8000-000000000101', '43000000-0000-4000-8000-000000000001',
  'wallet-concurrent', 'devnet', 'test', 'ram_test', 'Test bundle', 500, 'SOL', 20000000, 9,
  'treasury', 'reference-concurrent', 'msg', repeat('0', 64), 'tx', 'blockhash', 1, 5000,
  now() + interval '5 minutes', 'payment_verified', 'payment-concurrent'
);

SELECT dblink_connect('shop_gate', 'dbname=' || current_database() || ' user=' || current_user || ' host=localhost');
SELECT dblink_connect('shop_a', 'dbname=' || current_database() || ' user=' || current_user || ' host=localhost');
SELECT dblink_connect('shop_b', 'dbname=' || current_database() || ' user=' || current_user || ' host=localhost');
SELECT dblink_exec('shop_gate', 'BEGIN');
SELECT dblink_exec('shop_gate', 'DO $lock$ BEGIN PERFORM pg_advisory_xact_lock(43000000101); END $lock$');

SELECT dblink_send_query(
  'shop_a',
  $$WITH gate AS MATERIALIZED (SELECT pg_advisory_xact_lock_shared(43000000101))
    SELECT public.ram_shop_settle_purchase('43000000-0000-4000-8000-000000000101', 'sig-a', 'receipt-a')::TEXT
      FROM gate$$
);
SELECT dblink_send_query(
  'shop_b',
  $$WITH gate AS MATERIALIZED (SELECT pg_advisory_xact_lock_shared(43000000101))
    SELECT public.ram_shop_settle_purchase('43000000-0000-4000-8000-000000000101', 'sig-b', 'receipt-a')::TEXT
      FROM gate$$
);

SELECT pg_sleep(0.1);
SELECT dblink_exec('shop_gate', 'COMMIT');

CREATE TEMP TABLE concurrent_shop_results(label TEXT PRIMARY KEY, payload JSONB);
INSERT INTO concurrent_shop_results
SELECT 'a', payload::JSONB FROM dblink_get_result('shop_a') AS result(payload TEXT);
INSERT INTO concurrent_shop_results
SELECT 'b', payload::JSONB FROM dblink_get_result('shop_b') AS result(payload TEXT);

SELECT is((SELECT count(*)::TEXT FROM concurrent_shop_results WHERE payload->>'status' = 'settled'), '2',
          'both concurrent settle calls return settled');
SELECT is((SELECT count(*)::TEXT FROM concurrent_shop_results WHERE payload->>'replayed' = 'true'), '1',
          'exactly one concurrent call is a replay');
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles
            WHERE player_profile_id = '43000000-0000-4000-8000-000000000001'), '500',
          'concurrent settlement credits the purchase once');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations
            WHERE operation_id = '43000000-0000-4000-8000-000000000101'), '1',
          'concurrent settlement writes one shop_purchase row');
SELECT ok((SELECT balanced FROM public.ram_v2_audit_service('43000000-0000-4000-8000-000000000001')),
          'audit is balanced after concurrent settlement');

SELECT * FROM finish();

SELECT dblink_disconnect('shop_gate');
SELECT dblink_disconnect('shop_a');
SELECT dblink_disconnect('shop_b');

DELETE FROM public.ram_ledger_operations WHERE player_profile_id = '43000000-0000-4000-8000-000000000001';
DELETE FROM public.ram_purchase_orders WHERE player_profile_id = '43000000-0000-4000-8000-000000000001';
DELETE FROM public.ram_ledger_accounts WHERE player_profile_id = '43000000-0000-4000-8000-000000000001';
DELETE FROM public.player_profiles WHERE player_profile_id = '43000000-0000-4000-8000-000000000001';
