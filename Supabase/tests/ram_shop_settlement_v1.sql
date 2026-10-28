-- RAM Shop R2 Task 2: purchase settlement writes a settled shop_purchase ledger
-- row, stays balanced in ram_v2_audit_service, and never touches the daily earn
-- cap. Run on tests/fixtures/ram_shop_minimal_prerequisites.sql plus the
-- migration list in that fixture's header.
BEGIN;

SELECT plan(26);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('42000000-0000-4000-8000-000000000001', 'test', 'test', 0),
  ('42000000-0000-4000-8000-000000000002', 'test', 'test', 100),
  ('42000000-0000-4000-8000-000000000003', 'test', 'test', 0),
  ('42000000-0000-4000-8000-000000000004', 'test', 'test', 0);

INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
SELECT player_profile_id, 'wallet-' || right(player_profile_id::TEXT, 1), 'devnet', 'active'
  FROM public.player_profiles
 WHERE player_profile_id::TEXT LIKE '42000000-%';

-- Profile 2 already earned the full cap today, backed by an opening row.
INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, settled_at
) VALUES (
  '42000000-0000-5000-8000-000000000002', '42000000-0000-4000-8000-000000000002', NULL,
  'devnet', 'opening_balance', 'credit', 100, 'settled', now()
);
INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
VALUES ('42000000-0000-4000-8000-000000000002', (now() AT TIME ZONE 'UTC')::DATE, 100);

CREATE FUNCTION pg_temp.order_for(p_order TEXT, p_profile TEXT, p_ram BIGINT, p_status TEXT)
RETURNS VOID LANGUAGE sql AS $$
  INSERT INTO public.ram_purchase_orders(
    order_id, player_profile_id, wallet_address, cluster, catalog_version, sku,
    bundle_name, ram_amount, tender, payment_atomic_amount, decimals, destination,
    reference_address, expected_message_base64, expected_message_hash,
    unsigned_transaction_base64, recent_blockhash, last_valid_block_height,
    estimated_fee_lamports, quote_expires_at, status, payment_signature
  ) VALUES (
    p_order::UUID, p_profile::UUID, 'wallet-' || right(p_profile, 1), 'devnet', 'test', 'ram_test',
    'Test bundle', p_ram, 'SOL', 5000000, 9, 'treasury',
    'reference-' || p_order, 'msg', repeat('0', 64), 'tx', 'blockhash', 1,
    5000, now() + interval '5 minutes', p_status, 'payment-' || p_order
  );
$$;

SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000101', '42000000-0000-4000-8000-000000000001', 100, 'payment_verified');
SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000102', '42000000-0000-4000-8000-000000000002', 500, 'credit_submitted');
SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000103', '42000000-0000-4000-8000-000000000003', 100, 'payment_verified');
SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000104', '42000000-0000-4000-8000-000000000003', 500, 'payment_verified');
SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000105', '42000000-0000-4000-8000-000000000004', 100, 'quoted');
SELECT pg_temp.order_for('42000000-0000-4000-8000-000000000106', '42000000-0000-4000-8000-000000000004', 100, 'payment_verified');

CREATE TEMP TABLE settle_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO settle_results VALUES
  ('first', public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000101', 'credit-sig', 'receipt-101')),
  ('retry', public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000101', 'credit-sig', 'receipt-101'));

-- Single purchase.
SELECT is((SELECT payload->>'replayed' FROM settle_results WHERE name = 'first'), 'false', 'first settle is new');
SELECT is((SELECT payload->>'balance_after' FROM settle_results WHERE name = 'first'), '100', 'purchase credits the bundle');
SELECT is(
  (SELECT operation_kind || ':' || direction || ':' || amount || ':' || state
     FROM public.ram_ledger_operations WHERE operation_id = '42000000-0000-4000-8000-000000000101'),
  'shop_purchase:credit:100:settled',
  'settle writes one settled shop_purchase credit keyed by order_id'
);
SELECT is(
  (SELECT metadata_json->>'payment_signature' FROM public.ram_ledger_operations
    WHERE operation_id = '42000000-0000-4000-8000-000000000101'),
  'payment-42000000-0000-4000-8000-000000000101',
  'ledger row keeps the payment signature as purchase proof'
);
SELECT ok(
  (SELECT balanced FROM public.ram_v2_audit_service('42000000-0000-4000-8000-000000000001')),
  'audit is balanced after a purchase'
);

-- Replay.
SELECT is((SELECT payload->>'replayed' FROM settle_results WHERE name = 'retry'), 'true', 'second settle replays');
SELECT is(
  (SELECT payload->>'balance_after' || ':' || (payload->>'balance_version') FROM settle_results WHERE name = 'retry'),
  (SELECT payload->>'balance_after' || ':' || (payload->>'balance_version') FROM settle_results WHERE name = 'first'),
  'replay returns the stored balance/version pair'
);
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_ledger_operations WHERE operation_id = '42000000-0000-4000-8000-000000000101'),
  '1', 'replay writes no second ledger row'
);
SELECT is(
  (SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id = '42000000-0000-4000-8000-000000000001'),
  '100', 'replay credits nothing'
);
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_daily_earnings WHERE player_profile_id = '42000000-0000-4000-8000-000000000001'),
  '0', 'purchase creates no daily cap row'
);

-- Profile already at the cap still gets its purchase, and the cap row is untouched.
SELECT is(
  public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000102', 'credit-sig-2', 'receipt-102')->>'balance_after',
  '600', 'purchase credits a profile already at 100 earned today'
);
SELECT is(
  (SELECT earned::TEXT FROM public.ram_daily_earnings
    WHERE player_profile_id = '42000000-0000-4000-8000-000000000002'
      AND utc_day = (now() AT TIME ZONE 'UTC')::DATE),
  '100', 'purchase leaves earned unchanged at the cap'
);
SELECT ok(
  (SELECT balanced FROM public.ram_v2_audit_service('42000000-0000-4000-8000-000000000002')),
  'audit is balanced for a capped buyer'
);

-- Buy twice, then earn the full cap the same UTC day.
SELECT public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000103', 'credit-sig-103', 'receipt-103');
SELECT public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000104', 'credit-sig-104', 'receipt-104');
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_daily_earnings WHERE player_profile_id = '42000000-0000-4000-8000-000000000003'),
  '0', 'several purchases in one day leave no cap counter'
);

UPDATE public.battle_reward_configs
   SET enabled = true
 WHERE config_version = 3
   AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';
UPDATE public.battle_reward_settings
   SET mode = 'grant', cluster = 'devnet',
       profile_allowlist = ARRAY['42000000-0000-4000-8000-000000000003'::UUID], updated_at = now()
 WHERE singleton;

INSERT INTO public.authoritative_pve_battles(
  battle_id, player_profile_id, start_command_id, status, battle_kind,
  state_version, seed, manifest_hash, config_hash, state_json,
  equipment_snapshot, expires_at, completed_at, encounter_id, reward_tier,
  reward_config_version, reward_config_hash, terminal_outcome,
  terminal_reason, terminal_at
)
SELECT format('42000000-0000-4000-8000-%s', lpad((200 + i)::TEXT, 12, '0'))::UUID,
       '42000000-0000-4000-8000-000000000003', 'buy-then-earn-' || i, 'completed', 'boss',
       1, i, repeat('a', 64), repeat('b', 64), '{}', '{}', now() + interval '5 minutes', now(),
       'test_buy_then_earn_' || i, 'boss', 3,
       '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
       'victory', 'Victory', now()
  FROM generate_series(1, 5) AS i;

CREATE TEMP TABLE reward_results(i INTEGER PRIMARY KEY, payload JSONB);
INSERT INTO reward_results
SELECT i, public.ram_battle_reward_settle_service(
         format('42000000-0000-4000-8000-%s', lpad((200 + i)::TEXT, 12, '0'))::UUID)
  FROM generate_series(1, 5) AS i ORDER BY i;

SELECT is(
  (SELECT sum((payload->>'appliedRam')::INTEGER)::TEXT FROM reward_results WHERE i <= 4),
  '100', 'buyer still earns the full 100 cap after purchasing'
);
SELECT is((SELECT payload->>'reason' FROM reward_results WHERE i = 5), 'daily_cap', 'the cap still applies after the 100');
SELECT is(
  (SELECT earned::TEXT FROM public.ram_daily_earnings
    WHERE player_profile_id = '42000000-0000-4000-8000-000000000003'
      AND utc_day = (now() AT TIME ZONE 'UTC')::DATE),
  '100', 'only earned RAM enters the cap counter'
);
SELECT is(
  (SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id = '42000000-0000-4000-8000-000000000003'),
  '700', 'buy N then earn: balance is N + 100'
);
SELECT ok(
  (SELECT balanced FROM public.ram_v2_audit_service('42000000-0000-4000-8000-000000000003')),
  'audit is balanced after purchases and capped battle credits'
);

-- Refusals.
SELECT throws_ok(
  $$SELECT public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000105', NULL, NULL)$$,
  'payment_not_verified', 'an unpaid order cannot settle'
);
SELECT throws_ok(
  $$SELECT public.ram_shop_settle_purchase('42000000-0000-4000-8000-0000000009ff', NULL, NULL)$$,
  'order_not_found', 'an unknown order cannot settle'
);
INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, settled_at
) VALUES (
  '42000000-0000-4000-8000-000000000106', '42000000-0000-4000-8000-000000000004', NULL,
  'devnet', 'shop_purchase', 'credit', 100, 'settled', now()
);
SELECT throws_ok(
  $$SELECT public.ram_shop_settle_purchase('42000000-0000-4000-8000-000000000106', 'credit-sig-106', 'receipt-106')$$,
  'shop_purchase_operation_conflict', 'an unsettled order that already owns a ledger row is refused'
);
SELECT is(
  (SELECT status FROM public.ram_purchase_orders WHERE order_id = '42000000-0000-4000-8000-000000000106'),
  'payment_verified', 'the refused order is left for review'
);

-- Privileges.
SELECT ok(
  NOT has_function_privilege('authenticated', 'public.ram_shop_settle_purchase(uuid, text, text)', 'EXECUTE'),
  'authenticated cannot settle purchases'
);
SELECT ok(
  NOT has_function_privilege('anon', 'public.ram_shop_settle_purchase(uuid, text, text)', 'EXECUTE'),
  'anon cannot settle purchases'
);
SELECT ok(
  has_function_privilege('service_role', 'public.ram_shop_settle_purchase(uuid, text, text)', 'EXECUTE'),
  'service_role can settle purchases'
);

SELECT * FROM finish();
ROLLBACK;
