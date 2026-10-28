-- RAM Shop R2 Task 3: ram_shop_settings.credit_mode. chain_mirror (default)
-- requires the on-chain credit signature and receipt; postgres_only settles with
-- NULLs and keeps the payment signature as the proof. Run on
-- tests/fixtures/ram_shop_minimal_prerequisites.sql plus the migration list in its header.
BEGIN;

SELECT plan(11);

SELECT is((SELECT credit_mode FROM public.ram_shop_settings WHERE singleton), 'chain_mirror',
          'credit_mode defaults to chain_mirror');
SELECT throws_ok(
  $$UPDATE public.ram_shop_settings SET credit_mode = 'memo' WHERE singleton$$,
  '23514', NULL, 'credit_mode accepts only chain_mirror or postgres_only'
);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES ('44000000-0000-4000-8000-000000000001', 'test', 'test', 0);
INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
VALUES ('44000000-0000-4000-8000-000000000001', 'wallet-mode', 'devnet', 'active');
INSERT INTO public.ram_purchase_orders(
  order_id, player_profile_id, wallet_address, cluster, catalog_version, sku,
  bundle_name, ram_amount, tender, payment_atomic_amount, decimals, destination,
  reference_address, expected_message_base64, expected_message_hash,
  unsigned_transaction_base64, recent_blockhash, last_valid_block_height,
  estimated_fee_lamports, quote_expires_at, status, payment_signature
)
SELECT format('44000000-0000-4000-8000-%s', lpad((100 + i)::TEXT, 12, '0'))::UUID,
       '44000000-0000-4000-8000-000000000001', 'wallet-mode', 'devnet', 'test', 'ram_test',
       'Test bundle', 100, 'SOL', 5000000, 9, 'treasury', 'reference-mode-' || i, 'msg',
       repeat('0', 64), 'tx', 'blockhash', 1, 5000, now() + interval '5 minutes',
       'payment_verified', 'payment-mode-' || i
  FROM generate_series(1, 2) AS i;

-- chain_mirror refuses to credit without the mirror's receipt.
SELECT throws_ok(
  $$SELECT public.ram_shop_settle_purchase('44000000-0000-4000-8000-000000000101', NULL, NULL)$$,
  'credit_receipt_required', 'chain_mirror refuses a settle without the on-chain credit'
);
SELECT throws_ok(
  $$SELECT public.ram_shop_settle_purchase('44000000-0000-4000-8000-000000000101', 'credit-sig', ' ')$$,
  'credit_receipt_required', 'chain_mirror refuses a blank receipt address'
);
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles
            WHERE player_profile_id = '44000000-0000-4000-8000-000000000001'), '0',
          'refused chain_mirror settles credit nothing');
SELECT is(
  public.ram_shop_settle_purchase('44000000-0000-4000-8000-000000000101', 'credit-sig', 'receipt-1')->>'balance_after',
  '100', 'chain_mirror settles with the credit signature and receipt'
);

-- postgres_only settles from the payment alone.
UPDATE public.ram_shop_settings SET credit_mode = 'postgres_only' WHERE singleton;
SELECT is(
  public.ram_shop_settle_purchase('44000000-0000-4000-8000-000000000102', NULL, NULL)->>'balance_after',
  '200', 'postgres_only settles with NULL credit fields'
);
SELECT is(
  (SELECT coalesce(credit_signature, 'null') || ':' || coalesce(credit_receipt_address, 'null')
     FROM public.ram_purchase_orders WHERE order_id = '44000000-0000-4000-8000-000000000102'),
  'null:null', 'postgres_only stores no credit signature or receipt'
);
SELECT is(
  (SELECT payment_signature || ':' || reference_address FROM public.ram_purchase_orders
    WHERE order_id = '44000000-0000-4000-8000-000000000102'),
  'payment-mode-2:reference-mode-2', 'the order keeps the payment signature and reference as proof'
);
SELECT is(
  (SELECT metadata_json->>'credit_mode' || ':' || (metadata_json->>'payment_signature')
     FROM public.ram_ledger_operations WHERE operation_id = '44000000-0000-4000-8000-000000000102'),
  'postgres_only:payment-mode-2', 'the ledger row records the mode and the payment proof'
);
SELECT ok((SELECT balanced FROM public.ram_v2_audit_service('44000000-0000-4000-8000-000000000001')),
          'audit is balanced across both modes');

SELECT * FROM finish();
ROLLBACK;
