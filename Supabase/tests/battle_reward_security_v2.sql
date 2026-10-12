BEGIN;

SELECT plan(20);

SELECT ok(to_regprocedure('public.ram_battle_reward_settle_service(uuid)') IS NOT NULL, 'settlement RPC exists');
SELECT ok(to_regprocedure('public.battle_reward_classify_service(uuid,text)') IS NOT NULL, 'classification RPC exists');
SELECT ok(to_regprocedure('public.battle_reward_stamp_terminal_service(uuid,uuid,text,text)') IS NULL, 'standalone terminal stamp RPC was removed');
SELECT ok(to_regprocedure('public.battle_reward_get_service(uuid)') IS NOT NULL, 'status RPC exists');
SELECT ok(to_regprocedure('public.authoritative_settle_battle_service(uuid,uuid,bigint,integer,integer,text,boolean,text,text)') IS NOT NULL, 'atomic forfeit terminal wrapper exists');
SELECT ok(to_regprocedure('public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb,text,text)') IS NOT NULL, 'atomic capture terminal wrapper exists');

SELECT ok(NOT has_function_privilege('anon', 'public.ram_battle_reward_settle_service(uuid)', 'EXECUTE'), 'anon cannot settle');
SELECT ok(NOT has_function_privilege('authenticated', 'public.ram_battle_reward_settle_service(uuid)', 'EXECUTE'), 'authenticated cannot settle');
SELECT ok(NOT has_function_privilege('authenticated', 'public.battle_reward_classify_service(uuid,text)', 'EXECUTE'), 'authenticated cannot classify');
SELECT ok(NOT EXISTS (
    SELECT 1
      FROM pg_proc AS p
      CROSS JOIN LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) AS privilege
     WHERE p.oid = 'public.ram_battle_reward_settle_service(uuid)'::regprocedure
       AND privilege.grantee = 0
       AND privilege.privilege_type = 'EXECUTE'
), 'PUBLIC cannot settle');
SELECT ok(NOT EXISTS (
    SELECT 1
      FROM pg_proc AS p
      CROSS JOIN LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) AS privilege
     WHERE p.oid = 'public.battle_reward_classify_service(uuid,text)'::regprocedure
       AND privilege.grantee = 0
       AND privilege.privilege_type = 'EXECUTE'
), 'PUBLIC cannot classify');
SELECT ok(NOT has_function_privilege('anon', 'public.battle_reward_get_service(uuid)', 'EXECUTE'), 'anon cannot read receipt RPC');
SELECT ok(NOT has_function_privilege('authenticated', 'public.battle_reward_get_service(uuid)', 'EXECUTE'), 'authenticated cannot read receipt RPC');
SELECT ok(NOT has_function_privilege('authenticated', 'public.authoritative_settle_battle_service(uuid,uuid,bigint,integer,integer,text,boolean,text,text)', 'EXECUTE'), 'authenticated cannot finalize a terminal forfeit');
SELECT ok(NOT has_function_privilege('authenticated', 'public.authoritative_finalize_capture_attempt_service(uuid,uuid,text,integer,integer,jsonb,boolean,text,text,integer,jsonb,text,text)', 'EXECUTE'), 'authenticated cannot finalize a terminal capture');
SELECT ok(NOT has_table_privilege('authenticated', 'public.battle_reward_settlements', 'SELECT'), 'authenticated cannot read reward receipts directly');
SELECT ok(NOT has_table_privilege('authenticated', 'public.ram_daily_earnings', 'SELECT'), 'authenticated cannot read daily cap rows directly');
SELECT ok(NOT has_table_privilege('authenticated', 'public.battle_reward_settings', 'SELECT'), 'authenticated cannot read rollout settings');
SELECT ok(has_function_privilege('service_role', 'public.ram_battle_reward_settle_service(uuid)', 'EXECUTE'), 'service role can settle');
SELECT ok(has_function_privilege('service_role', 'public.battle_reward_get_service(uuid)', 'EXECUTE'), 'service role can read receipts');

SELECT * FROM finish();
ROLLBACK;
