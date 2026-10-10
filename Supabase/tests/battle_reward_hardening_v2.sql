BEGIN;

SELECT plan(26);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('32000000-0000-4000-8000-000000000001', 'test', 'test', 0),
  ('32000000-0000-4000-8000-000000000002', 'test', 'test', 0),
  ('32000000-0000-4000-8000-000000000003', 'test', 'test', 0),
  ('32000000-0000-4000-8000-000000000004', 'test', 'test', 0);

UPDATE public.player_profiles
   SET bound_wallet_address = '11111111111111111111111111111111'
 WHERE player_profile_id = '32000000-0000-4000-8000-000000000002';

UPDATE public.battle_reward_configs SET enabled = true
 WHERE config_version = 3
   AND config_hash = '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194';

INSERT INTO public.authoritative_pve_battles(
  battle_id, player_profile_id, start_command_id, status, battle_kind,
  state_version, seed, manifest_hash, config_hash, state_json, equipment_snapshot,
  expires_at, completed_at, encounter_id, reward_tier, reward_config_version,
  reward_config_hash, terminal_outcome, terminal_reason, terminal_at
) VALUES
 ('32000000-0000-4000-8000-000000000101','32000000-0000-4000-8000-000000000001','mode-off','completed','wild',1,1,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'mode_off','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000102','32000000-0000-4000-8000-000000000002','mode-shadow','completed','wild',1,2,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'mode_shadow','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000103','32000000-0000-4000-8000-000000000003','not-allowed','completed','wild',1,3,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'not_allowed','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000104','32000000-0000-4000-8000-000000000004','legacy','completed','wild',1,4,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'legacy',NULL,NULL,NULL,'victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000105','32000000-0000-4000-8000-000000000001','story-first','completed','boss',1,5,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'story_once','boss',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000106','32000000-0000-4000-8000-000000000001','story-replay','completed','boss',1,6,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'story_once','boss',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000107','32000000-0000-4000-8000-000000000002','defeat','lost','wild',1,7,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'defeat','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','defeat','Defeat',now()),
 ('32000000-0000-4000-8000-000000000108','32000000-0000-4000-8000-000000000002','frozen-earns','completed','wild',1,8,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'frozen_earns','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000109','32000000-0000-4000-8000-000000000003','not-terminal','active','wild',0,9,repeat('a',64),repeat('b',64),'{}','{}',now(),NULL,'not_terminal','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',NULL,NULL,NULL),
 ('32000000-0000-4000-8000-000000000110','32000000-0000-4000-8000-000000000003','classify-first','completed','wild',1,10,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'classify_first','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now()),
 ('32000000-0000-4000-8000-000000000111','32000000-0000-4000-8000-000000000002','draw','lost','wild',1,11,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'draw','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','draw','Draw',now()),
 ('32000000-0000-4000-8000-000000000112','32000000-0000-4000-8000-000000000002','flee','lost','wild',1,12,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'flee','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','flee','Fled',now()),
 ('32000000-0000-4000-8000-000000000113','32000000-0000-4000-8000-000000000002','forfeit','abandoned','wild',1,13,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'forfeit','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','forfeit','Forfeited',now()),
 ('32000000-0000-4000-8000-000000000114','32000000-0000-4000-8000-000000000002','abandon','abandoned','wild',1,14,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'abandon','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','abandon','Expired',now()),
 ('32000000-0000-4000-8000-000000000115','32000000-0000-4000-8000-000000000004','config-disabled','completed','wild',1,15,repeat('a',64),repeat('b',64),'{}','{}',now(),now(),'config_disabled','standard',3,'695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194','victory','Victory',now());

UPDATE public.battle_reward_settings SET mode='off', cluster='devnet', profile_allowlist='{}'::UUID[] WHERE singleton;
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000101')->>'reason','rewards_disabled','database mode off wins over callers');

UPDATE public.battle_reward_settings SET mode='shadow' WHERE singleton;
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000102')->>'reason','shadow_mode','database shadow writes truthful zero');

UPDATE public.battle_reward_settings SET mode='grant', profile_allowlist=ARRAY['32000000-0000-4000-8000-000000000001'::UUID] WHERE singleton;
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000103')->>'reason','not_allowlisted','direct settle cannot bypass DB allowlist');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000104')->>'reason','legacy_battle','legacy battle settles to terminal zero');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000105')->>'appliedRam','25','first narrative victory can earn');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000106')->>'reason','replay_not_eligible','narrative replay is zero');
SELECT is((SELECT count(*)::TEXT FROM public.battle_reward_settlements WHERE player_profile_id='32000000-0000-4000-8000-000000000001' AND encounter_id='story_once' AND applied_amount>0),'1','only one positive narrative receipt exists');

UPDATE public.battle_reward_settings SET profile_allowlist=ARRAY['32000000-0000-4000-8000-000000000001'::UUID,'32000000-0000-4000-8000-000000000002'::UUID] WHERE singleton;
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000107')->>'reason','defeat','defeat is an immutable zero receipt');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000111')->>'reason','draw','draw is an immutable zero receipt');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000112')->>'reason','flee','flee is an immutable zero receipt');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000113')->>'reason','forfeit','forfeit is an immutable zero receipt');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000114')->>'reason','abandon','abandon is an immutable zero receipt');

INSERT INTO public.ram_ledger_accounts(player_profile_id,wallet_address,cluster,status)
VALUES('32000000-0000-4000-8000-000000000002','11111111111111111111111111111111','devnet','frozen_reconciliation');
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000108')->>'appliedRam','5','legacy frozen account still earns');
SELECT throws_ok($$SELECT public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000109')$$,'battle_not_terminal','unstamped active battle is refused');

SELECT is(public.battle_reward_classify_service('32000000-0000-4000-8000-000000000110','plausibility_rejected')->>'alreadyApplied','false','first zero classification is not reported as replay');
SELECT is(public.battle_reward_classify_service('32000000-0000-4000-8000-000000000110','plausibility_rejected')->>'alreadyApplied','true','second zero classification is idempotent replay');

CREATE TEMP TABLE terminal_guard_results(name TEXT PRIMARY KEY, payload JSONB);
INSERT INTO terminal_guard_results VALUES
  ('wrong_owner', public.authoritative_commit_action_step_service(
      '32000000-0000-4000-8000-000000000109',
      '32000000-0000-4000-8000-000000000004',
      'wrong-owner-terminal', 0, 1, 'completed', '{}', '{}',
      (extract(epoch FROM now() + interval '5 minutes') * 1000)::BIGINT,
      'victory', 'Victory')),
  ('overwrite', public.authoritative_commit_action_step_service(
      '32000000-0000-4000-8000-000000000105',
      '32000000-0000-4000-8000-000000000001',
      'overwrite-terminal', 1, 2, 'lost', '{}', '{}',
      (extract(epoch FROM now() + interval '5 minutes') * 1000)::BIGINT,
      'defeat', 'Defeat'));
SELECT is((SELECT payload->>'code' FROM terminal_guard_results WHERE name='wrong_owner'),
          'battle_not_found', 'wrong owner cannot stamp another profile battle');
SELECT is((SELECT payload->>'code' FROM terminal_guard_results WHERE name='overwrite'),
          'battle_not_ready', 'completed battle refuses a second terminal transition');
SELECT is((SELECT terminal_outcome FROM public.authoritative_pve_battles
            WHERE battle_id='32000000-0000-4000-8000-000000000105'),
          'victory', 'terminal outcome remains immutable after overwrite attempt');

SELECT is((SELECT (balance_after = balance_before + applied_amount)::TEXT FROM public.battle_reward_settlements WHERE battle_id='32000000-0000-4000-8000-000000000105'),'true','receipt balance equation holds');
SELECT ok((SELECT receipt_sequence > 0 FROM public.battle_reward_settlements WHERE battle_id='32000000-0000-4000-8000-000000000105'),'receipt carries monotonic sequence');

SELECT is(public.ram_ledger_register_audit('32000000-0000-4000-8000-000000000002','11111111111111111111111111111111','devnet',999)->>'status','active','audit mismatch is diagnostic only');
SELECT is((SELECT status FROM public.ram_ledger_accounts WHERE player_profile_id='32000000-0000-4000-8000-000000000002'),'active','audit mismatch never freezes account');
SELECT is((SELECT ram_balance::TEXT FROM public.player_profiles WHERE player_profile_id='32000000-0000-4000-8000-000000000001'),'25','gated and replay receipts never add RAM');
SELECT is((SELECT count(*)::TEXT FROM public.ram_ledger_operations WHERE player_profile_id='32000000-0000-4000-8000-000000000001' AND operation_kind='battle_reward'),'1','one ledger credit backs narrative settlement');

UPDATE public.battle_reward_settings SET profile_allowlist=array_append(profile_allowlist, '32000000-0000-4000-8000-000000000004'::UUID) WHERE singleton;
UPDATE public.battle_reward_configs SET enabled=false WHERE config_version=3;
SELECT is(public.ram_battle_reward_settle_service('32000000-0000-4000-8000-000000000115')->>'reason','rewards_disabled','disabled reward config fails to zero receipt');

SELECT * FROM finish();
ROLLBACK;
