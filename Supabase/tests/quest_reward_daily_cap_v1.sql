-- RAM Shop R2 Task 4 (D3): quest RAM goes through the 100/UTC-day earn cap.
-- Run on the RAM shop chain plus tests/fixtures/quest_reward_cap_prerequisites.sql
-- (see its header for the migration order).
BEGIN;

SELECT plan(22);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('45000000-0000-4000-8000-000000000001', 'test', 'test', 0),
  ('45000000-0000-4000-8000-000000000002', 'test', 'test', 90),
  ('45000000-0000-4000-8000-000000000003', 'test', 'test', 100),
  ('45000000-0000-4000-8000-000000000004', 'test', 'test', 0),
  ('45000000-0000-4000-8000-000000000005', 'test', 'test', 0);

INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, settled_at
) VALUES
  ('45000000-0000-5000-8000-000000000002', '45000000-0000-4000-8000-000000000002', NULL,
   'devnet', 'battle_reward', 'credit', 90, 'settled', now()),
  ('45000000-0000-5000-8000-000000000003', '45000000-0000-4000-8000-000000000003', NULL,
   'devnet', 'battle_reward', 'credit', 100, 'settled', now());
INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
VALUES
  ('45000000-0000-4000-8000-000000000002', (now() AT TIME ZONE 'UTC')::DATE, 90),
  ('45000000-0000-4000-8000-000000000003', (now() AT TIME ZONE 'UTC')::DATE, 100);

UPDATE public.battle_reward_settings SET cluster = 'devnet' WHERE singleton;

CREATE FUNCTION pg_temp.earned(p_profile TEXT) RETURNS TEXT LANGUAGE sql AS $$
  SELECT COALESCE((SELECT earned::TEXT FROM public.ram_daily_earnings
                    WHERE player_profile_id = p_profile::UUID
                      AND utc_day = (now() AT TIME ZONE 'UTC')::DATE), 'none');
$$;

CREATE TEMP TABLE quest_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO quest_results VALUES
  ('fresh', public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000001', 'fisher_ape_lucky_crayon')),
  ('fresh_retry', public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000001', 'fisher_ape_lucky_crayon')),
  ('at_90', public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000002', 'fisher_ape_lucky_crayon')),
  ('at_100', public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000003', 'fisher_ape_lucky_crayon')),
  ('at_100_retry', public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000003', 'fisher_ape_lucky_crayon'));

-- 0 earned: the full 15.
SELECT is((SELECT payload->>'appliedRam' || '/' || (payload->>'requestedRam') FROM quest_results WHERE name = 'fresh'),
          '15/15', 'quest at 0 earned applies the full 15');
SELECT is(pg_temp.earned('45000000-0000-4000-8000-000000000001'), '15', 'quest RAM enters the cap counter');
SELECT is((SELECT payload->>'balanceAfter' FROM quest_results WHERE name = 'fresh'), '15', 'balanceAfter reports the credit');
SELECT is((SELECT payload->>'ram_balance' FROM quest_results WHERE name = 'fresh'), '15', 'legacy ram_balance key is kept');

-- Retry.
SELECT is((SELECT payload->>'already_applied' FROM quest_results WHERE name = 'fresh_retry'), 'true', 'retry is idempotent');
SELECT is((SELECT payload->>'appliedRam' FROM quest_results WHERE name = 'fresh_retry'), '15', 'retry reports what was applied');
SELECT is(
  (SELECT ram_balance::TEXT || ':' || pg_temp.earned('45000000-0000-4000-8000-000000000001')
     FROM public.player_profiles WHERE player_profile_id = '45000000-0000-4000-8000-000000000001'),
  '15:15', 'retry credits nothing and counts nothing'
);

-- 90 earned: clamps to 10.
SELECT is((SELECT payload->>'appliedRam' FROM quest_results WHERE name = 'at_90'), '10', 'quest at 90 earned applies 10');
SELECT is((SELECT payload->>'reason' FROM quest_results WHERE name = 'at_90'), 'daily_cap_partial', 'partial clamp is reported');
SELECT is(pg_temp.earned('45000000-0000-4000-8000-000000000002'), '100', 'partial quest fills the cap');
SELECT is(
  (SELECT (metadata_json->>'requested') || '/' || (metadata_json->>'applied') FROM public.ram_ledger_operations
    WHERE player_profile_id = '45000000-0000-4000-8000-000000000002' AND operation_kind = 'quest_reward'),
  '15/10', 'ledger metadata keeps requested and applied'
);

-- 100 earned: zero, items still processed.
SELECT is((SELECT payload->>'appliedRam' || ':' || (payload->>'reason') FROM quest_results WHERE name = 'at_100'),
          '0:daily_cap', 'quest at the cap applies 0 with reason daily_cap');
SELECT is(
  (SELECT count(*)::TEXT FROM public.ram_ledger_operations
    WHERE player_profile_id = '45000000-0000-4000-8000-000000000003' AND operation_kind = 'quest_reward'),
  '0', 'a zero quest credit writes no ledger row'
);
SELECT is(
  (SELECT count(*)::TEXT FROM public.chainbound_profile_item_mutations
    WHERE player_profile_id = '45000000-0000-4000-8000-000000000003'
      AND idempotency_key = 'quest-reward:fisher_ape_lucky_crayon'),
  '1', 'items are still processed at the cap'
);
SELECT is((SELECT payload->>'already_applied' || ':' || (payload->>'reason') FROM quest_results WHERE name = 'at_100_retry'),
          'true:daily_cap', 'a capped quest replays as capped, not as a fresh credit');

-- Quest + battle together never exceed 100.
UPDATE public.battle_reward_configs
   SET enabled = true
 WHERE config_version = 3
   AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';
UPDATE public.battle_reward_settings
   SET mode = 'grant', cluster = 'devnet',
       profile_allowlist = ARRAY['45000000-0000-4000-8000-000000000004'::UUID], updated_at = now()
 WHERE singleton;
SELECT is(
  public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000004', 'fisher_ape_lucky_crayon')->>'appliedRam',
  '15', 'quest first applies 15'
);
INSERT INTO public.authoritative_pve_battles(
  battle_id, player_profile_id, start_command_id, status, battle_kind,
  state_version, seed, manifest_hash, config_hash, state_json,
  equipment_snapshot, expires_at, completed_at, encounter_id, reward_tier,
  reward_config_version, reward_config_hash, terminal_outcome,
  terminal_reason, terminal_at
)
SELECT format('45000000-0000-4000-8000-%s', lpad((300 + i)::TEXT, 12, '0'))::UUID,
       '45000000-0000-4000-8000-000000000004', 'quest-then-battle-' || i, 'completed', 'boss',
       1, i, repeat('a', 64), repeat('b', 64), '{}', '{}', now() + interval '5 minutes', now(),
       'test_quest_then_battle_' || i, 'boss', 3,
       '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
       'victory', 'Victory', now()
  FROM generate_series(1, 4) AS i;
CREATE TEMP TABLE battle_results(i INTEGER PRIMARY KEY, payload JSONB);
INSERT INTO battle_results
SELECT i, public.ram_battle_reward_settle_service(
         format('45000000-0000-4000-8000-%s', lpad((300 + i)::TEXT, 12, '0'))::UUID)
  FROM generate_series(1, 4) AS i ORDER BY i;
SELECT is(
  (SELECT string_agg(payload->>'appliedRam', ',' ORDER BY i) FROM battle_results),
  '25,25,25,10', 'battles after a quest clamp at the shared cap'
);
SELECT is(
  (SELECT ram_balance::TEXT || ':' || pg_temp.earned('45000000-0000-4000-8000-000000000004')
     FROM public.player_profiles WHERE player_profile_id = '45000000-0000-4000-8000-000000000004'),
  '100:100', 'quest + battle earn exactly 100 in one UTC day'
);

-- A same-day purchase changes neither the quest credit nor the counter (D2).
INSERT INTO public.ram_ledger_accounts(player_profile_id, wallet_address, cluster, status)
VALUES ('45000000-0000-4000-8000-000000000005', 'wallet-quest-5', 'devnet', 'active');
INSERT INTO public.ram_purchase_orders(
  order_id, player_profile_id, wallet_address, cluster, catalog_version, sku,
  bundle_name, ram_amount, tender, payment_atomic_amount, decimals, destination,
  reference_address, expected_message_base64, expected_message_hash,
  unsigned_transaction_base64, recent_blockhash, last_valid_block_height,
  estimated_fee_lamports, quote_expires_at, status, payment_signature
) VALUES (
  '45000000-0000-4000-8000-000000000105', '45000000-0000-4000-8000-000000000005', 'wallet-quest-5',
  'devnet', 'test', 'ram_test', 'Test bundle', 500, 'SOL', 20000000, 9, 'treasury',
  'reference-quest-5', 'msg', repeat('0', 64), 'tx', 'blockhash', 1, 5000,
  now() + interval '5 minutes', 'payment_verified', 'payment-quest-5'
);
SELECT public.ram_shop_settle_purchase('45000000-0000-4000-8000-000000000105', 'credit-sig', 'receipt-quest-5');
SELECT is(
  public.profile_apply_quest_reward_tx('45000000-0000-4000-8000-000000000005', 'fisher_ape_lucky_crayon')->>'appliedRam',
  '15', 'a purchase earlier the same day does not reduce the quest credit'
);
SELECT is(
  (SELECT ram_balance::TEXT || ':' || pg_temp.earned('45000000-0000-4000-8000-000000000005')
     FROM public.player_profiles WHERE player_profile_id = '45000000-0000-4000-8000-000000000005'),
  '515:15', 'only the quest RAM is counted toward the cap'
);

SELECT ok((SELECT bool_and(balanced) FROM public.ram_v2_audit_service(NULL)
            WHERE player_profile_id::TEXT LIKE '45000000-%'),
          'audit is balanced for every quest profile');
SELECT ok(
  NOT has_function_privilege('authenticated', 'public.profile_quest_reward_replay(uuid, uuid, bigint)', 'EXECUTE'),
  'the replay helper is not callable by clients'
);

SELECT * FROM finish();
ROLLBACK;
