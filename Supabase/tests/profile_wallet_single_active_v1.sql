BEGIN;

SELECT plan(4);

INSERT INTO public.player_profiles (player_profile_id, platform_origin, last_platform_seen)
VALUES
    ('23000000-0000-0000-0000-000000000001', 'test', 'test'),
    ('23000000-0000-0000-0000-000000000002', 'test', 'test');

INSERT INTO public.profile_wallets (
    player_profile_id,
    wallet_provider,
    wallet_public_key,
    is_active
) VALUES (
    '23000000-0000-0000-0000-000000000001',
    'seed_vault',
    'WalletSingleActive111111111111111111111111',
    true
);

SELECT is(
    (SELECT count(*)::text FROM public.profile_wallets
     WHERE player_profile_id = '23000000-0000-0000-0000-000000000001'
       AND is_active = true AND unlinked_at IS NULL),
    '1',
    'a profile begins with one active wallet'
);

SELECT throws_ok(
    $$INSERT INTO public.profile_wallets (player_profile_id, wallet_provider, wallet_public_key, is_active)
      VALUES ('23000000-0000-0000-0000-000000000001', 'seed_vault',
              'WalletSingleActive222222222222222222222222', true)$$,
    '23505',
    NULL,
    'a profile cannot acquire a second active wallet'
);

SELECT throws_ok(
    $$INSERT INTO public.profile_wallets (player_profile_id, wallet_provider, wallet_public_key, is_active)
      VALUES ('23000000-0000-0000-0000-000000000002', 'seed_vault',
              'WalletSingleActive111111111111111111111111', true)$$,
    '23505',
    NULL,
    'an active wallet cannot belong to a second profile'
);

UPDATE public.profile_wallets
SET is_active = false, unlinked_at = now()
WHERE player_profile_id = '23000000-0000-0000-0000-000000000001';

INSERT INTO public.profile_wallets (
    player_profile_id,
    wallet_provider,
    wallet_public_key,
    is_active
) VALUES (
    '23000000-0000-0000-0000-000000000001',
    'seed_vault',
    'WalletReplacement333333333333333333333333',
    true
);

SELECT is(
    (SELECT wallet_public_key FROM public.profile_wallets
     WHERE player_profile_id = '23000000-0000-0000-0000-000000000001'
       AND is_active = true AND unlinked_at IS NULL),
    'WalletReplacement333333333333333333333333',
    'explicit unlinking permits a later replacement flow'
);

SELECT * FROM finish();
ROLLBACK;
