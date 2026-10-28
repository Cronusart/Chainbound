-- RAM Shop R2 Task 5: a public payment signature recorded against an attacker's
-- order can't block the victim's verification or settlement, and one signature
-- still can't be verified for two orders. Run on the RAM shop chain
-- (tests/fixtures/ram_shop_minimal_prerequisites.sql header) plus the R2 migrations.
BEGIN;

SELECT plan(12);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('46000000-0000-4000-8000-000000000001', 'test', 'test', 0),  -- victim
  ('46000000-0000-4000-8000-000000000002', 'test', 'test', 0);  -- attacker
INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
VALUES
  ('46000000-0000-4000-8000-000000000001', 'wallet-victim', 'devnet', 'active'),
  ('46000000-0000-4000-8000-000000000002', 'wallet-attacker', 'devnet', 'active');

CREATE FUNCTION pg_temp.order_for(p_order TEXT, p_profile TEXT, p_wallet TEXT, p_status TEXT)
RETURNS VOID LANGUAGE sql AS $$
  INSERT INTO public.ram_purchase_orders(
    order_id, player_profile_id, wallet_address, cluster, catalog_version, sku,
    bundle_name, ram_amount, tender, payment_atomic_amount, decimals, destination,
    reference_address, expected_message_base64, expected_message_hash,
    unsigned_transaction_base64, recent_blockhash, last_valid_block_height,
    estimated_fee_lamports, quote_expires_at, status
  ) VALUES (
    p_order::UUID, p_profile::UUID, p_wallet, 'devnet', 'test', 'ram_test', 'Test bundle', 100,
    'SOL', 5000000, 9, 'treasury', 'reference-' || p_order, 'msg', repeat('0', 64), 'tx',
    'blockhash', 1, 5000, now() + interval '5 minutes', p_status
  );
$$;

SELECT pg_temp.order_for('46000000-0000-4000-8000-000000000101', '46000000-0000-4000-8000-000000000001', 'wallet-victim', 'submitted');
SELECT pg_temp.order_for('46000000-0000-4000-8000-000000000102', '46000000-0000-4000-8000-000000000002', 'wallet-attacker', 'quoted');
SELECT pg_temp.order_for('46000000-0000-4000-8000-000000000103', '46000000-0000-4000-8000-000000000002', 'wallet-attacker', 'quoted');
SELECT pg_temp.order_for('46000000-0000-4000-8000-000000000104', '46000000-0000-4000-8000-000000000001', 'wallet-victim', 'submitted');
SELECT pg_temp.order_for('46000000-0000-4000-8000-000000000105', '46000000-0000-4000-8000-000000000002', 'wallet-attacker', 'quoted');

-- 1. Poison via a terminal mismatch on the attacker's own order.
SELECT lives_ok(
  $$SELECT public.ram_shop_mark_order_terminal(
      '46000000-0000-4000-8000-000000000102', 'manual_review', 'payment_message_mismatch', 'victim-signature-1')$$,
  'attacker can still fail their own order with the victim signature'
);
SELECT lives_ok(
  $$SELECT public.ram_shop_mark_payment_verified('46000000-0000-4000-8000-000000000101', 'victim-signature-1')$$,
  'victim verifies after a poison attempt'
);
SELECT is(
  public.ram_shop_settle_purchase('46000000-0000-4000-8000-000000000101', 'credit-sig', 'receipt-101')->>'balance_after',
  '100', 'victim is credited after a poison attempt'
);
SELECT is(
  (SELECT status FROM public.ram_purchase_orders WHERE order_id = '46000000-0000-4000-8000-000000000102'),
  'manual_review', 'the attacker only moved their own order'
);
SELECT is(
  (SELECT string_agg(status, ',' ORDER BY status) FROM public.ram_payment_attempts WHERE signature = 'victim-signature-1'),
  'failed,verified', 'the failed observation stays scoped to the attacker order'
);

-- 2. Poison via a recorded submission.
SELECT lives_ok(
  $$SELECT public.ram_shop_record_submission('46000000-0000-4000-8000-000000000105', 'victim-signature-2')$$,
  'attacker can record a submission for the victim signature'
);
SELECT lives_ok(
  $$SELECT public.ram_shop_mark_payment_verified('46000000-0000-4000-8000-000000000104', 'victim-signature-2')$$,
  'victim verifies after a poisoned submission'
);

-- 3. A verified signature still can't be verified for a second order.
SELECT throws_ok(
  $$SELECT public.ram_shop_mark_payment_verified('46000000-0000-4000-8000-000000000103', 'victim-signature-1')$$,
  'signature_already_consumed', 'a verified signature cannot verify another order'
);
SELECT is(
  (SELECT status FROM public.ram_purchase_orders WHERE order_id = '46000000-0000-4000-8000-000000000103'),
  'quoted', 'the refused order is unchanged'
);
SELECT throws_ok(
  $$INSERT INTO public.ram_payment_attempts(order_id, signature, status)
    SELECT id, 'victim-signature-1', 'verified' FROM public.ram_purchase_orders
     WHERE order_id = '46000000-0000-4000-8000-000000000103'$$,
  '23505', NULL, 'the partial unique index allows one verified attempt per signature'
);

-- 4. Idempotent re-verification of the victim's own order still works.
SELECT is(
  public.ram_shop_mark_payment_verified('46000000-0000-4000-8000-000000000101', 'victim-signature-1')->>'replayed',
  'true', 'the victim can re-confirm the same signature'
);
SELECT ok((SELECT bool_and(balanced) FROM public.ram_v2_audit_service(NULL)
            WHERE player_profile_id::TEXT LIKE '46000000-%'),
          'audit stays balanced');

SELECT * FROM finish();
ROLLBACK;
