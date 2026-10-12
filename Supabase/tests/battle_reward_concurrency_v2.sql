-- This suite intentionally commits its fixture before opening independent
-- dblink sessions. It cleans up every row before returning.

CREATE EXTENSION IF NOT EXISTS dblink;

SELECT plan(5);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES ('33000000-0000-4000-8000-000000000001', 'test', 'test', 0);

UPDATE public.battle_reward_configs
   SET enabled = true
 WHERE config_version = 3
   AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';
UPDATE public.battle_reward_settings
   SET mode = 'grant', cluster = 'devnet',
       profile_allowlist = ARRAY['33000000-0000-4000-8000-000000000001'::UUID],
       updated_at = now()
 WHERE singleton;

INSERT INTO public.authoritative_pve_battles(
  battle_id, player_profile_id, start_command_id, status, battle_kind,
  state_version, seed, manifest_hash, config_hash, state_json,
  equipment_snapshot, expires_at, completed_at, encounter_id, reward_tier,
  reward_config_version, reward_config_hash, terminal_outcome,
  terminal_reason, terminal_at
) VALUES (
  '33000000-0000-4000-8000-000000000101',
  '33000000-0000-4000-8000-000000000001',
  'concurrent-settle', 'completed', 'wild', 1, 1,
  repeat('a', 64), repeat('b', 64), '{}', '{}', now(), now(),
  'concurrent_settle', 'standard', 3,
  '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
  'victory', 'Victory', now()
);

-- Run this local-only suite as the container's supabase_admin role. dblink
-- deliberately requires a superuser when the local socket uses trust auth.
SELECT dblink_connect('reward_gate', 'dbname=' || current_database());
SELECT dblink_connect('reward_a', 'dbname=' || current_database());
SELECT dblink_connect('reward_b', 'dbname=' || current_database());
SELECT dblink_exec('reward_gate', 'BEGIN');
SELECT dblink_exec(
  'reward_gate',
  'DO $lock$ BEGIN PERFORM pg_advisory_xact_lock(33000000101); END $lock$'
);

SELECT dblink_send_query(
  'reward_a',
  $$WITH gate AS MATERIALIZED (
      SELECT pg_advisory_xact_lock_shared(33000000101)
    )
    SELECT public.ram_battle_reward_settle_service(
      '33000000-0000-4000-8000-000000000101'
    )::TEXT
    FROM gate$$
);
SELECT dblink_send_query(
  'reward_b',
  $$WITH gate AS MATERIALIZED (
      SELECT pg_advisory_xact_lock_shared(33000000101)
    )
    SELECT public.ram_battle_reward_settle_service(
      '33000000-0000-4000-8000-000000000101'
    )::TEXT
    FROM gate$$
);

SELECT pg_sleep(0.1);
SELECT dblink_exec('reward_gate', 'COMMIT');

CREATE TEMP TABLE concurrent_reward_results(label TEXT PRIMARY KEY, payload JSONB);
INSERT INTO concurrent_reward_results
SELECT 'a', payload::JSONB
  FROM dblink_get_result('reward_a') AS result(payload TEXT);
INSERT INTO concurrent_reward_results
SELECT 'b', payload::JSONB
  FROM dblink_get_result('reward_b') AS result(payload TEXT);

SELECT is((SELECT count(*)::TEXT FROM concurrent_reward_results), '2',
          'both concurrent settlement calls return a receipt');
SELECT is((SELECT count(*)::TEXT FROM concurrent_reward_results
            WHERE payload->>'status' = 'settled'), '2',
          'both callers observe the same settled result');
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles
            WHERE player_profile_id = '33000000-0000-4000-8000-000000000001'), '5',
          'concurrent settlement credits the profile once');
SELECT is((SELECT count(*)::TEXT FROM public.battle_reward_settlements
            WHERE battle_id = '33000000-0000-4000-8000-000000000101'), '1',
          'concurrent settlement creates one domain receipt');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations
            WHERE operation_id = '33000000-0000-4000-8000-000000000101'), '1',
          'concurrent settlement creates one ledger credit');

SELECT * FROM finish();

SELECT dblink_disconnect('reward_gate');
SELECT dblink_disconnect('reward_a');
SELECT dblink_disconnect('reward_b');

DELETE FROM public.battle_reward_settlements
 WHERE battle_id = '33000000-0000-4000-8000-000000000101';
DELETE FROM public.ram_ledger_operations
 WHERE operation_id = '33000000-0000-4000-8000-000000000101';
DELETE FROM public.ram_daily_earnings
 WHERE player_profile_id = '33000000-0000-4000-8000-000000000001';
DELETE FROM public.authoritative_pve_battles
 WHERE battle_id = '33000000-0000-4000-8000-000000000101';
DELETE FROM public.player_profiles
 WHERE player_profile_id = '33000000-0000-4000-8000-000000000001';
UPDATE public.battle_reward_settings
   SET mode = 'off', profile_allowlist = '{}'::UUID[], updated_at = now()
 WHERE singleton;
