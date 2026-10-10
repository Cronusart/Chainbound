BEGIN;

SELECT plan(22);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('31000000-0000-4000-8000-000000000001', 'test', 'test', 0),
  ('31000000-0000-4000-8000-000000000002', 'test', 'test', 95),
  ('31000000-0000-4000-8000-000000000003', 'test', 'test', 100),
  ('31000000-0000-4000-8000-000000000004', 'test', 'test', 0),
  ('31000000-0000-4000-8000-000000000005', 'test', 'test', 0),
  ('31000000-0000-4000-8000-000000000006', 'test', 'test', 50);

UPDATE public.battle_reward_configs
   SET enabled = true
 WHERE config_version = 3
   AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';
UPDATE public.battle_reward_settings
   SET mode = 'grant', cluster = 'devnet',
       profile_allowlist = ARRAY[
         '31000000-0000-4000-8000-000000000001'::UUID,
         '31000000-0000-4000-8000-000000000002'::UUID,
         '31000000-0000-4000-8000-000000000003'::UUID,
         '31000000-0000-4000-8000-000000000004'::UUID,
         '31000000-0000-4000-8000-000000000005'::UUID,
         '31000000-0000-4000-8000-000000000006'::UUID
       ], updated_at = now()
 WHERE singleton;

INSERT INTO public.ram_ledger_operations(
  operation_id, player_profile_id, wallet_address, cluster,
  operation_kind, direction, amount, state, settled_at
) VALUES
  ('31000000-0000-5000-8000-000000000102', '31000000-0000-4000-8000-000000000002', NULL,
   'devnet', 'opening_balance', 'credit', 95, 'settled', now()),
  ('31000000-0000-5000-8000-000000000103', '31000000-0000-4000-8000-000000000003', NULL,
   'devnet', 'opening_balance', 'credit', 100, 'settled', now()),
  ('31000000-0000-5000-8000-000000000106', '31000000-0000-4000-8000-000000000006', NULL,
   'devnet', 'shop_purchase', 'credit', 50, 'settled', now());

INSERT INTO public.ram_daily_earnings(player_profile_id, utc_day, earned)
VALUES
  ('31000000-0000-4000-8000-000000000002', (now() AT TIME ZONE 'UTC')::DATE, 95),
  ('31000000-0000-4000-8000-000000000003', (now() AT TIME ZONE 'UTC')::DATE, 100),
  ('31000000-0000-4000-8000-000000000005', (now() AT TIME ZONE 'UTC')::DATE - 1, 100);

INSERT INTO public.authoritative_pve_battles(
  battle_id, player_profile_id, start_command_id, status, battle_kind,
  state_version, seed, manifest_hash, config_hash, state_json,
  equipment_snapshot, expires_at, completed_at, encounter_id, reward_tier,
  reward_config_version, reward_config_hash, terminal_outcome,
  terminal_reason, terminal_at
) VALUES
  ('31000000-0000-4000-8000-000000000101', '31000000-0000-4000-8000-000000000001',
   'reward-standard', 'completed', 'wild', 1, 1, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_standard', 'standard', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'victory', 'Victory', now()),
  ('31000000-0000-4000-8000-000000000102', '31000000-0000-4000-8000-000000000002',
   'reward-partial', 'completed', 'boss', 1, 2, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_boss', 'boss', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'victory', 'Victory', now()),
  ('31000000-0000-4000-8000-000000000103', '31000000-0000-4000-8000-000000000003',
   'reward-capped', 'completed', 'boss', 1, 3, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_elite', 'elite', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'victory', 'Victory', now()),
  ('31000000-0000-4000-8000-000000000104', '31000000-0000-4000-8000-000000000004',
   'reward-capture', 'completed', 'wild', 1, 4, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_capture', 'standard', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'capture', 'Captured', now()),
  ('31000000-0000-4000-8000-000000000105', '31000000-0000-4000-8000-000000000005',
   'reward-new-day', 'completed', 'wild', 1, 5, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_new_day', 'standard', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'victory', 'Victory', now()),
  ('31000000-0000-4000-8000-000000000106', '31000000-0000-4000-8000-000000000006',
   'reward-after-purchase', 'completed', 'wild', 1, 6, repeat('a', 64), repeat('b', 64), '{}', '{}',
   now() + interval '5 minutes', now(), 'test_purchase', 'standard', 3,
   '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
   'victory', 'Victory', now());

CREATE TEMP TABLE reward_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO reward_results VALUES
  ('standard', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000101')),
  ('standard_retry', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000101')),
  ('partial', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000102')),
  ('capped', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000103')),
  ('capture', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000104')),
  ('new_day', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000105')),
  ('after_purchase', public.ram_battle_reward_settle_service('31000000-0000-4000-8000-000000000106'));

SELECT is((SELECT payload->>'status' FROM reward_results WHERE name='standard'), 'settled', 'victory settles');
SELECT is((SELECT payload->>'requestedRam' FROM reward_results WHERE name='standard'), '5', 'standard requests 5');
SELECT is((SELECT payload->>'appliedRam' FROM reward_results WHERE name='standard'), '5', 'standard applies 5');
SELECT is((SELECT payload->>'balanceAfter' FROM reward_results WHERE name='standard'), '5', 'settlement returns balance');
SELECT is((SELECT payload->>'alreadyApplied' FROM reward_results WHERE name='standard'), 'false', 'first call is new');
SELECT is((SELECT payload->>'alreadyApplied' FROM reward_results WHERE name='standard_retry'), 'true', 'retry is idempotent');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations WHERE operation_id='31000000-0000-4000-8000-000000000101'), '1', 'one ledger op per battle');
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id='31000000-0000-4000-8000-000000000001'), '5', 'profile credited once');
SELECT is((SELECT payload->>'appliedRam' FROM reward_results WHERE name='partial'), '5', '95 plus boss clamps to five');
SELECT is((SELECT payload->>'dailyEarned' FROM reward_results WHERE name='partial'), '100', 'partial grant reaches daily cap');
SELECT is((SELECT payload->>'status' FROM reward_results WHERE name='capped'), 'zero', 'capped win gets zero receipt');
SELECT is((SELECT payload->>'reason' FROM reward_results WHERE name='capped'), 'daily_cap', 'capped reason is truthful');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations WHERE operation_id='31000000-0000-4000-8000-000000000103'), '0', 'zero receipt has no ledger op');
SELECT is((SELECT payload->>'status' FROM reward_results WHERE name='capture'), 'zero', 'capture is zero');
SELECT is((SELECT payload->>'reason' FROM reward_results WHERE name='capture'), 'capture', 'capture reason is stable');
SELECT is((SELECT payload->>'appliedRam' FROM reward_results WHERE name='new_day'), '5', 'previous UTC day cap does not block today');
SELECT is((SELECT payload->>'dailyEarned' FROM reward_results WHERE name='new_day'), '5', 'new UTC day starts a new earn bucket');
SELECT is((SELECT payload->>'appliedRam' FROM reward_results WHERE name='after_purchase'), '5', 'purchased RAM does not consume earn cap');
SELECT is((SELECT payload->>'balanceAfter' FROM reward_results WHERE name='after_purchase'), '55', 'battle credit adds to purchased balance');
SELECT is((SELECT payload->>'dailyEarned' FROM reward_results WHERE name='after_purchase'), '5', 'only earned RAM enters the daily bucket');
SELECT ok((SELECT bool_and(balanced) FROM public.ram_v2_audit_service(NULL)), 'ledger sums match profile balances');
SELECT is((SELECT count(*)::TEXT FROM public.battle_reward_settlements), '6', 'one domain receipt per terminal battle');

SELECT * FROM finish();
ROLLBACK;
