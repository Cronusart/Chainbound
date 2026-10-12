BEGIN;

SELECT plan(25);

INSERT INTO public.player_profiles(player_profile_id, platform_origin, last_platform_seen, ram_balance)
VALUES
  ('37000000-0000-4000-8000-000000000001', 'test', 'test', 25),
  ('37000000-0000-4000-8000-000000000002', 'test', 'test', 0),
  ('37000000-0000-4000-8000-000000000003', 'test', 'test', 0),
  ('37000000-0000-4000-8000-000000000004', 'test', 'test', 0),
  ('37000000-0000-4000-8000-000000000005', 'test', 'test', 0);

INSERT INTO public.profile_identities(player_profile_id, provider_subject, auth_user_id)
VALUES
  ('37000000-0000-4000-8000-000000000002', 'phase17-auth-2', 'phase17-auth-2');

SELECT is(
  public.wallet_resolve_bound_profile_service('66666666666666666666666666666666')->>'status',
  'no_profile', 'fresh wallet resolves to no_profile'
);
SELECT is(
  public.wallet_bind_profile_service(
    '37000000-0000-4000-8000-000000000001',
    '77777777777777777777777777777777', 'phantom')->>'success',
  'true', 'guest profile binds a fresh wallet once'
);
SELECT is(
  public.wallet_resolve_bound_profile_service('77777777777777777777777777777777')->>'status',
  'profile_found', 'bound wallet resolves to profile_found'
);
SELECT is(
  public.wallet_resolve_bound_profile_service('77777777777777777777777777777777')->>'player_profile_id',
  '37000000-0000-4000-8000-000000000001', 'resolution returns the bound profile'
);

UPDATE public.profile_wallets SET is_active=false, unlinked_at=now()
 WHERE player_profile_id='37000000-0000-4000-8000-000000000001'
   AND wallet_public_key='77777777777777777777777777777777';
SELECT is(
  public.wallet_bind_profile_service(
    '37000000-0000-4000-8000-000000000001',
    '88888888888888888888888888888888', 'phantom')->>'error',
  'wallet_already_bound_to_profile', 'unlink does not permit a replacement wallet'
);
SELECT is(
  public.wallet_bind_profile_service(
    '37000000-0000-4000-8000-000000000001',
    '77777777777777777777777777777777', 'phantom')->>'success',
  'true', 'the same wallet can relink after sign-out'
);
SELECT ok(
  (SELECT is_active AND unlinked_at IS NULL FROM public.profile_wallets
    WHERE player_profile_id='37000000-0000-4000-8000-000000000001'
      AND wallet_public_key='77777777777777777777777777777777'),
  'relink reactivates the historical link'
);

SELECT is(
  public.ram_ledger_ensure_account(
    '37000000-0000-4000-8000-000000000001',
    '77777777777777777777777777777777', 'devnet')->>'wallet_address',
  '77777777777777777777777777777777', 'ledger account is created for the permanent wallet'
);
SELECT throws_ok(
  $$SELECT public.ram_ledger_ensure_account(
    '37000000-0000-4000-8000-000000000001',
    '88888888888888888888888888888888', 'devnet')$$,
  'ram_ledger_scope_mismatch', 'ensure_account rejects a different wallet'
);
SELECT is(
  (SELECT wallet_address FROM public.ram_ledger_accounts
    WHERE player_profile_id='37000000-0000-4000-8000-000000000001'),
  '77777777777777777777777777777777', 'ledger account is never repointed'
);
SELECT throws_ok(
  $$UPDATE public.player_profiles SET bound_wallet_address='88888888888888888888888888888888'
     WHERE player_profile_id='37000000-0000-4000-8000-000000000001'$$,
  'wallet_binding_is_permanent', 'database trigger makes binding permanent'
);

CREATE TEMP TABLE created_profile(payload JSONB);
INSERT INTO created_profile VALUES (public.wallet_create_bound_profile_service(
  'phase17-auth-2', '37000000-0000-4000-8000-000000000002',
  '99999999999999999999999999999999', 'seed_vault', 'editor'
));
SELECT is((SELECT payload->>'status' FROM created_profile), 'profile_created',
          'new-game create makes a fresh bound profile');
SELECT isnt((SELECT payload->>'player_profile_id' FROM created_profile),
             '37000000-0000-4000-8000-000000000002',
             'new-game does not reuse the menu guest profile');
SELECT is(
  (SELECT player_profile_id::TEXT FROM public.profile_identities WHERE auth_user_id='phase17-auth-2'),
  (SELECT payload->>'player_profile_id' FROM created_profile),
  'new-game atomically moves the auth identity to the new profile'
);
SELECT is(
  public.wallet_resolve_bound_profile_service('99999999999999999999999999999999')->>'player_profile_id',
  (SELECT payload->>'player_profile_id' FROM created_profile),
  'new-game wallet resolves to the newly created profile'
);
SELECT is(
  public.wallet_create_bound_profile_service(
    'phase17-auth-2', (SELECT (payload->>'player_profile_id')::UUID FROM created_profile),
    '99999999999999999999999999999999', 'seed_vault', 'editor')->>'error',
  'wallet_bound_to_other_profile', 'second new-game create for one wallet is rejected'
);
SELECT is(
  public.wallet_bind_profile_service(
    '37000000-0000-4000-8000-000000000003',
    '99999999999999999999999999999999', 'seed_vault')->>'error',
  'wallet_bound_to_other_profile', 'guest cannot bind a wallet owned elsewhere'
);

UPDATE public.battle_reward_configs SET enabled=true WHERE config_version=3;
CREATE TEMP TABLE none_begin(payload JSONB);
INSERT INTO none_begin VALUES (public.authoritative_begin_battle_service(
  '37000000-0000-4000-8000-000000000101',
  '37000000-0000-4000-8000-000000000004', 'phase17-none', 'boss', 1,
  repeat('a',64), repeat('b',64), 'narrative_replay', 'none', 3,
  '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
  '{}', '{}', (extract(epoch FROM now()+interval '5 minutes')*1000)::BIGINT
));
SELECT is((SELECT payload->>'ok' FROM none_begin), 'true', 'none-tier narrative battle starts');
SELECT is(
  (SELECT reward_tier || ':' || reward_config_version::TEXT FROM public.authoritative_pve_battles
    WHERE battle_id='37000000-0000-4000-8000-000000000101'),
  'none:0', 'none tier always stores config version zero'
);
SELECT is(
  (SELECT reward_config_hash FROM public.authoritative_pve_battles
    WHERE battle_id='37000000-0000-4000-8000-000000000101'),
  repeat('0',64), 'none tier always stores the zero hash'
);
SELECT is(
  public.authoritative_begin_battle_service(
    '37000000-0000-4000-8000-000000000101',
    '37000000-0000-4000-8000-000000000004', 'phase17-none', 'boss', 1,
    repeat('a',64), repeat('b',64), 'narrative_replay', 'none', 3,
    '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
    '{}', '{}', (extract(epoch FROM now()+interval '5 minutes')*1000)::BIGINT
  )->>'already_applied', 'true', 'same none-tier begin is idempotent'
);

UPDATE public.battle_reward_configs SET enabled=false WHERE config_version=3;
SELECT is(
  public.authoritative_begin_battle_service(
    '37000000-0000-4000-8000-000000000102',
    '37000000-0000-4000-8000-000000000005', 'phase17-disabled', 'wild', 2,
    repeat('a',64), repeat('b',64), 'disabled_config', 'standard', 3,
    '695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194',
    '{}', '{}', (extract(epoch FROM now()+interval '5 minutes')*1000)::BIGINT
  )->>'ok', 'true', 'battle starts while reward config is disabled'
);
UPDATE public.authoritative_pve_battles
   SET status='completed', terminal_outcome='victory', terminal_reason='Victory',
       terminal_at=now(), completed_at=now()
 WHERE battle_id='37000000-0000-4000-8000-000000000102';
UPDATE public.battle_reward_settings
   SET mode='grant', cluster='devnet',
       profile_allowlist=ARRAY['37000000-0000-4000-8000-000000000005'::UUID]
 WHERE singleton;
SELECT is(
  public.ram_battle_reward_settle_service('37000000-0000-4000-8000-000000000102')->>'reason',
  'rewards_disabled', 'disabled-at-start receipt reports rewards_disabled'
);

INSERT INTO public.ram_purchase_orders(
  order_id, player_profile_id, wallet_address, cluster, ram_amount, status
) VALUES (
  '37000000-0000-4000-8000-000000000201',
  '37000000-0000-4000-8000-000000000001',
  '77777777777777777777777777777777', 'devnet', 5, 'payment_verified'
);
CREATE TEMP TABLE shop_settle(payload JSONB);
INSERT INTO shop_settle VALUES (public.ram_shop_settle_purchase(
  '37000000-0000-4000-8000-000000000201', 'test-credit-signature', 'test-receipt'
));
SELECT ok((SELECT (payload->>'balance_version')::BIGINT > 0 FROM shop_settle),
          'shop settlement returns a balance version');
SELECT is(
  (SELECT balance_after::TEXT || ':' || balance_version::TEXT FROM public.ram_purchase_orders
    WHERE order_id='37000000-0000-4000-8000-000000000201'),
  (SELECT (payload->>'balance_after') || ':' || (payload->>'balance_version') FROM shop_settle),
  'shop stores and returns the same balance/version pair'
);

SELECT * FROM finish();
ROLLBACK;
