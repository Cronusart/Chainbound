-- Daily login gift v1: two sessions claiming the same profile at the same
-- instant grant once. Like ram_shop_settlement_concurrency_v1, this suite
-- commits its fixture before opening independent dblink sessions, runs as the
-- container's supabase_admin role, and deletes every row it made.
-- Run on the same chain as daily_login_reward_v1.sql.

CREATE EXTENSION IF NOT EXISTS dblink;

SELECT plan(6);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES ('47000000-0000-4000-8000-000000000001', 'test', 'test', 0);
INSERT INTO public.profile_quest_states(player_profile_id, quest_id, lifecycle, resolution_id)
VALUES ('47000000-0000-4000-8000-000000000001', 'coffee_and_beer', 'completed', 'deliver_to_toby');

CREATE TEMP TABLE prior_cluster AS SELECT cluster FROM public.battle_reward_settings WHERE singleton;
UPDATE public.battle_reward_settings SET cluster = 'devnet' WHERE singleton;

SELECT dblink_connect('gift_gate', 'dbname=' || current_database());
SELECT dblink_connect('gift_a', 'dbname=' || current_database());
SELECT dblink_connect('gift_b', 'dbname=' || current_database());
SELECT dblink_exec('gift_gate', 'BEGIN');
SELECT dblink_exec('gift_gate', 'DO $lock$ BEGIN PERFORM pg_advisory_xact_lock(47000000001); END $lock$');

SELECT dblink_send_query(
  'gift_a',
  $$WITH gate AS MATERIALIZED (SELECT pg_advisory_xact_lock_shared(47000000001))
    SELECT public.profile_claim_daily_login_tx('47000000-0000-4000-8000-000000000001')::TEXT
      FROM gate$$
);
SELECT dblink_send_query(
  'gift_b',
  $$WITH gate AS MATERIALIZED (SELECT pg_advisory_xact_lock_shared(47000000001))
    SELECT public.profile_claim_daily_login_tx('47000000-0000-4000-8000-000000000001')::TEXT
      FROM gate$$
);

SELECT pg_sleep(0.1);
SELECT dblink_exec('gift_gate', 'COMMIT');

CREATE TEMP TABLE concurrent_gift_results(label TEXT PRIMARY KEY, payload JSONB);
INSERT INTO concurrent_gift_results
SELECT 'a', payload::JSONB FROM dblink_get_result('gift_a') AS result(payload TEXT);
INSERT INTO concurrent_gift_results
SELECT 'b', payload::JSONB FROM dblink_get_result('gift_b') AS result(payload TEXT);

SELECT is((SELECT count(*)::TEXT FROM concurrent_gift_results WHERE payload->>'ok' = 'true'), '2',
          'both concurrent claims return ok');
SELECT is((SELECT count(*)::TEXT FROM concurrent_gift_results WHERE payload->>'already_claimed' = 'true'), '1',
          'exactly one concurrent claim is already_claimed');
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles
            WHERE player_profile_id = '47000000-0000-4000-8000-000000000001'), '10',
          'concurrent claims credit 10 RAM once');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations
            WHERE player_profile_id = '47000000-0000-4000-8000-000000000001' AND operation_kind = 'daily_login'), '1',
          'concurrent claims write one daily_login row');
SELECT is((SELECT quantity::TEXT FROM public.chainbound_profile_item_stacks
            WHERE player_profile_id = '47000000-0000-4000-8000-000000000001' AND item_id = 'signal_orb'), '1',
          'concurrent claims grant one orb');
SELECT ok((SELECT balanced FROM public.ram_v2_audit_service('47000000-0000-4000-8000-000000000001')),
          'audit is balanced after concurrent claims');

SELECT * FROM finish();

SELECT dblink_disconnect('gift_gate');
SELECT dblink_disconnect('gift_a');
SELECT dblink_disconnect('gift_b');

DELETE FROM public.ram_ledger_operations WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.profile_daily_login_claims WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.profile_daily_login WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.chainbound_profile_item_mutations WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.chainbound_profile_item_stacks WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.profile_quest_states WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
DELETE FROM public.player_profiles WHERE player_profile_id = '47000000-0000-4000-8000-000000000001';
UPDATE public.battle_reward_settings SET cluster = (SELECT cluster FROM prior_cluster) WHERE singleton;
